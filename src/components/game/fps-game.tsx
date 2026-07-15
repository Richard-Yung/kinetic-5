"use client";

/* eslint-disable react-hooks/immutability -- refs mutables nécessaires pour la game loop (pattern standard) */

/**
 * KINETICS 5 — Niveau FPS 3D (React Three Fiber)
 * Scène : intérieur de vaisseau spatial (corridor + salle)
 * FPS : caméra first-person, déplacement, tir, ennemis
 * Contrôles : clavier/souris (desktop) + touch joystick (mobile)
 * Optimisé : géométries simples, instancing, frustum culling
 */

import { Canvas, useFrame, useThree } from "@react-three/fiber";
import { Stars } from "@react-three/drei";
import { useRef, useState, useMemo, useEffect, useCallback } from "react";
import * as THREE from "three";
import { useGameStore } from "@/store/game-store";
import { AGENTS, ENEMIES, MISSIONS, getWeapon, type Enemy } from "@/lib/kinetics-data";
import { HUDScreen, type HUDState } from "@/components/screens/hud-screen";
import { TouchControls } from "@/components/game/touch-controls";
import { KButton } from "@/components/kinetics/ui";
import { t } from "@/lib/i18n";

/* ============================================================
   Types internes
   ============================================================ */
interface EnemyInstance {
  id: number;
  enemy: Enemy;
  position: THREE.Vector3;
  health: number;
  maxHealth: number;
  alive: boolean;
  lastShot: number;
  hitFlash: number;
}

interface GameRefs {
  velocity: THREE.Vector3;
  moveForward: boolean;
  moveBackward: boolean;
  moveLeft: boolean;
  moveRight: boolean;
  jump: boolean;
  sprint: boolean;
  shoot: boolean;
  aim: boolean;
  reload: boolean;
  yaw: number;
  pitch: number;
  joystick: { x: number; y: number };
  lookDelta: { x: number; y: number };
}

/* ============================================================
   ShipEnvironment — intérieur du vaisseau
   Corridor + salle avec murs, sol, plafond, lumières
   ============================================================ */
