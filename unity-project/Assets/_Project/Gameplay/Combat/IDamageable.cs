using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Contrat minimal pour toute entité capable de subir des dégâts (joueur, ennemi, destructible).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cette interface permet à l'IA ennemie et aux projectiles d'appliquer des dégâts
    /// sans couplage fort au type <c>PlayerStats</c> (produit par l'agent 2-b) ou
    /// <c>EnemyController</c>. Toute cible implémente ce contrat et s'enregistre
    /// auprès du <see cref="PlayerContext"/> (pour le joueur) ou expose l'interface
    /// via <c>GetComponent&lt;IDamageable&gt;()</c> (pour les ennemis).
    /// </para>
    /// <para>
    /// L'argument <paramref name="sourceId"/> correspond à l'instanceId de l'attaquant
    /// (uint, attribué par <see cref="EnemyController"/>). Il est propagé dans le
    /// <c>DamageDealtEvent</c> du bus d'événements pour le telemetry et les killfeeds.
    /// </para>
    /// </remarks>
    public interface IDamageable
    {
        /// <summary>Vrai si la cible est encore vivante (n'a pas appelé <see cref="Die"/>).</summary>
        bool IsAlive { get; }

        /// <summary>Position monde actuelle de la cible (pour les checks de portée/LOS).</summary>
        Vector3 Position { get; }

        /// <summary>
        /// Applique des dégâts à la cible. L'implémentation doit gérer :
        /// <list type="bullet">
        ///   <item>Résistances/faiblesses élémentaires (multiplicateur).</item>
        ///   <item>Absorption par le bouclier avant la santé.</item>
        ///   <item>Publication d'un <c>DamageDealtEvent</c> sur le bus.</item>
        ///   <item>Déclenchement de la mort si PV &lt;= 0.</item>
        /// </list>
        /// </summary>
        /// <param name="amount">Montant brut de dégâts (post-multiplicateur élémentaire, pré-résistances).</param>
        /// <param name="element">Élément des dégâts (pour calcul de faiblesse/résistance).</param>
        /// <param name="sourceId">Identifiant unique de la source (ex: instanceId ennemi).</param>
        /// <param name="hitPoint">Point d'impact monde (pour VFX/decals).</param>
        /// <param name="isCritical">Vrai si coup critique (multiplicateur déjà appliqué).</param>
        /// <returns>Montant effectif de dégâts infligés (post-résistances, post-bouclier).</returns>
        float TakeDamage(float amount, Element element, uint sourceId, Vector3 hitPoint, bool isCritical = false);

        /// <summary>Soin direct (ignore le bouclier, s'applique à la santé).</summary>
        /// <param name="amount">Montant à restaurer.</param>
        void Heal(float amount);

        /// <summary>Restaure le bouclier (si la cible en a un).</summary>
        /// <param name="amount">Montant de bouclier à restaurer.</param>
        void RestoreShield(float amount);

        /// <summary>Force la mort immédiate de la cible (si elle n'est pas déjà morte).</summary>
        void Die();
    }
}
