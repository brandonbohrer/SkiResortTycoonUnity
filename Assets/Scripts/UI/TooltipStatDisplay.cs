using UnityEngine;
using TMPro;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Component for stat displays that shows a tooltip explaining what the stat means.
    /// Attach to TextMeshProUGUI elements that display stats.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TooltipStatDisplay : MonoBehaviour
    {
        [Header("Tooltip")]
        [SerializeField] private string _statName;
        [TextArea(2, 5)]
        [SerializeField] private string _statDescription;
        
        private TooltipTrigger _tooltipTrigger;
        
        void Awake()
        {
            // Ensure TooltipTrigger exists
            _tooltipTrigger = GetComponent<TooltipTrigger>();
            if (_tooltipTrigger == null)
            {
                _tooltipTrigger = gameObject.AddComponent<TooltipTrigger>();
            }
            
            // Set tooltip content
            _tooltipTrigger.SetContent(_statName, _statDescription);
        }
        
        /// <summary>
        /// Set tooltip content programmatically
        /// </summary>
        public void SetTooltip(string statName, string description)
        {
            _statName = statName;
            _statDescription = description;
            if (_tooltipTrigger != null)
            {
                _tooltipTrigger.SetContent(statName, description);
            }
        }
    }
}
