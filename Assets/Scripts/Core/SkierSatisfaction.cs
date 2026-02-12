using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Per-skier satisfaction aggregator.
    /// Holds a list of ISatisfactionFactor instances and computes
    /// an overall satisfaction score via weighted average.
    /// 
    /// Each skier gets one of these at spawn time with all available
    /// factors registered. New factors can be added at any time.
    /// </summary>
    public class SkierSatisfaction
    {
        private readonly List<ISatisfactionFactor> _factors = new List<ISatisfactionFactor>();
        
        /// <summary>
        /// Register a new satisfaction factor.
        /// </summary>
        public void AddFactor(ISatisfactionFactor factor)
        {
            _factors.Add(factor);
        }
        
        /// <summary>
        /// Remove a factor by name.
        /// </summary>
        public void RemoveFactor(string name)
        {
            _factors.RemoveAll(f => f.Name == name);
        }
        
        /// <summary>
        /// Get all registered factors (for debugging/UI).
        /// </summary>
        public IReadOnlyList<ISatisfactionFactor> Factors => _factors;
        
        /// <summary>
        /// Calculate the overall satisfaction score for this skier.
        /// Returns weighted average of all factors (0-1).
        /// </summary>
        public float Calculate(SkierNeeds needs)
        {
            if (_factors.Count == 0)
                return needs.Satisfaction; // Fallback to raw satisfaction if no factors
            
            float totalWeight = 0f;
            float totalScore = 0f;
            
            foreach (var factor in _factors)
            {
                float score = factor.Evaluate(needs);
                totalScore += score * factor.Weight;
                totalWeight += factor.Weight;
            }
            
            return totalWeight > 0f ? totalScore / totalWeight : 0.8f;
        }
        
        /// <summary>
        /// Get the score for a specific factor by name (for debugging/UI).
        /// Returns -1 if not found.
        /// </summary>
        public float GetFactorScore(string name, SkierNeeds needs)
        {
            foreach (var factor in _factors)
            {
                if (factor.Name == name)
                    return factor.Evaluate(needs);
            }
            return -1f;
        }
    }
}
