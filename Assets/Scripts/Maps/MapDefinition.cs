using UnityEngine;

namespace SkiResortTycoon.Maps
{
    /// <summary>
    /// Defines a playable map/mountain. Each asset represents one selectable map
    /// with display metadata and a stable ID for save files.
    /// The actual scene content lives under a <see cref="MapRoot"/> GameObject whose
    /// mapId matches this definition's mapId. MountainManager enables/disables roots
    /// at runtime so all maps stay editable in the scene hierarchy.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMap", menuName = "Ski Resort Tycoon/Map Definition")]
    public class MapDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier persisted in save files. Must match the MapRoot.mapId in the scene. Never change after release.")]
        public string mapId;

        [Tooltip("Display name shown in the map selection UI.")]
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("Flavor text shown on the map selection card.")]
        public string description;

        [Tooltip("Optional preview thumbnail for the map picker.")]
        public Sprite previewImage;
    }
}
