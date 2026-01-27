using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Debug renderer for visualizing the grid in play mode.
    /// Draws colored quads based on tile height.
    /// </summary>
    public class GridDebugRenderer : MonoBehaviour
    {
        [Header("Grid Reference")]
        [SerializeField] private bool _autoGenerate = true;
        [SerializeField] private int _gridWidth = 64;
        [SerializeField] private int _gridHeight = 64;
        [SerializeField] private int _seed = 12345;
        
        [Header("Visual Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private float _heightScale = 0.1f; // Visual offset per height unit
        [SerializeField] private bool _showGrid = true;
        [SerializeField] private Color _gridColor = new Color(1f, 1f, 1f, 0.2f);
        
        [Header("Height Colors")]
        [SerializeField] private Color _colorLow = new Color(0.2f, 0.4f, 0.8f); // Blue (low)
        [SerializeField] private Color _colorMid = new Color(0.9f, 0.9f, 0.9f); // White (mid)
        [SerializeField] private Color _colorHigh = new Color(0.6f, 0.3f, 0.1f); // Brown (high)
        
        private Core.TerrainData _terrainData;
        private GameObject _tilesContainer;
        
        public Core.TerrainData TerrainData => _terrainData;
        
        void Start()
        {
            if (_autoGenerate)
            {
                GenerateGrid();
            }
        }
        
        public void GenerateGrid()
        {
            // Create terrain data
            _terrainData = new Core.TerrainData(_gridWidth, _gridHeight, _seed);
            
            // Clean up old tiles
            if (_tilesContainer != null)
            {
                Destroy(_tilesContainer);
            }
            
            // Create container
            _tilesContainer = new GameObject("Tiles");
            _tilesContainer.transform.SetParent(transform);
            
            // Generate visual tiles
            for (int x = 0; x < _terrainData.Grid.Width; x++)
            {
                for (int y = 0; y < _terrainData.Grid.Height; y++)
                {
                    CreateTileSprite(x, y);
                }
            }
            
            Debug.Log($"Grid generated: {_gridWidth}x{_gridHeight} with seed {_seed}");
        }
        
        private void CreateTileSprite(int x, int y)
        {
            TileData tile = _terrainData.Grid.GetTile(x, y);
            if (tile == null) return;
            
            // Create game object
            GameObject tileObj = new GameObject($"Tile_{x}_{y}");
            tileObj.transform.SetParent(_tilesContainer.transform);
            
            // Position based on grid coordinates + height offset
            float worldX = x * _tileSize;
            float worldY = y * _tileSize + (tile.Height * _heightScale);
            tileObj.transform.position = new Vector3(worldX, worldY, 0f);
            
            // Add sprite renderer
            SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSquareSprite();
            sr.color = GetHeightColor(tile.Height);
            
            // Scale to tile size
            tileObj.transform.localScale = new Vector3(_tileSize * 0.95f, _tileSize * 0.95f, 1f);
            
            // Sorting order based on Y position
            sr.sortingOrder = -(y * 1000 + x);
        }
        
        private Color GetHeightColor(int height)
        {
            // For now, all heights are 0, so we'll just return mid color
            // Later when we have varied heights, this will interpolate
            if (height <= 0)
                return _colorLow;
            else if (height >= 10)
                return _colorHigh;
            else
            {
                float t = height / 10f;
                return Color.Lerp(_colorMid, _colorHigh, t);
            }
        }
        
        private Sprite CreateSquareSprite()
        {
            // Create a simple white square texture
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            
            return Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f
            );
        }
        
        void OnDrawGizmos()
        {
            if (!_showGrid || _terrainData == null) return;
            
            Gizmos.color = _gridColor;
            
            // Draw grid lines
            for (int x = 0; x <= _terrainData.Grid.Width; x++)
            {
                Vector3 start = new Vector3(x * _tileSize, 0, 0);
                Vector3 end = new Vector3(x * _tileSize, _terrainData.Grid.Height * _tileSize, 0);
                Gizmos.DrawLine(start, end);
            }
            
            for (int y = 0; y <= _terrainData.Grid.Height; y++)
            {
                Vector3 start = new Vector3(0, y * _tileSize, 0);
                Vector3 end = new Vector3(_terrainData.Grid.Width * _tileSize, y * _tileSize, 0);
                Gizmos.DrawLine(start, end);
            }
        }
        
        /// <summary>
        /// Updates a single tile's visual based on current data.
        /// </summary>
        public void RefreshTile(int x, int y)
        {
            if (_terrainData == null || _tilesContainer == null) return;
            
            // Find and update the tile
            Transform tileTransform = _tilesContainer.transform.Find($"Tile_{x}_{y}");
            if (tileTransform != null)
            {
                TileData tile = _terrainData.Grid.GetTile(x, y);
                if (tile != null)
                {
                    // Update position with height
                    float worldX = x * _tileSize;
                    float worldY = y * _tileSize + (tile.Height * _heightScale);
                    tileTransform.position = new Vector3(worldX, worldY, 0f);
                    
                    // Update color
                    SpriteRenderer sr = tileTransform.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = GetHeightColor(tile.Height);
                    }
                }
            }
        }
        
        /// <summary>
        /// Refreshes all tile visuals.
        /// </summary>
        public void RefreshAll()
        {
            if (_terrainData == null) return;
            
            for (int x = 0; x < _terrainData.Grid.Width; x++)
            {
                for (int y = 0; y < _terrainData.Grid.Height; y++)
                {
                    RefreshTile(x, y);
                }
            }
        }
    }
}

