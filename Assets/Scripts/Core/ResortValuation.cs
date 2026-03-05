using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Calculates what visitors consider a "fair" ticket price based on
    /// what the mountain offers. Pure function, no state.
    /// 
    /// fairPrice = base + lifts*liftValue + trails*trailValue
    ///           + difficultyVarietyBonus + amenityBonus
    /// </summary>
    public class ResortValuation
    {
        // ── Tunable weights ─────────────────────────────────────────────
        public float BaseValue { get; set; } = 10f;
        public float ValuePerLift { get; set; } = 8f;
        public float ValuePerTrail { get; set; } = 4f;
        
        // Amenity bonuses per lodge
        public float FoodBonus { get; set; } = 4f;
        public float BathroomBonus { get; set; } = 2f;
        public float RestBonus { get; set; } = 1f;
        
        // Difficulty variety bonus tiers (by distinct difficulty count)
        // 1 type = 0, 2 = 5, 3 = 10, 4+ = 15
        private static readonly float[] VarietyBonusTiers = { 0f, 0f, 5f, 10f, 15f };
        
        /// <summary>
        /// Calculates the fair ticket price.
        /// </summary>
        /// <param name="liftCount">Number of operational lifts.</param>
        /// <param name="trailCount">Number of operational trails.</param>
        /// <param name="lodgeAmenities">Per-lodge amenity flags: (hasFood, hasBathroom, hasRest).</param>
        /// <param name="distinctDifficultyCount">Number of distinct trail difficulty types (0-4+).</param>
        public float CalculateFairPrice(
            int liftCount,
            int trailCount,
            List<LodgeAmenityInfo> lodgeAmenities,
            int distinctDifficultyCount)
        {
            float price = BaseValue;
            
            // Infrastructure value
            price += liftCount * ValuePerLift;
            price += trailCount * ValuePerTrail;
            
            // Difficulty variety bonus
            int tier = System.Math.Min(distinctDifficultyCount, VarietyBonusTiers.Length - 1);
            price += VarietyBonusTiers[tier];
            
            // Amenity bonus per lodge
            if (lodgeAmenities != null)
            {
                foreach (var lodge in lodgeAmenities)
                {
                    if (lodge.HasFood) price += FoodBonus;
                    if (lodge.HasBathroom) price += BathroomBonus;
                    if (lodge.HasRest) price += RestBonus;
                }
            }
            
            return price;
        }
    }
    
    /// <summary>
    /// Lightweight struct for passing lodge amenity info from Unity layer
    /// into the pure-C# ResortValuation without referencing MonoBehaviour.
    /// </summary>
    public struct LodgeAmenityInfo
    {
        public bool HasFood;
        public bool HasBathroom;
        public bool HasRest;
        
        public LodgeAmenityInfo(bool hasFood, bool hasBathroom, bool hasRest)
        {
            HasFood = hasFood;
            HasBathroom = hasBathroom;
            HasRest = hasRest;
        }
    }
}
