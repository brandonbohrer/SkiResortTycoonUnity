using System;
using System.Collections.Generic;
using System.Linq;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// AI decision-making system for skiers.
    /// Handles goal selection and route planning.
    /// Pure C# - no Unity types.
    /// </summary>
    public class SkierAI
    {
        private NetworkGraph _network;
        private SnapRegistry _registry;
        private SkierDistribution _distribution;
        private List<TrailData> _allTrails;
        private List<LiftData> _allLifts;
        private Random _random;
        
        // Satisfaction impact values
        public static class SatisfactionModifiers
        {
            public const float PreferredTrailBonus = 0.05f;     // Skiing on preferred difficulty
            public const float WrongDifficultyPenalty = -0.1f;  // Forced to ski wrong difficulty
            public const float LongWaitPenalty = -0.02f;        // Per 5 minutes of wait
            public const float SuccessfulRunBonus = 0.02f;      // Completed any run
            public const float NoPathPenalty = -0.15f;          // Couldn't find path to destination
        }
        
        public SkierAI(NetworkGraph network, SnapRegistry registry, SkierDistribution distribution, 
                       List<TrailData> trails, List<LiftData> lifts, Random random)
        {
            _network = network;
            _registry = registry;
            _distribution = distribution;
            _allTrails = trails;
            _allLifts = lifts;
            _random = random;
        }
        
        /// <summary>
        /// Chooses a new goal for a skier and plans the route to achieve it.
        /// </summary>
        public SkierGoal PlanNewGoal(Skier skier)
        {
            // Check if skier wants to leave
            if (!skier.WantsToKeepSkiing())
            {
                return CreateReturnToBaseGoal();
            }
            
            // Future: Check for urgent needs (bathroom, hunger)
            // For now, always default to skiing
            
            // Find a preferred trail and plan a route to it
            TrailData targetTrail = ChooseDestinationTrail(skier);
            
            if (targetTrail == null)
            {
                // No valid trails for this skier
                skier.Needs.AdjustSatisfaction(SatisfactionModifiers.NoPathPenalty);
                return CreateReturnToBaseGoal();
            }
            
            // Plan route from current position to target trail
            List<PathStep> path = PlanRouteToTrail(skier, targetTrail);
            
            if (path == null || path.Count == 0)
            {
                // Can't reach the desired trail
                skier.Needs.AdjustSatisfaction(SatisfactionModifiers.NoPathPenalty * 0.5f);
                
                // Try to find any reachable trail
                path = FindAnyReachablePath(skier);
                
                if (path == null || path.Count == 0)
                {
                    return CreateReturnToBaseGoal();
                }
            }
            
            return SkierGoal.CreateSkiGoal(targetTrail.TrailId, path);
        }
        
        /// <summary>
        /// Chooses a destination trail based on skier preferences and skill.
        /// </summary>
        public TrailData ChooseDestinationTrail(Skier skier)
        {
            // Get trails this skier is allowed to ski (hard caps)
            var allowedTrails = _allTrails
                .Where(t => t.IsValid && _distribution.IsAllowed(skier.Skill, t.Difficulty))
                .ToList();
            
            if (allowedTrails.Count == 0)
                return null;
            
            // Build weighted list based on preferences
            List<(TrailData trail, float weight)> weightedTrails = new List<(TrailData, float)>();
            float totalWeight = 0f;
            
            foreach (var trail in allowedTrails)
            {
                float weight = _distribution.GetPreference(skier.Skill, trail.Difficulty);
                
                // Slight penalty for trails already skied (encourage variety)
                // Future enhancement: track per-skier trail history
                
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
            
            // Fallback
            return weightedTrails[weightedTrails.Count - 1].trail;
        }
        
        /// <summary>
        /// Plans a route from the skier's current position to a target trail.
        /// Uses BFS through the network graph.
        /// </summary>
        public List<PathStep> PlanRouteToTrail(Skier skier, TrailData targetTrail)
        {
            // Determine starting point based on skier state
            SnapPoint? startPoint = GetSkierStartPoint(skier);
            
            if (!startPoint.HasValue)
            {
                return null;
            }
            
            // Find the trail's start snap point
            var trailStarts = _registry.GetByType(SnapPointType.TrailStart)
                .Where(s => s.OwnerId == targetTrail.TrailId)
                .ToList();
            
            if (trailStarts.Count == 0)
            {
                return null;
            }
            
            SnapPoint targetPoint = trailStarts[0];
            
            // BFS to find path
            var path = FindPathBFS(startPoint.Value, targetPoint);
            
            if (path == null)
            {
                return null;
            }
            
            // Convert snap point path to PathSteps
            return ConvertToPathSteps(path);
        }
        
        /// <summary>
        /// Gets the snap point representing the skier's current position.
        /// Returns null if no valid start point found.
        /// </summary>
        private SnapPoint? GetSkierStartPoint(Skier skier)
        {
            switch (skier.CurrentState)
            {
                case SkierState.AtBase:
                    var basePoints = _registry.GetByType(SnapPointType.BaseSpawn);
                    return basePoints.Count > 0 ? basePoints[0] : (SnapPoint?)null;
                    
                case SkierState.RidingLift:
                    // Currently on a lift - will exit at LiftTop
                    var liftTops = _registry.GetByType(SnapPointType.LiftTop)
                        .Where(s => s.OwnerId == skier.CurrentLiftId)
                        .ToList();
                    return liftTops.Count > 0 ? liftTops[0] : (SnapPoint?)null;
                    
                case SkierState.SkiingTrail:
                    // Currently on a trail - will exit at TrailEnd
                    var trailEnds = _registry.GetByType(SnapPointType.TrailEnd)
                        .Where(s => s.OwnerId == skier.CurrentTrailId)
                        .ToList();
                    return trailEnds.Count > 0 ? trailEnds[0] : (SnapPoint?)null;
                    
                default:
                    // WalkingToLift, InQueue - treat as at lift bottom
                    if (skier.CurrentLiftId >= 0)
                    {
                        var liftBottoms = _registry.GetByType(SnapPointType.LiftBottom)
                            .Where(s => s.OwnerId == skier.CurrentLiftId)
                            .ToList();
                        return liftBottoms.Count > 0 ? liftBottoms[0] : (SnapPoint?)null;
                    }
                    // Default to base
                    var defaults = _registry.GetByType(SnapPointType.BaseSpawn);
                    return defaults.Count > 0 ? defaults[0] : (SnapPoint?)null;
            }
        }
        
        /// <summary>
        /// BFS pathfinding through the network graph.
        /// </summary>
        private List<SnapPoint> FindPathBFS(SnapPoint start, SnapPoint target)
        {
            // Note: start and target are value types, so they're always valid
            
            var visited = new HashSet<int>();
            var cameFrom = new Dictionary<int, SnapPoint?>();
            var queue = new Queue<SnapPoint>();
            
            int startHash = GetSnapPointHash(start);
            int targetHash = GetSnapPointHash(target);
            
            queue.Enqueue(start);
            visited.Add(startHash);
            cameFrom[startHash] = null; // Start has no predecessor
            
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int currentHash = GetSnapPointHash(current);
                
                if (currentHash == targetHash)
                {
                    // Found target, reconstruct path
                    return ReconstructPath(cameFrom, target);
                }
                
                var neighbors = _network.GetNeighbors(current);
                foreach (var neighbor in neighbors)
                {
                    int neighborHash = GetSnapPointHash(neighbor);
                    if (!visited.Contains(neighborHash))
                    {
                        visited.Add(neighborHash);
                        cameFrom[neighborHash] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            
            // No path found
            return null;
        }
        
        /// <summary>
        /// Reconstructs the path from BFS cameFrom map.
        /// </summary>
        private List<SnapPoint> ReconstructPath(Dictionary<int, SnapPoint?> cameFrom, SnapPoint target)
        {
            var path = new List<SnapPoint>();
            SnapPoint? current = target;
            
            while (current.HasValue)
            {
                path.Add(current.Value);
                int hash = GetSnapPointHash(current.Value);
                current = cameFrom.ContainsKey(hash) ? cameFrom[hash] : null;
            }
            
            path.Reverse();
            return path;
        }
        
        /// <summary>
        /// Converts a sequence of snap points into PathSteps.
        /// </summary>
        private List<PathStep> ConvertToPathSteps(List<SnapPoint> snapPath)
        {
            var steps = new List<PathStep>();
            
            for (int i = 0; i < snapPath.Count; i++)
            {
                var point = snapPath[i];
                
                // Create step based on snap point type
                switch (point.Type)
                {
                    case SnapPointType.LiftBottom:
                        // This indicates we need to ride this lift
                        var lift = _allLifts.FirstOrDefault(l => l.LiftId == point.OwnerId);
                        steps.Add(new PathStep(PathStepType.RideLift, point.OwnerId, lift?.Name ?? $"Lift {point.OwnerId}"));
                        break;
                        
                    case SnapPointType.TrailStart:
                        // This indicates we need to ski this trail
                        var trail = _allTrails.FirstOrDefault(t => t.TrailId == point.OwnerId);
                        steps.Add(new PathStep(PathStepType.SkiTrail, point.OwnerId, trail?.Name ?? $"Trail {point.OwnerId}"));
                        break;
                        
                    // LiftTop and TrailEnd are transitional, don't create steps for them
                }
            }
            
            return steps;
        }
        
        /// <summary>
        /// Attempts to find any reachable path when the preferred trail isn't accessible.
        /// </summary>
        private List<PathStep> FindAnyReachablePath(Skier skier)
        {
            // Get all trails this skier can ski
            var allowedTrails = _allTrails
                .Where(t => t.IsValid && _distribution.IsAllowed(skier.Skill, t.Difficulty))
                .ToList();
            
            // Try each trail until we find one with a valid path
            foreach (var trail in allowedTrails.OrderBy(t => _random.Next()))
            {
                var path = PlanRouteToTrail(skier, trail);
                if (path != null && path.Count > 0)
                {
                    return path;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Creates a goal to return to the base.
        /// </summary>
        private SkierGoal CreateReturnToBaseGoal()
        {
            var goal = new SkierGoal
            {
                Type = GoalType.ReturnToBase,
                TargetId = -1,
                Priority = 0.5f
            };
            return goal;
        }
        
        /// <summary>
        /// Called when a skier completes a run, to update satisfaction.
        /// </summary>
        public void OnRunCompleted(Skier skier, TrailData trail)
        {
            skier.RunsCompleted++;
            skier.Needs.AddRunFatigue();
            
            // Calculate satisfaction impact
            float satisfactionDelta = SatisfactionModifiers.SuccessfulRunBonus;
            
            // Bonus for preferred difficulty
            float preference = _distribution.GetPreference(skier.Skill, trail.Difficulty);
            if (preference >= 0.4f)
            {
                satisfactionDelta += SatisfactionModifiers.PreferredTrailBonus;
                skier.PreferredRunsCompleted++;
            }
            else if (preference <= 0.1f)
            {
                // Wrong difficulty (had to ski it as transit)
                satisfactionDelta += SatisfactionModifiers.WrongDifficultyPenalty * 0.5f;
            }
            
            skier.Needs.AdjustSatisfaction(satisfactionDelta);
            skier.WasServed = true;
        }
        
        /// <summary>
        /// Called when a skier has to wait in a lift queue.
        /// </summary>
        public void OnLiftWait(Skier skier, float waitMinutes)
        {
            // Every 5 minutes of wait reduces satisfaction
            float penaltyUnits = waitMinutes / 5f;
            skier.Needs.AdjustSatisfaction(SatisfactionModifiers.LongWaitPenalty * penaltyUnits);
        }
        
        private int GetSnapPointHash(SnapPoint point)
        {
            return ((int)point.Type * 1000000) + (point.OwnerId * 1000) + (point.Coord.X * 100) + point.Coord.Y;
        }
    }
}
