using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Central financial orchestrator for the resort.
    /// Manages ticket pricing, expenses, resort valuation, and daily financial history.
    /// Money is ONLY granted/deducted once per day at the end of the day.
    /// </summary>
    public class EconomySystem
    {
        // ── Sub-systems ─────────────────────────────────────────────────
        private TicketPricing _ticketPricing;
        private ExpenseTracker _expenseTracker;
        private ResortValuation _resortValuation;
        
        // ── State ───────────────────────────────────────────────────────
        private float _currentFairPrice = 50f;
        private DailyFinancialRecord _todayRecord;
        private List<DailyFinancialRecord> _history;
        
        // ── Public accessors ────────────────────────────────────────────
        public TicketPricing TicketPricing => _ticketPricing;
        public ExpenseTracker ExpenseTracker => _expenseTracker;
        public ResortValuation ResortValuation => _resortValuation;
        public float CurrentFairPrice => _currentFairPrice;
        public DailyFinancialRecord TodayRecord => _todayRecord;
        public List<DailyFinancialRecord> History => _history;
        
        public EconomySystem()
        {
            _ticketPricing = new TicketPricing();
            _expenseTracker = new ExpenseTracker();
            _resortValuation = new ResortValuation();
            _history = new List<DailyFinancialRecord>();
        }
        
        /// <summary>
        /// Recalculates fair price from current infrastructure.
        /// Call this whenever infrastructure changes or at the start of each day.
        /// </summary>
        public void UpdateFairPrice(
            int liftCount, int trailCount,
            List<LodgeAmenityInfo> lodgeAmenities,
            int distinctDifficultyCount)
        {
            _currentFairPrice = _resortValuation.CalculateFairPrice(
                liftCount, trailCount, lodgeAmenities, distinctDifficultyCount);
        }
        
        /// <summary>
        /// Gets the current demand multiplier based on ticket price vs fair price.
        /// Used by VisitorSystem.PriceMultiplier each tick.
        /// </summary>
        public float GetDemandMultiplier()
        {
            return _ticketPricing.GetDemandMultiplier(_currentFairPrice);
        }
        
        /// <summary>
        /// Gets the current price ratio (ticketPrice / fairPrice).
        /// Used to set SkierNeeds.TicketPriceRatio at spawn.
        /// </summary>
        public float GetPriceRatio()
        {
            return _ticketPricing.GetPriceRatio(_currentFairPrice);
        }
        
        /// <summary>
        /// Processes all end-of-day financial logic.
        /// Calculates revenue, expenses, net income, applies to state money,
        /// creates a DailyFinancialRecord, and stores it in history.
        /// </summary>
        /// <param name="state">Simulation state (money will be modified).</param>
        /// <param name="liftCount">Number of active lifts.</param>
        /// <param name="trailCount">Number of active trails.</param>
        /// <param name="lodgeCount">Number of active lodges.</param>
        /// <param name="lodgeRevenue">Total lodge revenue collected by all lodges today.</param>
        /// <returns>The financial record for this day.</returns>
        public DailyFinancialRecord ProcessEndOfDay(
            SimulationState state,
            int liftCount, int trailCount, int lodgeCount,
            float lodgeRevenue)
        {
            // 1. Ticket revenue = visitors * ticket price
            float ticketRevenue = state.VisitorsToday * _ticketPricing.TicketPrice;
            
            // 2. Lodge revenue = pass-through from Unity layer
            // (already collected per-visit by LodgeFacility)
            
            // 3. Calculate expenses
            DailyExpenses expenses = _expenseTracker.Calculate(liftCount, lodgeCount, trailCount);
            
            // 4. Create record
            _todayRecord = new DailyFinancialRecord(
                dayIndex: state.DayIndex,
                visitorCount: state.VisitorsToday,
                fairPrice: _currentFairPrice,
                ticketPrice: _ticketPricing.TicketPrice,
                ticketRevenue: ticketRevenue,
                lodgeRevenue: lodgeRevenue,
                liftExpenses: expenses.LiftExpenses,
                lodgeExpenses: expenses.LodgeExpenses,
                trailExpenses: expenses.TrailExpenses
            );
            
            // 5. Apply net income to state money
            state.Money += (int)_todayRecord.NetIncome;
            
            // 6. Store in history
            _history.Add(_todayRecord);
            
            return _todayRecord;
        }
        
        /// <summary>
        /// Resets all lodge revenue tracking. Call after end-of-day processing.
        /// Lodge revenue is tracked per-lodge on LodgePricing — the caller
        /// (SimulationRunner) should reset those after collecting the total.
        /// </summary>
        public void ResetDailyTracking()
        {
            // Nothing to reset on EconomySystem itself — lodge pricing
            // reset happens in the Unity layer (SimulationRunner).
        }
    }
}