function ShipEnvironment() {
  // Couleurs matériaux
  const wallColor = "#1a2a3e";
  const floorColor = "#0a1525";
  const accentColor = "#1AA1CE";

  return (
    <group>
      {/* Sol */}
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0, 0]} receiveShadow>
        <planeGeometry args={[40, 40]} />
        <meshStandardMaterial color={floorColor} metalness={0.7} roughness={0.4} />
      </mesh>
      {/* Plafond */}
      <mesh rotation={[Math.PI / 2, 0, 0]} position={[0, 4, 0]} receiveShadow>
        <planeGeometry args={[40, 40]} />
        <meshStandardMaterial color={floorColor} metalness={0.5} roughness={0.6} />
      </mesh>
      {/* Murs périphériques */}
      {[
        { pos: [0, 2, -20] as const, rot: [0, 0, 0] as const, size: [40, 4] as const },
        { pos: [0, 2, 20] as const, rot: [0, Math.PI, 0] as const, size: [40, 4] as const },
        { pos: [-20, 2, 0] as const, rot: [0, Math.PI / 2, 0] as const, size: [40, 4] as const },
        { pos: [20, 2, 0] as const, rot: [0, -Math.PI / 2, 0] as const, size: [40, 4] as const },
      ].map((w, i) => (
        <mesh key={i} position={w.pos} rotation={w.rot} castShadow receiveShadow>
          <planeGeometry args={w.size} />
          <meshStandardMaterial color={wallColor} metalness={0.6} roughness={0.5} />
        </mesh>
      ))}

      {/* Cloisons internes (créer un layout corridor + salle) */}
      {[
        { pos: [-8, 2, -8], size: [8, 4, 0.3] },
        { pos: [8, 2, -8], size: [8, 4, 0.3] },
        { pos: [-8, 2, 8], size: [8, 4, 0.3] },
        { pos: [8, 2, 8], size: [8, 4, 0.3] },
        { pos: [-12, 2, 0], size: [0.3, 4, 8] },
        { pos: [12, 2, 0], size: [0.3, 4, 8] },
      ].map((w, i) => (
        <mesh key={`inner-${i}`} position={w.pos as [number, number, number]} castShadow receiveShadow>
          <boxGeometry args={w.size as [number, number, number]} />
          <meshStandardMaterial color={wallColor} metalness={0.6} roughness={0.5} />
        </mesh>
      ))}

      {/* Piliers / caisses décoratifs (couvertures) */}
      {[
        { pos: [-5, 0.6, -3], size: [1.2, 1.2, 1.2] },
        { pos: [5, 0.6, 3], size: [1.2, 1.2, 1.2] },
        { pos: [-5, 0.6, 5], size: [1.2, 1.2, 1.2] },
        { pos: [5, 0.6, -5], size: [1.2, 1.2, 1.2] },
        { pos: [0, 0.4, -12], size: [2, 0.8, 1] },
        { pos: [0, 0.4, 12], size: [2, 0.8, 1] },
      ].map((c, i) => (
        <mesh key={`crate-${i}`} position={c.pos as [number, number, number]} castShadow>
          <boxGeometry args={c.size as [number, number, number]} />
          <meshStandardMaterial color="#2a3a4e" metalness={0.5} roughness={0.7} />
        </mesh>
      ))}

      {/* Lignes lumineuses au sol (cyan) */}
      {[-15, -5, 5, 15].map((z, i) => (
        <mesh key={`line-${i}`} position={[0, 0.02, z]} rotation={[-Math.PI / 2, 0, 0]}>
          <planeGeometry args={[40, 0.08]} />
          <meshBasicMaterial color={accentColor} toneMapped={false} />
        </mesh>
      ))}
      {[-15, -5, 5, 15].map((x, i) => (
        <mesh key={`vline-${i}`} position={[x, 0.02, 0]} rotation={[-Math.PI / 2, 0, Math.PI / 2]}>
          <planeGeometry args={[40, 0.08]} />
          <meshBasicMaterial color={accentColor} toneMapped={false} />
        </mesh>
      ))}

      {/* Panneaux lumineux muraux */}
      {Array.from({ length: 8 }).map((_, i) => {
        const angle = (i / 8) * Math.PI * 2;
        const x = Math.cos(angle) * 18;
        const z = Math.sin(angle) * 18;
        return (
          <mesh key={`panel-${i}`} position={[x, 3, z]} rotation={[0, -angle + Math.PI, 0]}>
            <planeGeometry args={[2, 0.5]} />
            <meshBasicMaterial color={accentColor} toneMapped={false} opacity={0.8} transparent />
          </mesh>
        );
      })}

      {/* Porte d'extraction (au fond) */}
      <mesh position={[0, 2, -19.8]}>
        <planeGeometry args={[3, 3.5]} />
        <meshBasicMaterial color="#6CF42E" toneMapped={false} opacity={0.3} transparent />
      </mesh>
      <mesh position={[0, 2, -19.7]}>
        <planeGeometry args={[2.8, 3.3]} />
        <meshBasicMaterial color="#6CF42E" toneMapped={false} opacity={0.15} transparent />
      </mesh>
    </group>
  );
}

/* ============================================================
   EnemyEntity — un ennemi 3D
   ============================================================ */
