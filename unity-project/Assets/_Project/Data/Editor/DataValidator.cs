#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEditor;
using UnityEngine;

namespace KINETICS5.Data.Editor
{
    /// <summary>
    /// Utilitaire éditeur de validation de la couche de données KINETICS 5.
    /// </summary>
    /// <remarks>
    /// <para>Valide :</para>
    /// <list type="bullet">
    /// <item>La présence et le parsing JSON de tous les fichiers de <c>Resources/Data/</c>.</item>
    /// <item>La cohérence du schéma (champs obligatoires non vides).</item>
    /// <item>L'intégrité référentielle (mission → ennemi, mission → région, région → mission).</item>
    /// <item>La balance de jeu (ennemis à 0 PV, armes à 0 power, paliers de progression).</item>
    /// <item>L'absence de doublons d'Id au sein de chaque collection.</item>
    /// </list>
    /// <para>Le rapport est affiché en console et dans une boîte de dialogue éditeur.</para>
    /// </remarks>
    public static class DataValidator
    {
        /// <summary>Chemin relatif (depuis Assets/) des fichiers de données JSON.</summary>
        private const string DataFolder = "Assets/_Project/Data/Resources/Data";

        private const string MenuRoot = "KINETICS 5/Data/";

        // =================================================================================
        //  MENU
        // =================================================================================

        [MenuItem(MenuRoot + "Valider les données", priority = 20)]
        public static void ValidateAll()
        {
            var report = new ValidationReport();
            report.Info("Général", "Démarrage de la validation KINETICS 5...");

            var agents = LoadAndValidate<AgentDto[]>(report, "agents", out var agentJson);
            var weapons = LoadAndValidate<WeaponDto[]>(report, "weapons", out _);
            var missions = LoadAndValidate<MissionDto[]>(report, "missions", out _);
            var enemies = LoadAndValidate<EnemyDto[]>(report, "enemies", out _);
            var regions = LoadAndValidate<RegionDto[]>(report, "regions", out _);
            var progression = LoadAndValidate<ProgressionCurveDto>(report, "progression", out _);

            if (agents != null) ValidateAgents(report, agents);
            if (weapons != null) ValidateWeapons(report, weapons);
            if (enemies != null) ValidateEnemies(report, enemies);
            if (missions != null && enemies != null && regions != null)
            {
                ValidateMissions(report, missions, enemies, regions);
            }
            if (regions != null && missions != null) ValidateRegions(report, regions, missions);
            if (progression != null) ValidateProgression(report, progression);

            // Cohérence globale des Id (doublons cross-fichiers optionnels — non bloquant).
            report.Info("Général", "Validation terminée.");

            report.Print();
            ShowResultDialog(report);
        }

        [MenuItem(MenuRoot + "Valider (runtime DataLoader)", priority = 21)]
        public static void ValidateRuntimeCache()
        {
            DataLoader.LoadAll();
            EditorUtility.DisplayDialog(
                "KINETICS 5 — DataLoader",
                $"Cache runtime rechargé : {DataLoader.LoadedCount} entités.\n" +
                "Consultez la console pour les avertissements d'intégrité.",
                "OK");
        }

        // =================================================================================
        //  CHARGEMENT + VALIDATION JSON
        // =================================================================================

