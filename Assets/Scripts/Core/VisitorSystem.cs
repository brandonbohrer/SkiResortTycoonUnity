namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# visitor accumulation system.
    /// Tracks fractional visitors and converts to whole visitors.
    /// Also computes the target active skier count for the visual layer.
    /// </summary>
    public class VisitorSystem
    {
        private float _fractionalVisitors = 0f;
        
        // Configuration: visitors per minute based on infrastructure
        private float _baseVisitorsPerMinute = 0.38f;
        private float _visitorsPerLift = 0.20f;
        private float _visitorsPerTrail = 0.14f;
        private float _visitorsPerLodge = 0.10f;
        
        // Satisfaction multiplier (set by Simulation)
        public float SatisfactionMultiplier { get; set; } = 1.0f;
        
        // Price demand multiplier (set by Simulation from TicketPricing)
        public float PriceMultiplier { get; set; } = 1.0f;
        
        // Lodge count for infrastructure scoring (set by Simulation each tick)
        public int LodgeCount { get; set; } = 0;
        
        /// <summary>
        /// The raw visitors-per-minute rate before multipliers.
        /// Represents the infrastructure capacity of the resort.
        /// </summary>
        public float BaseVisitorRate { get; private set; }
        
        /// <summary>
        /// The effective visitors-per-minute rate after all multipliers.
        /// </summary>
        public float EffectiveVisitorRate { get; private set; }
        
        /// <summary>
        /// Target number of skiers that should be visually active on the mountain.
        /// Driven by infrastructure size, satisfaction, and pricing.
        /// The visual layer should smoothly approach this target.
        /// 
        /// Formula: baseCapacity (from infrastructure) * satisfaction * pricing
        /// baseCapacity scales with lifts/trails/lodges, with diminishing returns.
        /// </summary>
        public int TargetActiveSkiers { get; private set; } = 10;
        
        // Skier capacity parameters
        private const int MIN_SKIERS = 4;
        private const int HARD_CAP = 300;
        private const float SKIERS_PER_LIFT = 10f;
        private const float SKIERS_PER_TRAIL = 5f;
        private const float SKIERS_PER_LODGE = 3f;
        private const float BASE_SKIERS = 6f;

        // Debug/telemetry visibility for UI and logging
        public float LastRawTarget { get; private set; }
        public float LastFillRate { get; private set; }
        public float LastProgressionBoost { get; private set; }
        public int? ForcedTargetActiveSkiers { get; set; }
        
        /// <summary>
        /// Accumulates visitors based on lifts and trails.
        /// Converts fractional visitors to whole visitors.
        /// Also computes the target active skier count.
        /// </summary>
        public void Tick(SimulationState state, float deltaTime, float speedMinutesPerSecond)
        {
            float minutesPassed = deltaTime * speedMinutesPerSecond;
            
            BaseVisitorRate = _baseVisitorsPerMinute 
                + (state.LiftsBuilt * _visitorsPerLift)
                + (state.TrailsBuilt * _visitorsPerTrail)
                + (LodgeCount * _visitorsPerLodge);
            
            EffectiveVisitorRate = BaseVisitorRate * SatisfactionMultiplier * PriceMultiplier;
            
            _fractionalVisitors += EffectiveVisitorRate * minutesPassed;
            
            int wholeVisitors = (int)_fractionalVisitors;
            if (wholeVisitors > 0)
            {
                state.VisitorsToday += wholeVisitors;
                _fractionalVisitors -= wholeVisitors;
            }
            
            UpdateTargetActiveSkiers(state, minutesPassed);
        }
        
        /// <summary>
        /// Computes how many skiers should be visually on the mountain.
        /// Infrastructure determines the "capacity ceiling" — a big resort CAN hold more.
        /// Satisfaction and pricing determine how full that capacity gets.
        /// </summary>
        private void UpdateTargetActiveSkiers(SimulationState state, float minutesPassed)
        {
            if (ForcedTargetActiveSkiers.HasValue)
            {
                int forced = System.Math.Max(MIN_SKIERS, System.Math.Min(HARD_CAP, ForcedTargetActiveSkiers.Value));
                state.SmoothedTargetActiveSkiers = forced;
                LastRawTarget = forced;
                LastFillRate = 1f;
                LastProgressionBoost = 1f;
                TargetActiveSkiers = forced;
                return;
            }

            float infrastructureCapacity = BASE_SKIERS
                + state.LiftsBuilt * SKIERS_PER_LIFT
                + state.TrailsBuilt * SKIERS_PER_TRAIL
                + LodgeCount * SKIERS_PER_LODGE;

            float fillRate = SatisfactionMultiplier * PriceMultiplier;
            fillRate = System.Math.Max(0.08f, System.Math.Min(1.2f, fillRate));

            float progressionBoost = CalculateProgressionBoost(state);
            float rawTarget = infrastructureCapacity * fillRate * progressionBoost;
            LastRawTarget = rawTarget;
            LastFillRate = fillRate;
            LastProgressionBoost = progressionBoost;

            float smoothed = state.SmoothedTargetActiveSkiers;
            if (smoothed <= 0f)
            {
                smoothed = rawTarget;
            }
            else
            {
                // Anti-spike smoothing: demand rises slowly early game, faster as
                // momentum is earned; demand can still drop quickly on bad choices.
                float riseRatePerMinute = 0.020f + state.DemandMomentum * 0.020f;
                float fallRatePerMinute = 0.055f;
                float rate = rawTarget >= smoothed ? riseRatePerMinute : fallRatePerMinute;
                float alpha = 1f - (float)System.Math.Exp(-rate * minutesPassed);
                smoothed += (rawTarget - smoothed) * alpha;
            }

            state.SmoothedTargetActiveSkiers = smoothed;
            TargetActiveSkiers = System.Math.Max(MIN_SKIERS, 
                System.Math.Min(HARD_CAP, (int)smoothed));
        }

        private float CalculateProgressionBoost(SimulationState state)
        {
            float momentum = System.Math.Max(0f, System.Math.Min(1f, state.DemandMomentum));
            float streak = System.Math.Max(0, state.ConsecutiveStrongServiceDays);

            // Strong S-curve source: sustained service quality ("word of mouth").
            // Little boost early, then stronger acceleration once quality is stable.
            float momentumTerm = momentum * momentum; // ease-in
            float streakTerm = 1f - (float)System.Math.Exp(-streak / 4f);
            float boost = 0.90f + momentumTerm * 0.35f + streakTerm * 0.20f;
            return System.Math.Max(0.85f, System.Math.Min(1.45f, boost));
        }
        
        /// <summary>
        /// Resets visitor count and fractional accumulator for a new day.
        /// </summary>
        public void ResetDay()
        {
            _fractionalVisitors = 0f;
        }
    }
}

