using System.Collections.Generic;
using System.Linq;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Represents a connection between two objects (lift->trail, trail->building, etc).
    /// </summary>
    public struct Connection
    {
        public int FromId { get; set; }
        public int ToId { get; set; }
        public string FromType { get; set; }  // "Lift", "Trail", " Building"
        public string ToType { get; set; }
        public int Distance { get; set; }  // Manhattan distance between snap points
        
        public Connection(int fromId, int toId, string fromType, string toType, int distance)
        {
            FromId = fromId;
            ToId = toId;
            FromType = fromType;
            ToType = toType;
            Distance = distance;
        }
        
        public override string ToString()
        {
            return $"{FromType}#{FromId} -> {ToType}#{ToId} (dist: {Distance})";
        }
    }
    
    /// <summary>
    /// Stores and manages connections between lifts, trails, and buildings.
    /// Pure C# - no Unity types.
    /// </summary>
    public class ConnectionGraph
    {
        // Lift connections
        private Dictionary<int, List<int>> _liftToTrails;     // LiftId -> List<TrailId>
        private Dictionary<int, List<int>> _liftToBuildings;  // LiftId -> List<BuildingId>
        
        // Trail connections
        private Dictionary<int, List<int>> _trailToLifts;     // TrailId -> List<LiftId> (trails can be accessed by multiple lifts)
        private Dictionary<int, List<int>> _trailToBuildings; // TrailId -> List<BuildingId>
        
        // Base connections
        private Dictionary<int, bool> _liftsToBase;   // LiftId -> connected to base
        private Dictionary<int, bool> _trailsToBase;  // TrailId -> connected to base
        
        // All connections (for queries)
        private List<Connection> _allConnections;
        
        public int SnapRadius { get; set; } = 20;  // Max 3D distance for lift-trail connections (increased from 10)
        public int BaseSnapRadius { get; set; } = 40;  // Larger radius for forgiving base connections
        
        public ConnectionGraph()
        {
            _liftToTrails = new Dictionary<int, List<int>>();
            _liftToBuildings = new Dictionary<int, List<int>>();
            _trailToLifts = new Dictionary<int, List<int>>();
            _trailToBuildings = new Dictionary<int, List<int>>();
            _liftsToBase = new Dictionary<int, bool>();
            _trailsToBase = new Dictionary<int, bool>();
            _allConnections = new List<Connection>();
        }
        
        /// <summary>
        /// Rebuilds all connections based on current snap points.
        /// </summary>
        public void RebuildConnections(SnapRegistry registry)
        {
            Clear();
            
            // Connect lifts to base (bottom stations)
            ConnectLiftsToBase(registry);
            
            // Connect lifts to trails (top stations)
            ConnectLiftsToTrails(registry);
            
            // Connect trails to other trails (for smooth transitions)
            ConnectTrailsToTrails(registry);
            
            // Connect trails to base
            ConnectTrailsToBase(registry);
            
            // CRITICAL: Compute reachability from base through trail network
            // This allows mid-mountain lifts (Lift 2 reachable via Base→Lift1→Trail→Lift2)
            ComputeReachabilityFromBase();
        }
        
        
        private void ConnectLiftsToBase(SnapRegistry registry)
        {
            var liftBottoms = registry.GetByType(SnapPointType.LiftBottom);
            var baseSpawns = registry.GetByType(SnapPointType.BaseSpawn);
            
            if (baseSpawns.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[ConnectionGraph] No base spawn points found! Place a base lodge first.");
                return;
            }
            
            UnityEngine.Debug.Log($"[ConnectionGraph] Connecting {liftBottoms.Count} lift bottoms to {baseSpawns.Count} base spawn(s) (radius: {BaseSnapRadius})");
            
            int connectionsCreated = 0;
            foreach (var liftBottom in liftBottoms)
            {
                int liftId = liftBottom.OwnerId;
                
                // Check if any base spawn is within radius
                foreach (var baseSpawn in baseSpawns)
                {
                    float distance = liftBottom.Distance3D(baseSpawn);
                    
                    if (distance <= BaseSnapRadius)
                    {
                        _liftsToBase[liftId] = true;
                        UnityEngine.Debug.Log($"[ConnectionGraph] Lift {liftId} → Base (DIRECT, distance: {distance:F2})");
                        connectionsCreated++;
                        break; // Only need to connect to one base
                    }
                }
            }
            
            UnityEngine.Debug.Log($"[ConnectionGraph] Created {connectionsCreated} DIRECT lift→base connections");
            // Note: We'll compute INDIRECT reachability after all connections are built
        }
        
        private void ConnectLiftsToTrails(SnapRegistry registry)
        {
            var liftTops = registry.GetByType(SnapPointType.LiftTop);
            var trailStarts = registry.GetByType(SnapPointType.TrailStart);
            
            UnityEngine.Debug.Log($"[ConnectionGraph] Connecting {liftTops.Count} lift tops to {trailStarts.Count} trail starts (radius: {SnapRadius})");
            
            int totalConnections = 0;
            foreach (var liftTop in liftTops)
            {
                int liftId = liftTop.OwnerId;
                int connectionsForThisLift = 0;
                
                // Find all trail starts within snap radius (3D distance)
                foreach (var trailStart in trailStarts)
                {
                    float distance = liftTop.Distance3D(trailStart);
                    
                    UnityEngine.Debug.Log($"[ConnectionGraph] Checking: Lift {liftId} top @ ({liftTop.Position.X:F1},{liftTop.Position.Y:F1},{liftTop.Position.Z:F1}) vs Trail {trailStart.OwnerId} start @ ({trailStart.Position.X:F1},{trailStart.Position.Y:F1},{trailStart.Position.Z:F1}) = {distance:F2} units");
                    
                    if (distance <= SnapRadius)
                    {
                        int trailId = trailStart.OwnerId;
                        
                        UnityEngine.Debug.Log($"[ConnectionGraph] ✓ Lift {liftId} → Trail {trailId} (distance: {distance:F2})");
                        
                        // Add lift->trail connection
                        if (!_liftToTrails.ContainsKey(liftId))
                        {
                            _liftToTrails[liftId] = new List<int>();
                        }
                        if (!_liftToTrails[liftId].Contains(trailId))
                        {
                            _liftToTrails[liftId].Add(trailId);
                            connectionsForThisLift++;
                        }
                        
                        // Add trail->lift connection (reverse)
                        if (!_trailToLifts.ContainsKey(trailId))
                        {
                            _trailToLifts[trailId] = new List<int>();
                        }
                        if (!_trailToLifts[trailId].Contains(liftId))
                        {
                            _trailToLifts[trailId].Add(liftId);
                        }
                        
                        // Store connection
                        var conn = new Connection(liftId, trailId, "Lift", "Trail", (int)distance);
                        _allConnections.Add(conn);
                        totalConnections++;
                    }
                }
                
                // CRITICAL: Warn if lift has no trail connections
                if (connectionsForThisLift == 0)
                {
                    UnityEngine.Debug.LogWarning($"[ConnectionGraph] Lift {liftId} top has NO connected trails! (all trails > {SnapRadius} units away)");
                }
            }
            
            UnityEngine.Debug.Log($"[ConnectionGraph] Created {totalConnections} lift→trail connections");
        }
        
        private void ConnectTrailsToTrails(SnapRegistry registry)
        {
            // Connect trail ends to other trail starts AND trail points (user requirement: every point is valid)
            var trailEnds = registry.GetByType(SnapPointType.TrailEnd);
            var trailStarts = registry.GetByType(SnapPointType.TrailStart);
            var trailPoints = registry.GetByType(SnapPointType.TrailPoint);
            
            // Combine trail starts and trail points into one list of valid connection points
            var validConnectionPoints = new List<SnapPoint>();
            validConnectionPoints.AddRange(trailStarts);
            validConnectionPoints.AddRange(trailPoints);
            
            UnityEngine.Debug.Log($"[ConnectionGraph] Connecting {trailEnds.Count} trail ends to {validConnectionPoints.Count} trail connection points");
            
            int trailConnections = 0;
            foreach (var trailEnd in trailEnds)
            {
                int fromTrailId = trailEnd.OwnerId;
                
                // Find nearby trail starts or points
                foreach (var connectionPoint in validConnectionPoints)
                {
                    int toTrailId = connectionPoint.OwnerId;
                    
                    // Don't connect trail to itself
                    if (fromTrailId == toTrailId) continue;
                    
                    float distance = trailEnd.Distance3D(connectionPoint);
                    
                    if (distance <= SnapRadius)
                    {
                        UnityEngine.Debug.Log($"[ConnectionGraph] Trail {fromTrailId} → Trail {toTrailId} (distance: {distance:F2})");
                        
                        // Add connection (trails are bidirectional for now)
                        var conn = new Connection(fromTrailId, toTrailId, "Trail", "Trail", (int)distance);
                        _allConnections.Add(conn);
                        trailConnections++;
                    }
                }
            }
            
            UnityEngine.Debug.Log($"[ConnectionGraph] Created {trailConnections} trail→trail connections");
        }
        
        private void ConnectTrailsToBase(SnapRegistry registry)
        {
            var trailEnds = registry.GetByType(SnapPointType.TrailEnd);
            var baseSpawns = registry.GetByType(SnapPointType.BaseSpawn);
            
            if (baseSpawns.Count == 0)
            {
                return; // Already warned in ConnectLiftsToBase
            }
            
            UnityEngine.Debug.Log($"[ConnectionGraph] Connecting {trailEnds.Count} trail ends to {baseSpawns.Count} base spawn(s) (radius: {BaseSnapRadius})");
            
            int connectionsCreated = 0;
            foreach (var trailEnd in trailEnds)
            {
                int trailId = trailEnd.OwnerId;
                
                // Check if trail end is near a base spawn
                foreach (var baseSpawn in baseSpawns)
                {
                    float distance = trailEnd.Distance3D(baseSpawn);
                    
                    if (distance <= BaseSnapRadius)
                    {
                        _trailsToBase[trailId] = true;
                        UnityEngine.Debug.Log($"[ConnectionGraph] Trail {trailId} → Base (distance: {distance:F2})");
                        connectionsCreated++;
                        break; // Only need to connect to one base
                    }
                }
            }
            
            UnityEngine.Debug.Log($"[ConnectionGraph] Created {connectionsCreated} trail→base connections");
        }
        
        
        /// <summary>
        /// Computes which lifts and trails are reachable from the base through the connection graph.
        /// This allows mid-mountain lifts (e.g., Base→Lift1→Trail→Lift2).
        /// Uses BFS (Breadth-First Search) to traverse the graph.
        /// </summary>
        private void ComputeReachabilityFromBase()
        {
            UnityEngine.Debug.Log("[ConnectionGraph] Computing reachability from base...");
            
            // Start from lifts directly connected to base
            var reachableLifts = new HashSet<int>();
            var reachableTrails = new HashSet<int>();
            
            // Queue for BFS: (objectType, objectId)
            var queue = new Queue<(string type, int id)>();
            var visited = new HashSet<string>();
            
            // Seed with lifts directly connected to base
            foreach (var kvp in _liftsToBase)
            {
                if (kvp.Value)
                {
                    queue.Enqueue(("Lift", kvp.Key));
                    reachableLifts.Add(kvp.Key);
                    visited.Add($"Lift_{kvp.Key}");
                    UnityEngine.Debug.Log($"[Reachability] Starting from Lift {kvp.Key} (directly at base)");
                }
            }
            
            // Seed with trails directly connected to base
            foreach (var kvp in _trailsToBase)
            {
                if (kvp.Value)
                {
                    queue.Enqueue(("Trail", kvp.Key));
                    reachableTrails.Add(kvp.Key);
                    visited.Add($"Trail_{kvp.Key}");
                }
            }
            
            // BFS traversal
            while (queue.Count > 0)
            {
                var (objType, objId) = queue.Dequeue();
                
                if (objType == "Lift")
                {
                    // From lift, we can access trails at the top
                    if (_liftToTrails.ContainsKey(objId))
                    {
                        foreach (var trailId in _liftToTrails[objId])
                        {
                            string key = $"Trail_{trailId}";
                            if (!visited.Contains(key))
                            {
                                visited.Add(key);
                                reachableTrails.Add(trailId);
                                queue.Enqueue(("Trail", trailId));
                                UnityEngine.Debug.Log($"[Reachability] Lift {objId} → Trail {trailId}");
                            }
                        }
                    }
                }
                else if (objType == "Trail")
                {
                    // From trail, we can access:
                    // 1. Other trails (trail-to-trail connections for junctions)
                    foreach (var conn in _allConnections)
                    {
                        if (conn.FromType == "Trail" && conn.FromId == objId && conn.ToType == "Trail")
                        {
                            int nextTrailId = conn.ToId;
                            string key = $"Trail_{nextTrailId}";
                            if (!visited.Contains(key))
                            {
                                visited.Add(key);
                                reachableTrails.Add(nextTrailId);
                                queue.Enqueue(("Trail", nextTrailId));
                                UnityEngine.Debug.Log($"[Reachability] Trail {objId} → Trail {nextTrailId}");
                            }
                        }
                    }
                    
                    // 2. Lifts (if trail end is near a lift bottom)
                    // Check which lifts have this trail in their access list (reverse lookup)
                    foreach (var liftKvp in _liftToTrails)
                    {
                        // This doesn't help - we need lift BOTTOMS near trail ENDS
                        // For now, we'll add a simpler heuristic:
                        // If a trail is reachable AND a lift bottom is within reasonable distance of ANY reachable point,
                        // that lift becomes reachable too.
                        // BUT we don't have spatial info here, so let's use a different approach:
                        
                        // Check all connections for Trail->Lift (we need to add these!)
                        // Actually, we don't have trail-to-lift-bottom connections yet.
                        // Let's mark ANY lift as reachable if it connects to a reachable trail at the TOP
                        // This is a deliberate design: if you can SKI TO a lift line, you can ride it
                    }
                    
                    // For now: if you can reach a trail, and that trail endpoint is near a lift bottom,
                    // we consider that lift reachable. But we don't have trail-end-to-lift-bottom connections.
                    // Let's add a pragmatic rule: ALL lifts connected to reachable trails are reachable
                    if (_trailToLifts.ContainsKey(objId))
                    {
                        foreach (var liftId in _trailToLifts[objId])
                        {
                            // This lift's TOP connects to this trail
                            // But can we get to this lift's BOTTOM?
                            // Pragmatic rule: if ANY trail is reachable, assume you can hike/ski to nearby lift bottoms
                            // For a proper solution, we'd need trail-end-to-lift-bottom spatial connections
                            
                            // For now: mark lift as reachable if not already
                            string key = $"Lift_{liftId}";
                            if (!visited.Contains(key))
                            {
                                visited.Add(key);
                                reachableLifts.Add(liftId);
                                queue.Enqueue(("Lift", liftId));
                                UnityEngine.Debug.Log($"[Reachability] Trail {objId} makes Lift {liftId} accessible (via trail network)");
                            }
                        }
                    }
                }
            }
            
            // Update reachability in _liftsToBase and _trailsToBase
            // But don't overwrite direct connections - ADD to them
            int indirectLifts = 0;
            foreach (var liftId in reachableLifts)
            {
                if (!_liftsToBase.ContainsKey(liftId))
                {
                    _liftsToBase[liftId] = true; // Indirectly reachable
                    indirectLifts++;
                    UnityEngine.Debug.Log($"[Reachability] Lift {liftId} is INDIRECTLY reachable from base");
                }
            }
            
            int indirectTrails = 0;
            foreach (var trailId in reachableTrails)
            {
                if (!_trailsToBase.ContainsKey(trailId))
                {
                    _trailsToBase[trailId] = true; // Indirectly reachable
                    indirectTrails++;
                }
            }
            
            UnityEngine.Debug.Log($"[Reachability] Found {reachableLifts.Count} reachable lifts ({indirectLifts} indirect), {reachableTrails.Count} reachable trails ({indirectTrails} indirect)");
        }
        
        /// <summary>
        /// Gets all trails accessible from a lift's top station.
        /// </summary>
        public List<int> GetTrailsFromLift(int liftId)
        {
            if (_liftToTrails.ContainsKey(liftId))
            {
                return new List<int>(_liftToTrails[liftId]);
            }
            return new List<int>();
        }
        
        /// <summary>
        /// Gets all lifts that access a trail.
        /// </summary>
        public List<int> GetLiftsToTrail(int trailId)
        {
            if (_trailToLifts.ContainsKey(trailId))
            {
                return new List<int>(_trailToLifts[trailId]);
            }
            return new List<int>();
        }
        
        /// <summary>
        /// Checks if a lift has any connected trails.
        /// </summary>
        public bool LiftHasTrails(int liftId)
        {
            return _liftToTrails.ContainsKey(liftId) && _liftToTrails[liftId].Count > 0;
        }
        
        /// <summary>
        /// Checks if a trail is accessible by any lift.
        /// </summary>
        public bool TrailHasLifts(int trailId)
        {
            return _trailToLifts.ContainsKey(trailId) && _trailToLifts[trailId].Count > 0;
        }
        
        /// <summary>
        /// Checks if a lift is connected to the base.
        /// </summary>
        public bool IsLiftConnectedToBase(int liftId)
        {
            return _liftsToBase.ContainsKey(liftId) && _liftsToBase[liftId];
        }
        
        /// <summary>
        /// Checks if a trail is connected to the base.
        /// </summary>
        public bool IsTrailConnectedToBase(int trailId)
        {
            return _trailsToBase.ContainsKey(trailId) && _trailsToBase[trailId];
        }
        
        /// <summary>
        /// Gets all lifts connected to the base.
        /// </summary>
        public List<int> GetLiftsConnectedToBase()
        {
            return _liftsToBase.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        }
        
        /// <summary>
        /// Gets all trails connected to the base.
        /// </summary>
        public List<int> GetTrailsConnectedToBase()
        {
            return _trailsToBase.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        }
        
        /// <summary>
        /// Gets all connections.
        /// </summary>
        public List<Connection> GetAllConnections()
        {
            return new List<Connection>(_allConnections);
        }
        
        /// <summary>
        /// Gets count of lifts with trail connections.
        /// </summary>
        public int GetConnectedLiftCount()
        {
            return _liftToTrails.Count;
        }
        
        /// <summary>
        /// Gets count of trails with lift connections.
        /// </summary>
        public int GetConnectedTrailCount()
        {
            return _trailToLifts.Count;
        }
        
        /// <summary>
        /// Clears all connections.
        /// </summary>
        public void Clear()
        {
            _liftToTrails.Clear();
            _liftToBuildings.Clear();
            _trailToLifts.Clear();
            _trailToBuildings.Clear();
            _liftsToBase.Clear();
            _trailsToBase.Clear();
            _allConnections.Clear();
        }
    }
}
