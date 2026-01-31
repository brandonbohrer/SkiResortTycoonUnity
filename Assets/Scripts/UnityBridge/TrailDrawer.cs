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
        [SerializeField] private MountainManager _mountainManager;
        [SerializeField] private LiftBuilder _liftBuilder;  // Reference to get connectivity
        [SerializeField] private Camera _camera;
        
        [Header("Drawing Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private int _minPointSpacing = 1; // Min tiles between points
        [SerializeField] private KeyCode _drawKey = KeyCode.T; // Press T to draw
        [SerializeField] private bool _debugMode = true;
        [SerializeField] private float _snapRadius = 5f; // Magnetic cursor snap radius
        
        [Header("Visual Feedback")]
        [SerializeField] private Color _snapColor = Color.green;
        [SerializeField] private Color _defaultColor = Color.yellow;
        
        private TrailSystem _trailSystem;
        private MagneticCursor _magneticCursor;
        private TrailData _currentTrail;
        private bool _isDrawing = false;
        private Vector3 _lastAddedWorldPoint;
        
        void Start()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }
        
        private void EnsureInitialized()
        {
            // Lazy initialization - create trail system when first needed
            if (_trailSystem == null && _mountainManager != null && _mountainManager.TerrainData != null)
            {
                // Get connectivity from LiftBuilder (shared snap registry)
                SnapRegistry registry = null;
                if (_liftBuilder != null && _liftBuilder.Connectivity != null)
                {
                    registry = _liftBuilder.Connectivity.Registry;
                    // Create magnetic cursor
                    _magneticCursor = new MagneticCursor(registry, _snapRadius);
                }
                
                _trailSystem = new TrailSystem(_mountainManager.TerrainData, registry);
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
            Vector3? worldPos = GetMountainPositionUnderMouse();
            if (worldPos.HasValue)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(worldPos.Value, 2f);
            }
        }
        
        private void HandleDrawingInput()
        {
            // Start drawing
            if (Input.GetKeyDown(_drawKey))
            {
                Vector3? position = GetMountainPositionUnderMouse();
                if (position.HasValue)
                {
                    StartDrawing(position.Value);
                }
                else if (_debugMode)
                {
                    Debug.LogWarning("Cannot start drawing - not on mountain surface");
                }
            }
            
            // Continue drawing (hold key and move mouse)
            if (Input.GetKey(_drawKey) && _isDrawing)
            {
                Vector3? position = GetMountainPositionUnderMouse();
                if (position.HasValue)
                {
                    ContinueDrawing(position.Value);
                }
            }
            
            // Finish drawing
            if (Input.GetKeyUp(_drawKey) && _isDrawing)
            {
                FinishDrawing();
            }
        }
        
        private void StartDrawing(Vector3 startPosition)
        {
            // TODO: Re-enable magnetic cursor after fixing trail building
            // For now, use raw position to ensure trails work
            _isDrawing = true;
            _currentTrail = _trailSystem.CreateTrail();
            _currentTrail.AddWorldPoint(MountainManager.ToVector3f(startPosition));
            
            // Also add legacy tile coord for backwards compatibility
            int tileX = Mathf.RoundToInt(startPosition.x / _tileSize);
            int tileY = Mathf.RoundToInt(startPosition.y / _tileSize);
            _currentTrail.AddPoint(new TileCoord(tileX, tileY));
            
            _lastAddedWorldPoint = startPosition;
        }
        
        private void ContinueDrawing(Vector3 position)
        {
            if (_currentTrail == null) return;
            
            // Check if far enough from last point (3D distance)
            float distance = Vector3.Distance(position, _lastAddedWorldPoint);
            
            if (distance >= _minPointSpacing)
            {
                _currentTrail.AddWorldPoint(MountainManager.ToVector3f(position));
                
                // Also add legacy tile coord
                int tileX = Mathf.RoundToInt(position.x / _tileSize);
                int tileY = Mathf.RoundToInt(position.y / _tileSize);
                _currentTrail.AddPoint(new TileCoord(tileX, tileY));
                
                _lastAddedWorldPoint = position;
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
                // Register snap points for EVERY point in the trail (user requirement)
                if (_liftBuilder != null && _liftBuilder.Connectivity != null)
                {
                    int snapPointsAdded = 0;
                    foreach (var point in _currentTrail.WorldPathPoints)
                    {
                        // Each trail point is a valid snap point
                        var snapPoint = new SnapPoint(SnapPointType.TrailPoint, point, _currentTrail.TrailId, $"Trail{_currentTrail.TrailId}_Point{snapPointsAdded}");
                        _liftBuilder.Connectivity.Registry.Register(snapPoint);
                        snapPointsAdded++;
                    }
                    
                    // Also register start/end as special types for connection logic
                    if (_currentTrail.WorldPathPoints.Count >= 2)
                    {
                        var startSnap = new SnapPoint(SnapPointType.TrailStart, _currentTrail.WorldPathPoints[0], _currentTrail.TrailId, $"Trail{_currentTrail.TrailId}_Start");
                        var endSnap = new SnapPoint(SnapPointType.TrailEnd, _currentTrail.WorldPathPoints[_currentTrail.WorldPathPoints.Count - 1], _currentTrail.TrailId, $"Trail{_currentTrail.TrailId}_End");
                        
                        _liftBuilder.Connectivity.Registry.Register(startSnap);
                        _liftBuilder.Connectivity.Registry.Register(endSnap);
                    }
                    
                    Debug.Log($"Snap Points Registered: {snapPointsAdded} trail points + start/end");
                }
                
                // Calculate difficulty and get detailed stats
                var stats = _trailSystem.CalculateDifficulty(_currentTrail);
                
                // Clear trees along the trail path
                List<Vector3> trailPath = new List<Vector3>();
                foreach (var point in _currentTrail.WorldPathPoints)
                {
                    trailPath.Add(MountainManager.ToUnityVector3(point));
                }
                TreeClearer.ClearTreesAlongPath(trailPath, corridorWidth: 8f); // Wider corridor for trails
                
                // Rebuild connections (automatically connects lifts to trails based on proximity)
                if (_liftBuilder != null && _liftBuilder.Connectivity != null)
                {
                    _liftBuilder.Connectivity.RebuildConnections();
                }
                
                Debug.Log($"=== TRAIL CREATED ===");
                Debug.Log($"Trail ID: {_currentTrail.TrailId}");
                Debug.Log($"Points: {_currentTrail.Length}");
                Debug.Log($"Difficulty: {_currentTrail.Difficulty}");
                Debug.Log($"--- Measurements ---");
                Debug.Log($"Drop: {stats.TotalDrop:F1} height units");
                Debug.Log($"Run: {stats.TotalRun:F1} tiles");
                Debug.Log($"AvgGrade: {stats.AvgGrade:F3} ({stats.AvgGrade * 100:F1}%)");
                Debug.Log($"MaxSegmentGrade: {stats.MaxSegmentGrade:F3} ({stats.MaxSegmentGrade * 100:F1}%) at segment {stats.MaxGradeSegment}");
                Debug.Log($"--- Thresholds ---");
                Debug.Log($"Green: < 12%, Blue: 12-22%, Black: 22-35%, Double Black: > 35%");
                
                // Log connectivity info
                if (_liftBuilder != null && _liftBuilder.Connectivity != null)
                {
                    var connectedLifts = _liftBuilder.Connectivity.Connections.GetLiftsToTrail(_currentTrail.TrailId);
                    Debug.Log($"Accessible by {connectedLifts.Count} lift(s)");
                }
                
                Debug.Log($"==================");
            }
            else
            {
                Debug.LogWarning("Trail invalid (too short, doesn't go downhill enough, or too many uphill segments). Removing.");
                _trailSystem.RemoveTrail(_currentTrail);
            }
            
            _currentTrail = null;
        }
        
        private Vector3? GetMountainPositionUnderMouse()
        {
            if (_camera == null || _mountainManager == null)
            {
                return null;
            }
            
            return _mountainManager.RaycastMountain(_camera, Input.mousePosition);
        }
        
        /// <summary>
        /// Gets the trail system for external access.
        /// </summary>
        public TrailSystem TrailSystem => _trailSystem;
        public MountainManager GridRenderer => _mountainManager;
        
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
            if (_mountainManager != null && _mountainManager.TerrainData != null)
            {
                int height = _mountainManager.TerrainData.GetHeight(coord);
                worldY += height * 0.1f; // Match heightScale from MountainManager
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
                GUI.Label(new Rect(20, 140, 280, 20), $"GridRenderer: {(_mountainManager != null ? "OK" : "NULL")}");
                GUI.Label(new Rect(20, 160, 280, 20), $"TerrainData: {(_mountainManager != null && _mountainManager.TerrainData != null ? "OK" : "NULL")}");
                return;
            }
            
            GUI.Label(new Rect(20, 120, 280, 20), $"Press and HOLD '{_drawKey}' to draw");
            GUI.Label(new Rect(20, 140, 280, 20), $"Drawing: {_isDrawing}");
            
            Vector3? position = GetMountainPositionUnderMouse();
            if (position.HasValue)
            {
                GUI.Label(new Rect(20, 160, 280, 20), $"Pos: ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1})");
                
                // Draw a visual cursor in Game view
                if (_camera != null)
                {
                    Vector3 screenPos = _camera.WorldToScreenPoint(position.Value);
                    screenPos.y = Screen.height - screenPos.y; // Flip Y for GUI coordinates
                    
                    // Draw crosshair
                    float size = 10f;
                    GUI.color = _defaultColor;
                    GUI.DrawTexture(new Rect(screenPos.x - size/2, screenPos.y - 1, size, 2), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(screenPos.x - 1, screenPos.y - size/2, 2, size), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }
            else
            {
                GUI.Label(new Rect(20, 160, 280, 20), "Pos: None (not on mountain)");
            }
            
            GUI.Label(new Rect(20, 180, 280, 20), $"Trails: {_trailSystem.Trails.Count}");
            GUI.Label(new Rect(20, 200, 280, 20), $"Status: Ready!");
        }
    }
}
