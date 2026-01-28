using System;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Tracks visitor satisfaction based on how well the resort serves demand.
    /// Pure C# - no Unity types.
    /// </summary>
    public class SatisfactionSystem
    {
        private float _satisfaction = 1.0f;
        
        // Configuration
        public float UnservedPenalty { get; set; } = 0.3f;  // k factor
        public float MinSatisfaction { get; set; } = 0.2f;
        public float MaxSatisfaction { get; set; } = 1.2f;
        
        public float Satisfaction => _satisfaction;
        
        /// <summary>
        /// Updates satisfaction based on day statistics.
        /// </summary>
        public void UpdateSatisfaction(DayStats stats)
        {
            if (stats.TotalVisitors == 0)
            {
                // No visitors, no change
                return;
            }
            
            float unservedRate = (float)stats.UnservedVisitors / stats.TotalVisitors;
            
            // Satisfaction decreases with unserved rate
            float delta = -UnservedPenalty * unservedRate;
            
            _satisfaction += delta;
            
            // Clamp
            _satisfaction = Math.Max(MinSatisfaction, Math.Min(MaxSatisfaction, _satisfaction));
        }
        
        /// <summary>
        /// Calculates visitor count modifier based on satisfaction.
        /// satisfaction 1.0 = 1.0x multiplier
        /// satisfaction 1.2 = 1.2x multiplier (20% more visitors)
        /// satisfaction 0.2 = 0.2x multiplier (80% fewer visitors)
        /// </summary>
        public float GetVisitorMultiplier()
        {
            return _satisfaction;
        }
        
        /// <summary>
        /// Resets satisfaction to baseline.
        /// </summary>
        public void Reset()
        {
            _satisfaction = 1.0f;
        }
    }
}

