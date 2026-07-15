using System;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Combat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KINETICS5.Gameplay.Enemies
{
    /// <summary>
    /// Système de drop de loot. Singleton léger qui, à la mort d'un ennemi, détermine les items
    /// à lâcher selon la loot table de l'ennemi (<see cref="EnemyDto.LootTable"/>), instancie des
    /// pickups poolés, et gère l'aimantation auto vers le joueur (rayon 3m).
    /// </summary>
    /// <remarks>
    /// <b>Items supportés :</b> ammo, health pack, shield cell, currency (CR), gear (mods/cores).
    /// La résolution du type d'item se fait par convention d'Id (préfixe : "ammo_", "health_",
    /// "shield_", "cr_", "mod_", "core_"). Le mapping est extensible.
    /// </remarks>
    [DisallowMultipleComponent]
    public class LootDropSystem : MonoBehaviour
    {
        private static LootDropSystem _instance;

        /// <summary>Instance globale (auto-créée si absente).</summary>
        public static LootDropSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[LootDropSystem]");
                    _instance = go.AddComponent<LootDropSystem>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>Vrai si une instance existe (pour guards externes).</summary>
        public static bool HasInstance => _instance != null;

        [Header("Pickup")]
        [Tooltip("Prefab de pickup générique (avec composant Pickup). Si null, primitive sphere.")]
        [SerializeField] private GameObject _pickupPrefab;
        [Tooltip("Id du pool de pickups.")]
        [SerializeField] private string _pickupPoolId = "Pickup";
        [Tooltip("Taille initiale du pool.")]
        [Min(1)][SerializeField] private int _poolPrewarm = 16;
        [Tooltip("Taille max du pool.")]
        [Min(1)][SerializeField] private int _poolMaxSize = 64;

        [Header("Magnet")]
        [Tooltip("Rayon d'aimantation (mètres).")]
        [SerializeField] private float _magnetRadius = 3f;
        [Tooltip("Distance de collecte automatique (mètres).")]
        [SerializeField] private float _collectRadius = 0.6f;
        [Tooltip("Vitesse d'aimantation (m/s).")]
        [SerializeField] private float _magnetSpeed = 8f;
        [Tooltip("Durée de vie d'un pickup avant despawn (secondes).")]
        [SerializeField] private float _pickupLifetime = 60f;

        [Header("Loot drop VFX")]
        [Tooltip("Vrai si un effet de particules est joué au spawn du loot.")]
        [SerializeField] private bool _spawnVfx = true;

        private readonly List<Pickup> _activePickups = new(32);
        private bool _poolRegistered;
        private float _magnetTickAccumulator;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // Tick d'aimantation à fréquence réduite (1 frame sur 2).
            _magnetTickAccumulator += Time.deltaTime;
            if (_magnetTickAccumulator < 0.05f) return;
            _magnetTickAccumulator = 0f;

            if (!PlayerContext.TryGetPlayer(out var player)) return;
            Vector3 playerPos = player.position;

            for (int i = _activePickups.Count - 1; i >= 0; i--)
            {
                var p = _activePickups[i];
                if (p == null || !p.isActiveAndEnabled)
                {
                    _activePickups.RemoveAt(i);
                    continue;
                }
                p.TickMagnet(playerPos, _magnetRadius, _collectRadius, _magnetSpeed);
            }
        }

        /// <summary>
        /// Fait apparaître les items lootés à partir de la loot table de l'ennemi.
        /// </summary>
        /// <param name="enemyData">Données de l'ennemi vaincu.</param>
        /// <param name="position">Position monde où lâcher le loot.</param>
        public void SpawnLoot(EnemyDto enemyData, Vector3 position)
        {
            if (enemyData?.LootTable == null || enemyData.LootTable.Count == 0) return;

            EnsurePoolRegistered();

            foreach (var entry in enemyData.LootTable)
            {
                // Roll de drop chance.
                float roll = UnityEngine.Random.value * 100f;
                if (roll > entry.DropChancePct) continue;

                int qty = UnityEngine.Random.Range(entry.MinQty, entry.MaxQty + 1);
                if (qty <= 0) continue;

                SpawnPickup(entry.ItemId, qty, position);
            }
        }

        /// <summary>Spawn direct d'un pickup par itemId + quantité (pour rewards de mission).</summary>
        public void SpawnPickup(string itemId, int quantity, Vector3 position)
        {
            EnsurePoolRegistered();

            // Offset aléatoire pour éviter la superposition.
            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                0.5f,
                UnityEngine.Random.Range(-1f, 1f));

            Pickup pickup;
            if (ObjectPooler.Instance != null)
            {
                pickup = ObjectPooler.Instance.Get<Pickup>(_pickupPoolId, position + offset, Quaternion.identity);
            }
            else if (_pickupPrefab != null)
            {
                var go = Instantiate(_pickupPrefab, position + offset, Quaternion.identity);
                pickup = go.GetComponent<Pickup>() ?? go.AddComponent<Pickup>();
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.position = position + offset;
                go.transform.localScale = Vector3.one * 0.3f;
                pickup = go.AddComponent<Pickup>();
            }

            if (pickup == null) return;
            pickup.Initialize(itemId, quantity, ResolveRarity(itemId), _pickupLifetime);
            _activePickups.Add(pickup);
        }

        /// <summary>Nettoie tous les pickups actifs (changement de scène).</summary>
        public void ClearAll()
        {
            for (int i = _activePickups.Count - 1; i >= 0; i--)
            {
                var p = _activePickups[i];
                if (p != null)
                {
                    if (ObjectPooler.Instance != null) ObjectPooler.Instance.Release(p);
                    else if (p.gameObject != null) Destroy(p.gameObject);
                }
            }
            _activePickups.Clear();
        }

        /// <summary>Notifier la collecte d'un pickup (appelé par <see cref="Pickup"/>).</summary>
        internal void NotifyPickupCollected(Pickup pickup)
        {
            _activePickups.Remove(pickup);
            if (ObjectPooler.Instance != null) ObjectPooler.Instance.Release(pickup);
            else if (pickup != null && pickup.gameObject != null) Destroy(pickup.gameObject);
        }

        private void EnsurePoolRegistered()
        {
            if (_poolRegistered || ObjectPooler.Instance == null) return;
            if (_pickupPrefab == null)
            {
                // Crée un prefab minimaliste runtime (sphere + Pickup component).
                var tmpGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tmpGo.name = "PickupTemplate";
                tmpGo.transform.localScale = Vector3.one * 0.3f;
                tmpGo.AddComponent<Pickup>();
                _pickupPrefab = tmpGo;
            }
            ObjectPooler.Instance.RegisterPool(_pickupPoolId, _pickupPrefab.GetComponent<Pickup>(),
                                               _poolPrewarm, _poolMaxSize);
            _poolRegistered = true;
        }

        /// <summary>Infère la rareté d'un item depuis son itemId (convention de nommage).</summary>
        private static Rarity ResolveRarity(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return Rarity.Common;
            if (itemId.Contains("legendary", StringComparison.OrdinalIgnoreCase)) return Rarity.Legendary;
            if (itemId.Contains("epic", StringComparison.OrdinalIgnoreCase)) return Rarity.Epic;
            if (itemId.Contains("rare", StringComparison.OrdinalIgnoreCase)) return Rarity.Rare;
            return Rarity.Common;
        }

        /// <summary>
        /// Catégorie d'item (pour VFX/couleur). Inférée depuis l'itemId.
        /// </summary>
        public static PickupCategory ResolveCategory(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return PickupCategory.Misc;
            if (itemId.StartsWith("ammo", StringComparison.OrdinalIgnoreCase)) return PickupCategory.Ammo;
            if (itemId.StartsWith("health", StringComparison.OrdinalIgnoreCase)) return PickupCategory.Health;
            if (itemId.StartsWith("shield", StringComparison.OrdinalIgnoreCase)) return PickupCategory.Shield;
            if (itemId.StartsWith("cr_", StringComparison.OrdinalIgnoreCase)) return PickupCategory.Currency;
            if (itemId.StartsWith("mod_", StringComparison.OrdinalIgnoreCase)) return PickupCategory.Gear;
            if (itemId.StartsWith("core_", StringComparison.OrdinalIgnoreCase)) return PickupCategory.Gear;
            return PickupCategory.Misc;
        }
    }

    /// <summary>Catégories de pickup (pour VFX/couleur/UI).</summary>
    public enum PickupCategory
    {
        Ammo,
        Health,
        Shield,
        Currency,
        Gear,
        Misc
    }

    /// <summary>
    /// Composant pickup (objet ramassable). Gère son propre cycle de vie, l'aimantation
    /// vers le joueur, et la publication de l'événement <see cref="LootPickupEvent"/> sur le bus
    /// global à la collecte.
    /// </summary>
    [DisallowMultipleComponent]
    public class Pickup : MonoBehaviour, IPooledItem
    {
        /// <summary>ItemId de ce pickup.</summary>
        public string ItemId { get; private set; }
        /// <summary>Quantité.</summary>
        public int Quantity { get; private set; }
        /// <summary>Rareté.</summary>
        public Rarity Rarity { get; private set; }

        private float _lifetimeTimer;
        private bool _isCollected;
        private Renderer _renderer;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
        }

        /// <summary>Initialise le pickup après spawn.</summary>
        public void Initialize(string itemId, int quantity, Rarity rarity, float lifetime)
        {
            ItemId = itemId;
            Quantity = quantity;
            Rarity = rarity;
            _lifetimeTimer = lifetime;
            _isCollected = false;

            // Couleur selon la catégorie (palette KINETICS 5).
            if (_renderer != null)
            {
                Color c = LootDropSystem.ResolveCategory(itemId) switch
                {
                    PickupCategory.Ammo => new Color(0.102f, 0.631f, 0.808f),    // cyan
                    PickupCategory.Health => new Color(0.42f, 0.96f, 0.17f),     // vert
                    PickupCategory.Shield => new Color(1f, 0.91f, 0.21f),        // jaune
                    PickupCategory.Currency => new Color(1f, 0.91f, 0.21f),      // jaune
                    PickupCategory.Gear => new Color(1f, 0f, 0.13f),             // rouge
                    _ => Color.white
                };
                // Tente d'appliquer la couleur via URP (shader _BaseColor) ; fallback via material.color.
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColor, c);
                _renderer.SetPropertyBlock(block);
                if (_renderer.material != null) _renderer.material.color = c;
            }
        }

        /// <summary>Tick d'aimantation (appelé par <see cref="LootDropSystem"/>).</summary>
        public void TickMagnet(Vector3 playerPos, float magnetRadius, float collectRadius, float magnetSpeed)
        {
            if (_isCollected) return;

            // Lifetime countdown.
            _lifetimeTimer -= Time.deltaTime;
            if (_lifetimeTimer <= 0f)
            {
                LootDropSystem.Instance?.NotifyPickupCollected(this);
                return;
            }

            Vector3 pos = transform.position;
            float dist = Vector3.Distance(pos, playerPos);

            // Collecte auto.
            if (dist <= collectRadius)
            {
                Collect();
                return;
            }

            // Aimantation.
            if (dist <= magnetRadius)
            {
                Vector3 dir = (playerPos - pos).normalized;
                transform.position = Vector3.MoveTowards(pos, playerPos, magnetSpeed * Time.deltaTime);
            }
        }

        private void Collect()
        {
            if (_isCollected) return;
            _isCollected = true;

            // Publication de l'événement LootPickupEvent (zero-alloc).
            if (GameEventBus.Instance != null)
            {
                uint playerId = PlayerContext.PlayerId;
                GameEventBus.Instance.Publish(new LootPickupEvent(ItemId, Quantity, playerId));
            }

            // Application de l'effet (si joueur présent).
            ApplyEffectToPlayer();

            // Retour au pool.
            LootDropSystem.Instance?.NotifyPickupCollected(this);
        }

        /// <summary>Applique l'effet du pickup au joueur (par catégorie).</summary>
        private void ApplyEffectToPlayer()
        {
            if (!PlayerContext.TryGetDamageable(out var dmg)) return;
            switch (LootDropSystem.ResolveCategory(ItemId))
            {
                case PickupCategory.Health:
                    dmg.Heal(Quantity * 25f); // 25 HP par unité
                    break;
                case PickupCategory.Shield:
                    dmg.RestoreShield(Quantity * 20f); // 20 shield par unité
                    break;
                // Ammo / Currency / Gear : appliqués par l'inventaire (hooks futurs via bus event).
                // Le LootPickupEvent publié ci-dessus permet à l'inventaire de réagir.
            }
        }

        /// <inheritdoc/>
        void IPooledItem.OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            _isCollected = false;
        }

        /// <inheritdoc/>
        void IPooledItem.OnReturnToPool()
        {
            gameObject.SetActive(false);
            _isCollected = false;
        }
    }
}
