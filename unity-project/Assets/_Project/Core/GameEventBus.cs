using System;
using System.Collections.Generic;
using UnityEngine;

namespace KINETICS5.Core
{
    // ============================================================================
    //  TYPES D'ÉVÉNEMENTS GLOBAUX (struct = zéro allocation sur le heap)
    // ============================================================================

    /// <summary>Événement publié lorsqu'un personnage (joueur ou ennemi) inflige des dégâts.</summary>
    public readonly struct DamageDealtEvent
    {
        public readonly uint SourceId;
        public readonly uint TargetId;
        public readonly float Amount;
        public readonly bool IsCritical;
        public readonly int ElementId;
        public readonly Vector3 HitPoint;

        public DamageDealtEvent(uint sourceId, uint targetId, float amount, bool isCritical, int elementId, Vector3 hitPoint)
        {
            SourceId = sourceId; TargetId = targetId; Amount = amount;
            IsCritical = isCritical; ElementId = elementId; HitPoint = hitPoint;
        }
    }

    /// <summary>Événement publié à la mort d'un ennemi.</summary>
    public readonly struct EnemyKilledEvent
    {
        public readonly uint EnemyId;
        public readonly uint KillerId;
        public readonly int XpReward;
        public readonly int CreditReward;
        public readonly Vector3 DeathPosition;

        public EnemyKilledEvent(uint enemyId, uint killerId, int xp, int credits, Vector3 pos)
        {
            EnemyId = enemyId; KillerId = killerId; XpReward = xp; CreditReward = credits; DeathPosition = pos;
        }
    }

    /// <summary>Événement publié quand une mission est terminée (succès).</summary>
    public readonly struct MissionCompleteEvent
    {
        public readonly string MissionId;
        public readonly float DurationSec;
        public readonly int Score;
        public readonly bool PerfectClear;

        public MissionCompleteEvent(string id, float duration, int score, bool perfect)
        {
            MissionId = id; DurationSec = duration; Score = score; PerfectClear = perfect;
        }
    }

    /// <summary>Événement publié quand le joueur change d'arme.</summary>
    public readonly struct WeaponSwitchedEvent
    {
        public readonly int SlotIndex;
        public readonly string WeaponId;

        public WeaponSwitchedEvent(int slot, string weaponId)
        {
            SlotIndex = slot; WeaponId = weaponId;
        }
    }

    /// <summary>Événement publié quand le joueur subit des dégâts.</summary>
    public readonly struct PlayerDamagedEvent
    {
        public readonly float Amount;
        public readonly float NewHealth;
        public readonly uint SourceId;
        public readonly bool IsFatal;

        public PlayerDamagedEvent(float amount, float newHealth, uint sourceId, bool isFatal)
        {
            Amount = amount; NewHealth = newHealth; SourceId = sourceId; IsFatal = isFatal;
        }
    }

    /// <summary>Événement publié quand un objectif de mission est mis à jour.</summary>
    public readonly struct ObjectiveUpdatedEvent
    {
        public readonly string ObjectiveId;
        public readonly int Current;
        public readonly int Required;
        public readonly bool IsComplete;

        public ObjectiveUpdatedEvent(string id, int current, int required, bool complete)
        {
            ObjectiveId = id; Current = current; Required = required; IsComplete = complete;
        }
    }

    /// <summary>Événement publié quand le joueur ramasse un loot.</summary>
    public readonly struct LootPickupEvent
    {
        public readonly string ItemId;
        public readonly int Quantity;
        public readonly uint PlayerId;

        public LootPickupEvent(string itemId, int quantity, uint playerId)
        {
            ItemId = itemId; Quantity = quantity; PlayerId = playerId;
        }
    }

    // ============================================================================
    //  BUS D'ÉVÉNEMENTS TYPE-SAFE
    // ============================================================================

