using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Shared modal UI for random events: powder day (intro + daily visuals) and rare injury lawsuits.
    /// Wire one panel, title, body, and two action buttons.
    /// </summary>
    public class RandomEventController : MonoBehaviour
    {
        private enum ModalKind
        {
            None,
            PowderIntro,
            Lawsuit
        }

        private const int PowderDayMin = 3;
        private const int PowderDayMax = 6;

        [Header("References")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private GameObject _rootPanel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _bodyText;
        [FormerlySerializedAs("_dealButton")]
        [SerializeField] private Button _primaryButton;
        [FormerlySerializedAs("_capacityButton")]
        [SerializeField] private Button _secondaryButton;
        [Header("Button captions")]
        [Tooltip("TextMeshPro on the primary button (drag from hierarchy).")]
        [SerializeField] private TextMeshProUGUI _primaryButtonCaption;
        [Tooltip("TextMeshPro on the secondary button.")]
        [SerializeField] private TextMeshProUGUI _secondaryButtonCaption;
        [Header("Powder intro — caption templates (optional)")]
        [Tooltip("If set, .text is copied to the primary button when the powder modal opens. Edit text on these objects in the scene.")]
        [SerializeField] private TextMeshProUGUI _powderPrimaryCaptionTemplate;
        [SerializeField] private TextMeshProUGUI _powderSecondaryCaptionTemplate;
        [Header("Lawsuit — caption templates (optional)")]
        [Tooltip("Primary: put {0} in the text for settlement amount. If unset, defaults are applied.")]
        [SerializeField] private TextMeshProUGUI _lawsuitPrimaryCaptionTemplate;
        [SerializeField] private TextMeshProUGUI _lawsuitSecondaryCaptionTemplate;
        [SerializeField] private TreeClearer _treeClearer;
        [SerializeField] private GameObject _snowfallObject;
        [SerializeField] private GameObject _snowyTreePrefab;

        [Header("Powder — Yes: lower prices + boost capacity (intro only)")]
        [SerializeField] private float _acceptedDemandMultiplier = 1.28f;
        [SerializeField] private float _acceptedSatisfactionMultiplier = 0.91f;
        [Tooltip("Applied to target on-mountain skiers and visitor arrivals when intro Yes is chosen.")]
        [SerializeField] private float _powderActiveSkierTargetMultiplier = 2.35f;

        [Header("Powder — after intro, random cosmetic days")]
        [Tooltip("Chance each morning (after intro is done) for white trees + snow only; no modal or economy.")]
        [SerializeField] private float _dailyVisualPowderChance = 0.1f;

        [Header("Lawsuit — beginner fall on black+ slope")]
        [Tooltip("Rolled once per qualifying fall. 0.001 = 1/1000.")]
        [SerializeField] private float _lawsuitChancePerQualifyingFall = 0.001f;
        [SerializeField] private int _settlementCost = 28000;
        [Tooltip("Subtracted from resort satisfaction (0–100 scale).")]
        [SerializeField] private float _courtSatisfactionPenalty = 10f;

        private ModalKind _modalKind;
        private bool _wasPausedByModal;

        private void Awake()
        {
            if (_simulationRunner == null)
                _simulationRunner = FindObjectOfType<SimulationRunner>();
            if (_treeClearer == null)
                _treeClearer = FindObjectOfType<TreeClearer>();

            if (_rootPanel != null)
                _rootPanel.SetActive(false);
            SetSnowActive(false);

            if (_primaryButton != null)
                _primaryButton.onClick.AddListener(OnPrimaryClicked);
            if (_secondaryButton != null)
                _secondaryButton.onClick.AddListener(OnSecondaryClicked);

            EnsureButtonCaptionRefs();
        }

        private void EnsureButtonCaptionRefs()
        {
            if (_primaryButtonCaption == null && _primaryButton != null)
                _primaryButtonCaption = _primaryButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (_secondaryButtonCaption == null && _secondaryButton != null)
                _secondaryButtonCaption = _secondaryButton.GetComponentInChildren<TextMeshProUGUI>(true);
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
        /// Call after <see cref="Simulation.EndDay"/> rolls to a new morning.
        /// </summary>
        public void AfterEndOfDayRollAndSync()
        {
            EnsurePowderSchedule();
            RollDailyVisualPowder();
            if (!TryShowPowderMorningIfNeeded())
                RestorePowderPresentationIfNeeded();
        }

        /// <summary>
        /// Start of end-of-day: clear powder visuals for the day that just finished.
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

        /// <summary>
        /// Called from <see cref="SkierVisualizer"/> when a skier begins a fall.
        /// </summary>
        public static void TryTriggerLawsuitFromFall(SimulationRunner runner, Skier skier, TrailData trail)
        {
            if (runner == null || skier == null || trail == null) return;
            var ctrl = FindObjectOfType<RandomEventController>();
            ctrl?.TryQualifyingLawsuitFall(skier, trail);
        }

        private void TryQualifyingLawsuitFall(Skier skier, TrailData trail)
        {
            if (_modalKind != ModalKind.None) return;
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;

            if (skier.Skill != SkillLevel.Beginner) return;
            if (trail.SlopeDifficulty < TrailDifficulty.Black) return;

            float p = Mathf.Clamp01(_lawsuitChancePerQualifyingFall);
            if (Random.value >= p) return;

            ShowLawsuitModal(skier, trail);
        }

        private void OnPrimaryClicked()
        {
            switch (_modalKind)
            {
                case ModalKind.PowderIntro:
                    ApplyPowderAccepted();
                    break;
                case ModalKind.Lawsuit:
                    OnLawsuitSettle();
                    break;
            }
        }

        private void OnSecondaryClicked()
        {
            switch (_modalKind)
            {
                case ModalKind.PowderIntro:
                    OnPowderNo();
                    break;
                case ModalKind.Lawsuit:
                    OnLawsuitCourt();
                    break;
            }
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
            if (_modalKind != ModalKind.None) return false;

            ShowPowderIntroModal();
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

        private void ShowPowderIntroModal()
        {
            SimulationState state = _simulationRunner.Sim.State;
            if (state.PowderDayModalDone) return;

            _modalKind = ModalKind.PowderIntro;
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
            ApplyPowderButtonCaptions();

            TimeController tc = _simulationRunner.Sim.TimeController;
            _wasPausedByModal = tc != null && !tc.IsPaused;
            if (tc != null)
                tc.Pause();

            BeginPowderPresentation();
        }

        private void ShowLawsuitModal(Skier skier, TrailData trail)
        {
            if (_rootPanel == null) return;

            _modalKind = ModalKind.Lawsuit;
            _rootPanel.SetActive(true);

            string name = string.IsNullOrEmpty(skier.DisplayName) ? "A guest" : skier.DisplayName;
            string terrain = trail.SlopeDifficulty == TrailDifficulty.DoubleBlack
                ? "double-black terrain"
                : "black-diamond terrain";

            if (_titleText != null)
                _titleText.text = "Injury claim";
            if (_bodyText != null)
            {
                _bodyText.text =
                    $"<b>{name}</b>, a beginner, was seriously hurt after falling on <b>{terrain}</b> " +
                    $"({trail.Name ?? "unnamed trail"}).\n\n" +
                    "Their attorney is demanding damages. You can offer a private settlement or fight it in court " +
                    "(bad press either way if you lose the public narrative).\n\n" +
                    $"<b>Settle</b> — pay <b>${_settlementCost:N0}</b> to make this go away.\n" +
                    $"<b>Go to court</b> — pay nothing now, but expect roughly " +
                    $"<b>{_courtSatisfactionPenalty:F0} points</b> of resort reputation damage.";
            }
            ApplyLawsuitButtonCaptions();

            TimeController tc = _simulationRunner.Sim.TimeController;
            _wasPausedByModal = tc != null && !tc.IsPaused;
            if (tc != null)
                tc.Pause();

            Debug.Log($"[Lawsuit] Triggered for {name} on trail {trail.TrailId} ({terrain}).");
        }

        private void ApplyPowderButtonCaptions()
        {
            string primary = "Yes";
            string secondary = "No";
            if (_powderPrimaryCaptionTemplate != null && !string.IsNullOrWhiteSpace(_powderPrimaryCaptionTemplate.text))
                primary = _powderPrimaryCaptionTemplate.text.Trim();
            if (_powderSecondaryCaptionTemplate != null && !string.IsNullOrWhiteSpace(_powderSecondaryCaptionTemplate.text))
                secondary = _powderSecondaryCaptionTemplate.text.Trim();
            SetButtonCaptions(primary, secondary);
        }

        private void ApplyLawsuitButtonCaptions()
        {
            string primary = $"Pay ${_settlementCost:N0}";
            string secondary = "Court";
            if (_lawsuitPrimaryCaptionTemplate != null && !string.IsNullOrWhiteSpace(_lawsuitPrimaryCaptionTemplate.text))
            {
                primary = _lawsuitPrimaryCaptionTemplate.text.Trim();
                if (primary.Contains("{0}"))
                {
                    try
                    {
                        primary = string.Format(primary, _settlementCost);
                    }
                    catch (System.FormatException)
                    {
                        // keep primary as-is
                    }
                }
            }
            if (_lawsuitSecondaryCaptionTemplate != null && !string.IsNullOrWhiteSpace(_lawsuitSecondaryCaptionTemplate.text))
                secondary = _lawsuitSecondaryCaptionTemplate.text.Trim();
            SetButtonCaptions(primary, secondary);
        }

        private void SetButtonCaptions(string primary, string secondary)
        {
            if (_primaryButtonCaption != null)
                _primaryButtonCaption.text = primary;
            if (_secondaryButtonCaption != null)
                _secondaryButtonCaption.text = secondary;
        }

        private void ApplyPowderAccepted()
        {
            if (_simulationRunner == null || _modalKind != ModalKind.PowderIntro) return;
            SimulationState state = _simulationRunner.Sim.State;

            state.ActivePowderChoice = PowderDayChoice.Accepted;
            state.PowderDemandEventMultiplier = _acceptedDemandMultiplier;
            state.PowderSatisfactionEventMultiplier = _acceptedSatisfactionMultiplier;
            state.PowderDayActiveSkierTargetMultiplier = Mathf.Max(1f, _powderActiveSkierTargetMultiplier);
            state.PowderDayModalDone = true;
            state.PowderIntroCompleted = true;

            CloseModal();
            Debug.Log(
                $"[PowderDay] Accepted — lower prices + extra capacity " +
                $"(demand x{_acceptedDemandMultiplier:F2}, sat x{_acceptedSatisfactionMultiplier:F2}, " +
                $"skiers/visitors x{state.PowderDayActiveSkierTargetMultiplier:F2}).");
        }

        private void OnPowderNo()
        {
            if (_simulationRunner == null || _modalKind != ModalKind.PowderIntro) return;
            SimulationState state = _simulationRunner.Sim.State;

            state.ActivePowderChoice = PowderDayChoice.Declined;
            state.PowderDemandEventMultiplier = 1f;
            state.PowderSatisfactionEventMultiplier = 1f;
            state.PowderDayActiveSkierTargetMultiplier = 1f;
            state.PowderDayModalDone = true;
            state.PowderIntroCompleted = true;

            CloseModal();
            Debug.Log("[PowderDay] Declined — no rush modifiers (visuals stay until night).");
        }

        private void OnLawsuitSettle()
        {
            if (_simulationRunner == null || _modalKind != ModalKind.Lawsuit) return;
            var state = _simulationRunner.Sim.State;
            state.Money = Mathf.Max(0, state.Money - _settlementCost);
            CloseModal();
            Debug.Log($"[Lawsuit] Settled for ${_settlementCost:N0}.");
        }

        private void OnLawsuitCourt()
        {
            if (_simulationRunner == null || _modalKind != ModalKind.Lawsuit) return;
            _simulationRunner.Sim.Satisfaction.ApplyResortDelta(-Mathf.Abs(_courtSatisfactionPenalty));
            CloseModal();
            Debug.Log($"[Lawsuit] Went to court — satisfaction -{_courtSatisfactionPenalty:F0}.");
        }

        private void CloseModal()
        {
            if (_rootPanel != null)
                _rootPanel.SetActive(false);
            _modalKind = ModalKind.None;

            TimeController tc = _simulationRunner != null ? _simulationRunner.Sim?.TimeController : null;
            if (tc != null && _wasPausedByModal)
                tc.Resume();
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
    }
}
