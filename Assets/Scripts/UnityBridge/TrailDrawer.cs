using System;
using System.Collections.Generic;
using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    public enum TrailBuildState
    {
        Idle,
        Placing,   // preview follows cursor from last anchor
        Settled    // trail set at current anchors, waiting for confirm
    }

    /// <summary>
    /// Backend for the new trail building system.
    /// Manages anchors, three drawing modes, state machine, and preview.
    /// All input is routed here from TrailBuildTool via public API — no
    /// keyboard handling or reflection hacks.
    /// </summary>
    public class TrailDrawer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MountainManager _mountainManager;
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private TrailPreviewRenderer _previewRenderer;
        [SerializeField] private Camera _camera;

        [Header("Settings")]
        [SerializeField] private float _snapRadius = 5f;
        [SerializeField] private float _paintSampleSpacing = 1.5f;
        [SerializeField] private float _penHandleMultiplier = 1.5f;
        [SerializeField] private float _anchorResumeRadius = 2f;

        [Header("Anchor Markers")]
        [SerializeField] private Color _anchorMarkerColor = new Color(0.29f, 0.56f, 0.85f, 0.6f);
        [SerializeField] private float _anchorMarkerRadius = 0.5f;

        // ── Runtime state ────────────────────────────────────────────────
        private TrailSystem _trailSystem;
        private MagneticCursor _magneticCursor;
        private TrailBuildState _state = TrailBuildState.Idle;
        private TrailDrawMode _mode = TrailDrawMode.Paint;
        private float _trailWidth = 7.5f;

        private readonly List<TrailAnchorPoint> _anchors = new List<TrailAnchorPoint>();
        private Vector3 _cursorWorldPos;

        // Paint mode accumulator
        private readonly List<Vector3> _paintPoints = new List<Vector3>();
        private Vector3 _lastPaintSample;

        // Pen mode handle dragging
        private bool _isDraggingHandle;
        private Vector3 _penDragStart;

        // Cached evaluated path (committed segments only)
        private readonly List<Vector3> _committedPathCache = new List<Vector3>();
        // Cached preview segment (last anchor → cursor)
        private readonly List<Vector3> _previewSegCache = new List<Vector3>();

        // Anchor marker GameObjects
        private readonly List<GameObject> _anchorMarkers = new List<GameObject>();
        private Material _anchorMarkerMat;

        // ── Events ───────────────────────────────────────────────────────
        public event Action OnTrailCancelled;
        public event Action<TrailBuildState> OnStateChanged;
        public event Action OnAnchorPlaced;
        public event Action<SelectableStructure> OnTrailConfirmed;

        // ── Public properties ────────────────────────────────────────────
        public TrailSystem TrailSystem => _trailSystem;
        public MountainManager GridRenderer => _mountainManager;
        public TrailBuildState State => _state;
        public TrailDrawMode Mode => _mode;
        public float TrailWidth => _trailWidth;
        public int AnchorCount => _anchors.Count;
        public bool IsBuilding => _state != TrailBuildState.Idle;
        // Legacy compat used by TrailVisualizer
        public TrailData CurrentTrail => null;
        public bool IsDrawing => false;

        // ── Initialization ───────────────────────────────────────────────

        void Start()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void EnsureInitialized()
        {
            if (_trailSystem == null && _mountainManager != null && _mountainManager.TerrainData != null)
            {
                SnapRegistry registry = null;
                if (_liftBuilder != null && _liftBuilder.Connectivity != null)
                {
                    registry = _liftBuilder.Connectivity.Registry;
                    _magneticCursor = new MagneticCursor(registry, _snapRadius);
                }
                _trailSystem = new TrailSystem(_mountainManager.TerrainData, registry);
            }
        }

        void Update()
        {
            EnsureInitialized();
        }

        // ── Public API — called by TrailBuildTool ────────────────────────

        public void StartBuilding()
        {
            EnsureInitialized();
            _anchors.Clear();
            _paintPoints.Clear();
            _isDraggingHandle = false;
            SetState(TrailBuildState.Idle);
        }

        public void SetMode(TrailDrawMode mode) => _mode = mode;

        public void SetWidth(float width)
        {
            _trailWidth = Mathf.Clamp(width, 5f, 10f);
            RebuildPreview();
        }

        public void UpdateCursorPosition(Vector3 worldPos)
        {
            _cursorWorldPos = worldPos;
            if (_state == TrailBuildState.Placing)
                RebuildPreviewSegment();
        }

        // ── Anchor placement ─────────────────────────────────────────────

        /// <summary>
        /// Place an anchor at the given world position. For paint mode this is
        /// called rapidly as the mouse moves; for line/pen it's called on click.
        /// </summary>
        public void PlaceAnchor(Vector3 worldPos)
        {
            EnsureInitialized();
            if (_trailSystem == null) return;

            Vector3 snapped = TrySnap(worldPos, _anchors.Count == 0);

            var anchor = new TrailAnchorPoint(
                MountainManager.ToVector3f(snapped), _mode);

            _anchors.Add(anchor);
            SetState(TrailBuildState.Placing);
            OnAnchorPlaced?.Invoke();
            RebuildPreview();
        }

        /// <summary>
        /// Paint-mode: add a raw sample point. Converted to anchors on settle.
        /// </summary>
        public void AddPaintSample(Vector3 worldPos)
        {
            if (_paintPoints.Count == 0)
            {
                _paintPoints.Add(worldPos);
                _lastPaintSample = worldPos;
                if (_anchors.Count == 0)
                    PlaceAnchor(worldPos);
                return;
            }

            if (Vector3.Distance(worldPos, _lastPaintSample) >= _paintSampleSpacing)
            {
                _paintPoints.Add(worldPos);
                _lastPaintSample = worldPos;
                RebuildPaintPreview();
            }
        }

        /// <summary>
        /// Paint-mode: finish the current stroke. Converts raw samples into
        /// anchors and transitions to Settled.
        /// </summary>
        public void FinishPaintStroke()
        {
            if (_paintPoints.Count < 2)
            {
                // Too short — discard
                _paintPoints.Clear();
                return;
            }

            // Replace all anchors with paint samples
            _anchors.Clear();
            foreach (var pt in _paintPoints)
            {
                _anchors.Add(new TrailAnchorPoint(
                    MountainManager.ToVector3f(pt), TrailDrawMode.Paint));
            }
            _paintPoints.Clear();
            SetState(TrailBuildState.Settled);
            RebuildPreview();
        }

        /// <summary>
        /// Pen-mode: begin dragging a handle from the most recent anchor.
        /// Called on mouse-down when placing the second+ pen anchor.
        /// </summary>
        public void BeginPenHandleDrag(Vector3 anchorWorldPos)
        {
            _isDraggingHandle = true;
            _penDragStart = anchorWorldPos;
        }

        /// <summary>
        /// Pen-mode: update handle while dragging.
        /// The drag offset from the anchor position defines handleOut on the
        /// current anchor and a mirrored handleIn.
        /// </summary>
        public void UpdatePenHandleDrag(Vector3 currentWorldPos)
        {
            if (!_isDraggingHandle || _anchors.Count == 0) return;

            var last = _anchors[_anchors.Count - 1];
            Vector3 anchorPos = MountainManager.ToUnityVector3(last.Position);
            Vector3 offset = (currentWorldPos - anchorPos) * _penHandleMultiplier;

            last.HandleOut = MountainManager.ToVector3f(anchorPos + offset);
            last.HandleIn = MountainManager.ToVector3f(anchorPos - offset);

            RebuildPreview();
        }

        /// <summary>
        /// Pen-mode: end handle drag.
        /// </summary>
        public void EndPenHandleDrag()
        {
            _isDraggingHandle = false;
        }

        public bool IsDraggingHandle => _isDraggingHandle;

        /// <summary>
        /// When in Settled state, checks if a click position is near the last
        /// anchor. If so, resumes Placing from that anchor.
        /// Returns true if building was resumed.
        /// </summary>
        public bool TryResumeFromAnchor(Vector3 clickWorldPos)
        {
            if (_state != TrailBuildState.Settled || _anchors.Count == 0)
                return false;

            var lastAnchor = _anchors[_anchors.Count - 1];
            Vector3 lastPos = MountainManager.ToUnityVector3(lastAnchor.Position);
            float dist = Vector3.Distance(clickWorldPos, lastPos);

            if (dist <= _anchorResumeRadius)
            {
                SetState(TrailBuildState.Placing);
                RebuildPreview();
                return true;
            }

            return false;
        }

        // ── Undo / Cancel ────────────────────────────────────────────────

        /// <summary>
        /// Right-click behaviour:
        ///   Placing → Settled  (stop preview, keep anchors)
        ///   Settled → remove last anchor, back to Placing
        ///   Single anchor left + right-click → cancel
        /// Returns true if the trail was fully cancelled.
        /// </summary>
        public bool UndoOrSettle()
        {
            if (_state == TrailBuildState.Placing)
            {
                SetState(TrailBuildState.Settled);
                RebuildPreview();
                return false;
            }

            if (_state == TrailBuildState.Settled)
            {
                if (_anchors.Count <= 1)
                {
                    CancelBuilding();
                    return true;
                }

                _anchors.RemoveAt(_anchors.Count - 1);
                SetState(TrailBuildState.Placing);
                RebuildPreview();
                return false;
            }

            return false;
        }

        public void CancelBuilding()
        {
            _anchors.Clear();
            _paintPoints.Clear();
            _isDraggingHandle = false;
            ClearAnchorMarkers();
            SetState(TrailBuildState.Idle);
            _previewRenderer?.HideAll();
            OnTrailCancelled?.Invoke();
        }

        // ── Confirm ──────────────────────────────────────────────────────

        public void ConfirmTrail()
        {
            if (_trailSystem == null || _anchors.Count < 2)
            {
                CancelBuilding();
                return;
            }

            TrailData trail = _trailSystem.CreateTrail();
            trail.TrailWidth = _trailWidth;

            foreach (var a in _anchors)
                trail.Anchors.Add(a);

            trail.EvaluatePathFromAnchors();

            // Snap end point
            if (_magneticCursor != null && trail.WorldPathPoints.Count >= 2)
            {
                var lastPt = trail.WorldPathPoints[trail.WorldPathPoints.Count - 1];
                var lastUnity = MountainManager.ToUnityVector3(lastPt);
                _magneticCursor.Update(lastUnity, new[]
                {
                    SnapPointType.BuildingEntrance,
                    SnapPointType.LiftBottom,
                    SnapPointType.BaseSpawn,
                    SnapPointType.TrailStart
                });
                if (_magneticCursor.IsSnapped)
                    trail.WorldPathPoints[trail.WorldPathPoints.Count - 1] =
                        MountainManager.ToVector3f(_magneticCursor.SnappedPosition);
            }

            bool valid = _trailSystem.ValidateTrail(trail);
            if (!valid)
            {
                Debug.LogWarning("[TrailDrawer] Trail invalid. Removing.");
                _trailSystem.RemoveTrail(trail);
                CancelBuilding();
                return;
            }

            // Register snap points
            RegisterTrailSnapPoints(trail);

            var stats = _trailSystem.CalculateDifficulty(trail);
            trail.GenerateBoundaries();

            // Clear trees
            var pathUnity = new List<Vector3>();
            foreach (var pt in trail.WorldPathPoints)
                pathUnity.Add(MountainManager.ToUnityVector3(pt));
            TreeClearer.ClearTreesAlongPath(pathUnity, _trailWidth);

            // Rebuild connectivity
            if (_liftBuilder != null && _liftBuilder.Connectivity != null)
                _liftBuilder.Connectivity.RebuildConnections();

            // Invalidate skier goals
            var skierViz = FindObjectOfType<SkierVisualizer>();
            if (skierViz != null) skierViz.InvalidateAllSkierGoals();

            Debug.Log($"[TrailDrawer] Trail {trail.TrailId} confirmed — " +
                      $"{trail.WorldPathPoints.Count} pts, {trail.Difficulty}, " +
                      $"drop {stats.TotalDrop:F1}, grade {stats.AvgGrade * 100:F1}%");

            // Build visual and get selectable
            SelectableStructure selectable = null;
            var visualizer = FindObjectOfType<TrailVisualizer>();
            // The visualizer picks up new trails automatically on LateUpdate

            _anchors.Clear();
            _paintPoints.Clear();
            ClearAnchorMarkers();
            SetState(TrailBuildState.Idle);
            _previewRenderer?.HideAll();

            OnTrailConfirmed?.Invoke(selectable);
        }

        // ── Preview rebuilding ───────────────────────────────────────────

        private void RebuildPreview()
        {
            if (_previewRenderer == null) return;

            _committedPathCache.Clear();
            EvaluateAnchorsToUnityList(_anchors, _committedPathCache);
            _previewRenderer.SetCommittedPath(_committedPathCache, _trailWidth);

            RebuildAnchorMarkers();

            if (_state == TrailBuildState.Placing)
                RebuildPreviewSegment();
            else
                _previewRenderer.HidePreview();
        }

        private void RebuildPreviewSegment()
        {
            if (_previewRenderer == null || _anchors.Count == 0) return;

            _previewSegCache.Clear();
            var lastAnchor = _anchors[_anchors.Count - 1];

            bool useBezier = _mode == TrailDrawMode.Pen && lastAnchor.HandleOut.HasValue;

            if (useBezier)
            {
                var tempV3f = new List<Vector3f>();
                TrailData.EvaluateBezierSegment(
                    lastAnchor.Position, lastAnchor.HandleOut,
                    null, MountainManager.ToVector3f(_cursorWorldPos),
                    tempV3f, 20, skipFirst: false);

                foreach (var p in tempV3f)
                    _previewSegCache.Add(ProjectOntoTerrain(MountainManager.ToUnityVector3(p)));
            }
            else
            {
                _previewSegCache.Add(ProjectOntoTerrain(MountainManager.ToUnityVector3(lastAnchor.Position)));
                _previewSegCache.Add(ProjectOntoTerrain(_cursorWorldPos));
            }

            _previewRenderer.SetPreviewSegment(_previewSegCache, _trailWidth);
        }

        private void RebuildPaintPreview()
        {
            if (_previewRenderer == null || _paintPoints.Count < 2) return;
            var projected = new List<Vector3>(_paintPoints.Count);
            foreach (var pt in _paintPoints)
                projected.Add(ProjectOntoTerrain(pt));
            _previewRenderer.SetCommittedPath(projected, _trailWidth);
        }

        private void EvaluateAnchorsToUnityList(List<TrailAnchorPoint> anchors, List<Vector3> outList)
        {
            if (anchors.Count == 0) return;
            if (anchors.Count == 1)
            {
                outList.Add(ProjectOntoTerrain(MountainManager.ToUnityVector3(anchors[0].Position)));
                return;
            }

            var tempV3f = new List<Vector3f>();
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                var a = anchors[i];
                var b = anchors[i + 1];
                bool hasBezier = a.HandleOut.HasValue || b.HandleIn.HasValue;

                if (hasBezier)
                {
                    TrailData.EvaluateBezierSegment(
                        a.Position, a.HandleOut,
                        b.HandleIn, b.Position,
                        tempV3f, 20, skipFirst: i > 0);
                }
                else
                {
                    if (i == 0) tempV3f.Add(a.Position);
                    tempV3f.Add(b.Position);
                }
            }

            foreach (var p in tempV3f)
                outList.Add(ProjectOntoTerrain(MountainManager.ToUnityVector3(p)));
        }

        /// <summary>
        /// Projects a world point down onto the mountain surface so the trail
        /// hugs the terrain instead of floating above/below it.
        /// </summary>
        private Vector3 ProjectOntoTerrain(Vector3 point)
        {
            if (_mountainManager == null) return point;
            float? y = _mountainManager.GetHeightAtWorldPos(point);
            if (y.HasValue)
                point.y = y.Value;
            return point;
        }

        // ── Anchor markers ───────────────────────────────────────────────

        private void RebuildAnchorMarkers()
        {
            ClearAnchorMarkers();

            if (_anchorMarkerMat == null)
            {
                _anchorMarkerMat = new Material(Shader.Find("Sprites/Default"));
                _anchorMarkerMat.color = _anchorMarkerColor;
            }

            foreach (var a in _anchors)
            {
                Vector3 pos = ProjectOntoTerrain(MountainManager.ToUnityVector3(a.Position));
                pos.y += 0.2f;
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "AnchorMarker";
                marker.transform.position = pos;
                marker.transform.localScale = Vector3.one * (_anchorMarkerRadius * 2f);
                marker.GetComponent<Renderer>().material = _anchorMarkerMat;
                var col = marker.GetComponent<Collider>();
                if (col != null) Destroy(col);
                _anchorMarkers.Add(marker);
            }
        }

        private void ClearAnchorMarkers()
        {
            foreach (var m in _anchorMarkers)
                if (m != null) Destroy(m);
            _anchorMarkers.Clear();
        }

        // ── Snap helpers ─────────────────────────────────────────────────

        private Vector3 TrySnap(Vector3 rawPos, bool isStart)
        {
            if (_magneticCursor == null) return rawPos;

            var types = isStart
                ? new[] { SnapPointType.BuildingEntrance, SnapPointType.LiftTop, SnapPointType.TrailEnd }
                : new[] { SnapPointType.BuildingEntrance, SnapPointType.LiftBottom, SnapPointType.BaseSpawn, SnapPointType.TrailStart };

            _magneticCursor.Update(rawPos, types);
            return _magneticCursor.IsSnapped ? _magneticCursor.SnappedPosition : rawPos;
        }

        private void RegisterTrailSnapPoints(TrailData trail)
        {
            if (_liftBuilder == null || _liftBuilder.Connectivity == null) return;
            var registry = _liftBuilder.Connectivity.Registry;

            int count = 0;
            foreach (var pt in trail.WorldPathPoints)
            {
                registry.Register(new SnapPoint(
                    SnapPointType.TrailPoint, pt, trail.TrailId,
                    $"Trail{trail.TrailId}_Pt{count}"));
                count++;
            }

            if (trail.WorldPathPoints.Count >= 2)
            {
                registry.Register(new SnapPoint(
                    SnapPointType.TrailStart, trail.WorldPathPoints[0],
                    trail.TrailId, $"Trail{trail.TrailId}_Start"));
                registry.Register(new SnapPoint(
                    SnapPointType.TrailEnd,
                    trail.WorldPathPoints[trail.WorldPathPoints.Count - 1],
                    trail.TrailId, $"Trail{trail.TrailId}_End"));
            }
        }

        // ── Raycast helper (used by TrailBuildTool) ──────────────────────

        public Vector3? GetMountainPositionUnderMouse()
        {
            if (_camera == null || _mountainManager == null) return null;
            return _mountainManager.RaycastMountain(_camera, Input.mousePosition);
        }

        // ── State machine ────────────────────────────────────────────────

        private void SetState(TrailBuildState newState)
        {
            if (_state == newState) return;
            _state = newState;
            OnStateChanged?.Invoke(_state);
        }
    }
}