function EnemyEntity({
  instance,
  playerPos,
}: {
  instance: EnemyInstance;
  playerPos: React.MutableRefObject<THREE.Vector3>;
}) {
  const ref = useRef<THREE.Group>(null);
  const [flash, setFlash] = useState(0);

  useFrame((_, delta) => {
    if (!ref.current || !instance.alive) return;
    const dir = new THREE.Vector3().subVectors(playerPos.current, instance.position);
    dir.y = 0;
    const dist = dir.length();
    dir.normalize();

    // Mouvement vers le joueur (si à portée)
    if (dist > instance.enemy.attackRange * 0.6) {
      const speed = instance.enemy.moveSpeed * 0.6;
      instance.position.x += dir.x * speed * delta;
      instance.position.z += dir.z * speed * delta;
    }

    ref.current.position.copy(instance.position);
    // Face au joueur
    ref.current.lookAt(playerPos.current.x, instance.position.y, playerPos.current.z);

    // Flash de dégât
    if (instance.hitFlash > 0) {
      instance.hitFlash -= delta * 4;
      setFlash(instance.hitFlash > 0 ? instance.hitFlash : 0);
    }
  });

  if (!instance.alive) return null;

  const color = instance.enemy.enemyClass === "Boss" ? "#FE0022" : "#8a4a3a";
  const emissiveColor = flash > 0 ? "#FFFFFF" : color;
  const scale = instance.enemy.enemyClass === "Boss" ? 2.5 : instance.enemy.enemyClass === "Heavy" ? 1.6 : 1;

  return (
    <group ref={ref} scale={scale}>
      {/* Corps */}
      <mesh position={[0, 1, 0]} castShadow>
        <capsuleGeometry args={[0.35, 0.8, 4, 8]} />
        <meshStandardMaterial
          color={color}
          emissive={emissiveColor}
          emissiveIntensity={flash * 2}
          metalness={0.4}
          roughness={0.6}
        />
      </mesh>
      {/* Tête */}
      <mesh position={[0, 1.7, 0]} castShadow>
        <sphereGeometry args={[0.25, 8, 8]} />
        <meshStandardMaterial color={color} emissive={emissiveColor} emissiveIntensity={flash * 2} />
      </mesh>
      {/* Visire (lumineuse) */}
      <mesh position={[0, 1.7, 0.2]}>
        <boxGeometry args={[0.3, 0.1, 0.05]} />
        <meshBasicMaterial color={instance.enemy.enemyClass === "Boss" ? "#FE0022" : "#1AA1CE"} toneMapped={false} />
      </mesh>
      {/* Bras */}
      <mesh position={[-0.45, 1, 0]} castShadow>
        <capsuleGeometry args={[0.1, 0.6, 4, 6]} />
        <meshStandardMaterial color={color} emissive={emissiveColor} emissiveIntensity={flash * 2} />
      </mesh>
      <mesh position={[0.45, 1, 0]} castShadow>
        <capsuleGeometry args={[0.1, 0.6, 4, 6]} />
        <meshStandardMaterial color={color} emissive={emissiveColor} emissiveIntensity={flash * 2} />
      </mesh>
      {/* Jambes */}
      <mesh position={[-0.2, 0.3, 0]} castShadow>
        <capsuleGeometry args={[0.12, 0.5, 4, 6]} />
        <meshStandardMaterial color={color} emissive={emissiveColor} emissiveIntensity={flash * 2} />
      </mesh>
      <mesh position={[0.2, 0.3, 0]} castShadow>
        <capsuleGeometry args={[0.12, 0.5, 4, 6]} />
        <meshStandardMaterial color={color} emissive={emissiveColor} emissiveIntensity={flash * 2} />
      </mesh>

      {/* Barre de vie au-dessus */}
      <group position={[0, 2.5, 0]}>
        <mesh>
          <planeGeometry args={[1, 0.08]} />
          <meshBasicMaterial color="#0a0a0a" toneMapped={false} />
        </mesh>
        <mesh position={[-(1 - instance.health / instance.maxHealth) / 2, 0, 0.01]}>
          <planeGeometry args={[instance.health / instance.maxHealth, 0.06]} />
          <meshBasicMaterial color={instance.health / instance.maxHealth > 0.5 ? "#6CF42E" : instance.health / instance.maxHealth > 0.25 ? "#FFE735" : "#FE0022"} toneMapped={false} />
        </mesh>
      </group>
    </group>
  );
}

/* ============================================================
   WeaponViewmodel — arme en vue first-person
   ============================================================ */
function WeaponViewmodel({
  weaponId,
  shooting,
  reloading,
  aim,
}: {
  weaponId: string;
  shooting: boolean;
  reloading: boolean;
  aim: boolean;
}) {
  const ref = useRef<THREE.Group>(null);
  const weapon = getWeapon(weaponId);
  const color = weapon ? "#3a4a5e" : "#444";
  const accent = "#1AA1CE";

  useFrame((_, delta) => {
    if (!ref.current) return;
    // Sway / bob
    const targetX = aim ? 0 : 0.3;
    const targetY = aim ? -0.15 : -0.25;
    const targetZ = aim ? -0.4 : -0.5;
    ref.current.position.x += (targetX - ref.current.position.x) * delta * 10;
    ref.current.position.y += (targetY - ref.current.position.y) * delta * 10;
    ref.current.position.z += (targetZ - ref.current.position.z) * delta * 10;
    // Recoil
    if (shooting) {
      ref.current.position.z += 0.05;
      ref.current.rotation.x = -0.05;
    } else {
      ref.current.rotation.x += (0 - ref.current.rotation.x) * delta * 8;
    }
    // Reload spin
    if (reloading) {
      ref.current.rotation.z = Math.sin(performance.now() / 100) * 0.3;
    } else {
      ref.current.rotation.z += (0 - ref.current.rotation.z) * delta * 8;
    }
  });

  return (
    <group ref={ref} position={[0.3, -0.25, -0.5]}>
      {/* Corps de l'arme */}
      <mesh>
        <boxGeometry args={[0.08, 0.12, 0.5]} />
        <meshStandardMaterial color={color} metalness={0.8} roughness={0.3} />
      </mesh>
      {/* Canon */}
      <mesh position={[0, 0.02, -0.3]}>
        <boxGeometry args={[0.04, 0.04, 0.2]} />
        <meshStandardMaterial color="#1a1a1a" metalness={0.9} roughness={0.2} />
      </mesh>
      {/* Poignée */}
      <mesh position={[0, -0.12, 0.1]} rotation={[0.3, 0, 0]}>
        <boxGeometry args={[0.06, 0.14, 0.08]} />
        <meshStandardMaterial color={color} metalness={0.7} roughness={0.4} />
      </mesh>
      {/* Chargeur */}
      <mesh position={[0, -0.1, -0.05]}>
        <boxGeometry args={[0.05, 0.1, 0.12]} />
        <meshStandardMaterial color="#2a2a2a" metalness={0.6} roughness={0.5} />
      </mesh>
      {/* Viseur */}
      <mesh position={[0, 0.1, 0]}>
        <boxGeometry args={[0.02, 0.04, 0.02]} />
        <meshBasicMaterial color={accent} toneMapped={false} />
      </mesh>
      {/* Bande lumineuse élément */}
      <mesh position={[0, 0, 0.05]}>
        <boxGeometry args={[0.02, 0.02, 0.3]} />
        <meshBasicMaterial color={accent} toneMapped={false} />
      </mesh>
      {/* Muzzle flash */}
      {shooting && (
        <mesh position={[0, 0.02, -0.45]}>
          <sphereGeometry args={[0.08, 6, 6]} />
          <meshBasicMaterial color="#FFE735" toneMapped={false} transparent opacity={0.9} />
        </mesh>
      )}
    </group>
  );
}

