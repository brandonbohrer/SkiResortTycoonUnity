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
    /// * Lateral offset uses multi-octave Perlin noise for natural S-curves
    ///   and carving, hard-clamped to 92 % of half-trail-width.
    /// </summary>
    public class SkierMotionController
    {
        // ── Configuration (set once at creation) ────────────────────────
        private readonly int _skierId;            // unique seed for Perlin
        private readonly Transform _transform;
        private readonly float _heightOffset;
        private readonly System.Func<Vector3, float?> _terrainHeightSampler;

        // ── Terrain grounding (Y smoothing) ───────────────────────────
        private float _groundedY;
        private bool _groundedYInitialized;

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

        // ── Chair attachment (skier rides a specific chair) ──────────────
        private LiftChairMover _chairMover;
        private int _assignedChairIndex = -1;

        // ── Walk-to-lift target ─────────────────────────────────────────
        private Vector3 _walkTarget;

        // ── Lateral offset (corridor-aware) ───────────────────────────
        private float _lateralOffset;             // -1..1  normalised
        private float _preferredLane;             // per-skier baseline side bias (-0.7..0.7)
        private float _carvingAmplitude;          // per-skier turn width multiplier (0.4..1.0)
        private const float LATERAL_DRIFT_SPEED = 1.2f;
        private const float MAX_LATERAL_RATIO = 0.92f;  // nearly full corridor width

        // ── Anti-teleport smoothing ─────────────────────────────────────
        private Vector3 _smoothedPosition;
        private bool _positionInitialized;

        // ── Arc transition (smooth bezier curve between trails) ────────
        private bool _isTransitioning;
        private Vector3 _arcP0, _arcP1, _arcP2, _arcP3; // cubic bezier control points
        private float _arcStartDist;   // _distanceAlongTrail at arc start
        private float _arcMergeDist;   // _distanceAlongTrail where arc ends

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
        public SkierMotionController(int skierId, Transform transform, float heightOffset,
            System.Func<Vector3, float?> terrainHeightSampler = null)
        {
            _skierId = skierId;
            _transform = transform;
            _heightOffset = heightOffset;
            _terrainHeightSampler = terrainHeightSampler;
            _preferredLane = Random.Range(-0.7f, 0.7f);
            _carvingAmplitude = Random.Range(0.4f, 1.0f);
            _lateralOffset = _preferredLane;
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
            _isTransitioning = false;
            _preferredLane = Random.Range(-0.7f, 0.7f);
            _lateralOffset = _preferredLane;
        }

        /// <summary>
        /// Switch to a new trail via a smooth cubic-bezier arc.
        /// Captures the skier's current facing (old trail tangent) and builds a
        /// hermite-derived curve that ends at a point downstream on the new trail,
        /// giving a natural carved turn rather than a lateral slide.
        /// </summary>
        public void SwitchTrail(TrailData newTrail, Vector3 currentWorldPos)
        {
            Vector3 oldTangent = _currentTangent;

            _currentTrail = newTrail;
            CacheTrailLengths(newTrail);

            float closestDist = FindClosestDistanceOnTrail(currentWorldPos, newTrail);

            // Pick a merge point downstream so the arc has room to curve naturally.
            // Faster skiers get a wider turn radius.
            float lookAhead = Mathf.Clamp(BaseSkiSpeed * 1.5f, 8f, 20f);
            float mergeDist = Mathf.Min(closestDist + lookAhead, _trailTotalLength);

            Vector3 mergePos, mergeTangent;
            float mergeWidth;
            SampleTrail(mergeDist, out mergePos, out mergeTangent, out mergeWidth);

            // Re-roll preferred lane for the new trail so the skier doesn't
            // always ride the same side every run
            _preferredLane = Random.Range(-0.7f, 0.7f);

            // Offset the merge target laterally so the arc doesn't always
            // aim dead-center — each skier enters the new trail at a unique spot
            float mergeOffset = Random.Range(-0.5f, 0.5f);
            float halfW = mergeWidth * 0.5f;
            Vector3 mergePerp = new Vector3(-mergeTangent.z, 0f, mergeTangent.x);
            if (mergePerp.sqrMagnitude > 0.0001f) mergePerp.Normalize();
            mergePos += mergePerp * (mergeOffset * halfW);

            // Hermite → cubic bezier control points.
            // Handle length scales with XZ chord distance for proportional curvature.
            float chordXZ = Vector3.Distance(
                new Vector3(currentWorldPos.x, 0f, currentWorldPos.z),
                new Vector3(mergePos.x, 0f, mergePos.z));
            float handleLen = Mathf.Max(chordXZ * 0.4f, 3f);

            _arcP0 = currentWorldPos;
            _arcP1 = currentWorldPos + oldTangent * handleLen;
            _arcP2 = mergePos - mergeTangent * handleLen;
            _arcP3 = mergePos;

            _arcStartDist = closestDist;
            _arcMergeDist = mergeDist;
            _distanceAlongTrail = closestDist;

            _isTransitioning = true;
            _lateralOffset = mergeOffset;
            ReachedTrailEnd = false;
        }

        /// <summary>Assign the lift the skier is about to ride (no chair attachment).</summary>
        public void SetLift(LiftData lift)
        {
            _currentLift = lift;
            _liftProgress = 0f;
            ReachedLiftTop = false;
            _chairMover = null;
            _assignedChairIndex = -1;
        }

        /// <summary>
        /// Assign the lift with a specific chair to ride.
        /// The skier will snap to the chair's position each frame.
        /// </summary>
        public void SetLift(LiftData lift, LiftChairMover mover, int chairIndex)
        {
            _currentLift = lift;
            _liftProgress = 0f;
            ReachedLiftTop = false;
            _chairMover = mover;
            _assignedChairIndex = chairIndex;
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

            // 0 = WalkingToLift, 1 = RidingLift, 2 = SkiingTrail, 3 = WalkingToLodge, 5 = ReturningToBase
            switch (phase)
            {
                case 0: targetPos = TickWalkToLift(dt);   break;
                case 1: targetPos = TickRideLift(dt);     break;
                case 2: targetPos = TickSkiTrail(dt);     break;
                case 3: targetPos = TickWalkToLift(dt);   break; // WalkingToLodge uses same walk logic
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

            // Ground to terrain while walking
            next = GroundToTerrain(next);
            return next;
        }

        private Vector3 TickRideLift(float dt)
        {
            if (_currentLift == null) return _smoothedPosition;

            // Offset to lower skier from chair pivot into the seat
            const float SKIER_SEAT_Y_OFFSET = -3.25f;
            const float SKIER_SEAT_FORWARD_OFFSET = 0.5f;

            // ── Chair-attached mode: snap to chair position ──────────
            if (_chairMover != null && _assignedChairIndex >= 0)
            {
                Vector3 chairPos = _chairMover.GetUpChairPosition(_assignedChairIndex);
                float progress = _chairMover.GetUpChairProgress(_assignedChairIndex);

                if (progress >= 0.95f)
                {
                    ReachedLiftTop = true;
                    _chairMover.ReleaseChair(_assignedChairIndex);
                    _chairMover = null;
                    _assignedChairIndex = -1;
                }

                // Tangent is lift direction
                Vector3 start = V3f(_currentLift.StartPosition);
                Vector3 end = V3f(_currentLift.EndPosition);
                Vector3 liftDir = (end - start);
                liftDir.y = 0;
                if (liftDir.sqrMagnitude > 0.001f) _currentTangent = liftDir.normalized;

                // Sit in the chair: lower Y and nudge forward
                chairPos.y += SKIER_SEAT_Y_OFFSET;
                chairPos += _currentTangent * SKIER_SEAT_FORWARD_OFFSET;
                return chairPos;
            }

            // ── Fallback: independent movement (no chair available) ──
            float liftLength = _currentLift.Length;
            if (liftLength <= 0f) liftLength = 1f;

            _liftProgress += (LiftSpeed / liftLength) * dt;

            if (_liftProgress >= 1f)
            {
                _liftProgress = 1f;
                ReachedLiftTop = true;
            }

            Vector3 startFb = V3f(_currentLift.StartPosition);
            Vector3 endFb   = V3f(_currentLift.EndPosition);
            
            // Position along lift path
            Vector3 pos = Vector3.Lerp(startFb, endFb, _liftProgress);
            
            // Add chair height so skier rides at chair level
            const float CHAIR_HEIGHT = 7.825f;
            pos.y += CHAIR_HEIGHT + SKIER_SEAT_Y_OFFSET;

            // Tangent is lift direction
            Vector3 liftDirFb = (endFb - startFb);
            liftDirFb.y = 0;
            if (liftDirFb.sqrMagnitude > 0.001f) _currentTangent = liftDirFb.normalized;

            return pos;
        }

        private Vector3 TickSkiTrail(float dt)
        {
            if (_currentTrail == null || _trailTotalLength <= 0f)
                return _smoothedPosition;

            // ── Slope-based speed ──────────────────────────────────
            float slope = GetSlopeAtCurrentDistance();
            float speedMult = Mathf.Lerp(0.6f, 1.8f, Mathf.Clamp01(slope / 45f));
            float effectiveSpeed = BaseSkiSpeed * speedMult;

            _distanceAlongTrail += effectiveSpeed * dt;

            if (_distanceAlongTrail >= _trailTotalLength)
            {
                _distanceAlongTrail = _trailTotalLength;
                if (!_isTransitioning)
                    ReachedTrailEnd = true;
            }

            // ── Arc transition: follow bezier curve to new trail ────
            if (_isTransitioning)
            {
                float arcSpan = _arcMergeDist - _arcStartDist;
                float t = arcSpan > 0.01f
                    ? Mathf.Clamp01((_distanceAlongTrail - _arcStartDist) / arcSpan)
                    : 1f;

                Vector3 arcPos = EvalBezier(_arcP0, _arcP1, _arcP2, _arcP3, t);
                Vector3 arcTan = EvalBezierDerivative(_arcP0, _arcP1, _arcP2, _arcP3, t);
                arcTan.y = 0f;
                if (arcTan.sqrMagnitude > 0.001f)
                    arcTan.Normalize();
                else
                    arcTan = _currentTangent;

                _currentTangent = arcTan;

                if (t >= 1f)
                {
                    _isTransitioning = false;
                    _distanceAlongTrail = _arcMergeDist;
                }

                return GroundToTerrain(arcPos);
            }

            // ── Normal trail following ──────────────────────────────
            UpdateLateralOffset(dt);

            Vector3 centerPos;
            Vector3 tangent;
            float trailWidth;
            SampleTrail(_distanceAlongTrail, out centerPos, out tangent, out trailWidth);

            Vector3 perp = new Vector3(-tangent.z, 0f, tangent.x);
            if (perp.sqrMagnitude < 0.0001f)
                perp = Vector3.right;
            perp.Normalize();

            float halfW = trailWidth * 0.5f;
            Vector3 trailTarget = centerPos + perp * (_lateralOffset * halfW);

            _currentTangent = tangent;

            return GroundToTerrain(trailTarget);
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
        //  Lateral offset  (corridor-aware, multi-octave)
        // ─────────────────────────────────────────────────────────────────
        private void UpdateLateralOffset(float dt)
        {
            float noiseSeed = _skierId * 137.31f;

            // Octave 1 — slow, wide S-curves (macro drift across the corridor)
            float macro = Mathf.PerlinNoise(_distanceAlongTrail * 0.04f, noiseSeed) * 2f - 1f;

            // Octave 2 — faster carving turns layered on top
            float micro = Mathf.PerlinNoise(_distanceAlongTrail * 0.15f, noiseSeed + 50f) * 2f - 1f;

            // Steeper slopes allow wider carves, but even on flats skiers use the corridor
            float slope = GetSlopeAtCurrentDistance();
            float speedFactor = Mathf.Lerp(0.65f, 1.0f, Mathf.Clamp01(slope / 35f));

            // Carving oscillation centered on this skier's preferred lane
            float carving = (macro * 0.65f + micro * 0.35f) * _carvingAmplitude * speedFactor;
            float targetOffset = _preferredLane + carving;

            _lateralOffset = Mathf.MoveTowards(_lateralOffset, targetOffset, LATERAL_DRIFT_SPEED * dt);

            _lateralOffset = Mathf.Clamp(_lateralOffset, -MAX_LATERAL_RATIO, MAX_LATERAL_RATIO);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Terrain grounding
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces the Y component of <paramref name="pos"/> with the actual
        /// terrain surface height plus <c>_heightOffset</c>. Lerps toward the
        /// sampled Y to avoid micro-jitter from mesh triangle boundaries.
        /// Falls back to the original Y if no sampler is available.
        /// </summary>
        private Vector3 GroundToTerrain(Vector3 pos)
        {
            if (_terrainHeightSampler == null)
            {
                pos.y += _heightOffset;
                return pos;
            }

            float? terrainY = _terrainHeightSampler(pos);
            if (!terrainY.HasValue)
            {
                pos.y += _heightOffset;
                return pos;
            }

            float targetY = terrainY.Value + _heightOffset;

            if (!_groundedYInitialized)
            {
                _groundedY = targetY;
                _groundedYInitialized = true;
            }
            else
            {
                // Smooth toward sampled Y to prevent jitter from mesh triangle edges
                _groundedY = Mathf.Lerp(_groundedY, targetY, 0.35f);
            }

            pos.y = _groundedY;
            return pos;
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
        //  Bezier helpers (arc transitions)
        // ─────────────────────────────────────────────────────────────────

        private static Vector3 EvalBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float uuu = uu * u;
            float tt = t * t;
            float ttt = tt * t;
            return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
        }

        private static Vector3 EvalBezierDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
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
