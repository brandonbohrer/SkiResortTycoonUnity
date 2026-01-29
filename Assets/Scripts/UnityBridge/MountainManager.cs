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
        /// Gets the height at a world position by raycasting the mountain mesh.
        /// TODO: Implement raycasting in the future.
        /// </summary>
        public float GetHeightAtWorldPos(Vector3 worldPos)
        {
            // For now, return 0 (flat)
            // Later: raycast down from worldPos onto _mountainMesh
            return 0f;
        }
    }
}

