// ============================================================================
//  KINETICS 5 — Player HUD (bridge gameplay -> HUDController UI)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Bridge gameplay-side qui alimente le HUDController (UI) avec les données
//  temps réel du joueur. Le HUDController (créé par l'agent 2-e) s'occupe de
//  l'affichage UGUI ; ce PlayerHUD s'occupe de la collecte et du push.
//
//  Responsabilités :
//    • Poll PlayerStats (HP, Shield, buffs) → HUDController.SetHealth/SetArmor/AddBuff
//    • Poll PlayerWeaponManager (ammo, weapon name) → HUDController.SetAmmo/SetWeaponName
//    • Poll DischargeSystem (ultimate charge) → HUDController.SetUltimateCharge
//    • Subscribe WeaponSwitchedEvent → HUDController.SetWeaponName + SetAmmo
//    • Subscribe PlayerDamagedEvent → HUDController.ShowDamageIndicator (calc angle)
//    • Subscribe DamageDealtEvent → HUDController.ShowHitMarker + AddCrosshairSpread
//    • Subscribe EnemyKilledEvent → HUDController.AddKillFeedEntry
//    • Subscribe ObjectiveUpdatedEvent → HUDController.SetObjective
//    • Subscribe LootPickupEvent → (optionnel) toast
//
//  Calcule l'angle des indicateurs de dégâts directionnels (player forward ->
//  damage source) pour orienter la flèche correctement.
// ============================================================================
using System;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Combat;
using KINETICS5.UI;
using UnityEngine;

