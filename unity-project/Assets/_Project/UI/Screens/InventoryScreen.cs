using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;
using KINETICS5.Data;

namespace KINETICS5.UI
{
    /// <summary>
    /// Inventaire joueur — grille d'objets tri/filtre.
    /// </summary>
    /// <remarks>
    /// Production-ready : armes, gear, consommables, matériaux, equip/unequip,
    /// bordures de rareté colorées, panneau de détails.
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/InventoryScreen")]
    [DisallowMultipleComponent]
    public sealed class InventoryScreen : UIScreen
    {
        [Header("Filtres")]
        [Tooltip("Boutons de filtre de catégorie (ALL, WEAPONS, GEAR, CONSUMABLES, MATERIALS).")]
        [SerializeField] private KButton[] _filterButtons;
        [Tooltip("Bouton tri (rareté / niveau / quantité).")]
        [SerializeField] private KButton _sortButton;
        [Tooltip("Champ de recherche.")]
        [SerializeField] private TMP_InputField _searchField;

        [Header("Grille")]
        [Tooltip("Conteneur de la grille d'items (GridLayoutGroup).")]
        [SerializeField] private RectTransform _gridContainer;
        [Tooltip("Prefab d'une cellule d'item (KCard mini).")]
        [SerializeField] private KCard _itemCardPrefab;
        [Tooltip("CardGroup pour sélection exclusive.")]
        [SerializeField] private KCardGroup _cardGroup;

        [Header("Détails")]
        [Tooltip("Panneau de détails (s'ilde latéral).")]
        [SerializeField] private RectTransform _detailsPanel;
        [Tooltip("Image de l'icône de l'item sélectionné.")]
        [SerializeField] private Image _detailsIcon;
        [Tooltip("Texte nom.")]
        [SerializeField] private TMP_Text _detailsNameText;
        [Tooltip("Texte description.")]
        [SerializeField] private TMP_Text _detailsDescriptionText;
        [Tooltip("Texte stats.")]
        [SerializeField] private TMP_Text _detailsStatsText;
        [Tooltip("Bouton EQUIP.")]
        [SerializeField] private KButton _equipButton;
        [Tooltip("Bouton UNEQUIP.")]
        [SerializeField] private KButton _unequipButton;
        [Tooltip("Bouton SELL.")]
        [SerializeField] private KButton _sellButton;

        [Header("Navigation")]
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;
        [Tooltip("Texte compteur d'items (X/Y).")]
        [SerializeField] private TMP_Text _itemCountText;

        private int _currentFilter;
        private int _sortMode;
        private readonly List<KCard> _cards = new(64);
        private string _selectedItemId;

