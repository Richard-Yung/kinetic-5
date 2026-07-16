"use client";

/**
 * KINETICS 5 — Écran de chargement (PDF page 2 — écran du bas)
 * Positionnement EXACT d'après l'image de référence :
 * - Character : déjà dans le background (droite)
 * - TIP box : 15% haut, 12% gauche
 * - LOADING text + barre + % : BAS de l'écran (85% haut), 12% gauche
 * - Barre : 76% largeur
 */

import { useEffect } from "react";
import { useGameStore, useSelectedMission } from "@/store/game-store";
import { t } from "@/lib/i18n";
import { formatTime } from "@/lib/kinetics-data";

export function LoadingScreen() {
  const { language, loadingProgress, loadingTip, setLoadingProgress, setScreen } = useGameStore();
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
    <div className="relative w-full h-screen min-h-[400px] overflow-hidden">
      {/* === BACKGROUND : image source du PDF nettoyée (garde personnage + bataille à droite) === */}
      <div
        className="absolute inset-0 bg-cover bg-center"
        style={{
          backgroundImage: "url(/kinetics/loading-bg-clean.png)",
          backgroundPosition: "center center",
        }}
      />
      <div className="absolute inset-0 bg-gradient-to-b from-k5-deep-space/20 via-transparent to-k5-deep-space/40" />
      <div className="absolute inset-0 k5-grid-bg opacity-10" />

      {/* === TIP TEXT BOX : 15% haut, 12% gauche === */}
      <div
        className="absolute max-w-xs bg-k5-deep-space/85 backdrop-blur-sm border-l-2 border-k5-yellow px-3 py-2 k5-clip-sm"
        style={{ top: "12%", left: "5%", zIndex: 10, maxWidth: "40%" }}
      >
        <div className="text-[9px] font-display tracking-wider text-k5-yellow mb-0.5">
          {t(language, "loading.tip")}
        </div>
        <p className="text-[10px] text-white/90 leading-relaxed line-clamp-3">{loadingTip}</p>
      </div>

      {/* === ÉLÉMENTS LOADING : BAS de l'écran (85% haut), 12% gauche === */}
      <div
        className="absolute"
        style={{ bottom: "8%", left: "5%", right: "5%", zIndex: 10 }}
      >
        <div style={{ width: "76%", maxWidth: "600px" }}>
          {/* LOADING text + % */}
          <div className="flex items-baseline justify-between mb-1.5">
            <span className="font-display text-sm text-k5-cyan k5-text-glow-cyan tracking-wider">
              {t(language, "loading.loading")}
            </span>
            <span className="font-display text-3xl sm:text-4xl text-white tabular-nums k5-text-glow-cyan">
              {Math.floor(loadingProgress)}%
            </span>
          </div>

          {/* Barre de progression */}
          <div className="relative h-3 bg-k5-panel/90 border border-k5-border rounded-sm overflow-hidden">
            <div
              className="absolute inset-y-0 left-0 bg-gradient-to-r from-k5-cyan-dark to-k5-cyan transition-all duration-150"
              style={{ width: `${loadingProgress}%`, boxShadow: "0 0 12px #1AA1CE" }}
            />
            {/* Segments */}
            <div className="absolute inset-0 flex pointer-events-none">
              {Array.from({ length: 20 }).map((_, i) => (
                <div key={i} className="flex-1 border-r border-k5-deep-space/60 last:border-r-0" />
              ))}
            </div>
            {/* Scan effect */}
            <div
              className="absolute inset-y-0 w-12 bg-gradient-to-r from-transparent via-white/30 to-transparent k5-scan"
              style={{ left: `${Math.max(0, loadingProgress - 12)}%` }}
            />
          </div>

          {/* Indicateurs étapes */}
          <div className="mt-2 flex gap-3 text-[8px] font-display">
            {["TERRAIN", "ENTITIES", "AUDIO", "NETWORK"].map((step, i) => {
              const threshold = (i + 1) * 25;
              const done = loadingProgress >= threshold;
              return (
                <div key={step} className="flex items-center gap-1">
                  <span
                    className={`inline-block w-1.5 h-1.5 rounded-full ${done ? "bg-k5-green" : "bg-k5-muted"}`}
                    style={done ? { boxShadow: "0 0 6px #6CF42E" } : {}}
                  />
                  <span className={done ? "text-k5-green" : "text-k5-muted"}>{step}</span>
                </div>
              );
            })}
          </div>
        </div>
      </div>

      {/* === Info mission en bas à droite === */}
      <div
        className="absolute text-[9px] font-display text-k5-muted text-right"
        style={{ bottom: "8%", right: "5%", zIndex: 10 }}
      >
        <div className="text-k5-cyan">{mission.displayName}</div>
        <div>{mission.type.toUpperCase()} • {mission.region.toUpperCase()}</div>
        <div>REC. POWER {mission.recommendedPower}</div>
        <div>{t(language, "hud.timeLeft")}: {formatTime(mission.timeLimit)}</div>
        <div>VAGUES: {mission.waves.length}</div>
      </div>

      {/* Coins décoratifs */}
      <div className="pointer-events-none absolute top-0 left-0 w-12 h-12 border-l-2 border-t-2 border-k5-cyan/30 z-10" />
      <div className="pointer-events-none absolute top-0 right-0 w-12 h-12 border-r-2 border-t-2 border-k5-cyan/30 z-10" />
      <div className="pointer-events-none absolute bottom-0 left-0 w-12 h-12 border-l-2 border-b-2 border-k5-cyan/30 z-10" />
      <div className="pointer-events-none absolute bottom-0 right-0 w-12 h-12 border-r-2 border-b-2 border-k5-cyan/30 z-10" />
    </div>
  );
}
