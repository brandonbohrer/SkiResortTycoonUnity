namespace SkiResortTycoon.Core.SatisfactionFactors
{
    /// <summary>
    /// Satisfaction factor based on perceived ticket value.
    /// 
    /// Guests who paid above fair price have higher expectations.
    /// A mediocre experience at a premium price tanks satisfaction;
    /// the same experience at a bargain price gets a pass.
    /// 
    /// Reads SkierNeeds.TicketPriceRatio (set at spawn) plus
    /// session quality metrics to determine the score.
    /// </summary>
    public class TicketValueFactor : ISatisfactionFactor
    {
        public string Name => "TicketValue";
        public float Weight => 0.8f;
        
        public float Evaluate(SkierNeeds needs)
        {
            float ratio = needs.TicketPriceRatio;
            
            // ── Bargain zone (ratio ≤ 1.0): bonus feeling ──────────────
            if (ratio <= 1.0f)
            {
                // The lower the ratio, the more forgiving guests are.
                // ratio 0.5 → score 1.0 (great deal!)
                // ratio 1.0 → score 0.9 (fair, neutral)
                return 0.9f + (1.0f - ratio) * 0.2f;  // 0.9 to 1.1, clamped to 1.0 max
            }
            
            // ── Premium zone (ratio > 1.0): expectations rise ──────────
            // Need good experience to justify the premium price.
            float experienceQuality = CalculateExperienceQuality(needs);
            
            if (ratio <= 1.5f)
            {
                // Mild premium: good experience = neutral, bad = penalty
                // Lerp between "needs okay experience" and "needs good experience"
                float premiumSeverity = (ratio - 1.0f) / 0.5f; // 0 at 1.0, 1 at 1.5
                float requiredQuality = 0.4f + premiumSeverity * 0.2f; // 0.4 to 0.6
                
                if (experienceQuality >= requiredQuality)
                    return 0.8f + (experienceQuality - requiredQuality) * 0.3f;
                else
                    return 0.5f + experienceQuality * 0.5f; // 0.5 to 0.7 range
            }
            else
            {
                // High premium (ratio > 1.5): needs excellent experience
                if (experienceQuality >= 0.7f)
                    return 0.6f + experienceQuality * 0.3f; // 0.81-0.9
                else if (experienceQuality >= 0.4f)
                    return 0.3f + experienceQuality * 0.4f; // 0.46-0.58
                else
                    return 0.2f + experienceQuality * 0.3f; // 0.2-0.32
            }
        }
        
        /// <summary>
        /// Calculates an experience quality score (0-1) from session metrics.
        /// 1.0 = perfect day, 0.0 = terrible day.
        /// </summary>
        private float CalculateExperienceQuality(SkierNeeds needs)
        {
            float score = 1.0f;
            
            // Runs completion penalty
            if (needs.DesiredRuns > 0)
            {
                float runCompletion = (float)needs.RunsCompleted / needs.DesiredRuns;
                runCompletion = System.Math.Min(1f, runCompletion);
                // Not completing desired runs is a moderate penalty
                score -= (1f - runCompletion) * 0.3f;
            }
            
            // Unfulfilled needs penalty (tried to find lodge but couldn't)
            if (needs.UnfulfilledNeedAttempts > 0)
            {
                float needsPenalty = System.Math.Min(0.3f, needs.UnfulfilledNeedAttempts * 0.1f);
                score -= needsPenalty;
            }
            
            // Time with urgent needs penalty
            // More than 60 game minutes with urgent needs = significant penalty
            if (needs.TimeWithUrgentNeeds > 0f)
            {
                float urgentPenalty = System.Math.Min(0.3f, needs.TimeWithUrgentNeeds / 200f);
                score -= urgentPenalty;
            }
            
            return System.Math.Max(0f, System.Math.Min(1f, score));
        }
    }
}
