using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Represents an edge in the network graph.
    /// </summary>
    public struct NetworkEdge
    {
        public SnapPoint From { get; set; }
        public SnapPoint To { get; set; }
        
        public NetworkEdge(SnapPoint from, SnapPoint to)
        {
            From = from;
            To = to;
        }
    }
    
    /// <summary>
    /// Directed graph of the ski resort network.
    /// Edges represent valid traversal paths (Base → Lift → Trail → etc.)
    /// Pure C# - no Unity types.
    /// </summary>
    public class NetworkGraph
    {
        private SnapRegistry _registry;
        private TerrainData _terrain;
        
        // Adjacency list: SnapPoint → List of connected SnapPoints
        private Dictionary<int, List<SnapPoint>> _adjacencyList;
        
        public int SnapRadius { get; set; } = 2;  // Max distance for connections
        
        public NetworkGraph(SnapRegistry registry, TerrainData terrain)
        {
            _registry = registry;
            _terrain = terrain;
            _adjacencyList = new Dictionary<int, List<SnapPoint>>();
        }
        
        /// <summary>
        /// Builds the directed graph based on current snap points.
        /// </summary>
        public void BuildGraph()
        {
            _adjacencyList.Clear();
            
            // Debug: Log all snap points
            var allSnaps = _registry.GetAll();
            UnityEngine.Debug.Log($"[NetworkGraph] Building graph with {allSnaps.Count} snap points");
            
            var basePoints = _registry.GetByType(SnapPointType.BaseSpawn);
            var liftBottoms = _registry.GetByType(SnapPointType.LiftBottom);
            var liftTops = _registry.GetByType(SnapPointType.LiftTop);
            var trailStarts = _registry.GetByType(SnapPointType.TrailStart);
            var trailEnds = _registry.GetByType(SnapPointType.TrailEnd);
            
            UnityEngine.Debug.Log($"[NetworkGraph] BaseSpawn: {basePoints.Count}, LiftBottom: {liftBottoms.Count}, LiftTop: {liftTops.Count}, TrailStart: {trailStarts.Count}, TrailEnd: {trailEnds.Count}");
            
            // 1. Base → LiftBottom connections
            ConnectBaseToLifts();
            
            // 2. LiftBottom → LiftTop connections (riding the lift UP)
            ConnectLiftBottomsToTops();
            
            // 3. LiftTop → TrailStart connections
            ConnectLiftsToTrails();
            
            // 4. TrailEnd → LiftBottom connections (back to lifts)
            ConnectTrailsToLifts();
            
            // 5. TrailEnd → TrailStart connections (trail branching)
            ConnectTrailsToTrails();
            
            UnityEngine.Debug.Log($"[NetworkGraph] Graph built. Total edges: {CountTotalEdges()}");
        }
        
        private int CountTotalEdges()
        {
            int count = 0;
            foreach (var list in _adjacencyList.Values)
            {
                count += list.Count;
            }
            return count;
        }
        
        private void ConnectBaseToLifts()
        {
            var basePoints = _registry.GetByType(SnapPointType.BaseSpawn);
            var liftBottoms = _registry.GetByType(SnapPointType.LiftBottom);
            
            int connectionsCreated = 0;
            
            foreach (var basePoint in basePoints)
            {
                foreach (var liftBottom in liftBottoms)
                {
                    int distance = basePoint.DistanceTo(liftBottom);
                    UnityEngine.Debug.Log($"[NetworkGraph] Distance from Base {basePoint.Coord} to LiftBottom {liftBottom.Coord}: {distance} (max: {SnapRadius})");
                    
                    if (distance <= SnapRadius)
                    {
                        AddEdge(basePoint, liftBottom);
                        connectionsCreated++;
                        UnityEngine.Debug.Log($"[NetworkGraph] Connected Base → LiftBottom (Lift #{liftBottom.OwnerId})");
                    }
                }
            }
            
            UnityEngine.Debug.Log($"[NetworkGraph] Base→Lift connections: {connectionsCreated}");
        }
        
        private void ConnectLiftBottomsToTops()
        {
            var liftBottoms = _registry.GetByType(SnapPointType.LiftBottom);
            var liftTops = _registry.GetByType(SnapPointType.LiftTop);
            
            int connectionsCreated = 0;
            
            // Connect each lift bottom to its corresponding top (same owner ID)
            foreach (var liftBottom in liftBottoms)
            {
                foreach (var liftTop in liftTops)
                {
                    // Same lift if they have the same owner ID
                    if (liftBottom.OwnerId == liftTop.OwnerId)
                    {
                        AddEdge(liftBottom, liftTop);
                        connectionsCreated++;
                        UnityEngine.Debug.Log($"[NetworkGraph] Connected LiftBottom → LiftTop (Lift #{liftBottom.OwnerId})");
                    }
                }
            }
            
            UnityEngine.Debug.Log($"[NetworkGraph] Lift traversal connections: {connectionsCreated}");
        }
        
        private void ConnectLiftsToTrails()
        {
            var liftTops = _registry.GetByType(SnapPointType.LiftTop);
            var trailStarts = _registry.GetByType(SnapPointType.TrailStart);
            
            foreach (var liftTop in liftTops)
            {
                foreach (var trailStart in trailStarts)
                {
                    int distance = liftTop.DistanceTo(trailStart);
                    if (distance <= SnapRadius)
                    {
                        AddEdge(liftTop, trailStart);
                    }
                }
            }
        }
        
        private void ConnectTrailsToLifts()
        {
            var trailEnds = _registry.GetByType(SnapPointType.TrailEnd);
            var liftBottoms = _registry.GetByType(SnapPointType.LiftBottom);
            
            foreach (var trailEnd in trailEnds)
            {
                foreach (var liftBottom in liftBottoms)
                {
                    int distance = trailEnd.DistanceTo(liftBottom);
                    if (distance <= SnapRadius)
                    {
                        AddEdge(trailEnd, liftBottom);
                    }
                }
            }
        }
        
        private void ConnectTrailsToTrails()
        {
            var trailEnds = _registry.GetByType(SnapPointType.TrailEnd);
            var trailStarts = _registry.GetByType(SnapPointType.TrailStart);
            
            foreach (var trailEnd in trailEnds)
            {
                foreach (var trailStart in trailStarts)
                {
                    // Don't connect trail to itself
                    if (trailEnd.OwnerId == trailStart.OwnerId)
                        continue;
                    
                    int distance = trailEnd.DistanceTo(trailStart);
                    if (distance <= SnapRadius)
                    {
                        AddEdge(trailEnd, trailStart);
                    }
                }
            }
        }
        
        private void AddEdge(SnapPoint from, SnapPoint to)
        {
            int fromHash = GetSnapPointHash(from);
            
            if (!_adjacencyList.ContainsKey(fromHash))
            {
                _adjacencyList[fromHash] = new List<SnapPoint>();
            }
            
            _adjacencyList[fromHash].Add(to);
        }
        
        /// <summary>
        /// Gets all neighbors of a snap point (outgoing edges).
        /// </summary>
        public List<SnapPoint> GetNeighbors(SnapPoint point)
        {
            int hash = GetSnapPointHash(point);
            
            if (_adjacencyList.ContainsKey(hash))
            {
                return new List<SnapPoint>(_adjacencyList[hash]);
            }
            
            return new List<SnapPoint>();
        }
        
        private int GetSnapPointHash(SnapPoint point)
        {
            return ((int)point.Type * 1000000) + (point.OwnerId * 1000) + (point.Coord.X * 100) + point.Coord.Y;
        }
    }
}

