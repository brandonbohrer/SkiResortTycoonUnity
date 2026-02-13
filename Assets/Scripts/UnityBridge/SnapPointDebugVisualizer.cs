using UnityEngine;
using SkiResortTycoon.Core;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Optional debug visualizer for snap points and connections.
    /// Shows colored markers at snap points and lines for connections.
    /// </summary>
    public class SnapPointDebugVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LiftBuilder _liftBuilder;
        
        [Header("Visualization Settings")]
        [SerializeField] private bool _showSnapPoints = true;
        [SerializeField] private bool _showConnections = true;
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private float _snapPointSize = 0.3f;
        [SerializeField] private float _connectionLineWidth = 0.2f;
        
        [Header("Colors")]
        [SerializeField] private Color _liftBottomColor = Color.red;
        [SerializeField] private Color _liftTopColor = Color.cyan;
        [SerializeField] private Color _trailStartColor = Color.green;
        [SerializeField] private Color _trailEndColor = Color.yellow;
        [SerializeField] private Color _connectionColor = new Color(1f, 1f, 0f, 0.5f);
        
        private Dictionary<int, GameObject> _snapPointMarkers = new Dictionary<int, GameObject>();
        private Dictionary<string, LineRenderer> _connectionLines = new Dictionary<string, LineRenderer>();
        
        void LateUpdate()
        {
            if (_liftBuilder == null || _liftBuilder.Connectivity == null) return;
            
            if (_showSnapPoints)
            {
                UpdateSnapPointMarkers();
            }
            else
            {
                ClearSnapPointMarkers();
            }
            
            if (_showConnections)
            {
                UpdateConnectionLines();
            }
            else
            {
                ClearConnectionLines();
            }
        }
        
        private void UpdateSnapPointMarkers()
        {
            var registry = _liftBuilder.Connectivity.Registry;
            var allPoints = registry.GetAll();
            
            HashSet<int> activeIds = new HashSet<int>();
            
            foreach (var point in allPoints)
            {
                int hash = GetSnapPointHash(point);
                activeIds.Add(hash);
                
                if (!_snapPointMarkers.ContainsKey(hash))
                {
                    // Create new marker
                    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    marker.name = $"SnapPoint_{point.Type}_{point.OwnerId}";
                    marker.transform.SetParent(transform);
                    marker.transform.localScale = Vector3.one * _snapPointSize;
                    
                    // Remove collider (debug only)
                    Destroy(marker.GetComponent<Collider>());
                    
                    _snapPointMarkers[hash] = marker;
                }
                
                // Update position and color
                GameObject obj = _snapPointMarkers[hash];
                obj.transform.position = TileToWorldPos(point.Coord);
                
                var renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = GetColorForType(point.Type);
                }
            }
            
            // Remove markers for deleted snap points
            List<int> toRemove = new List<int>();
            foreach (var kvp in _snapPointMarkers)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    Destroy(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (int id in toRemove)
            {
                _snapPointMarkers.Remove(id);
            }
        }
        
        private void UpdateConnectionLines()
        {
            var connections = _liftBuilder.Connectivity.Connections.GetAllConnections();
            var registry = _liftBuilder.Connectivity.Registry;
            
            HashSet<string> activeKeys = new HashSet<string>();
            
            foreach (var conn in connections)
            {
                string key = $"{conn.FromType}{conn.FromId}_to_{conn.ToType}{conn.ToId}";
                activeKeys.Add(key);
                
                if (!_connectionLines.ContainsKey(key))
                {
                    // Create new line
                    GameObject lineObj = new GameObject($"Connection_{key}");
                    lineObj.transform.SetParent(transform);
                    LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                    
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startWidth = _connectionLineWidth;
                    lr.endWidth = _connectionLineWidth;
                    lr.startColor = _connectionColor;
                    lr.endColor = _connectionColor;
                    lr.useWorldSpace = true;
                    
                    _connectionLines[key] = lr;
                }
                
                // Update line positions
                LineRenderer lineRenderer = _connectionLines[key];
                
                // Find the snap points
                TileCoord? fromCoord = null;
                TileCoord? toCoord = null;
                
                if (conn.FromType == "Lift")
                {
                    var liftTops = registry.GetByType(SnapPointType.LiftTop);
                    foreach (var snap in liftTops)
                    {
                        if (snap.OwnerId == conn.FromId)
                        {
                            fromCoord = snap.Coord;
                            break;
                        }
                    }
                }
                
                if (conn.ToType == "Trail")
                {
                    var trailStarts = registry.GetByType(SnapPointType.TrailStart);
                    foreach (var snap in trailStarts)
                    {
                        if (snap.OwnerId == conn.ToId)
                        {
                            toCoord = snap.Coord;
                            break;
                        }
                    }
                }
                
                if (fromCoord.HasValue && toCoord.HasValue)
                {
                    lineRenderer.positionCount = 2;
                    lineRenderer.SetPosition(0, TileToWorldPos(fromCoord.Value));
                    lineRenderer.SetPosition(1, TileToWorldPos(toCoord.Value));
                }
            }
            
            // Remove lines for deleted connections
            List<string> toRemove = new List<string>();
            foreach (var kvp in _connectionLines)
            {
                if (!activeKeys.Contains(kvp.Key))
                {
                    Destroy(kvp.Value.gameObject);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (string key in toRemove)
            {
                _connectionLines.Remove(key);
            }
        }
        
        private void ClearSnapPointMarkers()
        {
            foreach (var kvp in _snapPointMarkers)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            _snapPointMarkers.Clear();
        }
        
        private void ClearConnectionLines()
        {
            foreach (var kvp in _connectionLines)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _connectionLines.Clear();
        }
        
        private int GetSnapPointHash(SnapPoint point)
        {
            // Simple hash combining type, owner, and coord
            return ((int)point.Type * 1000000) + (point.OwnerId * 1000) + (point.Coord.X * 100) + point.Coord.Y;
        }
        
        private Color GetColorForType(SnapPointType type)
        {
            switch (type)
            {
                case SnapPointType.LiftBottom: return _liftBottomColor;
                case SnapPointType.LiftTop: return _liftTopColor;
                case SnapPointType.TrailStart: return _trailStartColor;
                case SnapPointType.TrailEnd: return _trailEndColor;
                default: return Color.white;
            }
        }
        
        private Vector3 TileToWorldPos(TileCoord coord)
        {
            // Use MountainManager for proper 3D coordinates if available
            if (_liftBuilder != null)
            {
                var mountainField = typeof(LiftBuilder).GetField("_mountainManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mountainField != null)
                {
                    var mountain = mountainField.GetValue(_liftBuilder) as MountainManager;
                    if (mountain != null)
                    {
                        Vector3 pos = mountain.TileToWorldPos(coord);
                        pos.y += 0.5f; // Slight offset above terrain for visibility
                        return pos;
                    }
                }
            }
            
            float worldX = coord.X * _tileSize;
            float worldZ = coord.Y * _tileSize;
            return new Vector3(worldX, 0.5f, worldZ);
        }
        
        void OnGUI()
        {
            if (_liftBuilder == null || _liftBuilder.Connectivity == null) return;
            
            GUI.Box(new Rect(10, 330, 400, 100), "Snap Point Debug");
            GUI.Label(new Rect(20, 350, 380, 20), _liftBuilder.Connectivity.GetDebugInfo());
            
            _showSnapPoints = GUI.Toggle(new Rect(20, 390, 180, 20), _showSnapPoints, "Show Snap Points");
            _showConnections = GUI.Toggle(new Rect(210, 390, 180, 20), _showConnections, "Show Connections");
        }
        
        void OnDestroy()
        {
            ClearSnapPointMarkers();
            ClearConnectionLines();
        }
    }
}

