using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Saving;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Manages the Save Game menu: new save, rename, overwrite, delete, and slot list.
    /// Assign buttons, input field, and slot template in the inspector.
    /// Input field + Accept/Cancel are hidden until user clicks "+ New Save" or "Rename" on a slot.
    /// </summary>
    public class SaveGameManager : MonoBehaviour
    {
        [Header("Top bar — New Save + Input row")]
        [Tooltip("Input field on the manager screen that shows the current resort name. Drag it here.")]
        [SerializeField] private TMP_InputField _resortNameInputField;
        [SerializeField] private Button _newSaveButton;
        [SerializeField] private GameObject _inputRow; // parent of input field + accept + cancel
        [Tooltip("Input field where you type the save/rename; auto-fills from resort name when you click New Save or Rename.")]
        [SerializeField] private TMP_InputField _nameInputField;
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _cancelButton;

        [Header("Slot list")]
        [Tooltip("Parent transform for slot instances (e.g. scroll content).")]
        [SerializeField] private Transform _slotContent;
        [Tooltip("Slot template prefab (or child). Must have SaveSlotEntry with Name, Day/Money, Save/Rename/Delete assigned.")]
        [SerializeField] private GameObject _slotTemplate;

        [Header("Back")]
        [SerializeField] private Button _backButton;

        [Header("Confirm dialog (your panel)")]
        [SerializeField] private GameObject _confirmDialogPanel;
        [SerializeField] private Button _confirmDialogCancelButton;
        [SerializeField] private Button _confirmDialogConfirmButton;
        [Tooltip("Show this text when confirming overwrite save.")]
        [SerializeField] private GameObject _confirmOverwriteText;
        [Tooltip("Show this text when confirming delete.")]
        [SerializeField] private GameObject _confirmDeleteText;

        [Header("Open from game")]
        [Tooltip("Drag the in-game Save button here. If this panel is disabled at start, Awake never runs and this won't work — use SaveMenuOpener on an active object (e.g. Dock) instead.")]
        [SerializeField] private Button _openSaveMenuButton;

        [Header("References for capture")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private TrailDrawer _trailDrawer;
        [SerializeField] private LodgeManager _lodgeManager;
        [Tooltip("Optional. Assign to include skiers (names, skills, progress) in save.")]
        [SerializeField] private SkierVisualizer _skierVisualizer;

        private enum InputMode { None, NewSave, Rename }
        private InputMode _inputMode;
        private SaveSlotInfo? _renameSlot;

        private System.Action _pendingConfirm;

        private void Awake()
        {
            HideInputRow();
            if (_confirmDialogPanel != null) _confirmDialogPanel.SetActive(false);

            if (_newSaveButton != null) _newSaveButton.onClick.AddListener(OnNewSaveClicked);
            if (_acceptButton != null) _acceptButton.onClick.AddListener(OnAcceptClicked);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(OnCancelClicked);
            if (_backButton != null) _backButton.onClick.AddListener(OnBackClicked);
            if (_openSaveMenuButton != null) _openSaveMenuButton.onClick.AddListener(OpenSaveMenu);
            if (_confirmDialogCancelButton != null) _confirmDialogCancelButton.onClick.AddListener(OnConfirmDialogCancel);
            if (_confirmDialogConfirmButton != null) _confirmDialogConfirmButton.onClick.AddListener(OnConfirmDialogConfirm);
        }

        /// <summary>
        /// Opens the save menu (this panel). Called by the in-game Save button when assigned.
        /// </summary>
        public void OpenSaveMenu()
        {
            gameObject.SetActive(true);
        }

        private void OnEnable()
        {
            _inputMode = InputMode.None;
            _renameSlot = null;
            HideInputRow();
            RefreshSlotList();
        }

        /// <summary>
        /// Call when opening the save menu so slots are up to date.
        /// </summary>
        public void RefreshSlotList()
        {
            if (_slotContent == null) return;

            // Remove existing slot instances; hide template if it's a direct child
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

            List<SaveSlotInfo> saves = GameSaveService.ListSaves();

            foreach (var slot in saves)
            {
                GameObject slotGo = _slotTemplate != null ? Instantiate(_slotTemplate, _slotContent) : null;
                if (slotGo == null) continue;

                slotGo.SetActive(true);

                var entry = slotGo.GetComponent<SaveSlotEntry>();
                if (entry != null)
                    entry.Setup(slot, OnSaveSlotClicked, OnRenameSlotClicked, OnDeleteSlotClicked);
            }

            ButtonHoverFeedback.ApplyUnder(_slotContent, UIManager.Instance?.Theme);
        }

        /// <summary>
        /// Format money as 17.2K or 1.02M.
        /// </summary>
        public static string FormatMoney(int money)
        {
            if (money >= 1_000_000)
                return $"{(money / 1_000_000f):0.##}M";
            if (money >= 1_000)
                return $"{(money / 1_000f):0.#}K";
            return money.ToString("N0");
        }

        private void OnNewSaveClicked()
        {
            _inputMode = InputMode.NewSave;
            _renameSlot = null;
            if (_nameInputField != null)
            {
                _nameInputField.text = _resortNameInputField != null ? (_resortNameInputField.text ?? "") : "";
                _nameInputField.interactable = true;
            }
            ShowInputRow();
            FocusInputField();
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
            FocusInputField();
        }

        private void ShowInputRow()
        {
            if (_inputRow != null) _inputRow.SetActive(true);
            if (_nameInputField != null) _nameInputField.gameObject.SetActive(true);
            if (_acceptButton != null) _acceptButton.gameObject.SetActive(true);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(true);
        }

        private void FocusInputField()
        {
            if (_nameInputField != null)
            {
                _nameInputField.ActivateInputField();
                _nameInputField.Select();
            }
        }

        private void OnAcceptClicked()
        {
            string name = _nameInputField != null ? _nameInputField.text?.Trim() : "";
            if (_inputMode == InputMode.NewSave)
            {
                if (string.IsNullOrEmpty(name))
                    name = "Unnamed Resort";
                var data = GameSaveService.CaptureFromGame(_simulationRunner, _liftBuilder, _trailDrawer, _lodgeManager, _skierVisualizer);
                data.resortName = name;
                string safeName = MakeSafeFileName(name);
                string path = PathWithoutExtension(safeName);
                GameSaveService.Save(path, data);
                HideInputRow();
                RefreshSlotList();
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

        private void OnCancelClicked()
        {
            HideInputRow();
            _renameSlot = null;
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

        private void OnSaveSlotClicked(SaveSlotInfo slot)
        {
            if (_confirmOverwriteText != null) _confirmOverwriteText.SetActive(true);
            if (_confirmDeleteText != null) _confirmDeleteText.SetActive(false);
            _pendingConfirm = () =>
            {
                var data = GameSaveService.CaptureFromGame(_simulationRunner, _liftBuilder, _trailDrawer, _lodgeManager, _skierVisualizer);
                data.resortName = slot.DisplayName ?? "Unnamed Resort";
                GameSaveService.Save(slot.Path, data);
                RefreshSlotList();
            };
            if (_confirmDialogPanel != null) _confirmDialogPanel.SetActive(true);
        }

        private void OnDeleteSlotClicked(SaveSlotInfo slot)
        {
            if (_confirmOverwriteText != null) _confirmOverwriteText.SetActive(false);
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

        private void OnBackClicked()
        {
            if (_inputMode != InputMode.None)
            {
                OnCancelClicked();
                return;
            }
            gameObject.SetActive(false);
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

        /// <summary>
        /// Set the current resort name (e.g. after loading). Used to pre-fill "New Save" if desired.
        /// </summary>
        public void SetCurrentResortName(string name)
        {
            if (_nameInputField != null && _inputMode == InputMode.NewSave)
                _nameInputField.text = name ?? "";
        }
    }
}
