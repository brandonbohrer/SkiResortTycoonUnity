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
        [SerializeField] private float _snapRadius = 5f; // Magnetic cursor snap radius
        
        [Header("Visual Feedback")]
        [SerializeField] private Color _snapColor = Color.green;
        [SerializeField] private Color _defaultColor = Color.white;
        
        [Header("Prefab Builder (optional - enables 3D lift visuals)")]
        [SerializeField] private LiftPrefabBuilder _prefabBuilder;
        
        private LiftSystem _liftSystem;
        private WorldConnectivity _connectivity;
        private MagneticCursor _magneticCursor;
        private bool _isBuildMode = false;
        private bool _hasBottomStation = false;
        private TileCoord _bottomStation;
        private LiftData _currentLift;
        private GameObject _cursorVisual;
        
        public LiftSystem LiftSystem => _liftSystem;
        public WorldConnectivity Connectivity => _connectivity;
        public bool IsBuildMode => _isBuildMode;
        public bool HasBottomStation => _hasBottomStation;
        public Vector3? BottomWorldPosition => _hasBottomStation && _currentLift != null
            ? (Vector3?)MountainManager.ToUnityVector3(_currentLift.StartPosition) : null;
        public LiftPrefabBuilder PrefabBuilder => _prefabBuilder;
        
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
                
                // Create magnetic cursor
                _magneticCursor = new MagneticCursor(_connectivity.Registry, _snapRadius);
                
                // Create cursor visual (larger sphere for visibility)
                _cursorVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _cursorVisual.name = "LiftCursor";
                _cursorVisual.transform.localScale = Vector3.one * 2.0f; // Bigger sphere for visibility
                var renderer = _cursorVisual.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = _defaultColor;
                Destroy(_cursorVisual.GetComponent<Collider>()); // Remove collider
                _cursorVisual.SetActive(false); // Hidden until build mode
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
                    if (_cursorVisual != null)
                    {
                        _cursorVisual.SetActive(false);
                    }
                    TreeClearer.RestorePreviewTrees();
                }
                else
                {
                    if (_cursorVisual != null)
                    {
                        _cursorVisual.SetActive(true);
                    }
                }
            }
        }
        
        private void HandlePlacement()
        {
            // Update magnetic cursor
            Vector3? rawPosition = GetMountainPositionUnderMouse();
            
            if (rawPosition.HasValue && _magneticCursor != null)
            {
                // Update magnetic cursor (snap to lift tops/bottoms or trail ends when placing bottom, no snap for top)
                SnapPointType[] validTypes = null;
                if (!_hasBottomStation)
                {
                    // When placing bottom station, snap to lift tops, trail ends, or base
                    validTypes = new SnapPointType[] { SnapPointType.LiftTop, SnapPointType.TrailEnd, SnapPointType.BaseSpawn };
                }
                // When placing top station, don't snap (validTypes = null allows free placement)
                
                _magneticCursor.Update(rawPosition.Value, validTypes);
                
                // Update cursor visual
                if (_cursorVisual != null)
                {
                    _cursorVisual.transform.position = _magneticCursor.SnappedPosition;
                    var renderer = _cursorVisual.GetComponent<Renderer>();
                    renderer.material.color = _magneticCursor.IsSnapped ? _snapColor : _defaultColor;
                }
            }
            
            // Live preview: update prefab builder while dragging top point
            if (_hasBottomStation && rawPosition.HasValue)
            {
                Vector3 baseWorld = MountainManager.ToUnityVector3(_currentLift.StartPosition);
                Vector3 topWorld = _magneticCursor != null ? _magneticCursor.SnappedPosition : rawPosition.Value;
                
                // Only show preview if top is above base (valid lift)
                if (topWorld.y > baseWorld.y)
                {
                    // Update 3D preview
                    if (_prefabBuilder != null)
                    {
                        _prefabBuilder.UpdatePreview(baseWorld, topWorld);
                    }
                    
                    // Dynamic tree clearing for preview
                    float length = Vector3.Distance(baseWorld, topWorld);
                    float step = 3f; // Dense sampling for smooth preview clearing
                    int samples = Mathf.Max(2, Mathf.CeilToInt(length / step) + 1);
                    var points = new List<Vector3>();
                    for (int i = 0; i < samples; i++)
                    {
                        float t = (float)i / (samples - 1);
                        points.Add(Vector3.Lerp(baseWorld, topWorld, t));
                    }
                    TreeClearer.ClearTreesForPreview(points, corridorWidth: 8f);
                }
                else
                {
                    if (_prefabBuilder != null) _prefabBuilder.DestroyPreview();
                    TreeClearer.RestorePreviewTrees();
                }
            }
            else if (!_hasBottomStation)
            {
                // Not placing yet, ensure preview trees are restored
                TreeClearer.RestorePreviewTrees();
            }
            
            // Click to place stations
            if (Input.GetMouseButtonDown(0)) // Left click
            {
                if (rawPosition.HasValue)
                {
                    // Use snapped position from magnetic cursor
                    Vector3 placementPosition = _magneticCursor != null ? _magneticCursor.SnappedPosition : rawPosition.Value;
                    
                    if (!_hasBottomStation)
                    {
                        // Place bottom station
                        PlaceBottomStation(placementPosition);
                    }
                    else
                    {
                        // Place top station and complete lift
                        PlaceTopStation(placementPosition);
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
                    // Register snap points for this lift (top and bottom only)
                    var bottomSnap = new SnapPoint(SnapPointType.LiftBottom, _currentLift.StartPosition, _currentLift.LiftId, $"Lift{_currentLift.LiftId}_Bottom");
                    var topSnap = new SnapPoint(SnapPointType.LiftTop, _currentLift.EndPosition, _currentLift.LiftId, $"Lift{_currentLift.LiftId}_Top");
                    
                    _connectivity.Registry.Register(bottomSnap);
                    _connectivity.Registry.Register(topSnap);
                    
                    Vector3 baseWorld = MountainManager.ToUnityVector3(_currentLift.StartPosition);
                    Vector3 topWorld  = MountainManager.ToUnityVector3(_currentLift.EndPosition);
                    
                    // Restore preview trees first, then permanently clear
                    TreeClearer.RestorePreviewTrees();
                    
                    // Clear trees along the FULL lift corridor (not just endpoints)
                    if (_prefabBuilder != null)
                    {
                        _prefabBuilder.DestroyPreview();
                        _prefabBuilder.ClearTreesAlongLift(baseWorld, topWorld);
                        _prefabBuilder.BuildLift(_currentLift);
                    }
                    else
                    {
                        // Legacy: clear at endpoints only
                        List<Vector3> liftPath = new List<Vector3> { baseWorld, topWorld };
                        TreeClearer.ClearTreesAlongPath(liftPath, corridorWidth: 5f);
                    }
                    
                    // Rebuild connections (this will automatically connect lift tops to nearby trail starts)
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
                    Debug.Log($"Snap Points Registered: Bottom + Top");
                    
                    // Log connectivity info
                    var connectedTrails = _connectivity.Connections.GetTrailsFromLift(_currentLift.LiftId);
                    Debug.Log($"Connected to {connectedTrails.Count} trail(s)");
                    Debug.Log($"==================");
                    
                    // Invalidate all skier goals so they re-plan with the new lift
                    var skierViz = FindObjectOfType<SkierVisualizer>();
                    if (skierViz != null) skierViz.InvalidateAllSkierGoals();
                }
                else
                {
                    Debug.LogWarning($"Failed to build lift: {errorMessage}");
                }
            }
            
            // Reset for next lift
            _hasBottomStation = false;
            _currentLift = null;
            
            // Clean up preview visuals
            if (_prefabBuilder != null) _prefabBuilder.DestroyPreview();
            TreeClearer.RestorePreviewTrees();
        }
        
        private void CancelPlacement()
        {
            _hasBottomStation = false;
            _currentLift = null;
            if (_prefabBuilder != null) _prefabBuilder.DestroyPreview();
            TreeClearer.RestorePreviewTrees();
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

