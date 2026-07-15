using System;
using KINETICS5.Core;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Composant de santé partagé (joueur ET ennemis).
    /// Implémente <see cref="IDamageable"/> pour exposer une API uniforme aux projectiles,
    /// IA et zones de dégâts. Gère santé + bouclier + résistances élémentaires + mort.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Origine :</b> ce composant est livré par l'agent 2-c (Enemies &amp; Missions) en
    /// tant que shim fonctionnel, l'agent 2-b (Combat) étant attendu en parallèle. Si 2-b
    /// produit une version plus riche (effets de statut, armure par hitbox), il doit
    /// remplacer ce fichier en conservant l'API publique (<see cref="IDamageable"/> +
    /// événements <see cref="OnDamaged"/>/<see cref="OnDied"/>).
    /// </para>
    /// <para>
    /// <b>Mobile-friendly :</b> aucun GC dans <see cref="TakeDamage"/> (struct d'événement
    /// via <see cref="GameEventBus"/>), pas de réflexion, cache des multiplicateurs.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Santé")]
        [Tooltip("Santé maximale de base (avant scaling de difficulté).")]
        [SerializeField] private float _maxHealth = 100f;
        [Tooltip("Bouclier maximal (absorbé avant la santé).")]
        [SerializeField] private float _maxShield = 0f;
        [Tooltip("Vrai si l'entité est détruite à 0 PV (faux pour les objets destructibles à états).")]
        [SerializeField] private bool _destroyOnDeath = true;
        [Tooltip("Délai avant destruction du GameObject après la mort (pour VFX/loot).")]
        [SerializeField] private float _corpseDelay = 3f;

        [Header("Résistances élémentaires")]
        [Tooltip("Élément de faiblesse (subit x2 dégâts).")]
        [SerializeField] private Element _weakness = Element.Kinetic;
        [Tooltip("Élément de résistance (subit x0.5 dégâts).")]
        [SerializeField] private Element _resistance = Element.Kinetic;

        /// <summary>Événement déclenché à chaque coup reçu (avant publication sur le bus global).</summary>
        public event Action<HealthComponent, float, Element, uint> OnDamaged;

        /// <summary>Événement déclenché à la mort (avant destruction / désactivation).</summary>
        public event Action<HealthComponent, uint> OnDied;

        /// <summary>Santé courante (lecture seule depuis l'extérieur).</summary>
        public float CurrentHealth { get; private set; }

        /// <summary>Bouclier courant.</summary>
        public float CurrentShield { get; private set; }

        /// <summary>Santé maximale (post-scaling).</summary>
        public float MaxHealth => _maxHealth;

        /// <summary>Bouclier maximal.</summary>
        public float MaxShield => _maxShield;

        /// <summary>Vrai si la santé est &gt; 0.</summary>
        public bool IsAlive => CurrentHealth > 0f;

        /// <inheritdoc/>
        bool IDamageable.IsAlive => IsAlive;

        /// <inheritdoc/>
        Vector3 IDamageable.Position => transform.position;

        /// <summary>
        /// Multiplicateur de difficulté appliqué à <see cref="_maxHealth"/> (set par
        /// <c>DifficultyManager</c> ou <c>EnemySpawner</c>). Setter idempotent.
        /// </summary>
        public float HealthScale { get; set; } = 1f;

        /// <summary>Vrai si le bouclier régénère passivement (override runtime).</summary>
        public bool ShieldRegenEnabled { get; set; }

        private float _shieldRegenAccumulator;
        private bool _isDead;

        private void Awake()
        {
            CurrentHealth = _maxHealth * HealthScale;
            CurrentShield = _maxShield;
        }

        private void Update()
        {
            // Régénération passive du bouclier (si activée par l'owner).
            if (ShieldRegenEnabled && CurrentShield < _maxShield && IsAlive)
            {
                _shieldRegenAccumulator += Time.deltaTime * 5f; // 5 bouclier/sec
                if (_shieldRegenAccumulator >= 1f)
                {
                    int amount = (int)_shieldRegenAccumulator;
                    _shieldRegenAccumulator -= amount;
                    CurrentShield = Mathf.Min(_maxShield, CurrentShield + amount);
                }
            }
        }

        /// <summary>Initialise la santé avec un scaling externe (à appeler avant Awake ou via Initialize).</summary>
        /// <param name="baseMaxHealth">Santé de base.</param>
        /// <param name="baseMaxShield">Bouclier de base.</param>
        /// <param name="healthScale">Multiplicateur de scaling (difficulté).</param>
        /// <param name="weakness">Élément de faiblesse.</param>
        /// <param name="resistance">Élément de résistance.</param>
        public void Initialize(float baseMaxHealth, float baseMaxShield, float healthScale,
                               Element weakness, Element resistance)
        {
            _maxHealth = Mathf.Max(1f, baseMaxHealth);
            _maxShield = Mathf.Max(0f, baseMaxShield);
            HealthScale = Mathf.Max(0.1f, healthScale);
            _weakness = weakness;
            _resistance = resistance;
            CurrentHealth = _maxHealth * HealthScale;
            CurrentShield = _maxShield;
            _isDead = false;
        }

        /// <inheritdoc/>
        public float TakeDamage(float amount, Element element, uint sourceId, Vector3 hitPoint, bool isCritical = false)
        {
            if (!IsAlive || amount <= 0f) return 0f;

            // NOTE : le multiplicateur élémentaire (weakness/resistance) est calculé en amont
            // par DamageCalculator (Combat/DamageCalculator.cs) pour les attaques d'arme.
            // HealthComponent applique le montant tel quel (déjà finalisé) pour éviter tout
            // double-application. L'argument `element` est conservé pour le telemetry et
            // l'affichage UI (killfeed, VFX de résistance/faiblesse).

            float finalAmount = amount;
            if (isCritical) finalAmount *= 1.5f; // crit au niveau cible (peut être désactivé si déjà appliqué amont)

            // Le bouclier absorbe en premier.
            if (CurrentShield > 0f)
            {
                float absorbed = Mathf.Min(CurrentShield, finalAmount);
                CurrentShield -= absorbed;
                finalAmount -= absorbed;
            }

            if (finalAmount > 0f)
            {
                CurrentHealth = Mathf.Max(0f, CurrentHealth - finalAmount);
            }

            // Événement local (pour les hooks de l'owner).
            try
            {
                OnDamaged?.Invoke(this, finalAmount, element, sourceId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HealthComponent] OnDamaged handler exception: {ex}");
            }

            // Publication globale zero-alloc via le bus.
            var bus = GameEventBus.Instance;
            if (bus != null)
            {
                uint targetId = (uint)GetInstanceID();
                bus.Publish(new DamageDealtEvent(sourceId, targetId, finalAmount, isCritical,
                                                 (int)element, hitPoint));
            }

            // Déclenchement de la mort.
            if (CurrentHealth <= 0f && !_isDead)
            {
                Die(sourceId);
            }

            return finalAmount;
        }

        /// <inheritdoc/>
        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            CurrentHealth = Mathf.Min(_maxHealth * HealthScale, CurrentHealth + amount);
        }

        /// <inheritdoc/>
        public void RestoreShield(float amount)
        {
            if (amount <= 0f) return;
            CurrentShield = Mathf.Min(_maxShield, CurrentShield + amount);
        }

        /// <summary>Réinitialise la santé (utile pour les ennemis poolés réutilisés).</summary>
        public void ResetHealth()
        {
            CurrentHealth = _maxHealth * HealthScale;
            CurrentShield = _maxShield;
            _isDead = false;
            enabled = true;
        }

        /// <inheritdoc/>
        public void Die()
        {
            Die(0u);
        }

        private void Die(uint killerId)
        {
            if (_isDead) return;
            _isDead = true;
            CurrentHealth = 0f;
            CurrentShield = 0f;
            enabled = false;

            try
            {
                OnDied?.Invoke(this, killerId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HealthComponent] OnDied handler exception: {ex}");
            }

            if (_destroyOnDeath)
            {
                // Destruction différée pour laisser le temps au loot/VFX.
                if (_corpseDelay > 0f && gameObject.activeInHierarchy)
                {
                    StartCoroutine(DestroyAfterDelay(_corpseDelay));
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        private System.Collections.IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (gameObject != null) Destroy(gameObject);
        }
    }
}
