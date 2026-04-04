using UnityEngine;
using UnityEngine.UI;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Manages the tab bar in the Manager screen.
    /// Tab visuals use the same rule as <see cref="DockController"/> / dock subrows: tint <see cref="Image.color"/>,
    /// then <see cref="ButtonHoverFeedback.Apply"/> — no separate ColorBlock hacks (those prefabs keep normalColor white).
    /// </summary>
    public class ManagerTabController : MonoBehaviour
    {
        private static readonly Color SelectedColor = new Color(0.9647f, 0.5373f, 0.2314f, 1f); // #F6893B

        [System.Serializable]
        public class Tab
        {
            public Button button;
            public GameObject panel;
        }

        [SerializeField] private Button _closeButton;
        [SerializeField] private Tab[] _tabs; // Order: Overview, Finance, Pricing, Guests, Research

        private Color[] _originalColors;
        private int _activeTab = -1;

        void Start()
        {
            // Wire close button
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(() => UIManager.Instance?.CloseManager());
                SetupTooltip(_closeButton, TooltipTexts.ManagerTabs.CloseHeader, TooltipTexts.ManagerTabs.CloseContent);
            }

            // Cache original button colors
            _originalColors = new Color[_tabs.Length];
            for (int i = 0; i < _tabs.Length; i++)
            {
                var img = _tabs[i].button?.targetGraphic as Image;
                _originalColors[i] = img != null ? img.color : Color.white;
            }

            // Wire click listeners and add tooltips
            string[] tabHeaders = { TooltipTexts.ManagerTabs.OverviewHeader, TooltipTexts.ManagerTabs.FinancesHeader, TooltipTexts.ManagerTabs.PricingHeader, TooltipTexts.ManagerTabs.GuestsHeader, TooltipTexts.ManagerTabs.ResearchHeader };
            string[] tabContents = { TooltipTexts.ManagerTabs.OverviewContent, TooltipTexts.ManagerTabs.FinancesContent, TooltipTexts.ManagerTabs.PricingContent, TooltipTexts.ManagerTabs.GuestsContent, TooltipTexts.ManagerTabs.ResearchContent };
            
            for (int i = 0; i < _tabs.Length; i++)
            {
                int idx = i;
                if (_tabs[i].button != null)
                {
                    _tabs[i].button.onClick.AddListener(() => SelectTab(idx));
                    // Add tooltip
                    string tabHeader = i < tabHeaders.Length ? tabHeaders[i] : "Tab";
                    string tabContent = i < tabContents.Length ? tabContents[i] : "View tab information.";
                    SetupTooltip(_tabs[i].button, tabHeader, tabContent);
                }
            }

            // Auto-select Overview (index 0) on start
            SelectTab(0);
        }

        /// <summary>
        /// Selects the tab at the given index, showing its panel and highlighting its button.
        /// Called automatically with index 0 when the manager screen opens.
        /// </summary>
        public void SelectTab(int index)
        {
            _activeTab = index;

            for (int i = 0; i < _tabs.Length; i++)
            {
                bool active = (i == index);

                if (_tabs[i].panel != null)
                    _tabs[i].panel.SetActive(active);

                SetButtonColor(_tabs[i].button, active, _originalColors[i]);
            }

            // Clear EventSystem selection so the clicked tab button doesn't stay
            // in the highlighted/selected visual state until something else is clicked.
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null)
                es.SetSelectedGameObject(null);
        }

        private static void SetButtonColor(Button button, bool active, Color originalColor)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = active ? SelectedColor : originalColor;
            ButtonHoverFeedback.Apply(button, UIManager.Instance?.Theme);
        }

        public int ActiveTab => _activeTab;
        
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
    }
}
