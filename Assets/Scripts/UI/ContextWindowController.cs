using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Inspector-wired controller for the context window.
    /// Opens when you click a trail (or lift / lodge / skier in future sections).
    ///
    /// Attach to ContextWindowRoot.
    /// Drag every field in from the Inspector — no hierarchy name lookups.
    /// </summary>
    public class ContextWindowController : MonoBehaviour
    {
        public static ContextWindowController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ── Panel root ───────────────────────────────────────────────
        [Header("Panel Root")]
        [SerializeField] private GameObject  _panelRoot;
        [SerializeField] private CanvasGroup _canvasGroup;

        // ── Header ───────────────────────────────────────────────────
        [Header("Header")]
        /// <summary>Input field — user can rename the structure at any time.</summary>
        [SerializeField] private TMP_InputField _titleInput;
        [SerializeField] private TextMeshProUGUI _subtitleText;  // e.g. "Green Circle"
        [SerializeField] private Image           _iconImage;
        [SerializeField] private Button          _closeButton;

        // ── Trail section root ────────────────────────────────────────
        [Header("Trail Section")]
        [SerializeField] private GameObject _trailSection;

        // Top row
        [Header("Trail — Top Row")]
        [SerializeField] private TextMeshProUGUI _trailStatusText;  // "Open" / "Closed"
        [SerializeField] private Toggle          _trailOpenToggle;

        // Difficulty picker — always-visible icon button + 4 expand buttons
        // DifficultyPicker itself is NEVER hidden by code — only the 4 expand buttons are toggled.
        [Header("Trail — Difficulty Picker")]
        [SerializeField] private Button _difficultyPickerButton; // The main button that shows current difficulty
        [SerializeField] private Image  _difficultySetIcon;   // Image child inside DifficultyPicker button
        [SerializeField] private Sprite _greenSprite;
        [SerializeField] private Sprite _blueSprite;
        [SerializeField] private Sprite _blackSprite;
        [SerializeField] private Sprite _doubleBlackSprite;
        // The 4 expand buttons — start hidden, toggle on picker click
        [SerializeField] private Button _diffGreenButton;
        [SerializeField] private Button _diffBlueButton;
        [SerializeField] private Button _diffBlackButton;
        [SerializeField] private Button _diffDoubleBlackButton;

        // ── Lift section ─────────────────────────────────────────────
        [Header("Lift Section")]
        [SerializeField] private GameObject _liftSection;

        [Header("Lift — Top Row")]
        [SerializeField] private TextMeshProUGUI _liftStatusText;   // "Open" / "Closed"
        [SerializeField] private Toggle          _liftOpenToggle;
        [SerializeField] private TextMeshProUGUI _liftTypeText;     // e.g. "Chairlift", "Gondola"

        [Header("Lift — Stats (value text of each KeyVal row)")]
        [SerializeField] private TextMeshProUGUI _ridesTodayValue;
        [SerializeField] private TextMeshProUGUI _ridesAllTimeValue;
        [SerializeField] private TextMeshProUGUI _avgWaitTimeValue;
        [SerializeField] private TextMeshProUGUI _liftUpkeepValue;

        // Usage stats — drag the VALUE text of each KeyVal row here
        [Header("Trail — Usage Stats (value text of each KeyVal row)")]
        [SerializeField] private TextMeshProUGUI _runsTodayValue;
        [SerializeField] private TextMeshProUGUI _runsAllTimeValue;
        [SerializeField] private TextMeshProUGUI _upkeepValue;

        // Physical stats — drag the VALUE text of each KeyVal row here
        [Header("Trail — Physical Stats (value text of each KeyVal row)")]
        [SerializeField] private TextMeshProUGUI _lengthValue;
        [SerializeField] private TextMeshProUGUI _verticalDropValue;
        [SerializeField] private TextMeshProUGUI _avgSlopeValue;
        [SerializeField] private TextMeshProUGUI _maxSlopeValue;
        [SerializeField] private TextMeshProUGUI _capacityValue;

        // ── Lodge section ────────────────────────────────────────────
        [Header("Lodge Section")]
        [SerializeField] private GameObject _lodgeSection;

        [Header("Lodge — Top Row")]
        [SerializeField] private TextMeshProUGUI _lodgeStatusText;
        [SerializeField] private Toggle          _lodgeOpenToggle;

        [Header("Lodge — Buffet Price")]
        [SerializeField] private TextMeshProUGUI _buffetPriceValue;
        [SerializeField] private Button          _buffetDecrementButton;
        [SerializeField] private Button          _buffetIncrementButton;

        [Header("Lodge — Stats (value text of each KeyVal row)")]
        [SerializeField] private TextMeshProUGUI _lodgeVisitsTodayValue;
        [SerializeField] private TextMeshProUGUI _lodgeVisitsAllTimeValue;
        [SerializeField] private TextMeshProUGUI _lodgeUpkeepValue;

        // ── Lift Building section ────────────────────────────────────────────
        [Header("Lift Building Section")]
        [SerializeField] private GameObject _liftBuildSection;

        [Header("Lift Building — Stats (always visible, update live)")]
        [SerializeField] private TextMeshProUGUI _liftBuildTypeValue;
        [SerializeField] private TextMeshProUGUI _liftBuildBaseCostValue;
        [SerializeField] private TextMeshProUGUI _liftBuildLengthCostValue;
        [SerializeField] private TextMeshProUGUI _liftBuildTotalCostValue;
        [SerializeField] private TextMeshProUGUI _liftBuildUpkeepValue;
        [SerializeField] private TextMeshProUGUI _liftBuildLengthFtValue;
        [SerializeField] private TextMeshProUGUI _liftBuildVerticalValue;
        [SerializeField] private TextMeshProUGUI _liftBuildCapacityValue;

        [Header("Lift Building — Confirm / Cancel (shown only after 2nd click)")]
        [SerializeField] private Button          _liftBuildConfirmButton;
        [SerializeField] private Button          _liftBuildCancelButton;

        // ── Trail Building section ───────────────────────────────────────────
        [Header("Trail Building Section")]
        [SerializeField] private GameObject      _trailBuildSection;

        [Header("Trail Building — Stats")]
        [SerializeField] private TextMeshProUGUI _trailBuildWidthValue;
        [SerializeField] private TextMeshProUGUI _trailBuildLengthValue;
        [SerializeField] private TextMeshProUGUI _trailBuildCostValue;

        [Header("Trail Building — Confirm / Cancel")]
        [SerializeField] private Button          _trailBuildConfirmButton;
        [SerializeField] private Button          _trailBuildCancelButton;

        // ── Lodge Building section ────────────────────────────────────────────
        [Header("Lodge Building Section")]
        [SerializeField] private GameObject      _lodgeBuildSection;
        [SerializeField] private TextMeshProUGUI _lodgeBuildCostValue;

        // ── Skier section ────────────────────────────────────────────
        [Header("Skier Section")]
        [SerializeField] private GameObject _skierSection;

        [Header("Skier — Stats (value text of each KeyVal row)")]
        [SerializeField] private TextMeshProUGUI _skierStatusValue;
        [SerializeField] private TextMeshProUGUI _skierSkillValue;
        [SerializeField] private TextMeshProUGUI _skierFavoriteRunValue;
        [SerializeField] private TextMeshProUGUI _skierSatisfactionValue;
        [SerializeField] private TextMeshProUGUI _skierRunsTodayValue;
        [SerializeField] private TextMeshProUGUI _skierRunsAllTimeValue;

        // ── Difficulty subtitle colors ────────────────────────────────
        [Header("Difficulty Colors (subtitle text tint)")]
        [SerializeField] private Color _greenColor       = new Color(0.18f, 0.80f, 0.18f);
        [SerializeField] private Color _blueColor        = new Color(0.18f, 0.45f, 0.95f);
        [SerializeField] private Color _blackColor       = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color _doubleBlackColor = new Color(0.80f, 0.20f, 0.20f);

        // ── Animation ────────────────────────────────────────────────
        [Header("Animation")]
        [SerializeField] private float _fadeSpeed = 8f;

        // ── Type icons ────────────────────────────────────────────────
        [Header("Type Icons")]
        [SerializeField] private Sprite _trailIcon;
        [SerializeField] private Sprite _liftIcon;
        [SerializeField] private Sprite _lodgeIcon;
        [SerializeField] private Sprite _skierIcon;

        // ── Action buttons ────────────────────────────────────────────
        [Header("Action Buttons")]
        [SerializeField] private Button _demolishButton;
        [SerializeField] private Button _findButton;
        [SerializeField] private Button _followButton;

        [Header("Lift Upgrade (only when a lift is selected)")]
        [Tooltip("Assign the Upgrade button from the context window. Hidden for trails, lodges, skiers, and build modes.")]
        [SerializeField] private Button _liftUpgradeButton;

        // ── Internal state ────────────────────────────────────────────
        private SelectableStructure _current;
        private GameObject _liftUpgradeTooltipOverlay;
        private bool  _visible;
        private float _targetAlpha;

        // Build callbacks (shared by lift/lodge/trail build windows)
        private System.Action _liftBuildOnConfirm;
        private System.Action _liftBuildOnCancel;

        private const float MetresToFeet = 3.28084f;

        // ─────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────

        private void Start()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(OnCloseClicked);
                SetupTooltip(_closeButton, TooltipTexts.ContextWindow.CloseHeader, TooltipTexts.ContextWindow.CloseContent);
            }

            if (_trailOpenToggle != null)
            {
                _trailOpenToggle.onValueChanged.AddListener(OnOpenToggleChanged);
                SetupTooltip(_trailOpenToggle, TooltipTexts.ContextWindow.TrailStatusHeader, TooltipTexts.ContextWindow.TrailStatusContent);
            }

            if (_liftOpenToggle != null)
            {
                _liftOpenToggle.onValueChanged.AddListener(OnLiftOpenToggleChanged);
                SetupTooltip(_liftOpenToggle, TooltipTexts.ContextWindow.LiftStatusHeader, TooltipTexts.ContextWindow.LiftStatusContent);
            }

            if (_lodgeOpenToggle != null)
            {
                _lodgeOpenToggle.onValueChanged.AddListener(OnLodgeOpenToggleChanged);
                SetupTooltip(_lodgeOpenToggle, TooltipTexts.ContextWindow.LodgeStatusHeader, TooltipTexts.ContextWindow.LodgeStatusContent);
            }

            if (_buffetDecrementButton != null)
            {
                _buffetDecrementButton.onClick.AddListener(OnBuffetDecrement);
                SetupTooltip(_buffetDecrementButton, TooltipTexts.ContextWindow.BuffetDecrementHeader, TooltipTexts.ContextWindow.BuffetDecrementContent);
            }
            if (_buffetIncrementButton != null)
            {
                _buffetIncrementButton.onClick.AddListener(OnBuffetIncrement);
                SetupTooltip(_buffetIncrementButton, TooltipTexts.ContextWindow.BuffetIncrementHeader, TooltipTexts.ContextWindow.BuffetIncrementContent);
            }

            // Title input — rename structure when user finishes editing
            if (_titleInput != null)
                _titleInput.onEndEdit.AddListener(OnTitleEndEdit);

            // Difficulty picker main button (shows current difficulty)
            // Try to find button automatically if not set (it's the parent of the difficulty icon)
            if (_difficultyPickerButton == null && _difficultySetIcon != null)
            {
                _difficultyPickerButton = _difficultySetIcon.GetComponentInParent<Button>();
            }
            
            if (_difficultyPickerButton != null)
            {
                _difficultyPickerButton.onClick.AddListener(ToggleDifficultyPicker);
                // Tooltip will be updated dynamically when difficulty changes
                UpdateDifficultyPickerTooltip(TrailDifficulty.Green); // Default, will update when trail is shown
            }
            else
            {
                Debug.LogWarning("[ContextWindowController] Difficulty picker button not found. Tooltip will not work. Please wire _difficultyPickerButton in Inspector or ensure _difficultySetIcon has a Button parent.");
            }
            
            // Difficulty picker buttons
            if (_diffGreenButton != null)
            {
                _diffGreenButton.onClick.AddListener(() => OnDifficultyChosen(TrailDifficulty.Green));
                SetupTooltip(_diffGreenButton, TooltipTexts.ContextWindow.GreenDifficultyHeader, TooltipTexts.ContextWindow.GreenDifficultyContent);
            }
            if (_diffBlueButton != null)
            {
                _diffBlueButton.onClick.AddListener(() => OnDifficultyChosen(TrailDifficulty.Blue));
                SetupTooltip(_diffBlueButton, TooltipTexts.ContextWindow.BlueDifficultyHeader, TooltipTexts.ContextWindow.BlueDifficultyContent);
            }
            if (_diffBlackButton != null)
            {
                _diffBlackButton.onClick.AddListener(() => OnDifficultyChosen(TrailDifficulty.Black));
                SetupTooltip(_diffBlackButton, TooltipTexts.ContextWindow.BlackDifficultyHeader, TooltipTexts.ContextWindow.BlackDifficultyContent);
            }
            if (_diffDoubleBlackButton != null)
            {
                _diffDoubleBlackButton.onClick.AddListener(() => OnDifficultyChosen(TrailDifficulty.DoubleBlack));
                SetupTooltip(_diffDoubleBlackButton, TooltipTexts.ContextWindow.DoubleBlackDifficultyHeader, TooltipTexts.ContextWindow.DoubleBlackDifficultyContent);
            }

            if (_liftBuildConfirmButton != null)
            {
                _liftBuildConfirmButton.onClick.AddListener(OnLiftBuildConfirm);
                SetupTooltip(_liftBuildConfirmButton, TooltipTexts.ContextWindow.ConfirmHeader, TooltipTexts.ContextWindow.ConfirmContent);
            }
            if (_liftBuildCancelButton != null)
            {
                _liftBuildCancelButton.onClick.AddListener(OnLiftBuildCancel);
                SetupTooltip(_liftBuildCancelButton, TooltipTexts.ContextWindow.CancelHeader, TooltipTexts.ContextWindow.CancelContent);
            }

            if (_trailBuildConfirmButton != null)
            {
                _trailBuildConfirmButton.onClick.AddListener(OnLiftBuildConfirm); // Reuses same callback
                SetupTooltip(_trailBuildConfirmButton, TooltipTexts.ContextWindow.ConfirmHeader, TooltipTexts.ContextWindow.ConfirmContent);
            }
            if (_trailBuildCancelButton != null)
            {
                _trailBuildCancelButton.onClick.AddListener(OnLiftBuildCancel); // Reuses same callback
                SetupTooltip(_trailBuildCancelButton, TooltipTexts.ContextWindow.CancelHeader, TooltipTexts.ContextWindow.CancelContent);
            }

            if (_demolishButton != null)
            {
                _demolishButton.onClick.AddListener(OnDemolishClicked);
                SetupTooltip(_demolishButton, TooltipTexts.ContextWindow.DemolishHeader, TooltipTexts.ContextWindow.DemolishContent);
            }
            if (_findButton != null)
            {
                _findButton.onClick.AddListener(OnFindClicked);
                SetupTooltip(_findButton, TooltipTexts.ContextWindow.FindHeader, TooltipTexts.ContextWindow.FindContent);
            }
            if (_followButton != null)
            {
                _followButton.onClick.AddListener(OnFollowClicked);
                SetupTooltip(_followButton, TooltipTexts.ContextWindow.FollowHeader, TooltipTexts.ContextWindow.FollowContent);
            }

            if (_liftUpgradeButton != null)
                _liftUpgradeButton.onClick.AddListener(OnLiftUpgradeClicked);

            // Expand buttons start hidden; DifficultyPicker itself is never touched
            SetExpandButtonsActive(false);

            SetPanelVisible(false, instant: true);

            if (UIManager.Instance != null)
                UIManager.Instance.OnToolChanged.AddListener(OnToolChanged);

            LiftResearchEvents.Changed += OnLiftResearchChanged;
        }

        private void OnDestroy()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.OnToolChanged.RemoveListener(OnToolChanged);
            LiftResearchEvents.Changed -= OnLiftResearchChanged;
        }

        private void OnLiftResearchChanged()
        {
            if (_current != null && _current.Type == StructureType.Lift)
                RefreshLiftUpgradeButtonAppearance();
        }

        private void Update()
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha, _targetAlpha, Time.unscaledDeltaTime * _fadeSpeed);

            if (_canvasGroup.alpha <= 0.01f && !_visible && _panelRoot != null)
                _panelRoot.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────
        //  Public API — called by StructureSelectionManager
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the context window for the given structure.
        /// Called automatically when the player clicks a trail, lift, or lodge.
        /// </summary>
        public void ShowStructure(SelectableStructure structure)
        {
            if (structure == null) { Hide(); return; }
            _current = structure;
            SetPanelVisible(true);
            PopulateHeader();

            SetSectionActive(_trailSection,      structure.Type == StructureType.Trail);
            SetSectionActive(_liftSection,       structure.Type == StructureType.Lift);
            SetSectionActive(_lodgeSection,      structure.Type == StructureType.Lodge);
            SetSectionActive(_skierSection,      structure.Type == StructureType.Skier);
            SetSectionActive(_liftBuildSection,  false);
            SetSectionActive(_trailBuildSection, false);
            SetSectionActive(_lodgeBuildSection, false);

            if (_liftBuildConfirmButton != null) _liftBuildConfirmButton.gameObject.SetActive(false);
            if (_liftBuildCancelButton  != null) _liftBuildCancelButton.gameObject.SetActive(false);
            if (_trailBuildConfirmButton != null) _trailBuildConfirmButton.gameObject.SetActive(false);
            if (_trailBuildCancelButton  != null) _trailBuildCancelButton.gameObject.SetActive(false);

            switch (structure.Type)
            {
                case StructureType.Trail:
                    PopulateTrail();
                    SetActionButtons(find: true, follow: false, demolish: true);
                    break;
                case StructureType.Lift:
                    PopulateLift();
                    SetActionButtons(find: true, follow: false, demolish: true);
                    break;
                case StructureType.Lodge:
                    PopulateLodge();
                    SetActionButtons(find: true, follow: false, demolish: true);
                    break;
                case StructureType.Skier:
                    PopulateSkier();
                    SetActionButtons(find: true, follow: true, demolish: false);
                    break;
            }

            UpdateLiftUpgradeButtonForSelection();
        }

        public void Hide()
        {
            _current = null;
            _liftBuildOnConfirm  = null;
            _liftBuildOnCancel   = null;
            CollapsePickerIfOpen();
            SetPanelVisible(false);
            SetLiftUpgradeButtonVisible(false);
        }

        // ─────────────────────────────────────────────────────────────
        //  Lift Building — Phase 1  (bottom station placed)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the lift-building context window.
        /// Stats show "--" until the cursor moves over the mountain.
        /// Confirm / Cancel buttons are hidden until Phase 2.
        /// </summary>
        public void ShowLiftBuildPhase1()
        {
            _current = null;
            SetPanelVisible(true);

            if (_titleInput != null) _titleInput.SetTextWithoutNotify("New Lift");
            SetIcon(_liftIcon);
            if (_subtitleText != null)
            {
                _subtitleText.text      = "LIFT";
                _subtitleText.color     = Color.white;
                _subtitleText.fontSize  = 15f;
                _subtitleText.fontStyle = TMPro.FontStyles.UpperCase;
            }

            SetSectionActive(_trailSection,      false);
            SetSectionActive(_liftSection,       false);
            SetSectionActive(_lodgeSection,      false);
            SetSectionActive(_skierSection,      false);
            SetSectionActive(_liftBuildSection,  true);
            SetSectionActive(_trailBuildSection, false);
            SetSectionActive(_lodgeBuildSection, false);

            // Stats start blank, Confirm/Cancel hidden until 2nd click
            SetText(_liftBuildTypeValue,       "--");
            SetText(_liftBuildBaseCostValue,   "--");
            SetText(_liftBuildLengthCostValue, "--");
            SetText(_liftBuildTotalCostValue,  "--");
            SetText(_liftBuildUpkeepValue,     "$500 / day");
            SetText(_liftBuildLengthFtValue,   "--");
            SetText(_liftBuildVerticalValue,   "--");
            SetText(_liftBuildCapacityValue,   "--");

            if (_liftBuildConfirmButton != null) _liftBuildConfirmButton.gameObject.SetActive(false);
            if (_liftBuildCancelButton  != null) _liftBuildCancelButton.gameObject.SetActive(false);
            SetActionButtons(find: false, follow: false, demolish: false);
            SetLiftUpgradeButtonVisible(false);
        }

        // ─────────────────────────────────────────────────────────────
        //  Live stat update (called every frame while dragging top pt)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates the stat rows in real-time while the player hovers the top station.
        /// Does not show Confirm / Cancel yet.
        /// </summary>
        public void UpdateLiftBuildStats(float lengthM, float elevationM, int baseCost, int addedCost)
        {
            if (!_visible) return;

            int totalCost = baseCost + addedCost;
            SetText(_liftBuildBaseCostValue,   $"${baseCost:N0}");
            SetText(_liftBuildLengthCostValue, $"${addedCost:N0}");
            SetText(_liftBuildTotalCostValue,  $"${totalCost:N0}");
            SetText(_liftBuildLengthFtValue,   $"{lengthM * MetresToFeet:N0} ft");
            SetText(_liftBuildVerticalValue,   $"{elevationM * MetresToFeet:N0} ft");
        }

        // ─────────────────────────────────────────────────────────────
        //  Lift Building — Phase 2  (pending confirmation)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Locks the stats to the placed values and reveals Confirm / Cancel.
        /// </summary>
        public void ShowLiftBuildPhase2(LiftData liftData, int baseCost, int lengthAddedCost,
            System.Action onConfirm, System.Action onCancel)
        {
            _liftBuildOnConfirm = onConfirm;
            _liftBuildOnCancel  = onCancel;

            if (!_visible) SetPanelVisible(true);

            if (liftData != null)
            {
                SetText(_liftBuildTypeValue,       LiftTypeSpecs.GetDisplayName(liftData.Type));
                SetText(_liftBuildBaseCostValue,   $"${baseCost:N0}");
                SetText(_liftBuildLengthCostValue, $"${lengthAddedCost:N0}");
                SetText(_liftBuildTotalCostValue,  $"${liftData.BuildCost:N0}");
                SetText(_liftBuildUpkeepValue,     "$500 / day");
                SetText(_liftBuildLengthFtValue,   $"{liftData.Length * MetresToFeet:N0} ft");
                SetText(_liftBuildVerticalValue,   $"{liftData.ElevationGain * MetresToFeet:N0} ft");
                SetText(_liftBuildCapacityValue,   $"{liftData.Capacity:N0} / hr");
            }

            // Reveal Confirm / Cancel buttons
            if (_liftBuildConfirmButton != null) _liftBuildConfirmButton.gameObject.SetActive(true);
            if (_liftBuildCancelButton  != null) _liftBuildCancelButton.gameObject.SetActive(true);
            SetLiftUpgradeButtonVisible(false);
        }

        // ─────────────────────────────────────────────────────────────
        //  Header
        // ─────────────────────────────────────────────────────────────

        private void PopulateHeader()
        {
            if (_current == null) return;

            // Title input field — pre-fill with current name
            if (_titleInput != null)
                _titleInput.SetTextWithoutNotify(_current.StructureName);

            switch (_current.Type)
            {
                case StructureType.Trail: SetIcon(_trailIcon);  break;
                case StructureType.Lift:  SetIcon(_liftIcon);   break;
                case StructureType.Lodge: SetIcon(_lodgeIcon);  break;
                case StructureType.Skier: SetIcon(_skierIcon);  break;
            }

            // Subtitle: always white, all-caps, 15pt
            if (_subtitleText != null)
            {
                _subtitleText.text      = SubtitleLabel(_current.Type);
                _subtitleText.color     = Color.white;
                _subtitleText.fontSize  = 15f;
                _subtitleText.fontStyle = TMPro.FontStyles.UpperCase;
            }
        }

        private string SubtitleLabel(StructureType type)
        {
            switch (type)
            {
                case StructureType.Trail: return "Trail";
                case StructureType.Lift:  return "Lift";
                case StructureType.Lodge: return "Lodge";
                case StructureType.Skier: return "Skier";
                default:                  return "";
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Trail section
        // ─────────────────────────────────────────────────────────────

        private void PopulateTrail()
        {
            var trail = _current?.TrailData;
            if (trail == null) return;

            // Status / open toggle
            SetText(_trailStatusText, "Open");
            if (_trailOpenToggle != null)
                _trailOpenToggle.SetIsOnWithoutNotify(true);

            // Difficulty set icon (picker starts collapsed)
            RefreshDifficultyIcon(trail.Difficulty);
            UpdateDifficultyPickerTooltip(trail.Difficulty);
            CollapsePickerIfOpen();

            // Usage stats
            int runsToday   = 0;
            int runsAllTime = 0;
            float capacity  = 0f;
            if (ResortTrafficManager.Instance != null)
            {
                runsToday   = ResortTrafficManager.Instance.GetTrailRunsToday(trail.TrailId);
                runsAllTime = ResortTrafficManager.Instance.GetTrailRunsAllTime(trail.TrailId);
                capacity    = ResortTrafficManager.Instance.GetTrailCapacity(trail.TrailId);
            }

            SetText(_runsTodayValue,   runsToday.ToString("N0"));
            SetText(_runsAllTimeValue, runsAllTime.ToString("N0"));
            SetText(_upkeepValue,      "$100 / day");  // matches ExpenseTracker.CostPerTrail

            // Physical stats
            float worldLen = trail.WorldLength;
            SetText(_lengthValue,
                worldLen > 1f ? $"{worldLen * MetresToFeet:N0} ft" : "—");

            SetText(_verticalDropValue,
                trail.TotalElevationDrop > 0.1f
                    ? $"{trail.TotalElevationDrop * MetresToFeet:N0} ft"
                    : "—");

            SetText(_avgSlopeValue,
                trail.AverageSlope > 0f ? $"{trail.AverageSlope * 100f:F0}%" : "—");

            SetText(_maxSlopeValue,
                trail.MaxSlope > 0f ? $"{trail.MaxSlope * 100f:F0}%" : "—");

            SetText(_capacityValue,
                capacity > 0f ? $"{Mathf.RoundToInt(capacity)}" : "—");
        }

        // ─────────────────────────────────────────────────────────────
        //  Lift section
        // ─────────────────────────────────────────────────────────────

        private void PopulateLift()
        {
            var lift = _current?.LiftData;
            if (lift == null) return;

            SetText(_liftStatusText, "Open");
            if (_liftOpenToggle != null)
                _liftOpenToggle.SetIsOnWithoutNotify(true);

            SetText(_liftTypeText, LiftTypeLabel(lift.Type));

            int ridesToday   = 0;
            int ridesAllTime = 0;
            if (ResortTrafficManager.Instance != null)
            {
                ridesToday   = ResortTrafficManager.Instance.GetLiftRidesToday(lift.LiftId);
                ridesAllTime = ResortTrafficManager.Instance.GetLiftRidesAllTime(lift.LiftId);
            }

            SetText(_ridesTodayValue,   ridesToday.ToString("N0"));
            SetText(_ridesAllTimeValue, ridesAllTime.ToString("N0"));
            SetText(_avgWaitTimeValue,  "—");           // not yet implemented
            SetText(_liftUpkeepValue,   "$500 / day");  // matches ExpenseTracker.CostPerLift

            RefreshLiftUpgradeButtonAppearance();
        }

        private string LiftTypeLabel(LiftType type)
        {
            return LiftTypeSpecs.GetDisplayName(type);
        }

        // ─────────────────────────────────────────────────────────────
        //  Lodge section
        // ─────────────────────────────────────────────────────────────

        private void PopulateLodge()
        {
            var lodge = _current?.Lodge;
            if (lodge == null) return;

            SetText(_lodgeStatusText, "Open");
            if (_lodgeOpenToggle != null)
                _lodgeOpenToggle.SetIsOnWithoutNotify(true);

            RefreshBuffetPrice(lodge.Pricing.FoodPrice);

            SetText(_lodgeVisitsTodayValue,   lodge.Pricing.TotalVisits.ToString("N0"));
            SetText(_lodgeVisitsAllTimeValue, lodge.Pricing.TotalVisitsAllTime.ToString("N0"));
            SetText(_lodgeUpkeepValue,        "$300 / day");  // matches ExpenseTracker.CostPerLodge
        }

        private void RefreshBuffetPrice(float price)
        {
            SetText(_buffetPriceValue, $"${price:F0}");
        }

        // ─────────────────────────────────────────────────────────────
        //  Skier section
        // ─────────────────────────────────────────────────────────────

        private void PopulateSkier()
        {
            var skier = _current?.SkierData;
            if (skier == null) return;

            SetText(_skierStatusValue,      SkierStateLabel(skier.CurrentState));
            SetText(_skierSkillValue,       SkillLabel(skier.Skill));
            SetText(_skierFavoriteRunValue, "—");   // not yet implemented
            
            float satisfaction = skier.GetSatisfaction() * 100f;
            SetText(_skierSatisfactionValue, $"{satisfaction:F0}%");
            
            SetText(_skierRunsTodayValue,   skier.RunsCompleted.ToString("N0"));
            SetText(_skierRunsAllTimeValue, skier.RunsCompleted.ToString("N0"));
        }

        private string SkierStateLabel(SkierState state)
        {
            switch (state)
            {
                case SkierState.AtBase:         return "At Base";
                case SkierState.WalkingToLift:  return "Walking to Lift";
                case SkierState.InQueue:        return "In Queue";
                case SkierState.RidingLift:     return "On Lift";
                case SkierState.SkiingTrail:    return "Skiing";
                case SkierState.AtAmenity:      return "In Lodge";
                default:                        return "—";
            }
        }

        private string SkillLabel(SkillLevel skill)
        {
            switch (skill)
            {
                case SkillLevel.Beginner:     return "Beginner";
                case SkillLevel.Intermediate: return "Intermediate";
                case SkillLevel.Advanced:     return "Advanced";
                case SkillLevel.Expert:       return "Expert";
                default:                      return "—";
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Difficulty picker
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by DifficultyPicker button's OnClick — toggles the 4 expand buttons.
        /// Wire this to DifficultyPicker's OnClick in the Inspector.
        /// </summary>
        public void ToggleDifficultyPicker()
        {
            bool anyActive = _diffGreenButton != null && _diffGreenButton.gameObject.activeSelf;
            SetExpandButtonsActive(!anyActive);
        }

        private void OnDifficultyChosen(TrailDifficulty difficulty)
        {
            var trail = _current?.TrailData;
            if (trail == null) return;

            trail.Difficulty = difficulty;

            // Recalculate and refresh
            RefreshDifficultyIcon(difficulty);
            SetText(_subtitleText, DifficultyLabel(difficulty), DifficultyColor(difficulty));
            UpdateDifficultyPickerTooltip(difficulty);
            CollapsePickerIfOpen();
        }

        private void RefreshDifficultyIcon(TrailDifficulty difficulty)
        {
            if (_difficultySetIcon != null)
            {
                _difficultySetIcon.sprite = DifficultySprite(difficulty);
                _difficultySetIcon.color  = DifficultyColor(difficulty);
            }
        }
        
        private void UpdateDifficultyPickerTooltip(TrailDifficulty difficulty)
        {
            // Try to find button if not set
            if (_difficultyPickerButton == null && _difficultySetIcon != null)
            {
                _difficultyPickerButton = _difficultySetIcon.GetComponentInParent<Button>();
            }
            
            if (_difficultyPickerButton == null) return;
            
            string difficultyName = DifficultyLabel(difficulty);
            string header = $"Trail Difficulty: {difficultyName}";
            string content = $"This trail is designated as {difficultyName}.\nClick to change difficulty (may affect guest satisfaction).";
            
            SetupTooltip(_difficultyPickerButton, header, content);
        }

        private void CollapsePickerIfOpen() => SetExpandButtonsActive(false);

        private void SetExpandButtonsActive(bool active)
        {
            if (_diffGreenButton       != null) _diffGreenButton.gameObject.SetActive(active);
            if (_diffBlueButton        != null) _diffBlueButton.gameObject.SetActive(active);
            if (_diffBlackButton       != null) _diffBlackButton.gameObject.SetActive(active);
            if (_diffDoubleBlackButton != null) _diffDoubleBlackButton.gameObject.SetActive(active);
        }

        // ─────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────

        private void SetPanelVisible(bool visible, bool instant = false)
        {
            _visible     = visible;
            _targetAlpha = visible ? 1f : 0f;

            if (_panelRoot == null) return;

            if (visible)
            {
                _panelRoot.SetActive(true);
                if (_canvasGroup != null)
                {
                    _canvasGroup.blocksRaycasts = true;
                    if (instant) _canvasGroup.alpha = 1f;
                }
            }
            else
            {
                // Immediately stop blocking raycasts so the fading panel
                // cannot intercept clicks while it animates out.
                if (_canvasGroup != null)
                {
                    _canvasGroup.blocksRaycasts = false;
                    if (instant)
                    {
                        _canvasGroup.alpha = 0f;
                        _panelRoot.SetActive(false);
                    }
                }
                else if (instant)
                {
                    _panelRoot.SetActive(false);
                }
            }
        }

        private static void SetSectionActive(GameObject section, bool active)
        {
            if (section != null) section.SetActive(active);
        }

        private void SetActionButtons(bool find, bool follow, bool demolish)
        {
            if (_findButton    != null) _findButton.gameObject.SetActive(find);
            if (_followButton  != null) _followButton.gameObject.SetActive(follow);
            if (_demolishButton != null) _demolishButton.gameObject.SetActive(demolish);
        }

        private void UpdateLiftUpgradeButtonForSelection()
        {
            bool liftSelected = _current != null && _current.Type == StructureType.Lift;
            SetLiftUpgradeButtonVisible(liftSelected);
            if (liftSelected)
                RefreshLiftUpgradeButtonAppearance();
        }

        private void SetLiftUpgradeButtonVisible(bool visible)
        {
            if (_liftUpgradeButton != null)
                _liftUpgradeButton.gameObject.SetActive(visible);
        }

        private void EnsureLiftUpgradeTooltipOverlay()
        {
            if (_liftUpgradeButton == null || _liftUpgradeTooltipOverlay != null) return;

            _liftUpgradeTooltipOverlay = new GameObject("LiftUpgradeTooltipHit", typeof(RectTransform), typeof(Image), typeof(TooltipTrigger));
            _liftUpgradeTooltipOverlay.transform.SetParent(_liftUpgradeButton.transform, false);
            _liftUpgradeTooltipOverlay.transform.SetAsLastSibling();

            var rt = _liftUpgradeTooltipOverlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = _liftUpgradeTooltipOverlay.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.01f);
            img.raycastTarget = false;
        }

        private void ApplyLiftUpgradeTooltip(string header, string content, bool useOverlayForTooltip)
        {
            EnsureLiftUpgradeTooltipOverlay();
            SetupTooltip(_liftUpgradeButton, header, content);

            if (_liftUpgradeTooltipOverlay != null)
            {
                var img = _liftUpgradeTooltipOverlay.GetComponent<Image>();
                if (img != null) img.raycastTarget = useOverlayForTooltip;

                var overlayTt = _liftUpgradeTooltipOverlay.GetComponent<TooltipTrigger>();
                if (overlayTt != null)
                    overlayTt.SetContent(header, content);
            }
        }

        /// <summary>
        /// Tooltip + interactable state for the lift upgrade button (only valid while a lift is selected).
        /// </summary>
        private void RefreshLiftUpgradeButtonAppearance()
        {
            if (_liftUpgradeButton == null) return;

            var lift = _current?.LiftData;
            var liftBuilder = FindObjectOfType<LiftBuilder>();
            liftBuilder?.EnsureReady();
            var liftSystem = liftBuilder != null ? liftBuilder.LiftSystem : null;

            if (liftBuilder == null || liftSystem == null)
            {
                _liftUpgradeButton.interactable = false;
                ApplyLiftUpgradeTooltip(
                    TooltipTexts.ContextWindow.LiftUpgradeMaxHeader,
                    "Lift system is not available.",
                    useOverlayForTooltip: true);
                return;
            }

            var next = lift != null ? LiftTypeSpecs.GetNextUpgrade(lift.Type) : null;
            if (lift == null || next == null)
            {
                _liftUpgradeButton.interactable = false;
                ApplyLiftUpgradeTooltip(
                    TooltipTexts.ContextWindow.LiftUpgradeMaxHeader,
                    TooltipTexts.ContextWindow.LiftUpgradeMaxContent,
                    useOverlayForTooltip: true);
                return;
            }

            int cost = LiftTypeSpecs.GetUpgradeCostToNext(lift.Type, lift, liftSystem);
            string nextName = LiftTypeSpecs.GetDisplayName(next.Value);
            string affordContent =
                $"Upgrade to {nextName}.\nCost: ${cost:N0}.";

            var sim = FindObjectOfType<SimulationRunner>()?.Sim;
            int money = -1;
            if (sim?.State != null)
                money = sim.State.Money;

            if (sim?.State != null && !LiftBuildUnlocks.IsUnlocked(sim.State, next.Value))
            {
                _liftUpgradeButton.interactable = false;
                ApplyLiftUpgradeTooltip(
                    TooltipTexts.ContextWindow.LiftUpgradeResearchLockedHeader,
                    TooltipTexts.ContextWindow.LiftUpgradeResearchLockedContent,
                    useOverlayForTooltip: true);
                return;
            }

            bool canAfford = money < 0 || money >= cost;

            if (!canAfford)
            {
                _liftUpgradeButton.interactable = false;
                ApplyLiftUpgradeTooltip(
                    TooltipTexts.ContextWindow.LiftUpgradeCannotAffordHeader,
                    $"Not enough cash.\nCost: ${cost:N0}.",
                    useOverlayForTooltip: true);
                return;
            }

            _liftUpgradeButton.interactable = true;
            ApplyLiftUpgradeTooltip(
                TooltipTexts.ContextWindow.LiftUpgradeHeader,
                affordContent,
                useOverlayForTooltip: false);
        }

        private void OnLiftUpgradeClicked()
        {
            if (_current == null || _current.Type != StructureType.Lift || _liftUpgradeButton == null || !_liftUpgradeButton.interactable)
                return;

            var lift = _current.LiftData;
            if (lift == null) return;

            var liftBuilder = FindObjectOfType<LiftBuilder>();
            if (liftBuilder == null)
            {
                NotificationManager.Instance?.ShowError("Lift system not available.");
                return;
            }

            liftBuilder.EnsureReady();

            var nextUp = LiftTypeSpecs.GetNextUpgrade(lift.Type);
            if (nextUp.HasValue)
            {
                var sim = FindObjectOfType<SimulationRunner>()?.Sim;
                if (sim?.State != null && !LiftBuildUnlocks.IsUnlocked(sim.State, nextUp.Value))
                {
                    NotificationManager.Instance?.ShowWarning(
                        TooltipTexts.ContextWindow.LiftUpgradeResearchLockedContent);
                    return;
                }
            }

            if (!liftBuilder.TryUpgradeLift(lift, out SelectableStructure newRoot, out string err))
            {
                NotificationManager.Instance?.ShowError(err);
                RefreshLiftUpgradeButtonAppearance();
                return;
            }

            if (newRoot != null && StructureSelectionManager.Instance != null)
                StructureSelectionManager.Instance.SelectStructure(newRoot);
            else
                PopulateLift();
        }

        private static void SetText(TextMeshProUGUI label, string text, Color? color = null)
        {
            if (label == null) return;
            label.text = text;
            if (color.HasValue) label.color = color.Value;
        }

        private void SetIcon(Sprite sprite)
        {
            if (_iconImage == null) return;
            if (sprite != null)
            {
                _iconImage.sprite = sprite;
                _iconImage.gameObject.SetActive(true);
            }
            else
            {
                _iconImage.gameObject.SetActive(false);
            }
        }

        private string DifficultyLabel(TrailDifficulty d)
        {
            switch (d)
            {
                case TrailDifficulty.Green:       return "Green Circle";
                case TrailDifficulty.Blue:        return "Blue Square";
                case TrailDifficulty.Black:       return "Black Diamond";
                case TrailDifficulty.DoubleBlack: return "Double Black";
                default:                          return "Trail";
            }
        }

        private Color DifficultyColor(TrailDifficulty d)
        {
            switch (d)
            {
                case TrailDifficulty.Green:       return _greenColor;
                case TrailDifficulty.Blue:        return _blueColor;
                case TrailDifficulty.Black:       return _blackColor;
                case TrailDifficulty.DoubleBlack: return _doubleBlackColor;
                default:                          return Color.white;
            }
        }

        private Sprite DifficultySprite(TrailDifficulty d)
        {
            switch (d)
            {
                case TrailDifficulty.Green:       return _greenSprite;
                case TrailDifficulty.Blue:        return _blueSprite;
                case TrailDifficulty.Black:       return _blackSprite;
                case TrailDifficulty.DoubleBlack: return _doubleBlackSprite;
                default:                          return null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Callbacks
        // ─────────────────────────────────────────────────────────────

        private void OnTitleEndEdit(string newName)
        {
            if (_current == null) return;
            _current.Rename(newName);
        }

        private void OnCloseClicked()
        {
            Hide();
            if (StructureSelectionManager.Instance != null)
                StructureSelectionManager.Instance.DeselectStructure();
        }

        private void OnOpenToggleChanged(bool isOn)
        {
            SetText(_trailStatusText, isOn ? "Open" : "Closed");
        }

        private void OnLiftOpenToggleChanged(bool isOn)
        {
            SetText(_liftStatusText, isOn ? "Open" : "Closed");
        }

        private void OnLodgeOpenToggleChanged(bool isOn)
        {
            SetText(_lodgeStatusText, isOn ? "Open" : "Closed");
        }

        private void OnBuffetDecrement()
        {
            var lodge = _current?.Lodge;
            if (lodge == null) return;
            lodge.Pricing.FoodPrice = UnityEngine.Mathf.Max(
                LodgePricing.MinPrice, lodge.Pricing.FoodPrice - 1f);
            RefreshBuffetPrice(lodge.Pricing.FoodPrice);
        }

        private void OnBuffetIncrement()
        {
            var lodge = _current?.Lodge;
            if (lodge == null) return;
            lodge.Pricing.FoodPrice = UnityEngine.Mathf.Min(
                LodgePricing.MaxFoodPrice, lodge.Pricing.FoodPrice + 1f);
            RefreshBuffetPrice(lodge.Pricing.FoodPrice);
        }

        private void OnToolChanged(BaseTool tool)
        {
            // Only auto-close when showing a selected structure and a build tool activates.
            // If _current is null the window belongs to a build mode — don't close it.
            if (tool != null && _visible && _current != null) Hide();
        }

        // ─────────────────────────────────────────────────────────────
        //  Trail Building
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the trail-building context window on first anchor placement.
        /// Reuses the shared Confirm / Cancel buttons (same as lift/lodge build).
        /// Buttons are hidden until the trail enters Settled state.
        /// </summary>
        public void ShowTrailBuildWindow(System.Action onConfirm, System.Action onCancel)
        {
            _liftBuildOnConfirm = onConfirm;
            _liftBuildOnCancel  = onCancel;
            _current = null;

            SetPanelVisible(true);

            if (_titleInput != null) _titleInput.SetTextWithoutNotify("New Trail");
            SetIcon(_trailIcon);
            if (_subtitleText != null)
            {
                _subtitleText.text      = "TRAIL";
                _subtitleText.color     = Color.white;
                _subtitleText.fontSize  = 15f;
                _subtitleText.fontStyle = TMPro.FontStyles.UpperCase;
            }

            SetSectionActive(_trailSection,      false);
            SetSectionActive(_liftSection,       false);
            SetSectionActive(_lodgeSection,      false);
            SetSectionActive(_skierSection,      false);
            SetSectionActive(_liftBuildSection,  false);
            SetSectionActive(_trailBuildSection, true);
            SetSectionActive(_lodgeBuildSection, false);

            SetText(_trailBuildWidthValue,  "--");
            SetText(_trailBuildLengthValue, "--");
            SetText(_trailBuildCostValue,   "--");

            if (_liftBuildConfirmButton != null) _liftBuildConfirmButton.gameObject.SetActive(false);
            if (_liftBuildCancelButton  != null) _liftBuildCancelButton.gameObject.SetActive(false);
            SetActionButtons(find: false, follow: false, demolish: false);
            SetLiftUpgradeButtonVisible(false);
        }

        /// <summary>
        /// Reveals the shared Confirm / Cancel after the trail enters Settled state.
        /// When canConfirm is false the Confirm button is visible but greyed out
        /// with a tooltip explaining why.
        /// </summary>
        public void ShowTrailBuildConfirmButtons(bool canConfirm = true)
        {
            if (_liftBuildConfirmButton != null)
            {
                _liftBuildConfirmButton.gameObject.SetActive(true);
                _liftBuildConfirmButton.interactable = canConfirm;

                if (!canConfirm)
                    SetupTooltip(_liftBuildConfirmButton,
                        "Cannot Confirm",
                        "Trail must have a valid top and bottom connection.");
                else
                    SetupTooltip(_liftBuildConfirmButton,
                        TooltipTexts.ContextWindow.ConfirmHeader,
                        TooltipTexts.ContextWindow.ConfirmContent);
            }
            if (_liftBuildCancelButton != null) _liftBuildCancelButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the shared Confirm / Cancel when resuming from Settled → Placing.
        /// </summary>
        public void HideTrailBuildConfirmButtons()
        {
            if (_liftBuildConfirmButton != null) _liftBuildConfirmButton.gameObject.SetActive(false);
            if (_liftBuildCancelButton  != null) _liftBuildCancelButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Live stat updates while building.
        /// </summary>
        public void UpdateTrailBuildStats(float widthWorld, float lengthWorld, int cost)
        {
            if (!_visible) return;
            SetText(_trailBuildWidthValue,  $"{widthWorld:F1}");
            SetText(_trailBuildLengthValue, lengthWorld > 1f ? $"{lengthWorld * MetresToFeet:N0} ft" : "--");
            SetText(_trailBuildCostValue,   $"${cost:N0}");
        }

        // ─────────────────────────────────────────────────────────────
        //  Lodge Building
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the lodge-building context window.
        /// Shows cost and Confirm / Cancel immediately.
        /// </summary>
        public void ShowLodgeBuildWindow(int buildCost, System.Action onConfirm, System.Action onCancel)
        {
            _liftBuildOnConfirm = onConfirm;
            _liftBuildOnCancel  = onCancel;

            _current = null;
            SetPanelVisible(true);

            if (_titleInput != null) _titleInput.SetTextWithoutNotify("New Lodge");
            SetIcon(_lodgeIcon);
            if (_subtitleText != null)
            {
                _subtitleText.text      = "LODGE";
                _subtitleText.color     = Color.white;
                _subtitleText.fontSize  = 15f;
                _subtitleText.fontStyle = TMPro.FontStyles.UpperCase;
            }

            SetSectionActive(_trailSection,      false);
            SetSectionActive(_liftSection,       false);
            SetSectionActive(_lodgeSection,      false);
            SetSectionActive(_skierSection,      false);
            SetSectionActive(_liftBuildSection,  false);
            SetSectionActive(_trailBuildSection, false);
            SetSectionActive(_lodgeBuildSection, true);

            SetText(_lodgeBuildCostValue, $"${buildCost:N0}");

            SetActionButtons(find: false, follow: false, demolish: false);

            // Confirm/Cancel visible immediately for lodge building
            if (_liftBuildConfirmButton != null) _liftBuildConfirmButton.gameObject.SetActive(true);
            if (_liftBuildCancelButton  != null) _liftBuildCancelButton.gameObject.SetActive(true);
            SetLiftUpgradeButtonVisible(false);
        }

        private void OnLiftBuildConfirm()
        {
            bool closeDock = IsLiftOrTrailBuildContextActive();
            var cb = _liftBuildOnConfirm;
            _liftBuildOnConfirm = null;
            _liftBuildOnCancel  = null;
            cb?.Invoke();
            if (closeDock)
                CloseDockIfLiftOrTrailBuild();
            // Context window will be updated by LiftBuildTool.HandleLiftPlaced → ShowStructure
        }

        private void OnLiftBuildCancel()
        {
            bool closeDock = IsLiftOrTrailBuildContextActive();
            var cb = _liftBuildOnCancel;
            _liftBuildOnConfirm = null;
            _liftBuildOnCancel  = null;
            cb?.Invoke();
            if (closeDock)
                CloseDockIfLiftOrTrailBuild();
        }

        private bool IsLiftOrTrailBuildContextActive()
        {
            return (_trailBuildSection != null && _trailBuildSection.activeSelf)
                || (_liftBuildSection != null && _liftBuildSection.activeSelf);
        }

        private static void CloseDockIfLiftOrTrailBuild()
        {
            var dock = UnityEngine.Object.FindFirstObjectByType<DockController>();
            if (dock != null)
                dock.CloseDock();
        }

        // ── Action button callbacks ───────────────────────────────────────

        private void OnDemolishClicked()
        {
            if (_current == null) return;

            var target = _current;
            Hide();

            if (StructureSelectionManager.Instance != null)
                StructureSelectionManager.Instance.DeselectStructure();

            // Lift: must remove from core lift system + connectivity + queues,
            // not just destroy the visual root.
            if (target.Type == StructureType.Lift)
            {
                int liftId = target.StructureId;
                if (liftId <= 0 && target.LiftData != null)
                    liftId = target.LiftData.LiftId;

                var liftBuilder = FindObjectOfType<LiftBuilder>();
                if (liftBuilder != null && liftBuilder.LiftSystem != null && liftId > 0)
                {
                    liftBuilder.LiftSystem.RemoveLiftById(liftId);
                    liftBuilder.PrefabBuilder?.DestroyLift(liftId);
                    liftBuilder.Connectivity?.RebuildConnections();

                    var skierVisualizer = FindObjectOfType<SkierVisualizer>();
                    if (skierVisualizer != null)
                        skierVisualizer.NotifyLiftDemolished(liftId);
                }

                if (target.gameObject != null)
                    Destroy(target.gameObject);
                return;
            }

            // Trail: remove from TrailSystem so the visualizer doesn't recreate it
            if (target.Type == StructureType.Trail && target.TrailData != null)
            {
                var drawer = FindObjectOfType<SkiResortTycoon.UnityBridge.TrailDrawer>();
                if (drawer != null)
                    drawer.DemolishTrail(target.TrailData);
            }

            // Lodge: unregister from manager before destroying
            if (target.Type == StructureType.Lodge && target.Lodge != null)
            {
                if (LodgeManager.Instance != null)
                    LodgeManager.Instance.UnregisterLodge(target.Lodge);
            }

            Destroy(target.gameObject);
        }

        private void OnFindClicked()
        {
            if (_current == null) return;

            var cam = FindObjectOfType<CameraController>();
            if (cam == null) return;

            Vector3 target;
            switch (_current.Type)
            {
                case StructureType.Trail:
                {
                    var pts = _current.TrailData?.WorldPathPoints;
                    if (pts == null || pts.Count == 0) return;
                    var mid = pts[pts.Count / 2];
                    target = MountainManager.ToUnityVector3(mid);
                    break;
                }
                case StructureType.Lift:
                {
                    var lift = _current.LiftData;
                    if (lift == null) return;
                    var s = MountainManager.ToUnityVector3(lift.StartPosition);
                    var e = MountainManager.ToUnityVector3(lift.EndPosition);
                    target = (s + e) * 0.5f;
                    break;
                }
                case StructureType.Lodge:
                {
                    if (_current.Lodge == null) return;
                    target = _current.Lodge.transform.position;
                    break;
                }
                case StructureType.Skier:
                {
                    target = _current.transform.position;
                    break;
                }
                default:
                    return;
            }

            float zoom;
            switch (_current.Type)
            {
                case StructureType.Lift:  zoom = 60f;  break;
                case StructureType.Skier: zoom = 13f;  break;
                default:                  zoom = 40f;  break;
            }
            cam.FindTarget(target, zoom);
        }

        private void OnFollowClicked()
        {
            if (_current == null || _current.Type != StructureType.Skier) return;

            var cam = FindObjectOfType<CameraController>();
            if (cam == null) return;

            cam.StartFollowing(_current.transform, _current.SkierData);
        }
        
        private void SetupTooltip(Button button, string header, string content)
        {
            if (button == null)
            {
                Debug.LogWarning("[ContextWindowController] SetupTooltip called with null button");
                return;
            }
            
            // Ensure button can receive raycasts for tooltip detection
            var image = button.targetGraphic as UnityEngine.UI.Image;
            if (image != null)
            {
                image.raycastTarget = true;
            }
            else
            {
                // Try to find Image component on the button
                image = button.GetComponent<UnityEngine.UI.Image>();
                if (image != null)
                {
                    image.raycastTarget = true;
                }
            }
            
            var tooltipTrigger = button.GetComponent<TooltipTrigger>();
            if (tooltipTrigger == null)
            {
                tooltipTrigger = button.gameObject.AddComponent<TooltipTrigger>();
            }
            tooltipTrigger.SetContent(header, content);
            
            // Debug log to verify tooltip is being set
            if (button == _difficultyPickerButton)
            {
                Debug.Log($"[ContextWindowController] Difficulty picker tooltip set: {header} - {content}");
            }
        }
        
        private void SetupTooltip(Toggle toggle, string header, string content)
        {
            if (toggle == null) return;
            
            var tooltipTrigger = toggle.GetComponent<TooltipTrigger>();
            if (tooltipTrigger == null)
            {
                tooltipTrigger = toggle.gameObject.AddComponent<TooltipTrigger>();
            }
            tooltipTrigger.SetContent(header, content);
        }
    }
}
