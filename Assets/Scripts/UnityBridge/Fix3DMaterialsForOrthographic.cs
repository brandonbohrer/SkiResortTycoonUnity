using UnityEngine;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Legacy component that previously fixed materials for orthographic camera.
    /// Now only ensures shadows are enabled on all renderers.
    /// Safe to remove from prefabs — kept for backwards compatibility so
    /// existing prefab references don't break.
    /// </summary>
    public class Fix3DMaterialsForOrthographic : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool _fixOnAwake = true;
        [SerializeField] private bool _enableShadows = true;

        void Awake()
        {
            if (_fixOnAwake)
            {
                FixAllMaterials();
            }
        }

        [ContextMenu("Fix Materials Now")]
        public void FixAllMaterials()
        {
            if (!_enableShadows) return;

            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            Debug.Log($"[Fix3DMaterials] Enabled shadows on {renderers.Length} renderers (orthographic fix no longer needed with perspective camera)");
        }
    }
}
