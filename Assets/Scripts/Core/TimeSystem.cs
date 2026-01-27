namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# time management system.
    /// Tracks in-game time in minutes since midnight.
    /// </summary>
    public class TimeSystem
    {
        // Constants: 9:00 AM to 5:00 PM
        public const float OpenTime = 9f * 60f;   // 540 minutes (9:00 AM)
        public const float CloseTime = 17f * 60f; // 1020 minutes (5:00 PM)
        
        private float _speedMinutesPerSecond;
        
        public TimeSystem(float speedMinutesPerSecond = 10f)
        {
            _speedMinutesPerSecond = speedMinutesPerSecond;
        }
        
        public float SpeedMinutesPerSecond
        {
            get => _speedMinutesPerSecond;
            set => _speedMinutesPerSecond = value;
        }
        
        /// <summary>
        /// Advances time based on real deltaTime.
        /// Returns true if the day has just ended (crossed CloseTime threshold).
        /// </summary>
        public bool Tick(SimulationState state, float deltaTime)
        {
            float previousTime = state.TimeMinutes;
            state.TimeMinutes += deltaTime * _speedMinutesPerSecond;
            
            // Check if we crossed the close time threshold
            return previousTime < CloseTime && state.TimeMinutes >= CloseTime;
        }
        
        /// <summary>
        /// Checks if the day is over.
        /// </summary>
        public bool IsDayOver(SimulationState state)
        {
            return state.TimeMinutes >= CloseTime;
        }
        
        /// <summary>
        /// Resets time to opening time for the next day.
        /// </summary>
        public void ResetToOpen(SimulationState state)
        {
            state.TimeMinutes = OpenTime;
        }
    }
}

