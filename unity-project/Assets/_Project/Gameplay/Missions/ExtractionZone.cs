using System;
using KINETICS5.Core;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Missions
{
    /// <summary>
    /// Zone d'extraction pour les missions de type <see cref="MissionType.Extraction"/>.
    /// Déclenche un timer (10s par défaut) lorsque le joueur entre dans la zone. Si le joueur
    /// reste dans la zone jusqu'à la fin du timer, l'extraction est complétée et notifie le
    /// <see cref="MissionDirector"/>. Le joueur peut sortir/rentrer librement ; le timer
    /// reprend à zéro en cas de sortie.
    /// </summary>
    /// <remarks>
    /// Placez ce composant sur un trigger Collider (IsTrigger = true) avec le tag "ExtractionZone".
    /// Le joueur doit avoir le tag "Player" ou implémenter <c>IDamageable</c> (via
    /// <see cref="Gameplay.Combat.PlayerContext"/>).
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class ExtractionZone : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Durée d'extraction requise (secondes).")]
        [Min(1f)][SerializeField] private float _extractionDuration = 10f;
        [Tooltip("Id de la zone (doit matcher le TargetId de l'objectif Extract).")]
        [SerializeField] private string _zoneId = "exfil_point";
        [Tooltip("Vrai si le joueur doit appuyer sur une touche pour démarrer l'extraction (sinon auto).")]
        [SerializeField] private bool _requireManualStart = false;
        [Tooltip("Couleur du VFX d'extraction (palette KINETICS 5).")]
        [SerializeField] private Color _zoneColor = new(0.102f, 0.631f, 0.808f, 0.4f);

        [Header("Références")]
        [Tooltip("MissionDirector à notifier (auto-résolu si dans la scène).")]
        [SerializeField] private MissionDirector _missionDirector;

        /// <summary>Progression de l'extraction (0..1).</summary>
        public float Progress { get; private set; }

        /// <summary>Vrai si l'extraction est en cours.</summary>
        public bool IsExtracting { get; private set; }

        /// <summary>Vrai si l'extraction est complétée.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>Événement de progression (0..1).</summary>
        public event Action<float> OnProgressUpdated;

        /// <summary>Événement de complétion.</summary>
        public event Action OnExtractionComplete;

        private float _extractionTimer;
        private bool _playerInZone;
        private Collider _zoneCollider;

        private void Reset()
        {
            _missionDirector = FindFirstObjectByType<MissionDirector>();
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            _zoneCollider = GetComponent<Collider>();
            if (_zoneCollider != null) _zoneCollider.isTrigger = true;
            if (_missionDirector == null) _missionDirector = FindFirstObjectByType<MissionDirector>();
        }

        private void Update()
        {
            if (IsComplete || !IsExtracting) return;

            _extractionTimer += Time.deltaTime;
            Progress = Mathf.Clamp01(_extractionTimer / _extractionDuration);
            OnProgressUpdated?.Invoke(Progress);

            if (_extractionTimer >= _extractionDuration)
            {
                CompleteExtraction();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            _playerInZone = true;
            if (!_requireManualStart && !IsComplete)
            {
                StartExtraction();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            _playerInZone = false;
            // Si le joueur sort, le timer se réinitialise (reprend à zéro à la re-entrée).
            if (!IsComplete)
            {
                IsExtracting = false;
                _extractionTimer = 0f;
                Progress = 0f;
                OnProgressUpdated?.Invoke(0f);
            }
        }

        /// <summary>Démarre manuellement l'extraction (si <see cref="_requireManualStart"/> est vrai).</summary>
        public void StartExtraction()
        {
            if (IsComplete) return;
            if (!_playerInZone) return;
            IsExtracting = true;
            _extractionTimer = 0f;
        }

        private void CompleteExtraction()
        {
            IsComplete = true;
            IsExtracting = false;
            Progress = 1f;
            OnProgressUpdated?.Invoke(1f);
            OnExtractionComplete?.Invoke();

            // Notification au MissionDirector (déclenche la complétion d'objectif Extract).
            if (_missionDirector != null)
            {
                _missionDirector.NotifyReachPoint(_zoneId);
                _missionDirector.NotifyExtractionComplete();
            }
        }

        private bool IsPlayer(Collider other)
        {
            // Check par tag (rapide) puis par IDamageable (fallback si tag absent).
            if (other.CompareTag("Player")) return true;
            return other.GetComponent<Gameplay.Combat.IDamageable>() != null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _zoneColor;
            if (_zoneCollider is BoxCollider box)
            {
                Gizmos.DrawCube(transform.position + box.center, box.size);
            }
            else if (_zoneCollider is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }
            else
            {
                Gizmos.DrawCube(transform.position, Vector3.one * 2f);
            }

            // Label.
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                $"EXTRACTION\n{_zoneId}\n{Progress * 100f:F0}%");
        }
#endif
    }
}
