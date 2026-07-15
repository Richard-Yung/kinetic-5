using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Boutique — featured, bundles, currency, cosmetics.
    /// </summary>
    /// <remarks>
    /// CR (monnaie earned) + premium currency. Buy confirm modal. Owned state.
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/ShopScreen")]
    [DisallowMultipleComponent]
    public sealed class ShopScreen : UIScreen
    {
        [Header("Tabs")]
        [Tooltip("Boutons de tab (FEATURED, BUNDLES, CURRENCY, COSMETICS).")]
        [SerializeField] private KButton[] _tabButtons;
        [Tooltip("Panneaux de tab.")]
        [SerializeField] private RectTransform[] _tabPanels;

        [Header("Grille")]
        [Tooltip("Conteneur des cartes d'articles.")]
        [SerializeField] private RectTransform _itemsContainer;
        [Tooltip("Prefab de carte d'article.")]
        [SerializeField] private KCard _itemCardPrefab;

        [Header("Devises")]
        [Tooltip("Texte CR courant.")]
        [SerializeField] private TMP_Text _crText;
        [Tooltip("Texte monnaie premium (gemmes).")]
        [SerializeField] private TMP_Text _premiumText;

        [Header("Modal achat")]
        [Tooltip("Référence à la modal KModal pour confirmation d'achat.")]
        [SerializeField] private KModal _buyModal;

        [Header("Navigation")]
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;

        private int _currentTab;
        private readonly List<KCard> _cards = new(32);
        private string _pendingItemId;
        private int _pendingPrice;

        protected override void Awake()
        {
            _screenType = ScreenType.Shop;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int idx = i;
                var label = idx switch { 0 => "FEATURED", 1 => "BUNDLES", 2 => "CURRENCY", 3 => "COSMETICS", _ => "FEATURED" };
                _tabButtons[i].SetText(label);
                _tabButtons[i].OnKClick += _ => SelectTab(idx);
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            SelectTab(0);
            RefreshCurrencyDisplay();
            TrackClick("shop_show");
        }

        protected override void OnHide()
        {
            ClearCards();
        }

        // =================================================================================
        //  TABS
        // =================================================================================

        private void SelectTab(int index)
        {
            _currentTab = index;
            for (int i = 0; i < _tabPanels.Length; i++)
            {
                if (_tabPanels[i] != null) _tabPanels[i].gameObject.SetActive(i == index);
            }
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null) _tabButtons[i].SetSelected(i == index);
            }
            BuildItems();
            TrackClick($"shop_tab_{index}");
        }

        private void BuildItems()
        {
            ClearCards();
            if (_itemsContainer == null || _itemCardPrefab == null) return;
            // Pour la démo : on instancie quelques cartes placeholder (extension future : data-driven).
            for (int i = 0; i < 6; i++)
            {
                var card = Instantiate(_itemCardPrefab, _itemsContainer);
                var label = $"{_currentTab}_{i}";
                card.ItemId = label;
                card.Bind(null, false);
                card.OnCardClicked += OnItemClicked;
                _cards.Add(card);
            }
        }

        private void OnItemClicked(KCard card)
        {
            if (card == null) return;
            _pendingItemId = card.ItemId;
            _pendingPrice = 100; // Prix factice.
            if (_buyModal != null)
            {
                _ = _buyModal.ConfirmAsync(
                    "CONFIRM PURCHASE",
                    $"{card.ItemId} for {_pendingPrice} CR?",
                    "BUY", "CANCEL");
                _buyModal.OnClosed += OnPurchaseResolved;
            }
            else
            {
                // Sans modal : achat direct.
                OnPurchaseResolved(true);
            }
        }

        private void OnPurchaseResolved(bool confirmed)
        {
            if (_buyModal != null) _buyModal.OnClosed -= OnPurchaseResolved;
            if (!confirmed) return;
            TelemetryLogger.Instance?.TrackPurchase(_pendingItemId, _pendingPrice, "CR");
            RefreshCurrencyDisplay();
            TrackClick($"shop_buy_{_pendingItemId}");
        }

        // =================================================================================
        //  HELPERS
        // =================================================================================

        private void RefreshCurrencyDisplay()
        {
            if (_crText != null)
            {
                _crText.text = "CR 0";
                _crText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _crText.color = ThemeManager.SubYellow;
            }
            if (_premiumText != null)
            {
                _premiumText.text = "0";
                _premiumText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _premiumText.color = ThemeManager.Main;
            }
        }

        private void OnBack()
        {
            TrackClick("shop_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }

        private void ClearCards()
        {
            foreach (var c in _cards)
            {
                if (c == null) continue;
                c.OnCardClicked -= OnItemClicked;
                Destroy(c.gameObject);
            }
            _cards.Clear();
        }
    }
}
