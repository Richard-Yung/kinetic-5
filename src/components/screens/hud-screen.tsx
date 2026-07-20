"use client";

/**
 * KINETICS 5 — Combat HUD (PDF page 6)
 * Layout :
 * - Top-left : minimap (collapsible HIDE MAP)
 * - Top-right : timer + objective
 * - Bottom-center : Health (green) + Armor (cyan) bars + ammo counter
 * - Bottom-left : weapon name
 * - Crosshair dynamique au centre
 * Touch controls overlay séparé (touch-controls.tsx)
 */

import { KProgressBar, KButton } from "@/components/kinetics/ui";
import { useGameStore } from "@/store/game-store";
import { t } from "@/lib/i18n";
import { formatTime } from "@/lib/kinetics-data";
import { useState } from "react";
import { Map as MapIcon, X, Pause, Target } from "lucide-react";

export interface HUDState {
  health: number;
  maxHealth: number;
  armor: number;
  maxArmor: number;
  ammo: number;
  reserveAmmo: number;
  weaponName: string;
  timeLeft: number;
  enemiesRemaining: number;
  waveIndex: number;
  totalWaves: number;
  ultimate: number;
  objective: string;
  extracting?: boolean;
  extractProgress?: number;
  hitMarker?: { visible: boolean; crit: boolean };
  damageDirection?: { angle: number; visible: boolean } | null;
  playerPos?: { x: number; z: number };
  enemyPositions?: { x: number; z: number; alive: boolean }[];
}

