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
        [SerializeField] private float _showDelay = 1.0f;
        [SerializeField] private float _maxWidth = 300f;
        [SerializeField] private Vector2 _offset = new Vector2(16f, -16f);
        
        [Header("Smart Positioning")]
        [Tooltip("Distance from screen edge to trigger smart positioning (in pixels)")]
        [SerializeField] private float _edgeMargin = 50f;
        [Tooltip("Offset when tooltip appears above cursor (bottom of screen)")]
        [SerializeField] private Vector2 _offsetAbove = new Vector2(16f, 16f);
        [Tooltip("Offset when tooltip appears below cursor (top of screen)")]
        [SerializeField] private Vector2 _offsetBelow = new Vector2(16f, -8f);
        [Tooltip("Offset when tooltip appears to left of cursor (right edge)")]
        [SerializeField] private Vector2 _offsetLeft = new Vector2(-16f, -16f);
        [Tooltip("Offset when tooltip appears to right of cursor (left edge)")]
        [SerializeField] private Vector2 _offsetRight = new Vector2(16f, -16f);
        
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
            
            Vector2 mousePos = Input.mousePosition;
            
            // Get tooltip size (force layout rebuild to get accurate size)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
            Vector2 tooltipSize = _rectTransform.sizeDelta;
            
            // Get screen size
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            
            // Determine which edges we're near (using screen coordinates)
            bool nearTop = mousePos.y > screenSize.y - _edgeMargin;
            bool nearBottom = mousePos.y < _edgeMargin;
            bool nearRight = mousePos.x > screenSize.x - _edgeMargin;
            bool nearLeft = mousePos.x < _edgeMargin;
            
            // Smart positioning: choose offset based on screen position
            Vector2 offset = _offset; // Default offset (bottom-right)
            
            // Priority: Top/Bottom first, then Left/Right
            if (nearTop)
            {
                // Near top of screen - show tooltip BELOW cursor
                offset = _offsetBelow;
            }
            else if (nearBottom)
            {
                // Near bottom of screen - show tooltip ABOVE cursor
                offset = _offsetAbove;
            }
            else if (nearRight)
            {
                // Near right edge - show tooltip to LEFT of cursor
                offset = _offsetLeft;
            }
            else if (nearLeft)
            {
                // Near left edge - show tooltip to RIGHT of cursor
                offset = _offsetRight;
            }
            
            // Calculate initial position with chosen offset
            Vector2 position = mousePos + offset;
            
            // Final bounds checking and smart flipping if still out of bounds
            // Right edge - flip to left side if needed
            if (position.x + tooltipSize.x > screenSize.x)
            {
                position.x = mousePos.x - tooltipSize.x - Mathf.Abs(offset.x);
            }
            
            // Left edge - ensure it doesn't go off screen
            if (position.x < 0)
            {
                position.x = Mathf.Abs(offset.x);
            }
            
            // Top edge - flip to below if needed
            if (position.y + tooltipSize.y > screenSize.y)
            {
                position.y = mousePos.y - tooltipSize.y - Mathf.Abs(offset.y);
            }
            
            // Bottom edge - ensure it doesn't go off screen
            if (position.y < 0)
            {
                position.y = Mathf.Abs(offset.y);
            }
            
            _rectTransform.position = position;
        }
    }
    
    /// <summary>
    /// Source for tooltip content
    /// </summary>
    public enum TooltipSource
    {
        Manual,      // Use manual header/content strings
        FromTool,    // Get content from BaseTool reference
        FromButton   // Get content from Button reference (future)
    }
    
    /// <summary>
    /// Attach to UI elements to show a tooltip on hover.
    /// Supports manual content or automatic content from BaseTool references.
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Content Source")]
        [SerializeField] private TooltipSource _source = TooltipSource.Manual;
        
        [Header("Manual Content")]
        [SerializeField] private string _header;
        [TextArea(2, 5)]
        [SerializeField] private string _content;
        
        [Header("Automatic Content")]
        [Tooltip("BaseTool to get tooltip content from (when Source = FromTool)")]
        [SerializeField] private BaseTool _toolReference;
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            string header = "";
            string content = "";
            
            // Get content based on source
            switch (_source)
            {
                case TooltipSource.Manual:
                    header = _header;
                    content = _content;
                    break;
                    
                case TooltipSource.FromTool:
                    if (_toolReference != null)
                    {
                        header = _toolReference.ToolName;
                        content = _toolReference.ToolDescription;
                    }
                    else
                    {
                        header = "Tool";
                        content = "No tool reference set";
                    }
                    break;
                    
                case TooltipSource.FromButton:
                    // Future: extract from button
                    header = _header;
                    content = _content;
                    break;
            }
            
            // Only show if we have content
            if (!string.IsNullOrEmpty(header) || !string.IsNullOrEmpty(content))
            {
                TooltipSystem.Instance?.PrepareShow(header, content);
            }
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipSystem.Instance?.Hide();
        }
        
        void OnDisable()
        {
            TooltipSystem.Instance?.Hide();
        }
        
        /// <summary>
        /// Set tooltip content programmatically
        /// </summary>
        public void SetContent(string header, string content)
        {
            _header = header;
            _content = content;
            _source = TooltipSource.Manual;
        }
        
        /// <summary>
        /// Set tooltip to use a BaseTool for content
        /// </summary>
        public void SetTool(BaseTool tool)
        {
            _toolReference = tool;
            _source = TooltipSource.FromTool;
        }
    }
}
