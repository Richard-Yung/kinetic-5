// ============================================================================
//  KINETICS 5 — Screen Shake (wrapper autour de CameraManager.Shake)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Centralise tous les camera shakes du jeu. Wraps <see cref="CameraManager.Shake"/>
//  avec une API sémantique (small/medium/big/explosion/ultimate) et un système
//  anti-nausée (cap amplitude 1.5, intervalle minimum 0.1s entre deux shakes).
//
//  Shakes disponibles :
//    • Hit       (small 0.1, direction away from hit point, 0.3s)
//    • Heavy hit (medium 0.3, 0.4s)
//    • Explosion (big 1.0, radial, 0.5s)
//    • Ultimate  (1.5, 1s)
//    • Custom    (magnitude, duration, direction)
// ============================================================================
using System;
using KINETICS5.Core;
using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Magnitudes prédéfinies pour les camera shakes.
    /// </summary>
    public enum ShakeIntensity
    {
        /// <summary>Petit shake (impact léger, 0.1).</summary>
        Small,
        /// <summary>Shake moyen (impact lourd, 0.3).</summary>
        Medium,
        /// <summary>Shake gros (explosion proche, 0.6).</summary>
        Big,
        /// <summary>Shake d'explosion (1.0, radial).</summary>
        Explosion,
        /// <summary>Shake d'ultimate (1.5, longue durée).</summary>
        Ultimate
    }

    /// <summary>
    /// Wrapper statique autour de <see cref="CameraManager.Shake"/>. Applique des
    /// caps anti-nausée et fournit une API sémantique pour les différents types
    /// de shakes utilisés dans KINETICS 5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Anti-nausée :</b>
    /// <list type="bullet">
    ///   <item>Amplitude maximale 1.5 (toute valeur supérieure est clampée).</item>
    ///   <item>Intervalle minimum 0.1s entre deux shakes (évite la saturation).</item>
    ///   <item>Durée maximale 1.5s (les ultimes inclus).</item>
    /// </list>
    /// </para>
    /// </remarks>
    public static class ScreenShake
    {
        /// <summary>Amplitude maximale (anti-nausée).</summary>
        public const float MaxMagnitude = 1.5f;
        /// <summary>Intervalle minimum entre deux shakes (anti-nausée).</summary>
        public const float MinInterval = 0.1f;
        /// <summary>Durée maximale d'un shake.</summary>
        public const float MaxDuration = 1.5f;

        // Magnitudes par défaut (pré-calculées pour la hot path).
        private const float SmallMag = 0.1f;
        private const float MediumMag = 0.3f;
        private const float BigMag = 0.6f;
        private const float ExplosionMag = 1.0f;
        private const float UltimateMag = 1.5f;

        // Fréquences par défaut (Hz du bruit de Perlin).
        private const float SmallFreq = 4f;
        private const float MediumFreq = 3f;
        private const float BigFreq = 2.5f;
        private const float ExplosionFreq = 2f;
        private const float UltimateFreq = 1.5f;

        // Durées par défaut (s).
        private const float SmallDur = 0.3f;
        private const float MediumDur = 0.4f;
        private const float BigDur = 0.5f;
        private const float ExplosionDur = 0.5f;
        private const float UltimateDur = 1f;

        private static float _lastShakeTime;

        /// <summary>
        /// Déclenche un shake directionnel (typiquement sur un coup reçu).
        /// Direction = away from hit point (normalized).
        /// </summary>
        /// <param name="intensity">Intensité prédéfinie.</param>
        /// <param name="hitPoint">Point d'impact monde (pour direction).</param>
        public static void Hit(ShakeIntensity intensity, Vector3 hitPoint)
        {
            CameraManager cam = CameraManager.Instance;
            if (cam == null) return;

            // Direction = away from hit point (le joueur est à la caméra).
            Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Vector3 direction = (camPos - hitPoint).normalized;

            switch (intensity)
            {
                case ShakeIntensity.Small:
                    Trigger(SmallMag, SmallFreq, SmallDur);
                    break;
                case ShakeIntensity.Medium:
                    Trigger(MediumMag, MediumFreq, MediumDur);
                    break;
                case ShakeIntensity.Big:
                    Trigger(BigMag, BigFreq, BigDur);
                    break;
                case ShakeIntensity.Explosion:
                    Trigger(ExplosionMag, ExplosionFreq, ExplosionDur);
                    break;
                case ShakeIntensity.Ultimate:
                    Trigger(UltimateMag, UltimateFreq, UltimateDur);
                    break;
            }
        }

        /// <summary>
        /// Shake radial d'explosion. Override de l'amplitude par distance.
        /// </summary>
        /// <param name="explosionCenter">Centre monde de l'explosion.</param>
        /// <param name="maxRadius">Rayon max d'effet (au-delà, pas de shake).</param>
        public static void Explosion(Vector3 explosionCenter, float maxRadius)
        {
            CameraManager cam = CameraManager.Instance;
            if (cam == null) return;

            Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            float dist = Vector3.Distance(camPos, explosionCenter);
            if (dist > maxRadius) return;

            // Falloff : amplitude proportionnelle à la proximité.
            float t = 1f - Mathf.Clamp01(dist / maxRadius);
            float mag = Mathf.Lerp(0.3f, ExplosionMag, t);
            Trigger(mag, ExplosionFreq, ExplosionDur);
        }

        /// <summary>
        /// Shake d'ultimate (gros burst + longue durée).
        /// </summary>
        public static void Ultimate()
        {
            Trigger(UltimateMag, UltimateFreq, UltimateDur);
        }

        /// <summary>
        /// Déclenche un shake personnalisé (respecte les caps anti-nausée).
        /// </summary>
        /// <param name="magnitude">Amplitude 0..1.5 (clamped).</param>
        /// <param name="frequency">Fréquence Hz (typique 1..5).</param>
        /// <param name="duration">Durée en secondes (clamped à 1.5s max).</param>
        public static void Trigger(float magnitude, float frequency, float duration)
        {
            CameraManager cam = CameraManager.Instance;
            if (cam == null) return;

            // Anti-nausée : intervalle minimum entre deux shakes.
            if (Time.time - _lastShakeTime < MinInterval) return;

            // Caps.
            magnitude = Mathf.Clamp(magnitude, 0f, MaxMagnitude);
            duration = Mathf.Min(duration, MaxDuration);

            cam.Shake(magnitude, frequency, duration);
            _lastShakeTime = Time.time;
        }

        /// <summary>
        /// Force un reset immédiat (utile lors d'un respawn ou d'une cutscene).
        /// </summary>
        public static void Reset()
        {
            _lastShakeTime = 0f;
        }
    }
}
