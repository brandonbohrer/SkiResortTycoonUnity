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

        /// <summary>
        /// When true this anchor was created by a segment-drag operation and
        /// should not be rendered as a visible marker or be directly
        /// selectable by the player. It exists only to shape the curve.
        /// </summary>
        public bool IsCurveControl;

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

        /// <summary>
        /// Difficulty derived purely from the terrain slope, ignoring any player override.
        /// Uses the same grade thresholds as TrailSystem.ApplyDifficultyFromGrade().
        /// </summary>
        public TrailDifficulty SlopeDifficulty
        {
            get
            {
                float effectiveGrade = AverageSlope;
                if (MaxSlope > AverageSlope * 1.5f)
                    effectiveGrade = AverageSlope * 0.9f + MaxSlope * 0.1f;

                if (effectiveGrade > 0.35f) return TrailDifficulty.DoubleBlack;
                if (effectiveGrade > 0.22f) return TrailDifficulty.Black;
                if (effectiveGrade > 0.12f) return TrailDifficulty.Blue;
                return TrailDifficulty.Green;
            }
        }

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
                    // Subdivide linear segments so intermediate points can be
                    // terrain-projected, preventing the boundary lines from
                    // floating between the two endpoints on long straight runs.
                    float segLen = Vector3f.Distance(a.Position, b.Position);
                    int samples = System.Math.Max(1, (int)(segLen / 2f));
                    if (samples > BezierSamplesPerSegment)
                        samples = BezierSamplesPerSegment;

                    for (int s = 0; s <= samples; s++)
                    {
                        if (s == 0 && i > 0) continue;
                        float t = s / (float)samples;
                        WorldPathPoints.Add(new Vector3f(
                            a.Position.X + (b.Position.X - a.Position.X) * t,
                            a.Position.Y + (b.Position.Y - a.Position.Y) * t,
                            a.Position.Z + (b.Position.Z - a.Position.Z) * t
                        ));
                    }
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
        /// Finds the closest point on this trail's centerline to the given XZ position.
        /// Returns the 3D closest point, tangent direction, XZ perpendicular (left-hand normal),
        /// the segment index + parametric t, and the squared XZ distance.
        /// </summary>
        public void FindClosestPointOnPath(float x, float z,
            out Vector3f closestPoint, out Vector3f tangent, out Vector3f perpendicular,
            out int segmentIndex, out float segmentT, out float distanceSq)
        {
            closestPoint = default;
            tangent = new Vector3f(0, 0, 1);
            perpendicular = new Vector3f(1, 0, 0);
            segmentIndex = 0;
            segmentT = 0f;
            distanceSq = float.MaxValue;

            if (WorldPathPoints == null || WorldPathPoints.Count < 2) return;

            for (int i = 0; i < WorldPathPoints.Count - 1; i++)
            {
                var pA = WorldPathPoints[i];
                var pB = WorldPathPoints[i + 1];

                float ax = pA.X, az = pA.Z;
                float bx = pB.X, bz = pB.Z;
                float abx = bx - ax, abz = bz - az;
                float segLenSq = abx * abx + abz * abz;

                float t = 0f;
                if (segLenSq > 0.0001f)
                {
                    t = ((x - ax) * abx + (z - az) * abz) / segLenSq;
                    if (t < 0f) t = 0f;
                    else if (t > 1f) t = 1f;
                }

                float cx = ax + abx * t;
                float cz = az + abz * t;
                float dx = x - cx;
                float dz2 = z - cz;
                float dSq = dx * dx + dz2 * dz2;

                if (dSq < distanceSq)
                {
                    distanceSq = dSq;
                    segmentIndex = i;
                    segmentT = t;
                }
            }

            var segA = WorldPathPoints[segmentIndex];
            var segB = WorldPathPoints[segmentIndex + 1];
            closestPoint = new Vector3f(
                segA.X + (segB.X - segA.X) * segmentT,
                segA.Y + (segB.Y - segA.Y) * segmentT,
                segA.Z + (segB.Z - segA.Z) * segmentT
            );

            float tdx = segB.X - segA.X;
            float tdz = segB.Z - segA.Z;
            float tLen = (float)System.Math.Sqrt(tdx * tdx + tdz * tdz);
            if (tLen > 0.0001f)
            {
                tangent = new Vector3f(tdx / tLen, 0f, tdz / tLen);
            }

            perpendicular = new Vector3f(-tangent.Z, 0f, tangent.X);
        }

        /// <summary>
        /// Checks whether the XZ position (x, z) lies inside this trail's corridor
        /// (within half-width of the centerline, ignoring Y). If inside, sets
        /// <paramref name="distanceAlong"/> to the arc-length distance along the
        /// centerline at the closest point, suitable for passing directly to
        /// <c>SkierMotionController.SetTrail</c>.
        /// </summary>
        public bool IsInsideCorridor(float x, float z, out float distanceAlong)
        {
            distanceAlong = 0f;
            if (WorldPathPoints == null || WorldPathPoints.Count < 2)
                return false;

            float halfW = TrailWidth * 0.5f;
            float bestDistSq = float.MaxValue;
            float bestAlong = 0f;
            float cumDist = 0f;

            for (int i = 0; i < WorldPathPoints.Count - 1; i++)
            {
                var pA = WorldPathPoints[i];
                var pB = WorldPathPoints[i + 1];

                float ax = pA.X, az = pA.Z;
                float bx = pB.X, bz = pB.Z;

                float abx = bx - ax, abz = bz - az;
                float segLenSq = abx * abx + abz * abz;

                float t = 0f;
                if (segLenSq > 0.0001f)
                {
                    t = ((x - ax) * abx + (z - az) * abz) / segLenSq;
                    if (t < 0f) t = 0f;
                    else if (t > 1f) t = 1f;
                }

                float closestX = ax + abx * t;
                float closestZ = az + abz * t;
                float dx = x - closestX;
                float dz2 = z - closestZ;
                float dSq = dx * dx + dz2 * dz2;

                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    float segLen = (float)System.Math.Sqrt(segLenSq);
                    bestAlong = cumDist + segLen * t;
                }

                cumDist += Vector3f.Distance(pA, pB);
            }

            if (bestDistSq <= halfW * halfW)
            {
                distanceAlong = bestAlong;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Generates left and right boundary edges from the centerline path.
        /// Perpendicular is computed in the XZ plane only (Y-up world) so that
        /// boundary width matches the horizontal tree-clearing corridor exactly.
        /// Uses miter joins at interior points so the perpendicular distance stays
        /// constant through curves.  Y is copied from the centerline and will be
        /// terrain-projected by TrailDrawer after this call.
        /// </summary>
        public void GenerateBoundaries()
        {
            LeftBoundaryPoints.Clear();
            RightBoundaryPoints.Clear();

            if (WorldPathPoints.Count < 2)
                return;

            float halfWidth = TrailWidth / 2f;
            const float MAX_MITER = 2f;

            for (int i = 0; i < WorldPathPoints.Count; i++)
            {
                Vector3f pt = WorldPathPoints[i];
                float perpX, perpZ;
                float miterScale = 1f;

                if (i == 0)
                {
                    XZPerp(WorldPathPoints[0], WorldPathPoints[1], out perpX, out perpZ);
                }
                else if (i == WorldPathPoints.Count - 1)
                {
                    XZPerp(WorldPathPoints[i - 1], WorldPathPoints[i], out perpX, out perpZ);
                }
                else
                {
                    // Miter join: average the normals of the two adjacent segments
                    // then scale so the perpendicular distance to each segment stays
                    // exactly halfWidth.
                    float n1x, n1z, n2x, n2z;
                    XZPerp(WorldPathPoints[i - 1], WorldPathPoints[i], out n1x, out n1z);
                    XZPerp(WorldPathPoints[i], WorldPathPoints[i + 1], out n2x, out n2z);

                    perpX = n1x + n2x;
                    perpZ = n1z + n2z;
                    float mLen = (float)System.Math.Sqrt(perpX * perpX + perpZ * perpZ);

                    if (mLen < 0.0001f)
                    {
                        perpX = n1x;
                        perpZ = n1z;
                    }
                    else
                    {
                        perpX /= mLen;
                        perpZ /= mLen;

                        float dot = perpX * n1x + perpZ * n1z;
                        if (dot > 0.0001f)
                            miterScale = 1f / dot;
                        else
                            miterScale = MAX_MITER;

                        if (miterScale > MAX_MITER)
                            miterScale = MAX_MITER;
                    }
                }

                float ox = perpX * halfWidth * miterScale;
                float oz = perpZ * halfWidth * miterScale;

                LeftBoundaryPoints.Add(new Vector3f(pt.X + ox, pt.Y, pt.Z + oz));
                RightBoundaryPoints.Add(new Vector3f(pt.X - ox, pt.Y, pt.Z - oz));
            }
        }

        /// <summary>
        /// Returns the unit-length XZ perpendicular (left-hand normal) of the
        /// segment from <paramref name="a"/> to <paramref name="b"/>.
        /// Y is ignored so the offset is always horizontal.
        /// </summary>
        private static void XZPerp(Vector3f a, Vector3f b, out float perpX, out float perpZ)
        {
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            float len = (float)System.Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.0001f)
            {
                perpX = 1f;
                perpZ = 0f;
                return;
            }
            dx /= len;
            dz /= len;
            perpX = -dz;
            perpZ = dx;
        }
    }
}
