namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Types of lifts (for future expansion).
    /// </summary>
    public enum LiftType
    {
        ChairLift = 0,
        Gondola = 1,
        TSBar = 2
    }
    
    /// <summary>
    /// Pure C# representation of a ski lift.
    /// No Unity types.
    /// </summary>
    public class LiftData
    {
        public int LiftId { get; set; }
        public string Name { get; set; }
        
        // World-space positions (authoritative for rendering and gameplay)
        public Vector3f StartPosition { get; set; } // Bottom station position on mountain mesh
        public Vector3f EndPosition { get; set; }   // Top station position on mountain mesh
        
        // Legacy grid coordinates (kept for backwards compatibility with existing systems)
        public TileCoord BottomStation { get; set; }
        public TileCoord TopStation { get; set; }
        
        public LiftType Type { get; set; }
        public float Length { get; set; } // 3D distance between start and end
        public float ElevationGain { get; set; } // Height difference (EndPosition.Y - StartPosition.Y)
        public int Capacity { get; set; } // Riders per hour
        public int BuildCost { get; set; }
        public bool IsValid { get; set; }
        
        public LiftData(int liftId)
        {
            LiftId = liftId;
            Name = $"Lift {liftId}";
            Type = LiftType.ChairLift;
            Capacity = 1000; // Default capacity
            IsValid = false;
        }
    }
}