        private static T? LoadAndValidate<T>(ValidationReport report, string name, out string rawJson)
            where T : class
        {
            rawJson = string.Empty;
            string path = $"{DataFolder}/{name}.json";
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                report.Error("Fichier", $"JSON introuvable : {path}");
                return null;
            }
            try
            {
                rawJson = File.ReadAllText(fullPath);
            }
            catch (Exception ex)
            {
                report.Error("Fichier", $"Lecture impossible de {path} : {ex.Message}");
                return null;
            }
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new HexColorConverter(), new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Error,
                };
                var result = JsonConvert.DeserializeObject<T>(rawJson, settings);
                if (result == null)
                {
                    report.Error("JSON", $"{name}.json : contenu null après désérialisation.");
                    return null;
                }
                report.Info("JSON", $"{name}.json : parsing OK.");
                return result;
            }
            catch (Exception ex)
            {
                report.Error("JSON", $"{name}.json : échec du parsing — {ex.Message}");
                return null;
            }
        }

        // =================================================================================
        //  VALIDATION PAR DOMAINE
        // =================================================================================

        private static void ValidateAgents(ValidationReport report, IList<AgentDto> agents)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in agents)
            {
                string ctx = $"Agent '{a.Id}'";
                if (string.IsNullOrEmpty(a.Id)) report.Error("Agent", "Agent sans Id.");
                if (!seen.Add(a.Id)) report.Error("Agent", $"Doublon d'Id : {a.Id}");
                if (string.IsNullOrEmpty(a.DisplayName)) report.Warn("Agent", $"{ctx} : displayName vide.");
                if (a.BaseHealth <= 0) report.Error("Balance", $"{ctx} : BaseHealth <= 0 ({a.BaseHealth}).");
                if (a.BaseShield < 0) report.Warn("Balance", $"{ctx} : BaseShield négatif.");
                if (a.BaseSpeed <= 0f) report.Error("Balance", $"{ctx} : BaseSpeed <= 0.");
                if (a.BasePower <= 0) report.Warn("Balance", $"{ctx} : BasePower <= 0.");
                if (a.UnlockLevel < 1) report.Warn("Balance", $"{ctx} : UnlockLevel < 1.");
                if (a.ThemeColor.a <= 0f) report.Warn("Balance", $"{ctx} : themeColor transparent.");
                if (a.Abilities.Count == 0) report.Warn("Schéma", $"{ctx} : aucune compétence définie.");
                foreach (var ab in a.Abilities)
                {
                    if (string.IsNullOrEmpty(ab.Id)) report.Warn("Schéma", $"{ctx} : compétence sans Id.");
                    if (ab.Cooldown < 0f) report.Warn("Balance", $"{ctx} compétence '{ab.Id}' : cooldown négatif.");
                }
                var talentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in a.AwakeningTree)
                {
                    if (string.IsNullOrEmpty(t.Id)) report.Warn("Schéma", $"{ctx} : nœud de talent sans Id.");
                    if (!talentIds.Add(t.Id)) report.Warn("Schéma", $"{ctx} : nœud de talent dupliqué '{t.Id}'.");
                    foreach (var req in t.RequiredNodeIds)
                    {
                        if (!a.AwakeningTree.Any(n => string.Equals(n.Id, req, StringComparison.OrdinalIgnoreCase)))
                        {
                            report.Warn("Référence", $"{ctx} talent '{t.Id}' : prérequis '{req}' introuvable.");
                        }
                    }
                }
            }
            if (agents.Count < 4) report.Warn("Schéma", $"agents.json : {agents.Count} agents (4 attendus).");
        }

        private static void ValidateWeapons(ValidationReport report, IList<WeaponDto> weapons)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in weapons)
            {
                string ctx = $"Arme '{w.Id}'";
                if (string.IsNullOrEmpty(w.Id)) report.Error("Arme", "Arme sans Id.");
                if (!seen.Add(w.Id)) report.Error("Arme", $"Doublon d'Id : {w.Id}");
                if (string.IsNullOrEmpty(w.DisplayName)) report.Warn("Schéma", $"{ctx} : displayName vide.");
                if (w.Power <= 0) report.Warn("Balance", $"{ctx} : Power <= 0.");
                if (!w.IsTactical)
                {
                    if (w.MagazineSize <= 0) report.Error("Balance", $"{ctx} : MagazineSize <= 0.");
                    if (w.FireModes.Count == 0) report.Warn("Schéma", $"{ctx} : aucun FireMode.");
                    if (w.Range <= 0f) report.Warn("Balance", $"{ctx} : Range <= 0.");
                    if (w.DamagePct <= 0f) report.Warn("Balance", $"{ctx} : DamagePct <= 0.");
                }
                else
                {
                    if (w.FuseTime < 0f) report.Warn("Balance", $"{ctx} tactique : fuseTime négatif.");
                    if (w.ExplosionRadiusPct <= 0f && w.Type == WeaponType.Grenade)
                    {
                        report.Warn("Balance", $"{ctx} grenade : explosionRadiusPct <= 0.");
                    }
                }
                if (w.DamagePct < 0f || w.DamagePct > 100f)
                {
                    report.Warn("Balance", $"{ctx} : DamagePct hors plage [0,100].");
                }
            }
            int primary = weapons.Count(w => w.Category == WeaponCategory.Primary);
            int secondary = weapons.Count(w => w.Category == WeaponCategory.Secondary);
            int tactical = weapons.Count(w => w.Category == WeaponCategory.Tactical);
            report.Info("Balance", $"Armes : {primary} primaires, {secondary} secondaires, {tactical} tactiques.");
            if (primary < 5) report.Warn("Schéma", $"Peu d'armes primaires ({primary}).");
            if (tactical < 4) report.Warn("Schéma", $"Peu d'armes tactiques ({tactical}).");
        }

        private static void ValidateEnemies(ValidationReport report, IList<EnemyDto> enemies)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in enemies)
            {
                string ctx = $"Ennemi '{e.Id}'";
                if (string.IsNullOrEmpty(e.Id)) report.Error("Ennemi", "Ennemi sans Id.");
                if (!seen.Add(e.Id)) report.Error("Ennemi", $"Doublon d'Id : {e.Id}");
                if (string.IsNullOrEmpty(e.DisplayName)) report.Warn("Schéma", $"{ctx} : displayName vide.");
                if (e.BaseHealth <= 0) report.Error("Balance", $"{ctx} : BaseHealth <= 0 ({e.BaseHealth}).");
                if (e.BaseDamage < 0) report.Warn("Balance", $"{ctx} : BaseDamage négatif.");
                if (e.MoveSpeed <= 0f) report.Warn("Balance", $"{ctx} : MoveSpeed <= 0.");
                if (e.AttackRate <= 0f) report.Warn("Balance", $"{ctx} : AttackRate <= 0.");
                if (e.Weakness == e.Resistance && e.Weakness != Element.Kinetic)
                {
                    report.Warn("Balance", $"{ctx} : faiblesse == résistance ({e.Weakness}).");
                }
                if (e.Class == EnemyClass.Boss && e.BaseHealth < 10000)
                {
                    report.Warn("Balance", $"{ctx} boss : BaseHealth < 10000 (peut être trop faible).");
                }
                foreach (var drop in e.LootTable)
                {
                    if (drop.DropChancePct < 0f || drop.DropChancePct > 100f)
                    {
                        report.Warn("Balance", $"{ctx} loot '{drop.ItemId}' : dropChance hors [0,100].");
                    }
                    if (drop.MaxQty < drop.MinQty)
                    {
                        report.Error("Schéma", $"{ctx} loot '{drop.ItemId}' : maxQty < minQty.");
                    }
                }
            }
        }

        private static void ValidateMissions(ValidationReport report, IList<MissionDto> missions,
            IList<EnemyDto> enemies, IList<RegionDto> regions)
        {
            var enemyIds = new HashSet<string>(enemies.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);
            var regionIds = new HashSet<string>(regions.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var m in missions)
            {
                string ctx = $"Mission '{m.Id}'";
                if (string.IsNullOrEmpty(m.Id)) report.Error("Mission", "Mission sans Id.");
                if (!seen.Add(m.Id)) report.Error("Mission", $"Doublon d'Id : {m.Id}");
                if (string.IsNullOrEmpty(m.DisplayName)) report.Warn("Schéma", $"{ctx} : displayName vide.");
                if (string.IsNullOrEmpty(m.SceneName)) report.Error("Schéma", $"{ctx} : sceneName manquant.");
                if (string.IsNullOrEmpty(m.Region)) report.Warn("Schéma", $"{ctx} : region manquante.");
                else if (!regionIds.Contains(m.Region))
                {
                    report.Error("Référence", $"{ctx} : région '{m.Region}' introuvable.");
                }
                if (m.RecommendedPower <= 0) report.Warn("Balance", $"{ctx} : recommendedPower <= 0.");
                if (m.Objectives.Count == 0) report.Warn("Schéma", $"{ctx} : aucun objectif.");
                if (m.Waves.Count == 0 && m.BossPhases.Count == 0)
                {
                    report.Warn("Schéma", $"{ctx} : aucune vague ni phase de boss.");
                }
                foreach (var w in m.Waves)
                {
                    if (!enemyIds.Contains(w.EnemyId))
                    {
                        report.Error("Référence", $"{ctx} vague '{w.Id}' : ennemi '{w.EnemyId}' introuvable.");
                    }
                    if (w.Count <= 0) report.Warn("Balance", $"{ctx} vague '{w.Id}' : count <= 0.");
                    if (w.Delay < 0f) report.Warn("Balance", $"{ctx} vague '{w.Id}' : delay négatif.");
                }
                foreach (var bp in m.BossPhases)
                {
                    if (!enemyIds.Contains(bp.EnemyId))
                    {
                        report.Error("Référence", $"{ctx} phase boss '{bp.Id}' : ennemi '{bp.EnemyId}' introuvable.");
                    }
                    if (bp.Phase < 1) report.Warn("Schéma", $"{ctx} phase '{bp.Id}' : phase < 1.");
                    if (bp.HealthThresholdPct < 0f || bp.HealthThresholdPct > 1f)
                    {
                        report.Warn("Balance", $"{ctx} phase '{bp.Id}' : healthThreshold hors [0,1].");
                    }
                }
                if (m.Rewards == null)
                {
                    report.Warn("Schéma", $"{ctx} : rewards manquantes.");
                }
                else
                {
                    if (m.Rewards.Xp < 0) report.Warn("Balance", $"{ctx} : reward XP négatif.");
                    if (m.Rewards.Cr < 0) report.Warn("Balance", $"{ctx} : reward CR négatif.");
                }
                if (m.Type == MissionType.Survival && m.TimeLimit <= 0)
                {
                    report.Warn("Balance", $"{ctx} (Survival) : timeLimit <= 0.");
                }
            }
        }

        private static void ValidateRegions(ValidationReport report, IList<RegionDto> regions, IList<MissionDto> missions)
        {
            var missionIds = new HashSet<string>(missions.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in regions)
            {
                string ctx = $"Région '{r.Id}'";
                if (string.IsNullOrEmpty(r.Id)) report.Error("Région", "Région sans Id.");
                if (!seen.Add(r.Id)) report.Error("Région", $"Doublon d'Id : {r.Id}");
                if (string.IsNullOrEmpty(r.DisplayName)) report.Warn("Schéma", $"{ctx} : displayName vide.");
                if (string.IsNullOrEmpty(r.SceneName)) report.Warn("Schéma", $"{ctx} : sceneName manquant.");
                if (r.Missions.Count == 0) report.Warn("Schéma", $"{ctx} : aucune mission référencée.");
                foreach (var mid in r.Missions)
                {
                    if (!missionIds.Contains(mid))
                    {
                        report.Error("Référence", $"{ctx} : mission '{mid}' introuvable.");
                    }
                }
            }
        }

        private static void ValidateProgression(ValidationReport report, ProgressionCurveDto curve)
        {
            if (curve.Levels.Count == 0)
            {
                report.Error("Progression", "Aucun palier défini.");
                return;
            }
            if (curve.MaxLevel < 2) report.Warn("Balance", "maxLevel < 2.");
            if (curve.Levels[0].TotalXp != 0)
            {
                report.Warn("Balance", "Le premier palier devrait avoir totalXp = 0.");
            }
            for (int i = 1; i < curve.Levels.Count; i++)
            {
                if (curve.Levels[i].TotalXp <= curve.Levels[i - 1].TotalXp)
                {
                    report.Error("Balance",
                        $"Paliers non croissants : niveau {curve.Levels[i - 1].Level} -> {curve.Levels[i].Level}.");
                    break;
                }
                if (curve.Levels[i].XpRequired < 0)
                {
                    report.Warn("Balance", $"Palier {curve.Levels[i].Level} : xpRequired négatif.");
                }
            }
            var last = curve.Levels[curve.Levels.Count - 1];
            if (last.Level != curve.MaxLevel)
            {
                report.Warn("Balance", $"Dernier palier ({last.Level}) != maxLevel ({curve.MaxLevel}).");
            }
        }

        // =================================================================================
        //  RAPPORT
        // =================================================================================

        private static void ShowResultDialog(ValidationReport report)
        {
            string title = report.HasErrors ? "Validation : ÉCHEC" : "Validation : OK";
            string body = report.HasErrors
                ? $"{report.ErrorCount} erreur(s), {report.WarningCount} avertissement(s).\nVoir la console pour le détail."
                : $"Toutes les données sont valides.\n{report.WarningCount} avertissement(s) mineur(s).";
            EditorUtility.DisplayDialog(title, body, "OK");
        }

        /// <summary>Rapport de validation accumulant erreurs, avertissements et infos.</summary>
        public sealed class ValidationReport
        {
            private readonly List<(string Level, string Category, string Message)> _entries = new();

            /// <summary>Nombre d'erreurs fatales.</summary>
            public int ErrorCount { get; private set; }

            /// <summary>Nombre d'avertissements.</summary>
            public int WarningCount { get; private set; }

            /// <summary>Vrai si au moins une erreur fatale a été levée.</summary>
            public bool HasErrors => ErrorCount > 0;

            /// <summary>Ajoute une erreur.</summary>
            public void Error(string category, string message)
            {
                _entries.Add(("ERROR", category, message));
                ErrorCount++;
            }

            /// <summary>Ajoute un avertissement.</summary>
            public void Warn(string category, string message)
            {
                _entries.Add(("WARN", category, message));
                WarningCount++;
            }

            /// <summary>Ajoute une information.</summary>
            public void Info(string category, string message)
            {
                _entries.Add(("INFO", category, message));
            }

            /// <summary>Affiche le rapport complet dans la console Unity.</summary>
            public void Print()
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Validation des données KINETICS 5 ===");
                foreach (var e in _entries)
                {
                    sb.AppendLine($"[{e.Level}] {e.Category}: {e.Message}");
                }
                sb.AppendLine("========================================");
                sb.AppendLine($"Total : {ErrorCount} erreur(s), {WarningCount} avertissement(s).");
                if (HasErrors)
                {
                    Debug.LogError(sb.ToString());
                }
                else
                {
                    Debug.Log(sb.ToString());
                }
            }
        }
    }
}
#endif
