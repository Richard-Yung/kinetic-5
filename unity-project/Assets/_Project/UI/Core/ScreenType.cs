namespace KINETICS5.UI
{
    /// <summary>
    /// Liste exhaustive des écrans gérés par <see cref="UIManager"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// L'ordre de l'enum correspond à l'ordre logique de navigation ; les
    /// écrans modaux (Settings, Pause, Inventory...) sont volontairement
    /// regroupés à la fin.
    /// </para>
    /// <para>
    /// <b>8 écrans PDF + 19 écrans additionnels</b> (production-ready) = 27
    /// écrans au total, plus Boot/Loading utilitaires.
    /// </para>
    /// </remarks>
    public enum ScreenType
    {
        /// <summary>Écran de boot (splash éditeur + initialisation).</summary>
        Boot,
        /// <summary>Menu principal (PDF page 2).</summary>
        Start,
        /// <summary>Chargement de mission (PDF page 2).</summary>
        Loading,
        /// <summary>Lobby / hub central (PDF page 4).</summary>
        Lobby,
        /// <summary>Sélection agent + loadout (PDF page 4).</summary>
        Loadout,
        /// <summary>Sélection d'arme (PDF page 5).</summary>
        Armory,
        /// <summary>HUD de combat (PDF page 6).</summary>
        HUD,
        /// <summary>Pause en mission.</summary>
        Pause,
        /// <summary>Écran de victoire (PDF page 7).</summary>
        Victory,
        /// <summary>Écran de défaite (PDF page 7).</summary>
        Defeat,
        /// <summary>Résumé de fin de mission (PDF page 8).</summary>
        OperationSummary,
        /// <summary>Paramètres (PDF page 7-8).</summary>
        Settings,

        // --- Écrans additionnels (production-ready, raisonnés du genre) ---

        /// <summary>Inventaire joueur (grille tri/filtre).</summary>
        Inventory,
        /// <summary>Boutique (CR + premium).</summary>
        Shop,
        /// <summary>Boîte de réception (mails + cadeaux).</summary>
        Mail,
        /// <summary>Pass de combat (50 paliers, free/premium).</summary>
        BattlePass,
        /// <summary>Profil joueur (stats + maîtrise agents).</summary>
        Profile,
        /// <summary>Classement global/amis/crew.</summary>
        Leaderboard,
        /// <summary>Guilde / crew.</summary>
        Crew,
        /// <summary>Sélection de mission (carte/liste).</summary>
        MissionSelect,
        /// <summary>Tutoriel interactif (overlay).</summary>
        Tutorial,
        /// <summary>Onboarding première session.</summary>
        Onboarding,
        /// <summary>Générique de fin (scrolling).</summary>
        Credits,
        /// <summary>Base de données lore (agents, ennemis, régions).</summary>
        Codex,
        /// <summary>Atelier de craft / upgrade.</summary>
        Crafting,
        /// <summary>Récompense de connexion quotidienne.</summary>
        DailyLogin,
        /// <summary>Succès / achievements.</summary>
        Achievements,
        /// <summary>Carte tactique en mission.</summary>
        Map
    }
}
