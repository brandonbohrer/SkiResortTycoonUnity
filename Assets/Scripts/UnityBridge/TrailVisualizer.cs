using UnityEngine;
using SkiResortTycoon.Core;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Visualizes trails on the terrain using LineRenderer components.
    /// </summary>
    public class TrailVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TrailDrawer _trailDrawer;
        [SerializeField] private GridDebugRenderer _gridRenderer;
        
        [Header("Visual Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private float _lineWidth = 0.3f;
        [SerializeField] private float _lineZOffset = -1f; // Closer to camera than terrain
        
        [Header("Difficulty Colors")]
        [SerializeField] private Color _colorGreen = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _colorBlue = new Color(0.2f, 0.4f, 1f);
        [SerializeField] private Color _colorBlack = new Color(0.1f, 0.1f, 0.1f);
        [SerializeField] private Color _colorDoubleBlack = new Color(0.0f, 0.0f, 0.0f);
        [SerializeField] private Color _colorDrawing = new Color(1f, 1f, 0.2f, 1f);
        
        private Dictionary<int, LineRenderer> _trailRenderers = new Dictionary<int, LineRenderer>();
        private LineRenderer _currentTrailRenderer;
        
        void LateUpdate()
        {
            if (_trailDrawer == null || _trailDrawer.TrailSystem == null) return;
            
            // Update all completed trails
            UpdateCompletedTrails();
            
            // Update current trail being drawn
            UpdateCurrentTrail();
        }
        
        private void UpdateCompletedTrails()
        {
            // Remove renderers for deleted trails
            List<int> toRemove = new List<int>();
            foreach (var kvp in _trailRenderers)
            {
                bool found = false;
                foreach (var trail in _trailDrawer.TrailSystem.Trails)
                {
                    if (trail.TrailId == kvp.Key)
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
                _trailRenderers.Remove(id);
            }
            
            // Create/update renderers for all trails
            foreach (var trail in _trailDrawer.TrailSystem.Trails)
            {
                if (!trail.IsValid) continue;
                
                if (!_trailRenderers.ContainsKey(trail.TrailId))
                {
                    // Create new renderer
                    GameObject trailObj = new GameObject($"Trail_{trail.TrailId}");
                    trailObj.transform.SetParent(transform);
                    LineRenderer lr = trailObj.AddComponent<LineRenderer>();
                    
                    // Configure line renderer - simpler approach
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startWidth = _lineWidth;
                    lr.endWidth = _lineWidth;
                    lr.useWorldSpace = true;
                    lr.textureMode = LineTextureMode.Tile;
                    
                    // Use VERY HIGH sorting order to be above ALL terrain
                    lr.sortingLayerName = "Default";
                    lr.sortingOrder = 32767; // Maximum short value - highest possible
                    
                    _trailRenderers[trail.TrailId] = lr;
                }
                
                // Update line - SET COLOR BASED ON ACTUAL DIFFICULTY
                LineRenderer lineRenderer = _trailRenderers[trail.TrailId];
                Color trailColor = GetDifficultyColor(trail.Difficulty);
                lineRenderer.startColor = trailColor;
                lineRenderer.endColor = trailColor;
                lineRenderer.enabled = true; // Make sure it's enabled
                UpdateLinePositions(lineRenderer, trail.PathPoints);
            }
        }
        
        private void UpdateCurrentTrail()
        {
            if (_trailDrawer.IsDrawing && _trailDrawer.CurrentTrail != null)
            {
                if (_currentTrailRenderer == null)
                {
                    GameObject obj = new GameObject("CurrentTrail");
                    obj.transform.SetParent(transform);
                    _currentTrailRenderer = obj.AddComponent<LineRenderer>();
                    
                    _currentTrailRenderer.material = new Material(Shader.Find("Sprites/Default"));
                    _currentTrailRenderer.startWidth = _lineWidth * 1.5f;
                    _currentTrailRenderer.endWidth = _lineWidth * 1.5f;
                    _currentTrailRenderer.useWorldSpace = true;
                    _currentTrailRenderer.textureMode = LineTextureMode.Tile;
                    _currentTrailRenderer.sortingLayerName = "Default";
                    _currentTrailRenderer.sortingOrder = 32767; // Max value
                }
                
                _currentTrailRenderer.startColor = _colorDrawing;
                _currentTrailRenderer.endColor = _colorDrawing;
                UpdateLinePositions(_currentTrailRenderer, _trailDrawer.CurrentTrail.PathPoints);
                _currentTrailRenderer.gameObject.SetActive(true);
            }
            else
            {
                if (_currentTrailRenderer != null)
                {
                    _currentTrailRenderer.gameObject.SetActive(false);
                }
            }
        }
        
        private void UpdateLinePositions(LineRenderer lineRenderer, List<TileCoord> points)
        {
            lineRenderer.positionCount = points.Count;
            
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 worldPos = TileToWorldPos(points[i]);
                lineRenderer.SetPosition(i, worldPos);
            }
        }
        
        private Vector3 TileToWorldPos(TileCoord coord)
        {
            float worldX = coord.X * _tileSize;
            float worldY = coord.Y * _tileSize;
            
            // Add height offset if terrain available
            if (_gridRenderer != null && _gridRenderer.TerrainData != null)
            {
                int height = _gridRenderer.TerrainData.GetHeight(coord);
                worldY += height * 0.1f; // Match heightScale
            }
            
            // Use Z = -5 to be in front of terrain (terrain is at z=0)
            return new Vector3(worldX, worldY, -5f);
        }
        
        private Color GetDifficultyColor(TrailDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TrailDifficulty.Green:
                    return _colorGreen;
                case TrailDifficulty.Blue:
                    return _colorBlue;
                case TrailDifficulty.Black:
                    return _colorBlack;
                case TrailDifficulty.DoubleBlack:
                    return _colorDoubleBlack;
                default:
                    return Color.white;
            }
        }
        
        void OnDestroy()
        {
            // Clean up all renderers
            foreach (var kvp in _trailRenderers)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            
            if (_currentTrailRenderer != null)
            {
                Destroy(_currentTrailRenderer.gameObject);
            }
        }
    }
}

