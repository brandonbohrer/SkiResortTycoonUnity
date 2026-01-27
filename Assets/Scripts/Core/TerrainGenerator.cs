using System;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Mountain archetype styles.
    /// </summary>
    public enum MountainArchetype
    {
        SinglePeak,
        Ridgeline
    }
    
    /// <summary>
    /// Settings for terrain generation.
    /// </summary>
    public class TerrainGenSettings
    {
        // Grid dimensions
        public int Width { get; set; } = 64;
        public int Height { get; set; } = 64;
        
        // Base area (flat region at bottom for village)
        public int BaseAreaHeight { get; set; } = 0; // Height of base area
        public float BaseAreaDepth { get; set; } = 0.25f; // % of map that is base (0-1)
        
        // Height range
        public int MaxHeight { get; set; } = 20;
        
        // Noise parameters
        public float NoiseScale { get; set; } = 0.05f; // Smaller = smoother, larger = more chaotic
        public int Octaves { get; set; } = 3;
        public float Persistence { get; set; } = 0.5f;
        public float Lacunarity { get; set; } = 2.0f;
        
        // Post-processing
        public int SmoothingPasses { get; set; } = 2;
        
        // Archetype-specific
        public float SlopeExponent { get; set; } = 1.2f; // Controls how steep the vertical gradient is
        public float DomainWarpStrength { get; set; } = 15f; // Strength of domain warping for natural features
        public float PeakRadiusX { get; set; } = 0.3f; // Horizontal peak spread (elliptical)
        public float PeakRadiusY { get; set; } = 0.2f; // Vertical peak spread (elliptical)
    }
    
    /// <summary>
    /// Pure C# terrain generator.
    /// Creates believable ski mountain heightmaps with archetypes.
    /// </summary>
    public static class TerrainGenerator
    {
        /// <summary>
        /// Generates terrain on the provided grid.
        /// Deterministic based on seed.
        /// </summary>
        public static void Generate(GridSystem grid, int seed, MountainArchetype archetype, TerrainGenSettings settings)
        {
            PerlinNoise noise = new PerlinNoise(seed);
            
            // Generate raw heightmap
            float[,] heightmap = new float[grid.Width, grid.Height];
            
            switch (archetype)
            {
                case MountainArchetype.SinglePeak:
                    GenerateSinglePeak(heightmap, grid.Width, grid.Height, noise, settings);
                    break;
                case MountainArchetype.Ridgeline:
                    GenerateRidgeline(heightmap, grid.Width, grid.Height, noise, settings);
                    break;
            }
            
            // Apply smoothing
            for (int i = 0; i < settings.SmoothingPasses; i++)
            {
                SmoothHeightmap(heightmap, grid.Width, grid.Height);
            }
            
            // Apply to grid
            ApplyHeightmapToGrid(heightmap, grid, settings.MaxHeight);
        }
        
        private static void GenerateSinglePeak(float[,] heightmap, int width, int height, PerlinNoise noise, TerrainGenSettings settings)
        {
            Random rnd = new Random(noise.GetHashCode());
            float baseAreaRows = height * settings.BaseAreaDepth;
            
            // Generate 2-4 ridge spines flowing from top to bottom
            int numRidges = 3;
            float[] ridgeStartX = new float[numRidges];
            
            for (int i = 0; i < numRidges; i++)
            {
                ridgeStartX[i] = width * (0.2f + i * 0.3f + (float)rnd.NextDouble() * 0.1f);
            }
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // === 1. BASE AREA (flat at bottom) ===
                    if (y < baseAreaRows)
                    {
                        float tinyNoise = noise.GetValue(x * 0.05f, y * 0.05f) * 0.5f + 0.5f;
                        heightmap[x, y] = 0.03f * tinyNoise;
                        continue;
                    }
                    
                    // === 2. VERTICAL GRADIENT (bottom to top) ===
                    float normalizedY = (y - baseAreaRows) / (height - baseAreaRows);
                    float verticalGradient = (float)Math.Pow(normalizedY, settings.SlopeExponent);
                    
                    // === 3. RIDGE FIELD (multiple flowing ridges) ===
                    float ridgeField = 0f;
                    
                    for (int r = 0; r < numRidges; r++)
                    {
                        // Ridge flows from top to bottom with some wandering
                        float ridgeWander = noise.GetOctaveValue(y * 0.02f + r * 100f, 0f, 2, 0.5f, 2f) * width * 0.15f;
                        float ridgeCenterX = ridgeStartX[r] + ridgeWander * (1f - normalizedY);
                        
                        // Distance from this ridge spine
                        float distFromRidge = Math.Abs(x - ridgeCenterX) / (width * 0.2f);
                        float ridgeMask = 1f - Math.Min(distFromRidge, 1f);
                        ridgeMask = (float)Math.Pow(ridgeMask, 1.5f);
                        
                        // Ridge strength increases toward the top
                        ridgeMask *= 0.3f + normalizedY * 0.7f;
                        
                        ridgeField = Math.Max(ridgeField, ridgeMask);
                    }
                    
                    // === 4. VALLEY CARVING (spaces between ridges) ===
                    // Lower areas between ridges
                    float valleyFactor = 1f - ridgeField * 0.4f;
                    
                    // === 5. DOMAIN WARPING (natural features) ===
                    float warpX = noise.GetOctaveValue(x * 0.015f, y * 0.015f, 2, 0.5f, 2f) * settings.DomainWarpStrength;
                    float warpY = noise.GetOctaveValue(x * 0.015f + 100f, y * 0.015f + 100f, 2, 0.5f, 2f) * settings.DomainWarpStrength;
                    
                    // === 6. DETAIL NOISE ===
                    float detailNoise = noise.GetOctaveValue(
                        (x + warpX) * settings.NoiseScale,
                        (y + warpY) * settings.NoiseScale,
                        settings.Octaves,
                        settings.Persistence,
                        settings.Lacunarity
                    );
                    
                    // === 7. COMBINE ALL ===
                    // Base shape: gradient + ridges
                    float baseShape = verticalGradient * (0.5f + ridgeField * 0.5f);
                    
                    // Apply valley carving
                    baseShape *= valleyFactor;
                    
                    // Add detail noise
                    float finalHeight = baseShape * (0.8f + detailNoise * 0.2f);
                    
                    heightmap[x, y] = Math.Max(0f, Math.Min(1f, finalHeight));
                }
            }
        }
        
        private static void GenerateRidgeline(float[,] heightmap, int width, int height, PerlinNoise noise, TerrainGenSettings settings)
        {
            float baseAreaRows = height * settings.BaseAreaDepth;
            float ridgeBaseY = height * 0.7f; // Ridge is in upper third
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // === 1. VERTICAL GRADIENT (bottom to top) ===
                    float normalizedY = Math.Max(0f, Math.Min(1f, (y - baseAreaRows) / (height - baseAreaRows)));
                    float verticalGradient = (float)Math.Pow(normalizedY, settings.SlopeExponent);
                    
                    // === 2. BASE AREA MASK (flat at bottom) ===
                    if (y < baseAreaRows)
                    {
                        heightmap[x, y] = 0.05f * (0.8f + noise.GetValue(x * 0.1f, y * 0.1f) * 0.2f);
                        continue;
                    }
                    
                    // === 3. WIGGLING RIDGELINE ===
                    // Ridge center varies per X using low-frequency noise
                    float ridgeOffset = noise.GetOctaveValue(x * 0.03f, 0f, 2, 0.5f, 2f) * height * 0.15f;
                    float ridgeCenterY = ridgeBaseY + ridgeOffset;
                    
                    // === 4. DOMAIN WARPING ===
                    float warpX = noise.GetOctaveValue(x * 0.02f, y * 0.02f, 2, 0.5f, 2f) * settings.DomainWarpStrength;
                    float warpY = noise.GetOctaveValue(x * 0.02f + 100f, y * 0.02f + 100f, 2, 0.5f, 2f) * settings.DomainWarpStrength;
                    
                    float warpedX = x + warpX;
                    float warpedY = y + warpY;
                    
                    // === 5. MAIN TERRAIN NOISE ===
                    float noiseValue = noise.GetOctaveValue(
                        warpedX * settings.NoiseScale,
                        warpedY * settings.NoiseScale,
                        settings.Octaves,
                        settings.Persistence,
                        settings.Lacunarity
                    );
                    
                    // === 6. RIDGE MASK (distance from wiggling ridgeline) ===
                    float distFromRidge = Math.Abs(y - ridgeCenterY) / (height * 0.25f);
                    float ridgeMask = 1f - Math.Min(distFromRidge, 1f);
                    ridgeMask = (float)Math.Pow(ridgeMask, 1.2f);
                    
                    // === 7. COMBINE ===
                    float mountainShape = verticalGradient * (0.5f + ridgeMask * 0.5f);
                    float finalHeight = mountainShape * (0.75f + noiseValue * 0.25f);
                    
                    heightmap[x, y] = Math.Max(0f, Math.Min(1f, finalHeight));
                }
            }
        }
        
        private static void SmoothHeightmap(float[,] heightmap, int width, int height)
        {
            float[,] smoothed = new float[width, height];
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float sum = 0f;
                    int count = 0;
                    
                    // Average with neighbors (3x3 kernel)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                sum += heightmap[nx, ny];
                                count++;
                            }
                        }
                    }
                    
                    smoothed[x, y] = sum / count;
                }
            }
            
            // Copy back
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    heightmap[x, y] = smoothed[x, y];
                }
            }
        }
        
        private static void ApplyHeightmapToGrid(float[,] heightmap, GridSystem grid, int maxHeight)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    TileData tile = grid.GetTile(x, y);
                    if (tile != null)
                    {
                        // Convert 0-1 heightmap to integer height
                        tile.Height = (int)(heightmap[x, y] * maxHeight);
                        
                        // Set tile type based on height
                        if (tile.Height <= 2)
                            tile.Type = TileType.Grass; // Base area
                        else if (tile.Height <= 12)
                            tile.Type = TileType.Snow; // Mid mountain
                        else
                            tile.Type = TileType.Rock; // Peak
                    }
                }
            }
        }
    }
}

