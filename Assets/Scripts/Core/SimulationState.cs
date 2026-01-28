namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# state container for the simulation.
    /// No Unity types allowed.
    /// </summary>
    public class SimulationState
    {
        // Core state
        public int DayIndex { get; set; } = 1;
        public float TimeMinutes { get; set; } = 540f; // Start at 9:00 AM
        public int VisitorsToday { get; set; } = 0;
        public int Money { get; set; } = 15000;
        
        // Infrastructure counts (updated by systems)
        public int LiftsBuilt { get; set; } = 0;
        public int TrailsBuilt { get; set; } = 0;
    }
}

