using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace SkiResortTycoon.Editor
{
    /// <summary>
    /// Editor tool to automatically place trees on the mountain.
    /// Creates/uses a "Trees" container and fills it with tree prefabs.
    /// 
    /// Key behavior:
    /// - Samples X/Z across mountain renderer bounds.
    /// - Raycasts downward from above bounds.
    /// - Accepts collider hits on the mountain OR any of its children.
    /// - Height rules are WORLD Y and now allow negative Min/Max values.
    /// </summary>
    public class TreePlacer : EditorWindow
    {
        [MenuItem("Tools/Ski Resort Tycoon/Place Trees on Mountain")]
        public static void ShowWindow()
        {
            GetWindow<TreePlacer>("Tree Placer");
        }

        private GameObject _mountainObject;
        private GameObject _treePrefab;

        private int _treeCount = 500;

        // Height constraints (world-space Y) — allow negative values
        private float _minHeight = -40f;   // default below your base (-35.01)
        private float _maxHeight = 100f;   // upper treeline-ish

        // Optional: base "no-tree" clearing (world-space Y) — allow negative values.
        // If you want trees all the way to base, set this <= _minHeight (or very low).
        private float _baseNoTreeHeight = -10000f;

        private float _maxSlope = 45f;
        private float _minSpacing = 5f;
        private Vector2 _scaleRange = new Vector2(0.8f, 1.2f);

        // Performance
        private int _attemptMultiplier = 20; // attempts = treeCount * multiplier
        private float _raycastExtraAboveBounds = 500f;
        private float _raycastDistance = 3000f;

        void OnGUI()
        {
            GUILayout.Label("Automatic Tree Placement", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _mountainObject = (GameObject)EditorGUILayout.ObjectField(
                "Mountain Object", _mountainObject, typeof(GameObject), true);

            _treePrefab = (GameObject)EditorGUILayout.ObjectField(
                "Tree Prefab", _treePrefab, typeof(GameObject), false);

            EditorGUILayout.Space();

            _treeCount = EditorGUILayout.IntSlider("Tree Count", _treeCount, 10, 5000);

            EditorGUILayout.Space();
            GUILayout.Label("Height Rules (World Y)", EditorStyles.boldLabel);

            // Allow negative world Y. Using wide slider ranges so your -35 base is covered.
            _baseNoTreeHeight = EditorGUILayout.Slider(
                "Base No-Tree Height", _baseNoTreeHeight, -500f, 500f);

            _minHeight = EditorGUILayout.Slider("Min Height", _minHeight, -500f, 500f);
            _maxHeight = EditorGUILayout.Slider("Max Height (Tree Line)", _maxHeight, -500f, 500f);

            EditorGUILayout.Space();

            _maxSlope = EditorGUILayout.Slider("Max Slope (degrees)", _maxSlope, 0f, 90f);
            _minSpacing = EditorGUILayout.Slider("Min Spacing", _minSpacing, 0.5f, 30f);
            _scaleRange = EditorGUILayout.Vector2Field("Scale Range (min, max)", _scaleRange);

            EditorGUILayout.Space();
            _attemptMultiplier = EditorGUILayout.IntSlider("Attempt Multiplier", _attemptMultiplier, 5, 50);

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Trees", GUILayout.Height(40)))
            {
                PlaceTrees();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Clear All Trees", GUILayout.Height(30)))
            {
                ClearTrees();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Height rules are WORLD Y.\n" +
                "If your mountain base is negative (e.g., -35), set Min Height <= base.\n" +
                "If you want trees all the way to the base, set Base No-Tree Height <= Min Height (or very low).\n" +
                "Max Height creates the upper tree line.\n" +
                "If you get too few trees, lower Min Spacing or increase Attempt Multiplier.",
                MessageType.Info);
        }

        private void PlaceTrees()
        {
            if (_mountainObject == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Mountain Object!", "OK");
                return;
            }

            if (_treePrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Tree Prefab!", "OK");
                return;
            }

            if (_scaleRange.x <= 0f || _scaleRange.y <= 0f || _scaleRange.y < _scaleRange.x)
            {
                EditorUtility.DisplayDialog("Error", "Scale Range is invalid. Ensure min > 0 and max >= min.", "OK");
                return;
            }

            if (_maxHeight < _minHeight)
            {
                EditorUtility.DisplayDialog("Error", "Max Height must be >= Min Height.", "OK");
                return;
            }

            if (_baseNoTreeHeight > _maxHeight)
            {
                EditorUtility.DisplayDialog("Error", "Base No-Tree Height should be <= Max Height (tree line).", "OK");
                return;
            }

            // Get or create Trees container
            GameObject treesContainer = GameObject.Find("Trees");
            if (treesContainer == null)
            {
                treesContainer = new GameObject("Trees");
                Undo.RegisterCreatedObjectUndo(treesContainer, "Create Trees Container");
            }

            // Get mountain bounds (Renderer bounds is fine for sampling area)
            Renderer mountainRenderer = _mountainObject.GetComponentInChildren<Renderer>();
            if (mountainRenderer == null)
            {
                EditorUtility.DisplayDialog("Error", "Mountain object has no Renderer (on itself or children)!", "OK");
                return;
            }

            Bounds bounds = mountainRenderer.bounds;

            // We'll store placed positions for spacing checks
            List<Vector3> placedPositions = new List<Vector3>(_treeCount);

            int placedCount = 0;
            int maxAttempts = Mathf.Max(_treeCount * _attemptMultiplier, _treeCount);
            int attempts = 0;

            float edgeInset = 0f;

            while (placedCount < _treeCount && attempts < maxAttempts)
            {
                attempts++;

                float x = Random.Range(bounds.min.x + edgeInset, bounds.max.x - edgeInset);
                float z = Random.Range(bounds.min.z + edgeInset, bounds.max.z - edgeInset);

                Vector3 rayStart = new Vector3(x, bounds.max.y + _raycastExtraAboveBounds, z);
                Ray ray = new Ray(rayStart, Vector3.down);

                if (!Physics.Raycast(ray, out RaycastHit hit, _raycastDistance))
                    continue;

                // Accept hits on mountain or its children (common if collider is on a child)
                if (!hit.collider.transform.IsChildOf(_mountainObject.transform))
                    continue;

                float y = hit.point.y;

                // Optional base clearing rule (WORLD Y)
                // If you want trees all the way down, keep this very low or <= Min Height.
                if (y < _baseNoTreeHeight)
                    continue;

                // Height band (WORLD Y)
                if (y < _minHeight || y > _maxHeight)
                    continue;

                // Slope check
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope > _maxSlope)
                    continue;

                // Spacing check (O(n))
                bool tooClose = false;
                for (int i = 0; i < placedPositions.Count; i++)
                {
                    if (Vector3.Distance(placedPositions[i], hit.point) < _minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;

                // Place tree
                GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(_treePrefab, treesContainer.transform);
                Undo.RegisterCreatedObjectUndo(tree, "Place Tree");

                tree.transform.position = hit.point;

                // Random Y rotation
                tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                // Random uniform scale
                float scale = Random.Range(_scaleRange.x, _scaleRange.y);
                tree.transform.localScale = Vector3.one * scale;

                placedPositions.Add(hit.point);
                placedCount++;
            }

            Debug.Log($"✓ Placed {placedCount} trees! (attempted {attempts} positions)");

            if (placedCount < _treeCount)
            {
                EditorUtility.DisplayDialog(
                    "Tree Placement",
                    $"Placed {placedCount} out of {_treeCount} requested trees.\n\n" +
                    $"Try lowering Min Spacing or increasing Attempt Multiplier.\n\n" +
                    $"Current:\n" +
                    $"- Base No-Tree Height: {_baseNoTreeHeight}\n" +
                    $"- Min Height: {_minHeight}\n" +
                    $"- Tree Line Max Height: {_maxHeight}\n" +
                    $"- Min Spacing: {_minSpacing}\n" +
                    $"- Max Slope: {_maxSlope}",
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog("Success!", $"Placed {placedCount} trees successfully!", "OK");
            }
        }

        private void ClearTrees()
        {
            GameObject treesContainer = GameObject.Find("Trees");
            if (treesContainer != null)
            {
                if (EditorUtility.DisplayDialog(
                        "Clear Trees",
                        "Delete all trees in the 'Trees' container?",
                        "Yes", "Cancel"))
                {
                    Undo.DestroyObjectImmediate(treesContainer);
                    Debug.Log("✓ All trees cleared!");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("No Trees", "No 'Trees' container found!", "OK");
            }
        }
    }
}
