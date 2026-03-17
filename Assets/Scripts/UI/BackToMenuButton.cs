using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// In the game (Main) scene: assign the Quit button and the main menu scene name.
    /// When clicked, loads the main menu (does not exit the application).
    /// </summary>
    public class BackToMenuButton : MonoBehaviour
    {
        [Tooltip("Scene name for the main menu (e.g. MainMenu). Must be in Build Settings.")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        [SerializeField] private Button _quitButton;

        private void Awake()
        {
            if (_quitButton != null)
                _quitButton.onClick.AddListener(OnQuitToMenuClicked);
        }

        private void OnQuitToMenuClicked()
        {
            SceneManager.LoadScene(_mainMenuSceneName);
        }
    }
}
