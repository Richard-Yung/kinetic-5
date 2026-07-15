using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;
using KINETICS5.Data;

namespace KINETICS5.UI
{
    /// <summary>
    /// Contrôleur du HUD de combat KINETICS 5 (PDF page 6).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 6</b> :
    /// <list type="bullet">
    /// <item>Barre de santé segmentée verte (5000 HP max selon PDF).</item>
    /// <item>Barre d'armure segmentée cyan.</item>
    /// <item>Compteur de munitions gros Audiowide (20/60 format).</item>
    /// <item>Nom de l'arme (RIFLE).</item>
    /// <item>Timer de mission format 12:39.</item>
    /// <item>Minimaps avec bouton HIDE MAP.</item>
    /// <item>Tracker d'objectifs.</item>
    /// <item>Jauge d'ultimate.</item>
    /// <item>Icônes de buffs.</item>
    /// <item>Kill feed (rolling log).</item>
    /// <item>Indicateurs de dégâts directionnels.</item>
    /// <item>Crosshair dynamique (s'écarte au tir).</item>
    /// <item>Hit marker (apparaît à l'impact).</item>
    /// </list>
    /// </para>
    /// <para>
    /// Le HUD est piloté par événements via <see cref="GameEventBus"/> :
    /// <c>PlayerDamagedEvent</c>, <c>DamageDealtEvent</c>, <c>WeaponSwitchedEvent</c>,
    /// <c>ObjectiveUpdatedEvent</c>, <c>EnemyKilledEvent</c>, <c>LootPickupEvent</c>.
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/HUD/HUDController")]
    [DisallowMultipleComponent]
    public sealed class HUDController : UIScreen
    {
        [Header("HUD - Vitals")]
        [Tooltip("Barre de santé segmentée verte (HUD page 6).")]
        [SerializeField] private KProgressBar _healthBar;
        [Tooltip("Barre d'armure segmentée cyan (HUD page 6).")]
        [SerializeField] private KProgressBar _armorBar;
        [Tooltip("Texte santé numérique (5000 HP).")]
        [SerializeField] private TMP_Text _healthText;
        [Tooltip("Texte armure numérique.")]
        [SerializeField] private TMP_Text _armorText;
        [Tooltip("Jauge d'ultimate (segmentée cyan).")]
        [SerializeField] private KProgressBar _ultimateBar;

        [Header("HUD - Munitions")]
        [Tooltip("Texte gros Audiowide '20/60' (munitions courantes/réserve).")]
        [SerializeField] private TMP_Text _ammoText;
        [Tooltip("Texte Audiowide 'RIFLE' (nom de l'arme).")]
        [SerializeField] private TMP_Text _weaponNameText;
        [Tooltip("Texte 'RELOAD' indicateur recharge.")]
        [SerializeField] private TMP_Text _reloadIndicator;

        [Header("HUD - Timer")]
        [Tooltip("Texte Audiowide format 12:39 (temps restant).")]
        [SerializeField] private TMP_Text _timerText;

        [Header("HUD - Minimap")]
        [Tooltip("Conteneur de la minimap.")]
        [SerializeField] private RectTransform _minimapContainer;
        [Tooltip("Bouton 'HIDE MAP' (toggle).")]
        [SerializeField] private KButton _hideMapButton;
        [Tooltip("Image de la minimap (raw).")]
        [SerializeField] private RawImage _minimapImage;

        [Header("HUD - Objectifs")]
        [Tooltip("Texte principal d'objectif (mission + objectif courant).")]
        [SerializeField] private TMP_Text _objectiveText;
        [Tooltip("Sous-texte d'objectif (progression 1/3).")]
        [SerializeField] private TMP_Text _objectiveProgressText;

        [Header("HUD - Buffs")]
        [Tooltip("Conteneur des icônes de buffs.")]
        [SerializeField] private RectTransform _buffContainer;
        [Tooltip("Prefab d'icône de buff.")]
        [SerializeField] private GameObject _buffIconPrefab;

        [Header("HUD - Kill Feed")]
        [Tooltip("Conteneur du kill feed (vertical scroll).")]
        [SerializeField] private RectTransform _killFeedContainer;
        [Tooltip("Prefab d'une ligne de kill feed.")]
        [SerializeField] private GameObject _killFeedEntryPrefab;
        [Tooltip("Durée d'affichage d'une ligne de kill feed (secondes).")]
        [SerializeField] private float _killFeedEntryLifetime = 4f;
        [Tooltip("Nombre max de lignes simultanées.")]
        [SerializeField] private int _maxKillFeedEntries = 5;

