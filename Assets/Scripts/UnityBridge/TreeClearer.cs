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
        
        // ── Preview tree management (for interactive placement) ────────
        private HashSet<GameObject> _previewClearedTrees = new HashSet<GameObject>();
        private List<TreeState> _previewTreeStates = new List<TreeState>();
        
        void Awake()
        {
            _instance = this;
            Debug.Log("[TreeClearer] Initialized and ready!");
        }
        
        private struct TreeState
        {
            public GameObject Tree;
            public bool WasActive;
        }
        
        // ── Preview clearing (temporary, can be restored) ──────────────
        
        /// <summary>
        /// Clear trees for preview (hides them but stores state for restoration).
        /// Call RestorePreviewTrees() to bring them back.
        /// </summary>
        public static void ClearTreesForPreview(List<Vector3> pathPoints, float corridorWidth)
        {
            if (_instance == null) return;
            _instance.ClearTreesForPreviewInternal(pathPoints, corridorWidth);
        }
        
        /// <summary>
        /// Restore all trees that were hidden for preview.
        /// </summary>
        public static void RestorePreviewTrees()
        {
            if (_instance == null) return;
            _instance.RestorePreviewTreesInternal();
        }
        
        // ── Permanent clearing ──────────────────────────────────────────
        
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
        /// Interpolates between consecutive points so no tree in the corridor is missed.
        /// </summary>
        public static void ClearTreesAlongPath(List<Vector3> pathPoints, float corridorWidth)
        {
            if (_instance == null)
            {
                Debug.LogWarning("[TreeClearer] No instance found. Add TreeClearer component to scene.");
                return;
            }
            
            if (pathPoints.Count == 0) return;
            
            // Build a dense sample list: at every corridorWidth step between
            // consecutive path points + all original points.
            var samples = new List<Vector3>();
            samples.Add(pathPoints[0]);
            
            for (int i = 1; i < pathPoints.Count; i++)
            {
                Vector3 a = pathPoints[i - 1];
                Vector3 b = pathPoints[i];
                float segLen = Vector3.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / corridorWidth));
                for (int s = 1; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    samples.Add(Vector3.Lerp(a, b, t));
                }
            }
            
            foreach (Vector3 point in samples)
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
        
        // ── Preview clearing internals ──────────────────────────────────
        
        private void ClearTreesForPreviewInternal(List<Vector3> pathPoints, float corridorWidth)
        {
            // First restore any previously cleared trees
            RestorePreviewTreesInternal();
            
            // Find trees container
            if (_treesContainer == null)
            {
                _treesContainer = GameObject.Find("Trees");
                if (_treesContainer == null) return;
            }
            
            // Build dense sample list
            var samples = new List<Vector3>();
            if (pathPoints.Count > 0)
            {
                samples.Add(pathPoints[0]);
                for (int i = 1; i < pathPoints.Count; i++)
                {
                    Vector3 a = pathPoints[i - 1];
                    Vector3 b = pathPoints[i];
                    float segLen = Vector3.Distance(a, b);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / (corridorWidth * 0.5f))); // Denser sampling
                    for (int s = 1; s <= steps; s++)
                    {
                        float t = (float)s / steps;
                        samples.Add(Vector3.Lerp(a, b, t));
                    }
                }
            }
            
            // Get all trees (active and inactive)
            Transform[] allTransforms = _treesContainer.GetComponentsInChildren<Transform>(true);
            
            foreach (Vector3 sample in samples)
            {
                foreach (Transform treeTransform in allTransforms)
                {
                    if (treeTransform == _treesContainer.transform) continue;
                    
                    GameObject tree = treeTransform.gameObject;
                    if (_previewClearedTrees.Contains(tree)) continue; // Already hidden
                    
                    float distance = Vector3.Distance(tree.transform.position, sample);
                    if (distance <= corridorWidth)
                    {
                        _previewTreeStates.Add(new TreeState { Tree = tree, WasActive = tree.activeSelf });
                        tree.SetActive(false);
                        _previewClearedTrees.Add(tree);
                    }
                }
            }
        }
        
        private void RestorePreviewTreesInternal()
        {
            foreach (var state in _previewTreeStates)
            {
                if (state.Tree != null)
                {
                    state.Tree.SetActive(state.WasActive);
                }
            }
            _previewTreeStates.Clear();
            _previewClearedTrees.Clear();
        }
    }
}
