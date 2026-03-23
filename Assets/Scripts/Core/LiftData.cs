using System;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Supported lift variants.
    /// </summary>
    public enum LiftType
    {
        OneSeatLowSpeed = 0,
        OneSeatHighSpeed = 1,
        TwoSeatLowSpeed = 2,
        TwoSeatHighSpeed = 3
    }

    public static class LiftTypeSpecs
    {
        public static int GetCapacityPerHour(LiftType type)
        {
            switch (type)
            {
                case LiftType.OneSeatLowSpeed:  return 900;
                case LiftType.OneSeatHighSpeed: return 1300;
                case LiftType.TwoSeatLowSpeed:  return 1800;
                case LiftType.TwoSeatHighSpeed: return 2400;
                default:                        return 900;
            }
        }

        public static string GetDisplayName(LiftType type)
        {
            switch (type)
            {
                case LiftType.OneSeatLowSpeed:  return "1-Seat Low Speed";
                case LiftType.OneSeatHighSpeed: return "1-Seat High Speed";
                case LiftType.TwoSeatLowSpeed:  return "2-Seat Low Speed";
                case LiftType.TwoSeatHighSpeed: return "2-Seat High Speed";
                default:                        return "Lift";
            }
        }

        public static bool IsImplemented(LiftType type)
        {
            return type == LiftType.OneSeatLowSpeed || type == LiftType.OneSeatHighSpeed;
        }

        /// <summary>Next tier in the upgrade chain, or null if already max.</summary>
        public static LiftType? GetNextUpgrade(LiftType current)
        {
            if (current == LiftType.TwoSeatHighSpeed) return null;
            return (LiftType)((int)current + 1);
        }

        /// <summary>
        /// Cash cost for upgrading <paramref name="from"/> to the next tier (same lift geometry).
        /// Scales slightly with lift length and vertical.
        /// </summary>
        public static int GetUpgradeCostToNext(LiftType from, LiftData lift, LiftSystem liftSystem)
        {
            if (lift == null || liftSystem == null) return 10_000;
            if (!GetNextUpgrade(from).HasValue) return 0;

            int variablePortion = (int)(lift.Length * liftSystem.CostPerTile
                + lift.ElevationGain * liftSystem.CostPerHeightUnit);
            int step = (int)from;
            int baseStep = 8_000 + step * 5_000;
            return Math.Max(5_000, baseStep + variablePortion / 5);
        }
    }
    
    /// <summary>
    /// Pure C# representation of a ski lift.
    /// No Unity types.
    /// </summary>
    public class LiftData
    {
        public int LiftId { get; set; }
        public string Name { get; set; }
        
        // World-space positions (authoritative for rendering and gameplay)
        public Vector3f StartPosition { get; set; } // Bottom station position on mountain mesh
        public Vector3f EndPosition { get; set; }   // Top station position on mountain mesh
        
        // Legacy grid coordinates (kept for backwards compatibility with existing systems)
        public TileCoord BottomStation { get; set; }
        public TileCoord TopStation { get; set; }
        
        public LiftType Type { get; set; }
        public float Length { get; set; } // 3D distance between start and end
        public float ElevationGain { get; set; } // Height difference (EndPosition.Y - StartPosition.Y)
        public int Capacity { get; set; } // Riders per hour
        public int BuildCost { get; set; }
        public bool IsValid { get; set; }
        
        public LiftData(int liftId)
        {
            LiftId = liftId;
            Name = $"Lift {liftId}";
            Type = LiftType.OneSeatLowSpeed;
            Capacity = LiftTypeSpecs.GetCapacityPerHour(Type);
            IsValid = false;
        }
    }
}

