using UnityEngine;
using System.Collections.Generic;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Unity bridge that runs the core simulation.
    /// This is the ONLY script that can use UnityEngine types.
    /// </summary>
    public class SimulationRunner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private TrailDrawer _trailDrawer;
        [SerializeField] private SkierVisualizer _skierVisualizer;
        
        [Header("Performance Test Mode")]
        [SerializeField] private bool _enablePerformanceTestMode = false;
        [SerializeField] private bool _performanceTestDay1Only = true;
        [SerializeField] private int _performanceStartingMoney = 2000000;
        [SerializeField] private int _performanceTargetDay1Skiers = 300;

        private Simulation _sim;
        private DailyFinancialRecord _lastFinancialRecord;
        private DayStats _lastDayStats;
        private bool _systemsWired = false;
        private float _fairPriceRefreshTimer = 0f;
        private const float FAIR_PRICE_REFRESH_INTERVAL = 1f;
        private bool _performanceMoneyApplied = false;
        
        public Simulation Sim => _sim;
        public DailyFinancialRecord LastFinancialRecord => _lastFinancialRecord;
        public DayStats LastDayStats => _lastDayStats;
        
        void Awake()
        {
            _sim = new Simulation(timeSpeedMinutesPerSecond: 1.333f);
            Debug.Log($"Simulation started. Day {_sim.State.DayIndex}, Money: ${_sim.State.Money}");
        }
        
        void Update()
        {
            if (_sim == null) return;
            
            TryWireSystems();

            if (_systemsWired)
            {
                _fairPriceRefreshTimer += Time.deltaTime;
                if (_fairPriceRefreshTimer >= FAIR_PRICE_REFRESH_INTERVAL)
                {
                    _fairPriceRefreshTimer = 0f;
                    UpdateFairPrice();
                }
            }
            
            if (_skierVisualizer == null)
                _skierVisualizer = FindObjectOfType<SkierVisualizer>();
            if (_skierVisualizer != null)
                _sim.State.ActiveSkierCount = _skierVisualizer.ActiveSkierCount;
            else
                _sim.State.ActiveSkierCount = 0;

            ApplyPerformanceTestModeIfEnabled();

            // Update lodge count for visitor system
            _sim.State.LodgesBuilt = LodgeManager.Instance != null ? LodgeManager.Instance.AllLodges.Count : 0;

            bool dayEnded = _sim.Tick(Time.deltaTime);
            
            if (dayEnded)
            {
                HandleEndOfDay();
            }
        }
        
        private void TryWireSystems()
        {
            if (_systemsWired) return;
            
            if (_liftBuilder != null && _liftBuilder.LiftSystem != null &&
                _liftBuilder.Connectivity != null &&
                _trailDrawer != null && _trailDrawer.TrailSystem != null &&
                _trailDrawer.GridRenderer != null && _trailDrawer.GridRenderer.TerrainData != null)
            {
                _sim.SetSystems(
                    _liftBuilder.LiftSystem,
                    _trailDrawer.TrailSystem,
                    _liftBuilder.Connectivity.Connections
                );
                
                _sim.SetRegistryAndTerrain(
                    _liftBuilder.Connectivity.Registry,
                    _trailDrawer.GridRenderer.TerrainData
                );
                
                // Do initial fair price calculation
                UpdateFairPrice();
                
                _systemsWired = true;
                _fairPriceRefreshTimer = 0f;
                Debug.Log("[SimulationRunner] Systems wired to Simulation!");
            }
        }
        
        private void HandleEndOfDay()
        {
            int dayEnding = _sim.State.DayIndex;
            var powder = FindObjectOfType<PowderDayController>();
            if (powder != null)
                powder.OnPowderDayEnded(dayEnding);

            int visitorsToday = _sim.State.VisitorsToday;
            
            // Collect lodge data from the Unity layer
            int lodgeCount = 0;
            float lodgeRevenue = 0f;
            var lodgeAmenities = new List<LodgeAmenityInfo>();
            
            if (LodgeManager.Instance != null)
            {
                foreach (var lodge in LodgeManager.Instance.AllLodges)
                {
                    if (lodge == null) continue;
                    lodgeCount++;
                    lodgeRevenue += lodge.Pricing.TotalRevenue;
                    lodgeAmenities.Add(new LodgeAmenityInfo(
                        lodge.HasFood, lodge.HasBathroom, lodge.HasRest));
                }
            }
            
            // Count distinct trail difficulties
            int distinctDifficulties = CountDistinctDifficulties();
            
            // End day — EconomySystem handles all financial logic
            _lastFinancialRecord = _sim.EndDay(
                lodgeCount, lodgeRevenue, lodgeAmenities, distinctDifficulties);
            
            // Reset per-trail daily run counts for the new day
            if (ResortTrafficManager.Instance != null)
            {
                ResortTrafficManager.Instance.ResetDailyRunCounts();
            }
            
            // Reset lodge daily revenue for next day
            if (LodgeManager.Instance != null)
            {
                foreach (var lodge in LodgeManager.Instance.AllLodges)
                {
                    if (lodge != null)
                    {
                        lodge.Pricing.TotalRevenue = 0f;
                        lodge.Pricing.TotalVisits = 0;
                    }
                }
            }
            
            // Get detailed stats for logging
            if (_systemsWired)
            {
                _lastDayStats = _sim.VisitorFlow.SimulateDay(
                    visitorsToday,
                    _liftBuilder.LiftSystem.Lifts,
                    _trailDrawer.TrailSystem.Trails,
                    _liftBuilder.Connectivity.Registry,
                    _trailDrawer.GridRenderer.TerrainData
                );
                
                LogDetailedDayStats();
            }
            else
            {
                Debug.Log($"Day ended. Net Income: ${_lastFinancialRecord.NetIncome:N0}. " +
                          $"Money: ${_sim.State.Money:N0}. Day: {_sim.State.DayIndex}");
            }
            
            // Update fair price for next day (infrastructure may have changed)
            UpdateFairPrice();

            if (powder != null)
                powder.SyncPowderDayUi();
        }

        private void ApplyPerformanceTestModeIfEnabled()
        {
            if (_sim == null) return;

            bool dayGate = !_performanceTestDay1Only || _sim.State.DayIndex == 1;
            bool active = _enablePerformanceTestMode && dayGate;

            if (_enablePerformanceTestMode && !_performanceMoneyApplied)
            {
                _sim.State.Money = Mathf.Max(0, _performanceStartingMoney);
                _performanceMoneyApplied = true;
            }

            _sim.VisitorSystem.ForcedTargetActiveSkiers = active
                ? (int?)Mathf.Max(0, _performanceTargetDay1Skiers)
                : null;
        }
        
        /// <summary>
        /// Recalculates fair price from current infrastructure.
        /// Called at system wire-up and after each day ends.
        /// </summary>
        private void UpdateFairPrice()
        {
            int liftCount = _liftBuilder != null && _liftBuilder.LiftSystem != null
                ? _liftBuilder.LiftSystem.Lifts.Count : 0;
            int trailCount = _trailDrawer != null && _trailDrawer.TrailSystem != null
                ? _trailDrawer.TrailSystem.Trails.Count : 0;
            
            var lodgeAmenities = new List<LodgeAmenityInfo>();
            if (LodgeManager.Instance != null)
            {
                foreach (var lodge in LodgeManager.Instance.AllLodges)
                {
                    if (lodge != null)
                    {
                        lodgeAmenities.Add(new LodgeAmenityInfo(
                            lodge.HasFood, lodge.HasBathroom, lodge.HasRest));
                    }
                }
            }
            
            int distinctDifficulties = CountDistinctDifficulties();
            
            _sim.EconomySystem.UpdateFairPrice(
                liftCount, trailCount, lodgeAmenities, distinctDifficulties);
        }
        
        private int CountDistinctDifficulties()
        {
            if (_trailDrawer == null || _trailDrawer.TrailSystem == null)
                return 0;
            
            var seen = new HashSet<TrailDifficulty>();
            foreach (var trail in _trailDrawer.TrailSystem.Trails)
            {
                if (trail != null && trail.IsValid)
                    seen.Add(trail.Difficulty);
            }
            return seen.Count;
        }
        
        private void LogDetailedDayStats()
        {
            if (_lastFinancialRecord == null) return;
            
            var r = _lastFinancialRecord;
            
            Debug.Log("========================================");
            Debug.Log($"DAY {r.DayIndex} ENDED");
            Debug.Log("========================================");
            Debug.Log($"Visitors: {r.VisitorCount}");
            Debug.Log($"Fair Price: ${r.FairPrice:N0}  |  Ticket Price: ${r.TicketPrice:N0}  |  Ratio: {r.TicketPrice / System.Math.Max(1f, r.FairPrice):F2}");
            Debug.Log("────── Revenue ──────");
            Debug.Log($"  Tickets:  ${r.TicketRevenue:N0}");
            Debug.Log($"  Lodge:    ${r.LodgeRevenue:N0}");
            Debug.Log($"  TOTAL:    ${r.TotalRevenue:N0}");
            Debug.Log("────── Expenses ──────");
            Debug.Log($"  Lifts:    ${r.LiftExpenses:N0}");
            Debug.Log($"  Lodges:   ${r.LodgeExpenses:N0}");
            Debug.Log($"  Trails:   ${r.TrailExpenses:N0}");
            Debug.Log($"  TOTAL:    ${r.TotalExpenses:N0}");
            Debug.Log("────── Bottom Line ──────");
            Debug.Log($"  Net Income: ${r.NetIncome:N0}");
            Debug.Log($"  Money:      ${_sim.State.Money:N0}");
            Debug.Log($"  Satisfaction: {_sim.Satisfaction.Satisfaction:F1}/100 (multiplier: {_sim.Satisfaction.GetVisitorMultiplier():F2}x)");
            Debug.Log($"  Demand (price): {_sim.EconomySystem.GetDemandMultiplier():F2}x");
            Debug.Log($"  Demand (fill): {_sim.VisitorSystem.LastFillRate:F2}x");
            Debug.Log($"  Demand progression boost: {_sim.VisitorSystem.LastProgressionBoost:F2}x");
            Debug.Log($"  Demand raw target: {_sim.VisitorSystem.LastRawTarget:F1} | smoothed target: {_sim.State.SmoothedTargetActiveSkiers:F1}");
            Debug.Log($"  Demand momentum: {_sim.State.DemandMomentum:F2} | strong-day streak: {_sim.State.ConsecutiveStrongServiceDays}");
            Debug.Log("========================================");
            
            // Log visitor flow stats if available
            if (_lastDayStats != null)
            {
                Debug.Log($"Served: {_lastDayStats.ServedVisitors}/{_lastDayStats.TotalVisitors} " +
                          $"({GetPercentage(_lastDayStats.ServedVisitors, _lastDayStats.TotalVisitors):F0}%)");
                Debug.Log($"Skill access: {_lastDayStats.AvgSkillAccess:P0} | Preferred access: {_lastDayStats.AvgPreferredAccess:P0}");
            }

            if (_skierVisualizer != null && _skierVisualizer.GuestStats != null)
            {
                var gs = _skierVisualizer.GuestStats;
                Debug.Log($"Guest factors -> Price: {gs.AvgPriceFairness:F2}, Access: {gs.AvgTrailAccess:F2}, Food/Needs: {gs.AvgFoodSatisfaction:F2}, Wait: {gs.AvgWaitTimeSatisfaction:F2}");
            }
        }
        
        private float GetPercentage(int value, int total)
        {
            if (total == 0) return 0f;
            return (value / (float)total) * 100f;
        }
    }
}