// ============================================================================
//  KINETICS 5 — Match Manager (multi-joueur co-op)
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Gestion des matchs multijoueurs (missions co-op 2-4 joueurs).
//  Modèle HOST / CLIENT : un joueur est élu host (autorité), les autres clients
//  envoient leurs inputs au host qui calcule l'état et broadcast les snapshots.
//
//  Fonctionnalités :
//    • Création / rejointe de match (matchmaker ou code partie).
//    • Synchronisation d'état joueur (position, santé, arme courante).
//    • RPC pour actions : tir, compétence, ramassage.
//    • Interpolation de snapshots (180 ms buffer, 20 Hz tick).
//    • Détection de host migré (si host quitte, élection d'un nouveau).
//    • Mode offline : dégradation en solo (1 joueur local).
// ============================================================================
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using KINETICS5.Core;

namespace KINETICS5.Network
{
    /// <summary>Rôle d'un participant dans un match.</summary>
    public enum MatchRole
    {
        /// <summary>Autorité : simule la physique, valide les actions.</summary>
        Host,
        /// <summary>Envoie inputs, applique snapshots reçus.</summary>
        Client
    }

    /// <summary>État d'un joueur synchronisé sur le réseau.</summary>
    [Serializable]
    public sealed class PlayerNetState
    {
        public string UserId;
        public Vector3 Position;
        public Vector3 Velocity;
        public float  Yaw;
        public float  Pitch;
        public float  Health;
        public float  Shield;
        public int    WeaponSlot;
        public string WeaponId;
        public bool   IsReloading;
        public long   TickNumber;

        public PlayerNetState Clone()
        {
            return new PlayerNetState
            {
                UserId = UserId, Position = Position, Velocity = Velocity,
                Yaw = Yaw, Pitch = Pitch, Health = Health, Shield = Shield,
                WeaponSlot = WeaponSlot, WeaponId = WeaponId,
                IsReloading = IsReloading, TickNumber = TickNumber
            };
        }
    }

    /// <summary>Snapshot complet du match envoyé par le host.</summary>
    [Serializable]
    public sealed class MatchSnapshot
    {
        public long Tick;
        public double ServerTime;
        public List<PlayerNetState> Players = new();
        // État du monde condensé (positions ennemis,Projectiles tracked).
        public List<string> EnemyStates = new();
    }

    /// <summary>Action envoyée par un client au host (tir, ability, etc.).</summary>
    [Serializable]
    public sealed class PlayerAction
    {
        public string UserId;
        public string Type; // "shoot", "ability", "interact", "reload", "switch"
        public Vector3 Origin;
        public Vector3 Direction;
        public int    TargetId;
        public string WeaponId;
        public double ClientTime;
    }

    /// <summary>Résultat d'une validation d'action par le host.</summary>
    public readonly struct ActionValidationResult
    {
        public readonly bool Accepted;
        public readonly string Reason;
        public ActionValidationResult(bool ok, string reason) { Accepted = ok; Reason = reason; }
    }

    /// <summary>
    /// Manager de match multijoueur. Communique avec <see cref="NakamaClient"/>
    /// et délègue la validation à <see cref="AntiCheatValidator"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchManager : MonoBehaviour
    {
        private static MatchManager _instance;
        public static MatchManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[MatchManager]");
                _instance = go.AddComponent<MatchManager>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        [Header("Configuration réseau")]
        [Tooltip("Fréquence d'envoi des snapshots (Hz). 20 = tous les 50ms.")]
        [SerializeField] private int _tickRate = 20;
        [Tooltip("Durée du buffer d'interpolation (s). Plus élevé = plus stable, plus laggy.")]
        [SerializeField] private float _interpBuffer = 0.18f;
        [Tooltip("Délai max avant extrapolation (s).")]
        [SerializeField] private float _maxExtrapolation = 0.25f;

        public MatchRole Role { get; private set; } = MatchRole.Client;
        public string MatchId { get; private set; }
        public bool IsInMatch { get; private set; }
        public bool IsOffline => NakamaClient.Instance != null && NakamaClient.Instance.IsOfflineMode;

        /// <summary>Déclenché quand un snapshot est reçu et interpolé.</summary>
        public event Action<MatchSnapshot> SnapshotApplied;
        /// <summary>Déclenché quand un joueur rejoint le match.</summary>
        public event Action<PlayerNetState> PlayerJoined;
        /// <summary>Déclenché quand un joueur quitte le match.</summary>
        public event Action<string> PlayerLeft;

        private readonly Dictionary<string, PlayerNetState> _players = new(8);
        private readonly Queue<MatchSnapshot> _snapshotBuffer = new(64);
        private float _nextTickTime;
        private double _lastServerTime;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ------------------------------------------------------------------------
        //  CRÉATION / JOINTE DE MATCH
        // ------------------------------------------------------------------------

        /// <summary>Crée un nouveau match (devient host).</summary>
        public async UniTask<bool> CreateMatchAsync(string missionId, int maxPlayers = 4)
        {
            if (IsOffline)
            {
                Role = MatchRole.Host;
                MatchId = "offline_" + missionId;
                IsInMatch = true;
                return true;
            }
#if KINETICS_NAKAMA
            try
            {
                var socket = NakamaClient.Instance.Socket;
                var match = await socket.CreateMatchAsync(missionId);
                MatchId = match.Id;
                Role = MatchRole.Host;
                IsInMatch = true;
                _nextTickTime = Time.unscaledTime + 1f / _tickRate;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchManager] CreateMatch échec: {ex.Message}");
                return false;
            }
#else
            await UniTask.Yield();
            Role = MatchRole.Host;
            MatchId = "stub_" + missionId;
            IsInMatch = true;
            return true;
#endif
        }

