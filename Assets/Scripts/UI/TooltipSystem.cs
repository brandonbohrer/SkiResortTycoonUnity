using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Tooltip system that displays contextual information on hover.
    /// </summary>
    public class TooltipSystem : MonoBehaviour
    {
        public static TooltipSystem Instance { get; private set; }
        
        [Header("UI Elements")]
        [SerializeField] private GameObject _tooltipObject;
        [SerializeField] private TextMeshProUGUI _headerText;
        [SerializeField] private TextMeshProUGUI _contentText;
        [SerializeField] private LayoutElement _layoutElement;
        
        [Header("Settings")]
        [SerializeField] private float _showDelay = 0.5f;
        [SerializeField] private float _maxWidth = 300f;
        [SerializeField] private Vector2 _offset = new Vector2(16f, -16f);
        
        private RectTransform _rectTransform;
        private RectTransform _canvasRect;
        private float _hoverTimer = 0f;
        private bool _isHovering = false;
        private string _pendingHeader;
        private string _pendingContent;
        
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            _rectTransform = _tooltipObject?.GetComponent<RectTransform>();
            
            // Find canvas
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _canvasRect = canvas.GetComponent<RectTransform>();
            }
            
            Hide();
        }
        
        void Update()
        {
            if (_isHovering)
            {
                _hoverTimer += Time.unscaledDeltaTime;
                
                if (_hoverTimer >= _showDelay && !_tooltipObject.activeSelf)
                {
                    ShowInternal();
                }
                
                if (_tooltipObject.activeSelf)
                {
                    UpdatePosition();
                }
            }
        }
        
        /// <summary>
        /// Prepares to show a tooltip after the hover delay
        /// </summary>
        public void PrepareShow(string header, string content)
        {
            _pendingHeader = header;
            _pendingContent = content;
            _isHovering = true;
            _hoverTimer = 0f;
        }
        
        /// <summary>
        /// Shows a tooltip immediately (no delay)
        /// </summary>
        public void ShowImmediate(string header, string content)
        {
            _pendingHeader = header;
            _pendingContent = content;
            _isHovering = true;
            ShowInternal();
        }
        
        /// <summary>
        /// Hides the tooltip
        /// </summary>
        public void Hide()
        {
            _isHovering = false;
            _hoverTimer = 0f;
            
            if (_tooltipObject != null)
            {
                _tooltipObject.SetActive(false);
            }
        }
        
        private void ShowInternal()
        {
            if (_tooltipObject == null) return;
            
            // Set content
            if (_headerText != null)
            {
                _headerText.text = _pendingHeader;
                _headerText.gameObject.SetActive(!string.IsNullOrEmpty(_pendingHeader));
            }
            
            if (_contentText != null)
            {
                _contentText.text = _pendingContent;
            }
            
            // Update layout element for width
            if (_layoutElement != null)
            {
                int headerLength = _pendingHeader?.Length ?? 0;
                int contentLength = _pendingContent?.Length ?? 0;
                
                _layoutElement.enabled = (headerLength > 40 || contentLength > 80);
                _layoutElement.preferredWidth = _maxWidth;
            }
            
            _tooltipObject.SetActive(true);
            
            // Force layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
            
            UpdatePosition();
        }
        
        private void UpdatePosition()
        {
            if (_rectTransform == null) return;
            
            Vector2 position = Input.mousePosition;
            
            // Apply offset
            position += _offset;
            
            // Get tooltip size
            Vector2 tooltipSize = _rectTransform.sizeDelta;
            
            // Clamp to screen bounds
            if (_canvasRect != null)
            {
                Vector2 screenSize = _canvasRect.sizeDelta;
                
                // Right edge
                if (position.x + tooltipSize.x > screenSize.x)
                {
                    position.x = Input.mousePosition.x - tooltipSize.x - _offset.x;
                }
                
                // Bottom edge
                if (position.y - tooltipSize.y < 0)
                {
                    position.y = tooltipSize.y;
                }
                
                // Top edge
                if (position.y > screenSize.y)
                {
                    position.y = screenSize.y;
                }
                
                // Left edge
                if (position.x < 0)
                {
                    position.x = 0;
                }
            }
            
            _rectTransform.position = position;
        }
    }
    
    /// <summary>
    /// Attach to UI elements to show a tooltip on hover
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string _header;
        [TextArea(2, 5)]
        [SerializeField] private string _content;
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            TooltipSystem.Instance?.PrepareShow(_header, _content);
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipSystem.Instance?.Hide();
        }
        
        void OnDisable()
        {
            TooltipSystem.Instance?.Hide();
        }
    }
}
