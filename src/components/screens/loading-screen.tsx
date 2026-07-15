"use client";

/**
 * KINETICS 5 — Écran de chargement (PDF page 2)
 * Tip rotatif + barre de progression segmentée + aperçu mission
 */

import { useEffect } from "react";
import { KPanel, KProgressBar } from "@/components/kinetics/ui";
import { useGameStore, useSelectedMission } from "@/store/game-store";
import { t } from "@/lib/i18n";
import { formatTime } from "@/lib/kinetics-data";

export function LoadingScreen() {
  const { language, loadingProgress, loadingTip, setLoadingProgress, setScreen, currentMissionId } = useGameStore();
  const mission = useSelectedMission();

  useEffect(() => {
    let p = loadingProgress;
    const interval = setInterval(() => {
      p += Math.random() * 8 + 2;
      if (p >= 100) {
        p = 100;
        clearInterval(interval);
        setTimeout(() => setScreen("mission"), 600);
      }
      setLoadingProgress(Math.min(100, p));
    }, 120);
    return () => clearInterval(interval);
  }, [loadingProgress, setScreen, setLoadingProgress]);

  return (
    <div className="relative min-h-screen w-full overflow-hidden flex flex-col items-center justify-center px-6">
      {/* Fond */}
      <div
        className="absolute inset-0 bg-cover bg-center opacity-40"
        style={{ backgroundImage: "url(/kinetics/start-bg.png)" }}
      />
      <div className="absolute inset-0 bg-gradient-to-b from-k5-deep-space/80 via-k5-deep-space/90 to-k5-deep-space" />
      <div className="absolute inset-0 k5-grid-bg opacity-20" />

      <div className="relative z-10 w-full max-w-lg">
        {/* Aperçu mission */}
        <KPanel className="mb-6" scanlines>
          <div className="flex items-center justify-between mb-3">
            <span className="text-[10px] font-display tracking-wider text-k5-cyan">
              {mission.type.toUpperCase()} • {mission.region.toUpperCase()}
            </span>
            <span className="text-[10px] font-display text-k5-muted">
              REC. POWER {mission.recommendedPower}
            </span>
          </div>
          <h2 className="font-display text-2xl text-white k5-text-glow-cyan mb-1">
            {mission.displayName}
          </h2>
          <p className="text-xs text-k5-muted leading-relaxed">{mission.brief}</p>
          <div className="mt-3 grid grid-cols-2 gap-2 text-[10px]">
            <div className="flex justify-between bg-k5-panel/60 px-2 py-1 rounded-sm">
              <span className="text-k5-muted">{t(language, "hud.timeLeft")}</span>
              <span className="font-display text-white">{formatTime(mission.timeLimit)}</span>
            </div>
            <div className="flex justify-between bg-k5-panel/60 px-2 py-1 rounded-sm">
              <span className="text-k5-muted">VAGUES</span>
              <span className="font-display text-white">{mission.waves.length}</span>
            </div>
          </div>
        </KPanel>

        {/* Barre de progression */}
        <div className="space-y-2">
          <div className="flex justify-between items-baseline">
            <span className="font-display text-sm text-k5-cyan k5-text-glow-cyan">
              {t(language, "loading.loading")}
            </span>
            <span className="font-display text-3xl text-white tabular-nums">
              {Math.floor(loadingProgress)}%
            </span>
          </div>
          <div className="relative h-3 bg-k5-panel border border-k5-border rounded-sm overflow-hidden">
            <div
              className="absolute inset-y-0 left-0 bg-gradient-to-r from-k5-cyan-dark to-k5-cyan transition-all duration-150"
              style={{ width: `${loadingProgress}%`, boxShadow: "0 0 12px #1AA1CE" }}
            />
            <div
              className="absolute inset-y-0 w-12 bg-gradient-to-r from-transparent via-white/40 to-transparent k5-scan"
              style={{ left: `${loadingProgress - 12}%` }}
            />
          </div>

          {/* Tip */}
          <div className="mt-4 p-3 bg-k5-panel/60 border-l-2 border-k5-yellow k5-clip-sm">
            <div className="text-[10px] font-display tracking-wider text-k5-yellow mb-1">
              {t(language, "loading.tip")}
            </div>
            <p className="text-xs text-white/90 leading-relaxed">{loadingTip}</p>
          </div>
        </div>

        {/* Indicateurs chargement étapes */}
        <div className="mt-4 flex justify-center gap-6 text-[10px] font-display">
          {["TERRAIN", "ENTITIES", "AUDIO", "NETWORK"].map((step, i) => {
            const threshold = (i + 1) * 25;
            const done = loadingProgress >= threshold;
            return (
              <div key={step} className="flex items-center gap-1">
                <span
                  className={`inline-block w-1.5 h-1.5 rounded-full ${
                    done ? "bg-k5-green" : "bg-k5-muted"
                  }`}
                  style={done ? { boxShadow: "0 0 6px #6CF42E" } : {}}
                />
                <span className={done ? "text-k5-green" : "text-k5-muted"}>{step}</span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
