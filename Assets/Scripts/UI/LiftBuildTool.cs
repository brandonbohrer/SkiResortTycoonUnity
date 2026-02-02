using UnityEngine;
using SkiResortTycoon.UnityBridge;
using System.Reflection;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Lift building tool - integrates UI button with existing LiftBuilder system.
    /// Activating this tool enters lift build mode (same as pressing L).
    /// </summary>
    public class LiftBuildTool : BaseTool
    {
        [Header("Tool References")]
        [SerializeField] private LiftBuilder _liftBuilder;
        
        [Header("Lift Settings")]
        [SerializeField] private int _baseCost = 25000;
        
        private FieldInfo _isBuildModeField;
        private bool _previousBuildMode = false;
        
        public override string ToolName => "Lift";
        public override string ToolDescription => "Build a new ski lift";
        
        public override void OnActivate()
        {
            base.OnActivate();
            
            if (_liftBuilder == null)
            {
                _liftBuilder = FindObjectOfType<LiftBuilder>();
                if (_liftBuilder == null)
                {
                    Debug.LogError("[LiftBuildTool] LiftBuilder not found in scene!");
                    NotificationManager.Instance?.ShowError("Lift system not available");
                    return;
                }
            }
            
            // Get the private _isBuildMode field using reflection
            if (_isBuildModeField == null)
            {
                _isBuildModeField = typeof(LiftBuilder).GetField("_isBuildMode", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
            
            // Remember previous state
            if (_isBuildModeField != null)
            {
                _previousBuildMode = (bool)_isBuildModeField.GetValue(_liftBuilder);
                
                // Enable build mode
                _isBuildModeField.SetValue(_liftBuilder, true);
            }
            
            NotificationManager.Instance?.ShowInfo("Click bottom station, then top station");
            
            // Show cursor if available
            var cursorVisualField = typeof(LiftBuilder).GetField("_cursorVisual", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cursorVisualField != null)
            {
                var cursorVisual = cursorVisualField.GetValue(_liftBuilder) as GameObject;
                if (cursorVisual != null)
                {
                    cursorVisual.SetActive(true);
                }
            }
        }
        
        public override void OnDeactivate()
        {
            base.OnDeactivate();
            
            if (_liftBuilder == null || _isBuildModeField == null) return;
            
            // Cancel any in-progress placement
            var cancelMethod = typeof(LiftBuilder).GetMethod("CancelPlacement", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cancelMethod != null)
            {
                cancelMethod.Invoke(_liftBuilder, null);
            }
            
            // Restore previous build mode state (or turn off)
            _isBuildModeField.SetValue(_liftBuilder, _previousBuildMode);
            
            // Hide cursor
            var cursorVisualField = typeof(LiftBuilder).GetField("_cursorVisual", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cursorVisualField != null)
            {
                var cursorVisual = cursorVisualField.GetValue(_liftBuilder) as GameObject;
                if (cursorVisual != null && !_previousBuildMode)
                {
                    cursorVisual.SetActive(false);
                }
            }
        }
        
        protected override void HandleInput()
        {
            // The LiftBuilder handles its own input in its Update method
            // We just need to keep build mode enabled while this tool is active
            base.HandleInput();
            
            // If escape is pressed, cancel and deactivate
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                UIManager.Instance?.DeactivateTool();
            }
        }
        
        public override void OnCancel()
        {
            if (_liftBuilder != null)
            {
                var cancelMethod = typeof(LiftBuilder).GetMethod("CancelPlacement", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (cancelMethod != null)
                {
                    cancelMethod.Invoke(_liftBuilder, null);
                    NotificationManager.Instance?.ShowInfo("Lift placement cancelled");
                }
            }
            
            base.OnCancel();
        }
    }
}
