using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Unity input handler for placing lifts.
    /// Player clicks bottom station, then top station.
    /// </summary>
    public class LiftBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridDebugRenderer _gridRenderer;
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private Camera _camera;
        
        [Header("Build Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private KeyCode _buildModeKey = KeyCode.L; // Press L for Lift mode
        [SerializeField] private bool _debugMode = true;
        
        private LiftSystem _liftSystem;
        private WorldConnectivity _connectivity;
        private bool _isBuildMode = false;
        private bool _hasBottomStation = false;
        private TileCoord _bottomStation;
        private LiftData _currentLift;
        
        public LiftSystem LiftSystem => _liftSystem;
        public WorldConnectivity Connectivity => _connectivity;
        public bool IsBuildMode => _isBuildMode;
        
        void Start()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }
        
        private void EnsureInitialized()
        {
            // Lazy initialization - create lift system when first needed
            if (_liftSystem == null && _gridRenderer != null && _gridRenderer.TerrainData != null)
            {
                // Create world connectivity (shared snap point registry)
                if (_connectivity == null)
                {
                    _connectivity = new WorldConnectivity();
                }
                
                _liftSystem = new LiftSystem(_gridRenderer.TerrainData, _connectivity.Registry);
            }
        }
        
        void Update()
        {
            EnsureInitialized();
            
            if (_liftSystem == null)
            {
                // Still not ready - silently wait
                return;
            }
            
            HandleBuildMode();
            
            if (_isBuildMode)
            {
                HandlePlacement();
            }
        }
        
        private void HandleBuildMode()
        {
            // Toggle build mode
            if (Input.GetKeyDown(_buildModeKey))
            {
                _isBuildMode = !_isBuildMode;
                
                if (!_isBuildMode)
                {
                    CancelPlacement();
                }
            }
        }
        
        private void HandlePlacement()
        {
            // Click to place stations
            if (Input.GetMouseButtonDown(0)) // Left click
            {
                TileCoord? coord = GetTileUnderMouse();
                
                if (coord.HasValue)
                {
                    if (!_hasBottomStation)
                    {
                        // Place bottom station
                        PlaceBottomStation(coord.Value);
                    }
                    else
                    {
                        // Place top station and complete lift
                        PlaceTopStation(coord.Value);
                    }
                }
            }
            
            // Right click or Escape to cancel
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPlacement();
            }
        }
        
        private void PlaceBottomStation(TileCoord coord)
        {
            _bottomStation = coord;
            _hasBottomStation = true;
            _currentLift = _liftSystem.CreateLift();
            _currentLift.BottomStation = coord;
        }
        
        private void PlaceTopStation(TileCoord coord)
        {
            _currentLift.TopStation = coord;
            
            // Try to build the lift
            if (_simulationRunner != null && _simulationRunner.Sim != null)
            {
                string errorMessage;
                bool success = _liftSystem.TryPlaceLift(_currentLift, _simulationRunner.Sim.State, out errorMessage);
                
                if (success)
                {
                    // Rebuild connections
                    _connectivity.RebuildConnections();
                    
                    // Log lift info
                    Debug.Log($"=== LIFT BUILT ===");
                    Debug.Log($"Lift ID: {_currentLift.LiftId}");
                    Debug.Log($"Length: {_currentLift.Length} tiles");
                    Debug.Log($"Elevation Gain: {_currentLift.ElevationGain} units");
                    Debug.Log($"Cost: ${_currentLift.BuildCost}");
                    Debug.Log($"Money Remaining: ${_simulationRunner.Sim.State.Money}");
                    
                    // Log connectivity info
                    var connectedTrails = _connectivity.Connections.GetTrailsFromLift(_currentLift.LiftId);
                    Debug.Log($"Connected to {connectedTrails.Count} trail(s)");
                    Debug.Log($"==================");
                }
                else
                {
                    Debug.LogWarning($"Failed to build lift: {errorMessage}");
                }
            }
            
            // Reset for next lift
            _hasBottomStation = false;
            _currentLift = null;
        }
        
        private void CancelPlacement()
        {
            _hasBottomStation = false;
            _currentLift = null;
        }
        
        private TileCoord? GetTileUnderMouse()
        {
            if (_camera == null)
            {
                if (_debugMode) Debug.LogWarning("[LiftBuilder] Camera is null!");
                return null;
            }
            
            // Create a ray from the camera through the mouse position
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            
            // Create a plane at Z=0 (where our grid is)
            Plane gridPlane = new Plane(Vector3.forward, Vector3.zero);
            
            // Raycast against the plane
            if (gridPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPos = ray.GetPoint(distance);
                
                int tileX = Mathf.RoundToInt(worldPos.x / _tileSize);
                int tileY = Mathf.RoundToInt(worldPos.y / _tileSize);
                
                TileCoord coord = new TileCoord(tileX, tileY);
                
                if (_gridRenderer != null && 
                    _gridRenderer.TerrainData != null && 
                    _gridRenderer.TerrainData.Grid.InBounds(coord))
                {
                    return coord;
                }
            }
            
            return null;
        }
        
        void OnGUI()
        {
            if (!_debugMode) return;
            
            if (_isBuildMode)
            {
                GUI.Box(new Rect(10, 210, 300, 120), "Lift Build Mode");
                
                // Show current tile under cursor
                TileCoord? cursorTile = GetTileUnderMouse();
                if (cursorTile.HasValue)
                {
                    GUI.Label(new Rect(20, 230, 280, 20), $"Cursor: {cursorTile.Value}");
                }
                else
                {
                    GUI.Label(new Rect(20, 230, 280, 20), "Cursor: (out of bounds)");
                }
                
                if (_hasBottomStation)
                {
                    GUI.Label(new Rect(20, 250, 280, 20), $"Bottom: {_bottomStation}");
                    GUI.Label(new Rect(20, 270, 280, 20), "Click TOP station");
                    GUI.Label(new Rect(20, 290, 280, 20), "Right-click to cancel");
                }
                else
                {
                    GUI.Label(new Rect(20, 250, 280, 20), "Click BOTTOM station");
                }
                
                GUI.Label(new Rect(20, 310, 280, 20), $"Lifts: {_liftSystem?.Lifts.Count ?? 0}");
            }
            else
            {
                GUI.Box(new Rect(10, 210, 300, 40), "");
                GUI.Label(new Rect(20, 220, 280, 20), $"Press 'L' for Lift Build Mode");
            }
        }
    }
}

