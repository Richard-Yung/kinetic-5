"use client";

/**
 * KINETICS 5 — Écran de démarrage
 * Détails cyberpunk/retro appliqués :
 * - Texte grisâtre (pas blanc pur) : #C8CDD0
 * - Logo deux tons : "KINETICS" bleu-cyan, "5" gris
 * - Boutons à bordures coupées (clip-path cyberpunk)
 * - Overlay sombre transparent sur le background
 * - Lignes fines gris-blanc décoratives
 * - NEW GAME : fond bleu, autres : texte simple sans fond
 */

import { useGameStore } from "@/store/game-store";
import { t } from "@/lib/i18n";
import { useState } from "react";

export function StartScreen() {
  const setScreen = useGameStore((s) => s.setScreen);
  const language = useGameStore((s) => s.language);
  const [hovered, setHovered] = useState<string | null>(null);

  const menu = [
    { id: "new", label: t(language, "start.newGame"), action: () => setScreen("loading"), primary: true },
    { id: "continue", label: t(language, "start.continue"), action: () => setScreen("mission_select") },
    { id: "load", label: t(language, "start.loadGame"), action: () => setScreen("mission_select") },
    { id: "options", label: t(language, "start.options"), action: () => setScreen("settings") },
    { id: "quit", label: t(language, "start.quit"), action: () => window.close() },
  ];

  // Couleur texte grisâtre (pas blanc pur)
  const textColor = "#C8CDD0";

  return (
    <div className="relative w-full h-screen min-h-[300px] overflow-hidden">
      {/* Background full-screen */}
      <img
        src="/kinetics/bg-everspace.jpg"
        alt=""
        className="absolute inset-0 w-full h-full object-cover"
        aria-hidden
      />

      {/* Overlay sombre transparent (sous-couche) pour faire ressortir les textes */}
      <div className="absolute inset-0" style={{ background: "rgba(5, 8, 16, 0.65)" }} />

      {/* Teinte bleue froide */}
      <div className="absolute inset-0" style={{ background: "rgba(20, 40, 80, 0.18)", mixBlendMode: "color" }} />

      {/* Degrade radial */}
      <div
        className="absolute inset-0"
        style={{
          background: "radial-gradient(ellipse at 60% 45%, rgba(26, 161, 206, 0.1) 0%, transparent 50%, rgba(0, 0, 5, 0.4) 100%)",
        }}
      />

      {/* Lignes fines gris-blanc décoratives (horizontales) */}
      <div className="absolute inset-0 pointer-events-none" style={{ zIndex: 2 }}>
        <div className="absolute left-0 right-0" style={{ top: "18%", height: "1px", background: "linear-gradient(to right, transparent, rgba(200, 205, 208, 0.25) 20%, rgba(200, 205, 208, 0.25) 80%, transparent)" }} />
        <div className="absolute left-0 right-0" style={{ top: "82%", height: "1px", background: "linear-gradient(to right, transparent, rgba(200, 205, 208, 0.2) 20%, rgba(200, 205, 208, 0.2) 80%, transparent)" }} />
      </div>

      {/* Lignes verticales fines décoratives (bords) */}
      <div className="absolute pointer-events-none" style={{ left: "3%", top: "10%", bottom: "10%", width: "1px", background: "linear-gradient(to bottom, transparent, rgba(200, 205, 208, 0.15) 30%, rgba(200, 205, 208, 0.15) 70%, transparent)", zIndex: 2 }} />
      <div className="absolute pointer-events-none" style={{ right: "3%", top: "10%", bottom: "10%", width: "1px", background: "linear-gradient(to bottom, transparent, rgba(200, 205, 208, 0.15) 30%, rgba(200, 205, 208, 0.15) 70%, transparent)", zIndex: 2 }} />

      {/* Logo top-left — deux tons : KINETICS bleu-cyan, 5 gris */}
      <div className="absolute" style={{ top: "6%", left: "5%", zIndex: 10 }}>
        <h1
          className="font-display leading-none tracking-wider flex items-baseline"
          style={{
            fontSize: "clamp(1.5rem, 5vw, 3.5rem)",
            textShadow: "0 0 18px rgba(26, 161, 206, 0.6), 0 2px 6px rgba(0,0,0,0.9)",
          }}
        >
          <span style={{ color: "#1AA1CE" }}>KINETICS</span>
          <span style={{ color: textColor, margin: "0 2px" }}>·</span>
          <span style={{ color: textColor }}>5</span>
        </h1>
      </div>

      {/* 5 boutons en ligne — bordures coupées cyberpunk (clip-path) */}
      <nav
        className="absolute left-1/2 -translate-x-1/2 flex flex-row flex-nowrap items-center gap-3 sm:gap-4"
        style={{ top: "55%", zIndex: 10 }}
      >
        {menu.map((item) => (
          <button
            key={item.id}
            onMouseEnter={() => setHovered(item.id)}
            onMouseLeave={() => setHovered(null)}
            onClick={item.action}
            className={`font-display uppercase tracking-wider whitespace-nowrap transition-all duration-150 select-none ${
              item.primary ? "px-5 py-2.5" : "px-3 py-2.5"
            } ${hovered === item.id ? (item.primary ? "brightness-110 scale-105" : "scale-105") : ""}`}
            style={{
              fontSize: "clamp(0.7rem, 1.8vw, 1.1rem)",
              // Bordure coupée cyberpunk : coin haut-droit + bas-gauche coupés
              clipPath: "polygon(0 0, calc(100% - 10px) 0, 100% 10px, 100% 100%, 10px 100%, 0 calc(100% - 10px))",
              background: item.primary ? "#1AA1CE" : "rgba(10, 20, 35, 0.6)",
              color: item.primary ? "#FFFFFF" : textColor,
              border: item.primary ? "1px solid #1AA1CE" : "1px solid rgba(26, 161, 206, 0.35)",
              boxShadow: item.primary
                ? "0 0 16px rgba(26, 161, 206, 0.5), inset 0 0 8px rgba(255,255,255,0.1)"
                : hovered === item.id
                ? "0 0 10px rgba(26, 161, 206, 0.3)"
                : "none",
            }}
          >
            {item.label}
          </button>
        ))}
      </nav>
    </div>
  );
}
