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
    /// Manages visitor flow, trail selection, and statistics.
    /// Pure C# - no Unity types.
    /// </summary>
    public class VisitorFlowSystem
    {
        private SkierDistribution _distribution;
        private Random _random;
        
        // Configuration
        public int RunsPerVisitor { get; set; } = 5;  // How many runs each visitor attempts
        
        public VisitorFlowSystem(int seed = 0)
        {
            _distribution = new SkierDistribution();
            _random = new Random(seed);
        }
        
        /// <summary>
        /// Simulates a day of visitor flow.
        /// Returns statistics about served/unserved visitors.
        /// </summary>
        public DayStats SimulateDay(
            int visitorCount,
            List<LiftData> lifts,
            List<TrailData> trails,
            ConnectionGraph connections)
        {
            DayStats stats = new DayStats();
            stats.TotalVisitors = visitorCount;
            
            // Generate visitors
            List<Skier> visitors = GenerateVisitors(visitorCount);
            
            // Track visitors by skill
            foreach (var visitor in visitors)
            {
                stats.VisitorsBySkill[visitor.Skill]++;
            }
            
            // Get accessible trails for each lift
            var accessibleTrailsMap = BuildAccessibleTrailsMap(lifts, trails, connections);
            
            // Flatten to get all unique accessible trails
            HashSet<int> allAccessibleTrailIds = new HashSet<int>();
            foreach (var trailList in accessibleTrailsMap.Values)
            {
                foreach (var trail in trailList)
                {
                    allAccessibleTrailIds.Add(trail.TrailId);
                }
            }
            
            List<TrailData> accessibleTrails = trails.Where(t => allAccessibleTrailIds.Contains(t.TrailId)).ToList();
            
            // Each visitor attempts multiple runs
            foreach (var visitor in visitors)
            {
                bool served = false;
                
                for (int run = 0; run < RunsPerVisitor; run++)
                {
                    var chosenTrail = ChooseTrail(visitor, accessibleTrails);
                    
                    if (chosenTrail != null)
                    {
                        visitor.RunsCompleted++;
                        stats.RunsByDifficulty[chosenTrail.Difficulty]++;
                        served = true;
                    }
                }
                
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
        /// Builds a map of LiftId -> accessible trails.
        /// </summary>
        private Dictionary<int, List<TrailData>> BuildAccessibleTrailsMap(
            List<LiftData> lifts,
            List<TrailData> trails,
            ConnectionGraph connections)
        {
            Dictionary<int, List<TrailData>> map = new Dictionary<int, List<TrailData>>();
            
            foreach (var lift in lifts)
            {
                var connectedTrailIds = connections.GetTrailsFromLift(lift.LiftId);
                var connectedTrails = trails.Where(t => connectedTrailIds.Contains(t.TrailId)).ToList();
                map[lift.LiftId] = connectedTrails;
            }
            
            return map;
        }
        
        /// <summary>
        /// Chooses a trail for a visitor based on skill, caps, and preferences.
        /// Returns null if no suitable trail found.
        /// </summary>
        private TrailData ChooseTrail(Skier visitor, List<TrailData> accessibleTrails)
        {
            if (accessibleTrails == null || accessibleTrails.Count == 0)
                return null;
            
            // Filter by hard caps (skill level restrictions)
            var allowedTrails = accessibleTrails
                .Where(t => _distribution.IsAllowed(visitor.Skill, t.Difficulty))
                .ToList();
            
            if (allowedTrails.Count == 0)
                return null;
            
            // Build weighted list
            List<(TrailData trail, float weight)> weightedTrails = new List<(TrailData, float)>();
            float totalWeight = 0f;
            
            foreach (var trail in allowedTrails)
            {
                float weight = _distribution.GetPreference(visitor.Skill, trail.Difficulty);
                if (weight > 0)
                {
                    weightedTrails.Add((trail, weight));
                    totalWeight += weight;
                }
            }
            
            if (weightedTrails.Count == 0)
                return null;
            
            // Weighted random selection
            float roll = (float)_random.NextDouble() * totalWeight;
            float cumulative = 0f;
            
            foreach (var (trail, weight) in weightedTrails)
            {
                cumulative += weight;
                if (roll <= cumulative)
                {
                    return trail;
                }
            }
            
            // Fallback (shouldn't happen, but return last trail)
            return weightedTrails[weightedTrails.Count - 1].trail;
        }
        
        /// <summary>
        /// Gets the skier distribution for customization.
        /// </summary>
        public SkierDistribution GetDistribution()
        {
            return _distribution;
        }
    }
}

