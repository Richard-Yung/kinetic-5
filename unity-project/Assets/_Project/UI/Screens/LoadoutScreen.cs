using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;
using KINETICS5.Data;

namespace KINETICS5.UI
{
    /// <summary>
    /// Écran de loadout (agents + armes) — PDF page 4-5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 4-5</b> :
    /// <list type="bullet">
    /// <item>Tabs : AGENTS, PRIMARY, SECONDARY, TACTICAL.</item>
    /// <item>Cartes agents (VULCAN/XEN/JOLT/XANO) avec états unlocked/locked.</item>
    /// <item>Agent sélectionné : description, bouton FULL VIEW, panneau stats
    /// (POWER/HEALTH/SHIELD/SPEED segmented bars), bouton SAVE.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/LoadoutScreen")]
    [DisallowMultipleComponent]
    public sealed class LoadoutScreen : UIScreen
    {
        [Header("Tabs")]
        [Tooltip("Boutons de tab (AGENTS, PRIMARY, SECONDARY, TACTICAL).")]
        [SerializeField] private KButton[] _tabButtons = System.Array.Empty<KButton>();
        [Tooltip("Panneaux (un par tab).")]
        [SerializeField] private RectTransform[] _tabPanels = System.Array.Empty<RectTransform>();

        [Header("Agents - Carousel")]
        [Tooltip("Conteneur du carousel d'agents.")]
        [SerializeField] private RectTransform _agentsContainer;
        [Tooltip("Prefab de carte agent (KCard).")]
        [SerializeField] private KCard _agentCardPrefab;
        [Tooltip("CardGroup pour sélection exclusive agents.")]
        [SerializeField] private KCardGroup _agentGroup;
        [Tooltip("Niveau joueur courant (pour le déblocage des agents).")]
        [SerializeField] private int _playerLevel = 47;

        [Header("Agent sélectionné")]
        [Tooltip("Image du portrait de l'agent sélectionné.")]
        [SerializeField] private Image _selectedPortrait;
        [Tooltip("Texte nom de l'agent sélectionné.")]
        [SerializeField] private TMP_Text _selectedNameText;
        [Tooltip("Texte description de l'agent sélectionné.")]
        [SerializeField] private TMP_Text _selectedDescriptionText;
        [Tooltip("Texte motto de l'agent sélectionné.")]
        [SerializeField] private TMP_Text _selectedMottoText;
        [Tooltip("Bouton FULL VIEW (vue détaillée 3D).")]
        [SerializeField] private KButton _fullViewButton;
        [Tooltip("Barres stats agent sélectionné.")]
        [SerializeField] private KProgressBar _powerBar;
        [SerializeField] private KProgressBar _healthBar;
        [SerializeField] private KProgressBar _shieldBar;
        [SerializeField] private KProgressBar _speedBar;
        [Tooltip("Bouton SAVE (sauvegarde du loadout).")]
        [SerializeField] private KButton _saveButton;

        [Header("Armes - Listes")]
        [Tooltip("Conteneur des cartes primaires.")]
        [SerializeField] private RectTransform _primaryContainer;
        [Tooltip("Conteneur des cartes secondaires.")]
        [SerializeField] private RectTransform _secondaryContainer;
        [Tooltip("Conteneur des cartes tactiques.")]
        [SerializeField] private RectTransform _tacticalContainer;
        [Tooltip("Prefab carte arme.")]
        [SerializeField] private KCard _weaponCardPrefab;

        [Header("Retour")]
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;

        private int _currentTabIndex;
        private string _selectedAgentId;
        private readonly List<KCard> _agentCards = new(8);
        private readonly List<KCard> _weaponCards = new(16);

        protected override void Awake()
        {
            _screenType = ScreenType.Loadout;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();

            // Tabs.
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int idx = i; // closure capture.
                var labelKey = idx switch
                {
                    0 => "loadout.tab.agents",
                    1 => "loadout.tab.primary",
                    2 => "loadout.tab.secondary",
                    3 => "loadout.tab.tactical",
                    _ => "loadout.tab.agents"
                };
                var fallback = idx switch
                {
                    0 => "AGENTS",
                    1 => "PRIMARY",
                    2 => "SECONDARY",
                    3 => "TACTICAL",
                    _ => "AGENTS"
                };
                _tabButtons[i].SetLocalizationKey(labelKey, fallback);
                _tabButtons[i].OnKClick += _ => SelectTab(idx);
            }

            // Full view + Save + Back.
            if (_fullViewButton != null)
            {
                _fullViewButton.SetLocalizationKey("loadout.full_view", "FULL VIEW");
                _fullViewButton.OnKClick += _ => OnFullView();
            }
            if (_saveButton != null)
            {
                _saveButton.SetLocalizationKey("common.save", "SAVE");
                _saveButton.OnKClick += _ => OnSave();
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }

            // Card groups : sélection exclusive.
            if (_agentGroup == null && _agentsContainer != null)
            {
                var grp = _agentsContainer.GetComponent<KCardGroup>();
                if (grp == null) grp = _agentsContainer.gameObject.AddComponent<KCardGroup>();
                _agentGroup = grp;
            }
        }

        protected override void OnShow(object payload)
        {
            BuildAgentCards();
            BuildWeaponCards();
            SelectTab(0);
            // Sélectionne le premier agent débloqué.
            if (_agentCards.Count > 0) _agentCards[0].OnCardClicked?.Invoke(_agentCards[0]);
            TrackClick("loadout_show");
        }

