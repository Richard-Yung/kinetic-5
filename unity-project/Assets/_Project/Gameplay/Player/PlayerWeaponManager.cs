// ============================================================================
//  KINETICS 5 — Player Weapon Manager (3 slots + reload + ultimate meter)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Gère l'arsenal du joueur :
//    • 3 slots : Primary, Secondary, Tactical
//    • Switch par input (touches 1/2/3, swap button, swipe)
//    • Équipement depuis PlayerInventory
//    • Suivi munitions par arme (magasin + réserve)
//    • Reload (temps, animation via PlayerAnimator, transfert de munitions)
//    • Animation de switch (délégation PlayerAnimator)
//    • Ultimate meter (délégation DischargeSystem)
//    • Publication WeaponSwitchedEvent sur le bus
// ============================================================================
using System;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Combat;
using UnityEngine;

namespace KINETICS5.Gameplay.Player
{
    /// <summary>
    /// Index de slot d'arme.
    /// </summary>
    public enum WeaponSlot
    {
        /// <summary>Arme principale (fusil, sniper, lourd).</summary>
        Primary = 0,
        /// <summary>Arme secondaire (pistolet).</summary>
        Secondary = 1,
        /// <summary>Arme tactique (grenade, gadget).</summary>
        Tactical = 2
    }

    /// <summary>
    /// État runtime d'une arme équipée (magasin + réserve + reload).
    /// </summary>
    [Serializable]
    public sealed class WeaponRuntimeState
    {
        /// <summary>Id de l'arme.</summary>
        public string WeaponId = string.Empty;
        /// <summary>Munitions dans le chargeur.</summary>
        public int MagazineAmmo;
        /// <summary>Munitions en réserve.</summary>
        public int ReserveAmmo;
        /// <summary>Vrai si l'arme est en cours de reload.</summary>
        public bool IsReloading;
        /// <summary>Temps restant pour le reload en cours (s).</summary>
        public float ReloadTimer;
    }

