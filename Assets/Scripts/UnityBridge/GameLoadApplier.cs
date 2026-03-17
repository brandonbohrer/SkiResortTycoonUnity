using System.Collections;
using UnityEngine;
using SkiResortTycoon.Saving;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Place in the game scene (e.g. Main). When the scene loads, if the user chose
    /// Load Game from the main menu, applies the selected save (state + lifts + trails + lodges + skiers).
    /// Assign all references in the inspector so full restore can run after systems are ready.
    /// </summary>
    public class GameLoadApplier : MonoBehaviour
    {
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private TrailDrawer _trailDrawer;
        [SerializeField] private LodgeBuilder _lodgeBuilder;
        [SerializeField] private LodgeManager _lodgeManager;
        [SerializeField] private SkierVisualizer _skierVisualizer;

        [Tooltip("Max frames to wait for LiftSystem/TrailSystem before applying. Increase if your scene initializes slowly.")]
        [SerializeField] private int _maxWaitFrames = 30;

        private void Start()
        {
            string path = GameLoadBootstrap.PendingSavePath;
            if (string.IsNullOrEmpty(path)) return;

            GameLoadBootstrap.PendingSavePath = null;
            var data = GameSaveService.Load(path);
            if (data == null) return;

            if (_simulationRunner == null) _simulationRunner = FindObjectOfType<SimulationRunner>();
            if (_liftBuilder == null) _liftBuilder = FindObjectOfType<LiftBuilder>();
            if (_trailDrawer == null) _trailDrawer = FindObjectOfType<TrailDrawer>();
            if (_lodgeBuilder == null) _lodgeBuilder = FindObjectOfType<LodgeBuilder>();
            if (_lodgeManager == null) _lodgeManager = LodgeManager.Instance;
            if (_skierVisualizer == null) _skierVisualizer = FindObjectOfType<SkierVisualizer>();

            bool canFullApply = _liftBuilder != null && _trailDrawer != null && _lodgeBuilder != null;
            if (canFullApply)
                StartCoroutine(ApplyWhenReady(data));
            else if (_simulationRunner != null)
                GameSaveService.ApplyToGame(data, _simulationRunner);
        }

        private IEnumerator ApplyWhenReady(GameSaveData data)
        {
            int waited = 0;
            while (waited < _maxWaitFrames)
            {
                if (_liftBuilder.LiftSystem != null && _trailDrawer.TrailSystem != null)
                    break;
                waited++;
                yield return null;
            }
            if (_liftBuilder.LiftSystem == null || _trailDrawer.TrailSystem == null)
            {
                Debug.LogWarning("[GameLoadApplier] Lift or Trail system not ready after wait; applying state only.");
                if (_simulationRunner != null)
                    GameSaveService.ApplyToGame(data, _simulationRunner);
                yield break;
            }
            GameSaveService.ApplyToGameFull(data, _simulationRunner, _liftBuilder, _trailDrawer, _lodgeBuilder, _lodgeManager, _skierVisualizer);

            // Re-register base lodge snap points after a short delay so that all Invoke-based
            // registrars (BaseSnapPointRegistrar at 0.1s, PreplacedLodge at 0.15s) have already
            // fired, plus our explicit force-registration catches any that failed due to init order.
            StartCoroutine(ReRegisterBaseSnapPoints());
        }

        private IEnumerator ReRegisterBaseSnapPoints()
        {
            // Wait past the longest Invoke delay (PreplacedLodge Bootstrap at 0.15s) + buffer.
            yield return new WaitForSeconds(0.25f);

            foreach (var registrar in FindObjectsOfType<BaseSnapPointRegistrar>())
                registrar.EnsureRegistered();

            foreach (var preplaced in FindObjectsOfType<PreplacedLodge>())
                preplaced.EnsureSnapPointsRegistered();

            if (_liftBuilder?.Connectivity != null)
                _liftBuilder.Connectivity.RebuildConnections();
        }
    }
}
