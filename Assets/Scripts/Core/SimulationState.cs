namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# state container for the simulation.
    /// No Unity types allowed.
    /// </summary>
    public class SimulationState
    {
        /// <summary>Starting cash for a new game (must match empty save creation).</summary>
        public const int DefaultStartingMoney = 250000;

        // Core state
        public int DayIndex { get; set; } = 1;
        public float TimeMinutes { get; set; } = 540f; // Start at 9:00 AM
        public int VisitorsToday { get; set; } = 0;   // Cumulative arrivals today (for economy / end-of-day)
        public int ActiveSkierCount { get; set; } = 0; // Current skiers on mountain (set by Unity bridge for display)
        public int Money { get; set; } = DefaultStartingMoney;
        
        // Infrastructure counts (updated by systems)
        public int LiftsBuilt { get; set; } = 0;
        public int TrailsBuilt { get; set; } = 0;
        public int LodgesBuilt { get; set; } = 0;

        // Today's running financials (reset each day, updated by EconomySystem)
        public float TodayRevenue      { get; set; } = 0f;
        public float TodayExpenses     { get; set; } = 0f;
        public float TodayLodgeRevenue { get; set; } = 0f;  // accumulated by LodgeManager per visit
        public float TodayNetProfit    => TodayRevenue - TodayExpenses;

        // Demand progression state (persistent S-curve acceleration)
        public float DemandMomentum { get; set; } = 0f;              // 0..1, grows with consistent good operations
        public int ConsecutiveStrongServiceDays { get; set; } = 0;   // streak of high quality days
        public float SmoothedTargetActiveSkiers { get; set; } = 0f;  // persisted target smoothing anchor

        // ── Powder Day (one random morning between days 3–6) ─────────────────
        /// <summary>Which calendar day gets the event; 0 = not scheduled yet (roll at runtime).</summary>
        public int PowderDayTargetDay { get; set; } = 0;
        /// <summary>True after the morning modal was dismissed (choice may still be active that day).</summary>
        public bool PowderDayModalDone { get; set; }
        public PowderDayChoice ActivePowderChoice { get; set; } = PowderDayChoice.None;
        /// <summary>Extra demand multiplier from ticket pricing / buzz (applied on top of economy demand).</summary>
        public float PowderDemandEventMultiplier { get; set; } = 1f;
        /// <summary>Multiplier on resort satisfaction-driven visitor draw (crowding stress).</summary>
        public float PowderSatisfactionEventMultiplier { get; set; } = 1f;
    }
}