    /// <summary>
    /// Manager d'armes du joueur. Gère 3 slots, le switch, les munitions, le
    /// reload et l'ultimate meter (délégué à <see cref="DischargeSystem"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Architecture :</b>
    /// <list type="bullet">
    ///   <item>Résolution des données arme via <see cref="DataLoader.GetWeapon"/>.</item>
    ///   <item>Munitions : magasin + réserve par slot (taille chargeur depuis WeaponDto).</item>
    ///   <item>Reload : coroutine timer + callback PlayerAnimator pour l'animation.</item>
    ///   <item>Switch : input 1/2/3 (desktop) ou swipe (mobile) ou bouton swap.</item>
    ///   <item>Ultimate meter : délègue à <see cref="DischargeSystem"/> (source unique de vérité).</item>
    /// </list>
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponManager : MonoBehaviour
    {
        [Header("Slots")]
        [Tooltip("Référence au composant inventaire (pour résolution des armes équipées).")]
        [SerializeField] private PlayerInventory _inventory;
        [Tooltip("Référence au PlayerAnimator (pour animations fire/reload/switch).")]
        [SerializeField] private PlayerAnimator _animator;

        [Header("Munitions par défaut")]
        [Tooltip("Munitions de réserve par défaut pour les armes primaires.")]
        [SerializeField] private int _defaultPrimaryReserve = 120;
        [Tooltip("Munitions de réserve par défaut pour les armes secondaires.")]
        [SerializeField] private int _defaultSecondaryReserve = 60;
        [Tooltip("Nombre de tactiques (grenades) par défaut.")]
        [SerializeField] private int _defaultTacticalCount = 3;

        [Header("Switch")]
        [Tooltip("Durée de l'animation de switch d'arme (s).")]
        [SerializeField] private float _switchDuration = 0.4f;

        /// <summary>Slot courant (0=Primary, 1=Secondary, 2=Tactical).</summary>
        public WeaponSlot CurrentSlot { get; private set; } = WeaponSlot.Primary;
        /// <summary>Vrai si un switch est en cours (animation).</summary>
        public bool IsSwitching { get; private set; }
        /// <summary>Id de l'arme actuellement équipée (slot courant).</summary>
        public string CurrentWeaponId => _states[(int)CurrentSlot]?.WeaponId ?? string.Empty;
        /// <summary>WeaponDto de l'arme courante (ou null si aucun).</summary>
        public WeaponDto? CurrentWeapon => DataLoader.GetWeapon(CurrentWeaponId);
        /// <summary>État runtime de l'arme courante.</summary>
        public WeaponRuntimeState CurrentState => _states[(int)CurrentSlot];

        /// <summary>Jauge d'ultimate normalisée 0..1 (délègue à DischargeSystem).</summary>
        public float UltimateChargeNormalized => DischargeSystem.Instance?.NormalizedCharge ?? 0f;
        /// <summary>Vrai si l'ultimate est prêt (délègue à DischargeSystem).</summary>
        public bool UltimateReady => DischargeSystem.Instance?.IsReady ?? false;

        // 3 slots d'armes (index par WeaponSlot).
        private readonly WeaponRuntimeState[] _states = new WeaponRuntimeState[3];
        private float _switchTimer;
        private WeaponSlot _pendingSlot;

        /// <summary>Événement local déclenché quand le reload démarre (param : durée).</summary>
        public event Action<float> OnReloadStarted;
        /// <summary>Événement local déclenché quand le reload se termine.</summary>
        public event Action OnReloadCompleted;

        private void Awake()
        {
            for (int i = 0; i < 3; i++) _states[i] = new WeaponRuntimeState();
        }

        private void Start()
        {
            // Initialise les slots depuis l'inventaire (si disponible).
            if (_inventory != null)
            {
                RefreshFromInventory();
            }
        }

        private void Update()
        {
            // Gestion du switch d'arme (timer d'animation).
            if (IsSwitching)
            {
                _switchTimer -= Time.deltaTime;
                if (_switchTimer <= 0f)
                {
                    FinalizeSwitch();
                }
            }

            // Gestion du reload (timer par arme).
            for (int i = 0; i < 3; i++)
            {
                var state = _states[i];
                if (state.IsReloading)
                {
                    state.ReloadTimer -= Time.deltaTime;
                    if (state.ReloadTimer <= 0f)
                    {
                        CompleteReload(i);
                    }
                }
            }
        }

        // ==================== SWITCH ====================

        /// <summary>
        /// Demande un switch vers un slot donné.
        /// </summary>
        /// <param name="slot">Slot cible.</param>
        /// <returns>Vrai si le switch a été initié, faux si déjà sur ce slot ou slot vide.</returns>
        public bool SwitchToSlot(WeaponSlot slot)
        {
            if (slot == CurrentSlot) return false;
            if (IsSwitching) return false;
            var state = _states[(int)slot];
            if (state == null || string.IsNullOrEmpty(state.WeaponId)) return false;

            // Démarre l'animation de switch.
            IsSwitching = true;
            _switchTimer = _switchDuration;
            _pendingSlot = slot;
            _animator?.PlaySwitch(_switchDuration);
            return true;
        }

        /// <summary>
        /// Bascule vers le slot suivant (cyclique).
        /// </summary>
        public bool SwitchToNextSlot()
        {
            for (int i = 1; i <= 2; i++)
            {
                var next = (WeaponSlot)(((int)CurrentSlot + i) % 3);
                if (!string.IsNullOrEmpty(_states[(int)next]?.WeaponId))
                {
                    return SwitchToSlot(next);
                }
            }
            return false;
        }

        /// <summary>
        /// Tente de lire une touche de switch directe (1, 2, 3) depuis l'InputManager.
        /// </summary>
        public void HandleSwitchInput(InputState input)
        {
            // Le SwitchPressed de l'InputManager correspond à la touche swap générique.
            if (input.SwitchPressed)
            {
                SwitchToNextSlot();
                return;
            }
            // Touches directes 1/2/3 : on lit via le clavier (desktop uniquement).
#if ENABLE_INPUT_SYSTEM
            // L'ancien système Input n'est pas disponible avec Input System backend.
            // Les touches 1/2/3 sont gérées par les boutons UI mobile ou par l'InputActionAsset.
#else
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToSlot(WeaponSlot.Primary);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToSlot(WeaponSlot.Secondary);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToSlot(WeaponSlot.Tactical);
#endif
        }

        private void FinalizeSwitch()
        {
            IsSwitching = false;
            CurrentSlot = _pendingSlot;
            PublishWeaponSwitched();
        }

        // ==================== RELOAD ====================

        /// <summary>
        /// Démarre le reload de l'arme courante.
        /// </summary>
        /// <returns>Vrai si le reload a démarré.</returns>
        public bool StartReload()
        {
            if (IsSwitching) return false;
            var state = CurrentState;
            if (state == null || string.IsNullOrEmpty(state.WeaponId)) return false;
            if (state.IsReloading) return false;

            var weapon = DataLoader.GetWeapon(state.WeaponId);
            if (weapon == null) return false;

            // Vérifie qu'il y a des munitions en réserve.
            if (state.ReserveAmmo <= 0) return false;
            // Vérifie que le chargeur n'est pas déjà plein.
            if (state.MagazineAmmo >= weapon.Value.MagazineSize) return false;

            state.IsReloading = true;
            state.ReloadTimer = weapon.Value.ReloadTime;
            _animator?.PlayReload(weapon.Value.ReloadTime);
            OnReloadStarted?.Invoke(weapon.Value.ReloadTime);
            return true;
        }

        /// <summary>
        /// Annule le reload en cours (ex: switch d'arme pendant reload).
        /// </summary>
        public void CancelReload()
        {
            for (int i = 0; i < 3; i++)
            {
                if (_states[i] != null && _states[i].IsReloading)
                {
                    _states[i].IsReloading = false;
                    _states[i].ReloadTimer = 0f;
                }
            }
        }

        private void CompleteReload(int slotIndex)
        {
            var state = _states[slotIndex];
            if (state == null) return;
            state.IsReloading = false;
            state.ReloadTimer = 0f;

            var weapon = DataLoader.GetWeapon(state.WeaponId);
            if (weapon == null) return;

            int needed = weapon.Value.MagazineSize - state.MagazineAmmo;
            int transfer = Mathf.Min(needed, state.ReserveAmmo);
            state.MagazineAmmo += transfer;
            state.ReserveAmmo -= transfer;

            if (slotIndex == (int)CurrentSlot)
            {
                OnReloadCompleted?.Invoke();
            }
        }

        // ==================== MUNITIONS ====================

        /// <summary>
        /// Consomme une balle du chargeur courant.
        /// </summary>
        /// <returns>Vrai si la balle a été consommée, faux si chargeur vide.</returns>
        public bool ConsumeAmmo()
        {
            var state = CurrentState;
            if (state == null || state.MagazineAmmo <= 0) return false;
            state.MagazineAmmo--;
            return true;
        }

        /// <summary>
        /// Ajoute des munitions à la réserve d'un slot.
        /// </summary>
        public void AddReserveAmmo(WeaponSlot slot, int amount)
        {
            var state = _states[(int)slot];
            if (state == null) return;
            state.ReserveAmmo = Mathf.Max(0, state.ReserveAmmo + amount);
        }

        /// <summary>
        /// Ajoute des munitions à la réserve de l'arme courante.
        /// </summary>
        public void AddReserveAmmoToCurrent(int amount) => AddReserveAmmo(CurrentSlot, amount);

        /// <summary>
        /// Retourne les munitions courantes (magasin + réserve) du slot courant.
        /// </summary>
        public (int magazine, int reserve) GetAmmoForCurrent()
        {
            var state = CurrentState;
            if (state == null) return (0, 0);
            return (state.MagazineAmmo, state.ReserveAmmo);
        }

        // ==================== ÉQUIPEMENT ====================

        /// <summary>
        /// Rafraîchit les slots depuis l'inventaire (appelé à l'initialisation et
        /// après un changement d'équipement).
        /// </summary>
        public void RefreshFromInventory()
        {
            if (_inventory == null) return;

            for (int i = 0; i < 3; i++)
            {
                string wid = _inventory.GetEquippedInSlot(i);
                if (string.IsNullOrEmpty(wid))
                {
                    _states[i].WeaponId = string.Empty;
                    _states[i].MagazineAmmo = 0;
                    _states[i].ReserveAmmo = 0;
                    continue;
                }
                EquipWeaponInSlot(wid, (WeaponSlot)i, force: true);
            }
        }

        /// <summary>
        /// Équipe une arme dans un slot, avec munitions initiales.
        /// </summary>
        /// <param name="weaponId">Id de l'arme.</param>
        /// <param name="slot">Slot cible.</param>
        /// <param name="force">Vrai pour ignorer l'état actuel (re-équipement).</param>
        /// <returns>Vrai si l'équipement a réussi.</returns>
        public bool EquipWeaponInSlot(string weaponId, WeaponSlot slot, bool force = false)
        {
            if (string.IsNullOrEmpty(weaponId)) return false;
            var weapon = DataLoader.GetWeapon(weaponId);
            if (weapon == null)
            {
                Debug.LogWarning($"[PlayerWeaponManager] Arme '{weaponId}' introuvable dans DataLoader.");
                return false;
            }

            var state = _states[(int)slot];
            bool sameWeapon = state.WeaponId == weaponId;
            state.WeaponId = weaponId;
            if (!sameWeapon || force)
            {
                state.MagazineAmmo = weapon.Value.MagazineSize;
                state.ReserveAmmo = GetDefaultReserveForSlot(slot);
                state.IsReloading = false;
                state.ReloadTimer = 0f;
            }

            // Si l'arme équipée est celle du slot courant, publie l'événement.
            if (slot == CurrentSlot)
            {
                PublishWeaponSwitched();
            }
            return true;
        }

        /// <summary>
        /// Retourne la réserve par défaut selon le slot.
        /// </summary>
        private int GetDefaultReserveForSlot(WeaponSlot slot)
        {
            return slot switch
            {
                WeaponSlot.Primary   => _defaultPrimaryReserve,
                WeaponSlot.Secondary => _defaultSecondaryReserve,
                WeaponSlot.Tactical  => _defaultTacticalCount,
                _                     => 0
            };
        }

        // ==================== ULTIMATE ====================

        /// <summary>
        /// Active l'ultimate (délègue à DischargeSystem).
        /// </summary>
        public bool ActivateUltimate()
        {
            return DischargeSystem.Instance?.Activate() ?? false;
        }

        /// <summary>
        /// Met à jour la position du joueur pour le DischargeSystem.
        /// </summary>
        public void UpdateUltimatePosition(Vector3 position, uint playerId)
        {
            if (DischargeSystem.Instance != null)
            {
                DischargeSystem.Instance.UpdatePlayerPosition(position, playerId);
            }
        }

        // ==================== PUBLICATION ====================

        private void PublishWeaponSwitched()
        {
            GameEventBus.Instance?.Publish(new WeaponSwitchedEvent((int)CurrentSlot, CurrentWeaponId));
        }
    }
}
