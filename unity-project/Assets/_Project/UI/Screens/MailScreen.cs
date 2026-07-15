using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Boîte de réception (mails + cadeaux) — écran additionnel.
    /// </summary>
    /// <remarks>
    /// Mails système, récompenses, cadeaux. Read/unread. Claim attachment.
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/MailScreen")]
    [DisallowMultipleComponent]
    public sealed class MailScreen : UIScreen
    {
        [Header("Liste")]
        [Tooltip("Conteneur de la liste de mails (vertical).")]
        [SerializeField] private RectTransform _mailListContainer;
        [Tooltip("Prefab d'une ligne de mail.")]
        [SerializeField] private GameObject _mailEntryPrefab;
        [Tooltip("Nombre max de lignes.")]
        [SerializeField] private int _maxEntries = 50;

        [Header("Détails")]
        [Tooltip("Panneau de détails du mail sélectionné.")]
        [SerializeField] private RectTransform _detailsPanel;
        [Tooltip("Texte expéditeur.")]
        [SerializeField] private TMP_Text _senderText;
        [Tooltip("Texte sujet.")]
        [SerializeField] private TMP_Text _subjectText;
        [Tooltip("Texte corps.")]
        [SerializeField] private TMP_Text _bodyText;
        [Tooltip("Bouton CLAIM (claim attachment).")]
        [SerializeField] private KButton _claimButton;
        [Tooltip("Bouton DELETE.")]
        [SerializeField] private KButton _deleteButton;

        [Header("Navigation")]
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;
        [Tooltip("Badge compteur unread.")]
        [SerializeField] private TMP_Text _unreadCountText;

        private readonly List<GameObject> _entries = new(32);
        private string _selectedMailId;

        protected override void Awake()
        {
            _screenType = ScreenType.Mail;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_claimButton != null)
            {
                _claimButton.SetLocalizationKey("mail.claim", "CLAIM");
                _claimButton.OnKClick += _ => OnClaim();
            }
            if (_deleteButton != null)
            {
                _deleteButton.SetLocalizationKey("mail.delete", "DELETE");
                _deleteButton.OnKClick += _ => OnDelete();
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            BuildMailList();
            if (_detailsPanel != null) _detailsPanel.gameObject.SetActive(false);
            TrackClick("mail_show");
        }

        protected override void OnHide()
        {
            ClearEntries();
        }

        // =================================================================================
        //  MAIL LIST
        // =================================================================================

        private void BuildMailList()
        {
            ClearEntries();
            if (_mailListContainer == null || _mailEntryPrefab == null) return;
            // Placeholders : en production, alimenté par une backend mailbox (Nakama).
            for (int i = 0; i < 8; i++)
            {
                var entry = Instantiate(_mailEntryPrefab, _mailListContainer);
                _entries.Add(entry);
                var texts = entry.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 3)
                {
                    texts[0].text = i == 0 ? "COMMAND" : "SYSTEM";
                    texts[1].text = i == 0 ? "Daily Reward Available" : $"Mission Update #{i}";
                    texts[2].text = i == 0 ? "NEW" : "";
                    if (i == 0)
                    {
                        texts[0].color = ThemeManager.SubYellow;
                        texts[2].color = ThemeManager.SubRed;
                    }
                    else
                    {
                        texts[0].color = ThemeManager.TextMuted;
                        texts[2].color = ThemeManager.TextMuted;
                    }
                }
                var btn = entry.GetComponentInChildren<KButton>();
                if (btn != null)
                {
                    int idx = i;
                    btn.OnKClick += _ => SelectMail(idx);
                }
            }
            UpdateUnreadCount();
        }

        private void SelectMail(int index)
        {
            if (index < 0 || index >= _entries.Count) return;
            _selectedMailId = index.ToString();
            if (_detailsPanel != null) _detailsPanel.gameObject.SetActive(true);
            if (_senderText != null)
            {
                _senderText.text = index == 0 ? "COMMAND" : "SYSTEM";
                _senderText.color = ThemeManager.Main;
            }
            if (_subjectText != null)
            {
                _subjectText.text = index == 0 ? "Daily Reward Available" : $"Mission Update #{index}";
                _subjectText.color = ThemeManager.White;
            }
            if (_bodyText != null)
            {
                _bodyText.text = "Your daily login reward is ready to be claimed. Visit the Daily Login screen or click CLAIM below.";
                _bodyText.color = ThemeManager.White;
            }
            TrackClick($"mail_select_{index}");
        }

        private void UpdateUnreadCount()
        {
            if (_unreadCountText != null)
            {
                _unreadCountText.text = _entries.Count > 0 ? "1" : "0";
                _unreadCountText.color = ThemeManager.SubRed;
            }
        }

        // =================================================================================
        //  ACTIONS
        // =================================================================================

        private void OnClaim()
        {
            if (string.IsNullOrEmpty(_selectedMailId)) return;
            TrackClick($"mail_claim_{_selectedMailId}");
            TelemetryLogger.Instance?.Track("mail_claim", new() { { "mail_id", _selectedMailId } });
        }

        private void OnDelete()
        {
            if (string.IsNullOrEmpty(_selectedMailId)) return;
            TrackClick($"mail_delete_{_selectedMailId}");
            // Supprime de la liste.
            if (int.TryParse(_selectedMailId, out int idx) && idx >= 0 && idx < _entries.Count)
            {
                Destroy(_entries[idx]);
                _entries.RemoveAt(idx);
            }
            if (_detailsPanel != null) _detailsPanel.gameObject.SetActive(false);
        }

        private void OnBack()
        {
            TrackClick("mail_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }

        private void ClearEntries()
        {
            foreach (var e in _entries) { if (e != null) Destroy(e); }
            _entries.Clear();
        }
    }
}
