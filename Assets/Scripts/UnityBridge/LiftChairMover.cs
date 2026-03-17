using UnityEngine;
using System.Collections.Generic;

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
    /// Attached automatically by <see cref="LiftPrefabBuilder.BuildLift"/>.
    /// </summary>
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

        // ── Chair occupancy (for skier attachment) ──────────────────────
        private bool[] _occupied;

        // ── Conveyor phase (0 → 1, wraps) ──────────────────────────────
        private float _phase;

        private bool _initialised;
        
        // ── Time control ────────────────────────────────────────────────
        private SimulationRunner _simulationRunner;

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>Number of chairs per lane.</summary>
        public int ChairCount => _chairCount;

        /// <summary>
        /// Called by LiftPrefabBuilder after hierarchy is built.
        /// </summary>
        public void Initialise(LiftInstance inst, Vector3 basePos, Vector3 topPos,
            float upX, float downX, float chairY, SimulationRunner simulationRunner, float chairSpeed)
        {
            _basePos = basePos;
            _topPos  = topPos;
            _upX     = upX;
            _downX   = downX;
            _chairY  = chairY;
            _simulationRunner = simulationRunner;
            if (chairSpeed > 0.01f)
                _speed = chairSpeed;

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

            _occupied = new bool[_chairCount];

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

        /// <summary>
        /// Claim the nearest unoccupied up-lane chair to a world position.
        /// Only considers chairs within the bottom 5% of the lift path
        /// and within 20m to prevent skiers from flying up to chairs.
        /// Returns the chair index, or -1 if no chairs available.
        /// </summary>
        public int ClaimNearestUpChair(Vector3 worldPos)
        {
            if (!_initialised || _chairCount == 0) return -1;

            int bestIdx = -1;
            float bestDist = float.MaxValue;
            const float MAX_CLAIM_DISTANCE = 20f;
            const float MAX_PROGRESS = 0.05f; // bottom 5% only

            for (int i = 0; i < _chairCount; i++)
            {
                if (_occupied[i]) continue;
                if (_chairsUp[i] == null) continue;

                // Only claim chairs at the very bottom of the lift
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

            if (bestIdx >= 0)
            {
                _occupied[bestIdx] = true;
            }

            return bestIdx;
        }

        /// <summary>
        /// Release a previously claimed chair.
        /// </summary>
        public void ReleaseChair(int index)
        {
            if (index >= 0 && index < _chairCount)
                _occupied[index] = false;
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
        /// Claim a specific chair by index (for restore from save). Does not check position.
        /// </summary>
        public void ClaimChairByIndex(int index)
        {
            if (index >= 0 && index < _chairCount)
                _occupied[index] = true;
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
