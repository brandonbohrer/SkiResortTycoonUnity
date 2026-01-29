using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Simple script to place the base spawn point.
    /// Press 'B' to place base at clicked tile.
    /// </summary>
    public class BasePlacer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private Camera _camera;
        
        [Header("Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private KeyCode _placeKey = KeyCode.B;
        
        private bool _basePlaced = false;
        private TileCoord _baseLocation;
        
        void Start()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }
        
        void Update()
        {
            if (_liftBuilder == null || _liftBuilder.Connectivity == null)
                return;
            
            // Press B to place base
            if (Input.GetKeyDown(_placeKey) && !_basePlaced)
            {
                TileCoord? coord = GetTileUnderMouse();
                if (coord.HasValue)
                {
                    PlaceBase(coord.Value);
                }
            }
        }
        
        private void PlaceBase(TileCoord location)
        {
            _baseLocation = location;
            _basePlaced = true;
            
            // Register base snap point
            var baseSnap = new SnapPoint(
                SnapPointType.BaseSpawn,
                location,
                0,  // Base has ID 0
                "Base Lodge"
            );
            
            _liftBuilder.Connectivity.Registry.Register(baseSnap);
            _liftBuilder.Connectivity.RebuildConnections();
            
            Debug.Log($"=== BASE PLACED ===");
            Debug.Log($"Location: {location}");
            Debug.Log($"Base spawn point registered!");
            Debug.Log($"Build lifts near this location to connect to base");
            Debug.Log($"===================");
        }
        
        private TileCoord? GetTileUnderMouse()
        {
            if (_camera == null) return null;
            
            Vector3 mousePos = Input.mousePosition;
            Vector3 worldPos = _camera.ScreenToWorldPoint(mousePos);
            
            int tileX = Mathf.RoundToInt(worldPos.x / _tileSize);
            int tileY = Mathf.RoundToInt(worldPos.y / _tileSize);
            
            return new TileCoord(tileX, tileY);
        }
        
        void OnGUI()
        {
            if (!_basePlaced)
            {
                GUI.Box(new Rect(10, 450, 300, 60), "Base Placement");
                GUI.Label(new Rect(20, 470, 280, 20), "Press 'B' to place base");
                GUI.Label(new Rect(20, 490, 280, 20), "Place near bottom of mountain!");
            }
            else
            {
                GUI.Box(new Rect(10, 450, 300, 40), "");
                GUI.Label(new Rect(20, 460, 280, 20), $"Base: {_baseLocation}");
            }
        }
    }
}