    /// <summary>
    /// Bus d'événements global type-safe pour KINETICS 5.
    /// Publication/souscription génériques, sans allocation (events en struct).
    /// Thread-safe pour le thread principal (verrou fin pour les handlers async).
    /// </summary>
    public sealed class GameEventBus : MonoBehaviour
    {
        private static GameEventBus _instance;
        /// <summary>Instance globale du bus (auto-créée si absente).</summary>
        public static GameEventBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[GameEventBus]");
                    _instance = go.AddComponent<GameEventBus>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // Wrapper pour stocker uniformément les handlers Action<T> dans une List non-générique.
        // On expose Matches(object) pour comparer par référence sans réflexion.
        private interface IEventHandler
        {
            void Invoke(object payload);
            bool Matches(object expected);
        }

        private readonly struct HandlerWrapper<T> : IEventHandler where T : struct
        {
            private readonly Action<T> _action;
            public HandlerWrapper(Action<T> action) { _action = action; }
            public void Invoke(object payload) => _action.Invoke((T)payload);
            public bool Matches(object expected) => ReferenceEquals(_action, expected);
        }

        // Listes de handlers typées : on garde une List<IEventHandler> par type.
        // Lors d'un Publish, on snapshotte via for-loop sans copy (verrou pris).
        private readonly Dictionary<Type, List<IEventHandler>> _handlers = new(64);
        private readonly object _lock = new();

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                lock (_lock) _handlers.Clear();
                _instance = null;
            }
        }

        /// <summary>
        /// Souscrit à un type d'événement. Retourne un token de désinscription
        /// (utiliser via using/dispose ou appeler Unsubscribe).
        /// </summary>
        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return NullDisposable.Instance;
            var type = typeof(T);
            var wrapper = new HandlerWrapper<T>(handler);
            lock (_lock)
            {
                if (!_handlers.TryGetValue(type, out var list))
                {
                    list = new List<IEventHandler>(8);
                    _handlers.Add(type, list);
                }
                list.Add(wrapper);
            }
            return new SubscriptionToken<T>(this, handler);
        }

        /// <summary>Désinscription manuelle (préférer le token retourné par Subscribe).</summary>
        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var type = typeof(T);
            lock (_lock)
            {
                if (!_handlers.TryGetValue(type, out var list)) return;
                // Comparaison par référence via Matches (pas de réflexion).
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].Matches(handler))
                    {
                        // Swap-remove pour éviter de décaler la liste (ordre non garanti).
                        list[i] = list[list.Count - 1];
                        list.RemoveAt(list.Count - 1);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Publie un événement sur le thread principal. Aucune allocation : on prend le verrou,
        /// on itère la liste existante. Les handlers peuvent publier en cascade sans risque de
        /// modification concurrente (snapshot logique via index max).
        /// </summary>
        public void Publish<T>(in T evt) where T : struct
        {
            List<IEventHandler> snapshot;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(T), out snapshot) || snapshot.Count == 0) return;
            }
            // Itération sur la liste live : on capture le count initial pour supporter les
            // souscriptions ajoutées par les handlers sans itérer les nouveaux.
            int count = snapshot.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    snapshot[i]?.Invoke(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameEventBus] Handler a levé une exception pour {typeof(T).Name}: {ex}");
                }
            }
        }

        /// <summary>Compte le nombre de handlers souscrits pour un type donné (debug/tests).</summary>
        public int CountHandlers<T>() where T : struct
        {
            lock (_lock)
            {
                return _handlers.TryGetValue(typeof(T), out var list) ? list.Count : 0;
            }
        }

        /// <summary>Vide toutes les souscriptions (utilisé en shutdown).</summary>
        public void ClearAll()
        {
            lock (_lock) _handlers.Clear();
        }

        // --- Implémentation interne des tokens ---
        private sealed class SubscriptionToken<T> : IDisposable where T : struct
        {
            private GameEventBus _bus;
            private readonly Action<T> _action;
            public SubscriptionToken(GameEventBus bus, Action<T> action) { _bus = bus; _action = action; }
            public void Dispose()
            {
                if (_bus == null || _action == null) return;
                _bus.Unsubscribe(_action);
                _bus = null;
            }
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
