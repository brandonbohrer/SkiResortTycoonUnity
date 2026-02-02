using UnityEngine;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Placeholder tool for lift building.
    /// This is a minimal implementation to populate the BuildActionBar.
    /// </summary>
    public class LiftBuildTool : BaseTool
    {
        [Header("Lift Settings")]
        [SerializeField] private int _baseCost = 25000;
        
        private Vector3? _startPosition = null;
        
        public override string ToolName => "Lift";
        public override string ToolDescription => "Build a new ski lift";
        
        public override void OnActivate()
        {
            base.OnActivate();
            NotificationManager.Instance?.ShowInfo("Click to set lift bottom station");
            _startPosition = null;
        }
        
        public override void OnDeactivate()
        {
            base.OnDeactivate();
            _startPosition = null;
        }
        
        protected override void HandleInput()
        {
            base.HandleInput();
            
            if (IsMouseOverUI()) return;
            
            Vector3 worldPos = GetMouseWorldPosition();
            
            if (Input.GetMouseButtonDown(0))
            {
                if (_startPosition == null)
                {
                    _startPosition = worldPos;
                    Debug.Log($"[LiftBuildTool] Start position set at {worldPos}");
                    NotificationManager.Instance?.ShowInfo("Click to set lift top station");
                }
                else
                {
                    Debug.Log($"[LiftBuildTool] End position at {worldPos}");
                    
                    float distance = Vector3.Distance(_startPosition.Value, worldPos);
                    int cost = Mathf.RoundToInt(_baseCost * (distance / 100f));
                    
                    ConfirmationDialog.Instance?.ShowBuildConfirmation(
                        "Ski Lift", 
                        cost, 
                        500, // maintenance per day
                        () => {
                            NotificationManager.Instance?.ShowSuccess("Lift built!");
                            _startPosition = null;
                        },
                        () => {
                            NotificationManager.Instance?.ShowInfo("Lift cancelled");
                            _startPosition = null;
                        }
                    );
                }
            }
        }
        
        public override void OnCancel()
        {
            if (_startPosition != null)
            {
                _startPosition = null;
                NotificationManager.Instance?.ShowInfo("Lift placement cancelled");
            }
            else
            {
                base.OnCancel();
            }
        }
    }
}
