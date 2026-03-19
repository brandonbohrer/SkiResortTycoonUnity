using System;
using System.Collections.Generic;
using UnityEngine;
using SkiResortTycoon.Core;
using SkiResortTycoon.UI;

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
        private float _trailWidth = 20f;

        private readonly List<TrailAnchorPoint> _anchors = new List<TrailAnchorPoint>();
        private Vector3 _cursorWorldPos;
        private bool _isSnappedToLastAnchor;

        // Paint mode accumulator
        private readonly List<Vector3> _paintPoints = new List<Vector3>();
        private Vector3 _lastPaintSample;

        // Pen mode handle dragging (legacy — kept for old pen flow)
        private bool _isDraggingHandle;
        private Vector3 _penDragStart;

        // Segment drag state (new curvy flow)
        private bool _isDraggingSegment;
        private int _dragSegmentIndex = -1;
        private float _dragSegmentT;
        private int _segDragTargetIdx = -1;
        // Anchor drag state (new curvy flow)
        private bool _isDraggingAnchor;
        private int _dragAnchorIndex = -1;

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
        public bool IsCursorSnapped => _isSnappedToLastAnchor || (_magneticCursor != null && _magneticCursor.IsSnapped);
        public Vector3 CursorSnappedWorldPos => _isSnappedToLastAnchor
            ? _cursorWorldPos
            : (_magneticCursor != null ? _magneticCursor.SnappedPosition : _cursorWorldPos);
        public Vector3 CursorRawWorldPos => _magneticCursor != null
            ? _magneticCursor.RawPosition : _cursorWorldPos;
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
                    registry = _liftBuilder.Connectivity.Registry;
                _trailSystem = new TrailSystem(_mountainManager.TerrainData, registry);
            }

            if (_magneticCursor == null && _liftBuilder != null && _liftBuilder.Connectivity != null)
            {
                _magneticCursor = new MagneticCursor(_liftBuilder.Connectivity.Registry, _snapRadius);
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
            _isDraggingSegment = false;
            _dragSegmentIndex = -1;
            _segDragTargetIdx = -1;
            _isDraggingAnchor = false;
            _dragAnchorIndex = -1;
            SetState(TrailBuildState.Idle);
        }

        public void SetMode(TrailDrawMode mode) => _mode = mode;

        public void SetWidth(float width)
        {
            _trailWidth = Mathf.Clamp(width, 10f, 30f);
            RebuildPreview();
        }

        public void UpdateCursorPosition(Vector3 worldPos)
        {
            if (_magneticCursor != null)
            {
                bool isStart = _anchors.Count == 0;
                var types = isStart
                    ? new[] { SnapPointType.BuildingEntrance, SnapPointType.LiftTop, SnapPointType.TrailEnd, SnapPointType.TrailPoint }
                    : new[] { SnapPointType.BuildingEntrance, SnapPointType.LiftBottom,
                              SnapPointType.BaseSpawn, SnapPointType.TrailStart, SnapPointType.TrailPoint };
                _magneticCursor.Update(worldPos, types);
                _cursorWorldPos = _magneticCursor.SnappedPosition;
            }
            else
            {
                _cursorWorldPos = worldPos;
            }

            _isSnappedToLastAnchor = false;
            if (_state == TrailBuildState.Settled && _anchors.Count > 0)
            {
                var lastAnchor = _anchors[_anchors.Count - 1];
                Vector3 lastPos = MountainManager.ToUnityVector3(lastAnchor.Position);
                if (Vector3.Distance(worldPos, lastPos) <= _snapRadius)
                {
                    _cursorWorldPos = lastPos;
                    _isSnappedToLastAnchor = true;
                }
            }

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

            if (_mode == TrailDrawMode.Pen && _anchors.Count >= 2)
                SetState(TrailBuildState.Settled);
            else
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
                _paintPoints.Clear();
                return;
            }

            // Remove the initial paint anchor (placed by AddPaintSample) so we
            // don't duplicate it, then append all paint samples to existing anchors.
            if (_anchors.Count > 0 && _anchors[_anchors.Count - 1].SourceMode == TrailDrawMode.Paint)
                _anchors.RemoveAt(_anchors.Count - 1);

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

            last.HandleOut = MountainManager.ToVector3f(ProjectOntoTerrain(anchorPos + offset));
            last.HandleIn = MountainManager.ToVector3f(ProjectOntoTerrain(anchorPos - offset));

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
        public bool IsDraggingSegment => _isDraggingSegment;
        public bool IsDraggingAnchor => _isDraggingAnchor;
        public float SnapRadius => _snapRadius;

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
                // Break handle continuity so next segment starts as a fresh curve
                lastAnchor.HandleOut = null;
                SetState(TrailBuildState.Placing);
                RebuildPreview();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called by TrailBuildTool when the player clicks the last anchor
        /// without dragging. Transitions back to Placing so the preview
        /// extends from that anchor.
        /// </summary>
        public void ResumeFromLastAnchor()
        {
            if (_state != TrailBuildState.Settled || _anchors.Count == 0) return;
            var last = _anchors[_anchors.Count - 1];
            last.HandleOut = null;
            SetState(TrailBuildState.Placing);
            RebuildPreview();
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
                if (_mode == TrailDrawMode.Pen && _anchors.Count <= 1)
                {
                    CancelBuilding();
                    return true;
                }

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

                // Cascade-remove any trailing hidden curve-control anchors
                // so undo skips straight to the previous player-placed anchor.
                while (_anchors.Count > 0 && _anchors[_anchors.Count - 1].IsCurveControl)
                    _anchors.RemoveAt(_anchors.Count - 1);

                if (_anchors.Count <= 0)
                {
                    CancelBuilding();
                    return true;
                }

                if (_mode == TrailDrawMode.Pen && _anchors.Count >= 2)
                    SetState(TrailBuildState.Settled);
                else
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
            _isDraggingSegment = false;
            _dragSegmentIndex = -1;
            _segDragTargetIdx = -1;
            _isDraggingAnchor = false;
            _dragAnchorIndex = -1;
            ClearAnchorMarkers();
            TreeClearer.RestorePreviewTrees();
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

            // Project all evaluated points onto the terrain surface.
            // EvaluatePathFromAnchors does pure math (bezier/linear) without terrain
            // awareness, so intermediate points can float above or below the mesh.
            for (int i = 0; i < trail.WorldPathPoints.Count; i++)
            {
                Vector3 pt = MountainManager.ToUnityVector3(trail.WorldPathPoints[i]);
                pt = ProjectOntoTerrain(pt);
                trail.WorldPathPoints[i] = MountainManager.ToVector3f(pt);
            }

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

            _trailSystem.MinPoints = 2;
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
            
            // Deduct trail build cost
            int trailCost = _trailSystem.CalculateTrailCost(trail);
            var simRunner = FindObjectOfType<SimulationRunner>();
            if (simRunner?.Sim?.State != null)
            {
                if (simRunner.Sim.State.Money < trailCost)
                {
                    NotificationManager.Instance?.ShowError($"Not enough money! Trail costs ${trailCost:N0}");
                    _trailSystem.RemoveTrail(trail);
                    CancelBuilding();
                    return;
                }
                simRunner.Sim.State.Money -= trailCost;
            }
            
            trail.GenerateBoundaries();

            // Project boundary points onto terrain so the edge lines sit on the surface
            for (int i = 0; i < trail.LeftBoundaryPoints.Count; i++)
            {
                Vector3 pt = MountainManager.ToUnityVector3(trail.LeftBoundaryPoints[i]);
                pt = ProjectOntoTerrain(pt);
                trail.LeftBoundaryPoints[i] = MountainManager.ToVector3f(pt);
            }
            for (int i = 0; i < trail.RightBoundaryPoints.Count; i++)
            {
                Vector3 pt = MountainManager.ToUnityVector3(trail.RightBoundaryPoints[i]);
                pt = ProjectOntoTerrain(pt);
                trail.RightBoundaryPoints[i] = MountainManager.ToVector3f(pt);
            }

            // Commit preview-hidden trees so they stay disabled permanently,
            // then do a final pass with the confirmed path to catch any stragglers.
            TreeClearer.CommitPreviewTrees();
            var pathUnity = new List<Vector3>();
            foreach (var pt in trail.WorldPathPoints)
                pathUnity.Add(MountainManager.ToUnityVector3(pt));
            TreeClearer.ClearTreesAlongPath(pathUnity, _trailWidth * 0.5f);

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

            if (_committedPathCache.Count >= 2)
                TreeClearer.ClearTreesForPreview(_committedPathCache, _trailWidth * 0.5f);

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
                // Subdivide the straight-line preview so intermediate points
                // are projected onto the terrain and the line hugs the ground.
                Vector3 start = ProjectOntoTerrain(MountainManager.ToUnityVector3(lastAnchor.Position));
                Vector3 end = ProjectOntoTerrain(_cursorWorldPos);
                float dist = Vector3.Distance(start, end);
                int steps = Mathf.Max(2, Mathf.CeilToInt(dist / 2f));
                for (int s = 0; s <= steps; s++)
                {
                    float pct = s / (float)steps;
                    Vector3 pt = Vector3.Lerp(start, end, pct);
                    _previewSegCache.Add(ProjectOntoTerrain(pt));
                }
            }

            // Combine committed path + preview into a single continuous mesh so
            // the outline perpendiculars are averaged properly at the junction.
            var combined = new List<Vector3>(_committedPathCache.Count + _previewSegCache.Count);
            combined.AddRange(_committedPathCache);
            int skip = _committedPathCache.Count > 0 ? 1 : 0;
            for (int i = skip; i < _previewSegCache.Count; i++)
                combined.Add(_previewSegCache[i]);

            if (combined.Count >= 2)
                _previewRenderer.SetCommittedPath(combined, _trailWidth);
            _previewRenderer.HidePreview();
        }

        private void RebuildPaintPreview()
        {
            if (_previewRenderer == null || _paintPoints.Count < 2) return;
            var projected = new List<Vector3>(_paintPoints.Count);
            foreach (var pt in _paintPoints)
                projected.Add(ProjectOntoTerrain(pt));
            _previewRenderer.SetCommittedPath(projected, _trailWidth);

            TreeClearer.ClearTreesForPreview(projected, _trailWidth * 0.5f);
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
                    Vector3 startPos = MountainManager.ToUnityVector3(a.Position);
                    Vector3 endPos = MountainManager.ToUnityVector3(b.Position);
                    float dist = Vector3.Distance(startPos, endPos);
                    int steps = Mathf.Max(2, Mathf.CeilToInt(dist / 2f));
                    int s0 = (i == 0) ? 0 : 1;
                    for (int s = s0; s <= steps; s++)
                    {
                        float pct = s / (float)steps;
                        Vector3 pt = Vector3.Lerp(startPos, endPos, pct);
                        tempV3f.Add(MountainManager.ToVector3f(pt));
                    }
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
                if (a.IsCurveControl) continue; // hidden anchor — no marker

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

        // ── Segment drag ─────────────────────────────────────────────

        /// <summary>
        /// Finds which trail segment (pair of consecutive anchors) is closest
        /// to worldPos. Returns the segment index (i, where segment = anchor[i]→anchor[i+1]),
        /// or -1 if nothing is within maxDist. Also outputs the approximate
        /// Bezier parameter t at the closest point.
        /// </summary>
        public int FindSegmentUnderPoint(Vector3 worldPos, float maxDist, out float paramT)
        {
            paramT = 0f;
            if (_anchors.Count < 2) return -1;

            int bestSegment = -1;
            float bestDist = maxDist;
            float bestT = 0f;

            for (int seg = 0; seg < _anchors.Count - 1; seg++)
            {
                const int steps = 20;
                for (int step = 0; step <= steps; step++)
                {
                    float t = step / (float)steps;
                    Vector3 pt = EvaluateSegmentAt(_anchors[seg], _anchors[seg + 1], t);
                    float dist = Vector3.Distance(pt, worldPos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestSegment = seg;
                        bestT = t;
                    }
                }
            }

            paramT = bestT;
            return bestSegment;
        }

        /// <summary>
        /// Begins a segment drag. If either flanking anchor is already a
        /// hidden curve-control point, reuses it. Otherwise inserts a new
        /// hidden curve-control anchor between the two endpoints. The marker
        /// is never shown for curve-control anchors.
        /// </summary>
        public void BeginSegmentDrag(int segmentIndex, float t)
        {
            if (segmentIndex < 0 || segmentIndex >= _anchors.Count - 1) return;
            _isDraggingSegment = true;
            _dragSegmentIndex = segmentIndex;
            _dragSegmentT = Mathf.Clamp(t, 0.05f, 0.95f);

            // Reuse an existing curve-control anchor if present on either side
            if (_anchors[segmentIndex].IsCurveControl)
            {
                _segDragTargetIdx = segmentIndex;
            }
            else if (_anchors[segmentIndex + 1].IsCurveControl)
            {
                _segDragTargetIdx = segmentIndex + 1;
            }
            else
            {
                // Insert a new hidden curve-control anchor
                Vector3 initPos = EvaluateSegmentAt(
                    _anchors[segmentIndex], _anchors[segmentIndex + 1], _dragSegmentT);
                initPos = ProjectOntoTerrain(initPos);

                var cc = new TrailAnchorPoint(
                    MountainManager.ToVector3f(initPos), TrailDrawMode.Pen);
                cc.IsCurveControl = true;

                _anchors.Insert(segmentIndex + 1, cc);
                _segDragTargetIdx = segmentIndex + 1;

                // Clear stale handles from the original (now-split) segment
                _anchors[segmentIndex].HandleOut = null;
                if (segmentIndex + 2 < _anchors.Count)
                    _anchors[segmentIndex + 2].HandleIn = null;
            }

            ComputeSmoothHandles(_segDragTargetIdx);
            RebuildPreview();
        }

        /// <summary>
        /// Repositions the curve-control anchor to the drag position and
        /// recomputes smooth handles so the curve follows.
        /// </summary>
        public void UpdateSegmentDrag(Vector3 dragWorldPos)
        {
            if (!_isDraggingSegment || _segDragTargetIdx < 0) return;

            Vector3 projected = ProjectOntoTerrain(dragWorldPos);
            _anchors[_segDragTargetIdx].Position =
                MountainManager.ToVector3f(projected);

            ComputeSmoothHandles(_segDragTargetIdx);
            RebuildPreview();
        }

        public void EndSegmentDrag()
        {
            _isDraggingSegment = false;
            _dragSegmentIndex = -1;
            _segDragTargetIdx = -1;
        }

        /// <summary>
        /// Sets HandleIn/HandleOut on anchor[idx] so the curve passes smoothly
        /// through that point. Handles point along the tangent between the
        /// neighboring anchors, with length proportional to the distance to
        /// each neighbor.
        /// </summary>
        private void ComputeSmoothHandles(int idx)
        {
            if (idx <= 0 || idx >= _anchors.Count - 1) return;

            Vector3 prev = MountainManager.ToUnityVector3(_anchors[idx - 1].Position);
            Vector3 curr = MountainManager.ToUnityVector3(_anchors[idx].Position);
            Vector3 next = MountainManager.ToUnityVector3(_anchors[idx + 1].Position);

            Vector3 dirIn = (curr - prev).normalized;
            Vector3 dirOut = (next - curr).normalized;
            Vector3 tangent = (dirIn + dirOut);
            if (tangent.sqrMagnitude < 0.001f) tangent = dirIn;
            tangent.Normalize();

            float lenIn = Vector3.Distance(prev, curr);
            float lenOut = Vector3.Distance(curr, next);
            const float handleFactor = 0.3f;

            _anchors[idx].HandleIn = MountainManager.ToVector3f(
                ProjectOntoTerrain(curr - tangent * lenIn * handleFactor));
            _anchors[idx].HandleOut = MountainManager.ToVector3f(
                ProjectOntoTerrain(curr + tangent * lenOut * handleFactor));
        }

        // ── Anchor drag ──────────────────────────────────────────────

        /// <summary>
        /// Finds the closest anchor to worldPos within maxDist.
        /// Returns anchor index or -1.
        /// </summary>
        public int FindAnchorUnderPoint(Vector3 worldPos, float maxDist)
        {
            int bestIdx = -1;
            float bestDist = maxDist;

            for (int i = 0; i < _anchors.Count; i++)
            {
                if (_anchors[i].IsCurveControl) continue; // hidden — not selectable

                Vector3 anchorPos = MountainManager.ToUnityVector3(_anchors[i].Position);
                float dist = Vector3.Distance(anchorPos, worldPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        /// <summary>
        /// Begin dragging an anchor. Index 0 (the trail start) is locked and
        /// will be rejected.
        /// </summary>
        public void BeginAnchorDrag(int anchorIndex)
        {
            if (anchorIndex <= 0 || anchorIndex >= _anchors.Count) return;
            _isDraggingAnchor = true;
            _dragAnchorIndex = anchorIndex;
        }

        public void UpdateAnchorDrag(Vector3 dragWorldPos)
        {
            if (!_isDraggingAnchor || _dragAnchorIndex < 0) return;

            var anchor = _anchors[_dragAnchorIndex];
            Vector3 projected = ProjectOntoTerrain(dragWorldPos);
            Vector3 oldPos = MountainManager.ToUnityVector3(anchor.Position);
            Vector3 delta = projected - oldPos;

            anchor.Position = MountainManager.ToVector3f(projected);

            if (anchor.HandleIn.HasValue)
            {
                Vector3 hi = MountainManager.ToUnityVector3(anchor.HandleIn.Value);
                anchor.HandleIn = MountainManager.ToVector3f(ProjectOntoTerrain(hi + delta));
            }
            if (anchor.HandleOut.HasValue)
            {
                Vector3 ho = MountainManager.ToUnityVector3(anchor.HandleOut.Value);
                anchor.HandleOut = MountainManager.ToVector3f(ProjectOntoTerrain(ho + delta));
            }

            RebuildPreview();
        }

        public void EndAnchorDrag()
        {
            _isDraggingAnchor = false;
            _dragAnchorIndex = -1;
        }

        // ── Trail validation ─────────────────────────────────────────

        /// <summary>
        /// Returns true when the raw world position is within snap radius of a
        /// valid start type (LiftTop, TrailEnd, TrailPoint, BuildingEntrance).
        /// Does NOT mutate the magnetic cursor.
        /// </summary>
        public bool IsValidStartPosition(Vector3 worldPos)
        {
            return IsNearSnapPoint(worldPos, new[]
            {
                SnapPointType.LiftTop,
                SnapPointType.TrailEnd,
                SnapPointType.TrailPoint,
                SnapPointType.BuildingEntrance
            });
        }

        /// <summary>
        /// True when the trail has 2+ anchors AND both endpoints are at valid
        /// snap connections (start = lift top / trail; end = trail / base / lift bottom).
        /// </summary>
        public bool CanConfirmTrail()
        {
            if (_anchors.Count < 2) return false;

            Vector3 firstPos = MountainManager.ToUnityVector3(_anchors[0].Position);
            bool validStart = IsNearSnapPoint(firstPos, new[]
            {
                SnapPointType.LiftTop, SnapPointType.TrailEnd,
                SnapPointType.TrailPoint, SnapPointType.BuildingEntrance
            });
            if (!validStart) return false;

            Vector3 lastPos = MountainManager.ToUnityVector3(_anchors[_anchors.Count - 1].Position);
            bool validEnd = IsNearSnapPoint(lastPos, new[]
            {
                SnapPointType.LiftBottom, SnapPointType.BaseSpawn,
                SnapPointType.TrailStart, SnapPointType.TrailPoint,
                SnapPointType.BuildingEntrance
            });
            return validEnd;
        }

        private bool IsNearSnapPoint(Vector3 worldPos, SnapPointType[] types)
        {
            if (_liftBuilder == null || _liftBuilder.Connectivity == null) return false;
            var registry = _liftBuilder.Connectivity.Registry;
            if (registry == null) return false;

            foreach (var type in types)
            {
                foreach (var snap in registry.GetByType(type))
                {
                    Vector3 snapPos = new Vector3(snap.Position.X, snap.Position.Y, snap.Position.Z);
                    if (Vector3.Distance(worldPos, snapPos) <= _snapRadius)
                        return true;
                }
            }
            return false;
        }

        // ── Segment evaluation helper ────────────────────────────────

        private Vector3 EvaluateSegmentAt(TrailAnchorPoint a, TrailAnchorPoint b, float t)
        {
            Vector3 p0 = MountainManager.ToUnityVector3(a.Position);
            Vector3 p3 = MountainManager.ToUnityVector3(b.Position);

            if (!a.HandleOut.HasValue && !b.HandleIn.HasValue)
                return Vector3.Lerp(p0, p3, t);

            Vector3 p1 = a.HandleOut.HasValue
                ? MountainManager.ToUnityVector3(a.HandleOut.Value) : p0;
            Vector3 p2 = b.HandleIn.HasValue
                ? MountainManager.ToUnityVector3(b.HandleIn.Value) : p3;

            float u = 1f - t;
            return u * u * u * p0
                 + 3f * u * u * t * p1
                 + 3f * u * t * t * p2
                 + t * t * t * p3;
        }

        // ── Snap helpers ─────────────────────────────────────────────────

        private Vector3 TrySnap(Vector3 rawPos, bool isStart)
        {
            if (_magneticCursor == null) return rawPos;

            var types = isStart
                ? new[] { SnapPointType.BuildingEntrance, SnapPointType.LiftTop, SnapPointType.TrailEnd, SnapPointType.TrailPoint }
                : new[] { SnapPointType.BuildingEntrance, SnapPointType.LiftBottom, SnapPointType.BaseSpawn, SnapPointType.TrailStart, SnapPointType.TrailPoint };

            _magneticCursor.Update(rawPos, types);
            return _magneticCursor.IsSnapped ? _magneticCursor.SnappedPosition : rawPos;
        }

        /// <summary>
        /// Registers snap points for a trail. Used when confirming a new trail or when loading from save.
        /// </summary>
        public void RegisterTrailSnapPointsForLoad(TrailData trail)
        {
            if (trail == null || _liftBuilder == null || _liftBuilder.Connectivity == null) return;
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

        private void RegisterTrailSnapPoints(TrailData trail)
        {
            RegisterTrailSnapPointsForLoad(trail);
        }

        /// <summary>
        /// Applies trail setup after loading from save: boundaries, terrain projection, snap points, tree clearing.
        /// Call after TrailSystem.LoadTrails for each trail.
        /// </summary>
        public void ApplyTrailAfterLoad(TrailData trail)
        {
            if (trail == null) return;
            trail.GenerateBoundaries();
            for (int i = 0; i < trail.LeftBoundaryPoints.Count; i++)
            {
                Vector3 pt = MountainManager.ToUnityVector3(trail.LeftBoundaryPoints[i]);
                float? y = _mountainManager != null ? _mountainManager.GetHeightAtWorldPos(pt) : null;
                if (y.HasValue) pt.y = y.Value;
                trail.LeftBoundaryPoints[i] = MountainManager.ToVector3f(pt);
            }
            for (int i = 0; i < trail.RightBoundaryPoints.Count; i++)
            {
                Vector3 pt = MountainManager.ToUnityVector3(trail.RightBoundaryPoints[i]);
                float? y = _mountainManager != null ? _mountainManager.GetHeightAtWorldPos(pt) : null;
                if (y.HasValue) pt.y = y.Value;
                trail.RightBoundaryPoints[i] = MountainManager.ToVector3f(pt);
            }
            RegisterTrailSnapPointsForLoad(trail);
            if (trail.WorldPathPoints != null && trail.WorldPathPoints.Count > 0)
            {
                var pathUnity = new List<Vector3>();
                foreach (var p in trail.WorldPathPoints)
                    pathUnity.Add(MountainManager.ToUnityVector3(p));
                TreeClearer.ClearTreesAlongPath(pathUnity, trail.TrailWidth * 0.5f);
            }
        }

        // ── Demolish ─────────────────────────────────────────────────────

        /// <summary>
        /// Removes a trail from the system entirely (data, snap points, connectivity).
        /// Called by ContextWindowController when the player clicks Demolish.
        /// </summary>
        public void DemolishTrail(TrailData trail)
        {
            if (trail == null || _trailSystem == null) return;

            _trailSystem.RemoveTrail(trail);

            if (_liftBuilder != null && _liftBuilder.Connectivity != null)
                _liftBuilder.Connectivity.RebuildConnections();

            var skierViz = FindObjectOfType<SkierVisualizer>();
            if (skierViz != null) skierViz.InvalidateAllSkierGoals();

            Debug.Log($"[TrailDrawer] Trail {trail.TrailId} demolished.");
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
