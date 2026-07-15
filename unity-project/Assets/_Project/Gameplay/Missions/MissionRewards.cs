using System;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Missions
{
    /// <summary>
    /// Calcul des récompenses finales d'une mission. Comprend :
    /// <list type="bullet">
    ///   <item>Récompenses de base (XP, CR) du <see cref="MissionDto.Rewards"/>.</item>
    ///   <item>Bonus : pas de mort, bonus de temps, tous les objectifs, bonus furtif.</item>
    ///   <item>Vérification de level-up via <see cref="DataLoader.GetLevelForXp"/>.</item>
    ///   <item>Loot de fin de mission (roll de la loot table de récompenses).</item>
    ///   <item>Publication sur le bus global + persistance via <see cref="SaveSystem"/>.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Méthode statique <see cref="ComputeAndPublish"/> : aucune instance nécessaire,
    /// invocation par <see cref="MissionDirector.CompleteMission"/>.
    /// </remarks>
    public static class MissionRewards
    {
        /// <summary>Struct zero-alloc résumant les récompenses calculées.</summary>
        public readonly struct RewardSummary
        {
            /// <summary>Id de la mission.</summary>
            public readonly string MissionId;
            /// <summary>XP de base (avant bonus).</summary>
            public readonly int BaseXp;
            /// <summary>XP bonus (no death, time, etc.).</summary>
            public readonly int BonusXp;
            /// <summary>XP total.</summary>
            public readonly int TotalXp;
            /// <summary>CR de base.</summary>
            public readonly int BaseCr;
            /// <summary>CR bonus.</summary>
            public readonly int BonusCr;
            /// <summary>CR total.</summary>
            public readonly int TotalCr;
            /// <summary>Niveau avant ajout de l'XP.</summary>
            public readonly int LevelBefore;
            /// <summary>Niveau après ajout de l'XP.</summary>
            public readonly int LevelAfter;
            /// <summary>Vrai si un level-up a eu lieu.</summary>
            public readonly bool LeveledUp;
            /// <summary>Loot drop de fin de mission (itemId -> quantity).</summary>
            public readonly LootDrop[] Loot;
            /// <summary>Vrai si PerfectClear (aucune mort + bonus).</summary>
            public readonly bool PerfectClear;

            public RewardSummary(string missionId, int baseXp, int bonusXp, int totalXp,
                                 int baseCr, int bonusCr, int totalCr, int levelBefore, int levelAfter,
                                 bool leveledUp, LootDrop[] loot, bool perfectClear)
            {
                MissionId = missionId;
                BaseXp = baseXp; BonusXp = bonusXp; TotalXp = totalXp;
                BaseCr = baseCr; BonusCr = bonusCr; TotalCr = totalCr;
                LevelBefore = levelBefore; LevelAfter = levelAfter;
                LeveledUp = leveledUp; Loot = loot; PerfectClear = perfectClear;
            }
        }

        /// <summary>Loot drop résultant du roll de la table de récompenses.</summary>
        public struct LootDrop
        {
            public string ItemId;
            public int Quantity;
            public Rarity Rarity;
        }

        /// <summary>
        /// Calcule les récompenses finales et les publie (UI + save + events).
        /// </summary>
        /// <param name="mission">Données de la mission.</param>
        /// <param name="director">Director de mission (pour récupérer le contexte de performance).</param>
        /// <returns>Résumé des récompenses calculées.</returns>
        public static RewardSummary ComputeAndPublish(MissionDto mission, MissionDirector director)
        {
            if (mission == null) return default;

            // --- Base ---
            int baseXp = mission.Rewards?.Xp ?? 0;
            int baseCr = mission.Rewards?.Cr ?? 0;

            // --- Bonus ---
            int bonusXp = 0;
            int bonusCr = 0;

            bool noDeath = director != null && !director.PlayerDiedDuringMission;
            bool allOptional = director != null && AllOptionalObjectivesComplete(director);
            bool stealthMaintained = director != null && director.StealthMaintained && mission.StealthOptional;
            bool timeBonus = mission.TimeLimit > 0 && director != null &&
                             director.ElapsedTime < mission.TimeLimit * 0.5f;

            if (noDeath) { bonusXp += 500; bonusCr += 250; }
            if (allOptional) { bonusXp += 1000; bonusCr += 500; }
            if (stealthMaintained) { bonusXp += 1500; bonusCr += 750; }
            if (timeBonus) { bonusXp += 800; bonusCr += 400; }

            int totalXp = baseXp + bonusXp;
            int totalCr = baseCr + bonusCr;

            // --- Level-up check ---
            int levelBefore = 1;
            int levelAfter = 1;
            bool leveledUp = false;
            if (SaveSystem.Instance != null && SaveSystem.Instance.ActiveData != null)
            {
                var profile = SaveSystem.Instance.ActiveData.Profile;
                long oldXp = profile.Xp;
                levelBefore = DataLoader.GetLevelForXp((int)oldXp);
                long newXp = oldXp + totalXp;
                levelAfter = DataLoader.GetLevelForXp((int)newXp);
                leveledUp = levelAfter > levelBefore;

                // Persistance immédiate.
                profile.Xp = newXp;
                profile.Credits += totalCr;
                if (!SaveSystem.Instance.ActiveData.Progress.CompletedMissions.Contains(mission.Id))
                {
                    SaveSystem.Instance.ActiveData.Progress.CompletedMissions.Add(mission.Id);
                }
                SaveSystem.Instance.ActiveData.Progress.TotalPlaytime += director?.ElapsedTime ?? 0f;
                SaveSystem.Instance.SaveImmediate();
            }

            // --- Loot table roll ---
            LootDrop[] loot = RollLootTable(mission.Rewards?.LootTable);

            // --- Spawn pickups physiques (pour feedback visuel) ---
            if (director != null && Gameplay.Enemies.LootDropSystem.HasInstance && loot != null)
            {
                Vector3 spawnPos = director.transform.position;
                foreach (var l in loot)
                {
                    Gameplay.Enemies.LootDropSystem.Instance.SpawnPickup(l.ItemId, l.Quantity, spawnPos);
                }
            }

            // --- Publication telemetry ---
            if (TelemetryLogger.Instance != null)
            {
                TelemetryLogger.Instance.TrackMissionComplete(mission.Id, director?.ElapsedTime ?? 0f,
                    totalXp + totalCr, noDeath && (stealthMaintained || allOptional));
            }

            bool perfectClear = noDeath && (bonusXp > 0);

            var summary = new RewardSummary(mission.Id, baseXp, bonusXp, totalXp,
                                            baseCr, bonusCr, totalCr, levelBefore, levelAfter,
                                            leveledUp, loot, perfectClear);

            Debug.Log($"[MissionRewards] {mission.Id} : +{totalXp} XP, +{totalCr} CR" +
                      $"{(leveledUp ? $" (LEVEL UP {levelBefore}→{levelAfter})" : "")}" +
                      $"{(perfectClear ? " [PERFECT]" : "")}.");
            return summary;
        }

        /// <summary>Vérifie si tous les objectifs optionnels sont complétés.</summary>
        private static bool AllOptionalObjectivesComplete(MissionDirector director)
        {
            var objs = director.Objectives;
            for (int i = 0; i < objs.Count; i++)
            {
                if (objs[i].IsOptional && !objs[i].IsComplete) return false;
            }
            return true;
        }

        /// <summary>Roll la loot table de récompenses.</summary>
        private static LootDrop[] RollLootTable(List<LootTableEntryDto> table)
        {
            if (table == null || table.Count == 0) return Array.Empty<LootDrop>();
            var result = new List<LootDrop>(table.Count);
            foreach (var entry in table)
            {
                float roll = UnityEngine.Random.value * 100f;
                if (roll > entry.DropChancePct) continue;
                int qty = UnityEngine.Random.Range(entry.MinQty, entry.MaxQty + 1);
                if (qty <= 0) continue;
                result.Add(new LootDrop
                {
                    ItemId = entry.ItemId,
                    Quantity = qty,
                    Rarity = ResolveRarity(entry.ItemId)
                });
            }
            return result.ToArray();
        }

        private static Rarity ResolveRarity(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return Rarity.Common;
            if (itemId.Contains("legendary", StringComparison.OrdinalIgnoreCase)) return Rarity.Legendary;
            if (itemId.Contains("epic", StringComparison.OrdinalIgnoreCase)) return Rarity.Epic;
            if (itemId.Contains("rare", StringComparison.OrdinalIgnoreCase)) return Rarity.Rare;
            return Rarity.Common;
        }
    }
}
