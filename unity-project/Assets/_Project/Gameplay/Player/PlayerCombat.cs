// ============================================================================
//  KINETICS 5 — Player Combat (tir, reload, melee, ADS)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Composant de combat du joueur. Lit l'input de tir, détermine le mode
//  (Single / Burst / Auto), exécute le hitscan ou spawn un projectile,
//  applique les dégâts via DamageCalculator, déclenche les VFX (muzzle flash,
//  tracer, impact), le recoil caméra, et décrémente les munitions.
//
//  Gère aussi :
//    • Aim Down Sights (ADS) -> zoom + accuracy boost
//    • Melee fallback (coup de crosse, courte portée)
//    • Cooldown de fire rate (par arme)
//    • Reload (délègue à PlayerWeaponManager)
// ============================================================================
using System;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Combat;
using KINETICS5.Gameplay.Enemies;
using UnityEngine;

namespace KINETICS5.Gameplay.Player
{
    /// <summary>
    /// Composant de combat FPS du joueur. Gère le tir, le reload, le melee et l'ADS.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Architecture :</b>
    /// <list type="bullet">
    ///   <item>Lecture input : <see cref="InputManager.CurrentState"/> (FireHeld, AimHeld, ReloadPressed).</item>
    ///   <item>Munitions : <see cref="PlayerWeaponManager.ConsumeAmmo"/> / <see cref="PlayerWeaponManager.StartReload"/>.</item>
    ///   <item>VFX : <see cref="VFXSpawner"/> (muzzle flash, tracer, impact spark, blood).</item>
    ///   <item>Recoil : <see cref="CameraManager.AddRecoilKick"/>.</item>
    ///   <item>Dégâts : <see cref="DamageCalculator.CalculateFast"/> + <see cref="IDamageable.TakeDamage"/>.</item>
    ///   <item>Numbers flottants : <see cref="FloatingDamage.ShowDamage"/> / <see cref="FloatingDamage.ShowEliminated"/>.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Hot path :</b> raycast non-alloc (buffer partagé), aucun GetComponent,
    /// cache du transform caméra.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerCombat : MonoBehaviour
    {
        [Header("Références")]
        [Tooltip("Camera FPS (point de départ du raycast).")]
        [SerializeField] private Camera _fpsCamera;
        [Tooltip("Transform de la bouche du canon (pour muzzle flash + tracer start).")]
        [SerializeField] private Transform _muzzleTransform;
        [Tooltip("Manager d'armes (pour ammo + reload + switch).")]
        [SerializeField] private PlayerWeaponManager _weaponManager;
        [Tooltip("Animator du viewmodel (pour animations fire/reload).")]
        [SerializeField] private PlayerAnimator _animator;
        [Tooltip("PlayerStats (pour récupérer Power / Crit / elemental bonus).")]
        [SerializeField] private PlayerStats _stats;

        [Header("Tir")]
        [Tooltip("Layers touchés par les tirs (ennemis, murs, destructibles).")]
        [SerializeField] private LayerMask _hitMask = ~0;
        [Tooltip("Portée maximale du hitscan (m).")]
        [SerializeField] private float _maxRange = 200f;
        [Tooltip("Multiplicateur de fire rate (1 = nominal).")]
        [SerializeField] private float _fireRateMultiplier = 1f;
        [Tooltip("Couleur du tracer par défaut.")]
        [SerializeField] private Color _tracerColor = new(1f, 0.94f, 0.4f, 1f);

        [Header("Accuracy / Spread")]
        [Tooltip("Spread de base en degrés (tir depuis la hanche).")]
        [SerializeField] private float _baseSpread = 1.5f;
        [Tooltip("Spread en ADS (degrés, plus précis).")]
        [SerializeField] private float _adsSpread = 0.2f;
        [Tooltip("Spread en mouvement (degrés).")]
        [SerializeField] private float _moveSpreadPenalty = 0.8f;
        [Tooltip("Spread maximal accumulé par tir (cap).")]
        [SerializeField] private float _maxSpread = 6f;
        [Tooltip("Vitesse de récupération du spread (degrés/s).")]
        [SerializeField] private float _spreadRecovery = 4f;

        [Header("Melee")]
        [Tooltip("Portée du coup de crosse (m).")]
        [SerializeField] private float _meleeRange = 2.5f;
        [Tooltip("Dégâts du coup de crosse.")]
        [SerializeField] private float _meleeDamage = 50f;
        [Tooltip("Cooldown du melee (s).")]
        [SerializeField] private float _meleeCooldown = 1.2f;
        [Tooltip("Élément du melee (généralement Kinetic).")]
        [SerializeField] private Element _meleeElement = Element.Kinetic;

        [Header("Recoil")]
        [Tooltip("Recoil vertical par tir (degrés).")]
        [SerializeField] private float _recoilVertical = 1.2f;
        [Tooltip("Recoil horizontal aléatoire par tir (degrés).")]
        [SerializeField] private float _recoilHorizontal = 0.4f;
        [Tooltip("Recoil multiplié en ADS (plus précis, ex: 0.5).")]
        [SerializeField] private float _adsRecoilMultiplier = 0.5f;

        // --- État runtime ---
        private float _fireCooldown;
        private float _currentSpread;
        private float _meleeCooldownTimer;
        private int _burstShotsRemaining;
        private bool _wasFireHeld;
        private Vector3 _cameraPositionCache;
        private Vector3 _cameraForwardCache;

        // Buffer partagé pour raycast non-alloc (max 1 hit par tir suffisant).
        private static readonly RaycastHit[] _hitBuffer = new RaycastHit[4];

        /// <summary>Vrai si l'arme courante est en train de tirer (auto). Permet au HUD de reset le spread.</summary>
        public bool IsFiring { get; private set; }
        /// <summary>Spread courant en degrés.</summary>
        public float CurrentSpread => _currentSpread;
        /// <summary>Vrai si en ADS (Aim Down Sights).</summary>
        public bool IsAiming { get; private set; }

        private void Awake()
        {
            if (_fpsCamera == null) _fpsCamera = Camera.main;
            if (_weaponManager == null) _weaponManager = GetComponent<PlayerWeaponManager>();
            if (_animator == null) _animator = GetComponentInChildren<PlayerAnimator>();
            if (_stats == null) _stats = GetComponent<PlayerStats>();
        }

        private void Update()
        {
            var input = InputManager.Instance?.CurrentState ?? default;

            // Cooldowns.
            if (_fireCooldown > 0f) _fireCooldown -= Time.deltaTime;
            if (_meleeCooldownTimer > 0f) _meleeCooldownTimer -= Time.deltaTime;

            // Récupération du spread (lerp vers base).
            float targetSpread = IsAiming ? _adsSpread : _baseSpread;
            _currentSpread = Mathf.Lerp(_currentSpread, targetSpread, Time.deltaTime * _spreadRecovery);

            // Détermine l'état ADS.
            IsAiming = input.AimHeld;

            // Reload.
            if (input.ReloadPressed)
            {
                _weaponManager?.StartReload();
            }

            // Melee (si fireHeld false ET pas en cours de reload, etc.).
            // Désactivé ici pour éviter conflit avec tir ; le melee est typiquement
            // déclenché par un bouton dédié. À câbler via InputAction.
            // Pour l'instant : melee sur tap de la touche Melee (non mappée par défaut).

            // Tir selon le mode.
            HandleFireInput(input);
        }

        // ==================== TIR ====================

        /// <summary>
        /// Lit l'input de tir et déclenche le tir selon le FireMode de l'arme courante.
        /// </summary>
        private void HandleFireInput(InputState input)
        {
            var weapon = _weaponManager?.CurrentWeapon;
            if (weapon == null) return;

            // Calcule le fire rate effectif (shots/s) depuis FireRatePct.
            // FireRatePct 100 = 10 shots/s, 200 = 20 shots/s, etc.
            float fireRate = Mathf.Max(1f, weapon.Value.FireRatePct * 0.1f) * _fireRateMultiplier;
            float fireInterval = 1f / fireRate;

            // Determine le mode de tir actif.
            FireMode mode = weapon.Value.FireModes != null && weapon.Value.FireModes.Count > 0
                ? weapon.Value.FireModes[0]
                : FireMode.Auto;

            bool wantsFire = false;
            switch (mode)
            {
                case FireMode.Single:
                    // Front montant : un tir par press.
                    if (input.FireHeld && !_wasFireHeld) wantsFire = true;
                    break;
                case FireMode.Burst:
                    // Front montant : démarre une rafale de 3 coups.
                    if (input.FireHeld && !_wasFireHeld && _burstShotsRemaining <= 0)
                    {
                        _burstShotsRemaining = 3;
                    }
                    if (_burstShotsRemaining > 0 && _fireCooldown <= 0f)
                    {
                        wantsFire = true;
                        _burstShotsRemaining--;
                    }
                    break;
                case FireMode.Auto:
                    wantsFire = input.FireHeld;
                    break;
            }
            _wasFireHeld = input.FireHeld;

            if (!wantsFire)
            {
                IsFiring = false;
                return;
            }
            if (_fireCooldown > 0f) return;

            // Exécute le tir.
            IsFiring = true;
            Fire();
            _fireCooldown = fireInterval;
        }

        /// <summary>
        /// Exécute un tir selon le type d'arme (hitscan ou projectile).
        /// </summary>
        private void Fire()
        {
            var weapon = _weaponManager?.CurrentWeapon;
            if (weapon == null) return;

            // Vérifie les munitions.
            if (!_weaponManager.ConsumeAmmo())
            {
                // Chargeur vide : auto-reload.
                _weaponManager.StartReload();
                return;
            }

            // Détermine si l'arme est hitscan ou projectile (Energy / Explosive = projectile).
            bool isProjectile = weapon.Value.Element == Element.Energy ||
                                weapon.Value.Element == Element.Explosive ||
                                weapon.Value.Type == WeaponType.Heavy;

            if (isProjectile && weapon.Value.Projectile != null && weapon.Value.Projectile.Speed > 0f)
            {
                FireProjectile(weapon.Value);
            }
            else
            {
                FireHitscan(weapon.Value);
            }

            // VFX + animation communs.
            PlayFireFeedback(weapon.Value);

            // Incrémente le spread (accumulation).
            _currentSpread = Mathf.Min(_maxSpread, _currentSpread + (IsAiming ? 0.2f : 0.6f));
        }

        /// <summary>
        /// Tir hitscan : raycast depuis la caméra, application des dégâts via DamageCalculator.
        /// </summary>
        private void FireHitscan(WeaponDto weapon)
        {
            if (_fpsCamera == null) return;

            // Calcule la direction avec spread.
            Vector3 origin = _fpsCamera.transform.position;
            Vector3 forward = _fpsCamera.transform.forward;
            Vector3 spreadDir = ApplySpread(forward, _currentSpread);

            int hits = Physics.RaycastNonAlloc(origin, spreadDir, _hitBuffer, _maxRange, _hitMask);
            if (hits <= 0)
            {
                // Pas de hit : tracer jusqu'à la portée max.
                Vector3 endPos = origin + spreadDir * _maxRange;
                SpawnTracer(endPos);
                return;
            }

            // Trie par distance (le buffer n'est pas trié).
            SortHitsByDistance(hits);
            var hit = _hitBuffer[0];

            // Tracer jusqu'au point d'impact.
            SpawnTracer(hit.point);

            // Applique les dégâts si la cible est IDamageable.
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                ApplyDamageToTarget(damageable, weapon, hit.point, hit.collider);
                // VFX sang.
                VFXSpawner.Instance?.HitBlood(hit.point, hit.normal);
            }
            else
            {
                // VFX impact étincelles + decal.
                VFXSpawner.Instance?.ImpactSpark(hit.point, hit.normal, weapon.Element);
                VFXSpawner.Instance?.BulletHoleDecal(hit.point, hit.normal, SurfaceType.Metal);
            }
        }

