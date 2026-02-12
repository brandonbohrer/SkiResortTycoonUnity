namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Tracks physical needs of a skier that influence their goals.
    /// Values range from 0 (satisfied) to 1 (urgent need).
    /// 
    /// Also tracks session-level metrics that satisfaction factors read:
    /// walking distance, wait time, unfulfilled needs, lodge pricing, etc.
    /// These fields are always accumulated even before their corresponding
    /// satisfaction factor exists, so data is ready when factors are added.
    /// </summary>
    public class SkierNeeds
    {
        // ── Need levels (0 = satisfied, 1 = critical) ───────────────────
        public float Hunger { get; set; }
        public float Fatigue { get; set; }
        public float Bladder { get; set; }
        
        // ── Legacy satisfaction (kept for backward compat) ──────────────
        public float Satisfaction { get; set; }
        
        // ── Thresholds that trigger goal changes ────────────────────────
        public const float HungerThreshold = 0.7f;
        public const float BladderThreshold = 0.8f;
        public const float FatigueThreshold = 0.6f;
        
        // ── Rate of need increase per game minute ───────────────────────
        // Day is 480 game minutes (9am-5pm).
        // At Speed1x: 480 game min = 6 real min.
        // Hunger: reaches threshold (~0.7) in about 3-4 hours game time (~1.5 real min at 1x)
        // Bladder: reaches threshold (~0.8) in about 2-3 hours game time (~1 real min at 1x)
        public const float HungerRate = 0.003f;      // ~4 hours to threshold
        public const float BladderRate = 0.006f;     // ~2.2 hours to threshold
        public const float FatiguePerRun = 0.05f;    // 20 runs to exhaust
        public const float FatigueRecoveryRate = 0.01f; // Recovers while on lift
        
        // ── Session tracking (read by satisfaction factors) ─────────────
        
        /// <summary>Total meters walked during session (friction metric).</summary>
        public float TotalWalkingDistance { get; set; }
        
        /// <summary>Total seconds spent waiting (e.g. lift lines). Zeros until lift queues exist.</summary>
        public float TotalWaitTime { get; set; }
        
        /// <summary>Number of times skier tried to reach a lodge but failed (full, unreachable).</summary>
        public int UnfulfilledNeedAttempts { get; set; }
        
        /// <summary>Game minutes spent with at least one need above threshold.</summary>
        public float TimeWithUrgentNeeds { get; set; }
        
        /// <summary>Cumulative satisfaction penalty from lodge pricing this session.</summary>
        public float CumulativePricePenalty { get; set; }
        
        /// <summary>Number of lodge visits this session.</summary>
        public int LodgeVisitCount { get; set; }
        
        public SkierNeeds()
        {
            Hunger = 0f;
            Fatigue = 0f;
            Bladder = 0f;
            Satisfaction = 0.8f;
            
            // Session tracking starts at zero
            TotalWalkingDistance = 0f;
            TotalWaitTime = 0f;
            UnfulfilledNeedAttempts = 0;
            TimeWithUrgentNeeds = 0f;
            CumulativePricePenalty = 0f;
            LodgeVisitCount = 0;
        }
        
        // ── Need updates ────────────────────────────────────────────────
        
        /// <summary>
        /// Updates needs based on elapsed time (in game minutes).
        /// Also tracks time with urgent needs.
        /// </summary>
        public void UpdateNeeds(float deltaMinutes)
        {
            Hunger = System.Math.Min(1f, Hunger + HungerRate * deltaMinutes);
            Bladder = System.Math.Min(1f, Bladder + BladderRate * deltaMinutes);
            
            // Track time with urgent needs for satisfaction factors
            if (HasUrgentNeed())
            {
                TimeWithUrgentNeeds += deltaMinutes;
            }
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
        
        // ── Need fulfillment (called by lodge visits) ───────────────────
        
        /// <summary>
        /// Fulfills hunger need (e.g. ate food at lodge).
        /// </summary>
        public void FulfillHunger()
        {
            Hunger = 0f;
        }
        
        /// <summary>
        /// Fulfills bladder need (e.g. used bathroom at lodge).
        /// </summary>
        public void FulfillBladder()
        {
            Bladder = 0f;
        }
        
        // ── Need queries ────────────────────────────────────────────────
        
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
        
        // ── Session tracking helpers ────────────────────────────────────
        
        /// <summary>
        /// Add walking distance (called during walk phases).
        /// </summary>
        public void AddWalkingDistance(float meters)
        {
            TotalWalkingDistance += meters;
        }
        
        /// <summary>
        /// Add wait time (called during lift queue phase, when implemented).
        /// </summary>
        public void AddWaitTime(float seconds)
        {
            TotalWaitTime += seconds;
        }
        
        /// <summary>
        /// Record a failed lodge attempt (full or unreachable).
        /// </summary>
        public void RecordUnfulfilledNeed()
        {
            UnfulfilledNeedAttempts++;
        }
        
        /// <summary>
        /// Record a price penalty from a lodge visit.
        /// </summary>
        public void AddPricePenalty(float penalty)
        {
            CumulativePricePenalty += penalty;
        }
    }
}
