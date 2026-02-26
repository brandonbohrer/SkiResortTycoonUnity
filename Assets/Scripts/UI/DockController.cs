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

        // ── Runtime state ────────────────────────────────────────────────────
        private int _activeIndex = -1;
        private Color[] _originalColors;
        private TrailDrawMode _activeTrailMode = TrailDrawMode.Paint;
        private Color _paintOriginal, _lineOriginal, _penOriginal;
        private bool _skipNextToolClose;

        private const float WidthMin = 7.5f;
        private const float WidthMax = 18f;

        void Start()
        {
            _originalColors = new Color[_categories.Length];
            for (int i = 0; i < _categories.Length; i++)
            {
                var img = _categories[i].button?.targetGraphic as Image;
                _originalColors[i] = img != null ? img.color : Color.white;
            }

            if (_closeButton != null)
                _closeButton.onClick.AddListener(CloseDock);

            for (int i = 0; i < _categories.Length; i++)
            {
                int idx = i;
                if (_categories[i].button != null)
                    _categories[i].button.onClick.AddListener(() => OnCategoryClicked(idx));
            }

            // Trail mode buttons
            CacheButtonColor(_paintModeButton, out _paintOriginal);
            CacheButtonColor(_lineModeButton,  out _lineOriginal);
            CacheButtonColor(_penModeButton,   out _penOriginal);

            if (_paintModeButton != null) _paintModeButton.onClick.AddListener(() => SetTrailMode(TrailDrawMode.Paint));
            if (_lineModeButton  != null) _lineModeButton.onClick.AddListener(()  => SetTrailMode(TrailDrawMode.Line));
            if (_penModeButton   != null) _penModeButton.onClick.AddListener(()   => SetTrailMode(TrailDrawMode.Pen));

            if (_trailWidthSlider != null)
            {
                _trailWidthSlider.minValue = 1f;
                _trailWidthSlider.maxValue = 100f;
                _trailWidthSlider.wholeNumbers = true;
                _trailWidthSlider.value = 1f;
                _trailWidthSlider.onValueChanged.AddListener(OnTrailWidthChanged);
                RefreshWidthText(1f);
            }

            RefreshTrailModeButtons();

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
        }

        private void OnToolChanged(BaseTool tool)
        {
            if (_skipNextToolClose)
            {
                _skipNextToolClose = false;
                return;
            }
            if (tool != null) CloseDock();
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
        }

        // ── Trail mode / width ───────────────────────────────────────────────

        private void SetTrailMode(TrailDrawMode mode)
        {
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
            SetModeButtonColor(_paintModeButton, _paintOriginal, _activeTrailMode == TrailDrawMode.Paint);
            SetModeButtonColor(_lineModeButton,  _lineOriginal,  _activeTrailMode == TrailDrawMode.Line);
            SetModeButtonColor(_penModeButton,   _penOriginal,   _activeTrailMode == TrailDrawMode.Pen);
        }

        private static void SetModeButtonColor(Button btn, Color original, bool active)
        {
            if (btn == null) return;
            var img = btn.targetGraphic as Image;
            if (img != null)
                img.color = active ? SelectedColor : original;
        }

        private static void CacheButtonColor(Button btn, out Color color)
        {
            color = Color.white;
            if (btn == null) return;
            var img = btn.targetGraphic as Image;
            if (img != null) color = img.color;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Returns the index of the currently active category, or -1 if closed.</summary>
        public int ActiveIndex => _activeIndex;
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
