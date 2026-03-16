using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Saving;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Attach to the save slot template (or prefab). Assign the slot content fields in the inspector.
    /// Save and Delete trigger the confirm window (different text for overwrite vs delete).
    /// </summary>
    public class SaveSlotEntry : MonoBehaviour
    {
        [Header("Slot content — assign from this slot's hierarchy")]
        [Tooltip("Shows the save name (e.g. Alpine Valley).")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [Tooltip("Shows day and money (e.g. Day 42 - $1.2M).")]
        [SerializeField] private TextMeshProUGUI _detailsText;
        [Tooltip("Overwrite this save — opens confirm window with Cancel/Confirm.")]
        [SerializeField] private Button _saveButton;
        [Tooltip("Rename this save — shows input field with current name.")]
        [SerializeField] private Button _renameButton;
        [Tooltip("Delete this save — opens confirm window with Cancel/Confirm.")]
        [SerializeField] private Button _deleteButton;

        public void Setup(
            SaveSlotInfo slot,
            System.Action<SaveSlotInfo> onSave,
            System.Action<SaveSlotInfo> onRename,
            System.Action<SaveSlotInfo> onDelete)
        {
            if (_nameText != null) _nameText.text = slot.DisplayName;
            if (_detailsText != null) _detailsText.text = $"Day {slot.Day} - {SaveGameManager.FormatMoney(slot.Money)}";

            if (_saveButton != null)
            {
                _saveButton.onClick.RemoveAllListeners();
                _saveButton.onClick.AddListener(() => onSave(slot));
            }
            if (_renameButton != null)
            {
                _renameButton.onClick.RemoveAllListeners();
                _renameButton.onClick.AddListener(() => onRename(slot));
            }
            if (_deleteButton != null)
            {
                _deleteButton.onClick.RemoveAllListeners();
                _deleteButton.onClick.AddListener(() => onDelete(slot));
            }
        }
    }
}
