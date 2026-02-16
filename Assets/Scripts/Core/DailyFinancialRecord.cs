namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Immutable snapshot of one day's financial results.
    /// Created by EconomySystem.ProcessEndOfDay and stored in history.
    /// </summary>
    public class DailyFinancialRecord
    {
        // ── Context ─────────────────────────────────────────────────────
        public int DayIndex { get; }
        public int VisitorCount { get; }
        public float FairPrice { get; }
        public float TicketPrice { get; }
        
        // ── Revenue ─────────────────────────────────────────────────────
        public float TicketRevenue { get; }
        public float LodgeRevenue { get; }
        public float TotalRevenue => TicketRevenue + LodgeRevenue;
        
        // ── Expenses ────────────────────────────────────────────────────
        public float LiftExpenses { get; }
        public float LodgeExpenses { get; }
        public float TrailExpenses { get; }
        public float TotalExpenses => LiftExpenses + LodgeExpenses + TrailExpenses;
        
        // ── Bottom line ─────────────────────────────────────────────────
        public float NetIncome => TotalRevenue - TotalExpenses;
        
        public DailyFinancialRecord(
            int dayIndex, int visitorCount,
            float fairPrice, float ticketPrice,
            float ticketRevenue, float lodgeRevenue,
            float liftExpenses, float lodgeExpenses, float trailExpenses)
        {
            DayIndex = dayIndex;
            VisitorCount = visitorCount;
            FairPrice = fairPrice;
            TicketPrice = ticketPrice;
            TicketRevenue = ticketRevenue;
            LodgeRevenue = lodgeRevenue;
            LiftExpenses = liftExpenses;
            LodgeExpenses = lodgeExpenses;
            TrailExpenses = trailExpenses;
        }
    }
}
