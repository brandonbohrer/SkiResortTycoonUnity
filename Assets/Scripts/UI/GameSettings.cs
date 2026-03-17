using UnityEngine;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Persistent settings (PlayerPrefs). Apply volume to AudioListener; other scripts can read CameraSpeed, etc.
    /// </summary>
    public static class GameSettings
    {
        private const string KeyMaster = "Settings_MasterVolume";
        private const string KeyMusic = "Settings_MusicVolume";
        private const string KeySfx = "Settings_SFXVolume";
        private const string KeyCameraSpeed = "Settings_CameraSpeed";
        private const string KeyAutosave = "Settings_AutosaveFrequency";

        private const int DefaultVolume = 100;
        private const int DefaultCameraSpeed = 100;
        private const int DefaultAutosaveIndex = 0;

        public static int MasterVolume
        {
            get => PlayerPrefs.GetInt(KeyMaster, DefaultVolume);
            set
            {
                value = Mathf.Clamp(value, 0, 100);
                PlayerPrefs.SetInt(KeyMaster, value);
                ApplyMasterVolume();
            }
        }

        public static int MusicVolume
        {
            get => PlayerPrefs.GetInt(KeyMusic, DefaultVolume);
            set
            {
                value = Mathf.Clamp(value, 0, 100);
                PlayerPrefs.SetInt(KeyMusic, value);
            }
        }

        public static int SfxVolume
        {
            get => PlayerPrefs.GetInt(KeySfx, DefaultVolume);
            set
            {
                value = Mathf.Clamp(value, 0, 100);
                PlayerPrefs.SetInt(KeySfx, value);
            }
        }

        public static int CameraSpeed
        {
            get => PlayerPrefs.GetInt(KeyCameraSpeed, DefaultCameraSpeed);
            set
            {
                value = Mathf.Clamp(value, 0, 100);
                PlayerPrefs.SetInt(KeyCameraSpeed, value);
            }
        }

        public static int AutosaveFrequencyIndex
        {
            get => PlayerPrefs.GetInt(KeyAutosave, DefaultAutosaveIndex);
            set => PlayerPrefs.SetInt(KeyAutosave, value);
        }

        public static void ApplyMasterVolume()
        {
            if (AudioListener.volume != MasterVolume / 100f)
                AudioListener.volume = MasterVolume / 100f;
        }

        public static void ResetToDefaults()
        {
            PlayerPrefs.SetInt(KeyMaster, DefaultVolume);
            PlayerPrefs.SetInt(KeyMusic, DefaultVolume);
            PlayerPrefs.SetInt(KeySfx, DefaultVolume);
            PlayerPrefs.SetInt(KeyCameraSpeed, DefaultCameraSpeed);
            PlayerPrefs.SetInt(KeyAutosave, DefaultAutosaveIndex);
            ApplyMasterVolume();
        }
    }
}