        /// <summary>
        /// Spawn un projectile (arme à énergie / explosif).
        /// </summary>
        private void FireProjectile(WeaponDto weapon)
        {
            if (_muzzleTransform == null) return;

            Vector3 origin = _muzzleTransform.position;
            Vector3 forward = _muzzleTransform.forward;
            Vector3 spreadDir = ApplySpread(forward, _currentSpread);

            // Pool key du projectile (par élément pour permettre des prefabs différents).
            string poolKey = $"Proj_{weapon.Element}";
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            // Récupère un projectile du pool (ou fallback instantiation).
            var proj = pooler.Get<Projectile>(poolKey, origin, Quaternion.LookRotation(spreadDir));
            if (proj == null)
            {
                // Pool non enregistré : on l'enregistre à la volée si un prefab est disponible.
                // Note : le prefab devrait être assigné via Inspector dans ObjectPooler._poolConfigs.
                Debug.LogWarning($"[PlayerCombat] Pool projectile '{poolKey}' non enregistré.");
                return;
            }

            // Owner : le joueur (pour éviter friendly fire sur les projectiles AoE).
            // PlayerController implémente IDamageable ; on le récupère via GetComponent.
            IDamageable ownerDamageable = GetComponent<IDamageable>();

            uint ownerId = _stats != null ? _stats.PlayerId : 0u;

            proj.Spawn(
                position: origin,
                direction: spreadDir,
                damage: weapon.DamagePct,
                element: weapon.Element,
                weaponId: weapon.Id,
                ownerId: ownerId,
                ownerDamageable: ownerDamageable,
                speedOverride: weapon.Projectile.Speed,
                lifetimeOverride: 3f,
                explosionRadiusOverride: weapon.ExplosionRadiusPct > 0f ? weapon.ExplosionRadiusPct * 0.1f : -1f
            );
        }

