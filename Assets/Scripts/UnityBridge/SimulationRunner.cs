using UnityEngine;
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
        
        private Simulation _sim;
        private int _lastEndOfDayRevenue = 0;
        private DayStats _lastDayStats;
        private bool _systemsWired = false;
        
        public Simulation Sim => _sim;
        public int LastEndOfDayRevenue => _lastEndOfDayRevenue;
        public DayStats LastDayStats => _lastDayStats;
        
        void Awake()
        {
            // Initialize the simulation with new time speed
            // At Speed1x: 1 day = 6 minutes (1.333 game minutes per real second)
            _sim = new Simulation(timeSpeedMinutesPerSecond: 1.333f);
            
            Debug.Log($"Simulation started. Day {_sim.State.DayIndex}, Money: ${_sim.State.Money}");
        }
        
        void Update()
        {
            if (_sim == null) return;
            
            // Wire up systems once they're initialized (lazy)
            TryWireSystems();
            
            // Advance the simulation
            bool dayEnded = _sim.Tick(Time.deltaTime);
            
            // If the day just ended, handle end-of-day logic
            if (dayEnded)
            {
                HandleEndOfDay();
            }
        }
        
        private void TryWireSystems()
        {
            if (_systemsWired) return;
            
            // Check if all systems are ready
            if (_liftBuilder != null && _liftBuilder.LiftSystem != null &&
                _liftBuilder.Connectivity != null &&
                _trailDrawer != null && _trailDrawer.TrailSystem != null &&
                _trailDrawer.GridRenderer != null && _trailDrawer.GridRenderer.TerrainData != null)
            {
                // Wire up the simulation with the systems
                _sim.SetSystems(
                    _liftBuilder.LiftSystem,
                    _trailDrawer.TrailSystem,
                    _liftBuilder.Connectivity.Connections
                );
                
                // Wire up registry and terrain for pathfinding
                _sim.SetRegistryAndTerrain(
                    _liftBuilder.Connectivity.Registry,
                    _trailDrawer.GridRenderer.TerrainData
                );
                
                _systemsWired = true;
                Debug.Log("[SimulationRunner] Systems wired to Simulation!");
            }
        }
        
        private void HandleEndOfDay()
        {
            // Store stats before ending day (visitor count is about to reset)
            int visitorsToday = _sim.State.VisitorsToday;
            
            // End day and get revenue (this also calculates stats internally)
            _lastEndOfDayRevenue = _sim.EndDay();
            
            // Get detailed stats for logging (re-simulate to get stats)
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
                // Simple fallback log
                Debug.Log($"Day ended. Revenue: ${_lastEndOfDayRevenue}. Money now: ${_sim.State.Money}. Day: {_sim.State.DayIndex}");
            }
        }
        
        private void LogDetailedDayStats()
        {
            if (_lastDayStats == null) return;
            
            Debug.Log("========================================");
            Debug.Log($"DAY {_sim.State.DayIndex - 1} ENDED");
            Debug.Log("========================================");
            Debug.Log($"Total Visitors: {_lastDayStats.TotalVisitors}");
            Debug.Log($"Served: {_lastDayStats.ServedVisitors} ({GetPercentage(_lastDayStats.ServedVisitors, _lastDayStats.TotalVisitors)}%)");
            Debug.Log($"Unserved: {_lastDayStats.UnservedVisitors} ({GetPercentage(_lastDayStats.UnservedVisitors, _lastDayStats.TotalVisitors)}%)");
            Debug.Log("----------------------------------------");
            
            // Breakdown by skill
            Debug.Log("Visitors by Skill Level:");
            foreach (SkillLevel skill in System.Enum.GetValues(typeof(SkillLevel)))
            {
                int total = _lastDayStats.VisitorsBySkill[skill];
                int served = _lastDayStats.ServedBySkill[skill];
                int unserved = _lastDayStats.UnservedBySkill[skill];
                Debug.Log($"  {skill}: {total} total, {served} served, {unserved} unserved");
            }
            Debug.Log("----------------------------------------");
            
            // Breakdown by difficulty
            Debug.Log("Runs by Trail Difficulty:");
            foreach (TrailDifficulty diff in System.Enum.GetValues(typeof(TrailDifficulty)))
            {
                int runs = _lastDayStats.RunsByDifficulty[diff];
                if (runs > 0)
                {
                    Debug.Log($"  {diff}: {runs} runs");
                }
            }
            Debug.Log("----------------------------------------");
            
            // Economics
            Debug.Log($"Revenue: ${_lastEndOfDayRevenue} (${_sim.DollarsPerVisitor} per served visitor)");
            Debug.Log($"Money: ${_sim.State.Money}");
            Debug.Log($"Satisfaction: {_sim.Satisfaction.Satisfaction:F2}");
            Debug.Log("========================================");
        }
        
        private float GetPercentage(int value, int total)
        {
            if (total == 0) return 0f;
            return (value / (float)total) * 100f;
        }
    }
}