using UnityEngine;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// 3D perspective camera controller with orbit, pan, and zoom.
    /// Camera orbits a focus point on the terrain within bounded limits.
    /// Controls:
    ///   - Right-click drag: Orbit (rotate around focus point)
    ///   - Middle-click drag / WASD: Pan
    ///   - Scroll wheel: Zoom (dolly in/out)
    ///   - Q/E: Rotate left/right
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Mountain Reference")]
        [SerializeField] private MountainManager _mountainManager;
        [SerializeField] private bool _autoDetectBounds = true;

        [Header("Orbit Settings")]
        [SerializeField] private float _orbitSensitivity = 0.3f;
        [SerializeField] private float _keyboardRotateSpeed = 90f; // degrees per second
        [SerializeField] private float _minPitch = 10f;   // min angle above horizon
        [SerializeField] private float _maxPitch = 80f;   // max angle (near top-down)
        [SerializeField] private float _defaultYaw = -45f;
        [SerializeField] private float _defaultPitch = 40f;

        [Header("Zoom Settings")]
        [SerializeField] private float _zoomSpeed = 15f;
        [SerializeField] private float _minDistance = 30f;
        [SerializeField] private float _maxDistance = 500f;
        [SerializeField] private float _defaultDistance = 150f;
        [SerializeField] private float _zoomSmoothing = 10f;

        [Header("Pan Settings")]
        [SerializeField] private float _panSpeedKeyboard = 40f;
        [SerializeField] private float _panSpeedMouse = 1f;
        [SerializeField] private int _panMouseButton = 2;   // 0=Left, 1=Right, 2=Middle
        [SerializeField] private int _orbitMouseButton = 1;  // Right-click to orbit

        [Header("Perspective Settings")]
        [SerializeField] private float _fieldOfView = 50f;
        [SerializeField] private float _nearClip = 1f;
        [SerializeField] private float _farClip = 2000f;

        [Header("Focus Bounds (world-space box around mountain)")]
        [SerializeField] private bool _enableBounds = true;
        [SerializeField] private float _boundsMinX = -100f;
        [SerializeField] private float _boundsMaxX = 600f;
        [SerializeField] private float _boundsMinY = -50f;
        [SerializeField] private float _boundsMaxY = 500f;
        [SerializeField] private float _boundsMinZ = -100f;
        [SerializeField] private float _boundsMaxZ = 600f;
        [SerializeField] private float _boundsSoftness = 5f;

        [Header("Debug")]
        [SerializeField] private bool _showFocusGizmo = true;

        private Camera _camera;
        private Vector3 _focusPoint;
        private float _yaw;
        private float _pitch;
        private float _distance;
        private float _targetDistance;

        // Drag state
        private Vector3 _lastMousePosition;
        private bool _isOrbiting;
        private bool _isPanning;

        void Awake()
        {
            _camera = GetComponent<Camera>();

            // Switch to perspective
            _camera.orthographic = false;
            _camera.fieldOfView = _fieldOfView;
            _camera.nearClipPlane = _nearClip;
            _camera.farClipPlane = _farClip;

            // Initialize orbit angles and distance
            _yaw = _defaultYaw;
            _pitch = _defaultPitch;
            _distance = _defaultDistance;
            _targetDistance = _defaultDistance;
        }

        void Start()
        {
            if (_autoDetectBounds)
            {
                InitializeBoundsFromMountain();
            }

            // Start focused on the center of the mountain
            _focusPoint = new Vector3(
                (_boundsMinX + _boundsMaxX) / 2f,
                (_boundsMinY + _boundsMaxY) / 2f,
                (_boundsMinZ + _boundsMaxZ) / 2f
            );

            UpdateCameraTransform();
        }

        /// <summary>
        /// Auto-detect bounds from the mountain mesh renderer.
        /// </summary>
        private void InitializeBoundsFromMountain()
        {
            if (_mountainManager == null)
            {
                Debug.LogWarning("[CameraController] MountainManager not assigned, using default bounds");
                return;
            }

            // Get the mountain mesh via reflection (same pattern as before)
            var mountainMeshField = typeof(MountainManager).GetField("_mountainMesh",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (mountainMeshField == null) return;

            GameObject mountainMesh = mountainMeshField.GetValue(_mountainManager) as GameObject;
            if (mountainMesh == null) return;

            Renderer renderer = mountainMesh.GetComponent<Renderer>();
            if (renderer == null) renderer = mountainMesh.GetComponentInChildren<Renderer>();
            if (renderer == null) return;

            Bounds bounds = renderer.bounds;

            // Set bounds with generous padding
            float padding = 50f;
            _boundsMinX = bounds.min.x - padding;
            _boundsMaxX = bounds.max.x + padding;
            _boundsMinY = bounds.min.y;
            _boundsMaxY = bounds.max.y + padding;
            _boundsMinZ = bounds.min.z - padding;
            _boundsMaxZ = bounds.max.z + padding;

            // Start at the center of the mountain, at mid-height
            _focusPoint = new Vector3(
                bounds.center.x,
                bounds.center.y,
                bounds.center.z
            );

            Debug.Log($"[CameraController] Detected mountain bounds: {bounds.min} to {bounds.max}");
            Debug.Log($"[CameraController] Camera bounds: X[{_boundsMinX:F0},{_boundsMaxX:F0}] Y[{_boundsMinY:F0},{_boundsMaxY:F0}] Z[{_boundsMinZ:F0},{_boundsMaxZ:F0}]");
        }

        void Update()
        {
            HandleOrbit();
            HandlePanKeyboard();
            HandlePanMouse();
            HandleZoom();
            HandleKeyboardRotation();
            ClampFocusPoint();
            SmoothZoom();
            UpdateCameraTransform();
        }

        // ─── Orbit (right-click drag) ───────────────────────────────────

        private void HandleOrbit()
        {
            if (Input.GetMouseButtonDown(_orbitMouseButton))
            {
                _isOrbiting = true;
                _lastMousePosition = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(_orbitMouseButton))
            {
                _isOrbiting = false;
            }

            if (_isOrbiting)
            {
                Vector3 delta = Input.mousePosition - _lastMousePosition;
                _yaw += delta.x * _orbitSensitivity;
                _pitch -= delta.y * _orbitSensitivity;
                _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
                _lastMousePosition = Input.mousePosition;
            }
        }

        // ─── Keyboard rotation (Q / E) ─────────────────────────────────

        private void HandleKeyboardRotation()
        {
            if (Input.GetKey(KeyCode.Q))
                _yaw -= _keyboardRotateSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E))
                _yaw += _keyboardRotateSpeed * Time.deltaTime;
        }

        // ─── Pan with WASD / Arrow keys ────────────────────────────────

        private void HandlePanKeyboard()
        {
            Vector3 input = Vector3.zero;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    input.z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  input.z -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  input.x -= 1f;

            if (input == Vector3.zero) return;

            // Move in camera-relative XZ plane
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            // Scale pan speed by distance so it feels consistent at all zoom levels
            float scaledSpeed = _panSpeedKeyboard * (_distance / _defaultDistance);
            Vector3 panDelta = (forward * input.z + right * input.x) * scaledSpeed * Time.deltaTime;
            _focusPoint += panDelta;
        }

        // ─── Pan with middle-click drag ─────────────────────────────────

        private void HandlePanMouse()
        {
            if (Input.GetMouseButtonDown(_panMouseButton))
            {
                _isPanning = true;
                _lastMousePosition = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(_panMouseButton))
            {
                _isPanning = false;
            }

            if (!_isPanning) return;

            Vector3 currentMouse = Input.mousePosition;
            Vector3 screenDelta = currentMouse - _lastMousePosition;

            // Project mouse delta onto the focus plane (Y = focus height)
            Plane focusPlane = new Plane(Vector3.up, _focusPoint);

            Ray rayLast = _camera.ScreenPointToRay(_lastMousePosition);
            Ray rayCurrent = _camera.ScreenPointToRay(currentMouse);

            float enterLast, enterCurrent;
            if (focusPlane.Raycast(rayLast, out enterLast) &&
                focusPlane.Raycast(rayCurrent, out enterCurrent))
            {
                Vector3 worldLast = rayLast.GetPoint(enterLast);
                Vector3 worldCurrent = rayCurrent.GetPoint(enterCurrent);
                _focusPoint += (worldLast - worldCurrent) * _panSpeedMouse;
            }

            _lastMousePosition = currentMouse;
        }

        // ─── Zoom (scroll wheel → dolly) ────────────────────────────────

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll == 0f) return;

            // Zoom as a percentage of current distance for a natural feel
            _targetDistance -= scroll * _zoomSpeed * (_targetDistance * 0.1f);
            _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
        }

        private void SmoothZoom()
        {
            _distance = Mathf.Lerp(_distance, _targetDistance, Time.deltaTime * _zoomSmoothing);
        }

        // ─── Bounds clamping ─────────────────────────────────────────────

        private void ClampFocusPoint()
        {
            if (!_enableBounds) return;

            float s = _boundsSoftness;

            if (_focusPoint.x < _boundsMinX)
                _focusPoint.x = Mathf.Lerp(_focusPoint.x, _boundsMinX, s * Time.deltaTime);
            else if (_focusPoint.x > _boundsMaxX)
                _focusPoint.x = Mathf.Lerp(_focusPoint.x, _boundsMaxX, s * Time.deltaTime);

            if (_focusPoint.y < _boundsMinY)
                _focusPoint.y = Mathf.Lerp(_focusPoint.y, _boundsMinY, s * Time.deltaTime);
            else if (_focusPoint.y > _boundsMaxY)
                _focusPoint.y = Mathf.Lerp(_focusPoint.y, _boundsMaxY, s * Time.deltaTime);

            if (_focusPoint.z < _boundsMinZ)
                _focusPoint.z = Mathf.Lerp(_focusPoint.z, _boundsMinZ, s * Time.deltaTime);
            else if (_focusPoint.z > _boundsMaxZ)
                _focusPoint.z = Mathf.Lerp(_focusPoint.z, _boundsMaxZ, s * Time.deltaTime);
        }

        // ─── Core: position camera from yaw/pitch/distance ──────────────

        private void UpdateCameraTransform()
        {
            // Convert yaw/pitch to a direction vector
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 offset = rotation * Vector3.back * _distance;

            transform.position = _focusPoint + offset;
            transform.LookAt(_focusPoint, Vector3.up);
        }

        // ─── Utility: world position under mouse ────────────────────────

        /// <summary>
        /// Gets the world position under the mouse cursor by raycasting to a
        /// horizontal plane at the focus point height.
        /// </summary>
        private Vector3 GetMouseWorldPosition()
        {
            Plane focusPlane = new Plane(Vector3.up, _focusPoint);
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

            float enter;
            if (focusPlane.Raycast(ray, out enter))
                return ray.GetPoint(enter);

            return _focusPoint;
        }

        // ─── Public API (used by other systems) ─────────────────────────

        /// <summary>Sets camera bounds from terrain size.</summary>
        public void SetBounds(float minX, float maxX, float minZ, float maxZ)
        {
            _boundsMinX = minX;
            _boundsMaxX = maxX;
            _boundsMinZ = minZ;
            _boundsMaxZ = maxZ;
            _enableBounds = true;
        }

        /// <summary>Sets the Y height the camera focuses on.</summary>
        public void SetFocusHeight(float height)
        {
            _focusPoint.y = height;
        }

        /// <summary>Centers camera on a specific world XZ position.</summary>
        public void CenterOn(float x, float z)
        {
            _focusPoint.x = x;
            _focusPoint.z = z;
            UpdateCameraTransform();
        }

        /// <summary>The current focus point.</summary>
        public Vector3 FocusPoint => _focusPoint;

        // ─── Gizmos ─────────────────────────────────────────────────────

        void OnDrawGizmos()
        {
            if (!_showFocusGizmo) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_focusPoint, 3f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _focusPoint);

            // Draw bounds box
            if (_enableBounds)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
                Vector3 center = new Vector3(
                    (_boundsMinX + _boundsMaxX) / 2f,
                    (_boundsMinY + _boundsMaxY) / 2f,
                    (_boundsMinZ + _boundsMaxZ) / 2f
                );
                Vector3 size = new Vector3(
                    _boundsMaxX - _boundsMinX,
                    _boundsMaxY - _boundsMinY,
                    _boundsMaxZ - _boundsMinZ
                );
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