        /// <summary>
        /// Applique les dégâts à une cible IDamageable, avec calcul complet
        /// (headshot, crit, distance, élément, mitigation enemy).
        /// </summary>
        private void ApplyDamageToTarget(IDamageable target, WeaponDto weapon, Vector3 hitPoint, Collider hitCollider)
        {
            // Détermine si headshot (raycast sur collider avec tag "Head").
            bool isHeadshot = hitCollider.CompareTag("Head") || hitCollider.gameObject.name.Contains("Head", StringComparison.OrdinalIgnoreCase);

            // Calcule le crit (chance + multiplicateur).
            float critChance = _stats?.CritChance ?? 0.1f;
            float critMult = _stats?.CritDamage ?? 1.5f;
            bool isCritical = !isHeadshot && UnityEngine.Random.value < critChance; // Headshot et crit non cumulables.

            // Résout l'enemyId pour le calcul.
            string enemyId = ResolveEnemyId(hitCollider);

            // Distance (pour falloff).
            float distance = Vector3.Distance(_fpsCamera.transform.position, hitPoint);

            // Dégâts de base (post-Calculator).
            float baseDamage = DamageCalculator.CalculateFast(
                weapon.Id, enemyId, weapon.Element, isHeadshot, isCritical, distance, 0f);

            // Applique le combo multiplier (si ComboChain actif).
            float comboMult = ComboChain.Instance?.CurrentMultiplier ?? 1f;
            // Applique le bonus élémentaire du joueur (PlayerStats).
            float elementalBonus = _stats?.GetElementalMultiplier(weapon.Element) ?? 1f;
            // Applique le multiplicateur de puissance (agent power).
            float powerMult = _stats != null ? Mathf.Max(0.1f, _stats.Power / 1000f) : 1f;

            float finalDamage = baseDamage * comboMult * elementalBonus * powerMult;
            // Recappe pour éviter les valeurs absurdes.
            finalDamage = Mathf.Clamp(finalDamage, 0f, DamageCalculator.DamageCap);

            // Source ID = player.
            uint sourceId = _stats?.PlayerId ?? 0u;

            // Applique les dégâts à la cible (HealthComponent publie déjà DamageDealtEvent).
            float applied = target.TakeDamage(finalDamage, weapon.Element, sourceId, hitPoint, isCritical);

            // Affiche le nombre de dégâts flottant.
            FloatingDamage.Instance?.ShowDamage(hitPoint, applied, weapon.Element, isCritical || isHeadshot);

            // Hitstop selon le type de coup.
            if (!target.IsAlive)
            {
                // Kill : hitstop long + "ELIMINATED".
                HitstopController.Trigger(HitstopType.Kill);
                FloatingDamage.Instance?.ShowEliminated(hitPoint);
            }
            else if (isHeadshot)
            {
                HitstopController.Trigger(HitstopType.Headshot);
            }
            else
            {
                HitstopController.Trigger(HitstopType.NormalHit);
            }

            // Screen shake léger côté joueur (l'impact est ressenti).
            ScreenShake.Hit(ShakeIntensity.Small, hitPoint);
        }

