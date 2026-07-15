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
    /// Armurerie (sélection d'arme détaillée) — PDF page 5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 5</b> :
    /// <list type="bullet">
    /// <item>Cartes d'armes (HEAVY RX-14, GUARD V-9, FRAG-X).</item>
    /// <item>Mapping dynamique d'attributs (damage/utility par type d'arme).</item>
    /// <item>Barres de stats segmentées vertes (damage, fire rate, accuracy, stability).</item>
    /// <item>Render de l'arme (3D ou image).</item>
    /// <item>Affichage Power / Reload.</item>
    /// <item>Bouton SAVE.</item>
    /// <item>Carousel d'armes (RIFLE CX-24, AX-9 SR, CX-27 ATLAS, C-2, etc.).</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/ArmoryScreen")]
    [DisallowMultipleComponent]
    public sealed class ArmoryScreen : UIScreen
    {
        [Header("Carousel armes")]
        [Tooltip("Conteneur du carousel horizontal d'armes.")]
        [SerializeField] private RectTransform _weaponCarousel;
        [Tooltip("Prefab de carte arme (KCard).")]
        [SerializeField] private KCard _weaponCardPrefab;
        [Tooltip("CardGroup pour sélection exclusive.")]
        [SerializeField] private KCardGroup _weaponGroup;
        [Tooltip("Catégorie filtrée (Primary par défaut).")]
        [SerializeField] private WeaponCategory _filterCategory = WeaponCategory.Primary;
        [Tooltip("Boutons de filtre de catégorie.")]
        [SerializeField] private KButton[] _categoryFilterButtons;

        [Header("Arme sélectionnée")]
        [Tooltip("Image du render de l'arme.")]
        [SerializeField] private Image _weaponRender;
        [Tooltip("Texte nom de l'arme.")]
        [SerializeField] private TMP_Text _weaponNameText;
        [Tooltip("Texte catégorie / type.")]
        [SerializeField] private TMP_Text _weaponTypeText;
        [Tooltip("Texte Power (ex: 1500).")]
        [SerializeField] private TMP_Text _powerText;
        [Tooltip("Texte Reload (ex: 3.2s).")]
        [SerializeField] private TMP_Text _reloadText;
        [Tooltip("Texte magazine size.")]
        [SerializeField] private TMP_Text _magazineText;
        [Tooltip("Texte rareté.")]
        [SerializeField] private TMP_Text _rarityText;
        [Tooltip("Image du badge rareté.")]
        [SerializeField] private Image _rarityBadge;

        [Header("Stats segmentées")]
        [Tooltip("Barre DAMAGE (vert).")]
        [SerializeField] private KProgressBar _damageBar;
        [Tooltip("Barre FIRE RATE (vert).")]
        [SerializeField] private KProgressBar _fireRateBar;
        [Tooltip("Barre ACCURACY (vert).")]
        [SerializeField] private KProgressBar _accuracyBar;
        [Tooltip("Barre STABILITY (vert).")]
        [SerializeField] private KProgressBar _stabilityBar;
        [Tooltip("Barre RANGE (vert).")]
        [SerializeField] private KProgressBar _rangeBar;
        [Tooltip("Conteneur des attributs dynamiques (labels + valeurs).")]
        [SerializeField] private RectTransform _dynamicAttributesContainer;
        [Tooltip("Prefab d'une ligne attribut dynamique (label + barre).")]
        [SerializeField] private GameObject _attributeRowPrefab;

        [Header("Actions")]
        [Tooltip("Bouton SAVE.")]
        [SerializeField] private KButton _saveButton;
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;

        private readonly List<KCard> _cards = new(16);
        private readonly List<GameObject> _attributeRows = new(8);
        private string _selectedWeaponId;

        protected override void Awake()
        {
            _screenType = ScreenType.Armory;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();

            // Filtres catégorie.
            for (int i = 0; i < _categoryFilterButtons.Length; i++)
            {
                int idx = i;
                var label = idx switch
                {
                    0 => "PRIMARY",
                    1 => "SECONDARY",
                    2 => "TACTICAL",
                    _ => "PRIMARY"
                };
                _categoryFilterButtons[i].SetText(label);
                _categoryFilterButtons[i].OnKClick += _ => SetCategoryFilter((WeaponCategory)idx);
            }

            if (_saveButton != null)
            {
                _saveButton.SetLocalizationKey("common.save", "SAVE");
                _saveButton.OnKClick += _ => OnSave();
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            BuildWeaponCards();
            // Sélectionne la première carte.
            if (_cards.Count > 0) _cards[0].OnCardClicked?.Invoke(_cards[0]);
            TrackClick("armory_show");
        }

        protected override void OnHide()
        {
            ClearCards();
            ClearAttributeRows();
        }

        // =================================================================================
        //  CAROUSEL
        // =================================================================================

        private void BuildWeaponCards()
        {
            ClearCards();
            if (_weaponCarousel == null || _weaponCardPrefab == null) return;

            var weapons = DataLoader.GetWeaponsByCategory(_filterCategory);
            foreach (var w in weapons)
            {
                var card = Instantiate(_weaponCardPrefab, _weaponCarousel);
                card.Bind(w, false);
                card.OnCardClicked += OnWeaponCardClicked;
                _cards.Add(card);
                _weaponGroup?.Register(card);
            }
        }

        private void SetCategoryFilter(WeaponCategory category)
        {
            if (category == _filterCategory) return;
            _filterCategory = category;
            TrackClick($"armory_filter_{category}");
            BuildWeaponCards();
            if (_cards.Count > 0) _cards[0].OnCardClicked?.Invoke(_cards[0]);
        }

        private void OnWeaponCardClicked(KCard card)
        {
            if (card == null || card.IsLocked) return;
            _selectedWeaponId = card.ItemId;
            var weapon = DataLoader.GetWeapon(_selectedWeaponId);
            if (weapon == null) return;
            DisplaySelectedWeapon(weapon);
        }

        // =================================================================================
        //  AFFICHAGE ARME SÉLECTIONNÉE
        // =================================================================================

        private void DisplaySelectedWeapon(WeaponDto weapon)
        {
            if (_weaponNameText != null)
            {
                _weaponNameText.text = weapon.DisplayName;
                _weaponNameText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _weaponNameText.color = ThemeManager.Main;
            }
            if (_weaponTypeText != null)
            {
                _weaponTypeText.text = $"{weapon.Category} • {weapon.Type}";
                _weaponTypeText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _weaponTypeText.color = ThemeManager.TextMuted;
            }
            if (_powerText != null)
            {
                _powerText.text = $"PWR {weapon.Power}";
                _powerText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _powerText.color = ThemeManager.SubYellow;
            }
            if (_reloadText != null)
            {
                _reloadText.text = $"{weapon.ReloadTime:F1}s RELOAD";
                _reloadText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _reloadText.color = ThemeManager.White;
            }
            if (_magazineText != null)
            {
                _magazineText.text = $"{weapon.MagazineSize} MAG";
                _magazineText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _magazineText.color = ThemeManager.White;
            }
            if (_rarityText != null)
            {
                _rarityText.text = weapon.Rarity.ToString().ToUpperInvariant();
                _rarityText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _rarityText.color = ThemeManager.ColorForRarity(weapon.Rarity);
            }
            if (_rarityBadge != null) _rarityBadge.color = ThemeManager.ColorForRarity(weapon.Rarity);

            // Barres de stats segmentées (vertes).
            ConfigureStatBar(_damageBar, StatBarType.Damage, weapon.DamagePct, 100f);
            ConfigureStatBar(_fireRateBar, StatBarType.Damage, weapon.FireRatePct, 100f);
            ConfigureStatBar(_accuracyBar, StatBarType.Damage, weapon.AccuracyPct, 100f);
            ConfigureStatBar(_stabilityBar, StatBarType.Damage, weapon.StabilityPct, 100f);
            ConfigureStatBar(_rangeBar, StatBarType.Damage, weapon.Range, 200f);

            // Attributs dynamiques selon la catégorie.
            BuildDynamicAttributes(weapon);
        }

        private void BuildDynamicAttributes(WeaponDto weapon)
        {
            ClearAttributeRows();
            if (_dynamicAttributesContainer == null || _attributeRowPrefab == null) return;

            // Mapping dynamique : damage stats (toutes) + utility stats (explosion radius, fuse time pour tactiques).
            var damageStats = new (string label, float value, float max)[]
            {
                ("DAMAGE", weapon.DamagePct, 100f),
                ("FIRE RATE", weapon.FireRatePct, 100f),
                ("ACCURACY", weapon.AccuracyPct, 100f),
                ("STABILITY", weapon.StabilityPct, 100f),
                ("RANGE", weapon.Range, 200f),
                ("MAGAZINE", weapon.MagazineSize, 100f),
            };
            foreach (var s in damageStats)
            {
                CreateAttributeRow(s.label, s.value, s.max);
            }

            // Utility stats (spécifiques tactiques / lourds).
            if (weapon.Category == WeaponCategory.Tactical)
            {
                CreateAttributeRow("EXPLOSION RADIUS", weapon.ExplosionRadiusPct, 100f);
                CreateAttributeRow("FUSE TIME", weapon.FuseTime, 10f);
            }
            if (weapon.Projectile != null)
            {
                CreateAttributeRow("PROJECTILE SPEED", weapon.Projectile.Speed, 1000f);
                CreateAttributeRow("PENETRATION", weapon.Projectile.Penetration * 100f, 100f);
            }
        }

        private void CreateAttributeRow(string label, float value, float max)
        {
            var row = Instantiate(_attributeRowPrefab, _dynamicAttributesContainer);
            _attributeRows.Add(row);

            var labelTxt = row.GetComponentInChildren<TMP_Text>();
            if (labelTxt != null)
            {
                labelTxt.text = label;
                labelTxt.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                labelTxt.color = ThemeManager.TextMuted;
            }
            var bar = row.GetComponentInChildren<KProgressBar>();
            if (bar != null)
            {
                bar.SetType(StatBarType.Damage);
                bar.SetRange(0f, max);
                bar.Value = value;
            }
        }

        // =================================================================================
        //  ACTIONS
        // =================================================================================

        private void OnSave()
        {
            TrackClick("armory_save");
            var save = ServiceLocator.Instance?.Get<SaveSystem>();
            save?.MarkDirty();
        }

        private void OnBack()
        {
            TrackClick("armory_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack()
        {
            OnBack();
            return true;
        }

        // =================================================================================
        //  HELPERS
        // =================================================================================

        private void ConfigureStatBar(KProgressBar bar, StatBarType type, float value, float max)
        {
            if (bar == null) return;
            bar.SetType(type);
            bar.SetRange(0f, max);
            bar.Value = value;
        }

        private void ClearCards()
        {
            foreach (var c in _cards)
            {
                if (c == null) continue;
                _weaponGroup?.Unregister(c);
                c.OnCardClicked -= OnWeaponCardClicked;
                Destroy(c.gameObject);
            }
            _cards.Clear();
        }

        private void ClearAttributeRows()
        {
            foreach (var r in _attributeRows)
            {
                if (r != null) Destroy(r);
            }
            _attributeRows.Clear();
        }
    }
}
