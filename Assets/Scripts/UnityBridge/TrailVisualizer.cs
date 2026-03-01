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
        [SerializeField] private MountainManager _gridRenderer;
        
        [Header("Visual Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private float _lineWidth = 0.8f; // Much wider for visibility!
        [SerializeField] private float _trailHeightOffset = 0.3f; // Small offset above terrain to avoid z-fighting
        
        [Header("Difficulty Colors")]
        [SerializeField] private Color _colorGreen = new Color(0.1f, 1f, 0.1f, 1f); // Bright green
        [SerializeField] private Color _colorBlue = new Color(0.1f, 0.5f, 1f, 1f); // Bright blue
        [SerializeField] private Color _colorBlack = new Color(0.0f, 0.0f, 0.0f, 1f); // Pure black
        [SerializeField] private Color _colorDoubleBlack = new Color(0.5f, 0.0f, 0.0f, 1f); // Dark red for double-black
        [SerializeField] private Color _colorDrawing = new Color(1f, 1f, 0f, 1f); // Bright yellow
        
        [Header("Boundary Line Width")]
        [SerializeField] private float _boundaryLineWidth = 0.5f;
        
        
        private Dictionary<int, LineRenderer> _leftBoundaryRenderers = new Dictionary<int, LineRenderer>();
        private Dictionary<int, LineRenderer> _rightBoundaryRenderers = new Dictionary<int, LineRenderer>();
        // Legacy centerline renderers kept only for the SelectableStructure reference
        private Dictionary<int, GameObject> _trailRootObjects = new Dictionary<int, GameObject>();
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
            foreach (var kvp in _leftBoundaryRenderers)
            {
                if (kvp.Value == null)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }
                
                bool found = false;
                foreach (var trail in _trailDrawer.TrailSystem.Trails)
                {
                    if (trail.TrailId == kvp.Key) { found = true; break; }
                }
                
                if (!found)
                {
                    if (_leftBoundaryRenderers.ContainsKey(kvp.Key) && _leftBoundaryRenderers[kvp.Key] != null)
                        Destroy(_leftBoundaryRenderers[kvp.Key].gameObject);
                    if (_rightBoundaryRenderers.ContainsKey(kvp.Key) && _rightBoundaryRenderers[kvp.Key] != null)
                        Destroy(_rightBoundaryRenderers[kvp.Key].gameObject);
                    if (_trailRootObjects.ContainsKey(kvp.Key) && _trailRootObjects[kvp.Key] != null)
                        Destroy(_trailRootObjects[kvp.Key]);
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (int id in toRemove)
            {
                _leftBoundaryRenderers.Remove(id);
                _rightBoundaryRenderers.Remove(id);
                _trailRootObjects.Remove(id);
            }
            
            // Create/update renderers for all trails — boundary lines are the primary visual
            foreach (var trail in _trailDrawer.TrailSystem.Trails)
            {
                if (!trail.IsValid) continue;
                if (trail.LeftBoundaryPoints.Count == 0 || trail.RightBoundaryPoints.Count == 0)
                    continue;
                
                Color trailColor = GetDifficultyColor(trail.Difficulty);
                
                // Root object for SelectableStructure (no visible renderer)
                if (!_trailRootObjects.ContainsKey(trail.TrailId))
                {
                    GameObject rootObj = new GameObject($"Trail_{trail.TrailId}");
                    rootObj.transform.SetParent(transform);
                    var selectable = rootObj.AddComponent<SelectableStructure>();
                    // Pass null LineRenderer — selection uses the root GO
                    selectable.InitializeAsTrail(trail, null);
                    _trailRootObjects[trail.TrailId] = rootObj;
                }
                
                // Left boundary
                if (!_leftBoundaryRenderers.ContainsKey(trail.TrailId))
                {
                    GameObject leftObj = new GameObject($"Trail_{trail.TrailId}_LeftBoundary");
                    leftObj.transform.SetParent(transform);
                    LineRenderer leftLr = leftObj.AddComponent<LineRenderer>();
                    ConfigureBoundaryRenderer(leftLr);
                    _leftBoundaryRenderers[trail.TrailId] = leftLr;
                }
                LineRenderer leftRenderer = _leftBoundaryRenderers[trail.TrailId];
                leftRenderer.startColor = trailColor;
                leftRenderer.endColor = trailColor;
                leftRenderer.enabled = true;
                UpdateLinePositions(leftRenderer, trail.LeftBoundaryPoints);
                
                // Right boundary
                if (!_rightBoundaryRenderers.ContainsKey(trail.TrailId))
                {
                    GameObject rightObj = new GameObject($"Trail_{trail.TrailId}_RightBoundary");
                    rightObj.transform.SetParent(transform);
                    LineRenderer rightLr = rightObj.AddComponent<LineRenderer>();
                    ConfigureBoundaryRenderer(rightLr);
                    _rightBoundaryRenderers[trail.TrailId] = rightLr;
                }
                LineRenderer rightRenderer = _rightBoundaryRenderers[trail.TrailId];
                rightRenderer.startColor = trailColor;
                rightRenderer.endColor = trailColor;
                rightRenderer.enabled = true;
                UpdateLinePositions(rightRenderer, trail.RightBoundaryPoints);
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
                }
                
                _currentTrailRenderer.startColor = _colorDrawing;
                _currentTrailRenderer.endColor = _colorDrawing;
                UpdateLinePositions(_currentTrailRenderer, _trailDrawer.CurrentTrail.WorldPathPoints);
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
        
        /// <summary>
        /// Updates line positions from world-space Vector3f points.
        /// Adds a small height offset to avoid z-fighting with terrain.
        /// </summary>
        private void UpdateLinePositions(LineRenderer lineRenderer, List<Vector3f> points)
        {
            lineRenderer.positionCount = points.Count;
            
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 pos = MountainManager.ToUnityVector3(points[i]);
                pos.y += _trailHeightOffset;
                lineRenderer.SetPosition(i, pos);
            }
        }
        
        private Vector3 TileToWorldPos(TileCoord coord)
        {
            if (_gridRenderer != null)
            {
                Vector3 pos = _gridRenderer.TileToWorldPos(coord);
                pos.y += _trailHeightOffset; // Slight offset above terrain to avoid z-fighting
                return pos;
            }
            
            float worldX = coord.X * _tileSize;
            float worldZ = coord.Y * _tileSize;
            return new Vector3(worldX, _trailHeightOffset, worldZ);
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
        
        /// <summary>
        /// Configures a LineRenderer for boundary visualization.
        /// </summary>
        private void ConfigureBoundaryRenderer(LineRenderer lr)
        {
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = _boundaryLineWidth;
            lr.endWidth = _boundaryLineWidth;
            lr.useWorldSpace = true;
            lr.textureMode = LineTextureMode.Tile;
        }
        
        void OnDestroy()
        {
            foreach (var kvp in _leftBoundaryRenderers)
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            
            foreach (var kvp in _rightBoundaryRenderers)
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            
            foreach (var kvp in _trailRootObjects)
                if (kvp.Value != null) Destroy(kvp.Value);
            
            if (_currentTrailRenderer != null)
                Destroy(_currentTrailRenderer.gameObject);
        }
    }
}

