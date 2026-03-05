namespace SkiResortTycoon.Core.SatisfactionFactors
{
    /// <summary>
    /// Satisfaction factor based on walking friction and lift wait times.
    /// Long walks between trails, lifts, and lodges reduce satisfaction.
    /// Long lift waits are a MAJOR frustration source — the #1 complaint
    /// at real resorts. Even moderate waits should noticeably tank satisfaction.
    /// </summary>
    public class TraversalFrictionFactor : ISatisfactionFactor
    {
        public string Name => "TraversalFriction";
        public float Weight => 1.0f;
        
        private const float MaxPenaltyDistance = 500f;
        private const float MaxWalkPenalty = 0.4f;
        
        // Wait time thresholds (in seconds of effective game time at lift bottom)
        // 30s effective wait → noticeable, 120s → major frustration, 300s → rage quit
        private const float MaxWaitPenalty = 0.6f;
        private const float WaitPenaltyScale = 150f;
        
        public float Evaluate(SkierNeeds needs)
        {
            float score = 1.0f;
            
            if (needs.TotalWalkingDistance > 0f)
            {
                float walkPenalty = System.Math.Min(MaxWalkPenalty, 
                    (needs.TotalWalkingDistance / MaxPenaltyDistance) * MaxWalkPenalty);
                score -= walkPenalty;
            }
            
            if (needs.TotalWaitTime > 0f)
            {
                float waitPenalty = System.Math.Min(MaxWaitPenalty, 
                    needs.TotalWaitTime / WaitPenaltyScale * MaxWaitPenalty);
                score -= waitPenalty;
            }
            
            return System.Math.Max(0f, System.Math.Min(1f, score));
        }
    }
}
