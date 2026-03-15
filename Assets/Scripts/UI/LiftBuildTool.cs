using UnityEngine;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;
using System.Reflection;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Lift building tool — bridges the dock button with LiftBuilder and ContextWindowController.
    ///
    /// Flow:
    ///   Activate  → enable LiftBuilder._isBuildMode
    ///   1st click → OnBottomStationPlaced  → open context window Phase 1
    ///   2nd click → OnLiftReadyToConfirm   → open context window Phase 2 (stats + Confirm/Cancel)
    ///   Confirm   → LiftBuilder.ConfirmLift() → OnLiftPlaced → deactivate, show lift-selected window
    ///   Cancel    → LiftBuilder.CancelPendingLift() → OnLiftCancelled → deactivate, hide window
    /// </summary>
    public class LiftBuildTool : BaseTool
    {
        [Header("Tool References")]
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private LiftType _defaultLiftType = LiftType.OneSeatLowSpeed;
        
        private FieldInfo _isBuildModeField;
        private bool _previousBuildMode = false;
        private bool _hasExplicitTypeSelectionForNextActivate = false;
        
        // ── Activation ───────────────────────────────────────────────────────

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

            // If the tool was activated directly (not via a specific lift-type button),
            // default to 1-seat low speed so the "low-speed" action is deterministic.
            if (!_hasExplicitTypeSelectionForNextActivate)
                _liftBuilder.SetSelectedLiftType(_defaultLiftType);
            _hasExplicitTypeSelectionForNextActivate = false;
            
            // Enable _isBuildMode via reflection (field is private on LiftBuilder)
            if (_isBuildModeField == null)
                _isBuildModeField = typeof(LiftBuilder).GetField("_isBuildMode",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (_isBuildModeField != null)
            {
                _previousBuildMode = (bool)_isBuildModeField.GetValue(_liftBuilder);
                _isBuildModeField.SetValue(_liftBuilder, true);
            }

            // Show cursor
            var cursorField = typeof(LiftBuilder).GetField("_cursorVisual",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cursorField != null)
            {
                var cursor = cursorField.GetValue(_liftBuilder) as GameObject;
                if (cursor != null) cursor.SetActive(true);
            }

            // Subscribe to LiftBuilder events
            _liftBuilder.OnBottomStationPlaced  += HandleBottomStationPlaced;
            _liftBuilder.OnLiftPreviewUpdated   += HandleLiftPreviewUpdated;
            _liftBuilder.OnLiftReadyToConfirm   += HandleLiftReadyToConfirm;
            _liftBuilder.OnLiftPlaced           += HandleLiftPlaced;
            _liftBuilder.OnLiftCancelled        += HandleLiftCancelled;
            
            NotificationManager.Instance?.ShowInfo("Click bottom station, then top station");
        }
        
        public override void OnDeactivate()
        {
            base.OnDeactivate();

            if (_liftBuilder != null)
            {
                _liftBuilder.OnBottomStationPlaced  -= HandleBottomStationPlaced;
                _liftBuilder.OnLiftPreviewUpdated   -= HandleLiftPreviewUpdated;
                _liftBuilder.OnLiftReadyToConfirm   -= HandleLiftReadyToConfirm;
                _liftBuilder.OnLiftPlaced           -= HandleLiftPlaced;
                _liftBuilder.OnLiftCancelled        -= HandleLiftCancelled;

                // Cancel any in-progress (non-confirmed) placement
                if (!_liftBuilder.IsPendingConfirmation)
                {
                    var cancelMethod = typeof(LiftBuilder).GetMethod("CancelPlacement",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    cancelMethod?.Invoke(_liftBuilder, null);
                }

                if (_isBuildModeField != null)
                    _isBuildModeField.SetValue(_liftBuilder, _previousBuildMode);

                // Hide cursor
                var cursorField = typeof(LiftBuilder).GetField("_cursorVisual",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (cursorField != null)
                {
                    var cursor = cursorField.GetValue(_liftBuilder) as GameObject;
                    if (cursor != null && !_previousBuildMode) cursor.SetActive(false);
                }
            }
        }

        public override void OnCancel()
        {
            // If pending confirmation, tell LiftBuilder to cancel properly
            if (_liftBuilder != null && _liftBuilder.IsPendingConfirmation)
            {
                _liftBuilder.CancelPendingLift(); // fires OnLiftCancelled → HandleLiftCancelled
                return; // HandleLiftCancelled will call DeactivateTool
            }

            base.OnCancel();
        }

        // ── Input ────────────────────────────────────────────────────────────

        protected override void HandleInput()
        {
            // While pending confirmation, let LiftBuilder (and the context window
            // buttons) own all input — don't intercept ESC / right-click here.
            if (_liftBuilder != null && _liftBuilder.IsPendingConfirmation) return;

            base.HandleInput(); // right-click cancel

            if (Input.GetKeyDown(KeyCode.Escape))
                UIManager.Instance?.DeactivateTool();
        }

        // ── LiftBuilder event handlers ───────────────────────────────────────

        private void HandleBottomStationPlaced()
        {
            ContextWindowController.Instance?.ShowLiftBuildPhase1();
        }

        private void HandleLiftPreviewUpdated(float lengthM, float elevationM, int baseCost, int addedCost)
        {
            ContextWindowController.Instance?.UpdateLiftBuildStats(lengthM, elevationM, baseCost, addedCost);
        }

        private void HandleLiftReadyToConfirm(LiftData liftData, int baseCost, int lengthAddedCost)
        {
            ContextWindowController.Instance?.ShowLiftBuildPhase2(
                liftData,
                baseCost,
                lengthAddedCost,
                onConfirm: () => _liftBuilder?.ConfirmLift(),
                onCancel:  () => _liftBuilder?.CancelPendingLift()
            );
        }

        private void HandleLiftPlaced(SelectableStructure selectable)
        {
            // Unsubscribe first to avoid double calls during DeactivateTool
            _liftBuilder.OnLiftPlaced -= HandleLiftPlaced;

            UIManager.Instance?.DeactivateTool();

            // Transition context window from "building" to "lift selected"
            if (selectable != null)
                ContextWindowController.Instance?.ShowStructure(selectable);
            else
                ContextWindowController.Instance?.Hide();
        }

        private void HandleLiftCancelled()
        {
            _liftBuilder.OnLiftCancelled -= HandleLiftCancelled;

            UIManager.Instance?.DeactivateTool();
            ContextWindowController.Instance?.Hide();
        }

        // ── Lift type selection API (called by UI buttons) ───────────────────

        public void SetLiftType(LiftType liftType)
        {
            if (_liftBuilder == null)
                _liftBuilder = FindObjectOfType<LiftBuilder>();

            if (_liftBuilder == null)
            {
                NotificationManager.Instance?.ShowError("Lift system not available");
                return;
            }

            _hasExplicitTypeSelectionForNextActivate = true;
            _liftBuilder.SetSelectedLiftType(liftType);
        }
    }
}
