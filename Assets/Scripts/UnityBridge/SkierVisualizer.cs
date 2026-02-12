using UnityEngine;
using System.Collections.Generic;
using SkiResortTycoon.Core;
using SkiResortTycoon.Core.SatisfactionFactors;
using SkiResortTycoon.ScriptableObjects;

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
        [SerializeField] private float _lodgeVisitChance = 0.25f; // 25% chance to visit lodge after trail

        [Header("AI Config")]
        [SerializeField] private SkierAIConfig _aiConfig; // Assign a SkierAIConfig ScriptableObject asset
        
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
        
        // Satisfaction update timer (update resort satisfaction every 2 seconds)
        private float _satisfactionUpdateTimer = 0f;
        private const float SATISFACTION_UPDATE_INTERVAL = 2f;
        
        // Downstream value cache (cleared when mountain topology changes)
        private Dictionary<(SkillLevel, int), float> _downstreamCache = new Dictionary<(SkillLevel, int), float>();
        
        // Shorthand for config and traffic manager
        private SkierAIConfig Config => _aiConfig;
        private ResortTrafficManager Traffic => ResortTrafficManager.Instance;

        /// <summary>
        /// Number of skiers currently active on the mountain
        /// </summary>
        public int ActiveSkierCount => _activeSkiers?.Count ?? 0;

        /// <summary>
        /// Updates resort-level satisfaction from the average of all active skiers.
        /// Called periodically from the update loop.
        /// </summary>
        private void UpdateResortSatisfaction()
        {
            if (_activeSkiers.Count == 0 || _simRunner?.Sim?.Satisfaction == null)
                return;
            
            // Collect all active Skier objects
            var skiers = new List<Skier>(_activeSkiers.Count);
            foreach (var vs in _activeSkiers)
            {
                if (vs.Skier != null)
                    skiers.Add(vs.Skier);
            }
            
            _simRunner.Sim.Satisfaction.UpdateFromActiveSkiers(skiers);
        }

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
            
            // Reinitialize traffic manager with new topology
            if (Traffic != null && _trailDrawer != null && _liftBuilder != null)
            {
                var allTrails = _trailDrawer.TrailSystem.GetAllTrails();
                var allLifts = _liftBuilder.LiftSystem.GetAllLifts();
                Traffic.Reinitialize(allTrails, allLifts);
            }
            
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
            
            // Sync config values to distribution
            SyncConfigIfNeeded();

            // Get effective delta time (respects pause and game speed)
            float effectiveDeltaTime = Time.deltaTime;
            float speedMultiplier = 1f;
            bool isPaused = false;
            
            if (_simRunner.Sim != null && _simRunner.Sim.TimeController != null)
            {
                effectiveDeltaTime = _simRunner.Sim.TimeController.GetEffectiveDeltaTime(Time.deltaTime);
                speedMultiplier = _simRunner.Sim.TimeController.SpeedMultiplier;
                isPaused = _simRunner.Sim.TimeController.IsPaused;
            }

            // Spawn new skiers periodically
            _spawnTimer += effectiveDeltaTime;
            if (_spawnTimer >= _spawnInterval && _activeSkiers.Count < _maxActiveSkiers)
            {
                _spawnTimer = 0f;
                TrySpawnSkier();
            }

            // Convert effective delta time to game minutes for needs updates
            float effectiveGameMinutes = 0f;
            if (_simRunner.Sim != null && _simRunner.Sim.TimeSystem != null)
            {
                effectiveGameMinutes = effectiveDeltaTime * _simRunner.Sim.TimeSystem.SpeedMinutesPerSecond;
            }

            // Cache base position for returning-to-base proximity checks
            Vector3 cachedBasePos = Vector3.zero;
            bool hasBase = false;
            {
                var baseSpawns = _liftBuilder.Connectivity.Registry.GetByType(SnapPointType.BaseSpawn);
                if (baseSpawns.Count > 0)
                {
                    cachedBasePos = new Vector3(baseSpawns[0].Position.X, baseSpawns[0].Position.Y, baseSpawns[0].Position.Z);
                    hasBase = true;
                }
            }

            // Update all active skiers
            for (int i = _activeSkiers.Count - 1; i >= 0; i--)
            {
                var skier = _activeSkiers[i];
                
                // Control animator speed based on game speed and pause state
                if (skier.Animator != null)
                {
                    skier.Animator.speed = isPaused ? 0f : speedMultiplier;
                }
                
                // Update skier needs over time (hunger, bladder increase)
                if (effectiveGameMinutes > 0f)
                {
                    skier.Skier.Needs.UpdateNeeds(effectiveGameMinutes);
                    
                    // Track walking distance during walk phases
                    if (skier.Phase == SkierPhase.WalkingToLift || skier.Phase == SkierPhase.WalkingToLodge)
                    {
                        float walkDistance = skier.Motion.WalkSpeed * effectiveDeltaTime;
                        skier.Skier.Needs.AddWalkingDistance(walkDistance);
                    }
                }
                
                UpdateSkier(skier, effectiveDeltaTime);

                // Safety: if skier is in lodge phase but the lodge lost track of them,
                // force them out to prevent invisible stuck skiers
                if (skier.Phase == SkierPhase.InLodge && skier.TargetLodge != null 
                    && !skier.TargetLodge.ContainsSkier(skier.Skier.SkierId))
                {
                    skier.GameObject.SetActive(true);
                    skier.GameObject.transform.position = skier.TargetLodge.Position;
                    skier.Motion.Teleport(skier.TargetLodge.Position);
                    skier.TargetLodge = null;
                    ChooseNewDestination(skier);
                }

                // Continuous base proximity check: returning skiers despawn when near base (any phase)
                if (skier.IsReturningToBase && hasBase && skier.Phase != SkierPhase.InLodge)
                {
                    float distToBase = Vector3.Distance(skier.GameObject.transform.position, cachedBasePos);
                    if (distToBase <= 80f) // Generous radius — if they're in the base area, let them leave
                    {
                        if (_enableDebugLogs) Debug.Log($"[Skier {skier.Skier.SkierId}] Near base lodge ({distToBase:F0}m), leaving resort!");
                        skier.IsFinished = true;
                    }
                }

                // Safety timeout: if returning-to-base skier can't reach base in 1 in-game day (480 game minutes), force despawn
                if (skier.IsReturningToBase && effectiveGameMinutes > 0f)
                {
                    skier.ReturningToBaseTimer += effectiveGameMinutes; // Track in game minutes
                    if (skier.ReturningToBaseTimer > 480f) // 1 in-game day = 480 game minutes
                    {
                        if (_enableDebugLogs) Debug.Log($"[Skier {skier.Skier.SkierId}] Returning-to-base timeout (1 day) — force despawn");
                        skier.IsFinished = true;
                    }
                }

                // Remove if finished
                if (skier.IsFinished)
                {
                    // If skier was inside a lodge, free the slot and make visible before destroying
                    if (skier.TargetLodge != null)
                    {
                        skier.TargetLodge.ForceExitSkier(skier.Skier.SkierId);
                        skier.TargetLodge = null;
                    }
                    skier.GameObject.SetActive(true); // Ensure visible before destroy
                    Destroy(skier.GameObject);
                    _activeSkiers.RemoveAt(i);
                }
            }
            
            // Periodically update resort satisfaction from active skiers
            _satisfactionUpdateTimer += effectiveDeltaTime;
            if (_satisfactionUpdateTimer >= SATISFACTION_UPDATE_INTERVAL && _simRunner.Sim != null)
            {
                _satisfactionUpdateTimer = 0f;
                UpdateResortSatisfaction();
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
            
            // Auto-create traffic manager if none exists in scene
            if (ResortTrafficManager.Instance == null)
            {
                var go = new GameObject("ResortTrafficManager (Auto)");
                go.AddComponent<ResortTrafficManager>();
                Debug.Log("[SkierVisualizer] Auto-created ResortTrafficManager (add one to your scene to avoid this)");
            }
            
            // Auto-create default config if none assigned
            if (_aiConfig == null)
            {
                _aiConfig = ScriptableObject.CreateInstance<SkierAIConfig>();
                Debug.Log("[SkierVisualizer] No SkierAIConfig assigned — using default config. " +
                    "Create one via Assets > Create > Ski Resort Tycoon > Skier AI Config");
            }
            
            // Initialize traffic manager with current trails/lifts
            if (Traffic != null && _aiConfig != null)
            {
                Traffic.Initialize(allTrails, allLifts, _aiConfig);
            }
            
            // Apply config immediately
            SyncConfigIfNeeded();
        }
        
        /// <summary>
        /// Syncs SkierAIConfig values to the distribution and subsystems.
        /// Called every frame (cheap: only writes when config is present).
        /// </summary>
        private void SyncConfigIfNeeded()
        {
            if (Config == null || _distribution == null) return;
            
            Config.ApplyToDistribution(_distribution);
            
            _enableDebugLogs = Config.enableDebugLogs;
            
            if (_networkGraph != null)
                _networkGraph.SnapRadius3D = Config.networkSnapRadius;
            
            if (_skierAI != null)
                _skierAI.PreferredDifficultyBoost = Config.preferredDifficultyBoost;
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
            
            // Register satisfaction factors
            skier.SatisfactionTracker.AddFactor(new NeedsFulfillmentFactor());
            skier.SatisfactionTracker.AddFactor(new LodgePricingFactor());
            skier.SatisfactionTracker.AddFactor(new TraversalFrictionFactor());
            skier.SatisfactionTracker.AddFactor(new ReturnToBaseFactor());
            
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
                Motion = motion,
                PersonalityOffsets = SkierContext.GeneratePersonality(skier.SkierId)
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
                
                // Fire traffic event: skier entered lift
                if (Traffic != null) Traffic.FireLiftEntered(vs.Skier.SkierId, vs.CurrentLift.LiftId);
                
                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Boarding lift {vs.CurrentLift.LiftId}");
            }
        }

        // ── RidingLift ──────────────────────────────────────────────────

        private void HandleRidingLift(VisualSkier vs)
        {
            if (!vs.Motion.ReachedLiftTop) return;
            
            // Fire traffic event: skier exited lift at top
            if (Traffic != null) Traffic.FireLiftExited(vs.Skier.SkierId, vs.CurrentLift.LiftId);
            
            // Re-plan goal at lift top if enabled and goal is stale (skip if returning to base)
            bool replanAtTop = Config != null ? Config.replanAtLiftTop : true;
            if (!vs.IsReturningToBase && replanAtTop && (vs.Skier.CurrentGoal == null || vs.Skier.CurrentGoal.IsComplete || vs.Skier.CurrentGoal.GetCurrentStep() == null))
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

            // ── MERGE spatial + connection graph: find ALL reachable trails ──
            float trailSearchRadius = Config != null ? Config.trailStartSearchRadius : 50f;
            var candidateTrails = FindNearbyTrailStarts(liftTopPos, trailSearchRadius);
            
            // Also include trails from the connection graph (may be outside spatial radius)
            var graphTrailIds = _liftBuilder.Connectivity.Connections.GetTrailsFromLift(vs.CurrentLift.LiftId);
            var candidateIdSet = new HashSet<int>();
            foreach (var t in candidateTrails) candidateIdSet.Add(t.TrailId);
            foreach (var tid in graphTrailIds)
            {
                if (!candidateIdSet.Contains(tid))
                {
                    var trail = _trailDrawer.TrailSystem.GetTrail(tid);
                    if (trail != null && trail.IsValid)
                    {
                        candidateTrails.Add(trail);
                        candidateIdSet.Add(tid);
                    }
                }
            }
            
            TrailData chosenTrail = null;

            if (candidateTrails.Count == 0)
            {
                Debug.LogWarning($"[Skier {vs.Skier.SkierId}] No trails at lift {vs.CurrentLift.LiftId} top!");
                ChooseNewDestination(vs);
                return;
            }
            
            // Returning skiers strongly prefer trails that end near the base
            if (vs.IsReturningToBase)
            {
                var baseTrails = candidateTrails.FindAll(t => _liftBuilder.Connectivity.Connections.IsTrailConnectedToBase(t.TrailId));
                if (baseTrails.Count > 0)
                {
                    chosenTrail = baseTrails[Random.Range(0, baseTrails.Count)];
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] [returning] Chose base-bound trail {chosenTrail.TrailId}");
                }
            }
            
            if (chosenTrail == null)
            {
                // ── UNIFIED DECISION ENGINE: Trail selection via softmax ──
                var ctx = BuildContext(vs);
                
                chosenTrail = SkierDecisionEngine.ChooseTrail(
                    candidateTrails,
                    ctx,
                    Config,
                    Traffic?.State,
                    _distribution,
                    ComputeTrailDownstreamValue
                );
                
                if (chosenTrail == null)
                    chosenTrail = candidateTrails[Random.Range(0, candidateTrails.Count)];
            }
            
            // IMMEDIATELY record intent so the next skier deciding this frame sees updated state
            if (Traffic != null) Traffic.FireTrailIntended(vs.Skier.SkierId, chosenTrail.TrailId);
            
            // Advance goal if we picked the goal's trail
            if (!vs.IsReturningToBase && vs.Skier.CurrentGoal != null)
            {
                var goalCtx = BuildContext(vs);
                int goalTrailId = goalCtx.GoalTrailId;
                if (chosenTrail.TrailId == goalTrailId)
                {
                    vs.Skier.CurrentGoal.AdvanceToNextStep();
                }
                else if (goalTrailId >= 0)
                {
                    vs.Skier.CurrentGoal = null;
                }
            }

            vs.CurrentTrail = chosenTrail;
            vs.TrailsSkied.Add(chosenTrail.TrailId);
            vs.Phase = SkierPhase.SkiingTrail;
            vs.HasSwitchedAtJunction = false;
            vs.EvaluatedLiftExits.Clear();
            vs.EvaluatedTrailExits.Clear();
            vs.Skier.RunsCompleted++;
            
            // Update skier state
            vs.Skier.CurrentState = SkierState.SkiingTrail;
            vs.Skier.CurrentTrailId = chosenTrail.TrailId;
            
            // Fire traffic event: skier entered trail
            if (Traffic != null) Traffic.FireTrailEntered(vs.Skier.SkierId, chosenTrail.TrailId);

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

            // ── Mid-trail exit detection ─────────────────────────────
            // While skiing, check if we're passing a LIFT BOTTOM or a TRAIL START.
            // If so, offer the skier the choice to exit (using the decision engine).
            // This is NOT the old TryJunctionSwitch which detected random trail segments.
            // This only triggers at structurally meaningful exit points.
            TryMidTrailExits(vs);
        }
        
        /// <summary>
        /// While skiing a trail, checks if the skier is near a lift bottom or another
        /// trail's START point. If so, evaluates whether to exit the current trail
        /// using the same decision engine that handles lift-top decisions.
        /// 
        /// Each potential exit is only evaluated ONCE per trail run (tracked by ID).
        /// Uses the decision engine with softmax for natural distribution.
        /// </summary>
        private void TryMidTrailExits(VisualSkier vs)
        {
            Vector3 pos = vs.GameObject.transform.position;
            float exitRadius = Config != null ? Config.junctionDetectionRadius : 15f;
            
            // ── Check for pending lodge visit — divert if close ──
            if (vs.PendingLodgeVisit != null)
            {
                float distToLodge = Vector3.Distance(pos, vs.PendingLodgeVisit.Position);
                if (distToLodge <= 30f)
                {
                    // Close enough to divert! Fire trail completed and walk the short distance
                    if (Traffic != null) Traffic.FireTrailCompleted(vs.Skier.SkierId, vs.CurrentTrail.TrailId);
                    
                    vs.TargetLodge = vs.PendingLodgeVisit;
                    vs.PendingLodgeVisit = null;
                    vs.Phase = SkierPhase.WalkingToLodge;
                    vs.Motion.SetWalkTarget(vs.TargetLodge.Position);
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Passing lodge — diverting! ({distToLodge:F0}m)");
                    return;
                }
            }
            
            // ── Check for nearby LIFT BOTTOMS ──
            var allLifts = _liftBuilder.LiftSystem.GetAllLifts();
            foreach (var lift in allLifts)
            {
                if (!lift.IsValid) continue;
                if (vs.EvaluatedLiftExits.Contains(lift.LiftId)) continue;
                
                Vector3 liftBottom = new Vector3(lift.StartPosition.X, lift.StartPosition.Y, lift.StartPosition.Z);
                float dist = Vector3.Distance(pos, liftBottom);
                
                if (dist <= exitRadius)
                {
                    // Mark as evaluated so we don't re-check every frame
                    vs.EvaluatedLiftExits.Add(lift.LiftId);
                    
                    // Should we exit and take this lift? Compare "continue on trail" vs "take lift".
                    // Score the current trail continuation vs this lift opportunity.
                    float currentTrailValue = ComputeTrailDecisionValue(vs.Skier.Skill, vs.CurrentTrail);
                    float liftValue = GetBestTrailValueAtLift(vs.Skier.Skill, lift);
                    
                    // Add deficit bonus for the lift (under-used lifts are more attractive)
                    float deficitBonus = 0f;
                    if (Traffic?.State != null)
                    {
                        deficitBonus = Traffic.State.GetLiftDeficit(lift.LiftId) * 
                            (Config != null ? Config.deficitBonusStrength : 2.5f);
                    }
                    liftValue += deficitBonus;
                    
                    // Add novelty bonus if skier hasn't ridden this lift
                    if (!vs.LiftsRidden.Contains(lift.LiftId))
                    {
                        liftValue += Config != null ? Config.noveltyBonusStrength : 0.5f;
                    }
                    
                    // Softmax between "continue" and "exit to lift"
                    float temperature = Config != null ? Config.softmaxTemperature : 1.5f;
                    var options = new List<(string item, float score)>
                    {
                        ("continue", currentTrailValue),
                        ("exit", liftValue)
                    };
                    int choice = SkierDecisionEngine.SoftmaxSelect(options, temperature);
                    
                    if (choice == 1) // chose to exit
                    {
                        if (_enableDebugLogs)
                            Debug.Log($"[Skier {vs.Skier.SkierId}] Mid-trail exit → Lift {lift.LiftId} " +
                                $"(trail={currentTrailValue:F2} vs lift={liftValue:F2})");
                        
                        // Fire trail completed event
                        if (Traffic != null) Traffic.FireTrailCompleted(vs.Skier.SkierId, vs.CurrentTrail.TrailId);
                        if (Traffic != null) Traffic.FireLiftIntended(vs.Skier.SkierId, lift.LiftId);
                        
                        vs.CurrentLift = lift;
                        vs.Phase = SkierPhase.WalkingToLift;
                        vs.Motion.SetWalkTarget(new Vector3(
                            lift.StartPosition.X,
                            lift.StartPosition.Y + SKI_HEIGHT_OFFSET,
                            lift.StartPosition.Z
                        ));
                        vs.Motion.SetLift(lift);
                        return;
                    }
                }
            }
            
            // ── Check for nearby TRAIL STARTS ──
            var allTrails = _trailDrawer.TrailSystem.GetAllTrails();
            foreach (var trail in allTrails)
            {
                if (!trail.IsValid) continue;
                if (trail.TrailId == vs.CurrentTrail.TrailId) continue;
                if (vs.EvaluatedTrailExits.Contains(trail.TrailId)) continue;
                if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count == 0) continue;
                
                var trailStart = trail.WorldPathPoints[0];
                Vector3 startPos = new Vector3(trailStart.X, trailStart.Y, trailStart.Z);
                float dist = Vector3.Distance(pos, startPos);
                
                if (dist <= exitRadius)
                {
                    vs.EvaluatedTrailExits.Add(trail.TrailId);
                    
                    // Skill check: don't offer trails the skier can't handle
                    if (!_distribution.IsAllowed(vs.Skier.Skill, trail.Difficulty)) continue;
                    
                    // Score current trail vs the branching trail using the decision engine
                    var ctx = BuildContext(vs);
                    var candidates = new List<TrailData> { vs.CurrentTrail, trail };
                    
                    var chosenTrail = SkierDecisionEngine.ChooseTrail(
                        candidates, ctx, Config, Traffic?.State, _distribution, ComputeTrailDownstreamValue
                    );
                    
                    if (chosenTrail != null && chosenTrail.TrailId != vs.CurrentTrail.TrailId)
                    {
                        if (_enableDebugLogs)
                            Debug.Log($"[Skier {vs.Skier.SkierId}] Mid-trail switch → Trail {trail.TrailId} " +
                                $"({trail.Difficulty}) from Trail {vs.CurrentTrail.TrailId}");
                        
                        // Fire events
                        if (Traffic != null) Traffic.FireTrailCompleted(vs.Skier.SkierId, vs.CurrentTrail.TrailId);
                        if (Traffic != null) Traffic.FireTrailIntended(vs.Skier.SkierId, trail.TrailId);
                        
                        vs.CurrentTrail = trail;
                        vs.TrailsSkied.Add(trail.TrailId);
                        vs.Skier.CurrentTrailId = trail.TrailId;
                        vs.EvaluatedLiftExits.Clear();
                        vs.EvaluatedTrailExits.Clear();
                        
                        // Start the new trail from the beginning
                        vs.Motion.SetTrail(trail, 0f);
                        
                        if (Traffic != null) Traffic.FireTrailEntered(vs.Skier.SkierId, trail.TrailId);
                        return;
                    }
                }
            }
            
            // ── Check for TRAIL CROSSINGS (mid-trail intersections) ──
            // If another trail's path physically passes through our position,
            // offer the skier the choice to switch onto it at the crossing point.
            foreach (var trail in allTrails)
            {
                if (!trail.IsValid) continue;
                if (trail.TrailId == vs.CurrentTrail.TrailId) continue;
                if (vs.EvaluatedTrailExits.Contains(trail.TrailId)) continue;
                if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2) continue;
                
                // Check trail segments for proximity
                bool foundCrossing = false;
                for (int i = 0; i < trail.WorldPathPoints.Count - 1; i++)
                {
                    var p1 = trail.WorldPathPoints[i];
                    var p2 = trail.WorldPathPoints[i + 1];
                    Vector3 a = new Vector3(p1.X, p1.Y, p1.Z);
                    Vector3 b = new Vector3(p2.X, p2.Y, p2.Z);
                    
                    // Ignore segments at very different elevations
                    float segMidY = (a.y + b.y) * 0.5f;
                    if (Mathf.Abs(segMidY - pos.y) > 8f) continue;
                    
                    Vector3 closest = ClosestPointOnLineSegment(pos, a, b);
                    if (Vector3.Distance(pos, closest) <= exitRadius)
                    {
                        foundCrossing = true;
                        break;
                    }
                }
                
                if (!foundCrossing) continue;
                
                vs.EvaluatedTrailExits.Add(trail.TrailId);
                
                // Skill check
                if (!_distribution.IsAllowed(vs.Skier.Skill, trail.Difficulty)) continue;
                
                // Use the decision engine: current trail vs crossing trail
                var crossCtx = BuildContext(vs);
                var crossCandidates = new List<TrailData> { vs.CurrentTrail, trail };
                
                var crossChosen = SkierDecisionEngine.ChooseTrail(
                    crossCandidates, crossCtx, Config, Traffic?.State, _distribution, ComputeTrailDownstreamValue
                );
                
                if (crossChosen != null && crossChosen.TrailId != vs.CurrentTrail.TrailId)
                {
                    if (_enableDebugLogs)
                        Debug.Log($"[Skier {vs.Skier.SkierId}] Trail crossing switch → Trail {trail.TrailId} " +
                            $"({trail.Difficulty}) from Trail {vs.CurrentTrail.TrailId}");
                    
                    // Fire events
                    if (Traffic != null) Traffic.FireTrailCompleted(vs.Skier.SkierId, vs.CurrentTrail.TrailId);
                    if (Traffic != null) Traffic.FireTrailIntended(vs.Skier.SkierId, trail.TrailId);
                    
                    vs.CurrentTrail = trail;
                    vs.TrailsSkied.Add(trail.TrailId);
                    vs.Skier.CurrentTrailId = trail.TrailId;
                    vs.EvaluatedLiftExits.Clear();
                    vs.EvaluatedTrailExits.Clear();
                    
                    // Enter the crossing trail at the crossing point (not the start)
                    vs.Motion.SwitchTrail(trail, pos);
                    
                    if (Traffic != null) Traffic.FireTrailEntered(vs.Skier.SkierId, trail.TrailId);
                    return;
                }
            }
        }

        /// <summary>
        /// Builds a SkierContext from a VisualSkier for the decision engine.
        /// </summary>
        private SkierContext BuildContext(VisualSkier vs)
        {
            int goalTrailId = -1;
            int goalLiftId = -1;
            if (vs.Skier.CurrentGoal != null && !vs.Skier.CurrentGoal.IsComplete)
            {
                var step = vs.Skier.CurrentGoal.GetCurrentStep();
                if (step != null)
                {
                    if (step.StepType == PathStepType.SkiTrail) goalTrailId = step.EntityId;
                    else if (step.StepType == PathStepType.RideLift) goalLiftId = step.EntityId;
                }
            }
            
            return new SkierContext
            {
                SkierId = vs.Skier.SkierId,
                Skill = vs.Skier.Skill,
                GoalTrailId = goalTrailId,
                GoalLiftId = goalLiftId,
                LiftsRidden = vs.LiftsRidden,
                TrailsSkied = vs.TrailsSkied,
                PersonalityOffsets = vs.PersonalityOffsets
            };
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

            // Fire traffic event: skier completed trail
            if (Traffic != null) Traffic.FireTrailCompleted(vs.Skier.SkierId, vs.CurrentTrail.TrailId);

            // Update skier state so PlanNewGoal knows where we are (at end of this trail)
            vs.Skier.CurrentState = SkierState.SkiingTrail;
            vs.Skier.CurrentTrailId = vs.CurrentTrail.TrailId;

            // ── PENDING LODGE: If skier has a queued lodge visit and is now close, divert ──
            if (vs.PendingLodgeVisit != null)
            {
                float distToPending = Vector3.Distance(trailEndPos, vs.PendingLodgeVisit.Position);
                if (distToPending <= 30f)
                {
                    vs.TargetLodge = vs.PendingLodgeVisit;
                    vs.PendingLodgeVisit = null;
                    vs.Phase = SkierPhase.WalkingToLodge;
                    vs.Motion.SetWalkTarget(vs.TargetLodge.Position);
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Reached pending lodge at trail end ({distToPending:F0}m)");
                    return;
                }
            }

            // ── DONE CHECK: Always check after every trail ────────────────
            // If the skier is done, check if near base to despawn, otherwise
            // flag them as returning so they ski down toward the base lodge.
            if (!vs.Skier.WantsToKeepSkiing() || vs.IsReturningToBase)
            {
                // Check if near the base lodge — if so, despawn
                var baseSpawns = _liftBuilder.Connectivity.Registry.GetByType(SnapPointType.BaseSpawn);
                if (baseSpawns.Count > 0)
                {
                    Vector3 basePos = new Vector3(baseSpawns[0].Position.X, baseSpawns[0].Position.Y, baseSpawns[0].Position.Z);
                    if (Vector3.Distance(trailEndPos, basePos) <= 80f)
                    {
                        if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Reached base lodge, leaving resort! (runs: {vs.Skier.RunsCompleted}/{vs.Skier.DesiredRuns})");
                        vs.IsFinished = true;
                        return;
                    }
                }
                
                // Not near base yet — mark as returning and fall through to
                // the normal lift/trail routing below (skip lodges & replanning)
                if (!vs.IsReturningToBase)
                {
                    vs.IsReturningToBase = true;
                    vs.PendingLodgeVisit = null; // No more lodge visits when leaving
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Done skiing (runs: {vs.Skier.RunsCompleted}/{vs.Skier.DesiredRuns}), heading for base...");
                }
                
                // Skip lodge visits and goal replanning — go straight to lift/trail routing
            }
            else
            {
                // ── LODGE VISITS: Only for skiers still actively skiing ──────
                bool hasUrgentNeed = vs.Skier.Needs.HasUrgentNeed() && 
                                     vs.Skier.Needs.GetMostUrgentNeed() != "Fatigue"; // Fatigue = keep skiing, just slower
                float lodgeChance = Config != null ? Config.lodgeVisitChance : _lodgeVisitChance;
                bool randomVisit = Random.value < lodgeChance;
                
                if (hasUrgentNeed || randomVisit)
                {
                    LodgeFacility targetLodge = FindBestLodge(trailEndPos);
                    if (targetLodge != null)
                    {
                        float distToLodge = Vector3.Distance(trailEndPos, targetLodge.Position);
                        string reason = hasUrgentNeed ? $"urgent {vs.Skier.Needs.GetMostUrgentNeed()}" : "random visit";
                        
                        if (distToLodge <= 30f)
                        {
                            // Close enough — walk directly (short distance, like crossing from trail to building)
                            vs.TargetLodge = targetLodge;
                            vs.Phase = SkierPhase.WalkingToLodge;
                            vs.Motion.SetWalkTarget(targetLodge.Position);
                            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Walking to nearby lodge ({reason}, {distToLodge:F0}m)");
                            return;
                        }
                        else
                        {
                            // Lodge is far away — set as pending and continue via lifts/trails
                            // The skier will divert when they ski close to the lodge
                            vs.PendingLodgeVisit = targetLodge;
                            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Lodge queued as pending ({reason}, {distToLodge:F0}m away) — will ski there");
                            // Fall through to normal lift/trail routing
                        }
                    }
                    else if (hasUrgentNeed)
                    {
                        // Needed a lodge but none available -- record unfulfilled need
                        vs.Skier.Needs.RecordUnfulfilledNeed();
                    }
                }
            }

            // ── GOAL RE-PLANNING (skip if returning to base) ──────────
            // After every run, re-plan if the goal is stale/complete/null.
            // This makes skiers TRAVERSE the mountain: an expert finishing a green
            // connector will now plan "ride lift X → ski trail Y → ride lift Z →
            // ski that amazing double-black" instead of just picking the nearest lift.
            
            if (!vs.IsReturningToBase)
            {
                bool shouldReplan = Config != null ? Config.replanAfterEveryRun : true;
                bool goalStale = vs.Skier.CurrentGoal == null || vs.Skier.CurrentGoal.IsComplete || vs.Skier.CurrentGoal.GetCurrentStep() == null;
                
                if (goalStale || shouldReplan)
                {
                    var newGoal = _skierAI.PlanNewGoal(vs.Skier);
                    vs.Skier.CurrentGoal = newGoal;
                    vs.UseGoalBasedAI = (newGoal != null && newGoal.PlannedPath.Count > 0);
                    if (_enableDebugLogs && newGoal != null) 
                        Debug.Log($"[Skier {vs.Skier.SkierId}] Re-planned goal after run: {newGoal.PlannedPath.Count} steps → destination trail {newGoal.DestinationTrailId}");
                }
            }
            else
            {
                // Returning skier: clear goal so they just take the nearest route down
                vs.Skier.CurrentGoal = null;
                vs.UseGoalBasedAI = false;
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
                        
                        float goalWalkRadius = Config != null ? Config.goalLiftWalkRadius : 40f;
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

            // PRIORITY 3: Unified lift selection -- merge spatial + connection graph
            float liftSearchRadius = Config != null ? Config.liftSearchRadius : 50f;
            var nearbyLifts = FindNearbyLifts(trailEndPos, liftSearchRadius);
            
            // Also include lifts from connection graph: trail END → lift BOTTOM
            var graphLiftIds = _liftBuilder.Connectivity.Connections.GetLiftsAtTrailEnd(vs.CurrentTrail.TrailId);
            var liftIdSet = new HashSet<int>();
            foreach (var l in nearbyLifts) liftIdSet.Add(l.LiftId);
            var allLiftsData = _liftBuilder.LiftSystem.GetAllLifts();
            foreach (var lid in graphLiftIds)
            {
                if (!liftIdSet.Contains(lid))
                {
                    var lift = allLiftsData.Find(l => l.LiftId == lid);
                    if (lift != null && lift.IsValid)
                    {
                        nearbyLifts.Add(lift);
                        liftIdSet.Add(lid);
                    }
                }
            }

            if (nearbyLifts.Count > 0)
            {
                var ctx = BuildContext(vs);
                
                var chosenLift = SkierDecisionEngine.ChooseLift(
                    nearbyLifts,
                    ctx,
                    Config,
                    Traffic?.State,
                    _distribution,
                    GetBestTrailValueAtLift
                );
                
                if (chosenLift == null)
                    chosenLift = nearbyLifts[Random.Range(0, nearbyLifts.Count)];
                
                // IMMEDIATELY record intent so the next skier deciding this frame sees updated state
                if (Traffic != null) Traffic.FireLiftIntended(vs.Skier.SkierId, chosenLift.LiftId);
                
                vs.CurrentLift = chosenLift;
                vs.Phase = SkierPhase.WalkingToLift;

                var liftBottom = new Vector3(
                    vs.CurrentLift.StartPosition.X,
                    vs.CurrentLift.StartPosition.Y + SKI_HEIGHT_OFFSET,
                    vs.CurrentLift.StartPosition.Z
                );
                vs.Motion.SetWalkTarget(liftBottom);
                vs.Motion.SetLift(vs.CurrentLift);

                return;
            }

            // PRIORITY 4: Trail-to-trail connections (use decision engine, not random)
            var allConnections = _liftBuilder.Connectivity.Connections.GetAllConnections();
            var nextTrails = new List<TrailData>();
            foreach (var conn in allConnections)
            {
                if (conn.FromType == "Trail" && conn.FromId == vs.CurrentTrail.TrailId && conn.ToType == "Trail")
                {
                    var t = _trailDrawer.TrailSystem.GetTrail(conn.ToId);
                    if (t != null && t.IsValid) nextTrails.Add(t);
                }
            }

            if (nextTrails.Count > 0)
            {
                TrailData nextTrail = null;
                
                // Returning skiers strongly prefer trails that end near the base
                if (vs.IsReturningToBase)
                {
                    var baseTrails = nextTrails.FindAll(t => _liftBuilder.Connectivity.Connections.IsTrailConnectedToBase(t.TrailId));
                    if (baseTrails.Count > 0)
                    {
                        nextTrail = baseTrails[Random.Range(0, baseTrails.Count)];
                    }
                }
                
                if (nextTrail == null)
                {
                    // Use the decision engine for trail-to-trail choices
                    var trailCtx = BuildContext(vs);
                    nextTrail = SkierDecisionEngine.ChooseTrail(
                        nextTrails, trailCtx, Config, Traffic?.State, _distribution, ComputeTrailDownstreamValue
                    );
                    if (nextTrail == null) nextTrail = nextTrails[Random.Range(0, nextTrails.Count)];
                }
                
                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Trail junction → trail {nextTrail.TrailId} (from {nextTrails.Count} options){(vs.IsReturningToBase ? " [returning]" : "")}");
                vs.CurrentTrail = nextTrail;
                vs.TrailsSkied.Add(nextTrail.TrailId);
                vs.Skier.CurrentTrailId = nextTrail.TrailId;
                vs.Motion.SwitchTrail(nextTrail, trailEndPos);
                
                // Fire traffic events
                if (Traffic != null) Traffic.FireTrailEntered(vs.Skier.SkierId, nextTrail.TrailId);
                
                return;
            }

            // PRIORITY 5: Nearby trail starts spatially
            var nearbyTrails = FindNearbyTrailStarts(trailEndPos, 25f);
            var validTrails = nearbyTrails.FindAll(t => t.TrailId != vs.CurrentTrail.TrailId);
            if (validTrails.Count > 0)
            {
                TrailData chosenTrail = null;
                
                // Returning skiers strongly prefer trails that end near the base
                if (vs.IsReturningToBase)
                {
                    var baseTrails = validTrails.FindAll(t => _liftBuilder.Connectivity.Connections.IsTrailConnectedToBase(t.TrailId));
                    if (baseTrails.Count > 0)
                    {
                        chosenTrail = baseTrails[Random.Range(0, baseTrails.Count)];
                    }
                }
                
                if (chosenTrail == null)
                {
                    chosenTrail = ChooseTrailByPreference(vs.Skier, validTrails, true);
                }
                
                vs.CurrentTrail = chosenTrail;
                vs.Skier.CurrentTrailId = chosenTrail.TrailId;
                vs.Motion.SwitchTrail(chosenTrail, trailEndPos);
                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Continuing to trail {chosenTrail.TrailId}{(vs.IsReturningToBase ? " [returning]" : "")}");
                return;
            }

            // PRIORITY 6: Near base?
            {
                var baseSpawnsP6 = _liftBuilder.Connectivity.Registry.GetByType(SnapPointType.BaseSpawn);
                if (baseSpawnsP6.Count > 0)
                {
                    Vector3 basePos = new Vector3(baseSpawnsP6[0].Position.X, baseSpawnsP6[0].Position.Y, baseSpawnsP6[0].Position.Z);
                    if (Vector3.Distance(trailEndPos, basePos) <= 80f)
                    {
                        if (vs.IsReturningToBase)
                        {
                            // Returning skier reached base — despawn
                            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Reached base lodge, leaving resort!");
                            vs.IsFinished = true;
                            return;
                        }
                        if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Reached base! Run #{vs.Skier.RunsCompleted}");
                        ChooseNewDestination(vs);
                        return;
                    }
                }
            }

            // LAST RESORT: walk to the nearest lift bottom on the entire mountain (no radius limit)
            // Never teleport — skiers physically walk to their next lift.
            var allLiftsForRescue = _liftBuilder.LiftSystem.GetAllLifts();
            LiftData nearestLift = null;
            float nearestDist = float.MaxValue;
            foreach (var lift in allLiftsForRescue)
            {
                if (!lift.IsValid) continue;
                Vector3 lBottom = new Vector3(lift.StartPosition.X, lift.StartPosition.Y, lift.StartPosition.Z);
                float d = Vector3.Distance(trailEndPos, lBottom);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearestLift = lift;
                }
            }
            
            if (nearestLift != null)
            {
                Debug.Log($"[Skier {vs.Skier.SkierId}] Stranded at {trailEndPos}. Walking to nearest lift {nearestLift.LiftId} ({nearestDist:F0}m away)");
                vs.CurrentLift = nearestLift;
                vs.Phase = SkierPhase.WalkingToLift;
                var rescuePos = new Vector3(
                    nearestLift.StartPosition.X,
                    nearestLift.StartPosition.Y + SKI_HEIGHT_OFFSET,
                    nearestLift.StartPosition.Z
                );
                vs.Motion.SetWalkTarget(rescuePos);
                vs.Motion.SetLift(nearestLift);
            }
            else
            {
                // Truly no lifts on the mountain — finish the skier
                Debug.LogWarning($"[Skier {vs.Skier.SkierId}] No lifts on mountain! Finishing.");
                vs.IsFinished = true;
            }
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
                    float junctionRadius = Config != null ? Config.junctionDetectionRadius : 15f;
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
                            
                            float majorThresh = Config != null ? Config.junctionMajorThreshold : 0.25f;
                            float moderateThresh = Config != null ? Config.junctionModerateThreshold : 0.1f;
                            float majorChance = Config != null ? Config.junctionMajorSwitchChance : 0.50f;
                            float moderateChance = Config != null ? Config.junctionModerateSwitchChance : 0.25f;
                            float exploreChance = Config != null ? Config.junctionExplorationChance : 0.12f;
                            float exploreMin = Config != null ? Config.junctionExplorationMinValue : 0.2f;

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
                // Check if already at the base
                var baseSpawns = _liftBuilder.Connectivity.Registry.GetByType(SnapPointType.BaseSpawn);
                if (baseSpawns.Count > 0)
                {
                    Vector3 basePos = new Vector3(baseSpawns[0].Position.X, baseSpawns[0].Position.Y, baseSpawns[0].Position.Z);
                    Vector3 skierPos = vs.GameObject.transform.position;
                    if (Vector3.Distance(skierPos, basePos) <= 80f)
                    {
                        if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] At base and done skiing, leaving resort!");
                        vs.IsFinished = true;
                        return;
                    }
                }
                
                // Not at base — mark as returning; still needs a lift/trail to get down
                vs.IsReturningToBase = true;
                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Done skiing from ChooseNewDestination, heading for base...");
                // Fall through to pick a lift/trail like normal
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

            // Fallback: use the decision engine to pick a lift (not just nearest-to-base)
            if (nextLift == null)
            {
                var validLifts = allLifts.FindAll(l => l.IsValid);
                if (validLifts.Count > 0)
                {
                    var ctx = BuildContext(vs);
                    nextLift = SkierDecisionEngine.ChooseLift(
                        validLifts, ctx, Config, Traffic?.State, _distribution, GetBestTrailValueAtLift
                    );
                    if (nextLift == null)
                        nextLift = validLifts[Random.Range(0, validLifts.Count)];
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
                    // Lodge is full -- record unfulfilled need attempt
                    vs.Skier.Needs.RecordUnfulfilledNeed();
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
                // ── Fulfill needs based on lodge amenities ──────────────
                bool usedBathroom = false;
                bool usedFood = false;
                bool usedRest = false;
                
                if (vs.TargetLodge.HasBathroom && vs.Skier.Needs.Bladder > 0.1f)
                {
                    vs.Skier.Needs.FulfillBladder();
                    usedBathroom = true;
                }
                
                if (vs.TargetLodge.HasFood && vs.Skier.Needs.Hunger > 0.1f)
                {
                    vs.Skier.Needs.FulfillHunger();
                    usedFood = true;
                }
                
                if (vs.TargetLodge.HasRest)
                {
                    vs.Skier.Needs.RecoverFatigue(30f); // Recover as if rested 30 game minutes
                    usedRest = true;
                }
                
                // ── Apply lodge pricing and revenue ─────────────────────
                var pricing = vs.TargetLodge.Pricing;
                if (pricing != null)
                {
                    float charge = pricing.CalculateCharge(usedBathroom, usedFood, usedRest);
                    float satisfactionImpact = pricing.CalculateSatisfactionImpact(usedBathroom, usedFood, usedRest);
                    
                    // Record revenue
                    pricing.RecordVisit(charge);
                    
                    // Add revenue to resort
                    if (_simRunner?.Sim?.State != null)
                    {
                        _simRunner.Sim.State.Money += (int)charge;
                    }
                    
                    // Apply satisfaction penalty from pricing
                    vs.Skier.Needs.AddPricePenalty(satisfactionImpact);
                    vs.Skier.Needs.LodgeVisitCount++;
                    
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Lodge visit: charged ${charge:F0}, satisfaction impact {satisfactionImpact:F2}");
                }
                
                // ── Respawn and continue ─────────────────────────────────
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
        //  Lodge queries
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Find the best available lodge on the mountain.
        /// Searches ALL lodges (not proximity-limited) and picks the nearest
        /// one that isn't full. Lodges can be at trail starts, ends, or anywhere.
        /// </summary>
        private LodgeFacility FindBestLodge(Vector3 skierPos)
        {
            LodgeManager lodgeManager = LodgeManager.Instance;
            if (lodgeManager == null) return null;
            
            LodgeFacility best = null;
            float bestDist = float.MaxValue;
            
            foreach (var lodge in lodgeManager.AllLodges)
            {
                if (lodge == null || lodge.IsFull) continue;
                
                float dist = Vector3.Distance(skierPos, lodge.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = lodge;
                }
            }
            
            return best;
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
            
            int depth = Config != null ? Config.downstreamDepth : 3;
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
            int totalDepth = Config != null ? Config.downstreamDepth : 3;
            
            if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                return 0f;
            if (maxDepth <= 0)
                return 0f;
            if (visitedTrails.Contains(trail.TrailId))
                return 0f;
            
            visitedTrails.Add(trail.TrailId);
            
            // Depth discount from config (or hardcoded fallback)
            float depthDiscount = Config != null
                ? Config.GetDepthDiscount(maxDepth, totalDepth)
                : (maxDepth == totalDepth ? 1.0f : maxDepth == totalDepth - 1 ? 0.65f : 0.40f);

            var trailEnd = trail.WorldPathPoints[trail.WorldPathPoints.Count - 1];
            Vector3 endPos = new Vector3(trailEnd.X, trailEnd.Y, trailEnd.Z);
            
            float searchRadius = Config != null ? Config.liftSearchRadius : 50f;
            float trailSearchRadius = Config != null ? Config.trailStartSearchRadius : 50f;

            float bestPref = 0f;

            // ── Gather ALL lifts reachable from trail end (spatial + connection graph) ──
            var liftsAtEnd = FindNearbyLifts(endPos, searchRadius);
            var graphLiftIds = _liftBuilder.Connectivity.Connections.GetLiftsAtTrailEnd(trail.TrailId);
            var liftIdsSeen = new HashSet<int>();
            foreach (var l in liftsAtEnd) liftIdsSeen.Add(l.LiftId);
            var allLiftsLookup = _liftBuilder.LiftSystem.GetAllLifts();
            foreach (var lid in graphLiftIds)
            {
                if (!liftIdsSeen.Contains(lid))
                {
                    var liftData = allLiftsLookup.Find(l => l.LiftId == lid);
                    if (liftData != null && liftData.IsValid)
                    {
                        liftsAtEnd.Add(liftData);
                        liftIdsSeen.Add(lid);
                    }
                }
            }
            
            // Check lifts at trail end → trails at those lift tops → recurse deeper
            foreach (var lift in liftsAtEnd)
            {
                if (visitedLifts.Contains(lift.LiftId)) continue;
                visitedLifts.Add(lift.LiftId);
                
                // ── Gather ALL trails at lift top (spatial + connection graph) ──
                Vector3 liftTopPos = new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z);
                var trailsAtTop = FindNearbyTrailStarts(liftTopPos, trailSearchRadius);
                var graphTrailIds = _liftBuilder.Connectivity.Connections.GetTrailsFromLift(lift.LiftId);
                var trailIdsSeen = new HashSet<int>();
                foreach (var t in trailsAtTop) trailIdsSeen.Add(t.TrailId);
                foreach (var tid in graphTrailIds)
                {
                    if (!trailIdsSeen.Contains(tid))
                    {
                        var td = _trailDrawer.TrailSystem.GetTrail(tid);
                        if (td != null && td.IsValid)
                        {
                            trailsAtTop.Add(td);
                            trailIdsSeen.Add(tid);
                        }
                    }
                }
                
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

            // Also check trail-to-trail connections (spatial + connection graph)
            var nextTrails = FindNearbyTrailStarts(endPos, trailSearchRadius);
            var allConns = _liftBuilder.Connectivity.Connections.GetAllConnections();
            var nextTrailIdsSeen = new HashSet<int>();
            foreach (var t in nextTrails) nextTrailIdsSeen.Add(t.TrailId);
            foreach (var conn in allConns)
            {
                if (conn.FromType == "Trail" && conn.FromId == trail.TrailId && conn.ToType == "Trail")
                {
                    if (!nextTrailIdsSeen.Contains(conn.ToId))
                    {
                        var td = _trailDrawer.TrailSystem.GetTrail(conn.ToId);
                        if (td != null && td.IsValid)
                        {
                            nextTrails.Add(td);
                            nextTrailIdsSeen.Add(conn.ToId);
                        }
                    }
                }
            }
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

            float directWeight = Config != null ? Config.difficultyPreferenceStrength : 1.0f;
            float dsWeight = Config != null ? Config.downstreamValueStrength : 1.0f;
            
            float directPref = _distribution.GetPreference(skill, trail.Difficulty);
            float downstream = ComputeTrailDownstreamValue(skill, trail);

            // Always include direct preference — don't kill it just because downstream is low.
            // A trail with 0 downstream just doesn't get the downstream bonus.
            return directPref * directWeight + downstream * dsWeight;
        }

        /// <summary>
        /// Returns the best trail decision value at a lift's top.
        /// Used as a factor in the lift scoring formula.
        /// </summary>
        private float GetBestTrailValueAtLift(SkillLevel skill, LiftData lift)
        {
            float bestScore = 0f;
            float searchRadius = Config != null ? Config.trailStartSearchRadius : 50f;
            
            // Merge spatial + connection graph for trails at lift top
            Vector3 liftTopPos = new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z);
            var trailsAtTop = FindNearbyTrailStarts(liftTopPos, searchRadius);
            var graphTrailIds = _liftBuilder.Connectivity.Connections.GetTrailsFromLift(lift.LiftId);
            var seenIds = new HashSet<int>();
            foreach (var t in trailsAtTop) seenIds.Add(t.TrailId);
            foreach (var tid in graphTrailIds)
            {
                if (!seenIds.Contains(tid))
                {
                    var td = _trailDrawer.TrailSystem.GetTrail(tid);
                    if (td != null && td.IsValid)
                    {
                        trailsAtTop.Add(td);
                        seenIds.Add(tid);
                    }
                }
            }
            
            foreach (var trail in trailsAtTop)
            {
                float trailValue = ComputeTrailDecisionValue(skill, trail);
                bestScore = Mathf.Max(bestScore, trailValue);
            }
            
            return bestScore;
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
            float searchRadius = Config != null ? Config.trailStartSearchRadius : 30f;

            Vector3 liftTopPos = new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z);
            var trailsAtTop = FindNearbyTrailStarts(liftTopPos, searchRadius);

            foreach (var trail in trailsAtTop)
            {
                float trailValue = ComputeTrailDecisionValue(skill, trail);
                bestScore = Mathf.Max(bestScore, trailValue);
            }

            // Variety bonus (legacy scoring — decision engine uses novelty factor instead)
            float newBonus = 1.4f;
            float repeatPenalty = 0.85f;
            
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
            
            if (Config != null && Config.logLiftScores)
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
            
            // Mid-trail exit tracking: IDs of lifts/trails already evaluated on this trail run
            // Prevents re-checking the same exit every frame
            public HashSet<int> EvaluatedLiftExits = new HashSet<int>();
            public HashSet<int> EvaluatedTrailExits = new HashSet<int>();

            // Mountain traversal: track which lifts/trails this skier has used
            public HashSet<int> LiftsRidden = new HashSet<int>();
            public HashSet<int> TrailsSkied = new HashSet<int>();
            
            // Per-skier personality: small random offsets to scoring weights
            // Generated once at spawn, deterministic per skierId
            public float[] PersonalityOffsets;

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
            
            // Pending lodge visit: skier wants to visit this lodge but needs to ski there first
            public LodgeFacility PendingLodgeVisit;
            
            // Returning to base: skier is done skiing and heading for the base lodge to leave
            public bool IsReturningToBase;
            public float ReturningToBaseTimer; // Safety timeout (game minutes, 480 = 1 in-game day)
        }
    }
}
