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
            int pointCount = trail.WorldPathPoints.Count > 0 ? trail.WorldPathPoints.Count : trail.PathPoints.Count;
            if (pointCount < MinPoints)
            {
                return false;
            }
            
            // NEW: If we have WorldPathPoints, use those for validation
            if (trail.WorldPathPoints.Count >= MinPoints)
            {
                return ValidateTrailWorldSpace(trail);
            }
            
            // LEGACY: Use old tile-based validation
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
        
        /// <summary>
        /// Validates trail using world-space positions from the mountain mesh.
        /// VERY PERMISSIVE: Any amount of net downhill is valid.
        /// </summary>
        private bool ValidateTrailWorldSpace(TrailData trail)
        {
            // Calculate total drop and run using world positions
            Vector3f start = trail.WorldPathPoints[0];
            Vector3f end = trail.WorldPathPoints[trail.WorldPathPoints.Count - 1];
            
            float totalDrop = start.Y - end.Y;
            
            // Auto-reverse if drawn uphill
            if (totalDrop < 0)
            {
                trail.ReverseWorldPathPoints();
                start = trail.WorldPathPoints[0];
                end = trail.WorldPathPoints[trail.WorldPathPoints.Count - 1];
                totalDrop = start.Y - end.Y;
            }
            
            trail.TotalElevationDrop = totalDrop;
            
            // EXTREMELY PERMISSIVE: Just needs to go down at all (even 0.001 units)
            // This allows nearly flat trails as long as they have ANY net downhill
            if (totalDrop <= 0f)
            {
                return false; // Only reject if perfectly flat or uphill overall
            }
            
            // Calculate 3D path length
            float totalRun = 0f;
            int uphillCount = 0;
            
            for (int i = 0; i < trail.WorldPathPoints.Count - 1; i++)
            {
                Vector3f current = trail.WorldPathPoints[i];
                Vector3f next = trail.WorldPathPoints[i + 1];
                
                // Horizontal distance (XZ plane)
                float dx = next.X - current.X;
                float dz = next.Z - current.Z;
                float segmentRun = (float)Math.Sqrt(dx * dx + dz * dz);
                totalRun += segmentRun;
                
                // Check if uphill
                if (next.Y > current.Y + 0.1f) // Small tolerance for noise
                {
                    uphillCount++;
                }
            }
            
            // Minimum length: just 0.5 units (very short trails OK)
            if (totalRun < 0.5f)
            {
                return false;
            }
            
            // Allow up to 80% uphill segments (extremely permissive)
            int totalSegments = trail.WorldPathPoints.Count - 1;
            float uphillPercent = totalSegments > 0 ? (float)uphillCount / totalSegments : 0f;
            if (uphillPercent > 0.8f)
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
            
            // NEW: Use WorldPathPoints if available (preferred for 3D terrain)
            if (trail.WorldPathPoints != null && trail.WorldPathPoints.Count >= 2)
            {
                return CalculateDifficultyWorldSpace(trail, stats);
            }
            
            // LEGACY: Fall back to tile-based calculation
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
            float totalDrop = startHeight - endHeight;
            
            // Calculate total 2D run distance along path
            float totalRun = 0f;
            float maxSegmentGrade = 0f;
            int maxGradeSegment = -1;
            
            for (int i = 0; i < trail.PathPoints.Count - 1; i++)
            {
                TileCoord current = trail.PathPoints[i];
                TileCoord next = trail.PathPoints[i + 1];
                
                float segmentRun = CalculateHorizontalDistance(current, next);
                totalRun += segmentRun;
                
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
            
            ApplyDifficultyFromGrade(trail, stats, totalDrop, totalRun, maxSegmentGrade, maxGradeSegment);
            return stats;
        }
        
        /// <summary>
        /// Calculates difficulty using world-space 3D positions.
        /// </summary>
        private TrailStats CalculateDifficultyWorldSpace(TrailData trail, TrailStats stats)
        {
            Vector3f start = trail.WorldPathPoints[0];
            Vector3f end = trail.WorldPathPoints[trail.WorldPathPoints.Count - 1];
            
            float totalDrop = start.Y - end.Y;
            
            // Calculate total horizontal run and find steepest segment
            float totalRun = 0f;
            float maxSegmentGrade = 0f;
            int maxGradeSegment = -1;
            
            for (int i = 0; i < trail.WorldPathPoints.Count - 1; i++)
            {
                Vector3f current = trail.WorldPathPoints[i];
                Vector3f next = trail.WorldPathPoints[i + 1];
                
                // Horizontal distance (XZ plane)
                float dx = next.X - current.X;
                float dz = next.Z - current.Z;
                float segmentRun = (float)Math.Sqrt(dx * dx + dz * dz);
                totalRun += segmentRun;
                
                // Segment grade
                float segmentDrop = current.Y - next.Y;
                if (segmentRun > 0.1f)
                {
                    float segmentGrade = Math.Abs(segmentDrop / segmentRun);
                    if (segmentGrade > maxSegmentGrade)
                    {
                        maxSegmentGrade = segmentGrade;
                        maxGradeSegment = i;
                    }
                }
            }
            
            ApplyDifficultyFromGrade(trail, stats, totalDrop, totalRun, maxSegmentGrade, maxGradeSegment);
            return stats;
        }
        
        /// <summary>
        /// Applies difficulty rating based on calculated grades.
        /// </summary>
        private void ApplyDifficultyFromGrade(TrailData trail, TrailStats stats, 
            float totalDrop, float totalRun, float maxSegmentGrade, int maxGradeSegment)
        {
            float avgGrade = totalRun > 0 ? totalDrop / totalRun : 0f;
            
            stats.TotalRun = totalRun;
            stats.TotalDrop = totalDrop;
            stats.AvgGrade = avgGrade;
            stats.MaxSegmentGrade = maxSegmentGrade;
            stats.MaxGradeSegment = maxGradeSegment;
            
            trail.AverageSlope = avgGrade;
            trail.MaxSlope = maxSegmentGrade;
            trail.TotalElevationDrop = totalDrop;
            
            // Classify based on avgGrade (with penalty for steep segments)
            float effectiveGrade = avgGrade;
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

