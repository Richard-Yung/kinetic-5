using System;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Combat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KINETICS5.Gameplay.Enemies
{
    /// <summary>
    /// Spécialisation boss de <see cref="EnemyController"/>. Ajoute :
    /// <list type="bullet">
    ///   <item>Gestion multi-phases via <see cref="BossPhaseManager"/> (HP thresholds).</item>
    ///   <item>Fenêtres d'invulnérabilité (entre phases ou durant patterns spéciaux).</item>
    ///   <item>Timer d'enrage (fail-safe pour éviter un combat trop long).</item>
    ///   <item>Barre de vie UI dédiée (top de l'écran, hook évènementiel).</item>
    ///   <item>Points faibles multiples (weak points) qui multiplieront les dégâts reçus.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class BossController : EnemyController
    {
        [Header("Boss — Phases")]
        [Tooltip("Référence vers le BossPhaseManager (auto-résolu si sur le même GameObject).")]
        [SerializeField] private BossPhaseManager _phaseManager;

        [Header("Boss — Enrage")]
        [Tooltip("Durée avant enrage définitif (0 = désactivé).")]
        [SerializeField] private float _enrageTimer = 180f;
        [Tooltip("Multiplicateur de dégâts en enrage.")]
        [SerializeField] private float _enrageDamageMult = 1.5f;

        [Header("Boss — Points faibles")]
        [Tooltip("Transforms des weak points (chaque weak point multiplie les dégâts qu'il reçoit).")]
        [SerializeField] private WeakPoint[] _weakPoints = Array.Empty<WeakPoint>();

        [Header("Boss — UI")]
        [Tooltip("Id de la scène UI de barre de vie boss (chargée additivement).")]
        [SerializeField] private string _bossHealthBarScene = "UI_BossHealthBar";

        /// <summary>Événement : changement de phase.</summary>
        public event Action<int> OnPhaseChanged;

        /// <summary>Événement : début/fin de fenêtre d'invulnérabilité.</summary>
        public event Action<bool> OnInvulnerabilityChanged;

        /// <summary>Événement : enrage déclenché.</summary>
        public event Action OnEnraged;

        /// <summary>Événement : mise à jour de la barre de vie (HP normalisé 0..1).</summary>
        public event Action<float, float, string> OnHealthBarUpdated; // (hpPct, shieldPct, phaseLabel)

        /// <summary>Vrai si le boss est actuellement invulnérable.</summary>
        public bool IsInvulnerable { get; private set; }

        /// <summary>Phase courante (1-indexed).</summary>
        public int CurrentPhase { get; private set; } = 1;

        /// <summary>Vrai si le boss est enragé.</summary>
        public bool IsEnraged { get; private set; }

        private float _enrageCountdown;
        private float _invulnTimer;

        /// <summary>Données d'un point faible du boss.</summary>
        [Serializable]
        public struct WeakPoint
        {
            [Tooltip("Transform représentant le weak point (zone de hit).")]
            public Transform Transform;
            [Tooltip("Multiplicateur de dégâts subis par ce weak point (2 = double dégâts).")]
            [Range(1f, 10f)] public float DamageMultiplier;
            [Tooltip("Vrai si le weak point est actif (peut être désactivé après destruction).")]
            public bool IsActive;
        }

        /// <summary>Initialise le boss avec les données de phase et le spawner propriétaire.</summary>
        public void InitializeBoss(EnemySpawner spawner, List<BossPhaseData> phases,
                                    float hpMult = 1f, float damageMult = 1f)
        {
            // Initialise la partie EnemyController (charge données, santé, IA, combat).
            Initialize(spawner, hpMult, damageMult);

            if (_phaseManager == null) _phaseManager = GetComponent<BossPhaseManager>();
            if (_phaseManager != null && phases != null && phases.Count > 0)
            {
                _phaseManager.Initialize(this, phases, damageMult);
            }

            _enrageCountdown = _enrageTimer > 0f ? _enrageTimer : 0f;
            CurrentPhase = 1;
            IsInvulnerable = false;
            IsEnraged = false;

            // Abonnement aux events de santé pour déclencher les phases.
            if (Health != null)
            {
                Health.OnDamaged += HandleBossDamaged;
            }

            OnPhaseChanged?.Invoke(CurrentPhase);
            UpdateHealthBar();
        }

        protected override void Update()
        {
            // Appel du tick de base (IA + facing).
            base.Update();

            if (!IsActive || IsDead) return;

            // Timer d'enrage.
            if (_enrageCountdown > 0f && !IsEnraged)
            {
                _enrageCountdown -= Time.deltaTime;
                if (_enrageCountdown <= 0f)
                {
                    TriggerEnrage();
                }
            }

            // Fenêtre d'invulnérabilité temporisée.
            if (_invulnTimer > 0f)
            {
                _invulnTimer -= Time.deltaTime;
                if (_invulnTimer <= 0f)
                {
                    SetInvulnerable(false);
                }
            }

            UpdateHealthBar();
        }

        // =================================================================================
        //  PHASES
        // =================================================================================

        /// <summary>Appelé par <see cref="BossPhaseManager"/> pour changer de phase.</summary>
        public void SetPhase(int newPhase)
        {
            if (newPhase == CurrentPhase) return;
            CurrentPhase = newPhase;
            OnPhaseChanged?.Invoke(newPhase);

            // Fenêtre d'invulnérabilité courte pendant la transition (1.5s).
            SetInvulnerable(true, 1.5f);

            // VFX de transition : slow-mo via TimeManager.
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.TriggerSlowMotion(0.6f, 0.3f);
                TimeManager.Instance.TriggerHitstop(0.1f);
            }

            // Camera shake (hook futur via CameraManager).
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.Shake(2.5f, 0.4f, 0.8f);
            }
        }

        // =================================================================================
        //  INVULNÉRABILITÉ
        // =================================================================================

        /// <summary>Active/désactive l'invulnérabilité (les dégâts sont ignorés).</summary>
        public void SetInvulnerable(bool invulnerable, float duration = 0f)
        {
            IsInvulnerable = invulnerable;
            if (invulnerable && duration > 0f)
            {
                _invulnTimer = duration;
            }
            else if (!invulnerable)
            {
                _invulnTimer = 0f;
            }
            OnInvulnerabilityChanged?.Invoke(invulnerable);
        }

        // =================================================================================
        //  ENRAGE
        // =================================================================================

        private void TriggerEnrage()
        {
            if (IsEnraged) return;
            IsEnraged = true;
            OnEnraged?.Invoke();

            // Boost de dégâts via le combat.
            if (Combat != null)
            {
                // On force un pattern spécial d'enrage.
                Combat.ForceAttack(EnemyAttackType.AoESlam, 1.5f, _enrageDamageMult);
            }

            // Publication sur le bus (pour UI/VFX).
            if (GameEventBus.Instance != null)
            {
                // Pas d'événement dédié ; on log via telemetry future.
                Debug.Log($"[BossController] {Data?.Id} est entré en enrage.");
            }
        }

        // =================================================================================
        //  DAMAGE HOOK (pour gérer l'invulnérabilité et les weak points)
        // =================================================================================

        private void HandleBossDamaged(HealthComponent health, float amount, Element element, uint sourceId)
        {
            // Si invulnérable, le damage a déjà été appliqué par HealthComponent.TakeDamage.
            // Pour vraiment l'ignorer, on devrait patcher HealthComponent ; ici on log + rembourse
            // en restaurant les PV (workaround acceptable pour la sandbox).
            if (IsInvulnerable)
            {
                health.Heal(amount);
                return;
            }
            // Le BossPhaseManager va détecter le seuil et appeler SetPhase.
        }

        /// <summary>
        /// Calcule le multiplicateur de dégâts pour un hit sur un weak point donné.
        /// </summary>
        public float GetWeakPointMultiplier(Vector3 hitPoint)
        {
            for (int i = 0; i < _weakPoints.Length; i++)
            {
                ref var wp = ref _weakPoints[i];
                if (!wp.IsActive || wp.Transform == null) continue;
                float dist = Vector3.Distance(wp.Transform.position, hitPoint);
                if (dist <= 0.8f)
                {
                    // Désactive le weak point après un hit (destructible).
                    _weakPoints[i].IsActive = false;
                    return wp.DamageMultiplier;
                }
            }
            return 1f;
        }

        // =================================================================================
        //  HEALTH BAR
        // =================================================================================

        private void UpdateHealthBar()
        {
            if (Health == null) return;
            float hpPct = Health.CurrentHealth / (Health.MaxHealth * Health.HealthScale);
            float shieldPct = Health.MaxShield > 0f ? Health.CurrentShield / Health.MaxShield : 0f;
            string phaseLabel = $"PHASE {CurrentPhase}";
            OnHealthBarUpdated?.Invoke(hpPct, shieldPct, phaseLabel);
        }

        // =================================================================================
        //  CLEANUP
        // =================================================================================

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Health != null)
            {
                Health.OnDamaged -= HandleBossDamaged;
            }
        }
    }
}
