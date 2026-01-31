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
        
        private List<VisualSkier> _activeSkiers = new List<VisualSkier>();
        private float _spawnTimer;
        private Material _skierMaterial;
        private bool _hasLoggedUpdate = false;
        
        private void Awake()
        {
            // Create material for skier dots
            _skierMaterial = new Material(Shader.Find("Unlit/Color"));
            _skierMaterial.color = _skierColor;
            Debug.Log("[SkierVisualizer] Awake - initialized material");
        }
        
        private void Update()
        {
            if (!_hasLoggedUpdate)
            {
                Debug.Log("[SkierVisualizer] Update is being called");
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
            
            // Pick a random lift and trail
            var lift = allLifts[Random.Range(0, allLifts.Count)];
            var trail = allTrails[Random.Range(0, allTrails.Count)];
            
            // Create skier with random skill level
            var distribution = new SkierDistribution();
            var skillLevel = distribution.GetRandomSkillLevel(new System.Random());
            var skier = new Skier(_activeSkiers.Count, skillLevel);
            
            // Create visual skier GameObject
            var skierObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skierObj.name = $"Skier_{skier.SkierId}";
            skierObj.transform.localScale = Vector3.one * _skierSize;
            
            // Set material
            var renderer = skierObj.GetComponent<Renderer>();
            renderer.material = _skierMaterial;
            
            // Remove collider
            Destroy(skierObj.GetComponent<Collider>());
            
            // Set initial position at base (use X/Z for horizontal, Y for height)
            // baseSpawnPosition.x and .y are actually X and Z in 3D space
            var baseCoord = new TileCoord((int)baseSpawnPosition.x, (int)baseSpawnPosition.y);
            var tile = grid.GetTile(baseCoord);
            float baseHeight = tile != null ? tile.Height : -35f; // Default to Base Lodge height
            
            // Unity coordinates: X and Z horizontal, Y vertical
            var startPos = new Vector3(baseSpawnPosition.x, baseHeight, baseSpawnPosition.y);
            skierObj.transform.position = startPos;
            
            Debug.Log($"[Skier {skier.SkierId}] Spawned at {startPos}, will ride Lift {lift.LiftId} ({lift.StartPosition.X}, {lift.StartPosition.Y}, {lift.StartPosition.Z}) to ({lift.EndPosition.X}, {lift.EndPosition.Y}, {lift.EndPosition.Z})");
            
            // Create visual skier data
            var visualSkier = new VisualSkier
            {
                GameObject = skierObj,
                Skier = skier,
                CurrentLift = lift,
                CurrentTrail = trail,
                PlannedTrails = new List<TrailData> { trail },
                CurrentTrailIndex = 0,
                Phase = SkierPhase.WalkingToLift,
                PhaseProgress = 0f,
                Pathfinder = null,
                ReachableTrails = allTrails
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
                    
                    Debug.Log($"[Skier {vs.Skier.SkierId}] At lift bottom {liftBottom}, riding to ({vs.CurrentLift.EndPosition.X}, {vs.CurrentLift.EndPosition.Y}, {vs.CurrentLift.EndPosition.Z})");
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
                        Debug.Log($"[Skier {vs.Skier.SkierId}] Riding lift {(vs.PhaseProgress * 100):F0}% - pos {vs.GameObject.transform.position}");
                    }
                    
                    if (vs.PhaseProgress >= 1f)
                    {
                        // Reached top - transition to skiing
                        if (vs.CurrentTrailIndex < vs.PlannedTrails.Count)
                        {
                            vs.CurrentTrail = vs.PlannedTrails[vs.CurrentTrailIndex];
                            vs.Phase = SkierPhase.SkiingTrail;
                            vs.PhaseProgress = 0f;
                            vs.Skier.RunsCompleted++;
                            Debug.Log($"[Skier {vs.Skier.SkierId}] Reached top! Starting to ski trail {vs.CurrentTrail.TrailId}");
                        }
                        else
                        {
                            // No more trails - choose new destination
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
                    
                    vs.PhaseProgress += (_skiSpeed / trailLength) * deltaTime;
                    
                    if (vs.PhaseProgress >= 1f)
                    {
                        // Finished this trail - move to next
                        vs.CurrentTrailIndex++;
                        
                        if (vs.CurrentTrailIndex < vs.PlannedTrails.Count)
                        {
                            // More trails in plan - continue
                            vs.Phase = SkierPhase.WalkingToLift;
                            vs.PhaseProgress = 0f;
                        }
                        else
                        {
                            // Finished all trails - choose new destination
                            ChooseNewDestination(vs);
                        }
                        return;
                    }
                    
                    // Update position along trail
                    UpdateSkierOnTrail(vs, grid);
                    break;
                }
            }
        }
        
        private void ChooseNewDestination(VisualSkier vs)
        {
            // Simplified mode: just pick random lift and trail
            if (vs.Pathfinder == null && vs.ReachableTrails != null && vs.ReachableTrails.Count > 0)
            {
                var allLifts = _liftBuilder.LiftSystem.GetAllLifts();
                if (allLifts.Count == 0)
                {
                    vs.IsFinished = true;
                    return;
                }
                
                // Pick random lift and trail
                vs.CurrentLift = allLifts[Random.Range(0, allLifts.Count)];
                var randomTrail = vs.ReachableTrails[Random.Range(0, vs.ReachableTrails.Count)];
                
                vs.PlannedTrails = new List<TrailData> { randomTrail };
                vs.CurrentTrailIndex = 0;
                vs.Phase = SkierPhase.WalkingToLift;
                vs.PhaseProgress = 0f;
                Debug.Log($"[SkierVisualizer] Skier {vs.Skier.SkierId} chose new destination (simplified)");
                return;
            }
            
            // Pathfinder mode (original)
            if (vs.Pathfinder == null)
            {
                vs.IsFinished = true;
                return;
            }
            
            // Choose a new destination and plan route
            var destinationTrail = vs.Pathfinder.ChooseDestinationTrail(vs.Skier, vs.ReachableTrails);
            if (destinationTrail == null)
            {
                // No valid destination - remove skier
                vs.IsFinished = true;
                return;
            }
            
            var pathTrails = vs.Pathfinder.FindPathToTrail(destinationTrail);
            if (pathTrails.Count == 0)
            {
                // No path found - remove skier
                vs.IsFinished = true;
                return;
            }
            
            // Update plan
            vs.PlannedTrails = pathTrails;
            vs.CurrentTrailIndex = 0;
            vs.Phase = SkierPhase.WalkingToLift;
            vs.PhaseProgress = 0f;
        }
        
        private void UpdateSkierOnTrail(VisualSkier skier, GridSystem grid)
        {
            var trail = skier.CurrentTrail;
            if (trail.PathPoints.Count < 2)
                return;
            
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
                    // Interpolate between p1 and p2
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
        
        private float CalculateTrailDistance(TrailData trail)
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
            
            // Pathfinding references (for replanning)
            public SkierPathfinder Pathfinder;
            public List<TrailData> ReachableTrails;
        }
    }
}
