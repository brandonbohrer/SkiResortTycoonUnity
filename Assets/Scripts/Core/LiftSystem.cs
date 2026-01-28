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
        
        // Configurable costs and constraints
        public int BaseCost { get; set; } = 5000;           // Base cost per lift
        public int CostPerTile { get; set; } = 100;         // Additional cost per tile length
        public int CostPerHeightUnit { get; set; } = 200;   // Additional cost per height unit
        public int MaxLength { get; set; } = 50;            // Max horizontal distance
        public int MinElevationGain { get; set; } = 2;      // Minimum height gain required
        
        public List<LiftData> Lifts => _lifts;
        
        public LiftSystem(TerrainData terrain)
        {
            _terrain = terrain;
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
            cost += lift.Length * CostPerTile;
            cost += lift.ElevationGain * CostPerHeightUnit;
            
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
            
            // Mark tiles as occupied
            var bottomTile = _terrain.Grid.GetTile(lift.BottomStation);
            var topTile = _terrain.Grid.GetTile(lift.TopStation);
            bottomTile.Occupied = true;
            topTile.Occupied = true;
            
            // Add to lift list
            _lifts.Add(lift);
            
            errorMessage = "";
            return true;
        }
        
        /// <summary>
        /// Removes a lift from the system.
        /// </summary>
        public void RemoveLift(LiftData lift)
        {
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
    }
}

