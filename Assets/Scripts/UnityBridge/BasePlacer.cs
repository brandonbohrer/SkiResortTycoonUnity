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
        [SerializeField] private MountainManager _mountainManager;
        [SerializeField] private Camera _camera;
        
        [Header("Base Lodge Prefab")]
        [SerializeField] private GameObject _baseLodgePrefab;
        [SerializeField] private bool _useDebugCube = true; // Use simple placeholder (always use for now)
        
        [Header("Settings")]
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private KeyCode _placeKey = KeyCode.B;
        [SerializeField] private Vector3 _lodgeScale = new Vector3(10f, 10f, 10f); // Bigger for visibility
        [SerializeField] private Vector3 _lodgeOffset = new Vector3(0, 0, 0); // No offset
        [SerializeField] private float _lodgeZPosition = -5f; // Same depth as trails (-5 works with camera at -10)
        
        private bool _basePlaced = false;
        private TileCoord _baseLocation;
        private GameObject _spawnedLodge;
        
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
            
            // Calculate world position
            Vector3 worldPos = TileToWorldPos(location);
            
            // Spawn the 3D lodge model
            if (_baseLodgePrefab != null && !_useDebugCube)
            {
                // Spawn INACTIVE to prevent Awake() from running on bad scripts
                bool originalState = _baseLodgePrefab.activeSelf;
                _baseLodgePrefab.SetActive(false);
                
                _spawnedLodge = Instantiate(_baseLodgePrefab, worldPos + _lodgeOffset, Quaternion.identity);
                _spawnedLodge.name = "Base Lodge";
                _spawnedLodge.transform.localScale = _lodgeScale;
                _spawnedLodge.transform.SetParent(transform);
                
                // Restore prefab state
                _baseLodgePrefab.SetActive(originalState);
                
                // REMOVE the Fix3DMaterialsForOrthographic component BEFORE activating
                // (it was ruining all the materials by making everything the same color)
                var badScript = _spawnedLodge.GetComponent<Fix3DMaterialsForOrthographic>();
                if (badScript != null)
                {
                    DestroyImmediate(badScript);
                    Debug.Log("[BasePlacer] ✓ Removed Fix3DMaterials component (it was replacing all materials with one color)");
                }
                
                // Enable shadows on all renderers
                var renderers = _spawnedLodge.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                
                // NOW activate it
                _spawnedLodge.SetActive(true);
                
                Debug.Log($"[BasePlacer] ✓ Cabin spawned: {renderers.Length} pieces, original colors preserved, shadows ON");
            }
            else
            {
                // Create a simple debug cube (RED so it's visible!)
                _spawnedLodge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _spawnedLodge.name = "Base Lodge (Debug)";
                _spawnedLodge.transform.position = worldPos + _lodgeOffset;
                _spawnedLodge.transform.localScale = _lodgeScale;
                _spawnedLodge.transform.SetParent(transform);
                
                // Make it RED and shiny so we can see it!
                var renderer = _spawnedLodge.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Unlit/Color"));
                    renderer.material.color = new Color(0.8f, 0.1f, 0.1f); // Bright red
                }
                
                // Remove collider (we don't need physics)
                var collider = _spawnedLodge.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }
            
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
            Debug.Log($"World Position: {worldPos + _lodgeOffset}");
            Debug.Log($"Base spawn point registered!");
            if (_spawnedLodge != null)
            {
                string lodgeType = _useDebugCube ? "Debug Cube" : "Custom Prefab";
                Debug.Log($"Lodge spawned: {lodgeType}");
                Debug.Log($"Lodge scale: {_spawnedLodge.transform.localScale}");
                Debug.Log($"Lodge world pos: {_spawnedLodge.transform.position}");
                
                // Check if it has renderers
                var renderers = _spawnedLodge.GetComponentsInChildren<Renderer>();
                Debug.Log($"Renderers found: {renderers.Length}");
                if (renderers.Length > 0)
                {
                    Debug.Log($"First renderer bounds: {renderers[0].bounds}");
                }
            }
            Debug.Log($"Build lifts near this location to connect to base");
            Debug.Log($"===================");
        }
        
        private Vector3 TileToWorldPos(TileCoord coord)
        {
            float x = coord.X * _tileSize;
            float y = coord.Y * _tileSize;
            
            // Add height offset if terrain data is available
            if (_mountainManager != null && _mountainManager.TerrainData != null)
            {
                float height = _mountainManager.TerrainData.GetHeight(coord);
                y += height * 0.5f; // Match the 2.5D height offset
            }
            
            return new Vector3(x, y, _lodgeZPosition); // Use configurable Z position
        }
        
        private TileCoord? GetTileUnderMouse()
        {
            if (_camera == null) return null;
            
            // Create a ray from the camera through the mouse position
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            
            // Create a plane at Z=0 (where our grid is)
            Plane gridPlane = new Plane(Vector3.forward, Vector3.zero);
            
            // Raycast against the plane
            if (gridPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPos = ray.GetPoint(distance);
                
                int tileX = Mathf.RoundToInt(worldPos.x / _tileSize);
                int tileY = Mathf.RoundToInt(worldPos.y / _tileSize);
                
                TileCoord coord = new TileCoord(tileX, tileY);
                
                // Check if in bounds
                if (_mountainManager != null && 
                    _mountainManager.TerrainData != null && 
                    _mountainManager.TerrainData.Grid.InBounds(coord))
                {
                    return coord;
                }
            }
            
            return null;
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

