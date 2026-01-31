using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Attach this to the Base Lodge prefab in the scene.
    /// Registers the base spawn point automatically on Start.
    /// </summary>
    public class BaseSnapPointRegistrar : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int _baseId = 0;
        [SerializeField] private string _baseName = "Base Lodge";
        
        void Start()
        {
            // Wait a frame to ensure LiftBuilder is initialized
            Invoke(nameof(RegisterBaseSnapPoint), 0.1f);
        }
        
        private void RegisterBaseSnapPoint()
        {
            // Auto-find LiftBuilder in scene
            var liftBuilder = FindObjectOfType<LiftBuilder>();
            
            if (liftBuilder == null || liftBuilder.Connectivity == null)
            {
                Debug.LogWarning("[BaseSnapPoint] LiftBuilder not found in scene! Can't register base spawn point.");
                return;
            }
            
            // Get this object's 3D world position
            Vector3 worldPos = transform.position;
            Vector3f basePos = new Vector3f(worldPos.x, worldPos.y, worldPos.z);
            
            // Register base spawn point
            var baseSnap = new SnapPoint(
                SnapPointType.BaseSpawn,
                basePos,
                _baseId,
                _baseName
            );
            
            liftBuilder.Connectivity.Registry.Register(baseSnap);
            
            Debug.Log($"[BaseSnapPoint] ✓ Base spawn point registered at {worldPos}");
            Debug.Log($"[BaseSnapPoint] Skiers will spawn here!");
        }
    }
}
