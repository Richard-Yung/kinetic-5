// ============================================================================
//  KINETICS 5 — Leaderboard Service
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Service de classements (leaderboards) Nakama. Trois portées :
//    • GLOBAL : top mondial (top 100 + rank du joueur).
//    • FRIENDS : classement entre amis Nakama.
//    • CREW : classement de l'équipage (clan) du joueur.
//
//  Seasons : chaque season dure ~30 jours, les scores sont reset à la fin.
//  Le service gère aussi la soumission de score (post-mission).
// ============================================================================
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using KINETICS5.Core;

namespace KINETICS5.Network
{
    /// <summary>Portée d'un classement.</summary>
    public enum LeaderboardScope
    {
        Global,
        Friends,
        Crew
    }

    /// <summary>Entrée d'un classement (un joueur + son score).</summary>
    [Serializable]
    public sealed class LeaderboardEntry
    {
        public string UserId;
        public string DisplayName;
        public long Score;
        public int Rank;
        public string AgentId;
        public int PlayerLevel;
        public double SubmissionTimeUnix;
    }

    /// <summary>Infos d'une season de classement.</summary>
    [Serializable]
    public sealed class LeaderboardSeason
    {
        public string Id;
        public string DisplayName;
        public long StartUnix;
        public long EndUnix;
        public bool IsActive;
        public int DaysRemaining;
    }

    /// <summary>Réponse complète d'un classement.</summary>
    [Serializable]
    public sealed class LeaderboardResponse
    {
        public string LeaderboardId;
        public LeaderboardScope Scope;
        public List<LeaderboardEntry> Entries = new();
        public LeaderboardEntry PlayerEntry;
        public int TotalEntries;
        public LeaderboardSeason CurrentSeason;
    }

    /// <summary>Service de classements KINETICS 5.</summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardService : MonoBehaviour
    {
        private static LeaderboardService _instance;
        public static LeaderboardService Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[LeaderboardService]");
                _instance = go.AddComponent<LeaderboardService>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        [Header("Configuration")]
        [Tooltip("Id du leaderboard global principal (ex: weekly_score).")]
        [SerializeField] private string _globalLeaderboardId = "global_weekly_score";
        [Tooltip("Id du leaderboard d'équipage (clan).")]
        [SerializeField] private string _crewLeaderboardId = "crew_weekly_score";
        [Tooltip("Active le cache local (évite les requêtes réseau en boucle).")]
        [SerializeField] private float _cacheTtlSeconds = 30f;

        private readonly Dictionary<string, (LeaderboardResponse data, float expiresAt)> _cache = new();

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        /// <summary>Soumet un score au leaderboard global.</summary>
        /// <param name="missionId">Id de la mission terminée.</param>
        /// <param name="score">Score numérique (XP + bonus).</param>
        /// <param name="metadata">Données additionnelles (agent, kills, etc.).</param>
        public async UniTask<bool> SubmitScoreAsync(string missionId, long score, Dictionary<string, string> metadata = null)
        {
            var client = NakamaClient.Instance;
            if (client == null || client.IsOfflineMode) return false;

#if KINETICS_NAKAMA
            try
            {
                var meta = metadata != null ? JsonConvert.SerializeObject(metadata) : "{}";
                await client.Client.WriteLeaderboardRecordAsync(client.Session, _globalLeaderboardId, score, score, meta);
                InvalidateCache(_globalLeaderboardId);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardService] SubmitScore échec: {ex.Message}");
                return false;
            }
#else
            await UniTask.Yield();
            Debug.Log($"[LeaderboardService] (stub) SubmitScore {score} pour {missionId}.");
            return true;
#endif
        }

