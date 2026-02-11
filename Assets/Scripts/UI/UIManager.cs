using UnityEngine;
using UnityEngine.Events;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Central singleton managing all UI state and input routing.
    /// Handles global shortcuts, panel visibility, and tool activation.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        
        [Header("References")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private UITheme _theme;
        
        [Header("Panels")]
        [SerializeField] private GameObject _mainMenuOverlay;
        [SerializeField] private ResortPanel _resortPanel;
        [SerializeField] private BuildActionBar _buildActionBar;
        
        [Header("Settings")]
        [SerializeField] private bool _menuOpen = false;
        
        // Events
        public UnityEvent OnMenuOpened = new UnityEvent();
        public UnityEvent OnMenuClosed = new UnityEvent();
        public UnityEvent<BaseTool> OnToolChanged = new UnityEvent<BaseTool>();
        
        // Current state
        private BaseTool _activeTool;
        private TimeController _timeController;
        private bool _isChangingTool; // Re-entrancy guard to prevent recursive tool switching
        
        /// <summary>
        /// Currently active build/interaction tool
        /// </summary>
        public BaseTool ActiveTool => _activeTool;
        
        /// <summary>
        /// Whether the main menu is currently open
        /// </summary>
        public bool IsMenuOpen => _menuOpen;
        
        /// <summary>
        /// The current UI theme
        /// </summary>
        public UITheme Theme => _theme;
        
        /// <summary>
        /// Reference to the simulation runner
        /// </summary>
        public SimulationRunner SimulationRunner => _simulationRunner;
        
        void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        void Start()
        {
            // Get time controller reference
            if (_simulationRunner != null && _simulationRunner.Sim != null)
            {
                _timeController = _simulationRunner.Sim.TimeController;
            }
            
            // Ensure menu starts closed
            if (_mainMenuOverlay != null)
            {
                _mainMenuOverlay.SetActive(false);
            }
            _menuOpen = false;
        }
        
        void Update()
        {
            HandleGlobalInput();
        }
        
        /// <summary>
        /// Handles all global keyboard shortcuts
        /// </summary>
        private void HandleGlobalInput()
        {
            // ESC: Cancel tool or open menu
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscape();
            }
            
            // Space: Pause/Play toggle
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePause();
            }
            
            // Number keys for speed control
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SetGameSpeed(TimeController.Speed1x);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SetGameSpeed(TimeController.Speed2x);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                SetGameSpeed(TimeController.Speed3x);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                // 4 = Pause
                if (_timeController != null && !_timeController.IsPaused)
                {
                    _timeController.Pause();
                }
            }
        }
        
        /// <summary>
        /// Handles ESC key - cancel tool first, then open menu
        /// </summary>
        private void HandleEscape()
        {
            // If tool is active, cancel it first
            if (_activeTool != null)
            {
                CancelActiveTool();
                return;
            }
            
            // Otherwise toggle menu
            ToggleMenu();
        }
        
        /// <summary>
        /// Toggles the main menu overlay
        /// </summary>
        public void ToggleMenu()
        {
            if (_menuOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }
        
        /// <summary>
        /// Opens the main menu overlay
        /// </summary>
        public void OpenMenu()
        {
            _menuOpen = true;
            
            if (_mainMenuOverlay != null)
            {
                _mainMenuOverlay.SetActive(true);
            }
            
            // Pause the game when menu opens
            if (_timeController != null && !_timeController.IsPaused)
            {
                _timeController.Pause();
            }
            
            OnMenuOpened?.Invoke();
        }
        
        /// <summary>
        /// Closes the main menu overlay
        /// </summary>
        public void CloseMenu()
        {
            _menuOpen = false;
            
            if (_mainMenuOverlay != null)
            {
                _mainMenuOverlay.SetActive(false);
            }
            
            OnMenuClosed?.Invoke();
        }
        
        /// <summary>
        /// Toggles pause state
        /// </summary>
        public void TogglePause()
        {
            if (_timeController != null)
            {
                _timeController.TogglePause();
            }
        }
        
        /// <summary>
        /// Sets the game speed
        /// </summary>
        public void SetGameSpeed(float speed)
        {
            if (_timeController != null)
            {
                _timeController.SetSpeed(speed);
                
                // Resume if paused
                if (_timeController.IsPaused)
                {
                    _timeController.Resume();
                }
            }
        }
        
        /// <summary>
        /// Activates a tool, deactivating any current tool first
        /// </summary>
        public void ActivateTool(BaseTool tool)
        {
            // Re-entrancy guard: prevent recursive tool changes
            if (_isChangingTool)
            {
                Debug.LogWarning("[UIManager] ActivateTool called recursively - ignoring to prevent stack overflow");
                return;
            }
            
            // Early return if tool is already active
            if (_activeTool == tool)
            {
                return;
            }
            
            _isChangingTool = true;
            try
            {
                // Deactivate current tool if any
                if (_activeTool != null)
                {
                    _activeTool.OnDeactivate();
                }
                
                _activeTool = tool;
                
                if (_activeTool != null)
                {
                    _activeTool.OnActivate();
                }
                
                OnToolChanged?.Invoke(_activeTool);
            }
            finally
            {
                _isChangingTool = false;
            }
        }
        
        /// <summary>
        /// Deactivates the current tool
        /// </summary>
        public void DeactivateTool()
        {
            // Re-entrancy guard: prevent recursive tool changes
            if (_isChangingTool)
            {
                Debug.LogWarning("[UIManager] DeactivateTool called recursively - ignoring to prevent stack overflow");
                return;
            }
            
            if (_activeTool == null)
            {
                return; // Nothing to deactivate
            }
            
            _isChangingTool = true;
            try
            {
                _activeTool.OnDeactivate();
                _activeTool = null;
                OnToolChanged?.Invoke(null);
            }
            finally
            {
                _isChangingTool = false;
            }
        }
        
        /// <summary>
        /// Cancels and deactivates the current tool
        /// </summary>
        public void CancelActiveTool()
        {
            // Re-entrancy guard: prevent recursive tool changes
            if (_isChangingTool)
            {
                Debug.LogWarning("[UIManager] CancelActiveTool called recursively - ignoring to prevent stack overflow");
                return;
            }
            
            if (_activeTool == null)
            {
                return; // Nothing to cancel
            }
            
            _isChangingTool = true;
            try
            {
                _activeTool.OnCancel();
                _activeTool = null;
                OnToolChanged?.Invoke(null);
            }
            finally
            {
                _isChangingTool = false;
            }
        }
        
        /// <summary>
        /// Checks if a specific tool is currently active
        /// </summary>
        public bool IsToolActive(BaseTool tool)
        {
            return _activeTool != null && _activeTool == tool;
        }
        
        /// <summary>
        /// Checks if any tool is currently active
        /// </summary>
        public bool HasActiveTool()
        {
            return _activeTool != null;
        }
    }
}
