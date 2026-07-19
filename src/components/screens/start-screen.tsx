"use client";

/**
 * KINETICS 5 — Écran de démarrage
 * Full-screen responsive, background everspace, logo top-left, 5 boutons en ligne.
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
    { id: "continue", label: t(language, "start.continue"), action: () => setScreen("lobby") },
    { id: "load", label: t(language, "start.loadGame"), action: () => setScreen("lobby") },
    { id: "options", label: t(language, "start.options"), action: () => setScreen("settings") },
    { id: "quit", label: t(language, "start.quit"), action: () => window.close() },
  ];

  return (
    <div className="relative w-full h-screen min-h-[300px] overflow-hidden">
      {/* Background full-screen */}
      <img
        src="/kinetics/bg-everspace.jpg"
        alt=""
        className="absolute inset-0 w-full h-full object-cover"
        aria-hidden
      />

      {/* Teinte bleue froide subtile */}
      <div
        className="absolute inset-0"
        style={{ background: "rgba(20, 40, 80, 0.15)", mixBlendMode: "color" }}
      />

      {/* Degrade radial depuis la source lumineuse */}
      <div
        className="absolute inset-0"
        style={{
          background:
            "radial-gradient(ellipse at 60% 45%, rgba(180, 220, 255, 0.08) 0%, transparent 40%, rgba(5, 6, 15, 0.5) 100%)",
        }}
      />

      {/* Degrade vertical subtil */}
      <div
        className="absolute inset-0"
        style={{
          background: "linear-gradient(to bottom, rgba(10, 20, 50, 0.1) 0%, transparent 40%, rgba(0, 0, 5, 0.4) 100%)",
        }}
      />

      {/* Logo top-left */}
      <div className="absolute" style={{ top: "5%", left: "4%", zIndex: 10 }}>
        <h1
          className="font-display text-white leading-none tracking-wider"
          style={{
            fontSize: "clamp(1.5rem, 5vw, 3.5rem)",
            textShadow: "0 0 20px rgba(26, 161, 206, 0.8), 0 2px 8px rgba(0,0,0,0.9)",
          }}
        >
          KINETICS<span className="text-k5-cyan">·</span>5
        </h1>
      </div>

      {/* 5 boutons en une ligne horizontale, centres */}
      <nav
        className="absolute left-1/2 -translate-x-1/2 flex flex-row flex-nowrap items-center gap-3 sm:gap-5"
        style={{ top: "55%", zIndex: 10 }}
      >
        {menu.map((item) => (
          <button
            key={item.id}
            onMouseEnter={() => setHovered(item.id)}
            onMouseLeave={() => setHovered(null)}
            onClick={item.action}
            className={`font-display uppercase tracking-wider whitespace-nowrap transition-all duration-150 select-none ${
              item.primary ? "bg-k5-cyan text-white px-4 py-2" : "bg-transparent text-white px-2 py-2"
            } ${hovered === item.id ? (item.primary ? "brightness-110 scale-105" : "text-k5-cyan") : ""}`}
            style={{
              fontSize: "clamp(0.7rem, 1.8vw, 1.1rem)",
              borderRadius: item.primary ? "3px" : "0",
              boxShadow: item.primary ? "0 0 16px rgba(26, 161, 206, 0.5)" : "none",
            }}
          >
            {item.label}
          </button>
        ))}
      </nav>
    </div>
  );
}
