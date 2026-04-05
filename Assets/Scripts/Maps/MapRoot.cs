using UnityEngine;

namespace SkiResortTycoon.Maps
{
    /// <summary>
    /// Attach to the root GameObject of each map's content in the scene hierarchy.
    /// MountainManager discovers all MapRoot instances at startup, enables the one
    /// matching the selected mapId, and disables the rest. This keeps all maps
    /// visible and editable in the editor while only one is active at runtime.
    /// </summary>
    public class MapRoot : MonoBehaviour
    {
        [Tooltip("Must match the mapId on the corresponding MapDefinition ScriptableObject.")]
        public string mapId;

        [Tooltip("The mountain mesh GameObject within this map root (used for raycasts).")]
        public GameObject mountainMesh;

        [Header("Camera Overrides (0 = use CameraController defaults)")]
        [Tooltip("Max zoom-out distance. 0 = use CameraController default (500).")]
        public float maxCameraDistance = 0f;

        [Tooltip("Camera far clip plane. 0 = use CameraController default (2000).")]
        public float farClipPlane = 0f;

        [Tooltip("Starting zoom distance. 0 = use CameraController default (150).")]
        public float defaultCameraDistance = 0f;

        [Tooltip("Padding added around mountain bounds for camera focus limits. 0 = use default (50).")]
        public float boundsPadding = 0f;
    }
}
