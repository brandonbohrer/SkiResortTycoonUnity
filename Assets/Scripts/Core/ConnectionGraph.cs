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
        public string FromType { get; set; }  // "Lift", "Trail", "Building"
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
        
        // All connections (for queries)
        private List<Connection> _allConnections;
        
        public int SnapRadius { get; set; } = 10;  // Max 3D distance for auto-connection (world units)
        
        public ConnectionGraph()
        {
            _liftToTrails = new Dictionary<int, List<int>>();
            _liftToBuildings = new Dictionary<int, List<int>>();
            _trailToLifts = new Dictionary<int, List<int>>();
            _trailToBuildings = new Dictionary<int, List<int>>();
            _allConnections = new List<Connection>();
        }
        
        /// <summary>
        /// Rebuilds all connections based on current snap points.
        /// </summary>
        public void RebuildConnections(SnapRegistry registry)
        {
            Clear();
            
            // Connect lifts to trails
            ConnectLiftsToTrails(registry);
            
            // Connect trails to other trails (for smooth transitions)
            ConnectTrailsToTrails(registry);
            
            // Connect trails to base (future: buildings)
            ConnectTrailsToBase(registry);
        }
        
        private void ConnectLiftsToTrails(SnapRegistry registry)
        {
            var liftTops = registry.GetByType(SnapPointType.LiftTop);
            var trailStarts = registry.GetByType(SnapPointType.TrailStart);
            
            UnityEngine.Debug.Log($"[ConnectionGraph] Connecting {liftTops.Count} lift tops to {trailStarts.Count} trail starts (radius: {SnapRadius})");
            
            foreach (var liftTop in liftTops)
            {
                int liftId = liftTop.OwnerId;
                
                // Find all trail starts within snap radius (3D distance)
                foreach (var trailStart in trailStarts)
                {
                    float distance = liftTop.Distance3D(trailStart);
                    
                    if (distance <= SnapRadius)
                    {
                        int trailId = trailStart.OwnerId;
                        
                        UnityEngine.Debug.Log($"[ConnectionGraph] Lift {liftId} → Trail {trailId} (distance: {distance:F2})");
                        
                        // Add lift->trail connection
                        if (!_liftToTrails.ContainsKey(liftId))
                        {
                            _liftToTrails[liftId] = new List<int>();
                        }
                        if (!_liftToTrails[liftId].Contains(trailId))
                        {
                            _liftToTrails[liftId].Add(trailId);
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
                    }
                }
            }
            
            UnityEngine.Debug.Log($"[ConnectionGraph] Created {_allConnections.Count} lift→trail connections");
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
            // Future: connect trail ends to building entrances or base spawn
            // For now, just log that trail ends exist
            var trailEnds = registry.GetByType(SnapPointType.TrailEnd);
            var baseSpawns = registry.GetByType(SnapPointType.BaseSpawn);
            
            foreach (var trailEnd in trailEnds)
            {
                int trailId = trailEnd.OwnerId;
                
                // Check if trail end is near a base spawn
                foreach (var baseSpawn in baseSpawns)
                {
                    int distance = trailEnd.DistanceTo(baseSpawn);
                    
                    if (distance <= SnapRadius)
                    {
                        // Trail connects to base
                        // (Future: store this connection)
                    }
                }
            }
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
            _allConnections.Clear();
        }
    }
}

