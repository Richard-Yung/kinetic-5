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
    /// Guilde / Crew.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/CrewScreen")]
    [DisallowMultipleComponent]
    public sealed class CrewScreen : UIScreen
    {
        [Header("Infos Crew")]
        [Tooltip("Texte nom du crew.")]
        [SerializeField] private TMP_Text _crewNameText;
        [Tooltip("Texte level du crew.")]
        [SerializeField] private TMP_Text _crewLevelText;
        [Tooltip("Texte membres (X/Y).")]
        [SerializeField] private TMP_Text _crewMembersText;
        [Tooltip("Texte description du crew.")]
        [SerializeField] private TMP_Text _crewDescriptionText;

        [Header("Membres")]
        [Tooltip("Conteneur de la liste des membres.")]
        [SerializeField] private RectTransform _membersContainer;
        [Tooltip("Prefab d'une ligne membre.")]
        [SerializeField] private GameObject _memberRowPrefab;

        [Header("Events")]
        [Tooltip("Conteneur des events crew.")]
        [SerializeField] private RectTransform _eventsContainer;
        [Tooltip("Prefab d'une ligne event crew.")]
        [SerializeField] private GameObject _eventRowPrefab;

        [Header("Actions")]
        [Tooltip("Bouton JOIN (si pas de crew).")]
        [SerializeField] private KButton _joinButton;
        [Tooltip("Bouton LEAVE (si déjà dans un crew).")]
        [SerializeField] private KButton _leaveButton;
        [Tooltip("Bouton CREATE.")]
        [SerializeField] private KButton _createButton;
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;

        private readonly List<GameObject> _rows = new(64);

        protected override void Awake()
        {
            _screenType = ScreenType.Crew;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_crewNameText != null)
            {
                _crewNameText.text = "VOID WARDENS";
                _crewNameText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _crewNameText.color = ThemeManager.Main;
            }
            if (_crewLevelText != null) { _crewLevelText.text = "LVL 18"; _crewLevelText.color = ThemeManager.SubYellow; }
            if (_crewMembersText != null) { _crewMembersText.text = "12/30"; _crewMembersText.color = ThemeManager.White; }
            if (_crewDescriptionText != null) { _crewDescriptionText.text = "Elite operatives protecting the frontier."; _crewDescriptionText.color = ThemeManager.TextMuted; }

            if (_joinButton != null)
            {
                _joinButton.SetLocalizationKey("crew.join", "JOIN");
                _joinButton.OnKClick += _ => OnJoin();
            }
            if (_leaveButton != null)
            {
                _leaveButton.SetLocalizationKey("crew.leave", "LEAVE");
                _leaveButton.OnKClick += _ => OnLeave();
            }
            if (_createButton != null)
            {
                _createButton.SetLocalizationKey("crew.create", "CREATE");
                _createButton.OnKClick += _ => OnCreate();
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            BuildMembers();
            BuildEvents();
            TrackClick("crew_show");
        }

        protected override void OnHide()
        {
            foreach (var r in _rows) { if (r != null) Destroy(r); }
            _rows.Clear();
        }

        private void BuildMembers()
        {
            if (_membersContainer == null || _memberRowPrefab == null) return;
            string[] names = { "OPERATIVE-001", "REAPER-7", "GHOST-9", "VOLT-X", "ECHO-3" };
            for (int i = 0; i < names.Length; i++)
            {
                var row = Instantiate(_memberRowPrefab, _membersContainer);
                _rows.Add(row);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 3)
                {
                    texts[0].text = names[i];
                    texts[0].color = ThemeManager.White;
                    texts[1].text = i == 0 ? "LEADER" : "MEMBER";
                    texts[1].color = i == 0 ? ThemeManager.SubYellow : ThemeManager.TextMuted;
                    texts[2].text = $"LVL {40 + i * 3}";
                    texts[2].color = ThemeManager.Main;
                }
            }
        }

        private void BuildEvents()
        {
            if (_eventsContainer == null || _eventRowPrefab == null) return;
            string[] events = { "Crew War starts in 2d", "Weekly objectives reset in 18h", "New member joined: VOLT-X" };
            foreach (var e in events)
            {
                var row = Instantiate(_eventRowPrefab, _eventsContainer);
                _rows.Add(row);
                var txt = row.GetComponentInChildren<TMP_Text>();
                if (txt != null) { txt.text = e; txt.color = ThemeManager.White; }
            }
        }

        private void OnJoin() { TrackClick("crew_join"); TelemetryLogger.Instance?.Track("crew_join", new() { { "crew_id", _crewNameText?.text ?? "" } }); }
        private void OnLeave() { TrackClick("crew_leave"); }
        private void OnCreate() { TrackClick("crew_create"); }

        private void OnBack()
        {
            TrackClick("crew_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }
    }
}
