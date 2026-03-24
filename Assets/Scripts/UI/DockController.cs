using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Manages the bottom dock bar and its DockSubRows.
    /// Each category maps a bottom button to its own standalone subrow panel.
    /// Clicking a button shows that subrow (and highlights it orange).
    /// Clicking the same button again closes everything.
    /// </summary>
    public class DockController : MonoBehaviour
    {
        // ── Brand color ──────────────────────────────────────────────────────
        private static readonly Color SelectedColor = new Color(0.9647f, 0.5373f, 0.2314f, 1f); // #F6893B

        // ── Inspector ────────────────────────────────────────────────────────
        [Tooltip("Background panel shown behind any open subrow — toggled on/off with the active subrow.")]
        [SerializeField] private GameObject _subRowBg;

        [Tooltip("Optional close/back button that dismisses the open subrow.")]
        [SerializeField] private Button _closeButton;

        [SerializeField] private DockCategory[] _categories;

        // ── Trail Build Mode UI (drag from TrailsSubRow) ─────────────────────
        [Header("Trail Build Mode UI")]
        [Tooltip("The TrailBuildTool component in the scene.")]
        [SerializeField] private TrailBuildTool _trailBuildTool;
        [Tooltip("Paint mode button in the trails subrow.")]
        [SerializeField] private Button _paintModeButton;
        [Tooltip("Straight line mode button in the trails subrow.")]
        [SerializeField] private Button _lineModeButton;
        [Tooltip("Pen / curvy mode button in the trails subrow.")]
        [SerializeField] private Button _penModeButton;
        [Tooltip("Trail width slider (0–100). Maps to world width 5–10.")]
        [SerializeField] private Slider _trailWidthSlider;
        [Tooltip("Text label showing current width value.")]
        [SerializeField] private TextMeshProUGUI _trailWidthText;
        
        // ── Lift Build Type UI (drag from LiftsSubRow) ──────────────────────
        [Header("Lift Build Type UI")]
        [Tooltip("The LiftBuildTool component in the scene.")]
        [SerializeField] private LiftBuildTool _liftBuildTool;
        [SerializeField] private Button _oneSeatLowSpeedButton;
        [SerializeField] private Button _oneSeatHighSpeedButton;
        [SerializeField] private Button _twoSeatLowSpeedButton;
        [SerializeField] private Button _twoSeatHighSpeedButton;

        // ── Runtime state ────────────────────────────────────────────────────
        private int _activeIndex = -1;
        private Color[] _originalColors;
        private TrailDrawMode _activeTrailMode = TrailDrawMode.Paint;
        private Color _paintOriginal, _lineOriginal, _penOriginal;
        /// <summary>Orange sub-button tint only after the user picks a mode in this dock session.</summary>
        private bool _trailSubOptionChosen;
        private LiftType _selectedLiftType = LiftType.OneSeatLowSpeed;
        private Color _oneSeatLowOriginal, _oneSeatHighOriginal, _twoSeatLowOriginal, _twoSeatHighOriginal;
        /// <summary>Orange sub-button tint only after the user picks a lift type in this dock session.</summary>
        private bool _liftSubOptionChosen;
        private bool _skipNextToolClose;

        private const float WidthMin = 10f;
        private const float WidthMax = 30f;

        void Start()
        {
            _originalColors = new Color[_categories.Length];
            for (int i = 0; i < _categories.Length; i++)
            {
                var img = _categories[i].button?.targetGraphic as Image;
                _originalColors[i] = img != null ? img.color : Color.white;
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(CloseDock);
                SetupTooltip(_closeButton, TooltipTexts.Dock.CloseHeader, TooltipTexts.Dock.CloseContent);
            }

            for (int i = 0; i < _categories.Length; i++)
            {
                int idx = i;
                if (_categories[i].button != null)
                {
                    _categories[i].button.onClick.AddListener(() => OnCategoryClicked(idx));
                    // Add tooltip for category button
                    var rawCategoryName = _categories[i].subRow != null ? _categories[i].subRow.name : "Category";
                    // Clean up the name (remove "SubRow" suffix)
                    string cleanCategoryName = rawCategoryName;
                    if (cleanCategoryName.EndsWith("SubRow", System.StringComparison.OrdinalIgnoreCase))
                    {
                        cleanCategoryName = cleanCategoryName.Substring(0, cleanCategoryName.Length - 6);
                    }
                    // Capitalize first letter
                    if (cleanCategoryName.Length > 0)
                    {
                        cleanCategoryName = char.ToUpper(cleanCategoryName[0]) + cleanCategoryName.Substring(1).ToLower();
                    }
                    SetupTooltip(_categories[i].button, cleanCategoryName, TooltipTexts.Dock.GetCategoryContent(rawCategoryName));
                }
            }

            // Trail mode buttons
            CacheButtonColor(_paintModeButton, out _paintOriginal);
            CacheButtonColor(_lineModeButton,  out _lineOriginal);
            CacheButtonColor(_penModeButton,   out _penOriginal);

            // Lift type buttons
            CacheButtonColor(_oneSeatLowSpeedButton,  out _oneSeatLowOriginal);
            CacheButtonColor(_oneSeatHighSpeedButton, out _oneSeatHighOriginal);
            CacheButtonColor(_twoSeatLowSpeedButton,  out _twoSeatLowOriginal);
            CacheButtonColor(_twoSeatHighSpeedButton, out _twoSeatHighOriginal);

            if (_paintModeButton != null)
            {
                _paintModeButton.onClick.AddListener(() => SetTrailMode(TrailDrawMode.Paint));
                SetupTooltip(_paintModeButton, TooltipTexts.Dock.PaintModeHeader, TooltipTexts.Dock.PaintModeContent);
            }
            if (_lineModeButton != null)
            {
                _lineModeButton.onClick.AddListener(() => SetTrailMode(TrailDrawMode.Line));
                SetupTooltip(_lineModeButton, TooltipTexts.Dock.LineModeHeader, TooltipTexts.Dock.LineModeContent);
            }
            if (_penModeButton != null)
            {
                _penModeButton.onClick.AddListener(() => SetTrailMode(TrailDrawMode.Pen));
                SetupTooltip(_penModeButton, TooltipTexts.Dock.PenModeHeader, TooltipTexts.Dock.PenModeContent);
            }
            
            if (_trailWidthSlider != null)
            {
                SetupTooltip(_trailWidthSlider, TooltipTexts.Dock.TrailWidthHeader, TooltipTexts.Dock.TrailWidthContent);
            }

            if (_trailWidthSlider != null)
            {
                _trailWidthSlider.minValue = 1f;
                _trailWidthSlider.maxValue = 100f;
                _trailWidthSlider.wholeNumbers = true;
                _trailWidthSlider.value = 50f;
                _trailWidthSlider.onValueChanged.AddListener(OnTrailWidthChanged);
                RefreshWidthText(50f);
                // Sync initial value so cursor reflects slider from the start
                OnTrailWidthChanged(50f);
            }

            // Lift type buttons - wire up click handlers
            if (_oneSeatLowSpeedButton != null)
            {
                _oneSeatLowSpeedButton.onClick.AddListener(() => OnLiftTypeClicked(LiftType.OneSeatLowSpeed));
                SetupTooltip(_oneSeatLowSpeedButton, TooltipTexts.Dock.OneSeatLowSpeedHeader, TooltipTexts.Dock.OneSeatLowSpeedContent);
            }
            if (_oneSeatHighSpeedButton != null)
            {
                _oneSeatHighSpeedButton.onClick.AddListener(() => OnLiftTypeClicked(LiftType.OneSeatHighSpeed));
                SetupTooltip(_oneSeatHighSpeedButton, TooltipTexts.Dock.OneSeatHighSpeedHeader, TooltipTexts.Dock.OneSeatHighSpeedContent);
            }
            if (_twoSeatLowSpeedButton != null)
            {
                _twoSeatLowSpeedButton.onClick.AddListener(() => OnLiftTypeClicked(LiftType.TwoSeatLowSpeed));
                SetupTooltip(_twoSeatLowSpeedButton, TooltipTexts.Dock.TwoSeatLowSpeedHeader, TooltipTexts.Dock.TwoSeatLowSpeedContent);
            }
            if (_twoSeatHighSpeedButton != null)
            {
                _twoSeatHighSpeedButton.onClick.AddListener(() => OnLiftTypeClicked(LiftType.TwoSeatHighSpeed));
                SetupTooltip(_twoSeatHighSpeedButton, TooltipTexts.Dock.TwoSeatHighSpeedHeader, TooltipTexts.Dock.TwoSeatHighSpeedContent);
            }

            RefreshTrailModeButtons();
            RefreshLiftTypeButtons();

            var uiManager = UIManager.Instance;
            if (uiManager != null)
            {
                uiManager.OnMenuOpened.AddListener(CloseDock);
                uiManager.OnToolChanged.AddListener(OnToolChanged);
            }

            CloseDock();
        }

        // ── Button handler ───────────────────────────────────────────────────

        private void OnCategoryClicked(int index)
        {
            if (_activeIndex == index)
                CloseDock();
            else
                OpenCategory(index);
        }

        // ── Open / Close ─────────────────────────────────────────────────────

        private void OpenCategory(int index)
        {
            _activeIndex = index;

            // Show bg, hide/show each subrow
            if (_subRowBg != null) _subRowBg.SetActive(true);

            for (int i = 0; i < _categories.Length; i++)
            {
                if (_categories[i].subRow != null)
                    _categories[i].subRow.SetActive(i == index);

                SetButtonColor(_categories[i].button, i == index, _originalColors[i]);
            }

            GameObject opened = index >= 0 && index < _categories.Length ? _categories[index].subRow : null;
            if (opened != null)
            {
                var root = opened.transform;
                if (SubRowContains(root, _paintModeButton))
                {
                    _trailSubOptionChosen = false;
                    RefreshTrailModeButtons();
                }
                if (SubRowContains(root, _oneSeatLowSpeedButton))
                {
                    _liftSubOptionChosen = false;
                    RefreshLiftTypeButtons();
                }
            }
        }

        private static bool SubRowContains(Transform subRowRoot, Component control)
        {
            if (subRowRoot == null || control == null) return false;
            return control.transform.IsChildOf(subRowRoot);
        }

        private void OnToolChanged(BaseTool tool)
        {
            if (_skipNextToolClose)
            {
                _skipNextToolClose = false;
                return;
            }
            if (tool != null)
            {
                // Sync trail width when trail tool becomes active so the cursor
                // reflects the slider value immediately (slider starts at 50)
                if (tool is TrailBuildTool && _trailWidthSlider != null)
                    OnTrailWidthChanged(_trailWidthSlider.value);
                CloseDock();
            }
        }

        public void CloseDock()
        {
            _activeIndex = -1;

            if (_subRowBg != null) _subRowBg.SetActive(false);

            for (int i = 0; i < _categories.Length; i++)
            {
                if (_categories[i].subRow != null)
                    _categories[i].subRow.SetActive(false);

                SetButtonColor(_categories[i].button, false, _originalColors[i]);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void SetButtonColor(Button button, bool active, Color originalColor)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = active ? SelectedColor : originalColor;
            ButtonHoverFeedback.Apply(button, UIManager.Instance?.Theme);
        }

        // ── Trail mode / width ───────────────────────────────────────────────

        private void SetTrailMode(TrailDrawMode mode)
        {
            _trailSubOptionChosen = true;
            _activeTrailMode = mode;
            RefreshTrailModeButtons();

            // Activate the trail tool if it isn't already active
            if (_trailBuildTool != null && UIManager.Instance != null
                && !UIManager.Instance.IsToolActive(_trailBuildTool))
            {
                _skipNextToolClose = true;
                UIManager.Instance.ActivateTool(_trailBuildTool);
            }

            var tool = UIManager.Instance?.ActiveTool as TrailBuildTool;
            tool?.SetDrawMode(mode);
        }

        private void OnTrailWidthChanged(float sliderValue)
        {
            RefreshWidthText(sliderValue);
            float worldWidth = Mathf.Lerp(WidthMin, WidthMax, (sliderValue - 1f) / 99f);

            var tool = UIManager.Instance?.ActiveTool as TrailBuildTool;
            tool?.SetTrailWidth(worldWidth);
        }

        private void RefreshWidthText(float sliderValue)
        {
            if (_trailWidthText != null)
                _trailWidthText.text = Mathf.RoundToInt(sliderValue).ToString();
        }

        private void RefreshTrailModeButtons()
        {
            SetModeButtonColor(_paintModeButton, _paintOriginal, _trailSubOptionChosen && _activeTrailMode == TrailDrawMode.Paint);
            SetModeButtonColor(_lineModeButton,  _lineOriginal,  _trailSubOptionChosen && _activeTrailMode == TrailDrawMode.Line);
            SetModeButtonColor(_penModeButton,   _penOriginal,   _trailSubOptionChosen && _activeTrailMode == TrailDrawMode.Pen);
        }

        private void RefreshLiftTypeButtons()
        {
            SetModeButtonColor(_oneSeatLowSpeedButton,  _oneSeatLowOriginal,  _liftSubOptionChosen && _selectedLiftType == LiftType.OneSeatLowSpeed);
            SetModeButtonColor(_oneSeatHighSpeedButton, _oneSeatHighOriginal, _liftSubOptionChosen && _selectedLiftType == LiftType.OneSeatHighSpeed);
            SetModeButtonColor(_twoSeatLowSpeedButton,  _twoSeatLowOriginal,  _liftSubOptionChosen && _selectedLiftType == LiftType.TwoSeatLowSpeed);
            SetModeButtonColor(_twoSeatHighSpeedButton, _twoSeatHighOriginal, _liftSubOptionChosen && _selectedLiftType == LiftType.TwoSeatHighSpeed);
        }

        private static void SetModeButtonColor(Button btn, Color original, bool active)
        {
            if (btn == null) return;
            var img = btn.targetGraphic as Image;
            if (img != null)
                img.color = active ? SelectedColor : original;
            ButtonHoverFeedback.Apply(btn, UIManager.Instance?.Theme);
        }

        private static void CacheButtonColor(Button btn, out Color color)
        {
            color = Color.white;
            if (btn == null) return;
            var img = btn.targetGraphic as Image;
            if (img != null) color = img.color;
        }
        
        private void OnLiftTypeClicked(LiftType liftType)
        {
            _liftSubOptionChosen = true;
            _selectedLiftType = liftType;
            RefreshLiftTypeButtons();

            // Show warning for unimplemented types, but still allow selection
            if (!LiftTypeSpecs.IsImplemented(liftType))
            {
                NotificationManager.Instance?.ShowWarning($"{LiftTypeSpecs.GetDisplayName(liftType)} is not implemented yet.");
            }

            // Only activate the tool if it's not already active
            if (_liftBuildTool != null && UIManager.Instance != null
                && !UIManager.Instance.IsToolActive(_liftBuildTool))
            {
                _skipNextToolClose = true;
                UIManager.Instance.ActivateTool(_liftBuildTool);
            }

            // Update the active tool's lift type
            var tool = UIManager.Instance?.ActiveTool as LiftBuildTool;
            tool?.SetLiftType(liftType);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Returns the index of the currently active category, or -1 if closed.</summary>
        public int ActiveIndex => _activeIndex;
        
        private void SetupTooltip(Button button, string header, string content)
        {
            if (button == null) return;
            
            var tooltipTrigger = button.GetComponent<TooltipTrigger>();
            if (tooltipTrigger == null)
            {
                tooltipTrigger = button.gameObject.AddComponent<TooltipTrigger>();
            }
            tooltipTrigger.SetContent(header, content);
        }
        
        private void SetupTooltip(Slider slider, string header, string content)
        {
            if (slider == null) return;
            
            var tooltipTrigger = slider.GetComponent<TooltipTrigger>();
            if (tooltipTrigger == null)
            {
                tooltipTrigger = slider.gameObject.AddComponent<TooltipTrigger>();
            }
            tooltipTrigger.SetContent(header, content);
        }
    }

    // ── Data type ─────────────────────────────────────────────────────────────

    [Serializable]
    public class DockCategory
    {
        [Tooltip("The bottom dock button for this category.")]
        public Button button;

        [Tooltip("The standalone subrow panel shown when this category is active.")]
        public GameObject subRow;
    }
}
