using UnityEngine;
using SkiResortTycoon.Core;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Unity input handler for drawing trails on the terrain.
    /// Player clicks and drags to create a trail path.
    /// </summary>
    public class TrailDrawer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridDebugRenderer _gridRenderer;
        [SerializeField] private Camera _camera;
        
        [Header("Drawing Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private int _minPointSpacing = 1; // Min tiles between points
        [SerializeField] private KeyCode _drawKey = KeyCode.T; // Press T to draw
        [SerializeField] private bool _debugMode = true;
        
        private TrailSystem _trailSystem;
        private TrailData _currentTrail;
        private bool _isDrawing = false;
        private TileCoord _lastAddedPoint;
        
        void Start()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
            
            // Don't initialize yet - wait for terrain to be ready
            Debug.Log("TrailDrawer: Waiting for terrain to be ready...");
        }
        
        private void EnsureInitialized()
        {
            // Lazy initialization - create trail system when first needed
            if (_trailSystem == null && _gridRenderer != null && _gridRenderer.TerrainData != null)
            {
                _trailSystem = new TrailSystem(_gridRenderer.TerrainData);
                Debug.Log("TrailDrawer initialized successfully!");
            }
        }
        
        void Update()
        {
            EnsureInitialized();
            
            if (_trailSystem == null)
            {
                // Still not ready
                return;
            }
            
            HandleDrawingInput();
        }
        
        void OnDrawGizmos()
        {
            if (!_debugMode) return;
            
            // Draw cursor at mouse position (visible in Scene view)
            TileCoord? coord = GetTileUnderMouse();
            if (coord.HasValue && _gridRenderer != null && _gridRenderer.TerrainData != null)
            {
                Vector3 worldPos = TileToWorldPos(coord.Value);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(worldPos, Vector3.one * _tileSize);
            }
        }
        
        private void HandleDrawingInput()
        {
            // Debug logging
            if (_debugMode && Input.GetKeyDown(_drawKey))
            {
                Debug.Log($"Draw key pressed! Key: {_drawKey}");
            }
            
            // Start drawing
            if (Input.GetKeyDown(_drawKey))
            {
                TileCoord? coord = GetTileUnderMouse();
                if (coord.HasValue)
                {
                    StartDrawing(coord.Value);
                }
                else if (_debugMode)
                {
                    Debug.LogWarning("Cannot start drawing - no valid tile under mouse");
                }
            }
            
            // Continue drawing (hold key and move mouse)
            if (Input.GetKey(_drawKey) && _isDrawing)
            {
                TileCoord? coord = GetTileUnderMouse();
                if (coord.HasValue)
                {
                    ContinueDrawing(coord.Value);
                }
            }
            
            // Finish drawing
            if (Input.GetKeyUp(_drawKey) && _isDrawing)
            {
                FinishDrawing();
            }
            
            // Emergency test - if T is pressed, log it
            if (_debugMode && Input.GetKeyDown(KeyCode.T))
            {
                Debug.Log("T key detected directly!");
            }
        }
        
        private void StartDrawing(TileCoord startCoord)
        {
            _isDrawing = true;
            _currentTrail = _trailSystem.CreateTrail();
            _currentTrail.AddPoint(startCoord);
            _lastAddedPoint = startCoord;
            
            Debug.Log($"Started drawing trail at {startCoord}");
        }
        
        private void ContinueDrawing(TileCoord coord)
        {
            if (_currentTrail == null) return;
            
            // Check if far enough from last point
            int dx = coord.X - _lastAddedPoint.X;
            int dy = coord.Y - _lastAddedPoint.Y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            
            if (distance >= _minPointSpacing)
            {
                _currentTrail.AddPoint(coord);
                _lastAddedPoint = coord;
            }
        }
        
        private void FinishDrawing()
        {
            if (_currentTrail == null) return;
            
            _isDrawing = false;
            
            // Validate trail
            bool isValid = _trailSystem.ValidateTrail(_currentTrail);
            
            if (isValid)
            {
                // Calculate difficulty
                _trailSystem.CalculateDifficulty(_currentTrail);
                
                Debug.Log($"Trail created! Length: {_currentTrail.Length}, " +
                         $"Difficulty: {_currentTrail.Difficulty}, " +
                         $"Elevation Drop: {_currentTrail.TotalElevationDrop}, " +
                         $"Avg Slope: {_currentTrail.AverageSlope:F2}");
            }
            else
            {
                Debug.LogWarning("Trail invalid (too short or doesn't go downhill enough). Removing.");
                _trailSystem.RemoveTrail(_currentTrail);
            }
            
            _currentTrail = null;
        }
        
        private TileCoord? GetTileUnderMouse()
        {
            if (_camera == null) return null;
            
            Vector3 mousePos = Input.mousePosition;
            Vector3 worldPos = _camera.ScreenToWorldPoint(mousePos);
            
            // Convert world position to tile coordinates
            int tileX = Mathf.RoundToInt(worldPos.x / _tileSize);
            int tileY = Mathf.RoundToInt(worldPos.y / _tileSize);
            
            TileCoord coord = new TileCoord(tileX, tileY);
            
            // Check if in bounds
            if (_gridRenderer != null && 
                _gridRenderer.TerrainData != null && 
                _gridRenderer.TerrainData.Grid.InBounds(coord))
            {
                return coord;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets the trail system for external access.
        /// </summary>
        public TrailSystem TrailSystem => _trailSystem;
        
        /// <summary>
        /// Gets the currently drawing trail (or null).
        /// </summary>
        public TrailData CurrentTrail => _currentTrail;
        
        /// <summary>
        /// Returns true if currently drawing a trail.
        /// </summary>
        public bool IsDrawing => _isDrawing;
        
        private Vector3 TileToWorldPos(TileCoord coord)
        {
            float worldX = coord.X * _tileSize;
            float worldY = coord.Y * _tileSize;
            
            // Add height offset if terrain available
            if (_gridRenderer != null && _gridRenderer.TerrainData != null)
            {
                int height = _gridRenderer.TerrainData.GetHeight(coord);
                worldY += height * 0.1f; // Match heightScale from GridDebugRenderer
            }
            
            return new Vector3(worldX, worldY, 0f);
        }
        
        void OnGUI()
        {
            if (!_debugMode) return;
            
            GUI.Box(new Rect(10, 100, 300, 120), "Trail Drawing");
            
            // Show initialization status
            if (_trailSystem == null)
            {
                GUI.Label(new Rect(20, 120, 280, 20), "Status: Initializing...");
                GUI.Label(new Rect(20, 140, 280, 20), $"GridRenderer: {(_gridRenderer != null ? "OK" : "NULL")}");
                GUI.Label(new Rect(20, 160, 280, 20), $"TerrainData: {(_gridRenderer != null && _gridRenderer.TerrainData != null ? "OK" : "NULL")}");
                return;
            }
            
            GUI.Label(new Rect(20, 120, 280, 20), $"Press and HOLD '{_drawKey}' to draw");
            GUI.Label(new Rect(20, 140, 280, 20), $"Drawing: {_isDrawing}");
            
            TileCoord? coord = GetTileUnderMouse();
            if (coord.HasValue)
            {
                GUI.Label(new Rect(20, 160, 280, 20), $"Tile: {coord.Value}");
                
                // Draw a visual cursor in Game view
                if (_camera != null)
                {
                    Vector3 screenPos = _camera.WorldToScreenPoint(TileToWorldPos(coord.Value));
                    screenPos.y = Screen.height - screenPos.y; // Flip Y for GUI coordinates
                    
                    // Draw crosshair
                    float size = 10f;
                    GUI.color = Color.yellow;
                    GUI.DrawTexture(new Rect(screenPos.x - size/2, screenPos.y - 1, size, 2), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(screenPos.x - 1, screenPos.y - size/2, 2, size), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }
            else
            {
                GUI.Label(new Rect(20, 160, 280, 20), "Tile: None (hover over terrain)");
            }
            
            GUI.Label(new Rect(20, 180, 280, 20), $"Trails: {_trailSystem.Trails.Count}");
            GUI.Label(new Rect(20, 200, 280, 20), $"Status: Ready!");
        }
    }
}

