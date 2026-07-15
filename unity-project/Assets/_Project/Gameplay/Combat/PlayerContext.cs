using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Registre statique léger pour exposer la position et l'interface <see cref="IDamageable"/>
    /// du joueur local aux systèmes d'IA sans recourir à <c>GameObject.Find</c> ou au tag
    /// <c>Player</c> (coûteux sur mobile et fragile).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le <c>PlayerController</c> (produit par l'agent 2-b) doit appeler
    /// <see cref="Register"/> dans <c>OnEnable</c> et <see cref="Unregister"/> dans
    /// <c>OnDisable</c>. Les ennemis lisent <see cref="PlayerPosition"/> via
    /// <see cref="TryGetPlayer"/> chaque frame (zéro allocation, zéro FindObject).
    /// </para>
    /// <para>
    /// Thread-safety : conception mono-joueur (KINETICS 5 est un FPS solo PvE).
    /// Pour une future évolution co-op, ce registre évoluerait en tableau indexé
    /// par <c>PlayerInput.PlayerId</c>.
    /// </para>
    /// </remarks>
    public static class PlayerContext
    {
        private static Transform _playerTransform;
        private static IDamageable _playerDamageable;
        private static uint _playerId;

        /// <summary>Vrai si un joueur local est actuellement enregistré.</summary>
        public static bool HasPlayer => _playerTransform != null;

        /// <summary>Identifiant unique du joueur local (pour <c>DamageDealtEvent.SourceId</c>).</summary>
        public static uint PlayerId => _playerId;

        /// <summary>
        /// Enregistre le joueur local. À appeler par le PlayerController dans <c>OnEnable</c>.
        /// </summary>
        /// <param name="playerTransform">Transform racine du joueur (pour la position monde).</param>
        /// <param name="damageable">Interface <see cref="IDamageable"/> du joueur (peut être nulle si non résolu).</param>
        /// <param name="playerId">Identifiant unique (par ex. <c>(uint)GetInstanceID()</c>).</param>
        public static void Register(Transform playerTransform, IDamageable damageable = null, uint playerId = 0u)
        {
            _playerTransform = playerTransform;
            _playerDamageable = damageable;
            _playerId = playerId;
        }

        /// <summary>Désenregistre le joueur local. À appeler dans <c>OnDisable</c>.</summary>
        public static void Unregister(Transform playerTransform)
        {
            if (_playerTransform == playerTransform)
            {
                _playerTransform = null;
                _playerDamageable = null;
                _playerId = 0u;
            }
        }

        /// <summary>
        /// Met à jour l'interface <see cref="IDamageable"/> du joueur (si elle est résolue
        /// après le premier Register, par ex. en cas d'initialisation différée).
        /// </summary>
        public static void SetDamageable(IDamageable damageable)
        {
            _playerDamageable = damageable;
        }

        /// <summary>
        /// Tente de récupérer le transform du joueur.
        /// </summary>
        /// <param name="playerTransform">Sortie : transform du joueur, ou <c>null</c> si absent.</param>
        /// <returns>Vrai si un joueur est enregistré.</returns>
        public static bool TryGetPlayer(out Transform playerTransform)
        {
            playerTransform = _playerTransform;
            return _playerTransform != null;
        }

        /// <summary>
        /// Tente de récupérer l'interface <see cref="IDamageable"/> du joueur (pour infliger des dégâts).
        /// </summary>
        /// <param name="damageable">Sortie : interface dommageable du joueur, ou <c>null</c>.</param>
        /// <returns>Vrai si le joueur est enregistré ET implémente <see cref="IDamageable"/>.</returns>
        public static bool TryGetDamageable(out IDamageable damageable)
        {
            damageable = _playerDamageable;
            return _playerDamageable != null;
        }

        /// <summary>Position monde du joueur, ou <c>Vector3.zero</c> si aucun joueur enregistré.</summary>
        public static Vector3 PlayerPosition => _playerTransform != null ? _playerTransform.position : Vector3.zero;

        /// <summary>
        /// Tente d'infliger des dégâts au joueur local. No-op silencieux si le joueur est absent.
        /// </summary>
        /// <param name="amount">Montant de dégâts.</param>
        /// <param name="element">Élément.</param>
        /// <param name="sourceId">Identifiant de la source (ex: instanceId ennemi).</param>
        /// <param name="hitPoint">Point d'impact monde.</param>
        /// <param name="isCritical">Vrai si critique.</param>
        /// <returns>Montant effectif de dégâts infligés (0 si joueur absent).</returns>
        public static float DamagePlayer(float amount, UnityEngine.Vector3 hitPoint, uint sourceId = 0u,
                                          Data.Element element = Data.Element.Kinetic, bool isCritical = false)
        {
            if (_playerDamageable == null) return 0f;
            return _playerDamageable.TakeDamage(amount, element, sourceId, hitPoint, isCritical);
        }
    }
}
