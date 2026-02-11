using UnityEngine;
using System;
using System.Collections.Generic;
using SkiResortTycoon.Core;
using SkiResortTycoon.ScriptableObjects;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Singleton MonoBehaviour that tracks live skier traffic across the resort.
    /// Owns a ResortTrafficState (pure C#) and exposes C# events that
    /// SkierVisualizer fires at phase transitions.
    ///
    /// The decision engine reads deficit/crowding values from here.
    /// </summary>
    public class ResortTrafficManager : MonoBehaviour
    {
        public static ResortTrafficManager Instance { get; private set; }
        
        [Header("Config")]
        [SerializeField] private SkierAIConfig _config;
        
        [Header("References")]
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private TrailDrawer _trailDrawer;
        
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;
        
        /// <summary>
        /// The underlying traffic state (pure C#, no Unity deps).
        /// SkierDecisionEngine reads deficit/crowding from this.
        /// </summary>
        public ResortTrafficState State { get; private set; }
        
        /// <summary>
        /// Whether the traffic system has been initialized with trail/lift data.
        /// </summary>
        public bool IsInitialized { get; private set; }
        
        // ─────────────────────────────────────────────────────────────
        //  Events (fired by SkierVisualizer, consumed by State)
        // ─────────────────────────────────────────────────────────────
        
        /// <summary>Fired when a skier starts skiing a trail. Args: skierId, trailId</summary>
        public event Action<int, int> TrailEntered;
        
        /// <summary>Fired when a skier finishes a trail. Args: skierId, trailId</summary>
        public event Action<int, int> TrailCompleted;
        
        /// <summary>Fired when a skier boards a lift. Args: skierId, liftId</summary>
        public event Action<int, int> LiftEntered;
        
        /// <summary>Fired when a skier exits a lift at the top. Args: skierId, liftId</summary>
        public event Action<int, int> LiftExited;
        
        // ─────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ResortTrafficManager] Duplicate instance destroyed.");
                Destroy(this);
                return;
            }
            Instance = this;
            State = new ResortTrafficState();
        }
        
        private void OnEnable()
        {
            // Subscribe to our own events to update the state
            TrailEntered += HandleTrailEntered;
            TrailCompleted += HandleTrailCompleted;
            LiftEntered += HandleLiftEntered;
            LiftExited += HandleLiftExited;
        }
        
        private void OnDisable()
        {
            TrailEntered -= HandleTrailEntered;
            TrailCompleted -= HandleTrailCompleted;
            LiftEntered -= HandleLiftEntered;
            LiftExited -= HandleLiftExited;
        }
        
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        
        // ─────────────────────────────────────────────────────────────
        //  Initialization (called by SkierVisualizer after AI init)
        // ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Initializes the traffic system with all current trails and lifts.
        /// Call after trails/lifts are built or when the topology changes.
        /// </summary>
        public void Initialize(List<TrailData> allTrails, List<LiftData> allLifts, SkierAIConfig config)
        {
            _config = config;
            State.Clear();
            
            float capacityPerMeter = config != null ? config.trailCapacityPerMeter : 50f;
            float minCapacity = config != null ? config.minimumTrailCapacity : 2f;
            float liftDivisor = config != null ? config.liftCapacityDivisor : 200f;
            
            foreach (var trail in allTrails)
            {
                if (!trail.IsValid) continue;
                float capacity = Mathf.Max(trail.WorldLength / capacityPerMeter, minCapacity);
                State.RegisterTrail(trail.TrailId, capacity);
                
                if (_enableDebugLogs)
                    Debug.Log($"[Traffic] Trail {trail.TrailId} ({trail.Difficulty}): length={trail.WorldLength:F0}m, capacity={capacity:F1}");
            }
            
            foreach (var lift in allLifts)
            {
                if (!lift.IsValid) continue;
                float capacity = Mathf.Max(lift.Capacity / liftDivisor, 1f);
                State.RegisterLift(lift.LiftId, capacity);
                
                if (_enableDebugLogs)
                    Debug.Log($"[Traffic] Lift {lift.LiftId}: rawCapacity={lift.Capacity}, slots={capacity:F1}");
            }
            
            IsInitialized = true;
            if (_enableDebugLogs) Debug.Log($"[Traffic] Initialized: {allTrails.Count} trails, {allLifts.Count} lifts");
        }
        
        /// <summary>
        /// Reinitializes when new infrastructure is built.
        /// Preserves occupancy for existing trails/lifts.
        /// </summary>
        public void Reinitialize(List<TrailData> allTrails, List<LiftData> allLifts)
        {
            if (_config != null)
                Initialize(allTrails, allLifts, _config);
        }
        
        // ─────────────────────────────────────────────────────────────
        //  Event firing (called by SkierVisualizer)
        // ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Called immediately when a skier DECIDES on a trail (before they start skiing).
        /// Updates pending intent so the next decider sees an updated landscape.
        /// </summary>
        public void FireTrailIntended(int skierId, int trailId)
        {
            State.OnTrailIntended(skierId, trailId);
        }
        
        /// <summary>
        /// Called immediately when a skier DECIDES on a lift (before they board).
        /// </summary>
        public void FireLiftIntended(int skierId, int liftId)
        {
            State.OnLiftIntended(skierId, liftId);
        }
        
        public void FireTrailEntered(int skierId, int trailId)
        {
            TrailEntered?.Invoke(skierId, trailId);
        }
        
        public void FireTrailCompleted(int skierId, int trailId)
        {
            TrailCompleted?.Invoke(skierId, trailId);
        }
        
        public void FireLiftEntered(int skierId, int liftId)
        {
            LiftEntered?.Invoke(skierId, liftId);
        }
        
        public void FireLiftExited(int skierId, int liftId)
        {
            LiftExited?.Invoke(skierId, liftId);
        }
        
        // ─────────────────────────────────────────────────────────────
        //  Internal event handlers
        // ─────────────────────────────────────────────────────────────
        
        private void HandleTrailEntered(int skierId, int trailId)
        {
            State.OnTrailEntered(skierId, trailId);
            if (_enableDebugLogs)
                Debug.Log($"[Traffic] Skier {skierId} entered trail {trailId} (occ: {State.GetTrailCrowding(trailId):P0})");
        }
        
        private void HandleTrailCompleted(int skierId, int trailId)
        {
            State.OnTrailCompleted(skierId, trailId);
        }
        
        private void HandleLiftEntered(int skierId, int liftId)
        {
            State.OnLiftEntered(skierId, liftId);
        }
        
        private void HandleLiftExited(int skierId, int liftId)
        {
            State.OnLiftExited(skierId, liftId);
        }
    }
}