        [Header("HUD - Crosshair")]
        [Tooltip("Image du crosshair central (4 branches).")]
        [SerializeField] private RectTransform _crosshair;
        [Tooltip("Écart minimum du crosshair (au repos).")]
        [SerializeField] private float _crosshairMinSpread = 4f;
        [Tooltip("Écart maximum du crosshair (au tir / mouvement).")]
        [SerializeField] private float _crosshairMaxSpread = 24f;
        [Tooltip("Vitesse de récupération du crosshair.")]
        [Range(0.5f, 20f)][SerializeField] private float _crosshairRecovery = 6f;

        [Header("HUD - Hit Marker")]
        [Tooltip("Image du hit marker (X clignotant à l'impact).")]
        [SerializeField] private Image _hitMarker;
        [Tooltip("Durée d'affichage du hit marker (secondes).")]
        [SerializeField] private float _hitMarkerDuration = 0.18f;
        [Tooltip("Couleur du hit marker normal (blanc).")]
        [SerializeField] private Color _hitMarkerColor = Color.white;
        [Tooltip("Couleur du hit marker critique (rouge).")]
        [SerializeField] private Color _hitMarkerCriticalColor = new(1f, 0f, 0.13f, 1f); // #FE0022

        [Header("HUD - Damage Indicators")]
        [Tooltip("Conteneur des indicateurs de dégâts directionnels.")]
        [SerializeField] private RectTransform _damageIndicatorContainer;
        [Tooltip("Prefab d'un indicateur de dégâts (flèche rouge orientée).")]
        [SerializeField] private GameObject _damageIndicatorPrefab;
        [Tooltip("Durée d'affichage d'un indicateur (secondes).")]
        [SerializeField] private float _damageIndicatorDuration = 1.5f;

        [Header("HUD - État runtime")]
        [Tooltip("Santé courante du joueur.")]
        [SerializeField] private float _currentHealth = 5000f;
        [Tooltip("Santé max du joueur.")]
        [SerializeField] private float _maxHealth = 5000f;
        [Tooltip("Armure courante.")]
        [SerializeField] private float _currentArmor = 1500f;
        [Tooltip("Armure max.")]
        [SerializeField] private float _maxArmor = 1500f;
        [Tooltip("Charge d'ultimate (0..1).")]
        [Range(0f, 1f)][SerializeField] private float _ultimateCharge = 0f;
        [Tooltip("Munitions courantes dans le chargeur.")]
        [SerializeField] private int _currentAmmo = 20;
        [Tooltip("Munitions en réserve.")]
        [SerializeField] private int _reserveAmmo = 60;
        [Tooltip("Temps restant en secondes (mission).")]
        [SerializeField] private float _missionTimeRemaining = 759f; // 12:39

        private readonly List<GameObject> _activeKillFeedEntries = new(8);
        private readonly List<GameObject> _activeBuffs = new(8);
        private float _currentCrosshairSpread;
        private float _hitMarkerTimer;
        private float _reloadTimer;
        private bool _isReloading;

        private IDisposable _subDamageDealt;
        private IDisposable _subPlayerDamaged;
        private IDisposable _subWeaponSwitched;
        private IDisposable _subObjectiveUpdated;
        private IDisposable _subEnemyKilled;
        private IDisposable _subLootPickup;

        // =================================================================================
        //  CYCLE DE VIE
        // =================================================================================

        protected override void Awake()
        {
            base.Awake();
            _screenType = ScreenType.HUD;
        }

