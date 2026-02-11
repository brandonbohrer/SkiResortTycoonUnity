using System;
using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Trail difficulty ratings (ski resort standard).
    /// </summary>
    public enum TrailDifficulty
    {
        Green = 0,      // Beginner (easiest)
        Blue = 1,       // Intermediate
        Black = 2,      // Advanced
        DoubleBlack = 3 // Expert
    }
    
    /// <summary>
    /// Pure C# representation of a ski trail.
    /// No Unity types.
    /// </summary>
    public class TrailData
    {
        public int TrailId { get; set; }
        public string Name { get; set; }
        
        // World-space positions (authoritative for rendering and gameplay)
        public List<Vector3f> WorldPathPoints { get; private set; }
        
        // Boundary edges for skier navigation (perpendicular offsets from centerline)
        public List<Vector3f> LeftBoundaryPoints { get; private set; }
        public List<Vector3f> RightBoundaryPoints { get; private set; }
        public float TrailWidth { get; set; } = 8f; // Default matches tree clearing width
        
        // Legacy grid coordinates (kept for backwards compatibility)
        public List<TileCoord> PathPoints { get; private set; }
        
        public TrailDifficulty Difficulty { get; set; }
        public int Length { get; private set; }
        public float AverageSlope { get; set; }
        public float MaxSlope { get; set; }
        public float TotalElevationDrop { get; set; }
        public bool IsValid { get; set; }
        
        /// <summary>
        /// Total 3D arc-length of the trail in world units.
        /// Computed from WorldPathPoints on first access, then cached.
        /// Used by the traffic system to derive trail capacity (longer = more skiers).
        /// </summary>
        public float WorldLength
        {
            get
            {
                if (_worldLengthCached < 0f)
                    _worldLengthCached = ComputeWorldLength();
                return _worldLengthCached;
            }
        }
        private float _worldLengthCached = -1f;
        
        public TrailData(int trailId)
        {
            TrailId = trailId;
            Name = $"Trail {trailId}";
            WorldPathPoints = new List<Vector3f>();
            LeftBoundaryPoints = new List<Vector3f>();
            RightBoundaryPoints = new List<Vector3f>();
            PathPoints = new List<TileCoord>();
            Difficulty = TrailDifficulty.Green;
            IsValid = false;
        }
        
        /// <summary>
        /// Adds a point to the trail path (legacy tile coordinate).
        /// </summary>
        public void AddPoint(TileCoord coord)
        {
            PathPoints.Add(coord);
            Length = PathPoints.Count;
        }
        
        /// <summary>
        /// Adds a world-space point to the trail path.
        /// </summary>
        public void AddWorldPoint(Vector3f position)
        {
            WorldPathPoints.Add(position);
            Length = WorldPathPoints.Count;
            _worldLengthCached = -1f; // invalidate cache
        }
        
        /// <summary>
        /// Clears all points from the trail.
        /// </summary>
        public void Clear()
        {
            WorldPathPoints.Clear();
            LeftBoundaryPoints.Clear();
            RightBoundaryPoints.Clear();
            PathPoints.Clear();
            Length = 0;
            IsValid = false;
            _worldLengthCached = -1f;
        }
        
        /// <summary>
        /// Gets the start point of the trail.
        /// </summary>
        public TileCoord? GetStart()
        {
            if (PathPoints.Count > 0)
                return PathPoints[0];
            return null;
        }
        
        /// <summary>
        /// Gets the end point of the trail.
        /// </summary>
        public TileCoord? GetEnd()
        {
            if (PathPoints.Count > 0)
                return PathPoints[PathPoints.Count - 1];
            return null;
        }
        
        /// <summary>
        /// Reverses the direction of the trail (legacy tile coords).
        /// </summary>
        public void ReverseDirection()
        {
            PathPoints.Reverse();
        }
        
        /// <summary>
        /// Reverses the direction of the trail (world-space points).
        /// </summary>
        public void ReverseWorldPathPoints()
        {
            WorldPathPoints.Reverse();
        }
        
        /// <summary>
        /// Ensures WorldPathPoints are ordered top-to-bottom (highest elevation first).
        /// This guarantees skiers always face downhill when following the path.
        /// Also re-orders legacy PathPoints and regenerates boundaries.
        /// </summary>
        public void EnsureDownhillOrder()
        {
            if (WorldPathPoints.Count >= 2)
            {
                float startY = WorldPathPoints[0].Y;
                float endY = WorldPathPoints[WorldPathPoints.Count - 1].Y;
                
                if (endY > startY)
                {
                    // Trail goes uphill -- reverse to go downhill
                    WorldPathPoints.Reverse();
                    
                    // Keep boundaries in sync
                    if (LeftBoundaryPoints.Count > 0)
                        LeftBoundaryPoints.Reverse();
                    if (RightBoundaryPoints.Count > 0)
                        RightBoundaryPoints.Reverse();
                }
            }
            
            if (PathPoints.Count >= 2)
            {
                // Legacy coords: higher Y-index typically means higher elevation,
                // but we just mirror the world-space decision to stay consistent
                PathPoints.Reverse();
            }
        }
        
        /// <summary>
        /// Sums the 3D segment distances between consecutive WorldPathPoints.
        /// </summary>
        private float ComputeWorldLength()
        {
            if (WorldPathPoints == null || WorldPathPoints.Count < 2)
                return 0f;
            
            float total = 0f;
            for (int i = 0; i < WorldPathPoints.Count - 1; i++)
            {
                total += Vector3f.Distance(WorldPathPoints[i], WorldPathPoints[i + 1]);
            }
            return total;
        }
        
        /// <summary>
        /// Generates left and right boundary edges from the centerline path.
        /// Boundaries are perpendicular offsets at TrailWidth/2 distance from center.
        /// </summary>
        public void GenerateBoundaries()
        {
            LeftBoundaryPoints.Clear();
            RightBoundaryPoints.Clear();
            
            if (WorldPathPoints.Count < 2)
            {
                // Not enough points to generate boundaries
                return;
            }
            
            float halfWidth = TrailWidth / 2f;
            Vector3f up = new Vector3f(0, 0, 1); // Z-up axis for cross product
            
            for (int i = 0; i < WorldPathPoints.Count; i++)
            {
                Vector3f currentPoint = WorldPathPoints[i];
                Vector3f direction;
                
                // Calculate direction vector for this point
                if (i == 0)
                {
                    // First point: use direction to next point
                    direction = (WorldPathPoints[i + 1] - currentPoint).Normalized();
                }
                else if (i == WorldPathPoints.Count - 1)
                {
                    // Last point: use direction from previous point
                    direction = (currentPoint - WorldPathPoints[i - 1]).Normalized();
                }
                else
                {
                    // Middle points: average direction from previous and to next
                    Vector3f dirToPrev = (currentPoint - WorldPathPoints[i - 1]).Normalized();
                    Vector3f dirToNext = (WorldPathPoints[i + 1] - currentPoint).Normalized();
                    direction = (dirToPrev + dirToNext).Normalized();
                }
                
                // Calculate perpendicular offset (cross product with up vector)
                Vector3f perpendicular = Vector3f.Cross(direction, up).Normalized();
                
                // Generate left and right boundary points
                Vector3f leftPoint = currentPoint + perpendicular * halfWidth;
                Vector3f rightPoint = currentPoint - perpendicular * halfWidth;
                
                LeftBoundaryPoints.Add(leftPoint);
                RightBoundaryPoints.Add(rightPoint);
            }
        }
    }
}
