using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Saving;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// One save slot on the Load Game panel: name, day/money, Play, Rename, Delete.
    /// Assign all fields in the inspector (same pattern as SaveSlotEntry).
    /// </summary>
    public class MainMenuLoadSlotEntry : MonoBehaviour
    {
        [Header("Slot content")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _detailsText;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _renameButton;
        [SerializeField] private Button _deleteButton;

        public void Setup(
            SaveSlotInfo slot,
            System.Action<SaveSlotInfo> onPlay,
            System.Action<SaveSlotInfo> onRename,
            System.Action<SaveSlotInfo> onDelete,
            string mapDisplayName = null)
        {
            if (_nameText != null) _nameText.text = slot.DisplayName;

            string details = $"Day {slot.Day} - {SaveGameManager.FormatMoney(slot.Money)}";
            if (!string.IsNullOrEmpty(mapDisplayName))
                details += $" - {mapDisplayName}";
            if (_detailsText != null) _detailsText.text = details;

            if (_playButton != null)
            {
                _playButton.onClick.RemoveAllListeners();
                _playButton.onClick.AddListener(() => onPlay(slot));
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
