using UnityEngine;
using SkiResortTycoon.Core;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Unity input handler for placing lifts.
    /// Player clicks bottom station, then top station.
    /// </summary>
    public class LiftBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MountainManager _mountainManager;
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
            if (_liftSystem == null && _mountainManager != null && _mountainManager.TerrainData != null)
            {
                // Create world connectivity (shared snap point registry)
                if (_connectivity == null)
                {
                    _connectivity = new WorldConnectivity();
                }
                
                _liftSystem = new LiftSystem(_mountainManager.TerrainData, _connectivity.Registry);
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
                Vector3? position = GetMountainPositionUnderMouse();
                
                if (position.HasValue)
                {
                    if (!_hasBottomStation)
                    {
                        // Place bottom station
                        PlaceBottomStation(position.Value);
                    }
                    else
                    {
                        // Place top station and complete lift
                        PlaceTopStation(position.Value);
                    }
                }
            }
            
            // Right click or Escape to cancel
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPlacement();
            }
        }
        
        private void PlaceBottomStation(Vector3 worldPosition)
        {
            _hasBottomStation = true;
            _currentLift = _liftSystem.CreateLift();
            _currentLift.StartPosition = MountainManager.ToVector3f(worldPosition);
            
            // Also set legacy tile coord for backwards compatibility (optional)
            int tileX = Mathf.RoundToInt(worldPosition.x / _tileSize);
            int tileY = Mathf.RoundToInt(worldPosition.y / _tileSize);
            _bottomStation = new TileCoord(tileX, tileY);
            _currentLift.BottomStation = _bottomStation;
        }
        
        private void PlaceTopStation(Vector3 worldPosition)
        {
            _currentLift.EndPosition = MountainManager.ToVector3f(worldPosition);
            
            // Set legacy tile coord
            int tileX = Mathf.RoundToInt(worldPosition.x / _tileSize);
            int tileY = Mathf.RoundToInt(worldPosition.y / _tileSize);
            _currentLift.TopStation = new TileCoord(tileX, tileY);
            
            // Validate: lift must go uphill
            float elevationGain = _currentLift.EndPosition.Y - _currentLift.StartPosition.Y;
            if (elevationGain <= 0)
            {
                Debug.LogWarning($"Lift must go uphill! Elevation gain: {elevationGain:F1}m (negative or zero)");
                _hasBottomStation = false;
                _currentLift = null;
                return;
            }
            
            // Calculate 3D distance and elevation
            _currentLift.Length = Vector3f.Distance(_currentLift.StartPosition, _currentLift.EndPosition);
            _currentLift.ElevationGain = elevationGain;
            
            // Try to build the lift
            if (_simulationRunner != null && _simulationRunner.Sim != null)
            {
                string errorMessage;
                bool success = _liftSystem.TryPlaceLift(_currentLift, _simulationRunner.Sim.State, out errorMessage);
                
                if (success)
                {
                    // Clear trees along the lift path
                    List<Vector3> liftPath = new List<Vector3>
                    {
                        MountainManager.ToUnityVector3(_currentLift.StartPosition),
                        MountainManager.ToUnityVector3(_currentLift.EndPosition)
                    };
                    TreeClearer.ClearTreesAlongPath(liftPath, corridorWidth: 5f);
                    
                    // Rebuild connections
                    _connectivity.RebuildConnections();
                    
                    // Log lift info
                    Debug.Log($"=== LIFT BUILT ===");
                    Debug.Log($"Lift ID: {_currentLift.LiftId}");
                    Debug.Log($"Start: {_currentLift.StartPosition}");
                    Debug.Log($"End: {_currentLift.EndPosition}");
                    Debug.Log($"Length: {_currentLift.Length:F1}m");
                    Debug.Log($"Elevation Gain: {_currentLift.ElevationGain:F1}m");
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
        
        private Vector3? GetMountainPositionUnderMouse()
        {
            if (_camera == null || _mountainManager == null)
            {
                return null;
            }
            
            return _mountainManager.RaycastMountain(_camera, Input.mousePosition);
        }
        
        void OnGUI()
        {
            if (!_debugMode) return;
            
            if (_isBuildMode)
            {
                GUI.Box(new Rect(10, 210, 300, 120), "Lift Build Mode");
                
                // Show current position under cursor
                Vector3? cursorPos = GetMountainPositionUnderMouse();
                if (cursorPos.HasValue)
                {
                    GUI.Label(new Rect(20, 230, 280, 20), $"Cursor: ({cursorPos.Value.x:F1}, {cursorPos.Value.y:F1}, {cursorPos.Value.z:F1})");
                }
                else
                {
                    GUI.Label(new Rect(20, 230, 280, 20), "Cursor: (not on mountain)");
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

