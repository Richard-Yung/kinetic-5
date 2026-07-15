"use client";

/**
 * KINETICS 5 — Écran de démarrage (PDF page 2 — écran du haut)
 * Recodé au millimètre près d'après l'image de référence.
 *
 * Layout exact (d'après VLM analyse de l'image source) :
 * - Background : scène spatiale avec planète centrée + vaisseaux + étoiles
 * - Logo "KINETICS·5" : 10% gauche, 40% haut (blanc, Audiowide)
 * - 5 boutons HORIZONTAUX à 55% haut :
 *   NEW GAME (cyan #00BFFF), CONTINUE/LOAD GAME/OPTIONS/QUIT (gris foncé #333333)
 * - Grille subtile en overlay
 * - Astuce en bas
 */

import { KButton } from "@/components/kinetics/ui";
import { StarfieldBackground } from "@/components/kinetics/visuals";
import { useGameStore } from "@/store/game-store";
import { t } from "@/lib/i18n";
import { useState } from "react";
import { ChevronRight } from "lucide-react";

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
    <div className="relative w-full h-screen min-h-[500px] overflow-hidden flex flex-col">
      {/* === BACKGROUND : image source du PDF nettoyée (scène spatiale + planète) === */}
      <div
        className="absolute inset-0 bg-cover bg-center"
        style={{
          backgroundImage: "url(/kinetics/start-bg-clean.png)",
          backgroundPosition: "center 40%",
        }}
      />
      {/* Assombrir le bas pour les boutons */}
      <div className="absolute inset-0 bg-gradient-to-b from-transparent via-transparent to-k5-deep-space/80" />
      {/* Vignette */}
      <div className="absolute inset-0 pointer-events-none" style={{ boxShadow: "inset 0 0 150px rgba(5, 6, 15, 0.6)" }} />
      {/* Grille subtile */}
      <div className="absolute inset-0 k5-grid-bg opacity-20" />
      {/* Étoiles animées */}
      <StarfieldBackground density={30} />

      {/* === LOGO KINETICS·5 === */}
      <div className="relative z-10 pt-[8%] px-[5%]">
        <div className="font-display text-3xl sm:text-4xl md:text-5xl text-white tracking-wider k5-text-glow-cyan">
          KINETICS<span className="text-k5-cyan mx-1">·</span>5
        </div>
        <div className="text-[10px] font-display tracking-[0.3em] text-k5-cyan/70 mt-1">
          {t(language, "start.tagline")}
        </div>
      </div>

      {/* === BOUTONS MENU HORIZONTAUX (d'après le PDF) === */}
      <div className="relative z-10 flex-1 flex items-center justify-center px-[3%]">
        <nav className="flex flex-row gap-2 sm:gap-3 w-full max-w-4xl justify-center flex-wrap">
          {menu.map((item) => (
            <button
              key={item.id}
              onMouseEnter={() => setHoveredBtn(item.id)}
              onMouseLeave={() => setHoveredBtn(null)}
              onClick={item.action}
              className={`
                relative px-4 sm:px-6 py-3 sm:py-4 text-xs sm:text-sm font-display tracking-wider uppercase
                border transition-all duration-150 select-none no-select k5-clip-sm
                ${item.primary
                  ? "bg-k5-cyan text-k5-deep-space border-k5-cyan k5-glow-cyan hover:brightness-110"
                  : "bg-k5-deep-space/80 text-white border-k5-border/60 hover:border-k5-cyan hover:bg-k5-panel/80"
                }
                ${hoveredBtn === item.id ? "scale-105 -translate-y-0.5" : ""}
              `}
              style={item.primary ? { boxShadow: "0 0 20px rgba(26, 161, 206, 0.5)" } : undefined}
            >
              <span className="flex items-center gap-1.5">
                {hoveredBtn === item.id && <ChevronRight className="w-3 h-3" />}
                {item.label}
              </span>
            </button>
          ))}
        </nav>
      </div>

      {/* === BAS : tagline + statut serveur === */}
      <div className="relative z-10 px-[5%] pb-4 safe-bottom flex justify-between items-end text-[10px] font-display tracking-wider text-k5-muted">
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
      <div className="pointer-events-none absolute top-0 left-0 w-20 h-20 border-l-2 border-t-2 border-k5-cyan/30" />
      <div className="pointer-events-none absolute top-0 right-0 w-20 h-20 border-r-2 border-t-2 border-k5-cyan/30" />
      <div className="pointer-events-none absolute bottom-0 left-0 w-20 h-20 border-l-2 border-b-2 border-k5-cyan/30" />
      <div className="pointer-events-none absolute bottom-0 right-0 w-20 h-20 border-r-2 border-b-2 border-k5-cyan/30" />
    </div>
  );
}
