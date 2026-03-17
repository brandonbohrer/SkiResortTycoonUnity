using UnityEngine;
using SkiResortTycoon.Saving;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Place in the game scene (e.g. Main). When the scene loads, if the user chose
    /// Load Game from the main menu, applies the selected save to the simulation.
    /// Assign SimulationRunner in the inspector.
    /// </summary>
    public class GameLoadApplier : MonoBehaviour
    {
        [SerializeField] private SimulationRunner _simulationRunner;

        private void Start()
        {
            string path = GameLoadBootstrap.PendingSavePath;
            if (string.IsNullOrEmpty(path)) return;

            GameLoadBootstrap.PendingSavePath = null;

            var data = GameSaveService.Load(path);
            if (data != null)
            {
                if (_simulationRunner == null)
                    _simulationRunner = FindObjectOfType<SimulationRunner>();
                if (_simulationRunner != null)
                    GameSaveService.ApplyToGame(data, _simulationRunner);
            }
        }
    }
}
