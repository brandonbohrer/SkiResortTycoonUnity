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
        
        // ── Prefab references (assign in Inspector) ─────────────────────
        [Header("Lift Prefabs")]
        [SerializeField] private GameObject _turnPrefab;    // SM_Prop_Lift_Turn_01
        [SerializeField] private GameObject _pillarPrefab;  // SM_Prop_Lift_Pillar_01
        [SerializeField] private GameObject _cablePrefab;   // SM_Prop_Lift_Cable_01
        [SerializeField] private GameObject _chairPrefab;   // SM_Prop_Lift_Chair_01

        [Header("Spacing")]
        [SerializeField] private float _pillarSpacing = 20f;   // metres between pillars
        [SerializeField] private float _chairSpacing = 8f;     // metres between chairs per lane
        [SerializeField] private float _corridorWidth = 8f;    // tree-clearing width

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

            var inst = CreateLiftHierarchy(basePos, topPos, $"LiftRoot_{lift.LiftId}");
            inst.LiftId = lift.LiftId;
            _builtLifts[lift.LiftId] = inst;

            // Attach chair mover component
            var mover = inst.Root.GetComponent<LiftChairMover>();
            if (mover == null) mover = inst.Root.AddComponent<LiftChairMover>();
            mover.Initialise(inst, basePos, topPos, _chairUpX, _chairDownX, _chairY, _simulationRunner);

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
        public void UpdatePreview(Vector3 basePos, Vector3 topPos)
        {
            // Tear down old preview
            DestroyPreview();

            _preview = CreateLiftHierarchy(basePos, topPos, "LiftPreview");
            // No chair mover on preview (static snapshot)
        }

        /// <summary>Destroy the live preview.</summary>
        public void DestroyPreview()
        {
            if (_preview != null && _preview.Root != null)
            {
                Destroy(_preview.Root);
                _preview = null;
            }
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

        private LiftInstance CreateLiftHierarchy(Vector3 basePos, Vector3 topPos, string rootName)
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

            // ── Cables parent ───────────────────────────────────────────
            GameObject cablesParent = new GameObject("Cables");
            cablesParent.transform.SetParent(inst.Root.transform, false);

            if (_cablePrefab != null)
            {
                // Up cable
                inst.CableUp = SpawnCable(cablesParent.transform, basePos, topPos,
                    liftRot, dir, length, _cableUpX, _cableY, "CablesUp");

                // Down cable (identical mesh, different lane offset)
                inst.CableDown = SpawnCable(cablesParent.transform, basePos, topPos,
                    liftRot, dir, length, _cableDownX, _cableY, "CablesDown");
            }

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
                    Vector3 pos = Vector3.Lerp(basePos, topPos, t);

                    var pillar = Instantiate(_pillarPrefab, pos, liftRot, pillarsParent.transform);
                    pillar.name = $"Pillar_{i}";
                    inst.Pillars.Add(pillar);
                }
            }

            // ── Chairs ──────────────────────────────────────────────────
            inst.ChairsUpParent = new GameObject("ChairsUp");
            inst.ChairsUpParent.transform.SetParent(inst.Root.transform, false);

            inst.ChairsDownParent = new GameObject("ChairsDown");
            inst.ChairsDownParent.transform.SetParent(inst.Root.transform, false);

            inst.ChairsUp = new List<GameObject>();
            inst.ChairsDown = new List<GameObject>();

            if (_chairPrefab != null)
            {
                int chairCount = Mathf.Max(1, Mathf.FloorToInt(length / _chairSpacing));

                // Right perpendicular in world space (for lane offsets)
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;

                for (int i = 0; i < chairCount; i++)
                {
                    float t = (float)i / chairCount;

                    // Up lane: base → top
                    Vector3 upPos = Vector3.Lerp(basePos, topPos, t)
                                    + right * _chairUpX
                                    + Vector3.up * _chairY;
                    var chairUp = Instantiate(_chairPrefab, upPos, liftRot, inst.ChairsUpParent.transform);
                    chairUp.name = $"Chair_{i}";
                    inst.ChairsUp.Add(chairUp);

                    // Down lane: top → base (rotated 180° on Y)
                    Vector3 downPos = Vector3.Lerp(topPos, basePos, t)
                                      + right * _chairDownX
                                      + Vector3.up * _chairY;
                    Quaternion downRot = liftRot * Quaternion.Euler(0f, 180f, 0f);
                    var chairDown = Instantiate(_chairPrefab, downPos, downRot, inst.ChairsDownParent.transform);
                    chairDown.name = $"Chair_{i}";
                    inst.ChairsDown.Add(chairDown);
                }
            }

            return inst;
        }

        /// <summary>
        /// Spawn a single cable mesh scaled to span from base to top,
        /// offset laterally and vertically.
        /// </summary>
        private GameObject SpawnCable(Transform parent, Vector3 basePos, Vector3 topPos,
            Quaternion liftRot, Vector3 dir, float length, float lateralX, float verticalY, string name)
        {
            // Cable parent (holds the offset)
            GameObject cableParent = new GameObject(name);
            cableParent.transform.SetParent(parent, false);

            // World-space perpendicular
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;

            // Cable start point (at base) + offsets
            Vector3 cableStart = basePos + right * lateralX + Vector3.up * verticalY;

            var cable = Instantiate(_cablePrefab, cableStart, liftRot, cableParent.transform);
            cable.name = "SM_Prop_Lift_Cable_01";

            // Scale the cable along its local Z (forward) to span the full length.
            // The cable mesh is positioned at its START (not center), so we scale from there.
            float meshLength = GetMeshZExtent(cable);
            if (meshLength > 0.001f)
            {
                float zScale = length / meshLength;
                cable.transform.localScale = new Vector3(
                    cable.transform.localScale.x,
                    cable.transform.localScale.y,
                    cable.transform.localScale.z * zScale
                );
            }
            else
            {
                // Fallback: just set Z scale equal to length (assumes 1-unit mesh)
                Vector3 ls = cable.transform.localScale;
                cable.transform.localScale = new Vector3(ls.x, ls.y, length);
            }

            return cableParent;
        }

        /// <summary>
        /// Measure the Z extent of a mesh (bounds.size.z) to know how much
        /// to scale a cable to fill the lift span.
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
        public GameObject CableUp;
        public GameObject CableDown;
        public List<GameObject> Pillars;
        public GameObject ChairsUpParent;
        public GameObject ChairsDownParent;
        public List<GameObject> ChairsUp;
        public List<GameObject> ChairsDown;
    }
}
