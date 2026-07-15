// ============================================================================
//  KINETICS 5 — Chat Service (chat mondial, équipage, messages privés)
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Service de messagerie temps réel basé sur Nakama Realtime Channels :
//    • WORLD CHAT  : canal public global (typé "worldchat").
//    • CREW CHAT   : canal d'équipage (typé "crewchat:<crewId>").
//    • PRIVATE DM  : messages directs 1-à-1 (typé "dm:<userIdA>_<userIdB>").
//
//  Sécurité :
//    • FILTRE DE BLASPHEME (regex + liste noire, FR + EN).
//    • RATE LIMITING : 5 messages / 10s max (anti-spam).
//    • LONGUEUR MAX : 280 caractères par message (style Twitter).
//    • HTML SANITIZATION : strip des balises <>, protège contre XSS UI.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using KINETICS5.Core;

namespace KINETICS5.Network
{
    /// <summary>Type de canal de chat.</summary>
    public enum ChatChannelType
    {
        World,
        Crew,
        DirectMessage
    }

    /// <summary>Message de chat temps réel.</summary>
    [Serializable]
    public sealed class ChatMessage
    {
        public string Id;
        public ChatChannelType ChannelType;
        public string ChannelId;
        public string SenderId;
        public string SenderName;
        public string Text;
        public long UnixTimestamp;
        public bool IsSystem;
    }

    /// <summary>État d'un canal de chat auquel on est abonné.</summary>
    public sealed class ChatChannelState
    {
        public ChatChannelType Type;
        public string Id;
        public string DisplayName;
        public int UnreadCount;
        public DateTime LastMessageAt;
        public readonly List<ChatMessage> History = new(64);
    }

    /// <summary>Service de chat temps réel KINETICS 5.</summary>
    [DisallowMultipleComponent]
    public sealed class ChatService : MonoBehaviour
    {
        private static ChatService _instance;
        public static ChatService Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[ChatService]");
                _instance = go.AddComponent<ChatService>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        [Header("Limites")]
        [Tooltip("Nombre max de messages par fenêtre de rate-limit.")]
        [SerializeField] private int _rateLimitCount = 5;
        [Tooltip("Durée de la fenêtre de rate-limit (s).")]
        [SerializeField] private float _rateLimitWindow = 10f;
        [Tooltip("Longueur maximale d'un message.")]
        [SerializeField] private int _maxMessageLength = 280;
        [Tooltip("Active le filtre de blasphème.")]
        [SerializeField] private bool _profanityFilterEnabled = true;
        [Tooltip("Caractère de remplacement pour les mots bannis.")]
        [SerializeField] private char _censorChar = '*';

        /// <summary>Déclenché à chaque message reçu sur un canal abonné.</summary>
        public event Action<ChatMessage> MessageReceived;

        private readonly Dictionary<string, ChatChannelState> _channels = new(16);
        private readonly Queue<DateTime> _sendTimestamps = new(8);

