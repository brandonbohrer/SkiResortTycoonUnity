using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Manages the bottom dock bar and its DockSubRows.
    /// Each category entry maps a bottom button to its subrow panel.
    /// Clicking a button opens its subrow (and highlights it orange).
    /// Clicking the same button again closes everything.
    /// Attach to the bottom dock root GameObject.
    /// </summary>
    public class DockController : MonoBehaviour
    {
        // ── Brand color ─────────────────────────────────────────────────
        private static readonly Color SelectedColor = new Color(0.9647f, 0.5373f, 0.2314f, 1f); // #F6893B

        // ── Inspector ────────────────────────────────────────────────────
        [Tooltip("The DockSubRows container that slides/fades in when a category is active.")]
        [SerializeField] private GameObject _dockSubRows;

        [Tooltip("Background panel for the subrow area — toggled in sync with DockSubRows.")]
        [SerializeField] private GameObject _subRowBg;

        [Tooltip("Optional close/back button that dismisses the open subrow.")]
        [SerializeField] private Button _closeButton;

        [SerializeField] private DockCategory[] _categories;

        // ── Runtime state ────────────────────────────────────────────────
        private int _activeIndex = -1;
        private Color[] _originalColors;
        private RectTransform _dockSubRowsRect;

        void Start()
        {
            // Cache RectTransform for layout rebuilds
            if (_dockSubRows != null)
                _dockSubRowsRect = _dockSubRows.GetComponent<RectTransform>();

            // Cache each button's original Image color (preserve the dark-blue look)
            _originalColors = new Color[_categories.Length];
            for (int i = 0; i < _categories.Length; i++)
            {
                var img = _categories[i].button?.targetGraphic as Image;
                _originalColors[i] = img != null ? img.color : Color.white;
            }

            // Wire close button
            if (_closeButton != null)
                _closeButton.onClick.AddListener(CloseDock);

            // Wire click listeners
            for (int i = 0; i < _categories.Length; i++)
            {
                int idx = i; // capture for closure
                if (_categories[i].button != null)
                    _categories[i].button.onClick.AddListener(() => OnCategoryClicked(idx));
            }

            // Close menu when game-menu opens
            var uiManager = UIManager.Instance;
            if (uiManager != null)
                uiManager.OnMenuOpened.AddListener(CloseDock);

            // Start fully closed
            CloseDock();
        }

        // ── Button handler ───────────────────────────────────────────────

        private void OnCategoryClicked(int index)
        {
            if (_activeIndex == index)
                CloseDock();   // same button → toggle off
            else
                OpenCategory(index);
        }

        // ── Open / Close ─────────────────────────────────────────────────

        private void OpenCategory(int index)
        {
            StopAllCoroutines();

            var oldSubRow = (_activeIndex >= 0 && _activeIndex < _categories.Length)
                ? _categories[_activeIndex].subRow : null;
            var newSubRow = _categories[index].subRow;

            // 1. old on
            if (oldSubRow != null) oldSubRow.SetActive(true);
            // 2. old off
            if (oldSubRow != null) oldSubRow.SetActive(false);
            // 3. dock off
            if (_subRowBg    != null) _subRowBg.SetActive(false);
            if (_dockSubRows != null) _dockSubRows.SetActive(false);
            // 4. dock on
            if (_dockSubRows != null) _dockSubRows.SetActive(true);
            if (_subRowBg    != null) _subRowBg.SetActive(true);
            // 5. new on
            if (newSubRow != null) newSubRow.SetActive(true);
            // 6. new off
            if (newSubRow != null) newSubRow.SetActive(false);
            // 7. dock off
            if (_subRowBg    != null) _subRowBg.SetActive(false);
            if (_dockSubRows != null) _dockSubRows.SetActive(false);
            // 8. dock on
            if (_dockSubRows != null) _dockSubRows.SetActive(true);
            if (_subRowBg    != null) _subRowBg.SetActive(true);
            // 9. new on
            if (newSubRow != null) newSubRow.SetActive(true);

            _activeIndex = index;

            // Update button highlights
            for (int i = 0; i < _categories.Length; i++)
                SetButtonColor(_categories[i].button, i == index, _originalColors[i]);

            ForceLayoutRebuild();
        }

        [System.Obsolete("Unused — replaced by synchronous OpenCategory")]
        private System.Collections.IEnumerator SwitchCategory(int index) { yield break; }

        public void CloseDock()
        {
            _activeIndex = -1;

            // Hide DockSubRows container
            if (_dockSubRows != null) _dockSubRows.SetActive(false);
            if (_subRowBg    != null) _subRowBg.SetActive(false);

            // Hide all subrows and deselect all buttons
            for (int i = 0; i < _categories.Length; i++)
            {
                if (_categories[i].subRow != null)
                    _categories[i].subRow.SetActive(false);

                SetButtonColor(_categories[i].button, false, _originalColors[i]);
            }
        }

        // ── Layout fix ───────────────────────────────────────────────────

        private void ForceLayoutRebuild()
        {
            if (_dockSubRowsRect == null) return;

            // Walk up the hierarchy and rebuild each parent so sizes propagate correctly
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_dockSubRowsRect);

            // Also rebuild the parent of dockSubRows so it adjusts to the new size
            RectTransform parent = _dockSubRowsRect.parent as RectTransform;
            if (parent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static void SetButtonColor(Button button, bool active, Color originalColor)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = active ? SelectedColor : originalColor;
        }

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>Returns the index of the currently active category, or -1 if closed.</summary>
        public int ActiveIndex => _activeIndex;
    }

    // ── Data type ─────────────────────────────────────────────────────────

    [Serializable]
    public class DockCategory
    {
        [Tooltip("The bottom dock button for this category.")]
        public Button button;

        [Tooltip("The subrow panel shown inside DockSubRows when this category is active.")]
        public GameObject subRow;
    }
}
