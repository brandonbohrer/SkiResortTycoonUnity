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
        
        private Core.TerrainData _terrainData;
        
        public Core.TerrainData TerrainData => _terrainData;
        public float TileSize => _tileSize;
        
        void Awake()
        {
            // Create simple flat grid (heights will be determined by raycasting mountain later)
            _terrainData = new Core.TerrainData(_gridWidth, _gridHeight, seed: 0);
            
            Debug.Log($"[MountainManager] Grid initialized: {_gridWidth}x{_gridHeight}");
        }
        
        /// <summary>
        /// Converts a tile coordinate to world position.
        /// </summary>
        public Vector3 TileToWorldPos(TileCoord coord)
        {
            float x = coord.X * _tileSize;
            float y = coord.Y * _tileSize;
            
            // Get height from terrain data
            if (_terrainData != null)
            {
                float height = _terrainData.GetHeight(coord);
                y += height * 0.1f; // Height offset (adjust as needed)
            }
            
            return new Vector3(x, y, 0f);
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
            
            // Raycast against mountain - check the mountain itself or any of its children
            RaycastHit[] hits = Physics.RaycastAll(ray, 10000f);
            
            foreach (RaycastHit hit in hits)
            {
                // Accept hits on the mountain or its children (collider might be on a child)
                if (hit.collider.transform == _mountainMesh.transform || 
                    hit.collider.transform.IsChildOf(_mountainMesh.transform))
                {
                    return hit.point;
                }
            }
            
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
            
            // Raycast down from well above the position
            Ray ray = new Ray(new Vector3(worldPos.x, worldPos.y + 1000f, worldPos.z), Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, 2000f);
            
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform == _mountainMesh.transform || 
                    hit.collider.transform.IsChildOf(_mountainMesh.transform))
                {
                    return hit.point.y;
                }
            }
            
            return null;
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