        protected override void OnHide()
        {
            // Libère les cartes instanciées.
            ClearCards(_agentCards);
            ClearCards(_weaponCards);
        }

        // =================================================================================
        //  TABS
        // =================================================================================

        private void SelectTab(int index)
        {
            if (index < 0 || index >= _tabPanels.Length) return;
            _currentTabIndex = index;
            for (int i = 0; i < _tabPanels.Length; i++)
            {
                if (_tabPanels[i] == null) continue;
                _tabPanels[i].gameObject.SetActive(i == index);
            }
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                _tabButtons[i].SetSelected(i == index);
            }
            TrackClick($"loadout_tab_{index}");
        }

        // =================================================================================
        //  AGENTS
        // =================================================================================

        private void BuildAgentCards()
        {
            ClearCards(_agentCards);
            if (_agentsContainer == null || _agentCardPrefab == null) return;

            var agents = DataLoader.GetAllAgents();
            foreach (var agent in agents)
            {
                var card = Instantiate(_agentCardPrefab, _agentsContainer);
                bool locked = agent.UnlockLevel > _playerLevel;
                card.Bind(agent, locked);
                card.OnCardClicked += OnAgentCardClicked;
                _agentCards.Add(card);
                _agentGroup?.Register(card);
            }
        }

        private void OnAgentCardClicked(KCard card)
        {
            if (card == null || card.IsLocked) return;
            _selectedAgentId = card.ItemId;
            var agent = DataLoader.GetAgent(_selectedAgentId);
            if (agent == null) return;
            DisplaySelectedAgent(agent);
        }

        private void DisplaySelectedAgent(AgentDto agent)
        {
            if (_selectedNameText != null)
            {
                _selectedNameText.text = agent.DisplayName;
                _selectedNameText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _selectedNameText.color = ThemeManager.Main;
            }
            if (_selectedDescriptionText != null)
            {
                _selectedDescriptionText.text = agent.Description;
                _selectedDescriptionText.font = ThemeManager.Instance.GetFont(FontRole.Body);
                _selectedDescriptionText.color = ThemeManager.White;
            }
            if (_selectedMottoText != null)
            {
                _selectedMottoText.text = $"\"{agent.Motto}\"";
                _selectedMottoText.font = ThemeManager.Instance.GetFont(FontRole.Body);
                _selectedMottoText.color = ThemeManager.SubYellow;
            }
            ConfigureStatBar(_powerBar, StatBarType.Power, agent.BasePower, 5000);
            ConfigureStatBar(_healthBar, StatBarType.Health, agent.BaseHealth, 10000);
            ConfigureStatBar(_shieldBar, StatBarType.Shield, agent.BaseShield, 5000);
            ConfigureStatBar(_speedBar, StatBarType.Speed, agent.BaseSpeed * 100f, 300f);
        }

        // =================================================================================
        //  ARMES
        // =================================================================================

        private void BuildWeaponCards()
        {
            ClearCards(_weaponCards);
            // Primaires.
            BuildWeaponCardsForCategory(WeaponCategory.Primary, _primaryContainer);
            // Secondaires.
            BuildWeaponCardsForCategory(WeaponCategory.Secondary, _secondaryContainer);
            // Tactiques.
            BuildWeaponCardsForCategory(WeaponCategory.Tactical, _tacticalContainer);
        }

        private void BuildWeaponCardsForCategory(WeaponCategory category, RectTransform container)
        {
            if (container == null || _weaponCardPrefab == null) return;
            var weapons = DataLoader.GetWeaponsByCategory(category);
            foreach (var w in weapons)
            {
                var card = Instantiate(_weaponCardPrefab, container);
                bool locked = false; // Critère de déblocage à brancher sur la progression joueur.
                card.Bind(w, locked);
                card.OnCardClicked += OnWeaponCardClicked;
                _weaponCards.Add(card);
            }
        }

        private void OnWeaponCardClicked(KCard card)
        {
            if (card == null || card.IsLocked) return;
            TrackClick($"loadout_weapon_{card.ItemId}");
            // Extension future : déclencher l'aperçu 3D de l'arme via ArmoryScreen ou un panneau dédié.
        }

        // =================================================================================
        //  ACTIONS
        // =================================================================================

        private void OnFullView()
        {
            TrackClick("loadout_full_view");
            // Ouvre une modal ou un écran de vue 3D détaillée.
            // Pour l'instant : feedback simple (placeholder intentionnel, extension future).
        }

        private void OnSave()
        {
            TrackClick("loadout_save");
            var save = ServiceLocator.Instance?.Get<SaveSystem>();
            if (save != null)
            {
                // SaveData doit exposer le loadout ; extension future.
                save.MarkDirty();
            }
        }

        private void OnBack()
        {
            TrackClick("loadout_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack()
        {
            OnBack();
            return true;
        }

        // =================================================================================
        //  HELPERS
        // =================================================================================

        private void ConfigureStatBar(KProgressBar bar, StatBarType type, float value, float max)
        {
            if (bar == null) return;
            bar.SetType(type);
            bar.SetRange(0f, max);
            bar.Value = value;
        }

        private void ClearCards(List<KCard> cards)
        {
            foreach (var c in cards)
            {
                if (c == null) continue;
                _agentGroup?.Unregister(c);
                c.OnCardClicked -= OnAgentCardClicked;
                c.OnCardClicked -= OnWeaponCardClicked;
                Destroy(c.gameObject);
            }
            cards.Clear();
        }
    }
}
