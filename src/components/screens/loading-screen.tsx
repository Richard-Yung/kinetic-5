"use client";

/**
 * KINETICS 5 — Écran de chargement (PDF page 2 — écran du bas)
 * Recodé au millimètre près d'après l'image de référence.
 *
 * Layout exact (d'après VLM analyse) :
 * - Background : scène de bataille spatiale (de l'image source du PDF)
 * - Personnage armuré à droite (centre-droite, grand)
 * - TIP text box en haut-centre (fond semi-transparent, texte blanc)
 * - "LOADING" texte à 38% largeur, 32% haut
 * - Barre de progression à 38% largeur, 35% haut (remplissage cyan, 55%)
 * - "55%" à 85% largeur, 34% haut (blanc, gras, grand)
 */

import { useEffect } from "react";
import { useGameStore, useSelectedMission, LOADING_TIPS } from "@/store/game-store";
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
    <div className="relative w-full h-screen min-h-[500px] overflow-hidden flex flex-col">
      {/* === BACKGROUND : image source du PDF nettoyée (scène de bataille + personnage) === */}
      <div
        className="absolute inset-0 bg-cover bg-center"
        style={{
          backgroundImage: "url(/kinetics/loading-bg-clean.png)",
          backgroundPosition: "center center",
        }}
      />
      {/* Overlay sombre pour lisibilité */}
      <div className="absolute inset-0 bg-gradient-to-b from-k5-deep-space/30 via-transparent to-k5-deep-space/60" />
      <div className="absolute inset-0 k5-grid-bg opacity-10" />

      {/* === TIP TEXT BOX (haut-centre) === */}
      <div className="relative z-10 pt-[5%] px-[5%] flex justify-center">
        <div className="max-w-md bg-k5-deep-space/80 backdrop-blur-sm border-l-2 border-k5-yellow px-4 py-2.5 k5-clip-sm">
          <div className="text-[9px] font-display tracking-wider text-k5-yellow mb-0.5">
            {t(language, "loading.tip")}
          </div>
          <p className="text-[11px] text-white/90 leading-relaxed">{loadingTip}</p>
        </div>
      </div>

      {/* === ZONE CENTRE-GAUCHE : LOADING + barre + % === */}
      <div className="relative z-10 flex-1 flex flex-col justify-center px-[8%]">
        <div className="max-w-sm">
          {/* Mission preview */}
          <div className="mb-6">
            <div className="text-[10px] font-display tracking-wider text-k5-cyan mb-1">
              {mission.type.toUpperCase()} • {mission.region.toUpperCase()}
            </div>
            <div className="font-display text-2xl text-white k5-text-glow-cyan mb-1">
              {mission.displayName}
            </div>
            <p className="text-[11px] text-k5-muted leading-relaxed line-clamp-2">{mission.brief}</p>
          </div>

          {/* LOADING text */}
          <div className="flex items-baseline justify-between mb-2">
            <span className="font-display text-sm text-k5-cyan k5-text-glow-cyan tracking-wider">
              {t(language, "loading.loading")}
            </span>
            <span className="font-display text-4xl text-white tabular-nums k5-text-glow-cyan">
              {Math.floor(loadingProgress)}%
            </span>
          </div>

          {/* Barre de progression (style PDF) */}
          <div className="relative h-3 bg-k5-panel border border-k5-border rounded-sm overflow-hidden">
            <div
              className="absolute inset-y-0 left-0 bg-gradient-to-r from-k5-cyan-dark to-k5-cyan transition-all duration-150"
              style={{ width: `${loadingProgress}%`, boxShadow: "0 0 12px #1AA1CE" }}
            />
            {/* Segment dividers */}
            <div className="absolute inset-0 flex">
              {Array.from({ length: 20 }).map((_, i) => (
                <div key={i} className="flex-1 border-r border-k5-deep-space/50 last:border-r-0" />
              ))}
            </div>
            {/* Scan effect */}
            <div
              className="absolute inset-y-0 w-12 bg-gradient-to-r from-transparent via-white/30 to-transparent k5-scan"
              style={{ left: `${loadingProgress - 12}%` }}
            />
          </div>

          {/* Indicateurs étapes */}
          <div className="mt-4 flex gap-4 text-[9px] font-display">
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

      {/* === BAS : info mission === */}
      <div className="relative z-10 px-[5%] pb-4 safe-bottom">
        <div className="flex justify-between text-[9px] font-display text-k5-muted">
          <span>REC. POWER {mission.recommendedPower}</span>
          <span>{t(language, "hud.timeLeft")}: {formatTime(mission.timeLimit)}</span>
          <span>VAGUES: {mission.waves.length}</span>
        </div>
      </div>

      {/* Coins décoratifs */}
      <div className="pointer-events-none absolute top-0 left-0 w-16 h-16 border-l-2 border-t-2 border-k5-cyan/30" />
      <div className="pointer-events-none absolute top-0 right-0 w-16 h-16 border-r-2 border-t-2 border-k5-cyan/30" />
      <div className="pointer-events-none absolute bottom-0 left-0 w-16 h-16 border-l-2 border-b-2 border-k5-cyan/30" />
      <div className="pointer-events-none absolute bottom-0 right-0 w-16 h-16 border-r-2 border-b-2 border-k5-cyan/30" />
    </div>
  );
}
