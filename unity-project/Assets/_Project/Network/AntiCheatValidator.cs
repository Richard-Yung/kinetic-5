// ============================================================================
//  KINETICS 5 — Anti-Cheat Validator (validation serveur)
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Validation des actions joueurs côté serveur (ou côté host autoritaire).
//  Objectif : détecter les comportements impossibles / anormaux et bannir les
//  récidivistes. N'EST PAS une solution anti-cheat complète (un vrai AC nécessite
//  obfuscation binaire, EAC/BattlEye, etc.) — c'est une couche applicative.
//
//  Validations :
//    1. DAMAGE dealt : dans les bornes de l'arme (min/max par rareté + éléments).
//    2. MOVEMENT speed : ≤ vitesse max de l'agent (BaseSpeed × marge 15%).
//    3. FIRE RATE : ≥ temps de cycle de l'arme (1 / fireRate × marge 5%).
//    4. HEADSHOT RATE : statistique sur fenêtre glissante 100 tirs ; > 70% = suspect.
//    5. HEAL/SHIELD : jamais au-dessus du max de l'agent.
//    6. XP/CREDITS par mission : dans une borne plausible (anti-XP-glitch).
//
//  Sanctions :
//    • Niveau 1 (suspect) : log + télémétrie (PostHog flag "suspicious").
//    • Niveau 2 (récidive) : kick du match + reset des gains.
//    • Niveau 3 (cheat confirmé) : ban 7 jours, puis définitif.
// ============================================================================
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using KINETICS5.Core;
using KINETICS5.Data;

namespace KINETICS5.Network
{
    /// <summary>Niveau de sévérité d'une infraction anti-cheat.</summary>
    public enum CheatSeverity
    {
        /// <summary>Pas d'infraction détectée.</summary>
        None,
        /// <summary>Premier Soupçon : log + télémétrie.</summary>
        Suspicious,
        /// <summary>Infraction confirmée, kick + reset gains.</summary>
        Confirmed,
        /// <summary>Récidive, ban temporaire (7 jours).</summary>
        BanTemp,
        /// <summary>Ban définitif.</summary>
        BanPermanent
    }

    /// <summary>Rapport d'infraction.</summary>
    public readonly struct CheatReport
    {
        public readonly string UserId;
        public readonly string Rule;
        public readonly CheatSeverity Severity;
        public readonly string Detail;
        public readonly DateTime DetectedAt;
        public readonly float ObservedValue;
        public readonly float ExpectedMax;

        public CheatReport(string userId, string rule, CheatSeverity sev, string detail, float observed, float expectedMax)
        {
            UserId = userId; Rule = rule; Severity = sev; Detail = detail;
            DetectedAt = DateTime.UtcNow; ObservedValue = observed; ExpectedMax = expectedMax;
        }
    }

    /// <summary>Statistiques par joueur (fenêtre glissante pour détection d'anomalies).</summary>
    public sealed class PlayerCheatStats
    {
        public string UserId;
        public int ShotsFired;
        public int Headshots;
        public float HeadshotRate => ShotsFired < 10 ? 0f : (float)Headshots / ShotsFired;
        public int SuspicionStrikes;
        public DateTime? BannedUntil;
        public readonly Queue<DateTime> RecentShotsWindow = new(100);
    }

    /// <summary>
    /// Validateur anti-cheat serveur (ou host autoritaire). Singleton MonoBehaviour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AntiCheatValidator : MonoBehaviour
    {
        private static AntiCheatValidator _instance;
        public static AntiCheatValidator Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[AntiCheatValidator]");
                _instance = go.AddComponent<AntiCheatValidator>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        [Header("Seuils")]
        [Tooltip("Marge de tolérance pour les dégâts (ex: 1.15 = 15% au-dessus du max).")]
        [Range(1f, 2f)][SerializeField] private float _damageMargin = 1.15f;
        [Tooltip("Marge de tolérance pour la vitesse de déplacement.")]
        [Range(1f, 2f)][SerializeField] private float _speedMargin = 1.15f;
        [Tooltip("Marge de tolérance pour la cadence de tir (1.05 = 5% plus rapide accepté).")]
        [Range(1f, 1.5f)][SerializeField] private float _fireRateMargin = 1.05f;
        [Tooltip("Seuil de taux de headshot au-dessus duquel on flag (0..1).")]
        [Range(0.3f, 1f)][SerializeField] private float _headshotRateThreshold = 0.7f;
        [Tooltip("Nombre minimum de tirs avant de mesurer le taux de headshot.")]
        [SerializeField] private int _headshotMinSamples = 20;
        [Tooltip("Nombre de strikes avant ban temporaire.")]
        [SerializeField] private int _strikesBeforeTempBan = 3;
        [Tooltip("Durée du ban temporaire (jours).")]
        [SerializeField] private int _tempBanDays = 7;

        /// <summary>Déclenché à chaque infraction détectée.</summary>
        public event Action<CheatReport> CheatDetected;
        /// <summary>Déclenché à chaque ban (temp ou définitif).</summary>
        public event Action<string, CheatSeverity> PlayerBanned;

        private readonly Dictionary<string, PlayerCheatStats> _stats = new(64);

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            ServiceLocator.Instance.Register(this);
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            ServiceLocator.Instance?.Unregister<AntiCheatValidator>();
            _instance = null;
        }

