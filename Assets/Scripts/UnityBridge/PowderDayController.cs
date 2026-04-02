using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// One-time "Powder Day" morning modal (random calendar day 3–6). Pauses; if the player accepts,
    /// optional snow VFX, snowy trees, and combined "lower prices + extra capacity" day modifiers apply.
    /// </summary>
    public class PowderDayController : MonoBehaviour
    {
        private const int PowderDayMin = 3;
        private const int PowderDayMax = 6;

        [Header("References")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private GameObject _rootPanel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _bodyText;
        [FormerlySerializedAs("_dealButton")]
        [SerializeField] private Button _yesButton;
        [FormerlySerializedAs("_capacityButton")]
        [SerializeField] private Button _noButton;
        [SerializeField] private TreeClearer _treeClearer;
        [SerializeField] private GameObject _snowfallObject;
        [SerializeField] private GameObject _snowyTreePrefab;

        [Header("Yes — lower prices + boost capacity (free)")]
        [SerializeField] private float _acceptedDemandMultiplier = 1.28f;
        [SerializeField] private float _acceptedSatisfactionMultiplier = 0.91f;

        private bool _wasPausedByPowder;

        private void Awake()
        {
            if (_simulationRunner == null)
                _simulationRunner = FindObjectOfType<SimulationRunner>();
            if (_treeClearer == null)
                _treeClearer = FindObjectOfType<TreeClearer>();

            if (_rootPanel != null)
                _rootPanel.SetActive(false);
            SetSnowActive(false);

            if (_yesButton != null)
                _yesButton.onClick.AddListener(OnChooseYes);
            if (_noButton != null)
                _noButton.onClick.AddListener(OnChooseNo);
        }

        private void Start()
        {
            SyncPowderDayUi();
        }

        /// <summary>
        /// Re-evaluate schedule, morning modal, and mid-day visuals (after load or day rollover).
        /// </summary>
        public void SyncPowderDayUi()
        {
            EnsurePowderSchedule();
            if (!TryShowPowderMorningIfNeeded())
                RestorePowderPresentationIfNeeded();
        }

        /// <summary>
        /// Call at the start of end-of-day processing when the powder day is finishing.
        /// </summary>
        public void OnPowderDayEnded(int dayThatJustFinished)
        {
            SimulationState s = _simulationRunner != null ? _simulationRunner.Sim.State : null;
            if (s == null) return;
            if (dayThatJustFinished != s.PowderDayTargetDay) return;
            if (!s.PowderDayModalDone) return;

            EndPowderPresentation();
        }

        private void EnsurePowderSchedule()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;
            SimulationState state = _simulationRunner.Sim.State;

            if (state.PowderDayTargetDay != 0) return;

            state.PowderDayTargetDay = Random.Range(PowderDayMin, PowderDayMax + 1);
            if (state.DayIndex > state.PowderDayTargetDay)
                state.PowderDayModalDone = true;
        }

        /// <returns>True if the modal was opened (caller should not restore visuals separately).</returns>
        private bool TryShowPowderMorningIfNeeded()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return false;
            SimulationState state = _simulationRunner.Sim.State;
            EnsurePowderSchedule();

            if (state.PowderDayModalDone) return false;
            if (state.DayIndex != state.PowderDayTargetDay) return false;
            if (_rootPanel == null) return false;

            ShowModal();
            return true;
        }

        private void RestorePowderPresentationIfNeeded()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;
            SimulationState state = _simulationRunner.Sim.State;
            if (state.DayIndex != state.PowderDayTargetDay) return;
            if (!state.PowderDayModalDone) return;
            if (state.ActivePowderChoice != PowderDayChoice.Accepted) return;

            BeginPowderPresentation();
        }

        private void ShowModal()
        {
            SimulationState state = _simulationRunner.Sim.State;
            if (state.PowderDayModalDone) return;

            _rootPanel.SetActive(true);
            if (_titleText != null)
                _titleText.text = "Powder Day";
            if (_bodyText != null)
            {
                _bodyText.text =
                    "Fresh snow overnight — word is out.\n\n" +
                    "<b>Yes</b> — cut ticket prices for the day and bring in extra staffing to boost capacity " +
                    "(bigger crowds, but you are trying to keep lines in check).\n\n" +
                    "<b>No</b> — run a normal day. No special rush.";
            }

            TimeController tc = _simulationRunner.Sim.TimeController;
            _wasPausedByPowder = tc != null && !tc.IsPaused;
            if (tc != null)
                tc.Pause();
        }

        private void BeginPowderPresentation()
        {
            SetSnowActive(true);
            if (_treeClearer != null && _snowyTreePrefab != null)
                _treeClearer.BeginPowderTreeOverlay(_snowyTreePrefab);
        }

        private void EndPowderPresentation()
        {
            SetSnowActive(false);
            if (_treeClearer != null)
                _treeClearer.EndPowderTreeOverlay();
        }

        private void SetSnowActive(bool on)
        {
            if (_snowfallObject != null)
                _snowfallObject.SetActive(on);
        }

        private void OnChooseYes()
        {
            ApplyAccepted();
        }

        private void OnChooseNo()
        {
            if (_simulationRunner == null) return;
            SimulationState state = _simulationRunner.Sim.State;

            state.ActivePowderChoice = PowderDayChoice.Declined;
            state.PowderDemandEventMultiplier = 1f;
            state.PowderSatisfactionEventMultiplier = 1f;
            state.PowderDayModalDone = true;

            if (_rootPanel != null)
                _rootPanel.SetActive(false);

            TimeController tc = _simulationRunner.Sim.TimeController;
            if (tc != null && _wasPausedByPowder)
                tc.Resume();

            Debug.Log("[PowderDay] Declined — no rush modifiers.");
        }

        private void ApplyAccepted()
        {
            if (_simulationRunner == null) return;
            SimulationState state = _simulationRunner.Sim.State;

            state.ActivePowderChoice = PowderDayChoice.Accepted;
            state.PowderDemandEventMultiplier = _acceptedDemandMultiplier;
            state.PowderSatisfactionEventMultiplier = _acceptedSatisfactionMultiplier;
            state.PowderDayModalDone = true;

            if (_rootPanel != null)
                _rootPanel.SetActive(false);

            BeginPowderPresentation();

            TimeController tc = _simulationRunner.Sim.TimeController;
            if (tc != null && _wasPausedByPowder)
                tc.Resume();

            Debug.Log(
                $"[PowderDay] Accepted — lower prices + extra capacity " +
                $"(demand x{_acceptedDemandMultiplier:F2}, sat x{_acceptedSatisfactionMultiplier:F2}).");
        }
    }
}
