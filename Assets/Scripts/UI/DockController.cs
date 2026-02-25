using System;
using UnityEngine;
using UnityEngine.UI;

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

        // ── Runtime state ────────────────────────────────────────────────────
        private int _activeIndex = -1;
        private Color[] _originalColors;

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

            var uiManager = UIManager.Instance;
            if (uiManager != null)
            {
                uiManager.OnMenuOpened.AddListener(CloseDock);
                // Close the dock whenever a build tool is activated (clears screen for terrain work)
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
