namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# 3D vector struct (no Unity dependencies).
    /// Used for world-space positions in the core game logic.
    /// </summary>
    public struct Vector3f
    {
        public float X;
        public float Y;
        public float Z;
        
        public Vector3f(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        
        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
        
        public override bool Equals(object obj)
        {
            if (obj is Vector3f other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X.GetHashCode();
                hash = hash * 31 + Y.GetHashCode();
                hash = hash * 31 + Z.GetHashCode();
                return hash;
            }
        }
        
        public static bool operator ==(Vector3f a, Vector3f b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        public static bool operator !=(Vector3f a, Vector3f b) => !(a == b);
        
        /// <summary>
        /// Returns the distance between two points.
        /// </summary>
        public static float Distance(Vector3f a, Vector3f b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        
        /// <summary>
        /// Returns the squared distance (faster than Distance, no sqrt).
        /// </summary>
        public static float DistanceSquared(Vector3f a, Vector3f b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }
        
        public static readonly Vector3f Zero = new Vector3f(0, 0, 0);
    }
}
