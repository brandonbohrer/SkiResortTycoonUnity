using UnityEngine;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Manages all lodges in the resort.
    /// Provides a central registry for skiers to find nearest available lodge.
    /// </summary>
    public class LodgeManager : MonoBehaviour
    {
        private static LodgeManager _instance;
        private List<LodgeFacility> _allLodges = new List<LodgeFacility>();
        
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;
        
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static LodgeManager Instance => _instance;
        
        /// <summary>
        /// All lodges in the resort
        /// </summary>
        public List<LodgeFacility> AllLodges => _allLodges;
        
        /// <summary>
        /// Total number of lodges
        /// </summary>
        public int LodgeCount => _allLodges.Count;
        
        void Awake()
        {
            // Singleton pattern
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            // Find all existing lodges in scene
            LodgeFacility[] existingLodges = FindObjectsOfType<LodgeFacility>();
            foreach (LodgeFacility lodge in existingLodges)
            {
                RegisterLodge(lodge);
            }
            
            if (_enableDebugLogs) Debug.Log($"[LodgeManager] Initialized with {_allLodges.Count} lodges");
        }
        
        /// <summary>
        /// Registers a new lodge with the manager.
        /// </summary>
        public void RegisterLodge(LodgeFacility lodge)
        {
            if (lodge == null) return;
            
            if (!_allLodges.Contains(lodge))
            {
                _allLodges.Add(lodge);
                if (_enableDebugLogs) Debug.Log($"[LodgeManager] Registered lodge at {lodge.Position}. Total: {_allLodges.Count}");
            }
        }
        
        /// <summary>
        /// Unregisters a lodge from the manager.
        /// </summary>
        public void UnregisterLodge(LodgeFacility lodge)
        {
            if (lodge == null) return;
            
            _allLodges.Remove(lodge);
            if (_enableDebugLogs) Debug.Log($"[LodgeManager] Unregistered lodge. Total: {_allLodges.Count}");
        }
        
        /// <summary>
        /// Finds the nearest lodge to a position that has available capacity.
        /// Returns null if no lodges are available.
        /// </summary>
        public LodgeFacility FindNearestAvailableLodge(Vector3 position)
        {
            LodgeFacility nearest = null;
            float nearestDistance = float.MaxValue;
            
            foreach (LodgeFacility lodge in _allLodges)
            {
                if (lodge == null) continue;
                if (lodge.IsFull) continue;
                
                float distance = Vector3.Distance(position, lodge.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = lodge;
                }
            }
            
            return nearest;
        }
        
        /// <summary>
        /// Finds the nearest lodge to a position (regardless of capacity).
        /// </summary>
        public LodgeFacility FindNearestLodge(Vector3 position)
        {
            LodgeFacility nearest = null;
            float nearestDistance = float.MaxValue;
            
            foreach (LodgeFacility lodge in _allLodges)
            {
                if (lodge == null) continue;
                
                float distance = Vector3.Distance(position, lodge.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = lodge;
                }
            }
            
            return nearest;
        }
        
        /// <summary>
        /// Finds all lodges within a radius of a position.
        /// </summary>
        public List<LodgeFacility> FindLodgesInRadius(Vector3 position, float radius)
        {
            List<LodgeFacility> lodgesInRadius = new List<LodgeFacility>();
            
            foreach (LodgeFacility lodge in _allLodges)
            {
                if (lodge == null) continue;
                
                float distance = Vector3.Distance(position, lodge.Position);
                if (distance <= radius)
                {
                    lodgesInRadius.Add(lodge);
                }
            }
            
            return lodgesInRadius;
        }
        
        /// <summary>
        /// Gets the total capacity across all lodges.
        /// </summary>
        public int GetTotalCapacity()
        {
            int total = 0;
            foreach (LodgeFacility lodge in _allLodges)
            {
                if (lodge != null)
                {
                    total += lodge.Capacity;
                }
            }
            return total;
        }
        
        /// <summary>
        /// Gets the total current occupancy across all lodges.
        /// </summary>
        public int GetTotalOccupancy()
        {
            int total = 0;
            foreach (LodgeFacility lodge in _allLodges)
            {
                if (lodge != null)
                {
                    total += lodge.CurrentOccupancy;
                }
            }
            return total;
        }
        
        /// <summary>
        /// Gets the total number of available slots across all lodges.
        /// </summary>
        public int GetAvailableSlots()
        {
            return GetTotalCapacity() - GetTotalOccupancy();
        }
    }
}
