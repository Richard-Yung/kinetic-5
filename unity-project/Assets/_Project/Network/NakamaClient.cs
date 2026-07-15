// ============================================================================
//  KINETICS 5 — Nakama Client (wrapper multiplateforme)
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Wrapper autour du SDK Nakama Unity (Heroic Labs) avec DEGRADATION GRACIEUSE
//  en mode hors-ligne quand le package Nakama n'est pas installé ou quand le
//  serveur est injoignable.
//
//  Responsabilités :
//    • Authentification : email/mot de passe, device ID, OAuth (Google/Apple).
//    • Gestion de session (token, refresh, expiration).
//    • Connect / reconnect automatique avec backoff exponentiel.
//    • Mode hors-ligne : toutes les APIs retournent des stubs jouables.
//    • Hook d'événements : SessionExpired, Authenticated, ConnectionStateChanged.
//
//  Configuration serveur :
//    Hébergement Nakama 3.x : Docker (heroiclabs/nakama:3.x) sur GCP/AWS,
//    port 7350 (TCP), 7351 (HTTP), 7352 (gRPC). TLS obligatoire en production.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using KINETICS5.Core;

namespace KINETICS5.Network
{
    /// <summary>État de la connexion réseau.</summary>
    public enum NetworkConnectionState
    {
        /// <summary>Non encore initialisé.</summary>
        Disconnected,
        /// <summary>Connexion en cours (avec retry backoff).</summary>
        Connecting,
        /// <summary>Connecté et authentifié.</summary>
        Connected,
        /// <summary>Mode hors-ligne (stubs locaux — jeu jouable mais non sync).</summary>
        Offline,
        /// <summary>Erreur fatale (serveur down, credentials invalides).</summary>
        Error
    }

    /// <summary>Méthode d'authentification supportée.</summary>
    public enum AuthMethod
    {
        Email,
        Device,
        GoogleOAuth,
        AppleOAuth,
        FacebookOAuth,
        Steam
    }

    /// <summary>Résultat d'une demande d'authentification.</summary>
    public readonly struct AuthResult
    {
        public readonly bool Success;
        public readonly string UserId;
        public readonly string Username;
        public readonly string SessionToken;
        public readonly DateTime ExpiresAt;
        public readonly string Error;

        public AuthResult(bool success, string userId, string username, string token, DateTime expiresAt, string error)
        {
            Success = success; UserId = userId; Username = username;
            SessionToken = token; ExpiresAt = expiresAt; Error = error;
        }
    }

    /// <summary>
    /// Wrapper du client Nakama. Singleton MonoBehaviour, configuré via Inspector
    /// ou via <see cref="Configure"/> au boot. Degrade en mode offline si le
    /// package Nakama n'est pas présent (define <c>KINETICS_NAKAMA</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NakamaClient : MonoBehaviour
    {
        // --- Singleton ---
        private static NakamaClient _instance;
        public static NakamaClient Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[NakamaClient]");
                _instance = go.AddComponent<NakamaClient>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        [Header("Configuration serveur")]
        [Tooltip("Schéma : http pour dév local, https en production.")]
        [SerializeField] private string _scheme = "https";
        [Tooltip("Hôte Nakama (ex: nakama.kinetics5.gg).")]
        [SerializeField] private string _host = "127.0.0.1";
        [Tooltip("Port HTTP Nakama (7350 par défaut).")]
        [SerializeField] private int _port = 7350;
        [Tooltip("Clé serveur Nakama (defaultkey en dév).")]
        [SerializeField] private string _serverKey = "defaultkey";

        [Header("Reconnexion")]
        [Tooltip("Active la reconnexion automatique.")]
        [SerializeField] private bool _autoReconnect = true;
        [Tooltip("Délai initial (s) entre deux tentatives.")]
        [SerializeField] private float _reconnectInitialDelay = 1f;
        [Tooltip("Délai max (s) entre deux tentatives.")]
        [SerializeField] private float _reconnectMaxDelay = 30f;
        [Tooltip("Nombre max de tentatives avant passage en offline.")]
        [SerializeField] private int _maxReconnectAttempts = 5;

        [Header("Mode hors-ligne")]
        [Tooltip("Force le mode offline (utile en dév / tests).")]
        [SerializeField] private bool _forceOffline = false;

        // --- État ---
        public NetworkConnectionState State { get; private set; } = NetworkConnectionState.Disconnected;
        public AuthResult? CurrentAuth { get; private set; }
        public bool IsOfflineMode => State == NetworkConnectionState.Offline;

        /// <summary>Déclenché quand l'état de la connexion change.</summary>
        public event Action<NetworkConnectionState> ConnectionStateChanged;
        /// <summary>Déclenché après authentification réussie.</summary>
        public event Action<AuthResult> Authenticated;
        /// <summary>Déclenché quand la session expire ou est invalidée.</summary>
        public event Action SessionExpired;

        private float _currentReconnectDelay;
        private int _reconnectAttempts;
        private CancellationTokenSource _connectCts;

#if KINETICS_NAKAMA
        // Références fortement typées au SDK Nakama.
        private Nakama.Client _client;
        private Nakama.ISocket _socket;
        private Nakama.ISession _session;
#endif

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Configure(_scheme, _host, _port, _serverKey);
        }

