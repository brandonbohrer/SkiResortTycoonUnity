using UnityEngine;
using System.Collections.Generic;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Continuously moves chairs along the cable loop like a conveyor belt.
    /// Up-lane chairs travel base → top; down-lane chairs travel top → base.
    /// Chairs follow the segmented cable path (anchor points) rather than a
    /// straight line, so they match the terrain-following cables.
    ///
    /// Also provides chair-claim API so that skiers can attach to a specific
    /// up-lane chair and ride it to the top.
    ///
    /// Execution order is early so chair transforms are current when skiers Tick.
    /// Attached automatically by <see cref="LiftPrefabBuilder.BuildLift"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LiftChairMover : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] private float _speed = 3f; // metres per second

        // ── Geometry ────────────────────────────────────────────────────
        private Vector3 _basePos;
        private Vector3 _topPos;
        private Vector3 _dir;          // base → top normalised
        private float _length;
        private Vector3 _right;        // perpendicular (for lane offsets)

        // ── Cable polyline (anchor points from base to top) ─────────────
        private Vector3[] _anchors;      // world-space cable-level positions
        private float[] _segStartT;      // cumulative normalised t at each anchor
        private float _totalPolyLength;  // total polyline length in metres

        // ── Lane offsets ────────────────────────────────────────────────
        private float _upX;
        private float _downX;
        private float _chairY;

        // ── Chair lists ─────────────────────────────────────────────────
        private List<GameObject> _chairsUp;
        private List<GameObject> _chairsDown;
        private int _chairCount;

        /// <summary>1 = classic chair; 2 = two skiers can share (pair or solo center).</summary>
        private int _seatsPerChair;

        // ── Chair occupancy (riders currently on each up-lane chair) ───
        private int[] _riderCount;

        // ── Conveyor phase (0 → 1, wraps) ──────────────────────────────
        private float _phase;

        private bool _initialised;
        
        // ── Time control ────────────────────────────────────────────────
        private SimulationRunner _simulationRunner;

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>Number of chairs per lane.</summary>
        public int ChairCount => _chairCount;

        public int SeatsPerChair => _seatsPerChair;

        /// <summary>
        /// Called by LiftPrefabBuilder after hierarchy is built.
        /// </summary>
        public void Initialise(LiftInstance inst, Vector3 basePos, Vector3 topPos,
            float upX, float downX, float chairY, SimulationRunner simulationRunner, float chairSpeed,
            LiftType liftType)
        {
            _basePos = basePos;
            _topPos  = topPos;
            _upX     = upX;
            _downX   = downX;
            _chairY  = chairY;
            _simulationRunner = simulationRunner;
            if (chairSpeed > 0.01f)
                _speed = chairSpeed;

            _seatsPerChair = Mathf.Max(1, LiftTypeSpecs.GetSeatsPerChair(liftType));

            Vector3 delta = topPos - basePos;
            _length = delta.magnitude;
            if (_length < 0.01f) _length = 0.01f;
            _dir = delta / _length;

            _right = Vector3.Cross(Vector3.up, _dir).normalized;
            if (_right.sqrMagnitude < 0.001f) _right = Vector3.right;

            // ── Build polyline from anchor points ────────────────────
            if (inst.CableAnchorPoints != null && inst.CableAnchorPoints.Count >= 2)
            {
                _anchors = inst.CableAnchorPoints.ToArray();
            }
            else
            {
                // Fallback: straight line from base to top at cable height
                _anchors = new Vector3[]
                {
                    basePos + Vector3.up * chairY,
                    topPos + Vector3.up * chairY
                };
            }

            // Pre-compute cumulative normalised t for each anchor
            _totalPolyLength = 0f;
            float[] segLengths = new float[_anchors.Length - 1];
            for (int i = 0; i < _anchors.Length - 1; i++)
            {
                segLengths[i] = Vector3.Distance(_anchors[i], _anchors[i + 1]);
                _totalPolyLength += segLengths[i];
            }
            if (_totalPolyLength < 0.01f) _totalPolyLength = 0.01f;

            _segStartT = new float[_anchors.Length];
            _segStartT[0] = 0f;
            float cumulative = 0f;
            for (int i = 0; i < segLengths.Length; i++)
            {
                cumulative += segLengths[i];
                _segStartT[i + 1] = cumulative / _totalPolyLength;
            }

            _chairsUp   = inst.ChairsUp   ?? new List<GameObject>();
            _chairsDown = inst.ChairsDown ?? new List<GameObject>();
            _chairCount = _chairsUp.Count; // same count for both lanes

            _riderCount = new int[_chairCount];

            _phase = 0f;
            _initialised = true;
        }

        /// <summary>
        /// Get the world position of up-lane chair at index.
        /// </summary>
        public Vector3 GetUpChairPosition(int index)
        {
            if (!_initialised || index < 0 || index >= _chairCount)
                return Vector3.zero;
            
            if (_chairsUp[index] != null)
                return _chairsUp[index].transform.position;
            
            return Vector3.zero;
        }

        /// <summary>
        /// Bench lateral (perpendicular to lift, horizontal) for seating offsets.
        /// </summary>
        public Vector3 GetBenchRightWorld()
        {
            return _right.sqrMagnitude > 0.0001f ? _right : Vector3.right;
        }

        /// <summary>
        /// Get the 0-1 progress of up-lane chair at index along the lift.
        /// 0 = at base, 1 = at top.
        /// </summary>
        public float GetUpChairProgress(int index)
        {
            if (!_initialised || index < 0 || index >= _chairCount)
                return 0f;
            
            float baseT = (float)index / _chairCount;
            return (baseT + _phase) % 1f;
        }

        /// <summary>Up-lane chair transform (rotation is piecewise-constant between cable anchors).</summary>
        public Transform GetUpChairTransform(int index)
        {
            if (!_initialised || index < 0 || index >= _chairCount)
                return null;
            return _chairsUp[index] != null ? _chairsUp[index].transform : null;
        }

        /// <summary>
        /// Index of the polyline segment (0 .. anchorCount-2) the given up-lane chair is on.
        /// Changes when the chair passes a cable anchor / pillar.
        /// </summary>
        public int GetUpChairPolylineSegmentIndex(int index)
        {
            if (!_initialised || index < 0 || index >= _chairCount ||
                _anchors == null || _anchors.Length < 2 || _segStartT == null)
                return 0;

            float t = Mathf.Clamp01(GetUpChairProgress(index));

            for (int i = 0; i < _anchors.Length - 1; i++)
            {
                float tEnd = _segStartT[i + 1];
                if (t <= tEnd || i == _anchors.Length - 2)
                    return i;
            }

            return _anchors.Length - 2;
        }

        /// <summary>
        /// One skier boards: 1-seat lift, or 2-seat lift alone (center seat).
        /// </summary>
        public bool TryClaimSoloBoarding(Vector3 worldPos, out int chairIndex)
        {
            chairIndex = -1;
            if (!_initialised || _chairCount == 0) return false;

            int bestIdx = FindBestEmptyChairIndex(worldPos, out float bestDist);
            if (bestIdx < 0) return false;

            _riderCount[bestIdx] = 1;
            chairIndex = bestIdx;
            return true;
        }

        /// <summary>
        /// Two skiers board together on the same chair (only for 2-seat lifts).
        /// </summary>
        public bool TryClaimPairBoarding(Vector3 worldPosA, Vector3 worldPosB, out int chairIndex)
        {
            chairIndex = -1;
            if (!_initialised || _chairCount == 0 || _seatsPerChair < 2) return false;

            Vector3 mid = (worldPosA + worldPosB) * 0.5f;
            int bestIdx = FindBestEmptyChairIndex(mid, out _);
            if (bestIdx < 0) return false;

            _riderCount[bestIdx] = 2;
            chairIndex = bestIdx;
            return true;
        }

        private int FindBestEmptyChairIndex(Vector3 worldPos, out float bestDist)
        {
            bestDist = float.MaxValue;
            int bestIdx = -1;
            const float MAX_CLAIM_DISTANCE = 20f;
            const float MAX_PROGRESS = 0.05f;

            for (int i = 0; i < _chairCount; i++)
            {
                if (_riderCount[i] != 0) continue;
                if (_chairsUp[i] == null) continue;

                float tUp = GetUpChairProgress(i);
                if (tUp > MAX_PROGRESS) continue;

                float d = Vector3.Distance(worldPos, _chairsUp[i].transform.position);
                if (d > MAX_CLAIM_DISTANCE) continue;

                if (d < bestDist)
                {
                    bestDist = d;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        /// <summary>
        /// Release one rider from this chair when they exit at the top.
        /// </summary>
        public void ReleaseChair(int index)
        {
            if (index >= 0 && index < _chairCount && _riderCount[index] > 0)
                _riderCount[index]--;
        }

        /// <summary>
        /// Set the conveyor phase (0-1) for exact restore when loading a save.
        /// Puts all chairs at the correct position so a skier can be restored to their exact seat.
        /// Call ApplyPhaseImmediate() after this to update chair transforms in the same frame.
        /// </summary>
        public void SetPhase(float phase)
        {
            _phase = Mathf.Repeat(phase, 1f);
        }

        /// <summary>
        /// Updates all chair positions to match current _phase. Call after SetPhase when loading a save.
        /// </summary>
        public void ApplyPhaseImmediate()
        {
            if (!_initialised || _chairCount == 0) return;
            for (int i = 0; i < _chairCount; i++)
            {
                float baseT = (float)i / _chairCount;
                float tUp = (baseT + _phase) % 1f;
                Vector3 upCenter = SamplePolyline(tUp);
                Vector3 upPos = upCenter + _right * _upX;
                if (_chairsUp[i] != null)
                {
                    _chairsUp[i].transform.position = upPos;
                    _chairsUp[i].transform.rotation = GetPolylineRotation(tUp);
                }
                float tDown = (baseT + _phase) % 1f;
                Vector3 downCenter = SamplePolyline(1f - tDown);
                Vector3 downPos = downCenter + _right * _downX;
                if (_chairsDown[i] != null)
                {
                    _chairsDown[i].transform.position = downPos;
                    _chairsDown[i].transform.rotation = GetPolylineRotation(1f - tDown) * Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }

        /// <summary>
        /// Restore-from-save: mark one rider occupying this chair (call once per restored skier on that chair).
        /// </summary>
        public void RegisterRestoredRider(int index)
        {
            if (index >= 0 && index < _chairCount && _riderCount[index] < _seatsPerChair)
                _riderCount[index]++;
        }

        private void Update()
        {
            if (!_initialised || _chairCount == 0) return;

            // Get effective delta time (respects pause and game speed)
            float effectiveDeltaTime = Time.deltaTime;
            if (_simulationRunner != null && _simulationRunner.Sim != null && _simulationRunner.Sim.TimeController != null)
            {
                effectiveDeltaTime = _simulationRunner.Sim.TimeController.GetEffectiveDeltaTime(Time.deltaTime);
            }

            // Advance conveyor phase
            float phaseSpeed = _speed / _totalPolyLength; // fraction of path per second
            _phase += phaseSpeed * effectiveDeltaTime;
            if (_phase >= 1f) _phase -= 1f;

            for (int i = 0; i < _chairCount; i++)
            {
                // Each chair is evenly spaced: its base offset is i / count
                float baseT = (float)i / _chairCount;

                // Up lane: base → top (sample the polyline)
                float tUp = (baseT + _phase) % 1f;
                Vector3 upCenter = SamplePolyline(tUp);
                Vector3 upPos = upCenter + _right * _upX;

                if (_chairsUp[i] != null)
                {
                    _chairsUp[i].transform.position = upPos;
                    _chairsUp[i].transform.rotation = GetPolylineRotation(tUp);
                }

                // Down lane: top → base (reversed)
                float tDown = (baseT + _phase) % 1f;
                Vector3 downCenter = SamplePolyline(1f - tDown);
                Vector3 downPos = downCenter + _right * _downX;

                if (_chairsDown[i] != null)
                {
                    _chairsDown[i].transform.position = downPos;
                    _chairsDown[i].transform.rotation = GetPolylineRotation(1f - tDown) * Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }

        // ── Polyline helpers ─────────────────────────────────────────────

        /// <summary>
        /// Given a normalised t (0-1), return the world-space position along
        /// the cable anchor polyline.
        /// </summary>
        private Vector3 SamplePolyline(float t)
        {
            t = Mathf.Clamp01(t);

            // Find which segment t falls into
            for (int i = 0; i < _anchors.Length - 1; i++)
            {
                float tEnd = _segStartT[i + 1];

                if (t <= tEnd || i == _anchors.Length - 2)
                {
                    float tStart = _segStartT[i];
                    float segRange = tEnd - tStart;
                    float localT = (segRange > 0.0001f) ? (t - tStart) / segRange : 0f;
                    return Vector3.Lerp(_anchors[i], _anchors[i + 1], localT);
                }
            }

            return _anchors[_anchors.Length - 1];
        }

        /// <summary>
        /// Get the forward rotation at a point along the polyline.
        /// </summary>
        private Quaternion GetPolylineRotation(float t)
        {
            t = Mathf.Clamp01(t);

            for (int i = 0; i < _anchors.Length - 1; i++)
            {
                if (t <= _segStartT[i + 1] || i == _anchors.Length - 2)
                {
                    Vector3 segDir = (_anchors[i + 1] - _anchors[i]).normalized;
                    if (segDir.sqrMagnitude < 0.001f) segDir = _dir;
                    return Quaternion.LookRotation(segDir, Vector3.up);
                }
            }

            return Quaternion.LookRotation(_dir, Vector3.up);
        }
    }
}
