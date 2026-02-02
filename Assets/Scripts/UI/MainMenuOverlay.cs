using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Main menu overlay accessible via ESC or menu button.
    /// Contains resume, settings, save/load, and quit options.
    /// </summary>
    public class MainMenuOverlay : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _quitButton;
        
        [Header("Panels")]
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _saveLoadPanel;
        
        [Header("Backdrop")]
        [SerializeField] private Image _backdrop;
        [SerializeField] private float _backdropAlpha = 0.5f;
        
        void Start()
        {
            // Set up button listeners
            if (_resumeButton != null)
            {
                _resumeButton.onClick.AddListener(OnResumeClicked);
            }
            
            if (_settingsButton != null)
            {
                _settingsButton.onClick.AddListener(OnSettingsClicked);
            }
            
            if (_saveButton != null)
            {
                _saveButton.onClick.AddListener(OnSaveClicked);
            }
            
            if (_loadButton != null)
            {
                _loadButton.onClick.AddListener(OnLoadClicked);
            }
            
            if (_quitButton != null)
            {
                _quitButton.onClick.AddListener(OnQuitClicked);
            }
            
            // Set up backdrop
            if (_backdrop != null)
            {
                var color = _backdrop.color;
                color.a = _backdropAlpha;
                _backdrop.color = color;
                
                // Click backdrop to close
                var button = _backdrop.gameObject.AddComponent<Button>();
                button.onClick.AddListener(OnResumeClicked);
            }
            
            // Start with main panel visible
            ShowMainPanel();
        }
        
        void OnEnable()
        {
            // Always show main panel when opened
            ShowMainPanel();
        }
        
        private void OnResumeClicked()
        {
            UIManager.Instance?.CloseMenu();
        }
        
        private void OnSettingsClicked()
        {
            ShowSettingsPanel();
        }
        
        private void OnSaveClicked()
        {
            // TODO: Implement save functionality
            NotificationManager.Instance?.ShowInfo("Save functionality coming soon!");
        }
        
        private void OnLoadClicked()
        {
            // TODO: Implement load functionality
            NotificationManager.Instance?.ShowInfo("Load functionality coming soon!");
        }
        
        private void OnQuitClicked()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
        
        private void ShowMainPanel()
        {
            if (_mainPanel != null) _mainPanel.SetActive(true);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_saveLoadPanel != null) _saveLoadPanel.SetActive(false);
        }
        
        private void ShowSettingsPanel()
        {
            if (_mainPanel != null) _mainPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(true);
            if (_saveLoadPanel != null) _saveLoadPanel.SetActive(false);
        }
        
        /// <summary>
        /// Called by back button in sub-panels
        /// </summary>
        public void BackToMainPanel()
        {
            ShowMainPanel();
        }
    }
}
