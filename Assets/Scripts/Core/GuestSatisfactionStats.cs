using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Aggregated snapshot of guest satisfaction metrics across all active skiers.
    /// Computed periodically by the visual layer and consumed by the Guests tab UI.
    /// 
    /// Each metric maps to a qualitative rating for display:
    ///   0.0-0.3 = Poor, 0.3-0.5 = Low, 0.5-0.7 = Medium, 0.7-0.85 = Good, 0.85+ = Excellent
    /// </summary>
    public class GuestSatisfactionStats
    {
        // Raw averages (0-1 scale, higher = better)
        public float AvgWaitTimeSatisfaction { get; set; } = 1f;
        public float AvgLiftTraversal { get; set; } = 1f;
        public float AvgTrailAccess { get; set; } = 1f;
        public float AvgFoodSatisfaction { get; set; } = 1f;
        public float AvgPriceFairness { get; set; } = 1f;
        public float AvgTrailVariety { get; set; } = 1f;
        
        // Concrete numbers for display
        public float AvgWaitTimeSeconds { get; set; } = 0f;
        public int SkierCount { get; set; } = 0;
        
        /// <summary>
        /// Converts a 0-1 score to a qualitative rating string.
        /// </summary>
        public static string GetRating(float score)
        {
            if (score >= 0.85f) return "Excellent";
            if (score >= 0.7f)  return "Good";
            if (score >= 0.5f)  return "Fair";
            if (score >= 0.3f)  return "Low";
            return "Poor";
        }
        
        /// <summary>
        /// Computes aggregated stats from a collection of active skiers.
        /// Each stat maps to one or more satisfaction factors or needs.
        /// </summary>
        public static GuestSatisfactionStats ComputeFrom(IList<Skier> skiers, float ticketPriceRatio, int distinctDifficulties, int totalTrailCount)
        {
            var stats = new GuestSatisfactionStats();
            if (skiers == null || skiers.Count == 0)
                return stats;
            
            stats.SkierCount = skiers.Count;
            
            float totalWaitSat = 0f;
            float totalTraversal = 0f;
            float totalTrailAccess = 0f;
            float totalFoodSat = 0f;
            float totalPriceFairness = 0f;
            float totalVariety = 0f;
            float totalWaitSeconds = 0f;
            int count = 0;
            
            foreach (var skier in skiers)
            {
                if (skier?.Needs == null) continue;
                var needs = skier.Needs;
                count++;
                
                // Wait time satisfaction: based on total wait accumulated
                // 0 wait = 1.0, 60s wait = 0.6, 150s+ = 0.0
                float waitScore = 1f;
                if (needs.TotalWaitTime > 0f)
                    waitScore = System.Math.Max(0f, 1f - needs.TotalWaitTime / 150f);
                totalWaitSat += waitScore;
                totalWaitSeconds += needs.TotalWaitTime;
                
                // Lift traversal: based on walking distance (how easy is it to get around?)
                // 0m = 1.0, 500m = 0.0
                float traversalScore = System.Math.Max(0f, 1f - needs.TotalWalkingDistance / 500f);
                totalTraversal += traversalScore;
                
                // Trail access: use the SkillMatch factor's score directly
                float accessScore = skier.SatisfactionTracker.GetFactorScore("SkillMatch", needs);
                if (accessScore < 0f) accessScore = 0.7f; // factor not registered
                totalTrailAccess += accessScore;
                
                // Food satisfaction: how well are hunger/bladder needs being met?
                // Combines: current hunger level, unfulfilled attempts, time with urgent needs
                float foodScore = 1f;
                if (needs.Hunger >= SkierNeeds.HungerThreshold)
                {
                    float excess = (needs.Hunger - SkierNeeds.HungerThreshold) / (1f - SkierNeeds.HungerThreshold);
                    foodScore -= excess * 0.4f;
                }
                if (needs.Bladder >= SkierNeeds.BladderThreshold)
                {
                    float excess = (needs.Bladder - SkierNeeds.BladderThreshold) / (1f - SkierNeeds.BladderThreshold);
                    foodScore -= excess * 0.3f;
                }
                foodScore -= System.Math.Min(0.3f, needs.UnfulfilledNeedAttempts * 0.1f);
                foodScore = System.Math.Max(0f, foodScore);
                totalFoodSat += foodScore;
                
                // Price fairness: use TicketValue factor score
                float priceScore = skier.SatisfactionTracker.GetFactorScore("TicketValue", needs);
                if (priceScore < 0f) priceScore = 0.8f;
                totalPriceFairness += priceScore;
                
                // Trail variety: use SkillMatch factor (already accounts for preferred %)
                float varietyScore = accessScore;
                totalVariety += varietyScore;
            }
            
            if (count > 0)
            {
                stats.AvgWaitTimeSatisfaction = totalWaitSat / count;
                stats.AvgLiftTraversal = totalTraversal / count;
                stats.AvgTrailAccess = totalTrailAccess / count;
                stats.AvgFoodSatisfaction = totalFoodSat / count;
                stats.AvgPriceFairness = totalPriceFairness / count;
                stats.AvgTrailVariety = totalVariety / count;
                stats.AvgWaitTimeSeconds = totalWaitSeconds / count;
            }
            
            // Trail variety bonus: having more distinct difficulty types is inherently good
            if (totalTrailCount > 0)
            {
                float varietyBonus = 0f;
                if (distinctDifficulties >= 4) varietyBonus = 0.15f;
                else if (distinctDifficulties >= 3) varietyBonus = 0.08f;
                else if (distinctDifficulties >= 2) varietyBonus = 0.03f;
                
                stats.AvgTrailVariety = System.Math.Min(1f, stats.AvgTrailVariety + varietyBonus);
            }
            
            return stats;
        }
    }
}
