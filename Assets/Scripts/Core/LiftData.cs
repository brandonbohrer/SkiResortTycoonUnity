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
        public TileCoord BottomStation { get; set; }
        public TileCoord TopStation { get; set; }
        public LiftType Type { get; set; }
        public int Length { get; set; } // Horizontal distance in tiles
        public int ElevationGain { get; set; } // Height difference
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

