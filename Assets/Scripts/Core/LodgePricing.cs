namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Manages pricing for a single lodge's services.
    /// Player can set prices; higher prices = more revenue but lower satisfaction.
    /// 
    /// Baseline prices represent what skiers consider "fair."
    /// Prices above baseline reduce satisfaction; below baseline improve it slightly.
    /// </summary>
    public class LodgePricing
    {
        // ── Player-settable prices ──────────────────────────────────────
        public float BathroomPrice { get; set; } = 2f;
        public float FoodPrice { get; set; } = 8f;
        public float RestPrice { get; set; } = 0f;
        
        // ── Baseline "fair" prices ──────────────────────────────────────
        public const float BathroomBaseline = 2f;
        public const float FoodBaseline = 8f;
        public const float RestBaseline = 0f;
        
        // ── Price limits ────────────────────────────────────────────────
        public const float MinPrice = 0f;
        public const float MaxBathroomPrice = 10f;
        public const float MaxFoodPrice = 30f;
        public const float MaxRestPrice = 10f;
        
        // ── Satisfaction penalty scaling ─────────────────────────────────
        // At 2x baseline: -0.1 satisfaction per visit
        // At 3x baseline: -0.2 per visit
        // Capped at -0.5 per visit
        private const float PenaltyPerRatio = 0.1f;
        private const float MaxPenaltyPerVisit = 0.5f;
        
        // ── Revenue tracking ────────────────────────────────────────────
        public float TotalRevenue { get; set; }
        public int TotalVisits { get; set; }
        
        /// <summary>
        /// Calculate the charge for a lodge visit based on which services were used.
        /// </summary>
        public float CalculateCharge(bool usedBathroom, bool usedFood, bool usedRest)
        {
            float charge = 0f;
            if (usedBathroom) charge += BathroomPrice;
            if (usedFood) charge += FoodPrice;
            if (usedRest) charge += RestPrice;
            return charge;
        }
        
        /// <summary>
        /// Calculate the satisfaction penalty for a visit based on prices paid.
        /// Returns a negative value (penalty) or small positive (below-baseline bonus).
        /// </summary>
        public float CalculateSatisfactionImpact(bool usedBathroom, bool usedFood, bool usedRest)
        {
            float totalPenalty = 0f;
            
            if (usedBathroom && BathroomBaseline > 0f)
            {
                float ratio = BathroomPrice / BathroomBaseline;
                totalPenalty += CalculatePenaltyForRatio(ratio);
            }
            
            if (usedFood && FoodBaseline > 0f)
            {
                float ratio = FoodPrice / FoodBaseline;
                totalPenalty += CalculatePenaltyForRatio(ratio);
            }
            
            if (usedRest && RestBaseline > 0f)
            {
                float ratio = RestPrice / RestBaseline;
                totalPenalty += CalculatePenaltyForRatio(ratio);
            }
            
            // Clamp total penalty per visit
            return System.Math.Max(-MaxPenaltyPerVisit, totalPenalty);
        }
        
        /// <summary>
        /// Calculate penalty for a single price ratio.
        /// ratio 1.0 = 0 penalty (fair price)
        /// ratio < 1.0 = small positive bonus (cheap!)
        /// ratio > 1.0 = negative penalty (expensive!)
        /// </summary>
        private float CalculatePenaltyForRatio(float ratio)
        {
            // Below baseline: small bonus (capped at +0.05)
            if (ratio <= 1f)
                return System.Math.Min(0.05f, (1f - ratio) * 0.05f);
            
            // Above baseline: increasing penalty
            return -(ratio - 1f) * PenaltyPerRatio;
        }
        
        /// <summary>
        /// Record a completed visit with revenue.
        /// </summary>
        public void RecordVisit(float charge)
        {
            TotalRevenue += charge;
            TotalVisits++;
        }
    }
}
