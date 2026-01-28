using System.Collections.Generic;
using System.Linq;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Central registry for all snap points in the world.
    /// Pure C# - no Unity types.
    /// </summary>
    public class SnapRegistry
    {
        private List<SnapPoint> _allSnapPoints;
        private Dictionary<SnapPointType, List<SnapPoint>> _pointsByType;
        
        public SnapRegistry()
        {
            _allSnapPoints = new List<SnapPoint>();
            _pointsByType = new Dictionary<SnapPointType, List<SnapPoint>>();
            
            // Initialize empty lists for each type
            foreach (SnapPointType type in System.Enum.GetValues(typeof(SnapPointType)))
            {
                _pointsByType[type] = new List<SnapPoint>();
            }
        }
        
        /// <summary>
        /// Registers a snap point.
        /// </summary>
        public void Register(SnapPoint point)
        {
            if (!_allSnapPoints.Contains(point))
            {
                _allSnapPoints.Add(point);
                _pointsByType[point.Type].Add(point);
            }
        }
        
        /// <summary>
        /// Unregisters all snap points owned by a specific object.
        /// </summary>
        public void UnregisterByOwner(int ownerId)
        {
            _allSnapPoints.RemoveAll(p => p.OwnerId == ownerId);
            
            foreach (var list in _pointsByType.Values)
            {
                list.RemoveAll(p => p.OwnerId == ownerId);
            }
        }
        
        /// <summary>
        /// Unregisters a specific snap point.
        /// </summary>
        public void Unregister(SnapPoint point)
        {
            _allSnapPoints.Remove(point);
            _pointsByType[point.Type].Remove(point);
        }
        
        /// <summary>
        /// Clears all snap points.
        /// </summary>
        public void Clear()
        {
            _allSnapPoints.Clear();
            foreach (var list in _pointsByType.Values)
            {
                list.Clear();
            }
        }
        
        /// <summary>
        /// Gets all snap points of a specific type.
        /// </summary>
        public List<SnapPoint> GetByType(SnapPointType type)
        {
            return new List<SnapPoint>(_pointsByType[type]);
        }
        
        /// <summary>
        /// Gets all snap points owned by a specific object.
        /// </summary>
        public List<SnapPoint> GetByOwner(int ownerId)
        {
            return _allSnapPoints.Where(p => p.OwnerId == ownerId).ToList();
        }
        
        /// <summary>
        /// Finds all snap points of a given type within a radius of a coordinate.
        /// </summary>
        public List<SnapPoint> FindNearby(TileCoord coord, SnapPointType type, int maxDistance)
        {
            var results = new List<SnapPoint>();
            
            foreach (var point in _pointsByType[type])
            {
                if (point.DistanceTo(coord) <= maxDistance)
                {
                    results.Add(point);
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Finds all snap points (any type) within a radius of a coordinate.
        /// </summary>
        public List<SnapPoint> FindNearby(TileCoord coord, int maxDistance)
        {
            var results = new List<SnapPoint>();
            
            foreach (var point in _allSnapPoints)
            {
                if (point.DistanceTo(coord) <= maxDistance)
                {
                    results.Add(point);
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Gets the closest snap point of a given type to a coordinate.
        /// </summary>
        public SnapPoint? GetClosest(TileCoord coord, SnapPointType type)
        {
            SnapPoint? closest = null;
            int minDistance = int.MaxValue;
            
            foreach (var point in _pointsByType[type])
            {
                int dist = point.DistanceTo(coord);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = point;
                }
            }
            
            return closest;
        }
        
        /// <summary>
        /// Gets all registered snap points.
        /// </summary>
        public List<SnapPoint> GetAll()
        {
            return new List<SnapPoint>(_allSnapPoints);
        }
        
        /// <summary>
        /// Gets count of snap points by type.
        /// </summary>
        public int GetCount(SnapPointType type)
        {
            return _pointsByType[type].Count;
        }
        
        /// <summary>
        /// Gets total count of all snap points.
        /// </summary>
        public int GetTotalCount()
        {
            return _allSnapPoints.Count;
        }
    }
}

