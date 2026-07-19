"use client";

/**
 * KINETICS 5 — Écran de chargement
 * Full-screen, background armure, TIP top-left, LOADING+barre+% en bas.
 */

import { useEffect } from "react";
import { useGameStore, useSelectedMission } from "@/store/game-store";
import { t } from "@/lib/i18n";

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
    <div className="relative w-full h-screen min-h-[300px] overflow-hidden">
      {/* Background full-screen (armure) */}
      <img
        src="/kinetics/bg-loading.jpg"
        alt=""
        className="absolute inset-0 w-full h-full object-cover"
        aria-hidden
      />

      {/* Degrade bleu fonce vers noir */}
      <div
        className="absolute inset-0"
        style={{
          background: "linear-gradient(to bottom, rgba(0, 0, 30, 0.4) 0%, transparent 40%, rgba(0, 0, 0, 0.6) 100%)",
        }}
      />

      {/* Teinte bleue froide */}
      <div
        className="absolute inset-0"
        style={{ background: "rgba(10, 30, 60, 0.12)", mixBlendMode: "color" }}
      />

      {/* Grille subtile teal */}
      <div
        className="absolute inset-0"
        style={{
          backgroundImage:
            "linear-gradient(rgba(0, 206, 209, 0.06) 1px, transparent 1px), linear-gradient(90deg, rgba(0, 206, 209, 0.06) 1px, transparent 1px)",
          backgroundSize: "40px 40px",
        }}
      />

      {/* TIP box top-left */}
      <div
        className="absolute"
        style={{ top: "5%", left: "4%", maxWidth: "45%", zIndex: 10, background: "rgba(0, 0, 0, 0.5)", padding: "10px 14px", borderRadius: "4px" }}
      >
        <div className="text-[9px] font-display tracking-wider text-k5-cyan mb-0.5">
          {t(language, "loading.tip")}
        </div>
        <p className="text-[10px] text-white leading-relaxed line-clamp-3">{loadingTip}</p>
      </div>

      {/* Info mission top-right */}
      <div className="absolute text-right" style={{ top: "5%", right: "4%", zIndex: 10 }}>
        <div className="font-display text-sm text-white k5-text-glow-cyan">{mission.displayName}</div>
        <div className="text-[9px] text-k5-cyan">{mission.type.toUpperCase()} • {mission.region.toUpperCase()}</div>
      </div>

      {/* LOADING + barre + % en bas */}
      <div className="absolute" style={{ bottom: "8%", left: "4%", right: "4%", zIndex: 10 }}>
        <div className="flex items-baseline justify-between mb-2">
          <span
            className="font-display text-white tracking-wider"
            style={{ fontSize: "clamp(0.8rem, 2vw, 1.2rem)", textShadow: "0 0 12px rgba(26, 161, 206, 0.6)" }}
          >
            {t(language, "loading.loading")}
          </span>
          <span
            className="font-display text-white tabular-nums"
            style={{ fontSize: "clamp(1.5rem, 4vw, 2.5rem)", textShadow: "0 0 16px rgba(26, 161, 206, 0.6)" }}
          >
            {Math.floor(loadingProgress)}%
          </span>
        </div>

        {/* Barre de progression */}
        <div
          className="relative overflow-hidden"
          style={{ height: "8px", background: "#222222", borderRadius: "4px", width: "100%", boxShadow: "0 0 8px rgba(0, 206, 209, 0.4)" }}
        >
          <div
            className="absolute inset-y-0 left-0 transition-all duration-150"
            style={{
              width: `${loadingProgress}%`,
              background: "linear-gradient(to right, #00CED1, #00FFFF)",
              borderRadius: "4px",
              boxShadow: "0 0 12px rgba(0, 206, 209, 0.8)",
            }}
          />
          <div
            className="absolute inset-y-0 w-10"
            style={{ left: `${Math.max(0, loadingProgress - 10)}%`, background: "linear-gradient(to right, transparent, rgba(255,255,255,0.4), transparent)" }}
          />
        </div>

        {/* Indicateurs etapes */}
        <div className="mt-2 flex justify-center gap-4 text-[8px] font-display">
          {["TERRAIN", "ENTITIES", "AUDIO", "NETWORK"].map((step, i) => {
            const threshold = (i + 1) * 25;
            const done = loadingProgress >= threshold;
            return (
              <div key={step} className="flex items-center gap-1">
                <span className={`inline-block w-1.5 h-1.5 rounded-full ${done ? "bg-k5-green" : "bg-k5-muted"}`} style={done ? { boxShadow: "0 0 6px #6CF42E" } : {}} />
                <span className={done ? "text-k5-green" : "text-k5-muted"}>{step}</span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
