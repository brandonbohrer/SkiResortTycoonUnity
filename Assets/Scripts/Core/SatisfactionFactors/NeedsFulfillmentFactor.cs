namespace SkiResortTycoon.Core.SatisfactionFactors
{
    /// <summary>
    /// Satisfaction factor based on how well a skier's physical needs are being met.
    /// 
    /// Score drops when:
    /// - Needs are above threshold for extended time
    /// - Failed attempts to reach a lodge (full or unreachable)
    /// - Current need levels are high
    /// </summary>
    public class NeedsFulfillmentFactor : ISatisfactionFactor
    {
        public string Name => "NeedsFulfillment";
        public float Weight => 1.0f; // Core driver of satisfaction
        
        public float Evaluate(SkierNeeds needs)
        {
            float score = 1.0f;
            
            // ── Penalty for current unmet needs ─────────────────────────
            // Each need at threshold = -0.15, at max (1.0) = -0.25
            score -= CalculateNeedPenalty(needs.Hunger, SkierNeeds.HungerThreshold);
            score -= CalculateNeedPenalty(needs.Bladder, SkierNeeds.BladderThreshold);
            score -= CalculateNeedPenalty(needs.Fatigue, SkierNeeds.FatigueThreshold);
            
            // ── Penalty for failed lodge attempts ───────────────────────
            // Each failed attempt = -0.1 (resort has a capacity/accessibility problem)
            score -= needs.UnfulfilledNeedAttempts * 0.1f;
            
            // ── Penalty for extended time with urgent needs ─────────────
            // Max penalty of -0.4 at 60+ game minutes with urgent needs
            float urgentPenalty = System.Math.Min(0.4f, needs.TimeWithUrgentNeeds / 150f);
            score -= urgentPenalty;
            
            return System.Math.Max(0f, System.Math.Min(1f, score));
        }
        
        /// <summary>
        /// Calculate penalty for a single need level.
        /// Returns 0 if below threshold, scaling up to 0.25 at max.
        /// </summary>
        private float CalculateNeedPenalty(float needLevel, float threshold)
        {
            if (needLevel < threshold)
                return 0f;
            
            // Scale from 0 at threshold to 0.25 at 1.0
            float excessRatio = (needLevel - threshold) / (1f - threshold);
            return 0.15f + excessRatio * 0.10f;
        }
    }
}