/* ============================================================
   Player — caméra + contrôles + tir
   ============================================================ */
function Player({
  gameRefs,
  enemies,
  onFire,
  onHit,
  onPlayerHit,
  playerPos,
}: {
  gameRefs: React.MutableRefObject<GameRefs>;
  enemies: React.MutableRefObject<EnemyInstance[]>;
  onFire: () => void;
  onHit: (enemyId: number, damage: number, isHead: boolean) => void;
  onPlayerHit: (damage: number) => void;
  playerPos: React.MutableRefObject<THREE.Vector3>;
}) {
  const { camera, scene } = useThree();
  const weaponId = useGameStore((s) => s.equippedPrimaryId);
  const agent = useSelectedAgentStore();
  const [shooting, setShooting] = useState(false);
  const [reloading, setReloading] = useState(false);
  const [aim, setAim] = useState(false);
  const lastFireRef = useRef(0);
  const raycaster = useMemo(() => new THREE.Raycaster(), []);

  // Setup initial camera
  useEffect(() => {
    camera.position.set(0, 1.6, 8);
    playerPos.current.set(0, 1.6, 8);
  }, [camera, playerPos]);

  const triggerReload = useCallback(() => {
    setReloading(true);
    setTimeout(() => setReloading(false), 2000);
  }, []);

  // Keyboard input
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      switch (e.code) {
        case "KeyW": case "ArrowUp": gameRefs.current.moveForward = true; break;
        case "KeyS": case "ArrowDown": gameRefs.current.moveBackward = true; break;
        case "KeyA": case "ArrowLeft": gameRefs.current.moveLeft = true; break;
        case "KeyD": case "ArrowRight": gameRefs.current.moveRight = true; break;
        case "Space": gameRefs.current.jump = true; break;
        case "ShiftLeft": gameRefs.current.sprint = true; break;
        case "KeyR": triggerReload(); break;
        case "KeyE": setAim(a => !a); break;
      }
    };
    const onKeyUp = (e: KeyboardEvent) => {
      switch (e.code) {
        case "KeyW": case "ArrowUp": gameRefs.current.moveForward = false; break;
        case "KeyS": case "ArrowDown": gameRefs.current.moveBackward = false; break;
        case "KeyA": case "ArrowLeft": gameRefs.current.moveLeft = false; break;
        case "KeyD": case "ArrowRight": gameRefs.current.moveRight = false; break;
        case "Space": gameRefs.current.jump = false; break;
        case "ShiftLeft": gameRefs.current.sprint = false; break;
      }
    };
    const onMouseDown = (e: MouseEvent) => {
      if (e.button === 0) gameRefs.current.shoot = true;
      if (e.button === 2) setAim(true);
    };
    const onMouseUp = (e: MouseEvent) => {
      if (e.button === 0) gameRefs.current.shoot = false;
      if (e.button === 2) setAim(false);
    };
    const onContextMenu = (e: Event) => e.preventDefault();

    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("keyup", onKeyUp);
    window.addEventListener("mousedown", onMouseDown);
    window.addEventListener("mouseup", onMouseUp);
    window.addEventListener("contextmenu", onContextMenu);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("keyup", onKeyUp);
      window.removeEventListener("mousedown", onMouseDown);
      window.removeEventListener("mouseup", onMouseUp);
      window.removeEventListener("contextmenu", onContextMenu);
    };
  }, [gameRefs]);

  // Main loop
  useFrame((_, delta) => {
    const refs = gameRefs.current;
    const speed = (agent.baseSpeed * 4) * (refs.sprint ? 1.6 : 1);

    // Rotation (look) depuis le joystick ou souris (PointerLock gère la souris)
    if (refs.lookDelta.x !== 0 || refs.lookDelta.y !== 0) {
      refs.yaw -= refs.lookDelta.x * 0.003;
      refs.pitch -= refs.lookDelta.y * 0.003;
      refs.pitch = Math.max(-Math.PI / 2 + 0.1, Math.min(Math.PI / 2 - 0.1, refs.pitch));
      refs.lookDelta.x = 0;
      refs.lookDelta.y = 0;
    }

    // Appliquer rotation caméra
    camera.rotation.order = "YXZ";
    camera.rotation.y = refs.yaw;
    camera.rotation.x = refs.pitch;

    // Mouvement
    const forward = new THREE.Vector3();
    const right = new THREE.Vector3();
    camera.getWorldDirection(forward);
    forward.y = 0;
    forward.normalize();
    right.crossVectors(forward, new THREE.Vector3(0, 1, 0)).normalize();

    const move = new THREE.Vector3();
    if (refs.moveForward) move.add(forward);
    if (refs.moveBackward) move.sub(forward);
    if (refs.moveLeft) move.sub(right);
    if (refs.moveRight) move.add(right);
    // Joystick
    if (refs.joystick.x !== 0 || refs.joystick.y !== 0) {
      move.add(right.clone().multiplyScalar(refs.joystick.x));
      move.add(forward.clone().multiplyScalar(-refs.joystick.y));
    }
    if (move.lengthSq() > 0) move.normalize().multiplyScalar(speed * delta);

    // Appliquer mouvement avec collisions simples (limites)
    const newX = Math.max(-18, Math.min(18, camera.position.x + move.x));
    const newZ = Math.max(-18, Math.min(18, camera.position.z + move.z));
    camera.position.x = newX;
    camera.position.z = newZ;
    // Gravité / saut simple
    if (refs.jump && camera.position.y <= 1.6) {
      refs.velocity.y = 6;
    }
    refs.velocity.y -= 18 * delta;
    camera.position.y += refs.velocity.y * delta;
    if (camera.position.y < 1.6) {
      camera.position.y = 1.6;
      refs.velocity.y = 0;
    }

    playerPos.current.copy(camera.position);

    // Tir
    const weapon = getWeapon(weaponId);
    const fireRate = weapon ? 60 / (weapon.fireRate * 2 + 10) : 0.15;
    const now = performance.now() / 1000;
    if (refs.shoot && !reloading && now - lastFireRef.current > fireRate) {
      lastFireRef.current = now;
      setShooting(true);
      setTimeout(() => setShooting(false), 50);
      onFire();

      // Raycast
      const dir = new THREE.Vector3();
      camera.getWorldDirection(dir);
      raycaster.set(camera.position, dir);
      raycaster.far = weapon?.range ?? 100;

      // Trouver ennemi le plus proche sur la ligne
      let closestHit: { enemy: EnemyInstance; distance: number; isHead: boolean } | null = null;
      for (const e of enemies.current) {
        if (!e.alive) continue;
        // Test intersection avec capsule (head + body)
        const headPos = new THREE.Vector3(e.position.x, e.position.y + 1.7, e.position.z);
        const bodyPos = new THREE.Vector3(e.position.x, e.position.y + 1, e.position.z);
        const headDist = raycaster.ray.distanceToPoint(headPos);
        const bodyDist = raycaster.ray.distanceToPoint(bodyPos);
        const headAlong = raycaster.ray.direction.dot(headPos.clone().sub(raycaster.ray.origin));
        const bodyAlong = raycaster.ray.direction.dot(bodyPos.clone().sub(raycaster.ray.origin));
        if (headAlong > 0 && headAlong < (weapon?.range ?? 100) && headDist < 0.35) {
          if (!closestHit || headAlong < closestHit.distance) {
            closestHit = { enemy: e, distance: headAlong, isHead: true };
          }
        } else if (bodyAlong > 0 && bodyAlong < (weapon?.range ?? 100) && bodyDist < 0.6) {
          if (!closestHit || bodyAlong < closestHit.distance) {
            closestHit = { enemy: e, distance: bodyAlong, isHead: false };
          }
        }
      }
      if (closestHit) {
        const baseDmg = (weapon?.damage ?? 50) * 10;
        const dmg = closestHit.isHead ? baseDmg * 2 : baseDmg;
        onHit(closestHit.enemy.id, dmg, closestHit.isHead);
      }
    }

    // Ennemis tirent sur le joueur
    for (const e of enemies.current) {
      if (!e.alive) continue;
      const dist = e.position.distanceTo(camera.position);
      if (dist < e.enemy.attackRange && now - e.lastShot > 1 / e.enemy.attackRate) {
        e.lastShot = now;
        // Chance de toucher basée sur la distance
        const hitChance = 1 - (dist / e.enemy.attackRange) * 0.5;
        if (Math.random() < hitChance) {
          onPlayerHit(e.enemy.baseDamage * 0.3);
        }
      }
    }
  });

  return <WeaponViewmodel weaponId={weaponId} shooting={shooting} reloading={reloading} aim={aim} />;
}

