using KINETICS5.Data;
using KINETICS5.Gameplay.Enemies;
using UnityEngine;

namespace KINETICS5.Gameplay.Missions
{
    /// <summary>
    /// Composant posé sur un Transform servant de point d'apparition d'ennemis. Offre :
    /// <list type="bullet">
    ///   <item>Identifiant <see cref="EnemyId"/> (résolu via <see cref="DataLoader"/>).</item>
    ///   <item>Gizmo éditeur (sphere colorée selon la classe d'ennemi).</item>
    ///   <item>API de spawn direct (<see cref="Spawn"/>) via <see cref="EnemySpawner"/>.</item>
    ///   <item>Indicateur visuel éditeur-only (pictogramme directionnel).</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Utilisé par <see cref="EnemySpawner"/> (mode auto-discover) pour peupler dynamiquement
    /// les points d'apparition d'une scène de mission.
    /// </remarks>
    public class EnemySpawnPoint : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Id de l'ennemi à faire apparaître à ce point (EnemySO.Id).")]
        [SerializeField] private string _enemyId = string.Empty;
        [Tooltip("Classe d'ennemi (pour couleur du gizmo).")]
        [SerializeField] private EnemyClass _gizmoClass = EnemyClass.Soldier;
        [Tooltip("Rayon du gizmo éditeur.")]
        [SerializeField] private float _gizmoRadius = 0.6f;
        [Tooltip("Vrai si le spawn point est activé (utilisé par le spawner).")]
        [SerializeField] private bool _isActive = true;

        /// <summary>Id de l'ennemi à spawner.</summary>
        public string EnemyId => _enemyId;

        /// <summary>Vrai si le spawn point est activé.</summary>
        public bool IsActive => _isActive;

        /// <summary>Position monde du spawn point.</summary>
        public Vector3 Position => transform.position;

        /// <summary>Rotation du spawn point (pour orientation initiale de l'ennemi).</summary>
        public Quaternion Rotation => transform.rotation;

        /// <summary>
        /// Spawn l'ennemi configuré via le spawner fourni.
        /// </summary>
        /// <param name="spawner">Spawner à utiliser (ne doit pas être null).</param>
        /// <returns>L'<see cref="EnemyController"/> instancié, ou null si échec.</returns>
        public EnemyController Spawn(EnemySpawner spawner)
        {
            if (spawner == null)
            {
                Debug.LogWarning("[EnemySpawnPoint] Spawner null, spawn impossible.");
                return null;
            }
            if (!_isActive || string.IsNullOrEmpty(_enemyId))
            {
                return null;
            }
            return spawner.SpawnEnemy(_enemyId, transform.position);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Couleur selon la classe (palette KINETICS 5).
            Color c = _gizmoClass switch
            {
                EnemyClass.Grunt => new Color(0.42f, 0.96f, 0.17f, 0.8f),     // vert
                EnemyClass.Soldier => new Color(0.102f, 0.631f, 0.808f, 0.8f), // cyan
                EnemyClass.Elite => new Color(1f, 0.91f, 0.21f, 0.8f),         // jaune
                EnemyClass.Sniper => new Color(0.6f, 0.4f, 1f, 0.8f),          // violet
                EnemyClass.Heavy => new Color(1f, 0.5f, 0f, 0.8f),             // orange
                EnemyClass.Drone => new Color(0.7f, 0.7f, 0.7f, 0.8f),         // gris
                EnemyClass.Boss => new Color(1f, 0f, 0.13f, 0.9f),             // rouge
                _ => Color.white
            };
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position, _gizmoRadius);
            Gizmos.DrawWireSphere(transform.position, _gizmoRadius * 1.5f);

            // Flèche directionnelle (forward).
            Gizmos.color = Color.white;
            Vector3 forward = transform.forward * 2f;
            Gizmos.DrawLine(transform.position, transform.position + forward);
            // Tête de flèche.
            Vector3 right = Quaternion.Euler(0, 30, 0) * -forward * 0.3f;
            Vector3 left = Quaternion.Euler(0, -30, 0) * -forward * 0.3f;
            Gizmos.DrawLine(transform.position + forward, transform.position + forward + right);
            Gizmos.DrawLine(transform.position + forward, transform.position + forward + left);

            // Label.
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f,
                $"{_gizmoClass}\n[{_enemyId}]");
        }

        private void OnValidate()
        {
            // Validation : warn si enemyId non vide mais introuvable dans DataLoader.
            if (!string.IsNullOrEmpty(_enemyId))
            {
                var data = DataLoader.GetEnemy(_enemyId);
                if (data == null)
                {
                    Debug.LogWarning($"[EnemySpawnPoint] EnemyId '{_enemyId}' introuvable dans DataLoader (sur {gameObject.name}).", this);
                }
                else
                {
                    _gizmoClass = data.Class; // auto-sync de la classe pour le gizmo.
                }
            }
        }
#endif
    }
}
