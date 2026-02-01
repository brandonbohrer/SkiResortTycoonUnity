namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Tracks physical needs of a skier that influence their goals.
    /// Values range from 0 (satisfied) to 1 (urgent need).
    /// </summary>
    public class SkierNeeds
    {
        // Need levels (0 = satisfied, 1 = critical)
        public float Hunger { get; set; }
        public float Fatigue { get; set; }
        public float Bladder { get; set; }
        
        // Satisfaction tracking (0 = miserable, 1 = very happy)
        public float Satisfaction { get; set; }
        
        // Thresholds that trigger goal changes
        public const float HungerThreshold = 0.7f;
        public const float BladderThreshold = 0.8f;
        public const float FatigueThreshold = 0.6f;
        
        // Rate of need increase per game minute
        public const float HungerRate = 0.002f;      // ~8 hours to full
        public const float BladderRate = 0.004f;     // ~4 hours to full
        public const float FatiguePerRun = 0.05f;    // 20 runs to exhaust
        public const float FatigueRecoveryRate = 0.01f; // Recovers while on lift
        
        public SkierNeeds()
        {
            Hunger = 0f;
            Fatigue = 0f;
            Bladder = 0f;
            Satisfaction = 0.8f; // Start happy but not perfect
        }
        
        /// <summary>
        /// Updates needs based on elapsed time (in game minutes).
        /// </summary>
        public void UpdateNeeds(float deltaMinutes)
        {
            Hunger = System.Math.Min(1f, Hunger + HungerRate * deltaMinutes);
            Bladder = System.Math.Min(1f, Bladder + BladderRate * deltaMinutes);
        }
        
        /// <summary>
        /// Increases fatigue after completing a run.
        /// </summary>
        public void AddRunFatigue()
        {
            Fatigue = System.Math.Min(1f, Fatigue + FatiguePerRun);
        }
        
        /// <summary>
        /// Recovers fatigue while resting (on lift, at base, etc.).
        /// </summary>
        public void RecoverFatigue(float deltaMinutes)
        {
            Fatigue = System.Math.Max(0f, Fatigue - FatigueRecoveryRate * deltaMinutes);
        }
        
        /// <summary>
        /// Checks if any need is above its threshold (requires attention).
        /// </summary>
        public bool HasUrgentNeed()
        {
            return Hunger >= HungerThreshold || 
                   Bladder >= BladderThreshold || 
                   Fatigue >= FatigueThreshold;
        }
        
        /// <summary>
        /// Gets the most urgent need type, or null if none are urgent.
        /// </summary>
        public string GetMostUrgentNeed()
        {
            if (Bladder >= BladderThreshold) return "Bladder";
            if (Hunger >= HungerThreshold) return "Hunger";
            if (Fatigue >= FatigueThreshold) return "Fatigue";
            return null;
        }
        
        /// <summary>
        /// Applies a satisfaction change, clamped to 0-1.
        /// </summary>
        public void AdjustSatisfaction(float delta)
        {
            Satisfaction = System.Math.Max(0f, System.Math.Min(1f, Satisfaction + delta));
        }
    }
}
