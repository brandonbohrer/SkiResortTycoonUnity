using UnityEngine;
using SkiResortTycoon.Core;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Visualizes lifts.  When a <see cref="LiftPrefabBuilder"/> is present
    /// the full 3D prefab system is used; otherwise falls back to simple
    /// <see cref="LineRenderer"/> lines.
    /// Also draws a preview line while the user is placing a lift.
    /// </summary>
    public class LiftVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private MountainManager _gridRenderer;
        
        [Header("Fallback Line Settings (used when no PrefabBuilder)")]
        [SerializeField] private float _lineWidth = 0.8f;
        [SerializeField] private Color _liftColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color _previewColor = new Color(1f, 1f, 0f, 1f);
        
        private Dictionary<int, LineRenderer> _liftRenderers = new Dictionary<int, LineRenderer>();
        private LineRenderer _previewRenderer;
        
        /// <summary>True when a LiftPrefabBuilder is active (3D models replace lines).</summary>
        private bool UsePrefabs => _liftBuilder != null && _liftBuilder.PrefabBuilder != null;
        
        void LateUpdate()
        {
            if (_liftBuilder == null || _liftBuilder.LiftSystem == null) return;
            
            if (!UsePrefabs)
            {
                UpdateFallbackLines();
            }
            
            UpdatePreview();
        }
        
        // ── Fallback line rendering (only when no PrefabBuilder) ────────
        
        private void UpdateFallbackLines()
        {
            // Remove renderers for deleted lifts
            List<int> toRemove = new List<int>();
            foreach (var kvp in _liftRenderers)
            {
                bool found = false;
                foreach (var lift in _liftBuilder.LiftSystem.Lifts)
                {
                    if (lift.LiftId == kvp.Key) { found = true; break; }
                }
                if (!found)
                {
                    Destroy(kvp.Value.gameObject);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (int id in toRemove) _liftRenderers.Remove(id);
            
            // Create/update renderers for all lifts
            foreach (var lift in _liftBuilder.LiftSystem.Lifts)
            {
                if (!lift.IsValid) continue;
                
                if (!_liftRenderers.ContainsKey(lift.LiftId))
                {
                    GameObject liftObj = new GameObject($"Lift_{lift.LiftId}");
                    liftObj.transform.SetParent(transform);
                    LineRenderer lr = liftObj.AddComponent<LineRenderer>();
                    
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startWidth = _lineWidth;
                    lr.endWidth = _lineWidth;
                    lr.useWorldSpace = true;
                    lr.textureMode = LineTextureMode.Tile;
                    lr.sortingLayerName = "Default";
                    lr.sortingOrder = 32766;
                    lr.startColor = _liftColor;
                    lr.endColor = _liftColor;
                    
                    _liftRenderers[lift.LiftId] = lr;
                }
                
                LineRenderer lineRenderer = _liftRenderers[lift.LiftId];
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, MountainManager.ToUnityVector3(lift.StartPosition));
                lineRenderer.SetPosition(1, MountainManager.ToUnityVector3(lift.EndPosition));
            }
        }
        
        // ── Preview line (shown during lift placement) ──────────────────
        
        private void UpdatePreview()
        {
            // The 3D preview is handled by LiftPrefabBuilder.UpdatePreview,
            // but we still show a thin guide line for clarity.
            if (_liftBuilder.IsBuildMode && _liftBuilder.HasBottomStation && _liftBuilder.BottomWorldPosition.HasValue)
            {
                if (_previewRenderer == null)
                {
                    GameObject obj = new GameObject("LiftPreviewLine");
                    obj.transform.SetParent(transform);
                    _previewRenderer = obj.AddComponent<LineRenderer>();
                    
                    _previewRenderer.material = new Material(Shader.Find("Sprites/Default"));
                    _previewRenderer.startWidth = _lineWidth * 0.5f;
                    _previewRenderer.endWidth = _lineWidth * 0.5f;
                    _previewRenderer.useWorldSpace = true;
                    _previewRenderer.sortingOrder = 32767;
                    _previewRenderer.startColor = _previewColor;
                    _previewRenderer.endColor = _previewColor;
                }
                
                _previewRenderer.gameObject.SetActive(true);
                _previewRenderer.positionCount = 2;
                _previewRenderer.SetPosition(0, _liftBuilder.BottomWorldPosition.Value);
                
                // Get current mouse position on mountain
                Vector3? mousePos = _gridRenderer != null
                    ? _gridRenderer.RaycastMountain(Camera.main, Input.mousePosition)
                    : null;
                
                if (mousePos.HasValue)
                {
                    _previewRenderer.SetPosition(1, mousePos.Value);
                }
                else
                {
                    _previewRenderer.SetPosition(1, _liftBuilder.BottomWorldPosition.Value);
                }
            }
            else
            {
                if (_previewRenderer != null)
                    _previewRenderer.gameObject.SetActive(false);
            }
        }
        
        void OnDestroy()
        {
            foreach (var kvp in _liftRenderers)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            if (_previewRenderer != null) Destroy(_previewRenderer.gameObject);
        }
    }
}
