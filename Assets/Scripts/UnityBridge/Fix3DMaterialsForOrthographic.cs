using UnityEngine;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Fixes 3D materials to render properly in orthographic camera.
    /// Attach this to any 3D prefab that isn't rendering correctly.
    /// </summary>
    public class Fix3DMaterialsForOrthographic : MonoBehaviour
    {
        [Header("Auto-Fix Settings")]
        [SerializeField] private bool _fixOnAwake = true;
        [SerializeField] private bool _forceUnlitShader = false; // Use Unlit shader instead of Lit
        [SerializeField] private bool _enableShadows = true; // Enable shadows for depth
        [SerializeField] private bool _skipMaterialConversion = false; // Set to TRUE if materials are already URP-ready
        
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
            var renderers = GetComponentsInChildren<Renderer>(true);
            int materialsFixed = 0;
            
            // If materials are already URP-ready, just enable shadows and skip conversion
            if (_skipMaterialConversion)
            {
                Debug.Log("[Fix3DMaterials] Skipping material conversion - using original URP materials");
                foreach (var renderer in renderers)
                {
                    renderer.enabled = true;
                    if (_enableShadows)
                    {
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                    }
                    
                    // Log material info for debugging
                    foreach (var mat in renderer.materials)
                    {
                        if (mat != null)
                        {
                            Debug.Log($"[Fix3DMaterials] Using material: {mat.name}, Shader: {mat.shader.name}");
                        }
                    }
                }
                return;
            }
            
            // Load Winter Pack material ONCE for all renderers
            Material winterPackMat = null;
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Winter_Pack_Mat_URP t:Material");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                winterPackMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            else
            {
                Debug.LogError("[Fix3DMaterials] ✗ Winter_Pack_Mat_URP not found! Cannot fix materials.");
                return;
            }
            #endif
            
            if (winterPackMat == null)
            {
                Debug.LogError("[Fix3DMaterials] Failed to load Winter_Pack_Mat_URP!");
                return;
            }
            
            // Load the texture too
            Texture winterPackTexture = null;
            #if UNITY_EDITOR
            string[] textureGuids = UnityEditor.AssetDatabase.FindAssets("winter_pack_texture t:Texture");
            if (textureGuids.Length > 0)
            {
                string texturePath = UnityEditor.AssetDatabase.GUIDToAssetPath(textureGuids[0]);
                winterPackTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
            }
            #endif
            
            foreach (var renderer in renderers)
            {
                // Ensure renderer is enabled
                renderer.enabled = true;
                
                // Enable shadows (CRITICAL for depth!)
                if (_enableShadows)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                
                // Get current materials
                Material[] mats = renderer.sharedMaterials;
                
                // ALWAYS create new material instances with the texture applied
                // Don't check if they're null - just replace them all!
                Material[] newMats = new Material[mats.Length];
                
                for (int i = 0; i < mats.Length; i++)
                {
                    // Create a NEW instance of the Winter Pack material
                    Material newMat = new Material(winterPackMat);
                    
                    // FORCE the texture to be assigned
                    if (winterPackTexture != null)
                    {
                        newMat.SetTexture("_BaseMap", winterPackTexture);
                        newMat.mainTexture = winterPackTexture;
                    }
                    
                    // Ensure URP/Lit shader is being used
                    Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpLitShader != null)
                    {
                        newMat.shader = urpLitShader;
                    }
                    
                    // Set proper material properties for wood
                    newMat.SetFloat("_Metallic", 0f);
                    newMat.SetFloat("_Smoothness", 0.3f);
                    
                    // Enable shadows in material (disable the "shadows off" keyword!)
                    newMat.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                    newMat.SetFloat("_Surface", 0); // Opaque
                    newMat.SetFloat("_WorkflowMode", 1); // Metallic workflow
                    newMat.renderQueue = 2000; // Opaque render queue
                    
                    newMats[i] = newMat;
                    materialsFixed++;
                }
                
                // Apply the new materials
                renderer.materials = newMats;
            }
            
            Debug.Log($"[Fix3DMaterials] ✓ Fixed {materialsFixed} materials on {renderers.Length} renderers with texture and shadows enabled!");
        }
    }
}

