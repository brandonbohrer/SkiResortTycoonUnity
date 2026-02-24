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

        // ── Internal state ────────────────────────────────────────────
        private SelectableStructure _current;
        private bool  _visible;
        private float _targetAlpha;

        private const float MetresToFeet = 3.28084f;

        // ─────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────

        private void Start()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);

            if (_trailOpenToggle != null)
                _trailOpenToggle.onValueChanged.AddListener(OnOpenToggleChanged);

            if (_liftOpenToggle != null)
                _liftOpenToggle.onValueChanged.AddListener(OnLiftOpenToggleChanged);

            if (_lodgeOpenToggle != null)
                _lodgeOpenToggle.onValueChanged.AddListener(OnLodgeOpenToggleChanged);

            if (_buffetDecrementButton != null)
                _buffetDecrementButton.onClick.AddListener(OnBuffetDecrement);
            if (_buffetIncrementButton != null)
                _buffetIncrementButton.onClick.AddListener(OnBuffetIncrement);

            // Title input — rename structure when user finishes editing
            if (_titleInput != null)
                _titleInput.onEndEdit.AddListener(OnTitleEndEdit);

            // Difficulty picker buttons
            if (_diffGreenButton       != null) _diffGreenButton.onClick.AddListener(()       => OnDifficultyChosen(TrailDifficulty.Green));
            if (_diffBlueButton        != null) _diffBlueButton.onClick.AddListener(()        => OnDifficultyChosen(TrailDifficulty.Blue));
            if (_diffBlackButton       != null) _diffBlackButton.onClick.AddListener(()       => OnDifficultyChosen(TrailDifficulty.Black));
            if (_diffDoubleBlackButton != null) _diffDoubleBlackButton.onClick.AddListener(() => OnDifficultyChosen(TrailDifficulty.DoubleBlack));

            // Expand buttons start hidden; DifficultyPicker itself is never touched
            SetExpandButtonsActive(false);

            SetPanelVisible(false, instant: true);

            if (UIManager.Instance != null)
                UIManager.Instance.OnToolChanged.AddListener(OnToolChanged);
        }

        private void OnDestroy()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.OnToolChanged.RemoveListener(OnToolChanged);
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

            SetSectionActive(_trailSection, structure.Type == StructureType.Trail);
            SetSectionActive(_liftSection,  structure.Type == StructureType.Lift);
            SetSectionActive(_lodgeSection, structure.Type == StructureType.Lodge);
            SetSectionActive(_skierSection, structure.Type == StructureType.Skier);

            switch (structure.Type)
            {
                case StructureType.Trail: PopulateTrail(); break;
                case StructureType.Lift:  PopulateLift();  break;
                case StructureType.Lodge: PopulateLodge(); break;
                case StructureType.Skier: PopulateSkier(); break;
            }
        }

        public void Hide()
        {
            _current = null;
            CollapsePickerIfOpen();
            SetPanelVisible(false);
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
        }

        private string LiftTypeLabel(LiftType type)
        {
            switch (type)
            {
                case LiftType.ChairLift: return "Chairlift";
                case LiftType.Gondola:   return "Gondola";
                case LiftType.TSBar:     return "T-Bar";
                default:                 return "Lift";
            }
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
                if (instant && _canvasGroup != null) _canvasGroup.alpha = 1f;
            }
            else if (instant)
            {
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                _panelRoot.SetActive(false);
            }
        }

        private static void SetSectionActive(GameObject section, bool active)
        {
            if (section != null) section.SetActive(active);
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
            if (tool != null && _visible) Hide();
        }
    }
}
