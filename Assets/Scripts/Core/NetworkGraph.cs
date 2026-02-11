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
        
        public int SnapRadius { get; set; } = 2;  // Legacy: Max Manhattan tile distance
        public float SnapRadius3D { get; set; } = 25f;  // Max 3D Euclidean distance for connections (matches spatial queries)
        
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
            
            // 1. Base → LiftBottom connections
            ConnectBaseToLifts();
            
            // 2. LiftBottom → LiftTop connections (riding the lift UP)
            ConnectLiftBottomsToTops();
            
            // 3. LiftTop → TrailStart connections
            ConnectLiftsToTrails();
            
            // 4. TrailStart → TrailEnd connections (skiing down a trail!)
            //    CRITICAL: Without this, BFS cannot plan multi-hop routes.
            //    e.g. "ride lift 1 → ski green → ride lift 2 → ski double-black"
            ConnectTrailStartsToEnds();
            
            // 5. TrailEnd → LiftBottom connections (back to lifts)
            ConnectTrailsToLifts();
            
            // 6. TrailEnd → TrailStart connections (trail branching)
            ConnectTrailsToTrails();
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
            
            // Use generous radius for base connections (players expect base lifts to just work)
            float baseRadius = SnapRadius3D * 1.5f;
            
            foreach (var basePoint in basePoints)
            {
                foreach (var liftBottom in liftBottoms)
                {
                    float distance = basePoint.Distance3D(liftBottom);
                    
                    if (distance <= baseRadius)
                    {
                        AddEdge(basePoint, liftBottom);
                        connectionsCreated++;
                    }
                }
            }
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
                    }
                }
            }
        }
        
        private void ConnectLiftsToTrails()
        {
            var liftTops = _registry.GetByType(SnapPointType.LiftTop);
            var trailStarts = _registry.GetByType(SnapPointType.TrailStart);
            
            int connectionsCreated = 0;
            
            foreach (var liftTop in liftTops)
            {
                foreach (var trailStart in trailStarts)
                {
                    float distance = liftTop.Distance3D(trailStart);
                    if (distance <= SnapRadius3D)
                    {
                        AddEdge(liftTop, trailStart);
                        connectionsCreated++;
                    }
                }
            }
        }
        
        /// <summary>
        /// Connects each trail's start to its end (same owner ID = same trail).
        /// This represents "skiing down a trail" in the graph, enabling multi-hop
        /// route planning: Base → Lift1 → TrailStart(A) → TrailEnd(A) → Lift2 → ...
        /// </summary>
        private void ConnectTrailStartsToEnds()
        {
            var trailStarts = _registry.GetByType(SnapPointType.TrailStart);
            var trailEnds = _registry.GetByType(SnapPointType.TrailEnd);
            
            int connectionsCreated = 0;
            
            foreach (var start in trailStarts)
            {
                foreach (var end in trailEnds)
                {
                    if (start.OwnerId == end.OwnerId)
                    {
                        AddEdge(start, end);
                        connectionsCreated++;
                    }
                }
            }
        }
        
        private void ConnectTrailsToLifts()
        {
            var trailEnds = _registry.GetByType(SnapPointType.TrailEnd);
            var liftBottoms = _registry.GetByType(SnapPointType.LiftBottom);
            
            int connectionsCreated = 0;
            
            foreach (var trailEnd in trailEnds)
            {
                foreach (var liftBottom in liftBottoms)
                {
                    float distance = trailEnd.Distance3D(liftBottom);
                    if (distance <= SnapRadius3D)
                    {
                        AddEdge(trailEnd, liftBottom);
                        connectionsCreated++;
                    }
                }
            }
        }
        
        private void ConnectTrailsToTrails()
        {
            var trailEnds = _registry.GetByType(SnapPointType.TrailEnd);
            var trailStarts = _registry.GetByType(SnapPointType.TrailStart);
            
            int connectionsCreated = 0;
            
            foreach (var trailEnd in trailEnds)
            {
                foreach (var trailStart in trailStarts)
                {
                    // Don't connect trail to itself
                    if (trailEnd.OwnerId == trailStart.OwnerId)
                        continue;
                    
                    float distance = trailEnd.Distance3D(trailStart);
                    if (distance <= SnapRadius3D)
                    {
                        AddEdge(trailEnd, trailStart);
                        connectionsCreated++;
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

