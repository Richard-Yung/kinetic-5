"use client";

/**
 * KINETICS 5 — Composants visuels partagés
 * - StarfieldBackground : fond étoilé animé (parallax)
 * - AgentAvatar : avatar d'agent stylisé (silhouette armure sci-fi)
 * - WeaponRender : rendu stylisé d'arme
 * - ScanlineOverlay : overlay scanlines global
 */

import { useEffect, useState } from "react";
import { cn } from "@/lib/utils";
import type { Agent, Weapon } from "@/lib/kinetics-data";
import { RARITY_COLORS, ELEMENT_COLORS } from "@/lib/kinetics-data";

/* ============================================================
   StarfieldBackground — fond étoilé parallax
   ============================================================ */
export function StarfieldBackground({
  density = 80,
  className,
}: {
  density?: number;
  className?: string;
}) {
  // Génère les étoiles côté client uniquement pour éviter les mismatches SSR/hydration
  const [stars, setStars] = useState<Array<{ top: number; left: number; size: number; delay: number; duration: number }>>([]);
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- initialisation client-only pour éviter hydration mismatch
    setStars(
      Array.from({ length: density }, () => ({
        top: Math.random() * 100,
        left: Math.random() * 100,
        size: Math.random() * 2 + 0.5,
        delay: Math.random() * 4,
        duration: Math.random() * 3 + 2,
      }))
    );
  }, [density]);

  return (
    <div className={cn("absolute inset-0 overflow-hidden pointer-events-none", className)}>
      {/* Nébuleuse */}
      <div
        className="absolute inset-0"
        style={{
          background:
            "radial-gradient(ellipse at 30% 20%, rgba(26, 161, 206, 0.12) 0%, transparent 50%), radial-gradient(ellipse at 70% 80%, rgba(168, 85, 247, 0.08) 0%, transparent 50%)",
        }}
      />
      {/* Étoiles */}
      {stars.map((s, i) => (
        <div
          key={i}
          className="absolute rounded-full bg-white"
          style={{
            top: `${s.top}%`,
            left: `${s.left}%`,
            width: `${s.size}px`,
            height: `${s.size}px`,
            animation: `k5-twinkle ${s.duration}s ease-in-out ${s.delay}s infinite`,
          }}
        />
      ))}
      {/* Grille de fond */}
      <div className="absolute inset-0 k5-grid-bg opacity-30" />
    </div>
  );
}

/* ============================================================
   AgentAvatar — silhouette d'agent stylisée (SVG)
   Représentation sci-fi codée par classe + couleur thème
   ============================================================ */
