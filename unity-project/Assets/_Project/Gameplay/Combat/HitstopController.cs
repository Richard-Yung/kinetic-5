// ============================================================================
//  KINETICS 5 — Hitstop Controller (freeze frames sur gros impacts)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Wrapper autour de <see cref="TimeManager.TriggerHitstop"/>. Fournit une API
//  sémantique pour les hitstops selon le type d'événement :
//    • Normal hit    : 3 frames (~0.05s)
//    • Headshot      : 5 frames (~0.08s)
//    • Kill          : 8 frames (~0.13s)
//    • Boss kill     : 12 frames (~0.20s)
//
//  Le TimeManager maintient une file de max 2 hitstops en attente. Au-delà,
//  les hitstops supplémentaires sont ignorés (anti-spam).
// ============================================================================
using KINETICS5.Core;
using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Type d'événement déclenchant un hitstop. Détermine la durée du freeze.
    /// </summary>
    public enum HitstopType
    {
        /// <summary>Coup normal (3 frames ~0.05s).</summary>
        NormalHit,
        /// <summary>Headshot (5 frames ~0.08s).</summary>
        Headshot,
        /// <summary>Kill d'un ennemi standard (8 frames ~0.13s).</summary>
        Kill,
        /// <summary>Kill d'un boss (12 frames ~0.20s).</summary>
        BossKill
    }

    /// <summary>
    /// Wrapper statique autour de <see cref="TimeManager.TriggerHitstop"/>.
    /// Centralise les durées par type d'événement et applique des caps anti-spam.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Design :</b> ce wrapper ne gère PAS la file d'attente (rôle de
    /// <see cref="TimeManager"/> qui cap à 2 hitstops en attente). Ce wrapper
    /// se contente de mapper <see cref="HitstopType"/> à une durée et un TimeScale.
    /// </para>
    /// <para>
    /// <b>Performance :</b> aucune allocation, appel direct au singleton TimeManager.
    /// </para>
    /// </remarks>
    public static class HitstopController
    {
        // Durées par défaut (en secondes, calculées à 60 FPS).
        // 1 frame à 60 FPS = 1/60 = 0.0167s.
        /// <summary>3 frames ~0.05s.</summary>
        public const float NormalHitDuration = 3f / 60f;
        /// <summary>5 frames ~0.08s.</summary>
        public const float HeadshotDuration = 5f / 60f;
        /// <summary>8 frames ~0.13s.</summary>
        public const float KillDuration = 8f / 60f;
        /// <summary>12 frames ~0.20s.</summary>
        public const float BossKillDuration = 12f / 60f;

        // TimeScale appliqué pendant le hitstop (très bas mais pas zéro pour
        // permettre aux particules de continuer un minimum).
        /// <summary>TimeScale pendant hitstop (proche de 0 mais pas figé).</summary>
        public const float HitstopTimeScale = 0.05f;

        /// <summary>
        /// Déclenche un hitstop selon le type d'événement.
        /// </summary>
        /// <param name="type">Type d'événement.</param>
        public static void Trigger(HitstopType type)
        {
            TimeManager tm = TimeManager.Instance;
            if (tm == null) return;

            float duration = type switch
            {
                HitstopType.NormalHit => NormalHitDuration,
                HitstopType.Headshot  => HeadshotDuration,
                HitstopType.Kill      => KillDuration,
                HitstopType.BossKill  => BossKillDuration,
                _                      => NormalHitDuration
            };

            tm.TriggerHitstop(duration, HitstopTimeScale);
        }

        /// <summary>
        /// Déclenche un hitstop avec une durée personnalisée (en secondes).
        /// </summary>
        /// <param name="duration">Durée en secondes (clamped entre 0.02 et 0.2).</param>
        public static void TriggerCustom(float duration)
        {
            TimeManager tm = TimeManager.Instance;
            if (tm == null) return;

            duration = Mathf.Clamp(duration, 0.02f, 0.2f);
            tm.TriggerHitstop(duration, HitstopTimeScale);
        }

        /// <summary>
        /// Annule immédiatement tout hitstop en cours (utile pour reprendre le contrôle).
        /// </summary>
        public static void Cancel()
        {
            TimeManager.Instance?.CancelAllEffects();
        }
    }
}
