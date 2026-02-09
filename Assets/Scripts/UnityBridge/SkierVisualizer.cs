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

            // Reached top -- decide which trail to ski
            Vector3 liftTopPos = new Vector3(
                vs.CurrentLift.EndPosition.X,
                vs.CurrentLift.EndPosition.Y,
                vs.CurrentLift.EndPosition.Z
            );

            var nearbyTrails = FindNearbyTrailStarts(liftTopPos, 25f);
            TrailData chosenTrail = null;

            // EXPLORATION CHANCE: 20% chance to ditch goal and explore
            if (Random.value < 0.20f && nearbyTrails.Count > 0)
            {
                var preferredTrails = nearbyTrails.FindAll(t =>
                {
                    float pref = _distribution.GetPreference(vs.Skier.Skill, t.Difficulty);
                    if (pref >= 0.15f) return true;
                    // Also consider trails that lead to desirable terrain downstream
                    float downstream = ComputeTrailDownstreamValue(vs.Skier.Skill, t);
                    return downstream >= 0.3f;
                });
                if (preferredTrails.Count > 0)
                {
                    chosenTrail = ChooseTrailByPreference(vs.Skier, preferredTrails, true);
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] EXPLORING! Ditched goal for {chosenTrail.Difficulty} trail {chosenTrail.TrailId}");
                }
            }

            // GOAL PATH FOLLOWING (with downstream override)
            if (chosenTrail == null && vs.Skier.CurrentGoal != null)
            {
                var currentStep = vs.Skier.CurrentGoal.GetCurrentStep();
                if (currentStep != null && currentStep.StepType == PathStepType.SkiTrail)
                {
                    var plannedTrail = _trailDrawer.TrailSystem.GetTrail(currentStep.EntityId);
                    if (plannedTrail != null && nearbyTrails.Contains(plannedTrail))
                    {
                        chosenTrail = plannedTrail;
                        vs.Skier.CurrentGoal.AdvanceToNextStep();

                        // DOWNSTREAM OVERRIDE: check if another nearby trail leads to
                        // significantly better terrain than the planned trail.
                        // Uses dead-end-aware values so beginners won't chase greens
                        // into double-black-only areas. Also probabilistic (60%) to
                        // preserve some variety and ensure all lifts get traffic.
                        float chosenValue = ComputeTrailDecisionValue(vs.Skier.Skill, chosenTrail);
                        TrailData bestAlt = null;
                        float bestAltValue = 0f;

                        foreach (var alt in nearbyTrails)
                        {
                            if (alt.TrailId == chosenTrail.TrailId) continue;
                            float altValue = ComputeTrailDecisionValue(vs.Skier.Skill, alt);
                            if (altValue > bestAltValue)
                            {
                                bestAltValue = altValue;
                                bestAlt = alt;
                            }
                        }

                        if (bestAlt != null && bestAltValue > chosenValue + 0.2f && Random.value < 0.60f)
                        {
                            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Overriding goal for downstream: Trail {bestAlt.TrailId} (value: {bestAltValue:F2}) over planned {chosenTrail.TrailId} (value: {chosenValue:F2})");
                            chosenTrail = bestAlt;
                            vs.Skier.CurrentGoal = null; // Goal is stale after deviation
                        }

                        if (vs.Skier.CurrentGoal != null && _enableDebugLogs)
                            Debug.Log($"[Skier {vs.Skier.SkierId}] Following path: trail {chosenTrail.TrailId} toward goal");
                    }
                    else if (plannedTrail != null && nearbyTrails.Count > 0)
                    {
                        chosenTrail = ChooseTrailByPreference(vs.Skier, nearbyTrails, true);
                        if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Goal not nearby, taking {chosenTrail.TrailId}");
                    }
                }
            }

            // FALLBACK: weighted preference with downstream awareness
            if (chosenTrail == null && nearbyTrails.Count > 0)
            {
                chosenTrail = ChooseTrailByPreference(vs.Skier, nearbyTrails, true);
            }

            // LAST RESORT: Connection graph
            if (chosenTrail == null)
            {
                var connectedTrailIds = _liftBuilder.Connectivity.Connections.GetTrailsFromLift(vs.CurrentLift.LiftId);
                if (connectedTrailIds.Count > 0)
                {
                    int trailId = connectedTrailIds[Random.Range(0, connectedTrailIds.Count)];
                    chosenTrail = _trailDrawer.TrailSystem.GetTrail(trailId);
                }
            }

            if (chosenTrail != null)
            {
                vs.CurrentTrail = chosenTrail;
                vs.Phase = SkierPhase.SkiingTrail;
                vs.HasSwitchedAtJunction = false;
                vs.Skier.RunsCompleted++;

                // Tell motion controller to start skiing this trail from distance 0
                vs.Motion.SetTrail(chosenTrail, 0f);

                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Reached top! Starting trail {chosenTrail.TrailId}");
            }
            else
            {
                Debug.LogWarning($"[Skier {vs.Skier.SkierId}] No trails accessible from lift {vs.CurrentLift.LiftId} top!");
                ChooseNewDestination(vs);
            }
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
        /// </summary>
        private void OnTrailFinished(VisualSkier vs)
        {
            Vector3 trailEndPos = vs.GameObject.transform.position;
            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Finished trail {vs.CurrentTrail.TrailId} at {trailEndPos}");

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
                        // Choose random available lodge
                        LodgeFacility targetLodge = availableLodges[Random.Range(0, availableLodges.Count)];
                        vs.TargetLodge = targetLodge;
                        vs.Phase = SkierPhase.WalkingToLodge;
                        vs.Motion.SetWalkTarget(targetLodge.Position);
                        
                        if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Heading to lodge at {targetLodge.Position}");
                        return;
                    }
                }
            }

            // PRIORITY 2: Check for nearby lifts (downstream-aware scoring)
            var nearbyLifts = FindNearbyLifts(trailEndPos, 20f);
            bool isJerry = Random.value < 0.02f;
            var scoredLifts = new List<(LiftData lift, float score)>();

            foreach (var lift in nearbyLifts)
            {
                if (isJerry)
                {
                    scoredLifts.Add((lift, 1f));
                    continue;
                }

                // Score lift by what terrain is reachable (immediate + 2-hop downstream)
                float score = ScoreLiftForSkier(vs.Skier.Skill, lift);
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

                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Boarding lift {vs.CurrentLift.LiftId}{(isJerry ? " (JERRY!)" : "")}");
                return;
            }

            // PRIORITY 3: Trail-to-trail connections
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
                    vs.Motion.SwitchTrail(nextTrail, trailEndPos);
                    return;
                }
            }

            // PRIORITY 4: Nearby trail starts spatially
            var nearbyTrails = FindNearbyTrailStarts(trailEndPos, 25f);
            var validTrails = nearbyTrails.FindAll(t => t.TrailId != vs.CurrentTrail.TrailId);
            if (validTrails.Count > 0)
            {
                var chosenTrail = validTrails[Random.Range(0, validTrails.Count)];
                vs.CurrentTrail = chosenTrail;
                vs.Motion.SwitchTrail(chosenTrail, trailEndPos);
                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Continuing to trail {chosenTrail.TrailId}");
                return;
            }

            // PRIORITY 5: Near base?
            var baseSpawns = _liftBuilder.Connectivity.Registry.GetByType(SnapPointType.BaseSpawn);
            if (baseSpawns.Count > 0)
            {
                Vector3 basePos = new Vector3(baseSpawns[0].Position.X, baseSpawns[0].Position.Y, baseSpawns[0].Position.Z);
                if (Vector3.Distance(trailEndPos, basePos) <= 50f)
                {
                    vs.Skier.RunsCompleted++;
                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Reached base! Run #{vs.Skier.RunsCompleted}");
                    ChooseNewDestination(vs);
                    return;
                }
            }

            // LAST RESORT: stranded
            vs.Skier.RunsCompleted++;
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
                    Vector3 currentPos = vs.GameObject.transform.position;
                    var nearbyTrails = FindNearbyTrailSegments(currentPos, 15f);
                    var validTrails = nearbyTrails.FindAll(t => t.TrailId != vs.CurrentTrail.TrailId);

                    if (validTrails.Count > 0)
                    {
                        // Dead-end-aware value of current trail
                        float currentValue = ComputeTrailDecisionValue(vs.Skier.Skill, vs.CurrentTrail);

                        // Find the best junction trail using dead-end-aware scoring
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

                            if (improvement > 0.25f)
                            {
                                // Much better terrain ahead - likely to switch
                                switchChance = 0.50f;
                            }
                            else if (improvement > 0.1f)
                            {
                                // Noticeably better terrain
                                switchChance = 0.25f;
                            }
                            else if (bestJunctionValue >= 0.2f)
                            {
                                // Decent trail nearby, normal exploration
                                switchChance = 0.12f;
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
        /// Computes the best preference value of terrain reachable 1 hop beyond a trail's end.
        /// Looks at: trail end → nearby lifts → trails at those lift tops,
        /// plus trail-to-trail connections at the trail end.
        /// This lets skiers evaluate connector trails by what they lead to.
        /// </summary>
        private float ComputeTrailDownstreamValue(SkillLevel skill, TrailData trail)
        {
            if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                return 0f;

            // Get trail end position
            var trailEnd = trail.WorldPathPoints[trail.WorldPathPoints.Count - 1];
            Vector3 endPos = new Vector3(trailEnd.X, trailEnd.Y, trailEnd.Z);

            float bestPref = 0f;

            // Check lifts reachable from trail end → trails at those lift tops
            var liftsAtEnd = FindNearbyLifts(endPos, 25f);
            foreach (var lift in liftsAtEnd)
            {
                Vector3 liftTopPos = new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z);
                var trailsAtTop = FindNearbyTrailStarts(liftTopPos, 25f);
                foreach (var t in trailsAtTop)
                {
                    // Only count trails the skier can actually ski
                    if (!_distribution.IsAllowed(skill, t.Difficulty)) continue;
                    float pref = _distribution.GetPreference(skill, t.Difficulty);
                    bestPref = Mathf.Max(bestPref, pref);
                }
            }

            // Also check trail-to-trail connections (trails that start near this trail's end)
            var nextTrails = FindNearbyTrailStarts(endPos, 25f);
            foreach (var t in nextTrails)
            {
                if (t.TrailId == trail.TrailId) continue;
                if (!_distribution.IsAllowed(skill, t.Difficulty)) continue;
                float pref = _distribution.GetPreference(skill, t.Difficulty);
                bestPref = Mathf.Max(bestPref, pref);
            }

            return bestPref;
        }

        /// <summary>
        /// Computes a trail's decision value for a skier, combining direct appeal
        /// with what terrain is reachable beyond it. Trails that dead-end (no reachable
        /// terrain the skier can ski after) are heavily penalized. This prevents
        /// beginners eagerly taking a green that leads to a double-black-only lift.
        /// </summary>
        private float ComputeTrailDecisionValue(SkillLevel skill, TrailData trail)
        {
            if (!_distribution.IsAllowed(skill, trail.Difficulty)) return 0f;
            if (_distribution.IsDesperateOnly(skill, trail.Difficulty)) return 0.01f;

            float directPref = _distribution.GetPreference(skill, trail.Difficulty);
            float downstream = ComputeTrailDownstreamValue(skill, trail);

            if (downstream > 0.01f)
            {
                // Trail leads to reachable terrain for this skier
                return Mathf.Max(directPref, downstream);
            }
            else
            {
                // Dead end: no allowed terrain beyond this trail for this skier.
                // The trail itself is still skiable, but getting stuck afterwards
                // makes it much less attractive as a destination choice.
                return directPref * 0.4f;
            }
        }

        /// <summary>
        /// Scores how attractive a lift is for a skier, considering both immediate
        /// trails at the top and whether those trails lead to useful terrain.
        /// Dead-end trails (that strand the skier) are penalized in the scoring.
        /// </summary>
        private float ScoreLiftForSkier(SkillLevel skill, LiftData lift)
        {
            float bestScore = 0f;

            Vector3 liftTopPos = new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z);
            var trailsAtTop = FindNearbyTrailStarts(liftTopPos, 30f);

            foreach (var trail in trailsAtTop)
            {
                float trailValue = ComputeTrailDecisionValue(skill, trail);
                bestScore = Mathf.Max(bestScore, trailValue);
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
