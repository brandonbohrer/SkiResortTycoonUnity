using UnityEngine;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// 3D perspective camera controller with orbit, pan, and zoom.
    /// Camera orbits a focus point on the terrain within bounded limits.
    /// After orbiting, the focus point re-anchors to the mountain surface
    /// at screen center so terrain collision and height tracking stay accurate.
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
        [SerializeField] private float _keyboardRotateSpeed = 90f;
        [SerializeField] private float _minPitch = 10f;
        [SerializeField] private float _maxPitch = 80f;
        [SerializeField] private float _defaultYaw = -45f;
        [SerializeField] private float _defaultPitch = 40f;

        [Header("Zoom Settings")]
        [SerializeField] private float _zoomSpeed = 15f;
        [SerializeField] private float _minDistanceAboveTerrain = 5f;
        [SerializeField] private float _minDistanceFallback = 10f;
        [SerializeField] private float _maxDistance = 500f;
        [SerializeField] private float _defaultDistance = 150f;
        [SerializeField] private float _zoomSmoothing = 10f;

        [Header("Terrain Following")]
        [SerializeField] private float _focusHeightSmoothing = 8f;
        [SerializeField] private float _focusHeightOffset = 2f;

        [Header("Pan Settings")]
        [SerializeField] private float _panSpeedKeyboard = 40f;
        [SerializeField] private float _panSpeedMouse = 1f;
        [SerializeField] private int _panMouseButton = 2;
        [SerializeField] private int _orbitMouseButton = 1;

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

        // Cached mountain reference for terrain queries
        private GameObject _mountainMeshObj;
        private Collider[] _mountainColliders;

        // The actual distance used last frame (after terrain clamping).
        private float _effectiveDistance;

        // Drag state
        private Vector3 _lastMousePosition;
        private bool _isOrbiting;
        private bool _isPanning;

        // UI-over-pointer detection — fresh raycast every frame to avoid stale
        // EventSystem cache that can block scroll/zoom after clicking UI elements.
        private UnityEngine.EventSystems.PointerEventData _pointerEventData;
        private readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> _uiRaycastHits =
            new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        private bool _pointerOverUI;

        void Awake()
        {
            _camera = GetComponent<Camera>();

            _camera.orthographic = false;
            _camera.fieldOfView = _fieldOfView;
            _camera.nearClipPlane = _nearClip;
            _camera.farClipPlane = _farClip;

            _yaw = _defaultYaw;
            _pitch = _defaultPitch;
            _distance = _defaultDistance;
            _targetDistance = _defaultDistance;
            _effectiveDistance = _defaultDistance;
        }

        void Start()
        {
            if (_autoDetectBounds)
            {
                InitializeBoundsFromMountain();
            }

            _focusPoint = new Vector3(
                (_boundsMinX + _boundsMaxX) / 2f,
                (_boundsMinY + _boundsMaxY) / 2f,
                (_boundsMinZ + _boundsMaxZ) / 2f
            );

            UpdateCameraTransform();
        }

        private void InitializeBoundsFromMountain()
        {
            if (_mountainManager == null)
            {
                Debug.LogWarning("[CameraController] MountainManager not assigned, using default bounds");
                return;
            }

            var mountainMeshField = typeof(MountainManager).GetField("_mountainMesh",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (mountainMeshField == null) return;

            GameObject mountainMesh = mountainMeshField.GetValue(_mountainManager) as GameObject;
            if (mountainMesh == null) return;

            _mountainMeshObj = mountainMesh;
            _mountainColliders = mountainMesh.GetComponentsInChildren<Collider>();

            Renderer renderer = mountainMesh.GetComponent<Renderer>();
            if (renderer == null) renderer = mountainMesh.GetComponentInChildren<Renderer>();
            if (renderer == null) return;

            Bounds bounds = renderer.bounds;

            float padding = 50f;
            _boundsMinX = bounds.min.x - padding;
            _boundsMaxX = bounds.max.x + padding;
            _boundsMinY = bounds.min.y;
            _boundsMaxY = bounds.max.y + padding;
            _boundsMinZ = bounds.min.z - padding;
            _boundsMaxZ = bounds.max.z + padding;

            _focusPoint = bounds.center;

            Debug.Log($"[CameraController] Detected mountain bounds: {bounds.min} to {bounds.max}");
            Debug.Log($"[CameraController] Camera bounds: X[{_boundsMinX:F0},{_boundsMaxX:F0}] Y[{_boundsMinY:F0},{_boundsMaxY:F0}] Z[{_boundsMinZ:F0},{_boundsMaxZ:F0}]");
        }

        void Update()
        {
            // Freeze all camera input while any overlay (menu or manager) is open
            if (SkiResortTycoon.UI.UIManager.Instance != null &&
                SkiResortTycoon.UI.UIManager.Instance.IsAnyOverlayOpen)
                return;

            // Fresh raycast every frame — avoids stale EventSystem cache that makes
            // IsPointerOverGameObject() return true after clicking UI even when the
            // cursor has moved back to the map.
            RefreshPointerOverUI();

            // When the cursor is over the world (not UI), deselect any focused UI
            // element so it doesn't continue consuming scroll or keyboard input.
            if (!_pointerOverUI)
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null)
                {
                    bool isInputField = es.currentSelectedGameObject
                        .GetComponent<TMPro.TMP_InputField>() != null;
                    if (!isInputField)
                        es.SetSelectedGameObject(null);
                }
            }

            if (_isFollowing)
            {
                UpdateFollowMode();
                return;
            }

            if (_isFindAnimating)
            {
                UpdateFindAnimation();
                return;
            }

            HandleOrbit();
            HandleKeyboardRotation();
            HandlePanKeyboard();
            HandlePanMouse();
            HandleZoom();
            ClampFocusPoint();
            UpdateFocusHeight();
            SmoothZoom();
            UpdateCameraTransform();
        }

        private void RefreshPointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) { _pointerOverUI = false; return; }

            if (_pointerEventData == null)
                _pointerEventData = new UnityEngine.EventSystems.PointerEventData(es);

            _pointerEventData.position = Input.mousePosition;
            _uiRaycastHits.Clear();
            es.RaycastAll(_pointerEventData, _uiRaycastHits);
            _pointerOverUI = _uiRaycastHits.Count > 0;
        }

        // ─── Orbit (right-click drag) ───────────────────────────────────

        private bool IsPointerOverUI => _pointerOverUI;

        private void HandleOrbit()
        {
            if (Input.GetMouseButtonDown(_orbitMouseButton) && !IsPointerOverUI)
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
            if (IsTypingInInputField) return;

            if (Input.GetKey(KeyCode.Q))
                _yaw -= _keyboardRotateSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E))
                _yaw += _keyboardRotateSpeed * Time.deltaTime;
        }

        // ─── Pan with WASD / Arrow keys ────────────────────────────────

        private static bool IsTypingInInputField =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                .GetComponent<TMPro.TMP_InputField>() != null;

        private void HandlePanKeyboard()
        {
            if (IsTypingInInputField) return;

            Vector3 input = Vector3.zero;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    input.z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  input.z -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  input.x -= 1f;

            if (input == Vector3.zero) return;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            float scaledSpeed = _panSpeedKeyboard * (_effectiveDistance / _defaultDistance);
            Vector3 panDelta = (forward * input.z + right * input.x) * scaledSpeed * Time.deltaTime;
            _focusPoint += panDelta;
        }

        // ─── Pan with middle-click drag ─────────────────────────────────

        private void HandlePanMouse()
        {
            if (Input.GetMouseButtonDown(_panMouseButton) && !IsPointerOverUI)
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
            // Don't zoom when the pointer is over any UI element (e.g. the context window scroll view)
            if (_pointerOverUI)
                return;

            float scroll = Input.mouseScrollDelta.y;
            if (scroll == 0f) return;

            _targetDistance -= scroll * _zoomSpeed * (_targetDistance * 0.1f);
            _targetDistance = Mathf.Clamp(_targetDistance, _minDistanceFallback, _maxDistance);
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

        // ─── Terrain-following focus point ───────────────────────────────

        private void UpdateFocusHeight()
        {
            float? terrainY = GetTerrainHeightAt(_focusPoint);
            if (terrainY.HasValue)
            {
                float targetY = terrainY.Value + _focusHeightOffset;
                _focusPoint.y = Mathf.Lerp(_focusPoint.y, targetY, _focusHeightSmoothing * Time.deltaTime);
            }
        }

        private void SnapFocusHeight()
        {
            float? terrainY = GetTerrainHeightAt(_focusPoint);
            if (terrainY.HasValue)
                _focusPoint.y = terrainY.Value + _focusHeightOffset;
        }

        // ─── Core: position camera from yaw/pitch/distance ──────────────

        private void UpdateCameraTransform()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 direction = rotation * Vector3.back;

            float usedDistance = _distance;

            // Terrain collision — clamp distance so camera doesn't go through mountain.
            // Does NOT modify _targetDistance — that's the user's zoom intent.
            float terrainMinDist = GetTerrainClampedDistance(direction, _distance);
            if (terrainMinDist < usedDistance)
            {
                usedDistance = terrainMinDist;
            }

            usedDistance = Mathf.Max(usedDistance, _minDistanceFallback);
            _effectiveDistance = usedDistance;

            Vector3 offset = direction * usedDistance;
            transform.position = _focusPoint + offset;
            transform.LookAt(_focusPoint, Vector3.up);
        }

        // ─── Terrain collision ──────────────────────────────────────────

        private float GetTerrainClampedDistance(Vector3 cameraDirection, float desiredDistance)
        {
            if (_mountainColliders == null || _mountainColliders.Length == 0)
                return desiredDistance;

            float clampedDistance = desiredDistance;

            // 1) Raycast from focus toward camera — catches mountain in line of sight
            Ray ray = new Ray(_focusPoint, cameraDirection);
            RaycastHit[] hits = Physics.RaycastAll(ray, desiredDistance);
            foreach (var hit in hits)
            {
                if (IsMountainCollider(hit.collider))
                {
                    float safeDistance = hit.distance - _minDistanceAboveTerrain;
                    if (safeDistance < clampedDistance)
                        clampedDistance = safeDistance;
                }
            }

            // 2) Check if camera would be below terrain surface
            Vector3 candidatePos = _focusPoint + cameraDirection * clampedDistance;
            float? terrainBelow = GetTerrainHeightAt(candidatePos);
            if (terrainBelow.HasValue)
            {
                float minCamY = terrainBelow.Value + _minDistanceAboveTerrain;
                if (candidatePos.y < minCamY)
                {
                    float deficit = minCamY - candidatePos.y;
                    float yPerUnit = Mathf.Abs(cameraDirection.y);
                    if (yPerUnit > 0.01f)
                    {
                        clampedDistance -= deficit / yPerUnit;
                    }
                }
            }

            return clampedDistance;
        }

        private float? GetTerrainHeightAt(Vector3 position)
        {
            if (_mountainColliders == null || _mountainColliders.Length == 0)
                return null;

            Ray downRay = new Ray(new Vector3(position.x, _boundsMaxY + 100f, position.z), Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(downRay, _boundsMaxY + 200f);

            float? bestY = null;
            foreach (var hit in hits)
            {
                if (IsMountainCollider(hit.collider))
                {
                    if (!bestY.HasValue || hit.point.y > bestY.Value)
                        bestY = hit.point.y;
                }
            }
            return bestY;
        }

        private bool IsMountainCollider(Collider col)
        {
            if (_mountainMeshObj == null) return false;
            return col.transform == _mountainMeshObj.transform ||
                   col.transform.IsChildOf(_mountainMeshObj.transform);
        }

        // ─── Find animation ─────────────────────────────────────────────

        private void UpdateFindAnimation()
        {
            _findT += Time.deltaTime / FIND_ANIM_DURATION;

            // Any user input cancels the animation and snaps to destination
            bool userInterrupted =
                Input.GetMouseButtonDown(_orbitMouseButton) ||
                Input.GetMouseButtonDown(_panMouseButton) ||
                Input.mouseScrollDelta.y != 0f ||
                (!IsTypingInInputField &&
                 (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                  Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D)));

            if (_findT >= 1f || userInterrupted)
            {
                _focusPoint.x = _findEndFocus.x;
                _focusPoint.z = _findEndFocus.z;
                _distance = _findEndDist;
                _targetDistance = _findEndDist;
                _isFindAnimating = false;
                SnapFocusHeight();
                UpdateCameraTransform();
                return;
            }

            // Smooth ease-in-out curve
            float t = _findT < 0.5f
                ? 2f * _findT * _findT
                : 1f - Mathf.Pow(-2f * _findT + 2f, 2f) / 2f;

            _focusPoint.x = Mathf.Lerp(_findStartFocus.x, _findEndFocus.x, t);
            _focusPoint.z = Mathf.Lerp(_findStartFocus.z, _findEndFocus.z, t);
            _distance = Mathf.Lerp(_findStartDist, _findEndDist, t);
            _targetDistance = _distance;
            SnapFocusHeight();
            UpdateCameraTransform();
        }

        // ─── Follow mode ────────────────────────────────────────────────

        private void UpdateFollowMode()
        {
            if (_followTarget == null)
            {
                StopFollowing();
                return;
            }

            // Auto-exit when skier enters a lodge
            if (_followSkier != null &&
                _followSkier.CurrentState == SkiResortTycoon.Core.SkierState.AtAmenity)
            {
                StopFollowing();
                return;
            }

            // Exit on Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopFollowing();
                return;
            }

            // Exit on WASD / arrow pan keys
            if (!IsTypingInInputField &&
                (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                 Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                 Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                 Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
            {
                StopFollowing();
                return;
            }

            // Track whether the player is actively rotating the camera
            bool playerRotated = false;

            if (Input.GetMouseButton(_orbitMouseButton))
            {
                if (Input.GetMouseButtonDown(_orbitMouseButton))
                    _lastMousePosition = Input.mousePosition;

                Vector3 delta = Input.mousePosition - _lastMousePosition;
                if (delta.sqrMagnitude > 0.1f)
                {
                    _followYaw += delta.x * _orbitSensitivity;
                    _followPitch -= delta.y * _orbitSensitivity;
                    _followPitch = Mathf.Clamp(_followPitch, -80f, 80f);
                    playerRotated = true;
                }
                _lastMousePosition = Input.mousePosition;
            }

            if (!IsTypingInInputField)
            {
                if (Input.GetKey(KeyCode.Q))
                {
                    _followYaw -= _keyboardRotateSpeed * Time.deltaTime;
                    playerRotated = true;
                }
                if (Input.GetKey(KeyCode.E))
                {
                    _followYaw += _keyboardRotateSpeed * Time.deltaTime;
                    playerRotated = true;
                }
            }

            // Compute the skier's facing yaw from their transform
            Vector3 skierForward = _followTarget.forward;
            float skierYaw = Mathf.Atan2(skierForward.x, skierForward.z) * Mathf.Rad2Deg;

            if (playerRotated)
            {
                _followIdleTimer = 0f;
            }
            else
            {
                _followIdleTimer += Time.deltaTime;

                // After idle timeout, smoothly return to the skier's facing direction
                if (_followIdleTimer >= FOLLOW_IDLE_TIMEOUT)
                {
                    _followYaw = Mathf.LerpAngle(_followYaw, skierYaw,
                        FOLLOW_RETURN_SPEED * Time.deltaTime);
                    _followPitch = Mathf.Lerp(_followPitch, 10f,
                        FOLLOW_RETURN_SPEED * Time.deltaTime);
                }
            }

            Vector3 headPos = _followTarget.position + Vector3.up * _followHeightOffset;
            transform.position = headPos;
            transform.rotation = Quaternion.Euler(_followPitch, _followYaw, 0f);
        }

        // ─── Utility ────────────────────────────────────────────────────

        private Vector3 GetMouseWorldPosition()
        {
            Plane focusPlane = new Plane(Vector3.up, _focusPoint);
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

            float enter;
            if (focusPlane.Raycast(ray, out enter))
                return ray.GetPoint(enter);

            return _focusPoint;
        }

        // ─── Follow-mode state ─────────────────────────────────────────
        private Transform _followTarget;
        private SkiResortTycoon.Core.Skier _followSkier;
        private bool _isFollowing;
        private float _followYaw;
        private float _followPitch;
        private float _followHeightOffset = 1.8f;
        private float _followIdleTimer;
        private const float FOLLOW_IDLE_TIMEOUT = 5f;
        private const float FOLLOW_RETURN_SPEED = 1.5f;

        // ─── Find-target animation state ────────────────────────────────
        private bool _isFindAnimating;
        private Vector3 _findStartFocus;
        private Vector3 _findEndFocus;
        private float _findStartDist;
        private float _findEndDist;
        private float _findT;
        private const float FIND_ANIM_DURATION = 0.6f;

        public bool IsFollowing => _isFollowing;

        /// <summary>
        /// Event raised when follow mode ends (for UI to react).
        /// </summary>
        public event System.Action OnFollowEnded;

        // ─── Public API ─────────────────────────────────────────────────

        public void SetBounds(float minX, float maxX, float minZ, float maxZ)
        {
            _boundsMinX = minX;
            _boundsMaxX = maxX;
            _boundsMinZ = minZ;
            _boundsMaxZ = maxZ;
            _enableBounds = true;
        }

        public void SetFocusHeight(float height)
        {
            _focusPoint.y = height;
        }

        public void CenterOn(float x, float z)
        {
            _focusPoint.x = x;
            _focusPoint.z = z;
            UpdateCameraTransform();
        }

        /// <summary>
        /// Smoothly pans and zooms the camera to look at the given world position.
        /// </summary>
        public void FindTarget(Vector3 worldPosition, float zoomDistance = 40f)
        {
            StopFollowing();
            _isFindAnimating = true;
            _findStartFocus = _focusPoint;
            _findEndFocus = worldPosition;
            _findStartDist = _distance;
            _findEndDist = zoomDistance;
            _findT = 0f;
        }

        /// <summary>
        /// Enters first-person follow mode: camera is locked to the target transform's
        /// position (at eye height) and the player can look around with right-click / Q/E.
        /// Automatically exits when the skier enters a lodge (AtAmenity state).
        /// </summary>
        public void StartFollowing(Transform target, SkiResortTycoon.Core.Skier skierData = null)
        {
            if (target == null) return;
            _followTarget = target;
            _followSkier = skierData;
            _isFollowing = true;
            _isFindAnimating = false;

            Vector3 fwd = target.forward;
            _followYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            _followPitch = 10f;
            _followIdleTimer = FOLLOW_IDLE_TIMEOUT;
        }

        /// <summary>
        /// Exits follow mode only if the camera is currently following the given target.
        /// </summary>
        public void StopFollowingIfTarget(Transform target)
        {
            if (_isFollowing && _followTarget == target)
                StopFollowing();
        }

        /// <summary>
        /// Exits follow mode and returns to normal orbit camera.
        /// </summary>
        public void StopFollowing()
        {
            if (!_isFollowing) return;

            _isFollowing = false;
            _isFindAnimating = false;

            if (_followTarget != null)
            {
                _focusPoint = _followTarget.position;
                _targetDistance = 40f;
                _distance = 40f;
            }

            _yaw = _followYaw;
            _pitch = _defaultPitch;
            _followTarget = null;
            _followSkier = null;

            UpdateCameraTransform();
            OnFollowEnded?.Invoke();
        }

        public Vector3 FocusPoint => _focusPoint;

        // ─── Gizmos ─────────────────────────────────────────────────────

        void OnDrawGizmos()
        {
            if (!_showFocusGizmo) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_focusPoint, 3f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _focusPoint);

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