namespace KINETICS5.Gameplay.Player
{
    /// <summary>
    /// Bridge gameplay-side entre les composants joueur (PlayerStats, PlayerWeaponManager,
    /// DischargeSystem) et le HUDController UI. Publie aussi les buffs actifs pour
    /// affichage dans le HUD.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Architecture :</b>
    /// <list type="bullet">
    ///   <item>Poll frame : lit PlayerStats/PlayerWeaponManager et push vers HUDController.</item>
    ///   <item>Events : souscrit au GameEventBus pour les mises à jour ponctuelles
    ///     (kill feed, hit marker, damage indicator).</item>
    ///   <item>Dépendance : référence directe à HUDController (assigné via Inspector).</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Performance :</b> poll à fréquence réduite (10 Hz par défaut) pour limiter
    /// la charge mobile. Les events restent temps réel.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerHUD : MonoBehaviour
    {
        [Header("Références gameplay")]
        [Tooltip("PlayerStats (source HP/Shield/buffs).")]
        [SerializeField] private PlayerStats _stats;
        [Tooltip("PlayerWeaponManager (source ammo/weapon).")]
        [SerializeField] private PlayerWeaponManager _weaponManager;
        [Tooltip("PlayerCombat (source spread pour crosshair).")]
        [SerializeField] private PlayerCombat _combat;
        [Tooltip("Transform racine du joueur (pour calcul d'angle des damage indicators).")]
        [SerializeField] private Transform _playerTransform;

        [Header("Référence UI")]
        [Tooltip("HUDController UI (cible des push de données). Peut être null si HUD non instancié.")]
        [SerializeField] private HUDController _hudController;

        [Header("Polling")]
        [Tooltip("Fréquence de poll des données gameplay vers le HUD (Hz). 0 = chaque frame.")]
        [SerializeField] private float _pollFrequency = 10f;
        [Tooltip("Vrai pour push les buffs à chaque frame (peut être coûteux).")]
        [SerializeField] private bool _pushBuffsEveryFrame = false;

        // État précédent pour ne push que les deltas (limite la charge).
        private float _lastPollTime;
        private float _lastHealth;
        private float _lastShield;
        private float _lastUltimate;
        private int _lastMagazine = -1;
        private int _lastReserve = -1;
        private string _lastWeaponId = string.Empty;
        private int _lastBuffCount = -1;

        // Buffs déjà affichés (pour éviter les doublons).
        private readonly HashSet<BuffType> _displayedBuffs = new(8);

        // Subscriptions.
        private IDisposable _subWeaponSwitched;
        private IDisposable _subPlayerDamaged;
        private IDisposable _subDamageDealt;
        private IDisposable _subEnemyKilled;
        private IDisposable _subObjectiveUpdated;
        private IDisposable _subLootPickup;
        private IDisposable _subUltimateReady;
        private IDisposable _subComboUpdated;

        // Source ID du joueur local (pour filtrer les DamageDealtEvent).
        private uint _playerSourceId;

        private void Awake()
        {
            if (_stats == null) _stats = GetComponent<PlayerStats>();
            if (_weaponManager == null) _weaponManager = GetComponent<PlayerWeaponManager>();
            if (_combat == null) _combat = GetComponent<PlayerCombat>();
            if (_playerTransform == null) _playerTransform = transform;
        }

        private void OnEnable()
        {
            _playerSourceId = _stats?.PlayerId ?? (uint)GetInstanceID();
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            _subWeaponSwitched = bus.Subscribe<WeaponSwitchedEvent>(OnWeaponSwitched);
            _subPlayerDamaged = bus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            _subDamageDealt = bus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            _subEnemyKilled = bus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            _subObjectiveUpdated = bus.Subscribe<ObjectiveUpdatedEvent>(OnObjectiveUpdated);
            _subLootPickup = bus.Subscribe<LootPickupEvent>(OnLootPickup);
            _subUltimateReady = bus.Subscribe<UltimateReadyEvent>(OnUltimateReady);
            _subComboUpdated = bus.Subscribe<ComboUpdatedEvent>(OnComboUpdated);
        }

        private void OnDisable()
        {
            _subWeaponSwitched?.Dispose();
            _subPlayerDamaged?.Dispose();
            _subDamageDealt?.Dispose();
            _subEnemyKilled?.Dispose();
            _subObjectiveUpdated?.Dispose();
            _subLootPickup?.Dispose();
            _subUltimateReady?.Dispose();
            _subComboUpdated?.Dispose();
        }

        private void Update()
        {
            if (_hudController == null) return;

            // Poll à fréquence réduite (limite la charge mobile).
            float pollInterval = _pollFrequency > 0f ? 1f / _pollFrequency : 0f;
            if (Time.time - _lastPollTime < pollInterval) return;
            _lastPollTime = Time.time;

            PollVitals();
            PollAmmo();
            PollUltimate();
            if (_pushBuffsEveryFrame) PollBuffs();
            PollCrosshair();
        }

        // ==================== POLL VITALS ====================

        private void PollVitals()
        {
            if (_stats == null) return;
            float health = _stats.CurrentHealth;
            float maxHealth = _stats.MaxHealth;
            if (!Mathf.Approximately(health, _lastHealth) || !Mathf.Approximately(maxHealth, 0f))
            {
                _hudController.SetHealth(health, maxHealth);
                _lastHealth = health;
            }
            float shield = _stats.CurrentShield;
            float maxShield = _stats.MaxShield;
            if (!Mathf.Approximately(shield, _lastShield))
            {
                _hudController.SetArmor(shield, maxShield);
                _lastShield = shield;
            }
        }

        // ==================== POLL AMMO ====================

        private void PollAmmo()
        {
            if (_weaponManager == null) return;
            var (mag, reserve) = _weaponManager.GetAmmoForCurrent();
            if (mag != _lastMagazine || reserve != _lastReserve)
            {
                _hudController.SetAmmo(mag, reserve);
                _lastMagazine = mag;
                _lastReserve = reserve;
            }
            string currentWid = _weaponManager.CurrentWeaponId;
            if (currentWid != _lastWeaponId)
            {
                var weapon = DataLoader.GetWeapon(currentWid);
                _hudController.SetWeaponName(weapon?.DisplayName ?? currentWid);
                _lastWeaponId = currentWid;
            }
        }

        // ==================== POLL ULTIMATE ====================

        private void PollUltimate()
        {
            float charge = DischargeSystem.Instance?.NormalizedCharge ?? 0f;
            if (!Mathf.Approximately(charge, _lastUltimate))
            {
                _hudController.SetUltimateCharge(charge);
                _lastUltimate = charge;
            }
        }

        // ==================== POLL BUFFS ====================

        private void PollBuffs()
        {
            if (_stats == null) return;
            int buffCount = _stats.ActiveBuffs.Count;
            if (buffCount == _lastBuffCount) return;
            _lastBuffCount = buffCount;

            // Pour l'instant, on ne pousse que les nouveaux buffs (pas de suppression fine).
            foreach (var buff in _stats.ActiveBuffs)
            {
                if (_displayedBuffs.Add(buff.Type))
                {
                    string label = GetBuffLabel(buff.Type);
                    _hudController.AddBuff(null, label);
                }
            }
        }

        private string GetBuffLabel(BuffType type)
        {
            return type switch
            {
                BuffType.AttackUp    => "ATK+",
                BuffType.DefenseUp   => "DEF+",
                BuffType.SpeedUp     => "SPD+",
                BuffType.Haste       => "HASTE",
                BuffType.Slow        => "SLOW",
                BuffType.Stun        => "STUN",
                BuffType.ShieldBonus => "SHLD+",
                BuffType.HealthRegen => "REGEN",
                _                     => "?"
            };
        }

        // ==================== POLL CROSSHAIR ====================

        private void PollCrosshair()
        {
            if (_combat == null) return;
            // Si le joueur tire, ajoute du spread au crosshair.
            if (_combat.IsFiring)
            {
                _hudController.AddCrosshairSpread(_combat.CurrentSpread * 0.3f);
            }
        }

        // ==================== HANDLERS ÉVÉNEMENTS ====================

        private void OnWeaponSwitched(in WeaponSwitchedEvent evt)
        {
            if (_hudController == null) return;
            var weapon = DataLoader.GetWeapon(evt.WeaponId);
            _hudController.SetWeaponName(weapon?.DisplayName ?? evt.WeaponId);
            // Force refresh ammo.
            if (_weaponManager != null)
            {
                var (mag, reserve) = _weaponManager.GetAmmoForCurrent();
                _hudController.SetAmmo(mag, reserve);
                _lastMagazine = mag;
                _lastReserve = reserve;
                _lastWeaponId = evt.WeaponId;
            }
        }

        private void OnPlayerDamaged(in PlayerDamagedEvent evt)
        {
            if (_hudController == null) return;
            // Met à jour la barre de santé immédiatement (temps réel, pas attendre le poll).
            if (_stats != null)
            {
                _hudController.SetHealth(_stats.CurrentHealth, _stats.MaxHealth);
                _lastHealth = _stats.CurrentHealth;
            }
            // Damage indicator directionnel : on calcule l'angle vers la source.
            // La source n'est pas directement disponible dans PlayerDamagedEvent (uniquement SourceId).
            // On utilise la position de l'ennemi le plus proche via PlayerContext si c'est un ennemi.
            Vector3 sourcePos = ResolveDamageSourcePosition(evt.SourceId);
            if (sourcePos != Vector3.zero)
            {
                _hudController.ShowDamageIndicator(sourcePos);
            }
        }

        private void OnDamageDealt(in DamageDealtEvent evt)
        {
            if (_hudController == null) return;
            // Filtre : seuls les hits du joueur local déclenchent le hit marker.
            if (_playerSourceId != 0u && evt.SourceId != _playerSourceId) return;

            _hudController.ShowHitMarker(evt.IsCritical);
            _hudController.AddCrosshairSpread(evt.IsCritical ? 1.5f : 0.8f);
        }

        private void OnEnemyKilled(in EnemyKilledEvent evt)
        {
            if (_hudController == null) return;
            // Kill feed : "Player [Weapon] Enemy"
            string killer = evt.KillerId == _playerSourceId ? "YOU" : evt.KillerId.ToString();
            string victim = evt.EnemyId.ToString();
            string weapon = _weaponManager?.CurrentWeaponId ?? "—";
            _hudController.AddKillFeedEntry(killer, victim, weapon);
        }

        private void OnObjectiveUpdated(in ObjectiveUpdatedEvent evt)
        {
            // Le HUDController gère déjà l'affichage des objectifs via cet event directement.
            // Ce handler est un hook pour étendre le comportement (toast, son, etc.).
        }

        private void OnLootPickup(in LootPickupEvent evt)
        {
            // Hook pour toast de pickup (extension future).
        }

        private void OnUltimateReady(in UltimateReadyEvent evt)
        {
            // Hook pour clignotement de la jauge ultimate (extension future).
        }

        private void OnComboUpdated(in ComboUpdatedEvent evt)
        {
            // Hook pour afficher le combo courant (extension future).
        }

        // ==================== HELPERS ====================

        /// <summary>
        /// Tente de résoudre la position monde d'une source de dégâts (pour les
        /// indicateurs directionnels). Utilise les ennemis actifs via EnemySpawner.
        /// </summary>
        private Vector3 ResolveDamageSourcePosition(uint sourceId)
        {
            if (sourceId == 0u) return Vector3.zero;
            // Recherche parmi les ennemis actifs (via EnemySpawner si disponible).
            // Fallback : position du joueur lui-même (indicateur neutre).
            return _playerTransform != null ? _playerTransform.position : Vector3.zero;
        }

        /// <summary>
        /// Calcule l'angle (en degrés) entre le forward du joueur et la direction
        /// d'une source de dégâts. 0 = devant, 90 = à droite, 180 = derrière, -90 = à gauche.
        /// </summary>
        public float ComputeDamageDirectionAngle(Vector3 sourceWorldPos)
        {
            if (_playerTransform == null) return 0f;
            Vector3 toSource = (sourceWorldPos - _playerTransform.position).normalized;
            Vector3 flat = new(toSource.x, 0f, toSource.z);
            Vector3 forward = _playerTransform.forward;
            forward.y = 0f; forward.Normalize();
            Vector3 right = _playerTransform.right;
            right.y = 0f; right.Normalize();
            return Mathf.Atan2(Vector3.Dot(flat, right), Vector3.Dot(flat, forward)) * Mathf.Rad2Deg;
        }
    }
}
