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
        private SnapRegistry _snapRegistry;
        
        // Configurable validation thresholds
        public int MinPoints { get; set; } = 3;                    // Minimum trail points
        public float MinRunLength { get; set; } = 6f;              // Minimum run distance (tiles)
        public float MinDrop { get; set; } = 1f;                   // Minimum vertical drop (height units)
        public float MaxUphillPercent { get; set; } = 0.2f;        // Max % of uphill segments (0.2 = 20%)
        
        public List<TrailData> Trails => _trails;
        
        public TrailSystem(TerrainData terrain, SnapRegistry snapRegistry = null)
        {
            _terrain = terrain;
            _snapRegistry = snapRegistry;
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
        /// Uses configurable thresholds.
        /// </summary>
        public bool ValidateTrail(TrailData trail)
        {
            // Check minimum points
            if (trail.PathPoints.Count < MinPoints)
            {
                return false;
            }
            
            // Check overall elevation drop
            var start = trail.GetStart();
            var end = trail.GetEnd();
            
            if (!start.HasValue || !end.HasValue)
                return false;
            
            int startHeight = _terrain.GetHeight(start.Value);
            int endHeight = _terrain.GetHeight(end.Value);
            float totalDrop = startHeight - endHeight;
            
            // ENFORCE DIRECTION: Trail must go downhill (top to bottom)
            // If drawn uphill, auto-reverse it
            if (totalDrop < 0)
            {
                trail.ReverseDirection();
                
                // Recalculate after reversal
                start = trail.GetStart();
                end = trail.GetEnd();
                startHeight = _terrain.GetHeight(start.Value);
                endHeight = _terrain.GetHeight(end.Value);
                totalDrop = startHeight - endHeight;
            }
            
            trail.TotalElevationDrop = totalDrop;
            
            // Must go downhill overall (configurable minimum)
            if (totalDrop < MinDrop)
            {
                return false;
            }
            
            // Calculate total run length
            float totalRun = 0f;
            int uphillCount = 0;
            int totalSegments = 0;
            
            for (int i = 0; i < trail.PathPoints.Count - 1; i++)
            {
                TileCoord current = trail.PathPoints[i];
                TileCoord next = trail.PathPoints[i + 1];
                
                totalRun += CalculateHorizontalDistance(current, next);
                
                int currentHeight = _terrain.GetHeight(current);
                int nextHeight = _terrain.GetHeight(next);
                
                if (nextHeight > currentHeight)
                {
                    uphillCount++;
                }
                totalSegments++;
            }
            
            // Check minimum run length
            if (totalRun < MinRunLength)
            {
                return false;
            }
            
            // Check uphill percentage
            float uphillPercent = totalSegments > 0 ? (float)uphillCount / totalSegments : 0f;
            if (uphillPercent > MaxUphillPercent)
            {
                return false;
            }
            
            trail.IsValid = true;
            return true;
        }
        
        // Difficulty thresholds (avgGrade = total drop / total run)
        // These are configurable and tunable
        public const float GRADE_GREEN_MAX = 0.12f;      // < 12% grade = Green (easiest)
        public const float GRADE_BLUE_MAX = 0.22f;       // 12-22% grade = Blue (intermediate)
        public const float GRADE_BLACK_MAX = 0.35f;      // 22-35% grade = Black (advanced)
        // > 35% grade = Double Black (expert)
        
        /// <summary>
        /// Calculates trail difficulty based on average grade (drop / run).
        /// Simple, clear formula: avgGrade = totalDrop / totalRun.
        /// </summary>
        public TrailStats CalculateDifficulty(TrailData trail)
        {
            TrailStats stats = new TrailStats();
            
            if (trail.PathPoints.Count < 2)
            {
                trail.Difficulty = TrailDifficulty.Green;
                return stats;
            }
            
            // Get start and end heights
            var start = trail.GetStart();
            var end = trail.GetEnd();
            
            if (!start.HasValue || !end.HasValue)
            {
                trail.Difficulty = TrailDifficulty.Green;
                return stats;
            }
            
            int startHeight = _terrain.GetHeight(start.Value);
            int endHeight = _terrain.GetHeight(end.Value);
            float totalDrop = startHeight - endHeight; // Net vertical drop (must be positive)
            
            // Calculate total 2D run distance along path
            float totalRun = 0f;
            float maxSegmentGrade = 0f;
            int maxGradeSegment = -1;
            
            for (int i = 0; i < trail.PathPoints.Count - 1; i++)
            {
                TileCoord current = trail.PathPoints[i];
                TileCoord next = trail.PathPoints[i + 1];
                
                // 2D distance in tile units (1 for cardinal, ~1.414 for diagonal)
                float segmentRun = CalculateHorizontalDistance(current, next);
                totalRun += segmentRun;
                
                // Calculate segment grade for max detection
                int currentHeight = _terrain.GetHeight(current);
                int nextHeight = _terrain.GetHeight(next);
                float segmentDrop = currentHeight - nextHeight;
                
                if (segmentRun > 0)
                {
                    float segmentGrade = Math.Abs(segmentDrop / segmentRun);
                    if (segmentGrade > maxSegmentGrade)
                    {
                        maxSegmentGrade = segmentGrade;
                        maxGradeSegment = i;
                    }
                }
            }
            
            // Average grade = total drop / total run
            float avgGrade = totalRun > 0 ? totalDrop / totalRun : 0f;
            
            // Store stats
            stats.TotalRun = totalRun;
            stats.TotalDrop = totalDrop;
            stats.AvgGrade = avgGrade;
            stats.MaxSegmentGrade = maxSegmentGrade;
            stats.MaxGradeSegment = maxGradeSegment;
            
            trail.AverageSlope = avgGrade;
            trail.MaxSlope = maxSegmentGrade;
            trail.TotalElevationDrop = totalDrop;
            
            // Classify based on avgGrade (with small penalty for steep segments)
            float effectiveGrade = avgGrade;
            
            // Add small penalty if max segment is significantly steeper
            if (maxSegmentGrade > avgGrade * 1.5f)
            {
                effectiveGrade = avgGrade * 0.9f + maxSegmentGrade * 0.1f;
            }
            
            // Apply thresholds
            if (effectiveGrade > GRADE_BLACK_MAX)
            {
                trail.Difficulty = TrailDifficulty.DoubleBlack;
            }
            else if (effectiveGrade > GRADE_BLUE_MAX)
            {
                trail.Difficulty = TrailDifficulty.Black;
            }
            else if (effectiveGrade > GRADE_GREEN_MAX)
            {
                trail.Difficulty = TrailDifficulty.Blue;
            }
            else
            {
                trail.Difficulty = TrailDifficulty.Green;
            }
            
            // Register snap points now that trail is finalized
            RegisterSnapPoints(trail);
            
            return stats;
        }
        
        /// <summary>
        /// Registers snap points for a trail.
        /// </summary>
        private void RegisterSnapPoints(TrailData trail)
        {
            if (_snapRegistry == null) return;
            
            var start = trail.GetStart();
            var end = trail.GetEnd();
            
            if (!start.HasValue || !end.HasValue) return;
            
            // Register trail start (top of trail)
            var startSnap = new SnapPoint(
                SnapPointType.TrailStart,
                start.Value,
                trail.TrailId,
                trail.Name
            );
            _snapRegistry.Register(startSnap);
            
            // Register trail end (bottom of trail)
            var endSnap = new SnapPoint(
                SnapPointType.TrailEnd,
                end.Value,
                trail.TrailId,
                trail.Name
            );
            _snapRegistry.Register(endSnap);
        }
        
        /// <summary>
        /// Unregisters snap points for a trail.
        /// </summary>
        private void UnregisterSnapPoints(TrailData trail)
        {
            if (_snapRegistry == null) return;
            
            _snapRegistry.UnregisterByOwner(trail.TrailId);
        }
        
        /// <summary>
        /// Calculates horizontal distance between two tile coordinates (ignores height).
        /// </summary>
        private float CalculateHorizontalDistance(TileCoord a, TileCoord b)
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
            // Unregister snap points
            UnregisterSnapPoints(trail);
            
            _trails.Remove(trail);
        }
        
        /// <summary>
        /// Gets all trails.
        /// </summary>
        public List<TrailData> GetAllTrails()
        {
            return new List<TrailData>(_trails);
        }
        
        /// <summary>
        /// Gets a trail by ID.
        /// </summary>
        public TrailData GetTrail(int trailId)
        {
            return _trails.Find(t => t.TrailId == trailId);
        }
    }
    
    /// <summary>
    /// Detailed statistics about a trail for debugging.
    /// </summary>
    public class TrailStats
    {
        public float TotalRun { get; set; }          // Total 2D distance along path (tiles)
        public float TotalDrop { get; set; }         // Net vertical drop (height units)
        public float AvgGrade { get; set; }          // drop / run (unitless, 0.12 = 12%)
        public float MaxSegmentGrade { get; set; }   // Steepest single segment
        public int MaxGradeSegment { get; set; }     // Index of steepest segment
    }
}

