using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Audio;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Wire up the Settings menu (main menu or in-game). Same script in both scenes; drag all UI refs in the Inspector.
    /// Put this on an GameObject that is active at load (e.g. Canvas) so the Settings button is wired even if the panel starts disabled.
    /// </summary>
    public class SettingsMenuController : MonoBehaviour
    {
        [Header("Panel & trigger")]
        [Tooltip("The full Settings panel (can start disabled).")]
        [SerializeField] private GameObject _settingsPanel;
        [Tooltip("Button that opens the settings panel (e.g. SETTINGS header or a gear icon).")]
        [SerializeField] private Button _settingsButton;

        [Header("Audio — Slider + value text (displays \"100%\")")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private TextMeshProUGUI _masterVolumeText;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private TextMeshProUGUI _musicVolumeText;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI _sfxVolumeText;

        [Header("Gameplay")]
        [SerializeField] private Slider _cameraSpeedSlider;
        [SerializeField] private TextMeshProUGUI _cameraSpeedText;
        [SerializeField] private TMP_Dropdown _autosaveFrequencyDropdown;

        [Header("Buttons")]
        [SerializeField] private Button _resetToDefaultsButton;
        [SerializeField] private Button _backButton;

        private void Awake()
        {
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OpenSettings);
            if (_backButton != null)
                _backButton.onClick.AddListener(CloseSettings);
            if (_resetToDefaultsButton != null)
                _resetToDefaultsButton.onClick.AddListener(ResetToDefaults);

            EnsureAutosaveOptions();
            SubscribeSliders();
            if (_autosaveFrequencyDropdown != null)
                _autosaveFrequencyDropdown.onValueChanged.AddListener(OnAutosaveChanged);
        }

        private void Start()
        {
            LoadAndApply();
        }

        private void OnEnable()
        {
            GameSettings.ApplyMasterVolume();
        }

        private void SubscribeSliders()
        {
            // Sliders are 0-1 by default; we store and display 0-100
            if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.AddListener(v => { int p = SliderToPercent(v); GameSettings.MasterVolume = p; UpdateMasterText(); });
            if (_musicVolumeSlider != null) _musicVolumeSlider.onValueChanged.AddListener(v => { GameSettings.MusicVolume = SliderToPercent(v); UpdateMusicText(); MusicManager.Instance?.ApplyVolume(); });
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.onValueChanged.AddListener(v => { GameSettings.SfxVolume = SliderToPercent(v); UpdateSfxText(); });
            if (_cameraSpeedSlider != null) _cameraSpeedSlider.onValueChanged.AddListener(v => { GameSettings.CameraSpeed = SliderToPercent(v); UpdateCameraSpeedText(); });
        }

        private static int SliderToPercent(float sliderValue)
        {
            return Mathf.Clamp(Mathf.RoundToInt(sliderValue * 100f), 0, 100);
        }

        private static float PercentToSlider(int percent)
        {
            return Mathf.Clamp01(percent / 100f);
        }

        private void UpdateMasterText()
        {
            if (_masterVolumeText != null) _masterVolumeText.text = $"{GameSettings.MasterVolume}%";
        }
        private void UpdateMusicText()
        {
            if (_musicVolumeText != null) _musicVolumeText.text = $"{GameSettings.MusicVolume}%";
        }
        private void UpdateSfxText()
        {
            if (_sfxVolumeText != null) _sfxVolumeText.text = $"{GameSettings.SfxVolume}%";
        }
        private void UpdateCameraSpeedText()
        {
            if (_cameraSpeedText != null) _cameraSpeedText.text = $"{GameSettings.CameraSpeed}%";
        }

        private void OnAutosaveChanged(int index)
        {
            GameSettings.AutosaveFrequencyIndex = index;
        }

        /// <summary>
        /// Call when opening the panel. Loads saved values and refreshes UI.
        /// </summary>
        public void LoadAndApply()
        {
            // Stored as 0-100; sliders use 0-1
            if (_masterVolumeSlider != null) _masterVolumeSlider.SetValueWithoutNotify(PercentToSlider(GameSettings.MasterVolume));
            if (_musicVolumeSlider != null) _musicVolumeSlider.SetValueWithoutNotify(PercentToSlider(GameSettings.MusicVolume));
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.SetValueWithoutNotify(PercentToSlider(GameSettings.SfxVolume));
            if (_cameraSpeedSlider != null) _cameraSpeedSlider.SetValueWithoutNotify(PercentToSlider(GameSettings.CameraSpeed));
            EnsureAutosaveOptions();
            if (_autosaveFrequencyDropdown != null && _autosaveFrequencyDropdown.options.Count > 0)
                _autosaveFrequencyDropdown.SetValueWithoutNotify(Mathf.Clamp(GameSettings.AutosaveFrequencyIndex, 0, _autosaveFrequencyDropdown.options.Count - 1));

            UpdateMasterText();
            UpdateMusicText();
            UpdateSfxText();
            UpdateCameraSpeedText();
            GameSettings.ApplyMasterVolume();
            MusicManager.Instance?.ApplyVolume();
        }

        private void EnsureAutosaveOptions()
        {
            if (_autosaveFrequencyDropdown == null) return;
            if (_autosaveFrequencyDropdown.options.Count == 3 &&
                _autosaveFrequencyDropdown.options[0].text == "Daily") return;
            _autosaveFrequencyDropdown.ClearOptions();
            _autosaveFrequencyDropdown.AddOptions(new System.Collections.Generic.List<string> { "Daily", "Weekly", "Never" });
        }

        public void OpenSettings()
        {
            LoadAndApply();
            if (_settingsPanel != null)
                _settingsPanel.SetActive(true);
        }

        public void CloseSettings()
        {
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);
        }

        private void ResetToDefaults()
        {
            GameSettings.ResetToDefaults();
            LoadAndApply();
        }
    }
}
