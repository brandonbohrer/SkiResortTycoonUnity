using UnityEngine;
using System.Collections.Generic;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Runtime component added automatically to placed lodges by LodgeBuilder.
    /// Handles skier entry/exit, capacity, rest timers, amenities, pricing,
    /// and the snap-zone trigger.
    /// You never need to put this on a prefab yourself.
    /// </summary>
    public class LodgeFacility : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField] private int _capacity = 10;
        [SerializeField] private float _restDurationSeconds = 30f; // real-time seconds skiers stay inside

        [Header("Amenities")]
        [SerializeField] private bool _hasBathroom = true;
        [SerializeField] private bool _hasFood = true;
        [SerializeField] private bool _hasRest = true;

        [Header("Snap Zone")]
        [SerializeField] private float _snapRadius = 20f;
        
        [Header("Footprint")]
        [SerializeField] private float _footprintRadius = 15f; // Cleared area around lodge

        [Header("Debug")]
        [SerializeField] private bool _showDebugGizmos = true;
        [SerializeField] private bool _enableDebugLogs = false;

        private SphereCollider _snapZoneTrigger;
        private readonly HashSet<int> _occupiedSlots = new HashSet<int>();
        private readonly Dictionary<int, float> _restTimers = new Dictionary<int, float>();
        private LodgePricing _pricing;

        // ── Public API ──────────────────────────────────────────────────

        public int CurrentOccupancy => _occupiedSlots.Count;
        public int Capacity => _capacity;
        public bool IsFull => CurrentOccupancy >= _capacity;
        public Vector3 Position => transform.position;
        public float SnapRadius => _snapRadius;
        public float FootprintRadius => _footprintRadius;
        
        // ── Amenities ───────────────────────────────────────────────────
        public bool HasBathroom => _hasBathroom;
        public bool HasFood => _hasFood;
        public bool HasRest => _hasRest;
        
        // ── Pricing ─────────────────────────────────────────────────────
        public LodgePricing Pricing
        {
            get
            {
                if (_pricing == null)
                    _pricing = new LodgePricing();
                return _pricing;
            }
        }

        /// <summary>
        /// Called by LodgeBuilder right after instantiation.
        /// </summary>
        public void Initialize(float snapRadius)
        {
            _snapRadius = snapRadius;
            SetupSnapZone();
        }

        /// <summary>
        /// Try to check a skier into the lodge. Returns false if full.
        /// </summary>
        public bool TryEnterLodge(int skierId)
        {
            if (IsFull)
            {
                if (_enableDebugLogs) Debug.Log($"[Lodge] Skier {skierId} rejected – full ({CurrentOccupancy}/{_capacity})");
                return false;
            }

            _occupiedSlots.Add(skierId);
            _restTimers[skierId] = _restDurationSeconds;

            if (_enableDebugLogs) Debug.Log($"[Lodge] Skier {skierId} entered. {CurrentOccupancy}/{_capacity}");
            return true;
        }

        /// <summary>
        /// Is skier still inside?
        /// </summary>
        public bool ContainsSkier(int skierId) => _occupiedSlots.Contains(skierId);

        /// <summary>
        /// Immediately remove a skier (e.g. if their GameObject is destroyed).
        /// </summary>
        public void ForceExitSkier(int skierId)
        {
            _occupiedSlots.Remove(skierId);
            _restTimers.Remove(skierId);
        }

        // ── Lifecycle ───────────────────────────────────────────────────

        void Awake()
        {
            SetupSnapZone();
        }

        void Update()
        {
            if (_restTimers.Count == 0) return;

            // Use effective delta time so lodge timers respect pause and game speed
            float dt = Time.deltaTime;
            SimulationRunner simRunner = FindObjectOfType<SimulationRunner>();
            if (simRunner?.Sim?.TimeController != null)
            {
                dt = simRunner.Sim.TimeController.GetEffectiveDeltaTime(Time.deltaTime);
            }
            
            if (dt <= 0f) return; // Paused

            // Collect finished skiers first, THEN mutate -- avoids
            // InvalidOperationException from modifying dict during iteration.
            List<int> finished = null;
            List<KeyValuePair<int, float>> snapshot = new List<KeyValuePair<int, float>>(_restTimers);

            foreach (var kvp in snapshot)
            {
                float remaining = kvp.Value - dt;
                if (remaining <= 0f)
                {
                    if (finished == null) finished = new List<int>();
                    finished.Add(kvp.Key);
                }
                else
                {
                    _restTimers[kvp.Key] = remaining;
                }
            }

            if (finished != null)
            {
                foreach (int id in finished)
                {
                    _occupiedSlots.Remove(id);
                    _restTimers.Remove(id);
                    if (_enableDebugLogs) Debug.Log($"[Lodge] Skier {id} finished resting. {CurrentOccupancy}/{_capacity}");
                }
            }
        }

        // ── Internals ───────────────────────────────────────────────────

        private void SetupSnapZone()
        {
            _snapZoneTrigger = GetComponent<SphereCollider>();
            if (_snapZoneTrigger == null)
                _snapZoneTrigger = gameObject.AddComponent<SphereCollider>();

            _snapZoneTrigger.radius = _snapRadius;
            _snapZoneTrigger.isTrigger = true;
        }

        // ── Gizmos ──────────────────────────────────────────────────────

        void OnDrawGizmos()
        {
            if (!_showDebugGizmos) return;
            Gizmos.color = IsFull ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, _snapRadius);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawSphere(transform.position, _snapRadius);
        }

        // ── Cleanup ─────────────────────────────────────────────────────

        void OnDestroy()
        {
            // Unregister from manager
            if (LodgeManager.Instance != null)
                LodgeManager.Instance.UnregisterLodge(this);

            // Remove snap point
            LiftBuilder lb = FindObjectOfType<LiftBuilder>();
            if (lb?.Connectivity != null)
            {
                lb.Connectivity.Registry.UnregisterByOwner(GetInstanceID());
                lb.Connectivity.RebuildConnections();
            }
        }
    }
}
