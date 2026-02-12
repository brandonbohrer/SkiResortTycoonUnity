using System;
using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Tracks resort-level satisfaction as the average of individual skier satisfaction.
    /// 
    /// Each skier has a SkierSatisfaction aggregator with pluggable ISatisfactionFactor
    /// instances. This system averages those scores to get the resort-level value,
    /// which drives the visitor arrival multiplier.
    /// 
    /// Also retains legacy end-of-day unserved penalty as a supplementary signal.
    /// </summary>
    public class SatisfactionSystem
    {
        private float _satisfaction = 1.0f;
        private float _realtimeSatisfaction = 1.0f; // Updated from active skier average
        
        // Configuration
        public float UnservedPenalty { get; set; } = 0.3f;  // k factor for end-of-day
        public float MinSatisfaction { get; set; } = 0.2f;
        public float MaxSatisfaction { get; set; } = 1.2f;
        
        /// <summary>
        /// Current resort satisfaction (blended from real-time + end-of-day).
        /// </summary>
        public float Satisfaction => _satisfaction;
        
        /// <summary>
        /// Real-time satisfaction from active skiers (updated each tick).
        /// </summary>
        public float RealtimeSatisfaction => _realtimeSatisfaction;
        
        /// <summary>
        /// Updates real-time satisfaction from active skiers.
        /// Call periodically (e.g. every 1-2 seconds) from the simulation tick.
        /// </summary>
        public void UpdateFromActiveSkiers(IList<Skier> activeSkiers)
        {
            if (activeSkiers == null || activeSkiers.Count == 0)
                return; // Keep previous value
            
            float total = 0f;
            int count = 0;
            
            foreach (var skier in activeSkiers)
            {
                if (skier?.Needs != null)
                {
                    total += skier.GetSatisfaction();
                    count++;
                }
            }
            
            if (count > 0)
            {
                _realtimeSatisfaction = total / count;
                
                // Blend real-time into the main satisfaction value
                // Use weighted blend: 70% real-time, 30% historical
                _satisfaction = _realtimeSatisfaction * 0.7f + _satisfaction * 0.3f;
                _satisfaction = Math.Max(MinSatisfaction, Math.Min(MaxSatisfaction, _satisfaction));
            }
        }
        
        /// <summary>
        /// Updates satisfaction based on end-of-day statistics.
        /// Supplements the real-time satisfaction with unserved visitor penalty.
        /// </summary>
        public void UpdateSatisfaction(DayStats stats)
        {
            if (stats.TotalVisitors == 0)
                return;
            
            float unservedRate = (float)stats.UnservedVisitors / stats.TotalVisitors;
            
            // Unserved penalty still matters at end of day
            float delta = -UnservedPenalty * unservedRate;
            _satisfaction += delta;
            
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
            _realtimeSatisfaction = 1.0f;
        }
    }
}