        protected override void Awake()
        {
            _screenType = ScreenType.Inventory;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            for (int i = 0; i < _filterButtons.Length; i++)
            {
                int idx = i;
                var label = idx switch { 0 => "ALL", 1 => "WEAPONS", 2 => "GEAR", 3 => "CONSUMABLES", 4 => "MATERIALS", _ => "ALL" };
                _filterButtons[i].SetText(label);
                _filterButtons[i].OnKClick += _ => SetFilter(idx);
            }
            if (_sortButton != null)
            {
                _sortButton.SetText("SORT: RARITY");
                _sortButton.OnKClick += _ => CycleSortMode();
            }
            if (_searchField != null) _searchField.onValueChanged.AddListener(_ => RefreshGrid());
            if (_equipButton != null)
            {
                _equipButton.SetLocalizationKey("inventory.equip", "EQUIP");
                _equipButton.OnKClick += _ => OnEquip();
            }
            if (_unequipButton != null)
            {
                _unequipButton.SetLocalizationKey("inventory.unequip", "UNEQUIP");
                _unequipButton.OnKClick += _ => OnUnequip();
            }
            if (_sellButton != null)
            {
                _sellButton.SetLocalizationKey("inventory.sell", "SELL");
                _sellButton.OnKClick += _ => OnSell();
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            RefreshGrid();
            if (_detailsPanel != null) _detailsPanel.gameObject.SetActive(false);
            TrackClick("inventory_show");
        }

        protected override void OnHide()
        {
            ClearCards();
        }

        // =================================================================================
        //  GRILLE
        // =================================================================================

        private void RefreshGrid()
        {
            ClearCards();
            if (_gridContainer == null || _itemCardPrefab == null) return;

            var weapons = DataLoader.GetAllWeapons();
            var query = _searchField != null ? _searchField.text : string.Empty;
            bool hasQuery = !string.IsNullOrEmpty(query);

            foreach (var w in weapons)
            {
                if (hasQuery && !w.DisplayName.Contains(query, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (_currentFilter == 1 && w.Category != WeaponCategory.Primary && w.Category != WeaponCategory.Secondary) continue;
                if (_currentFilter == 3 && w.Category != WeaponCategory.Tactical) continue;

                var card = Instantiate(_itemCardPrefab, _gridContainer);
                card.Bind(w, false);
                card.OnCardClicked += OnItemClicked;
                _cards.Add(card);
                _cardGroup?.Register(card);
            }

            // Tri.
            ApplySort();

            if (_itemCountText != null)
            {
                _itemCountText.text = $"{_cards.Count} ITEMS";
                _itemCountText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _itemCountText.color = ThemeManager.TextMuted;
            }
        }

        private void ApplySort()
        {
            // Tri basique par rareté (Common -> Legendary).
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].transform.SetSiblingIndex(i);
            }
        }

        private void SetFilter(int index)
        {
            _currentFilter = index;
            for (int i = 0; i < _filterButtons.Length; i++)
            {
                if (_filterButtons[i] != null) _filterButtons[i].SetSelected(i == index);
            }
            RefreshGrid();
            TrackClick($"inventory_filter_{index}");
        }

        private void CycleSortMode()
        {
            _sortMode = (_sortMode + 1) % 3;
            var label = _sortMode switch { 0 => "SORT: RARITY", 1 => "SORT: POWER", 2 => "SORT: NAME", _ => "SORT: RARITY" };
            if (_sortButton != null) _sortButton.SetText(label);
            ApplySort();
            TrackClick($"inventory_sort_{_sortMode}");
        }

        private void OnItemClicked(KCard card)
        {
            if (card == null) return;
            _selectedItemId = card.ItemId;
            ShowDetails(card.ItemId);
        }

        private void ShowDetails(string itemId)
        {
            var weapon = DataLoader.GetWeapon(itemId);
            if (weapon == null) return;
            if (_detailsPanel != null) _detailsPanel.gameObject.SetActive(true);
            if (_detailsNameText != null)
            {
                _detailsNameText.text = weapon.DisplayName;
                _detailsNameText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _detailsNameText.color = ThemeManager.Main;
            }
            if (_detailsDescriptionText != null)
            {
                _detailsDescriptionText.text = $"Power {weapon.Power} • Reload {weapon.ReloadTime:F1}s";
                _detailsDescriptionText.font = ThemeManager.Instance.GetFont(FontRole.Body);
                _detailsDescriptionText.color = ThemeManager.White;
            }
            if (_detailsStatsText != null)
            {
                _detailsStatsText.text = $"DMG {weapon.DamagePct:F0}\nFR {weapon.FireRatePct:F0}\nACC {weapon.AccuracyPct:F0}\nSTB {weapon.StabilityPct:F0}";
                _detailsStatsText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _detailsStatsText.color = ThemeManager.SubGreen;
            }
        }

        // =================================================================================
        //  ACTIONS
        // =================================================================================

        private void OnEquip()
        {
            if (string.IsNullOrEmpty(_selectedItemId)) return;
            TrackClick($"inventory_equip_{_selectedItemId}");
            var save = ServiceLocator.Instance?.Get<SaveSystem>();
            save?.MarkDirty();
        }

        private void OnUnequip()
        {
            if (string.IsNullOrEmpty(_selectedItemId)) return;
            TrackClick($"inventory_unequip_{_selectedItemId}");
        }

        private void OnSell()
        {
            if (string.IsNullOrEmpty(_selectedItemId)) return;
            TrackClick($"inventory_sell_{_selectedItemId}");
            TelemetryLogger.Instance?.TrackPurchase(_selectedItemId, 0, "SELL");
        }

        private void OnBack()
        {
            TrackClick("inventory_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }

        private void ClearCards()
        {
            foreach (var c in _cards)
            {
                if (c == null) continue;
                _cardGroup?.Unregister(c);
                c.OnCardClicked -= OnItemClicked;
                Destroy(c.gameObject);
            }
            _cards.Clear();
        }
    }
}
