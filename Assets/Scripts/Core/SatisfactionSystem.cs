using System;
using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Tracks resort-level satisfaction on a 0-100 scale.
    /// Baseline is 50: below 50 = losing skiers, above 50 = gaining skiers.
    /// 
    /// Satisfaction is the average of individual skier satisfaction scores,
    /// scaled to the 0-100 range and blended with historical value.
    /// 
    /// The visitor multiplier is linear: satisfaction / 50.
    /// So 50 → 1.0x (stable), 80 → 1.6x (growing), 30 → 0.6x (shrinking).
    /// </summary>
    public class SatisfactionSystem
    {
        private float _satisfaction = 50f;
        private float _realtimeSatisfaction = 50f;
        
        // Configuration
        public float UnservedPenalty { get; set; } = 24f;   // Max penalty from unserved visitors (on 0-100 scale)
        public float MinSatisfaction { get; set; } = 5f;
        public float MaxSatisfaction { get; set; } = 100f;
        public float Baseline { get; set; } = 50f;
        public float RealtimeBlendUpward { get; set; } = 0.80f;
        public float RealtimeBlendDownward { get; set; } = 0.92f;
        
        /// <summary>
        /// Current resort satisfaction (0-100, baseline 50).
        /// </summary>
        public float Satisfaction => _satisfaction;
        
        /// <summary>
        /// Real-time satisfaction from active skiers (0-100).
        /// </summary>
        public float RealtimeSatisfaction => _realtimeSatisfaction;
        
        /// <summary>
        /// Updates real-time satisfaction from active skiers.
        /// Skier satisfaction factors return 0-1; we scale to 0-100.
        /// </summary>
        public void UpdateFromActiveSkiers(IList<Skier> activeSkiers)
        {
            if (activeSkiers == null || activeSkiers.Count == 0)
                return;
            
            float total = 0f;
            int count = 0;
            
            foreach (var skier in activeSkiers)
            {
                if (skier?.Needs != null)
                {
                    // Skier satisfaction is 0-1, scale to 0-100
                    total += skier.GetSatisfaction() * 100f;
                    count++;
                }
            }
            
            if (count > 0)
            {
                _realtimeSatisfaction = total / count;
                
                // More reactive than before, and intentionally harsher on drops.
                float blend = _realtimeSatisfaction < _satisfaction ? RealtimeBlendDownward : RealtimeBlendUpward;
                _satisfaction = _realtimeSatisfaction * blend + _satisfaction * (1f - blend);
                _satisfaction = Math.Max(MinSatisfaction, Math.Min(MaxSatisfaction, _satisfaction));
            }
        }
        
        /// <summary>
        /// Updates satisfaction based on end-of-day statistics.
        /// Unserved visitors lower satisfaction.
        /// </summary>
        public void UpdateSatisfaction(DayStats stats)
        {
            if (stats.TotalVisitors == 0)
                return;
            
            float unservedRate = (float)stats.UnservedVisitors / stats.TotalVisitors;
            
            // Unserved penalty on 0-100 scale
            float delta = -UnservedPenalty * unservedRate;

            // Accessibility penalty: if skill-appropriate terrain is hard to reach,
            // satisfaction should fall even if ticket price is fair.
            if (stats.AvgSkillAccess > 0f)
            {
                float blockedSkill = 1f - stats.AvgSkillAccess;
                float blockedPreferred = 1f - stats.AvgPreferredAccess;
                delta -= blockedSkill * 8f;
                delta -= blockedPreferred * 5f;
            }
            _satisfaction += delta;
            
            _satisfaction = Math.Max(MinSatisfaction, Math.Min(MaxSatisfaction, _satisfaction));
        }
        
        /// <summary>
        /// Calculates visitor count modifier based on satisfaction.
        /// satisfaction 50 = 1.0x (stable, baseline)
        /// satisfaction 80 = 1.6x (growing)
        /// satisfaction 30 = 0.6x (shrinking)
        /// satisfaction 10 = 0.2x (nearly dead)
        /// </summary>
        public float GetVisitorMultiplier()
        {
            return _satisfaction / Baseline;
        }
        
        /// <summary>
        /// Resets satisfaction to baseline.
        /// </summary>
        public void Reset()
        {
            _satisfaction = Baseline;
            _realtimeSatisfaction = Baseline;
        }
    }
}