        protected override void InitBindings()
        {
            // Configuration initiale des barres.
            if (_healthBar != null)
            {
                _healthBar.SetType(StatBarType.Health);
                _healthBar.SetRange(0f, _maxHealth);
                _healthBar.Value = _currentHealth;
            }
            if (_armorBar != null)
            {
                _armorBar.SetType(StatBarType.Armor);
                _armorBar.SetRange(0f, _maxArmor);
                _armorBar.Value = _currentArmor;
            }
            if (_ultimateBar != null)
            {
                _ultimateBar.SetType(StatBarType.Ultimate);
                _ultimateBar.SetRange(0f, 1f);
                _ultimateBar.Value = _ultimateCharge;
            }
            if (_hideMapButton != null)
            {
                _hideMapButton.SetText(L("hud.minimap.hide", "HIDE MAP"));
                _hideMapButton.OnKClick += _ => ToggleMinimap();
            }
            if (_hitMarker != null) _hitMarker.color = new Color(1f, 1f, 1f, 0f);
            if (_reloadIndicator != null) _reloadIndicator.gameObject.SetActive(false);
            UpdateAmmoDisplay();
            UpdateTimerDisplay(_missionTimeRemaining);
        }

        protected override void OnShow(object payload)
        {
            SubscribeEvents();
            TrackClick("hud_show");
        }

        protected override void OnHide()
        {
            UnsubscribeEvents();
        }

        protected override void RefreshLocalization()
        {
            base.RefreshLocalization();
            if (_hideMapButton != null)
                _hideMapButton.SetText(L("hud.minimap.hide", "HIDE MAP"));
        }

        private void Update()
        {
            if (!IsVisible) return;

            // Timer mission.
            if (_missionTimeRemaining > 0f)
            {
                _missionTimeRemaining -= Time.deltaTime;
                if (_missionTimeRemaining < 0f) _missionTimeRemaining = 0f;
                UpdateTimerDisplay(_missionTimeRemaining);
            }

            // Crosshair recovery.
            if (_currentCrosshairSpread > _crosshairMinSpread)
            {
                _currentCrosshairSpread = Mathf.Lerp(
                    _currentCrosshairSpread,
                    _crosshairMinSpread,
                    Time.deltaTime * _crosshairRecovery);
                ApplyCrosshairSpread();
            }

            // Hit marker fade-out.
            if (_hitMarkerTimer > 0f)
            {
                _hitMarkerTimer -= Time.deltaTime;
                if (_hitMarkerTimer <= 0f && _hitMarker != null)
                {
                    _hitMarker.DOFade(0f, 0.08f).SetUpdate(true);
                }
            }

            // Reload timer.
            if (_isReloading)
            {
                _reloadTimer -= Time.deltaTime;
                if (_reloadTimer <= 0f) FinishReload();
            }
        }

        // =================================================================================
        //  API PUBLIQUE
        // =================================================================================

        /// <summary>Définit la santé courante (rafraîchit la barre).</summary>
        public void SetHealth(float current, float max)
        {
            _currentHealth = Mathf.Clamp(current, 0f, max);
            _maxHealth = Mathf.Max(0.1f, max);
            if (_healthBar != null)
            {
                _healthBar.SetRange(0f, _maxHealth);
                _healthBar.Value = _currentHealth;
            }
            if (_healthText != null)
                _healthText.text = Mathf.RoundToInt(_currentHealth).ToString();
        }

        /// <summary>Définit l'armure courante.</summary>
        public void SetArmor(float current, float max)
        {
            _currentArmor = Mathf.Clamp(current, 0f, max);
            _maxArmor = Mathf.Max(0.1f, max);
            if (_armorBar != null)
            {
                _armorBar.SetRange(0f, _maxArmor);
                _armorBar.Value = _currentArmor;
            }
            if (_armorText != null)
                _armorText.text = Mathf.RoundToInt(_currentArmor).ToString();
        }

        /// <summary>Définit la charge d'ultimate (0..1).</summary>
        public void SetUltimateCharge(float normalized)
        {
            _ultimateCharge = Mathf.Clamp01(normalized);
            if (_ultimateBar != null) _ultimateBar.Value = _ultimateCharge;
        }

        /// <summary>Définit les munitions (courantes / réserve).</summary>
        public void SetAmmo(int current, int reserve)
        {
            _currentAmmo = Mathf.Max(0, current);
            _reserveAmmo = Mathf.Max(0, reserve);
            UpdateAmmoDisplay();
        }

        /// <summary>Définit le nom de l'arme affiché.</summary>
        public void SetWeaponName(string name)
        {
            if (_weaponNameText != null) _weaponNameText.text = name ?? string.Empty;
        }

        /// <summary>Déclenche une animation de recharge.</summary>
        public void StartReload(float durationSec)
        {
            _isReloading = true;
            _reloadTimer = Mathf.Max(0.1f, durationSec);
            if (_reloadIndicator != null) _reloadIndicator.gameObject.SetActive(true);
        }

