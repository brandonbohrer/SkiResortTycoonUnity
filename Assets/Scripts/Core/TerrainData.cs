namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# terrain data container.
    /// Stores heightmap and tile types for the world.
    /// No generation logic yet - just data storage.
    /// </summary>
    public class TerrainData
    {
        private GridSystem _grid;
        private int _seed;
        
        public GridSystem Grid => _grid;
        public int Seed => _seed;
        
        /// <summary>
        /// Creates terrain data with the specified dimensions.
        /// </summary>
        public TerrainData(int width, int height, int seed = 0)
        {
            _grid = new GridSystem(width, height);
            _seed = seed;
            
            // Initialize with default flat terrain
            InitializeFlat();
        }
        
        /// <summary>
        /// Initializes all tiles to flat (height 0) snow terrain.
        /// </summary>
        private void InitializeFlat()
        {
            for (int x = 0; x < _grid.Width; x++)
            {
                for (int y = 0; y < _grid.Height; y++)
                {
                    var tile = _grid.GetTile(x, y);
                    tile.Height = 0;
                    tile.Type = TileType.Snow;
                    tile.Buildable = true;
                    tile.Occupied = false;
                }
            }
        }
        
        /// <summary>
        /// Gets the height at a specific coordinate.
        /// Returns 0 if out of bounds.
        /// </summary>
        public int GetHeight(int x, int y)
        {
            var tile = _grid.GetTile(x, y);
            return tile != null ? tile.Height : 0;
        }
        
        /// <summary>
        /// Gets the height at a specific coordinate.
        /// </summary>
        public int GetHeight(TileCoord coord)
        {
            return GetHeight(coord.X, coord.Y);
        }
        
        /// <summary>
        /// Sets the height at a specific coordinate.
        /// </summary>
        public void SetHeight(int x, int y, int height)
        {
            var tile = _grid.GetTile(x, y);
            if (tile != null)
            {
                tile.Height = height;
            }
        }
        
        /// <summary>
        /// Sets the height at a specific coordinate.
        /// </summary>
        public void SetHeight(TileCoord coord, int height)
        {
            SetHeight(coord.X, coord.Y, height);
        }
        
        /// <summary>
        /// Gets the tile type at a specific coordinate.
        /// Returns TileType.Empty if out of bounds.
        /// </summary>
        public TileType GetTileType(int x, int y)
        {
            var tile = _grid.GetTile(x, y);
            return tile != null ? tile.Type : TileType.Empty;
        }
        
        /// <summary>
        /// Sets the tile type at a specific coordinate.
        /// </summary>
        public void SetTileType(int x, int y, TileType type)
        {
            var tile = _grid.GetTile(x, y);
            if (tile != null)
            {
                tile.Type = type;
            }
        }
    }
}

