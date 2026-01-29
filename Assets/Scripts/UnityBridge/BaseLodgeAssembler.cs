using UnityEngine;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Auto-assembles a simple cabin from the Low Poly Winter Cabin asset pieces.
    /// Attach this to a prefab or GameObject to build a cabin at runtime.
    /// </summary>
    public class BaseLodgeAssembler : MonoBehaviour
    {
        [Header("Cabin Piece References")]
        [Tooltip("Drag prefabs from: Assets/Assets/Small Hearth Studios/Low_Poly_Winter_Cabin/Prefabs/")]
        [SerializeField] private GameObject _cabinFloor;
        [SerializeField] private GameObject _cabinWallTall;
        [SerializeField] private GameObject _cabinWallPointed;
        [SerializeField] private GameObject _cabinDoor;
        [SerializeField] private GameObject _cabinWindow;
        [SerializeField] private GameObject _cabinRoofL;
        [SerializeField] private GameObject _cabinRoofR;
        
        [Header("Assembly Settings")]
        [SerializeField] private bool _assembleOnAwake = true;
        [SerializeField] private bool _useSnowVariants = true;
        
        void Awake()
        {
            if (_assembleOnAwake)
            {
                AssembleCabin();
            }
        }
        
        /// <summary>
        /// Assembles a simple 4-wall cabin with a door, window, and roof.
        /// </summary>
        public void AssembleCabin()
        {
            // Clear any existing children
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            
            // Floor (centered at origin)
            if (_cabinFloor != null)
            {
                Instantiate(_cabinFloor, transform.position, Quaternion.identity, transform);
            }
            
            // Front wall with door (facing +Y)
            if (_cabinDoor != null)
            {
                var door = Instantiate(_cabinDoor, transform.position, Quaternion.identity, transform);
                door.transform.localPosition = new Vector3(0, 0.5f, 0);
            }
            
            // Back wall (facing -Y)
            if (_cabinWallTall != null)
            {
                var backWall = Instantiate(_cabinWallTall, transform.position, Quaternion.Euler(0, 180, 0), transform);
                backWall.transform.localPosition = new Vector3(0, -2f, 0);
            }
            
            // Left wall with window (facing -X)
            if (_cabinWindow != null)
            {
                var leftWall = Instantiate(_cabinWindow, transform.position, Quaternion.Euler(0, -90, 0), transform);
                leftWall.transform.localPosition = new Vector3(-2f, 0, 0);
            }
            
            // Right wall (facing +X)
            if (_cabinWallTall != null)
            {
                var rightWall = Instantiate(_cabinWallTall, transform.position, Quaternion.Euler(0, 90, 0), transform);
                rightWall.transform.localPosition = new Vector3(2f, 0, 0);
            }
            
            // Left roof piece
            if (_cabinRoofL != null)
            {
                var roofL = Instantiate(_cabinRoofL, transform.position, Quaternion.identity, transform);
                roofL.transform.localPosition = new Vector3(-1f, 2f, 0);
            }
            
            // Right roof piece
            if (_cabinRoofR != null)
            {
                var roofR = Instantiate(_cabinRoofR, transform.position, Quaternion.identity, transform);
                roofR.transform.localPosition = new Vector3(1f, 2f, 0);
            }
        }
        
        /// <summary>
        /// Disassembles the cabin (removes all child objects).
        /// </summary>
        public void DisassembleCabin()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
        }
    }
}

