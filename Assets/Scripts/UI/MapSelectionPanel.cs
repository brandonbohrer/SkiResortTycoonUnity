using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Maps;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Displays available maps and lets the player pick one when starting a new game.
    /// Spawns one MapSelectionCard per MapDefinition from the registry.
    /// Call Show() with a callback; the panel hides itself after selection.
    /// </summary>
    public class MapSelectionPanel : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MapRegistry _mapRegistry;
        public MapRegistry MapRegistry => _mapRegistry;

        [Header("UI")]
        [Tooltip("Title text at the top of the panel.")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [Tooltip("Parent transform for map card instances.")]
        [SerializeField] private Transform _cardContainer;

        [Tooltip("Template card (must have MapSelectionCard component). Hidden at runtime.")]
        [SerializeField] private GameObject _cardTemplate;

        [Header("Back")]
        [SerializeField] private Button _backButton;

        private Action<string> _onMapSelected;
        private readonly List<GameObject> _spawnedCards = new List<GameObject>();

        private void Awake()
        {
            if (_cardTemplate != null)
                _cardTemplate.SetActive(false);
            if (_backButton != null)
                _backButton.onClick.AddListener(OnBackClicked);
        }

        /// <summary>
        /// Opens the map selection panel. Calls onMapSelected with the chosen mapId.
        /// </summary>
        public void Show(Action<string> onMapSelected)
        {
            _onMapSelected = onMapSelected;
            gameObject.SetActive(true);
            Rebuild();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Rebuild()
        {
            foreach (var card in _spawnedCards)
                Destroy(card);
            _spawnedCards.Clear();

            if (_mapRegistry == null || _cardTemplate == null || _cardContainer == null)
                return;

            foreach (var mapDef in _mapRegistry.maps)
            {
                if (mapDef == null) continue;

                var cardGo = Instantiate(_cardTemplate, _cardContainer);
                cardGo.SetActive(true);
                _spawnedCards.Add(cardGo);

                var card = cardGo.GetComponent<MapSelectionCard>();
                if (card != null)
                    card.Setup(mapDef, OnCardClicked);
            }

            ButtonHoverFeedback.ApplyUnder(_cardContainer, null);
        }

        private void OnCardClicked(string mapId)
        {
            _onMapSelected?.Invoke(mapId);
            Hide();
        }

        private void OnBackClicked()
        {
            _onMapSelected = null;
            Hide();
        }
    }
}
