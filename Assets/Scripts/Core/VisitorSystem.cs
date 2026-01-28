namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# visitor accumulation system.
    /// Tracks fractional visitors and converts to whole visitors.
    /// </summary>
    public class VisitorSystem
    {
        private float _fractionalVisitors = 0f;
        
        // Configuration: visitors per minute based on infrastructure
        private float _baseVisitorsPerMinute = 0.5f;
        private float _visitorsPerLift = 0.2f;
        private float _visitorsPerTrail = 0.15f;
        
        // Satisfaction multiplier (set by Simulation)
        public float SatisfactionMultiplier { get; set; } = 1.0f;
        
        /// <summary>
        /// Accumulates visitors based on lifts and trails.
        /// Converts fractional visitors to whole visitors.
        /// </summary>
        public void Tick(SimulationState state, float deltaTime, float speedMinutesPerSecond)
        {
            // Calculate how many in-game minutes passed
            float minutesPassed = deltaTime * speedMinutesPerSecond;
            
            // Calculate visitor rate based on infrastructure
            float visitorsPerMinute = _baseVisitorsPerMinute 
                + (state.LiftsBuilt * _visitorsPerLift)
                + (state.TrailsBuilt * _visitorsPerTrail);
            
            // Apply satisfaction multiplier
            visitorsPerMinute *= SatisfactionMultiplier;
            
            // Accumulate fractional visitors
            _fractionalVisitors += visitorsPerMinute * minutesPassed;
            
            // Convert to whole visitors
            int wholeVisitors = (int)_fractionalVisitors;
            if (wholeVisitors > 0)
            {
                state.VisitorsToday += wholeVisitors;
                _fractionalVisitors -= wholeVisitors;
            }
        }
        
        /// <summary>
        /// Resets visitor count and fractional accumulator for a new day.
        /// </summary>
        public void ResetDay()
        {
            _fractionalVisitors = 0f;
        }
    }
}

