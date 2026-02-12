namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Interface for modular satisfaction factors.
    /// Each factor independently evaluates one aspect of a skier's experience
    /// and returns a score from 0 (terrible) to 1 (perfect).
    /// 
    /// To add a new satisfaction factor (e.g. lift wait times):
    /// 1. Create a class implementing ISatisfactionFactor
    /// 2. Register it on skier spawn via SkierSatisfaction.AddFactor()
    /// 3. The framework handles aggregation automatically
    /// </summary>
    public interface ISatisfactionFactor
    {
        /// <summary>
        /// Unique name for this factor (e.g. "NeedsFulfillment", "LiftWaitTime").
        /// Used for debugging and factor removal.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// How much this factor contributes to overall satisfaction (0-1).
        /// Higher weight = more impact on final score.
        /// </summary>
        float Weight { get; }
        
        /// <summary>
        /// Evaluate this factor for a given skier's current state.
        /// Returns 0 (terrible experience) to 1 (perfect experience).
        /// </summary>
        float Evaluate(SkierNeeds needs);
    }
}
