using System.Collections.Generic;
using UnityEngine;

namespace SkiResortTycoon.Maps
{
    /// <summary>
    /// Central registry of all playable maps. Assign one of these in the inspector
    /// on MountainManager and MapSelectionPanel so they can look up maps by ID.
    /// </summary>
    [CreateAssetMenu(fileName = "MapRegistry", menuName = "Ski Resort Tycoon/Map Registry")]
    public class MapRegistry : ScriptableObject
    {
        [Tooltip("All available maps. Order determines display order in the picker.")]
        public List<MapDefinition> maps = new List<MapDefinition>();

        [Tooltip("Map ID used when no explicit choice is made (e.g. legacy saves).")]
        public string defaultMapId;

        /// <summary>
        /// Constant fallback for saves that predate the map system.
        /// Matches the classic mountain's mapId.
        /// </summary>
        public const string LegacyMapId = "alpine_valley";

        public MapDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
                id = string.IsNullOrEmpty(defaultMapId) ? LegacyMapId : defaultMapId;

            foreach (var map in maps)
            {
                if (map != null && map.mapId == id)
                    return map;
            }

            if (maps.Count > 0 && maps[0] != null)
            {
                Debug.LogWarning($"[MapRegistry] Map '{id}' not found, falling back to '{maps[0].mapId}'");
                return maps[0];
            }

            Debug.LogError("[MapRegistry] No maps configured!");
            return null;
        }

        public MapDefinition GetDefault()
        {
            return GetById(defaultMapId);
        }
    }
}
