"use client";

/**
 * KINETICS 5 — Écran de chargement
 * Détails appliqués :
 * - Barre de progression en PETITS CARRÉS segmentés (~40 segments)
 * - Chargement plus lent (pour admirer la page)
 * - Image de fond fournie (pasted_image_1784439356655.png)
 * - Texte grisâtre (pas blanc pur)
 * - Logo deux tons : KINETICS bleu, 5 gris
 * - Overlay sombre transparent sur le background
 * - Lignes fines gris-blanc décoratives
 * - Bordures coupées cyberpunk sur éléments
 */

import { useEffect } from "react";
import { useGameStore, useSelectedMission } from "@/store/game-store";
import { t } from "@/lib/i18n";

export function LoadingScreen() {
  const { language, loadingProgress, loadingTip, setLoadingProgress, setScreen } = useGameStore();
  const mission = useSelectedMission();

  useEffect(() => {
    let p = loadingProgress;
    // Chargement plus lent : +2-4% toutes les 200ms (au lieu de +8-10% toutes les 120ms)
    const interval = setInterval(() => {
      p += Math.random() * 3 + 1.5;
      if (p >= 100) {
        p = 100;
        clearInterval(interval);
        setTimeout(() => setScreen("mission"), 1000);
      }
      setLoadingProgress(Math.min(100, p));
    }, 200);
    return () => clearInterval(interval);
  }, [loadingProgress, setScreen, setLoadingProgress]);

  // Couleur texte grisâtre
  const textColor = "#C8CDD0";
  // Nombre de segments (petits carrés) de la barre
  const SEGMENTS = 40;
  const filledSegments = Math.round((loadingProgress / 100) * SEGMENTS);

  return (
    <div className="relative w-full h-screen min-h-[300px] overflow-hidden">
      {/* Background full-screen (image fournie) */}
      <img
        src="/kinetics/bg-loading-v2.png"
        alt=""
        className="absolute inset-0 w-full h-full object-cover"
        aria-hidden
      />

      {/* Overlay sombre transparent (sous-couche) pour faire ressortir les textes */}
      <div className="absolute inset-0" style={{ background: "rgba(5, 8, 16, 0.7)" }} />

      {/* Teinte bleue froide */}
      <div className="absolute inset-0" style={{ background: "rgba(10, 30, 60, 0.15)", mixBlendMode: "color" }} />

      {/* Degrade vertical */}
      <div
        className="absolute inset-0"
        style={{
          background: "linear-gradient(to bottom, rgba(0, 10, 30, 0.3) 0%, transparent 40%, rgba(0, 0, 5, 0.5) 100%)",
        }}
      />

      {/* Lignes fines gris-blanc décoratives */}
      <div className="absolute inset-0 pointer-events-none" style={{ zIndex: 2 }}>
        <div className="absolute left-0 right-0" style={{ top: "15%", height: "1px", background: "linear-gradient(to right, transparent, rgba(200, 205, 208, 0.25) 20%, rgba(200, 205, 208, 0.25) 80%, transparent)" }} />
        <div className="absolute left-0 right-0" style={{ top: "85%", height: "1px", background: "linear-gradient(to right, transparent, rgba(200, 205, 208, 0.2) 20%, rgba(200, 205, 208, 0.2) 80%, transparent)" }} />
      </div>
      <div className="absolute pointer-events-none" style={{ left: "3%", top: "8%", bottom: "8%", width: "1px", background: "linear-gradient(to bottom, transparent, rgba(200, 205, 208, 0.15) 30%, rgba(200, 205, 208, 0.15) 70%, transparent)", zIndex: 2 }} />
      <div className="absolute pointer-events-none" style={{ right: "3%", top: "8%", bottom: "8%", width: "1px", background: "linear-gradient(to bottom, transparent, rgba(200, 205, 208, 0.15) 30%, rgba(200, 205, 208, 0.15) 70%, transparent)", zIndex: 2 }} />

      {/* Logo deux tons : KINETICS bleu, 5 gris — top-left */}
      <div className="absolute" style={{ top: "5%", left: "5%", zIndex: 10 }}>
        <h1
          className="font-display leading-none tracking-wider flex items-baseline"
          style={{
            fontSize: "clamp(1.3rem, 4vw, 2.8rem)",
            textShadow: "0 0 18px rgba(26, 161, 206, 0.6), 0 2px 6px rgba(0,0,0,0.9)",
          }}
        >
          <span style={{ color: "#1AA1CE" }}>KINETICS</span>
          <span style={{ color: textColor, margin: "0 2px" }}>·</span>
          <span style={{ color: textColor }}>5</span>
        </h1>
      </div>

      {/* TIP box — bordure coupée cyberpunk, top-left sous le logo */}
      <div
        className="absolute"
        style={{
          top: "18%",
          left: "5%",
          maxWidth: "42%",
          zIndex: 10,
          background: "rgba(0, 0, 0, 0.55)",
          padding: "10px 14px",
          clipPath: "polygon(0 0, calc(100% - 8px) 0, 100% 8px, 100% 100%, 8px 100%, 0 calc(100% - 8px))",
          borderLeft: "2px solid #FFE735",
        }}
      >
        <div className="text-[9px] font-display tracking-wider mb-1" style={{ color: "#FFE735" }}>
          {t(language, "loading.tip")}
        </div>
        <p className="text-[10px] leading-relaxed line-clamp-3" style={{ color: textColor }}>{loadingTip}</p>
      </div>

      {/* Info mission top-right */}
      <div className="absolute text-right" style={{ top: "5%", right: "5%", zIndex: 10 }}>
        <div className="font-display text-sm" style={{ color: textColor, textShadow: "0 0 10px rgba(26, 161, 206, 0.5)" }}>{mission.displayName}</div>
        <div className="text-[9px]" style={{ color: "#1AA1CE" }}>{mission.type.toUpperCase()} • {mission.region.toUpperCase()}</div>
      </div>

      {/* LOADING + barre en carrés + % — BAS */}
      <div className="absolute" style={{ bottom: "10%", left: "5%", right: "5%", zIndex: 10 }}>
        {/* LOADING text + % */}
        <div className="flex items-baseline justify-between mb-2">
          <span
            className="font-display tracking-wider"
            style={{ fontSize: "clamp(0.8rem, 2vw, 1.2rem)", color: textColor, textShadow: "0 0 10px rgba(26, 161, 206, 0.5)" }}
          >
            {t(language, "loading.loading")}
          </span>
          <span
            className="font-display tabular-nums"
            style={{ fontSize: "clamp(1.5rem, 4vw, 2.5rem)", color: textColor, textShadow: "0 0 14px rgba(26, 161, 206, 0.5)" }}
          >
            {Math.floor(loadingProgress)}%
          </span>
        </div>

        {/* Barre de progression en PETITS CARRÉS segmentés */}
        <div
          className="flex gap-1"
          style={{ width: "100%", height: "10px" }}
        >
          {Array.from({ length: SEGMENTS }).map((_, i) => {
            const isFilled = i < filledSegments;
            const isPartial = i === filledSegments && loadingProgress % (100 / SEGMENTS) > 0;
            return (
              <div
                key={i}
                className="flex-1 transition-all duration-150"
                style={{
                  background: isFilled
                    ? "linear-gradient(to right, #00CED1, #1AA1CE)"
                    : isPartial
                    ? "rgba(26, 161, 206, 0.4)"
                    : "rgba(40, 50, 65, 0.6)",
                  boxShadow: isFilled ? "0 0 4px rgba(0, 206, 209, 0.7)" : "none",
                  clipPath: "polygon(0 0, 100% 0, 100% 100%, 0 100%)",
                }}
              />
            );
          })}
        </div>

        {/* Indicateurs etapes */}
        <div className="mt-3 flex justify-center gap-4 text-[8px] font-display">
          {["TERRAIN", "ENTITIES", "AUDIO", "NETWORK"].map((step, i) => {
            const threshold = (i + 1) * 25;
            const done = loadingProgress >= threshold;
            return (
              <div key={step} className="flex items-center gap-1">
                <span className={`inline-block w-1.5 h-1.5 rounded-full ${done ? "bg-k5-green" : "bg-k5-muted"}`} style={done ? { boxShadow: "0 0 6px #6CF42E" } : {}} />
                <span style={{ color: done ? "#6CF42E" : textColor, opacity: done ? 1 : 0.6 }}>{step}</span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
