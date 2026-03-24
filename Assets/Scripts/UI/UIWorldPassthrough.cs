using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Marker component: UI elements under this tag allow world-space interactions
    /// (trail building, camera zoom, structure selection) to pass through.
    /// Tooltips and hover effects still work — only the "is pointer over blocking UI"
    /// checks in BaseTool, CameraController, and StructureSelectionManager respect this.
    /// Attach to HUD containers (stat pills, etc.) that should never eat world clicks.
    /// </summary>
    public class UIWorldPassthrough : MonoBehaviour
    {
        /// <summary>
        /// Returns true if any raycast result is on a UI element that should block
        /// world interaction (i.e. does NOT have a <see cref="UIWorldPassthrough"/>
        /// ancestor). Returns false when all hits are on passthrough elements or the
        /// list is empty.
        /// </summary>
        public static bool HasBlockingHit(List<RaycastResult> results)
        {
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null) continue;
                if (go.GetComponentInParent<UIWorldPassthrough>() == null)
                    return true;
            }
            return false;
        }
    }
}
