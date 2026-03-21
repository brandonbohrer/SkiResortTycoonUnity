using System;
using UnityEngine;
using SkiResortTycoon.Core;
using SkiResortTycoon.UI;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Unity input handler for placing lifts.
    /// Player clicks bottom station, then top station.
    /// After the second click the lift enters PendingConfirmation — the UI shows
    /// a context window with stats and Confirm / Cancel buttons before the lift
    /// is actually registered or charged.
    /// </summary>
    public class LiftBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MountainManager _mountainManager;
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private Camera _camera;
        
        [Header("Build Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private KeyCode _buildModeKey = KeyCode.L;
        [SerializeField] private bool _debugMode = true;
        [SerializeField] private float _snapRadius = 25f;
        [SerializeField] private LiftType _selectedLiftType = LiftType.OneSeatLowSpeed;
        
        [Header("Visual Feedback")]
        [SerializeField] private Color _snapColor = Color.green;
        [SerializeField] private Color _defaultColor = Color.white;
        [Tooltip("Material used for the lift cursor sphere. Must be assigned — Shader.Find is not used in builds.")]
        [SerializeField] private Material _cursorMaterialTemplate;
        [Tooltip("Limits heavy lift preview rebuild work while dragging (seconds between refreshes).")]
        [SerializeField] private float _previewRefreshInterval = 0.05f;
        [Tooltip("Minimum cursor movement (meters) before forcing a preview refresh.")]
        [SerializeField] private float _previewMinMoveDistance = 0.35f;
        
        [Header("Prefab Builder (optional - enables 3D lift visuals)")]
        [SerializeField] private LiftPrefabBuilder _prefabBuilder;
        
        // ── Runtime state ────────────────────────────────────────────────────
        private LiftSystem _liftSystem;
        private WorldConnectivity _connectivity;
        private MagneticCursor _magneticCursor;
        private bool _isBuildMode = false;
        private bool _hasBottomStation = false;
        private bool _isPendingConfirmation = false;
        private TileCoord _bottomStation;
        private LiftData _currentLift;
        private GameObject _cursorVisual;
        private bool _previewVisible;
        private bool _hasLastPreviewPose;
        private Vector3 _lastPreviewBaseWorld;
        private Vector3 _lastPreviewTopWorld;
        private LiftType _lastPreviewLiftType;
        private float _nextPreviewRefreshTime;
        private readonly List<Vector3> _previewPathPoints = new List<Vector3>(64);

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>Fired immediately after the bottom station is placed.</summary>
        public event Action OnBottomStationPlaced;

        /// <summary>
        /// Fired every frame while the player is hovering the top station.
        /// Carries: lengthM, elevationM, baseCost, addedCost — for live stat display.
        /// </summary>
        public event Action<float, float, int, int> OnLiftPreviewUpdated;

        /// <summary>
        /// Fired after the top station is placed. The lift is valid and ready to
        /// confirm, but has NOT been charged or registered yet.
        /// Carries: liftData, baseCost, lengthAddedCost.
        /// </summary>
        public event Action<LiftData, int, int> OnLiftReadyToConfirm;

        /// <summary>
        /// Fired after ConfirmLift() succeeds. Carries the SelectableStructure on
        /// the newly-built lift root so subscribers can open its context window.
        /// </summary>
        public event Action<SelectableStructure> OnLiftPlaced;

        /// <summary>Fired when a pending lift is cancelled (or confirmation fails).</summary>
        public event Action OnLiftCancelled;

        // ── Public properties ────────────────────────────────────────────────
        public LiftSystem LiftSystem => _liftSystem;
        public WorldConnectivity Connectivity => _connectivity;
        public bool IsBuildMode => _isBuildMode;
        public bool HasBottomStation => _hasBottomStation;
        public bool IsPendingConfirmation => _isPendingConfirmation;
        public Vector3? BottomWorldPosition => _hasBottomStation && _currentLift != null
            ? (Vector3?)MountainManager.ToUnityVector3(_currentLift.StartPosition) : null;
        public LiftPrefabBuilder PrefabBuilder => _prefabBuilder;
        public LiftType SelectedLiftType => _selectedLiftType;
        
        void Start()
        {
            if (_camera == null) _camera = Camera.main;
        }
        
        private void EnsureInitialized()
        {
            if (_liftSystem == null && _mountainManager != null && _mountainManager.TerrainData != null)
            {
                if (_connectivity == null)
                    _connectivity = new WorldConnectivity();
                
                _liftSystem = new LiftSystem(_mountainManager.TerrainData, _connectivity.Registry);
                _magneticCursor = new MagneticCursor(_connectivity.Registry, _snapRadius);
                
                _cursorVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _cursorVisual.name = "LiftCursor";
                _cursorVisual.transform.localScale = Vector3.one * 2.0f;
                var rend = _cursorVisual.GetComponent<Renderer>();
                if (_cursorMaterialTemplate != null)
                {
                    rend.material = new Material(_cursorMaterialTemplate);
                    rend.material.color = _defaultColor;
                }
                else
                {
                    Debug.LogWarning("[LiftBuilder] _cursorMaterialTemplate not assigned — cursor will use default material. Assign a Standard material to fix this in builds.");
                }
                Destroy(_cursorVisual.GetComponent<Collider>());
                _cursorVisual.SetActive(false);
            }
        }
        
        void Update()
        {
            EnsureInitialized();
            if (_liftSystem == null) return;
            HandleBuildMode();
            if (_isBuildMode) HandlePlacement();
        }
        
        // ── Input ────────────────────────────────────────────────────────────

        private void HandleBuildMode()
        {
            if (_isPendingConfirmation) return; // L-key is locked while confirming

            if (Input.GetKeyDown(_buildModeKey))
            {
                _isBuildMode = !_isBuildMode;
                
                if (!_isBuildMode)
                {
                    CancelPlacement();
                    if (_cursorVisual != null) _cursorVisual.SetActive(false);
                    TreeClearer.RestorePreviewTrees();
                }
                else
                {
                    if (_cursorVisual != null) _cursorVisual.SetActive(true);
                }
            }
        }
        
        private void HandlePlacement()
        {
            // While waiting for Confirm/Cancel, only allow ESC or right-click to cancel
            if (_isPendingConfirmation)
            {
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                    CancelPendingLift();
                return;
            }

            // ── Cursor update ────────────────────────────────────────────
            Vector3? rawPosition = GetMountainPositionUnderMouse();

            if (rawPosition.HasValue && _magneticCursor != null)
            {
                SnapPointType[] validTypes = null;
                if (!_hasBottomStation)
                    validTypes = new SnapPointType[] { SnapPointType.LiftTop, SnapPointType.TrailEnd, SnapPointType.BaseSpawn, SnapPointType.BuildingEntrance };
                
                _magneticCursor.Update(rawPosition.Value, validTypes);
                
                if (_cursorVisual != null)
                {
                    _cursorVisual.transform.position = _magneticCursor.SnappedPosition;
                    var rend = _cursorVisual.GetComponent<Renderer>();
                    rend.material.color = _magneticCursor.IsSnapped ? _snapColor : _defaultColor;
                }
            }
            
            // ── Live preview ─────────────────────────────────────────────
            if (_hasBottomStation && rawPosition.HasValue)
            {
                Vector3 baseWorld = MountainManager.ToUnityVector3(_currentLift.StartPosition);
                Vector3 topWorld = _magneticCursor != null ? _magneticCursor.SnappedPosition : rawPosition.Value;
                
                if (topWorld.y > baseWorld.y)
                {
                    MaybeRefreshLivePreview(baseWorld, topWorld);
                    _previewVisible = true;

                    // Fire live stat update for the context window
                    if (_liftSystem != null && OnLiftPreviewUpdated != null)
                    {
                        float length = Vector3.Distance(baseWorld, topWorld);
                        float elevation = topWorld.y - baseWorld.y;
                        int baseCost    = _liftSystem.BaseCost;
                        int addedCost   = (int)(length * _liftSystem.CostPerTile)
                                        + (int)(elevation * _liftSystem.CostPerHeightUnit);
                        OnLiftPreviewUpdated.Invoke(length, elevation, baseCost, addedCost);
                    }
                }
                else
                {
                    HideLivePreview();
                }
            }
            else if (!_hasBottomStation)
            {
                HideLivePreview();
            }
            
            // ── Click to place ───────────────────────────────────────────
            if (Input.GetMouseButtonDown(0) && rawPosition.HasValue)
            {
                Vector3 placementPosition = _magneticCursor != null
                    ? _magneticCursor.SnappedPosition : rawPosition.Value;
                
                if (!_hasBottomStation)
                    PlaceBottomStation(placementPosition);
                else
                    PlaceTopStation(placementPosition);
            }
            
            // ── Cancel ───────────────────────────────────────────────────
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                CancelPlacement();
        }
        
        // ── Placement steps ──────────────────────────────────────────────────

        private void PlaceBottomStation(Vector3 worldPosition)
        {
            if (!LiftTypeSpecs.IsImplemented(_selectedLiftType))
            {
                NotificationManager.Instance?.ShowWarning($"{LiftTypeSpecs.GetDisplayName(_selectedLiftType)} is not implemented yet.");
                return;
            }

            _hasBottomStation = true;
            _currentLift = _liftSystem.CreateLift();
            _currentLift.StartPosition = MountainManager.ToVector3f(worldPosition);
            _currentLift.Type = _selectedLiftType;
            _currentLift.Capacity = LiftTypeSpecs.GetCapacityPerHour(_selectedLiftType);
            
            int tileX = Mathf.RoundToInt(worldPosition.x / _tileSize);
            int tileY = Mathf.RoundToInt(worldPosition.z / _tileSize);
            _bottomStation = new TileCoord(tileX, tileY);
            _currentLift.BottomStation = _bottomStation;

            OnBottomStationPlaced?.Invoke();
        }
        
        private void PlaceTopStation(Vector3 worldPosition)
        {
            _currentLift.EndPosition = MountainManager.ToVector3f(worldPosition);
            
            int tileX = Mathf.RoundToInt(worldPosition.x / _tileSize);
            int tileY = Mathf.RoundToInt(worldPosition.z / _tileSize);
            _currentLift.TopStation = new TileCoord(tileX, tileY);
            
            float elevationGain = _currentLift.EndPosition.Y - _currentLift.StartPosition.Y;
            if (elevationGain <= 0)
            {
                Debug.LogWarning($"[LiftBuilder] Lift must go uphill! Elevation gain: {elevationGain:F1}m");
                NotificationManager.Instance?.ShowWarning("Lift must go uphill!");
                _hasBottomStation = false;
                _currentLift = null;
                if (_prefabBuilder != null) _prefabBuilder.DestroyPreview();
                TreeClearer.RestorePreviewTrees();
                return;
            }
            
            _currentLift.Length = Vector3f.Distance(_currentLift.StartPosition, _currentLift.EndPosition);
            _currentLift.ElevationGain = elevationGain;

            // Pre-calculate cost so the context window can show the breakdown
            int baseCost = 0;
            int lengthAddedCost = 0;
            if (_liftSystem != null)
            {
                _liftSystem.CalculateCost(_currentLift); // sets _currentLift.BuildCost
                baseCost = _liftSystem.BaseCost;
                lengthAddedCost = _currentLift.BuildCost - baseCost;
            }

            // Enter pending confirmation — preview stays visible, no money charged yet
            _hasBottomStation = false;
            _isPendingConfirmation = true;
            if (_cursorVisual != null) _cursorVisual.SetActive(false);

            OnLiftReadyToConfirm?.Invoke(_currentLift, baseCost, lengthAddedCost);
        }

        // ── Public confirm / cancel API ──────────────────────────────────────

        /// <summary>
        /// Finalizes the pending lift: deducts cost, registers snap points,
        /// builds the 3D visual, and fires OnLiftPlaced with the SelectableStructure.
        /// </summary>
        public void ConfirmLift()
        {
            if (_currentLift == null || !_isPendingConfirmation)
            {
                Debug.LogWarning("[LiftBuilder] ConfirmLift called but no lift is pending.");
                return;
            }

            SelectableStructure selectable = null;
            bool success = false;

            if (_simulationRunner != null && _simulationRunner.Sim != null)
            {
                string errorMessage;
                success = _liftSystem.TryPlaceLift(_currentLift, _simulationRunner.Sim.State, out errorMessage);

                if (success)
                {
                    var bottomSnap = new SnapPoint(SnapPointType.LiftBottom, _currentLift.StartPosition,
                        _currentLift.LiftId, $"Lift{_currentLift.LiftId}_Bottom");
                    var topSnap = new SnapPoint(SnapPointType.LiftTop, _currentLift.EndPosition,
                        _currentLift.LiftId, $"Lift{_currentLift.LiftId}_Top");
                    _connectivity.Registry.Register(bottomSnap);
                    _connectivity.Registry.Register(topSnap);

                    Vector3 baseWorld = MountainManager.ToUnityVector3(_currentLift.StartPosition);
                    Vector3 topWorld  = MountainManager.ToUnityVector3(_currentLift.EndPosition);

                    TreeClearer.RestorePreviewTrees();

                    if (_prefabBuilder != null)
                    {
                        _prefabBuilder.DestroyPreview();
                        _prefabBuilder.ClearTreesAlongLift(baseWorld, topWorld);
                        var liftRoot = _prefabBuilder.BuildLift(_currentLift);
                        if (liftRoot != null)
                            selectable = liftRoot.GetComponent<SelectableStructure>();
                    }
                    else
                    {
                        TreeClearer.ClearTreesAlongPath(new List<Vector3> { baseWorld, topWorld }, 5f);
                    }

                    _connectivity.RebuildConnections();

                    var skierViz = FindObjectOfType<SkierVisualizer>();
                    if (skierViz != null) skierViz.InvalidateAllSkierGoals();

                    Debug.Log($"[LiftBuilder] Lift {_currentLift.LiftId} confirmed. Cost: ${_currentLift.BuildCost}");
                }
                else
                {
                    Debug.LogWarning($"[LiftBuilder] Confirmation failed: {errorMessage}");
                    NotificationManager.Instance?.ShowError($"Cannot build lift: {errorMessage}");
                }
            }

            // Always reset state after attempt
            var confirmedLift = _currentLift;
            ResetBuildState();

            if (success)
                OnLiftPlaced?.Invoke(selectable);
            else
                OnLiftCancelled?.Invoke(); // treat failed confirm as cancel
        }

        /// <summary>
        /// Loads lifts from save data (no cost). Call after EnsureInitialized. Registers snap points, builds visuals, clears trees, rebuilds connections.
        /// </summary>
        public void LoadLiftsFromSave(IList<LiftData> lifts)
        {
            if (lifts == null || lifts.Count == 0) return;
            EnsureInitialized();
            if (_liftSystem == null || _connectivity == null) return;
            _liftSystem.LoadLifts(lifts);
            TreeClearer.RestorePreviewTrees();
            foreach (var lift in _liftSystem.Lifts)
            {
                var bottomSnap = new SnapPoint(SnapPointType.LiftBottom, lift.StartPosition, lift.LiftId, $"Lift{lift.LiftId}_Bottom");
                var topSnap = new SnapPoint(SnapPointType.LiftTop, lift.EndPosition, lift.LiftId, $"Lift{lift.LiftId}_Top");
                _connectivity.Registry.Register(bottomSnap);
                _connectivity.Registry.Register(topSnap);
                Vector3 baseWorld = MountainManager.ToUnityVector3(lift.StartPosition);
                Vector3 topWorld = MountainManager.ToUnityVector3(lift.EndPosition);
                if (_prefabBuilder != null)
                {
                    _prefabBuilder.ClearTreesAlongLift(baseWorld, topWorld);
                    _prefabBuilder.BuildLift(lift);
                }
                else
                    TreeClearer.ClearTreesAlongPath(new List<Vector3> { baseWorld, topWorld }, 5f);
            }
            _connectivity.RebuildConnections();
        }

        /// <summary>
        /// Cancels the pending lift, destroys the preview, and fires OnLiftCancelled.
        /// </summary>
        public void CancelPendingLift()
        {
            if (!_isPendingConfirmation && _currentLift == null) return;

            if (_prefabBuilder != null) _prefabBuilder.DestroyPreview();
            TreeClearer.RestorePreviewTrees();

            ResetBuildState();
            OnLiftCancelled?.Invoke();
        }

        // ── Internals ────────────────────────────────────────────────────────

        private void ResetBuildState()
        {
            _isPendingConfirmation = false;
            _hasBottomStation = false;
            _currentLift = null;
            _isBuildMode = false;
            if (_cursorVisual != null) _cursorVisual.SetActive(false);
            HideLivePreview();
        }

        private void CancelPlacement()
        {
            _hasBottomStation = false;
            _currentLift = null;
            HideLivePreview();
        }
        
        private Vector3? GetMountainPositionUnderMouse()
        {
            if (_camera == null || _mountainManager == null) return null;
            return _mountainManager.RaycastMountain(_camera, Input.mousePosition);
        }


        public void SetSelectedLiftType(LiftType liftType)
        {
            _selectedLiftType = liftType;
            if (_currentLift != null)
            {
                _currentLift.Type = liftType;
                _currentLift.Capacity = LiftTypeSpecs.GetCapacityPerHour(liftType);
            }
        }

        private void MaybeRefreshLivePreview(Vector3 baseWorld, Vector3 topWorld)
        {
            float now = Time.unscaledTime;
            bool hasPose = _hasLastPreviewPose;
            float topDelta = hasPose ? Vector3.Distance(topWorld, _lastPreviewTopWorld) : float.MaxValue;
            float baseDelta = hasPose ? Vector3.Distance(baseWorld, _lastPreviewBaseWorld) : float.MaxValue;
            bool typeChanged = !hasPose || _selectedLiftType != _lastPreviewLiftType;
            bool movedEnough = !hasPose || topDelta >= _previewMinMoveDistance || baseDelta >= _previewMinMoveDistance;
            bool intervalElapsed = now >= _nextPreviewRefreshTime;

            if (!typeChanged && !movedEnough && !intervalElapsed)
                return;

            if (_prefabBuilder != null)
                _prefabBuilder.UpdatePreview(baseWorld, topWorld, _selectedLiftType);

            float length = Vector3.Distance(baseWorld, topWorld);
            int samples = Mathf.Max(2, Mathf.CeilToInt(length / 3f) + 1);
            _previewPathPoints.Clear();
            if (_previewPathPoints.Capacity < samples)
                _previewPathPoints.Capacity = samples;
            for (int i = 0; i < samples; i++)
                _previewPathPoints.Add(Vector3.Lerp(baseWorld, topWorld, (float)i / (samples - 1)));
            TreeClearer.ClearTreesForPreview(_previewPathPoints, corridorWidth: 8f);

            _hasLastPreviewPose = true;
            _lastPreviewBaseWorld = baseWorld;
            _lastPreviewTopWorld = topWorld;
            _lastPreviewLiftType = _selectedLiftType;
            _nextPreviewRefreshTime = now + Mathf.Max(0.01f, _previewRefreshInterval);
        }

        private void HideLivePreview()
        {
            if (!_previewVisible && !_hasLastPreviewPose)
                return;

            if (_prefabBuilder != null)
                _prefabBuilder.DestroyPreview();
            TreeClearer.RestorePreviewTrees();

            _previewVisible = false;
            _hasLastPreviewPose = false;
            _nextPreviewRefreshTime = 0f;
            _previewPathPoints.Clear();
        }
        
        void OnGUI()
        {
            if (!_debugMode) return;
            
            if (_isPendingConfirmation)
            {
                GUI.Box(new Rect(10, 210, 300, 60), "Lift Build Mode — Pending Confirmation");
                GUI.Label(new Rect(20, 240, 280, 20), "Confirm or cancel in the context window");
            }
            else if (_isBuildMode)
            {
                GUI.Box(new Rect(10, 210, 300, 120), "Lift Build Mode");
                
                Vector3? cursorPos = GetMountainPositionUnderMouse();
                GUI.Label(new Rect(20, 230, 280, 20), cursorPos.HasValue
                    ? $"Cursor: ({cursorPos.Value.x:F1}, {cursorPos.Value.y:F1}, {cursorPos.Value.z:F1})"
                    : "Cursor: (not on mountain)");
                
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
                GUI.Label(new Rect(20, 220, 280, 20), "Press 'L' for Lift Build Mode");
            }
        }
    }
}
