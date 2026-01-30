using System;
using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# lift management system.
    /// Handles lift placement, validation, and cost calculation.
    /// </summary>
    public class LiftSystem
    {
        private List<LiftData> _lifts;
        private int _nextLiftId = 1;
        private TerrainData _terrain;
        private SnapRegistry _snapRegistry;
        
        // Configurable costs and constraints
        public int BaseCost { get; set; } = 5000;           // Base cost per lift
        public int CostPerTile { get; set; } = 100;         // Additional cost per tile length
        public int CostPerHeightUnit { get; set; } = 200;   // Additional cost per height unit
        public int MaxLength { get; set; } = 50;            // Max horizontal distance
        public int MinElevationGain { get; set; } = 2;      // Minimum height gain required
        
        public List<LiftData> Lifts => _lifts;
        
        public LiftSystem(TerrainData terrain, SnapRegistry snapRegistry)
        {
            _terrain = terrain;
            _snapRegistry = snapRegistry;
            _lifts = new List<LiftData>();
        }
        
        /// <summary>
        /// Creates a new lift (not yet placed).
        /// </summary>
        public LiftData CreateLift()
        {
            LiftData lift = new LiftData(_nextLiftId++);
            return lift;
        }
        
        /// <summary>
        /// Validates a lift placement.
        /// </summary>
        public bool ValidateLift(LiftData lift, out string errorMessage)
        {
            errorMessage = "";
            
            // NEW: If we have world positions, use those for validation (skip grid checks)
            bool hasWorldPositions = lift.StartPosition.X != 0 || lift.StartPosition.Y != 0 || lift.StartPosition.Z != 0;
            
            if (hasWorldPositions)
            {
                // Validate using world-space positions
                // Elevation gain should already be calculated and validated by LiftBuilder
                if (lift.ElevationGain <= 0)
                {
                    errorMessage = $"Lift must go uphill (elevation gain: {lift.ElevationGain:F1}m)";
                    return false;
                }
                
                // Check reasonable length (using world units, not tiles)
                if (lift.Length > MaxLength * 10) // Scale up for world units (rough conversion)
                {
                    errorMessage = $"Lift too long ({lift.Length:F1}m)";
                    return false;
                }
                
                lift.IsValid = true;
                return true;
            }
            
            // LEGACY: Old tile-based validation (for backwards compatibility)
            // Check if coordinates are in bounds
            if (!_terrain.Grid.InBounds(lift.BottomStation) || !_terrain.Grid.InBounds(lift.TopStation))
            {
                errorMessage = "Lift stations out of bounds";
                return false;
            }
            
            // Check if tiles are buildable
            var bottomTile = _terrain.Grid.GetTile(lift.BottomStation);
            var topTile = _terrain.Grid.GetTile(lift.TopStation);
            
            if (!bottomTile.Buildable || !topTile.Buildable)
            {
                errorMessage = "One or both stations are not buildable";
                return false;
            }
            
            if (bottomTile.Occupied || topTile.Occupied)
            {
                errorMessage = "One or both stations are already occupied";
                return false;
            }
            
            // Calculate stats
            int bottomHeight = _terrain.GetHeight(lift.BottomStation);
            int topHeight = _terrain.GetHeight(lift.TopStation);
            int elevationGain = topHeight - bottomHeight;
            
            // Check elevation gain (top must be higher than bottom)
            if (elevationGain < MinElevationGain)
            {
                errorMessage = $"Insufficient elevation gain ({elevationGain}). Minimum: {MinElevationGain}";
                return false;
            }
            
            // Calculate length
            int dx = lift.TopStation.X - lift.BottomStation.X;
            int dy = lift.TopStation.Y - lift.BottomStation.Y;
            int length = (int)Math.Sqrt(dx * dx + dy * dy);
            
            // Check max length
            if (length > MaxLength)
            {
                errorMessage = $"Lift too long ({length} tiles). Maximum: {MaxLength}";
                return false;
            }
            
            // Store calculated values
            lift.Length = length;
            lift.ElevationGain = elevationGain;
            lift.IsValid = true;
            
            return true;
        }
        
        /// <summary>
        /// Calculates the cost to build a lift.
        /// </summary>
        public int CalculateCost(LiftData lift)
        {
            int cost = BaseCost;
            cost += (int)lift.Length * CostPerTile;
            cost += (int)lift.ElevationGain * CostPerHeightUnit;
            
            lift.BuildCost = cost;
            return cost;
        }
        
        /// <summary>
        /// Attempts to place a lift (validates and checks money).
        /// </summary>
        public bool TryPlaceLift(LiftData lift, SimulationState state, out string errorMessage)
        {
            // Validate placement
            if (!ValidateLift(lift, out errorMessage))
            {
                return false;
            }
            
            // Calculate cost
            int cost = CalculateCost(lift);
            
            // Check if player has enough money
            if (state.Money < cost)
            {
                errorMessage = $"Not enough money. Cost: ${cost}, Available: ${state.Money}";
                return false;
            }
            
            // Deduct money
            state.Money -= cost;
            
            // Mark tiles as occupied (only for tile-based lifts)
            bool hasWorldPositions = lift.StartPosition.X != 0 || lift.StartPosition.Y != 0 || lift.StartPosition.Z != 0;
            if (!hasWorldPositions && _terrain != null && _terrain.Grid != null)
            {
                // Legacy tile-based marking
                if (_terrain.Grid.InBounds(lift.BottomStation) && _terrain.Grid.InBounds(lift.TopStation))
                {
                    var bottomTile = _terrain.Grid.GetTile(lift.BottomStation);
                    var topTile = _terrain.Grid.GetTile(lift.TopStation);
                    if (bottomTile != null) bottomTile.Occupied = true;
                    if (topTile != null) topTile.Occupied = true;
                }
            }
            
            // Add to lift list
            _lifts.Add(lift);
            
            // Register snap points (only for tile-based lifts)
            if (!hasWorldPositions)
            {
                RegisterSnapPoints(lift);
            }
            
            errorMessage = "";
            return true;
        }
        
        /// <summary>
        /// Registers snap points for a lift.
        /// </summary>
        private void RegisterSnapPoints(LiftData lift)
        {
            if (_snapRegistry == null) return;
            
            // Register bottom station
            var bottomSnap = new SnapPoint(
                SnapPointType.LiftBottom,
                lift.BottomStation,
                lift.LiftId,
                lift.Name
            );
            _snapRegistry.Register(bottomSnap);
            
            // Register top station
            var topSnap = new SnapPoint(
                SnapPointType.LiftTop,
                lift.TopStation,
                lift.LiftId,
                lift.Name
            );
            _snapRegistry.Register(topSnap);
        }
        
        /// <summary>
        /// Unregisters snap points for a lift.
        /// </summary>
        private void UnregisterSnapPoints(LiftData lift)
        {
            if (_snapRegistry == null) return;
            
            _snapRegistry.UnregisterByOwner(lift.LiftId);
        }
        
        /// <summary>
        /// Removes a lift from the system.
        /// </summary>
        public void RemoveLift(LiftData lift)
        {
            // Unregister snap points
            UnregisterSnapPoints(lift);
            
            // Free up tiles
            var bottomTile = _terrain.Grid.GetTile(lift.BottomStation);
            var topTile = _terrain.Grid.GetTile(lift.TopStation);
            if (bottomTile != null) bottomTile.Occupied = false;
            if (topTile != null) topTile.Occupied = false;
            
            _lifts.Remove(lift);
        }
        
        /// <summary>
        /// Gets all lifts.
        /// </summary>
        public List<LiftData> GetAllLifts()
        {
            return new List<LiftData>(_lifts);
        }
        
        /// <summary>
        /// Gets a lift by ID.
        /// </summary>
        public LiftData GetLift(int liftId)
        {
            return _lifts.Find(l => l.LiftId == liftId);
        }
    }
}

