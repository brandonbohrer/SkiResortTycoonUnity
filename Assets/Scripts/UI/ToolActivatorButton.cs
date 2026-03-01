using UnityEngine;
using UnityEngine.UI;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Attach to any dock subrow button to wire it to a BaseTool.
    /// Clicking activates the tool (or cancels it if already active — toggle).
    /// The button image tints orange while the tool is active.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ToolActivatorButton : MonoBehaviour
    {
        private static readonly Color ActiveColor = new Color(0.9647f, 0.5373f, 0.2314f, 1f); // #F6893B

        [Tooltip("The tool to activate when this button is clicked.")]
        [SerializeField] private BaseTool _tool;

        private Button _button;
        private Image _buttonImage;
        private Color _originalColor;
        private TooltipTrigger _tooltipTrigger;

        void Start()
        {
            _button = GetComponent<Button>();
            _buttonImage = _button.targetGraphic as Image;
            if (_buttonImage != null) _originalColor = _buttonImage.color;

            _button.onClick.AddListener(OnClicked);

            var uiManager = UIManager.Instance;
            if (uiManager != null)
                uiManager.OnToolChanged.AddListener(OnToolChanged);
            
            // Setup tooltip
            SetupTooltip();
        }
        
        private void SetupTooltip()
        {
            // Ensure TooltipTrigger exists
            _tooltipTrigger = GetComponent<TooltipTrigger>();
            if (_tooltipTrigger == null)
            {
                _tooltipTrigger = gameObject.AddComponent<TooltipTrigger>();
            }
            
            // Set tooltip to use the tool reference
            if (_tool != null)
            {
                _tooltipTrigger.SetTool(_tool);
            }
        }

        void OnDestroy()
        {
            var uiManager = UIManager.Instance;
            if (uiManager != null)
                uiManager.OnToolChanged.RemoveListener(OnToolChanged);
        }

        private void OnClicked()
        {
            if (_tool == null) return;
            var uiManager = UIManager.Instance;
            if (uiManager == null) return;

            if (uiManager.IsToolActive(_tool))
                uiManager.CancelActiveTool();
            else
                uiManager.ActivateTool(_tool);
        }

        private void OnToolChanged(BaseTool activeTool)
        {
            if (_buttonImage == null) return;
            _buttonImage.color = (activeTool == _tool) ? ActiveColor : _originalColor;
        }
    }
}
