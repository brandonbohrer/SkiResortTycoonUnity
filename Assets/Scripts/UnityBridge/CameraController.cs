using UnityEngine;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Focus-point-based orthographic camera controller for isometric view.
    /// Maintains a focus point on the terrain and positions camera relative to it.
    /// Prevents terrain from disappearing when tilted by keeping focus centered.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Camera Angle (Fixed)")]
        [SerializeField] private Vector3 _cameraRotation = new Vector3(30f, -45f, 0f);
        [SerializeField] private float _cameraDistance = 100f; // Distance from focus point
        
        [Header("Mountain Reference")]
        [SerializeField] private MountainManager _mountainManager;
        [SerializeField] private bool _autoDetectBounds = true;
        
        [Header("Pan Settings")]
        [SerializeField] private float _panSpeedKeyboard = 20f;
        [SerializeField] private float _panSpeedMouse = 1f;
        [SerializeField] private bool _enableMouseDrag = true;
        [SerializeField] private int _dragMouseButton = 2; // 0=Left, 1=Right, 2=Middle
        
        [Header("Zoom Settings")]
        [SerializeField] private float _zoomSpeed = 10f;
        [SerializeField] private float _minZoom = 20f;
        [SerializeField] private float _maxZoom = 200f;
        [SerializeField] private float _defaultZoom = 100f;
        
        [Header("Focus Point Bounds")]
        [SerializeField] private bool _enableBounds = true; // Re-enabled with soft clamping
        [SerializeField] private float _minX = 0f;
        [SerializeField] private float _maxX = 64f;
        [SerializeField] private float _minZ = 0f;
        [SerializeField] private float _maxZ = 64f;
        [SerializeField] private float _focusHeight = 10f; // Y height of focus point on terrain
        
        [Header("Debug")]
        [SerializeField] private bool _showFocusGizmo = true;
        
        private Camera _camera;
        private Vector3 _focusPoint; // The point on the terrain we're looking at
        private Vector3 _lastMousePosition;
        private bool _isDragging = false;
        private float _targetFocusY; // Target Y for smooth interpolation
        
        void Awake()
        {
            _camera = GetComponent<Camera>();
            
            // Ensure orthographic
            if (!_camera.orthographic)
            {
                _camera.orthographic = true;
            }
            
            // Set initial zoom
            _camera.orthographicSize = _defaultZoom;
            
            // Set camera rotation (fixed)
            transform.rotation = Quaternion.Euler(_cameraRotation);
            
            // Set far clipping plane large enough to always see the mountain
            _camera.farClipPlane = 2000f;
        }
        
        void Start()
        {
            // Auto-detect bounds from mountain
            if (_autoDetectBounds)
            {
                InitializeBoundsFromMountain();
            }
            
            // Initialize focus point at terrain center
            _focusPoint = new Vector3(
                (_minX + _maxX) / 2f,
                _focusHeight,
                (_minZ + _maxZ) / 2f
            );
            _targetFocusY = _focusHeight; // Initialize target Y
            
            UpdateCameraPosition();
        }
        
        /// <summary>
        /// Automatically detects bounds from the mountain renderer.
        /// </summary>
        private void InitializeBoundsFromMountain()
        {
            if (_mountainManager == null)
            {
                Debug.LogWarning("[CameraController] MountainManager not assigned, using default bounds");
                return;
            }
            
            // Get the mountain GameObject reference via reflection
            var mountainMeshField = typeof(MountainManager).GetField("_mountainMesh", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (mountainMeshField != null)
            {
                GameObject mountainMesh = mountainMeshField.GetValue(_mountainManager) as GameObject;
                
                if (mountainMesh != null)
                {
                    // Get renderer bounds
                    Renderer renderer = mountainMesh.GetComponent<Renderer>();
                    if (renderer == null)
                    {
                        renderer = mountainMesh.GetComponentInChildren<Renderer>();
                    }
                    
                    if (renderer != null)
                    {
                        Bounds bounds = renderer.bounds;
                        
                        // Set camera bounds from mountain bounds
                        _minX = 0;
                        _maxX = 500;
                        _minZ = -100;
                        _maxZ = 500;
                        
                        // CRITICAL: Use MAX Y (peak height), not center Y
                        // Since camera tilts DOWN at 30°, setting focus at peak means:
                        // - Peak is at camera's "eye level" (always visible)
                        // - Base is below eye level (camera looks DOWN to see it)
                        // This ensures the entire mountain is always in view
                        _focusHeight = bounds.max.y;
                        
                        Debug.Log($"[CameraController] Auto-detected mountain bounds: X[{_minX:F1}, {_maxX:F1}] Z[{_minZ:F1}, {_maxZ:F1}]");
                        Debug.Log($"[CameraController] Mountain Y range: {bounds.min.y:F1} to {bounds.max.y:F1}, using peak height {_focusHeight:F1}");
                        Debug.Log($"[CameraController] Mountain size: {bounds.size}");
                    }
                    else
                    {
                        Debug.LogWarning("[CameraController] No renderer found on mountain mesh");
                    }
                }
                else
                {
                    Debug.LogWarning("[CameraController] Mountain mesh is null");
                }
            }
        }
        
        void Update()
        {
            HandlePanKeyboard();
            HandlePanMouse();
            HandleZoom();
            ClampFocusPoint();
            UpdateCameraPosition();
        }
        
        private void HandlePanKeyboard()
        {
            Vector3 movement = Vector3.zero;
            
            // WASD movement
            if (Input.GetKey(KeyCode.W)) movement.z += 1f;
            if (Input.GetKey(KeyCode.S)) movement.z -= 1f;
            if (Input.GetKey(KeyCode.D)) movement.x += 1f;
            if (Input.GetKey(KeyCode.A)) movement.x -= 1f;
            
            // Arrow keys
            if (Input.GetKey(KeyCode.UpArrow)) movement.z += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) movement.z -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) movement.x += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) movement.x -= 1f;
            
            if (movement != Vector3.zero)
            {
                // Move in camera-relative directions (not world space)
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                
                Vector3 panDelta = (forward * movement.z + right * movement.x) * _panSpeedKeyboard * Time.deltaTime;
                _focusPoint += panDelta;
            }
        }
        
        private void HandlePanMouse()
        {
            if (!_enableMouseDrag) return;
            
            // Start drag
            if (Input.GetMouseButtonDown(_dragMouseButton))
            {
                _isDragging = true;
                _lastMousePosition = Input.mousePosition;
            }
            
            // End drag
            if (Input.GetMouseButtonUp(_dragMouseButton))
            {
                _isDragging = false;
            }
            
            // Perform drag
            if (_isDragging)
            {
                Vector3 currentMousePosition = Input.mousePosition;
                Vector3 screenDelta = currentMousePosition - _lastMousePosition;
                
                // Convert screen delta to world delta at focus point plane
                // Project rays at both mouse positions onto the focus plane
                Plane focusPlane = new Plane(Vector3.up, _focusPoint);
                
                Ray rayLast = _camera.ScreenPointToRay(_lastMousePosition);
                Ray rayCurrent = _camera.ScreenPointToRay(currentMousePosition);
                
                float enterLast, enterCurrent;
                Vector3 worldDelta = Vector3.zero;
                
                if (focusPlane.Raycast(rayLast, out enterLast) && 
                    focusPlane.Raycast(rayCurrent, out enterCurrent))
                {
                    Vector3 worldLast = rayLast.GetPoint(enterLast);
                    Vector3 worldCurrent = rayCurrent.GetPoint(enterCurrent);
                    worldDelta = worldLast - worldCurrent;
                }
                
                _focusPoint += worldDelta * _panSpeedMouse;
                _lastMousePosition = currentMousePosition;
            }
        }
        
        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            
            if (scroll != 0f)
            {
                // Zoom toward mouse position by adjusting focus point slightly
                Vector3 mouseWorldPosBefore = GetMouseWorldPosition();
                
                _camera.orthographicSize -= scroll * _zoomSpeed;
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize, _minZoom, _maxZoom);
                
                // Optional: zoom toward mouse cursor
                // Uncomment to enable zoom-to-cursor behavior
                /*
                Vector3 mouseWorldPosAfter = GetMouseWorldPosition();
                Vector3 worldDelta = mouseWorldPosBefore - mouseWorldPosAfter;
                _focusPoint += worldDelta * 0.5f; // 50% zoom-to-cursor strength
                */
            }
        }
        
        private void ClampFocusPoint()
        {
            if (!_enableBounds) return;

            // Use soft boundaries - apply gentle force pushing back toward valid area
            // This prevents jerky movement while keeping camera over the mountain
            float softness = 3f; // How fast it pushes back (higher = faster)
            
            // Check X bounds
            if (_focusPoint.x < _minX)
            {
                _focusPoint.x = Mathf.Lerp(_focusPoint.x, _minX, softness * Time.deltaTime);
            }
            else if (_focusPoint.x > _maxX)
            {
                _focusPoint.x = Mathf.Lerp(_focusPoint.x, _maxX, softness * Time.deltaTime);
            }
            
            // Check Z bounds
            if (_focusPoint.z < _minZ)
            {
                _focusPoint.z = Mathf.Lerp(_focusPoint.z, _minZ, softness * Time.deltaTime);
            }
            else if (_focusPoint.z > _maxZ)
            {
                _focusPoint.z = Mathf.Lerp(_focusPoint.z, _maxZ, softness * Time.deltaTime);
            }
        }

        
        /// <summary>
        /// Updates camera position based on focus point and fixed offset.
        /// This is the core of the focus-point system.
        /// Focus Y stays FIXED at terrain center height so entire mountain is always visible.
        /// </summary>
        private void UpdateCameraPosition()
        {
            // Keep focus Y fixed at terrain center - do NOT track terrain height
            // This ensures the entire mountain (both peak and base) stays visible
            // The orthographic projection handles the rest
            
            // Calculate camera offset from focus point based on rotation and distance
            Vector3 offset = -transform.forward * _cameraDistance;
            transform.position = _focusPoint + offset;
        }
        
        /// <summary>
        /// Gets the world position under the mouse cursor by raycasting to focus plane.
        /// </summary>
        private Vector3 GetMouseWorldPosition()
        {
            Plane focusPlane = new Plane(Vector3.up, _focusPoint);
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            
            float enter;
            if (focusPlane.Raycast(ray, out enter))
            {
                return ray.GetPoint(enter);
            }
            
            return _focusPoint;
        }
        
        /// <summary>
        /// Sets camera bounds based on terrain size.
        /// </summary>
        public void SetBounds(float minX, float maxX, float minZ, float maxZ)
        {
            _minX = minX;
            _maxX = maxX;
            _minZ = minZ;
            _maxZ = maxZ;
            _enableBounds = true;
        }
        
        /// <summary>
        /// Sets the focus height to match terrain elevation.
        /// </summary>
        public void SetFocusHeight(float height)
        {
            _focusHeight = height;
            _focusPoint.y = height;
        }
        
        /// <summary>
        /// Centers camera on a specific world position.
        /// </summary>
        public void CenterOn(float x, float z)
        {
            _focusPoint = new Vector3(x, _focusHeight, z);
            UpdateCameraPosition();
        }
        
        /// <summary>
        /// Gets the current focus point.
        /// </summary>
        public Vector3 FocusPoint => _focusPoint;
        
        void OnDrawGizmos()
        {
            if (!_showFocusGizmo) return;
            
            // Draw focus point
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_focusPoint, 2f);
            
            // Draw line from camera to focus point
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _focusPoint);
        }
    }
}