        /// <summary>Ajoute du spread au crosshair (tir / mouvement).</summary>
        public void AddCrosshairSpread(float amount)
        {
            _currentCrosshairSpread = Mathf.Min(_crosshairMaxSpread, _currentCrosshairSpread + amount);
            ApplyCrosshairSpread();
        }

        /// <summary>Affiche le hit marker (impact sur ennemi).</summary>
        public void ShowHitMarker(bool critical = false)
        {
            if (_hitMarker == null) return;
            _hitMarkerTimer = _hitMarkerDuration;
            _hitMarker.color = critical ? _hitMarkerCriticalColor : _hitMarkerColor;
            _hitMarker.DOKill();
            _hitMarker.DOFade(1f, 0.04f).SetUpdate(true);
        }

        /// <summary>Ajoute une entrée au kill feed.</summary>
        public void AddKillFeedEntry(string killer, string victim, string weapon)
        {
            if (_killFeedContainer == null || _killFeedEntryPrefab == null) return;
            var entry = Instantiate(_killFeedEntryPrefab, _killFeedContainer);
            _activeKillFeedEntries.Add(entry);
            var texts = entry.GetComponentsInChildren<TMP_Text>();
            if (texts.Length >= 3)
            {
                texts[0].text = killer;
                texts[1].text = weapon;
                texts[2].text = victim;
            }
            else if (texts.Length >= 1)
            {
                texts[0].text = $"{killer}  [{weapon}]  {victim}";
            }
            // Fade-out puis destruction.
            var cg = entry.GetComponent<CanvasGroup>();
            if (cg == null) cg = entry.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.15f).SetUpdate(true);
            DOVirtual.DelayedCall(_killFeedEntryLifetime, () =>
            {
                if (entry == null) return;
                cg.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() =>
                {
                    if (entry != null) Destroy(entry);
                    _activeKillFeedEntries.Remove(entry);
                });
            }).SetUpdate(true);

