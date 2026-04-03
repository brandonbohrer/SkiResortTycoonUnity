using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// One-time intro: random morning day 3–6 with modal (Yes = economy boost, No = normal sim).
    /// Snow + white trees always show when the modal opens. After intro completes, each new day has a
    /// random chance of cosmetic powder only (no modal, no economy).
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

        [Header("Yes — lower prices + boost capacity (intro only)")]
        [SerializeField] private float _acceptedDemandMultiplier = 1.28f;
        [SerializeField] private float _acceptedSatisfactionMultiplier = 0.91f;
        [Tooltip("Applied to target on-mountain skiers and visitor arrivals when intro Yes is chosen.")]
        [SerializeField] private float _powderActiveSkierTargetMultiplier = 2.35f;

        [Header("After intro — random cosmetic powder days")]
        [Tooltip("Chance each morning (after intro is done) for white trees + snow only; no modal or economy.")]
        [SerializeField] private float _dailyVisualPowderChance = 0.1f;

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
        /// Load / start-of-scene: no daily roll (preserves save state).
        /// </summary>
        public void SyncPowderDayUi()
        {
            EnsurePowderSchedule();
            if (!TryShowPowderMorningIfNeeded())
                RestorePowderPresentationIfNeeded();
        }

        /// <summary>
        /// Call after <see cref="Simulation.EndDay"/> rolls to a new morning: daily cosmetic chance + intro modal.
        /// </summary>
        public void AfterEndOfDayRollAndSync()
        {
            EnsurePowderSchedule();
            RollDailyVisualPowder();
            if (!TryShowPowderMorningIfNeeded())
                RestorePowderPresentationIfNeeded();
        }

        /// <summary>
        /// Call at the start of end-of-day processing when the day that just ended may have had powder visuals.
        /// </summary>
        public void OnPowderDayEnded(int dayThatJustFinished)
        {
            SimulationState s = _simulationRunner != null ? _simulationRunner.Sim.State : null;
            if (s == null) return;

            bool introVisuals =
                dayThatJustFinished == s.PowderDayTargetDay && s.PowderDayModalDone;
            bool dailyVisuals = s.VisualPowderDayActive;

            if (introVisuals || dailyVisuals)
                EndPowderPresentation();
        }

        private void EnsurePowderSchedule()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;
            SimulationState state = _simulationRunner.Sim.State;

            if (state.PowderDayTargetDay != 0) return;

            state.PowderDayTargetDay = Random.Range(PowderDayMin, PowderDayMax + 1);
            if (state.DayIndex > state.PowderDayTargetDay)
            {
                state.PowderDayModalDone = true;
                state.PowderIntroCompleted = true;
            }
        }

        private void RollDailyVisualPowder()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;
            SimulationState state = _simulationRunner.Sim.State;

            if (!state.PowderIntroCompleted)
            {
                state.VisualPowderDayActive = false;
                return;
            }

            if (state.DayIndex == state.PowderDayTargetDay && !state.PowderDayModalDone)
            {
                state.VisualPowderDayActive = false;
                return;
            }

            float p = Mathf.Clamp01(_dailyVisualPowderChance);
            state.VisualPowderDayActive = Random.value < p;
        }

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

            if (state.VisualPowderDayActive && state.PowderIntroCompleted)
            {
                BeginPowderPresentation();
                return;
            }

            if (state.DayIndex == state.PowderDayTargetDay && state.PowderDayModalDone)
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
                    "<b>No</b> — run pricing and staffing as usual today (still a powder morning outside).";
            }

            TimeController tc = _simulationRunner.Sim.TimeController;
            _wasPausedByPowder = tc != null && !tc.IsPaused;
            if (tc != null)
                tc.Pause();

            BeginPowderPresentation();
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
            state.PowderDayActiveSkierTargetMultiplier = 1f;
            state.PowderDayModalDone = true;
            state.PowderIntroCompleted = true;

            if (_rootPanel != null)
                _rootPanel.SetActive(false);

            TimeController tc = _simulationRunner.Sim.TimeController;
            if (tc != null && _wasPausedByPowder)
                tc.Resume();

            Debug.Log("[PowderDay] Declined — no rush modifiers (visuals stay until night).");
        }

        private void ApplyAccepted()
        {
            if (_simulationRunner == null) return;
            SimulationState state = _simulationRunner.Sim.State;

            state.ActivePowderChoice = PowderDayChoice.Accepted;
            state.PowderDemandEventMultiplier = _acceptedDemandMultiplier;
            state.PowderSatisfactionEventMultiplier = _acceptedSatisfactionMultiplier;
            state.PowderDayActiveSkierTargetMultiplier = Mathf.Max(1f, _powderActiveSkierTargetMultiplier);
            state.PowderDayModalDone = true;
            state.PowderIntroCompleted = true;

            if (_rootPanel != null)
                _rootPanel.SetActive(false);

            TimeController tc = _simulationRunner.Sim.TimeController;
            if (tc != null && _wasPausedByPowder)
                tc.Resume();

            Debug.Log(
                $"[PowderDay] Accepted — lower prices + extra capacity " +
                $"(demand x{_acceptedDemandMultiplier:F2}, sat x{_acceptedSatisfactionMultiplier:F2}, " +
                $"skiers/visitors x{state.PowderDayActiveSkierTargetMultiplier:F2}).");
        }
    }
}
