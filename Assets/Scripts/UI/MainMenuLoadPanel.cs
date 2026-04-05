using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using SkiResortTycoon.Maps;
using SkiResortTycoon.Saving;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Load Game panel on main menu: + New Game (creates empty save), slot list with Play / Rename / Delete.
    /// New Game flow: name resort -> pick map -> create save + launch game.
    /// </summary>
    public class MainMenuLoadPanel : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string _gameSceneName = "Main";

        [Header("Top bar — + New Game + Input row")]
        [Tooltip("Optional: input that shows resort name; used to pre-fill the name field for New Game / Rename.")]
        [SerializeField] private TMP_InputField _resortNameInputField;
        [SerializeField] private Button _newGameButton;
        [Tooltip("Parent of input field + Accept + Cancel; hidden until + New Game or Rename.")]
        [SerializeField] private GameObject _inputRow;
        [Tooltip("Where you type the new game name or rename; toggles with Accept/Cancel.")]
        [SerializeField] private TMP_InputField _nameInputField;
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _cancelButton;

        [Header("Slot list")]
        [Tooltip("Scroll content parent for slot instances.")]
        [SerializeField] private Transform _slotContent;
        [Tooltip("Slot template with MainMenuLoadSlotEntry (name, details, Play, Rename, Delete).")]
        [SerializeField] private GameObject _slotTemplate;

        [Header("Map Selection")]
        [Tooltip("Panel that shows available maps after naming a new resort.")]
        [SerializeField] private MapSelectionPanel _mapSelectionPanel;

        [Header("Back")]
        [SerializeField] private Button _backButton;

        [Header("Confirm dialog (Delete only)")]
        [SerializeField] private GameObject _confirmDialogPanel;
        [SerializeField] private Button _confirmDialogCancelButton;
        [SerializeField] private Button _confirmDialogConfirmButton;
        [Tooltip("Text to show when confirming delete (e.g. 'Delete this save?').")]
        [SerializeField] private GameObject _confirmDeleteText;

        private enum InputMode { None, NewGame, Rename }
        private InputMode _inputMode;
        private SaveSlotInfo? _renameSlot;
        private System.Action _pendingConfirm;
        private string _pendingNewGameName;

        private void Awake()
        {
            HideInputRow();
            if (_confirmDialogPanel != null) _confirmDialogPanel.SetActive(false);
            if (_mapSelectionPanel != null) _mapSelectionPanel.gameObject.SetActive(false);

            if (_newGameButton != null) _newGameButton.onClick.AddListener(OnNewGameClicked);
            if (_acceptButton != null) _acceptButton.onClick.AddListener(OnAcceptClicked);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(OnCancelClicked);
            if (_backButton != null) _backButton.onClick.AddListener(() => gameObject.SetActive(false));
            if (_confirmDialogCancelButton != null) _confirmDialogCancelButton.onClick.AddListener(OnConfirmDialogCancel);
            if (_confirmDialogConfirmButton != null) _confirmDialogConfirmButton.onClick.AddListener(OnConfirmDialogConfirm);
        }

        /// <summary>
        /// Call when the panel is opened (e.g. from Play button).
        /// </summary>
        public void OnPanelOpened()
        {
            _inputMode = InputMode.None;
            _renameSlot = null;
            _pendingNewGameName = null;
            HideInputRow();
            RefreshSlotList();
        }

        public void RefreshSlotList()
        {
            if (_slotContent == null) return;

            for (int i = _slotContent.childCount - 1; i >= 0; i--)
            {
                var child = _slotContent.GetChild(i);
                if (child.gameObject == _slotTemplate)
                {
                    _slotTemplate.SetActive(false);
                    continue;
                }
                Destroy(child.gameObject);
            }

            var registry = _mapSelectionPanel != null ? _mapSelectionPanel.MapRegistry : null;

            var saves = GameSaveService.ListSaves();
            foreach (var slot in saves)
            {
                GameObject slotGo = _slotTemplate != null ? Instantiate(_slotTemplate, _slotContent) : null;
                if (slotGo == null) continue;
                slotGo.SetActive(true);

                string mapName = null;
                if (registry != null && !string.IsNullOrEmpty(slot.MapId))
                {
                    var mapDef = registry.GetById(slot.MapId);
                    if (mapDef != null) mapName = mapDef.displayName;
                }

                var entry = slotGo.GetComponent<MainMenuLoadSlotEntry>();
                if (entry != null)
                    entry.Setup(slot, OnPlaySlotClicked, OnRenameSlotClicked, OnDeleteSlotClicked, mapName);
            }

            ButtonHoverFeedback.ApplyUnder(_slotContent, null);
        }

        private void ShowInputRow()
        {
            if (_inputRow != null) _inputRow.SetActive(true);
            if (_nameInputField != null) _nameInputField.gameObject.SetActive(true);
            if (_acceptButton != null) _acceptButton.gameObject.SetActive(true);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(true);
        }

        private void HideInputRow()
        {
            _inputMode = InputMode.None;
            _renameSlot = null;
            if (_inputRow != null) _inputRow.SetActive(false);
            if (_nameInputField != null) _nameInputField.gameObject.SetActive(false);
            if (_acceptButton != null) _acceptButton.gameObject.SetActive(false);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(false);
        }

        private void OnNewGameClicked()
        {
            _inputMode = InputMode.NewGame;
            _renameSlot = null;
            if (_nameInputField != null)
            {
                _nameInputField.text = _resortNameInputField != null ? (_resortNameInputField.text ?? "") : "";
                _nameInputField.interactable = true;
            }
            ShowInputRow();
            if (_nameInputField != null) { _nameInputField.ActivateInputField(); _nameInputField.Select(); }
        }

        private void OnRenameSlotClicked(SaveSlotInfo slot)
        {
            _inputMode = InputMode.Rename;
            _renameSlot = slot;
            if (_nameInputField != null)
            {
                _nameInputField.text = _resortNameInputField != null ? (_resortNameInputField.text ?? "") : (slot.DisplayName ?? "");
                _nameInputField.interactable = true;
            }
            ShowInputRow();
            if (_nameInputField != null) { _nameInputField.ActivateInputField(); _nameInputField.Select(); }
        }

        private void OnAcceptClicked()
        {
            string name = _nameInputField != null ? _nameInputField.text?.Trim() : "";
            if (_inputMode == InputMode.NewGame)
            {
                if (string.IsNullOrEmpty(name)) name = "Unnamed Resort";
                _pendingNewGameName = name;
                HideInputRow();

                if (_mapSelectionPanel != null)
                {
                    _mapSelectionPanel.Show(OnMapSelectedForNewGame);
                }
                else
                {
                    OnMapSelectedForNewGame(MapRegistry.LegacyMapId);
                }
            }
            else if (_inputMode == InputMode.Rename && _renameSlot.HasValue)
            {
                if (string.IsNullOrEmpty(name)) name = "Unnamed Resort";
                GameSaveService.Rename(_renameSlot.Value.Path, name);
                _renameSlot = null;
                HideInputRow();
                RefreshSlotList();
            }
        }

        private void OnMapSelectedForNewGame(string mapId)
        {
            string name = _pendingNewGameName ?? "Unnamed Resort";
            _pendingNewGameName = null;

            var data = GameSaveService.CreateEmptySave(name, mapId);
            string safeName = MakeSafeFileName(name);
            string path = PathWithoutExtension(safeName);
            GameSaveService.Save(path, data);

            GameLoadBootstrap.PendingSavePath = GameSaveService.GetSaveDirectory()
                + System.IO.Path.DirectorySeparatorChar + safeName + ".json";
            GameLoadBootstrap.PendingMapId = mapId;
            SceneManager.LoadScene(_gameSceneName);
        }

        private void OnCancelClicked()
        {
            HideInputRow();
            _renameSlot = null;
            _pendingNewGameName = null;
        }

        private void OnPlaySlotClicked(SaveSlotInfo slot)
        {
            GameLoadBootstrap.PendingSavePath = slot.Path;
            GameLoadBootstrap.PendingMapId = string.IsNullOrEmpty(slot.MapId)
                ? MapRegistry.LegacyMapId
                : slot.MapId;
            SceneManager.LoadScene(_gameSceneName);
        }

        private void OnDeleteSlotClicked(SaveSlotInfo slot)
        {
            if (_confirmDeleteText != null) _confirmDeleteText.SetActive(true);
            _pendingConfirm = () =>
            {
                GameSaveService.Delete(slot.Path);
                RefreshSlotList();
            };
            if (_confirmDialogPanel != null) _confirmDialogPanel.SetActive(true);
        }

        private void OnConfirmDialogCancel()
        {
            _pendingConfirm = null;
            if (_confirmDialogPanel != null) _confirmDialogPanel.SetActive(false);
        }

        private void OnConfirmDialogConfirm()
        {
            _pendingConfirm?.Invoke();
            _pendingConfirm = null;
            if (_confirmDialogPanel != null) _confirmDialogPanel.SetActive(false);
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "save";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
                name = name.Replace(c, '_');
            return name.Length > 0 ? name : "save";
        }

        private static string PathWithoutExtension(string fileName)
        {
            return fileName.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - 5)
                : fileName;
        }
    }
}
