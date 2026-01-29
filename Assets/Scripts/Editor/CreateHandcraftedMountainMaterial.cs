using UnityEngine;
using UnityEditor;

namespace SkiResortTycoon.Editor
{
    /// <summary>
    /// Creates a beautiful snow material for the handcrafted mountain.
    /// </summary>
    public static class CreateHandcraftedMountainMaterial
    {
        [MenuItem("Tools/Ski Resort Tycoon/Create Handcrafted Mountain Material")]
        public static void CreateMaterial()
        {
            // Create simplest possible material - Unlit/Color
            Material snowMat = new Material(Shader.Find("Unlit/Color"));
            
            snowMat.name = "Handcrafted_Mountain_Snow";
            
            // Snow color: white with subtle blue-gray tint
            snowMat.SetColor("_Color", new Color(0.92f, 0.94f, 0.98f, 1f)); // Slightly blue-white
            
            // Save to Materials folder
            string path = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            
            string fullPath = $"{path}/Handcrafted_Mountain_Snow.mat";
            
            // Delete old if exists
            if (AssetDatabase.LoadAssetAtPath<Material>(fullPath) != null)
            {
                AssetDatabase.DeleteAsset(fullPath);
            }
            
            AssetDatabase.CreateAsset(snowMat, fullPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"✓ Handcrafted Mountain Snow material created!");
            Debug.Log($"  Location: {fullPath}");
            Debug.Log($"  Drag this onto your mountain mesh in the scene!");
            
            // Ping it
            EditorGUIUtility.PingObject(snowMat);
        }
    }
}

