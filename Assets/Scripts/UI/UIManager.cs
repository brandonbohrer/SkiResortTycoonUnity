using UnityEngine;
using UnityEngine.UI;
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
        [SerializeField] private Button     _menuButton;
        [SerializeField] private Button     _resumeButton;
        [SerializeField] private Button     _quitButton;
        [SerializeField] private GameObject _managerScreen;
        [SerializeField] private Button     _managerButton;
        [SerializeField] private BuildActionBar _buildActionBar;

        [Header("Input Blocking")]
        [Tooltip("CanvasGroup on the root game UI (TopHUD, build bar, etc.) — set interactable=false when menu opens")]
        [SerializeField] private CanvasGroup _gameUICanvasGroup;
        
        [Header("Settings")]
        [SerializeField] private bool _menuOpen = false;
        [SerializeField] private bool _managerOpen = false;
        
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
        public bool IsMenuOpen    => _menuOpen;
        public bool IsManagerOpen => _managerOpen;
        public bool IsAnyOverlayOpen => _menuOpen || _managerOpen;
        
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
            // Auto-find SimulationRunner if not wired
            if (_simulationRunner == null)
                _simulationRunner = FindObjectOfType<SimulationRunner>();

            // Get time controller reference
            if (_simulationRunner != null && _simulationRunner.Sim != null)
                _timeController = _simulationRunner.Sim.TimeController;

            // Wire menu / resume / quit buttons
            if (_menuButton   != null) _menuButton.onClick.AddListener(ToggleMenu);
            if (_resumeButton != null) _resumeButton.onClick.AddListener(CloseMenu);
            if (_quitButton   != null) _quitButton.onClick.AddListener(QuitGame);
            if (_managerButton != null) _managerButton.onClick.AddListener(OpenManager);

            // Ensure overlays start closed
            if (_mainMenuOverlay != null) _mainMenuOverlay.SetActive(false);
            if (_managerScreen   != null) _managerScreen.SetActive(false);
            _menuOpen    = false;
            _managerOpen = false;
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
            // Never fire keyboard shortcuts while the user is typing in a UI input field
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                    .GetComponent<TMPro.TMP_InputField>() != null)
                return;

            // ESC always works — even when overlays are open
            if (Input.GetKeyDown(KeyCode.Escape))
                HandleEscape();

            // All other keyboard input is blocked while any overlay is open
            if (_menuOpen || _managerOpen) return;

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
            // Close manager screen first if open
            if (_managerOpen)
            {
                CloseManager();
                return;
            }

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
        // ── Manager Screen ───────────────────────────────────────────────

        public void OpenManager()
        {
            _managerOpen = true;

            if (_managerScreen != null)
                _managerScreen.SetActive(true);

            // Block game UI and pause
            if (_gameUICanvasGroup != null)
            {
                _gameUICanvasGroup.interactable   = false;
                _gameUICanvasGroup.blocksRaycasts = false;
            }

            if (_timeController != null && !_timeController.IsPaused)
                _timeController.Pause();
        }

        public void CloseManager()
        {
            _managerOpen = false;

            if (_managerScreen != null)
                _managerScreen.SetActive(false);

            // Restore game UI only if the menu is also closed
            if (!_menuOpen && _gameUICanvasGroup != null)
            {
                _gameUICanvasGroup.interactable   = true;
                _gameUICanvasGroup.blocksRaycasts = true;
            }
        }

        // ── Game Menu ────────────────────────────────────────────────────

        public void OpenMenu()
        {
            _menuOpen = true;

            if (_mainMenuOverlay != null)
                _mainMenuOverlay.SetActive(true);

            // Block all game UI interaction
            if (_gameUICanvasGroup != null)
            {
                _gameUICanvasGroup.interactable   = false;
                _gameUICanvasGroup.blocksRaycasts = false;
            }

            // Pause the game when menu opens
            if (_timeController != null && !_timeController.IsPaused)
                _timeController.Pause();

            OnMenuOpened?.Invoke();
        }
        
        /// <summary>
        /// Closes the main menu overlay
        /// </summary>
        public void CloseMenu()
        {
            _menuOpen = false;

            if (_mainMenuOverlay != null)
                _mainMenuOverlay.SetActive(false);

            // Restore game UI interaction
            if (_gameUICanvasGroup != null)
            {
                _gameUICanvasGroup.interactable   = true;
                _gameUICanvasGroup.blocksRaycasts = true;
            }

            OnMenuClosed?.Invoke();
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
