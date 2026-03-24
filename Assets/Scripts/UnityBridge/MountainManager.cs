using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Central manager for the handcrafted mountain and grid system.
    /// Provides terrain data to all other systems.
    /// </summary>
    public class MountainManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int _gridWidth = 64;
        [SerializeField] private int _gridHeight = 64;
        [SerializeField] private float _tileSize = 1f;
        
        [Header("Mountain Reference")]
        [SerializeField] private GameObject _mountainMesh; // Reference to your handcrafted mountain

        // Cached layer mask built from the mountain mesh's layer for fast single-hit raycasts
        private int _mountainLayerMask = -1;
        private bool _hasDedicatedLayer;
        private readonly RaycastHit[] _mouseRayHits = new RaycastHit[256];
        private readonly RaycastHit[] _heightRayHits = new RaycastHit[128];
        
        private Core.TerrainData _terrainData;
        
        public Core.TerrainData TerrainData => _terrainData;
        public float TileSize => _tileSize;
        
        void Awake()
        {
            _terrainData = new Core.TerrainData(_gridWidth, _gridHeight, seed: 0);
            
            if (_mountainMesh != null)
            {
                _mountainLayerMask = 1 << _mountainMesh.layer;
                _hasDedicatedLayer = _mountainMesh.layer != 0;
            }
            
            Debug.Log($"[MountainManager] Grid initialized: {_gridWidth}x{_gridHeight}" +
                      (_mountainMesh != null
                          ? $", mountain layer={_mountainMesh.layer} ({LayerMask.LayerToName(_mountainMesh.layer)}), dedicatedLayer={_hasDedicatedLayer}"
                          : ", NO mountain mesh assigned"));
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
            
            // Get height from terrain data (Y = up)
            if (_terrainData != null)
            {
                float height = _terrainData.GetHeight(coord);
                y = height * 0.1f;
            }
            
            // Try to get accurate height from mountain mesh raycast
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

            // Fast path: when the mountain has a dedicated layer, a single layer-masked
            // raycast avoids the buffer-overflow problem that occurs near the base where
            // many structure colliders share the ray path.
            if (_hasDedicatedLayer)
            {
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 10000f, _mountainLayerMask))
                    return hit.point;
                return null;
            }
            
            // Fallback: multi-hit raycast when mountain is on default layer
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

            // Fast path: single raycast with layer mask when the mountain is on
            // a dedicated layer (non-default). This is the hot path for per-frame
            // skier grounding (~50 raycasts/frame).
            if (_mountainLayerMask > 0 && _mountainMesh.layer != 0)
            {
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 2000f, _mountainLayerMask))
                    return hit.point.y;
                return null;
            }
            
            // Fallback: non-alloc multi-hit raycast when mountain is on default layer
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
