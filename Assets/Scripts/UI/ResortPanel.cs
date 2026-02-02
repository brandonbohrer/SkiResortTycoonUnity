using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Collapsible panel showing resort status, visitors, finances, and alerts.
    /// Located at bottom-left of the screen.
    /// </summary>
    public class ResortPanel : MonoBehaviour
    {
        [Header("Panel Settings")]
        [SerializeField] private bool _startExpanded = true;
        [SerializeField] private float _collapsedHeight = 40f;
        [SerializeField] private float _expandedHeight = 280f;
        
        [Header("UI Elements")]
        [SerializeField] private Button _collapseButton;
        [SerializeField] private TextMeshProUGUI _collapseButtonText;
        [SerializeField] private RectTransform _contentContainer;
        [SerializeField] private CanvasGroup _contentCanvasGroup;
        
        [Header("Tab System")]
        [SerializeField] private List<Button> _tabButtons;
        [SerializeField] private List<GameObject> _tabContents;
        [SerializeField] private Color _tabActiveColor = new Color(0f, 0.737f, 0.831f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        private RectTransform _rectTransform;
        private bool _isExpanded;
        private int _activeTabIndex = 0;
        private Coroutine _animationCoroutine;
        
        /// <summary>
        /// Whether the panel is currently expanded
        /// </summary>
        public bool IsExpanded => _isExpanded;
        
        void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }
        
        void Start()
        {
            // Set initial state
            _isExpanded = _startExpanded;
            SetPanelState(_isExpanded, animate: false);
            
            // Set up collapse button
            if (_collapseButton != null)
            {
                _collapseButton.onClick.AddListener(ToggleExpanded);
            }
            
            // Set up tab buttons
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                int tabIndex = i; // Capture for closure
                _tabButtons[i].onClick.AddListener(() => SelectTab(tabIndex));
            }
            
            // Select first tab
            SelectTab(0);
        }
        
        /// <summary>
        /// Toggles the panel between expanded and collapsed states
        /// </summary>
        public void ToggleExpanded()
        {
            SetExpanded(!_isExpanded);
        }
        
        /// <summary>
        /// Sets the panel to expanded or collapsed
        /// </summary>
        public void SetExpanded(bool expanded)
        {
            if (_isExpanded == expanded) return;
            
            _isExpanded = expanded;
            SetPanelState(expanded, animate: true);
        }
        
        /// <summary>
        /// Selects a tab by index
        /// </summary>
        public void SelectTab(int index)
        {
            if (index < 0 || index >= _tabContents.Count) return;
            
            _activeTabIndex = index;
            
            // Update tab button visuals
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var colors = _tabButtons[i].colors;
                colors.normalColor = i == index ? _tabActiveColor : _tabInactiveColor;
                _tabButtons[i].colors = colors;
            }
            
            // Show/hide tab contents
            for (int i = 0; i < _tabContents.Count; i++)
            {
                _tabContents[i].SetActive(i == index);
            }
        }
        
        private void SetPanelState(bool expanded, bool animate)
        {
            float targetHeight = expanded ? _expandedHeight : _collapsedHeight;
            float targetAlpha = expanded ? 1f : 0f;
            
            // Update collapse button text
            if (_collapseButtonText != null)
            {
                _collapseButtonText.text = expanded ? "▼" : "▲";
            }
            
            if (animate && Application.isPlaying)
            {
                if (_animationCoroutine != null)
                {
                    StopCoroutine(_animationCoroutine);
                }
                _animationCoroutine = StartCoroutine(AnimatePanelState(targetHeight, targetAlpha));
            }
            else
            {
                // Instant change
                if (_rectTransform != null)
                {
                    var size = _rectTransform.sizeDelta;
                    size.y = targetHeight;
                    _rectTransform.sizeDelta = size;
                }
                
                if (_contentCanvasGroup != null)
                {
                    _contentCanvasGroup.alpha = targetAlpha;
                    _contentCanvasGroup.interactable = expanded;
                    _contentCanvasGroup.blocksRaycasts = expanded;
                }
            }
        }
        
        private IEnumerator AnimatePanelState(float targetHeight, float targetAlpha)
        {
            float duration = UIManager.Instance?.Theme?.PanelAnimationDuration ?? 0.25f;
            float elapsed = 0f;
            
            float startHeight = _rectTransform.sizeDelta.y;
            float startAlpha = _contentCanvasGroup?.alpha ?? 1f;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                // Use ease-in-out curve
                t = t * t * (3f - 2f * t);
                
                // Animate height
                if (_rectTransform != null)
                {
                    var size = _rectTransform.sizeDelta;
                    size.y = Mathf.Lerp(startHeight, targetHeight, t);
                    _rectTransform.sizeDelta = size;
                }
                
                // Animate content alpha
                if (_contentCanvasGroup != null)
                {
                    _contentCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                }
                
                yield return null;
            }
            
            // Final state
            if (_rectTransform != null)
            {
                var size = _rectTransform.sizeDelta;
                size.y = targetHeight;
                _rectTransform.sizeDelta = size;
            }
            
            if (_contentCanvasGroup != null)
            {
                _contentCanvasGroup.alpha = targetAlpha;
                _contentCanvasGroup.interactable = targetAlpha > 0.5f;
                _contentCanvasGroup.blocksRaycasts = targetAlpha > 0.5f;
            }
            
            _animationCoroutine = null;
        }
    }
}