        private void OnDestroy()
        {
            _connectCts?.Cancel();
            _connectCts?.Dispose();
            _connectCts = null;
            if (_instance == this) _instance = null;
        }

        /// <summary>Reconfigure les paramètres serveur à runtime (avant Connect).</summary>
        public void Configure(string scheme, string host, int port, string serverKey)
        {
            _scheme = scheme; _host = host; _port = port; _serverKey = serverKey;
#if KINETICS_NAKAMA
            _client = new Nakama.Client(_scheme, _host, _port, _serverKey, UnityWebRequestAdapter.Instance);
#endif
        }

        // ------------------------------------------------------------------------
        //  AUTHENTIFICATION
        // ------------------------------------------------------------------------

        /// <summary>Authentification par email/mot de passe.</summary>
        public async UniTask<AuthResult> AuthenticateEmailAsync(string email, string password, string username = null, bool createAccount = false)
        {
            if (_forceOffline) return EnterOfflineMode(email);

#if KINETICS_NAKAMA
            try
            {
                SetState(NetworkConnectionState.Connecting);
                _session = createAccount
                    ? await _client.AuthenticateEmailAsync(email, password, username, createAccount)
                    : await _client.AuthenticateEmailAsync(email, password);
                return FinalizeAuth(_session);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NakamaClient] Auth email échouée : {ex.Message}");
                return FallbackOffline(email, ex.Message);
            }
#else
            await UniTask.Delay(50); // simule latence réseau
            return EnterOfflineMode(email);
#endif
        }

        /// <summary>Authentification par device ID (anonyme, persistant).</summary>
        public async UniTask<AuthResult> AuthenticateDeviceAsync(string deviceId = null)
        {
            deviceId ??= SystemInfo.deviceUniqueIdentifier;
            if (_forceOffline) return EnterOfflineMode(deviceId);

#if KINETICS_NAKAMA
            try
            {
                SetState(NetworkConnectionState.Connecting);
                _session = await _client.AuthenticateDeviceAsync(deviceId, true);
                return FinalizeAuth(_session);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NakamaClient] Auth device échouée : {ex.Message}");
                return FallbackOffline(deviceId, ex.Message);
            }
#else
            await UniTask.Delay(50);
            return EnterOfflineMode(deviceId);
#endif
        }

        /// <summary>Authentification OAuth (Google / Apple / Facebook / Steam).</summary>
        public async UniTask<AuthResult> AuthenticateOAuthAsync(AuthMethod provider, string oauthToken, string username = null)
        {
            if (_forceOffline) return EnterOfflineMode(oauthToken);

#if KINETICS_NAKAMA
            try
            {
                SetState(NetworkConnectionState.Connecting);
                _session = provider switch
                {
                    AuthMethod.GoogleOAuth  => await _client.AuthenticateGoogleAsync(oauthToken, true),
                    AuthMethod.AppleOAuth   => await _client.AuthenticateAppleAsync(oauthToken, true),
                    AuthMethod.FacebookOAuth=> await _client.AuthenticateFacebookAsync(oauthToken, true),
                    AuthMethod.Steam         => await _client.AuthenticateSteamAsync(oauthToken, true),
                    _ => throw new ArgumentException($"Provider OAuth non supporté : {provider}")
                };
                return FinalizeAuth(_session);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NakamaClient] Auth OAuth ({provider}) échouée : {ex.Message}");
                return FallbackOffline(oauthToken, ex.Message);
            }
#else
            await UniTask.Delay(50);
            return EnterOfflineMode(oauthToken);