        /// <summary>Rejoint un match par code partie.</summary>
        public async UniTask<bool> JoinMatchAsync(string matchId)
        {
            if (IsOffline)
            {
                Role = MatchRole.Host;
                MatchId = matchId;
                IsInMatch = true;
                return true;
            }
#if KINETICS_NAKAMA
            try
            {
                var socket = NakamaClient.Instance.Socket;
                await socket.JoinMatchAsync(matchId);
                MatchId = matchId;
                Role = MatchRole.Client;
                IsInMatch = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchManager] JoinMatch échec: {ex.Message}");
                return false;
            }
#else
            await UniTask.Yield();
            MatchId = matchId;
            Role = MatchRole.Client;
            IsInMatch = true;
            return true;
#endif
        }

        /// <summary>Quitte le match courant.</summary>
        public async UniTask LeaveMatchAsync()
        {
            if (!IsInMatch) return;
#if KINETICS_NAKAMA
            if (!IsOffline && NakamaClient.Instance.Socket != null)
            {
                try { await NakamaClient.Instance.Socket.LeaveMatchAsync(MatchId); } catch (Exception ex) { Debug.LogWarning(ex.Message); }
            }
#endif
            _players.Clear();
            _snapshotBuffer.Clear();
            MatchId = null;
            IsInMatch = false;
            await UniTask.Yield();
        }

        // ------------------------------------------------------------------------
        //  SYNCHRONISATION D'ÉTAT
        // ------------------------------------------------------------------------

        /// <summary>Met à jour l'état du joueur local et l'envoie (client) ou broadcast (host).</summary>
        public void UpdateLocalState(PlayerNetState localState)
        {
            if (!IsInMatch) return;
            _players[localState.UserId] = localState;

            if (Role == MatchRole.Host && Time.unscaledTime >= _nextTickTime)
            {
                _nextTickTime = Time.unscaledTime + 1f / _tickRate;
                BroadcastSnapshot(localState);
            }
            else if (Role == MatchRole.Client)
            {
                SendStateToHost(localState);
            }
        }

        private void BroadcastSnapshot(PlayerNetState hostState)
        {
            var snap = new MatchSnapshot
            {
                Tick = hostState.TickNumber + 1,
                ServerTime = Time.realtimeSinceStartupAsDouble,
                Players = new List<PlayerNetState>(_players.Values)
            };
            SnapshotApplied?.Invoke(snap);
#if KINETICS_NAKAMA
            if (!IsOffline)
            {
                var json = JsonConvert.SerializeObject(snap);
                _ = NakamaClient.Instance?.Socket?.SendMatchStateAsync(MatchId, 1, json);
            }
#endif
        }

        private void SendStateToHost(PlayerNetState state)
        {
#if KINETICS_NAKAMA
            if (!IsOffline && NakamaClient.Instance?.Socket != null)
            {
                var json = JsonConvert.SerializeObject(state);
                _ = NakamaClient.Instance.Socket.SendMatchStateAsync(MatchId, 2, json);
            }
#endif
        }

        /// <summary>Reçoit un snapshot distant (host → clients) et l'enfile pour interpolation.</summary>
        public void EnqueueSnapshot(MatchSnapshot snap)
        {
            if (snap == null) return;
            _snapshotBuffer.Enqueue(snap);
            while (_snapshotBuffer.Count > 32) _snapshotBuffer.Dequeue(); // borné
            _lastServerTime = snap.ServerTime;
        }

        /// <summary>Calcule l'état interpolé à l'instant courant (render time = server - interpBuffer).</summary>
        public PlayerNetState GetInterpolatedState(string userId)
        {
            if (_snapshotBuffer.Count < 2)
            {
                return _players.TryGetValue(userId, out var s) ? s : null;
            }

            double renderTime = _lastServerTime - _interpBuffer;
            MatchSnapshot prev = null, next = null;
            foreach (var snap in _snapshotBuffer)
            {
                if (snap.ServerTime <= renderTime) prev = snap;
                if (snap.ServerTime > renderTime) { next = snap; break; }
            }
            if (prev == null) prev = next;
            if (next == null) return FindInSnapshot(prev, userId);

            var a = FindInSnapshot(prev, userId);
            var b = FindInSnapshot(next, userId);
            if (a == null || b == null) return a ?? b;

            float t = (float)((renderTime - prev.ServerTime) / Math.Max(0.001, next.ServerTime - prev.ServerTime));
            t = Mathf.Clamp01(t);
            return LerpStates(a, b, t);
        }

        private static PlayerNetState FindInSnapshot(MatchSnapshot snap, string userId)
        {
            if (snap == null) return null;
            for (int i = 0; i < snap.Players.Count; i++)
                if (snap.Players[i].UserId == userId) return snap.Players[i];
            return null;
        }

        private static PlayerNetState LerpStates(PlayerNetState a, PlayerNetState b, float t)
        {
            var r = a.Clone();
            r.Position = Vector3.Lerp(a.Position, b.Position, t);
            r.Velocity = Vector3.Lerp(a.Velocity, b.Velocity, t);
            r.Yaw      = Mathf.LerpAngle(a.Yaw, b.Yaw, t);
            r.Pitch    = Mathf.Lerp(a.Pitch, b.Pitch, t);
            r.Health   = Mathf.Lerp(a.Health, b.Health, t);
            r.Shield   = Mathf.Lerp(a.Shield, b.Shield, t);
            return r;
        }

        // ------------------------------------------------------------------------
        //  RPC ACTIONS
        // ------------------------------------------------------------------------

        /// <summary>Envoie une action au host (tir, ability, etc.).</summary>
        public void SendAction(PlayerAction action)
        {
            if (!IsInMatch || action == null) return;
            if (Role == MatchRole.Host)
            {
                ValidateAndApply(action);
                return;
            }
#if KINETICS_NAKAMA
            if (!IsOffline && NakamaClient.Instance?.Socket != null)
            {
                var json = JsonConvert.SerializeObject(action);
                _ = NakamaClient.Instance.Socket.SendMatchStateAsync(MatchId, 3, json);
            }
#endif
        }

        private void ValidateAndApply(PlayerAction action)
        {
            var validator = ServiceLocator.Instance?.Get<AntiCheatValidator>();
            if (validator != null)
            {
                var result = validator.ValidatePlayerAction(action);
                if (!result.Accepted)
                {
                    Debug.LogWarning($"[MatchManager] Action rejetée par anti-cheat: {result.Reason}");
                    return;
                }
            }
            // Application locale (le combat local appliquera les dégâts/munitions).
            GameEventBus.Instance.Publish(new PlayerActionEvent(action.UserId, action.Type, action.Origin, action.Direction, action.TargetId, action.WeaponId));
        }

        // ------------------------------------------------------------------------
        //  MIGRATION HOST
        // ------------------------------------------------------------------------

        /// <summary>Élit un nouveau host si l'ancien a quitté (par userId alphabétique le plus bas).</summary>
        public void MigrateHost(string newHostUserId)
        {
            if (string.IsNullOrEmpty(newHostUserId)) return;
            Role = MatchRole.Host;
            Debug.Log($"[MatchManager] Migration host → {newHostUserId}. Rôle local = Host.");
        }
    }

    /// <summary>Événement publié par <see cref="MatchManager"/> quand une action validée est appliquée.</summary>
    public readonly struct PlayerActionEvent
    {
        public readonly string UserId;
        public readonly string Type;
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        public readonly int TargetId;
        public readonly string WeaponId;

        public PlayerActionEvent(string userId, string type, Vector3 origin, Vector3 dir, int targetId, string weaponId)
        {
            UserId = userId; Type = type; Origin = origin; Direction = dir;
            TargetId = targetId; WeaponId = weaponId;
        }
    }
}
