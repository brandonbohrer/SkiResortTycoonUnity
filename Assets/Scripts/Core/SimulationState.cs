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
        /// <summary>
        /// Scales target on-mountain skiers and visitor arrival rate on powder day (Accepted only); 1 = normal.
        /// </summary>
        public float PowderDayActiveSkierTargetMultiplier { get; set; } = 1f;
        /// <summary>True after the one-time intro modal (days 3–6) is resolved or skipped.</summary>
        public bool PowderIntroCompleted { get; set; }
        /// <summary>Random cosmetic powder (white trees + snow) for this day only; no modal or economy.</summary>
        public bool VisualPowderDayActive { get; set; }

        // ── Injury lawsuit (RandomEventController) ─────────────────────────
        /// <summary>True after the player resolves any lawsuit modal (settle or court).</summary>
        public bool LawsuitFirstEventCompleted { get; set; }
        /// <summary>
        /// First lawsuit is guaranteed on the first qualifying fall on or after this day (rolled once, typically 10–15).
        /// 0 = not scheduled yet.
        /// </summary>
        public int LawsuitGuaranteedTargetDay { get; set; }

        // ── Lift build research (Mountain Manager → Research tab) ─────────────
        /// <summary>Unlocks beyond 1-seat low speed (always available).</summary>
        public bool UnlockedLiftOneSeatHighSpeed { get; set; }
        public bool UnlockedLiftTwoSeatLowSpeed { get; set; }
        public bool UnlockedLiftTwoSeatHighSpeed { get; set; }

        public bool LiftResearchSlot0Done { get; set; }
        public bool LiftResearchSlot1Done { get; set; }
        public bool LiftResearchSlot2Done { get; set; }

        /// <summary>Calendar day index when the active project completes; -1 if idle.</summary>
        public int LiftResearchSlot0CompletionDay { get; set; } = -1;
        public int LiftResearchSlot1CompletionDay { get; set; } = -1;
        public int LiftResearchSlot2CompletionDay { get; set; } = -1;

        /// <summary><see cref="LiftType"/> as int while research runs; -1 if none.</summary>
        public int LiftResearchSlot0PendingUnlockType { get; set; } = -1;
        public int LiftResearchSlot1PendingUnlockType { get; set; } = -1;
        public int LiftResearchSlot2PendingUnlockType { get; set; } = -1;

        public int LiftResearchSlot0PaidAmount { get; set; }
        public int LiftResearchSlot1PaidAmount { get; set; }
        public int LiftResearchSlot2PaidAmount { get; set; }

        public int GetLiftResearchCompletionDay(int slot)
        {
            switch (slot)
            {
                case 0: return LiftResearchSlot0CompletionDay;
                case 1: return LiftResearchSlot1CompletionDay;
                case 2: return LiftResearchSlot2CompletionDay;
                default: return -1;
            }
        }

        public int GetLiftResearchPendingUnlockType(int slot)
        {
            switch (slot)
            {
                case 0: return LiftResearchSlot0PendingUnlockType;
                case 1: return LiftResearchSlot1PendingUnlockType;
                case 2: return LiftResearchSlot2PendingUnlockType;
                default: return -1;
            }
        }

        public void ClearLiftResearchInProgress(int slot)
        {
            switch (slot)
            {
                case 0:
                    LiftResearchSlot0CompletionDay = -1;
                    LiftResearchSlot0PendingUnlockType = -1;
                    LiftResearchSlot0PaidAmount = 0;
                    break;
                case 1:
                    LiftResearchSlot1CompletionDay = -1;
                    LiftResearchSlot1PendingUnlockType = -1;
                    LiftResearchSlot1PaidAmount = 0;
                    break;
                case 2:
                    LiftResearchSlot2CompletionDay = -1;
                    LiftResearchSlot2PendingUnlockType = -1;
                    LiftResearchSlot2PaidAmount = 0;
                    break;
            }
        }

        public void SetLiftResearchSlotDone(int slot, bool done)
        {
            switch (slot)
            {
                case 0: LiftResearchSlot0Done = done; break;
                case 1: LiftResearchSlot1Done = done; break;
                case 2: LiftResearchSlot2Done = done; break;
            }
        }
    }
}


