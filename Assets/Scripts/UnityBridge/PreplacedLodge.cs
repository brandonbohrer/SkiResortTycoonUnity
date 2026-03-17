using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Attach to any pre-placed lodge in the scene (e.g. the base lodge).
    /// Registers 8 snap points at the lodge footprint so lifts and trails can connect.
    /// </summary>
    public class PreplacedLodge : MonoBehaviour
    {
        [Header("Lodge Settings")]
        [SerializeField] private int _capacity = 20;
        [SerializeField] private float _restDurationSeconds = 30f;
        [SerializeField] private float _snapRadius = 50f;
        [Tooltip("How far from the lodge center to place the 8 snap points.")]
        [SerializeField] private float _footprintRadius = 25f;

        [Header("Snap Perimeter")]
        [Tooltip("Half-size of the rectangle of snap points around the lodge.")]
        [SerializeField] private float _perimeterHalfSize = 30f;
        [Tooltip("Spacing between snap points along each edge.")]
        [SerializeField] private float _perimeterSpacing = 5f;

        [Header("Amenities")]
        [SerializeField] private bool _hasBathroom = true;
        [SerializeField] private bool _hasFood = true;
        [SerializeField] private bool _hasRest = true;

        private bool _snapPointsRegistered;

        void Start()
        {
            Invoke(nameof(Bootstrap), 0.15f);
        }

        void Update()
        {
            // Retry every frame until LiftBuilder.Connectivity exists (init-order safety).
            if (_snapPointsRegistered) return;
            var facility = GetComponent<LodgeFacility>();
            if (facility == null) return;
            var liftBuilder = FindObjectOfType<LiftBuilder>();
            if (liftBuilder?.Connectivity == null) return;

            RegisterSnapPoints(liftBuilder);
            liftBuilder.Connectivity.RebuildConnections();
            _snapPointsRegistered = true;
        }

        private void Bootstrap()
        {
            var facility = GetComponent<LodgeFacility>();
            if (facility == null)
                facility = gameObject.AddComponent<LodgeFacility>();

            facility.Initialize(_snapRadius);
            facility.SetCapacity(_capacity);
            facility.SetRestDuration(_restDurationSeconds);

            if (GetComponent<SelectableStructure>() == null)
            {
                var selectable = gameObject.AddComponent<SelectableStructure>();
                selectable.InitializeAsLodge(facility);
            }

            if (LodgeManager.Instance != null)
                LodgeManager.Instance.RegisterLodge(facility);

            var liftBuilder = FindObjectOfType<LiftBuilder>();
            if (liftBuilder?.Connectivity != null)
            {
                RegisterSnapPoints(liftBuilder);
                liftBuilder.Connectivity.RebuildConnections();
                _snapPointsRegistered = true;
            }

            Debug.Log($"[PreplacedLodge] Registered '{gameObject.name}' as lodge (capacity {_capacity})");
        }

        /// <summary>Call after a save is loaded from the main menu to force snap point registration.</summary>
        public void EnsureSnapPointsRegistered()
        {
            var liftBuilder = FindObjectOfType<LiftBuilder>();
            if (liftBuilder?.Connectivity == null) return;
            RegisterSnapPoints(liftBuilder);
            liftBuilder.Connectivity.RebuildConnections();
            _snapPointsRegistered = true;
        }

        private void RegisterSnapPoints(LiftBuilder liftBuilder)
        {
            var facility = GetComponent<LodgeFacility>();
            int ownerId = facility != null ? facility.GetInstanceID() : GetInstanceID();
            string ownerName = $"Lodge_{ownerId}";
            var registry = liftBuilder.Connectivity.Registry;
            Vector3 center = transform.position;
            Quaternion rotation = transform.rotation;

            // 8 points at the lodge footprint — same as what was working before, no GroundToTerrain
            Vector3[] dirs = {
                Vector3.forward,
                (Vector3.forward + Vector3.right).normalized,
                Vector3.right,
                (-Vector3.forward + Vector3.right).normalized,
                -Vector3.forward,
                (-Vector3.forward - Vector3.right).normalized,
                -Vector3.right,
                (Vector3.forward - Vector3.right).normalized
            };
            foreach (var dir in dirs)
            {
                Vector3 edgePos = center + (rotation * dir) * _footprintRadius;
                edgePos.y = center.y;
                var p = MountainManager.ToVector3f(edgePos);
                registry.Register(new SnapPoint(SnapPointType.BuildingEntrance, p, ownerId, ownerName));
                registry.Register(new SnapPoint(SnapPointType.BaseSpawn,        p, ownerId, ownerName));
            }

            // Rectangle perimeter — use GroundToTerrain since these are farther out on the slope
            float h = _perimeterHalfSize;
            float step = Mathf.Max(_perimeterSpacing, 1f);
            int steps = Mathf.Max(2, Mathf.RoundToInt(h * 2f / step));
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Lerp(-h, h, (float)i / steps);
                RegisterPerimeterPoint(registry, center + new Vector3(t,  0f,  h), center.y, ownerId, ownerName);
                RegisterPerimeterPoint(registry, center + new Vector3(t,  0f, -h), center.y, ownerId, ownerName);
                RegisterPerimeterPoint(registry, center + new Vector3( h, 0f,  t), center.y, ownerId, ownerName);
                RegisterPerimeterPoint(registry, center + new Vector3(-h, 0f,  t), center.y, ownerId, ownerName);
            }
        }

        private static void RegisterPerimeterPoint(Core.SnapRegistry registry, Vector3 pos, float centerY, int ownerId, string ownerName)
        {
            pos.y = centerY;
            var p = MountainManager.ToVector3f(pos);
            registry.Register(new SnapPoint(SnapPointType.BaseSpawn,        p, ownerId, ownerName));
            registry.Register(new SnapPoint(SnapPointType.BuildingEntrance, p, ownerId, ownerName));
        }
    }
}
