using System;
using System.Collections.Generic;
using System.Linq;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Tracks visitor flow statistics for a single day.
    /// </summary>
    public class DayStats
    {
        public int TotalVisitors { get; set; }
        public int ServedVisitors { get; set; }
        public int UnservedVisitors { get; set; }
        
        // Breakdown by skill
        public Dictionary<SkillLevel, int> VisitorsBySkill { get; set; }
        public Dictionary<SkillLevel, int> ServedBySkill { get; set; }
        public Dictionary<SkillLevel, int> UnservedBySkill { get; set; }
        
        // Breakdown by difficulty
        public Dictionary<TrailDifficulty, int> RunsByDifficulty { get; set; }
        
        public DayStats()
        {
            VisitorsBySkill = new Dictionary<SkillLevel, int>();
            ServedBySkill = new Dictionary<SkillLevel, int>();
            UnservedBySkill = new Dictionary<SkillLevel, int>();
            RunsByDifficulty = new Dictionary<TrailDifficulty, int>();
            
            // Initialize all to 0
            foreach (SkillLevel skill in Enum.GetValues(typeof(SkillLevel)))
            {
                VisitorsBySkill[skill] = 0;
                ServedBySkill[skill] = 0;
                UnservedBySkill[skill] = 0;
            }
            
            foreach (TrailDifficulty diff in Enum.GetValues(typeof(TrailDifficulty)))
            {
                RunsByDifficulty[diff] = 0;
            }
        }
    }
    
    /// <summary>
    /// Manages visitor flow using individual skier pathfinding.
    /// Pure C# - no Unity types.
    /// </summary>
    public class VisitorFlowSystem
    {
        private SkierDistribution _distribution;
        private Random _random;
        private NetworkGraph _network;
        private SkierPathfinder _pathfinder;
        
        // Configuration
        public int RunsPerVisitor { get; set; } = 5;  // How many runs each visitor attempts
        public int RandomSeed { get; set; } = 0;
        
        public VisitorFlowSystem(int seed = 0)
        {
            _distribution = new SkierDistribution();
            RandomSeed = seed;
            _random = new Random(seed);
        }
        
        /// <summary>
        /// Simulates a day of visitor flow using individual skier pathfinding.
        /// Returns statistics about served/unserved visitors.
        /// </summary>
        public DayStats SimulateDay(
            int visitorCount,
            List<LiftData> lifts,
            List<TrailData> trails,
            SnapRegistry registry,
            TerrainData terrain)
        {
            DayStats stats = new DayStats();
            stats.TotalVisitors = visitorCount;
            
            // Build network graph
            _network = new NetworkGraph(registry, terrain);
            _network.BuildGraph();
            
            // Create pathfinder
            _pathfinder = new SkierPathfinder(_network, registry, _distribution, trails, _random);
            
            // Find reachable trails once (optimization)
            List<TrailData> reachableTrails = _pathfinder.FindReachableTrails();
            
            // Generate visitors
            List<Skier> visitors = GenerateVisitors(visitorCount);
            
            // Track visitors by skill
            foreach (var visitor in visitors)
            {
                stats.VisitorsBySkill[visitor.Skill]++;
            }
            
            // Simulate each visitor individually
            foreach (var visitor in visitors)
            {
                bool served = SimulateSkier(visitor, reachableTrails, stats);
                
                visitor.WasServed = served;
                
                if (served)
                {
                    stats.ServedVisitors++;
                    stats.ServedBySkill[visitor.Skill]++;
                }
                else
                {
                    stats.UnservedVisitors++;
                    stats.UnservedBySkill[visitor.Skill]++;
                }
            }
            
            return stats;
        }
        
        /// <summary>
        /// Simulates a single skier's day.
        /// Returns true if served (found at least one valid destination).
        /// </summary>
        private bool SimulateSkier(Skier skier, List<TrailData> reachableTrails, DayStats stats)
        {
            // Filter by skill caps
            var allowedTrails = reachableTrails
                .Where(t => _distribution.IsAllowed(skier.Skill, t.Difficulty))
                .ToList();
            
            if (allowedTrails.Count == 0)
            {
                // No trails this skier can access
                return false;
            }
            
            // Skier attempts multiple runs
            bool servedAtLeastOnce = false;
            
            for (int run = 0; run < RunsPerVisitor; run++)
            {
                // Choose destination trail (weighted random)
                TrailData destination = _pathfinder.ChooseDestinationTrail(skier, reachableTrails);
                
                if (destination == null)
                    continue;
                
                // Find path to destination
                List<TrailData> pathTrails = _pathfinder.FindPathToTrail(destination);
                
                if (pathTrails.Count == 0)
                    continue;
                
                // "Ski" all trails along the path
                foreach (var trail in pathTrails)
                {
                    skier.RunsCompleted++;
                    stats.RunsByDifficulty[trail.Difficulty]++;
                }
                
                servedAtLeastOnce = true;
            }
            
            return servedAtLeastOnce;
        }
        
        /// <summary>
        /// Generates a list of visitors based on skill distribution.
        /// </summary>
        private List<Skier> GenerateVisitors(int count)
        {
            List<Skier> visitors = new List<Skier>();
            
            for (int i = 0; i < count; i++)
            {
                SkillLevel skill = _distribution.GetRandomSkillLevel(_random);
                visitors.Add(new Skier(i, skill));
            }
            
            return visitors;
        }
        
        /// <summary>
        /// Gets the skier distribution for customization.
        /// </summary>
        public SkierDistribution GetDistribution()
        {
            return _distribution;
        }
        
        /// <summary>
        /// Sets a new random seed (for testing/determinism).
        /// </summary>
        public void SetSeed(int seed)
        {
            RandomSeed = seed;
            _random = new Random(seed);
        }
    }
}
