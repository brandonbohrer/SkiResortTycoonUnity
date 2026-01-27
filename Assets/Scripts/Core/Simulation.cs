namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Main simulation orchestrator.
    /// Pure C# - no Unity types.
    /// Coordinates all systems and manages the day cycle.
    /// </summary>
    public class Simulation
    {
        private SimulationState _state;
        private TimeSystem _timeSystem;
        private VisitorSystem _visitorSystem;
        private EconomySystem _economySystem;
        
        public Simulation(float timeSpeedMinutesPerSecond = 10f)
        {
            _state = new SimulationState();
            _timeSystem = new TimeSystem(timeSpeedMinutesPerSecond);
            _visitorSystem = new VisitorSystem();
            _economySystem = new EconomySystem();
        }
        
        // Public accessors
        public SimulationState State => _state;
        public TimeSystem TimeSystem => _timeSystem;
        public VisitorSystem VisitorSystem => _visitorSystem;
        public EconomySystem EconomySystem => _economySystem;
        
        /// <summary>
        /// Advances the simulation by deltaTime.
        /// Returns true when the day has ended.
        /// </summary>
        public bool Tick(float deltaTime)
        {
            // Only advance time and visitors if the day is still active
            if (!_timeSystem.IsDayOver(_state))
            {
                // Accumulate visitors during the day
                _visitorSystem.Tick(_state, deltaTime, _timeSystem.SpeedMinutesPerSecond);
            }
            
            // Advance time and check if day just ended
            return _timeSystem.Tick(_state, deltaTime);
        }
        
        /// <summary>
        /// Handles end-of-day logic:
        /// - Computes revenue
        /// - Applies money
        /// - Increments day
        /// - Resets visitors and time for next day
        /// Returns the revenue earned.
        /// </summary>
        public int EndDay()
        {
            // Compute revenue based on visitors
            int revenue = _economySystem.ComputeEndOfDayRevenue(_state);
            
            // Apply the revenue
            _economySystem.ApplyRevenue(_state, revenue);
            
            // Increment day counter
            _state.DayIndex++;
            
            // Reset for next day
            _state.VisitorsToday = 0;
            _visitorSystem.ResetDay();
            _timeSystem.ResetToOpen(_state);
            
            return revenue;
        }
    }
}

