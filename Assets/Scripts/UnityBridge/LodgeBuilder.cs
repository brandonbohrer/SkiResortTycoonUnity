using UnityEngine;
using System.Collections.Generic;
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
    ///
    /// Snapping: the lodge preview hugs the outside edge of the nearest trail.
    /// It auto-rotates to align with the trail and turns red if it would overlap.
    /// </summary>
    public class LodgeBuilder : BaseTool
    {
        [Header("Lodge Prefab (drag your model here)")]
        [SerializeField] private GameObject _lodgePrefab;

        [Header("References")]
        [SerializeField] private MountainManager _mountainManager;
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private TrailDrawer _trailDrawer;
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private Camera _camera;

        [Header("Build Settings")]
        [SerializeField] private float _treeClearRadius = 15f;
        [SerializeField] private int _buildCost = 25000;

        [Header("Rotation")]
        [SerializeField] private float _rotationSpeed = 90f;

        [Header("Trail Snapping")]
        [SerializeField] private float _trailSnapRadius = 30f;
        [SerializeField] private float _lodgeEdgeOffset = 10f;
        [SerializeField] private float _trailOverlapClearance = 2f;
        [SerializeField] private Color _snapColor = new Color(0f, 1f, 1f, 0.8f);

        [Header("Visual Feedback")]
        [SerializeField] private Color _validColor   = new Color(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color _invalidColor = new Color(1f, 0f, 0f, 0.5f);

        private GameObject  _previewInstance;
        private Renderer[]  _previewRenderers;
        private bool        _canPlace;
        private bool        _isPendingConfirmation;
        private Vector3     _pendingPosition;
        private Quaternion  _pendingRotation;
        private float       _rotationAngle;
        private bool        _isSnappedToTrail;

        public override string ToolName        => "Lodge";
        public override string ToolDescription => "Place a lodge";

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

            if (_isPendingConfirmation)
            {
                _previewInstance.transform.position = _pendingPosition;
                _previewInstance.transform.rotation = _pendingRotation;
                _previewInstance.SetActive(true);
                return;
            }

            Vector3? hit = _mountainManager?.RaycastMountain(_camera, Input.mousePosition);

            if (hit.HasValue)
            {
                Vector3 placementPos = hit.Value;
                float autoAngle = 0f;
                _isSnappedToTrail = false;

                if (_trailDrawer != null && _trailDrawer.TrailSystem != null)
                {
                    placementPos = ComputeTrailSnappedPosition(hit.Value, out autoAngle);
                }

                float finalAngle = _isSnappedToTrail ? (autoAngle + _rotationAngle) : _rotationAngle;
                Quaternion rot = Quaternion.Euler(0f, finalAngle, 0f);

                _previewInstance.transform.position = placementPos;
                _previewInstance.transform.rotation = rot;
                _previewInstance.SetActive(true);

                _canPlace = IsValidPlacement(placementPos);

                Color tint;
                if (_isSnappedToTrail)
                    tint = _canPlace ? _snapColor : _invalidColor;
                else
                    tint = _canPlace ? _validColor : _invalidColor;

                foreach (var r in _previewRenderers)
                {
                    Color c = r.material.color;
                    c.r = tint.r; c.g = tint.g; c.b = tint.b;
                    r.material.color = c;
                }

                var pts = new List<Vector3> { placementPos, placementPos };
                TreeClearer.ClearTreesForPreview(pts, _treeClearRadius);
            }
            else
            {
                _previewInstance.SetActive(false);
                TreeClearer.RestorePreviewTrees();
            }
        }

        protected override void HidePreview() => CleanupPreview();

        // ── Trail Snapping ─────────────────────────────────────────────────

        private Vector3 ComputeTrailSnappedPosition(Vector3 cursorHit, out float autoAngle)
        {
            autoAngle = 0f;
            var allTrails = _trailDrawer.TrailSystem.GetAllTrails();
            if (allTrails == null || allTrails.Count == 0)
                return cursorHit;

            TrailData bestTrail = null;
            float bestDistSq = _trailSnapRadius * _trailSnapRadius;
            Vector3f bestClosest = default;
            Vector3f bestTangent = default;
            Vector3f bestPerp = default;
            int bestSegIdx = 0;
            float bestSegT = 0f;

            foreach (var trail in allTrails)
            {
                if (!trail.IsValid || trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                    continue;

                Vector3f closest, tangent, perp;
                int segIdx;
                float segT, distSq;
                trail.FindClosestPointOnPath(cursorHit.x, cursorHit.z,
                    out closest, out tangent, out perp, out segIdx, out segT, out distSq);

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestTrail = trail;
                    bestClosest = closest;
                    bestTangent = tangent;
                    bestPerp = perp;
                    bestSegIdx = segIdx;
                    bestSegT = segT;
                }
            }

            if (bestTrail == null)
                return cursorHit;

            _isSnappedToTrail = true;

            float dx = cursorHit.x - bestClosest.X;
            float dz = cursorHit.z - bestClosest.Z;
            float side = dx * bestPerp.X + dz * bestPerp.Z;
            float pushSign = side >= 0f ? 1f : -1f;

            Vector3 boundaryPoint;
            if (bestTrail.LeftBoundaryPoints != null &&
                bestTrail.LeftBoundaryPoints.Count == bestTrail.WorldPathPoints.Count &&
                bestTrail.RightBoundaryPoints.Count == bestTrail.WorldPathPoints.Count)
            {
                var boundaryList = pushSign >= 0f
                    ? bestTrail.LeftBoundaryPoints
                    : bestTrail.RightBoundaryPoints;

                var bA = boundaryList[bestSegIdx];
                int nextIdx = bestSegIdx + 1;
                if (nextIdx >= boundaryList.Count) nextIdx = boundaryList.Count - 1;
                var bB = boundaryList[nextIdx];

                boundaryPoint = new Vector3(
                    bA.X + (bB.X - bA.X) * bestSegT,
                    bA.Y + (bB.Y - bA.Y) * bestSegT,
                    bA.Z + (bB.Z - bA.Z) * bestSegT
                );
            }
            else
            {
                float halfW = bestTrail.TrailWidth * 0.5f;
                boundaryPoint = new Vector3(
                    bestClosest.X + bestPerp.X * halfW * pushSign,
                    bestClosest.Y,
                    bestClosest.Z + bestPerp.Z * halfW * pushSign
                );
            }

            Vector3 placementPos = new Vector3(
                boundaryPoint.x + bestPerp.X * _lodgeEdgeOffset * pushSign,
                cursorHit.y,
                boundaryPoint.z + bestPerp.Z * _lodgeEdgeOffset * pushSign
            );

            float tangentAngle = Mathf.Atan2(bestTangent.X, bestTangent.Z) * Mathf.Rad2Deg;
            autoAngle = tangentAngle + 90f;

            return placementPos;
        }

        // ── Input ────────────────────────────────────────────────────────────

        protected override void HandleInput()
        {
            if (Input.GetKey(KeyCode.R))
            {
                _rotationAngle -= _rotationSpeed * Time.deltaTime;
            }

            if (_isPendingConfirmation)
            {
                if (Input.GetKey(KeyCode.R))
                {
                    _pendingRotation = Quaternion.Euler(0f, _pendingRotation.eulerAngles.y - _rotationSpeed * Time.deltaTime, 0f);
                }
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                    CancelLodge();
                return;
            }

            base.HandleInput();

            if (IsMouseOverUI()) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (_previewInstance != null && _previewInstance.activeSelf && _canPlace)
                {
                    _pendingPosition       = _previewInstance.transform.position;
                    _pendingRotation       = _previewInstance.transform.rotation;
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
                if (_previewInstance == null || !_previewInstance.activeSelf || !_canPlace)
                {
                    NotificationManager.Instance?.ShowWarning("Place the lodge on the mountain first!");
                    return;
                }
                _pendingPosition = _previewInstance.transform.position;
                _pendingRotation = _previewInstance.transform.rotation;
            }

            var selectable = PlaceLodge(_pendingPosition, _pendingRotation);

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

            if (_trailDrawer != null && _trailDrawer.TrailSystem != null)
            {
                foreach (var trail in _trailDrawer.TrailSystem.GetAllTrails())
                {
                    if (!trail.IsValid || trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                        continue;
                    float unused;
                    if (trail.IsInsideCorridor(pos.x, pos.z, out unused))
                        return false;
                }
            }

            return true;
        }

        // ── Placement ────────────────────────────────────────────────────────

        private SelectableStructure PlaceLodge(Vector3 pos, Quaternion rotation)
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

            GameObject lodgeObj = Instantiate(_lodgePrefab, pos, rotation);
            lodgeObj.name = $"Lodge_{Time.frameCount}";

            LodgeFacility facility = lodgeObj.AddComponent<LodgeFacility>();
            facility.Initialize(_treeClearRadius);

            var selectable = lodgeObj.AddComponent<SelectableStructure>();
            selectable.InitializeAsLodge(facility);

            TreeClearer.RestorePreviewTrees();
            TreeClearer.ClearTreesAroundPoint(pos, _treeClearRadius);

            if (_liftBuilder?.Connectivity != null)
            {
                RegisterFootprintSnapPoints(pos, rotation, facility);
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

        private void RegisterFootprintSnapPoints(Vector3 lodgeCenter, Quaternion rotation, LodgeFacility facility)
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
                Vector3 rotatedDir = rotation * dir;
                Vector3 edgePos = lodgeCenter + rotatedDir * radius;
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
