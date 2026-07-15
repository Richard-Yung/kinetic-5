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
    /// Résumé de fin de mission (Operation Summary) — PDF page 8.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 8</b> :
    /// <list type="bullet">
    /// <item>Multi-colonnes :</item>
    /// <item>- MISSION OBJECTIVES : PRIMARY: NEURAL CORE SECURED +4500 XP,
    /// SPEC-OPS CLEAR +2500 XP, TACTICAL: STEALTH DATA RECOVERY +XP.</item>
    /// <item>- REWARDS EARNED : CONTRACT COMPLETION BONUS +3200 CR,
    /// COMBAT PERFORMANCE +1800 CR, FOUND TECH SCRAPS +45 CR.</item>
    /// <item>Level up : "LEVEL 47 +7000 XP".</item>
    /// <item>Bouton OK.</item>
    /// <item>Animation de remplissage de la barre XP.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/OperationSummaryScreen")]
    [DisallowMultipleComponent]
    public sealed class OperationSummaryScreen : UIScreen
    {
        [Header("Titre")]
        [Tooltip("Texte 'OPERATION SUMMARY' (Audiowide).")]
        [SerializeField] private TMP_Text _titleText;
        [Tooltip("Texte nom de la mission.")]
        [SerializeField] private TMP_Text _missionNameText;
        [Tooltip("Texte statut (SUCCESS / FAILED).")]
        [SerializeField] private TMP_Text _statusText;

        [Header("Colonnes")]
        [Tooltip("Conteneur des objectifs de mission.")]
        [SerializeField] private RectTransform _objectivesContainer;
        [Tooltip("Conteneur des récompenses gagnées.")]
        [SerializeField] private RectTransform _rewardsContainer;
        [Tooltip("Prefab d'une ligne objectif (label + XP).")]
        [SerializeField] private GameObject _objectiveRowPrefab;
        [Tooltip("Prefab d'une ligne récompense (label + CR).")]
        [SerializeField] private GameObject _rewardRowPrefab;

        [Header("Level up")]
        [Tooltip("Texte 'LEVEL 47 +7000 XP'.")]
        [SerializeField] private TMP_Text _levelUpText;
        [Tooltip("Barre XP segmentée (animation de remplissage).")]
        [SerializeField] private KProgressBar _xpBar;
        [Tooltip("Texte niveau courant.")]
        [SerializeField] private TMP_Text _currentLevelText;
        [Tooltip("Texte niveau suivant.")]
        [SerializeField] private TMP_Text _nextLevelText;

        [Header("Actions")]
        [Tooltip("Bouton OK.")]
        [SerializeField] private KButton _okButton;

        /// <summary>Ligne d'objectif affichée.</summary>
        [System.Serializable]
        public struct ObjectiveRow
        {
            public string Tier; // PRIMARY, SPEC-OPS, TACTICAL.
            public string Label;
            public int Xp;
        }

        /// <summary>Ligne de récompense affichée.</summary>
        [System.Serializable]
        public struct RewardRow
        {
            public string Label;
            public int Cr;
        }

        /// <summary>Payload de l'écran de résumé.</summary>
        public struct SummaryPayload
        {
            public string MissionId;
            public bool Success;
            public List<ObjectiveRow> Objectives;
            public List<RewardRow> Rewards;
            public int PreviousLevel;
            public int NewLevel;
            public int XpGained;
            public int TotalXpBefore;
            public int TotalXpAfter;
        }

        private SummaryPayload _payload;
        private readonly List<GameObject> _objectiveRows = new(8);
        private readonly List<GameObject> _rewardRows = new(8);

        protected override void Awake()
        {
            _screenType = ScreenType.OperationSummary;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_titleText != null)
            {
                _titleText.text = "OPERATION SUMMARY";
                _titleText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _titleText.color = ThemeManager.Main;
            }
            if (_okButton != null)
            {
                _okButton.SetLocalizationKey("summary.ok", "OK");
                _okButton.OnKClick += _ => OnOk();
            }
        }

        protected override void OnShow(object payload)
        {
            if (payload is SummaryPayload sp) _payload = sp;
            else
            {
                // Construit un payload par défaut depuis la mission.
                _payload = BuildDefaultPayload();
            }
            ApplySummary();
            TrackClick("summary_show");
        }

        protected override void OnHide()
        {
            ClearRows();
        }

        // =================================================================================
        //  AFFICHAGE
        // =================================================================================

        private void ApplySummary()
        {
            var mission = DataLoader.GetMission(_payload.MissionId);
            if (_missionNameText != null)
            {
                _missionNameText.text = mission != null ? mission.DisplayName : _payload.MissionId;
                _missionNameText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _missionNameText.color = ThemeManager.White;
            }
            if (_statusText != null)
            {
                _statusText.text = _payload.Success ? "SUCCESS" : "FAILED";
                _statusText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _statusText.color = _payload.Success ? ThemeManager.SubGreen : ThemeManager.SubRed;
            }

            BuildObjectiveRows();
            BuildRewardRows();
            AnimateXpBar();
        }

        private void BuildObjectiveRows()
        {
            ClearObjectiveRows();
            if (_objectivesContainer == null || _objectiveRowPrefab == null) return;
            foreach (var obj in _payload.Objectives)
            {
                var row = Instantiate(_objectiveRowPrefab, _objectivesContainer);
                _objectiveRows.Add(row);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 3)
                {
                    texts[0].text = obj.Tier;
                    texts[0].font = ThemeManager.Instance.GetFont(FontRole.Mono);
                    texts[0].color = ThemeManager.Main;
                    texts[1].text = obj.Label;
                    texts[1].font = ThemeManager.Instance.GetFont(FontRole.Body);
                    texts[1].color = ThemeManager.White;
                    texts[2].text = $"+{obj.Xp} XP";
                    texts[2].font = ThemeManager.Instance.GetFont(FontRole.Display);
                    texts[2].color = ThemeManager.XpPurple;
                }
            }
        }

        private void BuildRewardRows()
        {
            ClearRewardRows();
            if (_rewardsContainer == null || _rewardRowPrefab == null) return;
            foreach (var r in _payload.Rewards)
            {
                var row = Instantiate(_rewardRowPrefab, _rewardsContainer);
                _rewardRows.Add(row);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 2)
                {
                    texts[0].text = r.Label;
                    texts[0].font = ThemeManager.Instance.GetFont(FontRole.Body);
                    texts[0].color = ThemeManager.White;
                    texts[1].text = $"+{r.Cr} CR";
                    texts[1].font = ThemeManager.Instance.GetFont(FontRole.Display);
                    texts[1].color = ThemeManager.SubYellow;
                }
            }
        }

        private void AnimateXpBar()
        {
            if (_xpBar == null) return;
            // Calcule la range du niveau courant.
            int xpForCurrent = DataLoader.GetXpRequiredForLevel(_payload.PreviousLevel);
            int xpForNext = DataLoader.GetXpRequiredForLevel(_payload.NewLevel + 1);
            _xpBar.SetType(StatBarType.Xp);
            _xpBar.SetRange(0f, Mathf.Max(1f, xpForNext - xpForCurrent));
            _xpBar.Value = 0f;

            if (_currentLevelText != null)
            {
                _currentLevelText.text = _payload.PreviousLevel.ToString();
                _currentLevelText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _currentLevelText.color = ThemeManager.White;
            }
            if (_nextLevelText != null)
            {
                _nextLevelText.text = (_payload.NewLevel + 1).ToString();
                _nextLevelText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _nextLevelText.color = ThemeManager.TextMuted;
            }
            if (_levelUpText != null)
            {
                _levelUpText.text = $"LEVEL {_payload.NewLevel}   +{_payload.XpGained} XP";
                _levelUpText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _levelUpText.color = ThemeManager.XpPurple;
                _levelUpText.transform.localScale = Vector3.zero;
                _levelUpText.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true).SetDelay(1.2f);
            }

            // Anime le remplissage progressif.
            float target = Mathf.Clamp01((_payload.TotalXpAfter - xpForCurrent) / Mathf.Max(1f, xpForNext - xpForCurrent));
            DOVirtual.DelayedCall(0.5f, () =>
            {
                if (_xpBar != null) _xpBar.Value = target * (xpForNext - xpForCurrent);
            }).SetUpdate(true);
        }

        // =================================================================================
        //  ACTIONS
        // =================================================================================

        private void OnOk()
        {
            TrackClick("summary_ok");
            _ = GameManager.Instance?.ReturnToMainMenu();
        }

        public override bool HandleBack()
        {
            OnOk();
            return true;
        }

        // =================================================================================
        //  HELPERS
        // =================================================================================

        private SummaryPayload BuildDefaultPayload()
        {
            var objectives = new List<ObjectiveRow>
            {
                new() { Tier = "PRIMARY", Label = "NEURAL CORE SECURED", Xp = 4500 },
                new() { Tier = "SPEC-OPS", Label = "SPEC-OPS CLEAR", Xp = 2500 },
                new() { Tier = "TACTICAL", Label = "STEALTH DATA RECOVERY", Xp = 1500 },
            };
            var rewards = new List<RewardRow>
            {
                new() { Label = "CONTRACT COMPLETION BONUS", Cr = 3200 },
                new() { Label = "COMBAT PERFORMANCE", Cr = 1800 },
                new() { Label = "FOUND TECH SCRAPS", Cr = 45 },
            };
            return new SummaryPayload
            {
                MissionId = "shadow_fall",
                Success = true,
                Objectives = objectives,
                Rewards = rewards,
                PreviousLevel = 46,
                NewLevel = 47,
                XpGained = 7000,
                TotalXpBefore = 0,
                TotalXpAfter = 7000,
            };
        }

        private void ClearRows()
        {
            ClearObjectiveRows();
            ClearRewardRows();
        }

        private void ClearObjectiveRows()
        {
            foreach (var r in _objectiveRows) { if (r != null) Destroy(r); }
            _objectiveRows.Clear();
        }

        private void ClearRewardRows()
        {
            foreach (var r in _rewardRows) { if (r != null) Destroy(r); }
            _rewardRows.Clear();
        }
    }
}
