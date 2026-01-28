using UnityEngine;
using SkiResortTycoon.Core;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Visualizes lifts as simple lines (always above terrain).
    /// </summary>
    public class LiftVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private GridDebugRenderer _gridRenderer;
        
        [Header("Visual Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private float _lineWidth = 0.4f;
        [SerializeField] private Color _liftColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Dark gray
        [SerializeField] private Color _previewColor = new Color(1f, 1f, 0f, 0.7f); // Yellow preview
        
        private Dictionary<int, LineRenderer> _liftRenderers = new Dictionary<int, LineRenderer>();
        private LineRenderer _previewRenderer;
        
        void LateUpdate()
        {
            if (_liftBuilder == null || _liftBuilder.LiftSystem == null) return;
            
            UpdateCompletedLifts();
            UpdatePreview();
        }
        
        private void UpdateCompletedLifts()
        {
            // Remove renderers for deleted lifts
            List<int> toRemove = new List<int>();
            foreach (var kvp in _liftRenderers)
            {
                bool found = false;
                foreach (var lift in _liftBuilder.LiftSystem.Lifts)
                {
                    if (lift.LiftId == kvp.Key)
                    {
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    Destroy(kvp.Value.gameObject);
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (int id in toRemove)
            {
                _liftRenderers.Remove(id);
            }
            
            // Create/update renderers for all lifts
            foreach (var lift in _liftBuilder.LiftSystem.Lifts)
            {
                if (!lift.IsValid) continue;
                
                if (!_liftRenderers.ContainsKey(lift.LiftId))
                {
                    // Create new renderer
                    GameObject liftObj = new GameObject($"Lift_{lift.LiftId}");
                    liftObj.transform.SetParent(transform);
                    LineRenderer lr = liftObj.AddComponent<LineRenderer>();
                    
                    // Configure
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startWidth = _lineWidth;
                    lr.endWidth = _lineWidth;
                    lr.useWorldSpace = true;
                    lr.textureMode = LineTextureMode.Tile;
                    lr.sortingLayerName = "Default";
                    lr.sortingOrder = 32766; // Just below trails
                    lr.startColor = _liftColor;
                    lr.endColor = _liftColor;
                    
                    _liftRenderers[lift.LiftId] = lr;
                }
                
                // Update line positions
                LineRenderer lineRenderer = _liftRenderers[lift.LiftId];
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, TileToWorldPos(lift.BottomStation));
                lineRenderer.SetPosition(1, TileToWorldPos(lift.TopStation));
            }
        }
        
        private void UpdatePreview()
        {
            // Show preview line when placing lift
            if (_liftBuilder.IsBuildMode)
            {
                // Create preview renderer if needed
                if (_previewRenderer == null)
                {
                    GameObject obj = new GameObject("LiftPreview");
                    obj.transform.SetParent(transform);
                    _previewRenderer = obj.AddComponent<LineRenderer>();
                    
                    _previewRenderer.material = new Material(Shader.Find("Sprites/Default"));
                    _previewRenderer.startWidth = _lineWidth * 1.2f;
                    _previewRenderer.endWidth = _lineWidth * 1.2f;
                    _previewRenderer.useWorldSpace = true;
                    _previewRenderer.textureMode = LineTextureMode.Tile;
                    _previewRenderer.sortingLayerName = "Default";
                    _previewRenderer.sortingOrder = 32767;
                    _previewRenderer.startColor = _previewColor;
                    _previewRenderer.endColor = _previewColor;
                }
                
                _previewRenderer.gameObject.SetActive(true);
                
                // If we have bottom station, show preview to mouse
                // This would need access to LiftBuilder's internal state
                // For now, just hide it
                _previewRenderer.gameObject.SetActive(false);
            }
            else
            {
                if (_previewRenderer != null)
                {
                    _previewRenderer.gameObject.SetActive(false);
                }
            }
        }
        
        private Vector3 TileToWorldPos(TileCoord coord)
        {
            float worldX = coord.X * _tileSize;
            float worldY = coord.Y * _tileSize;
            
            // Add height offset
            if (_gridRenderer != null && _gridRenderer.TerrainData != null)
            {
                int height = _gridRenderer.TerrainData.GetHeight(coord);
                worldY += height * 0.1f;
            }
            
            return new Vector3(worldX, worldY, -5f);
        }
        
        void OnDestroy()
        {
            // Clean up
            foreach (var kvp in _liftRenderers)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            
            if (_previewRenderer != null)
            {
                Destroy(_previewRenderer.gameObject);
            }
        }
    }
}

