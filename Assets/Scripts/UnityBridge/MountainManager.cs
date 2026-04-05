using UnityEngine;
using SkiResortTycoon.Core;
using SkiResortTycoon.Maps;
using SkiResortTycoon.Saving;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Central manager for the handcrafted mountain and grid system.
    /// At startup, discovers all <see cref="MapRoot"/> components in the scene,
    /// enables the one matching the player's map choice, disables the rest,
    /// and wires up terrain data for all other systems.
    /// </summary>
    public class MountainManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int _gridWidth = 64;
        [SerializeField] private int _gridHeight = 64;
        [SerializeField] private float _tileSize = 1f;
        
        [Header("Map System")]
        [SerializeField] private MapRegistry _mapRegistry;

        [Header("Mountain Reference (auto-populated at runtime from active MapRoot)")]
        [SerializeField] private GameObject _mountainMesh;

        private int _mountainLayerMask = -1;
        private bool _hasDedicatedLayer;
        private readonly RaycastHit[] _mouseRayHits = new RaycastHit[256];
        private readonly RaycastHit[] _heightRayHits = new RaycastHit[128];
        
        private Core.TerrainData _terrainData;
        private string _activeMapId;
        
        public Core.TerrainData TerrainData => _terrainData;
        public float TileSize => _tileSize;

        /// <summary>
        /// The map ID currently loaded. Used by the save system to persist which map is active.
        /// </summary>
        public string ActiveMapId => _activeMapId;

        /// <summary>
        /// The mountain mesh GameObject for the active map. Used by CameraController for
        /// bounds detection and terrain collision instead of reflection.
        /// </summary>
        public GameObject MountainMesh => _mountainMesh;

        /// <summary>
        /// The active MapRoot, exposing per-map camera overrides and other settings.
        /// </summary>
        public MapRoot ActiveMapRoot { get; private set; }
        
        void Awake()
        {
            _terrainData = new Core.TerrainData(_gridWidth, _gridHeight, seed: 0);
            
            ActivateSelectedMap();
            SetupMountainLayer();
            
            Debug.Log($"[MountainManager] Grid initialized: {_gridWidth}x{_gridHeight}, map='{_activeMapId}'" +
                      (_mountainMesh != null
                          ? $", mountain layer={_mountainMesh.layer} ({LayerMask.LayerToName(_mountainMesh.layer)}), dedicatedLayer={_hasDedicatedLayer}"
                          : ", NO mountain mesh assigned"));
        }

        /// <summary>
        /// Finds all MapRoot components in the scene, enables the one matching the
        /// requested map ID, and disables every other one. Falls back to the
        /// MapRegistry default (or LegacyMapId) when no explicit choice was made.
        /// </summary>
        private void ActivateSelectedMap()
        {
            string requestedId = GameLoadBootstrap.PendingMapId;
            GameLoadBootstrap.PendingMapId = null;

            if (_mapRegistry != null)
            {
                var mapDef = _mapRegistry.GetById(requestedId);
                _activeMapId = mapDef != null ? mapDef.mapId : MapRegistry.LegacyMapId;
            }
            else
            {
                _activeMapId = string.IsNullOrEmpty(requestedId) ? MapRegistry.LegacyMapId : requestedId;
            }

            // Find every MapRoot in the scene (including inactive ones)
            var allRoots = FindObjectsOfType<MapRoot>(true);
            MapRoot activeRoot = null;

            foreach (var root in allRoots)
            {
                if (root.mapId == _activeMapId)
                {
                    root.gameObject.SetActive(true);
                    activeRoot = root;
                }
                else
                {
                    root.gameObject.SetActive(false);
                }
            }

            if (activeRoot != null)
            {
                _mountainMesh = activeRoot.mountainMesh;
                ActiveMapRoot = activeRoot;
                Debug.Log($"[MountainManager] Activated MapRoot '{activeRoot.mapId}' ({activeRoot.gameObject.name})");
            }
            else if (allRoots.Length > 0)
            {
                Debug.LogWarning($"[MountainManager] No MapRoot found for '{_activeMapId}', falling back to scene-placed mountain mesh.");
            }
        }

        private void SetupMountainLayer()
        {
            if (_mountainMesh != null)
            {
                _mountainLayerMask = 1 << _mountainMesh.layer;
                _hasDedicatedLayer = _mountainMesh.layer != 0;
            }
        }
        
        /// <summary>
        /// Converts a tile coordinate to world position.
        /// Tile X maps to world X, Tile Y maps to world Z, height maps to world Y.
        /// </summary>
        public Vector3 TileToWorldPos(TileCoord coord)
        {
            float x = coord.X * _tileSize;
            float z = coord.Y * _tileSize;
            float y = 0f;
            
            if (_terrainData != null)
            {
                float height = _terrainData.GetHeight(coord);
                y = height * 0.1f;
            }
            
            float? meshHeight = GetHeightAtWorldPos(new Vector3(x, 0f, z));
            if (meshHeight.HasValue)
            {
                y = meshHeight.Value;
            }
            
            return new Vector3(x, y, z);
        }
        
        /// <summary>
        /// Raycasts onto the mountain mesh from a screen position (mouse).
        /// Returns the world position where the ray hits the mountain, or null if no hit.
        /// </summary>
        public Vector3? RaycastMountain(Camera camera, Vector3 screenPosition)
        {
            if (camera == null || _mountainMesh == null)
            {
                return null;
            }
            
            Ray ray = camera.ScreenPointToRay(screenPosition);

            if (_hasDedicatedLayer)
            {
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 10000f, _mountainLayerMask))
                    return hit.point;
                return null;
            }
            
            int hitCount = Physics.RaycastNonAlloc(ray, _mouseRayHits, 10000f);
            if (TryGetNearestMountainHit(_mouseRayHits, hitCount, out RaycastHit nearest))
                return nearest.point;
            
            return null;
        }
        
        /// <summary>
        /// Raycasts down from a position to find the mountain surface below.
        /// Returns the Y coordinate of the surface, or null if no hit.
        /// </summary>
        public float? GetHeightAtWorldPos(Vector3 worldPos)
        {
            if (_mountainMesh == null)
            {
                return null;
            }
            
            Ray ray = new Ray(new Vector3(worldPos.x, worldPos.y + 1000f, worldPos.z), Vector3.down);

            if (_mountainLayerMask > 0 && _mountainMesh.layer != 0)
            {
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 2000f, _mountainLayerMask))
                    return hit.point.y;
                return null;
            }
            
            int hitCount = Physics.RaycastNonAlloc(ray, _heightRayHits, 2000f);
            if (TryGetNearestMountainHit(_heightRayHits, hitCount, out RaycastHit nearest))
                return nearest.point.y;
            
            return null;
        }

        private bool TryGetNearestMountainHit(RaycastHit[] hits, int hitCount, out RaycastHit nearestHit)
        {
            nearestHit = default;
            bool found = false;
            float nearestDistance = float.MaxValue;
            Transform mountainTransform = _mountainMesh != null ? _mountainMesh.transform : null;
            if (mountainTransform == null || hitCount <= 0) return false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null) continue;
                Transform hitTransform = hit.collider.transform;
                if (hitTransform != mountainTransform && !hitTransform.IsChildOf(mountainTransform))
                    continue;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestHit = hit;
                    found = true;
                }
            }

            return found;
        }
        
        /// <summary>
        /// Converts a Core Vector3f to Unity Vector3.
        /// </summary>
        public static Vector3 ToUnityVector3(Vector3f v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }
        
        /// <summary>
        /// Converts a Unity Vector3 to Core Vector3f.
        /// </summary>
        public static Vector3f ToVector3f(Vector3 v)
        {
            return new Vector3f(v.x, v.y, v.z);
        }
    }
}
