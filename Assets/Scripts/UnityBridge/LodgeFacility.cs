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
        private readonly Dictionary<int, float> _realTimeCounters = new Dictionary<int, float>();
        private LodgePricing _pricing;
        private SimulationRunner _cachedSimRunner;
        
        private const float MAX_REAL_TIME_IN_LODGE = 90f;

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

        public void SetCapacity(int capacity) => _capacity = capacity;
        public void SetRestDuration(float seconds) => _restDurationSeconds = seconds;

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
            _realTimeCounters[skierId] = 0f;

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
            _realTimeCounters.Remove(skierId);
        }

        // ── Lifecycle ───────────────────────────────────────────────────

        void Awake()
        {
            SetupSnapZone();
        }

        void Update()
        {
            if (_restTimers.Count == 0) return;

            float realDt = Time.deltaTime;
            float dt = realDt;

            if (_cachedSimRunner == null)
                _cachedSimRunner = FindObjectOfType<SimulationRunner>();

            if (_cachedSimRunner?.Sim?.TimeController != null)
            {
                dt = _cachedSimRunner.Sim.TimeController.GetEffectiveDeltaTime(realDt);
            }

            List<int> finished = null;
            List<KeyValuePair<int, float>> snapshot = new List<KeyValuePair<int, float>>(_restTimers);

            foreach (var kvp in snapshot)
            {
                int skierId = kvp.Key;

                if (dt > 0f)
                {
                    float remaining = kvp.Value - dt;
                    if (remaining <= 0f)
                    {
                        if (finished == null) finished = new List<int>();
                        finished.Add(skierId);
                        continue;
                    }
                    _restTimers[skierId] = remaining;
                }

                if (_realTimeCounters.ContainsKey(skierId))
                {
                    _realTimeCounters[skierId] += realDt;
                    if (_realTimeCounters[skierId] >= MAX_REAL_TIME_IN_LODGE)
                    {
                        if (finished == null) finished = new List<int>();
                        if (!finished.Contains(skierId))
                        {
                            finished.Add(skierId);
                            if (_enableDebugLogs) Debug.LogWarning($"[Lodge] Skier {skierId} force-exited after {MAX_REAL_TIME_IN_LODGE}s real time");
                        }
                    }
                }
            }

            if (finished != null)
            {
                foreach (int id in finished)
                {
                    _occupiedSlots.Remove(id);
                    _restTimers.Remove(id);
                    _realTimeCounters.Remove(id);
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