export function AgentAvatar({
  agent,
  className,
  showName = false,
}: {
  agent: Agent;
  className?: string;
  showName?: boolean;
}) {
  const color = agent.themeColor;
  return (
    <div className={cn("relative flex flex-col items-center", className)}>
      <div className="relative" style={{ width: "100%", aspectRatio: "3/4" }}>
        <svg viewBox="0 0 120 160" className="w-full h-full" style={{ filter: `drop-shadow(0 0 12px ${color}88)` }}>
          <defs>
            <linearGradient id={`armor-${agent.id}`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={color} stopOpacity="0.9" />
              <stop offset="50%" stopColor={color} stopOpacity="0.4" />
              <stop offset="100%" stopColor="#05060F" stopOpacity="1" />
            </linearGradient>
            <linearGradient id={`glow-${agent.id}`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={color} stopOpacity="1" />
              <stop offset="100%" stopColor={color} stopOpacity="0" />
            </linearGradient>
          </defs>
          {/* Halo de fond */}
          <ellipse cx="60" cy="80" rx="50" ry="70" fill={`url(#glow-${agent.id})`} opacity="0.15" />
          {/* Casque */}
          <path
            d="M 45 30 Q 45 18 60 18 Q 75 18 75 30 L 78 50 Q 78 55 60 55 Q 42 55 42 50 Z"
            fill={`url(#armor-${agent.id})`}
            stroke={color}
            strokeWidth="1.5"
          />
          {/* Visière */}
          <path
            d="M 48 36 Q 60 32 72 36 L 70 44 Q 60 42 50 44 Z"
            fill={color}
            opacity="0.8"
          />
          <line x1="60" y1="38" x2="60" y2="42" stroke="#05060F" strokeWidth="0.5" />
          {/* Plastron */}
          <path
            d="M 38 55 L 82 55 L 85 95 L 78 100 L 42 100 L 35 95 Z"
            fill={`url(#armor-${agent.id})`}
            stroke={color}
            strokeWidth="1.5"
          />
          {/* Lignes de plaque */}
          <line x1="60" y1="55" x2="60" y2="100" stroke={color} strokeWidth="0.8" opacity="0.6" />
          <line x1="42" y1="70" x2="78" y2="70" stroke={color} strokeWidth="0.5" opacity="0.4" />
          {/* Épaules */}
          <ellipse cx="35" cy="60" rx="8" ry="12" fill={`url(#armor-${agent.id})`} stroke={color} strokeWidth="1" />
          <ellipse cx="85" cy="60" rx="8" ry="12" fill={`url(#armor-${agent.id})`} stroke={color} strokeWidth="1" />
          {/* Bras */}
          <rect x="28" y="65" width="10" height="30" rx="4" fill={`url(#armor-${agent.id})`} stroke={color} strokeWidth="1" />
          <rect x="82" y="65" width="10" height="30" rx="4" fill={`url(#armor-${agent.id})`} stroke={color} strokeWidth="1" />
          {/* Jambes */}
          <rect x="44" y="100" width="14" height="45" rx="3" fill={`url(#armor-${agent.id})`} stroke={color} strokeWidth="1" />
          <rect x="62" y="100" width="14" height="45" rx="3" fill={`url(#armor-${agent.id})`} stroke={color} strokeWidth="1" />
          {/* Genouillères */}
          <ellipse cx="51" cy="120" rx="5" ry="3" fill={color} opacity="0.7" />
          <ellipse cx="69" cy="120" rx="5" ry="3" fill={color} opacity="0.7" />
          {/* Core lumineux (poitrine) */}
          <circle cx="60" cy="72" r="4" fill={color}>
            <animate attributeName="opacity" values="0.6;1;0.6" dur="2s" repeatCount="indefinite" />
          </circle>
          <circle cx="60" cy="72" r="2" fill="#FFFFFF" opacity="0.9" />
          {/* Détails selon classe */}
          {agent.class === "Tank" && (
            <>
              {/* Bouclier dorsal */}
              <rect x="30" y="50" width="60" height="55" rx="2" fill="none" stroke={color} strokeWidth="0.5" opacity="0.3" strokeDasharray="2 2" />
            </>
          )}
          {agent.class === "Assault" && (
            <>
              {/* Lames d'épaule */}
              <path d="M 28 55 L 35 50 L 42 55" fill="none" stroke={color} strokeWidth="1.5" />
              <path d="M 78 55 L 85 50 L 92 55" fill="none" stroke={color} strokeWidth="1.5" />
            </>
          )}
          {agent.class === "Support" && (
            <>
              {/* Pack médical dorsal */}
              <rect x="48" y="58" width="24" height="8" rx="2" fill={color} opacity="0.5" />
              <circle cx="54" cy="62" r="1.5" fill="#FFFFFF" />
              <circle cx="60" cy="62" r="1.5" fill="#FFFFFF" />
              <circle cx="66" cy="62" r="1.5" fill="#FFFFFF" />
            </>
          )}
          {agent.class === "Recon" && (
            <>
              {/* Capuche */}
              <path d="M 42 30 Q 60 10 78 30 L 75 25 Q 60 12 45 25 Z" fill={`url(#armor-${agent.id})`} stroke={color} strokeWidth="1" />
            </>
          )}
        </svg>
        {/* Reflet au sol */}
        <div
          className="absolute bottom-0 left-1/2 -translate-x-1/2 w-3/4 h-2 rounded-full blur-sm"
          style={{ background: `${color}44` }}
        />
      </div>
      {showName && (
        <div className="mt-2 text-center">
          <div className="font-display text-lg text-white k5-text-glow-cyan">{agent.displayName}</div>
          <div className="text-xs uppercase tracking-wider" style={{ color }}>{agent.class}</div>
        </div>
      )}
    </div>
  );
}

/* ============================================================
   WeaponRender — rendu stylisé d'arme (SVG)
   ============================================================ */
export function WeaponRender({
  weapon,
  className,
}: {
  weapon: Weapon;
  className?: string;
}) {
  const color = RARITY_COLORS[weapon.rarity];
  const elementColor = ELEMENT_COLORS[weapon.element];

  return (
    <div className={cn("relative flex items-center justify-center", className)}>
      <svg viewBox="0 0 200 80" className="w-full" style={{ filter: `drop-shadow(0 0 6px ${color}66)` }}>
        <defs>
          <linearGradient id={`w-${weapon.id}`} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#3a4a5e" />
            <stop offset="50%" stopColor="#1a2a3e" />
            <stop offset="100%" stopColor="#0a1525" />
          </linearGradient>
        </defs>
        {weapon.category === "Primary" && weapon.type === "Heavy" && (
          <>
            {/* Arme lourde type minigun */}
            <rect x="40" y="30" width="120" height="22" rx="2" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="1" />
            <rect x="50" y="25" width="100" height="6" fill={color} opacity="0.7" />
            {/* Canon multiple */}
            <circle cx="165" cy="36" r="4" fill="#0a0a0a" stroke={color} strokeWidth="1" />
            <circle cx="165" cy="44" r="4" fill="#0a0a0a" stroke={color} strokeWidth="1" />
            <circle cx="170" cy="40" r="4" fill="#0a0a0a" stroke={color} strokeWidth="1" />
            {/* Chargeur */}
            <rect x="70" y="52" width="20" height="18" rx="1" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="0.8" />
            {/* Poignée */}
            <path d="M 95 52 L 110 52 L 108 68 L 97 68 Z" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="0.8" />
            {/* Détails élément */}
            <circle cx="60" cy="41" r="3" fill={elementColor} opacity="0.9" />
          </>
        )}
        {weapon.category === "Primary" && weapon.type === "AssaultRifle" && (
          <>
            {/* Fusil d'assaut */}
            <rect x="30" y="35" width="140" height="12" rx="1" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="1" />
            <rect x="155" y="36" width="25" height="10" fill="#0a0a0a" stroke={color} strokeWidth="0.8" />
            <rect x="60" y="47" width="14" height="16" rx="1" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="0.8" />
            <path d="M 85 47 L 100 47 L 98 62 L 87 62 Z" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="0.8" />
            <rect x="35" y="33" width="40" height="3" fill={color} opacity="0.6" />
            <circle cx="50" cy="41" r="2.5" fill={elementColor} opacity="0.9" />
          </>
        )}
        {weapon.category === "Primary" && weapon.type === "Sniper" && (
          <>
            {/* Sniper long */}
            <rect x="10" y="37" width="170" height="8" rx="1" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="1" />
            <rect x="170" y="35" width="20" height="12" fill="#0a0a0a" stroke={color} strokeWidth="0.8" />
            {/* Scope */}
            <rect x="80" y="28" width="30" height="8" rx="2" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="0.8" />
            <circle cx="95" cy="32" r="3" fill={elementColor} opacity="0.9" />
            <line x1="80" y1="36" x2="110" y2="36" stroke={color} strokeWidth="0.5" opacity="0.5" />
            {/* Bipied */}
            <line x1="140" y1="45" x2="135" y2="55" stroke={color} strokeWidth="1" />
            <line x1="150" y1="45" x2="155" y2="55" stroke={color} strokeWidth="1" />
          </>
        )}
        {weapon.category === "Primary" && weapon.type === "SMG" && (
          <>
            {/* SMG compact */}
            <rect x="50" y="36" width="100" height="10" rx="1" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="1" />
            <rect x="150" y="37" width="15" height="8" fill="#0a0a0a" stroke={color} strokeWidth="0.8" />
            <rect x="75" y="46" width="12" height="14" rx="1" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="0.8" />
            <path d="M 90 46 L 102 46 L 100 60 L 92 60 Z" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="0.8" />
            <circle cx="60" cy="41" r="2" fill={elementColor} opacity="0.9" />
          </>
        )}
        {weapon.category === "Secondary" && (
          <>
            {/* Pistolet */}
            <rect x="70" y="35" width="50" height="10" rx="1" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="1" />
            <rect x="118" y="36" width="12" height="8" fill="#0a0a0a" stroke={color} strokeWidth="0.8" />
            <path d="M 85 45 L 105 45 L 102 62 L 88 62 Z" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="0.8" />
            <circle cx="80" cy="40" r="2" fill={elementColor} opacity="0.9" />
          </>
        )}
        {weapon.category === "Tactical" && (
          <>
            {/* Grenade */}
            <ellipse cx="100" cy="40" rx="16" ry="20" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="1.5" />
            <rect x="92" y="18" width="16" height="8" rx="1" fill={`url(#w-${weapon.id})`} stroke={color} strokeWidth="0.8" />
            <line x1="100" y1="18" x2="100" y2="12" stroke={color} strokeWidth="1" />
            <circle cx="100" cy="12" r="2" fill={elementColor} opacity="0.9" />
            <path d="M 88 40 Q 100 30 112 40" fill="none" stroke={color} strokeWidth="0.8" opacity="0.6" />
            <text x="100" y="45" textAnchor="middle" fontSize="8" fill={color} fontFamily="monospace" fontWeight="bold">
              {weapon.id.includes("frag") ? "FRAG" : weapon.id.includes("titan") ? "TITAN" : weapon.id.includes("super") ? "NOVA" : "TRAP"}
            </text>
          </>
        )}
      </svg>
    </div>
  );
}

/* ============================================================
   ScanlineOverlay — overlay global scanlines
   ============================================================ */
export function ScanlineOverlay({ className }: { className?: string }) {
  return (
    <div
      className={cn("pointer-events-none fixed inset-0 z-[100] k5-scanlines", className)}
      aria-hidden
    />
  );
}
