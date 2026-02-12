namespace SkiResortTycoon.Core.SatisfactionFactors
{
    /// <summary>
    /// Satisfaction factor based on how much unnecessary walking/traversal
    /// a skier has to do to get around the resort.
    /// 
    /// Captures the "this resort is poorly laid out" frustration.
    /// Long walks between trails, lifts, and lodges reduce satisfaction.
    /// </summary>
    public class TraversalFrictionFactor : ISatisfactionFactor
    {
        public string Name => "TraversalFriction";
        public float Weight => 0.8f; // Major frustration source
        
        // Max walking distance before satisfaction bottoms out
        private const float MaxPenaltyDistance = 500f; // meters
        private const float MaxPenalty = 0.5f;
        
        public float Evaluate(SkierNeeds needs)
        {
            if (needs.TotalWalkingDistance <= 0f)
                return 1.0f; // No walking = perfect
            
            // Linear penalty: 0m = 1.0, 500m+ = 0.5
            float walkPenalty = System.Math.Min(MaxPenalty, 
                (needs.TotalWalkingDistance / MaxPenaltyDistance) * MaxPenalty);
            
            // Also factor in wait time (for future lift line support)
            // 0 seconds = no penalty, 300+ seconds total waiting = -0.3
            float waitPenalty = System.Math.Min(0.3f, needs.TotalWaitTime / 1000f);
            
            float score = 1.0f - walkPenalty - waitPenalty;
            return System.Math.Max(0f, System.Math.Min(1f, score));
        }
    }
}
