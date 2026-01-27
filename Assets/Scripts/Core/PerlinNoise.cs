using System;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# Perlin noise implementation.
    /// Deterministic based on seed.
    /// No Unity dependencies.
    /// </summary>
    public class PerlinNoise
    {
        private int[] _permutation;
        private const int PermutationSize = 256;
        
        public PerlinNoise(int seed)
        {
            InitializePermutation(seed);
        }
        
        private void InitializePermutation(int seed)
        {
            Random random = new Random(seed);
            _permutation = new int[PermutationSize * 2];
            
            // Fill with 0-255
            int[] p = new int[PermutationSize];
            for (int i = 0; i < PermutationSize; i++)
            {
                p[i] = i;
            }
            
            // Shuffle using Fisher-Yates
            for (int i = PermutationSize - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                int temp = p[i];
                p[i] = p[j];
                p[j] = temp;
            }
            
            // Duplicate for wrapping
            for (int i = 0; i < PermutationSize * 2; i++)
            {
                _permutation[i] = p[i % PermutationSize];
            }
        }
        
        /// <summary>
        /// Gets Perlin noise value at (x, y).
        /// Returns value between -1 and 1.
        /// </summary>
        public float GetValue(float x, float y)
        {
            // Find unit grid cell
            int xi = (int)Math.Floor(x) & 255;
            int yi = (int)Math.Floor(y) & 255;
            
            // Relative position in cell
            float xf = x - (float)Math.Floor(x);
            float yf = y - (float)Math.Floor(y);
            
            // Fade curves
            float u = Fade(xf);
            float v = Fade(yf);
            
            // Hash coordinates of 4 corners
            int aa = _permutation[_permutation[xi] + yi];
            int ab = _permutation[_permutation[xi] + yi + 1];
            int ba = _permutation[_permutation[xi + 1] + yi];
            int bb = _permutation[_permutation[xi + 1] + yi + 1];
            
            // Blend results from 4 corners
            float x1 = Lerp(Gradient(aa, xf, yf), Gradient(ba, xf - 1, yf), u);
            float x2 = Lerp(Gradient(ab, xf, yf - 1), Gradient(bb, xf - 1, yf - 1), u);
            
            return Lerp(x1, x2, v);
        }
        
        /// <summary>
        /// Gets layered Perlin noise with multiple octaves.
        /// Returns value between 0 and 1.
        /// </summary>
        public float GetOctaveValue(float x, float y, int octaves, float persistence, float lacunarity)
        {
            float total = 0f;
            float frequency = 1f;
            float amplitude = 1f;
            float maxValue = 0f;
            
            for (int i = 0; i < octaves; i++)
            {
                total += GetValue(x * frequency, y * frequency) * amplitude;
                
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            
            // Normalize to 0-1
            return (total / maxValue + 1f) * 0.5f;
        }
        
        private float Fade(float t)
        {
            // 6t^5 - 15t^4 + 10t^3
            return t * t * t * (t * (t * 6 - 15) + 10);
        }
        
        private float Lerp(float a, float b, float t)
        {
            return a + t * (b - a);
        }
        
        private float Gradient(int hash, float x, float y)
        {
            // Take the hashed value and use it to determine gradient direction
            int h = hash & 3;
            float u = h < 2 ? x : y;
            float v = h < 2 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}

