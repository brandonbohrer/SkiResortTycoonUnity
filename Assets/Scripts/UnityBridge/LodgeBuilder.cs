using UnityEngine;
using SkiResortTycoon.Core;
using SkiResortTycoon.UI;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Lodge placement tool.
    ///
    /// Flow:
    ///   Activate  → context window opens (cost, Confirm, Cancel)
    ///   Left-click on valid terrain → preview locks at that position (pending)
    ///   Confirm   → place lodge, deduct cost, switch to lodge-selected context window
    ///   Cancel    → destroy preview, exit build mode
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

        [Header("Snapping")]
        [SerializeField] private float _snapRadius = 10f;
        [SerializeField] private Color _snapColor = new Color(0f, 1f, 1f, 0.8f);

        [Header("Visual Feedback")]
        [SerializeField] private Color _validColor   = new Color(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color _invalidColor = new Color(1f, 0f, 0f, 0.5f);

        private GameObject  _previewInstance;
        private Renderer[]  _previewRenderers;
        private bool        _canPlace;
        private bool        _isPendingConfirmation;
        private Vector3     _pendingPosition;
        private MagneticCursor _magneticCursor;

        public override string ToolName        => "Lodge";
        public override string ToolDescription => "Place a lodge";

        // ── BaseTool overrides ───────────────────────────────────────────────

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

            if (_liftBuilder?.Connectivity != null)
                _magneticCursor = new MagneticCursor(_liftBuilder.Connectivity.Registry, _snapRadius);

            // Open context window immediately — preview follows cursor until first click
            ContextWindowController.Instance?.ShowLodgeBuildWindow(
                _buildCost,
                onConfirm: ConfirmLodge,
                onCancel:  CancelLodge);
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            _isPendingConfirmation = false;
            CleanupPreview();
            _magneticCursor = null;
        }

        public override void OnCancel()
        {
            CancelLodge();
        }

        // ── Preview ──────────────────────────────────────────────────────────

        protected override void ShowPreview()
        {
            if (_lodgePrefab == null) return;

            _previewInstance = Instantiate(_lodgePrefab);
            _previewInstance.name = "LodgePreview";

            foreach (var col in _previewInstance.GetComponentsInChildren<Collider>())
                Destroy(col);
            foreach (var fac in _previewInstance.GetComponentsInChildren<LodgeFacility>())
                Destroy(fac);

            _previewRenderers = _previewInstance.GetComponentsInChildren<Renderer>();
            foreach (var r in _previewRenderers)
            {
                Material mat = new Material(r.material);
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

            // Preview is frozen once the player clicks a position
            if (_isPendingConfirmation)
            {
                _previewInstance.transform.position = _pendingPosition;
                _previewInstance.SetActive(true);
                return;
            }

            Vector3? hit = _mountainManager?.RaycastMountain(_camera, Input.mousePosition);

            if (hit.HasValue)
            {
                Vector3 placementPos = hit.Value;

                if (_magneticCursor != null)
                {
                    SnapPointType[] validTypes = new[]
                    {
                        SnapPointType.TrailStart, SnapPointType.TrailEnd, SnapPointType.TrailPoint
                    };
                    _magneticCursor.Update(hit.Value, validTypes);

                    if (_magneticCursor.IsSnapped)
                    {
                        Vector3 snapPos   = _magneticCursor.SnappedPosition;
                        Vector3 dirFromSnap = (hit.Value - snapPos).normalized;
                        if (dirFromSnap.sqrMagnitude < 0.01f) dirFromSnap = Vector3.forward;
                        placementPos = snapPos + dirFromSnap * _treeClearRadius;
                        placementPos.y = hit.Value.y;
                    }
                }

                _previewInstance.transform.position = placementPos;
                _previewInstance.SetActive(true);

                _canPlace = IsValidPlacement(placementPos);

                Color tint;
                if (_magneticCursor != null && _magneticCursor.IsSnapped)
                    tint = _canPlace ? _snapColor : _invalidColor;
                else
                    tint = _canPlace ? _validColor : _invalidColor;

                foreach (var r in _previewRenderers)
                {
                    Color c = r.material.color;
                    c.r = tint.r; c.g = tint.g; c.b = tint.b;
                    r.material.color = c;
                }

                var pts = new System.Collections.Generic.List<Vector3> { placementPos, placementPos };
                TreeClearer.ClearTreesForPreview(pts, _treeClearRadius);
            }
            else
            {
                _previewInstance.SetActive(false);
                TreeClearer.RestorePreviewTrees();
            }
        }

        protected override void HidePreview() => CleanupPreview();

        // ── Input ────────────────────────────────────────────────────────────

        protected override void HandleInput()
        {
            // While pending, let only the context window buttons act
            if (_isPendingConfirmation)
            {
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                    CancelLodge();
                return;
            }

            base.HandleInput(); // right-click cancel

            if (IsMouseOverUI()) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (_previewInstance != null && _previewInstance.activeSelf && _canPlace)
                {
                    // Lock preview — wait for Confirm or Cancel
                    _pendingPosition       = _previewInstance.transform.position;
                    _isPendingConfirmation = true;
                    TreeClearer.RestorePreviewTrees();
                }
                else if (!_canPlace)
                {
                    NotificationManager.Instance?.ShowWarning("Cannot place lodge here!");
                }
            }
        }

        // ── Confirm / Cancel API ─────────────────────────────────────────────

        public void ConfirmLodge()
        {
            if (!_isPendingConfirmation)
            {
                // If no position locked yet, use current preview position
                if (_previewInstance == null || !_previewInstance.activeSelf || !_canPlace)
                {
                    NotificationManager.Instance?.ShowWarning("Place the lodge on the mountain first!");
                    return;
                }
                _pendingPosition = _previewInstance.transform.position;
            }

            var selectable = PlaceLodge(_pendingPosition);

            _isPendingConfirmation = false;
            CleanupPreview();
            UIManager.Instance?.DeactivateTool();

            if (selectable != null)
                ContextWindowController.Instance?.ShowStructure(selectable);
            else
                ContextWindowController.Instance?.Hide();
        }

        public void CancelLodge()
        {
            _isPendingConfirmation = false;
            CleanupPreview();
            UIManager.Instance?.DeactivateTool();
            ContextWindowController.Instance?.Hide();
        }

        // ── Validation ───────────────────────────────────────────────────────

        private bool IsValidPlacement(Vector3 pos)
        {
            if (_simulationRunner?.Sim?.State != null && _simulationRunner.Sim.State.Money < _buildCost)
                return false;

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

        // ── Placement ────────────────────────────────────────────────────────

        private SelectableStructure PlaceLodge(Vector3 pos)
        {
            if (_simulationRunner?.Sim?.State != null)
            {
                if (_simulationRunner.Sim.State.Money < _buildCost)
                {
                    NotificationManager.Instance?.ShowError($"Not enough money! Need ${_buildCost}");
                    return null;
                }
                _simulationRunner.Sim.State.Money -= _buildCost;
            }

            GameObject lodgeObj = Instantiate(_lodgePrefab, pos, Quaternion.identity);
            lodgeObj.name = $"Lodge_{Time.frameCount}";

            LodgeFacility facility = lodgeObj.AddComponent<LodgeFacility>();
            facility.Initialize(_treeClearRadius);

            var selectable = lodgeObj.AddComponent<SelectableStructure>();
            selectable.InitializeAsLodge(facility);

            TreeClearer.RestorePreviewTrees();
            TreeClearer.ClearTreesAroundPoint(pos, _treeClearRadius);

            if (_liftBuilder?.Connectivity != null)
            {
                RegisterFootprintSnapPoints(pos, facility);
                _liftBuilder.Connectivity.RebuildConnections();
            }

            if (LodgeManager.Instance != null)
                LodgeManager.Instance.RegisterLodge(facility);
            else
                Debug.LogWarning("[LodgeBuilder] No LodgeManager in scene – add one!");

            NotificationManager.Instance?.ShowSuccess($"Lodge built! (${_buildCost})");
            Debug.Log($"[LodgeBuilder] Placed lodge at {pos}");

            return selectable;
        }

        // ── Footprint Snap Points ────────────────────────────────────────────

        private void RegisterFootprintSnapPoints(Vector3 lodgeCenter, LodgeFacility facility)
        {
            float radius    = facility.FootprintRadius;
            int   ownerId   = facility.GetInstanceID();
            string ownerName = $"Lodge_{ownerId}";

            Vector3[] directions = new Vector3[]
            {
                Vector3.forward,
                (Vector3.forward + Vector3.right).normalized,
                Vector3.right,
                (-Vector3.forward + Vector3.right).normalized,
                -Vector3.forward,
                (-Vector3.forward - Vector3.right).normalized,
                -Vector3.right,
                (Vector3.forward - Vector3.right).normalized
            };

            foreach (var dir in directions)
            {
                Vector3 edgePos = lodgeCenter + dir * radius;
                edgePos.y = lodgeCenter.y;

                var snap = new SnapPoint(
                    SnapPointType.BuildingEntrance,
                    MountainManager.ToVector3f(edgePos),
                    ownerId,
                    ownerName);
                _liftBuilder.Connectivity.Registry.Register(snap);
            }
        }

        // ── Cleanup ──────────────────────────────────────────────────────────

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
