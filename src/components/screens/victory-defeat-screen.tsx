"use client";

/**
 * KINETICS 5 — Victory / Defeat (PDF page 7)
 * VICTORY : titre cyan glow, récompenses +5000 CR +2500 XP, boutons
 * FAILED : titre rouge, mêmes boutons
 */

import { KButton, KCurrency, KPanel } from "@/components/kinetics/ui";
import { StarfieldBackground } from "@/components/kinetics/visuals";
import { useGameStore } from "@/store/game-store";
import { t } from "@/lib/i18n";
import { useEffect } from "react";

export function VictoryDefeatScreen() {
  const { combatResult, setScreen, language } = useGameStore();
  const victory = combatResult?.victory ?? false;
  const mission = useGameStore.getState().currentMissionId;

  // Auto-redirect to summary after a few seconds (or on Continue)
  useEffect(() => {
    // Confetti effect could go here
  }, []);

  const title = victory ? t(language, "victory.title") : t(language, "defeat.title");
  const titleColor = victory ? "#1AA1CE" : "#FE0022";

  return (
    <div className="relative min-h-screen w-full overflow-hidden flex flex-col items-center justify-center bg-k5-deep-space">
      <StarfieldBackground density={victory ? 120 : 40} />

      {/* Overlay couleur */}
      <div
        className="absolute inset-0"
        style={{
          background: victory
            ? "radial-gradient(ellipse at center, rgba(26, 161, 206, 0.15) 0%, transparent 60%)"
            : "radial-gradient(ellipse at center, rgba(254, 0, 34, 0.12) 0%, transparent 60%)",
        }}
      />

      <div className="relative z-10 flex flex-col items-center px-6">
        {/* Titre */}
        <h1
          className="font-display text-7xl md:text-8xl mb-2"
          style={{
            color: titleColor,
            textShadow: `0 0 30px ${titleColor}, 0 0 60px ${titleColor}66`,
            animation: victory ? "k5-pulse-cyan 2s ease-in-out infinite" : undefined,
          }}
        >
          {title}
        </h1>

        {/* Ligne décorative */}
        <div className="h-px w-64 mb-6" style={{ background: `linear-gradient(90deg, transparent, ${titleColor}, transparent)` }} />

        {/* Stats rapides */}
        <KPanel className="mb-6 w-full max-w-md" scanlines>
          <div className="grid grid-cols-3 gap-4 text-center">
            <div>
              <div className="text-[10px] font-display text-k5-muted tracking-wider">KILLS</div>
              <div className="font-display text-2xl text-white">{combatResult?.enemiesKilled ?? 0}</div>
            </div>
            <div>
              <div className="text-[10px] font-display text-k5-muted tracking-wider">ACCURACY</div>
              <div className="font-display text-2xl text-k5-cyan">{Math.floor(combatResult?.accuracy ?? 0)}%</div>
            </div>
            <div>
              <div className="text-[10px] font-display text-k5-muted tracking-wider">TIME</div>
              <div className="font-display text-2xl text-white">
                {Math.floor((combatResult?.timeElapsed ?? 0) / 60)}:
                {String(Math.floor((combatResult?.timeElapsed ?? 0) % 60)).padStart(2, "0")}
              </div>
            </div>
          </div>
        </KPanel>

        {/* Récompenses */}
        {victory && combatResult && (
          <div className="flex items-center gap-6 mb-6">
            <KCurrency type="cr" value={combatResult.rewards.cr} />
            <div className="h-8 w-px bg-k5-border" />
            <KCurrency type="xp" value={combatResult.rewards.xp} />
          </div>
        )}

        {/* Boutons */}
        <div className="flex flex-col sm:flex-row gap-3 w-full max-w-md">
          <KButton
            variant="primary"
            size="lg"
            glow={victory}
            className="flex-1"
            onClick={() => setScreen("summary")}
          >
            {t(language, "result.continue")}
          </KButton>
          <KButton variant="secondary" size="lg" className="flex-1" onClick={() => setScreen("loading")}>
            {t(language, "result.rematch")}
          </KButton>
        </div>
        <div className="flex gap-3 mt-3 w-full max-w-md">
          <KButton variant="ghost" size="md" className="flex-1" onClick={() => setScreen("settings")}>
            {t(language, "result.settings")}
          </KButton>
          <KButton variant="ghost" size="md" className="flex-1" onClick={() => setScreen("lobby")}>
            {t(language, "result.reviewMissions")}
          </KButton>
        </div>
      </div>
    </div>
  );
}
