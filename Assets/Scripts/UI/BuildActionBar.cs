using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Tab-based action bar for build tools and actions.
    /// Located at bottom-right of the screen.
    /// </summary>
    public class BuildActionBar : MonoBehaviour
    {
        [Header("Tab System")]
        [SerializeField] private List<BuildTab> _tabs;
        [SerializeField] private Transform _tabButtonContainer;
        [SerializeField] private Transform _toolButtonContainer;
        
        [Header("Tab Button Prefab")]
        [SerializeField] private GameObject _tabButtonPrefab;
        
        [Header("Tool Button Prefab")]
        [SerializeField] private GameObject _toolButtonPrefab;
        
        [Header("Visual Settings")]
        [SerializeField] private Color _tabActiveColor = new Color(0f, 0.737f, 0.831f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _toolActiveColor = new Color(0f, 0.737f, 0.831f, 1f);
        [SerializeField] private Color _toolNormalColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        
        private int _activeTabIndex = -1;
        private List<Button> _tabButtons = new List<Button>();
        private List<Button> _toolButtons = new List<Button>();
        
        /// <summary>
        /// Currently active tab index
        /// </summary>
        public int ActiveTabIndex => _activeTabIndex;
        
        void Start()
        {
            // Create tab buttons
            CreateTabButtons();
            
            // Select first tab
            if (_tabs.Count > 0)
            {
                SelectTab(0);
            }
            
            // Subscribe to tool change events
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnToolChanged.AddListener(OnToolChanged);
            }
        }
        
        void OnDestroy()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnToolChanged.RemoveListener(OnToolChanged);
            }
        }
        
        /// <summary>
        /// Selects a tab by index
        /// </summary>
        public void SelectTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;
            if (index == _activeTabIndex) return;
            
            _activeTabIndex = index;
            
            // Update tab button visuals
            UpdateTabButtonVisuals();
            
            // Rebuild tool buttons for this tab
            RebuildToolButtons();
        }
        
        private void CreateTabButtons()
        {
            if (_tabButtonContainer == null || _tabButtonPrefab == null) return;
            
            // Clear existing buttons
            foreach (Transform child in _tabButtonContainer)
            {
                Destroy(child.gameObject);
            }
            _tabButtons.Clear();
            
            // Create button for each tab
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                var buttonObj = Instantiate(_tabButtonPrefab, _tabButtonContainer);
                var button = buttonObj.GetComponent<Button>();
                
                // Set up button text/icon
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = tab.TabName;
                }
                
                var icon = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null && tab.TabIcon != null)
                {
                    icon.sprite = tab.TabIcon;
                }
                
                // Add click listener
                int tabIndex = i; // Capture for closure
                button.onClick.AddListener(() => SelectTab(tabIndex));
                
                _tabButtons.Add(button);
            }
        }
        
        private void UpdateTabButtonVisuals()
        {
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var colors = _tabButtons[i].colors;
                colors.normalColor = i == _activeTabIndex ? _tabActiveColor : _tabInactiveColor;
                _tabButtons[i].colors = colors;
            }
        }
        
        private void RebuildToolButtons()
        {
            if (_toolButtonContainer == null || _toolButtonPrefab == null) return;
            
            // Clear existing buttons
            foreach (Transform child in _toolButtonContainer)
            {
                Destroy(child.gameObject);
            }
            _toolButtons.Clear();
            
            // Get tools for active tab
            if (_activeTabIndex < 0 || _activeTabIndex >= _tabs.Count) return;
            
            var tab = _tabs[_activeTabIndex];
            if (tab.Tools == null) return;
            
            // Create button for each tool
            foreach (var tool in tab.Tools)
            {
                if (tool == null) continue;
                
                var buttonObj = Instantiate(_toolButtonPrefab, _toolButtonContainer);
                var button = buttonObj.GetComponent<Button>();
                
                // Set up button text/icon
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = tool.ToolName;
                }
                
                var icon = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null && tool.ToolIcon != null)
                {
                    icon.sprite = tool.ToolIcon;
                }
                
                // Add click listener
                var capturedTool = tool; // Capture for closure
                button.onClick.AddListener(() => OnToolButtonClicked(capturedTool));
                
                _toolButtons.Add(button);
            }
            
            // Update visuals for current active tool
            UpdateToolButtonVisuals();
        }
        
        private void OnToolButtonClicked(BaseTool tool)
        {
            if (UIManager.Instance == null) return;
            
            // Toggle tool if already active
            if (UIManager.Instance.IsToolActive(tool))
            {
                UIManager.Instance.DeactivateTool();
            }
            else
            {
                UIManager.Instance.ActivateTool(tool);
            }
        }
        
        private void OnToolChanged(BaseTool tool)
        {
            UpdateToolButtonVisuals();
        }
        
        private void UpdateToolButtonVisuals()
        {
            if (_activeTabIndex < 0 || _activeTabIndex >= _tabs.Count) return;
            
            var tab = _tabs[_activeTabIndex];
            if (tab.Tools == null) return;
            
            for (int i = 0; i < _toolButtons.Count && i < tab.Tools.Count; i++)
            {
                var tool = tab.Tools[i];
                bool isActive = UIManager.Instance?.IsToolActive(tool) ?? false;
                
                var colors = _toolButtons[i].colors;
                colors.normalColor = isActive ? _toolActiveColor : _toolNormalColor;
                _toolButtons[i].colors = colors;
            }
        }
    }
    
    /// <summary>
    /// Represents a tab in the build action bar
    /// </summary>
    [System.Serializable]
    public class BuildTab
    {
        public string TabName = "Tab";
        public Sprite TabIcon;
        public List<BaseTool> Tools;
    }
}
