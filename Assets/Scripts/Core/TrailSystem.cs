using System;
using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# trail management system.
    /// Handles trail creation, validation, and difficulty calculation.
    /// </summary>
    public class TrailSystem
    {
        private List<TrailData> _trails;
        private int _nextTrailId = 1;
        private TerrainData _terrain;
        
        public List<TrailData> Trails => _trails;
        
        public TrailSystem(TerrainData terrain)
        {
            _terrain = terrain;
            _trails = new List<TrailData>();
        }
        
        /// <summary>
        /// Creates a new trail.
        /// </summary>
        public TrailData CreateTrail()
        {
            TrailData trail = new TrailData(_nextTrailId++);
            _trails.Add(trail);
            return trail;
        }
        
        /// <summary>
        /// Validates a trail (must go downhill, reasonable length, etc.).
        /// </summary>
        public bool ValidateTrail(TrailData trail)
        {
            if (trail.PathPoints.Count < 5)
            {
                return false; // Too short
            }
            
            // Check overall elevation drop
            var start = trail.GetStart();
            var end = trail.GetEnd();
            
            if (!start.HasValue || !end.HasValue)
                return false;
            
            int startHeight = _terrain.GetHeight(start.Value);
            int endHeight = _terrain.GetHeight(end.Value);
            
            trail.TotalElevationDrop = startHeight - endHeight;
            
            // Must go downhill overall
            if (trail.TotalElevationDrop < 2)
            {
                return false;
            }
            
            trail.IsValid = true;
            return true;
        }
        
        /// <summary>
        /// Calculates trail difficulty based on slope, length, and terrain.
        /// </summary>
        public void CalculateDifficulty(TrailData trail)
        {
            if (trail.PathPoints.Count < 2)
            {
                trail.Difficulty = TrailDifficulty.Green;
                return;
            }
            
            // Calculate slopes along the trail
            float totalSlope = 0f;
            float maxSlope = 0f;
            int slopeCount = 0;
            
            for (int i = 0; i < trail.PathPoints.Count - 1; i++)
            {
                TileCoord current = trail.PathPoints[i];
                TileCoord next = trail.PathPoints[i + 1];
                
                int currentHeight = _terrain.GetHeight(current);
                int nextHeight = _terrain.GetHeight(next);
                
                float heightDiff = currentHeight - nextHeight;
                float distance = CalculateDistance(current, next);
                
                if (distance > 0)
                {
                    float slope = heightDiff / distance;
                    totalSlope += Math.Abs(slope);
                    maxSlope = Math.Max(maxSlope, Math.Abs(slope));
                    slopeCount++;
                }
            }
            
            float avgSlope = slopeCount > 0 ? totalSlope / slopeCount : 0f;
            
            trail.AverageSlope = avgSlope;
            trail.MaxSlope = maxSlope;
            
            // Determine difficulty based on slope
            // Green: gentle slopes (< 0.3)
            // Blue: moderate slopes (0.3 - 0.6)
            // Black: steep slopes (0.6 - 1.0)
            // Double Black: very steep (> 1.0)
            
            if (maxSlope > 1.0f || avgSlope > 0.7f)
            {
                trail.Difficulty = TrailDifficulty.DoubleBlack;
            }
            else if (maxSlope > 0.6f || avgSlope > 0.4f)
            {
                trail.Difficulty = TrailDifficulty.Black;
            }
            else if (maxSlope > 0.3f || avgSlope > 0.2f)
            {
                trail.Difficulty = TrailDifficulty.Blue;
            }
            else
            {
                trail.Difficulty = TrailDifficulty.Green;
            }
        }
        
        /// <summary>
        /// Calculates distance between two tile coordinates.
        /// </summary>
        private float CalculateDistance(TileCoord a, TileCoord b)
        {
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
        
        /// <summary>
        /// Removes a trail from the system.
        /// </summary>
        public void RemoveTrail(TrailData trail)
        {
            _trails.Remove(trail);
        }
        
        /// <summary>
        /// Gets all trails.
        /// </summary>
        public List<TrailData> GetAllTrails()
        {
            return new List<TrailData>(_trails);
        }
    }
}

