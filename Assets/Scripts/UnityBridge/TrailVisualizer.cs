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
        
        
        private Dictionary<int, List<LineRenderer>> _leftBoundarySegments = new Dictionary<int, List<LineRenderer>>();
        private Dictionary<int, List<LineRenderer>> _rightBoundarySegments = new Dictionary<int, List<LineRenderer>>();
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
            foreach (var kvp in _leftBoundarySegments)
            {
                bool found = false;
                foreach (var trail in _trailDrawer.TrailSystem.Trails)
                {
                    if (trail.TrailId == kvp.Key) { found = true; break; }
                }
                
                if (!found)
                {
                    if (_leftBoundarySegments.ContainsKey(kvp.Key))
                        foreach (var lr in _leftBoundarySegments[kvp.Key])
                            if (lr != null) Destroy(lr.gameObject);
                    if (_rightBoundarySegments.ContainsKey(kvp.Key))
                        foreach (var lr in _rightBoundarySegments[kvp.Key])
                            if (lr != null) Destroy(lr.gameObject);
                    if (_trailRootObjects.ContainsKey(kvp.Key) && _trailRootObjects[kvp.Key] != null)
                        Destroy(_trailRootObjects[kvp.Key]);
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (int id in toRemove)
            {
                _leftBoundarySegments.Remove(id);
                _rightBoundarySegments.Remove(id);
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
                
                // Boundaries as segments — only visible runs are drawn, hard cutoff at overlaps
                var allTrails = _trailDrawer.TrailSystem.Trails;
                var root = _trailRootObjects[trail.TrailId].transform;

                if (!_leftBoundarySegments.ContainsKey(trail.TrailId))
                    _leftBoundarySegments[trail.TrailId] = new List<LineRenderer>();
                if (!_rightBoundarySegments.ContainsKey(trail.TrailId))
                    _rightBoundarySegments[trail.TrailId] = new List<LineRenderer>();

                ApplyOverlapSegments(_leftBoundarySegments[trail.TrailId], trail.LeftBoundaryPoints, trail.TrailId, allTrails, trailColor, root, "Left");
                ApplyOverlapSegments(_rightBoundarySegments[trail.TrailId], trail.RightBoundaryPoints, trail.TrailId, allTrails, trailColor, root, "Right");
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
        
        /// <summary>
        /// Splits a boundary into segments — only draws visible runs (points outside
        /// other trails' corridors). Creates a hard cutoff at overlap boundaries.
        /// </summary>
        private void ApplyOverlapSegments(
            List<LineRenderer> segments,
            List<Vector3f> boundaryPoints,
            int ownTrailId,
            List<TrailData> allTrails,
            Color baseColor,
            Transform parent,
            string sideName)
        {
            if (boundaryPoints.Count < 2) return;

            // Mark points hidden if inside another trail's corridor
            bool[] hidden = new bool[boundaryPoints.Count];
            for (int i = 0; i < boundaryPoints.Count; i++)
            {
                var pt = boundaryPoints[i];
                foreach (var other in allTrails)
                {
                    if (other.TrailId == ownTrailId || !other.IsValid) continue;
                    if (other.WorldPathPoints == null || other.WorldPathPoints.Count < 2) continue;
                    float unused;
                    if (other.IsInsideCorridor(pt.X, pt.Z, out unused))
                    {
                        hidden[i] = true;
                        break;
                    }
                }
            }

            // Find visible runs (consecutive visible points, length >= 2)
            var runs = new List<(int start, int count)>();
            int runStart = -1;
            for (int i = 0; i < boundaryPoints.Count; i++)
            {
                if (!hidden[i])
                {
                    if (runStart < 0) runStart = i;
                }
                else
                {
                    if (runStart >= 0)
                    {
                        int count = i - runStart;
                        if (count >= 2) runs.Add((runStart, count));
                        runStart = -1;
                    }
                }
            }
            if (runStart >= 0)
            {
                int count = boundaryPoints.Count - runStart;
                if (count >= 2) runs.Add((runStart, count));
            }

            // Ensure we have enough segment LineRenderers
            while (segments.Count < runs.Count)
            {
                var go = new GameObject($"Boundary_{sideName}_Seg{segments.Count}");
                go.transform.SetParent(parent);
                var lr = go.AddComponent<LineRenderer>();
                ConfigureBoundaryRenderer(lr);
                segments.Add(lr);
            }

            // Assign each run to a segment
            for (int s = 0; s < runs.Count; s++)
            {
                var (start, count) = runs[s];
                var lr = segments[s];
                lr.enabled = true;
                lr.startColor = baseColor;
                lr.endColor = baseColor;

                var runPoints = new List<Vector3f>();
                for (int i = start; i < start + count; i++)
                    runPoints.Add(boundaryPoints[i]);
                UpdateLinePositions(lr, runPoints);
            }

            // Disable excess segments
            for (int s = runs.Count; s < segments.Count; s++)
                segments[s].enabled = false;
        }

        void OnDestroy()
        {
            foreach (var kvp in _leftBoundarySegments)
                foreach (var lr in kvp.Value)
                    if (lr != null) Destroy(lr.gameObject);
            
            foreach (var kvp in _rightBoundarySegments)
                foreach (var lr in kvp.Value)
                    if (lr != null) Destroy(lr.gameObject);
            
            foreach (var kvp in _trailRootObjects)
                if (kvp.Value != null) Destroy(kvp.Value);
            
            if (_currentTrailRenderer != null)
                Destroy(_currentTrailRenderer.gameObject);
        }
    }
}