            // Limite max.
            while (_activeKillFeedEntries.Count > _maxKillFeedEntries)
            {
                var oldest = _activeKillFeedEntries[0];
                _activeKillFeedEntries.RemoveAt(0);
                if (oldest != null) Destroy(oldest);
            }
        }

        /// <summary>Ajoute un buff (icône) au conteneur.</summary>
        public void AddBuff(Sprite icon, string label)
        {
            if (_buffContainer == null || _buffIconPrefab == null) return;
            var go = Instantiate(_buffIconPrefab, _buffContainer);
            _activeBuffs.Add(go);
            var img = go.GetComponentInChildren<Image>();
            if (img != null && icon != null) { img.sprite = icon; img.color = ThemeManager.SubGreen; }
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = label;
        }

        /// <summary>Affiche un indicateur de dégâts directionnel (depuis une source 3D).</summary>
        public void ShowDamageIndicator(Vector3 worldSource)
        {
            if (_damageIndicatorContainer == null || _damageIndicatorPrefab == null) return;
            var player = Camera.main;
            if (player == null) return;
            var toSource = (worldSource - player.transform.position).normalized;
            var flat = new Vector3(toSource.x, 0f, toSource.z);
            var forward = player.transform.forward; forward.y = 0f; forward.Normalize();
            var right = player.transform.right; right.y = 0f; right.Normalize();
            var angle = Mathf.Atan2(Vector3.Dot(flat, right), Vector3.Dot(flat, forward)) * Mathf.Rad2Deg;

            var go = Instantiate(_damageIndicatorPrefab, _damageIndicatorContainer);
            var rt = go.transform as RectTransform;
            if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, -angle);
            var img = go.GetComponentInChildren<Image>();
            if (img != null) img.color = ThemeManager.SubRed;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.DOFade(0f, _damageIndicatorDuration).SetUpdate(true).OnComplete(() =>
            {
                if (go != null) Destroy(go);
            });
        }

        /// <summary>Bascule la visibilité de la minimap (bouton HIDE MAP).</summary>
        public void ToggleMinimap()
        {
            if (_minimapContainer == null) return;
            var isActive = !_minimapContainer.gameObject.activeSelf;
            _minimapContainer.gameObject.SetActive(isActive);
            if (_hideMapButton != null)
                _hideMapButton.SetText(isActive ? L("hud.minimap.hide", "HIDE MAP") : L("hud.minimap.show", "SHOW MAP"));
            TrackClick("toggle_minimap");
        }

        // =================================================================================
        //  SOUSCRIPTIONS EVENTS
        // =================================================================================

        private void SubscribeEvents()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            _subDamageDealt = bus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            _subPlayerDamaged = bus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            _subWeaponSwitched = bus.Subscribe<WeaponSwitchedEvent>(OnWeaponSwitched);
            _subObjectiveUpdated = bus.Subscribe<ObjectiveUpdatedEvent>(OnObjectiveUpdated);
            _subEnemyKilled = bus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            _subLootPickup = bus.Subscribe<LootPickupEvent>(OnLootPickup);
        }

        private void UnsubscribeEvents()
        {
            _subDamageDealt?.Dispose();
            _subPlayerDamaged?.Dispose();
            _subWeaponSwitched?.Dispose();
            _subObjectiveUpdated?.Dispose();
            _subEnemyKilled?.Dispose();
            _subLootPickup?.Dispose();
            _subDamageDealt = _subPlayerDamaged = _subWeaponSwitched = null;
            _subObjectiveUpdated = _subEnemyKilled = _subLootPickup = null;
        }

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            // Hit marker + crosshair spread.
            ShowHitMarker(evt.IsCritical);
            AddCrosshairSpread(2f);
        }

        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            // Mise à jour barre santé + indicateur directionnel.
            SetHealth(evt.NewHealth, _maxHealth);
            // La source n'est pas un Vector3, on ne peut pas orienter précisément.
            // On utilise une direction aléatoire comme fallback visuel.
            if (_damageIndicatorContainer != null && Camera.main != null)
            {
                var fakePos = Camera.main.transform.position + Camera.main.transform.forward * 5f;
                ShowDamageIndicator(fakePos);
            }
        }

        private void OnWeaponSwitched(WeaponSwitchedEvent evt)
        {
            var weapon = DataLoader.GetWeapon(evt.WeaponId);
            if (weapon != null)
            {
                SetWeaponName(weapon.DisplayName.ToUpperInvariant());
                SetAmmo(weapon.MagazineSize, weapon.MagazineSize * 2);
                if (_isReloading) FinishReload();
            }
        }

        private void OnObjectiveUpdated(ObjectiveUpdatedEvent evt)
        {
            if (_objectiveText != null) _objectiveText.text = evt.ObjectiveId;
            if (_objectiveProgressText != null)
                _objectiveProgressText.text = $"{evt.Current}/{evt.Required}";
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            var enemy = DataLoader.GetEnemy(evt.EnemyId.ToString());
            var name = enemy != null ? enemy.DisplayName : $"ENEMY-{evt.EnemyId}";
            AddKillFeedEntry("YOU", name, "WEAPON");
        }

        private void OnLootPickup(LootPickupEvent evt)
        {
            // Pour l'instant pas de widget loot dédié dans le HUD — extension future.
            // On log juste telemetry.
            TrackClick($"loot_{evt.ItemId}");
        }

        // =================================================================================
        //  HELPERS INTERNES
        // =================================================================================

        private void UpdateAmmoDisplay()
        {
            if (_ammoText == null) return;
            // Format gros Audiowide "20 | 60" (courant | réserve).
            _ammoText.text = $"{_currentAmmo} | {_reserveAmmo}";
        }

        private void UpdateTimerDisplay(float seconds)
        {
            if (_timerText == null) return;
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            var min = total / 60;
            var sec = total % 60;
            _timerText.text = $"{min:00}:{sec:00}";
            // Couleur rouge si < 60s.
            _timerText.color = seconds < 60f ? ThemeManager.SubRed : ThemeManager.White;
        }

        private void ApplyCrosshairSpread()
        {
            if (_crosshair == null) return;
            // Les 4 branches sont supposées enfants du _crosshair RectTransform.
            // On écarte simplement en scale local.
            var s = 1f + (_currentCrosshairSpread - _crosshairMinSpread) * 0.05f;
            _crosshair.localScale = Vector3.one * s;
        }

        private void FinishReload()
        {
            _isReloading = false;
            _reloadTimer = 0f;
            if (_reloadIndicator != null) _reloadIndicator.gameObject.SetActive(false);
        }
    }
}
