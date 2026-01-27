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
        
        [Header("Terrain Generation")]
        [SerializeField] private bool _generateTerrain = true;
        [SerializeField] private MountainArchetype _archetype = MountainArchetype.SinglePeak;
        
        [Header("Visual Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private float _heightScale = 0.1f; // Visual offset per height unit
        [SerializeField] private bool _showGridLines = false;
        [SerializeField] private Color _gridColor = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private bool _showHeightColors = true;
        [SerializeField] private bool _showSlopeShading = true;
        [SerializeField] private float _slopeShadingStrength = 0.3f;
        [SerializeField] private bool _highlightBaseArea = false;
        
        [Header("Height Colors (Smooth Gradient)")]
        [SerializeField] private Color _colorLow = new Color(0.35f, 0.55f, 0.35f); // Green (low/base)
        [SerializeField] private Color _colorMid = new Color(0.75f, 0.85f, 0.95f); // Light blue (mid)
        [SerializeField] private Color _colorHigh = new Color(0.98f, 0.98f, 1f); // White (high/peak)
        [SerializeField] private int _maxHeight = 20; // Should match terrain generation
        
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
            _terrainData = new Core.TerrainData(_gridWidth, _gridHeight, _seed, _generateTerrain, _archetype);
            
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
            sr.color = GetTileColor(x, y, tile.Height);
            
            // Scale to tile size (no gaps for smooth appearance)
            tileObj.transform.localScale = new Vector3(_tileSize, _tileSize, 1f);
            
            // Sorting order based on Y position
            sr.sortingOrder = -(y * 1000 + x);
        }
        
        private Color GetHeightColor(int height)
        {
            if (!_showHeightColors)
            {
                return Color.white;
            }
            
            // Smooth gradient interpolation
            float t = Mathf.Clamp01(height / (float)_maxHeight);
            
            Color baseColor;
            if (t < 0.3f)
            {
                // Low to mid (base area to lower slopes)
                float localT = t / 0.3f;
                baseColor = Color.Lerp(_colorLow, _colorMid, SmoothStep(localT));
            }
            else if (t < 0.7f)
            {
                // Mid to high (slopes to peaks)
                float localT = (t - 0.3f) / 0.4f;
                baseColor = Color.Lerp(_colorMid, _colorHigh, SmoothStep(localT));
            }
            else
            {
                // High peaks with subtle variation
                float localT = (t - 0.7f) / 0.3f;
                baseColor = Color.Lerp(_colorHigh, Color.white, SmoothStep(localT) * 0.5f);
            }
            
            return baseColor;
        }
        
        private float SmoothStep(float t)
        {
            // Smooth interpolation curve (ease in-out)
            return t * t * (3f - 2f * t);
        }
        
        private Color GetTileColor(int x, int y, int height)
        {
            Color baseColor = GetHeightColor(height);
            
            // Apply slope shading (darker in valleys, lighter on ridges)
            if (_showSlopeShading && _terrainData != null)
            {
                float slopeFactor = CalculateSlopeFactor(x, y);
                
                // Darken valleys, lighten ridges
                float shadingMultiplier = 1f + (slopeFactor * _slopeShadingStrength);
                baseColor *= shadingMultiplier;
            }
            
            // Highlight base area with a slight yellow tint
            if (_highlightBaseArea && _terrainData != null)
            {
                int baseAreaRows = (int)(_terrainData.Grid.Height * 0.25f);
                if (y < baseAreaRows)
                {
                    // Tint base area slightly yellow
                    baseColor = Color.Lerp(baseColor, new Color(1f, 1f, 0.7f), 0.3f);
                }
            }
            
            return baseColor;
        }
        
        private float CalculateSlopeFactor(int x, int y)
        {
            if (_terrainData == null) return 0f;
            
            int centerHeight = _terrainData.GetHeight(x, y);
            
            // Calculate average neighbor height
            float avgNeighborHeight = 0f;
            int count = 0;
            
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (_terrainData.Grid.InBounds(nx, ny))
                    {
                        avgNeighborHeight += _terrainData.GetHeight(nx, ny);
                        count++;
                    }
                }
            }
            
            if (count == 0) return 0f;
            
            avgNeighborHeight /= count;
            
            // Positive = ridge (lighter), negative = valley (darker)
            float difference = (centerHeight - avgNeighborHeight) / (float)_maxHeight;
            
            return Mathf.Clamp(difference, -0.5f, 0.5f);
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
            if (!_showGridLines || _terrainData == null) return;
            
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
                        sr.color = GetTileColor(x, y, tile.Height);
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

