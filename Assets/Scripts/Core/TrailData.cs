using System;
using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    public enum TrailDifficulty
    {
        Green = 0,
        Blue = 1,
        Black = 2,
        DoubleBlack = 3
    }

    public enum TrailDrawMode
    {
        Paint,
        Line,
        Pen
    }

    /// <summary>
    /// A single anchor in the trail path. Pen-mode anchors carry cubic-bezier
    /// handles; Paint/Line anchors leave them null (treated as linear).
    /// </summary>
    public class TrailAnchorPoint
    {
        public Vector3f Position;
        public Vector3f? HandleIn;
        public Vector3f? HandleOut;
        public TrailDrawMode SourceMode;

        public TrailAnchorPoint(Vector3f position, TrailDrawMode mode)
        {
            Position = position;
            SourceMode = mode;
        }
    }

    /// <summary>
    /// Pure C# representation of a ski trail.
    /// No Unity types.
    /// </summary>
    public class TrailData
    {
        public int TrailId { get; set; }
        public string Name { get; set; }

        public List<Vector3f> WorldPathPoints { get; private set; }

        public List<Vector3f> LeftBoundaryPoints { get; private set; }
        public List<Vector3f> RightBoundaryPoints { get; private set; }
        public float TrailWidth { get; set; } = 7.5f;

        public List<TileCoord> PathPoints { get; private set; }

        /// <summary>
        /// Ordered anchor points placed by the player. Dense WorldPathPoints
        /// are evaluated from these via EvaluatePathFromAnchors().
        /// </summary>
        public List<TrailAnchorPoint> Anchors { get; private set; }

        public TrailDifficulty Difficulty { get; set; }
        public int Length { get; private set; }
        public float AverageSlope { get; set; }
        public float MaxSlope { get; set; }
        public float TotalElevationDrop { get; set; }
        public bool IsValid { get; set; }

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

        private const int BezierSamplesPerSegment = 20;

        public TrailData(int trailId)
        {
            TrailId = trailId;
            Name = $"Trail {trailId}";
            WorldPathPoints = new List<Vector3f>();
            LeftBoundaryPoints = new List<Vector3f>();
            RightBoundaryPoints = new List<Vector3f>();
            PathPoints = new List<TileCoord>();
            Anchors = new List<TrailAnchorPoint>();
            Difficulty = TrailDifficulty.Green;
            IsValid = false;
        }

        public void AddPoint(TileCoord coord)
        {
            PathPoints.Add(coord);
            Length = PathPoints.Count;
        }

        public void AddWorldPoint(Vector3f position)
        {
            WorldPathPoints.Add(position);
            Length = WorldPathPoints.Count;
            _worldLengthCached = -1f;
        }

        /// <summary>
        /// Rebuilds WorldPathPoints from the Anchors list.
        /// Linear interpolation for Paint/Line anchors, cubic bezier for Pen anchors.
        /// </summary>
        public void EvaluatePathFromAnchors()
        {
            WorldPathPoints.Clear();
            _worldLengthCached = -1f;

            if (Anchors.Count == 0) return;

            if (Anchors.Count == 1)
            {
                WorldPathPoints.Add(Anchors[0].Position);
                Length = 1;
                return;
            }

            for (int i = 0; i < Anchors.Count - 1; i++)
            {
                var a = Anchors[i];
                var b = Anchors[i + 1];
                bool hasBezier = a.HandleOut.HasValue || b.HandleIn.HasValue;

                if (hasBezier)
                {
                    Vector3f p0 = a.Position;
                    Vector3f p1 = a.HandleOut ?? a.Position;
                    Vector3f p2 = b.HandleIn ?? b.Position;
                    Vector3f p3 = b.Position;

                    for (int s = 0; s <= BezierSamplesPerSegment; s++)
                    {
                        if (s == 0 && i > 0) continue; // avoid duplicate at segment joins
                        float t = s / (float)BezierSamplesPerSegment;
                        WorldPathPoints.Add(CubicBezier(p0, p1, p2, p3, t));
                    }
                }
                else
                {
                    if (i == 0) WorldPathPoints.Add(a.Position);
                    WorldPathPoints.Add(b.Position);
                }
            }

            Length = WorldPathPoints.Count;
        }

        /// <summary>
        /// Evaluates a single cubic bezier segment and appends points to the
        /// supplied list. Useful for previewing the segment from lastAnchor to
        /// a tentative cursor position without rebuilding the whole path.
        /// </summary>
        public static void EvaluateBezierSegment(
            Vector3f p0, Vector3f? handleOut,
            Vector3f? handleIn, Vector3f p3,
            List<Vector3f> outPoints, int samples = 20, bool skipFirst = false)
        {
            Vector3f p1 = handleOut ?? p0;
            Vector3f p2 = handleIn ?? p3;

            for (int s = 0; s <= samples; s++)
            {
                if (s == 0 && skipFirst) continue;
                float t = s / (float)samples;
                outPoints.Add(CubicBezier(p0, p1, p2, p3, t));
            }
        }

        public static Vector3f CubicBezier(Vector3f p0, Vector3f p1, Vector3f p2, Vector3f p3, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float uuu = uu * u;
            float tt = t * t;
            float ttt = tt * t;

            return new Vector3f(
                uuu * p0.X + 3f * uu * t * p1.X + 3f * u * tt * p2.X + ttt * p3.X,
                uuu * p0.Y + 3f * uu * t * p1.Y + 3f * u * tt * p2.Y + ttt * p3.Y,
                uuu * p0.Z + 3f * uu * t * p1.Z + 3f * u * tt * p2.Z + ttt * p3.Z
            );
        }

        public void Clear()
        {
            WorldPathPoints.Clear();
            LeftBoundaryPoints.Clear();
            RightBoundaryPoints.Clear();
            PathPoints.Clear();
            Anchors.Clear();
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
