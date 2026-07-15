"use client";

/* eslint-disable react-hooks/immutability -- refs mutables pour contrôles */

/**
 * KINETICS 5 — Contrôles tactiles TOUJOURS VISIBLES
 * Joystick gauche + boutons droits (FIRE/AIM/RELOAD/GRENADE/SWITCH/JUMP/SPRINT)
 * Visible sur desktop ET mobile (pour test + cohérence avec PDF page 6)
 * Sur desktop : WASD + souris fonctionnent EN PLUS des boutons
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
  const [sprintActive, setSprintActive] = useState(false);
  const joystickOrigin = useRef({ x: 0, y: 0 });
  const lookTouchId = useRef<number | null>(null);
  const lookLastPos = useRef({ x: 0, y: 0 });

  // Détection desktop vs mobile
  const [isTouch, setIsTouch] = useState(false);
  useEffect(() => {
    setIsTouch(
      "ontouchstart" in window || navigator.maxTouchPoints > 0 || window.matchMedia("(pointer: coarse)").matches
    );
  }, []);

  // Joystick (touch) handlers
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
    const dist = Math.min(Math.sqrt(dx * dx + dy * dy), maxRadius);
    const angle = Math.atan2(dy, dx);
    const x = Math.cos(angle) * dist;
    const y = Math.sin(angle) * dist;
    setJoystickPos({ x, y });
    gameRefs.current.joystick = { x: x / maxRadius, y: y / maxRadius };
    // Appliquer au move
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

  // Boutons press handlers (touch + mouse)
  const press = (key: keyof GameRefs, val: boolean, setter?: (v: boolean) => void) => ({
    onTouchStart: (e: React.TouchEvent) => { e.preventDefault(); (gameRefs.current as any)[key] = val; setter?.(val); },
    onTouchEnd: (e: React.TouchEvent) => { e.preventDefault(); (gameRefs.current as any)[key] = !val; setter?.(!val); },
    onMouseDown: (e: React.MouseEvent) => { e.preventDefault(); (gameRefs.current as any)[key] = val; setter?.(val); },
    onMouseUp: (e: React.MouseEvent) => { e.preventDefault(); (gameRefs.current as any)[key] = !val; setter?.(!val); },
    onMouseLeave: (e: React.MouseEvent) => { if ((gameRefs.current as any)[key] === val) { (gameRefs.current as any)[key] = !val; setter?.(!val); } },
  });

  return (
    <div className="absolute inset-0 z-30 pointer-events-none select-none no-select">
      {/* Zone de look tactile (moitié droite, derrière les boutons) */}
      {isTouch && (
        <div
          className="absolute top-0 right-0 bottom-0 w-1/2 pointer-events-auto"
          onTouchStart={handleLookStart}
          onTouchMove={handleLookMove}
          onTouchEnd={handleLookEnd}
          onTouchCancel={handleLookEnd}
        />
      )}

      {/* === JOYSTICK GAUCHE === */}
      <div
        className="absolute bottom-24 left-6 w-32 h-32 pointer-events-auto"
        onTouchStart={isTouch ? handleJoystickStart : undefined}
        onTouchMove={isTouch ? handleJoystickMove : undefined}
        onTouchEnd={isTouch ? handleJoystickEnd : undefined}
        onTouchCancel={isTouch ? handleJoystickEnd : undefined}
        // Sur desktop : clic + drag = joystick
        onMouseDown={!isTouch ? (e) => {
          joystickOrigin.current = { x: e.clientX, y: e.clientY };
          setJoystickActive(true);
        } : undefined}
        onMouseMove={!isTouch && joystickActive ? (e) => {
          const dx = e.clientX - joystickOrigin.current.x;
          const dy = e.clientY - joystickOrigin.current.y;
          const maxRadius = 50;
          const dist = Math.min(Math.sqrt(dx * dx + dy * dy), maxRadius);
          const angle = Math.atan2(dy, dx);
          const x = Math.cos(angle) * dist;
          const y = Math.sin(angle) * dist;
          setJoystickPos({ x, y });
          gameRefs.current.move.x = x / maxRadius;
          gameRefs.current.move.y = y / maxRadius;
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
        {/* Cercle externe */}
        <div className="absolute inset-0 rounded-full border-2 border-k5-cyan/50 bg-k5-panel/40 backdrop-blur-sm" />
        {/* Croix directionnelle */}
        <div className="absolute top-1/2 left-0 right-0 h-px bg-k5-cyan/20" />
        <div className="absolute left-1/2 top-0 bottom-0 w-px bg-k5-cyan/20" />
        {/* Knob */}
        <div
          className="absolute top-1/2 left-1/2 w-14 h-14 rounded-full border-2 border-k5-cyan bg-k5-cyan/30 backdrop-blur-sm flex items-center justify-center transition-transform"
          style={{
            transform: `translate(calc(-50% + ${joystickPos.x}px), calc(-50% + ${joystickPos.y}px))`,
            boxShadow: joystickActive ? "0 0 16px #1AA1CE" : "0 0 8px #1AA1CE66",
          }}
        >
          <div className="w-2 h-2 rounded-full bg-k5-cyan" />
        </div>
        {/* Label */}
        <div className="absolute -top-5 left-1/2 -translate-x-1/2 text-[8px] font-display text-k5-cyan/70 tracking-wider">MOVE</div>
      </div>

      {/* === BOUTONS DROITS === */}
      <div className="absolute bottom-24 right-4 flex flex-col items-end gap-2 pointer-events-auto">
        {/* Ligne 1 : GRENADE + AIM + FIRE */}
        <div className="flex items-end gap-2">
          <button
            className="w-12 h-12 rounded-full border-2 border-k5-green/70 bg-k5-green/20 backdrop-blur-sm flex items-center justify-center active:scale-90 transition-transform"
            {...press("grenade", true)}
            style={{ touchAction: "none" }}
          >
            <span className="text-[8px] font-display text-k5-green leading-tight text-center">GRN<br/>G</span>
          </button>
          <button
            className={`w-14 h-14 rounded-full border-2 backdrop-blur-sm flex items-center justify-center active:scale-90 transition-transform ${aimActive ? "border-k5-yellow bg-k5-yellow/40" : "border-k5-yellow/70 bg-k5-yellow/20"}`}
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.aim = !gameRefs.current.aim; setAimActive(gameRefs.current.aim); }}
            onClick={() => { gameRefs.current.aim = !gameRefs.current.aim; setAimActive(gameRefs.current.aim); }}
            style={{ touchAction: "none" }}
          >
            <span className="text-[8px] font-display text-k5-yellow leading-tight text-center">AIM<br/>E</span>
          </button>
          <button
            className={`w-20 h-20 rounded-full border-2 border-k5-red backdrop-blur-sm flex items-center justify-center active:scale-90 transition-transform ${fireActive ? "bg-k5-red/60 border-k5-red" : "bg-k5-red/30"}`}
            style={{ boxShadow: fireActive ? "0 0 24px #FE0022" : "0 0 12px #FE002266", touchAction: "none" }}
            {...press("shoot", true, setFireActive)}
          >
            <span className="text-xs font-display text-white">FIRE</span>
          </button>
        </div>
        {/* Ligne 2 : SWITCH + RELOAD + JUMP */}
        <div className="flex items-end gap-2">
          <button
            className="w-12 h-12 rounded-full border-2 border-k5-purple/70 bg-k5-purple/20 backdrop-blur-sm flex items-center justify-center active:scale-90 transition-transform"
            onClick={() => {
              const next = weaponSlot === 1 ? 2 : weaponSlot === 2 ? 3 : 1;
              onSwitchWeapon(next);
            }}
            style={{ touchAction: "none" }}
          >
            <span className="text-[8px] font-display text-k5-purple leading-tight text-center">SWP<br/>1·2·3</span>
          </button>
          <button
            className="w-12 h-12 rounded-full border-2 border-k5-cyan/70 bg-k5-cyan/20 backdrop-blur-sm flex items-center justify-center active:scale-90 transition-transform"
            onTouchStart={(e) => { e.preventDefault(); gameRefs.current.reload = true; setTimeout(() => gameRefs.current.reload = false, 100); }}
            onClick={() => { gameRefs.current.reload = true; setTimeout(() => gameRefs.current.reload = false, 100); }}
            style={{ touchAction: "none" }}
          >
            <span className="text-[8px] font-display text-k5-cyan leading-tight text-center">RLD<br/>R</span>
          </button>
          <button
            className={`w-14 h-14 rounded-full border-2 border-k5-cyan/70 backdrop-blur-sm flex items-center justify-center active:scale-90 transition-transform ${sprintActive ? "bg-k5-cyan/50" : "bg-k5-cyan/20"}`}
            {...press("sprint", true, setSprintActive)}
            style={{ touchAction: "none" }}
          >
            <span className="text-[8px] font-display text-k5-cyan leading-tight text-center">SPR<br/>⇧</span>
          </button>
          <button
            className="w-14 h-14 rounded-full border-2 border-k5-cyan/70 bg-k5-cyan/20 backdrop-blur-sm flex items-center justify-center active:scale-90 transition-transform"
            {...press("jump", true)}
            style={{ touchAction: "none" }}
          >
            <span className="text-[8px] font-display text-k5-cyan leading-tight text-center">JMP<br/>␣</span>
          </button>
        </div>
      </div>

      {/* === Indicateur arme (bas-centre, sous le HUD) === */}
      <div className="absolute bottom-3 left-1/2 -translate-x-1/2 flex gap-1 pointer-events-auto">
        {([
          { slot: 1 as const, label: "1·PRI" },
          { slot: 2 as const, label: "2·SEC" },
          { slot: 3 as const, label: "3·TAC" },
        ]).map((w) => (
          <button
            key={w.slot}
            onClick={() => onSwitchWeapon(w.slot)}
            className={`px-2 py-1 text-[9px] font-display rounded-sm border ${weaponSlot === w.slot ? "bg-k5-cyan text-k5-deep-space border-k5-cyan" : "bg-k5-panel/60 text-k5-muted border-k5-border"}`}
          >
            {w.label}
          </button>
        ))}
      </div>

      {/* === Aide contrôles desktop === */}
      {!isTouch && (
        <div className="absolute top-1/2 left-3 -translate-y-1/2 bg-k5-panel/70 border border-k5-cyan/30 px-2 py-1.5 rounded-sm text-[8px] text-k5-muted leading-relaxed pointer-events-none">
          <div className="font-display text-k5-cyan text-[9px] mb-0.5">CONTROLES</div>
          <div>ZQSD/WASD : bouger</div>
          <div>Souris : regarder</div>
          <div>Clic G : tirer</div>
          <div>Clic D : viser</div>
          <div>R : recharger</div>
          <div>G : grenade</div>
          <div>1/2/3 : arme</div>
          <div>Espace : saut</div>
          <div>Shift : sprint</div>
        </div>
      )}
    </div>
  );
}
