"use client";

/* eslint-disable react-hooks/immutability -- refs mutables nécessaires pour les contrôles tactiles (pattern standard) */

/**
 * KINETICS 5 — Contrôles tactiles (mobile)
 * Joystick gauche flottant (mouvement) + boutons droits (FIRE/AIM/RELOAD/JUMP/GRENADE/SWITCH)
 * Zone de swipe droite (look caméra)
 * Détecte mobile vs desktop (n'affiche que sur touch)
 */

import { useEffect, useRef, useState, type MutableRefObject } from "react";

interface GameRefs {
  joystick: { x: number; y: number };
  lookDelta: { x: number; y: number };
  shoot: boolean;
  aim: boolean;
  jump: boolean;
  reload: boolean;
  moveForward: boolean;
  moveBackward: boolean;
  moveLeft: boolean;
  moveRight: boolean;
  sprint: boolean;
  velocity: { y: number };
  yaw: number;
  pitch: number;
}

export function TouchControls({
  gameRefs,
}: {
  gameRefs: MutableRefObject<GameRefs>;
}) {
  const [isTouch, setIsTouch] = useState(false);
  const joystickRef = useRef<HTMLDivElement>(null);
  const [joystickPos, setJoystickPos] = useState({ x: 0, y: 0 });
  const [joystickActive, setJoystickActive] = useState(false);
  const joystickOrigin = useRef({ x: 0, y: 0 });
  const lookTouchId = useRef<number | null>(null);
  const lookLastPos = useRef({ x: 0, y: 0 });

  useEffect(() => {
    setIsTouch(
      "ontouchstart" in window ||
        navigator.maxTouchPoints > 0 ||
        window.matchMedia("(pointer: coarse)").matches
    );
  }, []);

  // Joystick handlers
  const handleJoystickStart = (e: React.TouchEvent) => {
    e.preventDefault();
    const touch = e.touches[0];
    joystickOrigin.current = { x: touch.clientX, y: touch.clientY };
    setJoystickActive(true);
  };

  const handleJoystickMove = (e: React.TouchEvent) => {
    e.preventDefault();
    if (!joystickActive) return;
    const touch = e.touches[0];
    const dx = touch.clientX - joystickOrigin.current.x;
    const dy = touch.clientY - joystickOrigin.current.y;
    const maxRadius = 50;
    const dist = Math.sqrt(dx * dx + dy * dy);
    const clampedDist = Math.min(dist, maxRadius);
    const angle = Math.atan2(dy, dx);
    const x = Math.cos(angle) * clampedDist;
    const y = Math.sin(angle) * clampedDist;
    setJoystickPos({ x, y });
    // Normalized -1 to 1
    gameRefs.current.joystick = {
      x: x / maxRadius,
      y: y / maxRadius,
    };
  };

  const handleJoystickEnd = (e: React.TouchEvent) => {
    e.preventDefault();
    setJoystickActive(false);
    setJoystickPos({ x: 0, y: 0 });
    gameRefs.current.joystick = { x: 0, y: 0 };
  };

  // Look handlers (zone droite)
  const handleLookStart = (e: React.TouchEvent) => {
    if (lookTouchId.current !== null) return;
    const touch = e.changedTouches[0];
    lookTouchId.current = touch.identifier;
    lookLastPos.current = { x: touch.clientX, y: touch.clientY };
  };

  const handleLookMove = (e: React.TouchEvent) => {
    for (let i = 0; i < e.changedTouches.length; i++) {
      const touch = e.changedTouches[i];
      if (touch.identifier === lookTouchId.current) {
        const dx = touch.clientX - lookLastPos.current.x;
        const dy = touch.clientY - lookLastPos.current.y;
        gameRefs.current.lookDelta.x += dx;
        gameRefs.current.lookDelta.y += dy;
        lookLastPos.current = { x: touch.clientX, y: touch.clientY };
      }
    }
  };

  const handleLookEnd = (e: React.TouchEvent) => {
    for (let i = 0; i < e.changedTouches.length; i++) {
      if (e.changedTouches[i].identifier === lookTouchId.current) {
        lookTouchId.current = null;
      }
    }
  };

  if (!isTouch) return null;

  return (
    <div className="absolute inset-0 z-30 pointer-events-none select-none no-select">
      {/* Zone de look (moitié droite de l'écran, sauf boutons) */}
      <div
        className="absolute top-0 right-0 bottom-0 w-1/2 pointer-events-auto"
        onTouchStart={handleLookStart}
        onTouchMove={handleLookMove}
        onTouchEnd={handleLookEnd}
        onTouchCancel={handleLookEnd}
      />

      {/* Joystick gauche (flottant — apparaît où on touche) */}
      <div
        className="absolute bottom-20 left-8 w-32 h-32 pointer-events-auto"
        onTouchStart={handleJoystickStart}
        onTouchMove={handleJoystickMove}
        onTouchEnd={handleJoystickEnd}
        onTouchCancel={handleJoystickEnd}
      >
        {/* Cercle externe */}
        <div className="absolute inset-0 rounded-full border-2 border-k5-cyan/40 bg-k5-panel/30 backdrop-blur-sm" />
        {/* Cercle interne (knob) */}
        <div
          className="absolute top-1/2 left-1/2 w-14 h-14 rounded-full border-2 border-k5-cyan bg-k5-cyan/30 backdrop-blur-sm transition-transform duration-75"
          style={{
            transform: `translate(calc(-50% + ${joystickPos.x}px), calc(-50% + ${joystickPos.y}px))`,
            boxShadow: joystickActive ? "0 0 16px #1AA1CE" : "none",
          }}
        />
        {/* Indicateur directionnel */}
        {joystickActive && (Math.abs(joystickPos.x) > 10 || Math.abs(joystickPos.y) > 10) && (
          <div
            className="absolute top-1/2 left-1/2 w-1 h-8 bg-k5-cyan/60 origin-bottom"
            style={{
              transform: `translate(-50%, -100%) rotate(${Math.atan2(joystickPos.x, -joystickPos.y) * 180 / Math.PI}deg)`,
            }}
          />
        )}
      </div>

      {/* Boutons droits */}
      <div className="absolute bottom-20 right-6 flex flex-col items-end gap-3 pointer-events-auto">
        {/* Ligne 1 : FIRE + AIM */}
        <div className="flex items-end gap-3">
          <button
            className="w-14 h-14 rounded-full border-2 border-k5-yellow/60 bg-k5-yellow/20 backdrop-blur-sm flex items-center justify-center active:bg-k5-yellow/50 active:scale-95 transition-all"
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.aim = true; }}
            onTouchEnd={(e) => { e.preventDefault(); gameRefs.current.aim = false; }}
          >
            <span className="text-[8px] font-display text-k5-yellow">AIM</span>
          </button>
          <button
            className="w-20 h-20 rounded-full border-2 border-k5-red bg-k5-red/30 backdrop-blur-sm flex items-center justify-center active:bg-k5-red/60 active:scale-95 transition-all k5-glow-red"
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.shoot = true; }}
            onTouchEnd={(e) => { e.preventDefault(); gameRefs.current.shoot = false; }}
          >
            <span className="text-xs font-display text-white">FIRE</span>
          </button>
        </div>
        {/* Ligne 2 : RELOAD + GRENADE + JUMP */}
        <div className="flex items-end gap-2">
          <button
            className="w-12 h-12 rounded-full border-2 border-k5-cyan/60 bg-k5-cyan/20 backdrop-blur-sm flex items-center justify-center active:bg-k5-cyan/50 active:scale-95 transition-all"
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.reload = true; setTimeout(() => gameRefs.current.reload = false, 100); }}
          >
            <span className="text-[8px] font-display text-k5-cyan">RLD</span>
          </button>
          <button
            className="w-12 h-12 rounded-full border-2 border-k5-green/60 bg-k5-green/20 backdrop-blur-sm flex items-center justify-center active:bg-k5-green/50 active:scale-95 transition-all"
            onTouchStart={(e) => { e.preventDefault(); /* grenade */ }}
          >
            <span className="text-[8px] font-display text-k5-green">GRN</span>
          </button>
          <button
            className="w-14 h-14 rounded-full border-2 border-k5-cyan/60 bg-k5-cyan/20 backdrop-blur-sm flex items-center justify-center active:bg-k5-cyan/50 active:scale-95 transition-all"
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.jump = true; }}
            onTouchEnd={(e) => { e.preventDefault(); gameRefs.current.jump = false; }}
          >
            <span className="text-[8px] font-display text-k5-cyan">JMP</span>
          </button>
        </div>
      </div>

      {/* Bouton sprint (au-dessus du joystick) */}
      <button
        className="absolute bottom-52 left-12 w-12 h-12 rounded-full border-2 border-k5-purple/60 bg-k5-purple/20 backdrop-blur-sm flex items-center justify-center pointer-events-auto active:bg-k5-purple/50 active:scale-95 transition-all"
        onTouchStart={(e) => { e.preventDefault(); gameRefs.current.sprint = true; }}
        onTouchEnd={(e) => { e.preventDefault(); gameRefs.current.sprint = false; }}
      >
        <span className="text-[8px] font-display text-k5-purple">SPR</span>
      </button>
    </div>
  );
}
