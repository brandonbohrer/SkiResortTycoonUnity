using UnityEngine;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Helper script to clear trees in a radius around a world position.
    /// Used when lifts/trails are built to create clear corridors.
    /// Uses a spatial grid for O(1) cell lookups so preview clearing scales
    /// to thousands of trees without frame drops.
    /// </summary>
    public class TreeClearer : MonoBehaviour
    {
        private static TreeClearer _instance;
        private GameObject _treesContainer;
        private Transform[] _cachedTreeTransforms;
        private int _cachedTreeTransformCount = -1;

        private readonly List<GameObject> _powderHiddenTrees = new List<GameObject>();
        private readonly List<GameObject> _powderOverlayTrees = new List<GameObject>();

        // ── Preview tree management (for interactive placement) ────────
        private readonly HashSet<GameObject> _previewClearedTrees = new HashSet<GameObject>();
        private readonly List<TreeState> _previewTreeStates = new List<TreeState>();

        // ── Spatial grid for fast proximity queries ────────────────────
        private const float GridCellSize = 16f;
        private Dictionary<long, List<int>> _grid;
        private Vector3[] _treePositions;
        private GameObject[] _treeObjects;
        private int _treeCount;
        private bool _gridBuilt;

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
        /// Make preview-hidden trees permanent (don't restore them).
        /// Call this on trail/lift confirm so trees stay hidden.
        /// </summary>
        public static void CommitPreviewTrees()
        {
            if (_instance == null) return;
            _instance._previewTreeStates.Clear();
            _instance._previewClearedTrees.Clear();
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
        /// Temporarily replaces visible terrain trees with snowy variants (Powder Day).
        /// Call <see cref="EndPowderTreeOverlay"/> to restore.
        /// </summary>
        public void BeginPowderTreeOverlay(GameObject snowyTreePrefab)
        {
            EndPowderTreeOverlay();
            if (snowyTreePrefab == null || !TryEnsureTreesContainer()) return;

            Transform[] transforms = GetTreeTransforms();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null || t == _treesContainer.transform) continue;

                GameObject original = t.gameObject;
                if (!original.activeInHierarchy) continue;

                GameObject overlay = Instantiate(
                    snowyTreePrefab,
                    original.transform.position,
                    original.transform.rotation,
                    original.transform.parent);
                overlay.transform.localScale = original.transform.localScale;
                original.SetActive(false);
                _powderHiddenTrees.Add(original);
                _powderOverlayTrees.Add(overlay);
            }

            InvalidateTreeCache();
        }

        /// <summary>
        /// Removes snowy overlays and re-enables the original trees.
        /// </summary>
        public void EndPowderTreeOverlay()
        {
            for (int i = 0; i < _powderOverlayTrees.Count; i++)
            {
                if (_powderOverlayTrees[i] != null)
                    Destroy(_powderOverlayTrees[i]);
            }
            _powderOverlayTrees.Clear();

            for (int i = 0; i < _powderHiddenTrees.Count; i++)
            {
                if (_powderHiddenTrees[i] != null)
                    _powderHiddenTrees[i].SetActive(true);
            }
            _powderHiddenTrees.Clear();

            InvalidateTreeCache();
        }

        private void InvalidateTreeCache()
        {
            _cachedTreeTransforms = null;
            _cachedTreeTransformCount = -1;
            _gridBuilt = false;
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
        // Spatial grid
        // ─────────────────────────────────────────────────────────────

        private void EnsureGrid()
        {
            if (_gridBuilt) return;
            if (!TryEnsureTreesContainer()) return;

            Transform[] transforms = GetTreeTransforms();

            int count = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i] != _treesContainer.transform)
                    count++;
            }

            _treePositions = new Vector3[count];
            _treeObjects = new GameObject[count];
            _treeCount = count;

            if (_grid == null)
                _grid = new Dictionary<long, List<int>>(count / 4);
            else
                _grid.Clear();

            int idx = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null || t == _treesContainer.transform) continue;

                Vector3 pos = t.position;
                _treePositions[idx] = pos;
                _treeObjects[idx] = t.gameObject;

                long key = CellKey(pos.x, pos.z);
                if (!_grid.TryGetValue(key, out var list))
                {
                    list = new List<int>(8);
                    _grid[key] = list;
                }
                list.Add(idx);
                idx++;
            }

            _gridBuilt = true;
        }

        private static long CellKey(float x, float z)
        {
            int cx = Mathf.FloorToInt(x / GridCellSize);
            int cz = Mathf.FloorToInt(z / GridCellSize);
            return ((long)cx << 32) | (uint)cz;
        }

        /// <summary>
        /// Collects all grid cell keys that overlap a circle at (cx, cz) with the given radius.
        /// </summary>
        private static void GetCellKeysInRadius(float cx, float cz, float radius, List<long> keys)
        {
            int minCX = Mathf.FloorToInt((cx - radius) / GridCellSize);
            int maxCX = Mathf.FloorToInt((cx + radius) / GridCellSize);
            int minCZ = Mathf.FloorToInt((cz - radius) / GridCellSize);
            int maxCZ = Mathf.FloorToInt((cz + radius) / GridCellSize);

            for (int gx = minCX; gx <= maxCX; gx++)
                for (int gz = minCZ; gz <= maxCZ; gz++)
                    keys.Add(((long)gx << 32) | (uint)gz);
        }

        // Reusable buffers to avoid per-frame allocations
        private readonly HashSet<long> _queriedCells = new HashSet<long>();
        private readonly List<long> _cellKeyBuffer = new List<long>(64);

        /// <summary>
        /// Collects unique cell keys that cover the corridor around the given path.
        /// </summary>
        private void CollectCellsAlongPath(List<Vector3> pathPoints, float corridorWidth)
        {
            _queriedCells.Clear();
            _cellKeyBuffer.Clear();

            for (int s = 0; s < pathPoints.Count; s++)
            {
                Vector3 pt = pathPoints[s];
                GetCellKeysInRadius(pt.x, pt.z, corridorWidth, _cellKeyBuffer);
            }

            for (int i = 0; i < _cellKeyBuffer.Count; i++)
                _queriedCells.Add(_cellKeyBuffer[i]);
        }

        // ─────────────────────────────────────────────────────────────
        // Permanent clearing internals
        // ─────────────────────────────────────────────────────────────

        private void ClearTreesAlongPathInternal(List<Vector3> pathPoints, float corridorWidth)
        {
            if (!TryEnsureTreesContainer()) return;
            EnsureGrid();

            CollectCellsAlongPath(pathPoints, corridorWidth);

            int totalCleared = 0;
            float corridorSq = corridorWidth * corridorWidth;

            foreach (long key in _queriedCells)
            {
                if (!_grid.TryGetValue(key, out var indices)) continue;

                for (int i = 0; i < indices.Count; i++)
                {
                    int idx = indices[i];
                    GameObject obj = _treeObjects[idx];
                    if (obj == null || !obj.activeSelf) continue;

                    float minDist = MinDistanceToPathXZ(_treePositions[idx], pathPoints, corridorWidth);
                    if (minDist <= corridorWidth)
                    {
                        obj.SetActive(false);
                        totalCleared++;
                    }
                }
            }

            Debug.Log($"[TreeClearer] Disabled {totalCleared} trees along path (corridor={corridorWidth}m)");
        }

        private int ClearTreesInternal(Vector3 worldPosition, float radius)
        {
            if (!TryEnsureTreesContainer()) return 0;
            EnsureGrid();

            _cellKeyBuffer.Clear();
            GetCellKeysInRadius(worldPosition.x, worldPosition.z, radius, _cellKeyBuffer);

            int clearedCount = 0;
            float radiusSq = radius * radius;

            for (int k = 0; k < _cellKeyBuffer.Count; k++)
            {
                if (!_grid.TryGetValue(_cellKeyBuffer[k], out var indices)) continue;

                for (int i = 0; i < indices.Count; i++)
                {
                    int idx = indices[i];
                    GameObject obj = _treeObjects[idx];
                    if (obj == null) continue;

                    float dx = _treePositions[idx].x - worldPosition.x;
                    float dz = _treePositions[idx].z - worldPosition.z;
                    if (dx * dx + dz * dz <= radiusSq)
                    {
                        Destroy(obj);
                        _treeObjects[idx] = null;
                        clearedCount++;
                    }
                }
            }

            return clearedCount;
        }

        // ─────────────────────────────────────────────────────────────
        // Preview clearing internals
        // ─────────────────────────────────────────────────────────────

        private void ClearTreesForPreviewInternal(List<Vector3> pathPoints, float corridorWidth)
        {
            RestorePreviewTreesInternal();

            if (pathPoints == null || pathPoints.Count < 2) return;
            if (!TryEnsureTreesContainer()) return;
            EnsureGrid();

            CollectCellsAlongPath(pathPoints, corridorWidth);

            foreach (long key in _queriedCells)
            {
                if (!_grid.TryGetValue(key, out var indices)) continue;

                for (int i = 0; i < indices.Count; i++)
                {
                    int idx = indices[i];
                    GameObject obj = _treeObjects[idx];
                    if (obj == null) continue;
                    if (_previewClearedTrees.Contains(obj)) continue;

                    float minDist = MinDistanceToPathXZ(_treePositions[idx], pathPoints, corridorWidth);
                    if (minDist <= corridorWidth)
                    {
                        _previewTreeStates.Add(new TreeState { Tree = obj, WasActive = obj.activeSelf });
                        obj.SetActive(false);
                        _previewClearedTrees.Add(obj);
                    }
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

                if (minDist <= earlyOutRadius) break;
            }

            return minDist;
        }

        private static float DistancePointToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            float pax = p.x - a.x;
            float paz = p.z - a.z;
            float abx = b.x - a.x;
            float abz = b.z - a.z;

            float ab2 = abx * abx + abz * abz;
            if (ab2 < 0.0001f)
            {
                return Mathf.Sqrt(pax * pax + paz * paz);
            }

            float t = (pax * abx + paz * abz) / ab2;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;

            float cx = a.x + t * abx - p.x;
            float cz = a.z + t * abz - p.z;
            return Mathf.Sqrt(cx * cx + cz * cz);
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

        private Transform[] GetTreeTransforms()
        {
            if (_treesContainer == null)
                return System.Array.Empty<Transform>();

            int currentCount = _treesContainer.transform.childCount;
            if (_cachedTreeTransforms == null || _cachedTreeTransformCount != currentCount)
            {
                _cachedTreeTransforms = _treesContainer.GetComponentsInChildren<Transform>(true);
                _cachedTreeTransformCount = currentCount;
            }

            return _cachedTreeTransforms;
        }
    }
}
