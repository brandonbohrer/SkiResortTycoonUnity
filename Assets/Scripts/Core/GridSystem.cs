namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# grid coordinate struct (value type).
    /// </summary>
    public struct TileCoord
    {
        public int X;
        public int Y;
        
        public TileCoord(int x, int y)
        {
            X = x;
            Y = y;
        }
        
        public override string ToString() => $"({X}, {Y})";
        
        public override bool Equals(object obj)
        {
            if (obj is TileCoord other)
            {
                return X == other.X && Y == other.Y;
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            return (X * 397) ^ Y;
        }
        
        public static bool operator ==(TileCoord a, TileCoord b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(TileCoord a, TileCoord b) => !(a == b);
    }
    
    /// <summary>
    /// Types of tiles in the world.
    /// </summary>
    public enum TileType
    {
        Empty = 0,
        Snow = 1,
        Rock = 2,
        Grass = 3,
        Ice = 4,
        Dirt = 5
    }
    
    /// <summary>
    /// Data for a single tile in the grid.
    /// </summary>
    public class TileData
    {
        public int Height { get; set; }
        public TileType Type { get; set; }
        public bool Buildable { get; set; }
        public bool Occupied { get; set; }
        
        public TileData()
        {
            Height = 0;
            Type = TileType.Snow;
            Buildable = true;
            Occupied = false;
        }
        
        public TileData(int height, TileType type, bool buildable = true)
        {
            Height = height;
            Type = type;
            Buildable = buildable;
            Occupied = false;
        }
    }
    
    /// <summary>
    /// Pure C# grid system for the 2.5D world.
    /// No Unity types allowed.
    /// </summary>
    public class GridSystem
    {
        private int _width;
        private int _height;
        private TileData[,] _tiles;
        
        public int Width => _width;
        public int Height => _height;
        
        public GridSystem(int width, int height)
        {
            _width = width;
            _height = height;
            _tiles = new TileData[width, height];
            
            // Initialize all tiles
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _tiles[x, y] = new TileData();
                }
            }
        }
        
        /// <summary>
        /// Checks if coordinates are within grid bounds.
        /// </summary>
        public bool InBounds(int x, int y)
        {
            return x >= 0 && x < _width && y >= 0 && y < _height;
        }
        
        /// <summary>
        /// Checks if coordinate is within grid bounds.
        /// </summary>
        public bool InBounds(TileCoord coord)
        {
            return InBounds(coord.X, coord.Y);
        }
        
        /// <summary>
        /// Gets tile data at coordinates. Returns null if out of bounds.
        /// </summary>
        public TileData GetTile(int x, int y)
        {
            if (!InBounds(x, y))
            {
                return null;
            }
            return _tiles[x, y];
        }
        
        /// <summary>
        /// Gets tile data at coordinate. Returns null if out of bounds.
        /// </summary>
        public TileData GetTile(TileCoord coord)
        {
            return GetTile(coord.X, coord.Y);
        }
        
        /// <summary>
        /// Sets tile data at coordinates.
        /// </summary>
        public void SetTile(int x, int y, TileData tile)
        {
            if (InBounds(x, y))
            {
                _tiles[x, y] = tile;
            }
        }
        
        /// <summary>
        /// Sets tile data at coordinate.
        /// </summary>
        public void SetTile(TileCoord coord, TileData tile)
        {
            SetTile(coord.X, coord.Y, tile);
        }
        
        /// <summary>
        /// Gets 4-way neighbors (North, South, East, West).
        /// Only returns valid in-bounds coordinates.
        /// </summary>
        public TileCoord[] GetNeighbors4(int x, int y)
        {
            var neighbors = new System.Collections.Generic.List<TileCoord>();
            
            // North
            if (InBounds(x, y + 1)) neighbors.Add(new TileCoord(x, y + 1));
            // South
            if (InBounds(x, y - 1)) neighbors.Add(new TileCoord(x, y - 1));
            // East
            if (InBounds(x + 1, y)) neighbors.Add(new TileCoord(x + 1, y));
            // West
            if (InBounds(x - 1, y)) neighbors.Add(new TileCoord(x - 1, y));
            
            return neighbors.ToArray();
        }
        
        /// <summary>
        /// Gets 4-way neighbors.
        /// </summary>
        public TileCoord[] GetNeighbors4(TileCoord coord)
        {
            return GetNeighbors4(coord.X, coord.Y);
        }
        
        /// <summary>
        /// Gets 8-way neighbors (including diagonals).
        /// Only returns valid in-bounds coordinates.
        /// </summary>
        public TileCoord[] GetNeighbors8(int x, int y)
        {
            var neighbors = new System.Collections.Generic.List<TileCoord>();
            
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue; // Skip self
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (InBounds(nx, ny))
                    {
                        neighbors.Add(new TileCoord(nx, ny));
                    }
                }
            }
            
            return neighbors.ToArray();
        }
        
        /// <summary>
        /// Gets 8-way neighbors (including diagonals).
        /// </summary>
        public TileCoord[] GetNeighbors8(TileCoord coord)
        {
            return GetNeighbors8(coord.X, coord.Y);
        }
    }
}

