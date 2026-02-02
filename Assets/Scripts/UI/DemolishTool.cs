using UnityEngine;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Placeholder tool for demolishing objects.
    /// </summary>
    public class DemolishTool : BaseTool
    {
        public override string ToolName => "Demolish";
        public override string ToolDescription => "Remove buildings and infrastructure";
        
        public override void OnActivate()
        {
            base.OnActivate();
            NotificationManager.Instance?.ShowWarning("Click an object to demolish");
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
                // In a real implementation, this would raycast to find objects
                ConfirmationDialog.Instance?.Show(
                    "Confirm Demolition", 
                    "Are you sure you want to demolish this?",
                    () => {
                        NotificationManager.Instance?.ShowInfo("Object demolished");
                    },
                    () => {
                        NotificationManager.Instance?.ShowInfo("Cancelled");
                    }
                );
            }
        }
    }
}
