using UnityEngine;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Continuously moves chairs along the cable loop like a conveyor belt.
    /// Up-lane chairs travel base → top; down-lane chairs travel top → base.
    /// When a chair reaches the end of its lane it wraps to the start
    /// (no teleporting -- the offset is a continuous float modulo).
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

        // ── Lane offsets ────────────────────────────────────────────────
        private float _upX;
        private float _downX;
        private float _chairY;

        // ── Chair lists ─────────────────────────────────────────────────
        private List<GameObject> _chairsUp;
        private List<GameObject> _chairsDown;
        private int _chairCount;

        // ── Conveyor phase (0 → 1, wraps) ──────────────────────────────
        private float _phase;

        private bool _initialised;
        
        // ── Time control ────────────────────────────────────────────────
        private SimulationRunner _simulationRunner;

        /// <summary>
        /// Called by LiftPrefabBuilder after hierarchy is built.
        /// </summary>
        public void Initialise(LiftInstance inst, Vector3 basePos, Vector3 topPos,
            float upX, float downX, float chairY, SimulationRunner simulationRunner)
        {
            _basePos = basePos;
            _topPos  = topPos;
            _upX     = upX;
            _downX   = downX;
            _chairY  = chairY;
            _simulationRunner = simulationRunner;

            Vector3 delta = topPos - basePos;
            _length = delta.magnitude;
            if (_length < 0.01f) _length = 0.01f;
            _dir = delta / _length;

            _right = Vector3.Cross(Vector3.up, _dir).normalized;
            if (_right.sqrMagnitude < 0.001f) _right = Vector3.right;

            _chairsUp   = inst.ChairsUp   ?? new List<GameObject>();
            _chairsDown = inst.ChairsDown ?? new List<GameObject>();
            _chairCount = _chairsUp.Count; // same count for both lanes

            _phase = 0f;
            _initialised = true;
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
            float phaseSpeed = _speed / _length; // fraction of length per second
            _phase += phaseSpeed * effectiveDeltaTime;
            if (_phase >= 1f) _phase -= 1f;

            Quaternion upRot = Quaternion.LookRotation(_dir, Vector3.up);
            Quaternion downRot = upRot * Quaternion.Euler(0f, 180f, 0f);

            for (int i = 0; i < _chairCount; i++)
            {
                // Each chair is evenly spaced: its base offset is i / count
                float baseT = (float)i / _chairCount;

                // Up lane: base → top
                float tUp = (baseT + _phase) % 1f;
                Vector3 upPos = Vector3.Lerp(_basePos, _topPos, tUp)
                                + _right * _upX
                                + Vector3.up * _chairY;

                if (_chairsUp[i] != null)
                {
                    _chairsUp[i].transform.position = upPos;
                    _chairsUp[i].transform.rotation = upRot;
                }

                // Down lane: top → base (reversed)
                float tDown = (baseT + _phase) % 1f;
                Vector3 downPos = Vector3.Lerp(_topPos, _basePos, tDown)
                                  + _right * _downX
                                  + Vector3.up * _chairY;

                if (_chairsDown[i] != null)
                {
                    _chairsDown[i].transform.position = downPos;
                    _chairsDown[i].transform.rotation = downRot;
                }
            }
        }
    }
}
