"use client";

/**
 * KINETICS 5 — Operation Summary (PDF page 8)
 * Layout multi-colonnes : MISSION OBJECTIVES | REWARDS EARNED
 * Level up +7000 XP animé, bouton OK
 */

import { KButton, KCurrency, KPanel } from "@/components/kinetics/ui";
import { StarfieldBackground } from "@/components/kinetics/visuals";
import { useGameStore, useSelectedMission, XP_PER_LEVEL } from "@/store/game-store";
import { t } from "@/lib/i18n";
import { MISSIONS, formatNumber } from "@/lib/kinetics-data";
import { useEffect, useState } from "react";

export function OperationSummaryScreen() {
  const { combatResult, playerLevel, playerXp, playerCr, setScreen, language } = useGameStore();
  const fallbackMission = useSelectedMission();
  const missionId = combatResult?.missionId;
  const mission = MISSIONS.find((m) => m.id === missionId) ?? fallbackMission;

  const [displayedXp, setDisplayedXp] = useState(0);
  const targetXp = combatResult?.rewards.xp ?? 0;

  // Animation XP qui remonte
  useEffect(() => {
    const duration = 2000;
    const start = performance.now();
    let raf = 0;
    const animate = (now: number) => {
      const progress = Math.min(1, (now - start) / duration);
      const eased = 1 - Math.pow(1 - progress, 3);
      setDisplayedXp(Math.floor(targetXp * eased));
      if (progress < 1) raf = requestAnimationFrame(animate);
    };
    raf = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(raf);
  }, [targetXp]);

  if (!combatResult) {
    return (
      <div className="min-h-screen flex items-center justify-center text-k5-muted">
        No combat data.
      </div>
    );
  }

  const objectives = mission.objectives;
  const xpBeforeLevel = playerXp - targetXp;
  const leveledUp = xpBeforeLevel < 0;

  return (
    <div className="relative min-h-screen w-full overflow-hidden flex flex-col bg-k5-deep-space">
      <StarfieldBackground density={50} />

      <header className="relative z-10 pt-6 pb-2 text-center safe-top">
        <div className="text-[10px] font-display tracking-[0.4em] text-k5-cyan k5-text-glow-cyan">
          {t(language, "summary.title")}
        </div>
        <h1 className="font-display text-3xl text-white mt-1">{mission.displayName}</h1>
        <div className="text-xs text-k5-muted mt-1">{mission.type.toUpperCase()} • {mission.region}</div>
      </header>

      <main className="relative z-10 flex-1 px-4 py-4 overflow-y-auto">
        <div className="max-w-2xl mx-auto grid grid-cols-1 md:grid-cols-2 gap-3">
          {/* Colonne objectifs */}
          <KPanel scanlines>
            <div className="text-xs font-display tracking-wider text-k5-cyan mb-3 pb-2 border-b border-k5-border/50">
              {t(language, "summary.missionObjectives")}
            </div>
            <div className="space-y-2">
              {objectives.map((obj, i) => {
                const completed = i < combatResult.objectivesCompleted;
                return (
                  <div key={obj.id} className="flex items-start gap-2">
                    <span className={`mt-0.5 ${completed ? "text-k5-green" : "text-k5-red"}`}>
                      {completed ? "✓" : "✗"}
                    </span>
                    <div className="flex-1">
                      <div className="text-xs text-white">{obj.description}</div>
                      <div className="text-[10px] text-k5-green">+{formatNumber(obj.rewardXp)} XP</div>
                    </div>
                  </div>
                );
              })}
            </div>
          </KPanel>

          {/* Colonne récompenses */}
          <KPanel scanlines>
            <div className="text-xs font-display tracking-wider text-k5-yellow mb-3 pb-2 border-b border-k5-border/50">
              {t(language, "summary.rewardsEarned")}
            </div>
            <div className="space-y-2">
              <div className="flex justify-between items-center">
                <span className="text-xs text-k5-muted">{t(language, "summary.contractCompletion")}</span>
                <span className="text-sm font-display text-k5-yellow">+{formatNumber(Math.floor(combatResult.rewards.cr * 0.6))} CR</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-xs text-k5-muted">{t(language, "summary.combatPerformance")}</span>
                <span className="text-sm font-display text-k5-yellow">+{formatNumber(Math.floor(combatResult.rewards.cr * 0.3))} CR</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-xs text-k5-muted">{t(language, "summary.techScraps")}</span>
                <span className="text-sm font-display text-k5-yellow">+45 CR</span>
              </div>
              {/* Bonus */}
              {combatResult.bonuses.map((b, i) => (
                <div key={i} className="flex justify-between items-center pt-1 border-t border-k5-border/30">
                  <span className="text-xs text-k5-green">{b.label}</span>
                  <span className="text-sm font-display text-k5-green">+{b.cr ?? b.xp} {b.cr ? "CR" : "XP"}</span>
                </div>
              ))}
              <div className="pt-2 mt-2 border-t border-k5-border/50 flex justify-between items-center">
                <span className="text-xs font-display text-white">TOTAL</span>
                <div className="flex gap-3">
                  <span className="text-sm font-display text-k5-yellow">+{formatNumber(combatResult.rewards.cr)} CR</span>
                  <span className="text-sm font-display text-k5-green">+{formatNumber(combatResult.rewards.xp)} XP</span>
                </div>
              </div>
            </div>
          </KPanel>
        </div>

        {/* Level up banner */}
        {leveledUp && (
          <div className="max-w-2xl mx-auto mt-4">
            <div className="bg-gradient-to-r from-k5-purple/20 via-k5-cyan/20 to-k5-purple/20 border-2 border-k5-purple p-4 rounded-sm k5-clip text-center k5-glow-cyan">
              <div className="text-[10px] font-display tracking-[0.3em] text-k5-purple">
                {t(language, "summary.levelUp")}
              </div>
              <div className="font-display text-3xl text-white mt-1">
                LEVEL {playerLevel}
              </div>
              <div className="text-sm text-k5-green mt-1">+{formatNumber(combatResult.rewards.xp)} XP</div>
            </div>
          </div>
        )}

        {/* Barre de progression XP animée */}
        <div className="max-w-2xl mx-auto mt-4">
          <KPanel>
            <div className="flex justify-between text-[10px] font-display mb-2">
              <span className="text-k5-muted">NIVEAU {playerLevel}</span>
              <span className="text-k5-green">{formatNumber(displayedXp)} / {formatNumber(XP_PER_LEVEL)} XP</span>
            </div>
            <div className="h-3 bg-k5-panel border border-k5-border rounded-sm overflow-hidden">
              <div
                className="h-full bg-gradient-to-r from-k5-purple to-k5-cyan transition-all"
                style={{ width: `${(displayedXp / XP_PER_LEVEL) * 100}%`, boxShadow: "0 0 12px #A855F7" }}
              />
            </div>
          </KPanel>
        </div>

        {/* Stats combat */}
        <div className="max-w-2xl mx-auto mt-3 grid grid-cols-4 gap-2">
          {[
            { label: "KILLS", value: combatResult.enemiesKilled, color: "#6CF42E" },
            { label: "DAMAGE", value: formatNumber(Math.floor(combatResult.damageDealt)), color: "#1AA1CE" },
            { label: "TAKEN", value: formatNumber(Math.floor(combatResult.damageTaken)), color: "#FE0022" },
            { label: "ACCURACY", value: `${Math.floor(combatResult.accuracy)}%`, color: "#FFE735" },
          ].map((s) => (
            <KPanel key={s.label} className="text-center">
              <div className="text-[9px] font-display text-k5-muted tracking-wider">{s.label}</div>
              <div className="font-display text-lg" style={{ color: s.color }}>{s.value}</div>
            </KPanel>
          ))}
        </div>
      </main>

      {/* Footer */}
      <footer className="relative z-10 px-4 py-4 safe-bottom border-t border-k5-border/50 bg-k5-panel/40 backdrop-blur-sm">
        <div className="max-w-2xl mx-auto flex gap-3">
          <KButton variant="primary" size="lg" className="flex-1" onClick={() => setScreen("lobby")}>
            {t(language, "summary.ok")}
          </KButton>
        </div>
      </footer>
    </div>
  );
}
