using UnityEngine;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Placeholder tool for trail building.
    /// This is a minimal implementation to populate the BuildActionBar.
    /// </summary>
    public class TrailBuildTool : BaseTool
    {
        [Header("Trail Settings")]
        [SerializeField] private int _baseCost = 5000;
        
        public override string ToolName => "Trail";
        public override string ToolDescription => "Build a new ski trail";
        
        public override void OnActivate()
        {
            base.OnActivate();
            NotificationManager.Instance?.ShowInfo("Click and drag to draw trail");
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
                Debug.Log($"[TrailBuildTool] Start trail at {worldPos}");
            }
            
            if (Input.GetMouseButton(0))
            {
                // Continue drawing trail
                // TODO: Connect to actual trail building system
            }
            
            if (Input.GetMouseButtonUp(0))
            {
                Debug.Log($"[TrailBuildTool] End trail at {worldPos}");
                
                // Show confirmation dialog
                ConfirmationDialog.Instance?.ShowBuildConfirmation(
                    "Trail", 
                    _baseCost, 
                    100, // maintenance
                    () => {
                        NotificationManager.Instance?.ShowSuccess("Trail built!");
                    },
                    () => {
                        NotificationManager.Instance?.ShowInfo("Trail cancelled");
                    }
                );
            }
        }
    }
}
