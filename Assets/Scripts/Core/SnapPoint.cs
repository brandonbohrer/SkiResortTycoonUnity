namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Types of snap points for connections.
    /// </summary>
    public enum SnapPointType
    {
        LiftBottom = 0,
        LiftTop = 1,
        TrailStart = 2,
        TrailEnd = 3,
        BuildingEntrance = 4,
        BaseSpawn = 5  // Special: where visitors spawn at start of day
    }
    
    /// <summary>
    /// A connection point on the grid.
    /// Pure C# struct - no Unity types.
    /// </summary>
    public struct SnapPoint
    {
        public SnapPointType Type { get; set; }
        public TileCoord Coord { get; set; }
        public int OwnerId { get; set; }  // ID of the lift/trail/building that owns this
        public string OwnerName { get; set; }  // Human-readable name for debugging
        
        public SnapPoint(SnapPointType type, TileCoord coord, int ownerId, string ownerName = "")
        {
            Type = type;
            Coord = coord;
            OwnerId = ownerId;
            OwnerName = ownerName;
        }
        
        /// <summary>
        /// Manhattan distance to another snap point.
        /// </summary>
        public int DistanceTo(SnapPoint other)
        {
            return System.Math.Abs(Coord.X - other.Coord.X) + System.Math.Abs(Coord.Y - other.Coord.Y);
        }
        
        /// <summary>
        /// Manhattan distance to a tile coordinate.
        /// </summary>
        public int DistanceTo(TileCoord coord)
        {
            return System.Math.Abs(Coord.X - coord.X) + System.Math.Abs(Coord.Y - coord.Y);
        }
        
        public override string ToString()
        {
            return $"[{Type}] {OwnerName} @ {Coord}";
        }
    }
}

