using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Attach to any pre-placed lodge in the scene (e.g. the base lodge).
    /// On Start, bootstraps the same LodgeFacility + SelectableStructure +
    /// LodgeManager registration that LodgeBuilder.PlaceLodge does for
    /// player-built lodges, so skiers treat it identically.
    /// </summary>
    public class PreplacedLodge : MonoBehaviour
    {
        [Header("Lodge Settings")]
        [SerializeField] private int _capacity = 20;
        [SerializeField] private float _restDurationSeconds = 30f;
        [SerializeField] private float _snapRadius = 20f;
        [SerializeField] private float _footprintRadius = 15f;

        [Header("Amenities")]
        [SerializeField] private bool _hasBathroom = true;
        [SerializeField] private bool _hasFood = true;
        [SerializeField] private bool _hasRest = true;

        void Start()
        {
            Invoke(nameof(Bootstrap), 0.15f);
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
                RegisterFootprintSnapPoints(liftBuilder, facility);
                liftBuilder.Connectivity.RebuildConnections();
            }

            Debug.Log($"[PreplacedLodge] Registered '{gameObject.name}' as lodge (capacity {_capacity})");
        }

        private void RegisterFootprintSnapPoints(LiftBuilder liftBuilder, LodgeFacility facility)
        {
            float radius = facility.FootprintRadius;
            int ownerId = facility.GetInstanceID();
            string ownerName = $"Lodge_{ownerId}";
            Vector3 center = transform.position;
            Quaternion rotation = transform.rotation;

            Vector3[] directions = new Vector3[]
            {
                Vector3.forward,
                (Vector3.forward + Vector3.right).normalized,
                Vector3.right,
                (-Vector3.forward + Vector3.right).normalized,
                -Vector3.forward,
                (-Vector3.forward - Vector3.right).normalized,
                -Vector3.right,
                (Vector3.forward - Vector3.right).normalized
            };

            foreach (var dir in directions)
            {
                Vector3 rotatedDir = rotation * dir;
                Vector3 edgePos = center + rotatedDir * radius;
                edgePos.y = center.y;

                var snap = new SnapPoint(
                    SnapPointType.BuildingEntrance,
                    MountainManager.ToVector3f(edgePos),
                    ownerId,
                    ownerName);
                liftBuilder.Connectivity.Registry.Register(snap);
            }
        }
    }
}
