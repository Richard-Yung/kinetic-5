// ============================================================================
//  KINETICS 5 — Discharge System (jauge d'ultimate + activation)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Gère la jauge d'ultimate du joueur (0..1000) :
//    • Gain sur hit       : +5
//    • Gain sur kill      : +50
//    • Gain sur dégât subi : +2
//
//  À 1000, l'ultimate est prêt. Déclenchement (via bouton dédié) :
//    • VFX UltimateBurst (VFXSpawner.UltimateBurst)
//    • ScreenShake big (ScreenShake.Ultimate)
//    • Slow-mo 0.3x pendant 0.5s (TimeManager.TriggerSlowMotion)
//    • Dégâts AoE massifs dans un rayon autour du joueur
//    • Cooldown 2s après utilisation avant de recharger
//
//  L'effet exact de l'ultimate dépend de l'agent (lu depuis AgentSO.Abilities[ultimate]).
//
//  Publie UltimateReadyEvent quand la jauge atteint 1000.
// ============================================================================
using System;
using System.Collections;
using KINETICS5.Core;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Événement publié quand l'ultimate devient disponible.
    /// </summary>
    public readonly struct UltimateReadyEvent
    {
        /// <summary>Id de l'agent dont l'ultimate est prêt.</summary>
        public readonly string AgentId;
        /// <summary>Vrai si prêt, faux si consommé.</summary>
        public readonly bool IsReady;

        /// <summary>Constructeur.</summary>
        public UltimateReadyEvent(string agentId, bool isReady)
        {
            AgentId = agentId;
            IsReady = isReady;
        }
    }

    /// <summary>
    /// Gestion de la jauge d'ultimate du joueur et de son activation.
    /// Singleton par scène, consomme GameEventBus pour le gain passif.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sources de gain :</b>
    /// <list type="bullet">
    ///   <item>Hit réussi sur ennemi (DamageDealtEvent source = joueur local) : +5.</item>
    ///   <item>Kill d'ennemi (EnemyKilledEvent killerId = joueur local) : +50.</item>
    ///   <item>Dégât subi par le joueur (PlayerDamagedEvent) : +2.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Activation :</b> via <see cref="Activate"/>. Si la jauge n'est pas pleine,
    /// l'appel est ignoré. Sinon, déclenche l'effet et reset la jauge à 0, avec un
    /// cooldown de 2s avant que le gain ne reprenne.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DischargeSystem : MonoBehaviour
    {
        /// <summary>Valeur maximale de la jauge d'ultimate.</summary>
        public const float MaxCharge = 1000f;
        /// <summary>Gain par hit réussi.</summary>
        public const float GainPerHit = 5f;
        /// <summary>Gain par kill.</summary>
        public const float GainPerKill = 50f;
        /// <summary>Gain par dégât subi.</summary>
        public const float GainPerDamageTaken = 2f;
        /// <summary>Cooldown après activation avant que le gain ne reprenne (s).</summary>
        public const float CooldownAfterUse = 2f;
        /// <summary>Rayon par défaut de l'AoE d'ultimate (m).</summary>
        public const float DefaultUltimateRadius = 12f;
        /// <summary>Dégâts de base de l'ultimate.</summary>
        public const float DefaultUltimateDamage = 500f;
        /// <summary>Durée du slow-mo d'ultimate (s).</summary>
        public const float UltimateSlowMoDuration = 0.5f;
        /// <summary>TimeScale du slow-mo d'ultimate.</summary>
        public const float UltimateSlowMoScale = 0.3f;

        private static DischargeSystem _instance;
        /// <summary>Instance globale (auto-créée si absente).</summary>
        public static DischargeSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[DischargeSystem]");
                    _instance = go.AddComponent<DischargeSystem>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Configuration")]
        [Tooltip("Rayon de l'AoE d'ultimate (m).")]
        [SerializeField] private float _ultimateRadius = DefaultUltimateRadius;
        [Tooltip("Dégâts de base de l'ultimate (avant multiplicateur agent).")]
        [SerializeField] private float _ultimateDamage = DefaultUltimateDamage;
        [Tooltip("Layer des ennemis (pour l'AoE).")]
        [SerializeField] private LayerMask _enemyLayer = ~0;

        /// <summary>Jauge d'ultimate actuelle (0..1000).</summary>
        public float CurrentCharge { get; private set; }
        /// <summary>Vrai si l'ultimate est prêt (jauge pleine).</summary>
        public bool IsReady => CurrentCharge >= MaxCharge && _cooldownTimer <= 0f;
        /// <summary>Ratio normalisé 0..1 de la jauge.</summary>
        public float NormalizedCharge => CurrentCharge / MaxCharge;
        /// <summary>Vrai si l'ultimate est en cours d'activation.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Id de l'agent courant (pour l'effet ultimate spécifique).</summary>
        public string AgentId { get; private set; } = "VULCAN";
        /// <summary>Position actuelle du joueur (pour centrer l'AoE).</summary>
        public Vector3 PlayerPosition { get; private set; }
        /// <summary>Identifiant unique du joueur local (pour filtrer les hits).</summary>
        public uint PlayerSourceId { get; private set; }

        private float _cooldownTimer;
        private bool _wasReady;
        private IDisposable _subDamageDealt;
        private IDisposable _subEnemyKilled;
        private IDisposable _subPlayerDamaged;

        private static readonly Collider[] _aoeBuffer = new Collider[64];

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
        }

        private void OnEnable()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            _subDamageDealt = bus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            _subEnemyKilled = bus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            _subPlayerDamaged = bus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void OnDisable()
        {
            _subDamageDealt?.Dispose();
            _subEnemyKilled?.Dispose();
            _subPlayerDamaged?.Dispose();
            _subDamageDealt = null;
            _subEnemyKilled = null;
            _subPlayerDamaged = null;
        }

        private void Update()
        {
            // Décrémente le cooldown post-activation.
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }

            // Publication UltimateReadyEvent (front montant).
            if (IsReady && !_wasReady)
            {
                _wasReady = true;
                PublishUltimateReady(true);
            }
            else if (!IsReady && _wasReady)
            {
                _wasReady = false;
                PublishUltimateReady(false);
            }
        }

        /// <summary>
        /// Définit l'agent courant (pour l'effet ultimate spécifique).
        /// </summary>
        public void SetAgent(string agentId)
        {
            AgentId = agentId ?? "VULCAN";
        }

        /// <summary>
        /// Met à jour la position du joueur (à appeler chaque frame par le PlayerController).
        /// </summary>
        public void UpdatePlayerPosition(Vector3 position, uint sourceId)
        {
            PlayerPosition = position;
            PlayerSourceId = sourceId;
        }

        /// <summary>
        /// Active l'ultimate si la jauge est pleine.
        /// </summary>
        /// <returns>Vrai si l'activation a réussi, faux sinon (jauge vide ou cooldown).</returns>
        public bool Activate()
        {
            if (!IsReady) return false;
            if (IsActive) return false;

            IsActive = true;
            _cooldownTimer = CooldownAfterUse;
            CurrentCharge = 0f;
            _wasReady = false;
            PublishUltimateReady(false);

            StartCoroutine(ExecuteUltimate());
            return true;
        }

        /// <summary>
        /// Coroutine d'exécution de l'ultimate : VFX + slow-mo + AoE.
        /// </summary>
        private IEnumerator ExecuteUltimate()
        {
            Vector3 center = PlayerPosition;

            // Slow-mo immédiat.
            TimeManager.Instance?.TriggerSlowMotion(UltimateSlowMoDuration, UltimateSlowMoScale);

            // VFX burst.
            VFXSpawner.Instance?.UltimateBurst(center, AgentId);

            // Screen shake ultimate.
            ScreenShake.Ultimate();

            // Hitstop court pour marquer le moment.
            HitstopController.Trigger(HitstopType.BossKill);

            // Petite attente pour que le slow-mo soit perçu.
            yield return new WaitForSecondsRealtime(0.15f);

            // AoE : applique les dégâts à tous les ennemis dans le rayon.
            int count = Physics.OverlapSphereNonAlloc(center, _ultimateRadius, _aoeBuffer, _enemyLayer);
            float agentMultiplier = GetAgentUltimateMultiplier();
            float damage = _ultimateDamage * agentMultiplier;
            Element element = GetAgentUltimateElement();

            for (int i = 0; i < count; i++)
            {
                var col = _aoeBuffer[i];
                if (col == null) continue;
                var damageable = col.GetComponent<IDamageable>();
                if (damageable == null) continue;
                if (PlayerSourceId != 0u && damageable.IsAlive == false) continue;

                Vector3 hitPoint = col.ClosestPoint(center);
                damageable.TakeDamage(damage, element, PlayerSourceId, hitPoint, false);
                FloatingDamage.Instance?.ShowDamage(hitPoint, damage, element, false);
            }

            // Fin de l'activation.
            IsActive = false;
        }

        /// <summary>
        /// Récupère le multiplicateur d'ultimate spécifique à l'agent courant.
        /// </summary>
        private float GetAgentUltimateMultiplier()
        {
            var agent = DataLoader.GetAgent(AgentId);
            if (agent == null) return 1f;
            // L'agent peut avoir une compétence d'ultimate dans Abilities[2] (3e compétence).
            // On utilise sa magnitude comme multiplicateur (fallback 1.0).
            if (agent.Value.Abilities == null || agent.Value.Abilities.Count < 3) return 1f;
            var ult = agent.Value.Abilities[2];
            return Mathf.Max(0.5f, ult.Magnitude);
        }

        /// <summary>
        /// Récupère l'élément de l'ultimate de l'agent courant.
        /// </summary>
        private Element GetAgentUltimateElement()
        {
            // Pour l'instant, l'élément d'ultimate est dérivé de l'agent :
            // VULCAN (Tank) -> Explosive, XEN (Assault) -> Energy,
            // JOLT (Support) -> Volt, XANO (Recon) -> Cryo.
            return AgentId?.ToUpperInvariant() switch
            {
                "VULCAN" => Element.Explosive,
                "XEN"    => Element.Energy,
                "JOLT"   => Element.Volt,
                "XANO"   => Element.Cryo,
                _         => Element.Kinetic
            };
        }

        /// <summary>
        /// Ajoute manuellement de la charge à la jauge (hors bus d'événements).
        /// </summary>
        /// <param name="amount">Montant à ajouter.</param>
        public void AddCharge(float amount)
        {
            if (_cooldownTimer > 0f) return; // Pas de gain pendant le cooldown.
            CurrentCharge = Mathf.Clamp(CurrentCharge + amount, 0f, MaxCharge);
        }

        // --- Handlers d'événements ---

        private void OnDamageDealt(in DamageDealtEvent evt)
        {
            // Filtre : seuls les hits du joueur local génèrent du gain.
            if (PlayerSourceId != 0u && evt.SourceId != PlayerSourceId) return;
            if (evt.Amount <= 0f) return;
            AddCharge(GainPerHit);
        }

        private void OnEnemyKilled(in EnemyKilledEvent evt)
        {
            if (PlayerSourceId != 0u && evt.KillerId != PlayerSourceId) return;
            AddCharge(GainPerKill);
        }

        private void OnPlayerDamaged(in PlayerDamagedEvent evt)
        {
            if (evt.Amount <= 0f) return;
            AddCharge(GainPerDamageTaken);
        }

        // --- Publication ---

        private void PublishUltimateReady(bool isReady)
        {
            GameEventBus.Instance?.Publish(new UltimateReadyEvent(AgentId, isReady));
        }
    }
}
