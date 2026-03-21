namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Manages player-set ticket price and the demand curve.
    /// 
    /// Price ratio = ticketPrice / fairPrice
    /// Demand multiplier: smooth curve from 1.3x (bargain) to 0.05x (gouging).
    /// Also feeds TicketValueFactor for satisfaction impact.
    /// </summary>
    public class TicketPricing
    {
        private float _ticketPrice = 30f;
        
        /// <summary>
        /// Player-settable ticket price in dollars.
        /// </summary>
        public float TicketPrice
        {
            get => _ticketPrice;
            set => _ticketPrice = System.Math.Max(0f, value);
        }
        
        // ── Demand curve parameters (tunable) ───────────────────────────
        public float MaxDemandBonus { get; set; } = 1.10f;  // At ratio ≤ 0.8
        public float MinDemand { get; set; } = 0.03f;       // Floor at extreme prices
        public float DecayRate { get; set; } = 2.3f;        // Exponential decay steepness
        
        /// <summary>
        /// Price ratio = ticketPrice / fairPrice.
        /// 1.0 means priced at exactly what guests expect.
        /// </summary>
        public float GetPriceRatio(float fairPrice)
        {
            if (fairPrice <= 0f) return 1f;
            return _ticketPrice / fairPrice;
        }
        
        /// <summary>
        /// Demand multiplier based on price ratio.
        /// Below fair → bonus (up to 1.3x).
        /// At fair → 1.0x.
        /// Above fair → exponential decay toward 0.05x.
        /// </summary>
        public float GetDemandMultiplier(float fairPrice)
        {
            float ratio = GetPriceRatio(fairPrice);
            
            if (ratio <= 0.8f)
            {
                // Deep bargain — max demand
                return MaxDemandBonus;
            }
            else if (ratio <= 1.0f)
            {
                // Below fair price — linear interpolation from MaxDemandBonus to 1.0
                float t = (ratio - 0.8f) / 0.2f; // 0 at 0.8, 1 at 1.0
                return MaxDemandBonus + (1.0f - MaxDemandBonus) * t;
            }
            else
            {
                // Above fair price — exponential decay
                // At ratio 1.0: multiplier = 1.0
                // At ratio 2.5: multiplier ≈ 0.05
                float excess = ratio - 1.0f;
                float multiplier = (float)System.Math.Exp(-DecayRate * excess);
                return System.Math.Max(MinDemand, multiplier);
            }
        }
    }
}
