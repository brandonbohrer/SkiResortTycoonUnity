using UnityEngine;
using UnityEngine.UI;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Put this on the **menu overlay that contains the Save button** (that overlay can be inactive at start).
    /// Assign the Save button and the Save Menu panel. When the overlay is first enabled, the button is wired.
    /// When the user clicks Save, the save menu panel opens.
    /// </summary>
    public class SaveMenuOpener : MonoBehaviour
    {
        [Tooltip("The Save button (e.g. on this overlay).")]
        [SerializeField] private Button _saveButton;
        [Tooltip("The save menu panel to show when Save is clicked (can be inactive at start).")]
        [SerializeField] private GameObject _saveMenuPanel;

        private bool _wired;

        private void OnEnable()
        {
            if (_wired) return;
            if (_saveButton != null && _saveMenuPanel != null)
            {
                _saveButton.onClick.AddListener(OpenSaveMenu);
                _wired = true;
            }
        }

        private void OpenSaveMenu()
        {
            if (_saveMenuPanel != null)
                _saveMenuPanel.SetActive(true);
        }
    }
}
