namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# time control system.
    /// Manages pause/play state and speed multipliers.
    /// </summary>
    public class TimeController
    {
        private bool _isPaused = false;
        private float _speedMultiplier = 1f;
        
        // Common speed presets
        // Speed 1: 1 day = 6 minutes
        // Speed 2: 1 day = 3 minutes (2x)
        // Speed 3: 1 day = 30 seconds (12x)
        public const float Speed1x = 1f;
        public const float Speed2x = 2f;
        public const float Speed3x = 12f;
        
        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }
        
        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set => _speedMultiplier = value > 0f ? value : 1f; // Ensure positive
        }
        
        /// <summary>
        /// Gets the effective delta time after applying pause and speed multiplier.
        /// </summary>
        public float GetEffectiveDeltaTime(float deltaTime)
        {
            if (_isPaused)
            {
                return 0f;
            }
            
            return deltaTime * _speedMultiplier;
        }
        
        public void Pause()
        {
            _isPaused = true;
        }
        
        public void Resume()
        {
            _isPaused = false;
        }
        
        public void TogglePause()
        {
            _isPaused = !_isPaused;
        }
        
        public void SetSpeed(float multiplier)
        {
            SpeedMultiplier = multiplier;
        }
    }
}

