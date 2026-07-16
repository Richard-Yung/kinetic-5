"use client";

/**
 * KINETICS 5 — Écran de chargement (PDF page 2 — écran du bas)
 * Structure EXACTE d'après la référence :
 * - Conteneur arrondi centré sur fond noir
 * - Background (personnage armuré + bataille) À L'INTÉRIEUR du conteneur
 * - TIP box en haut du conteneur
 * - LOADING + barre + % centrés horizontalement dans le conteneur
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
    <div className="relative w-full h-screen min-h-[400px] bg-black overflow-hidden flex items-center justify-center">
      {/* === CONTENEUR ARRONDI === */}
      <div
        className="relative overflow-hidden"
        style={{
          width: "88%",
          height: "75%",
          borderRadius: "16px",
          border: "2px solid rgba(26, 161, 206, 0.4)",
          boxShadow: "0 0 40px rgba(26, 161, 206, 0.15), 0 8px 32px rgba(0,0,0,0.8)",
        }}
      >
        {/* Background (personnage + bataille) À L'INTÉRIEUR du conteneur */}
        <div
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage: "url(/kinetics/loading-bg-clean.png)",
            backgroundPosition: "center center",
          }}
        />
        <div className="absolute inset-0 bg-gradient-to-b from-black/20 via-transparent to-black/30" />
        <div className="absolute inset-0 k5-grid-bg opacity-10" />

        {/* === TIP TEXT BOX : haut du conteneur === */}
        <div
          className="absolute bg-black/80 backdrop-blur-sm border-l-2 border-k5-yellow px-3 py-2"
          style={{ top: "6%", left: "4%", maxWidth: "45%", zIndex: 10, borderRadius: "4px" }}
        >
          <div className="text-[9px] font-display tracking-wider text-k5-yellow mb-0.5">
            {t(language, "loading.tip")}
          </div>
          <p className="text-[10px] text-white/90 leading-relaxed line-clamp-2">{loadingTip}</p>
        </div>

        {/* === Info mission : haut-droite du conteneur === */}
        <div
          className="absolute text-right"
          style={{ top: "6%", right: "4%", zIndex: 10 }}
        >
          <div className="font-display text-sm text-white k5-text-glow-cyan">{mission.displayName}</div>
          <div className="text-[9px] text-k5-cyan">{mission.type.toUpperCase()} • {mission.region.toUpperCase()}</div>
          <div className="text-[8px] text-k5-muted">REC. POWER {mission.recommendedPower}</div>
        </div>

        {/* === LOADING + barre + % : centrés horizontalement, 35% du haut === */}
        <div
          className="absolute"
          style={{ top: "40%", left: "50%", transform: "translateX(-50%)", width: "80%", zIndex: 10 }}
        >
          {/* LOADING text + % */}
          <div className="flex items-baseline justify-between mb-2">
            <span className="font-display text-sm text-k5-cyan k5-text-glow-cyan tracking-wider">
              {t(language, "loading.loading")}
            </span>
            <span className="font-display text-3xl sm:text-4xl text-white tabular-nums k5-text-glow-cyan">
              {Math.floor(loadingProgress)}%
            </span>
          </div>

          {/* Barre de progression */}
          <div className="relative h-3 bg-black/80 border border-k5-border rounded-sm overflow-hidden">
            <div
              className="absolute inset-y-0 left-0 bg-gradient-to-r from-k5-cyan-dark to-k5-cyan transition-all duration-150"
              style={{ width: `${loadingProgress}%`, boxShadow: "0 0 12px #1AA1CE" }}
            />
            {/* Segments */}
            <div className="absolute inset-0 flex pointer-events-none">
              {Array.from({ length: 20 }).map((_, i) => (
                <div key={i} className="flex-1 border-r border-black/60 last:border-r-0" />
              ))}
            </div>
            {/* Scan effect */}
            <div
              className="absolute inset-y-0 w-12 bg-gradient-to-r from-transparent via-white/30 to-transparent k5-scan"
              style={{ left: `${Math.max(0, loadingProgress - 12)}%` }}
            />
          </div>

          {/* Indicateurs étapes */}
          <div className="mt-2 flex justify-center gap-4 text-[8px] font-display">
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

        {/* === Bas : time + vagues === */}
        <div
          className="absolute flex justify-between text-[9px] font-display text-k5-muted"
          style={{ bottom: "4%", left: "4%", right: "4%", zIndex: 10 }}
        >
          <span>{t(language, "hud.timeLeft")}: {formatTime(mission.timeLimit)}</span>
          <span>VAGUES: {mission.waves.length}</span>
        </div>

        {/* Coins décoratifs */}
        <div className="pointer-events-none absolute top-0 left-0 w-10 h-10 border-l-2 border-t-2 border-k5-cyan/40" />
        <div className="pointer-events-none absolute top-0 right-0 w-10 h-10 border-r-2 border-t-2 border-k5-cyan/40" />
        <div className="pointer-events-none absolute bottom-0 left-0 w-10 h-10 border-l-2 border-b-2 border-k5-cyan/40" />
        <div className="pointer-events-none absolute bottom-0 right-0 w-10 h-10 border-r-2 border-b-2 border-k5-cyan/40" />
      </div>
    </div>
  );
}
