using UnityEngine;
using UnityEngine.UI;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Button component with automatic tooltip support.
    /// Auto-attaches TooltipTrigger if missing.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TooltipButton : MonoBehaviour
    {
        [Header("Tooltip")]
        [SerializeField] private string _tooltipHeader;
        [TextArea(2, 5)]
        [SerializeField] private string _tooltipContent;
        
        [Header("Tool Reference (optional)")]
        [Tooltip("If set, tooltip will use tool's name and description")]
        [SerializeField] private BaseTool _toolReference;
        
        private TooltipTrigger _tooltipTrigger;
        
        void Awake()
        {
            // Ensure TooltipTrigger exists
            _tooltipTrigger = GetComponent<TooltipTrigger>();
            if (_tooltipTrigger == null)
            {
                _tooltipTrigger = gameObject.AddComponent<TooltipTrigger>();
            }
            
            // Configure tooltip trigger
            if (_toolReference != null)
            {
                _tooltipTrigger.SetTool(_toolReference);
            }
            else
            {
                _tooltipTrigger.SetContent(_tooltipHeader, _tooltipContent);
            }
        }
        
        /// <summary>
        /// Set tooltip content programmatically
        /// </summary>
        public void SetTooltip(string header, string content)
        {
            _tooltipHeader = header;
            _tooltipContent = content;
            if (_tooltipTrigger != null)
            {
                _tooltipTrigger.SetContent(header, content);
            }
        }
        
        /// <summary>
        /// Set tooltip to use a BaseTool
        /// </summary>
        public void SetTooltipTool(BaseTool tool)
        {
            _toolReference = tool;
            if (_tooltipTrigger != null && tool != null)
            {
                _tooltipTrigger.SetTool(tool);
            }
        }
    }
}
