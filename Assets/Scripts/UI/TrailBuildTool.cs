using UnityEngine;
using UnityEngine.UI;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Trail building tool — bridges dock buttons/slider with TrailDrawer and
    /// ContextWindowController.  Handles per-mode input, UI cursor circle, and
    /// the Cities-Skylines-style right-click undo chain.
    ///
    /// Flow:
    ///   Activate     → TrailDrawer.StartBuilding(), show cursor
    ///   First click  → PlaceAnchor → open context window
    ///   Subsequent   → mode-specific input (paint/line/pen)
    ///   Right-click  → UndoOrSettle chain
    ///   Confirm      → TrailDrawer.ConfirmTrail()
    ///   Cancel       → TrailDrawer.CancelBuilding()
    /// </summary>
    public class TrailBuildTool : BaseTool
    {
        [Header("Tool References")]
        [SerializeField] private TrailDrawer _trailDrawer;

        [Header("Cursor Circle")]
        [SerializeField] private RectTransform _cursorCircle;
        [SerializeField] private Image _cursorCircleImage;
        [SerializeField] private Image _cursorCircleBorder;

        [Header("Cursor Colors")]
        [SerializeField] private Color _cursorFillColor = new Color(0.29f, 0.56f, 0.85f, 0.15f);
        [SerializeField] private Color _cursorBorderColor = new Color(0.29f, 0.56f, 0.85f, 1f);
        [SerializeField] private Color _snapBorderColor = new Color(0.2f, 0.8f, 0.2f, 1f);

        [Header("Invalid Start Cursor")]
        [SerializeField] private Color _invalidStartFill = new Color(0.85f, 0.2f, 0.2f, 0.15f);
        [SerializeField] private Color _invalidStartBorder = new Color(0.85f, 0.2f, 0.2f, 1f);

        private Camera _cam;
        private bool _isPainting;
        private bool _penClickedThisFrame;
        private Vector3? _lastSnappedWorldPos;
        private Vector3? _lastValidWorldPos;

        // Pen-mode click-vs-drag tracking
        private bool _penMouseDownOnAnchor;
        private int _penMouseDownAnchorIndex = -1;
        private Vector3 _penMouseDownPos;
        private bool _penDragStarted;
        private const float PenDragThreshold = 0.5f;

        public override string ToolName => "Trail";
        public override string ToolDescription => "Build a new ski trail";

        // ── Activation ───────────────────────────────────────────────────

        public override void OnActivate()
        {
            base.OnActivate();

            if (_trailDrawer == null)
            {
                _trailDrawer = FindObjectOfType<TrailDrawer>();
                if (_trailDrawer == null)
                {
                    Debug.LogError("[TrailBuildTool] TrailDrawer not found!");
                    NotificationManager.Instance?.ShowError("Trail system not available");
                    return;
                }
            }

            _cam = Camera.main;
            _trailDrawer.StartBuilding();
            _trailDrawer.OnTrailCancelled += HandleTrailCancelled;
            _trailDrawer.OnTrailConfirmed += HandleTrailConfirmed;
            _trailDrawer.OnStateChanged += HandleStateChanged;
            _trailDrawer.OnAnchorPlaced += HandleAnchorPlaced;

            SetCursorVisible(true);
            DisableCursorRaycastTargets();
            NotificationManager.Instance?.ShowInfo("Click to place trail anchors");
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            SetCursorVisible(false);
            _isPainting = false;

            if (_trailDrawer != null)
            {
                _trailDrawer.OnTrailCancelled -= HandleTrailCancelled;
                _trailDrawer.OnTrailConfirmed -= HandleTrailConfirmed;
                _trailDrawer.OnStateChanged -= HandleStateChanged;
                _trailDrawer.OnAnchorPlaced -= HandleAnchorPlaced;

                if (_trailDrawer.IsBuilding)
                    _trailDrawer.CancelBuilding();
            }
        }

        public override void OnCancel()
        {
            if (_trailDrawer != null && _trailDrawer.IsBuilding)
            {
                _trailDrawer.CancelBuilding();
                return;
            }
            base.OnCancel();
        }

        // ── Input ────────────────────────────────────────────────────────

        protected override void HandleInput()
        {
            if (_trailDrawer == null) return;

            // ESC always cancels
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_trailDrawer.IsBuilding)
                    _trailDrawer.CancelBuilding();
                else
                    UIManager.Instance?.DeactivateTool();
                return;
            }

            // Right-click or Delete key: undo chain (always works during building)
            if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Delete))
                && _trailDrawer.IsBuilding)
            {
                HandleRightClick();
                return;
            }

            bool overUI = IsMouseOverUI();

            // Update cursor position
            UpdateCursor(overUI);

            if (overUI) return;

            // Route to mode-specific input
            switch (_trailDrawer.Mode)
            {
                case TrailDrawMode.Paint: HandlePaintInput(); break;
                case TrailDrawMode.Line:  HandleLineInput();  break;
                case TrailDrawMode.Pen:   HandlePenInput();   break;
            }
        }

        // ── Shared: get the best click position (snapped or raw) ────────

        private Vector3? GetClickPosition()
        {
            if (_lastSnappedWorldPos.HasValue)
                return _lastSnappedWorldPos.Value;
            if (_lastValidWorldPos.HasValue)
                return _lastValidWorldPos.Value;
            return _trailDrawer.GetMountainPositionUnderMouse();
        }

        // ── Paint mode ───────────────────────────────────────────────────

        private void HandlePaintInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector3? pos = GetClickPosition();
                if (!pos.HasValue) return;

                if (_trailDrawer.State == TrailBuildState.Settled)
                {
                    _trailDrawer.TryResumeFromAnchor(pos.Value);
                    return;
                }

                _isPainting = true;
                _trailDrawer.AddPaintSample(pos.Value);
            }

            if (Input.GetMouseButton(0) && _isPainting)
            {
                Vector3? pos = GetClickPosition();
                if (pos.HasValue)
                    _trailDrawer.AddPaintSample(pos.Value);
            }

            if (Input.GetMouseButtonUp(0) && _isPainting)
            {
                _isPainting = false;
                _trailDrawer.FinishPaintStroke();
            }
        }

        // ── Line mode ────────────────────────────────────────────────────

        private void HandleLineInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector3? pos = GetClickPosition();
                if (!pos.HasValue) return;

                if (_trailDrawer.State == TrailBuildState.Settled)
                {
                    _trailDrawer.TryResumeFromAnchor(pos.Value);
                    return;
                }

                _trailDrawer.PlaceAnchor(pos.Value);
            }
        }

        // ── Pen mode (new curvy flow) ────────────────────────────────

        private void HandlePenInput()
        {
            // ── Mouse down ──────────────────────────────────────────
            if (Input.GetMouseButtonDown(0))
            {
                Vector3? pos = GetClickPosition();
                if (!pos.HasValue) return;

                if (_trailDrawer.State == TrailBuildState.Settled)
                {
                    // Scale detection radii with camera distance so segments and
                    // anchors remain clickable at every zoom level.
                    float anchorRadius = _trailDrawer.SnapRadius;
                    float segRadius = _trailDrawer.TrailWidth * 0.75f;
                    if (_cam != null)
                    {
                        float camDist = Vector3.Distance(_cam.transform.position, pos.Value);
                        float minRadius = camDist * 0.035f;
                        anchorRadius = Mathf.Max(anchorRadius, minRadius);
                        segRadius = Mathf.Max(segRadius, minRadius);
                    }

                    int anchorIdx = _trailDrawer.FindAnchorUnderPoint(
                        pos.Value, anchorRadius);

                    if (anchorIdx >= 0)
                    {
                        if (anchorIdx == 0)
                            return;

                        _penMouseDownOnAnchor = true;
                        _penMouseDownAnchorIndex = anchorIdx;
                        _penMouseDownPos = pos.Value;
                        _penDragStarted = false;

                        _trailDrawer.BeginAnchorDrag(anchorIdx);
                        return;
                    }

                    float paramT;
                    int segIdx = _trailDrawer.FindSegmentUnderPoint(
                        pos.Value, segRadius, out paramT);

                    if (segIdx >= 0)
                    {
                        _trailDrawer.BeginSegmentDrag(segIdx, paramT);
                        return;
                    }

                    return;
                }

                if (_trailDrawer.State == TrailBuildState.Idle
                    || _trailDrawer.AnchorCount == 0)
                {
                    if (!_trailDrawer.IsValidStartPosition(pos.Value))
                        return;
                }

                _trailDrawer.PlaceAnchor(pos.Value);
                return;
            }

            // ── Mouse held ──────────────────────────────────────────
            if (Input.GetMouseButton(0))
            {
                Vector3? pos = GetClickPosition();
                // Fall back to last known position so drags never skip frames
                // when the mountain raycast intermittently misses.
                if (!pos.HasValue) pos = _lastValidWorldPos;
                if (!pos.HasValue) return;

                if (_trailDrawer.IsDraggingSegment)
                {
                    _trailDrawer.UpdateSegmentDrag(pos.Value);
                    return;
                }

                if (_penMouseDownOnAnchor)
                {
                    if (!_penDragStarted)
                    {
                        // XZ distance so slope-induced Y offset doesn't inflate the
                        // threshold and create a dead zone at the start of the drag.
                        float dx = pos.Value.x - _penMouseDownPos.x;
                        float dz = pos.Value.z - _penMouseDownPos.z;
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        if (dist > PenDragThreshold)
                            _penDragStarted = true;
                    }

                    if (_penDragStarted && _trailDrawer.IsDraggingAnchor)
                        _trailDrawer.UpdateAnchorDrag(pos.Value);
                    return;
                }
            }

            // ── Mouse up ────────────────────────────────────────────
            if (Input.GetMouseButtonUp(0))
            {
                if (_trailDrawer.IsDraggingSegment)
                {
                    _trailDrawer.EndSegmentDrag();
                }
                else if (_trailDrawer.IsDraggingAnchor)
                {
                    _trailDrawer.EndAnchorDrag();
                }

                if (_penMouseDownOnAnchor && !_penDragStarted)
                {
                    if (_penMouseDownAnchorIndex == _trailDrawer.AnchorCount - 1)
                        _trailDrawer.ResumeFromLastAnchor();
                }

                _penMouseDownOnAnchor = false;
                _penMouseDownAnchorIndex = -1;
                _penDragStarted = false;
            }
        }

        // ── Right-click ──────────────────────────────────────────────────

        private void HandleRightClick()
        {
            if (!_trailDrawer.IsBuilding)
            {
                UIManager.Instance?.DeactivateTool();
                return;
            }

            bool cancelled = _trailDrawer.UndoOrSettle();
            // If fully cancelled, the event handler will deactivate the tool
        }

        // ── Cursor circle ────────────────────────────────────────────────

        private void UpdateCursor(bool overUI)
        {
            if (_cursorCircle == null) return;

            // Circle cursor only visible when actively placing points
            bool showCircle = _trailDrawer.Mode != TrailDrawMode.Pen
                           || _trailDrawer.State == TrailBuildState.Placing
                           || _trailDrawer.State == TrailBuildState.Idle;
            _cursorCircle.gameObject.SetActive(showCircle);
            _lastSnappedWorldPos = null;

            // Size: trail width in world units → screen pixels
            float worldDiameter = _trailDrawer.TrailWidth;
            float pixelDiameter = WorldSizeToScreenPixels(worldDiameter);
            pixelDiameter = Mathf.Max(pixelDiameter, 16f);
            _cursorCircle.sizeDelta = new Vector2(pixelDiameter, pixelDiameter);

            // Feed the raw world position to TrailDrawer for snap detection + preview.
            // Cache the last valid position so clicks still work when the raycast
            // intermittently misses (edge of mountain, fast camera movement).
            if (!overUI)
            {
                Vector3? worldPos = _trailDrawer.GetMountainPositionUnderMouse();
                if (worldPos.HasValue)
                {
                    _lastValidWorldPos = worldPos.Value;
                    _trailDrawer.UpdateCursorPosition(worldPos.Value);
                    _lastSnappedWorldPos = _trailDrawer.CursorSnappedWorldPos;
                }
                else if (_lastValidWorldPos.HasValue)
                {
                    _lastSnappedWorldPos = _trailDrawer.CursorSnappedWorldPos;
                }
            }

            // Position the cursor circle: use screen-space delta so it works
            // regardless of Canvas render mode / scaler settings
            if (_trailDrawer.IsCursorSnapped && _cam != null)
            {
                Vector3 rawScreen = _cam.WorldToScreenPoint(_trailDrawer.CursorRawWorldPos);
                Vector3 snapScreen = _cam.WorldToScreenPoint(_trailDrawer.CursorSnappedWorldPos);
                Vector3 delta = snapScreen - rawScreen;
                _cursorCircle.position = Input.mousePosition + new Vector3(delta.x, delta.y, 0f);
            }
            else
            {
                _cursorCircle.position = Input.mousePosition;
            }

            // Color feedback: RED when placing first point and not snapped to valid start
            bool needsValidStart = _trailDrawer.Mode == TrailDrawMode.Pen
                                && _trailDrawer.AnchorCount == 0
                                && !_trailDrawer.IsCursorSnapped;

            if (_cursorCircleBorder != null)
            {
                if (needsValidStart)
                    _cursorCircleBorder.color = _invalidStartBorder;
                else if (_trailDrawer.IsCursorSnapped)
                    _cursorCircleBorder.color = _snapBorderColor;
                else
                    _cursorCircleBorder.color = _cursorBorderColor;
            }
            if (_cursorCircleImage != null)
            {
                if (needsValidStart)
                    _cursorCircleImage.color = _invalidStartFill;
                else if (_trailDrawer.IsCursorSnapped)
                    _cursorCircleImage.color = new Color(
                        _snapBorderColor.r, _snapBorderColor.g, _snapBorderColor.b, 0.15f);
                else
                    _cursorCircleImage.color = _cursorFillColor;
            }
        }

        private float WorldSizeToScreenPixels(float worldSize)
        {
            if (_cam == null) return 50f;

            if (_cam.orthographic)
            {
                return (worldSize / (_cam.orthographicSize * 2f)) * Screen.height;
            }

            // Perspective fallback: approximate using a point on the terrain
            Vector3? worldPos = _trailDrawer.GetMountainPositionUnderMouse();
            if (!worldPos.HasValue) return 50f;

            Vector3 left = _cam.WorldToScreenPoint(worldPos.Value - _cam.transform.right * worldSize * 0.5f);
            Vector3 right = _cam.WorldToScreenPoint(worldPos.Value + _cam.transform.right * worldSize * 0.5f);
            return Vector3.Distance(left, right);
        }

        private void SetCursorVisible(bool visible)
        {
            if (_cursorCircle != null)
                _cursorCircle.gameObject.SetActive(visible);
        }

        private void DisableCursorRaycastTargets()
        {
            if (_cursorCircleImage != null) _cursorCircleImage.raycastTarget = false;
            if (_cursorCircleBorder != null) _cursorCircleBorder.raycastTarget = false;
        }

        // ── Mode / width changes (called by DockController) ─────────────

        public void SetDrawMode(TrailDrawMode mode)
        {
            if (_trailDrawer != null)
                _trailDrawer.SetMode(mode);
        }

        public void SetTrailWidth(float width)
        {
            if (_trailDrawer != null)
                _trailDrawer.SetWidth(width);
        }

        // ── Event handlers ───────────────────────────────────────────────

        private void HandleAnchorPlaced()
        {
            if (_trailDrawer.AnchorCount == 1)
            {
                ContextWindowController.Instance?.ShowTrailBuildWindow(
                    onConfirm: () => _trailDrawer?.ConfirmTrail(),
                    onCancel: () => _trailDrawer?.CancelBuilding());
            }
        }

        private void HandleStateChanged(TrailBuildState state)
        {
            if (state == TrailBuildState.Settled && _trailDrawer.AnchorCount >= 2)
            {
                bool canConfirm = _trailDrawer.CanConfirmTrail();
                ContextWindowController.Instance?.ShowTrailBuildConfirmButtons(canConfirm);
            }
            else if (state == TrailBuildState.Placing)
            {
                ContextWindowController.Instance?.HideTrailBuildConfirmButtons();
            }
        }

        private void HandleTrailConfirmed(SelectableStructure selectable)
        {
            _trailDrawer.OnTrailConfirmed -= HandleTrailConfirmed;
            UIManager.Instance?.DeactivateTool();

            if (selectable != null)
                ContextWindowController.Instance?.ShowStructure(selectable);
            else
                ContextWindowController.Instance?.Hide();
        }

        private void HandleTrailCancelled()
        {
            _trailDrawer.OnTrailCancelled -= HandleTrailCancelled;
            UIManager.Instance?.DeactivateTool();
            ContextWindowController.Instance?.Hide();
        }
    }
}
