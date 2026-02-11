using System;
using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Tracks live traffic state across the resort and computes deficit scores.
    /// Pure C# — no Unity dependencies.
    ///
    /// Each trail/lift has a capacity (derived from physical length) and an
    /// occupancy counter. The DEFICIT is how under-used something is relative
    /// to its share of total capacity:
    ///
    ///   targetShare = capacity / totalCapacity
    ///   currentShare = occupancy / totalSkiersOnMountain
    ///   deficit = targetShare - currentShare
    ///
    /// Positive deficit = under-used → bonus in skier decision-making
    /// Negative deficit = over-used → penalty
    ///
    /// This is self-balancing: as skiers flow toward high-deficit trails,
    /// the deficit decreases and the bonus fades.
    /// </summary>
    public class ResortTrafficState
    {
        /// <summary>
        /// Per-entity traffic info.
        /// </summary>
        public class TrafficInfo
        {
            public int EntityId;
            public float Capacity;   // derived from length (trails) or LiftData.Capacity (lifts)
            public int Occupancy;    // current skier count
            public int PendingIntent; // skiers who have DECIDED to come here but haven't started yet
            public float Deficit;    // targetShare - currentShare, updated on every event
            
            /// <summary>
            /// Effective load including both active skiers and pending intents.
            /// This is what the decision engine should use for crowding.
            /// </summary>
            public int EffectiveLoad => Occupancy + PendingIntent;
            
            /// <summary>
            /// Crowding ratio including pending intents: (occupancy + pending) / capacity.
            /// 0 = empty, 1 = at capacity, >1 = over capacity.
            /// </summary>
            public float Crowding => Capacity > 0f ? (float)EffectiveLoad / Capacity : 0f;
        }
        
        private Dictionary<int, TrafficInfo> _trailTraffic = new Dictionary<int, TrafficInfo>();
        private Dictionary<int, TrafficInfo> _liftTraffic = new Dictionary<int, TrafficInfo>();
        
        private float _totalTrailCapacity;
        private float _totalLiftCapacity;
        private int _totalSkiersOnTrails;
        private int _totalSkiersOnLifts;
        
        // ── Recent choice memory (anti-herding) ──
        // Tracks the last N trail/lift choices so the decision engine can penalize
        // options that were just picked by the skiers immediately before.
        private const int RECENT_MEMORY_SIZE = 8;
        private Queue<int> _recentTrailChoices = new Queue<int>();
        private Queue<int> _recentLiftChoices = new Queue<int>();
        
        /// <summary>Total skiers currently on any trail or lift.</summary>
        public int TotalSkiersOnMountain => _totalSkiersOnTrails + _totalSkiersOnLifts;
        
        // ─────────────────────────────────────────────────────────────
        //  Initialization
        // ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Registers a trail with its capacity.
        /// Call once per trail during initialization (and when new trails are built).
        /// </summary>
        public void RegisterTrail(int trailId, float capacity)
        {
            if (!_trailTraffic.ContainsKey(trailId))
            {
                _trailTraffic[trailId] = new TrafficInfo
                {
                    EntityId = trailId,
                    Capacity = capacity,
                    Occupancy = 0,
                    Deficit = 0f
                };
            }
            else
            {
                _trailTraffic[trailId].Capacity = capacity;
            }
            RecalculateTotalCapacity();
        }
        
        /// <summary>
        /// Registers a lift with its capacity.
        /// </summary>
        public void RegisterLift(int liftId, float capacity)
        {
            if (!_liftTraffic.ContainsKey(liftId))
            {
                _liftTraffic[liftId] = new TrafficInfo
                {
                    EntityId = liftId,
                    Capacity = capacity,
                    Occupancy = 0,
                    Deficit = 0f
                };
            }
            else
            {
                _liftTraffic[liftId].Capacity = capacity;
            }
            RecalculateTotalCapacity();
        }
        
        /// <summary>
        /// Clears all registered trails and lifts. Call when rebuilding the resort.
        /// </summary>
        public void Clear()
        {
            _trailTraffic.Clear();
            _liftTraffic.Clear();
            _totalTrailCapacity = 0f;
            _totalLiftCapacity = 0f;
            _totalSkiersOnTrails = 0;
            _totalSkiersOnLifts = 0;
            _recentTrailChoices.Clear();
            _recentLiftChoices.Clear();
        }
        
        // ─────────────────────────────────────────────────────────────
        //  Event handlers (called by ResortTrafficManager)
        // ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Called when a skier DECIDES to take a trail (before they start skiing).
        /// Increments pending intent so subsequent deciders see updated state.
        /// </summary>
        public void OnTrailIntended(int skierId, int trailId)
        {
            if (_trailTraffic.TryGetValue(trailId, out var info))
            {
                info.PendingIntent++;
                ComputeAllDeficits();
            }
            // Track recent choice for anti-herding
            _recentTrailChoices.Enqueue(trailId);
            if (_recentTrailChoices.Count > RECENT_MEMORY_SIZE)
                _recentTrailChoices.Dequeue();
        }
        
        /// <summary>
        /// Called when a skier DECIDES to take a lift (before they board).
        /// </summary>
        public void OnLiftIntended(int skierId, int liftId)
        {
            if (_liftTraffic.TryGetValue(liftId, out var info))
            {
                info.PendingIntent++;
                ComputeAllDeficits();
            }
            _recentLiftChoices.Enqueue(liftId);
            if (_recentLiftChoices.Count > RECENT_MEMORY_SIZE)
                _recentLiftChoices.Dequeue();
        }
        
        public void OnTrailEntered(int skierId, int trailId)
        {
            if (_trailTraffic.TryGetValue(trailId, out var info))
            {
                // Convert pending intent to occupancy (net zero change to EffectiveLoad)
                info.PendingIntent = Math.Max(0, info.PendingIntent - 1);
                info.Occupancy++;
                _totalSkiersOnTrails++;
                ComputeAllDeficits();
            }
        }
        
        public void OnTrailCompleted(int skierId, int trailId)
        {
            if (_trailTraffic.TryGetValue(trailId, out var info))
            {
                info.Occupancy = Math.Max(0, info.Occupancy - 1);
                _totalSkiersOnTrails = Math.Max(0, _totalSkiersOnTrails - 1);
                ComputeAllDeficits();
            }
        }
        
        public void OnLiftEntered(int skierId, int liftId)
        {
            if (_liftTraffic.TryGetValue(liftId, out var info))
            {
                // Convert pending intent to occupancy
                info.PendingIntent = Math.Max(0, info.PendingIntent - 1);
                info.Occupancy++;
                _totalSkiersOnLifts++;
                ComputeAllDeficits();
            }
        }
        
        public void OnLiftExited(int skierId, int liftId)
        {
            if (_liftTraffic.TryGetValue(liftId, out var info))
            {
                info.Occupancy = Math.Max(0, info.Occupancy - 1);
                _totalSkiersOnLifts = Math.Max(0, _totalSkiersOnLifts - 1);
                ComputeAllDeficits();
            }
        }
        
        // ─────────────────────────────────────────────────────────────
        //  Queries (called by SkierDecisionEngine)
        // ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Returns the deficit for a trail. Positive = under-used, negative = over-used.
        /// Range is roughly [-1, 1] but can exceed if heavily skewed.
        /// Returns 0 if trail is not registered.
        /// </summary>
        public float GetTrailDeficit(int trailId)
        {
            if (_trailTraffic.TryGetValue(trailId, out var info))
                return info.Deficit;
            return 0f;
        }
        
        /// <summary>
        /// Returns the deficit for a lift.
        /// </summary>
        public float GetLiftDeficit(int liftId)
        {
            if (_liftTraffic.TryGetValue(liftId, out var info))
                return info.Deficit;
            return 0f;
        }
        
        /// <summary>
        /// Returns the crowding ratio for a trail: occupancy / capacity.
        /// 0 = empty, 1 = at capacity, >1 = over capacity.
        /// </summary>
        public float GetTrailCrowding(int trailId)
        {
            if (_trailTraffic.TryGetValue(trailId, out var info))
                return info.Crowding;
            return 0f;
        }
        
        /// <summary>
        /// Returns the crowding ratio for a lift.
        /// </summary>
        public float GetLiftCrowding(int liftId)
        {
            if (_liftTraffic.TryGetValue(liftId, out var info))
                return info.Crowding;
            return 0f;
        }
        
        /// <summary>
        /// Returns how many of the last N skiers chose this trail (0 to RECENT_MEMORY_SIZE).
        /// Used by the decision engine as a herding penalty.
        /// </summary>
        public float GetTrailRecentPopularity(int trailId)
        {
            if (_recentTrailChoices.Count == 0) return 0f;
            int count = 0;
            foreach (var id in _recentTrailChoices)
                if (id == trailId) count++;
            return (float)count / _recentTrailChoices.Count;
        }
        
        /// <summary>
        /// Returns how many of the last N skiers chose this lift (0 to 1).
        /// </summary>
        public float GetLiftRecentPopularity(int liftId)
        {
            if (_recentLiftChoices.Count == 0) return 0f;
            int count = 0;
            foreach (var id in _recentLiftChoices)
                if (id == liftId) count++;
            return (float)count / _recentLiftChoices.Count;
        }
        
        /// <summary>
        /// Returns all trail traffic info (for debug display).
        /// </summary>
        public IEnumerable<TrafficInfo> GetAllTrailTraffic() => _trailTraffic.Values;
        
        /// <summary>
        /// Returns all lift traffic info (for debug display).
        /// </summary>
        public IEnumerable<TrafficInfo> GetAllLiftTraffic() => _liftTraffic.Values;
        
        // ─────────────────────────────────────────────────────────────
        //  Internal
        // ─────────────────────────────────────────────────────────────
        
        private void RecalculateTotalCapacity()
        {
            _totalTrailCapacity = 0f;
            foreach (var t in _trailTraffic.Values)
                _totalTrailCapacity += t.Capacity;
            
            _totalLiftCapacity = 0f;
            foreach (var l in _liftTraffic.Values)
                _totalLiftCapacity += l.Capacity;
        }
        
        /// <summary>
        /// Recomputes deficit for every trail and lift.
        /// Called after every event to keep deficits current.
        ///
        /// Trail deficit and lift deficit are computed independently
        /// (a trail's deficit doesn't depend on lift occupancy and vice versa).
        /// </summary>
        private void ComputeAllDeficits()
        {
            // Count total effective load (occupancy + pending intents)
            int totalTrailLoad = 0;
            foreach (var info in _trailTraffic.Values)
                totalTrailLoad += info.EffectiveLoad;
            
            int totalLiftLoad = 0;
            foreach (var info in _liftTraffic.Values)
                totalLiftLoad += info.EffectiveLoad;
            
            // Trail deficits — use EffectiveLoad so pending intents count
            if (_totalTrailCapacity > 0f && totalTrailLoad > 0)
            {
                foreach (var info in _trailTraffic.Values)
                {
                    float targetShare = info.Capacity / _totalTrailCapacity;
                    float currentShare = (float)info.EffectiveLoad / totalTrailLoad;
                    info.Deficit = targetShare - currentShare;
                }
            }
            else
            {
                // No skiers or no capacity: deficits based purely on capacity share
                foreach (var info in _trailTraffic.Values)
                {
                    info.Deficit = _totalTrailCapacity > 0f
                        ? info.Capacity / _totalTrailCapacity
                        : 0f;
                }
            }
            
            // Lift deficits — use EffectiveLoad
            if (_totalLiftCapacity > 0f && totalLiftLoad > 0)
            {
                foreach (var info in _liftTraffic.Values)
                {
                    float targetShare = info.Capacity / _totalLiftCapacity;
                    float currentShare = (float)info.EffectiveLoad / totalLiftLoad;
                    info.Deficit = targetShare - currentShare;
                }
            }
            else
            {
                foreach (var info in _liftTraffic.Values)
                {
                    info.Deficit = _totalLiftCapacity > 0f
                        ? info.Capacity / _totalLiftCapacity
                        : 0f;
                }
            }
        }
    }
}
