using UnityEngine;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Placeholder tool for building facilities (lodges, restaurants, etc).
    /// </summary>
    public class FacilityBuildTool : BaseTool
    {
        [Header("Facility Settings")]
        [SerializeField] private string _facilityName = "Lodge";
        [SerializeField] private int _baseCost = 15000;
        
        public override string ToolName => _facilityName;
        public override string ToolDescription => $"Build a {_facilityName}";
        
        public override void OnActivate()
        {
            base.OnActivate();
            NotificationManager.Instance?.ShowInfo($"Click to place {_facilityName}");
        }
        
        public override void OnDeactivate()
        {
            base.OnDeactivate();
        }
        
        protected override void HandleInput()
        {
            base.HandleInput();
            
            if (IsMouseOverUI()) return;
            
            Vector3 worldPos = GetMouseWorldPosition();
            
            if (Input.GetMouseButtonDown(0))
            {
                ConfirmationDialog.Instance?.ShowBuildConfirmation(
                    _facilityName, 
                    _baseCost, 
                    200, // maintenance
                    () => {
                        NotificationManager.Instance?.ShowSuccess($"{_facilityName} built!");
                    },
                    () => {
                        NotificationManager.Instance?.ShowInfo("Cancelled");
                    }
                );
            }
        }
    }
}
