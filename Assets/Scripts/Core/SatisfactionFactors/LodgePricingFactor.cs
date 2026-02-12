namespace SkiResortTycoon.Core.SatisfactionFactors
{
    /// <summary>
    /// Satisfaction factor based on lodge pricing experienced by this skier.
    /// 
    /// Tracks cumulative price penalties from lodge visits.
    /// High prices = lower satisfaction, creating the player trade-off
    /// between revenue and guest happiness.
    /// </summary>
    public class LodgePricingFactor : ISatisfactionFactor
    {
        public string Name => "LodgePricing";
        public float Weight => 0.6f; // Matters but secondary to needs
        
        public float Evaluate(SkierNeeds needs)
        {
            // Start at 1.0 (no visits = no complaints about pricing)
            if (needs.LodgeVisitCount == 0)
                return 1.0f;
            
            // CumulativePricePenalty is a sum of negative values from lodge visits
            // Average penalty per visit tells us if prices are consistently bad
            float avgPenalty = needs.CumulativePricePenalty / needs.LodgeVisitCount;
            
            // Convert penalty to score:
            // avgPenalty 0 = fair prices = score 1.0
            // avgPenalty -0.1 = slightly expensive = score 0.8
            // avgPenalty -0.3 = very expensive = score 0.4
            // avgPenalty -0.5 = gouging = score 0.0
            float score = 1.0f + (avgPenalty * 2f); // Scale penalty to 0-1 range
            
            return System.Math.Max(0f, System.Math.Min(1f, score));
        }
    }
}
