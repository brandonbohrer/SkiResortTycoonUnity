using UnityEngine;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// DEPRECATED: This component has been replaced by SkierAIConfig (ScriptableObject)
    /// and the hybrid decision engine (SkierDecisionEngine + ResortTrafficManager).
    ///
    /// This stub remains so that existing scenes with this component don't throw
    /// missing-script errors. It does nothing at runtime.
    ///
    /// To migrate: assign a SkierAIConfig asset to SkierVisualizer's "AI Config" field,
    /// then remove this component from your GameObjects.
    /// </summary>
    [System.Obsolete("Use SkierAIConfig ScriptableObject instead. See SkierVisualizer._aiConfig field.")]
    public class SkierAITuning : MonoBehaviour
    {
        public static SkierAITuning Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Debug.LogWarning("[SkierAITuning] This component is DEPRECATED. " +
                "Use a SkierAIConfig ScriptableObject on SkierVisualizer instead. " +
                "Remove this component from your scene.");
        }
        
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
