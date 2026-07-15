// ============================================================================
//  KINETICS 5 — Player Inventory (gameplay-side, pont avec SaveSystem)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Composant gameplay du joueur qui gère l'inventaire runtime :
//    • Armes possédées (Primary, Secondary, Tactical)
//    • Tactiques (grenades, gadgets) avec stacks
//    • Consommables (kits de soin, shield boosters)
//    • Loot / matériaux (ressources)
//    • Équipement/retrait avec validation de classe d'agent
//    • Pickup auto depuis LootPickupEvent
//    • Sync avec SaveSystem (Core.PlayerInventory save-data)
//
//  Ce composant est le gameplay-side bridge : il ne persiste rien lui-même,
//  il délègue la persistance à <see cref="SaveSystem"/>.
// ============================================================================
using System;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Player
{
    /// <summary>
    /// Catégorie d'objet dans l'inventaire gameplay.
    /// </summary>
    public enum InventoryItemCategory
    {
        /// <summary>Arme primaire / secondaire / tactique.</summary>
        Weapon,
        /// <summary>Gadget tactique (grenade, trap).</summary>
        Tactical,
        /// <summary>Consommable (kit de soin, booster).</summary>
        Consumable,
        /// <summary>Matériau / ressource craft.</summary>
        Material,
        /// <summary>Loot divers (gear, mods).</summary>
        Loot
    }

    /// <summary>
    /// Entrée d'inventaire runtime. Représente un stack d'items identiques.
    /// </summary>
    [Serializable]
    public sealed class InventoryEntry
    {
        /// <summary>Id de l'item (matche DataLoader : weaponId, tacticalId, ou itemId custom).</summary>
        public string ItemId = string.Empty;
        /// <summary>Catégorie gameplay.</summary>
        public InventoryItemCategory Category;
        /// <summary>Quantité possédée.</summary>
        public int Quantity = 1;
        /// <summary>Vrai si l'item est équipé (slot correspondant occupé).</summary>
        public bool IsEquipped;
    }

    /// <summary>
    /// Composant d'inventaire gameplay du joueur. Bridge avec le SaveSystem
    /// (Core.PlayerInventory save-data) pour la persistance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Architecture :</b>
    /// <list type="bullet">
    ///   <item>Runtime : <see cref="_entries"/> (Dictionary&lt;string, InventoryEntry&gt;) pour accès O(1).</item>
    ///   <item>Persistance : <see cref="SyncFromSave"/> / <see cref="SyncToSave"/> vers SaveSystem.ActiveData.Inventory.</item>
    ///   <item>Auto-pickup : souscrit à <see cref="LootPickupEvent"/> via <see cref="GameEventBus"/>.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Id de l'agent (pour validation de classe d'équipement).")]
        [SerializeField] private string _agentId = "VULCAN";
        [Tooltip("Taille max d'inventaire (cap mobile).")]
        [SerializeField] private int _maxSlots = 60;
        [Tooltip("Stack max par entrée de consommable/matériau.")]
        [SerializeField] private int _maxStack = 99;
        [Tooltip("Poids max transportable (0 = encumbrance désactivé).")]
        [SerializeField] private float _maxWeight = 0f;
        [Tooltip("Poids par arme (unité arbitraire).")]
        [SerializeField] private float _weaponWeight = 5f;
        [Tooltip("Poids par consommable/matériau (unité arbitraire).")]
        [SerializeField] private float _itemWeight = 0.1f;

        /// <summary>Entrées d'inventaire indexées par ItemId.</summary>
        private readonly Dictionary<string, InventoryEntry> _entries = new(64);

        /// <summary>Slots d'équipement : 0=Primary, 1=Secondary, 2=Tactical.</summary>
        private readonly string[] _equippedSlots = new string[3] { string.Empty, string.Empty, string.Empty };

        /// <summary>Identifiant du joueur local (pour LootPickupEvent).</summary>
        public uint PlayerId { get; private set; }

        private IDisposable _subLootPickup;

        /// <summary>Événement déclenché à chaque modification de l'inventaire (UI refresh).</summary>
        public event Action OnInventoryChanged;

        private void Awake()
        {
            PlayerId = (uint)GetInstanceID();
        }

        private void OnEnable()
        {
            GameEventBus.Instance?.Subscribe<LootPickupEvent>(OnLootPickup);
        }

        private void OnDisable()
        {
            GameEventBus.Instance?.Unsubscribe<LootPickupEvent>(OnLootPickup);
        }

        // ==================== API PUBLIQUE ====================

        /// <summary>
        /// Ajoute un item à l'inventaire.
        /// </summary>
        /// <param name="itemId">Id de l'item.</param>
        /// <param name="quantity">Quantité (default 1).</param>
        /// <param name="category">Catégorie (auto-détectée si Unknown).</param>
        /// <returns>Vrai si l'ajout a réussi (place disponible).</returns>
        public bool AddItem(string itemId, int quantity = 1, InventoryItemCategory category = default)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return false;

            // Auto-detect category si non spécifiée.
            if (category == default && !_entries.ContainsKey(itemId))
            {
                category = DetectCategory(itemId);
            }

            if (_entries.TryGetValue(itemId, out var existing))
            {
                existing.Quantity = Mathf.Min(_maxStack, existing.Quantity + quantity);
            }
            else
            {
                if (_entries.Count >= _maxSlots)
                {
                    Debug.LogWarning($"[PlayerInventory] Inventaire plein ({_maxSlots} slots).");
                    return false;
                }
                _entries[itemId] = new InventoryEntry
                {
                    ItemId = itemId,
                    Category = category,
                    Quantity = Mathf.Min(_maxStack, quantity),
                    IsEquipped = false
                };
            }

            MarkSaveDirty();
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Retire une quantité d'un item. Retourne la quantité réellement retirée.
        /// </summary>
        public int RemoveItem(string itemId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return 0;
            if (!_entries.TryGetValue(itemId, out var entry)) return 0;

            int removed = Mathf.Min(quantity, entry.Quantity);
            entry.Quantity -= removed;
            if (entry.Quantity <= 0)
            {
                // Si l'item était équipé, on libère le slot.
                if (entry.IsEquipped)
                {
                    UnequipItem(itemId);
                }
                _entries.Remove(itemId);
            }

            MarkSaveDirty();
            OnInventoryChanged?.Invoke();
            return removed;
        }

        /// <summary>
        /// Vrai si l'inventaire contient au moins la quantité demandée de l'item.
        /// </summary>
        public bool HasItem(string itemId, int quantity = 1)
        {
            return _entries.TryGetValue(itemId, out var entry) && entry.Quantity >= quantity;
        }

        /// <summary>
        /// Retourne la quantité possédée d'un item (0 si absent).
        /// </summary>
        public int GetQuantity(string itemId)
        {
            return _entries.TryGetValue(itemId, out var entry) ? entry.Quantity : 0;
        }

        /// <summary>
        /// Équipe un item dans le slot correspondant à sa catégorie.
        /// </summary>
        /// <param name="itemId">Id de l'item à équiper.</param>
        /// <returns>Vrai si l'équipement a réussi.</returns>
        public bool EquipItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            if (!_entries.TryGetValue(itemId, out var entry)) return false;

            // Validation de classe d'agent (ex: VULCAN Tank ne peut pas équiper SR CX-27).
            if (!ValidateClassRequirement(itemId))
            {
                Debug.LogWarning($"[PlayerInventory] {itemId} non équipable par {_agentId} (classe incompatible).");
                return false;
            }

            int slotIndex = GetSlotIndexForCategory(entry.Category);
            if (slotIndex < 0) return false;

            // Déséquipe l'item actuellement dans le slot.
            string previousEquipped = _equippedSlots[slotIndex];
            if (!string.IsNullOrEmpty(previousEquipped) && _entries.TryGetValue(previousEquipped, out var prevEntry))
            {
                prevEntry.IsEquipped = false;
            }

            // Équipe le nouvel item.
            _equippedSlots[slotIndex] = itemId;
            entry.IsEquipped = true;

            MarkSaveDirty();
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Déséquipe un item (libère son slot).
        /// </summary>
        public bool UnequipItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            if (!_entries.TryGetValue(itemId, out var entry)) return false;
            if (!entry.IsEquipped) return false;

            int slotIndex = GetSlotIndexForCategory(entry.Category);
            if (slotIndex >= 0 && _equippedSlots[slotIndex] == itemId)
            {
                _equippedSlots[slotIndex] = string.Empty;
            }
            entry.IsEquipped = false;

            MarkSaveDirty();
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Retourne l'ItemId équipé dans un slot (0=Primary, 1=Secondary, 2=Tactical).
        /// </summary>
        public string GetEquippedInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _equippedSlots.Length) return string.Empty;
            return _equippedSlots[slotIndex];
        }

        /// <summary>
        /// Retourne un snapshot de toutes les entrées d'inventaire (pour l'UI).
        /// </summary>
        public IReadOnlyCollection<InventoryEntry> GetAllEntries()
        {
            return _entries.Values;
        }

        /// <summary>
        /// Poids total transporté (encumbrance). 0 si désactivé.
        /// </summary>
        public float CurrentWeight
        {
            get
            {
                if (_maxWeight <= 0f) return 0f;
                float w = 0f;
                foreach (var kvp in _entries)
                {
                    float perItem = kvp.Value.Category == InventoryItemCategory.Weapon ? _weaponWeight : _itemWeight;
                    w += perItem * kvp.Value.Quantity;
                }
                return w;
            }
        }

        // ==================== SYNC SAVE ====================

        /// <summary>
        /// Charge l'inventaire depuis le SaveSystem (slot actif).
        /// </summary>
        public void SyncFromSave()
        {
            _entries.Clear();
            for (int i = 0; i < _equippedSlots.Length; i++) _equippedSlots[i] = string.Empty;

            var save = SaveSystem.Instance?.ActiveData;
            if (save == null) return;

            var saveInv = save.Inventory;
            // Armes possédées.
            if (saveInv.OwnedWeapons != null)
            {
                foreach (var wid in saveInv.OwnedWeapons)
                {
                    if (string.IsNullOrEmpty(wid)) continue;
                    _entries[wid] = new InventoryEntry
                    {
                        ItemId = wid,
                        Category = InventoryItemCategory.Weapon,
                        Quantity = 1,
                        IsEquipped = false
                    };
                }
            }
            // Armes équipées.
            if (saveInv.Equipped != null)
            {
                for (int i = 0; i < saveInv.Equipped.Count && i < 3; i++)
                {
                    var eq = saveInv.Equipped[i];
                    if (string.IsNullOrEmpty(eq)) continue;
                    _equippedSlots[i] = eq;
                    if (_entries.TryGetValue(eq, out var e)) e.IsEquipped = true;
                }
            }
            // Ressources (matériaux).
            if (saveInv.Resources != null)
            {
                foreach (var kvp in saveInv.Resources)
                {
                    if (string.IsNullOrEmpty(kvp.Key)) continue;
                    _entries[kvp.Key] = new InventoryEntry
                    {
                        ItemId = kvp.Key,
                        Category = InventoryItemCategory.Material,
                        Quantity = kvp.Value,
                        IsEquipped = false
                    };
                }
            }

            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Pousse l'inventaire courant vers le SaveSystem (mark dirty).
        /// </summary>
        public void SyncToSave()
        {
            var save = SaveSystem.Instance?.ActiveData;
            if (save == null) return;

            var saveInv = save.Inventory;
            saveInv.OwnedWeapons.Clear();
            saveInv.Equipped = new List<string> { string.Empty, string.Empty, string.Empty };
            saveInv.Resources.Clear();

            foreach (var kvp in _entries)
            {
                if (kvp.Value.Category == InventoryItemCategory.Weapon)
                {
                    saveInv.OwnedWeapons.Add(kvp.Key);
                }
                else if (kvp.Value.Category == InventoryItemCategory.Material)
                {
                    saveInv.Resources[kvp.Key] = kvp.Value.Quantity;
                }
            }
            for (int i = 0; i < 3; i++)
            {
                saveInv.Equipped[i] = _equippedSlots[i] ?? string.Empty;
            }

            SaveSystem.Instance?.MarkDirty();
        }

        // ==================== HELPERS ====================

        /// <summary>
        /// Détecte la catégorie d'un item depuis son Id (via DataLoader).
        /// </summary>
        private InventoryItemCategory DetectCategory(string itemId)
        {
            var weapon = DataLoader.GetWeapon(itemId);
            if (weapon != null)
            {
                return weapon.Value.Category == WeaponCategory.Tactical
                    ? InventoryItemCategory.Tactical
                    : InventoryItemCategory.Weapon;
            }
            // Heuristique : si l'Id contient "ammo", "scrap", "circuit" -> Material.
            if (itemId.Contains("ammo", StringComparison.OrdinalIgnoreCase) ||
                itemId.Contains("scrap", StringComparison.OrdinalIgnoreCase) ||
                itemId.Contains("circuit", StringComparison.OrdinalIgnoreCase))
            {
                return InventoryItemCategory.Material;
            }
            if (itemId.Contains("med", StringComparison.OrdinalIgnoreCase) ||
                itemId.Contains("kit", StringComparison.OrdinalIgnoreCase))
            {
                return InventoryItemCategory.Consumable;
            }
            return InventoryItemCategory.Loot;
        }

        /// <summary>
        /// Valide qu'un item est équipable par la classe d'agent courante.
        /// </summary>
        private bool ValidateClassRequirement(string itemId)
        {
            var weapon = DataLoader.GetWeapon(itemId);
            if (weapon == null) return true; // Pas une arme -> pas de restriction.

            // Règles simples :
            // - Tank (VULCAN) ne peut pas équiper les snipers (SR CX-27).
            // - Recon (XANO) ne peut pas équiper les heavy (HEAVY RX-14).
            var agent = DataLoader.GetAgent(_agentId);
            if (agent == null) return true;

            return (agent.Value.Class, weapon.Value.Type) switch
            {
                (AgentClass.Tank, WeaponType.Sniper) => false,
                (AgentClass.Recon, WeaponType.Heavy) => false,
                _ => true
            };
        }

        /// <summary>
        /// Retourne l'index de slot (0=Primary, 1=Secondary, 2=Tactical) pour une catégorie.
        /// </summary>
        private int GetSlotIndexForCategory(InventoryItemCategory category)
        {
            // Pour les armes, on résout via DataLoader pour connaître la catégorie exacte.
            return category switch
            {
                InventoryItemCategory.Weapon   => 0, // Primary par défaut ; ajusté plus bas si besoin.
                InventoryItemCategory.Tactical => 2,
                _                               => -1 // Consommables/matériaux non équipables en slot.
            };
        }

        private void MarkSaveDirty()
        {
            SaveSystem.Instance?.MarkDirty();
        }

        // ==================== HANDLERS ÉVÉNEMENTS ====================

        private void OnLootPickup(in LootPickupEvent evt)
        {
            // Filtre : ne collecte que les loots destinés au joueur local.
            if (evt.PlayerId != 0u && evt.PlayerId != PlayerId) return;

            AddItem(evt.ItemId, evt.Quantity);
        }
    }
}
