using UnityEngine;
using SkiResortTycoon.Core;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Magnetic cursor that snaps to nearby snap points (Cities: Skylines style).
    /// Shows visual feedback when near valid snap points.
    /// </summary>
    public class MagneticCursor
    {
        private readonly SnapRegistry _registry;
        private readonly float _snapRadius;
        
        private Vector3 _rawPosition;
        private Vector3 _snappedPosition;
        private SnapPoint? _nearestSnap;
        
        public Vector3 RawPosition => _rawPosition;
        public Vector3 SnappedPosition => _snappedPosition;
        public bool IsSnapped => _nearestSnap.HasValue;
        public SnapPoint? NearestSnapPoint => _nearestSnap;
        
        public MagneticCursor(SnapRegistry registry, float snapRadius = 5f)
        {
            _registry = registry;
            _snapRadius = snapRadius;
        }
        
        /// <summary>
        /// Update cursor position. Call this every frame with the raw mouse position in world space.
        /// </summary>
        public void Update(Vector3 rawWorldPosition, SnapPointType[] validTypes = null)
        {
            _rawPosition = rawWorldPosition;
            
            // Find nearest valid snap point
            _nearestSnap = FindNearestSnapPoint(rawWorldPosition, _snapRadius, validTypes);
            
            if (_nearestSnap.HasValue)
            {
                // Snap to point
                var snap = _nearestSnap.Value;
                _snappedPosition = new Vector3(snap.Position.X, snap.Position.Y, snap.Position.Z);
            }
            else
            {
                // No snap - use raw position
                _snappedPosition = rawWorldPosition;
            }
        }
        
        /// <summary>
        /// Finds the nearest snap point within radius, optionally filtered by type.
        /// </summary>
        private SnapPoint? FindNearestSnapPoint(Vector3 position, float radius, SnapPointType[] validTypes = null)
        {
            List<SnapPoint> candidateSnaps;
            
            if (validTypes != null && validTypes.Length > 0)
            {
                // Get only specified types
                candidateSnaps = new List<SnapPoint>();
                foreach (var type in validTypes)
                {
                    candidateSnaps.AddRange(_registry.GetByType(type));
                }
            }
            else
            {
                // Get all snap points
                candidateSnaps = _registry.GetAll();
            }
            
            SnapPoint? nearest = null;
            float minDist = radius;
            
            foreach (var snap in candidateSnaps)
            {
                var snapPos = new Vector3(snap.Position.X, snap.Position.Y, snap.Position.Z);
                float dist = Vector3.Distance(position, snapPos);
                
                if (dist < minDist)
                {
                    nearest = snap;
                    minDist = dist;
                }
            }
            
            return nearest;
        }
    }
}