        // Liste noire FR + EN (à étendre). Compilation regex une seule fois.
        private static readonly Regex[] BannedPatterns =
        {
            new(@"\b(putain|merde|connard|salope|enculé|niquer|bite|couilles)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100)),
            new(@"\b(fuck|shit|bitch|asshole|dick|cunt|bastard|wanker|prick)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100)),
            new(@"\b(nigger|nigga|faggot|retard|tranny)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100)),
        };
        private static readonly Regex HtmlTagPattern = new(@"<[^>]+>", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _ = UnsubscribeAllAsync();
            _instance = null;
        }

        // ------------------------------------------------------------------------
        //  SOUSCRIPTION
        // ------------------------------------------------------------------------

        /// <summary>Rejoint un canal de chat.</summary>
        public async UniTask<bool> JoinChannelAsync(ChatChannelType type, string channelId, string displayName = null)
        {
            var key = ChannelKey(type, channelId);
            if (_channels.ContainsKey(key)) return true;

            _channels[key] = new ChatChannelState
            {
                Type = type,
                Id = channelId,
                DisplayName = displayName ?? channelId,
                LastMessageAt = DateTime.UtcNow
            };

            var client = NakamaClient.Instance;
            if (client == null || client.IsOfflineMode) return true;

#if KINETICS_NAKAMA
            try
            {
                var nakamaType = type switch
                {
                    ChatChannelType.World          => Nakama.ChannelType.Room,
                    ChatChannelType.Crew           => Nakama.ChannelType.Group,
                    ChatChannelType.DirectMessage  => Nakama.ChannelType.DirectMessage,
                    _ => Nakama.ChannelType.Room
                };
                var channel = await client.Socket.JoinChatAsync(channelId, nakamaType, true, false);
                _channels[key].Id = channel.Id;
                client.Socket.ReceivedChannelMessage += OnNakamaMessage;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChatService] JoinChannel échec: {ex.Message}");
                return false;
            }
#endif
            await UniTask.Yield();
            return true;
        }

        /// <summary>Quitte un canal.</summary>
        public async UniTask LeaveChannelAsync(ChatChannelType type, string channelId)
        {
            var key = ChannelKey(type, channelId);
            if (!_channels.Remove(key)) return;
#if KINETICS_NAKAMA
            var client = NakamaClient.Instance;
            if (client != null && !client.IsOfflineMode && client.Socket != null)
            {
                try { await client.Socket.LeaveChatAsync(channelId, type == ChatChannelType.Crew ? Nakama.ChannelType.Group : Nakama.ChannelType.Room); }
                catch (Exception ex) { Debug.LogWarning(ex.Message); }
            }
#endif
            await UniTask.Yield();
        }

        /// <summary>Quitte tous les canaux (au logout).</summary>
        public async UniTask UnsubscribeAllAsync()
        {
            var keys = new List<string>(_channels.Keys);
            foreach (var k in keys)
            {
                // On parse la clé pour récupérer type + id.
                var parts = k.Split('|', 2);
                if (parts.Length == 2 && Enum.TryParse<ChatChannelType>(parts[0], out var t))
                    await LeaveChannelAsync(t, parts[1]);
            }
        }

        // ------------------------------------------------------------------------
        //  ENVOI
        // ------------------------------------------------------------------------

        /// <summary>Envoie un message. Applique rate-limit + filtre + sanitization.</summary>
        public async UniTask<bool> SendMessageAsync(ChatChannelType type, string channelId, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (text.Length > _maxMessageLength) text = text.Substring(0, _maxMessageLength);

            if (!CheckRateLimit())
            {
                Debug.LogWarning("[ChatService] Rate limit dépassé, message rejeté.");
                return false;
            }

            text = Sanitize(text);
            if (_profanityFilterEnabled) text = CensorProfanity(text);

            var key = ChannelKey(type, channelId);
            if (!_channels.ContainsKey(key))
            {
                bool joined = await JoinChannelAsync(type, channelId);
                if (!joined) return false;
            }

            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                ChannelType = type,
                ChannelId = channelId,
                SenderId = NakamaClient.Instance?.CurrentAuth?.UserId ?? "local",
                SenderName = NakamaClient.Instance?.CurrentAuth?.Username ?? "Operative",
                Text = text,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                IsSystem = false
            };

            _channels[key].History.Add(msg);
            _channels[key].LastMessageAt = DateTime.UtcNow;

            var client = NakamaClient.Instance;
            if (client != null && !client.IsOfflineMode)
            {
#if KINETICS_NAKAMA
                try
                {
                    await client.Socket.WriteChatMessageAsync(channelId, text);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ChatService] Send échec: {ex.Message}");
                    return false;
                }
#endif
            }
            MessageReceived?.Invoke(msg);
            return true;
        }

        /// <summary>Envoie un message système (jointure, déconnexion...).</summary>
        public void SendSystemMessage(ChatChannelType type, string channelId, string text)
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                ChannelType = type,
                ChannelId = channelId,
                SenderId = "system",
                SenderName = "KINETICS 5",
                Text = text,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                IsSystem = true
            };
            MessageReceived?.Invoke(msg);
        }

        // ------------------------------------------------------------------------
        //  RÉCEPTION
        // ------------------------------------------------------------------------

#if KINETICS_NAKAMA
        private void OnNakamaMessage(Nakama.IApiChannelMessage message)
        {
            var msg = new ChatMessage
            {
                Id = message.MessageId,
                ChannelId = message.ChannelId,
                SenderId = message.SenderId,
                SenderName = message.Username,
                Text = Sanitize(message.Content),
                UnixTimestamp = message.CreateTime.Seconds
            };
            MessageReceived?.Invoke(msg);
        }
#endif

        // ------------------------------------------------------------------------
        //  HELPERS
        // ------------------------------------------------------------------------

        private bool CheckRateLimit()
        {
            var now = DateTime.UtcNow;
            while (_sendTimestamps.Count > 0 && (now - _sendTimestamps.Peek()).TotalSeconds > _rateLimitWindow)
                _sendTimestamps.Dequeue();
            if (_sendTimestamps.Count >= _rateLimitCount) return false;
            _sendTimestamps.Enqueue(now);
            return true;
        }

        private static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            // Strip balises HTML (protection XSS UI Text).
            input = HtmlTagPattern.Replace(input, string.Empty);
            // Strip caractères de contrôle.
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c >= 0x20 || c == '\n') sb.Append(c);
            }
            return sb.ToString();
        }

        private string CensorProfanity(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            foreach (var pattern in BannedPatterns)
            {
                input = pattern.Replace(input, match => new string(_censorChar, match.Length));
            }
            return input;
        }

        private static string ChannelKey(ChatChannelType type, string id) => $"{type}|{id}";

        /// <summary>Retourne l'état d'un canal (history + unread).</summary>
        public ChatChannelState GetChannelState(ChatChannelType type, string channelId)
        {
            return _channels.TryGetValue(ChannelKey(type, channelId), out var s) ? s : null;
        }

        /// <summary>Marque un canal comme lu (remet unread à 0).</summary>
        public void MarkRead(ChatChannelType type, string channelId)
        {
            var key = ChannelKey(type, channelId);
            if (_channels.TryGetValue(key, out var s)) s.UnreadCount = 0;
        }
    }
}
