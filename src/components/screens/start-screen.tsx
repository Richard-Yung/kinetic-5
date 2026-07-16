"use client";

/**
 * KINETICS 5 — Écran de démarrage (PDF page 2 — écran du haut)
 * Positionnement EXACT d'après l'image de référence :
 * - Logo KINETICS·5 : 10% gauche, 12% haut
 * - 5 boutons en UNE SEULE LIGNE horizontale à 35% haut, centrés
 * - NEW GAME : fond cyan solide ; autres : fond sombre visible avec bordure
 * - Boutons NE DOIVENT PAS se replier sur plusieurs lignes
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
    <div className="relative w-full h-screen min-h-[400px] overflow-hidden">
      {/* === BACKGROUND : image source du PDF nettoyée === */}
      <div
        className="absolute inset-0 bg-cover bg-center"
        style={{
          backgroundImage: "url(/kinetics/start-bg-clean.png)",
          backgroundPosition: "center 40%",
        }}
      />
      {/* Vignette + assombrir */}
      <div className="absolute inset-0 bg-gradient-to-b from-k5-deep-space/20 via-transparent to-k5-deep-space/60" />
      <div className="absolute inset-0 pointer-events-none" style={{ boxShadow: "inset 0 0 150px rgba(5, 6, 15, 0.5)" }} />
      <div className="absolute inset-0 k5-grid-bg opacity-15" />

      {/* === LOGO : 10% gauche, 12% haut === */}
      <div className="absolute" style={{ top: "10%", left: "5%", zIndex: 10 }}>
        <div className="font-display text-2xl sm:text-3xl md:text-4xl text-white tracking-wider k5-text-glow-cyan leading-none">
          KINETICS<span className="text-k5-cyan mx-0.5">·</span>5
        </div>
        <div className="text-[9px] sm:text-[10px] font-display tracking-[0.3em] text-k5-cyan/70 mt-1">
          {t(language, "start.tagline")}
        </div>
      </div>

      {/* === BOUTONS : UNE SEULE LIGNE horizontale à 35% haut, centrés === */}
      <nav
        className="absolute flex flex-row flex-nowrap justify-center items-center gap-2"
        style={{ top: "33%", left: "50%", transform: "translateX(-50%)", zIndex: 10, width: "92%" }}
      >
        {menu.map((item) => (
          <button
            key={item.id}
            onMouseEnter={() => setHoveredBtn(item.id)}
            onMouseLeave={() => setHoveredBtn(null)}
            onClick={item.action}
            className={`
              flex-1 min-w-0 max-w-[180px] px-3 py-3 text-[10px] sm:text-xs font-display tracking-wider uppercase
              border-2 transition-all duration-150 select-none no-select k5-clip-sm whitespace-nowrap
              ${item.primary
                ? "bg-k5-cyan text-k5-deep-space border-k5-cyan"
                : "bg-k5-panel/90 text-white border-k5-cyan/40"
              }
              ${hoveredBtn === item.id ? "scale-105 -translate-y-0.5 " + (item.primary ? "brightness-110" : "border-k5-cyan bg-k5-panel-light/90") : ""}
            `}
            style={item.primary
              ? { boxShadow: "0 0 16px rgba(26, 161, 206, 0.6)" }
              : { boxShadow: "0 2px 8px rgba(0,0,0,0.4)" }
            }
          >
            {item.label}
          </button>
        ))}
      </nav>

      {/* === BAS : tagline + statut serveur === */}
      <div className="absolute bottom-0 left-0 right-0 px-[5%] pb-3 safe-bottom flex justify-between items-end text-[9px] font-display tracking-wider text-k5-muted z-10">
        <div>
          <div className="text-k5-cyan/60">EXPLORATION • COMBAT • DOMINATION</div>
          <div className="mt-0.5">v0.1.0 — BUILD 2025.01</div>
        </div>
        <div className="flex items-center gap-2">
          <span className="inline-block w-2 h-2 rounded-full bg-k5-green animate-pulse k5-glow-green" />
          <span>SERVERS ONLINE</span>
        </div>
      </div>

      {/* Coins décoratifs sci-fi */}
      <div className="pointer-events-none absolute top-0 left-0 w-16 h-16 border-l-2 border-t-2 border-k5-cyan/30 z-10" />
      <div className="pointer-events-none absolute top-0 right-0 w-16 h-16 border-r-2 border-t-2 border-k5-cyan/30 z-10" />
      <div className="pointer-events-none absolute bottom-0 left-0 w-16 h-16 border-l-2 border-b-2 border-k5-cyan/30 z-10" />
      <div className="pointer-events-none absolute bottom-0 right-0 w-16 h-16 border-r-2 border-b-2 border-k5-cyan/30 z-10" />
    </div>
  );
}
