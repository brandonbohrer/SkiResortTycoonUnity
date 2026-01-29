using System;
using System.Collections.Generic;
using System.Linq;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pathfinding for individual skiers through the network.
    /// Pure C# - no Unity types.
    /// </summary>
    public class SkierPathfinder
    {
        private NetworkGraph _network;
        private SnapRegistry _registry;
        private SkierDistribution _distribution;
        private Random _random;
        private List<TrailData> _allTrails;
        
        public SkierPathfinder(NetworkGraph network, SnapRegistry registry, SkierDistribution distribution, List<TrailData> trails, Random random)
        {
            _network = network;
            _registry = registry;
            _distribution = distribution;
            _allTrails = trails;
            _random = random;
        }
        
        /// <summary>
        /// Finds all trails reachable from base using BFS.
        /// </summary>
        public List<TrailData> FindReachableTrails()
        {
            HashSet<int> visitedHashes = new HashSet<int>();
            HashSet<int> reachableTrailIds = new HashSet<int>();
            Queue<SnapPoint> queue = new Queue<SnapPoint>();
            
            // Start from all base spawn points
            var basePoints = _registry.GetByType(SnapPointType.BaseSpawn);
            if (basePoints.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[Pathfinding] No base spawn points found!");
                return new List<TrailData>();
            }
            
            foreach (var basePoint in basePoints)
            {
                queue.Enqueue(basePoint);
                visitedHashes.Add(GetSnapPointHash(basePoint));
            }
            
            // BFS through network
            int exploredCount = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                exploredCount++;
                
                // If this is a trail start, mark trail as reachable
                if (current.Type == SnapPointType.TrailStart)
                {
                    reachableTrailIds.Add(current.OwnerId);
                }
                
                // Explore neighbors
                var neighbors = _network.GetNeighbors(current);
                
                foreach (var neighbor in neighbors)
                {
                    int neighborHash = GetSnapPointHash(neighbor);
                    if (!visitedHashes.Contains(neighborHash))
                    {
                        visitedHashes.Add(neighborHash);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            
            return _allTrails.Where(t => reachableTrailIds.Contains(t.TrailId)).ToList();
        }
        
        /// <summary>
        /// Chooses a destination trail for a skier using weighted randomness.
        /// </summary>
        public TrailData ChooseDestinationTrail(Skier skier, List<TrailData> reachableTrails)
        {
            // Filter by hard caps (skill level restrictions)
            var allowedTrails = reachableTrails
                .Where(t => _distribution.IsAllowed(skier.Skill, t.Difficulty))
                .ToList();
            
            if (allowedTrails.Count == 0)
                return null;
            
            // Build weighted list based on preferences
            List<(TrailData trail, float weight)> weightedTrails = new List<(TrailData, float)>();
            float totalWeight = 0f;
            
            foreach (var trail in allowedTrails)
            {
                float weight = _distribution.GetPreference(skier.Skill, trail.Difficulty);
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
            
            // Fallback (shouldn't happen)
            return weightedTrails[weightedTrails.Count - 1].trail;
        }
        
        /// <summary>
        /// Finds a path from base to a destination trail, returning all trails traversed.
        /// Uses BFS to find shortest path.
        /// </summary>
        public List<TrailData> FindPathToTrail(TrailData destination)
        {
            // BFS to find path from base to destination trail's start
            Dictionary<int, SnapPoint> cameFrom = new Dictionary<int, SnapPoint>();
            HashSet<int> visited = new HashSet<int>();
            Queue<SnapPoint> queue = new Queue<SnapPoint>();
            
            // Start from base
            var basePoints = _registry.GetByType(SnapPointType.BaseSpawn);
            if (basePoints.Count == 0)
                return new List<TrailData>();
            
            var startPoint = basePoints[0];
            queue.Enqueue(startPoint);
            visited.Add(GetSnapPointHash(startPoint));
            cameFrom[GetSnapPointHash(startPoint)] = startPoint; // Self-reference for start
            
            // Find destination's TrailStart snap point
            var destStartPoints = _registry.GetByType(SnapPointType.TrailStart)
                .Where(s => s.OwnerId == destination.TrailId)
                .ToList();
            
            if (destStartPoints.Count == 0)
                return new List<TrailData>();
            
            var destPoint = destStartPoints[0];
            int destHash = GetSnapPointHash(destPoint);
            
            // BFS
            bool foundPath = false;
            while (queue.Count > 0 && !foundPath)
            {
                var current = queue.Dequeue();
                int currentHash = GetSnapPointHash(current);
                
                if (currentHash == destHash)
                {
                    foundPath = true;
                    break;
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
            
            if (!foundPath)
                return new List<TrailData>();
            
            // Reconstruct path
            List<SnapPoint> pathPoints = new List<SnapPoint>();
            var pathPoint = destPoint;
            
            while (GetSnapPointHash(pathPoint) != GetSnapPointHash(startPoint))
            {
                pathPoints.Add(pathPoint);
                pathPoint = cameFrom[GetSnapPointHash(pathPoint)];
            }
            
            pathPoints.Reverse();
            
            // Extract trail IDs from path (only TrailStart points)
            List<int> trailIds = pathPoints
                .Where(p => p.Type == SnapPointType.TrailStart)
                .Select(p => p.OwnerId)
                .ToList();
            
            // Return trail objects
            return _allTrails
                .Where(t => trailIds.Contains(t.TrailId))
                .ToList();
        }
        
        private int GetSnapPointHash(SnapPoint point)
        {
            return ((int)point.Type * 1000000) + (point.OwnerId * 1000) + (point.Coord.X * 100) + point.Coord.Y;
        }
    }
}

