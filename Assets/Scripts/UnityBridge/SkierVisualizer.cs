using UnityEngine;
using System.Collections.Generic;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Spawns and animates cosmetic skier dots to visualize visitor flow.
    /// These are purely visual - actual simulation runs at end-of-day.
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
        
        [Header("Spawn Settings")]
        [SerializeField] private Vector2 baseSpawnPosition = new Vector2(50, 50); // Default base position
        [SerializeField] private bool useSnapPoints = false; // Use snap point system or direct spawning
        
        [Header("Movement Settings")]
        [SerializeField] private float _liftSpeed = 2f; // tiles per second
        [SerializeField] private float _skiSpeed = 5f; // tiles per second
        [SerializeField] private float _spawnInterval = 2f; // seconds between spawns
        
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false; // Toggle console spam
        
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
                    Destroy(skier.GameObject);
                    _activeSkiers.RemoveAt(i);
                }
            }
        }
        
        /// <summary>
        /// Initializes the SkierAI system for goal-based decision making.
        /// </summary>
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
            
            if (_enableDebugLogs) Debug.Log("[SkierVisualizer] SkierAI initialized with {allTrails.Count} trails and {allLifts.Count} lifts");
        }
        
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
                // Extract first lift and trail from planned path
                foreach (var step in goal.PlannedPath)
                {
                    if (step.StepType == PathStepType.RideLift && startLift == null)
                    {
                        startLift = allLifts.Find(l => l.LiftId == step.EntityId);
                    }
                    else if (step.StepType == PathStepType.SkiTrail && targetTrail == null)
                    {
                        targetTrail = allTrails.Find(t => t.TrailId == step.EntityId);
                    }
                    
                    if (startLift != null && targetTrail != null)
                        break;
                }
                
                if (_enableDebugLogs) Debug.Log("[Skier {skier.SkierId}] AI planned {goal.PlannedPath.Count} steps to reach {goal.Type} (destination trail: {goal.DestinationTrailId})");
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
                    // SPATIAL AWARENESS: Find trails NEAR lift top
                    Vector3 liftTopPos = new Vector3(startLift.EndPosition.X, startLift.EndPosition.Y, startLift.EndPosition.Z);
                    var nearbyTrails = FindNearbyTrails(liftTopPos, 25f);
                    if (nearbyTrails.Count > 0)
                    {
                        targetTrail = nearbyTrails[Random.Range(0, nearbyTrails.Count)];
                    }
                }
            }
            
            if (targetTrail == null)
            {
                Debug.LogWarning($"[SkierVisualizer] Could not find valid trail for lift {startLift.LiftId}!");
                return;
            }
            
            // Create visual skier GameObject
            var skierObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skierObj.name = $"Skier_{skier.SkierId}_{skier.Skill}";
            skierObj.transform.localScale = Vector3.one * _skierSize;
            
            // Set material with skill-based color
            var renderer = skierObj.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = GetSkillColor(skier.Skill);
            
            // Remove collider
            Destroy(skierObj.GetComponent<Collider>());
            
            // Set initial position at base
            var baseCoord = new TileCoord((int)baseSpawnPosition.x, (int)baseSpawnPosition.y);
            var tile = grid.GetTile(baseCoord);
            float baseHeight = tile != null ? tile.Height : -35f;
            var startPos = new Vector3(baseSpawnPosition.x, baseHeight, baseSpawnPosition.y);
            skierObj.transform.position = startPos;
            
            // Log spawning info with skill and goal
            string goalInfo = goal != null && goal.PlannedPath.Count > 0 
                ? $"Goal: {goal.Type}, Path: {string.Join("→", goal.PlannedPath)}"
                : "Using fallback path";
            if (_enableDebugLogs) Debug.Log("[Skier {skier.SkierId}] {skier.Skill} spawned → Lift {startLift.LiftId} → Trail {targetTrail.TrailId} ({targetTrail.Difficulty}) | {goalInfo}");
            
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
                PhaseProgress = 0f,
                LateralOffset = Random.Range(-0.8f, 0.8f),
                HasSwitchedAtJunction = false,
                Pathfinder = null,
                ReachableTrails = allTrails,
                UseGoalBasedAI = (goal != null && goal.PlannedPath.Count > 0)
            };
            
            _activeSkiers.Add(visualSkier);
        }
        
        private void UpdateSkier(VisualSkier vs, float deltaTime)
        {
            var grid = _trailDrawer.GridRenderer.TerrainData.Grid;
            var snapRegistry = _liftBuilder.Connectivity.Registry;
            
            switch (vs.Phase)
            {
                case SkierPhase.WalkingToLift:
                {
                    // Transition to riding the current lift
                    if (vs.CurrentLift == null)
                    {
                        vs.IsFinished = true;
                        return;
                    }
                    
                    // Transition to riding this lift
                    vs.Phase = SkierPhase.RidingLift;
                    vs.PhaseProgress = 0f;
                    
                    // Set position to lift bottom using StartPosition (X, Y, Z)
                    var liftBottom = new Vector3(
                        vs.CurrentLift.StartPosition.X, 
                        vs.CurrentLift.StartPosition.Y, 
                        vs.CurrentLift.StartPosition.Z
                    );
                    vs.GameObject.transform.position = liftBottom;
                    
                    if (_enableDebugLogs) Debug.Log("[Skier {vs.Skier.SkierId}] At lift bottom {liftBottom}, riding to ({vs.CurrentLift.EndPosition.X}, {vs.CurrentLift.EndPosition.Y}, {vs.CurrentLift.EndPosition.Z})");
                    break;
                }
                
                case SkierPhase.RidingLift:
                {
                    // Move up the lift
                    float liftLength = vs.CurrentLift.Length;
                    if (liftLength <= 0) liftLength = 1f;
                    
                    float oldProgress = vs.PhaseProgress;
                    vs.PhaseProgress += (_liftSpeed / liftLength) * deltaTime;
                    
                    // Log position every ~25% progress
                    if ((int)(oldProgress * 4) != (int)(vs.PhaseProgress * 4))
                    {
                        if (_enableDebugLogs) Debug.Log("[Skier {vs.Skier.SkierId}] Riding lift {(vs.PhaseProgress * 100):F0}% - pos {vs.GameObject.transform.position}");
                    }
                    
                    if (vs.PhaseProgress >= 1f)
                    {
                        // Reached top of lift - find trails to ski!
                        Vector3 liftTopPos = new Vector3(
                            vs.CurrentLift.EndPosition.X,
                            vs.CurrentLift.EndPosition.Y,
                            vs.CurrentLift.EndPosition.Z
                        );
                        
                        // SPATIAL AWARENESS: Find trails near lift top (flexible, no exact connection needed!)
                        var nearbyTrails = FindNearbyTrails(liftTopPos, 25f); // 25 unit radius
                        
                        TrailData chosenTrail = null;
                        bool explored = false;
                        
                        // EXPLORATION CHANCE: 20% chance to ditch goal and explore a preferred trail
                        if (Random.value < 0.20f && nearbyTrails.Count > 0)
                        {
                            // Filter to trails within their skill preference
                            var preferredTrails = nearbyTrails.FindAll(t => 
                                _distribution.GetPreference(vs.Skier.Skill, t.Difficulty) >= 0.2f);
                            
                            if (preferredTrails.Count > 0)
                            {
                                chosenTrail = ChooseTrailByPreference(vs.Skier, preferredTrails);
                                explored = true;
                                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] EXPLORING! Ditched goal for {chosenTrail.Difficulty} trail {chosenTrail.TrailId}");
                            }
                        }
                        
                        // GOAL PATH FOLLOWING: If not exploring, follow PlannedPath
                        if (chosenTrail == null && vs.Skier.CurrentGoal != null)
                        {
                            var currentStep = vs.Skier.CurrentGoal.GetCurrentStep();
                            
                            if (currentStep != null && currentStep.StepType == PathStepType.SkiTrail)
                            {
                                var plannedTrail = _trailDrawer.TrailSystem.GetTrail(currentStep.EntityId);
                                
                                if (plannedTrail != null && nearbyTrails.Contains(plannedTrail))
                                {
                                    // Take the planned trail!
                                    chosenTrail = plannedTrail;
                                    vs.Skier.CurrentGoal.AdvanceToNextStep();
                                    if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Following path: trail {chosenTrail.TrailId} toward goal");
                                }
                                else if (plannedTrail != null)
                                {
                                    // Goal trail exists but not nearby - find shortest path there
                                    // For now, take any trail that leads downhill (toward more options)
                                    if (nearbyTrails.Count > 0)
                                    {
                                        chosenTrail = ChooseTrailByPreference(vs.Skier, nearbyTrails);
                                        if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Goal not nearby, taking {chosenTrail.TrailId} to get closer");
                                    }
                                }
                            }
                        }
                        
                        // FALLBACK: If no goal or goal complete, use weighted preference
                        if (chosenTrail == null && nearbyTrails.Count > 0)
                        {
                            chosenTrail = ChooseTrailByPreference(vs.Skier, nearbyTrails);
                            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] No goal, chose preferred {chosenTrail.Difficulty} trail {chosenTrail.TrailId}");
                        }
                        
                        // LAST RESORT: Connection graph
                        if (chosenTrail == null)
                        {
                            var connectedTrailIds = _liftBuilder.Connectivity.Connections.GetTrailsFromLift(vs.CurrentLift.LiftId);
                            if (connectedTrailIds.Count > 0)
                            {
                                int trailId = connectedTrailIds[Random.Range(0, connectedTrailIds.Count)];
                                chosenTrail = _trailDrawer.TrailSystem.GetTrail(trailId);
                                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Using connection graph, chose trail {trailId}");
                            }
                        }
                        
                        if (chosenTrail != null)
                        {
                            vs.CurrentTrail = chosenTrail;
                            vs.Phase = SkierPhase.SkiingTrail;
                            vs.PhaseProgress = 0f;
                            vs.LateralOffset = Random.Range(-0.8f, 0.8f); // New random position for this trail
                            vs.HasSwitchedAtJunction = false; // Reset for new run - can switch once on this run
                            vs.Skier.RunsCompleted++;
                            if (_enableDebugLogs) Debug.Log("[Skier {vs.Skier.SkierId}] Reached top! Starting to ski trail {vs.CurrentTrail.TrailId}");
                        }
                        else
                        {
                            // No trails found - choose new destination
                            Debug.LogWarning($"[Skier {vs.Skier.SkierId}] No trails accessible from lift {vs.CurrentLift.LiftId} top!");
                            ChooseNewDestination(vs);
                        }
                        return;
                    }
                    
                    // Update position using proper 3D coordinates from LiftData
                    var startWorld = new Vector3(
                        vs.CurrentLift.StartPosition.X,
                        vs.CurrentLift.StartPosition.Y, 
                        vs.CurrentLift.StartPosition.Z
                    );
                    var endWorld = new Vector3(
                        vs.CurrentLift.EndPosition.X,
                        vs.CurrentLift.EndPosition.Y,
                        vs.CurrentLift.EndPosition.Z
                    );
                    vs.GameObject.transform.position = Vector3.Lerp(startWorld, endWorld, vs.PhaseProgress);
                    break;
                }
                
                case SkierPhase.SkiingTrail:
                {
                    // Move down the trail
                    float trailLength = CalculateTrailDistance(vs.CurrentTrail);
                    if (trailLength <= 0) trailLength = 1f;
                    
                    float oldProgress = vs.PhaseProgress;
                    vs.PhaseProgress += (_skiSpeed / trailLength) * deltaTime;
                    
                    // Log position every ~25% progress
                    if ((int)(oldProgress * 4) != (int)(vs.PhaseProgress * 4))
                    {
                        if (_enableDebugLogs) Debug.Log("[Skier {vs.Skier.SkierId}] Skiing trail {(vs.PhaseProgress * 100):F0}% - pos {vs.GameObject.transform.position}");
                    }
                    
                    if (vs.PhaseProgress >= 1f)
                    {
                        // Finished this trail - figure out what to do next!
                        Vector3 trailEndPos = vs.GameObject.transform.position;
                        if (_enableDebugLogs) Debug.Log("[Skier {vs.Skier.SkierId}] Finished trail {vs.CurrentTrail.TrailId} at position {trailEndPos}");
                        
                        // PRIORITY 1: Check for nearby lifts to board (mid-mountain lifts!)
                        var nearbyLifts = FindNearbyLifts(trailEndPos, 20f);
                        
                        // Filter lifts by skill - only board lifts with trails we can ski
                        // BUT: 2% "Jerry" chance to accidentally board any lift (funny!)
                        bool isJerry = Random.value < 0.02f;
                        var validLifts = new List<LiftData>();
                        
                        foreach (var lift in nearbyLifts)
                        {
                            if (isJerry)
                            {
                                validLifts.Add(lift);
                                continue;
                            }
                            
                            // Check if this lift has any trails we can ski
                            bool hasValidTrail = false;
                            var nearbyTrailsAtTop = FindNearbyTrails(
                                new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z), 30f);
                            
                            foreach (var trail in nearbyTrailsAtTop)
                            {
                                if (_distribution.IsAllowed(vs.Skier.Skill, trail.Difficulty))
                                {
                                    hasValidTrail = true;
                                    break;
                                }
                            }
                            
                            if (hasValidTrail)
                            {
                                validLifts.Add(lift);
                            }
                        }
                        
                        if (validLifts.Count > 0)
                        {
                            // Board a valid lift!
                            vs.CurrentLift = validLifts[Random.Range(0, validLifts.Count)];
                            vs.Phase = SkierPhase.WalkingToLift;
                            vs.PhaseProgress = 0f;
                            if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Boarding lift {vs.CurrentLift.LiftId} {(isJerry ? "(JERRY!)" : "")}");
                            return;
                        }
                        
                        // PRIORITY 2: Check for trail-to-trail connections (junctions)
                        var allConnections = _liftBuilder.Connectivity.Connections.GetAllConnections();
                        var nextTrailIds = new List<int>();
                        
                        foreach (var conn in allConnections)
                        {
                            if (conn.FromType == "Trail" && conn.FromId == vs.CurrentTrail.TrailId && conn.ToType == "Trail")
                            {
                                nextTrailIds.Add(conn.ToId);
                            }
                        }
                        
                        if (nextTrailIds.Count > 0)
                        {
                            // Transition to connected trail!
                            int nextTrailId = nextTrailIds[Random.Range(0, nextTrailIds.Count)];
                            var nextTrail = _trailDrawer.TrailSystem.GetTrail(nextTrailId);
                            
                            if (nextTrail != null)
                            {
                                // Find closest point on next trail (no teleporting!)
                                float closestProgress = FindClosestProgressOnTrail(trailEndPos, nextTrail);
                                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Trail junction! Transitioning from trail {vs.CurrentTrail.TrailId} → trail {nextTrailId}");
                                vs.CurrentTrail = nextTrail;
                                vs.PhaseProgress = closestProgress;
                                vs.LateralOffset = Random.Range(-0.8f, 0.8f);
                                return;
                            }
                        }
                        
                        // PRIORITY 3: Check for nearby trails spatially (not just connections)
                        var nearbyTrails = FindNearbyTrails(trailEndPos, 25f);
                        if (nearbyTrails.Count > 0)
                        {
                            // Filter out the trail we just skied
                            var validTrails = nearbyTrails.FindAll(t => t.TrailId != vs.CurrentTrail.TrailId);
                            if (validTrails.Count > 0)
                            {
                                var chosenTrail = validTrails[Random.Range(0, validTrails.Count)];
                                // Find closest point on trail (no teleporting!)
                                float closestProgress = FindClosestProgressOnTrail(trailEndPos, chosenTrail);
                                vs.CurrentTrail = chosenTrail;
                                vs.PhaseProgress = closestProgress;
                                vs.LateralOffset = Random.Range(-0.8f, 0.8f);
                                if (_enableDebugLogs) Debug.Log($"[Skier {vs.Skier.SkierId}] Continuing to trail {vs.CurrentTrail.TrailId} at {(closestProgress * 100):F0}%");
                                return;
                            }
                        }
                        
                        // PRIORITY 4: Check if we're near base - if so, complete the run
                        var baseSpawns = _liftBuilder.Connectivity.Registry.GetByType(SnapPointType.BaseSpawn);
                        if (baseSpawns.Count > 0)
                        {
                            Vector3 basePos = new Vector3(baseSpawns[0].Position.X, baseSpawns[0].Position.Y, baseSpawns[0].Position.Z);
                            float distanceToBase = Vector3.Distance(trailEndPos, basePos);
                            
                            if (distanceToBase <= 50f) // Within 50 units of base
                            {
                                vs.Skier.RunsCompleted++;
                                if (_enableDebugLogs) Debug.Log("[Skier {vs.Skier.SkierId}] Reached base! Completed run #{vs.Skier.RunsCompleted}. Choosing new destination...");
                                ChooseNewDestination(vs);
                                return;
                            }
                        }
                        
                        // LAST RESORT: No lifts, trails, or base nearby - teleport back (shouldn't happen often now!)
                        vs.Skier.RunsCompleted++;
                        Debug.LogWarning($"[Skier {vs.Skier.SkierId}] Stranded at {trailEndPos}! No lifts or trails nearby. Teleporting to base...");
                        ChooseNewDestination(vs);
                        return;
                    }
                    
                    // Update position along trail
                    UpdateSkierOnTrail(vs, grid);
                    
                    // HYBRID JUNCTION HANDLING
                    // 1. Goal-based: follow AI-planned multi-trail routes
                    // 2. Exploration: small chance to switch to preferred-difficulty trails at junctions
                    if (!vs.HasSwitchedAtJunction)
                    {
                        bool switched = false;
                        
                        // First: Check if goal requires a trail switch
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
                                            float distToNextTrail = FindDistanceToTrailStart(currentPos, nextTrail);
                                            if (distToNextTrail < 20f)
                                            {
                                                float newProgress = FindClosestProgressOnTrail(currentPos, nextTrail);
                                                if (_enableDebugLogs) Debug.Log("[Skier {vs.Skier.SkierId}] Following goal: Trail {vs.CurrentTrail.TrailId} → Trail {nextTrail.TrailId}");
                                                vs.CurrentTrail = nextTrail;
                                                vs.PhaseProgress = newProgress;
                                                vs.LateralOffset = Random.Range(-0.8f, 0.8f);
                                                vs.HasSwitchedAtJunction = true;
                                                goal.AdvanceToNextStep();
                                                switched = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        
                        // Second: Natural exploration at junctions (20% chance, preference-weighted)
                        if (!switched && vs.PhaseProgress > 0.2f && vs.PhaseProgress < 0.8f)
                        {
                            // Only check occasionally (every ~10% progress)
                            if (Mathf.Abs(vs.PhaseProgress % 0.1f) < 0.015f)
                            {
                                Vector3 currentPos = vs.GameObject.transform.position;
                                var nearbyTrails = FindNearbyTrails(currentPos, 12f);
                                var validTrails = nearbyTrails.FindAll(t => t.TrailId != vs.CurrentTrail.TrailId);
                                
                                if (validTrails.Count > 0)
                                {
                                    // Filter by preference - only switch to trails the skier prefers
                                    var preferredTrails = validTrails.FindAll(t => 
                                        _distribution.GetPreference(vs.Skier.Skill, t.Difficulty) >= 0.3f);
                                    
                                    if (preferredTrails.Count > 0 && Random.value < 0.20f)
                                    {
                                        var chosenTrail = preferredTrails[Random.Range(0, preferredTrails.Count)];
                                        float newProgress = FindClosestProgressOnTrail(currentPos, chosenTrail);
                                        
                                        if (_enableDebugLogs) Debug.Log("[Skier {vs.Skier.SkierId}] Exploring junction: Trail {vs.CurrentTrail.TrailId} → Trail {chosenTrail.TrailId} ({chosenTrail.Difficulty})");
                                        vs.CurrentTrail = chosenTrail;
                                        vs.PhaseProgress = newProgress;
                                        vs.LateralOffset = Random.Range(-0.8f, 0.8f);
                                        vs.HasSwitchedAtJunction = true;
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }
        
        private void ChooseNewDestination(VisualSkier vs)
        {
            // Check if skier wants to keep skiing using AI logic
            if (!vs.Skier.WantsToKeepSkiing())
            {
                if (_enableDebugLogs) Debug.Log("[Skier {vs.Skier.SkierId}] Done skiing (runs: {vs.Skier.RunsCompleted}/{vs.Skier.DesiredRuns}, satisfaction: {vs.Skier.GetSatisfaction():F2})");
                vs.IsFinished = true;
                return;
            }
            
            // Use SkierAI to plan new goal
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
                    {
                        nextLift = allLifts.Find(l => l.LiftId == step.EntityId);
                    }
                    else if (step.StepType == PathStepType.SkiTrail && nextTrail == null)
                    {
                        nextTrail = allTrails.Find(t => t.TrailId == step.EntityId);
                    }
                    
                    if (nextLift != null && nextTrail != null)
                        break;
                }
                
                vs.UseGoalBasedAI = true;
            }
            
            // Fallback to legacy behavior
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
            
            if (nextLift == null)
            {
                vs.IsFinished = true;
                return;
            }
            
            // Fallback for trail
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
            vs.PhaseProgress = 0f;
            
            string pathInfo = vs.UseGoalBasedAI ? $"(AI goal: {goal?.Type})" : "(fallback)";
            if (_enableDebugLogs) Debug.Log("[Skier {vs.Skier.SkierId}] New destination: Lift {vs.CurrentLift.LiftId} → Trail {nextTrail.TrailId} ({nextTrail.Difficulty}) {pathInfo}");
        }
        
        private void UpdateSkierOnTrail(VisualSkier skier, GridSystem grid)
        {
            var trail = skier.CurrentTrail;
            
            // Use 3D WorldPathPoints if available, otherwise fall back to legacy
            if (trail.WorldPathPoints != null && trail.WorldPathPoints.Count >= 2)
            {
                float totalLength = CalculateTrailDistance(trail);
                float targetDistance = totalLength * skier.PhaseProgress;
                
                float currentDistance = 0f;
                for (int i = 0; i < trail.WorldPathPoints.Count - 1; i++)
                {
                    var p1 = trail.WorldPathPoints[i];
                    var p2 = trail.WorldPathPoints[i + 1];
                    
                    // Calculate 3D distance
                    float segmentDist = Vector3.Distance(
                        new Vector3(p1.X, p1.Y, p1.Z),
                        new Vector3(p2.X, p2.Y, p2.Z)
                    );
                    
                    if (currentDistance + segmentDist >= targetDistance)
                    {
                        // Interpolate between p1 and p2 in 3D
                        float localProgress = segmentDist > 0 ? (targetDistance - currentDistance) / segmentDist : 0f;
                        var w1 = new Vector3(p1.X, p1.Y, p1.Z);
                        var w2 = new Vector3(p2.X, p2.Y, p2.Z);
                        Vector3 centerlinePos = Vector3.Lerp(w1, w2, localProgress);
                        
                        // BOUNDARY-AWARE: Add lateral offset perpendicular to trail direction
                        Vector3 trailDirection = (w2 - w1).normalized;
                        // Perpendicular vector in XZ plane (rotate 90° around Y axis)
                        Vector3 perpendicular = new Vector3(-trailDirection.z, 0, trailDirection.x).normalized;
                        
                        // Get trail width at this point (use average of boundaries if available)
                        float trailWidth = 5f; // Default width
                        if (trail.LeftBoundaryPoints != null && trail.RightBoundaryPoints != null && 
                            trail.LeftBoundaryPoints.Count > 0 && trail.RightBoundaryPoints.Count > 0)
                        {
                            // Sample boundaries at similar progress point
                            int boundaryIndex = Mathf.Clamp((int)(localProgress * trail.LeftBoundaryPoints.Count), 0, trail.LeftBoundaryPoints.Count - 1);
                            var leftPt = trail.LeftBoundaryPoints[boundaryIndex];
                            var rightPt = trail.RightBoundaryPoints[boundaryIndex];
                            trailWidth = Vector3.Distance(
                                new Vector3(leftPt.X, leftPt.Y, leftPt.Z),
                                new Vector3(rightPt.X, rightPt.Y, rightPt.Z)
                            );
                        }
                        
                        // Apply lateral offset (-1 to 1) * (half width)
                        Vector3 finalPos = centerlinePos + perpendicular * (skier.LateralOffset * trailWidth * 0.5f);
                        skier.GameObject.transform.position = finalPos;
                        return;
                    }
                    
                    currentDistance += segmentDist;
                }
                
                // Fallback to end
                var endPoint = trail.WorldPathPoints[trail.WorldPathPoints.Count - 1];
                skier.GameObject.transform.position = new Vector3(endPoint.X, endPoint.Y, endPoint.Z);
            }
            else if (trail.PathPoints != null && trail.PathPoints.Count >= 2)
            {
                // Legacy fallback (use 2D tile coords)
                float totalLength = CalculateTrailDistance(trail);
                float targetDistance = totalLength * skier.PhaseProgress;
                
                float currentDistance = 0f;
                for (int i = 0; i < trail.PathPoints.Count - 1; i++)
                {
                    var p1 = trail.PathPoints[i];
                    var p2 = trail.PathPoints[i + 1];
                    float segmentDist = Vector2.Distance(new Vector2(p1.X, p1.Y), new Vector2(p2.X, p2.Y));
                    
                    if (currentDistance + segmentDist >= targetDistance)
                    {
                        float localProgress = segmentDist > 0 ? (targetDistance - currentDistance) / segmentDist : 0f;
                        var w1 = TileToWorld(p1, grid);
                        var w2 = TileToWorld(p2, grid);
                        skier.GameObject.transform.position = Vector3.Lerp(w1, w2, localProgress);
                        return;
                    }
                    
                    currentDistance += segmentDist;
                }
                
                // Fallback to end
                skier.GameObject.transform.position = TileToWorld(trail.PathPoints[trail.PathPoints.Count - 1], grid);
            }
        }
        
        private float CalculateTrailDistance(TrailData trail)
        {
            // Use 3D WorldPathPoints if available
            if (trail.WorldPathPoints != null && trail.WorldPathPoints.Count >= 2)
            {
                float totalDistance = 0f;
                for (int i = 0; i < trail.WorldPathPoints.Count - 1; i++)
                {
                    var p1 = trail.WorldPathPoints[i];
                    var p2 = trail.WorldPathPoints[i + 1];
                    totalDistance += Vector3.Distance(
                        new Vector3(p1.X, p1.Y, p1.Z),
                        new Vector3(p2.X, p2.Y, p2.Z)
                    );
                }
                return totalDistance > 0 ? totalDistance : 1f;
            }
            
            // Legacy fallback (2D tile coords)
            if (trail.PathPoints != null && trail.PathPoints.Count >= 2)
            {
                float totalDistance = 0f;
                for (int i = 0; i < trail.PathPoints.Count - 1; i++)
                {
                    var p1 = trail.PathPoints[i];
                    var p2 = trail.PathPoints[i + 1];
                    totalDistance += Vector2.Distance(new Vector2(p1.X, p1.Y), new Vector2(p2.X, p2.Y));
                }
                return totalDistance > 0 ? totalDistance : 1f;
            }
            
            return 1f;
        }
        
        /// <summary>
        /// Calculates the distance from a position to the start of a trail.
        /// Used for goal-based junction detection.
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
        
        private Vector3 TileToWorld(TileCoord coord, GridSystem grid)
        {
            if (grid == null)
            {
                Debug.LogError("[SkierVisualizer] Grid is null in TileToWorld!");
                return Vector3.zero;
            }
            
            var tile = grid.GetTile(coord);
            if (tile == null)
            {
                // Don't spam - tile might be out of bounds
                float x = coord.X;
                float y = coord.Y;
                return new Vector3(x, y, -5f);
            }
            
            float height = tile.Height;
            float xPos = coord.X;
            float yPos = coord.Y + height * 0.5f;
            return new Vector3(xPos, yPos, -5f);
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
                // Check distance to lift bottom (boarding point)
                Vector3 liftBottom = new Vector3(lift.StartPosition.X, lift.StartPosition.Y, lift.StartPosition.Z);
                float distance = Vector3.Distance(position, liftBottom);
                
                if (distance <= radius)
                {
                    nearbyLifts.Add(lift);
                }
            }
            
            return nearbyLifts;
        }
        
        /// <summary>
        /// Chooses a trail based on skier's skill preferences using weighted random selection.
        /// </summary>
        private TrailData ChooseTrailByPreference(Skier skier, List<TrailData> availableTrails)
        {
            if (availableTrails.Count == 0) return null;
            if (availableTrails.Count == 1) return availableTrails[0];
            
            // Calculate weighted probabilities
            float totalWeight = 0f;
            var weights = new List<float>();
            
            foreach (var trail in availableTrails)
            {
                // Get preference weight (0-1) based on skier skill and trail difficulty
                float pref = _distribution.GetPreference(skier.Skill, trail.Difficulty);
                
                // Ensure minimum weight so even non-preferred trails have tiny chance
                float weight = Mathf.Max(pref, 0.05f);
                weights.Add(weight);
                totalWeight += weight;
            }
            
            // Weighted random selection
            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            
            for (int i = 0; i < availableTrails.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    return availableTrails[i];
                }
            }
            
            // Fallback
            return availableTrails[availableTrails.Count - 1];
        }
        
        /// <summary>
        /// Returns a color based on skier skill level (matches trail difficulty colors).
        /// </summary>
        private Color GetSkillColor(SkillLevel skill)
        {
            switch (skill)
            {
                case SkillLevel.Beginner:
                    return new Color(0.1f, 1f, 0.1f); // Green
                case SkillLevel.Intermediate:
                    return new Color(0.2f, 0.5f, 1f); // Blue
                case SkillLevel.Advanced:
                    return new Color(0.1f, 0.1f, 0.1f); // Black/Dark gray
                case SkillLevel.Expert:
                    return new Color(1f, 0.2f, 0.2f); // Red (double-black)
                default:
                    return Color.white;
            }
        }
        
        /// <summary>
        /// Find trails within a specified radius of a position (for skiing off lifts or junctions).
        /// </summary>
        private List<TrailData> FindNearbyTrails(Vector3 position, float radius)
        {
            var nearbyTrails = new List<TrailData>();
            var allTrails = _trailDrawer.TrailSystem.GetAllTrails();
            
            foreach (var trail in allTrails)
            {
                if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count == 0)
                    continue;
                
                // Check distance to trail start
                var trailStart = trail.WorldPathPoints[0];
                Vector3 startPos = new Vector3(trailStart.X, trailStart.Y, trailStart.Z);
                float distanceToStart = Vector3.Distance(position, startPos);
                
                if (distanceToStart <= radius)
                {
                    nearbyTrails.Add(trail);
                    continue;
                }
                
                // Also check any point along the trail for more flexibility
                foreach (var point in trail.WorldPathPoints)
                {
                    Vector3 pointPos = new Vector3(point.X, point.Y, point.Z);
                    if (Vector3.Distance(position, pointPos) <= radius)
                    {
                        nearbyTrails.Add(trail);
                        break;
                    }
                }
            }
            
            return nearbyTrails;
        }
        
        /// <summary>
        /// Find the closest point on a trail's path and return the progress (0-1) at that point.
        /// Used for seamless trail switching at junctions.
        /// </summary>
        private float FindClosestProgressOnTrail(Vector3 position, TrailData trail)
        {
            if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                return 0f;
            
            float closestDistance = float.MaxValue;
            float closestProgress = 0f;
            float totalDistance = 0f;
            float distanceToClosest = 0f;
            
            // Calculate total trail length and find closest segment
            for (int i = 0; i < trail.WorldPathPoints.Count - 1; i++)
            {
                var p1 = trail.WorldPathPoints[i];
                var p2 = trail.WorldPathPoints[i + 1];
                
                Vector3 v1 = new Vector3(p1.X, p1.Y, p1.Z);
                Vector3 v2 = new Vector3(p2.X, p2.Y, p2.Z);
                
                float segmentLength = Vector3.Distance(v1, v2);
                
                // Find closest point on this segment
                Vector3 closestPointOnSegment = ClosestPointOnLineSegment(position, v1, v2);
                float distanceToSegment = Vector3.Distance(position, closestPointOnSegment);
                
                if (distanceToSegment < closestDistance)
                {
                    closestDistance = distanceToSegment;
                    distanceToClosest = totalDistance + Vector3.Distance(v1, closestPointOnSegment);
                }
                
                totalDistance += segmentLength;
            }
            
            // Convert distance to progress (0-1)
            if (totalDistance > 0)
            {
                closestProgress = Mathf.Clamp01(distanceToClosest / totalDistance);
            }
            
            return closestProgress;
        }
        
        /// <summary>
        /// Find the closest point on a line segment to a given position.
        /// </summary>
        private Vector3 ClosestPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 line = lineEnd - lineStart;
            float lineLength = line.magnitude;
            
            if (lineLength < 0.001f)
                return lineStart;
            
            Vector3 lineDirection = line / lineLength;
            
            // Project point onto line
            float projectionDistance = Vector3.Dot(point - lineStart, lineDirection);
            
            // Clamp to segment bounds
            projectionDistance = Mathf.Clamp(projectionDistance, 0f, lineLength);
            
            return lineStart + lineDirection * projectionDistance;
        }
        
        private enum SkierPhase
        {
            WalkingToLift,
            RidingLift,
            SkiingTrail
        }
        
        private class VisualSkier
        {
            public GameObject GameObject;
            public Skier Skier; // Core skier data
            public LiftData CurrentLift;
            public TrailData CurrentTrail;
            public List<TrailData> PlannedTrails; // Trails in current route
            public int CurrentTrailIndex; // Current position in plan
            public SkierPhase Phase;
            public float PhaseProgress; // 0 to 1 along current segment
            public bool IsFinished;
            
            // Boundary-aware movement
            public float LateralOffset; // -1 to 1, position across trail width
            public bool HasSwitchedAtJunction; // Prevent endless switching loops
            
            // Pathfinding references (for replanning)
            public SkierPathfinder Pathfinder;
            public List<TrailData> ReachableTrails;
            
            // Goal-based AI
            public bool UseGoalBasedAI; // Flag to enable new AI system
        }
    }
}
