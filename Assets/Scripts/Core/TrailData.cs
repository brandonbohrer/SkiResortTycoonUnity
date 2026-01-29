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
        public List<TileCoord> PathPoints { get; private set; }
        public TrailDifficulty Difficulty { get; set; }
        public int Length { get; private set; }
        public float AverageSlope { get; set; }
        public float MaxSlope { get; set; }
        public float TotalElevationDrop { get; set; }
        public bool IsValid { get; set; }
        
        public TrailData(int trailId)
        {
            TrailId = trailId;
            Name = $"Trail {trailId}";
            PathPoints = new List<TileCoord>();
            Difficulty = TrailDifficulty.Green;
            IsValid = false;
        }
        
        /// <summary>
        /// Adds a point to the trail path.
        /// </summary>
        public void AddPoint(TileCoord coord)
        {
            PathPoints.Add(coord);
            Length = PathPoints.Count;
        }
        
        /// <summary>
        /// Clears all points from the trail.
        /// </summary>
        public void Clear()
        {
            PathPoints.Clear();
            Length = 0;
            IsValid = false;
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
        /// Reverses the trail direction (used when user draws uphill).
        /// </summary>
        public void ReverseDirection()
        {
            PathPoints.Reverse();
        }
    }
}

