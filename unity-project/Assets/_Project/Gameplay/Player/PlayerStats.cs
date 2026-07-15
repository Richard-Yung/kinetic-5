// ============================================================================
//  KINETICS 5 — Player Stats (statistiques calculées + vitals + buffs)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Composant du joueur qui :
//    • Calcule les stats dérivées depuis AgentDto + équipement (MaxHealth, MaxShield,
//      MoveSpeed, Power, CritChance, CritDamage, ElementalBonus, mitigation).
//    • Détient l'état de santé courant (HP, Shield) avec régénération différée.
//    • Gère le système de buffs/debuffs temporisés (+ATK, +DEF, +SPD, haste, slow).
//    • Publie PlayerDeathEvent à la mort.
//    • Offre une API de respawn (reset HP/Shield/buffs/position).
//
//  Le PlayerController implémente IDamageable et forward à ce composant.
// ============================================================================
using System;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Player
{
    /// <summary>
    /// Événement publié à la mort du joueur local.
    /// </summary>
    public readonly struct PlayerDeathEvent
    {
        /// <summary>Id du joueur mort.</summary>
        public readonly uint PlayerId;
        /// <summary>Id de la source du coup fatal (0 si environnement).</summary>
        public readonly uint KillerId;
        /// <summary>Position du décès.</summary>
        public readonly Vector3 DeathPosition;

        /// <summary>Constructeur.</summary>
        public PlayerDeathEvent(uint playerId, uint killerId, Vector3 pos)
        {
            PlayerId = playerId;
            KillerId = killerId;
            DeathPosition = pos;
        }
    }

    /// <summary>
    /// Type de buff/debuff applicable au joueur.
    /// </summary>
    public enum BuffType
    {
        /// <summary>Bonus de dégâts (×ATK).</summary>
        AttackUp,
        /// <summary>Bonus de défense (×DEF).</summary>
        DefenseUp,
        /// <summary>Bonus de vitesse (×SPD).</summary>
        SpeedUp,
        /// <summary>Hâte : réduction des cooldowns.</summary>
        Haste,
        /// <summary>Ralentissement (debuff Cryo).</summary>
        Slow,
        /// <summary>Étourdissement (debuff Volt) : joueur incapable d'agir.</summary>
        Stun,
        /// <summary>Bouclier bonus (shield additionnel).</summary>
        ShieldBonus,
        /// <summary>Régénération de santé sur la durée.</summary>
        HealthRegen
    }

    /// <summary>
    /// Buff/debuff temporaire appliqué au joueur.
    /// </summary>
    public readonly struct ActiveBuff
    {
        /// <summary>Type de buff.</summary>
        public readonly BuffType Type;
        /// <summary>Magnitude (fraction : 0.2 = +20%).</summary>
        public readonly float Magnitude;
        /// <summary>Durée restante (s).</summary>
        public readonly float RemainingTime;
        /// <summary>Id de la source (pour le tracking).</summary>
        public readonly string SourceId;

        /// <summary>Constructeur.</summary>
        public ActiveBuff(BuffType type, float magnitude, float duration, string sourceId)
        {
            Type = type;
            Magnitude = magnitude;
            RemainingTime = duration;
            SourceId = sourceId ?? string.Empty;
        }
    }

    /// <summary>
    /// Composant de statistiques du joueur. Calcule les stats dérivées depuis
    /// l'agent et l'équipement, gère la santé/bouclier, les buffs et la mort.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>API :</b> <see cref="TakeDamage"/>, <see cref="Heal"/>, <see cref="RestoreShield"/>,
    /// <see cref="Die"/>, <see cref="Respawn"/>, <see cref="ApplyBuff"/>,
    /// <see cref="RecalculateFromAgent"/>.
    /// </para>
    /// <para>
    /// <b>Publications bus :</b>
    /// <list type="bullet">
    ///   <item><see cref="PlayerDamagedEvent"/> à chaque coup reçu.</item>
    ///   <item><see cref="PlayerDeathEvent"/> au décès.</item>
    ///   <item><see cref="DamageDealtEvent"/> quand le joueur subit (pour killfeed).</item>
    /// </list>
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
        [Header("Résolution data")]
        [Tooltip("Id de l'agent (VULCAN/XEN/JOLT/XANO).")]
        [SerializeField] private string _agentId = "VULCAN";

        [Header("Valeurs de base (fallback si AgentDto introuvable)")]
        [SerializeField] private float _baseMaxHealth = 100f;
        [SerializeField] private float _baseMaxShield = 50f;
        [SerializeField] private float _baseMoveSpeed = 4.5f;
        [SerializeField] private float _basePower = 1000f;
        [SerializeField] private float _baseCritChance = 0.1f;
        [SerializeField] private float _baseCritDamage = 1.5f;
        [Tooltip("Mitigation par défaut des dégâts reçus (0 = pas de réduction, 0.5 = -50%).")]
        [SerializeField] private float _baseMitigation = 0f;

        [Header("Régénération")]
        [Tooltip("Délai avant régénération du bouclier après le dernier coup (s).")]
        [SerializeField] private float _shieldRegenDelay = 3f;
        [Tooltip("Vitesse de régénération du bouclier (points/s).")]
        [SerializeField] private float _shieldRegenRate = 50f;

        [Header("Buffs")]
        [Tooltip("Nombre maximum de buffs simultanés (cap mobile).")]
        [SerializeField] private int _maxConcurrentBuffs = 8;

        // --- État runtime ---
        private float _currentHealth;
        private float _currentShield;
        private float _lastDamageTime;
        private bool _isDead;
        private uint _playerId;

        // --- Stats calculées (recalculées par RecalculateFromAgent) ---
        private float _maxHealth;
        private float _maxShield;
        private float _moveSpeed;
        private float _power;
        private float _critChance;
        private float _critDamage;
        private float _mitigation;
        private readonly Dictionary<Element, float> _elementalBonuses = new(5);

        // --- Buffs actifs ---
        private readonly List<ActiveBuff> _buffs = new(8);

        /// <summary>Identifiant unique du joueur (instanceId).</summary>
        public uint PlayerId => _playerId;
        /// <summary>Santé courante.</summary>
        public float CurrentHealth => _currentHealth;
        /// <summary>Bouclier courant.</summary>
        public float CurrentShield => _currentShield;
        /// <summary>Santé maximale (post-calcul).</summary>
        public float MaxHealth => _maxHealth;
        /// <summary>Bouclier maximal.</summary>
        public float MaxShield => _maxShield;
        /// <summary>Vitesse de déplacement calculée (avec buffs).</summary>
        public float MoveSpeed => _moveSpeed * GetBuffMultiplier(BuffType.SpeedUp) / GetBuffMultiplier(BuffType.Slow);
        /// <summary>Puissance de combat calculée.</summary>
        public float Power => _power * GetBuffMultiplier(BuffType.AttackUp);
        /// <summary>Chance de critique (0..1).</summary>
        public float CritChance => _critChance;
        /// <summary>Multiplicateur de critique (1.5 = +50% dégâts).</summary>
        public float CritDamage => _critDamage;
        /// <summary>Mitigation des dégâts reçus (0..0.95).</summary>
        public float Mitigation => Mathf.Clamp(_mitigation + GetBuffMagnitude(BuffType.DefenseUp), 0f, 0.95f);
        /// <summary>Vrai si le joueur est mort.</summary>
        public bool IsDead => _isDead;
        /// <summary>Vrai si le joueur est étourdi (buff Stun actif).</summary>
        public bool IsStunned => HasBuff(BuffType.Stun);
        /// <summary>Vrai si hâte active (cooldowns réduits).</summary>
        public bool IsHasted => HasBuff(BuffType.Haste);
        /// <summary>Bonus élémentaire par élément (multiplicateur additionnel 0..).</summary>
        public IReadOnlyDictionary<Element, float> ElementalBonuses => _elementalBonuses;
        /// <summary>Liste des buffs actifs (snapshot pour l'UI).</summary>
        public IReadOnlyList<ActiveBuff> ActiveBuffs => _buffs;

        private void Awake()
        {
            _playerId = (uint)GetInstanceID();
            RecalculateFromAgent();
            _currentHealth = _maxHealth;
            _currentShield = _maxShield;
        }

        private void Update()
        {
            // Régénération du bouclier après délai sans dégât.
            if (!_isDead && _currentShield < _maxShield && Time.time - _lastDamageTime >= _shieldRegenDelay)
            {
                _currentShield = Mathf.Min(_maxShield, _currentShield + _shieldRegenRate * Time.deltaTime);
            }

            // Régénération de santé via buffs HealthRegen.
            if (!_isDead)
            {
                float regenMag = GetBuffMagnitude(BuffType.HealthRegen);
                if (regenMag > 0f && _currentHealth < _maxHealth)
                {
                    _currentHealth = Mathf.Min(_maxHealth, _currentHealth + regenMag * Time.deltaTime);
                }
            }

            // Tick des buffs (décrémente durée, retire les expirés).
            TickBuffs();
        }

        /// <summary>
        /// Recalcule toutes les stats dérivées depuis AgentDto + équipement.
        /// À appeler à chaque changement d'agent ou d'équipement.
        /// </summary>
        public void RecalculateFromAgent()
        {
            var agent = DataLoader.GetAgent(_agentId);
            if (agent != null)
            {
                _maxHealth = Mathf.Max(1f, agent.Value.BaseHealth);
                _maxShield = Mathf.Max(0f, agent.Value.BaseShield);
                _moveSpeed = Mathf.Max(0.5f, agent.Value.BaseSpeed * 4.5f); // normalisation
                _power = Mathf.Max(1f, agent.Value.BasePower);
            }
            else
            {
                _maxHealth = _baseMaxHealth;
                _maxShield = _baseMaxShield;
                _moveSpeed = _baseMoveSpeed;
                _power = _basePower;
            }

            _critChance = _baseCritChance;
            _critDamage = _baseCritDamage;
            _mitigation = _baseMitigation;

            // Bonus élémentaires par défaut (peuvent être étendus par les talents/équipement).
            _elementalBonuses[Element.Kinetic] = 0f;
            _elementalBonuses[Element.Energy] = 0f;
            _elementalBonuses[Element.Cryo] = 0f;
            _elementalBonuses[Element.Volt] = 0f;
            _elementalBonuses[Element.Explosive] = 0f;

            // Clamp current values if max a baissé.
            _currentHealth = Mathf.Min(_currentHealth, _maxHealth);
            _currentShield = Mathf.Min(_currentShield, _maxShield);
        }

        /// <summary>
        /// Ajoute un bonus d'équipement (appelé par PlayerInventory à l'équipement).
        /// </summary>
        /// <param name="healthBonus">Bonus HP max.</param>
        /// <param name="shieldBonus">Bonus bouclier max.</param>
        /// <param name="powerBonus">Bonus puissance.</param>
        /// <param name="critChanceBonus">Bonus chance critique.</param>
        /// <param name="critDamageBonus">Bonus multiplicateur critique.</param>
        /// <param name="mitigationBonus">Bonus mitigation.</param>
        public void AddGearBonus(float healthBonus, float shieldBonus, float powerBonus,
                                  float critChanceBonus, float critDamageBonus, float mitigationBonus)
        {
            _maxHealth += Mathf.Max(0f, healthBonus);
            _maxShield += Mathf.Max(0f, shieldBonus);
            _power += Mathf.Max(0f, powerBonus);
            _critChance = Mathf.Clamp01(_critChance + critChanceBonus);
            _critDamage += Mathf.Max(0f, critDamageBonus);
            _mitigation = Mathf.Clamp(_mitigation + mitigationBonus, 0f, 0.95f);
            _currentHealth = _maxHealth; // Re-full sur nouvel équipement (design choice).
            _currentShield = _maxShield;
        }

        /// <summary>
        /// Ajoute un bonus élémentaire pour un élément donné (multiplicateur additionnel).
        /// </summary>
        public void AddElementalBonus(Element element, float bonus)
        {
            if (_elementalBonuses.TryGetValue(element, out float current))
            {
                _elementalBonuses[element] = current + bonus;
            }
            else
            {
                _elementalBonuses[element] = bonus;
            }
        }

        /// <summary>
        /// Retourne le multiplicateur élémentaire total (multiplicatif).
        /// </summary>
        public float GetElementalMultiplier(Element element)
        {
            return 1f + (_elementalBonuses.TryGetValue(element, out float b) ? b : 0f);
        }

        /// <summary>
        /// Applique des dégâts au joueur (bouclier d'abord, puis santé, avec mitigation).
        /// </summary>
        /// <param name="amount">Montant brut de dégâts.</param>
        /// <param name="element">Élément des dégâts.</param>
        /// <param name="sourceId">Identifiant de la source.</param>
        /// <param name="hitPoint">Point d'impact monde.</param>
        /// <param name="isCritical">Vrai si critique.</param>
        /// <returns>Montant effectif de dégâts infligés.</returns>
        public float TakeDamage(float amount, Element element, uint sourceId, Vector3 hitPoint, bool isCritical = false)
        {
            if (_isDead || amount <= 0f) return 0f;

            _lastDamageTime = Time.time;

            // Mitigation (réduction fixe %).
            float mitigated = amount * (1f - Mitigation);

            // Le bouclier absorbe 70% des dégâts (bouclier = buffer).
            float absorbed = 0f;
            if (_currentShield > 0f)
            {
                absorbed = Mathf.Min(_currentShield, mitigated * 0.7f);
                _currentShield -= absorbed;
                mitigated -= absorbed;
            }

            if (mitigated > 0f)
            {
                _currentHealth = Mathf.Max(0f, _currentHealth - mitigated);
            }

            // Publication de l'événement de dégât joueur (pour HUD, killfeed, telemetry).
            bool fatal = _currentHealth <= 0f;
            GameEventBus.Instance?.Publish(new PlayerDamagedEvent(amount, _currentHealth, sourceId, fatal));

            // Publication DamageDealtEvent (uniformise avec les ennemis pour le killfeed).
            GameEventBus.Instance?.Publish(new DamageDealtEvent(sourceId, _playerId, amount, isCritical, (int)element, hitPoint));

            if (fatal && !_isDead)
            {
                Die(sourceId);
            }

            return amount;
        }

        /// <summary>
        /// Soin direct (santé).
        /// </summary>
        public void Heal(float amount)
        {
            if (_isDead || amount <= 0f) return;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        }

        /// <summary>
        /// Restaure le bouclier.
        /// </summary>
        public void RestoreShield(float amount)
        {
            if (_isDead || amount <= 0f) return;
            _currentShield = Mathf.Min(_maxShield, _currentShield + amount);
        }

        /// <summary>
        /// Tue le joueur immédiatement.
        /// </summary>
        public void Die()
        {
            Die(0u);
        }

        private void Die(uint killerId)
        {
            if (_isDead) return;
            _isDead = true;
            _currentHealth = 0f;
            _currentShield = 0f;

            GameEventBus.Instance?.Publish(new PlayerDeathEvent(_playerId, killerId, transform.position));
        }

        /// <summary>
        /// Respawn : reset HP/Shield/buffs à une position donnée.
        /// </summary>
        public void Respawn(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            _isDead = false;
            _currentHealth = _maxHealth;
            _currentShield = _maxShield;
            _lastDamageTime = Time.time;
            _buffs.Clear();
        }

        // ==================== BUFFS ====================

        /// <summary>
        /// Applique un buff/debuff au joueur.
        /// </summary>
        /// <param name="type">Type de buff.</param>
        /// <param name="magnitude">Magnitude (fraction : 0.2 = +20%).</param>
        /// <param name="duration">Durée en secondes.</param>
        /// <param name="sourceId">Id de la source (optionnel).</param>
        public void ApplyBuff(BuffType type, float magnitude, float duration, string sourceId = "")
        {
            // Cap mobile : si trop de buffs, on remplace le plus ancien.
            if (_buffs.Count >= _maxConcurrentBuffs)
            {
                _buffs.RemoveAt(0);
            }
            _buffs.Add(new ActiveBuff(type, magnitude, duration, sourceId));
        }

        /// <summary>
        /// Vrai si au moins un buff du type donné est actif.
        /// </summary>
        public bool HasBuff(BuffType type)
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Type == type) return true;
            }
            return false;
        }

        /// <summary>
        /// Retourne le multiplicateur cumulé pour un type de buff (produit des magnitudes + 1).
        /// Ex : 2 buffs SpeedUp 0.2 chacun -> 1.2 * 1.2 = 1.44.
        /// </summary>
        public float GetBuffMultiplier(BuffType type)
        {
            float mult = 1f;
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Type == type)
                {
                    mult *= (1f + _buffs[i].Magnitude);
                }
            }
            return mult;
        }

        /// <summary>
        /// Retourne la magnitude cumulée pour un type de buff (somme).
        /// Utile pour DefenseUp (réduction % additive).
        /// </summary>
        public float GetBuffMagnitude(BuffType type)
        {
            float mag = 0f;
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Type == type)
                {
                    mag += _buffs[i].Magnitude;
                }
            }
            return mag;
        }

        /// <summary>
        /// Retire tous les buffs d'un type donné.
        /// </summary>
        public void RemoveBuff(BuffType type)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (_buffs[i].Type == type) _buffs.RemoveAt(i);
            }
        }

        /// <summary>
        /// Retire tous les buffs actifs.
        /// </summary>
        public void ClearAllBuffs()
        {
            _buffs.Clear();
        }

        private void TickBuffs()
        {
            if (_buffs.Count == 0) return;
            float dt = Time.deltaTime;
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                var b = _buffs[i];
                float remaining = b.RemainingTime - dt;
                if (remaining <= 0f)
                {
                    _buffs.RemoveAt(i);
                }
                else
                {
                    _buffs[i] = new ActiveBuff(b.Type, b.Magnitude, remaining, b.SourceId);
                }
            }
        }
    }
}