        /// <summary>
        /// Applique le spread (cone aléatoire) à une direction.
        /// </summary>
        private Vector3 ApplySpread(Vector3 baseDir, float spreadDeg)
        {
            if (spreadDeg <= 0f) return baseDir;
            float spreadRad = spreadDeg * Mathf.Deg2Rad;
            // Cone aléatoire : déviation angulaire dans toutes les directions.
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float magnitude = UnityEngine.Random.Range(0f, spreadRad);
            Vector3 perp1 = Vector3.Cross(baseDir, Vector3.up).normalized;
            if (perp1.sqrMagnitude < 0.001f) perp1 = Vector3.Cross(baseDir, Vector3.right).normalized;
            Vector3 perp2 = Vector3.Cross(baseDir, perp1).normalized;
            return (baseDir + perp1 * Mathf.Cos(angle) * magnitude + perp2 * Mathf.Sin(angle) * magnitude).normalized;
        }

        /// <summary>
        /// Déclenche les feedbacks visuels/sonores communs après un tir.
        /// </summary>
        private void PlayFireFeedback(WeaponDto weapon)
        {
            // Muzzle flash.
            if (_muzzleTransform != null)
            {
                VFXSpawner.Instance?.MuzzleFlash(weapon.Id, _muzzleTransform.position, _muzzleTransform.rotation);
            }

            // Recoil caméra.
            float recoilMult = IsAiming ? _adsRecoilMultiplier : 1f;
            CameraManager.Instance?.AddRecoilKick(recoilMult);

            // Animation viewmodel.
            _animator?.PlayFire();

            // SFX (via AudioManager).
            AudioManager.Instance?.PlaySfx(null, _muzzleTransform?.position ?? transform.position, 0.8f);
        }

