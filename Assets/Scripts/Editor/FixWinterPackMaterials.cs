using UnityEngine;
using UnityEditor;

namespace SkiResortTycoon.Editor
{
    /// <summary>
    /// One-time script to fix all Winter Pack prefabs that lost their materials during URP migration.
    /// </summary>
    public class FixWinterPackMaterials : EditorWindow
    {
        [MenuItem("Tools/Fix Winter Pack Materials")]
        public static void FixAllMaterials()
        {
            // Find the URP material
            string[] guids = AssetDatabase.FindAssets("Winter_Pack_Mat_URP t:Material");
            if (guids.Length == 0)
            {
                Debug.LogError("Could not find Winter_Pack_Mat_URP! Make sure it exists.");
                return;
            }
            
            string materialPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            Material urpMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            
            Debug.Log($"Found URP material: {materialPath}");
            
            // Find all prefabs in the Winter Cabin folder
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Assets/Small Hearth Studios/Low_Poly_Winter_Cabin/Prefabs" });
            
            int fixedCount = 0;
            
            foreach (string guid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab == null) continue;
                
                // Get all renderers in the prefab
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                bool prefabModified = false;
                
                foreach (var renderer in renderers)
                {
                    // Check if materials are using the old Standard shader
                    Material[] materials = renderer.sharedMaterials;
                    bool needsFix = false;
                    
                    for (int i = 0; i < materials.Length; i++)
                    {
                        // Replace if: null, broken shader, OR using old Standard shader
                        bool isNull = materials[i] == null;
                        bool isBrokenShader = materials[i] != null && (materials[i].shader == null || materials[i].shader.name.Contains("Hidden"));
                        bool isOldStandard = materials[i] != null && materials[i].name.Contains("Winter_Pack_Mat_Standard");
                        bool isStandardShader = materials[i] != null && materials[i].shader != null && materials[i].shader.name == "Standard";
                        
                        if (isNull || isBrokenShader || isOldStandard || isStandardShader)
                        {
                            materials[i] = urpMaterial;
                            needsFix = true;
                        }
                    }
                    
                    if (needsFix)
                    {
                        renderer.sharedMaterials = materials;
                        prefabModified = true;
                        fixedCount++;
                    }
                }
                
                if (prefabModified)
                {
                    EditorUtility.SetDirty(prefab);
                    Debug.Log($"Fixed materials on: {prefab.name}");
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"✓ DONE! Fixed {fixedCount} renderers across {prefabGuids.Length} prefabs.");
            EditorUtility.DisplayDialog("Fix Complete", $"Fixed materials on {fixedCount} renderers!", "OK");
        }
    }
}

