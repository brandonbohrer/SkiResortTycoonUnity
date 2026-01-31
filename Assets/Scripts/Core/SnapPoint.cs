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
        TrailPoint = 4,  // Any point along a trail (user requirement: every point is valid)
        BuildingEntrance = 5,
        BaseSpawn = 6  // Special: where visitors spawn at start of day
    }
    
    /// <summary>
    /// A connection point on the grid.
    /// Pure C# struct - no Unity types.
    /// </summary>
    public struct SnapPoint
    {
        public SnapPointType Type { get; set; }
        public TileCoord Coord { get; set; }
        public Vector3f Position { get; set; }  // 3D world position (authoritative)
        public int OwnerId { get; set; }  // ID of the lift/trail/building that owns this
        public string OwnerName { get; set; }  // Human-readable name for debugging
        
        public SnapPoint(SnapPointType type, TileCoord coord, int ownerId = 0, string ownerName = "")
        {
            Type = type;
            Coord = coord;
            Position = new Vector3f(coord.X, 0, coord.Y); // Legacy: convert tile to 3D
            OwnerId = ownerId;
            OwnerName = ownerName;
        }
        
        // 3D constructor (preferred for new code)
        public SnapPoint(SnapPointType type, Vector3f position, int ownerId = 0, string ownerName = "")
        {
            Type = type;
            Position = position;
            Coord = new TileCoord((int)position.X, (int)position.Z); // Convert 3D to legacy tile coord
            OwnerId = ownerId;
            OwnerName = ownerName;
        }
        
        /// <summary>
        /// Manhattan distance to another snap point (legacy 2D).
        /// </summary>
        public int DistanceTo(SnapPoint other)
        {
            return System.Math.Abs(Coord.X - other.Coord.X) + System.Math.Abs(Coord.Y - other.Coord.Y);
        }
        
        /// <summary>
        /// 3D Euclidean distance to another snap point.
        /// </summary>
        public float Distance3D(SnapPoint other)
        {
            float dx = Position.X - other.Position.X;
            float dy = Position.Y - other.Position.Y;
            float dz = Position.Z - other.Position.Z;
            return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
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