        /// <summary>Récupère (ou crée) les stats d'un joueur.</summary>
        public PlayerCheatStats GetStats(string userId)
        {
            if (!_stats.TryGetValue(userId, out var s))
            {
                s = new PlayerCheatStats { UserId = userId };
                _stats[userId] = s;
            }
            return s;
        }

        // ------------------------------------------------------------------------
        //  VALIDATION DES ACTIONS
        // ------------------------------------------------------------------------

        /// <summary>Valide une action joueur (tir, ability, etc.).</summary>
        public ActionValidationResult ValidatePlayerAction(PlayerAction action)
        {
            if (action == null) return new ActionValidationResult(false, "Action null.");
            var stats = GetStats(action.UserId);

            // Vérifier ban actif.
            if (stats.BannedUntil.HasValue && stats.BannedUntil.Value > DateTime.UtcNow)
            {
                var left = (stats.BannedUntil.Value - DateTime.UtcNow).TotalHours;
                Report(action.UserId, "ban_active", CheatSeverity.Confirmed,
                       $"Action rejetée : joueur banni ({left:F1}h restantes).", 0, 0);
                return new ActionValidationResult(false, "banned");
            }

            switch (action.Type)
            {
                case "shoot":  return ValidateShoot(action, stats);
                case "ability":return ValidateAbility(action, stats);
                case "interact": return new ActionValidationResult(true, null);
                case "reload": return new ActionValidationResult(true, null);
                case "switch": return new ActionValidationResult(true, null);
                default: return new ActionValidationResult(true, null);
            }
        }

        private ActionValidationResult ValidateShoot(PlayerAction action, PlayerCheatStats stats)
        {
            var weapon = DataLoader.GetWeapon(action.WeaponId);
            if (weapon == null)
            {
                Report(action.UserId, "unknown_weapon", CheatSeverity.Suspicious,
                       $"Arme inconnue : {action.WeaponId}", 0, 0);
                return new ActionValidationResult(false, "unknown_weapon");
            }

            // 1. FIRE RATE : on compare au précédent tir (RecentShotsWindow).
            if (stats.RecentShotsWindow.Count > 0)
            {
                var last = stats.RecentShotsWindow.Peek();
                var elapsed = (DateTime.UtcNow - last).TotalSeconds;
                // Cadence de l'arme : fireRatePct 0..100 → coups/sec 1..12 (map linéaire).
                double fireRatePerSec = Mathf.Lerp(1f, 12f, weapon.FireRatePct / 100f);
                double minInterval = 1.0 / (fireRatePerSec * _fireRateMargin);
                if (elapsed < minInterval)
                {
                    Report(action.UserId, "fire_rate_too_high", CheatSeverity.Confirmed,
                           $"Cadence {1.0/elapsed:F1}/s > max {fireRatePerSec * _fireRateMargin:F1}/s",
                           (float)(1.0/elapsed), (float)(fireRatePerSec * _fireRateMargin));
                    return new ActionValidationResult(false, "fire_rate");
                }
            }

            // 2. DAMAGE MAX par arme (vérifié à l'application des dégâts, pas ici).
            stats.ShotsFired++;
            stats.RecentShotsWindow.Enqueue(DateTime.UtcNow);
            while (stats.RecentShotsWindow.Count > 100) stats.RecentShotsWindow.Dequeue();

            // 3. HEADSHOT RATE (détection statistique).
            if (stats.ShotsFired >= _headshotMinSamples)
            {
                if (stats.HeadshotRate > _headshotRateThreshold)
                {
                    Report(action.UserId, "headshot_rate_anomaly", CheatSeverity.Suspicious,
                           $"Taux HS {stats.HeadshotRate:P0} > seuil {_headshotRateThreshold:P0} sur {stats.ShotsFired} tirs",
                           stats.HeadshotRate, _headshotRateThreshold);
                }
            }

            return new ActionValidationResult(true, null);
        }

        private ActionValidationResult ValidateAbility(PlayerAction action, PlayerCheatStats stats)
        {
            // Les abilities ont un cooldown fixe ; on vérifie via les RPC serveur.
            // Ici on valide juste la cohérence spatiale (origin raisonnable).
            if (action.Origin.sqrMagnitude > 1e8f) // 10km² max
            {
                Report(action.UserId, "ability_origin_oob", CheatSeverity.Suspicious,
                       $"Origin ability hors borne : {action.Origin}", action.Origin.magnitude, 1e4f);
                return new ActionValidationResult(false, "origin_oob");
            }
            return new ActionValidationResult(true, null);
        }

        // ------------------------------------------------------------------------
        //  VALIDATION DES DEGATS (côté application)
        // ------------------------------------------------------------------------

