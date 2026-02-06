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
        private readonly HashSet<GameObject> _previewClearedTrees = new HashSet<GameObject>();
        private readonly List<TreeState> _previewTreeStates = new List<TreeState>();

        private void Awake()
        {
            _instance = this;
            Debug.Log("[TreeClearer] Initialized and ready!");
        }

        private struct TreeState
        {
            public GameObject Tree;
            public bool WasActive;
        }

        // ─────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────

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

            int cleared = _instance.ClearTreesInternal(worldPosition, radius);
            if (cleared > 0)
            {
                Debug.Log($"[TreeClearer] Cleared {cleared} trees within {radius}m of {worldPosition}");
            }
        }

        /// <summary>
        /// Clears trees along a path (lift/trail). Uses true distance-to-segment in XZ
        /// so diagonal builds do NOT over-clear (no more circle-stamping samples).
        /// corridorWidth is the radius around the path centerline.
        /// </summary>
        public static void ClearTreesAlongPath(List<Vector3> pathPoints, float corridorWidth)
        {
            if (_instance == null)
            {
                Debug.LogWarning("[TreeClearer] No instance found. Add TreeClearer component to scene.");
                return;
            }

            if (pathPoints == null || pathPoints.Count < 2) return;
            _instance.ClearTreesAlongPathInternal(pathPoints, corridorWidth);
        }

        // ─────────────────────────────────────────────────────────────
        // Permanent clearing internals
        // ─────────────────────────────────────────────────────────────

        private void ClearTreesAlongPathInternal(List<Vector3> pathPoints, float corridorWidth)
        {
            if (!TryEnsureTreesContainer()) return;

            Transform[] trees = _treesContainer.GetComponentsInChildren<Transform>(true);
            int totalCleared = 0;

            for (int i = 0; i < trees.Length; i++)
            {
                Transform tree = trees[i];
                if (tree == _treesContainer.transform) continue;

                Vector3 tp = tree.position;

                // Minimum distance in XZ to any segment of the polyline
                float minDist = MinDistanceToPathXZ(tp, pathPoints, corridorWidth);
                if (minDist <= corridorWidth)
                {
                    Destroy(tree.gameObject);
                    totalCleared++;
                }
            }

            Debug.Log($"[TreeClearer] Cleared {totalCleared} trees along path (corridor={corridorWidth}m)");
        }

        private int ClearTreesInternal(Vector3 worldPosition, float radius)
        {
            if (!TryEnsureTreesContainer()) return 0;

            Transform[] trees = _treesContainer.GetComponentsInChildren<Transform>(true);
            int clearedCount = 0;

            foreach (Transform tree in trees)
            {
                if (tree == _treesContainer.transform) continue;

                float distance = Vector3.Distance(tree.position, worldPosition);
                if (distance <= radius)
                {
                    Destroy(tree.gameObject);
                    clearedCount++;
                }
            }

            return clearedCount;
        }

        // ─────────────────────────────────────────────────────────────
        // Preview clearing internals
        // ─────────────────────────────────────────────────────────────

        private void ClearTreesForPreviewInternal(List<Vector3> pathPoints, float corridorWidth)
        {
            // First restore any previously cleared preview trees
            RestorePreviewTreesInternal();

            if (pathPoints == null || pathPoints.Count < 2) return;
            if (!TryEnsureTreesContainer()) return;

            Transform[] allTransforms = _treesContainer.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform treeTransform = allTransforms[i];
                if (treeTransform == _treesContainer.transform) continue;

                GameObject tree = treeTransform.gameObject;
                if (_previewClearedTrees.Contains(tree)) continue; // already hidden

                float minDist = MinDistanceToPathXZ(treeTransform.position, pathPoints, corridorWidth);
                if (minDist <= corridorWidth)
                {
                    _previewTreeStates.Add(new TreeState { Tree = tree, WasActive = tree.activeSelf });
                    tree.SetActive(false);
                    _previewClearedTrees.Add(tree);
                }
            }
        }

        private void RestorePreviewTreesInternal()
        {
            for (int i = 0; i < _previewTreeStates.Count; i++)
            {
                var state = _previewTreeStates[i];
                if (state.Tree != null)
                {
                    state.Tree.SetActive(state.WasActive);
                }
            }

            _previewTreeStates.Clear();
            _previewClearedTrees.Clear();
        }

        // ─────────────────────────────────────────────────────────────
        // Geometry helpers
        // ─────────────────────────────────────────────────────────────

        private static float MinDistanceToPathXZ(Vector3 point, List<Vector3> pathPoints, float earlyOutRadius)
        {
            float minDist = float.MaxValue;

            for (int s = 1; s < pathPoints.Count; s++)
            {
                float d = DistancePointToSegmentXZ(point, pathPoints[s - 1], pathPoints[s]);
                if (d < minDist) minDist = d;

                // Early out if we already know it's inside the corridor
                if (minDist <= earlyOutRadius) break;
            }

            return minDist;
        }

        private static float DistancePointToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 P = new Vector2(p.x, p.z);
            Vector2 A = new Vector2(a.x, a.z);
            Vector2 B = new Vector2(b.x, b.z);

            Vector2 AB = B - A;
            float ab2 = Vector2.Dot(AB, AB);
            if (ab2 < 0.0001f) return Vector2.Distance(P, A); // a==b

            float t = Vector2.Dot(P - A, AB) / ab2;
            t = Mathf.Clamp01(t);
            Vector2 closest = A + t * AB;

            return Vector2.Distance(P, closest);
        }

        // ─────────────────────────────────────────────────────────────
        // Utility
        // ─────────────────────────────────────────────────────────────

        private bool TryEnsureTreesContainer()
        {
            if (_treesContainer != null) return true;

            _treesContainer = GameObject.Find("Trees");
            if (_treesContainer == null)
            {
                Debug.LogWarning("[TreeClearer] No 'Trees' container found in scene. Trees cannot be cleared.");
                return false;
            }

            return true;
        }
    }
}