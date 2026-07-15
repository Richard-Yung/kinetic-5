// ============================================================================
//  KINETICS 5 — Floating Damage (chiffres de dégâts flottants)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Affiche les nombres de dégâts en 3D monde (billboard caméra) :
//    • Couleur par élément (Kinetic #FFFFFF, Energy #1AA1CE, Cryo #60A5FA,
//      Volt #FFE735, Explosive #F97316).
//    • Critique : police plus grosse + couleur or (#fbbf24).
//    • Kill : texte "ELIMINATED" en rouge (#FE0022).
//    • Monte de +2m en 1s, fade-out progressif.
//    • Billboarded via LookAt caméra.
//    • Poolé via ObjectPooler, clé "DamageNumber". Max 30 simultanés.
//
//  Utilise TextMeshPro pour le rendu (police Audiowide pour les chiffres).
//  Utilise DOTween pour l'animation (montée + fade).
// ============================================================================
using System;
using KINETICS5.Core;
using KINETICS5.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Spawner des nombres de dégâts flottants. Singleton par scène.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tous les chiffres passent par <see cref="ObjectPooler"/> (pool key
    /// <c>"DamageNumber"</c>). Aucun Instantiate dans la hot path.
    /// </para>
    /// <para>
    /// <b>Cap mobile :</b> 30 chiffres simultanés maximum. Au-delà, le plus
    /// ancien est recyclé.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FloatingDamage : MonoBehaviour
    {
        /// <summary>Clé du pool ObjectPooler.</summary>
        public const string PoolKey = "DamageNumber";

        private static FloatingDamage _instance;
        /// <summary>Instance globale (auto-créée si absente).</summary>
        public static FloatingDamage Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[FloatingDamage]");
                    _instance = go.AddComponent<FloatingDamage>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Prefab")]
        [Tooltip("Prefab TextMeshPro world-space pour un nombre de dégâts.")]
        [SerializeField] private TextMeshPro _damageNumberPrefab;
        [Tooltip("Taille de police normale.")]
        [SerializeField] private float _normalFontSize = 6f;
        [Tooltip("Taille de police critique (plus grosse).")]
        [SerializeField] private float _critFontSize = 10f;
        [Tooltip("Taille de police pour 'ELIMINATED'.")]
        [SerializeField] private float _killFontSize = 9f;

        [Header("Animation")]
        [Tooltip("Hauteur de montée du chiffre (m).")]
        [SerializeField] private float _riseHeight = 2f;
        [Tooltip("Durée totale d'affichage (s).")]
        [SerializeField] private float _duration = 1f;
        [Tooltip("Offset aléatoire horizontal (m) pour éviter l'empilement.")]
        [SerializeField] private float _randomOffset = 0.3f;
        [Tooltip("Ease pour la montée.")]
        [SerializeField] private Ease _riseEase = Ease.OutQuad;

        [Header("Pool")]
        [Tooltip("Taille initiale du pool de chiffres.")]
        [SerializeField] private int _preWarm = 16;
        [Tooltip("Taille max du pool.")]
        [SerializeField] private int _maxPoolSize = 48;
        [Tooltip("Nombre maximal de chiffres simultanés (cap mobile).")]
        [SerializeField] private int _maxConcurrent = 30;

        // Suivi FIFO pour recyclage.
        private readonly System.Collections.Generic.List<TextMeshPro> _active = new(32);
        private int _fifoIndex;
        private bool _poolRegistered;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsurePool();
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
        }

        /// <summary>
        /// Affiche un nombre de dégâts à la position monde donnée.
        /// </summary>
        /// <param name="position">Position monde (point d'impact).</param>
        /// <param name="amount">Montant de dégâts affiché.</param>
        /// <param name="element">Élément (couleur).</param>
        /// <param name="isCritical">Vrai si critique (grosse police + or).</param>
        public void ShowDamage(Vector3 position, float amount, Element element, bool isCritical)
        {
            if (_damageNumberPrefab == null) return;
            EnsurePool();
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            Vector3 spawnPos = position + new Vector3(
                UnityEngine.Random.Range(-_randomOffset, _randomOffset),
                0.5f,
                UnityEngine.Random.Range(-_randomOffset, _randomOffset));

            var tmp = pooler.Get<TextMeshPro>(PoolKey, spawnPos, Quaternion.identity);
            if (tmp == null) return;

            // Couleur par élément (ou or si critique).
            Color color = isCritical
                ? new Color(0.984f, 0.749f, 0.141f, 1f) // #fbbf24 or
                : ElementalResolver.GetElementColor(element);

            tmp.text = Mathf.RoundToInt(amount).ToString();
            tmp.fontSize = isCritical ? _critFontSize : _normalFontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.alpha = 1f;

            // Animation : monte de _riseHeight en _duration, fade out.
            var tr = tmp.transform;
            Vector3 targetPos = spawnPos + Vector3.up * _riseHeight;
            DOTween.Kill(tr);
            tr.DOMove(targetPos, _duration).SetEase(_riseEase).SetUpdate(false);
            tmp.DOFade(0f, _duration * 0.6f).SetDelay(_duration * 0.4f).SetUpdate(false);

            // Planification du retour au pool.
            float expireAt = Time.time + _duration;
            var tracker = tmp.GetComponent<FloatingDamageTracker>();
            if (tracker == null)
            {
                tracker = tmp.gameObject.AddComponent<FloatingDamageTracker>();
            }
            tracker.Initialize(this, expireAt);

            _active.Add(tmp);
            EnforceCap();
        }

        /// <summary>
        /// Affiche le texte "ELIMINATED" en rouge (kill).
        /// </summary>
        /// <param name="position">Position monde du kill.</param>
        public void ShowEliminated(Vector3 position)
        {
            if (_damageNumberPrefab == null) return;
            EnsurePool();
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            Vector3 spawnPos = position + new Vector3(0f, 1.2f, 0f);
            var tmp = pooler.Get<TextMeshPro>(PoolKey, spawnPos, Quaternion.identity);
            if (tmp == null) return;

            tmp.text = "ELIMINATED";
            tmp.fontSize = _killFontSize;
            tmp.color = new Color(0.996f, 0f, 0.133f, 1f); // #FE0022 rouge
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.alpha = 1f;

            var tr = tmp.transform;
            Vector3 targetPos = spawnPos + Vector3.up * (_riseHeight * 1.2f);
            DOTween.Kill(tr);
            tr.DOMove(targetPos, _duration * 1.5f).SetEase(_riseEase).SetUpdate(false);
            tmp.DOFade(0f, _duration * 0.8f).SetDelay(_duration * 0.7f).SetUpdate(false);

            float expireAt = Time.time + _duration * 1.5f;
            var tracker = tmp.GetComponent<FloatingDamageTracker>();
            if (tracker == null) tracker = tmp.gameObject.AddComponent<FloatingDamageTracker>();
            tracker.Initialize(this, expireAt);

            _active.Add(tmp);
            EnforceCap();
        }

        /// <summary>
        /// Billboarding : tous les chiffres actifs font face à la caméra.
        /// Appelée par FloatingDamageTracker.Update.
        /// </summary>
        internal void BillboardActive(Camera cam)
        {
            if (cam == null) return;
            for (int i = 0; i < _active.Count; i++)
            {
                var tmp = _active[i];
                if (tmp == null) continue;
                tmp.transform.rotation = Quaternion.LookRotation(tmp.transform.position - cam.transform.position);
            }
        }

        /// <summary>
        /// Retourne un nombre au pool (appelé par FloatingDamageTracker à expiration).
        /// </summary>
        internal void Release(TextMeshPro tmp)
        {
            if (tmp == null) return;
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;
            DOTween.Kill(tmp.transform);
            DOTween.Kill(tmp);
            _active.Remove(tmp);
            pooler.Release(tmp);
        }

        /// <summary>
        /// Force le recyclage du plus ancien si cap dépassé.
        /// </summary>
        private void EnforceCap()
        {
            while (_active.Count > _maxConcurrent && _active.Count > 0)
            {
                int idx = _fifoIndex % _active.Count;
                var oldest = _active[idx];
                _active.RemoveAt(idx);
                _fifoIndex++;
                if (oldest != null)
                {
                    DOTween.Kill(oldest.transform);
                    DOTween.Kill(oldest);
                    ObjectPooler.Instance?.Release(oldest);
                }
            }
        }

        private void EnsurePool()
        {
            if (_poolRegistered || _damageNumberPrefab == null) return;
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;
            pooler.RegisterPool(PoolKey, _damageNumberPrefab, _preWarm, _maxPoolSize);
            _poolRegistered = true;
        }

        /// <summary>Nombre de chiffres actuellement affichés (debug).</summary>
        public int ActiveCount => _active.Count;
    }

    /// <summary>
    /// Composant attaché à chaque chiffre flottant pour gérer l'expiration
    /// et le billboarding caméra. Évite le couplage dur entre la caméra et
    /// le spawner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingDamageTracker : MonoBehaviour
    {
        private FloatingDamage _owner;
        private TextMeshPro _tmp;
        private float _expireAt;

        private void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
        }

        /// <summary>
        /// Initialise ce tracker avec l'owner et le temps d'expiration.
        /// </summary>
        public void Initialize(FloatingDamage owner, float expireAt)
        {
            _owner = owner;
            _expireAt = expireAt;
        }

        private void Update()
        {
            if (_owner == null) return;

            // Expiration.
            if (Time.time >= _expireAt)
            {
                _owner.Release(_tmp);
                return;
            }

            // Billboarding caméra.
            Camera cam = Camera.main;
            if (cam != null && _tmp != null)
            {
                _tmp.transform.rotation = Quaternion.LookRotation(_tmp.transform.position - cam.transform.position);
            }
        }
    }
}
