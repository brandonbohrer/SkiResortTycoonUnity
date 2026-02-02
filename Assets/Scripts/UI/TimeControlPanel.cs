using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// UI panel for controlling time (pause/play, speed controls).
    /// </summary>
    public class TimeControlPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationRunner _simulationRunner;
        
        [Header("UI Elements")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _speed1xButton;
        [SerializeField] private Button _speed2xButton;
        [SerializeField] private Button _speed5xButton;
        [SerializeField] private Button _speed10xButton;
        [SerializeField] private TextMeshProUGUI _pauseButtonText;
        
        private TimeController _timeController;
        
        void Start()
        {
            // Get reference to time controller
            if (_simulationRunner != null && _simulationRunner.Sim != null)
            {
                _timeController = _simulationRunner.Sim.TimeController;
            }
            
            // Set up button listeners
            if (_pauseButton != null)
            {
                _pauseButton.onClick.AddListener(OnPauseToggle);
            }
            
            if (_speed1xButton != null)
            {
                _speed1xButton.onClick.AddListener(() => OnSpeedChange(TimeController.Speed1x));
            }
            
            if (_speed2xButton != null)
            {
                _speed2xButton.onClick.AddListener(() => OnSpeedChange(TimeController.Speed2x));
            }
            
            if (_speed5xButton != null)
            {
                _speed5xButton.onClick.AddListener(() => OnSpeedChange(TimeController.Speed5x));
            }
            
            if (_speed10xButton != null)
            {
                _speed10xButton.onClick.AddListener(() => OnSpeedChange(TimeController.Speed10x));
            }
            
            UpdatePauseButtonText();
        }
        
        void Update()
        {
            // Update pause button text based on current state
            UpdatePauseButtonText();
            
            // Update speed button visuals to show active speed
            UpdateSpeedButtonHighlights();
        }
        
        private void OnPauseToggle()
        {
            if (_timeController == null) return;
            
            _timeController.TogglePause();
            UpdatePauseButtonText();
        }
        
        private void OnSpeedChange(float speed)
        {
            if (_timeController == null) return;
            
            _timeController.SetSpeed(speed);
            
            // Unpause if paused when changing speed
            if (_timeController.IsPaused)
            {
                _timeController.Resume();
            }
        }
        
        private void UpdatePauseButtonText()
        {
            if (_pauseButtonText == null || _timeController == null) return;
            
            _pauseButtonText.text = _timeController.IsPaused ? "▶ Play" : "⏸ Pause";
        }
        
        private void UpdateSpeedButtonHighlights()
        {
            if (_timeController == null) return;
            
            float currentSpeed = _timeController.SpeedMultiplier;
            
            // Highlight the active speed button
            HighlightButton(_speed1xButton, currentSpeed == TimeController.Speed1x);
            HighlightButton(_speed2xButton, currentSpeed == TimeController.Speed2x);
            HighlightButton(_speed5xButton, currentSpeed == TimeController.Speed5x);
            HighlightButton(_speed10xButton, currentSpeed == TimeController.Speed10x);
        }
        
        private void HighlightButton(Button button, bool isActive)
        {
            if (button == null) return;
            
            var colors = button.colors;
            
            // Use UITheme colors if available
            UITheme theme = UIManager.Instance?.Theme;
            if (theme != null)
            {
                colors.normalColor = isActive ? theme.ButtonSelected : theme.ButtonNormal;
                colors.highlightedColor = isActive ? theme.ButtonSelected : theme.ButtonHover;
            }
            else
            {
                colors.normalColor = isActive ? new Color(0f, 0.737f, 0.831f) : new Color(0.25f, 0.25f, 0.25f);
            }
            
            button.colors = colors;
        }
    }
}

