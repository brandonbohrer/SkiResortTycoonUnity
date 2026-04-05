using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Maps;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// One card in the map selection panel representing a single MapDefinition.
    /// Displays name, description, and optional preview image.
    /// </summary>
    public class MapSelectionCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Image _previewImage;
        [SerializeField] private Button _selectButton;

        private string _mapId;
        private Action<string> _onSelected;

        public void Setup(MapDefinition mapDef, Action<string> onSelected)
        {
            _mapId = mapDef.mapId;
            _onSelected = onSelected;

            if (_nameText != null)
                _nameText.text = mapDef.displayName;
            if (_descriptionText != null)
                _descriptionText.text = mapDef.description;

            if (_previewImage != null)
            {
                if (mapDef.previewImage != null)
                {
                    _previewImage.sprite = mapDef.previewImage;
                    _previewImage.gameObject.SetActive(true);
                }
                else
                {
                    _previewImage.gameObject.SetActive(false);
                }
            }

            if (_selectButton != null)
            {
                _selectButton.onClick.RemoveAllListeners();
                _selectButton.onClick.AddListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            _onSelected?.Invoke(_mapId);
        }
    }
}
