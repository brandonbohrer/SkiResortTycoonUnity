using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Attach to the Base Lodge prefab in the scene.
    /// Registers a square perimeter of snap points around the lodge so that
    /// lifts and trails can connect to any side via the MagneticCursor.
    /// Also keeps a central BaseSpawn point for skier spawning.
    /// </summary>
    public class BaseSnapPointRegistrar : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int _baseId = 0;
        [SerializeField] private string _baseName = "Base Lodge";

        [Header("Snap Perimeter")]
        [Tooltip("Half-size of the square perimeter around the lodge center.")]
        [SerializeField] private float _perimeterHalfSize = 25f;
        [Tooltip("Spacing between snap points along each edge.")]
        [SerializeField] private float _pointSpacing = 3f;

        [Header("Debug")]
        [SerializeField] private bool _showPerimeterLine = true;
        [SerializeField] private Color _lineColor = Color.cyan;
        [SerializeField] private float _lineWidth = 0.3f;

        private LineRenderer _perimeterLine;
        private bool _registered;

        void Start()
        {
            Invoke(nameof(TryRegister), 0.1f);
        }

        void Update()
        {
            // Retry every frame until successful — covers the case where LiftBuilder
            // initializes late (e.g. when the scene is loaded from the main menu).
            if (!_registered)
                TryRegister();
        }

        /// <summary>
        /// Re-registers snap points. Call after a save is loaded to ensure points are
        /// present even if registration was skipped due to init order.
        /// </summary>
        public void EnsureRegistered()
        {
            _registered = false;
            TryRegister();
        }

        private void TryRegister()
        {
            var liftBuilder = FindObjectOfType<LiftBuilder>();
            if (liftBuilder == null || liftBuilder.Connectivity == null) return;
            RegisterSnapPoints(liftBuilder);
        }

        private void RegisterSnapPoints(LiftBuilder liftBuilder)
        {
            _registered = true;
            var registry = liftBuilder.Connectivity.Registry;
            Vector3 center = transform.position;
            int count = 0;

            registry.Register(new SnapPoint(
                SnapPointType.BaseSpawn,
                MountainManager.ToVector3f(center),
                _baseId,
                _baseName));
            count++;

            float half = _perimeterHalfSize;
            float spacing = Mathf.Max(_pointSpacing, 1f);
            int steps = Mathf.Max(2, Mathf.RoundToInt((half * 2f) / spacing));

            Vector3[] corners = new Vector3[]
            {
                center + new Vector3(-half, 0f,  half),  // NW
                center + new Vector3( half, 0f,  half),  // NE
                center + new Vector3( half, 0f, -half),  // SE
                center + new Vector3(-half, 0f, -half),  // SW
            };
            foreach (var c in corners)
            {
                Vector3 grounded = GroundToTerrain(c, center.y);
                Vector3f pos = MountainManager.ToVector3f(grounded);
                string label = $"{_baseName}_Corner{count}";
                registry.Register(new SnapPoint(SnapPointType.BaseSpawn, pos, _baseId, label));
                registry.Register(new SnapPoint(SnapPointType.BuildingEntrance, pos, _baseId, label));
                count++;
            }

            for (int i = 0; i <= steps; i++)
            {
                float t = -half + (half * 2f) * (i / (float)steps);

                Vector3[] edgePoints = new Vector3[]
                {
                    center + new Vector3(t,    0f, half),
                    center + new Vector3(t,    0f, -half),
                    center + new Vector3(half,  0f, t),
                    center + new Vector3(-half, 0f, t),
                };

                foreach (var pt in edgePoints)
                {
                    Vector3 grounded = GroundToTerrain(pt, center.y);
                    Vector3f pos = MountainManager.ToVector3f(grounded);
                    string label = $"{_baseName}_Edge{count}";

                    registry.Register(new SnapPoint(
                        SnapPointType.BaseSpawn, pos, _baseId, label));
                    registry.Register(new SnapPoint(
                        SnapPointType.BuildingEntrance, pos, _baseId, label));
                    count++;
                }
            }

            liftBuilder.Connectivity.RebuildConnections();
            Debug.Log($"[BaseSnapPoint] Registered {count} snap points around {_baseName}");

            if (_showPerimeterLine && _perimeterLine == null)
                DrawPerimeterLine(center, half);
        }

        private void DrawPerimeterLine(Vector3 center, float half)
        {
            var go = new GameObject("BasePerimeterLine");
            go.transform.SetParent(transform);
            _perimeterLine = go.AddComponent<LineRenderer>();

            _perimeterLine.useWorldSpace = true;
            _perimeterLine.loop = true;
            _perimeterLine.startWidth = _lineWidth;
            _perimeterLine.endWidth = _lineWidth;
            _perimeterLine.material = new Material(Shader.Find("Sprites/Default"));
            _perimeterLine.startColor = _lineColor;
            _perimeterLine.endColor = _lineColor;

            float yOff = 0.5f;
            int segs = 12;
            int totalPoints = segs * 4;
            _perimeterLine.positionCount = totalPoints;

            int idx = 0;
            for (int s = 0; s <= segs - 1; s++)
            {
                float t = s / (float)segs;
                Vector3 pt = center + new Vector3(-half + t * half * 2f, 0f, half);
                _perimeterLine.SetPosition(idx++, GroundToTerrain(pt, center.y) + Vector3.up * yOff);
            }
            for (int s = 0; s <= segs - 1; s++)
            {
                float t = s / (float)segs;
                Vector3 pt = center + new Vector3(half, 0f, half - t * half * 2f);
                _perimeterLine.SetPosition(idx++, GroundToTerrain(pt, center.y) + Vector3.up * yOff);
            }
            for (int s = 0; s <= segs - 1; s++)
            {
                float t = s / (float)segs;
                Vector3 pt = center + new Vector3(half - t * half * 2f, 0f, -half);
                _perimeterLine.SetPosition(idx++, GroundToTerrain(pt, center.y) + Vector3.up * yOff);
            }
            for (int s = 0; s <= segs - 1; s++)
            {
                float t = s / (float)segs;
                Vector3 pt = center + new Vector3(-half, 0f, -half + t * half * 2f);
                _perimeterLine.SetPosition(idx++, GroundToTerrain(pt, center.y) + Vector3.up * yOff);
            }
        }

        private Vector3 GroundToTerrain(Vector3 point, float fallbackY)
        {
            if (Physics.Raycast(new Vector3(point.x, point.y + 200f, point.z),
                Vector3.down, out RaycastHit hit, 500f))
            {
                point.y = hit.point.y;
            }
            else
            {
                point.y = fallbackY;
            }
            return point;
        }
    }
}
