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
        public int VisitorsToday { get; set; } = 0;   // Cumulative arrivals today (for economy / end-of-day)
        public int ActiveSkierCount { get; set; } = 0; // Current skiers on mountain (set by Unity bridge for display)
        public int Money { get; set; } = 1000000;
        
        // Infrastructure counts (updated by systems)
        public int LiftsBuilt { get; set; } = 0;
        public int TrailsBuilt { get; set; } = 0;

        // Today's running financials (reset each day, updated by EconomySystem)
        public float TodayRevenue      { get; set; } = 0f;
        public float TodayExpenses     { get; set; } = 0f;
        public float TodayLodgeRevenue { get; set; } = 0f;  // accumulated by LodgeManager per visit
        public float TodayNetProfit    => TodayRevenue - TodayExpenses;
    }
}

