using UnityEngine;
using UnityEngine.UI;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Manages play/pause and 1x/2x/3x speed buttons in the new UI.
    /// Highlights the active button using the brand orange color (#F6893B).
    /// Attach to the SpeedControlsGroup GameObject in the TopHUD prefab.
    /// </summary>
    public class TimeControlPanel : MonoBehaviour
    {
        // Brand accent color for selected/active button state
        private static readonly Color SelectedColor = new Color(0.9647f, 0.5373f, 0.2314f, 1f); // #F6893B

        // Original colors saved from the prefab — restored when a button is deselected
        private Color _pauseOrigColor;
        private Color _speed1xOrigColor;
        private Color _speed2xOrigColor;
        private Color _speed3xOrigColor;

        [Header("References (auto-found if null)")]
        [SerializeField] private SimulationRunner _simulationRunner;

        [Header("Buttons")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _speed1xButton;
        [SerializeField] private Button _speed2xButton;
        [SerializeField] private Button _speed3xButton;

        private TimeController _timeController;

        void Start()
        {
            // Auto-find SimulationRunner if not wired in Inspector
            if (_simulationRunner == null)
                _simulationRunner = FindObjectOfType<SimulationRunner>();

            if (_simulationRunner != null && _simulationRunner.Sim != null)
                _timeController = _simulationRunner.Sim.TimeController;

            // Cache original prefab colors so we can restore them on deselect
            _pauseOrigColor   = GetButtonColor(_pauseButton);
            _speed1xOrigColor = GetButtonColor(_speed1xButton);
            _speed2xOrigColor = GetButtonColor(_speed2xButton);
            _speed3xOrigColor = GetButtonColor(_speed3xButton);

            // Wire button listeners
            if (_pauseButton != null)
            {
                _pauseButton.onClick.AddListener(OnPauseToggle);
                SetupTooltip(_pauseButton, TooltipTexts.TimeControls.PauseHeader, TooltipTexts.TimeControls.PauseContent);
            }

            if (_speed1xButton != null)
            {
                _speed1xButton.onClick.AddListener(() => OnSpeedChange(TimeController.Speed1x));
                SetupTooltip(_speed1xButton, TooltipTexts.TimeControls.Speed1xHeader, TooltipTexts.TimeControls.Speed1xContent);
            }

            if (_speed2xButton != null)
            {
                _speed2xButton.onClick.AddListener(() => OnSpeedChange(TimeController.Speed2x));
                SetupTooltip(_speed2xButton, TooltipTexts.TimeControls.Speed2xHeader, TooltipTexts.TimeControls.Speed2xContent);
            }

            if (_speed3xButton != null)
            {
                _speed3xButton.onClick.AddListener(() => OnSpeedChange(TimeController.Speed3x));
                SetupTooltip(_speed3xButton, TooltipTexts.TimeControls.Speed3xHeader, TooltipTexts.TimeControls.Speed3xContent);
            }

            RefreshVisuals();
        }
        
        private void SetupTooltip(Button button, string header, string content)
        {
            if (button == null) return;
            
            var tooltipTrigger = button.GetComponent<TooltipTrigger>();
            if (tooltipTrigger == null)
            {
                tooltipTrigger = button.gameObject.AddComponent<TooltipTrigger>();
            }
            tooltipTrigger.SetContent(header, content);
        }

        void Update()
        {
            // Lazy-init TimeController (simulation may start after this component)
            if (_timeController == null && _simulationRunner != null && _simulationRunner.Sim != null)
                _timeController = _simulationRunner.Sim.TimeController;

            RefreshVisuals();
        }

        // ── Button Handlers ──────────────────────────────────────────────

        private void OnPauseToggle()
        {
            if (_timeController == null) return;
            _timeController.TogglePause();
            RefreshVisuals();
        }

        private void OnSpeedChange(float speed)
        {
            if (_timeController == null) return;
            _timeController.SetSpeed(speed);

            // Unpause when a speed is explicitly chosen
            if (_timeController.IsPaused)
                _timeController.Resume();

            RefreshVisuals();
        }

        // ── Visuals ──────────────────────────────────────────────────────

        private void RefreshVisuals()
        {
            if (_timeController == null) return;

            float speed = _timeController.SpeedMultiplier;
            bool paused = _timeController.IsPaused;

            // Pause button is highlighted when the game is PAUSED
            SetButtonColor(_pauseButton,   paused,                             _pauseOrigColor);

            // Speed buttons are highlighted when that speed is active AND not paused
            SetButtonColor(_speed1xButton, !paused && speed == TimeController.Speed1x, _speed1xOrigColor);
            SetButtonColor(_speed2xButton, !paused && speed == TimeController.Speed2x, _speed2xOrigColor);
            SetButtonColor(_speed3xButton, !paused && speed == TimeController.Speed3x, _speed3xOrigColor);
        }

        private static Color GetButtonColor(Button button)
        {
            var image = button?.targetGraphic as Image;
            return image != null ? image.color : Color.white;
        }

        /// <summary>
        /// Tints the button orange when active; restores the original prefab color when inactive.
        /// </summary>
        private void SetButtonColor(Button button, bool active, Color originalColor)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = active ? SelectedColor : originalColor;
            ButtonHoverFeedback.Apply(button, UIManager.Instance?.Theme);
        }
    }
}
