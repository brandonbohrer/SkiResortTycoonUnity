using UnityEngine;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Unity camera controller for 2.5D orthographic view.
    /// Supports pan (WASD + mouse drag) and zoom (mouse wheel).
    /// NO rotation allowed - fixed angle only.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Pan Settings")]
        [SerializeField] private float _panSpeedKeyboard = 10f;
        [SerializeField] private float _panSpeedMouse = 0.5f;
        [SerializeField] private bool _enableMouseDrag = true;
        [SerializeField] private int _dragMouseButton = 2; // 0=Left, 1=Right, 2=Middle
        
        [Header("Zoom Settings")]
        [SerializeField] private float _zoomSpeed = 2f;
        [SerializeField] private float _minZoom = 2f;
        [SerializeField] private float _maxZoom = 20f;
        [SerializeField] private float _defaultZoom = 10f;
        
        [Header("Bounds (Optional)")]
        [SerializeField] private bool _enableBounds = true;
        [SerializeField] private float _minX = -50f;
        [SerializeField] private float _maxX = 50f;
        [SerializeField] private float _minY = -50f;
        [SerializeField] private float _maxY = 50f;
        
        private Camera _camera;
        private Vector3 _lastMousePosition;
        private bool _isDragging = false;
        
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
        }
        
        void Update()
        {
            // TODO: Re-enable input handling with new Input System
            // HandlePanKeyboard();
            // HandlePanMouse();
            // HandleZoom();
            ClampPosition();
        }
        
        private void HandlePanKeyboard()
        {
            Vector3 movement = Vector3.zero;
            
            // WASD movement
            if (Input.GetKey(KeyCode.W)) movement.y += 1f;
            if (Input.GetKey(KeyCode.S)) movement.y -= 1f;
            if (Input.GetKey(KeyCode.D)) movement.x += 1f;
            if (Input.GetKey(KeyCode.A)) movement.x -= 1f;
            
            // Arrow keys
            if (Input.GetKey(KeyCode.UpArrow)) movement.y += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) movement.y -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) movement.x += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) movement.x -= 1f;
            
            if (movement != Vector3.zero)
            {
                transform.position += movement.normalized * _panSpeedKeyboard * Time.deltaTime;
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
                Vector3 delta = Input.mousePosition - _lastMousePosition;
                
                // Convert screen space to world space
                Vector3 worldDelta = _camera.ScreenToWorldPoint(Input.mousePosition) 
                                   - _camera.ScreenToWorldPoint(_lastMousePosition);
                
                // Only use X and Y, ignore Z
                worldDelta.z = 0f;
                
                transform.position -= worldDelta * _panSpeedMouse;
                
                _lastMousePosition = Input.mousePosition;
            }
        }
        
        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            
            if (scroll != 0f)
            {
                _camera.orthographicSize -= scroll * _zoomSpeed;
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize, _minZoom, _maxZoom);
            }
        }
        
        private void ClampPosition()
        {
            if (!_enableBounds) return;
            
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, _minX, _maxX);
            pos.y = Mathf.Clamp(pos.y, _minY, _maxY);
            transform.position = pos;
        }
        
        /// <summary>
        /// Sets camera bounds based on grid size.
        /// </summary>
        public void SetBounds(float minX, float maxX, float minY, float maxY)
        {
            _minX = minX;
            _maxX = maxX;
            _minY = minY;
            _maxY = maxY;
            _enableBounds = true;
        }
        
        /// <summary>
        /// Centers camera on a specific world position.
        /// </summary>
        public void CenterOn(float x, float y)
        {
            Vector3 pos = transform.position;
            pos.x = x;
            pos.y = y;
            transform.position = pos;
        }
    }
}

