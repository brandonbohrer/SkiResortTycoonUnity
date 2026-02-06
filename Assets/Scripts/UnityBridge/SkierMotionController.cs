using UnityEngine;
using System.Collections.Generic;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Handles all skier position, rotation, and boundary math.
    /// Owns the "where is the skier right now?" question so that
    /// SkierVisualizer can focus on AI / lifecycle decisions.
    ///
    /// Key design choices
    /// ------------------
    /// * Distance-based trail following (float metres along polyline)
    ///   instead of 0-1 progress.  This decouples speed from trail length
    ///   and makes slope-speed trivial.
    /// * Anti-teleport: every frame, the final world position is capped via
    ///   Vector3.MoveTowards so the model can never jump more than
    ///   maxSpeed * 1.5 * dt in a single frame.
    /// * Rotation is computed from the trail tangent (or lift direction),
    ///   never from position-delta, eliminating wrong-facing on transitions.
    /// * Lateral offset uses Perlin noise for natural S-curves and is
    ///   hard-clamped to 85 % of half-trail-width.
    /// </summary>
    public class SkierMotionController
    {
        // ── Configuration (set once at creation) ────────────────────────
        private readonly int _skierId;            // unique seed for Perlin
        private readonly Transform _transform;
        private readonly float _heightOffset;

        // ── Speeds (may be updated externally) ──────────────────────────
        public float WalkSpeed   { get; set; } = 4f;
        public float LiftSpeed   { get; set; } = 2f;
        public float BaseSkiSpeed { get; set; } = 5f;

        // ── Trail state ─────────────────────────────────────────────────
        private TrailData _currentTrail;
        private float _distanceAlongTrail;        // metres from trail start
        private float _trailTotalLength;          // cached arc-length
        private float[] _segmentCumulativeDistances; // cumulative dist at end of each segment

        // ── Lift state ──────────────────────────────────────────────────
        private LiftData _currentLift;
        private float _liftProgress;              // 0-1 along lift

        // ── Walk-to-lift target ─────────────────────────────────────────
        private Vector3 _walkTarget;

        // ── Lateral offset (boundary-aware) ─────────────────────────────
        private float _lateralOffset;             // -1..1  normalised
        private const float LATERAL_DRIFT_SPEED = 0.6f;
        private const float MAX_LATERAL_RATIO = 0.85f;  // stay off the very edge

        // ── Anti-teleport smoothing ─────────────────────────────────────
        private Vector3 _smoothedPosition;
        private bool _positionInitialized;

        // ── Rotation state ──────────────────────────────────────────────
        private Vector3 _currentTangent = Vector3.forward;

        // ── Phase completion flags (read by SkierVisualizer) ────────────
        /// <summary>True the frame the skier arrives at the lift bottom.</summary>
        public bool ReachedLiftBottom { get; private set; }
        /// <summary>True the frame the skier reaches lift top.</summary>
        public bool ReachedLiftTop { get; private set; }
        /// <summary>True the frame the skier finishes the current trail.</summary>
        public bool ReachedTrailEnd { get; private set; }

        /// <summary>
        /// 0-1 progress along current trail (for AI junction checks).
        /// Derived from distance / total length.
        /// </summary>
        public float TrailProgress =>
            _trailTotalLength > 0f ? Mathf.Clamp01(_distanceAlongTrail / _trailTotalLength) : 0f;

        /// <summary>Distance in metres travelled so far on current trail.</summary>
        public float DistanceAlongTrail => _distanceAlongTrail;

        /// <summary>Cached total arc-length of current trail.</summary>
        public float TrailTotalLength => _trailTotalLength;

        /// <summary>Current lateral offset (-1..1).</summary>
        public float LateralOffset => _lateralOffset;

        // ─────────────────────────────────────────────────────────────────
        //  Construction
        // ─────────────────────────────────────────────────────────────────
        public SkierMotionController(int skierId, Transform transform, float heightOffset)
        {
            _skierId = skierId;
            _transform = transform;
            _heightOffset = heightOffset;
            _lateralOffset = Random.Range(-0.6f, 0.6f);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Public setters (called by SkierVisualizer on phase changes)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Assign the trail the skier is about to ski.</summary>
        public void SetTrail(TrailData trail, float startDistance = 0f)
        {
            _currentTrail = trail;
            _distanceAlongTrail = startDistance;
            CacheTrailLengths(trail);
            ReachedTrailEnd = false;
        }

        /// <summary>Switch to a new trail mid-run, preserving lateral offset direction.</summary>
        public void SwitchTrail(TrailData newTrail, Vector3 currentWorldPos)
        {
            _currentTrail = newTrail;
            CacheTrailLengths(newTrail);
            _distanceAlongTrail = FindClosestDistanceOnTrail(currentWorldPos, newTrail);
            // Preserve lateral offset sign, just slightly randomise magnitude
            _lateralOffset = Mathf.Sign(_lateralOffset) * Random.Range(0.1f, 0.7f);
            ReachedTrailEnd = false;
        }

        /// <summary>Assign the lift the skier is about to ride.</summary>
        public void SetLift(LiftData lift)
        {
            _currentLift = lift;
            _liftProgress = 0f;
            ReachedLiftTop = false;
        }

        /// <summary>Set the position the skier should walk toward (lift bottom).</summary>
        public void SetWalkTarget(Vector3 target)
        {
            _walkTarget = target;
            ReachedLiftBottom = false;
        }

        /// <summary>Teleport the model to a position (used once at spawn).</summary>
        public void Teleport(Vector3 position)
        {
            _smoothedPosition = position;
            _transform.position = position;
            _positionInitialized = true;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Tick  (called once per frame by SkierVisualizer)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Advance movement for one frame.
        /// <paramref name="phase"/> tells the controller which motion mode to use.
        /// </summary>
        public void Tick(float dt, int phase, Animator animator)
        {
            // Reset per-frame flags
            ReachedLiftBottom = false;
            ReachedLiftTop = false;
            ReachedTrailEnd = false;

            Vector3 targetPos = _smoothedPosition;

            // 0 = WalkingToLift, 1 = RidingLift, 2 = SkiingTrail
            switch (phase)
            {
                case 0: targetPos = TickWalkToLift(dt);   break;
                case 1: targetPos = TickRideLift(dt);     break;
                case 2: targetPos = TickSkiTrail(dt);     break;
            }

            // ── Anti-teleport: cap per-frame movement ──────────────
            float maxSpeed = Mathf.Max(BaseSkiSpeed, LiftSpeed, WalkSpeed) * 2f;
            float maxStep = maxSpeed * dt;

            if (!_positionInitialized)
            {
                _smoothedPosition = targetPos;
                _positionInitialized = true;
            }
            else
            {
                _smoothedPosition = Vector3.MoveTowards(_smoothedPosition, targetPos, maxStep);
            }

            _transform.position = _smoothedPosition;

            // ── Rotation from tangent ──────────────────────────────
            ApplyRotation(dt, phase);

            // ── Animation parameters ───────────────────────────────
            if (animator != null)
            {
                animator.SetBool("IsRidingLift", phase == 1);
                animator.SetBool("IsSkiing", phase == 2);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Private: per-phase tick methods
        // ─────────────────────────────────────────────────────────────────

        private Vector3 TickWalkToLift(float dt)
        {
            // Walk toward _walkTarget at WalkSpeed
            Vector3 current = _smoothedPosition;
            Vector3 next = Vector3.MoveTowards(current, _walkTarget, WalkSpeed * dt);

            // Direction for rotation
            Vector3 dir = (_walkTarget - current);
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f) _currentTangent = dir.normalized;

            if (Vector3.Distance(next, _walkTarget) < 0.5f)
            {
                ReachedLiftBottom = true;
                return _walkTarget;
            }
            return next;
        }

        private Vector3 TickRideLift(float dt)
        {
            if (_currentLift == null) return _smoothedPosition;

            float liftLength = _currentLift.Length;
            if (liftLength <= 0f) liftLength = 1f;

            _liftProgress += (LiftSpeed / liftLength) * dt;

            if (_liftProgress >= 1f)
            {
                _liftProgress = 1f;
                ReachedLiftTop = true;
            }

            Vector3 start = V3f(_currentLift.StartPosition);
            Vector3 end   = V3f(_currentLift.EndPosition);
            Vector3 pos   = Vector3.Lerp(start, end, _liftProgress);
            pos.y += _heightOffset;

            // Tangent is lift direction
            Vector3 liftDir = (end - start);
            liftDir.y = 0;
            if (liftDir.sqrMagnitude > 0.001f) _currentTangent = liftDir.normalized;

            return pos;
        }

        private Vector3 TickSkiTrail(float dt)
        {
            if (_currentTrail == null || _trailTotalLength <= 0f)
                return _smoothedPosition;

            // ── Slope-based speed ──────────────────────────────────
            float slope = GetSlopeAtCurrentDistance();
            // Steeper → faster.  0.6x on flats, up to 1.8x on 45-degree steeps
            float speedMult = Mathf.Lerp(0.6f, 1.8f, Mathf.Clamp01(slope / 45f));
            float effectiveSpeed = BaseSkiSpeed * speedMult;

            _distanceAlongTrail += effectiveSpeed * dt;

            if (_distanceAlongTrail >= _trailTotalLength)
            {
                _distanceAlongTrail = _trailTotalLength;
                ReachedTrailEnd = true;
            }

            // ── Dynamic lateral offset (Perlin drift) ──────────────
            UpdateLateralOffset(dt);

            // ── Sample position on trail ────────────────────────────
            Vector3 centerPos;
            Vector3 tangent;
            float trailWidth;
            SampleTrail(_distanceAlongTrail, out centerPos, out tangent, out trailWidth);

            // Apply lateral offset perpendicular to tangent in XZ
            Vector3 perp = new Vector3(-tangent.z, 0f, tangent.x);
            if (perp.sqrMagnitude < 0.0001f)
                perp = Vector3.right; // fallback

            perp.Normalize();

            float halfW = trailWidth * 0.5f;
            Vector3 finalPos = centerPos + perp * (_lateralOffset * halfW);
            finalPos.y += _heightOffset;

            // Store tangent for rotation
            _currentTangent = tangent;

            return finalPos;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Trail sampling helpers
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Given a distance along the trail, return the world-space centerline
        /// position, forward tangent, and trail width at that point.
        /// </summary>
        private void SampleTrail(float distance, out Vector3 position, out Vector3 tangent, out float width)
        {
            var pts = _currentTrail.WorldPathPoints;
            if (pts == null || pts.Count < 2)
            {
                position = _smoothedPosition;
                tangent = Vector3.forward;
                width = _currentTrail.TrailWidth;
                return;
            }

            // Clamp distance
            distance = Mathf.Clamp(distance, 0f, _trailTotalLength);

            // Binary-style search: find the segment that contains 'distance'
            int segIdx = 0;
            for (int i = 0; i < _segmentCumulativeDistances.Length; i++)
            {
                if (_segmentCumulativeDistances[i] >= distance)
                {
                    segIdx = i;
                    break;
                }
            }

            float segStart = segIdx > 0 ? _segmentCumulativeDistances[segIdx - 1] : 0f;
            float segEnd = _segmentCumulativeDistances[segIdx];
            float segLen = segEnd - segStart;
            float localT = segLen > 0.001f ? (distance - segStart) / segLen : 0f;

            Vector3 a = V3f(pts[segIdx]);
            Vector3 b = V3f(pts[segIdx + 1]);
            position = Vector3.Lerp(a, b, localT);

            // Tangent: direction of the segment projected onto XZ
            tangent = (b - a);
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 0.0001f)
                tangent.Normalize();
            else
                tangent = Vector3.forward;

            // Width: sample from boundary points if available
            width = _currentTrail.TrailWidth; // default
            if (_currentTrail.LeftBoundaryPoints != null &&
                _currentTrail.RightBoundaryPoints != null &&
                _currentTrail.LeftBoundaryPoints.Count > 0 &&
                _currentTrail.RightBoundaryPoints.Count > 0)
            {
                // Map distance → boundary index
                float normalizedProgress = _trailTotalLength > 0f ? distance / _trailTotalLength : 0f;
                int bIdx = Mathf.Clamp(
                    (int)(normalizedProgress * (_currentTrail.LeftBoundaryPoints.Count - 1)),
                    0, _currentTrail.LeftBoundaryPoints.Count - 1);
                var lp = _currentTrail.LeftBoundaryPoints[bIdx];
                var rp = _currentTrail.RightBoundaryPoints[bIdx];
                width = Vector3.Distance(V3f(lp), V3f(rp));
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Slope helper  (Step 7)
        // ─────────────────────────────────────────────────────────────────
        private float GetSlopeAtCurrentDistance()
        {
            if (_currentTrail == null || _currentTrail.WorldPathPoints == null || _currentTrail.WorldPathPoints.Count < 2)
                return 10f; // default moderate slope

            // Find segment
            int segIdx = 0;
            for (int i = 0; i < _segmentCumulativeDistances.Length; i++)
            {
                if (_segmentCumulativeDistances[i] >= _distanceAlongTrail)
                {
                    segIdx = i;
                    break;
                }
            }

            var pts = _currentTrail.WorldPathPoints;
            Vector3 a = V3f(pts[segIdx]);
            Vector3 b = V3f(pts[segIdx + 1]);

            float dx = b.x - a.x;
            float dz = b.z - a.z;
            float horizontalDist = Mathf.Sqrt(dx * dx + dz * dz);
            float drop = a.y - b.y; // positive = downhill

            if (horizontalDist < 0.01f)
                return drop > 0 ? 90f : 0f;

            // angle in degrees
            return Mathf.Atan2(Mathf.Abs(drop), horizontalDist) * Mathf.Rad2Deg;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Lateral offset  (Step 5 — boundary enforcement)
        // ─────────────────────────────────────────────────────────────────
        private void UpdateLateralOffset(float dt)
        {
            // Perlin noise gives smooth, deterministic S-curves unique per skier
            float noiseInput = _distanceAlongTrail * 0.05f;
            float noiseSeed  = _skierId * 137.31f; // unique offset per skier
            float noiseVal = Mathf.PerlinNoise(noiseInput, noiseSeed);
            float targetOffset = noiseVal * 2f - 1f; // remap 0..1 → -1..1

            _lateralOffset = Mathf.MoveTowards(_lateralOffset, targetOffset, LATERAL_DRIFT_SPEED * dt);

            // Hard-clamp so skier never touches the boundary edge
            _lateralOffset = Mathf.Clamp(_lateralOffset, -MAX_LATERAL_RATIO, MAX_LATERAL_RATIO);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Rotation  (tangent-based, never position-delta)
        // ─────────────────────────────────────────────────────────────────
        private void ApplyRotation(float dt, int phase)
        {
            if (_currentTangent.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(_currentTangent, Vector3.up);
            float slerpSpeed = (phase == 2) ? 8f : 6f; // faster turning while skiing
            _transform.rotation = Quaternion.Slerp(_transform.rotation, target, dt * slerpSpeed);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Trail length caching
        // ─────────────────────────────────────────────────────────────────
        private void CacheTrailLengths(TrailData trail)
        {
            _trailTotalLength = 0f;

            if (trail == null || trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
            {
                _segmentCumulativeDistances = new float[0];
                return;
            }

            var pts = trail.WorldPathPoints;
            int segCount = pts.Count - 1;
            _segmentCumulativeDistances = new float[segCount];

            float cumDist = 0f;
            for (int i = 0; i < segCount; i++)
            {
                cumDist += Vector3.Distance(V3f(pts[i]), V3f(pts[i + 1]));
                _segmentCumulativeDistances[i] = cumDist;
            }
            _trailTotalLength = cumDist > 0f ? cumDist : 1f;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Closest-distance helper (for trail switching)
        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Returns the distance along <paramref name="trail"/> that is closest
        /// to <paramref name="worldPos"/>.  Used for seamless trail switching.
        /// </summary>
        public static float FindClosestDistanceOnTrail(Vector3 worldPos, TrailData trail)
        {
            if (trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                return 0f;

            float bestDist = float.MaxValue;
            float bestAlong = 0f;
            float cumDist = 0f;

            for (int i = 0; i < trail.WorldPathPoints.Count - 1; i++)
            {
                Vector3 a = V3f(trail.WorldPathPoints[i]);
                Vector3 b = V3f(trail.WorldPathPoints[i + 1]);

                Vector3 closest = ClosestPointOnSegment(worldPos, a, b);
                float d = Vector3.Distance(worldPos, closest);

                if (d < bestDist)
                {
                    bestDist = d;
                    bestAlong = cumDist + Vector3.Distance(a, closest);
                }

                cumDist += Vector3.Distance(a, b);
            }

            return bestAlong;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Utility
        // ─────────────────────────────────────────────────────────────────
        private static Vector3 V3f(Vector3f v) => new Vector3(v.X, v.Y, v.Z);

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.magnitude;
            if (len < 0.001f) return a;
            Vector3 dir = ab / len;
            float proj = Mathf.Clamp(Vector3.Dot(point - a, dir), 0f, len);
            return a + dir * proj;
        }
    }
}