#endif
        }

        /// <summary>Déconnexion propre (logout côté serveur).</summary>
        public async UniTask LogoutAsync()
        {
#if KINETICS_NAKAMA
            if (_session != null && _client != null)
            {
                try { await _client.SessionLogoutAsync(_session); } catch (Exception ex) { Debug.LogWarning(ex.Message); }
            }
            if (_socket != null && _socket.IsConnected) { await _socket.CloseAsync(); }
            _session = null;
#endif
            SetState(NetworkConnectionState.Disconnected);
            CurrentAuth = null;
            await UniTask.Yield();
        }

        // ------------------------------------------------------------------------
        //  CONNEXION SOCKET (temps réel — matches, chat)
        // ------------------------------------------------------------------------

        /// <summary>Établit la connexion socket (temps réel).</summary>
        public async UniTask<bool> ConnectSocketAsync()
        {
            if (IsOfflineMode) return false;
#if KINETICS_NAKAMA
            if (_session == null || _client == null) return false;
            try
            {
                _socket = _client.NewSocket();
                _socket.Connected += () => SetState(NetworkConnectionState.Connected);
                _socket.Closed += () => OnSocketClosed();
                _socket.ReceivedError += err => Debug.LogError($"[NakamaClient] Socket error: {err}");
                await _socket.ConnectAsync(_session, true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NakamaClient] Socket connect échec: {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        private void OnSocketClosed()
        {
            if (_autoReconnect && State != NetworkConnectionState.Offline)
            {
                ScheduleReconnect();
            }
            else
            {
                SetState(NetworkConnectionState.Disconnected);
            }
        }

        private async void ScheduleReconnect()
        {
            _connectCts?.Cancel();
            _connectCts = new CancellationTokenSource();
            var token = _connectCts.Token;
            _currentReconnectDelay = _reconnectInitialDelay;
            _reconnectAttempts = 0;

            while (_reconnectAttempts < _maxReconnectAttempts && !token.IsCancellationRequested)
            {
                SetState(NetworkConnectionState.Connecting);
                await UniTask.Delay(TimeSpan.FromSeconds(_currentReconnectDelay), cancellationToken: token);
                if (token.IsCancellationRequested) return;
                var ok = await ConnectSocketAsync();
                if (ok)
                {
                    _reconnectAttempts = 0;
                    return;
                }
                _reconnectAttempts++;
                _currentReconnectDelay = Mathf.Min(_reconnectMaxDelay, _currentReconnectDelay * 2f);
            }

            // Échec définitif → passage en mode offline.
            Debug.LogWarning("[NakamaClient] Reconnexion échouée, passage en mode offline.");
            SetState(NetworkConnectionState.Offline);
        }

        // ------------------------------------------------------------------------
        //  HELPERS AUTH
        // ------------------------------------------------------------------------

        private AuthResult FinalizeAuth(object session)
        {
            string userId, username, token;
            DateTime expires;
#if KINETICS_NAKAMA
            var s = (Nakama.ISession)session;
            userId = s.UserId; username = s.Username; token = s.AuthToken;
            expires = s.ExpiresAt;
#else
            userId = SystemInfo.deviceUniqueIdentifier; username = "Operative"; token = Guid.NewGuid().ToString("N"); expires = DateTime.UtcNow.AddHours(1);
#endif
            var result = new AuthResult(true, userId, username, token, expires, null);
            CurrentAuth = result;
            Authenticated?.Invoke(result);
            SetState(NetworkConnectionState.Connected);
            return result;
        }

        private AuthResult EnterOfflineMode(string pseudoId)
        {
            var result = new AuthResult(true, "offline_" + pseudoId, pseudoId, "offline_token", DateTime.UtcNow.AddDays(7), null);
            CurrentAuth = result;
            SetState(NetworkConnectionState.Offline);
            Authenticated?.Invoke(result);
            return result;
        }

        private AuthResult FallbackOffline(string id, string error)
        {
            Debug.LogWarning($"[NakamaClient] Bascule en mode offline ({error}).");
            return EnterOfflineMode(id);
        }

        private void SetState(NetworkConnectionState newState)
        {
            if (State == newState) return;
            State = newState;
            ConnectionStateChanged?.Invoke(newState);
        }

        /// <summary>Vérifie la validité de la session courante (expire dans 60s ?).</summary>
        public bool IsSessionValid()
        {
            if (CurrentAuth == null) return false;
            return CurrentAuth.Value.ExpiresAt > DateTime.UtcNow.AddSeconds(60);
        }

        /// <summary>Refresh la session si elle va expirer.</summary>
        public async UniTask<bool> RefreshSessionIfNeededAsync()
        {
            if (CurrentAuth == null || IsOfflineMode) return false;
            if (IsSessionValid()) return true;

#if KINETICS_NAKAMA
            try
            {
                _session = await _client.SessionRefreshAsync(_session);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NakamaClient] Refresh session échoué: {ex.Message}");
                SessionExpired?.Invoke();
                return false;
            }
#else
            await UniTask.Yield();
            return false;
#endif
        }

#if KINETICS_NAKAMA
        /// <summary>Accès direct au socket Nakama (pour MatchManager, ChatService).</summary>
        public Nakama.ISocket Socket => _socket;
        /// <summary>Accès direct au client Nakama (pour requêtes custom).</summary>
        public Nakama.Client Client => _client;
        /// <summary>Session courante (null si non authentifié).</summary>
        public Nakama.ISession Session => _session;
#endif
    }
}