        /// <summary>Récupère un classement. Utilise le cache si frais.</summary>
        public async UniTask<LeaderboardResponse> GetLeaderboardAsync(LeaderboardScope scope, int limit = 100)
        {
            var boardId = scope == LeaderboardScope.Crew ? _crewLeaderboardId : _globalLeaderboardId;
            if (TryGetCached(boardId, out var cached)) return cached;

            var client = NakamaClient.Instance;
            if (client == null || client.IsOfflineMode)
            {
                return BuildOfflineResponse(scope, limit);
            }

#if KINETICS_NAKAMA
            try
            {
                var ownerId = client.Session.UserId;
                var records = scope switch
                {
                    LeaderboardScope.Global  => await client.Client.ListLeaderboardRecordsAsync(client.Session, boardId, ownerId, limit),
                    LeaderboardScope.Friends => await client.Client.ListLeaderboardRecordsAroundOwnerAsync(client.Session, boardId, ownerId, limit),
                    LeaderboardScope.Crew    => await client.Client.ListLeaderboardRecordsAsync(client.Session, boardId, ownerId, limit),
                    _ => null
                };

                var resp = new LeaderboardResponse
                {
                    LeaderboardId = boardId,
                    Scope = scope,
                    TotalEntries = records?.Records?.Count ?? 0,
                    CurrentSeason = await FetchSeasonAsync()
                };
                if (records != null)
                {
                    foreach (var r in records.Records)
                    {
                        resp.Entries.Add(new LeaderboardEntry
                        {
                            UserId = r.OwnerId,
                            DisplayName = r.Username,
                            Score = r.Score,
                            Rank = r.Rank,
                            SubmissionTimeUnix = r.UpdateTime.Seconds
                        });
                    }
                    if (records.OwnerRecord != null)
                    {
                        resp.PlayerEntry = new LeaderboardEntry
                        {
                            UserId = records.OwnerRecord.OwnerId,
                            DisplayName = records.OwnerRecord.Username,
                            Score = records.OwnerRecord.Score,
                            Rank = records.OwnerRecord.Rank
                        };
                    }
                }
                PutCache(boardId, resp);
                return resp;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardService] GetLeaderboard échec: {ex.Message}");
                return BuildOfflineResponse(scope, limit);
            }
#else
            await UniTask.Delay(50);
            var stub = BuildOfflineResponse(scope, limit);
            PutCache(boardId, stub);
            return stub;
#endif
        }

        /// <summary>Récupère les infos de la season courante.</summary>
        public async UniTask<LeaderboardSeason> GetCurrentSeasonAsync()
        {
            var client = NakamaClient.Instance;
            if (client == null || client.IsOfflineMode)
            {
                return BuildOfflineSeason();
            }
#if KINETICS_NAKAMA
            try
            {
                var list = await client.Client.ListLeaderboardsAsync(client.Session);
                foreach (var b in list.Leaderboards)
                {
                    if (b.Id == _globalLeaderboardId)
                    {
                        return new LeaderboardSeason
                        {
                            Id = b.Id,
                            DisplayName = b.Title,
                            StartUnix = b.StartTime?.Seconds ?? 0,
                            EndUnix = b.EndTime?.Seconds ?? 0,
                            IsActive = b.EndTime == null || b.EndTime.Seconds > DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            DaysRemaining = b.EndTime != null
                                ? (int)Math.Max(0, (b.EndTime.Seconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / 86400.0)
                                : 0
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardService] Season fetch échec: {ex.Message}");
            }
#endif
            await UniTask.Yield();
            return BuildOfflineSeason();
        }

        private async UniTask<LeaderboardSeason> FetchSeasonAsync() => await GetCurrentSeasonAsync();

        // ------------------------------------------------------------------------
        //  CACHE & FALLBACK
        // ------------------------------------------------------------------------

        private bool TryGetCached(string id, out LeaderboardResponse data)
        {
            if (_cache.TryGetValue(id, out var entry) && entry.expiresAt > Time.unscaledTime)
            {
                data = entry.data; return true;
            }
            data = null; return false;
        }

        private void PutCache(string id, LeaderboardResponse data)
        {
            _cache[id] = (data, Time.unscaledTime + _cacheTtlSeconds);
        }

        private void InvalidateCache(string id) => _cache.Remove(id);

        private static LeaderboardResponse BuildOfflineResponse(LeaderboardScope scope, int limit)
        {
            var resp = new LeaderboardResponse
            {
                LeaderboardId = "offline",
                Scope = scope,
                TotalEntries = 1,
                CurrentSeason = BuildOfflineSeason()
            };
            resp.Entries.Add(new LeaderboardEntry
            {
                UserId = "offline_player",
                DisplayName = "Operative",
                Score = 0,
                Rank = 1,
                AgentId = "VULCAN",
                PlayerLevel = 1
            });
            resp.PlayerEntry = resp.Entries[0];
            return resp;
        }

        private static LeaderboardSeason BuildOfflineSeason() => new()
        {
            Id = "offline_season",
            DisplayName = "Season (offline)",
            IsActive = true,
            DaysRemaining = 30,
            StartUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            EndUnix = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
        };
    }
}
