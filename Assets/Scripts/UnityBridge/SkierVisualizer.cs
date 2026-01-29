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
        
        [Header("Movement Settings")]
        [SerializeField] private float _liftSpeed = 2f; // tiles per second
        [SerializeField] private float _skiSpeed = 5f; // tiles per second
        [SerializeField] private float _spawnInterval = 2f; // seconds between spawns
        
        private List<VisualSkier> _activeSkiers = new List<VisualSkier>();
        private float _spawnTimer;
        private Material _skierMaterial;
        
        private void Awake()
        {
            // Create material for skier dots
            _skierMaterial = new Material(Shader.Find("Unlit/Color"));
            _skierMaterial.color = _skierColor;
        }
        
        private void Update()
        {
            // Check if all systems are ready
            if (_simRunner == null || _liftBuilder == null || _trailDrawer == null)
                return;
            
            if (_liftBuilder.LiftSystem == null || _trailDrawer.TrailSystem == null)
                return;
            
            if (_trailDrawer.GridRenderer == null || _trailDrawer.GridRenderer.TerrainData == null)
                return;
            
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
            var snapRegistry = _liftBuilder.Connectivity.Registry;
            var terrainData = _trailDrawer.GridRenderer.TerrainData;
            
            // Get base spawn point
            var basePoints = snapRegistry.GetByType(SnapPointType.BaseSpawn);
            if (basePoints.Count == 0)
                return;
            
            var basePoint = basePoints[0];
            
            // Find a lift near base
            var lifts = _liftBuilder.LiftSystem.Lifts;
            if (lifts.Count == 0)
                return;
            
            // Pick a random lift
            var lift = lifts[Random.Range(0, lifts.Count)];
            
            // Find trails connected to this lift's top station
            var trailsFromLift = _liftBuilder.Connectivity.Connections.GetTrailsFromLift(lift.LiftId);
            if (trailsFromLift.Count == 0)
                return;
            
            // Pick a random trail
            int trailId = trailsFromLift[Random.Range(0, trailsFromLift.Count)];
            var trail = _trailDrawer.TrailSystem.GetTrail(trailId);
            if (trail == null)
                return;
            
            // Create visual skier
            var skierObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skierObj.name = "VisualSkier";
            skierObj.transform.localScale = Vector3.one * _skierSize;
            
            // Set material
            var renderer = skierObj.GetComponent<Renderer>();
            renderer.material = _skierMaterial;
            
            // Remove collider (we don't need physics)
            Destroy(skierObj.GetComponent<Collider>());
            
            // Set initial position at lift bottom
            var startPos = TileToWorld(lift.BottomStation, terrainData.Grid);
            skierObj.transform.position = startPos;
            
            Debug.Log($"[SkierVisualizer] Spawned skier at {startPos} (lift bottom: {lift.BottomStation})");
            
            var skier = new VisualSkier
            {
                GameObject = skierObj,
                CurrentLift = lift,
                CurrentTrail = trail,
                Phase = SkierPhase.RidingLift,
                PhaseProgress = 0f
            };
            
            _activeSkiers.Add(skier);
        }
        
        private void UpdateSkier(VisualSkier skier, float deltaTime)
        {
            var grid = _trailDrawer.GridRenderer.TerrainData.Grid;
            
            switch (skier.Phase)
            {
                case SkierPhase.RidingLift:
                {
                    // Move up the lift
                    float liftLength = skier.CurrentLift.Length;
                    if (liftLength <= 0) liftLength = 1f;
                    
                    skier.PhaseProgress += (_liftSpeed / liftLength) * deltaTime;
                    
                    if (skier.PhaseProgress >= 1f)
                    {
                        // Transition to skiing
                        skier.Phase = SkierPhase.SkiingTrail;
                        skier.PhaseProgress = 0f;
                    }
                    
                    // Update position
                    var start = skier.CurrentLift.BottomStation;
                    var end = skier.CurrentLift.TopStation;
                    var startWorld = TileToWorld(start, grid);
                    var endWorld = TileToWorld(end, grid);
                    skier.GameObject.transform.position = Vector3.Lerp(startWorld, endWorld, skier.PhaseProgress);
                    break;
                }
                
                case SkierPhase.SkiingTrail:
                {
                    // Move down the trail
                    float trailLength = CalculateTrailDistance(skier.CurrentTrail);
                    if (trailLength <= 0) trailLength = 1f;
                    
                    skier.PhaseProgress += (_skiSpeed / trailLength) * deltaTime;
                    
                    if (skier.PhaseProgress >= 1f)
                    {
                        // Finished run
                        skier.IsFinished = true;
                        return;
                    }
                    
                    // Update position along trail
                    UpdateSkierOnTrail(skier, grid);
                    break;
                }
            }
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
            float height = grid.GetTile(coord).Height;
            // Match the same conversion used by visualizers
            float x = coord.X;
            float y = coord.Y + height * 0.5f; // Height offset for 2.5D
            return new Vector3(x, y, -5f); // Z negative to be in front of terrain
        }
        
        private enum SkierPhase
        {
            RidingLift,
            SkiingTrail
        }
        
        private class VisualSkier
        {
            public GameObject GameObject;
            public LiftData CurrentLift;
            public TrailData CurrentTrail;
            public SkierPhase Phase;
            public float PhaseProgress; // 0 to 1
            public bool IsFinished;
        }
    }
}
