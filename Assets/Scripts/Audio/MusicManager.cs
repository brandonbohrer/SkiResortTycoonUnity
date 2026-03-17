using UnityEngine;
using SkiResortTycoon.UI;

namespace SkiResortTycoon.Audio
{
    /// <summary>
    /// Plays background music. Survives scene loads (DontDestroyOnLoad).
    /// Assign your 3 MP3s in the Inspector: track 0 plays on game start, then 1 and 2 can follow.
    /// Respects Settings → Music Volume and Master Volume.
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        private static MusicManager _instance;

        [Tooltip("Track 0 = plays when game starts. Tracks 1 & 2 = optional playlist after.")]
        [SerializeField] private AudioClip[] _tracks = new AudioClip[3];

        [Tooltip("After track 0, play 1 and 2 in order (true) or random (false).")]
        [SerializeField] private bool _playlistInOrder = true;

        [SerializeField] private bool _loopPlaylist = true;

        private AudioSource _source;
        private int _lastAppliedMusicVolume = -1;
        private int _playlistIndex;

        public static MusicManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.priority = 0;
            _source.spatialBlend = 0f;  // 2D so music isn't spatialized (avoids pitch/speed quirks)
            _source.pitch = 1f;
        }

        private void Start()
        {
            ApplyVolume();
            if (_tracks != null && _tracks.Length > 0 && _tracks[0] != null)
            {
                _source.clip = _tracks[0];
                _source.Play();
                _playlistIndex = 0;
            }
        }

        private void Update()
        {
            ApplyVolumeIfChanged();

            if (_tracks == null || _tracks.Length < 2) return;
            if (!_source.isPlaying && _source.clip != null)
            {
                _playlistIndex = _playlistInOrder
                    ? (_playlistIndex + 1) % _tracks.Length
                    : Random.Range(0, _tracks.Length);
                if (_playlistIndex == 0 && !_loopPlaylist) return;
                AudioClip next = _tracks[_playlistIndex];
                if (next != null)
                {
                    _source.clip = next;
                    _source.Play();
                }
            }
        }

        private void ApplyVolumeIfChanged()
        {
            int current = GameSettings.MusicVolume;
            if (current == _lastAppliedMusicVolume) return;
            ApplyVolume();
        }

        /// <summary>
        /// Call from settings UI when user changes music volume for immediate feedback.
        /// </summary>
        public void ApplyVolume()
        {
            _lastAppliedMusicVolume = GameSettings.MusicVolume;
            if (_source != null)
                _source.volume = Mathf.Clamp01(_lastAppliedMusicVolume / 100f);
        }
    }
}