export function HUDScreen({
  hudState,
  onPause,
}: {
  hudState: HUDState;
  onPause: () => void;
}) {
  const { language } = useGameStore();
  const [mapVisible, setMapVisible] = useState(true);

  const ultPct = (hudState.ultimate / 1000) * 100;
  const ultReady = hudState.ultimate >= 1000;

  return (
    <div className="absolute inset-0 pointer-events-none z-20 select-none no-select">
      {/* === TOP-LEFT : Minimap fonctionnelle === */}
      <div className="absolute top-3 left-3 safe-top pointer-events-auto">
        {mapVisible ? (
          <div
            className="relative rounded-lg backdrop-blur-sm overflow-hidden"
            style={{
              width: "120px", height: "120px",
              background: "rgba(10, 20, 35, 0.7)",
              border: "1.5px solid rgba(26, 161, 206, 0.5)",
              boxShadow: "0 0 12px rgba(26, 161, 206, 0.15), inset 0 0 8px rgba(26, 161, 206, 0.08)",
            }}
          >
            {/* Grille minimap */}
            <div className="absolute inset-0" style={{
              backgroundImage: "linear-gradient(rgba(26, 161, 206, 0.12) 1px, transparent 1px), linear-gradient(90deg, rgba(26, 161, 206, 0.12) 1px, transparent 1px)",
              backgroundSize: "20px 20px",
            }} />

            {/* Porte d'extraction (haut, vert) */}
            <div className="absolute top-0 left-1/2 -translate-x-1/2 w-4 h-1 bg-green-400 rounded-full" style={{ boxShadow: "0 0 4px #6CF42E" }} />

            {/* Joueur (centre, cyan) */}
            {hudState.playerPos && (
              <div
                className="absolute w-2 h-2 rounded-full"
                style={{
                  left: `${50 + (hudState.playerPos.x / 20) * 45}%`,
                  top: `${50 + (hudState.playerPos.z / 20) * 45}%`,
                  transform: "translate(-50%, -50%)",
                  background: "#1AA1CE",
                  boxShadow: "0 0 6px #1AA1CE",
                }}
              >
                {/* Direction du joueur (triangle) */}
                <div className="absolute -top-2 left-1/2 -translate-x-1/2 w-0 h-0 border-l-[3px] border-r-[3px] border-b-[5px] border-l-transparent border-r-transparent border-b-cyan-400" />
              </div>
            )}

            {/* Ennemis (rouge, pulsants) */}
            {hudState.enemyPositions?.filter(e => e.alive).map((e, i) => (
              <div
                key={i}
                className="absolute w-1.5 h-1.5 rounded-full animate-pulse"
                style={{
                  left: `${50 + (e.x / 20) * 45}%`,
                  top: `${50 + (e.z / 20) * 45}%`,
                  transform: "translate(-50%, -50%)",
                  background: "#FE0022",
                  boxShadow: "0 0 4px #FE0022",
                }}
              />
            ))}

            {/* Bouton hide */}
            <button
              onClick={() => setMapVisible(false)}
              className="absolute -top-1 -right-1 w-5 h-5 rounded-full flex items-center justify-center transition-colors"
              style={{ background: "rgba(10, 20, 35, 0.9)", border: "1px solid rgba(26, 161, 206, 0.6)" }}
            >
              <X className="w-3 h-3 text-cyan-400" />
            </button>

            {/* Label */}
            <div className="absolute bottom-0.5 left-1 text-[7px] font-display text-cyan-400/60">MAP</div>
          </div>
        ) : (
          <button
            onClick={() => setMapVisible(true)}
            className="w-10 h-10 rounded-lg flex items-center justify-center"
            style={{ background: "rgba(10, 20, 35, 0.7)", border: "1.5px solid rgba(26, 161, 206, 0.5)", backdropFilter: "blur(4px)" }}
          >
            <MapIcon className="w-4 h-4 text-cyan-400" />
          </button>
        )}
      </div>

      {/* === TOP-RIGHT : Timer + wave + pause === */}
      <div className="absolute top-3 right-3 safe-top pointer-events-auto flex flex-col items-end gap-1">
        <button
          onClick={onPause}
          className="w-10 h-10 bg-k5-panel/80 border border-k5-border rounded-sm flex items-center justify-center hover:border-k5-cyan mb-1"
        >
          <Pause className="w-4 h-4 text-white" />
        </button>
        <div className="bg-k5-panel/80 border border-k5-cyan/40 px-3 py-1.5 rounded-sm text-right backdrop-blur-sm">
          <div className="text-[9px] font-display text-k5-muted tracking-wider">{t(language, "hud.timeLeft")}</div>
          <div className={`font-display text-xl tabular-nums ${hudState.timeLeft < 60 ? "text-k5-red k5-text-glow-red" : "text-white"}`}>
            {formatTime(hudState.timeLeft)}
          </div>
        </div>
        <div className="bg-k5-panel/80 border border-k5-border px-2 py-1 rounded-sm text-right">
          <div className="text-[9px] font-display text-k5-muted">{t(language, "hud.wave")}</div>
          <div className="font-display text-sm text-k5-cyan">
            {hudState.waveIndex}/{hudState.totalWaves}
          </div>
        </div>
      </div>

      {/* === TOP-CENTER : Objective === */}
      <div className="absolute top-3 left-1/2 -translate-x-1/2 safe-top">
        <div className="bg-k5-panel/70 border border-k5-yellow/40 px-4 py-1.5 rounded-sm backdrop-blur-sm flex items-center gap-2">
          <Target className="w-3 h-3 text-k5-yellow" />
          <span className="text-[10px] font-display text-k5-yellow tracking-wider">
            {hudState.objective}
          </span>
          <span className="text-[10px] font-display text-k5-red ml-2">
            {hudState.enemiesRemaining} {t(language, "hud.enemies")}
          </span>
        </div>
      </div>

      {/* === CENTER : Crosshair === */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2">
        <div className="relative w-8 h-8">
          {/* Croix */}
          <div className="absolute top-1/2 left-0 w-2.5 h-0.5 bg-k5-cyan/80 -translate-y-1/2" />
          <div className="absolute top-1/2 right-0 w-2.5 h-0.5 bg-k5-cyan/80 -translate-y-1/2" />
          <div className="absolute left-1/2 top-0 h-2.5 w-0.5 bg-k5-cyan/80 -translate-x-1/2" />
          <div className="absolute left-1/2 bottom-0 h-2.5 w-0.5 bg-k5-cyan/80 -translate-x-1/2" />
          {/* Centre */}
          <div className="absolute top-1/2 left-1/2 w-1 h-1 bg-k5-cyan rounded-full -translate-x-1/2 -translate-y-1/2" />
          {/* Hit marker */}
          {hudState.hitMarker?.visible && (
            <>
              <div className={`absolute top-1/2 left-1/2 w-4 h-0.5 -translate-x-1/2 -translate-y-1/2 rotate-45 ${hudState.hitMarker.crit ? "bg-k5-yellow" : "bg-k5-red"}`} />
              <div className={`absolute top-1/2 left-1/2 w-4 h-0.5 -translate-x-1/2 -translate-y-1/2 -rotate-45 ${hudState.hitMarker.crit ? "bg-k5-yellow" : "bg-k5-red"}`} />
            </>
          )}
        </div>
      </div>

      {/* === Damage direction indicators === */}
      {hudState.damageDirection?.visible && (
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-48 h-48 pointer-events-none">
          <div
            className="absolute top-0 left-1/2 -translate-x-1/2"
            style={{ transform: `translateX(-50%) rotate(${hudState.damageDirection.angle}deg) translateY(-80px)` }}
          >
            <div className="w-0 h-0 border-l-4 border-r-4 border-b-8 border-l-transparent border-r-transparent border-b-k5-red" style={{ filter: "drop-shadow(0 0 4px #FE0022)" }} />
          </div>
        </div>
      )}

      {/* === Extraction indicator === */}
      {hudState.extracting && (
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 mt-16">
          <div className="bg-k5-panel/90 border-2 border-k5-green px-4 py-2 rounded-sm k5-glow-green text-center">
            <div className="text-[10px] font-display text-k5-green tracking-wider">{t(language, "hud.extracting")}</div>
            <KProgressBar value={hudState.extractProgress ?? 0} max={100} color="green" segments={20} showValue={false} height="sm" className="mt-1" />
          </div>
        </div>
      )}

      {/* === BOTTOM-CENTER : Vitals + Ammo === */}
      <div className="absolute bottom-3 left-1/2 -translate-x-1/2 safe-bottom pointer-events-none">
        <div className="flex items-end gap-4">
          {/* Health + Armor */}
          <div className="w-56 space-y-1">
            <KProgressBar
              label={t(language, "hud.health")}
              value={hudState.health}
              max={hudState.maxHealth}
              color={hudState.health < hudState.maxHealth * 0.3 ? "red" : "green"}
              valueText={`${Math.ceil(hudState.health)}`}
              segments={20}
            />
            <KProgressBar
              label={t(language, "hud.armor")}
              value={hudState.armor}
              max={hudState.maxArmor}
              color="cyan"
              valueText={`${Math.ceil(hudState.armor)}`}
              segments={20}
            />
            {/* Ultimate meter */}
            <div className="mt-1">
              <div className="flex justify-between text-[9px] mb-0.5">
                <span className="font-display text-k5-muted">{t(language, "hud.ultimate")}</span>
                <span className={`font-display ${ultReady ? "text-k5-yellow k5-text-glow-cyan animate-pulse" : "text-k5-muted"}`}>
                  {ultReady ? t(language, "hud.ready") : `${Math.floor(ultPct)}%`}
                </span>
              </div>
              <div className="h-1.5 flex gap-px">
                {Array.from({ length: 10 }).map((_, i) => (
                  <div
                    key={i}
                    className="flex-1 rounded-[1px] transition-all"
                    style={{
                      background: i < Math.floor(ultPct / 10) ? (ultReady ? "#FFE735" : "#A855F7") : "rgba(26, 74, 110, 0.4)",
                      boxShadow: i < Math.floor(ultPct / 10) ? `0 0 4px ${ultReady ? "#FFE735" : "#A855F7"}` : "none",
                    }}
                  />
                ))}
              </div>
            </div>
          </div>

          {/* Ammo counter */}
          <div className="text-right">
            <div className="font-display text-4xl text-white tabular-nums leading-none">
              {hudState.ammo}
              <span className="text-k5-muted text-2xl"> | {hudState.reserveAmmo}</span>
            </div>
            <div className="font-display text-sm text-k5-cyan tracking-wider mt-1">
              {hudState.weaponName}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
