"use client";

/* eslint-disable react-hooks/immutability -- refs mutables pour contrôles */

/**
 * KINETICS 5 — Contrôles tactiles épurés (style mission 2+)
 * D-pad circulaire avec flèches directionnelles + bouton FIRE raffiné
 */

import { useEffect, useRef, useState, type MutableRefObject } from "react";

interface GameRefs {
  joystick: { x: number; y: number };
  move: { x: number; y: number };
  lookDelta: { x: number; y: number };
  shoot: boolean;
  aim: boolean;
  jump: boolean;
  sprint: boolean;
  reload: boolean;
  grenade: boolean;
  switchWeapon: number;
  velocity: { y: number };
  yaw: number;
  pitch: number;
  isDragging: boolean;
}

export function TouchControls({
  gameRefs,
  weaponSlot,
  onSwitchWeapon,
}: {
  gameRefs: MutableRefObject<GameRefs>;
  weaponSlot: 1 | 2 | 3;
  onSwitchWeapon: (slot: 1 | 2 | 3) => void;
}) {
  const joystickRef = useRef<HTMLDivElement>(null);
  const [joystickPos, setJoystickPos] = useState({ x: 0, y: 0 });
  const [joystickActive, setJoystickActive] = useState(false);
  const [fireActive, setFireActive] = useState(false);
  const [aimActive, setAimActive] = useState(false);
  const joystickOrigin = useRef({ x: 0, y: 0 });
  const lookTouchId = useRef<number | null>(null);
  const lookLastPos = useRef({ x: 0, y: 0 });

  const [isTouch, setIsTouch] = useState(false);
  useEffect(() => {
    setIsTouch("ontouchstart" in window || navigator.maxTouchPoints > 0 || window.matchMedia("(pointer: coarse)").matches);
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
    const maxRadius = 45;
    const dist = Math.min(Math.sqrt(dx * dx + dy * dy), maxRadius);
    const angle = Math.atan2(dy, dx);
    const x = Math.cos(angle) * dist;
    const y = Math.sin(angle) * dist;
    setJoystickPos({ x, y });
    gameRefs.current.joystick = { x: x / maxRadius, y: y / maxRadius };
    gameRefs.current.move.x = x / maxRadius;
    gameRefs.current.move.y = y / maxRadius;
  };
  const handleJoystickEnd = (e: React.TouchEvent) => {
    e.preventDefault();
    setJoystickActive(false);
    setJoystickPos({ x: 0, y: 0 });
    gameRefs.current.joystick = { x: 0, y: 0 };
    gameRefs.current.move.x = 0;
    gameRefs.current.move.y = 0;
  };

  // Look (touch) - moitié droite
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
        gameRefs.current.lookDelta.x += touch.clientX - lookLastPos.current.x;
        gameRefs.current.lookDelta.y += touch.clientY - lookLastPos.current.y;
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

  return (
    <div className="absolute inset-0 z-30 pointer-events-none select-none no-select">
      {/* Zone de look (moitié droite) */}
      {isTouch && (
        <div
          className="absolute top-0 right-0 bottom-0 w-1/2 pointer-events-auto"
          onTouchStart={handleLookStart}
          onTouchMove={handleLookMove}
          onTouchEnd={handleLookEnd}
          onTouchCancel={handleLookEnd}
        />
      )}

      {/* === D-PAD CIRCULAIRE (bas-gauche) === */}
      <div
        className="absolute pointer-events-auto"
        style={{ bottom: "20px", left: "20px", width: "120px", height: "120px" }}
        onTouchStart={isTouch ? handleJoystickStart : undefined}
        onTouchMove={isTouch ? handleJoystickMove : undefined}
        onTouchEnd={isTouch ? handleJoystickEnd : undefined}
        onTouchCancel={isTouch ? handleJoystickEnd : undefined}
        onMouseDown={!isTouch ? (e) => {
          joystickOrigin.current = { x: e.clientX, y: e.clientY };
          setJoystickActive(true);
        } : undefined}
        onMouseMove={!isTouch && joystickActive ? (e) => {
          const dx = e.clientX - joystickOrigin.current.x;
          const dy = e.clientY - joystickOrigin.current.y;
          const maxRadius = 45;
          const dist = Math.min(Math.sqrt(dx * dx + dy * dy), maxRadius);
          const angle = Math.atan2(dy, dx);
          setJoystickPos({ x: Math.cos(angle) * dist, y: Math.sin(angle) * dist });
          gameRefs.current.move.x = Math.cos(angle) * dist / maxRadius;
          gameRefs.current.move.y = Math.sin(angle) * dist / maxRadius;
        } : undefined}
        onMouseUp={!isTouch ? () => {
          setJoystickActive(false);
          setJoystickPos({ x: 0, y: 0 });
          gameRefs.current.move.x = 0;
          gameRefs.current.move.y = 0;
        } : undefined}
        onMouseLeave={!isTouch && joystickActive ? () => {
          setJoystickActive(false);
          setJoystickPos({ x: 0, y: 0 });
          gameRefs.current.move.x = 0;
          gameRefs.current.move.y = 0;
        } : undefined}
      >
        {/* Cercle externe — épuré, bordure fine */}
        <div
          className="absolute inset-0 rounded-full"
          style={{
            background: "rgba(10, 20, 35, 0.4)",
            border: "2px solid rgba(26, 161, 206, 0.4)",
            boxShadow: "inset 0 0 12px rgba(26, 161, 206, 0.1)",
            backdropFilter: "blur(4px)",
          }}
        />

        {/* Flèches directionnelles (4) */}
        <div className="absolute top-1 left-1/2 -translate-x-1/2 text-cyan-400/50 text-xs">▲</div>
        <div className="absolute bottom-1 left-1/2 -translate-x-1/2 text-cyan-400/50 text-xs">▼</div>
        <div className="absolute left-1 top-1/2 -translate-y-1/2 text-cyan-400/50 text-xs">◄</div>
        <div className="absolute right-1 top-1/2 -translate-y-1/2 text-cyan-400/50 text-xs">►</div>

        {/* Knob central — épuré */}
        <div
          className="absolute top-1/2 left-1/2 rounded-full transition-transform"
          style={{
            width: "50px",
            height: "50px",
            transform: `translate(calc(-50% + ${joystickPos.x}px), calc(-50% + ${joystickPos.y}px))`,
            background: "rgba(26, 161, 206, 0.3)",
            border: "2px solid rgba(26, 161, 206, 0.8)",
            boxShadow: joystickActive ? "0 0 16px rgba(26, 161, 206, 0.6)" : "0 0 6px rgba(26, 161, 206, 0.3)",
            backdropFilter: "blur(4px)",
          }}
        >
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-1.5 h-1.5 rounded-full bg-cyan-300" />
        </div>
      </div>

      {/* === BOUTONS DROITS — épurés === */}
      <div className="absolute pointer-events-auto flex flex-col items-end gap-2" style={{ bottom: "20px", right: "20px" }}>
        {/* Ligne 1 : GRENADE + AIM + FIRE */}
        <div className="flex items-end gap-2">
          {/* Grenade — petit, transparent */}
          <button
            className="rounded-full flex items-center justify-center transition-all active:scale-90"
            style={{
              width: "44px", height: "44px",
              background: "rgba(108, 244, 46, 0.15)",
              border: "1.5px solid rgba(108, 244, 46, 0.5)",
              backdropFilter: "blur(4px)",
            }}
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.grenade = true; setTimeout(() => gameRefs.current.grenade = false, 100); }}
            onClick={() => { gameRefs.current.grenade = true; setTimeout(() => gameRefs.current.grenade = false, 100); }}
          >
            <span className="text-[8px] font-display text-green-400">GRN</span>
          </button>

          {/* AIM — moyen, transparent */}
          <button
            className={`rounded-full flex items-center justify-center transition-all active:scale-90 ${aimActive ? "bg-yellow-400/30 border-yellow-400" : ""}`}
            style={{
              width: "52px", height: "52px",
              background: aimActive ? "rgba(255, 231, 53, 0.3)" : "rgba(255, 231, 53, 0.12)",
              border: `1.5px solid ${aimActive ? "#FFE735" : "rgba(255, 231, 53, 0.5)"}`,
              backdropFilter: "blur(4px)",
            }}
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.aim = !gameRefs.current.aim; setAimActive(gameRefs.current.aim); }}
            onClick={() => { gameRefs.current.aim = !gameRefs.current.aim; setAimActive(gameRefs.current.aim); }}
          >
            <span className="text-[8px] font-display text-yellow-400">AIM</span>
          </button>

          {/* FIRE — grand, visible */}
          <button
            className={`rounded-full flex items-center justify-center transition-all active:scale-90 ${fireActive ? "brightness-125" : ""}`}
            style={{
              width: "72px", height: "72px",
              background: fireActive ? "rgba(254, 0, 34, 0.5)" : "rgba(254, 0, 34, 0.25)",
              border: "2px solid rgba(254, 0, 34, 0.8)",
              boxShadow: fireActive ? "0 0 24px rgba(254, 0, 34, 0.6)" : "0 0 8px rgba(254, 0, 34, 0.3)",
              backdropFilter: "blur(4px)",
            }}
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.shoot = true; setFireActive(true); }}
            onTouchEnd={(e) => { e.preventDefault(); gameRefs.current.shoot = false; setFireActive(false); }}
            onMouseDown={(e) => { e.preventDefault(); gameRefs.current.shoot = true; setFireActive(true); }}
            onMouseUp={(e) => { e.preventDefault(); gameRefs.current.shoot = false; setFireActive(false); }}
            onMouseLeave={() => { if (fireActive) { gameRefs.current.shoot = false; setFireActive(false); } }}
          >
            <span className="text-xs font-display text-white">FIRE</span>
          </button>
        </div>

        {/* Ligne 2 : SWITCH + RELOAD + SPRINT + JUMP */}
        <div className="flex items-end gap-2">
          <button
            className="rounded-full flex items-center justify-center transition-all active:scale-90"
            style={{ width: "40px", height: "40px", background: "rgba(168, 85, 247, 0.12)", border: "1.5px solid rgba(168, 85, 247, 0.5)", backdropFilter: "blur(4px)" }}
            onClick={() => {
              const next = weaponSlot === 1 ? 2 : weaponSlot === 2 ? 3 : 1;
              onSwitchWeapon(next);
            }}
          >
            <span className="text-[7px] font-display text-purple-400">SWP</span>
          </button>
          <button
            className="rounded-full flex items-center justify-center transition-all active:scale-90"
            style={{ width: "40px", height: "40px", background: "rgba(26, 161, 206, 0.12)", border: "1.5px solid rgba(26, 161, 206, 0.5)", backdropFilter: "blur(4px)" }}
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.reload = true; setTimeout(() => gameRefs.current.reload = false, 100); }}
            onClick={() => { gameRefs.current.reload = true; setTimeout(() => gameRefs.current.reload = false, 100); }}
          >
            <span className="text-[7px] font-display text-cyan-400">RLD</span>
          </button>
          <button
            className="rounded-full flex items-center justify-center transition-all active:scale-90"
            style={{ width: "44px", height: "44px", background: "rgba(26, 161, 206, 0.12)", border: "1.5px solid rgba(26, 161, 206, 0.5)", backdropFilter: "blur(4px)" }}
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.sprint = true; }}
            onTouchEnd={(e) => { e.preventDefault(); gameRefs.current.sprint = false; }}
            onMouseDown={(e) => { e.preventDefault(); gameRefs.current.sprint = true; }}
            onMouseUp={(e) => { e.preventDefault(); gameRefs.current.sprint = false; }}
          >
            <span className="text-[7px] font-display text-cyan-400">SPR</span>
          </button>
          <button
            className="rounded-full flex items-center justify-center transition-all active:scale-90"
            style={{ width: "44px", height: "44px", background: "rgba(26, 161, 206, 0.12)", border: "1.5px solid rgba(26, 161, 206, 0.5)", backdropFilter: "blur(4px)" }}
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.jump = true; }}
            onTouchEnd={(e) => { e.preventDefault(); gameRefs.current.jump = false; }}
            onMouseDown={(e) => { e.preventDefault(); gameRefs.current.jump = true; }}
            onMouseUp={(e) => { e.preventDefault(); gameRefs.current.jump = false; }}
          >
            <span className="text-[7px] font-display text-cyan-400">JMP</span>
          </button>
        </div>
      </div>

      {/* Indicateur arme (bas-centre) */}
      <div className="absolute pointer-events-auto flex gap-1" style={{ bottom: "10px", left: "50%", transform: "translateX(-50%)" }}>
        {([
          { slot: 1 as const, label: "1" },
          { slot: 2 as const, label: "2" },
          { slot: 3 as const, label: "3" },
        ]).map((w) => (
          <button
            key={w.slot}
            onClick={() => onSwitchWeapon(w.slot)}
            className="rounded-full transition-all"
            style={{
              width: "28px", height: "28px",
              background: weaponSlot === w.slot ? "rgba(26, 161, 206, 0.5)" : "rgba(10, 20, 35, 0.5)",
              border: weaponSlot === w.slot ? "1.5px solid #1AA1CE" : "1px solid rgba(26, 161, 206, 0.3)",
              color: weaponSlot === w.slot ? "#FFFFFF" : "#6B8CAE",
              fontSize: "10px",
              fontFamily: "var(--font-audiowide)",
              backdropFilter: "blur(4px)",
            }}
          >
            {w.label}
          </button>
        ))}
      </div>

      {/* Aide contrôles desktop */}
      {!isTouch && (
        <div className="absolute pointer-events-none" style={{ top: "50%", left: "10px", transform: "translateY(-50%)" }}>
          <div className="bg-black/50 backdrop-blur-sm rounded px-2 py-1.5 text-[7px] text-cyan-300/70 leading-relaxed border border-cyan-500/20">
            <div className="font-display text-cyan-400 text-[8px] mb-0.5">CONTROLS</div>
            <div>WASD: move</div>
            <div>Mouse: look</div>
            <div>Click: fire</div>
            <div>R: reload | G: grenade</div>
            <div>1/2/3: weapon</div>
            <div>Space: jump | Shift: sprint</div>
          </div>
        </div>
      )}
    </div>
  );
}
