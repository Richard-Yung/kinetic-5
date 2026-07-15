using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace KINETICS5.Data
{
    /// <summary>
    /// Chargeur runtime statique de la couche de données KINETICS 5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Source de vérité :</b> les fichiers JSON de <c>Resources/Data/</c>
    /// (agents.json, weapons.json, missions.json, enemies.json, regions.json,
    /// progression.json). Le chargement est <b>data-driven</b> : aucune donnée
    /// de gameplay n'est codée en dur.
    /// </para>
    /// <para>
    /// <b>Boot :</b> un <c>[RuntimeInitializeOnLoadMethod]</c> déclenche
    /// <see cref="LoadAll"/> automatiquement avant la première scène. Les
    /// accesseurs appellent <see cref="EnsureLoaded"/> par sécurité (lazy-load).
    /// </para>
    /// <para>
    /// <b>Thread-safety :</b> le chargement est protégé par un verrou ; les
    /// accesseurs en lecture seule sont sans lock côté runtime Unity (thread principal).
    /// </para>
    /// <para>
    /// <b>Hot-reload éditeur :</b> le menu <i>KINETICS 5/Data/Recharger les données</i>
    /// recharge tous les JSON sans redémarrer l'éditeur.
    /// </para>
    /// </remarks>
    public static class DataLoader
    {
        /// <summary>Racine Resources des fichiers de données (sans extension).</summary>
        private const string DataRoot = "Data/";

        private static readonly object Lock = new();

        private static readonly Dictionary<string, AgentDto> Agents =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, WeaponDto> Weapons =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, MissionDto> Missions =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, EnemyDto> Enemies =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RegionDto> Regions =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, TacticalDto> Tacticals =
            new(StringComparer.OrdinalIgnoreCase);

        private static ProgressionCurveDto _progression = new();
        private static bool _isLoaded;
        private static bool _isLoading;

        // =================================================================================
        //  ÉTAT PUBLIC
        // =================================================================================

        /// <summary>Vrai si <see cref="LoadAll"/> a été exécuté avec succès.</summary>
        public static bool IsLoaded
        {
            get
            {
                lock (Lock)
                {
                    return _isLoaded;
                }
            }
        }

        /// <summary>Nombre total d'entités chargées (utile pour diagnostics).</summary>
        public static int LoadedCount
        {
            get
            {
                lock (Lock)
                {
                    return Agents.Count + Weapons.Count + Missions.Count +
                           Enemies.Count + Regions.Count + Tacticals.Count;
                }
            }
        }

        // =================================================================================
        //  CHARGEMENT
        // =================================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoLoad()
        {
            if (!_isLoaded && !_isLoading)
            {
                LoadAll();
            }
        }

        /// <summary>
        /// (Re)charge tous les fichiers JSON de <c>Resources/Data/</c> et reconstruit
        /// les caches. Idempotent et sûr à appeler plusieurs fois.
        /// </summary>
        public static void LoadAll()
        {
            lock (Lock)
            {
                if (_isLoading)
                {
                    return;
                }
                _isLoading = true;

                try
                {
                    ResetCaches();
                    JsonSerializerSettings settings = CreateSerializerSettings();

                    LoadList(Agents, LoadJson<AgentDto[]>("agents", settings), a => a.Id);
                    LoadList(Weapons, LoadJson<WeaponDto[]>("weapons", settings), w => w.Id);
                    LoadList(Missions, LoadJson<MissionDto[]>("missions", settings), m => m.Id);
                    LoadList(Enemies, LoadJson<EnemyDto[]>("enemies", settings), e => e.Id);
                    LoadList(Regions, LoadJson<RegionDto[]>("regions", settings), r => r.Id);

                    // Les objets tactiques sont dérivés des armes de catégorie Tactical
                    // (loadout unifié) — on les expose aussi via des accesseurs dédiés.
                    Tacticals.Clear();
                    foreach (var w in Weapons.Values.Where(w => w.IsTactical))
                    {
                        Tacticals[w.Id] = ToTacticalDto(w);
                    }

                    _progression = LoadJson<ProgressionCurveDto>("progression", settings) ?? new ProgressionCurveDto();

                    ValidateIntegrity();

                    _isLoaded = true;
                    Debug.Log($"[DataLoader] Données chargées : {Agents.Count} agents, " +
                              $"{Weapons.Count} armes ({Tacticals.Count} tactiques), " +
                              $"{Missions.Count} missions, {Enemies.Count} ennemis, " +
                              $"{Regions.Count} régions, " +
                              $"{_progression.Levels.Count} paliers de progression.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DataLoader] Échec critique du chargement : {ex}");
                    _isLoaded = false;
                }
                finally
                {
                    _isLoading = false;
                }
            }
        }

        /// <summary>
        /// Force le rechargement complet (alias éditoriteur de <see cref="LoadAll"/>).
        /// </summary>
        public static void Reload() => LoadAll();

        /// <summary>
        /// Garantit que les données sont chargées ; appelle <see cref="LoadAll"/> si besoin.
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_isLoaded || _isLoading)
            {
                return;
            }
            LoadAll();
        }

        // =================================================================================
        //  ACCESSEURS — AGENTS
        // =================================================================================

        /// <summary>Retourne l'agent portant cet Id, ou <c>null</c> si introuvable.</summary>
        public static AgentDto? GetAgent(string id)
        {
            EnsureLoaded();
            return Agents.TryGetValue(id ?? string.Empty, out var a) ? a : null;
        }

        /// <summary>Retourne tous les agents chargés.</summary>
        public static IReadOnlyList<AgentDto> GetAllAgents()
        {
            EnsureLoaded();
            return Agents.Values.ToArray();
        }

        /// <summary>Retourne les agents d'une classe donnée.</summary>
        public static IReadOnlyList<AgentDto> GetAgentsByClass(AgentClass cls)
        {
            EnsureLoaded();
            return Agents.Values.Where(a => a.Class == cls).ToArray();
        }

        /// <summary>Retourne les agents débloquables à un niveau joueur donné.</summary>
        public static IReadOnlyList<AgentDto> GetUnlockedAgents(int playerLevel)
        {
            EnsureLoaded();
            return Agents.Values.Where(a => a.UnlockLevel <= playerLevel).ToArray();
        }

        // =================================================================================
        //  ACCESSEURS — ARMES
        // =================================================================================

        /// <summary>Retourne l'arme portant cet Id, ou <c>null</c>.</summary>
        public static WeaponDto? GetWeapon(string id)
        {
            EnsureLoaded();
            return Weapons.TryGetValue(id ?? string.Empty, out var w) ? w : null;
        }

        /// <summary>Retourne toutes les armes chargées.</summary>
        public static IReadOnlyList<WeaponDto> GetAllWeapons()
        {
            EnsureLoaded();
            return Weapons.Values.ToArray();
        }

        /// <summary>Retourne les armes d'une catégorie donnée (Primary/Secondary/Tactical).</summary>
        public static IReadOnlyList<WeaponDto> GetWeaponsByCategory(WeaponCategory category)
        {
            EnsureLoaded();
            return Weapons.Values.Where(w => w.Category == category).ToArray();
        }

        /// <summary>Retourne les armes d'une rareté donnée.</summary>
        public static IReadOnlyList<WeaponDto> GetWeaponsByRarity(Rarity rarity)
        {
            EnsureLoaded();
            return Weapons.Values.Where(w => w.Rarity == rarity).ToArray();
        }

        // =================================================================================
        //  ACCESSEURS — OBJETS TACTIQUES
        // =================================================================================

        /// <summary>Retourne l'objet tactique portant cet Id, ou <c>null</c>.</summary>
        public static TacticalDto? GetTactical(string id)
        {
            EnsureLoaded();
            return Tacticals.TryGetValue(id ?? string.Empty, out var t) ? t : null;
        }

        /// <summary>Retourne tous les objets tactiques chargés.</summary>
        public static IReadOnlyList<TacticalDto> GetAllTacticals()
        {
            EnsureLoaded();
            return Tacticals.Values.ToArray();
        }

        // =================================================================================
        //  ACCESSEURS — MISSIONS
        // =================================================================================

        /// <summary>Retourne la mission portant cet Id, ou <c>null</c>.</summary>
        public static MissionDto? GetMission(string id)
        {
            EnsureLoaded();
            return Missions.TryGetValue(id ?? string.Empty, out var m) ? m : null;
        }

        /// <summary>Retourne toutes les missions chargées.</summary>
        public static IReadOnlyList<MissionDto> GetAllMissions()
        {
            EnsureLoaded();
            return Missions.Values.ToArray();
        }

        /// <summary>Retourne les missions d'un type donné.</summary>
        public static IReadOnlyList<MissionDto> GetMissionsByType(MissionType type)
        {
            EnsureLoaded();
            return Missions.Values.Where(m => m.Type == type).ToArray();
        }

        // =================================================================================
        //  ACCESSEURS — ENNEMIS
        // =================================================================================

        /// <summary>Retourne l'ennemi portant cet Id, ou <c>null</c>.</summary>
        public static EnemyDto? GetEnemy(string id)
        {
            EnsureLoaded();
            return Enemies.TryGetValue(id ?? string.Empty, out var e) ? e : null;
        }

        /// <summary>Retourne tous les ennemis chargés.</summary>
        public static IReadOnlyList<EnemyDto> GetAllEnemies()
        {
            EnsureLoaded();
            return Enemies.Values.ToArray();
        }

        /// <summary>Retourne les ennemis d'une classe donnée.</summary>
        public static IReadOnlyList<EnemyDto> GetEnemiesByClass(EnemyClass cls)
        {
            EnsureLoaded();
            return Enemies.Values.Where(e => e.Class == cls).ToArray();
        }

        // =================================================================================
        //  ACCESSEURS — RÉGIONS
        // =================================================================================

        /// <summary>Retourne la région portant cet Id, ou <c>null</c>.</summary>
        public static RegionDto? GetRegion(string id)
        {
            EnsureLoaded();
            return Regions.TryGetValue(id ?? string.Empty, out var r) ? r : null;
        }

        /// <summary>Retourne toutes les régions chargées.</summary>
        public static IReadOnlyList<RegionDto> GetAllRegions()
        {
            EnsureLoaded();
            return Regions.Values.ToArray();
        }

        /// <summary>Retourne les missions référencées par une région (résolution d'Id).</summary>
        public static IReadOnlyList<MissionDto> GetMissionsForRegion(string regionId)
        {
            EnsureLoaded();
            if (!Regions.TryGetValue(regionId ?? string.Empty, out var region))
            {
                return Array.Empty<MissionDto>();
            }
            var result = new List<MissionDto>(region.Missions.Count);
            foreach (var mid in region.Missions)
            {
                if (Missions.TryGetValue(mid, out var m))
                {
                    result.Add(m);
                }
            }
            return result;
        }

        // =================================================================================
        //  ACCESSEURS — PROGRESSION
        // =================================================================================

        /// <summary>Retourne la courbe de progression chargée.</summary>
        public static ProgressionCurveDto GetProgressionCurve()
        {
            EnsureLoaded();
            return _progression;
        }

        /// <summary>
        /// Retourne l'XP total cumulé requis pour atteindre le niveau donné.
        /// </summary>
        public static int GetXpRequiredForLevel(int level)
        {
            EnsureLoaded();
            var entry = _progression.Levels.FirstOrDefault(l => l.Level == level);
            return entry?.TotalXp ?? 0;
        }

        /// <summary>
        /// Retourne le niveau atteint pour un montant d'XP cumulé donné.
        /// </summary>
        public static int GetLevelForXp(int totalXp)
        {
            EnsureLoaded();
            if (_progression.Levels.Count == 0)
            {
                return 1;
            }
            int level = 1;
            foreach (var l in _progression.Levels)
            {
                if (totalXp >= l.TotalXp)
                {
                    level = l.Level;
                }
                else
                {
                    break;
                }
            }
            return level;
        }

        /// <summary>Retourne l'XP restant pour passer du niveau courant au suivant.</summary>
        public static int GetXpToNextLevel(int currentLevel, int currentTotalXp)
        {
            EnsureLoaded();
            int nextLevel = Math.Min(currentLevel + 1, _progression.MaxLevel);
            int next = GetXpRequiredForLevel(nextLevel);
            return Math.Max(0, next - currentTotalXp);
        }

        // =================================================================================
        //  VALIDATION D'INTÉGRITÉ (runtime — avertissements seulement)
        // =================================================================================

        private static void ValidateIntegrity()
        {
            // Références mission -> ennemi (vagues + boss phases).
            foreach (var m in Missions.Values)
            {
                if (string.IsNullOrEmpty(m.SceneName))
                {
                    Warn($"Mission '{m.Id}' : sceneName manquant.");
                }
                if (string.IsNullOrEmpty(m.Region))
                {
                    Warn($"Mission '{m.Id}' : region manquante.");
                }
                else if (!Regions.ContainsKey(m.Region))
                {
                    Warn($"Mission '{m.Id}' : région '{m.Region}' introuvable dans regions.json.");
                }
                foreach (var w in m.Waves)
                {
                    if (!Enemies.ContainsKey(w.EnemyId))
                    {
                        Warn($"Mission '{m.Id}' vague '{w.Id}' : ennemi '{w.EnemyId}' introuvable.");
                    }
                    if (w.Count <= 0)
                    {
                        Warn($"Mission '{m.Id}' vague '{w.Id}' : count <= 0.");
                    }
                }
                foreach (var bp in m.BossPhases)
                {
                    if (!Enemies.ContainsKey(bp.EnemyId))
                    {
                        Warn($"Mission '{m.Id}' phase boss '{bp.Id}' : ennemi '{bp.EnemyId}' introuvable.");
                    }
                }
                if (m.Rewards == null)
                {
                    Warn($"Mission '{m.Id}' : rewards manquantes.");
                }
            }

            // Références région -> mission.
            foreach (var r in Regions.Values)
            {
                foreach (var mid in r.Missions)
                {
                    if (!Missions.ContainsKey(mid))
                    {
                        Warn($"Région '{r.Id}' : mission '{mid}' introuvable dans missions.json.");
                    }
                }
            }

            // Cohérence agents.
            foreach (var a in Agents.Values)
            {
                if (a.BaseHealth <= 0) Warn($"Agent '{a.Id}' : BaseHealth <= 0.");
                if (a.BasePower <= 0) Warn($"Agent '{a.Id}' : BasePower <= 0.");
                if (a.Abilities.Count == 0) Warn($"Agent '{a.Id}' : aucune compétence définie.");
            }

            // Cohérence armes.
            foreach (var w in Weapons.Values)
            {
                if (w.Power <= 0) Warn($"Arme '{w.Id}' : Power <= 0.");
                if (w.MagazineSize <= 0 && !w.IsTactical) Warn($"Arme '{w.Id}' : MagazineSize <= 0.");
                if (w.FireModes.Count == 0 && !w.IsTactical) Warn($"Arme '{w.Id}' : aucun FireMode défini.");
            }

            // Cohérence ennemis.
            foreach (var e in Enemies.Values)
            {
                if (e.BaseHealth <= 0) Warn($"Ennemi '{e.Id}' : BaseHealth <= 0.");
                if (e.XpReward < 0) Warn($"Ennemi '{e.Id}' : XpReward négatif.");
            }

            // Cohérence progression.
            if (_progression.Levels.Count == 0)
            {
                Warn("progression.json : aucun palier défini.");
            }
            else if (_progression.Levels[0].TotalXp != 0)
            {
                Warn("progression.json : le premier palier devrait avoir totalXp = 0.");
            }
        }

        // =================================================================================
        //  HELPERS PRIVÉS
        // =================================================================================

        private static void ResetCaches()
        {
            Agents.Clear();
            Weapons.Clear();
            Missions.Clear();
            Enemies.Clear();
            Regions.Clear();
            Tacticals.Clear();
            _progression = new ProgressionCurveDto();
        }

        private static JsonSerializerSettings CreateSerializerSettings()
        {
            var settings = new JsonSerializerSettings
            {
                Converters = { new HexColorConverter(), new StringEnumConverter() },
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
            };
            return settings;
        }

        private static T? LoadJson<T>(string resourceName, JsonSerializerSettings settings) where T : class
        {
            var asset = Resources.Load<TextAsset>(DataRoot + resourceName);
            if (asset == null)
            {
                Warn($"Fichier JSON introuvable : Resources/{DataRoot}{resourceName}.json");
                return default;
            }
            try
            {
                return JsonConvert.DeserializeObject<T>(asset.text, settings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataLoader] Erreur de parsing JSON pour '{resourceName}' : {ex.Message}");
                return default;
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }

        private static void LoadList<T>(Dictionary<string, T> dict, T[]? items, Func<T, string> keySelector)
        {
            if (items == null)
            {
                return;
            }
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                string key = keySelector(item);
                if (string.IsNullOrEmpty(key))
                {
                    Warn($"Entrée sans Id ignorée dans le cache {typeof(T).Name}.");
                    continue;
                }
                if (dict.ContainsKey(key))
                {
                    Warn($"Doublon d'Id '{key}' dans le cache {typeof(T).Name} : la dernière entrée écrase la précédente.");
                }
                dict[key] = item;
            }
        }

        /// <summary>
        /// Convertit une arme tactique (<see cref="WeaponDto"/> de catégorie Tactical)
        /// en <see cref="TacticalDto"/> pour l'usage gadget.
        /// </summary>
        private static TacticalDto ToTacticalDto(WeaponDto w)
        {
            var effect = TacticalEffectType.Damage;
            // Inférence heuristique du type d'effet selon l'élément / l'Id.
            if (w.Element == Element.Volt)
            {
                effect = TacticalEffectType.EMP;
            }
            if (w.Id.Contains("trap", StringComparison.OrdinalIgnoreCase))
            {
                effect = TacticalEffectType.Trap;
            }
            if (w.Id.Contains("decoy", StringComparison.OrdinalIgnoreCase))
            {
                effect = TacticalEffectType.Decoy;
            }

            return new TacticalDto
            {
                Id = w.Id,
                DisplayName = w.DisplayName,
                Description = $"Objet tactique {w.DisplayName} (power {w.Power}).",
                EffectType = effect,
                Power = w.Power,
                FuseTime = w.FuseTime,
                ExplosionRadius = w.ExplosionRadiusPct / 100f * 50f, // conversion % -> mètres (échelle 50m max)
                Magnitude = w.DamagePct / 100f,
                Duration = 0f,
                Rarity = w.Rarity,
                Icon = w.Icon,
                ModelPrefab = w.ModelPrefab,
            };
        }

        private static void Warn(string message)
        {
            Debug.LogWarning($"[DataLoader] {message}");
        }

        // =================================================================================
        //  HOT-RELOAD ÉDITEUR
        // =================================================================================

#if UNITY_EDITOR
        /// <summary>Menu éditeur : recharge tous les fichiers JSON sans redémarrer.</summary>
        [UnityEditor.MenuItem("KINETICS 5/Data/Recharger les données", priority = 10)]
        public static void ReloadMenu()
        {
            LoadAll();
            UnityEditor.EditorUtility.DisplayDialog(
                "KINETICS 5 — DataLoader",
                $"Rechargement terminé.\n{LoadedCount} entités en cache.",
                "OK");
        }

        /// <summary>Menu éditeur : affiche un récapitulatif des données chargées.</summary>
        [UnityEditor.MenuItem("KINETICS 5/Data/Récapitulatif", priority = 11)]
        public static void SummaryMenu()
        {
            EnsureLoaded();
            UnityEditor.EditorUtility.DisplayDialog(
                "KINETICS 5 — Récapitulatif données",
                $"Agents : {Agents.Count}\nArmes : {Weapons.Count}\n" +
                $"Tactiques : {Tacticals.Count}\nMissions : {Missions.Count}\n" +
                $"Ennemis : {Enemies.Count}\nRégions : {Regions.Count}\n" +
                $"Paliers progression : {_progression.Levels.Count}",
                "OK");
        }
#endif
    }
}
