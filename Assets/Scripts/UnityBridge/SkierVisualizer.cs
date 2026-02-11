using UnityEngine;
using System.Collections.Generic;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Spawns and animates cosmetic skier dots to visualize visitor flow.
    /// These are purely visual - actual simulation runs at end-of-day.
    ///
    /// Movement math is delegated to <see cref="SkierMotionController"/>.
    /// This class owns: spawning, AI decisions, lifecycle, and the
    /// VisualSkier collection.
    /// </summary>
    public class SkierVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationRunner _simRunner;
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private TrailDrawer _trailDrawer;

        [Header("Visual Settings")]
        [SerializeField] private Color _skierColor = new Color(1f, 0.2f, 0.2f); // Red
        [SerializeField] private float _skierSize = 1.5f; // Bigger so we can see them
        [SerializeField] private int _maxActiveSkiers = 50;

        // NEW: Prefab-based visuals (assign SkierRoot.prefab here)
        [SerializeField] private GameObject _skierPrefab; // assign SkierRoot.prefab
        [SerializeField] private Transform _skierParent;  // optional (can be null)

        [Header("Spawn Settings")]
        [SerializeField] private Vector2 baseSpawnPosition = new Vector2(50, 50); // Default base position
        [SerializeField] private bool useSnapPoints = false; // Use snap point system or direct spawning

        [Header("Movement Settings")]
        [SerializeField] private float _liftSpeed = 2f; // tiles per second
        [SerializeField] private float _skiSpeed = 5f; // tiles per second
        [SerializeField] private float _spawnInterval = 2f; // seconds between spawns
        
        [Header("Lodge Settings")]
        [SerializeField] private float _lodgeCheckRadius = 30f; // How far skiers look for lodges
        [SerializeField] private float _lodgeVisitChance = 0.15f; // 15% chance to visit lodge after trail

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false; // Toggle console spam

        // Height offset to keep skis on top of snow (not buried)
        private const float SKI_HEIGHT_OFFSET = 2.0f;

        private List<VisualSkier> _activeSkiers = new List<VisualSkier>();
        private float _spawnTimer;
        private Material _skierMaterial;
        private bool _hasLoggedUpdate = false;

        // Skier Intelligence System
        private SkierAI _skierAI;
        private NetworkGraph _networkGraph;
        private SkierDistribution _distribution;
        
        // Downstream value cache (cleared when mountain topology or tuning changes)
        private Dictionary<(SkillLevel, int), float> _downstreamCache = new Dictionary<(SkillLevel, int), float>();
        
        // Tuning system: reads from SkierAITuning singleton, falls back to defaults
        private SkierAITuning Tuning => SkierAITuning.Instance;
        private int _lastTuningVersion = -1;

        /// <summary>
        /// Number of skiers currently active on the mountain
        /// </summary>
        public int ActiveSkierCount => _activeSkiers?.Count ?? 0;

        /// <summary>
        /// Invalidates all active skier goals, forcing them to re-plan on their next
        /// decision point. Call this when new trails or lifts are built so skiers
        /// discover and use the new infrastructure.
        /// </summary>
        public void InvalidateAllSkierGoals()
        {
            if (_activeSkiers == null) return;
            int count = 0;
            foreach (var vs in _activeSkiers)
            {
                if (vs.Skier.CurrentGoal != null)
                {
                    vs.Skier.CurrentGoal = null;
                    count++;
                }
            }
            _downstreamCache.Clear(); // Topology changed, clear cached downstream values
            if (_enableDebugLogs) Debug.Log($"[SkierVisualizer] Invalidated {count} skier goals (new infrastructure built)");
        }

        private void Awake()
        {
            // Create material for skier dots
            _skierMaterial = new Material(Shader.Find("Unlit/Color"));
            _skierMaterial.color = _skierColor;
            if (_enableDebugLogs) Debug.Log("[SkierVisualizer] Awake - initialized material");
        }

        private void Update()
        {
            if (!_hasLoggedUpdate)
            {
                if (_enableDebugLogs) Debug.Log("[SkierVisualizer] Update is being called");
                _hasLoggedUpdate = true;
            }

            // Check if all systems are ready
            if (_simRunner == null || _liftBuilder == null || _trailDrawer == null)
            {
                Debug.LogWarning("[SkierVisualizer] Missing references: SimRunner, LiftBuilder, or TrailDrawer");
                return;
            }

            if (_liftBuilder.LiftSystem == null || _trailDrawer.TrailSystem == null)
            {
                Debug.LogWarning("[SkierVisualizer] LiftSystem or TrailSystem not initialized");
                return;
            }

            if (_trailDrawer.GridRenderer == null || _trailDrawer.GridRenderer.TerrainData == null)
            {
                Debug.LogWarning("[SkierVisualizer] GridRenderer or TerrainData not initialized");
                return;
            }

            // Initialize SkierAI if needed (lazy initialization)
            if (_skierAI == null)
            {
                InitializeSkierAI();
            }
            
            // Sync tuning values when they change (runtime slider adjustments)
            SyncTuningIfChanged();

            // Spawn new skiers periodically
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= _spawnInterval && _activeSkiers.Count < _maxActiveSkiers)
            {
                _spawnTimer = 0f;
                TrySpawnSkier();
            }

            // Update all active skiers
            for (int i = _activeSkiers.Count - 1; i >= 0; i--)
            {
                var skier = _activeSkiers[i];
                UpdateSkier(skier, Time.deltaTime);

                // Remove if finished
                if (skier.IsFinished)
                {
                    // If skier was inside a lodge, free the slot
                    if (skier.TargetLodge != null)
                    {
                        skier.TargetLodge.ForceExitSkier(skier.Skier.SkierId);
                        skier.TargetLodge = null;
                    }
                    Destroy(skier.GameObject);
                    _activeSkiers.RemoveAt(i);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Initialization
        // ─────────────────────────────────────────────────────────────────

        private void InitializeSkierAI()
        {
            var terrainData = _trailDrawer.GridRenderer.TerrainData;
            var registry = _liftBuilder.Connectivity.Registry;
            var allTrails = _trailDrawer.TrailSystem.GetAllTrails();
            var allLifts = _liftBuilder.LiftSystem.GetAllLifts();

            // Create distribution (fixed 20/30/30/20 for now)
            _distribution = new SkierDistribution();

            // Create network graph
            _networkGraph = new NetworkGraph(registry, terrainData);
            _networkGraph.BuildGraph();

            // Create AI system
            _skierAI = new SkierAI(
                _networkGraph,
                registry,
                _distribution,
                allTrails,
                allLifts,
                new System.Random()
            );

            if (_enableDebugLogs) Debug.Log($"[SkierVisualizer] SkierAI initialized with {allTrails.Count} trails and {allLifts.Count} lifts");
            
            // Apply tuning immediately if available
            SyncTuningIfChanged();
        }
        
        /// <summary>
        /// Checks if SkierAITuning has been modified (slider change) and syncs
        /// all tunable parameters. Clears the downstream cache so new decisions
        /// use updated values immediately.
        /// </summary>
        private void SyncTuningIfChanged()
        {
            if (Tuning == null || _distribution == null) return;
            if (Tuning.Version == _lastTuningVersion) return;
            
            _lastTuningVersion = Tuning.Version;
            
            // Sync all preferences and tunable params to the distribution
            Tuning.ApplyToDistribution(_distribution);
            
            // Sync debug log setting
            _enableDebugLogs = Tuning.enableDebugLogs;
            
            // Sync network snap radius
            if (_networkGraph != null)
            {
                _networkGraph.SnapRadius3D = Tuning.networkSnapRadius;
            }
            
            // Sync SkierAI tunables
            if (_skierAI != null)
            {
                _skierAI.PreferredDifficultyBoost = Tuning.preferredDifficultyBoost;
            }
            
            // Clear downstream cache - all values are stale now
            _downstreamCache.Clear();
            
            if (_enableDebugLogs) Debug.Log($"[SkierAITuning] Synced tuning v{Tuning.Version} → distribution + cache cleared");
        }

        // ─────────────────────────────────────────────────────────────────
        //  Spawning
        // ─────────────────────────────────────────────────────────────────

        private void TrySpawnSkier()
        {
            var terrainData = _trailDrawer.GridRenderer.TerrainData;
            if (terrainData == null || terrainData.Grid == null)
                return;

            var grid = terrainData.Grid;

            // Get all lifts and trails directly from systems
            var allLifts = _liftBuilder.LiftSystem.GetAllLifts();
            var allTrails = _trailDrawer.TrailSystem.GetAllTrails();

            if (allLifts.Count == 0 || allTrails.Count == 0)
                return;

            // Create skier with random skill level
            var skillLevel = _distribution.GetRandomSkillLevel(new System.Random());
            var skier = new Skier(_activeSkiers.Count, skillLevel);

            // Use SkierAI to plan the skier's initial goal
            var goal = _skierAI.PlanNewGoal(skier);
            skier.CurrentGoal = goal;

            // Determine starting lift and trail from goal's planned path
            LiftData startLift = null;
            TrailData targetTrail = null;

            if (goal != null && goal.PlannedPath.Count > 0)
            {
                foreach (var step in goal.PlannedPath)
                {
                    if (step.StepType == PathStepType.RideLift && startLift == null)
                        startLift = allLifts.Find(l => l.LiftId == step.EntityId);
                    else if (step.StepType == PathStepType.SkiTrail && targetTrail == null)
                        targetTrail = allTrails.Find(t => t.TrailId == step.EntityId);

                    if (startLift != null && targetTrail != null) break;
                }

                if (_enableDebugLogs) Debug.Log($"[Skier {skier.SkierId}] AI planned {goal.PlannedPath.Count} steps (destination trail: {goal.DestinationTrailId})");
            }

            // Fallback: if AI couldn't plan, use legacy proximity-based selection
            if (startLift == null)
            {
                float closestDist = float.MaxValue;
                foreach (var lift in allLifts)
                {
                    Vector3f basePos3D = new Vector3f(baseSpawnPosition.x, 0, baseSpawnPosition.y);
                    float dist = Vector3f.Distance(basePos3D, lift.StartPosition);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        startLift = lift;
                    }
                }
            }

            if (startLift == null)
            {
                Debug.LogWarning("[SkierVisualizer] No lift found near base!");
                return;
            }

            // Fallback for trail if AI didn't provide one
            if (targetTrail == null)
            {
                var connectedTrailIds = _liftBuilder.Connectivity.Connections.GetTrailsFromLift(startLift.LiftId);
                if (connectedTrailIds.Count > 0)
                {
                    int trailId = connectedTrailIds[Random.Range(0, connectedTrailIds.Count)];
                    targetTrail = _trailDrawer.TrailSystem.GetTrail(trailId);
                }
                else
                {
                    Vector3 liftTopPos = new Vector3(startLift.EndPosition.X, startLift.EndPosition.Y, startLift.EndPosition.Z);
                    var nearbyTrails = FindNearbyTrailStarts(liftTopPos, 25f);
                    if (nearbyTrails.Count > 0)
                        targetTrail = nearbyTrails[Random.Range(0, nearbyTrails.Count)];
                }
            }

            if (targetTrail == null)
            {
                Debug.LogWarning($"[SkierVisualizer] Could not find valid trail for lift {startLift.LiftId}!");
                return;
            }

            // Create visual skier GameObject (PREFAB instead of sphere)
            if (_skierPrefab == null)
            {
                Debug.LogWarning("[SkierVisualizer] Skier Prefab is not assigned!");
                return;
            }

            var skierObj = Instantiate(_skierPrefab, _skierParent);
            skierObj.name = $"Skier_{skier.SkierId}_{skier.Skill}";

            // Apply scale multiplier
            skierObj.transform.localScale = skierObj.transform.localScale * _skierSize;

            // Set material tint with skill-based color
            var targetColor = GetSkillColor(skier.Skill);
            var renderers = skierObj.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mat = r.material;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", targetColor);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", targetColor);
            }

            // Remove colliders (keep "purely visual" behavior)
            var colliders = skierObj.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders) Destroy(c);

            // Find Animator component
            var animator = skierObj.GetComponentInChildren<Animator>(true);

            // Set initial position at base
            var baseCoord = new TileCoord((int)baseSpawnPosition.x, (int)baseSpawnPosition.y);
            var tile = grid.GetTile(baseCoord);
            float baseHeight = tile != null ? tile.Height : -35f;
            var startPos = new Vector3(baseSpawnPosition.x, baseHeight + SKI_HEIGHT_OFFSET, baseSpawnPosition.y);

            // Create motion controller
            var motion = new SkierMotionController(skier.SkierId, skierObj.transform, SKI_HEIGHT_OFFSET);
            motion.WalkSpeed = _skiSpeed * 0.6f; // walk a bit slower than ski
            motion.LiftSpeed = _liftSpeed;
            motion.BaseSkiSpeed = _skiSpeed;
            motion.Teleport(startPos);

            // Set initial walk target = lift bottom
            var liftBottomPos = new Vector3(
                startLift.StartPosition.X,
                startLift.StartPosition.Y + SKI_HEIGHT_OFFSET,
                startLift.StartPosition.Z
            );
            motion.SetWalkTarget(liftBottomPos);
            motion.SetLift(startLift);

            // Update skier state for proper pathfinding
            skier.CurrentState = SkierState.WalkingToLift;
            skier.CurrentLiftId = startLift.LiftId;
            
            // Log spawning info
            if (_enableDebugLogs) Debug.Log($"[Skier {skier.SkierId}] {skier.Skill} spawned → Lift {startLift.LiftId} → Trail {targetTrail.TrailId} ({targetTrail.Difficulty})");

            // Create visual skier data
            var visualSkier = new VisualSkier
            {
                GameObject = skierObj,
                Skier = skier,
                CurrentLift = startLift,
                CurrentTrail = targetTrail,
                PlannedTrails = new List<TrailData> { targetTrail },
                CurrentTrailIndex = 0,
                Phase = SkierPhase.WalkingToLift,
                IsFinished = false,
                HasSwitchedAtJunction = false,
                ReachableTrails = allTrails,
                UseGoalBasedAI = (goal != null && goal.PlannedPath.Count > 0),
                Animator = animator,
                Motion = motion
            };

            _activeSkiers.Add(visualSkier);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Per-frame update  (AI decisions + delegate movement)
        // ─────────────────────────────────────────────────────────────────

        private void UpdateSkier(VisualSkier vs, float deltaTime)
        {
            // ── Let the motion controller do position / rotation ────
            vs.Motion.Tick(deltaTime, (int)vs.Phase, vs.Animator);

            // ── React to motion-controller completion flags ─────────
            switch (vs.Phase)
            {
                case SkierPhase.WalkingToLift:
                    HandleWalkingToLift(vs);
                    break;

                case SkierPhase.RidingLift:
                    HandleRidingLift(vs);
                    break;

                case SkierPhase.SkiingTrail:
                    HandleSkiingTrail(vs, deltaTime);
                    break;
                    
                case SkierPhase.WalkingToLodge:
                    HandleWalkingToLodge(vs);
                    break;
                    
                case SkierPhase.InLodge:
                    HandleInLodge(vs);
                    break;
            }
        }

        // ── WalkingToLift ───────────────────────────────────────────────

        private void HandleWalkingToLift(VisualSkier vs)
        {
            if (vs.CurrentLift == null)
            {
                vs.IsFinished = true;
                return;
            }

            if (vs.Motion.ReachedLiftBottom)
            {
                // Track lift usage for mountain traversal variety
                vs.LiftsRidden.Add(vs.CurrentLift.LiftId);
                
                // Update skier state so PlanNewGoal knows where we are
                vs.Skier.CurrentState = SkierState.RidingLift;
                vs.Skier.CurrentLiftId = vs.CurrentLift.LiftId;
                
                // Advance goal past the RideLift step (critical fix: these were never consumed!)
                if (vs.Skier.CurrentGoal != null && !vs.Skier.CurrentGoal.IsComplete)
                {
                    var step = vs.Skier.CurrentGoal.GetCurrentStep();
                    if (step != null && step.StepType == PathStepType.RideLift && step.EntityId == vs.CurrentLift.LiftId)
                    {
                        vs.Skier.CurrentGoal.AdvanceToNextStep();
                        if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Goal advanced past RideLift({vs.CurrentLift.LiftId}) → next step ready");
                    }
                }
                
                // Transition to riding the lift
                vs.Phase = SkierPhase.RidingLift;
                vs.Motion.SetLift(vs.CurrentLift);
                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Boarding lift {vs.CurrentLift.LiftId}");
            }
        }

        // ── RidingLift ──────────────────────────────────────────────────

        private void HandleRidingLift(VisualSkier vs)
        {
            if (!vs.Motion.ReachedLiftTop) return;
            
            // Re-plan goal at lift top if enabled and goal is stale
            bool replanAtTop = Tuning?.replanAtLiftTop ?? true;
            if (replanAtTop && (vs.Skier.CurrentGoal == null || vs.Skier.CurrentGoal.IsComplete || vs.Skier.CurrentGoal.GetCurrentStep() == null))
            {
                var newGoal = _skierAI.PlanNewGoal(vs.Skier);
                vs.Skier.CurrentGoal = newGoal;
                if (_enableDebugLogs && newGoal != null)
                    Debug.Log($"[Skier {vs.Skier.SkierId}] Re-planned at lift top: {newGoal.PlannedPath.Count} steps → trail {newGoal.DestinationTrailId}");
            }

            // Reached top -- decide which trail to ski
            Vector3 liftTopPos = new Vector3(
                vs.CurrentLift.EndPosition.X,
                vs.CurrentLift.EndPosition.Y,
                vs.CurrentLift.EndPosition.Z
            );

            float trailSearchRadius = Tuning?.trailStartSearchRadius ?? 25f;
            var nearbyTrails = FindNearbyTrailStarts(liftTopPos, trailSearchRadius);
            TrailData chosenTrail = null;

            if (nearbyTrails.Count == 0)
            {
                // No trails at lift top - use connection graph as last resort
                var connectedTrailIds = _liftBuilder.Connectivity.Connections.GetTrailsFromLift(vs.CurrentLift.LiftId);
                if (connectedTrailIds.Count > 0)
                {
                    int trailId = connectedTrailIds[Random.Range(0, connectedTrailIds.Count)];
                    chosenTrail = _trailDrawer.TrailSystem.GetTrail(trailId);
                }
                if (chosenTrail == null)
                {
                    Debug.LogWarning($"[Skier {vs.Skier.SkierId}] No trails at lift {vs.CurrentLift.LiftId} top!");
                    ChooseNewDestination(vs);
                    return;
                }
            }
            else
            {
                // ── SCORING-FIRST TRAIL SELECTION ──────────────────────────
                // Score ALL nearby trails by ComputeTrailDecisionValue.
                // This naturally handles:
                //   - Beginners avoid dead-end greens (score ≈ 0.02)
                //   - Experts prefer greens leading to great terrain (score ≈ 0.60)
                //   - Everyone prefers trails with good downstream options
                //
                // Goal gets a small bonus (20%) as tiebreaker, NOT a veto.
                // Jerry (2%) picks randomly.
                
                float jerryChance = Tuning?.jerryChance ?? 0.02f;
                bool isJerry = Random.value < jerryChance;
                
                // Determine which trail the goal wants (if any)
                int goalTrailId = -1;
                if (vs.Skier.CurrentGoal != null && !vs.Skier.CurrentGoal.IsComplete)
                {
                    var step = vs.Skier.CurrentGoal.GetCurrentStep();
                    if (step != null && step.StepType == PathStepType.SkiTrail)
                    {
                        goalTrailId = step.EntityId;
                    }
                }

                // Score all trails
                var scoredTrails = new List<(TrailData trail, float score)>();
                float totalScore = 0f;
                
                foreach (var trail in nearbyTrails)
                {
                    float score;
                    if (isJerry)
                    {
                        score = 1f;
                    }
                    else
                    {
                        score = ComputeTrailDecisionValue(vs.Skier.Skill, trail);
                        float minScore = Tuning?.minimumTrailScore ?? 0.01f;
                        score = Mathf.Max(score, minScore);
                        
                        // Goal bonus: slight preference for the trail the goal planned
                        float goalBonus = Tuning?.goalTrailBonus ?? 1.2f;
                        if (trail.TrailId == goalTrailId)
                        {
                            score *= goalBonus;
                        }
                    }
                    
                    scoredTrails.Add((trail, score));
                    totalScore += score;
                }
                
                // Weighted random selection
                float roll = Random.value * totalScore;
                float cumulative = 0f;
                chosenTrail = scoredTrails[0].trail;
                
                foreach (var (trail, score) in scoredTrails)
                {
                    cumulative += score;
                    if (roll <= cumulative)
                    {
                        chosenTrail = trail;
                        break;
                    }
                }
                
                // Advance goal if we picked the goal's trail
                if (chosenTrail.TrailId == goalTrailId && vs.Skier.CurrentGoal != null)
                {
                    vs.Skier.CurrentGoal.AdvanceToNextStep();
                }
                else if (goalTrailId >= 0)
                {
                    // Deviated from goal - clear it so it gets re-planned
                    vs.Skier.CurrentGoal = null;
                }
                
                bool shouldLog = _enableDebugLogs || (Tuning != null && Tuning.logTrailScores);
                bool isTrackedSkier = Tuning != null && Tuning.debugSkierId >= 0 && vs.Skier.SkierId == Tuning.debugSkierId;
                
                if (shouldLog || isTrackedSkier)
                {
                    string scoreList = "";
                    foreach (var (t, s) in scoredTrails)
                    {
                        string marker = t.TrailId == chosenTrail.TrailId ? ">>>" : "   ";
                        string goalMarker = t.TrailId == goalTrailId ? " [GOAL]" : "";
                        scoreList += $"\n  {marker} Trail {t.TrailId} ({t.Difficulty}): {s:F3}{goalMarker}";
                    }
                    Debug.Log($"[Skier {vs.Skier.SkierId}] {vs.Skier.Skill} at Lift {vs.CurrentLift.LiftId} top - trail scores:{scoreList}{(isJerry ? "\n  JERRY MODE!" : "")}");
                }
            }

            vs.CurrentTrail = chosenTrail;
            vs.Phase = SkierPhase.SkiingTrail;
            vs.HasSwitchedAtJunction = false;
            vs.Skier.RunsCompleted++;
            
            // Update skier state
            vs.Skier.CurrentState = SkierState.SkiingTrail;
            vs.Skier.CurrentTrailId = chosenTrail.TrailId;

            vs.Motion.SetTrail(chosenTrail, 0f);
        }

        // ── SkiingTrail ─────────────────────────────────────────────────

        private void HandleSkiingTrail(VisualSkier vs, float deltaTime)
        {
            // ── Trail finished? ─────────────────────────────────────
            if (vs.Motion.ReachedTrailEnd)
            {
                OnTrailFinished(vs);
                return;
            }

            // ── Mid-trail junction handling ──────────────────────────
            if (!vs.HasSwitchedAtJunction)
            {
                TryJunctionSwitch(vs);
            }
        }

        /// <summary>
        /// Called when the motion controller signals the skier finished the current trail.
        /// Decide what to do next: board lift, continue to another trail, visit lodge, or return to base.
        /// 
        /// KEY DESIGN: After every run, skiers re-plan their goal. This enables mountain
        /// traversal - an expert who just finished a green connector will now plan to ride
        /// the next lift toward their dream double-black run, potentially several hops away.
        /// </summary>
        private void OnTrailFinished(VisualSkier vs)
        {
            Vector3 trailEndPos = vs.GameObject.transform.position;
            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Finished {vs.CurrentTrail.Difficulty} trail {vs.CurrentTrail.TrailId} at {trailEndPos}");

            // Update skier state so PlanNewGoal knows where we are (at end of this trail)
            vs.Skier.CurrentState = SkierState.SkiingTrail;
            vs.Skier.CurrentTrailId = vs.CurrentTrail.TrailId;

            // PRIORITY 1: Check for nearby lodges (random chance)
            if (Random.value < _lodgeVisitChance)
            {
                LodgeManager lodgeManager = LodgeManager.Instance;
                if (lodgeManager != null)
                {
                    var nearbyLodges = lodgeManager.FindLodgesInRadius(trailEndPos, _lodgeCheckRadius);
                    var availableLodges = nearbyLodges.FindAll(l => !l.IsFull);
                    
                    if (availableLodges.Count > 0)
                    {
                        LodgeFacility targetLodge = availableLodges[Random.Range(0, availableLodges.Count)];
                        vs.TargetLodge = targetLodge;
                        vs.Phase = SkierPhase.WalkingToLodge;
                        vs.Motion.SetWalkTarget(targetLodge.Position);
                        
                        if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Heading to lodge at {targetLodge.Position}");
                        return;
                    }
                }
            }

            // ── GOAL RE-PLANNING: The heart of mountain traversal ──────────
            // After every run, re-plan if the goal is stale/complete/null.
            // This makes skiers TRAVERSE the mountain: an expert finishing a green
            // connector will now plan "ride lift X → ski trail Y → ride lift Z →
            // ski that amazing double-black" instead of just picking the nearest lift.
            
            bool shouldReplan = Tuning?.replanAfterEveryRun ?? true;
            bool goalStale = vs.Skier.CurrentGoal == null || vs.Skier.CurrentGoal.IsComplete || vs.Skier.CurrentGoal.GetCurrentStep() == null;
            
            if (goalStale || shouldReplan)
            {
                if (!vs.Skier.WantsToKeepSkiing())
                {
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Done skiing (runs: {vs.Skier.RunsCompleted}/{vs.Skier.DesiredRuns})");
                    vs.IsFinished = true;
                    return;
                }
                
                var newGoal = _skierAI.PlanNewGoal(vs.Skier);
                vs.Skier.CurrentGoal = newGoal;
                vs.UseGoalBasedAI = (newGoal != null && newGoal.PlannedPath.Count > 0);
                if (_enableDebugLogs && newGoal != null) 
                    Debug.Log($"[Skier {vs.Skier.SkierId}] Re-planned goal after run: {newGoal.PlannedPath.Count} steps → destination trail {newGoal.DestinationTrailId}");
            }
            
            // PRIORITY 2: Follow goal's next lift step
            if (vs.Skier.CurrentGoal != null && !vs.Skier.CurrentGoal.IsComplete)
            {
                var step = vs.Skier.CurrentGoal.GetCurrentStep();
                if (step != null && step.StepType == PathStepType.RideLift)
                {
                    var allLifts = _liftBuilder.LiftSystem.GetAllLifts();
                    var goalLift = allLifts.Find(l => l.LiftId == step.EntityId);
                    if (goalLift != null)
                    {
                        var liftBottomPos = new Vector3(goalLift.StartPosition.X, goalLift.StartPosition.Y + SKI_HEIGHT_OFFSET, goalLift.StartPosition.Z);
                        float distToGoalLift = Vector3.Distance(trailEndPos, liftBottomPos);
                        
                        float goalWalkRadius = Tuning?.goalLiftWalkRadius ?? 40f;
                        if (distToGoalLift <= goalWalkRadius)
                        {
                            vs.CurrentLift = goalLift;
                            vs.Phase = SkierPhase.WalkingToLift;
                            vs.Motion.SetWalkTarget(liftBottomPos);
                            vs.Motion.SetLift(goalLift);
                            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Following goal → lift {goalLift.LiftId} (dist: {distToGoalLift:F0})");
                            return;
                        }
                        else
                        {
                            // Goal lift is too far - goal is stale, will fall through to scored selection
                            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Goal lift {goalLift.LiftId} too far ({distToGoalLift:F0}), using local selection");
                            vs.Skier.CurrentGoal = null;
                        }
                    }
                }
                // If goal step is SkiTrail, check for trail-to-trail connection
                else if (step != null && step.StepType == PathStepType.SkiTrail)
                {
                    var goalTrail = _trailDrawer.TrailSystem.GetTrail(step.EntityId);
                    if (goalTrail != null)
                    {
                        float distToTrail = FindDistanceToTrailStart(trailEndPos, goalTrail);
                        if (distToTrail <= 25f)
                        {
                            vs.CurrentTrail = goalTrail;
                            vs.Skier.CurrentTrailId = goalTrail.TrailId;
                            vs.Motion.SwitchTrail(goalTrail, trailEndPos);
                            vs.Skier.CurrentGoal.AdvanceToNextStep();
                            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Following goal → trail {goalTrail.TrailId}");
                            return;
                        }
                    }
                }
            }

            // PRIORITY 3: Scored lift selection (with deep downstream awareness + variety bonus)
            float liftSearchRadius = Tuning?.liftSearchRadius ?? 25f;
            float jerryChanceOTF = Tuning?.jerryChance ?? 0.02f;
            var nearbyLifts = FindNearbyLifts(trailEndPos, liftSearchRadius);
            bool isJerry = Random.value < jerryChanceOTF;
            var scoredLifts = new List<(LiftData lift, float score)>();

            foreach (var lift in nearbyLifts)
            {
                if (isJerry)
                {
                    scoredLifts.Add((lift, 1f));
                    continue;
                }

                // Score lift by deep downstream terrain value + variety bonus
                float score = ScoreLiftForSkier(vs.Skier.Skill, lift, vs.LiftsRidden);
                if (score > 0.01f)
                {
                    scoredLifts.Add((lift, score));
                }
            }

            // Desperation fallback: if no good lifts found, accept any lift with trails
            if (scoredLifts.Count == 0 && nearbyLifts.Count > 0)
            {
                foreach (var lift in nearbyLifts)
                {
                    var trailsAtTop = FindNearbyTrailStarts(
                        new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z), 30f);
                    if (trailsAtTop.Count > 0)
                    {
                        scoredLifts.Add((lift, 0.01f));
                    }
                }
                if (scoredLifts.Count > 0 && _enableDebugLogs)
                    Debug.Log($"[Skier {vs.Skier.SkierId}] Taking desperate lift choice (no preferred terrain reachable)");
            }

            if (scoredLifts.Count > 0)
            {
                // Weighted random selection: prefer lifts leading to better terrain
                float totalScore = 0f;
                foreach (var (l, s) in scoredLifts)
                    totalScore += s;

                float roll = Random.value * totalScore;
                float cumulative = 0f;
                vs.CurrentLift = scoredLifts[0].lift;
                foreach (var (l, s) in scoredLifts)
                {
                    cumulative += s;
                    if (roll <= cumulative)
                    {
                        vs.CurrentLift = l;
                        break;
                    }
                }

                vs.Phase = SkierPhase.WalkingToLift;

                var liftBottom = new Vector3(
                    vs.CurrentLift.StartPosition.X,
                    vs.CurrentLift.StartPosition.Y + SKI_HEIGHT_OFFSET,
                    vs.CurrentLift.StartPosition.Z
                );
                vs.Motion.SetWalkTarget(liftBottom);
                vs.Motion.SetLift(vs.CurrentLift);

                bool shouldLogLifts = _enableDebugLogs || (Tuning != null && Tuning.logLiftScores);
                bool isTracked = Tuning != null && Tuning.debugSkierId >= 0 && vs.Skier.SkierId == Tuning.debugSkierId;
                if (shouldLogLifts || isTracked)
                {
                    string liftList = "";
                    foreach (var (l, s) in scoredLifts)
                    {
                        string marker = l.LiftId == vs.CurrentLift.LiftId ? ">>>" : "   ";
                        liftList += $"\n  {marker} Lift {l.LiftId}: {s:F3}";
                    }
                    Debug.Log($"[Skier {vs.Skier.SkierId}] {vs.Skier.Skill} at trail end - lift scores:{liftList}{(isJerry ? "\n  JERRY MODE!" : "")}");
                }
                return;
            }

            // PRIORITY 4: Trail-to-trail connections
            var allConnections = _liftBuilder.Connectivity.Connections.GetAllConnections();
            var nextTrailIds = new List<int>();
            foreach (var conn in allConnections)
            {
                if (conn.FromType == "Trail" && conn.FromId == vs.CurrentTrail.TrailId && conn.ToType == "Trail")
                    nextTrailIds.Add(conn.ToId);
            }

            if (nextTrailIds.Count > 0)
            {
                int nextTrailId = nextTrailIds[Random.Range(0, nextTrailIds.Count)];
                var nextTrail = _trailDrawer.TrailSystem.GetTrail(nextTrailId);
                if (nextTrail != null)
                {
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Trail junction → trail {nextTrailId}");
                    vs.CurrentTrail = nextTrail;
                    vs.Skier.CurrentTrailId = nextTrail.TrailId;
                    vs.Motion.SwitchTrail(nextTrail, trailEndPos);
                    return;
                }
            }

            // PRIORITY 5: Nearby trail starts spatially
            var nearbyTrails = FindNearbyTrailStarts(trailEndPos, 25f);
            var validTrails = nearbyTrails.FindAll(t => t.TrailId != vs.CurrentTrail.TrailId);
            if (validTrails.Count > 0)
            {
                var chosenTrail = ChooseTrailByPreference(vs.Skier, validTrails, true);
                vs.CurrentTrail = chosenTrail;
                vs.Skier.CurrentTrailId = chosenTrail.TrailId;
                vs.Motion.SwitchTrail(chosenTrail, trailEndPos);
                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Continuing to trail {chosenTrail.TrailId}");
                return;
            }

            // PRIORITY 6: Near base?
            var baseSpawns = _liftBuilder.Connectivity.Registry.GetByType(SnapPointType.BaseSpawn);
            if (baseSpawns.Count > 0)
            {
                Vector3 basePos = new Vector3(baseSpawns[0].Position.X, baseSpawns[0].Position.Y, baseSpawns[0].Position.Z);
                if (Vector3.Distance(trailEndPos, basePos) <= 50f)
                {
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Reached base! Run #{vs.Skier.RunsCompleted}");
                    ChooseNewDestination(vs);
                    return;
                }
            }

            // LAST RESORT: stranded
            Debug.LogWarning($"[Skier {vs.Skier.SkierId}] Stranded at {trailEndPos}! Teleporting to base...");
            ChooseNewDestination(vs);
        }

        /// <summary>
        /// While skiing, check if the skier should switch to a different trail
        /// (goal-based or exploration).
        /// </summary>
        private void TryJunctionSwitch(VisualSkier vs)
        {
            bool switched = false;
            float progress = vs.Motion.TrailProgress;

            // Goal-based trail switch
            if (vs.UseGoalBasedAI && vs.Skier.CurrentGoal != null)
            {
                var goal = vs.Skier.CurrentGoal;
                var currentStep = goal.GetCurrentStep();
                if (currentStep != null && currentStep.StepType == PathStepType.SkiTrail)
                {
                    if (goal.CurrentPathIndex < goal.PlannedPath.Count - 1)
                    {
                        var nextStep = goal.PlannedPath[goal.CurrentPathIndex + 1];
                        if (nextStep.StepType == PathStepType.SkiTrail && nextStep.EntityId != vs.CurrentTrail.TrailId)
                        {
                            Vector3 currentPos = vs.GameObject.transform.position;
                            var nextTrail = _trailDrawer.TrailSystem.GetTrail(nextStep.EntityId);
                            if (nextTrail != null)
                            {
                                float distToNext = FindDistanceToTrailStart(currentPos, nextTrail);
                                if (distToNext < 20f)
                                {
                                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Following goal: Trail {vs.CurrentTrail.TrailId} → Trail {nextTrail.TrailId}");
                                    vs.CurrentTrail = nextTrail;
                                    vs.Motion.SwitchTrail(nextTrail, currentPos);
                                    vs.HasSwitchedAtJunction = true;
                                    goal.AdvanceToNextStep();
                                    switched = true;
                                }
                            }
                        }
                    }
                }
            }

            // ── Downstream-aware junction switching ──
            // Skiers seek out junctions leading to better terrain, but with moderation.
            // Uses dead-end-aware values (ComputeTrailDecisionValue) so beginners won't
            // switch to a green that leads to a double-black-only area.
            // Probabilities are balanced to ensure all lifts/trails get some traffic.
            if (!switched && progress > 0.1f && progress < 0.9f)
            {
                if (Mathf.Abs(progress % 0.05f) < 0.015f)
                {
                    float junctionRadius = Tuning?.junctionDetectionRadius ?? 15f;
                    Vector3 currentPos = vs.GameObject.transform.position;
                    var nearbyTrails = FindNearbyTrailSegments(currentPos, junctionRadius);
                    var validTrails = nearbyTrails.FindAll(t => t.TrailId != vs.CurrentTrail.TrailId);

                    if (validTrails.Count > 0)
                    {
                        float currentValue = ComputeTrailDecisionValue(vs.Skier.Skill, vs.CurrentTrail);

                        TrailData bestJunction = null;
                        float bestJunctionValue = 0f;

                        foreach (var t in validTrails)
                        {
                            float value = ComputeTrailDecisionValue(vs.Skier.Skill, t);
                            if (value > bestJunctionValue)
                            {
                                bestJunctionValue = value;
                                bestJunction = t;
                            }
                        }

                        if (bestJunction != null)
                        {
                            float improvement = bestJunctionValue - currentValue;
                            float switchChance = 0f;
                            
                            float majorThresh = Tuning?.junctionMajorThreshold ?? 0.25f;
                            float moderateThresh = Tuning?.junctionModerateThreshold ?? 0.1f;
                            float majorChance = Tuning?.junctionMajorSwitchChance ?? 0.50f;
                            float moderateChance = Tuning?.junctionModerateSwitchChance ?? 0.25f;
                            float exploreChance = Tuning?.junctionExplorationChance ?? 0.12f;
                            float exploreMin = Tuning?.junctionExplorationMinValue ?? 0.2f;

                            if (improvement > majorThresh)
                            {
                                switchChance = majorChance;
                            }
                            else if (improvement > moderateThresh)
                            {
                                switchChance = moderateChance;
                            }
                            else if (bestJunctionValue >= exploreMin)
                            {
                                switchChance = exploreChance;
                            }

                            if (switchChance > 0f && Random.value < switchChance)
                            {
                                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Junction switch: Trail {vs.CurrentTrail.TrailId} → Trail {bestJunction.TrailId} (value: {bestJunctionValue:F2} vs current: {currentValue:F2})");
                                vs.CurrentTrail = bestJunction;
                                vs.Motion.SwitchTrail(bestJunction, currentPos);
                                vs.HasSwitchedAtJunction = true;
                            }
                        }
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Choose new destination (end-of-run AI)
        // ─────────────────────────────────────────────────────────────────

        private void ChooseNewDestination(VisualSkier vs)
        {
            if (!vs.Skier.WantsToKeepSkiing())
            {
                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Done skiing (runs: {vs.Skier.RunsCompleted}/{vs.Skier.DesiredRuns})");
                vs.IsFinished = true;
                return;
            }

            // Reset state to AtBase for proper pathfinding start point
            vs.Skier.CurrentState = SkierState.AtBase;
            
            var goal = _skierAI.PlanNewGoal(vs.Skier);
            vs.Skier.CurrentGoal = goal;

            var allLifts = _liftBuilder.LiftSystem.GetAllLifts();
            var allTrails = _trailDrawer.TrailSystem.GetAllTrails();

            LiftData nextLift = null;
            TrailData nextTrail = null;

            // Extract path from goal
            if (goal != null && goal.PlannedPath.Count > 0)
            {
                foreach (var step in goal.PlannedPath)
                {
                    if (step.StepType == PathStepType.RideLift && nextLift == null)
                        nextLift = allLifts.Find(l => l.LiftId == step.EntityId);
                    else if (step.StepType == PathStepType.SkiTrail && nextTrail == null)
                        nextTrail = allTrails.Find(t => t.TrailId == step.EntityId);
                    if (nextLift != null && nextTrail != null) break;
                }
                vs.UseGoalBasedAI = true;
            }

            // Fallback to legacy
            if (nextLift == null)
            {
                var baseSpawn = _liftBuilder.Connectivity.Registry.GetByType(SnapPointType.BaseSpawn);
                Vector3f basePos = baseSpawn.Count > 0 ? baseSpawn[0].Position : new Vector3f(224f, -35f, 205f);

                float closestDist = float.MaxValue;
                foreach (var lift in allLifts)
                {
                    float dist = Vector3f.Distance(basePos, lift.StartPosition);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        nextLift = lift;
                    }
                }
                vs.UseGoalBasedAI = false;
            }

            if (nextLift == null) { vs.IsFinished = true; return; }

            if (nextTrail == null)
            {
                var connectedTrailIds = _liftBuilder.Connectivity.Connections.GetTrailsFromLift(nextLift.LiftId);
                if (connectedTrailIds.Count > 0)
                {
                    int trailId = connectedTrailIds[Random.Range(0, connectedTrailIds.Count)];
                    nextTrail = _trailDrawer.TrailSystem.GetTrail(trailId);
                }
            }

            if (nextTrail == null)
            {
                Debug.LogWarning($"[Skier {vs.Skier.SkierId}] Lift {nextLift.LiftId} has no connected trails!");
                vs.IsFinished = true;
                return;
            }

            vs.CurrentLift = nextLift;
            vs.CurrentTrail = nextTrail;
            vs.PlannedTrails = new List<TrailData> { nextTrail };
            vs.CurrentTrailIndex = 0;
            vs.Phase = SkierPhase.WalkingToLift;
            vs.HasSwitchedAtJunction = false;
            
            // Update skier state
            vs.Skier.CurrentState = SkierState.WalkingToLift;
            vs.Skier.CurrentLiftId = nextLift.LiftId;

            // Tell motion controller where to walk
            var liftBottom = new Vector3(
                nextLift.StartPosition.X,
                nextLift.StartPosition.Y + SKI_HEIGHT_OFFSET,
                nextLift.StartPosition.Z
            );
            vs.Motion.SetWalkTarget(liftBottom);
            vs.Motion.SetLift(nextLift);

            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] New destination: Lift {nextLift.LiftId} → Trail {nextTrail.TrailId} ({nextTrail.Difficulty})");
        }

        // ── WalkingToLodge ──────────────────────────────────────────────
        
        private void HandleWalkingToLodge(VisualSkier vs)
        {
            if (vs.TargetLodge == null)
            {
                // Lodge was destroyed, choose new destination
                ChooseNewDestination(vs);
                return;
            }
            
            // Check if reached lodge
            float distanceToLodge = Vector3.Distance(vs.GameObject.transform.position, vs.TargetLodge.Position);
            if (distanceToLodge <= 3f) // Within 3m
            {
                // Try to enter lodge
                if (vs.TargetLodge.TryEnterLodge(vs.Skier.SkierId))
                {
                    // Success! Hide skier and start resting
                    vs.Phase = SkierPhase.InLodge;
                    vs.GameObject.SetActive(false); // Hide while inside
                    vs.LodgeRestTimer = 0f; // Timer tracked by lodge facility
                    
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Entered lodge!");
                }
                else
                {
                    // Lodge is full, go somewhere else
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Lodge full, choosing new destination");
                    ChooseNewDestination(vs);
                }
            }
        }
        
        // ── InLodge ─────────────────────────────────────────────────────
        
        private void HandleInLodge(VisualSkier vs)
        {
            if (vs.TargetLodge == null)
            {
                // Lodge was destroyed, exit immediately
                vs.GameObject.SetActive(true);
                ChooseNewDestination(vs);
                return;
            }
            
            // Check if rest time is complete (lodge handles timing)
            if (!vs.TargetLodge.ContainsSkier(vs.Skier.SkierId))
            {
                // Rest complete! Respawn and continue skiing
                vs.GameObject.SetActive(true);
                
                // Position at lodge exit
                vs.GameObject.transform.position = vs.TargetLodge.Position;
                vs.Motion.Teleport(vs.TargetLodge.Position);
                
                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Left lodge, choosing new destination");
                
                // Clear lodge reference
                vs.TargetLodge = null;
                
                // Choose new destination
                ChooseNewDestination(vs);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Spatial queries  (FIXED: no more "check all points" bug)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Find trails whose START point (WorldPathPoints[0]) is within radius.
        /// Used when exiting a lift or finishing a trail -- you always enter
        /// a trail at its start (top).
        /// This replaces the old FindNearbyTrails which checked every point
        /// on every trail, causing skiers to teleport to trail tops.
        /// </summary>
        private List<TrailData> FindNearbyTrailStarts(Vector3 position, float radius)
        {
            var result = new List<TrailData>();
            var allTrails = _trailDrawer.TrailSystem.GetAllTrails();

            foreach (var trail in allTrails)
            {
                if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count == 0)
                    continue;

                var trailStart = trail.WorldPathPoints[0];
                Vector3 startPos = new Vector3(trailStart.X, trailStart.Y, trailStart.Z);
                if (Vector3.Distance(position, startPos) <= radius)
                    result.Add(trail);
            }
            return result;
        }

        /// <summary>
        /// Find trails that have a segment passing near the given position.
        /// Used for mid-trail junction detection (skier is skiing and a
        /// different trail's path runs alongside).
        /// Only considers segments that the skier could continue downhill on.
        /// </summary>
        private List<TrailData> FindNearbyTrailSegments(Vector3 position, float radius)
        {
            var result = new List<TrailData>();
            var allTrails = _trailDrawer.TrailSystem.GetAllTrails();
            float currentY = position.y;

            foreach (var trail in allTrails)
            {
                if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                    continue;

                // Check each segment
                for (int i = 0; i < trail.WorldPathPoints.Count - 1; i++)
                {
                    var p1 = trail.WorldPathPoints[i];
                    var p2 = trail.WorldPathPoints[i + 1];
                    Vector3 a = new Vector3(p1.X, p1.Y, p1.Z);
                    Vector3 b = new Vector3(p2.X, p2.Y, p2.Z);

                    // Only consider segments that go downhill from roughly our elevation
                    // (prevents matching segments far above/below us)
                    float segMidY = (a.y + b.y) * 0.5f;
                    if (Mathf.Abs(segMidY - currentY) > 10f) continue;

                    // Closest point on segment
                    Vector3 closest = ClosestPointOnLineSegment(position, a, b);
                    if (Vector3.Distance(position, closest) <= radius)
                    {
                        result.Add(trail);
                        break; // one match per trail is enough
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Find lifts within a specified radius of a position (for flexible boarding).
        /// </summary>
        private List<LiftData> FindNearbyLifts(Vector3 position, float radius)
        {
            var nearbyLifts = new List<LiftData>();
            var allLifts = _liftBuilder.LiftSystem.GetAllLifts();

            foreach (var lift in allLifts)
            {
                Vector3 liftBottom = new Vector3(lift.StartPosition.X, lift.StartPosition.Y, lift.StartPosition.Z);
                if (Vector3.Distance(position, liftBottom) <= radius)
                    nearbyLifts.Add(lift);
            }
            return nearbyLifts;
        }

        /// <summary>
        /// Distance from a position to the start of a trail (for junction detection).
        /// </summary>
        private float FindDistanceToTrailStart(Vector3 position, TrailData trail)
        {
            if (trail.WorldPathPoints != null && trail.WorldPathPoints.Count > 0)
            {
                var start = trail.WorldPathPoints[0];
                return Vector3.Distance(position, new Vector3(start.X, start.Y, start.Z));
            }
            return float.MaxValue;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Downstream awareness helpers (look-ahead terrain evaluation)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the best preference value of terrain reachable from a trail's end,
        /// looking up to 3 hops deep through the mountain network.
        /// Results are cached per (skill, trailId) and invalidated when topology changes.
        /// 
        /// This multi-hop evaluation is CRITICAL for two behaviors:
        /// 1. BEGINNERS: A green that leads to a lift with only blacks → downstream=0 →
        ///    dead-end penalty. But if that lift also has a green that leads to another
        ///    lift with greens, downstream > 0, so it's safe. The old 1-hop version
        ///    couldn't see this far.
        /// 2. EXPERTS: A green that leads (2 hops) to amazing double-black terrain
        ///    gets a high downstream value, making experts willing to ski it as transit.
        /// </summary>
        private float ComputeTrailDownstreamValue(SkillLevel skill, TrailData trail)
        {
            var key = (skill, trail.TrailId);
            if (_downstreamCache.TryGetValue(key, out float cached))
                return cached;
            
            int depth = Tuning?.downstreamDepth ?? 3;
            var visitedTrails = new HashSet<int>();
            var visitedLifts = new HashSet<int>();
            float value = ComputeDeepDownstreamValue(skill, trail, depth, visitedTrails, visitedLifts);
            _downstreamCache[key] = value;
            return value;
        }

        /// <summary>
        /// Recursive multi-hop downstream evaluation with DEPTH DISCOUNTING.
        /// 
        /// Key insight: terrain 1 hop away is worth MUCH more than terrain 3 hops away.
        /// An expert at base lift sees:
        ///   - Green-to-dangerous: blacks are 1 hop away → value = 0.58 * 1.0 = 0.58
        ///   - Blue-to-base: blacks are 3 hops away → value = 0.58 * 0.4 = 0.23
        /// This makes the green-to-dangerous clearly superior for experts.
        /// 
        /// Depth discount: hop 1 = 100%, hop 2 = 65%, hop 3 = 40%
        /// </summary>
        private float ComputeDeepDownstreamValue(SkillLevel skill, TrailData trail, int maxDepth, 
            HashSet<int> visitedTrails, HashSet<int> visitedLifts)
        {
            int totalDepth = Tuning?.downstreamDepth ?? 3;
            
            if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                return 0f;
            if (maxDepth <= 0)
                return 0f;
            if (visitedTrails.Contains(trail.TrailId))
                return 0f;
            
            visitedTrails.Add(trail.TrailId);
            
            // Depth discount from tuning (or hardcoded fallback)
            float depthDiscount = Tuning != null
                ? Tuning.GetDepthDiscount(maxDepth, totalDepth)
                : (maxDepth == totalDepth ? 1.0f : maxDepth == totalDepth - 1 ? 0.65f : 0.40f);

            var trailEnd = trail.WorldPathPoints[trail.WorldPathPoints.Count - 1];
            Vector3 endPos = new Vector3(trailEnd.X, trailEnd.Y, trailEnd.Z);
            
            float searchRadius = Tuning?.liftSearchRadius ?? 25f;
            float trailSearchRadius = Tuning?.trailStartSearchRadius ?? 25f;

            float bestPref = 0f;

            // Check lifts at trail end → trails at those lift tops → recurse deeper
            var liftsAtEnd = FindNearbyLifts(endPos, searchRadius);
            foreach (var lift in liftsAtEnd)
            {
                if (visitedLifts.Contains(lift.LiftId)) continue;
                visitedLifts.Add(lift.LiftId);
                
                Vector3 liftTopPos = new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z);
                var trailsAtTop = FindNearbyTrailStarts(liftTopPos, trailSearchRadius);
                
                foreach (var t in trailsAtTop)
                {
                    if (visitedTrails.Contains(t.TrailId)) continue;
                    if (!_distribution.IsAllowed(skill, t.Difficulty)) continue;
                    
                    float pref = _distribution.GetPreference(skill, t.Difficulty) * depthDiscount;
                    bestPref = Mathf.Max(bestPref, pref);
                    
                    if (maxDepth > 1)
                    {
                        float deeper = ComputeDeepDownstreamValue(skill, t, maxDepth - 1, visitedTrails, visitedLifts);
                        bestPref = Mathf.Max(bestPref, deeper);
                    }
                }
            }

            // Also check trail-to-trail connections
            var nextTrails = FindNearbyTrailStarts(endPos, trailSearchRadius);
            foreach (var t in nextTrails)
            {
                if (t.TrailId == trail.TrailId) continue;
                if (visitedTrails.Contains(t.TrailId)) continue;
                if (!_distribution.IsAllowed(skill, t.Difficulty)) continue;
                
                float pref = _distribution.GetPreference(skill, t.Difficulty) * depthDiscount;
                bestPref = Mathf.Max(bestPref, pref);
                
                if (maxDepth > 1)
                {
                    float deeper = ComputeDeepDownstreamValue(skill, t, maxDepth - 1, visitedTrails, visitedLifts);
                    bestPref = Mathf.Max(bestPref, deeper);
                }
            }

            return bestPref;
        }

        /// <summary>
        /// Computes a trail's decision value for a skier, combining direct appeal
        /// with what terrain is reachable beyond it. Trails that dead-end (no reachable
        /// terrain the skier can ski after) are heavily penalized. This prevents
        /// beginners eagerly taking a green that leads to a double-black-only lift.
        /// </summary>
        /// <summary>
        /// Computes a trail's total decision value: direct appeal + downstream terrain value.
        /// Uses ADDITIVE scoring so a green trail leading directly to amazing blacks
        /// scores much higher than a green trail leading nowhere special.
        /// 
        /// Example for an Expert at base lift:
        ///   Green-to-dangerous (1 hop to doubles): 0.02 + 0.58 = 0.60
        ///   Blue-to-base (3 hops to doubles): 0.10 + 0.23 = 0.33
        ///   Green-to-base (3 hops to doubles): 0.02 + 0.23 = 0.25
        ///   → Expert clearly prefers green-to-dangerous!
        ///
        ///   Beginner at base lift:
        ///   Green-to-base (safe): 0.75 + 0.75*depth_discounted = ~1.0
        ///   Green-to-dangerous (DEAD END): 0.02
        ///   → Beginner clearly avoids the death trap!
        /// </summary>
        private float ComputeTrailDecisionValue(SkillLevel skill, TrailData trail)
        {
            if (!_distribution.IsAllowed(skill, trail.Difficulty)) return 0f;
            if (_distribution.IsDesperateOnly(skill, trail.Difficulty)) return 0.01f;

            float directWeight = Tuning?.directPreferenceWeight ?? 1.0f;
            float dsWeight = Tuning?.downstreamWeight ?? 1.0f;
            float deadEnd = Tuning?.deadEndScore ?? 0.02f;
            
            float directPref = _distribution.GetPreference(skill, trail.Difficulty);
            float downstream = ComputeTrailDownstreamValue(skill, trail);

            if (downstream > 0.01f)
            {
                // Weighted formula: direct * weight + downstream * weight
                return directPref * directWeight + downstream * dsWeight;
            }
            else
            {
                return deadEnd;
            }
        }

        /// <summary>
        /// Scores how attractive a lift is for a skier, considering:
        /// 1. Trails at the top and their deep downstream value
        /// 2. Variety bonus for lifts the skier hasn't ridden (encourages mountain traversal)
        /// 3. Dead-end penalty for lifts with no good options for this skill level
        /// </summary>
        private float ScoreLiftForSkier(SkillLevel skill, LiftData lift, HashSet<int> liftsRidden = null)
        {
            float bestScore = 0f;
            float searchRadius = Tuning?.trailStartSearchRadius ?? 30f;

            Vector3 liftTopPos = new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z);
            var trailsAtTop = FindNearbyTrailStarts(liftTopPos, searchRadius);

            foreach (var trail in trailsAtTop)
            {
                float trailValue = ComputeTrailDecisionValue(skill, trail);
                bestScore = Mathf.Max(bestScore, trailValue);
            }

            // Variety bonus: unridden lifts are more attractive (encourages mountain exploration)
            float newBonus = Tuning?.liftVarietyNewBonus ?? 1.4f;
            float repeatPenalty = Tuning?.liftVarietyRepeatPenalty ?? 0.85f;
            
            if (liftsRidden != null && bestScore > 0.05f)
            {
                if (!liftsRidden.Contains(lift.LiftId))
                {
                    bestScore *= newBonus;
                }
                else
                {
                    bestScore *= repeatPenalty;
                }
            }
            
            if (Tuning != null && Tuning.logLiftScores)
            {
                Debug.Log($"[LiftScore] Lift {lift.LiftId}: score={bestScore:F3} for {skill}");
            }

            return bestScore;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Trail preference / color helpers
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Chooses a trail from a list using weighted random selection.
        /// When considerDownstream is true, factors in what terrain is reachable
        /// beyond each trail (transit awareness), so skiers will take connector
        /// trails that lead to terrain they prefer.
        /// </summary>
        private TrailData ChooseTrailByPreference(Skier skier, List<TrailData> availableTrails, bool considerDownstream = false)
        {
            if (availableTrails.Count == 0) return null;
            if (availableTrails.Count == 1) return availableTrails[0];

            float totalWeight = 0f;
            var weights = new List<float>();

            foreach (var trail in availableTrails)
            {
                float weight;

                if (considerDownstream)
                {
                    float downstream = ComputeTrailDownstreamValue(skier.Skill, trail);
                    weight = _distribution.GetEffectiveWeight(skier.Skill, trail.Difficulty, downstream);
                    weight = Mathf.Max(weight, 0.05f);
                }
                else
                {
                    float pref = _distribution.GetPreference(skier.Skill, trail.Difficulty);
                    weight = Mathf.Max(pref, 0.05f);
                }

                weights.Add(weight);
                totalWeight += weight;
            }

            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < availableTrails.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative) return availableTrails[i];
            }
            return availableTrails[availableTrails.Count - 1];
        }

        private Color GetSkillColor(SkillLevel skill)
        {
            switch (skill)
            {
                case SkillLevel.Beginner:    return new Color(0.1f, 1f, 0.1f);
                case SkillLevel.Intermediate: return new Color(0.2f, 0.5f, 1f);
                case SkillLevel.Advanced:     return new Color(0.1f, 0.1f, 0.1f);
                case SkillLevel.Expert:       return new Color(1f, 0.2f, 0.2f);
                default:                      return Color.white;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Geometry helper
        // ─────────────────────────────────────────────────────────────────

        private static Vector3 ClosestPointOnLineSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.magnitude;
            if (len < 0.001f) return a;
            Vector3 dir = ab / len;
            float proj = Mathf.Clamp(Vector3.Dot(point - a, dir), 0f, len);
            return a + dir * proj;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Types
        // ─────────────────────────────────────────────────────────────────

        private enum SkierPhase
        {
            WalkingToLift = 0,
            RidingLift = 1,
            SkiingTrail = 2,
            WalkingToLodge = 3,
            InLodge = 4
        }

        private class VisualSkier
        {
            public GameObject GameObject;
            public Skier Skier;
            public LiftData CurrentLift;
            public TrailData CurrentTrail;
            public List<TrailData> PlannedTrails;
            public int CurrentTrailIndex;
            public SkierPhase Phase;
            public bool IsFinished;

            // Junction tracking
            public bool HasSwitchedAtJunction;

            // Mountain traversal: track which lifts this skier has ridden
            public HashSet<int> LiftsRidden = new HashSet<int>();

            // Pathfinding references (for replanning)
            public List<TrailData> ReachableTrails;

            // Goal-based AI
            public bool UseGoalBasedAI;

            // Animation
            public Animator Animator;

            // Motion controller (owns all position / rotation math)
            public SkierMotionController Motion;
            
            // Lodge tracking
            public LodgeFacility TargetLodge;
            public float LodgeRestTimer;
        }
    }
}
