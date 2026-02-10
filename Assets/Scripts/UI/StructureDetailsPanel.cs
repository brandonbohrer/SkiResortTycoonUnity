using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;
using System.Collections.Generic;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// UI Panel that displays details about a selected structure.
    /// Shows different information based on structure type (lift, trail, lodge).
    /// </summary>
    public class StructureDetailsPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private CanvasGroup _canvasGroup;
        
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _typeText;
        [SerializeField] private Image _typeIcon;
        [SerializeField] private Button _closeButton;
        
        [Header("Stats Section")]
        [SerializeField] private Transform _statsContainer;
        [SerializeField] private GameObject _statRowPrefab;
        
        [Header("Actions Section")]
        [SerializeField] private Button _renameButton;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private Button _upgradeButton; // Future feature
        
        [Header("Type Icons")]
        [SerializeField] private Sprite _liftIcon;
        [SerializeField] private Sprite _trailIcon;
        [SerializeField] private Sprite _lodgeIcon;
        
        [Header("Trail Difficulty Colors")]
        [SerializeField] private Color _greenColor = new Color(0.1f, 0.8f, 0.1f);
        [SerializeField] private Color _blueColor = new Color(0.1f, 0.4f, 0.9f);
        [SerializeField] private Color _blackColor = Color.black;
        [SerializeField] private Color _doubleBlackColor = new Color(0.5f, 0f, 0f);
        
        [Header("Animation")]
        [SerializeField] private float _fadeSpeed = 5f;
        
        // State
        private SelectableStructure _currentStructure;
        private bool _isVisible;
        private float _targetAlpha;
        
        void Start()
        {
            // Setup button listeners
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }
            
            if (_renameButton != null)
            {
                _renameButton.onClick.AddListener(OnRenameClicked);
            }
            
            if (_deleteButton != null)
            {
                _deleteButton.onClick.AddListener(OnDeleteClicked);
            }
            
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.AddListener(OnUpgradeClicked);
                _upgradeButton.interactable = false; // Not implemented yet
            }
            
            // Start hidden
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }
            
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
            
            // Subscribe to tool changes to hide panel when a tool is activated
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
        
        private void OnToolChanged(BaseTool tool)
        {
            // Hide panel when any tool is activated
            if (tool != null && _isVisible)
            {
                Hide();
            }
        }
        
        void Update()
        {
            // Smooth fade animation
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, Time.unscaledDeltaTime * _fadeSpeed);
                
                if (_canvasGroup.alpha <= 0.01f && !_isVisible)
                {
                    if (_panelRoot != null)
                    {
                        _panelRoot.SetActive(false);
                    }
                }
            }
        }
        
        /// <summary>
        /// Show the details panel for a structure.
        /// </summary>
        public void ShowStructure(SelectableStructure structure)
        {
            if (structure == null) return;
            
            _currentStructure = structure;
            _isVisible = true;
            _targetAlpha = 1f;
            
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
            }
            
            // Update header
            UpdateHeader();
            
            // Update stats based on type
            ClearStats();
            
            switch (structure.Type)
            {
                case StructureType.Lift:
                    PopulateLiftStats();
                    break;
                case StructureType.Trail:
                    PopulateTrailStats();
                    break;
                case StructureType.Lodge:
                    PopulateLodgeStats();
                    break;
            }
        }
        
        /// <summary>
        /// Hide the details panel.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _targetAlpha = 0f;
            _currentStructure = null;
            
            // Deselect structure
            if (StructureSelectionManager.Instance != null)
            {
                StructureSelectionManager.Instance.DeselectStructure();
            }
        }
        
        private void UpdateHeader()
        {
            if (_currentStructure == null) return;
            
            // Title
            if (_titleText != null)
            {
                _titleText.text = _currentStructure.StructureName;
            }
            
            // Type text and icon
            string typeString = "";
            Sprite icon = null;
            
            switch (_currentStructure.Type)
            {
                case StructureType.Lift:
                    typeString = "Chairlift";
                    icon = _liftIcon;
                    break;
                case StructureType.Trail:
                    typeString = GetTrailDifficultyString();
                    icon = _trailIcon;
                    break;
                case StructureType.Lodge:
                    typeString = "Lodge";
                    icon = _lodgeIcon;
                    break;
            }
            
            if (_typeText != null)
            {
                _typeText.text = typeString;
                
                // Color for trails
                if (_currentStructure.Type == StructureType.Trail && _currentStructure.TrailData != null)
                {
                    _typeText.color = GetDifficultyColor(_currentStructure.TrailData.Difficulty);
                }
                else
                {
                    _typeText.color = Color.white;
                }
            }
            
            if (_typeIcon != null && icon != null)
            {
                _typeIcon.sprite = icon;
                _typeIcon.gameObject.SetActive(true);
            }
            else if (_typeIcon != null)
            {
                _typeIcon.gameObject.SetActive(false);
            }
        }
        
        private string GetTrailDifficultyString()
        {
            if (_currentStructure?.TrailData == null) return "Trail";
            
            switch (_currentStructure.TrailData.Difficulty)
            {
                case TrailDifficulty.Green: return "Green Circle";
                case TrailDifficulty.Blue: return "Blue Square";
                case TrailDifficulty.Black: return "Black Diamond";
                case TrailDifficulty.DoubleBlack: return "Double Black";
                default: return "Trail";
            }
        }
        
        private Color GetDifficultyColor(TrailDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TrailDifficulty.Green: return _greenColor;
                case TrailDifficulty.Blue: return _blueColor;
                case TrailDifficulty.Black: return _blackColor;
                case TrailDifficulty.DoubleBlack: return _doubleBlackColor;
                default: return Color.white;
            }
        }
        
        private void ClearStats()
        {
            if (_statsContainer == null) return;
            
            foreach (Transform child in _statsContainer)
            {
                Destroy(child.gameObject);
            }
        }
        
        private void AddStatRow(string label, string value, Color? valueColor = null)
        {
            if (_statsContainer == null) return;
            
            GameObject row;
            
            if (_statRowPrefab != null)
            {
                row = Instantiate(_statRowPrefab, _statsContainer);
            }
            else
            {
                // Create row programmatically as fallback
                row = CreateStatRowProgrammatically(label, value, valueColor);
                return;
            }
            
            var labelText = row.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            var valueText = row.transform.Find("Value")?.GetComponent<TextMeshProUGUI>();
            
            if (labelText != null)
            {
                labelText.text = label;
            }
            
            if (valueText != null)
            {
                valueText.text = value;
                if (valueColor.HasValue)
                {
                    valueText.color = valueColor.Value;
                }
            }
        }
        
        private GameObject CreateStatRowProgrammatically(string label, string value, Color? valueColor)
        {
            // Create a simple horizontal layout for the stat row
            var row = new GameObject($"StatRow_{label}");
            row.transform.SetParent(_statsContainer, false);
            
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 24);
            
            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 14;
            labelTmp.color = new Color(0.7f, 0.7f, 0.7f);
            labelTmp.alignment = TextAlignmentOptions.Left;
            
            var labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1;
            
            // Value
            var valueObj = new GameObject("Value");
            valueObj.transform.SetParent(row.transform, false);
            var valueTmp = valueObj.AddComponent<TextMeshProUGUI>();
            valueTmp.text = value;
            valueTmp.fontSize = 14;
            valueTmp.color = valueColor ?? Color.white;
            valueTmp.alignment = TextAlignmentOptions.Right;
            valueTmp.fontStyle = FontStyles.Bold;
            
            var valueLayout = valueObj.AddComponent<LayoutElement>();
            valueLayout.flexibleWidth = 1;
            
            return row;
        }
        
        private void PopulateLiftStats()
        {
            var lift = _currentStructure?.LiftData;
            if (lift == null) return;
            
            AddStatRow("Length", $"{lift.Length:F0}m");
            AddStatRow("Elevation Gain", $"{lift.ElevationGain:F0}m");
            AddStatRow("Capacity", $"{lift.Capacity}/hr");
            AddStatRow("Build Cost", $"${lift.BuildCost:N0}");
            
            // TODO: Add real-time stats when tracking is implemented
            AddStatRow("Utilization", "—"); // Placeholder
            AddStatRow("Wait Time", "—"); // Placeholder
        }
        
        private void PopulateTrailStats()
        {
            var trail = _currentStructure?.TrailData;
            if (trail == null) return;
            
            AddStatRow("Difficulty", GetTrailDifficultyString(), GetDifficultyColor(trail.Difficulty));
            AddStatRow("Length", $"{CalculateTrailLength(trail):F0}m");
            AddStatRow("Elevation Drop", $"{trail.TotalElevationDrop:F0}m");
            AddStatRow("Average Grade", $"{trail.AverageSlope * 100:F0}%");
            AddStatRow("Max Grade", $"{trail.MaxSlope * 100:F0}%");
            
            // TODO: Add real-time stats when tracking is implemented
            AddStatRow("Runs Today", "—"); // Placeholder
            AddStatRow("Popularity", "—"); // Placeholder
        }
        
        private float CalculateTrailLength(TrailData trail)
        {
            if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                return 0f;
            
            float length = 0f;
            for (int i = 0; i < trail.WorldPathPoints.Count - 1; i++)
            {
                var p1 = trail.WorldPathPoints[i];
                var p2 = trail.WorldPathPoints[i + 1];
                length += Vector3f.Distance(p1, p2);
            }
            return length;
        }
        
        private void PopulateLodgeStats()
        {
            var lodge = _currentStructure?.Lodge;
            if (lodge == null) return;
            
            AddStatRow("Capacity", $"{lodge.Capacity} skiers");
            AddStatRow("Current Occupancy", $"{lodge.CurrentOccupancy}/{lodge.Capacity}");
            
            // Status color
            Color statusColor = lodge.IsFull ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.3f);
            AddStatRow("Status", lodge.IsFull ? "Full" : "Open", statusColor);
            
            // TODO: Add more lodge stats when tracking is implemented
            AddStatRow("Visitors Today", "—"); // Placeholder
        }
        
        private void OnRenameClicked()
        {
            // TODO: Implement rename dialog
            NotificationManager.Instance?.ShowInfo("Rename feature coming soon!");
        }
        
        private void OnDeleteClicked()
        {
            if (_currentStructure == null) return;
            
            string structureName = _currentStructure.StructureName;
            
            ConfirmationDialog.Instance?.Show(
                "Confirm Deletion",
                $"Are you sure you want to delete {structureName}?",
                () => {
                    DeleteCurrentStructure();
                },
                () => {
                    // Cancelled
                }
            );
        }
        
        private void DeleteCurrentStructure()
        {
            if (_currentStructure == null) return;
            
            switch (_currentStructure.Type)
            {
                case StructureType.Lift:
                    DeleteLift();
                    break;
                case StructureType.Trail:
                    DeleteTrail();
                    break;
                case StructureType.Lodge:
                    DeleteLodge();
                    break;
            }
            
            Hide();
        }
        
        private void DeleteLift()
        {
            var lift = _currentStructure?.LiftData;
            if (lift == null) return;
            
            // Find the LiftBuilder to access LiftSystem
            var liftBuilder = FindObjectOfType<LiftBuilder>();
            if (liftBuilder?.LiftSystem != null)
            {
                // Remove from core system
                liftBuilder.LiftSystem.RemoveLift(lift);
                
                // Remove visual
                var prefabBuilder = liftBuilder.PrefabBuilder;
                if (prefabBuilder != null)
                {
                    prefabBuilder.DestroyLift(lift.LiftId);
                }
                
                // Rebuild connections
                liftBuilder.Connectivity?.RebuildConnections();
                
                NotificationManager.Instance?.ShowSuccess($"Deleted {lift.Name}");
            }
        }
        
        private void DeleteTrail()
        {
            var trail = _currentStructure?.TrailData;
            if (trail == null) return;
            
            // Find the TrailDrawer to access TrailSystem
            var trailDrawer = FindObjectOfType<TrailDrawer>();
            if (trailDrawer?.TrailSystem != null)
            {
                // Remove from core system
                trailDrawer.TrailSystem.RemoveTrail(trail);
                
                // Visual will be cleaned up by TrailVisualizer in its update loop
                
                // Rebuild connections
                var liftBuilder = FindObjectOfType<LiftBuilder>();
                liftBuilder?.Connectivity?.RebuildConnections();
                
                NotificationManager.Instance?.ShowSuccess($"Deleted {trail.Name}");
            }
        }
        
        private void DeleteLodge()
        {
            var lodge = _currentStructure?.Lodge;
            if (lodge == null) return;
            
            string lodgeName = lodge.gameObject.name;
            
            // The LodgeFacility.OnDestroy handles cleanup
            Destroy(lodge.gameObject);
            
            NotificationManager.Instance?.ShowSuccess($"Deleted {lodgeName}");
        }
        
        private void OnUpgradeClicked()
        {
            NotificationManager.Instance?.ShowInfo("Upgrades coming in a future update!");
        }
    }
}
