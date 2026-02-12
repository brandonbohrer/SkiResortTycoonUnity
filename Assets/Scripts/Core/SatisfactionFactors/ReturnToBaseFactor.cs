namespace SkiResortTycoon.Core.SatisfactionFactors
{
    /// <summary>
    /// Satisfaction factor based on how easy it is for a skier to return to base.
    /// 
    /// Evaluated based on the skier's accumulated frustration metrics.
    /// A well-designed resort should make it easy for tired/done skiers to leave.
    /// 
    /// For now, this uses proxy metrics (walking distance + fatigue level).
    /// When full pathfinding evaluation is available, this can be enhanced
    /// to evaluate actual path quality from current position to base.
    /// </summary>
    public class ReturnToBaseFactor : ISatisfactionFactor
    {
        public string Name => "ReturnToBase";
        public float Weight => 0.7f; // Important at end of day
        
        public float Evaluate(SkierNeeds needs)
        {
            // High fatigue + lots of walking = hard to get back to base
            // This is a proxy until we have full path evaluation
            
            float score = 1.0f;
            
            // Penalize if skier has been walking a lot AND is fatigued
            // (suggests the resort layout makes it hard to get around when tired)
            if (needs.Fatigue > SkierNeeds.FatigueThreshold)
            {
                // Walking penalty scales up when fatigued
                float fatigueMultiplier = needs.Fatigue; // 0-1
                float walkPenalty = System.Math.Min(0.4f, 
                    (needs.TotalWalkingDistance / 300f) * fatigueMultiplier);
                score -= walkPenalty;
            }
            
            // Penalize unfulfilled needs (skier wanted to stop at lodge but couldn't)
            score -= needs.UnfulfilledNeedAttempts * 0.05f;
            
            return System.Math.Max(0f, System.Math.Min(1f, score));
        }
    }
}
