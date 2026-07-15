"use client";

/**
 * KINETICS 5 — Écran de démarrage (PDF page 2)
 * Boutons : NEW GAME / CONTINUE / LOAD GAME / OPTIONS / QUIT
 * Fond : scène spatiale + personnage armuré
 * Logo KINETICS 5 en Audiowide
 */

import { KButton, KLogo } from "@/components/kinetics/ui";
import { StarfieldBackground } from "@/components/kinetics/visuals";
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
    { id: "quit", label: t(language, "start.quit"), action: () => window.close(), danger: true },
  ];

  return (
    <div className="relative min-h-screen w-full overflow-hidden flex flex-col">
      {/* Fond image + étoiles */}
      <div
        className="absolute inset-0 bg-cover bg-center"
        style={{ backgroundImage: "url(/kinetics/start-bg.png)" }}
      />
      <div className="absolute inset-0 bg-gradient-to-b from-k5-deep-space/40 via-k5-deep-space/60 to-k5-deep-space" />
      <StarfieldBackground density={60} />

      {/* En-tête : logo + tagline */}
      <header className="relative z-10 pt-12 px-6 text-center safe-top">
        <div className="inline-block">
          <div className="text-xs font-display tracking-[0.4em] text-k5-cyan k5-text-glow-cyan mb-2">
            {t(language, "start.tagline")}
          </div>
          <KLogo size="lg" className="k5-pulse" />
          <div className="mt-2 h-px w-48 mx-auto bg-gradient-to-r from-transparent via-k5-cyan to-transparent" />
          <div className="mt-1 text-[10px] font-display tracking-[0.3em] text-k5-muted">
            EXPLORATION • COMBAT • DOMINATION
          </div>
        </div>
      </header>

      {/* Centre : personnage / décor */}
      <main className="relative z-10 flex-1 flex items-center justify-center px-6">
        <div className="w-full max-w-2xl">
          {/* Boutons menu — empilés verticalement style PDF */}
          <nav className="flex flex-col gap-3 items-center">
            {menu.map((item) => (
              <KButton
                key={item.id}
                variant={item.primary ? "primary" : item.danger ? "danger" : "secondary"}
                size="lg"
                glow={item.primary}
                className="w-full max-w-xs"
                onMouseEnter={() => setHoveredBtn(item.id)}
                onMouseLeave={() => setHoveredBtn(null)}
                onClick={item.action}
              >
                <span className="flex items-center justify-center gap-3">
                  {hoveredBtn === item.id && (
                    <span className="text-k5-cyan">▸</span>
                  )}
                  {item.label}
                </span>
              </KButton>
            ))}
          </nav>
        </div>
      </main>

      {/* Pied de page : version + credit */}
      <footer className="relative z-10 px-6 py-4 flex justify-between items-center text-[10px] font-display tracking-wider text-k5-muted safe-bottom">
        <span>v0.1.0 — BUILD 2025.01</span>
        <span className="flex items-center gap-2">
          <span className="inline-block w-2 h-2 rounded-full bg-k5-green animate-pulse" />
          SERVERS ONLINE
        </span>
      </footer>

      {/* Bordures décoratives sci-fi */}
      <div className="pointer-events-none absolute top-0 left-0 w-32 h-32 border-l-2 border-t-2 border-k5-cyan/40" />
      <div className="pointer-events-none absolute top-0 right-0 w-32 h-32 border-r-2 border-t-2 border-k5-cyan/40" />
      <div className="pointer-events-none absolute bottom-0 left-0 w-32 h-32 border-l-2 border-b-2 border-k5-cyan/40" />
      <div className="pointer-events-none absolute bottom-0 right-0 w-32 h-32 border-r-2 border-b-2 border-k5-cyan/40" />
    </div>
  );
}
