using UnityEngine;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Trail building tool - integrates UI button with existing TrailDrawer system.
    /// Simulates holding the T key when active.
    /// </summary>
    public class TrailBuildTool : BaseTool
    {
        [Header("Tool References")]
        [SerializeField] private TrailDrawer _trailDrawer;
        
        [Header("Trail Settings")]
        [SerializeField] private int _baseCost = 5000;
        
        private bool _wasDrawing = false;
        
        public override string ToolName => "Trail";
        public override string ToolDescription => "Build a new ski trail";
        
        public override void OnActivate()
        {
            base.OnActivate();
            
            if (_trailDrawer == null)
            {
                _trailDrawer = FindObjectOfType<TrailDrawer>();
                if (_trailDrawer == null)
                {
                    Debug.LogError("[TrailBuildTool] TrailDrawer not found in scene!");
                    NotificationManager.Instance?.ShowError("Trail system not available");
                    return;
                }
            }
            
            NotificationManager.Instance?.ShowInfo("Click and drag to draw trail");
        }
        
        public override void OnDeactivate()
        {
            base.OnDeactivate();
            _wasDrawing = false;
        }
        
        protected override void HandleInput()
        {
            base.HandleInput();
            
            if (_trailDrawer == null || IsMouseOverUI()) return;
            
            // Simulate holding T key behavior from TrailDrawer
            // Start drawing on mouse down
            if (Input.GetMouseButtonDown(0))
            {
                // Simulate T key down
                var startMethod = typeof(TrailDrawer).GetMethod("StartDrawing", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (startMethod != null)
                {
                    Vector3? position = GetMountainPositionUnderMouse();
                    if (position.HasValue)
                    {
                        startMethod.Invoke(_trailDrawer, new object[] { position.Value });
                        _wasDrawing = true;
                    }
                }
            }
            
            // Continue drawing while holding mouse button
            if (Input.GetMouseButton(0) && _wasDrawing)
            {
                var continueMethod = typeof(TrailDrawer).GetMethod("ContinueDrawing", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (continueMethod != null)
                {
                    Vector3? position = GetMountainPositionUnderMouse();
                    if (position.HasValue)
                    {
                        continueMethod.Invoke(_trailDrawer, new object[] { position.Value });
                    }
                }
            }
            
            // Finish drawing on mouse up
            if (Input.GetMouseButtonUp(0) && _wasDrawing)
            {
                var finishMethod = typeof(TrailDrawer).GetMethod("FinishDrawing", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (finishMethod != null)
                {
                    finishMethod.Invoke(_trailDrawer, null);
                    _wasDrawing = false;
                    
                    // Show confirmation if trail was valid
                    // (The TrailDrawer already handles validation and logging)
                    NotificationManager.Instance?.ShowSuccess("Trail created!");
                }
            }
        }
        
        private Vector3? GetMountainPositionUnderMouse()
        {
            // Use the same raycast method as TrailDrawer
            var method = typeof(TrailDrawer).GetMethod("GetMountainPositionUnderMouse", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var result = method.Invoke(_trailDrawer, null);
                return result as Vector3?;
            }
            
            return base.GetMouseWorldPosition();
        }
    }
}
