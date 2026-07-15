using System;
using UnityEngine;

namespace KINETICS5.Data
{
    /// <summary>
    /// Courbe de progression joueur (XP → niveau).
    /// Asset authoring éditeur ; équivalent runtime data-driven :
    /// <see cref="ProgressionCurveDto"/> chargé depuis
    /// <c>Resources/Data/progression.json</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressionCurve", menuName = "KINETICS 5/Progression Curve", order = 16)]
    public sealed class ProgressionCurveSO : ScriptableObject
    {
        [Header("Courbe d'XP")]
        [Tooltip("Axe X = niveau (1..MaxLevel), axe Y = XP total cumulé requis pour atteindre ce niveau.")]
        public AnimationCurve XpCurve = AnimationCurve.Linear(1f, 0f, 60f, 100000f);

        [Tooltip("Niveau maximum atteignable.")]
        [Range(2, 200)] public int MaxLevel = 60;

        /// <summary>
        /// Retourne l'XP cumulé total requis pour atteindre le niveau donné.
        /// </summary>
        /// <param name="level">Niveau cible (borné à [1, <see cref="MaxLevel"/>]).</param>
        /// <returns>XP total cumulé.</returns>
        public int GetTotalXpForLevel(int level)
        {
            int clamped = Mathf.Clamp(level, 1, MaxLevel);
            return Mathf.RoundToInt(XpCurve.Evaluate(clamped));
        }

        /// <summary>
        /// Retourne le niveau atteint pour un montant d'XP cumulé donné.
        /// </summary>
        /// <param name="totalXp">XP total cumulé du joueur.</param>
        /// <returns>Niveau atteint (1..MaxLevel).</returns>
        public int GetLevelForTotalXp(int totalXp)
        {
            for (int lvl = MaxLevel; lvl >= 1; lvl--)
            {
                if (totalXp >= GetTotalXpForLevel(lvl))
                {
                    return lvl;
                }
            }
            return 1;
        }

        /// <summary>
        /// Retourne l'XP restant pour passer du niveau courant au suivant.
        /// </summary>
        public int GetXpToNextLevel(int currentLevel, int currentTotalXp)
        {
            int next = GetTotalXpForLevel(Mathf.Min(currentLevel + 1, MaxLevel));
            return Mathf.Max(0, next - currentTotalXp);
        }
    }
}
