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
        public float Weight => 1.4f;
        
        public float Evaluate(SkierNeeds needs)
        {
            float ratio = needs.TicketPriceRatio;
            
            // ── Bargain zone (ratio ≤ 1.0): modest forgiveness, not a free pass ──────────────
            if (ratio <= 1.0f)
            {
                // ratio 1.0 -> 0.82 (fair but not "amazing"), ratio 0.5 -> 0.91
                return 0.82f + (1.0f - ratio) * 0.18f;
            }

            // ── Premium zone (ratio > 1.0): increasingly harsh ──────────
            float experienceQuality = CalculateExperienceQuality(needs);

            if (ratio <= 1.25f)
            {
                // Mild premium already hurts unless the day is excellent.
                float severity = (ratio - 1.0f) / 0.25f;
                float priceTerm = 0.72f - severity * 0.28f; // 0.72 -> 0.44
                return System.Math.Max(0.10f, System.Math.Min(1f, priceTerm * 0.6f + experienceQuality * 0.4f));
            }
            else
            {
                // Heavy premium rapidly collapses value perception.
                float excess = ratio - 1.25f;
                float priceDecay = (float)System.Math.Exp(-2.2f * excess);
                float combined = priceDecay * (0.35f + experienceQuality * 0.65f);
                return System.Math.Max(0.05f, System.Math.Min(1f, combined));
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
                score -= (1f - runCompletion) * 0.35f;
            }
            
            // Unfulfilled needs penalty (tried to find lodge but couldn't)
            if (needs.UnfulfilledNeedAttempts > 0)
            {
                float needsPenalty = System.Math.Min(0.45f, needs.UnfulfilledNeedAttempts * 0.15f);
                score -= needsPenalty;
            }
            
            // Time with urgent needs penalty
            // More than 60 game minutes with urgent needs = significant penalty
            if (needs.TimeWithUrgentNeeds > 0f)
            {
                float urgentPenalty = System.Math.Min(0.45f, needs.TimeWithUrgentNeeds / 150f);
                score -= urgentPenalty;
            }
            
            return System.Math.Max(0f, System.Math.Min(1f, score));
        }
    }
}