// Hook helper pour éviter re-render
function useSelectedAgentStore() {
  const id = useGameStore((s) => s.selectedAgentId);
  return AGENTS.find((a) => a.id === id) ?? AGENTS[0];
}

/* ============================================================
   FPSGame — composant principal de la scène
   ============================================================ */
export function FPSGame({ onExit }: { onExit: () => void }) {
  const { endMission, currentMissionId, language, difficulty } = useGameStore();
  const mission = MISSIONS.find((m) => m.id === currentMissionId) ?? MISSIONS[0];
  const agentId = useGameStore((s) => s.selectedAgentId);
  const agent = AGENTS.find((a) => a.id === agentId) ?? AGENTS[0];

  const playerPos = useRef(new THREE.Vector3(0, 1.6, 8));
  const gameRefs = useRef<GameRefs>({
    velocity: new THREE.Vector3(),
    moveForward: false, moveBackward: false, moveLeft: false, moveRight: false,
    jump: false, sprint: false, shoot: false, aim: false, reload: false,
    yaw: 0, pitch: 0,
    joystick: { x: 0, y: 0 },
    lookDelta: { x: 0, y: 0 },
  });

  // État de jeu
  const [health, setHealth] = useState(agent.baseHealth);
  const [armor, setArmor] = useState(agent.baseShield);
  const [ammo, setAmmo] = useState(() => {
    const w = getWeapon(useGameStore.getState().equippedPrimaryId);
    return w?.magazineSize ?? 30;
  });
  const [reserveAmmo, setReserveAmmo] = useState(() => {
    const w = getWeapon(useGameStore.getState().equippedPrimaryId);
    return (w?.magazineSize ?? 30) * 4;
  });
  const [ultimate, setUltimate] = useState(0);
  const [timeLeft, setTimeLeft] = useState(mission.timeLimit);
  const [waveIndex, setWaveIndex] = useState(1);
  const [enemiesKilled, setEnemiesKilled] = useState(0);
  const [damageDealt, setDamageDealt] = useState(0);
  const [damageTaken, setDamageTaken] = useState(0);
  const [shotsFired, setShotsFired] = useState(0);
  const [shotsHit, setShotsHit] = useState(0);
  const [hitMarker, setHitMarker] = useState<{ visible: boolean; crit: boolean }>({ visible: false, crit: false });
  const [damageDirection, setDamageDirection] = useState<{ angle: number; visible: boolean } | null>(null);
  const [paused, setPaused] = useState(false);

  // Ennemis
  const enemies = useRef<EnemyInstance[]>([]);
  const [enemyVersion, setEnemyVersion] = useState(0); // force re-render
  const enemyIdCounter = useRef(0);

  const weaponName = getWeapon(useGameStore.getState().equippedPrimaryId)?.displayName ?? "RIFLE";

  // Difficulty modifiers
  const diffMult = difficulty === "easy" ? 0.7 : difficulty === "hard" ? 1.4 : 1;

  // Spawn initial wave
  useEffect(() => {
    const wave = mission.waves[0];
    if (!wave) return;
    const newEnemies: EnemyInstance[] = [];
    for (const spawn of wave.spawns) {
      const enemyData = ENEMIES.find((e) => e.id === spawn.enemyId);
      if (!enemyData) continue;
      for (let i = 0; i < spawn.count; i++) {
        const angle = Math.random() * Math.PI * 2;
        const dist = 10 + Math.random() * 8;
        newEnemies.push({
          id: enemyIdCounter.current++,
          enemy: enemyData,
          position: new THREE.Vector3(Math.cos(angle) * dist, 0, Math.sin(angle) * dist - 5),
          health: enemyData.baseHealth * diffMult,
          maxHealth: enemyData.baseHealth * diffMult,
          alive: true,
          lastShot: 0,
          hitFlash: 0,
        });
      }
    }
    enemies.current = newEnemies;
    setEnemyVersion(v => v + 1);
  }, [mission, diffMult]);

  // Timer
  useEffect(() => {
    if (paused) return;
    const interval = setInterval(() => {
      setTimeLeft((t) => {
        if (t <= 1) {
          // Time out = defeat
          handleEnd(false);
          return 0;
        }
        return t - 1;
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [paused]);

  // Spawn next wave
  useEffect(() => {
    if (enemies.current.filter((e) => e.alive).length === 0 && waveIndex < mission.waves.length) {
      const timer = setTimeout(() => {
        const wave = mission.waves[waveIndex];
        if (!wave) return;
        const newEnemies: EnemyInstance[] = [];
        for (const spawn of wave.spawns) {
          const enemyData = ENEMIES.find((e) => e.id === spawn.enemyId);
          if (!enemyData) continue;
          for (let i = 0; i < spawn.count; i++) {
            const angle = Math.random() * Math.PI * 2;
            const dist = 12 + Math.random() * 6;
            newEnemies.push({
              id: enemyIdCounter.current++,
              enemy: enemyData,
              position: new THREE.Vector3(Math.cos(angle) * dist, 0, Math.sin(angle) * dist - 8),
              health: enemyData.baseHealth * diffMult,
              maxHealth: enemyData.baseHealth * diffMult,
              alive: true,
              lastShot: 0,
              hitFlash: 0,
            });
          }
        }
        enemies.current.push(...newEnemies);
        setEnemyVersion(v => v + 1);
        setWaveIndex(w => w + 1);
      }, 2000);
      return () => clearTimeout(timer);
    }
  }, [enemyVersion, waveIndex, mission, diffMult]);

  // Victory check
  useEffect(() => {
    if (
      enemies.current.filter((e) => e.alive).length === 0 &&
      waveIndex >= mission.waves.length
    ) {
      const timer = setTimeout(() => handleEnd(true), 1500);
      return () => clearTimeout(timer);
    }
  }, [enemyVersion, waveIndex, mission]);

  // Death check
  useEffect(() => {
    if (health <= 0) {
      handleEnd(false);
    }
  }, [health]);

  const handleEnd = useCallback((victory: boolean) => {
    const objectivesCompleted = victory ? mission.objectives.length : Math.floor(mission.objectives.length / 2);
    endMission({
      victory,
      missionId: mission.id,
      objectivesCompleted,
      objectivesTotal: mission.objectives.length,
      enemiesKilled,
      timeElapsed: mission.timeLimit - timeLeft,
      damageDealt,
      damageTaken,
      accuracy: shotsFired > 0 ? (shotsHit / shotsFired) * 100 : 0,
      rewards: {
        xp: victory ? mission.rewards.xp : Math.floor(mission.rewards.xp * 0.2),
        cr: victory ? mission.rewards.cr : Math.floor(mission.rewards.cr * 0.1),
      },
      bonuses: victory
        ? [
            { label: t(language, "summary.bonusNoDeath"), cr: 500 },
            { label: t(language, "summary.combatPerformance"), cr: 1800 },
            { label: t(language, "summary.techScraps"), cr: 45 },
          ]
        : [],
    });
  }, [mission, enemiesKilled, timeLeft, damageDealt, damageTaken, shotsFired, shotsHit, language, endMission]);

  // Handlers
  const onFire = useCallback(() => {
    setAmmo((a) => Math.max(0, a - 1));
    setShotsFired((s) => s + 1);
  }, []);

  const onHit = useCallback((enemyId: number, damage: number, isHead: boolean) => {
    setShotsHit((s) => s + 1);
    setDamageDealt((d) => d + damage);
    setUltimate((u) => Math.min(1000, u + 5));
    // Hit marker
    setHitMarker({ visible: true, crit: isHead });
    setTimeout(() => setHitMarker({ visible: false, crit: false }), 150);
    // Apply damage
    const e = enemies.current.find((en) => en.id === enemyId);
    if (e) {
      e.health -= damage;
      e.hitFlash = 1;
      if (e.health <= 0) {
        e.alive = false;
        setEnemiesKilled((k) => k + 1);
        setUltimate((u) => Math.min(1000, u + 50));
        setEnemyVersion((v) => v + 1);
      }
    }
  }, []);

  const onPlayerHit = useCallback((damage: number) => {
    setDamageTaken((d) => d + damage);
    setUltimate((u) => Math.min(1000, u + 2));
    // Damage direction (random for now)
    setDamageDirection({ angle: Math.random() * 360, visible: true });
    setTimeout(() => setDamageDirection(null), 800);
    // Apply to armor first, then health
    setArmor((a) => {
      if (a > 0) {
        const absorbed = Math.min(a, damage * 0.6);
        const remaining = damage - absorbed;
        if (remaining > 0) {
          setHealth((h) => Math.max(0, h - remaining));
        }
        return a - absorbed;
      }
      setHealth((h) => Math.max(0, h - damage));
      return 0;
    });
  }, []);

  // Auto-reload when empty
  useEffect(() => {
    if (ammo === 0 && reserveAmmo > 0) {
      const w = getWeapon(useGameStore.getState().equippedPrimaryId);
      const timer = setTimeout(() => {
        const need = (w?.magazineSize ?? 30) - ammo;
        const take = Math.min(need, reserveAmmo);
        setAmmo(ammo + take);
        setReserveAmmo(reserveAmmo - take);
      }, 1500);
      return () => clearTimeout(timer);
    }
  }, [ammo, reserveAmmo]);

  const enemiesRemaining = enemies.current.filter((e) => e.alive).length;

  const hudState: HUDState = {
    health,
    maxHealth: agent.baseHealth,
    armor,
    maxArmor: agent.baseShield,
    ammo,
    reserveAmmo,
    weaponName,
    timeLeft,
    enemiesRemaining,
    waveIndex,
    totalWaves: mission.waves.length,
    ultimate,
    objective: mission.objectives[0]?.description ?? mission.displayName,
    hitMarker,
    damageDirection,
  };

  return (
    <div className="fixed inset-0 bg-black overflow-hidden">
      {/* Canvas 3D */}
      <Canvas
        shadows={false}
        dpr={[1, 1.5]}
        gl={{ antialias: false, powerPreference: "high-performance", preserveDrawingBuffer: true }}
        camera={{ fov: 75, near: 0.1, far: 200, position: [0, 1.6, 8] }}
        frameloop="always"
      >
        <color attach="background" args={["#05060F"]} />
        <fog attach="fog" args={["#05060F", 25, 60]} />
        <ambientLight intensity={0.6} color="#1A4A6E" />
        <pointLight position={[0, 3.5, 0]} intensity={2.0} color="#1AA1CE" distance={40} />
        <pointLight position={[-10, 3, -10]} intensity={1.0} color="#6CF42E" distance={25} />
        <pointLight position={[10, 3, 10]} intensity={1.0} color="#A855F7" distance={25} />
        <pointLight position={[0, 3.5, -15]} intensity={1.5} color="#1AA1CE" distance={30} />
        <hemisphereLight args={["#1AA1CE", "#05060F", 0.4]} />
        <Stars radius={50} depth={20} count={500} factor={2} fade speed={0.5} />

        <ShipEnvironment />

        {enemies.current.map((e) => (
          <EnemyEntity key={e.id} instance={e} playerPos={playerPos} />
        ))}

        <Player
          gameRefs={gameRefs}
          enemies={enemies}
          onFire={onFire}
          onHit={onHit}
          onPlayerHit={onPlayerHit}
          playerPos={playerPos}
        />
      </Canvas>

      {/* HUD overlay */}
      <HUDScreen hudState={hudState} onPause={() => setPaused(true)} />

      {/* Touch controls */}
      <TouchControls gameRefs={gameRefs} />

      {/* Pause menu */}
      {paused && (
        <div className="absolute inset-0 z-50 bg-k5-deep-space/90 backdrop-blur-sm flex items-center justify-center">
          <div className="bg-k5-panel border-2 border-k5-cyan p-6 rounded-sm k5-clip k5-glow-cyan w-80">
            <h2 className="font-display text-2xl text-white k5-text-glow-cyan text-center mb-4">PAUSE</h2>
            <div className="flex flex-col gap-2">
              <KButton variant="primary" size="lg" onClick={() => setPaused(false)}>
                {t(language, "result.continue")}
              </KButton>
              <KButton variant="secondary" size="md" onClick={() => setPaused(false)}>
                {t(language, "result.settings")}
              </KButton>
              <KButton variant="danger" size="md" onClick={onExit}>
                {t(language, "common.back")}
              </KButton>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
