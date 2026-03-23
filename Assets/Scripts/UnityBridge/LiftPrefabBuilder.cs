using UnityEngine;
using System.Collections.Generic;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Builds a full 3D lift from prefabs: turn wheels (base/top), cables,
    /// pillars, and chairs.  Works both for finalized lifts and live preview
    /// during placement.
    ///
    /// Hierarchy produced:
    ///   LiftRoot_{id}
    ///     BaseTurn          (SM_Prop_Lift_Turn_01)
    ///     TopTurn           (SM_Prop_Lift_Turn_01)
    ///     Cables
    ///       CablesUp        (SM_Prop_Lift_Cable_01, offset x=+1.5  y=+7.8)
    ///       CablesDown      (SM_Prop_Lift_Cable_01, offset x=-1.5  y=+7.8)
    ///     Pillars
    ///       Pillar_0 .. N   (SM_Prop_Lift_Pillar_01)
    ///     ChairsUp          (empty parent, offset x=+2  y=+7.825)
    ///       Chair_0 .. N
    ///     ChairsDown        (empty parent, offset x=-2  y=+7.825)
    ///       Chair_0 .. N
    /// </summary>
    public class LiftPrefabBuilder : MonoBehaviour
    {
        // ── References ──────────────────────────────────────────────────
        [Header("Core References")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private MountainManager _mountainManager;
        
        // ── Prefab references (assign in Inspector) ─────────────────────
        [Header("Lift Prefabs")]
        [SerializeField] private GameObject _turnPrefab;    // SM_Prop_Lift_Turn_01
        [SerializeField] private GameObject _pillarPrefab;  // SM_Prop_Lift_Pillar_01
        [SerializeField] private GameObject _cablePrefab;   // SM_Prop_Lift_Cable_01
        [SerializeField] private GameObject _chairPrefab;   // SM_Prop_Lift_Chair_01

        [Header("Spacing")]
        [SerializeField] private float _pillarSpacing = 20f;   // metres between pillars
        [SerializeField] private float _chairSpacing = 16f;    // baseline spacing before per-type multiplier
        [SerializeField] private float _corridorWidth = 8f;    // tree-clearing width
        
        [Header("Lift Type Tuning")]
        [SerializeField] private float _lowSpeedChairSpeed = 3f;
        [SerializeField] private float _highSpeedMultiplier = 1.5f;
        [SerializeField] private float _oneSeatSpacingMultiplier = 2f; // doubles chair spacing vs old baseline
        [Tooltip("Local X scale on chair roots for 1-seat lifts (fraction of prefab width).")]
        [SerializeField] private float _oneSeatChairWidthScale = 0.75f;
        [Tooltip("Extra local X scale on chair roots for 2-seat lifts (bench width). Assumes chair forward follows the cable; adjust axis in code if your mesh uses Z for width.")]
        [SerializeField] private float _twoSeatChairWidthScale = 1.25f;

        [Header("Lane Offsets (local-space, perpendicular to lift direction)")]
        [SerializeField] private float _cableUpX = 1.5f;
        [SerializeField] private float _cableDownX = -1.5f;
        [SerializeField] private float _cableY = 7.8f;
        [SerializeField] private float _chairUpX = 2f;
        [SerializeField] private float _chairDownX = -2f;
        [SerializeField] private float _chairY = 7.825f;

        // ── Built lift instances ────────────────────────────────────────
        private Dictionary<int, LiftInstance> _builtLifts = new Dictionary<int, LiftInstance>();

        // ── Preview instance (used during interactive placement) ────────
        private LiftInstance _preview;
        private bool _previewPoseCached;
        private Vector3 _previewBasePos;
        private Vector3 _previewTopPos;
        private LiftType _previewLiftType;
        private const float PREVIEW_POSE_EPSILON = 0.05f;

        /// <summary>Access a built lift's root for later queries (e.g. chair mover).</summary>
        public LiftInstance GetLiftInstance(int liftId)
        {
            _builtLifts.TryGetValue(liftId, out var inst);
            return inst;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build (or rebuild) a finalized lift from LiftData.
        /// Returns the root GameObject.
        /// </summary>
        public GameObject BuildLift(LiftData lift)
        {
            // Tear down previous instance if rebuilding
            if (_builtLifts.TryGetValue(lift.LiftId, out var old))
            {
                Destroy(old.Root);
                _builtLifts.Remove(lift.LiftId);
            }

            Vector3 basePos = MountainManager.ToUnityVector3(lift.StartPosition);
            Vector3 topPos  = MountainManager.ToUnityVector3(lift.EndPosition);

            var inst = CreateLiftHierarchy(basePos, topPos, $"LiftRoot_{lift.LiftId}", lift.Type);
            inst.LiftId = lift.LiftId;
            _builtLifts[lift.LiftId] = inst;

            // Attach chair mover component
            var mover = inst.Root.GetComponent<LiftChairMover>();
            if (mover == null) mover = inst.Root.AddComponent<LiftChairMover>();
            mover.Initialise(
                inst,
                basePos,
                topPos,
                _chairUpX,
                _chairDownX,
                _cableY,
                _simulationRunner,
                GetChairSpeedForType(lift.Type),
                lift.Type);

            // Attach selectable structure component for management
            var selectable = inst.Root.GetComponent<SelectableStructure>();
            if (selectable == null) selectable = inst.Root.AddComponent<SelectableStructure>();
            selectable.InitializeAsLift(lift);

            return inst.Root;
        }

        /// <summary>
        /// Destroy a finalized lift's visual.
        /// </summary>
        public void DestroyLift(int liftId)
        {
            if (_builtLifts.TryGetValue(liftId, out var inst))
            {
                Destroy(inst.Root);
                _builtLifts.Remove(liftId);
            }
        }

        // ── Live preview ────────────────────────────────────────────────

        /// <summary>
        /// Create or update the live preview while the user drags the top
        /// point during placement.  Cheap: reuses/recreates the hierarchy.
        /// </summary>
        public void UpdatePreview(Vector3 basePos, Vector3 topPos, LiftType liftType)
        {
            if (_preview != null && _previewPoseCached && liftType == _previewLiftType)
            {
                if (Vector3.Distance(basePos, _previewBasePos) <= PREVIEW_POSE_EPSILON &&
                    Vector3.Distance(topPos, _previewTopPos) <= PREVIEW_POSE_EPSILON)
                {
                    return;
                }
            }

            // Tear down old preview
            DestroyPreview();

            _preview = CreateLiftHierarchy(basePos, topPos, "LiftPreview", liftType);
            // No chair mover on preview (static snapshot)
            _previewPoseCached = true;
            _previewBasePos = basePos;
            _previewTopPos = topPos;
            _previewLiftType = liftType;
        }

        /// <summary>Destroy the live preview.</summary>
        public void DestroyPreview()
        {
            if (_preview != null && _preview.Root != null)
            {
                Destroy(_preview.Root);
                _preview = null;
            }
            _previewPoseCached = false;
        }

        // ── Tree clearing ───────────────────────────────────────────────

        /// <summary>
        /// Clear trees along the full lift corridor (not just endpoints).
        /// Samples densely along the entire length regardless of distance.
        /// </summary>
        public void ClearTreesAlongLift(Vector3 basePos, Vector3 topPos)
        {
            float length = Vector3.Distance(basePos, topPos);
            // Use smaller step for very dense sampling - ensures no trees are missed
            float step = Mathf.Min(3f, _corridorWidth * 0.5f);
            int samples = Mathf.Max(2, Mathf.CeilToInt(length / step) + 1);

            var points = new List<Vector3>();
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / (samples - 1);
                points.Add(Vector3.Lerp(basePos, topPos, t));
            }
            
            Debug.Log($"[LiftPrefabBuilder] Clearing {samples} sample points along {length:F1}m lift (step={step:F1}m, corridor={_corridorWidth}m)");
            TreeClearer.ClearTreesAlongPath(points, _corridorWidth);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Hierarchy construction
        // ─────────────────────────────────────────────────────────────────

        private LiftInstance CreateLiftHierarchy(Vector3 basePos, Vector3 topPos, string rootName, LiftType liftType)
        {
            var inst = new LiftInstance();

            // Direction & length
            Vector3 delta = topPos - basePos;
            float length = delta.magnitude;
            if (length < 0.1f) length = 0.1f;
            Vector3 dir = delta / length;

            // Rotation: look along lift direction projected to XZ, with Y up
            Quaternion liftRot = Quaternion.LookRotation(dir, Vector3.up);

            // ── Root ────────────────────────────────────────────────────
            inst.Root = new GameObject(rootName);
            inst.Root.transform.position = basePos;
            inst.Root.transform.rotation = liftRot;

            // ── Base Turn ───────────────────────────────────────────────
            if (_turnPrefab != null)
            {
                inst.BaseTurn = Instantiate(_turnPrefab, basePos, liftRot, inst.Root.transform);
                inst.BaseTurn.name = "BaseTurn";
            }

            // ── Top Turn ────────────────────────────────────────────────
            if (_turnPrefab != null)
            {
                inst.TopTurn = Instantiate(_turnPrefab, topPos, liftRot, inst.Root.transform);
                inst.TopTurn.name = "TopTurn";
            }

            // ── Build anchor points (base, pillars, top) ────────────────
            // Anchor points define where supports stand. Cables run between them.
            // Cable height at each pillar = ground + _cableY (the wheel/sheave height).
            // NOTE: We use _cableY (manually tuned) instead of measuring the pillar mesh,
            // because the mesh bounds include the spike/antenna above the wheel.
            var anchorPoints = new List<Vector3>(); // world-space cable-level positions
            var groundPoints = new List<Vector3>(); // world-space ground-level positions

            // Base anchor (at turn wheel)
            anchorPoints.Add(basePos + Vector3.up * _cableY);
            groundPoints.Add(basePos);

            // ── Pillars ─────────────────────────────────────────────────
            GameObject pillarsParent = new GameObject("Pillars");
            pillarsParent.transform.SetParent(inst.Root.transform, false);
            inst.Pillars = new List<GameObject>();

            if (_pillarPrefab != null && length > _pillarSpacing)
            {
                // Inset first/last pillar to avoid overlapping turn wheels
                float inset = Mathf.Min(_pillarSpacing * 0.5f, length * 0.15f);
                float usableLength = length - inset * 2f;
                int pillarCount = Mathf.Max(1, Mathf.FloorToInt(usableLength / _pillarSpacing));
                float actualSpacing = usableLength / pillarCount;

                for (int i = 0; i <= pillarCount; i++)
                {
                    float t = (inset + i * actualSpacing) / length;
                    Vector3 liftLinePos = Vector3.Lerp(basePos, topPos, t);

                    // Raycast to find ground height at this XZ position
                    float groundY = liftLinePos.y; // fallback
                    if (_mountainManager != null)
                    {
                        float? terrainY = _mountainManager.GetHeightAtWorldPos(liftLinePos);
                        if (terrainY.HasValue)
                            groundY = terrainY.Value;
                    }

                    // Place pillar at ground level — do NOT scale it
                    Vector3 groundPos = new Vector3(liftLinePos.x, groundY, liftLinePos.z);
                    var pillar = Instantiate(_pillarPrefab, groundPos, liftRot, pillarsParent.transform);
                    pillar.name = $"Pillar_{i}";
                    inst.Pillars.Add(pillar);

                    // Cable anchor at pillar wheel height (_cableY above ground)
                    float cableWorldY = groundY + _cableY;
                    anchorPoints.Add(new Vector3(liftLinePos.x, cableWorldY, liftLinePos.z));
                    groundPoints.Add(groundPos);
                }
            }

            // Top anchor (at turn wheel)
            anchorPoints.Add(topPos + Vector3.up * _cableY);
            groundPoints.Add(topPos);

            // ── Cables (per-segment between anchors) ────────────────────
            GameObject cablesParent = new GameObject("Cables");
            cablesParent.transform.SetParent(inst.Root.transform, false);

            inst.CableSegmentsUp = new List<GameObject>();
            inst.CableSegmentsDown = new List<GameObject>();

            if (_cablePrefab != null && anchorPoints.Count >= 2)
            {
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;

                for (int i = 0; i < anchorPoints.Count - 1; i++)
                {
                    Vector3 segStart = anchorPoints[i];
                    Vector3 segEnd = anchorPoints[i + 1];

                    Vector3 segDelta = segEnd - segStart;
                    float segLen = segDelta.magnitude;
                    if (segLen < 0.01f) continue;
                    Vector3 segDir = segDelta / segLen;
                    Quaternion segRot = Quaternion.LookRotation(segDir, Vector3.up);

                    // Up cable segment
                    var upSeg = SpawnCableSegment(cablesParent.transform,
                        segStart + right * _cableUpX,
                        segEnd + right * _cableUpX,
                        segRot, segLen, $"CableUp_{i}");
                    inst.CableSegmentsUp.Add(upSeg);

                    // Down cable segment
                    var downSeg = SpawnCableSegment(cablesParent.transform,
                        segStart + right * _cableDownX,
                        segEnd + right * _cableDownX,
                        segRot, segLen, $"CableDown_{i}");
                    inst.CableSegmentsDown.Add(downSeg);
                }
            }

            // Store anchor points and cable height for the chair mover
            inst.CableAnchorPoints = new List<Vector3>(anchorPoints);
            inst.PillarNativeHeight = _cableY;

            // ── Chairs ──────────────────────────────────────────────────
            inst.ChairsUpParent = new GameObject("ChairsUp");
            inst.ChairsUpParent.transform.SetParent(inst.Root.transform, false);

            inst.ChairsDownParent = new GameObject("ChairsDown");
            inst.ChairsDownParent.transform.SetParent(inst.Root.transform, false);

            inst.ChairsUp = new List<GameObject>();
            inst.ChairsDown = new List<GameObject>();

            if (_chairPrefab != null)
            {
                float effectiveChairSpacing = GetChairSpacingForType(liftType);
                int chairCount = Mathf.Max(1, Mathf.FloorToInt(length / effectiveChairSpacing));

                // Right perpendicular in world space (for lane offsets)
                Vector3 right2 = Vector3.Cross(Vector3.up, dir).normalized;
                if (right2.sqrMagnitude < 0.001f) right2 = Vector3.right;

                // Pre-compute cumulative distances for polyline sampling
                float totalPolyLen = 0f;
                float[] segLens = new float[anchorPoints.Count - 1];
                for (int s = 0; s < anchorPoints.Count - 1; s++)
                {
                    segLens[s] = Vector3.Distance(anchorPoints[s], anchorPoints[s + 1]);
                    totalPolyLen += segLens[s];
                }
                float[] cumT = new float[anchorPoints.Count];
                cumT[0] = 0f;
                float cum = 0f;
                for (int s = 0; s < segLens.Length; s++)
                {
                    cum += segLens[s];
                    cumT[s + 1] = (totalPolyLen > 0.01f) ? cum / totalPolyLen : (float)(s + 1) / segLens.Length;
                }

                for (int i = 0; i < chairCount; i++)
                {
                    float t = (float)i / chairCount;

                    // Sample the polyline at t to match cable path
                    Vector3 polyPos = SamplePolylineStatic(anchorPoints, cumT, t);

                    // Up lane: base → top
                    Vector3 upPos = polyPos + right2 * _chairUpX;
                    Quaternion upSegRot = GetPolylineRotStatic(anchorPoints, cumT, t, liftRot);
                    var chairUp = Instantiate(_chairPrefab, upPos, upSegRot, inst.ChairsUpParent.transform);
                    chairUp.name = $"Chair_{i}";
                    ApplyChairWidthScaleForLiftType(chairUp.transform, liftType);
                    inst.ChairsUp.Add(chairUp);

                    // Down lane: top → base (reversed along polyline)
                    Vector3 downPolyPos = SamplePolylineStatic(anchorPoints, cumT, 1f - t);
                    Vector3 downPos = downPolyPos + right2 * _chairDownX;
                    Quaternion downSegRot = GetPolylineRotStatic(anchorPoints, cumT, 1f - t, liftRot) * Quaternion.Euler(0f, 180f, 0f);
                    var chairDown = Instantiate(_chairPrefab, downPos, downSegRot, inst.ChairsDownParent.transform);
                    chairDown.name = $"Chair_{i}";
                    ApplyChairWidthScaleForLiftType(chairDown.transform, liftType);
                    inst.ChairsDown.Add(chairDown);
                }
            }

            return inst;
        }

        private static bool IsOneSeatLiftType(LiftType type)
        {
            return type == LiftType.OneSeatLowSpeed || type == LiftType.OneSeatHighSpeed;
        }

        private static bool IsTwoSeatLiftType(LiftType type)
        {
            return type == LiftType.TwoSeatLowSpeed || type == LiftType.TwoSeatHighSpeed;
        }

        /// <summary>
        /// Non-uniform local X scale on chair roots: narrower 1-seat, wider 2-seat (same prefab).
        /// </summary>
        private void ApplyChairWidthScaleForLiftType(Transform chairRoot, LiftType liftType)
        {
            if (chairRoot == null) return;

            float widthMult = 0f;
            if (IsTwoSeatLiftType(liftType) && _twoSeatChairWidthScale > 0f)
                widthMult = _twoSeatChairWidthScale;
            else if (IsOneSeatLiftType(liftType) && _oneSeatChairWidthScale > 0f)
                widthMult = _oneSeatChairWidthScale;

            if (widthMult <= 0f) return;

            Vector3 s = chairRoot.localScale;
            chairRoot.localScale = new Vector3(s.x * widthMult, s.y, s.z);
        }

        private float GetChairSpacingForType(LiftType type)
        {
            switch (type)
            {
                case LiftType.OneSeatLowSpeed:
                case LiftType.OneSeatHighSpeed:
                    return Mathf.Max(1f, _chairSpacing * _oneSeatSpacingMultiplier);
                case LiftType.TwoSeatLowSpeed:
                case LiftType.TwoSeatHighSpeed:
                    return Mathf.Max(1f, _chairSpacing * _oneSeatSpacingMultiplier);
                default:
                    return Mathf.Max(1f, _chairSpacing * _oneSeatSpacingMultiplier);
            }
        }

        private float GetChairSpeedForType(LiftType type)
        {
            switch (type)
            {
                case LiftType.OneSeatHighSpeed:
                case LiftType.TwoSeatHighSpeed:
                    return _lowSpeedChairSpeed * _highSpeedMultiplier;
                case LiftType.OneSeatLowSpeed:
                case LiftType.TwoSeatLowSpeed:
                default:
                    return _lowSpeedChairSpeed;
            }
        }

        /// <summary>
        /// Spawn a single cable segment between two anchor points.
        /// The cable mesh is scaled along local Z to fit the segment length.
        /// </summary>
        private GameObject SpawnCableSegment(Transform parent,
            Vector3 startPos, Vector3 endPos, Quaternion segRot, float segLength, string name)
        {
            var cable = Instantiate(_cablePrefab, startPos, segRot, parent);
            cable.name = name;

            // Scale the cable along its local Z (forward) to span the segment length.
            float meshLength = GetMeshZExtent(cable);
            if (meshLength > 0.001f)
            {
                float zScale = segLength / meshLength;
                cable.transform.localScale = new Vector3(
                    cable.transform.localScale.x,
                    cable.transform.localScale.y,
                    cable.transform.localScale.z * zScale
                );
            }
            else
            {
                Vector3 ls = cable.transform.localScale;
                cable.transform.localScale = new Vector3(ls.x, ls.y, segLength);
            }

            return cable;
        }

        /// <summary>
        /// Measure the Z extent of a mesh (bounds.size.z) to know how much
        /// to scale a cable to fill a segment.
        /// </summary>
        private float GetMeshZExtent(GameObject obj)
        {
            var mf = obj.GetComponent<MeshFilter>();
            if (mf == null) mf = obj.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                return mf.sharedMesh.bounds.size.z;
            }
            return 0f;
        }

        /// <summary>
        /// Measure the Y extent of a mesh (bounds.size.y) for pillar height scaling.
        /// </summary>
        private float GetMeshYExtent(GameObject obj)
        {
            var mf = obj.GetComponent<MeshFilter>();
            if (mf == null) mf = obj.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                return mf.sharedMesh.bounds.size.y * obj.transform.localScale.y;
            }
            // Try renderer bounds as fallback
            var r = obj.GetComponent<Renderer>();
            if (r == null) r = obj.GetComponentInChildren<Renderer>();
            if (r != null) return r.bounds.size.y;
            return 1f;
        }

        // ── Static polyline helpers (used for initial chair placement) ────

        /// <summary>
        /// Sample a position along a polyline defined by anchor points.
        /// cumT is the pre-computed cumulative normalised-distance array.
        /// </summary>
        private static Vector3 SamplePolylineStatic(List<Vector3> anchors, float[] cumT, float t)
        {
            t = Mathf.Clamp01(t);
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                float tEnd = cumT[i + 1];
                if (t <= tEnd || i == anchors.Count - 2)
                {
                    float tStart = cumT[i];
                    float segRange = tEnd - tStart;
                    float localT = (segRange > 0.0001f) ? (t - tStart) / segRange : 0f;
                    return Vector3.Lerp(anchors[i], anchors[i + 1], localT);
                }
            }
            return anchors[anchors.Count - 1];
        }

        /// <summary>
        /// Get forward rotation at a point along the polyline.
        /// </summary>
        private static Quaternion GetPolylineRotStatic(List<Vector3> anchors, float[] cumT, float t, Quaternion fallback)
        {
            t = Mathf.Clamp01(t);
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                if (t <= cumT[i + 1] || i == anchors.Count - 2)
                {
                    Vector3 segDir = (anchors[i + 1] - anchors[i]).normalized;
                    if (segDir.sqrMagnitude < 0.001f) return fallback;
                    return Quaternion.LookRotation(segDir, Vector3.up);
                }
            }
            return fallback;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Data holder for a single built lift's GameObjects
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Holds references to every part of a built lift so that the chair
    /// mover and the visualizer can access them.
    /// </summary>
    public class LiftInstance
    {
        public int LiftId;
        public GameObject Root;
        public GameObject BaseTurn;
        public GameObject TopTurn;
        public List<GameObject> CableSegmentsUp;
        public List<GameObject> CableSegmentsDown;
        public List<GameObject> Pillars;
        public GameObject ChairsUpParent;
        public GameObject ChairsDownParent;
        public List<GameObject> ChairsUp;
        public List<GameObject> ChairsDown;
        public float PillarNativeHeight; // cable/chair height above ground
        public List<Vector3> CableAnchorPoints; // world-space cable-level positions (base → top)
    }
}
