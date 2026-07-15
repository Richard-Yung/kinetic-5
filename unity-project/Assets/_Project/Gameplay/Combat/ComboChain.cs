// ============================================================================
//  KINETICS 5 — Combo Chain (combos consécutifs + multiplicateur de dégâts)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Suit les hits consécutifs du joueur dans une fenêtre de temps (3s par défaut).
//  Le multiplicateur de dégâts augmente avec le combo :
//    • 0..9   : ×1.0 (pas de bonus)
//    • 10..19 : ×1.5
//    • 20+    : ×2.0 (cap)
//
//  Le combo se réinitialise si :
//    • Pas de hit pendant 3s.
//    • Le joueur subit des dégâts.
//
//  Publie l'événement <see cref="ComboUpdatedEvent"/> sur le <see cref="GameEventBus"/>
//  pour le HUD (jauge de combo + VFX de break).
// ============================================================================
using System;
using KINETICS5.Core;
using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Événement publié à chaque mise à jour du combo (hit, reset).
    /// </summary>
    public readonly struct ComboUpdatedEvent
    {
        /// <summary>Nombre de hits consécutifs actuels.</summary>
        public readonly int Combo;
        /// <summary>Multiplicateur de dégâts actuel (1.0, 1.5, 2.0).</summary>
        public readonly float Multiplier;
        /// <summary>Vrai si le combo vient de se reset (VFX de break à jouer).</summary>
        public readonly bool WasBroken;

        /// <summary>Constructeur.</summary>
        public ComboUpdatedEvent(int combo, float multiplier, bool wasBroken)
        {
            Combo = combo;
            Multiplier = multiplier;
            WasBroken = wasBroken;
        }
    }

    /// <summary>
    /// Suivi des combos consécutifs du joueur. Singleton par scène.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le système écoute les <see cref="DamageDealtEvent"/> publiés par le
    /// <see cref="GameEventBus"/> (issus de HealthComponent via PlayerCombat ou
    /// Projectile). À chaque hit réussi, le combo est incrémenté et le timer
    /// de fenêtre est rafraîchi.
    /// </para>
    /// <para>
    /// Le système écoute aussi <see cref="PlayerDamagedEvent"/> pour reset le
    /// combo si le joueur subit des dégâts.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ComboChain : MonoBehaviour
    {
        private static ComboChain _instance;
        /// <summary>Instance globale (auto-créée si absente).</summary>
        public static ComboChain Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ComboChain]");
                    _instance = go.AddComponent<ComboChain>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Configuration")]
        [Tooltip("Fenêtre de temps entre deux hits pour maintenir le combo (s).")]
        [SerializeField] private float _comboWindow = 3f;
        [Tooltip("Seuil de combo pour passer en ×1.5.")]
        [SerializeField] private int _tier1Threshold = 10;
        [Tooltip("Seuil de combo pour passer en ×2.0 (cap).")]
        [SerializeField] private int _tier2Threshold = 20;
        [Tooltip("Multiplicateur tier 1.")]
        [SerializeField] private float _tier1Multiplier = 1.5f;
        [Tooltip("Multiplicateur tier 2 (cap).")]
        [SerializeField] private float _tier2Multiplier = 2.0f;
        [Tooltip("Si vrai, le combo reset quand le joueur subit des dégâts.")]
        [SerializeField] private bool _resetOnDamageTaken = true;

        /// <summary>Combo actuel (nombre de hits consécutifs).</summary>
        public int CurrentCombo { get; private set; }
        /// <summary>Multiplicateur de dégâts courant (1.0, 1.5 ou 2.0).</summary>
        public float CurrentMultiplier => GetMultiplier(CurrentCombo);
        /// <summary>Vrai si un combo est en cours (au moins 1 hit récent).</summary>
        public bool IsActive => CurrentCombo > 0 && _comboTimer > 0f;

        private float _comboTimer;
        private uint _lastSourceId; // Pour filtrer les hits du joueur local.
        private IDisposable _subDamageDealt;
        private IDisposable _subPlayerDamaged;

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
            _subPlayerDamaged = bus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void OnDisable()
        {
            _subDamageDealt?.Dispose();
            _subPlayerDamaged?.Dispose();
            _subDamageDealt = null;
            _subPlayerDamaged = null;
        }

        private void Update()
        {
            if (_comboTimer > 0f)
            {
                _comboTimer -= Time.deltaTime;
                if (_comboTimer <= 0f)
                {
                    // Fenêtre expirée : reset.
                    BreakCombo();
                }
            }
        }

        /// <summary>
        /// Force l'ID source du joueur local (pour filtrer les DamageDealtEvent).
        /// À appeler par le PlayerController quand il s'enregistre auprès du PlayerContext.
        /// </summary>
        /// <param name="sourceId">Identifiant unique du joueur local.</param>
        public void SetLocalPlayerSourceId(uint sourceId)
        {
            _lastSourceId = sourceId;
        }

        /// <summary>
        /// Retourne le multiplicateur de dégâts pour un combo donné.
        /// </summary>
        /// <param name="combo">Combo actuel.</param>
        /// <returns>Multiplicateur (1.0, 1.5 ou 2.0).</returns>
        public float GetMultiplier(int combo)
        {
            if (combo >= _tier2Threshold) return _tier2Multiplier;
            if (combo >= _tier1Threshold) return _tier1Multiplier;
            return 1f;
        }

        /// <summary>
        /// Incrémente manuellement le combo (si besoin, hors bus d'événements).
        /// </summary>
        public void RegisterHit()
        {
            CurrentCombo++;
            _comboTimer = _comboWindow;
            PublishComboUpdated(false);
        }

        /// <summary>
        /// Force le reset du combo (avec VFX de break).
        /// </summary>
        public void BreakCombo()
        {
            if (CurrentCombo == 0) return;
            CurrentCombo = 0;
            _comboTimer = 0f;
            PublishComboUpdated(true);
        }

        // --- Handlers d'événements ---

        private void OnDamageDealt(in DamageDealtEvent evt)
        {
            // Filtre : on ne compte que les hits du joueur local.
            if (_lastSourceId != 0u && evt.SourceId != _lastSourceId) return;
            // On ignore les dégâts à 0 (miss).
            if (evt.Amount <= 0f) return;

            RegisterHit();
        }

        private void OnPlayerDamaged(in PlayerDamagedEvent evt)
        {
            if (!_resetOnDamageTaken) return;
            BreakCombo();
        }

        // --- Publication ---

        private void PublishComboUpdated(bool broken)
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Publish(new ComboUpdatedEvent(CurrentCombo, CurrentMultiplier, broken));
        }
    }
}
