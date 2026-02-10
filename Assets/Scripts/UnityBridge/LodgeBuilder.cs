using UnityEngine;
using SkiResortTycoon.Core;
using SkiResortTycoon.UI;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Lodge placement tool. Drag any lodge prefab into the _lodgePrefab slot.
    /// When activated via BuildActionBar, preview follows mouse, left-click places,
    /// right-click / Esc cancels. LodgeFacility is added automatically at placement.
    /// </summary>
    public class LodgeBuilder : BaseTool
    {
        [Header("Lodge Prefab (drag your model here)")]
        [SerializeField] private GameObject _lodgePrefab;

        [Header("References")]
        [SerializeField] private MountainManager _mountainManager;
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private Camera _camera;

        [Header("Build Settings")]
        [SerializeField] private float _treeClearRadius = 15f;
        [SerializeField] private int _buildCost = 25000;

        [Header("Visual Feedback")]
        [SerializeField] private Color _validColor = new Color(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color _invalidColor = new Color(1f, 0f, 0f, 0.5f);

        private GameObject _previewInstance;
        private Renderer[] _previewRenderers;
        private bool _canPlace;

        public override string ToolName => "Lodge";
        public override string ToolDescription => "Place a lodge";

        // ── BaseTool overrides ──────────────────────────────────────────

        void Start()
        {
            if (_camera == null) _camera = Camera.main;
        }

        public override void OnActivate()
        {
            base.OnActivate();

            if (_lodgePrefab == null)
            {
                Debug.LogError("[LodgeBuilder] No lodge prefab assigned!");
                NotificationManager.Instance?.ShowError("Lodge prefab not assigned!");
                UIManager.Instance?.DeactivateTool();
                return;
            }

            NotificationManager.Instance?.ShowInfo("Click to place lodge (Right-click / ESC to cancel)");
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            CleanupPreview();
        }

        public override void OnCancel()
        {
            base.OnCancel();
            CleanupPreview();
        }

        // ── Preview ─────────────────────────────────────────────────────

        protected override void ShowPreview()
        {
            if (_lodgePrefab == null) return;

            _previewInstance = Instantiate(_lodgePrefab);
            _previewInstance.name = "LodgePreview";

            // Strip anything that shouldn't be on a ghost
            foreach (var col in _previewInstance.GetComponentsInChildren<Collider>())
                Destroy(col);
            foreach (var fac in _previewInstance.GetComponentsInChildren<LodgeFacility>())
                Destroy(fac);

            // Make all renderers semi-transparent
            _previewRenderers = _previewInstance.GetComponentsInChildren<Renderer>();
            foreach (var r in _previewRenderers)
            {
                Material mat = new Material(r.material);
                // Standard pipeline transparency
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                Color c = mat.color; c.a = 0.5f; mat.color = c;
                r.material = mat;
            }
        }

        protected override void UpdatePreview()
        {
            if (_previewInstance == null) return;

            Vector3? hit = _mountainManager?.RaycastMountain(_camera, Input.mousePosition);

            if (hit.HasValue)
            {
                _previewInstance.transform.position = hit.Value;
                _previewInstance.SetActive(true);

                _canPlace = IsValidPlacement(hit.Value);

                // Tint preview green / red
                Color tint = _canPlace ? _validColor : _invalidColor;
                foreach (var r in _previewRenderers)
                {
                    Color c = r.material.color;
                    c.r = tint.r; c.g = tint.g; c.b = tint.b;
                    r.material.color = c;
                }

                // Preview tree clearing -- ClearTreesForPreview needs >=2 points,
                // so we pass two identical points to satisfy the polyline API.
                var pts = new System.Collections.Generic.List<Vector3> { hit.Value, hit.Value };
                TreeClearer.ClearTreesForPreview(pts, _treeClearRadius);
            }
            else
            {
                _previewInstance.SetActive(false);
                TreeClearer.RestorePreviewTrees();
            }
        }

        protected override void HidePreview() => CleanupPreview();

        // ── Input ───────────────────────────────────────────────────────

        protected override void HandleInput()
        {
            base.HandleInput(); // right-click cancel from BaseTool

            if (IsMouseOverUI()) return;

            if (Input.GetMouseButtonDown(0))
            {
                Vector3? hit = _mountainManager?.RaycastMountain(_camera, Input.mousePosition);
                if (hit.HasValue && _canPlace)
                    PlaceLodge(hit.Value);
                else if (!_canPlace)
                    NotificationManager.Instance?.ShowWarning("Cannot place lodge here!");
            }
        }

        // ── Validation ──────────────────────────────────────────────────

        private bool IsValidPlacement(Vector3 pos)
        {
            // Money check
            if (_simulationRunner?.Sim?.State != null && _simulationRunner.Sim.State.Money < _buildCost)
                return false;

            // Minimum spacing from other placed lodges
            if (LodgeManager.Instance != null)
            {
                foreach (var lodge in LodgeManager.Instance.AllLodges)
                {
                    if (lodge != null && Vector3.Distance(pos, lodge.Position) < _treeClearRadius * 2f)
                        return false;
                }
            }

            return true;
        }

        // ── Placement ───────────────────────────────────────────────────

        private void PlaceLodge(Vector3 pos)
        {
            // Deduct cost
            if (_simulationRunner?.Sim?.State != null)
            {
                if (_simulationRunner.Sim.State.Money < _buildCost)
                {
                    NotificationManager.Instance?.ShowError($"Not enough money! Need ${_buildCost}");
                    return;
                }
                _simulationRunner.Sim.State.Money -= _buildCost;
            }

            // Instantiate the prefab
            GameObject lodgeObj = Instantiate(_lodgePrefab, pos, Quaternion.identity);
            lodgeObj.name = $"Lodge_{Time.frameCount}";

            // Add the runtime component automatically
            LodgeFacility facility = lodgeObj.AddComponent<LodgeFacility>();
            facility.Initialize(_treeClearRadius);

            // Add selectable structure component for management
            var selectable = lodgeObj.AddComponent<SelectableStructure>();
            selectable.InitializeAsLodge(facility);

            // Permanently clear trees
            TreeClearer.RestorePreviewTrees();
            TreeClearer.ClearTreesAroundPoint(pos, _treeClearRadius);

            // Register snap point so trails can connect
            if (_liftBuilder?.Connectivity != null)
            {
                var snap = new SnapPoint(
                    SnapPointType.BuildingEntrance,
                    MountainManager.ToVector3f(pos),
                    facility.GetInstanceID(),
                    $"Lodge_{facility.GetInstanceID()}"
                );
                _liftBuilder.Connectivity.Registry.Register(snap);
                _liftBuilder.Connectivity.RebuildConnections();
            }

            // Register with manager
            if (LodgeManager.Instance != null)
                LodgeManager.Instance.RegisterLodge(facility);
            else
                Debug.LogWarning("[LodgeBuilder] No LodgeManager in scene – add one!");

            NotificationManager.Instance?.ShowSuccess($"Lodge built! (${_buildCost})");
            Debug.Log($"[LodgeBuilder] Placed lodge at {pos}, cleared {_treeClearRadius}m radius");
        }

        // ── Cleanup ─────────────────────────────────────────────────────

        private void CleanupPreview()
        {
            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
                _previewInstance = null;
            }
            _previewRenderers = null;
            TreeClearer.RestorePreviewTrees();
        }
    }
}
