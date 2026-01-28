namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Central coordinator for snap points and connections.
    /// Holds the registry and connection graph, rebuilds connections as needed.
    /// Pure C# - no Unity types.
    /// </summary>
    public class WorldConnectivity
    {
        public SnapRegistry Registry { get; private set; }
        public ConnectionGraph Connections { get; private set; }
        
        public int SnapRadius { get; set; } = 2;  // Max Manhattan distance for auto-connection
        
        public WorldConnectivity()
        {
            Registry = new SnapRegistry();
            Connections = new ConnectionGraph();
            Connections.SnapRadius = SnapRadius;
        }
        
        /// <summary>
        /// Rebuilds all connections based on current snap points.
        /// Call this after placing/removing lifts, trails, or buildings.
        /// </summary>
        public void RebuildConnections()
        {
            Connections.SnapRadius = SnapRadius;
            Connections.RebuildConnections(Registry);
        }
        
        /// <summary>
        /// Gets debug info about connectivity status.
        /// </summary>
        public string GetDebugInfo()
        {
            int totalSnaps = Registry.GetTotalCount();
            int liftBottoms = Registry.GetCount(SnapPointType.LiftBottom);
            int liftTops = Registry.GetCount(SnapPointType.LiftTop);
            int trailStarts = Registry.GetCount(SnapPointType.TrailStart);
            int trailEnds = Registry.GetCount(SnapPointType.TrailEnd);
            
            int connectedLifts = Connections.GetConnectedLiftCount();
            int connectedTrails = Connections.GetConnectedTrailCount();
            int totalConnections = Connections.GetAllConnections().Count;
            
            return $"Snap Points: {totalSnaps} (LiftBottom:{liftBottoms}, LiftTop:{liftTops}, TrailStart:{trailStarts}, TrailEnd:{trailEnds})\n" +
                   $"Connections: {totalConnections} total ({connectedLifts} lifts connected, {connectedTrails} trails connected)";
        }
    }
}

