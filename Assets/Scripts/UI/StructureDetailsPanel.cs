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
            // Setup button listeners (RemoveAllListeners first to avoid duplicates
            // since CreateDefaultDetailsPanel may have already added them via reflection)
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(OnCloseClicked);
            }
            
            if (_renameButton != null)
            {
                _renameButton.onClick.RemoveAllListeners();
                _renameButton.onClick.AddListener(OnRenameClicked);
            }
            
            if (_deleteButton != null)
            {
                _deleteButton.onClick.RemoveAllListeners();
                _deleteButton.onClick.AddListener(OnDeleteClicked);
            }
            
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.RemoveAllListeners();
                _upgradeButton.onClick.AddListener(OnUpgradeClicked);
                _upgradeButton.interactable = false; // Not implemented yet
            }
            
            Debug.Log($"[StructureDetailsPanel] Start() - buttons wired: close={_closeButton != null}, delete={_deleteButton != null}, rename={_renameButton != null}");
            
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
            
            // NOTE: Don't call DeselectStructure() here - it creates a mutual recursion loop.
            // The caller (DeselectStructure or Close button) is responsible for deselection.
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
            
            var rowLayoutElem = row.AddComponent<LayoutElement>();
            rowLayoutElem.minHeight = 28;
            rowLayoutElem.preferredHeight = 28;
            
            // Load TMP default font (required for runtime-created text)
            var font = TMPro.TMP_Settings.defaultFontAsset;
            
            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            if (font != null) labelTmp.font = font;
            labelTmp.text = label;
            labelTmp.fontSize = 14;
            labelTmp.color = new Color(0.85f, 0.85f, 0.85f); // Bright enough to read on dark bg
            labelTmp.alignment = TextAlignmentOptions.Left;
            labelTmp.enableWordWrapping = false;
            labelTmp.overflowMode = TextOverflowModes.Ellipsis;
            
            var labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1;
            
            // Value
            var valueObj = new GameObject("Value");
            valueObj.transform.SetParent(row.transform, false);
            var valueTmp = valueObj.AddComponent<TextMeshProUGUI>();
            if (font != null) valueTmp.font = font;
            valueTmp.text = value;
            valueTmp.fontSize = 14;
            valueTmp.color = valueColor ?? Color.white;
            valueTmp.alignment = TextAlignmentOptions.Right;
            valueTmp.fontStyle = FontStyles.Bold;
            valueTmp.enableWordWrapping = false;
            valueTmp.overflowMode = TextOverflowModes.Ellipsis;
            
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
        
        private void OnCloseClicked()
        {
            Hide();
            // Tell the selection manager to deselect (which won't call Hide again due to idempotent guard)
            if (StructureSelectionManager.Instance != null)
            {
                StructureSelectionManager.Instance.DeselectStructure();
            }
        }
        
        private void OnDeleteClicked()
        {
            Debug.Log($"[StructureDetailsPanel] OnDeleteClicked called. _currentStructure={((_currentStructure != null) ? _currentStructure.StructureName : "NULL")}");
            
            if (_currentStructure == null)
            {
                Debug.LogWarning("[StructureDetailsPanel] OnDeleteClicked: _currentStructure is null, aborting");
                return;
            }
            
            // Skip confirmation dialog — just delete directly
            DeleteCurrentStructure();
        }
        
        private void DeleteCurrentStructure()
        {
            Debug.Log($"[StructureDetailsPanel] DeleteCurrentStructure called. _currentStructure={((_currentStructure != null) ? _currentStructure.StructureName : "NULL")}");
            
            if (_currentStructure == null)
            {
                Debug.LogWarning("[StructureDetailsPanel] DeleteCurrentStructure: _currentStructure is null, aborting");
                return;
            }
            
            // Cache ref before deletion clears it
            var structureToDelete = _currentStructure;
            var structureGO = _currentStructure.gameObject;
            
            Debug.Log($"[StructureDetailsPanel] Deleting {structureToDelete.Type}: {structureToDelete.StructureName}");
            
            switch (structureToDelete.Type)
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
            
            // Destroy the GameObject if it still exists (ensures visual removal for all types)
            if (structureGO != null)
            {
                Debug.Log($"[StructureDetailsPanel] Destroying GameObject: {structureGO.name}");
                Destroy(structureGO);
            }
            
            // Close panel and deselect
            Hide();
            if (StructureSelectionManager.Instance != null)
            {
                StructureSelectionManager.Instance.DeselectStructure();
            }
            
            Debug.Log($"[StructureDetailsPanel] Successfully deleted {structureToDelete.Type}: {structureToDelete.StructureName}");
        }
        
        private void DeleteLift()
        {
            var lift = _currentStructure?.LiftData;
            if (lift == null)
            {
                Debug.LogWarning("[StructureDetailsPanel] DeleteLift: LiftData is null");
                return;
            }
            
            Debug.Log($"[StructureDetailsPanel] DeleteLift: Removing lift '{lift.Name}' (ID: {lift.LiftId})");
            
            // Find the LiftBuilder to access LiftSystem
            var liftBuilder = FindObjectOfType<LiftBuilder>();
            if (liftBuilder == null)
            {
                Debug.LogWarning("[StructureDetailsPanel] DeleteLift: No LiftBuilder found in scene");
                return;
            }
            
            if (liftBuilder.LiftSystem == null)
            {
                Debug.LogWarning("[StructureDetailsPanel] DeleteLift: LiftBuilder.LiftSystem is null");
                return;
            }
            
            // Remove from core system
            liftBuilder.LiftSystem.RemoveLift(lift);
            Debug.Log("[StructureDetailsPanel] DeleteLift: Removed from LiftSystem");
            
            // Remove visual
            var prefabBuilder = liftBuilder.PrefabBuilder;
            if (prefabBuilder != null)
            {
                prefabBuilder.DestroyLift(lift.LiftId);
                Debug.Log("[StructureDetailsPanel] DeleteLift: Destroyed visual via PrefabBuilder");
            }
            
            // Rebuild connections
            liftBuilder.Connectivity?.RebuildConnections();
            
            NotificationManager.Instance?.ShowSuccess($"Deleted {lift.Name}");
        }
        
        private void DeleteTrail()
        {
            var trail = _currentStructure?.TrailData;
            if (trail == null)
            {
                Debug.LogWarning("[StructureDetailsPanel] DeleteTrail: TrailData is null");
                return;
            }
            
            Debug.Log($"[StructureDetailsPanel] DeleteTrail: Removing trail '{trail.Name}'");
            
            // Find the TrailDrawer to access TrailSystem
            var trailDrawer = FindObjectOfType<TrailDrawer>();
            if (trailDrawer == null)
            {
                Debug.LogWarning("[StructureDetailsPanel] DeleteTrail: No TrailDrawer found in scene");
                return;
            }
            
            if (trailDrawer.TrailSystem == null)
            {
                Debug.LogWarning("[StructureDetailsPanel] DeleteTrail: TrailDrawer.TrailSystem is null");
                return;
            }
            
            // Remove from core system
            trailDrawer.TrailSystem.RemoveTrail(trail);
            Debug.Log("[StructureDetailsPanel] DeleteTrail: Removed from TrailSystem");
            
            // Rebuild connections
            var liftBuilder = FindObjectOfType<LiftBuilder>();
            liftBuilder?.Connectivity?.RebuildConnections();
            
            NotificationManager.Instance?.ShowSuccess($"Deleted {trail.Name}");
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
