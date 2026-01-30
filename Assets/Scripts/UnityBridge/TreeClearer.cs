using UnityEngine;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Helper script to clear trees in a radius around a world position.
    /// Used when lifts/trails are built to create clear corridors.
    /// </summary>
    public class TreeClearer : MonoBehaviour
    {
        private static TreeClearer _instance;
        private GameObject _treesContainer;
        
        void Awake()
        {
            _instance = this;
            Debug.Log("[TreeClearer] Initialized and ready!");
        }
        
        /// <summary>
        /// Clears trees within a radius of a single point.
        /// </summary>
        public static void ClearTreesAroundPoint(Vector3 worldPosition, float radius)
        {
            if (_instance == null)
            {
                Debug.LogWarning("[TreeClearer] No instance found. Add TreeClearer component to scene.");
                return;
            }
            
            _instance.ClearTreesInternal(worldPosition, radius);
        }
        
        /// <summary>
        /// Clears trees along a line path (for lifts or trails).
        /// </summary>
        public static void ClearTreesAlongPath(List<Vector3> pathPoints, float corridorWidth)
        {
            if (_instance == null)
            {
                Debug.LogWarning("[TreeClearer] No instance found. Add TreeClearer component to scene.");
                return;
            }
            
            foreach (Vector3 point in pathPoints)
            {
                _instance.ClearTreesInternal(point, corridorWidth);
            }
        }
        
        private void ClearTreesInternal(Vector3 worldPosition, float radius)
        {
            // Find trees container
            if (_treesContainer == null)
            {
                _treesContainer = GameObject.Find("Trees");
                if (_treesContainer == null)
                {
                    Debug.LogWarning("[TreeClearer] No 'Trees' container found in scene. Trees cannot be cleared.");
                    return;
                }
            }
            
            // Get all tree transforms
            Transform[] trees = _treesContainer.GetComponentsInChildren<Transform>();
            int clearedCount = 0;
            
            foreach (Transform tree in trees)
            {
                // Skip the container itself
                if (tree == _treesContainer.transform) continue;
                
                // Check distance to world position
                float distance = Vector3.Distance(tree.position, worldPosition);
                if (distance <= radius)
                {
                    Destroy(tree.gameObject);
                    clearedCount++;
                }
            }
            
            if (clearedCount > 0)
            {
                Debug.Log($"[TreeClearer] Cleared {clearedCount} trees within {radius}m of {worldPosition}");
            }
        }
    }
}
