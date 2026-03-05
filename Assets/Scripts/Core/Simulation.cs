using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Main simulation orchestrator.
    /// Pure C# - no Unity types.
    /// Coordinates all systems and manages the day cycle.
    /// </summary>
    public class Simulation
    {
        private SimulationState _state;
        private TimeSystem _timeSystem;
        private VisitorSystem _visitorSystem;
        private EconomySystem _economySystem;
        private TimeController _timeController;
        private VisitorFlowSystem _visitorFlow;
        private SatisfactionSystem _satisfaction;
        
        // References to other systems (set after initialization)
        private LiftSystem _liftSystem;
        private TrailSystem _trailSystem;
        private ConnectionGraph _connections;
        
        // At Speed1x: 1 day = 6 minutes (1.333 game minutes per real second)
        public Simulation(float timeSpeedMinutesPerSecond = 1.333f)
        {
            _state = new SimulationState();
            _timeSystem = new TimeSystem(timeSpeedMinutesPerSecond);
            _visitorSystem = new VisitorSystem();
            _economySystem = new EconomySystem();
            _timeController = new TimeController();
            _visitorFlow = new VisitorFlowSystem();
            _satisfaction = new SatisfactionSystem();
        }
        
        // Public accessors
        public SimulationState State => _state;
        public TimeSystem TimeSystem => _timeSystem;
        public VisitorSystem VisitorSystem => _visitorSystem;
        public EconomySystem EconomySystem => _economySystem;
        public TimeController TimeController => _timeController;
        public VisitorFlowSystem VisitorFlow => _visitorFlow;
        public SatisfactionSystem Satisfaction => _satisfaction;
        
        /// <summary>
        /// Sets references to lift, trail, and connection systems.
        /// Call this from Unity bridge after systems are initialized.
        /// </summary>
        public void SetSystems(LiftSystem lifts, TrailSystem trails, ConnectionGraph connections)
        {
            _liftSystem = lifts;
            _trailSystem = trails;
            _connections = connections;
        }
        
        /// <summary>
        /// Advances the simulation by deltaTime.
        /// Returns true when the day has ended.
        /// </summary>
        public bool Tick(float deltaTime)
        {
            // Apply time control (pause and speed multiplier)
            float effectiveDeltaTime = _timeController.GetEffectiveDeltaTime(deltaTime);
            
            // Update infrastructure counts from systems
            if (_liftSystem != null && _trailSystem != null)
            {
                _state.LiftsBuilt = _liftSystem.Lifts.Count;
                _state.TrailsBuilt = _trailSystem.Trails.Count;
            }
            
            // Update visitor system multipliers and lodge count
            _visitorSystem.SatisfactionMultiplier = _satisfaction.GetVisitorMultiplier();
            _visitorSystem.PriceMultiplier = _economySystem.GetDemandMultiplier();
            _visitorSystem.LodgeCount = _state.LodgesBuilt;
            
            // Only advance time and visitors if the day is still active
            if (!_timeSystem.IsDayOver(_state))
            {
                // Accumulate visitors during the day
                _visitorSystem.Tick(_state, effectiveDeltaTime, _timeSystem.SpeedMinutesPerSecond);
            }
            
            // Advance time and check if day just ended
            return _timeSystem.Tick(_state, effectiveDeltaTime);
        }
        
        /// <summary>
        /// Handles end-of-day logic using EconomySystem.
        /// 
        /// The caller (SimulationRunner) must provide lodge data because
        /// lodge facilities live in the Unity bridge layer.
        /// </summary>
        /// <param name="lodgeCount">Number of active lodges.</param>
        /// <param name="lodgeRevenue">Total lodge revenue collected today.</param>
        /// <param name="lodgeAmenities">Per-lodge amenity info for valuation.</param>
        /// <param name="distinctDifficultyCount">Number of distinct trail difficulty types.</param>
        /// <returns>The DailyFinancialRecord for this day.</returns>
        public DailyFinancialRecord EndDay(
            int lodgeCount,
            float lodgeRevenue,
            List<LodgeAmenityInfo> lodgeAmenities,
            int distinctDifficultyCount)
        {
            // Update fair price before processing financials
            int liftCount = _liftSystem != null ? _liftSystem.Lifts.Count : 0;
            int trailCount = _trailSystem != null ? _trailSystem.Trails.Count : 0;
            _economySystem.UpdateFairPrice(liftCount, trailCount, lodgeAmenities, distinctDifficultyCount);
            
            // Simulate visitor flow (if systems are initialized)
            DayStats dayStats = null;
            
            if (_liftSystem != null && _trailSystem != null && _connections != null)
            {
                var registry = GetSnapRegistry();
                var terrain = GetTerrainData();
                
                if (registry != null && terrain != null)
                {
                    dayStats = _visitorFlow.SimulateDay(
                        _state.VisitorsToday,
                        _liftSystem.Lifts,
                        _trailSystem.Trails,
                        registry,
                        terrain
                    );
                    
                    // Update satisfaction based on performance
                    _satisfaction.UpdateSatisfaction(dayStats);
                }
            }
            
            // Process all financials via EconomySystem
            var record = _economySystem.ProcessEndOfDay(
                _state, liftCount, trailCount, lodgeCount, lodgeRevenue);
            
            // Populate DayStats with financial data (if we have stats)
            if (dayStats != null)
            {
                dayStats.TicketRevenue = record.TicketRevenue;
                dayStats.LodgeRevenue = record.LodgeRevenue;
                dayStats.TotalRevenue = record.TotalRevenue;
                dayStats.TotalExpenses = record.TotalExpenses;
                dayStats.NetIncome = record.NetIncome;
            }
            
            // Increment day counter
            _state.DayIndex++;

            // Reset for next day
            _state.VisitorsToday    = 0;
            _state.TodayRevenue     = 0f;
            _state.TodayExpenses    = 0f;
            _state.TodayLodgeRevenue = 0f;
            _visitorSystem.ResetDay();
            _timeSystem.ResetToOpen(_state);
            
            return record;
        }
        
        // Helper to get registry (will be set by Unity bridge)
        private SnapRegistry _snapRegistry;
        private TerrainData _terrainData;
        
        public void SetRegistryAndTerrain(SnapRegistry registry, TerrainData terrain)
        {
            _snapRegistry = registry;
            _terrainData = terrain;
        }
        
        private SnapRegistry GetSnapRegistry()
        {
            return _snapRegistry;
        }
        
        private TerrainData GetTerrainData()
        {
            return _terrainData;
        }
        
        /// <summary>
        /// Gets the last day's statistics (for logging).
        /// Call after EndDay().
        /// </summary>
        public DayStats GetLastDayStats()
        {
            if (_liftSystem != null && _trailSystem != null && _connections != null &&
                _snapRegistry != null && _terrainData != null)
            {
                return _visitorFlow.SimulateDay(
                    _state.VisitorsToday,
                    _liftSystem.Lifts,
                    _trailSystem.Trails,
                    _snapRegistry,
                    _terrainData
                );
            }
            return null;
        }
    }
}
