using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SkiResortTycoon.Saving;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Main menu: Continue = load most recent save and go to game. Play = open Load Game menu.
    /// Assign Continue button, Play button, and the Load Game panel in the inspector.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("Scene name to load (e.g. Main). Add to Build Settings.")]
        [SerializeField] private string _gameSceneName = "Main";

        [Header("Buttons")]
        [Tooltip("Continue = auto-load most recent save and go to game. If no saves, starts new game.")]
        [SerializeField] private Button _continueButton;
        [Tooltip("Play = open the Load Game panel (pick a save or create + New Game).")]
        [SerializeField] private Button _playButton;
        [Tooltip("Quit = exit the application. Only used in main menu.")]
        [SerializeField] private Button _quitButton;

        [Header("Panels")]
        [Tooltip("Load Game panel (save list, + New Game, Play/Rename/Delete per slot).")]
        [SerializeField] private GameObject _loadGamePanel;
        [Tooltip("Optional: Back button on the load panel to close it.")]
        [SerializeField] private Button _loadGameBackButton;

        private void Awake()
        {
            if (_loadGamePanel != null)
                _loadGamePanel.SetActive(false);

            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnContinueClicked);
            if (_playButton != null)
                _playButton.onClick.AddListener(OnPlayClicked);
            if (_quitButton != null)
                _quitButton.onClick.AddListener(OnQuitClicked);
            if (_loadGameBackButton != null)
                _loadGameBackButton.onClick.AddListener(OnLoadGameBackClicked);
        }

        private void Start()
        {
            StartCoroutine(ApplyButtonHoverAfterFirstFrame());
        }

        private IEnumerator ApplyButtonHoverAfterFirstFrame()
        {
            yield return null;
            ButtonHoverFeedback.ApplyAllInScene(null);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnContinueClicked()
        {
            string path = GameSaveService.GetMostRecentSavePath();
            GameLoadBootstrap.PendingSavePath = path;
            SceneManager.LoadScene(_gameSceneName);
        }

        private void OnPlayClicked()
        {
            if (_loadGamePanel != null)
            {
                _loadGamePanel.SetActive(true);
                var panel = _loadGamePanel.GetComponent<MainMenuLoadPanel>();
                if (panel != null)
                    panel.OnPanelOpened();
            }
        }

        private void OnLoadGameBackClicked()
        {
            if (_loadGamePanel != null)
                _loadGamePanel.SetActive(false);
        }
    }
}
