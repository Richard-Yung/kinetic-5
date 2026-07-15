using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Couleurs thématiques KINETICS 5 exposées via <see cref="ThemeManager"/>.
    /// Tous les composants UI doivent récupérer leurs couleurs par cette énumération
    /// afin de garantir la cohérence stricte avec la palette du PDF page 8.
    /// </summary>
    public enum ThemeColor
    {
        /// <summary>Couleur principale cyan #1AA1CE.</summary>
        Main,
        /// <summary>Vert néon #6CF42E (santé, succès).</summary>
        SubGreen,
        /// <summary>Jaune #FFE735 (avertissements, éléments rares).</summary>
        SubYellow,
        /// <summary>Rouge #FE0022 (danger, échec).</summary>
        SubRed,
        /// <summary>Bleu nuit foncé #10204B (panneaux).</summary>
        DarkBlue,
        /// <summary>Blanc #FFFFFF (texte primaire).</summary>
        White,
        /// <summary>Fond espace profond #05060F.</summary>
        BackgroundDeep,
        /// <summary>Fond alternatif #020207.</summary>
        BackgroundVoid,
        /// <summary>Gris Foncé pour surfaces UI (dérivé).</summary>
        Surface,
        /// <summary>Gris clair pour texte secondaire (dérivé).</summary>
        TextMuted,
        /// <summary>Cyan atténué pour bordures (dérivé).</summary>
        BorderCyan,
        /// <summary>Violet pour XP (extension palette gameplay).</summary>
        XpPurple,
        /// <summary>Orange pour vitesse (extension palette gameplay).</summary>
        SpeedOrange,
        /// <summary>Rareté commune (gris).</summary>
        RarityCommon,
        /// <summary>Rareté rare (cyan).</summary>
        RarityRare,
        /// <summary>Rareté épique (violet).</summary>
        RarityEpic,
        /// <summary>Rareté légendaire (jaune).</summary>
        RarityLegendary,
        /// <summary>Overlay sombre pour modales (80% noir).</summary>
        Backdrop
    }

    /// <summary>
    /// Rôle typographique. Tous les composants UI récupèrent la police
    /// correspondante via <see cref="ThemeManager.GetFont(FontRole)"/>.
    /// </summary>
    public enum FontRole
    {
        /// <summary>Titres, boutons et chiffres (Audiowide).</summary>
        Display,
        /// <summary>Corps de texte (Rajdhani).</summary>
        Body,
        /// <summary>Données chiffrées, codes, journaux (JetBrains Mono).</summary>
        Mono
    }

    /// <summary>
    /// Registre statique central des couleurs et polices KINETICS 5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Palette NON-NÉGOCIABLE</b> (PDF page 8) :
    /// cyan <c>#1AA1CE</c>, vert <c>#6CF42E</c>, jaune <c>#FFE735</c>,
    /// rouge <c>#FE0022</c>, bleu nuit <c>#10204B</c>, blanc <c>#FFFFFF</c>,
    /// fonds <c>#05060F</c> / <c>#020207</c>.
    /// </para>
    /// <para>
    /// Aucune couleur hex ne doit être codée en dur ailleurs que dans ce fichier.
    /// Tout composant UI utilise <see cref="GetColor(ThemeColor)"/> ou
    /// <see cref="Apply(Graphic, ThemeColor)"/>.
    /// </para>
    /// <para>
    /// Les polices (TMP_FontAsset) sont des assets assignés une fois via
    /// <see cref="Instance"/> (asset ScriptableObject optionnel) ; en fallback
    /// on utilise <c>TMP_Settings.defaultFontAsset</c> pour rester fonctionnel
    /// sans asset dédié.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ThemeManager : MonoBehaviour
    {
        // =================================================================================
        //  PALETTE — valeurs hex brutes (UNIQUE source de vérité pour les couleurs)
        // =================================================================================

        /// <summary>Cyan principal #1AA1CE.</summary>
        public static readonly Color Main = Hex("#1AA1CE");
        /// <summary>Vert néon #6CF42E.</summary>
        public static readonly Color SubGreen = Hex("#6CF42E");
        /// <summary>Jaune #FFE735.</summary>
        public static readonly Color SubYellow = Hex("#FFE735");
        /// <summary>Rouge #FE0022.</summary>
        public static readonly Color SubRed = Hex("#FE0022");
        /// <summary>Bleu nuit foncé #10204B.</summary>
        public static readonly Color DarkBlue = Hex("#10204B");
        /// <summary>Blanc #FFFFFF.</summary>
        public static readonly Color White = Hex("#FFFFFF");
        /// <summary>Fond deep space #05060F.</summary>
        public static readonly Color BackgroundDeep = Hex("#05060F");
        /// <summary>Fond vide absolu #020207.</summary>
        public static readonly Color BackgroundVoid = Hex("#020207");
        /// <summary>Surface gris-bleu (panneaux secondaires) — dérivé de DarkBlue.</summary>
        public static readonly Color Surface = Hex("#0E1A3A");
        /// <summary>Texte secondaire (cyan-gris) — dérivé.</summary>
        public static readonly Color TextMuted = Hex("#7E9DC4");
        /// <summary>Bordure cyan atténuée — dérivé.</summary>
        public static readonly Color BorderCyan = Hex("#2C7AA0");
        /// <summary>Violet XP (extension gameplay) — dérivé.</summary>
        public static readonly Color XpPurple = Hex("#9B4DFF");
        /// <summary>Orange vitesse (extension gameplay) — dérivé.</summary>
        public static readonly Color SpeedOrange = Hex("#FF8A1A");
        /// <summary>Rareté commune (gris).</summary>
        public static readonly Color RarityCommon = Hex("#9AA7B5");
        /// <summary>Rareté rare (cyan).</summary>
        public static readonly Color RarityRare = Hex("#1AA1CE");
        /// <summary>Rareté épique (violet).</summary>
        public static readonly Color RarityEpic = Hex("#9B4DFF");
        /// <summary>Rareté légendaire (jaune).</summary>
        public static readonly Color RarityLegendary = Hex("#FFE735");
        /// <summary>Overlay modale (80% noir).</summary>
        public static readonly Color Backdrop = new(0f, 0f, 0f, 0.80f);

        // =================================================================================
        //  SINGLETON
        // =================================================================================

        private static ThemeManager _instance;
        /// <summary>Instance globale (auto-créée si absente).</summary>
        public static ThemeManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[ThemeManager]");
                _instance = go.AddComponent<ThemeManager>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        [Header("Polices (TMP_FontAsset)")]
        [Tooltip("Police Audiowide pour titres, boutons et chiffres.")]
        [SerializeField] private TMP_FontAsset _displayFont;
        [Tooltip("Police Rajdhani pour le corps de texte.")]
        [SerializeField] private TMP_FontAsset _bodyFont;
        [Tooltip("Police JetBrains Mono pour les données chiffrées.")]
        [SerializeField] private TMP_FontAsset _monoFont;
        [Tooltip("Police de fallback si une police spécifique n'est pas assignée.")]
        [SerializeField] private TMP_FontAsset _fallbackFont;

        /// <summary>Vrai si le thème a été initialisé.</summary>
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            IsInitialized = true;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // =================================================================================
        //  API PUBLIQUE
        // =================================================================================

        /// <summary>
        /// Retourne la couleur normalisée (0..1) pour le rôle demandé.
        /// </summary>
        public static Color GetColor(ThemeColor color) => color switch
        {
            ThemeColor.Main => Main,
            ThemeColor.SubGreen => SubGreen,
            ThemeColor.SubYellow => SubYellow,
            ThemeColor.SubRed => SubRed,
            ThemeColor.DarkBlue => DarkBlue,
            ThemeColor.White => White,
            ThemeColor.BackgroundDeep => BackgroundDeep,
            ThemeColor.BackgroundVoid => BackgroundVoid,
            ThemeColor.Surface => Surface,
            ThemeColor.TextMuted => TextMuted,
            ThemeColor.BorderCyan => BorderCyan,
            ThemeColor.XpPurple => XpPurple,
            ThemeColor.SpeedOrange => SpeedOrange,
            ThemeColor.RarityCommon => RarityCommon,
            ThemeColor.RarityRare => RarityRare,
            ThemeColor.RarityEpic => RarityEpic,
            ThemeColor.RarityLegendary => RarityLegendary,
            ThemeColor.Backdrop => Backdrop,
            _ => White
        };

        /// <summary>Retourne la couleur hex d'un rôle (ex: <c>#1AA1CE</c>).</summary>
        public static string GetHex(ThemeColor color)
        {
            var c = GetColor(color);
            return ColorUtility.ToHtmlStringRGBA(c);
        }

        /// <summary>Retourne la police TMP associée au rôle.</summary>
        public TMP_FontAsset GetFont(FontRole role)
        {
            return role switch
            {
                FontRole.Display => _displayFont ? _displayFont : Fallback(),
                FontRole.Body => _bodyFont ? _bodyFont : Fallback(),
                FontRole.Mono => _monoFont ? _monoFont : Fallback(),
                _ => Fallback()
            };
        }

        /// <summary>
        /// Applique une couleur thématique à un Graphic (Image, Text, etc.).
        /// Helper compact pour les composants UI.
        /// </summary>
        public static void Apply(UnityEngine.UI.Graphic graphic, ThemeColor color)
        {
            if (graphic == null) return;
            graphic.color = GetColor(color);
        }

        /// <summary>
        /// Applique une police à un TextMeshProUGUI selon le rôle.
        /// </summary>
        public void ApplyFont(TMP_Text text, FontRole role)
        {
            if (text == null) return;
            text.font = GetFont(role);
        }

        /// <summary>
        /// Convertit une rareté en couleur thématique (bordures, gemmes).
        /// </summary>
        public static Color ColorForRarity(KINETICS5.Data.Rarity rarity) => rarity switch
        {
            KINETICS5.Data.Rarity.Common => RarityCommon,
            KINETICS5.Data.Rarity.Rare => RarityRare,
            KINETICS5.Data.Rarity.Epic => RarityEpic,
            KINETICS5.Data.Rarity.Legendary => RarityLegendary,
            _ => RarityCommon
        };

        /// <summary>
        /// Retourne la couleur associée à une barre de stat (HUD page 6).
        /// </summary>
        public static Color ColorForStatBar(StatBarType barType) => barType switch
        {
            StatBarType.Health => SubGreen,
            StatBarType.Armor => Main,
            StatBarType.Shield => Main,
            StatBarType.Xp => XpPurple,
            StatBarType.Speed => SpeedOrange,
            StatBarType.Damage => SubGreen,
            StatBarType.Power => SubYellow,
            _ => Main
        };

        // =================================================================================
        //  HELPERS PRIVÉS
        // =================================================================================

        private TMP_FontAsset Fallback()
        {
            if (_fallbackFont != null) return _fallbackFont;
            // Fallback global TMP pour rester fonctionnel sans asset dédié.
            return TMP_Settings.defaultFontAsset;
        }

        /// <summary>
        /// Convertit une chaîne hex <c>#RRGGBB</c> ou <c>#RRGGBBAA</c> en Color.
        /// Lance une exception si le format est invalide (fail-fast).
        /// </summary>
        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            Debug.LogError($"[ThemeManager] Couleur hex invalide : {hex}");
            return Color.magenta;
        }
    }

    /// <summary>
    /// Type de barre de statistique segmentée (HUD + Armory + Lobby).
    /// Détermine la couleur via <see cref="ThemeManager.ColorForStatBar"/>.
    /// </summary>
    public enum StatBarType
    {
        Health,
        Armor,
        Shield,
        Xp,
        Speed,
        Damage,
        Power,
        Ultimate
    }
}