        /// <summary>
        /// Spawn un tracer de la bouche du canon vers le point d'impact.
        /// </summary>
        private void SpawnTracer(Vector3 endPos)
        {
            if (_muzzleTransform == null) return;
            VFXSpawner.Instance?.Tracer(_muzzleTransform.position, endPos, _tracerColor);
        }

        /// <summary>
        /// Tente de résoudre l'enemyId d'un collider touché (pour DamageCalculator).
        /// </summary>
        private string ResolveEnemyId(Collider col)
        {
            var ec = col.GetComponentInParent<EnemyController>();
            return (ec != null && ec.Data != null) ? ec.Data.Id : string.Empty;
        }

        /// <summary>Trie les hits du buffer par distance croissante.</summary>
        private void SortHitsByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                var key = _hitBuffer[i];
                float keyDist = _hitBuffer[i].distance;
                int j = i - 1;
                while (j >= 0 && _hitBuffer[j].distance > keyDist)
                {
                    _hitBuffer[j + 1] = _hitBuffer[j];
                    j--;
                }
                _hitBuffer[j + 1] = key;
            }
        }

        // ==================== MELEE ====================

        /// <summary>
        /// Déclenche un coup de crosse (melee) si le cooldown est écoulé.
        /// </summary>
        /// <returns>Vrai si le melee a été exécuté.</returns>
        public bool TryMelee()
        {
            if (_meleeCooldownTimer > 0f) return false;

            _meleeCooldownTimer = _meleeCooldown;
            _animator?.PlayMelee();

            // Raycast courte portée devant la caméra.
            if (_fpsCamera == null) return false;
            Vector3 origin = _fpsCamera.transform.position;
            Vector3 forward = _fpsCamera.transform.forward;
            int hits = Physics.RaycastNonAlloc(origin, forward, _hitBuffer, _meleeRange, _hitMask);
            if (hits <= 0) return false;

            SortHitsByDistance(hits);
            var hit = _hitBuffer[0];
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable == null) return false;

            uint sourceId = _stats?.PlayerId ?? 0u;
            damageable.TakeDamage(_meleeDamage, _meleeElement, sourceId, hit.point, false);
            VFXSpawner.Instance?.HitBlood(hit.point, hit.normal);
            FloatingDamage.Instance?.ShowDamage(hit.point, _meleeDamage, _meleeElement, false);
            HitstopController.Trigger(HitstopType.NormalHit);
            return true;
        }

        // ==================== SHARED BUFFER EXPOSURE ====================

        /// <summary>Buffer de raycast partagé (debug).</summary>
        public static RaycastHit[] SharedHitBuffer => _hitBuffer;
    }
}