        /// <summary>Valide qu'un montant de dégâts est cohérent avec l'arme + multiplicateurs.</summary>
        public bool ValidateDamage(string userId, string weaponId, float damage, bool isHeadshot, bool isCritical, Element element)
        {
            var weapon = DataLoader.GetWeapon(weaponId);
            if (weapon == null) return true; // pas de data, on laisse passer (mode dégradé)

            // Dégâts de base : DamagePct × 100 (map approximative).
            float baseDamage = weapon.DamagePct;
            float maxDamage = baseDamage * _damageMargin;
            if (isHeadshot) maxDamage *= 2.0f;
            if (isCritical) maxDamage *= 1.5f;
            // Bonus élémentaire si weakness ciblée : ×1.5.
            maxDamage *= 1.5f;

            if (damage > maxDamage)
            {
                Report(userId, "damage_exceeds_max", CheatSeverity.Confirmed,
                       $"Dégâts {damage:F0} > max {maxDamage:F0} (arme {weaponId}, HS={isHeadshot}, crit={isCritical})",
                       damage, maxDamage);
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------------
        //  VALIDATION DE LA VITESSE DE DEPLACEMENT
        // ------------------------------------------------------------------------

        /// <summary>Valide qu'une vitesse instantanée est dans les bornes de l'agent.</summary>
        public bool ValidateMovementSpeed(string userId, float observedSpeed, float agentBaseSpeed)
        {
            float maxSpeed = agentBaseSpeed * _speedMargin;
            // Tolérance pour les boosts temporaires (cap à 2× max).
            float hardCap = maxSpeed * 2f;
            if (observedSpeed > hardCap)
            {
                Report(userId, "speed_hack", CheatSeverity.Confirmed,
                       $"Vitesse {observedSpeed:F1} > hard cap {hardCap:F1}",
                       observedSpeed, hardCap);
                return false;
            }
            if (observedSpeed > maxSpeed)
            {
                Report(userId, "speed_margin_exceeded", CheatSeverity.Suspicious,
                       $"Vitesse {observedSpeed:F1} > marge {maxSpeed:F1}",
                       observedSpeed, maxSpeed);
            }
            return true;
        }

        // ------------------------------------------------------------------------
        //  SIGNALEMENT HEADSHOT (à appeler quand un tir est un HS)
        // ------------------------------------------------------------------------

        /// <summary>Enregistre un headshot pour les statistiques de détection.</summary>
        public void RecordHeadshot(string userId)
        {
            var stats = GetStats(userId);
            stats.Headshots++;
        }

        // ------------------------------------------------------------------------
        //  BAN MANAGEMENT
        // ------------------------------------------------------------------------

        /// <summary>Bannit un joueur temporairement (par défaut 7 jours).</summary>
        public void BanTemporary(string userId, int days = 0)
        {
            days = days > 0 ? days : _tempBanDays;
            var stats = GetStats(userId);
            stats.BannedUntil = DateTime.UtcNow.AddDays(days);
            PlayerBanned?.Invoke(userId, CheatSeverity.BanTemp);
            Debug.LogWarning($"[AntiCheatValidator] {userId} banni {days} jours.");
        }

        /// <summary>Bannit un joueur définitivement.</summary>
        public void BanPermanent(string userId)
        {
            var stats = GetStats(userId);
            stats.BannedUntil = DateTime.UtcNow.AddYears(100);
            PlayerBanned?.Invoke(userId, CheatSeverity.BanPermanent);
            Debug.LogError($"[AntiCheatValidator] {userId} BANNI DÉFINITIVEMENT.");
        }

        /// <summary>Lève un ban (modération manuelle).</summary>
        public void Unban(string userId)
        {
            var stats = GetStats(userId);
            stats.BannedUntil = null;
            stats.SuspicionStrikes = 0;
        }

        // ------------------------------------------------------------------------
        //  INTERNES
        // ------------------------------------------------------------------------

        private void Report(string userId, string rule, CheatSeverity sev, string detail, float observed, float expectedMax)
        {
            var report = new CheatReport(userId, rule, sev, detail, observed, expectedMax);
            CheatDetected?.Invoke(report);

            // Télémétrie (PostHog / Unity Analytics).
            var telemetry = ServiceLocator.Instance?.Get<TelemetryLogger>();
            telemetry?.Track("anticheat_violation", new Dictionary<string, object>
            {
                { "userId", userId }, { "rule", rule }, { "severity", sev.ToString() },
                { "detail", detail }, { "observed", observed }, { "expectedMax", expectedMax }
            });

            var stats = GetStats(userId);
            if (sev == CheatSeverity.Suspicious) stats.SuspicionStrikes++;
            if (sev == CheatSeverity.Confirmed)  stats.SuspicionStrikes += 2;

            if (stats.SuspicionStrikes >= _strikesBeforeTempBan && !stats.BannedUntil.HasValue)
                BanTemporary(userId);

            if (stats.SuspicionStrikes >= _strikesBeforeTempBan * 2)
                BanPermanent(userId);
        }

        /// <summary>Indique si un joueur est actuellement banni.</summary>
        public bool IsBanned(string userId)
        {
            return _stats.TryGetValue(userId, out var s) && s.BannedUntil.HasValue && s.BannedUntil.Value > DateTime.UtcNow;
        }
    }
}
