"use client";

/**
 * KINETICS 5 — Écran de démarrage (PDF page 2 — écran du haut)
 * Recodage EXACT d'après la référence :
 * - Conteneur arrondi centré (85% largeur × 60% hauteur) sur fond noir
 * - Background spatial (planète) À L'INTÉRIEUR du conteneur
 * - Logo KINETICS·5 top-left du conteneur
 * - 5 boutons horizontaux à 55% du conteneur, tous avec fond visible
 * - AUCUN élément supplémentaire (pas de version, pas de server status)
 */

import { useGameStore } from "@/store/game-store";
import { t } from "@/lib/i18n";
import { useState } from "react";

export function StartScreen() {
  const setScreen = useGameStore((s) => s.setScreen);
  const language = useGameStore((s) => s.language);
  const [hoveredBtn, setHoveredBtn] = useState<string | null>(null);

  const menu = [
    { id: "new", label: t(language, "start.newGame"), action: () => setScreen("loading"), primary: true },
    { id: "continue", label: t(language, "start.continue"), action: () => setScreen("lobby") },
    { id: "load", label: t(language, "start.loadGame"), action: () => setScreen("lobby") },
    { id: "options", label: t(language, "start.options"), action: () => setScreen("settings") },
    { id: "quit", label: t(language, "start.quit"), action: () => window.close() },
  ];

  return (
    <div className="relative w-full h-screen min-h-[400px] bg-black overflow-hidden flex items-center justify-center">
      {/* === CONTENEUR ARRONDI (85% largeur × 60% hauteur) === */}
      <div
        className="relative overflow-hidden"
        style={{
          width: "88%",
          height: "65%",
          borderRadius: "16px",
          border: "2px solid rgba(26, 161, 206, 0.4)",
          boxShadow: "0 0 40px rgba(26, 161, 206, 0.15), 0 8px 32px rgba(0,0,0,0.8)",
        }}
      >
        {/* Background spatial À L'INTÉRIEUR du conteneur */}
        <div
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage: "url(/kinetics/start-bg-clean.png)",
            backgroundPosition: "center 35%",
          }}
        />
        {/* Léger gradient vers le bas pour lisibilité */}
        <div className="absolute inset-0 bg-gradient-to-b from-transparent via-transparent to-black/40" />
        {/* Grille subtile */}
        <div className="absolute inset-0 k5-grid-bg opacity-10" />

        {/* === LOGO KINETICS·5 : top-left du conteneur (15% gauche, 20% haut) === */}
        <div className="absolute" style={{ top: "12%", left: "5%", zIndex: 10 }}>
          <div className="font-display text-2xl sm:text-3xl md:text-4xl text-white tracking-wider leading-none"
               style={{ textShadow: "0 0 20px rgba(26, 161, 206, 0.8), 0 2px 8px rgba(0,0,0,0.8)" }}>
            KINETICS<span className="text-k5-cyan mx-0.5">·</span>5
          </div>
        </div>

        {/* === 5 BOUTONS HORIZONTAUX à 55% du conteneur, centrés === */}
        <nav
          className="absolute flex flex-row flex-nowrap justify-center items-center gap-2"
          style={{ top: "50%", left: "50%", transform: "translate(-50%, -50%)", zIndex: 10, width: "92%" }}
        >
          {menu.map((item) => (
            <button
              key={item.id}
              onMouseEnter={() => setHoveredBtn(item.id)}
              onMouseLeave={() => setHoveredBtn(null)}
              onClick={item.action}
              className={`
                flex-1 min-w-0 px-3 py-3 text-[10px] sm:text-xs font-display tracking-wider uppercase
                whitespace-nowrap transition-all duration-150 select-none no-select
                ${item.primary
                  ? "bg-k5-cyan text-k5-deep-space"
                  : "bg-k5-deep-space/85 text-white border border-k5-cyan/50"
                }
                ${hoveredBtn === item.id ? "scale-105 -translate-y-0.5 " + (item.primary ? "brightness-110" : "bg-k5-panel border-k5-cyan") : ""}
              `}
              style={{
                borderRadius: "8px",
                boxShadow: item.primary
                  ? "0 0 20px rgba(26, 161, 206, 0.6), 0 4px 12px rgba(0,0,0,0.4)"
                  : "0 2px 8px rgba(0,0,0,0.5)",
                border: item.primary ? "none" : "1px solid rgba(26, 161, 206, 0.5)",
              }}
            >
              {item.label}
            </button>
          ))}
        </nav>

        {/* Coins décoratifs à l'intérieur du conteneur */}
        <div className="pointer-events-none absolute top-0 left-0 w-10 h-10 border-l-2 border-t-2 border-k5-cyan/40" />
        <div className="pointer-events-none absolute top-0 right-0 w-10 h-10 border-r-2 border-t-2 border-k5-cyan/40" />
        <div className="pointer-events-none absolute bottom-0 left-0 w-10 h-10 border-l-2 border-b-2 border-k5-cyan/40" />
        <div className="pointer-events-none absolute bottom-0 right-0 w-10 h-10 border-r-2 border-b-2 border-k5-cyan/40" />
      </div>
    </div>
  );
}
