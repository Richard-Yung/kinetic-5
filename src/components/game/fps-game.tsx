"use client";

/* eslint-disable react-hooks/immutability -- refs mutables pour game loop */

/**
 * KINETICS 5 — Niveau FPS 3D RECONSTRUIT (React Three Fiber)
 *
 * Reconstruit suite aux retours utilisateur :
 * - Contrôles visibles TOUJOURS (joystick + boutons) sur desktop ET mobile
 * - Déplacement clavier (WASD) + souris (drag look) + touch
 * - Tir fonctionnel (clic gauche / bouton FIRE)
 * - Switch arme (1/2/3 + bouton SWITCH)
 * - Reload (R + bouton RELOAD)
 * - Grenade (G + bouton GRENADE)
 * - Environnement multi-zones navigable (corridor + salles + portes)
 * - Éclairage fort, ennemis visibles avec glow
 * - IA ennemie : poursuite, tir, mort, respawn par vagues
 * - HUD complet (PDF page 6)
 */

import { Canvas, useFrame, useThree } from "@react-three/fiber";
import { useRef, useState, useMemo, useEffect, useCallback } from "react";
import * as THREE from "three";
import { useGameStore } from "@/store/game-store";
import {
  AGENTS,
  ENEMIES,
  MISSIONS,
  getWeapon,
  type Enemy,
} from "@/lib/kinetics-data";
import { HUDScreen, type HUDState } from "@/components/screens/hud-screen";
import { TouchControls } from "@/components/game/touch-controls";
import { KButton } from "@/components/kinetics/ui";
import { t } from "@/lib/i18n";

/* ============================================================
   Types
   ============================================================ */
interface EnemyInstance {
  id: number;
  enemy: Enemy;
  position: THREE.Vector3;
  velocity: THREE.Vector3;
  health: number;
  maxHealth: number;
  alive: boolean;
  lastShot: number;
  hitFlash: number;
  attackCooldown: number;
  spawnTime: number;
}

interface GameRefs {
  velocity: THREE.Vector3;
  move: { x: number; y: number }; // joystick or WASD normalized
  shoot: boolean;
  aim: boolean;
  jump: boolean;
  sprint: boolean;
  reload: boolean;
  grenade: boolean;
  switchWeapon: number; // 0=none, 1=primary, 2=secondary, 3=tactical
  yaw: number;
  pitch: number;
  lookDelta: { x: number; y: number };
  isDragging: boolean;
}

/* ============================================================
   ShipEnvironment — vaisseau multi-zones navigable
   ============================================================ */
/* ============================================================
   Géométrie du niveau — définie au niveau module pour collision + rendu
   Architecture de forteresse : hub central + 4 ailes + piliers
   ============================================================ */
export interface Box { min: [number, number, number]; max: [number, number, number]; color: string; mat: "steel" | "concrete" | "panel" | "metal"; }

// Murs solides (collision + rendu). Coordonnées monde.
const LEVEL_WALLS: Box[] = [
  // === Hub central (pièce 12x12, origine) — 4 murs avec portes centrales ===
  // Nord (z=-6), porte x=[-1.5,1.5]
  { min: [-6, 0, -6.2], max: [-1.5, 4, -5.9], color: "#4a5560", mat: "steel" },
  { min: [1.5, 0, -6.2], max: [6, 4, -5.9], color: "#4a5560", mat: "steel" },
  // Sud (z=6)
  { min: [-6, 0, 5.9], max: [-1.5, 4, 6.2], color: "#4a5560", mat: "steel" },
  { min: [1.5, 0, 5.9], max: [6, 4, 6.2], color: "#4a5560", mat: "steel" },
  // Est (x=6)
  { min: [5.9, 0, -6], max: [6.2, 4, -1.5], color: "#4a5560", mat: "steel" },
  { min: [5.9, 0, 1.5], max: [6.2, 4, 6], color: "#4a5560", mat: "steel" },
  // Ouest (x=-6)
  { min: [-6.2, 0, -6], max: [-5.9, 4, -1.5], color: "#4a5560", mat: "steel" },
  { min: [-6.2, 0, 1.5], max: [-5.9, 4, 6], color: "#4a5560", mat: "steel" },

  // === Couloir Nord (z=-6 à z=-12, x=[-2,2]) ===
  { min: [-2.2, 0, -12], max: [-1.9, 4, -6], color: "#3a4250", mat: "metal" },
  { min: [1.9, 0, -12], max: [2.2, 4, -6], color: "#3a4250", mat: "metal" },
  // === Couloir Sud ===
  { min: [-2.2, 0, 6], max: [-1.9, 4, 12], color: "#3a4250", mat: "metal" },
  { min: [1.9, 0, 6], max: [2.2, 4, 12], color: "#3a4250", mat: "metal" },
  // === Couloir Est ===
  { min: [6, 0, -2.2], max: [12, 4, -1.9], color: "#3a4250", mat: "metal" },
  { min: [6, 0, 1.9], max: [12, 4, 2.2], color: "#3a4250", mat: "metal" },
  // === Couloir Ouest ===
  { min: [-12, 0, -2.2], max: [-6, 4, -1.9], color: "#3a4250", mat: "metal" },
  { min: [-12, 0, 1.9], max: [-6, 4, 2.2], color: "#3a4250", mat: "metal" },

  // === Salle Nord (Extraction, z=-12 à z=-18) ===
  { min: [-5, 0, -18.2], max: [5, 4, -17.9], color: "#2d5a4a", mat: "panel" }, // fond
  { min: [-5.2, 0, -18], max: [-4.9, 4, -12], color: "#3a5050", mat: "panel" }, // gauche
  { min: [4.9, 0, -18], max: [5.2, 4, -12], color: "#3a5050", mat: "panel" }, // droite
  // === Salle Sud (Armurerie, z=12 à z=18) ===
  { min: [-5, 0, 17.9], max: [5, 4, 18.2], color: "#5a4a2a", mat: "panel" },
  { min: [-5.2, 0, 12], max: [-4.9, 4, 18], color: "#504030", mat: "panel" },
  { min: [4.9, 0, 12], max: [5.2, 4, 18], color: "#504030", mat: "panel" },
  // === Salle Est (Machine, x=12 à x=18) ===
  { min: [17.9, 0, -4], max: [18.2, 4, 4], color: "#5a3a2a", mat: "panel" },
  { min: [12, 0, -4.2], max: [18, 4, -3.9], color: "#503530", mat: "panel" },
  { min: [12, 0, 3.9], max: [18, 4, 4.2], color: "#503530", mat: "panel" },
  // === Salle Ouest (Médical, x=-18 à x=-12) ===
  { min: [-18.2, 0, -4], max: [-17.9, 4, 4], color: "#3a4a5a", mat: "panel" },
  { min: [-18, 0, -4.2], max: [-12, 4, -3.9], color: "#304050", mat: "panel" },
  { min: [-18, 0, 3.9], max: [-12, 4, 4.2], color: "#304050", mat: "panel" },
];

// Piliers porteurs (collision + rendu)
const LEVEL_PILLARS: Box[] = [
  // Coins du hub
  { min: [-6.4, 0, -6.4], max: [-5.6, 4, -5.6], color: "#5a5a55", mat: "concrete" },
  { min: [5.6, 0, -6.4], max: [6.4, 4, -5.6], color: "#5a5a55", mat: "concrete" },
  { min: [-6.4, 0, 5.6], max: [-5.6, 4, 6.4], color: "#5a5a55", mat: "concrete" },
  { min: [5.6, 0, 5.6], max: [6.4, 4, 6.4], color: "#5a5a55", mat: "concrete" },
  // Coins couloirs (entrée salles)
  { min: [-2.4, 0, -12.4], max: [-1.6, 4, -11.6], color: "#4a4a45", mat: "concrete" },
  { min: [1.6, 0, -12.4], max: [2.4, 4, -11.6], color: "#4a4a45", mat: "concrete" },
  { min: [-2.4, 0, 11.6], max: [-1.6, 4, 12.4], color: "#4a4a45", mat: "concrete" },
  { min: [1.6, 0, 11.6], max: [2.4, 4, 12.4], color: "#4a4a45", mat: "concrete" },
];

// Toutes les boîtes solides pour la collision
const ALL_SOLID: Box[] = [...LEVEL_WALLS, ...LEVEL_PILLARS];

/* Test de collision cercle-AABB : corrige la position si collision */
function resolveCollision(px: number, pz: number, radius: number): [number, number] {
  let x = px, z = pz;
  for (const b of ALL_SOLID) {
    // Trouve le point le plus proche de (x,z) sur la boîte
    const cx = Math.max(b.min[0], Math.min(x, b.max[0]));
    const cz = Math.max(b.min[2], Math.min(z, b.max[2]));
    const dx = x - cx;
    const dz = z - cz;
    const distSq = dx * dx + dz * dz;
    if (distSq < radius * radius) {
      const dist = Math.sqrt(distSq);
      if (dist > 0.001) {
        // Pousser hors de la boîte
        const push = radius - dist;
        x += (dx / dist) * push;
        z += (dz / dist) * push;
      } else {
        // Centre dans la boîte — pousser en X
        x += radius;
      }
    }
  }
  return [x, z];
}

/* ============================================================
   ShipEnvironment — rendu de la forteresse (matériaux riches)
   ============================================================ */
function ShipEnvironment() {
  const matProps: Record<string, { metalness: number; roughness: number }> = {
    steel: { metalness: 0.85, roughness: 0.3 },
    concrete: { metalness: 0.1, roughness: 0.9 },
    panel: { metalness: 0.4, roughness: 0.6 },
    metal: { metalness: 0.7, roughness: 0.5 },
  };

  return (
    <group>
      {/* === SOL : plaques métalliques par zone avec couleurs distinctes === */}
      {/* Hub central — plaques d'acier */}
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0, 0]} receiveShadow>
        <planeGeometry args={[12, 12]} />
        <meshStandardMaterial color="#3a4250" metalness={0.7} roughness={0.4} />
      </mesh>
      {/* Couloirs — grissage sombre */}
      {[
        { pos: [0, 0, -9] as [number, number, number], size: [4, 6] },
        { pos: [0, 0, 9] as [number, number, number], size: [4, 6] },
        { pos: [9, 0, 0] as [number, number, number], size: [6, 4] },
        { pos: [-9, 0, 0] as [number, number, number], size: [6, 4] },
      ].map((f, i) => (
        <mesh key={`corr-floor-${i}`} rotation={[-Math.PI / 2, 0, 0]} position={f.pos}>
          <planeGeometry args={f.size} />
          <meshStandardMaterial color="#2a3040" metalness={0.6} roughness={0.6} />
        </mesh>
      ))}
      {/* Salles — sols colorés par fonction */}
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0, -15]}><planeGeometry args={[10, 6]} /><meshStandardMaterial color="#2d4a3a" metalness={0.4} roughness={0.7} /></mesh>
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0, 15]}><planeGeometry args={[10, 6]} /><meshStandardMaterial color="#4a3a2a" metalness={0.4} roughness={0.7} /></mesh>
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[15, 0, 0]}><planeGeometry args={[6, 8]} /><meshStandardMaterial color="#4a2a2a" metalness={0.4} roughness={0.7} /></mesh>
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[-15, 0, 0]}><planeGeometry args={[6, 8]} /><meshStandardMaterial color="#2a3a4a" metalness={0.4} roughness={0.7} /></mesh>

      {/* Lignes de sol cyan au hub */}
      {[-4, 0, 4].map((v, i) => (
        <group key={`grid-${i}`}>
          <mesh position={[0, 0.02, v]} rotation={[-Math.PI / 2, 0, 0]}><planeGeometry args={[12, 0.08]} /><meshBasicMaterial color="#4fc8e8" toneMapped={false} opacity={0.5} transparent /></mesh>
          <mesh position={[v, 0.02, 0]} rotation={[-Math.PI / 2, 0, Math.PI / 2]}><planeGeometry args={[12, 0.08]} /><meshBasicMaterial color="#4fc8e8" toneMapped={false} opacity={0.5} transparent /></mesh>
        </group>
      ))}

      {/* Emblème central au sol */}
      <mesh position={[0, 0.03, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <ringGeometry args={[1.5, 1.8, 8]} />
        <meshBasicMaterial color="#1AA1CE" toneMapped={false} opacity={0.6} transparent />
      </mesh>

      {/* === PLAFOND avec poutres === */}
      <mesh rotation={[Math.PI / 2, 0, 0]} position={[0, 4, 0]}><planeGeometry args={[12, 12]} /><meshStandardMaterial color="#2a2a30" metalness={0.3} roughness={0.8} /></mesh>
      {/* Poutres de plafond (structure) */}
      {[-4, 0, 4].map((v, i) => (
        <group key={`beam-${i}`}>
          <mesh position={[0, 3.9, v]}><boxGeometry args={[12, 0.2, 0.15]} /><meshStandardMaterial color="#3a3a40" metalness={0.5} roughness={0.6} /></mesh>
          <mesh position={[v, 3.9, 0]}><boxGeometry args={[0.15, 0.2, 12]} /><meshStandardMaterial color="#3a3a40" metalness={0.5} roughness={0.6} /></mesh>
        </group>
      ))}

      {/* === MURS avec matériaux riches === */}
      {LEVEL_WALLS.map((w, i) => {
        const sx = w.max[0] - w.min[0];
        const sy = w.max[1] - w.min[1];
        const sz = w.max[2] - w.min[2];
        const cx = (w.min[0] + w.max[0]) / 2;
        const cy = (w.min[1] + w.max[1]) / 2;
        const cz = (w.min[2] + w.max[2]) / 2;
        const mp = matProps[w.mat];
        return (
          <mesh key={`wall-${i}`} position={[cx, cy, cz]} castShadow receiveShadow>
            <boxGeometry args={[sx, sy, sz]} />
            <meshStandardMaterial color={w.color} metalness={mp.metalness} roughness={mp.roughness} />
          </mesh>
        );
      })}

      {/* === PILIERS porteurs (béton) avec capuchons acier === */}
      {LEVEL_PILLARS.map((p, i) => {
        const sx = p.max[0] - p.min[0];
        const sz = p.max[2] - p.min[2];
        const cx = (p.min[0] + p.max[0]) / 2;
        const cz = (p.min[2] + p.max[2]) / 2;
        const mp = matProps[p.mat];
        return (
          <group key={`pillar-${i}`} position={[cx, 2, cz]}>
            <mesh castShadow>
              <boxGeometry args={[sx, 4, sz]} />
              <meshStandardMaterial color={p.color} metalness={mp.metalness} roughness={mp.roughness} />
            </mesh>
            {/* Capuchon acier en haut */}
            <mesh position={[0, 2, 0]}>
              <boxGeometry args={[sx + 0.1, 0.15, sz + 0.1]} />
              <meshStandardMaterial color="#4a5560" metalness={0.8} roughness={0.3} />
            </mesh>
            {/* Bande lumineuse sur pilier */}
            <mesh position={[0, 1, sz / 2 + 0.01]}>
              <planeGeometry args={[sx * 0.6, 0.4]} />
              <meshBasicMaterial color="#4fc8e8" toneMapped={false} opacity={0.5} transparent />
            </mesh>
          </group>
        );
      })}

      {/* === BANDES LUMINEUSES murales (glow chaud/froid) === */}
      {[
        { pos: [0, 3.3, -5.85], size: [4, 0.1], color: "#4fc8e8" },
        { pos: [0, 3.3, 5.85], size: [4, 0.1], color: "#4fc8e8" },
        { pos: [5.85, 3.3, 0], size: [0.1, 4], color: "#f0a030" },
        { pos: [-5.85, 3.3, 0], size: [0.1, 4], color: "#f0a030" },
      ].map((g, i) => (
        <mesh key={`strip-${i}`} position={g.pos as [number, number, number]}>
          <boxGeometry args={g.size as [number, number, number]} />
          <meshBasicMaterial color={g.color} toneMapped={false} opacity={0.7} transparent />
        </mesh>
      ))}

      {/* === TUYAUX le long des murs (immersion industrielle) === */}
      {/* Tuyaux horizontaux cuivre — hub */}
      <mesh position={[-5.7, 2.5, 0]}><cylinderGeometry args={[0.08, 0.08, 10, 6]} rotation={[Math.PI / 2, 0, 0]} /><meshStandardMaterial color="#8a5a3a" metalness={0.7} roughness={0.4} /></mesh>
      <mesh position={[5.7, 2.5, 0]}><cylinderGeometry args={[0.08, 0.08, 10, 6]} rotation={[Math.PI / 2, 0, 0]} /><meshStandardMaterial color="#8a5a3a" metalness={0.7} roughness={0.4} /></mesh>
      <mesh position={[0, 2.5, -5.7]}><cylinderGeometry args={[0.08, 0.08, 10, 6]} rotation={[0, 0, Math.PI / 2]} /><meshStandardMaterial color="#6a7080" metalness={0.7} roughness={0.4} /></mesh>
      <mesh position={[0, 2.5, 5.7]}><cylinderGeometry args={[0.08, 0.08, 10, 6]} rotation={[0, 0, Math.PI / 2]} /><meshStandardMaterial color="#6a7080" metalness={0.7} roughness={0.4} /></mesh>

      {/* === PORTES encadrées (4 entrées du hub) === */}
      {[
        { pos: [0, 0, -6] as [number, number, number], rot: 0 },
        { pos: [0, 0, 6] as [number, number, number], rot: Math.PI },
        { pos: [6, 0, 0] as [number, number, number], rot: -Math.PI / 2 },
        { pos: [-6, 0, 0] as [number, number, number], rot: Math.PI / 2 },
      ].map((d, i) => (
        <group key={`door-${i}`} position={d.pos} rotation={[0, d.rot, 0]}>
          <mesh position={[0, 3.8, 0]}><boxGeometry args={[3.4, 0.2, 0.15]} /><meshStandardMaterial color="#3a3a40" metalness={0.7} roughness={0.4} /></mesh>
          <mesh position={[-1.7, 1.8, 0]}><boxGeometry args={[0.15, 3.8, 0.1]} /><meshBasicMaterial color="#4fc8e8" toneMapped={false} opacity={0.6} transparent /></mesh>
          <mesh position={[1.7, 1.8, 0]}><boxGeometry args={[0.15, 3.8, 0.1]} /><meshBasicMaterial color="#4fc8e8" toneMapped={false} opacity={0.6} transparent /></mesh>
        </group>
      ))}

      {/* === PROPS par salle (immersion) === */}
      {/* Salle Nord : extraction — panneau vert + caisses */}
      <mesh position={[0, 1.8, -17.9]}><planeGeometry args={[4, 3.2]} /><meshBasicMaterial color="#6CF42E" toneMapped={false} opacity={0.3} transparent /></mesh>
      <mesh position={[0, 2, -18.1]}><boxGeometry args={[4.2, 0.15, 0.1]} /><meshBasicMaterial color="#6CF42E" toneMapped={false} /></mesh>
      <mesh position={[-2, 2, -18.1]}><boxGeometry args={[0.15, 4, 0.1]} /><meshBasicMaterial color="#6CF42E" toneMapped={false} /></mesh>
      <mesh position={[2, 2, -18.1]}><boxGeometry args={[0.15, 4, 0.1]} /><meshBasicMaterial color="#6CF42E" toneMapped={false} /></mesh>

      {/* Salle Sud : armurerie — râteliers d'armes */}
      {[-3, 0, 3].map((x, i) => (
        <mesh key={`rack-${i}`} position={[x, 1, 17.8]}><boxGeometry args={[1.5, 2, 0.3]} /><meshStandardMaterial color="#3a3a30" metalness={0.6} roughness={0.5} /></mesh>
      ))}

      {/* Salle Est : machine — générateurs */}
      <mesh position={[17.8, 1, -2]}><boxGeometry args={[0.5, 2, 1.5]} /><meshStandardMaterial color="#5a3a2a" metalness={0.5} roughness={0.6} /></mesh>
      <mesh position={[17.8, 1, 2]}><boxGeometry args={[0.5, 2, 1.5]} /><meshStandardMaterial color="#5a3a2a" metalness={0.5} roughness={0.6} /></mesh>
      <mesh position={[17.8, 2.2, 0]}><boxGeometry args={[0.3, 0.4, 0.3]} /><meshBasicMaterial color="#f0a030" toneMapped={false} /></mesh>

      {/* Salle Ouest : médical — lits/stations */}
      <mesh position={[-17.8, 0.5, -2]}><boxGeometry args={[0.5, 1, 1.5]} /><meshStandardMaterial color="#3a5060" metalness={0.4} roughness={0.6} /></mesh>
      <mesh position={[-17.8, 0.5, 2]}><boxGeometry args={[0.5, 1, 1.5]} /><meshStandardMaterial color="#3a5060" metalness={0.4} roughness={0.6} /></mesh>
      <mesh position={[-17.8, 1.2, 0]}><boxGeometry args={[0.3, 0.3, 0.3]} /><meshBasicMaterial color="#6CF42E" toneMapped={false} /></mesh>

      {/* Caisses de couverture dans le hub */}
      {[
        { pos: [-4, 0.6, -4], size: [1.2, 1.2, 1.2] },
        { pos: [4, 0.6, 4], size: [1.2, 1.2, 1.2] },
        { pos: [-4, 0.6, 4], size: [1.2, 1.2, 1.2] },
        { pos: [4, 0.6, -4], size: [1.2, 1.2, 1.2] },
      ].map((c, i) => (
        <mesh key={`crate-${i}`} position={c.pos as [number, number, number]} castShadow>
          <boxGeometry args={c.size as [number, number, number]} />
          <meshStandardMaterial color="#3a4550" metalness={0.5} roughness={0.7} />
        </mesh>
      ))}

      {/* Écrans muraux colorés (ambiance) */}
      {[
        { pos: [-5.6, 2.2, -3], rot: [0, Math.PI / 2, 0], color: "#1AA1CE" },
        { pos: [5.6, 2.2, 3], rot: [0, -Math.PI / 2, 0], color: "#A855F7" },
        { pos: [3, 2.2, -5.6], rot: [0, 0, 0], color: "#FFE735" },
        { pos: [-3, 2.2, 5.6], rot: [0, Math.PI, 0], color: "#6CF42E" },
      ].map((s, i) => (
        <mesh key={`screen-${i}`} position={s.pos as [number, number, number]} rotation={s.rot as [number, number, number]}>
          <planeGeometry args={[1.2, 0.8]} />
          <meshBasicMaterial color={s.color} toneMapped={false} opacity={0.35} transparent />
        </mesh>
      ))}
    </group>
  );
}

/* ============================================================
   EnemyEntity — ennemi 3D visible avec glow
   ============================================================ */
function EnemyEntity({
  instance,
  playerPos,
  onShootPlayer,
}: {
  instance: EnemyInstance;
  playerPos: React.MutableRefObject<THREE.Vector3>;
  onShootPlayer: (damage: number) => void;
}) {
  const ref = useRef<THREE.Group>(null);
  const [flash, setFlash] = useState(0);
  const bodyRef = useRef<THREE.Mesh>(null);

  useFrame((_, delta) => {
    if (!ref.current || !instance.alive) return;
    const playerP = playerPos.current;
    const dir = new THREE.Vector3().subVectors(playerP, instance.position);
    dir.y = 0;
    const dist = dir.length();
    dir.normalize();

    // Mouvement vers le joueur (à portée moyenne, pas trop près)
    const desiredDist = instance.enemy.attackRange * 0.5;
    const speed = instance.enemy.moveSpeed * 0.7;
    if (dist > desiredDist) {
      instance.position.x += dir.x * speed * delta;
      instance.position.z += dir.z * speed * delta;
    } else if (dist < desiredDist * 0.6) {
      // Recule un peu si trop près
      instance.position.x -= dir.x * speed * delta * 0.5;
      instance.position.z -= dir.z * speed * delta * 0.5;
    }

    // Strafe latéral occasionnel (plus vivant)
    if (Math.random() < 0.01) {
      const strafe = new THREE.Vector3(-dir.z, 0, dir.x);
      instance.velocity.x = strafe.x * speed * 0.5;
      instance.velocity.z = strafe.z * speed * 0.5;
    }
    instance.position.x += instance.velocity.x * delta;
    instance.position.z += instance.velocity.z * delta;
    instance.velocity.x *= 0.9;
    instance.velocity.z *= 0.9;

    // Limites
    instance.position.x = Math.max(-19, Math.min(19, instance.position.x));
    instance.position.z = Math.max(-19, Math.min(19, instance.position.z));
    // Collision ennemi-murs
    const [ex, ez] = resolveCollision(instance.position.x, instance.position.z, 0.35);
    instance.position.x = ex;
    instance.position.z = ez;

    ref.current.position.copy(instance.position);
    ref.current.lookAt(playerP.x, instance.position.y, playerP.z);

    // Flash dégât
    if (instance.hitFlash > 0) {
      instance.hitFlash -= delta * 4;
      setFlash(instance.hitFlash > 0 ? instance.hitFlash : 0);
    }

    // Bobbing vertical (marche)
    if (bodyRef.current) {
      bodyRef.current.position.y = 1 + Math.sin(performance.now() / 200 + instance.id) * 0.05;
    }

    // Tir sur le joueur
    instance.attackCooldown -= delta;
    if (dist < instance.enemy.attackRange && instance.attackCooldown <= 0) {
      instance.attackCooldown = 1 / instance.enemy.attackRate;
      const hitChance = 0.3 + (1 - dist / instance.enemy.attackRange) * 0.4;
      if (Math.random() < hitChance) {
        onShootPlayer(instance.enemy.baseDamage * 0.4);
      }
    }
  });

  if (!instance.alive) return null;

  const isBoss = instance.enemy.enemyClass === "Boss";
  const color = isBoss ? "#8a0000" : instance.enemy.enemyClass === "Heavy" ? "#5a3a2a" : "#7a4a3a";
  const emissiveColor = flash > 0 ? "#FFFFFF" : (isBoss ? "#FE0022" : "#1AA1CE");
  const scale = isBoss ? 2.2 : instance.enemy.enemyClass === "Heavy" ? 1.5 : 1;

  return (
    <group ref={ref} scale={scale} position={instance.position}>
      {/* Halo au sol */}
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.03, 0]}>
        <ringGeometry args={[0.6, 0.8, 16]} />
        <meshBasicMaterial color={isBoss ? "#FE0022" : "#1AA1CE"} toneMapped={false} transparent opacity={0.4} />
      </mesh>

      {/* Corps */}
      <mesh ref={bodyRef} position={[0, 1, 0]} castShadow>
        <capsuleGeometry args={[0.4, 0.9, 4, 8]} />
        <meshStandardMaterial
          color={color}
          emissive={emissiveColor}
          emissiveIntensity={flash > 0 ? flash * 3 : (isBoss ? 0.4 : 0.2)}
          metalness={0.4}
          roughness={0.6}
        />
      </mesh>
      {/* Tête */}
      <mesh position={[0, 1.75, 0]} castShadow>
        <sphereGeometry args={[0.28, 10, 10]} />
        <meshStandardMaterial color={color} emissive={emissiveColor} emissiveIntensity={flash * 3 || (isBoss ? 0.5 : 0.2)} />
      </mesh>
      {/* Visière lumineuse (toujours visible) */}
      <mesh position={[0, 1.78, 0.22]}>
        <boxGeometry args={[0.35, 0.12, 0.06]} />
        <meshBasicMaterial color={isBoss ? "#FE0022" : "#1AA1CE"} toneMapped={false} />
      </mesh>
      {/* Bras */}
      <mesh position={[-0.5, 1, 0]} castShadow>
        <capsuleGeometry args={[0.12, 0.65, 4, 6]} />
        <meshStandardMaterial color={color} emissive={emissiveColor} emissiveIntensity={flash * 2 || 0.1} />
      </mesh>
      <mesh position={[0.5, 1, 0]} castShadow>
        <capsuleGeometry args={[0.12, 0.65, 4, 6]} />
        <meshStandardMaterial color={color} emissive={emissiveColor} emissiveIntensity={flash * 2 || 0.1} />
      </mesh>
      {/* Jambes */}
      <mesh position={[-0.22, 0.3, 0]} castShadow>
        <capsuleGeometry args={[0.14, 0.55, 4, 6]} />
        <meshStandardMaterial color={color} emissive={emissiveColor} emissiveIntensity={flash * 2 || 0.1} />
      </mesh>
      <mesh position={[0.22, 0.3, 0]} castShadow>
        <capsuleGeometry args={[0.14, 0.55, 4, 6]} />
        <meshStandardMaterial color={color} emissive={emissiveColor} emissiveIntensity={flash * 2 || 0.1} />
      </mesh>
      {/* Arme (si non drone) */}
      {instance.enemy.enemyClass !== "Drone" && (
        <mesh position={[0.4, 0.9, 0.3]} rotation={[0, 0, -0.3]}>
          <boxGeometry args={[0.08, 0.08, 0.6]} />
          <meshStandardMaterial color="#1a1a1a" metalness={0.8} roughness={0.3} />
        </mesh>
      )}

      {/* Barre de vie au-dessus */}
      <group position={[0, 2.6, 0]}>
        <mesh>
          <planeGeometry args={[1.2, 0.12]} />
          <meshBasicMaterial color="#0a0a0a" toneMapped={false} />
        </mesh>
        <mesh position={[-(1.2 * (1 - instance.health / instance.maxHealth)) / 2, 0, 0.01]}>
          <planeGeometry args={[1.2 * (instance.health / instance.maxHealth), 0.1]} />
          <meshBasicMaterial
            color={instance.health / instance.maxHealth > 0.5 ? "#6CF42E" : instance.health / instance.maxHealth > 0.25 ? "#FFE735" : "#FE0022"}
            toneMapped={false}
          />
        </mesh>
      </group>
    </group>
  );
}

/* ============================================================
   Tracer — ligne de tir visible (impact visuel)
   ============================================================ */
function Tracer({ from, to, color = "#FFE735", onDone }: { from: THREE.Vector3; to: THREE.Vector3; color?: string; onDone: () => void }) {
  const ref = useRef<THREE.Mesh>(null);
  useEffect(() => {
    const timer = setTimeout(onDone, 80);
    return () => clearTimeout(timer);
  }, [onDone]);
  const mid = useMemo(() => new THREE.Vector3().lerpVectors(from, to, 0.5), [from, to]);
  const len = useMemo(() => from.distanceTo(to), [from, to]);
  const quat = useMemo(() => {
    const dir = new THREE.Vector3().subVectors(to, from).normalize();
    return new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
  }, [from, to]);
  return (
    <mesh ref={ref} position={mid} quaternion={quat}>
      <cylinderGeometry args={[0.02, 0.02, len, 4]} />
      <meshBasicMaterial color={color} toneMapped={false} transparent opacity={0.8} />
    </mesh>
  );
}

/* ============================================================
   WeaponViewmodel — arme first-person
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
  const isPistol = weapon?.category === "Secondary";

  useFrame((_, delta) => {
    if (!ref.current) return;
    const targetX = aim ? 0 : 0.25;
    const targetY = aim ? -0.18 : -0.28;
    const targetZ = aim ? -0.45 : -0.55;
    ref.current.position.x += (targetX - ref.current.position.x) * delta * 10;
    ref.current.position.y += (targetY - ref.current.position.y) * delta * 10;
    ref.current.position.z += (targetZ - ref.current.position.z) * delta * 10;
    if (shooting) {
      ref.current.position.z += 0.06;
      ref.current.rotation.x = -0.06;
    } else {
      ref.current.rotation.x += (0 - ref.current.rotation.x) * delta * 8;
    }
    if (reloading) {
      ref.current.rotation.z = Math.sin(performance.now() / 80) * 0.4;
    } else {
      ref.current.rotation.z += (0 - ref.current.rotation.z) * delta * 8;
    }
  });

  return (
    <group ref={ref} position={[0.28, -0.30, -0.60]}>
      {/* Corps principal de l'arme — détaillé */}
      <mesh>
        <boxGeometry args={[0.09, 0.13, isPistol ? 0.32 : 0.58]} />
        <meshStandardMaterial color="#2a3a4e" metalness={0.85} roughness={0.25} />
      </mesh>
      {/* Canon */}
      <mesh position={[0, 0.03, isPistol ? -0.22 : -0.35]}>
        <boxGeometry args={[0.05, 0.05, 0.22]} />
        <meshStandardMaterial color="#0a0a0a" metalness={0.9} roughness={0.15} />
      </mesh>
      {/* Bout du canon (orange/rouge) */}
      <mesh position={[0, 0.03, isPistol ? -0.34 : -0.48]}>
        <cylinderGeometry args={[0.03, 0.03, 0.06, 8]} rotation={[Math.PI / 2, 0, 0]} />
        <meshStandardMaterial color="#1a1a1a" metalness={0.9} roughness={0.2} />
      </mesh>
      {/* Poignée */}
      <mesh position={[0, -0.14, 0.12]} rotation={[0.35, 0, 0]}>
        <boxGeometry args={[0.07, 0.16, 0.09]} />
        <meshStandardMaterial color="#1a2a3e" metalness={0.7} roughness={0.4} />
      </mesh>
      {/* Chargeur */}
      <mesh position={[0, -0.12, -0.05]}>
        <boxGeometry args={[0.06, 0.12, 0.14]} />
        <meshStandardMaterial color="#1a1a1a" metalness={0.6} roughness={0.5} />
      </mesh>
      {/* Viseur (scope) */}
      <mesh position={[0, 0.12, 0.05]}>
        <boxGeometry args={[0.05, 0.06, 0.15]} />
        <meshStandardMaterial color="#1a2a3e" metalness={0.8} roughness={0.3} />
      </mesh>
      {/* Lente du scope (glow cyan) */}
      <mesh position={[0, 0.12, -0.04]}>
        <circleGeometry args={[0.025, 8]} />
        <meshBasicMaterial color="#00CED1" toneMapped={false} />
      </mesh>
      {/* Bande lumineuse supérieure (glow cyan) */}
      <mesh position={[0, 0.07, 0]}>
        <boxGeometry args={[0.03, 0.015, isPistol ? 0.25 : 0.45]} />
        <meshBasicMaterial color="#00CED1" toneMapped={false} />
      </mesh>
      {/* Bande lumineuse latérale (glow cyan) */}
      <mesh position={[0.05, 0, 0]}>
        <boxGeometry args={[0.015, 0.04, isPistol ? 0.2 : 0.38]} />
        <meshBasicMaterial color="#1AA1CE" toneMapped={false} opacity={0.8} transparent />
      </mesh>
      {/* Détail pontet */}
      <mesh position={[0, -0.05, 0.05]} rotation={[0.2, 0, 0]}>
        <torusGeometry args={[0.04, 0.012, 6, 12, Math.PI]} />
        <meshStandardMaterial color="#1a2a3e" metalness={0.7} roughness={0.4} />
      </mesh>
      {/* Indicateur élément (glow couleur) */}
      <mesh position={[0, -0.02, 0.08]}>
        <boxGeometry args={[0.03, 0.03, 0.03]} />
        <meshBasicMaterial color="#1AA1CE" toneMapped={false} />
      </mesh>
      {/* Muzzle flash */}
      {shooting && (
        <>
          <mesh position={[0, 0.03, isPistol ? -0.40 : -0.55]}>
            <sphereGeometry args={[0.12, 6, 6]} />
            <meshBasicMaterial color="#FFE735" toneMapped={false} transparent opacity={0.9} />
          </mesh>
          <mesh position={[0, 0.03, isPistol ? -0.45 : -0.60]}>
            <sphereGeometry args={[0.06, 6, 6]} />
            <meshBasicMaterial color="#FFFFFF" toneMapped={false} transparent opacity={0.8} />
          </mesh>
        </>
      )}
    </group>
  );
}

/* ============================================================
   Player — caméra FPS + contrôles
   ============================================================ */
function Player({
  gameRefs,
  enemies,
  onFire,
  onHit,
  onPlayerHit,
  playerPos,
  currentWeaponId,
}: {
  gameRefs: React.MutableRefObject<GameRefs>;
  enemies: React.MutableRefObject<EnemyInstance[]>;
  onFire: () => void;
  onHit: (enemyId: number, damage: number, isHead: boolean) => void;
  onPlayerHit: (damage: number) => void;
  playerPos: React.MutableRefObject<THREE.Vector3>;
  currentWeaponId: string;
}) {
  const { camera, gl } = useThree();
  const [shooting, setShooting] = useState(false);
  const [reloading, setReloading] = useState(false);
  const [aim, setAim] = useState(false);
  const lastFireRef = useRef(0);
  const raycaster = useMemo(() => new THREE.Raycaster(), []);
  const [tracers, setTracers] = useState<{ id: number; from: THREE.Vector3; to: THREE.Vector3 }[]>([]);
  const tracerId = useRef(0);

  useEffect(() => {
    camera.position.set(0, 1.6, 5);
    playerPos.current.set(0, 1.6, 5);
  }, [camera, playerPos]);

  // === Keyboard ===
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const r = gameRefs.current;
      switch (e.code) {
        case "KeyW": case "ArrowUp": r.move.y = -1; break;
        case "KeyS": case "ArrowDown": r.move.y = 1; break;
        case "KeyA": case "ArrowLeft": r.move.x = -1; break;
        case "KeyD": case "ArrowRight": r.move.x = 1; break;
        case "Space": r.jump = true; break;
        case "ShiftLeft": r.sprint = true; break;
        case "KeyR": doReload(); break;
        case "KeyE": setAim(a => !a); break;
        case "KeyG": r.grenade = true; setTimeout(() => r.grenade = false, 100); break;
        case "Digit1": r.switchWeapon = 1; break;
        case "Digit2": r.switchWeapon = 2; break;
        case "Digit3": r.switchWeapon = 3; break;
      }
    };
    const onKeyUp = (e: KeyboardEvent) => {
      const r = gameRefs.current;
      switch (e.code) {
        case "KeyW": case "ArrowUp": if (r.move.y < 0) r.move.y = 0; break;
        case "KeyS": case "ArrowDown": if (r.move.y > 0) r.move.y = 0; break;
        case "KeyA": case "ArrowLeft": if (r.move.x < 0) r.move.x = 0; break;
        case "KeyD": case "ArrowRight": if (r.move.x > 0) r.move.x = 0; break;
        case "Space": r.jump = false; break;
        case "ShiftLeft": r.sprint = false; break;
      }
    };
    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("keyup", onKeyUp);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("keyup", onKeyUp);
    };
  }, [gameRefs]);

  // === Mouse look (drag) + click to shoot ===
  useEffect(() => {
    const canvas = gl.domElement;
    const onMouseDown = (e: MouseEvent) => {
      if (e.button === 0) {
        gameRefs.current.shoot = true;
        gameRefs.current.isDragging = true;
      }
      if (e.button === 2) setAim(true);
    };
    const onMouseUp = (e: MouseEvent) => {
      if (e.button === 0) {
        gameRefs.current.shoot = false;
        gameRefs.current.isDragging = false;
      }
      if (e.button === 2) setAim(false);
    };
    const onMouseMove = (e: MouseEvent) => {
      if (gameRefs.current.isDragging || aim) {
        gameRefs.current.lookDelta.x += e.movementX;
        gameRefs.current.lookDelta.y += e.movementY;
      }
    };
    const onContextMenu = (e: Event) => e.preventDefault();
    canvas.addEventListener("mousedown", onMouseDown);
    window.addEventListener("mouseup", onMouseUp);
    window.addEventListener("mousemove", onMouseMove);
    canvas.addEventListener("contextmenu", onContextMenu);
    return () => {
      canvas.removeEventListener("mousedown", onMouseDown);
      window.removeEventListener("mouseup", onMouseUp);
      window.removeEventListener("mousemove", onMouseMove);
      canvas.removeEventListener("contextmenu", onContextMenu);
    };
  }, [gl, aim, gameRefs]);

  const doReload = useCallback(() => {
    setReloading(true);
    setTimeout(() => setReloading(false), 1800);
  }, []);

  useFrame((_, delta) => {
    const r = gameRefs.current;
    const agent = AGENTS.find(a => a.id === useGameStore.getState().selectedAgentId) ?? AGENTS[0];
    const speed = agent.baseSpeed * 5 * (r.sprint ? 1.6 : 1);

    // Look
    if (r.lookDelta.x !== 0 || r.lookDelta.y !== 0) {
      r.yaw -= r.lookDelta.x * 0.0025;
      r.pitch -= r.lookDelta.y * 0.0025;
      r.pitch = Math.max(-Math.PI / 2 + 0.1, Math.min(Math.PI / 2 - 0.1, r.pitch));
      r.lookDelta.x = 0;
      r.lookDelta.y = 0;
    }
    camera.rotation.order = "YXZ";
    camera.rotation.y = r.yaw;
    camera.rotation.x = r.pitch;

    // Mouvement
    const forward = new THREE.Vector3();
    const right = new THREE.Vector3();
    camera.getWorldDirection(forward);
    forward.y = 0;
    forward.normalize();
    right.crossVectors(forward, new THREE.Vector3(0, 1, 0)).normalize();

    const move = new THREE.Vector3();
    if (r.move.y < 0) move.add(forward);
    if (r.move.y > 0) move.sub(forward);
    if (r.move.x < 0) move.sub(right);
    if (r.move.x > 0) move.add(right);
    if (move.lengthSq() > 0) move.normalize().multiplyScalar(speed * delta);

    // Collision : limites du niveau + murs solides (AABB)
    const boundX = Math.max(-19, Math.min(19, camera.position.x + move.x));
    const boundZ = Math.max(-19, Math.min(19, camera.position.z + move.z));
    // Résolution collision cercle-AABB contre tous les murs/piliers (rayon 0.4)
    const [colX, colZ] = resolveCollision(boundX, boundZ, 0.4);
    camera.position.x = colX;
    camera.position.z = colZ;

    // Saut + gravité
    if (r.jump && camera.position.y <= 1.6) {
      r.velocity.y = 6;
    }
    r.velocity.y -= 18 * delta;
    camera.position.y += r.velocity.y * delta;
    if (camera.position.y < 1.6) {
      camera.position.y = 1.6;
      r.velocity.y = 0;
    }
    playerPos.current.copy(camera.position);

    // Tir
    const weapon = getWeapon(currentWeaponId);
    const fireRate = weapon ? 60 / (weapon.fireRate * 1.5 + 15) : 0.15;
    const now = performance.now() / 1000;
    if (r.shoot && !reloading && now - lastFireRef.current > fireRate) {
      lastFireRef.current = now;
      setShooting(true);
      setTimeout(() => setShooting(false), 50);
      onFire();

      // Raycast vers ennemis
      const dir = new THREE.Vector3();
      camera.getWorldDirection(dir);
      const origin = camera.position.clone();
      // Spread (aim réduit)
      const spread = aim ? 0.005 : 0.03;
      dir.x += (Math.random() - 0.5) * spread;
      dir.y += (Math.random() - 0.5) * spread;
      dir.normalize();
      raycaster.set(origin, dir);
      raycaster.far = weapon?.range ?? 80;

      let closestHit: { enemy: EnemyInstance; distance: number; isHead: boolean } | null = null;
      for (const e of enemies.current) {
        if (!e.alive) continue;
        const headPos = new THREE.Vector3(e.position.x, e.position.y + 1.78, e.position.z);
        const bodyPos = new THREE.Vector3(e.position.x, e.position.y + 1, e.position.z);
        const headDist = raycaster.ray.distanceToPoint(headPos);
        const bodyDist = raycaster.ray.distanceToPoint(bodyPos);
        const headAlong = raycaster.ray.direction.dot(headPos.clone().sub(raycaster.ray.origin));
        const bodyAlong = raycaster.ray.direction.dot(bodyPos.clone().sub(raycaster.ray.origin));
        const enemyScale = e.enemy.enemyClass === "Boss" ? 2.2 : e.enemy.enemyClass === "Heavy" ? 1.5 : 1;
        if (headAlong > 0 && headAlong < (weapon?.range ?? 80) && headDist < 0.4 * enemyScale) {
          if (!closestHit || headAlong < closestHit.distance) closestHit = { enemy: e, distance: headAlong, isHead: true };
        } else if (bodyAlong > 0 && bodyAlong < (weapon?.range ?? 80) && bodyDist < 0.7 * enemyScale) {
          if (!closestHit || bodyAlong < closestHit.distance) closestHit = { enemy: e, distance: bodyAlong, isHead: false };
        }
      }

      // Tracer visuel (vers le hit ou loin devant)
      const tracerEnd = closestHit
        ? origin.clone().add(dir.clone().multiplyScalar(closestHit.distance))
        : origin.clone().add(dir.clone().multiplyScalar(40));
      const tid = tracerId.current++;
      setTracers(prev => [...prev, { id: tid, from: origin.clone(), to: tracerEnd }]);
      setTimeout(() => setTracers(prev => prev.filter(t => t.id !== tid)), 80);

      if (closestHit) {
        const baseDmg = (weapon?.damage ?? 50) * 12;
        const dmg = closestHit.isHead ? baseDmg * 2.5 : baseDmg;
        onHit(closestHit.enemy.id, dmg, closestHit.isHead);
      }
    }
  });

  return (
    <>
      <WeaponViewmodel weaponId={currentWeaponId} shooting={shooting} reloading={reloading} aim={aim} />
      {tracers.map(t => (
        <Tracer key={t.id} from={t.from} to={t.to} onDone={() => setTracers(prev => prev.filter(x => x.id !== t.id))} />
      ))}
    </>
  );
}

/* ============================================================
   FPSGame — composant principal
   ============================================================ */
export function FPSGame({ onExit }: { onExit: () => void }) {
  const { endMission, currentMissionId, language, difficulty, selectedAgentId, equippedPrimaryId, equippedSecondaryId, equippedTacticalId } = useGameStore();
  const mission = MISSIONS.find((m) => m.id === currentMissionId) ?? MISSIONS[0];
  const agent = AGENTS.find((a) => a.id === selectedAgentId) ?? AGENTS[0];

  const playerPos = useRef(new THREE.Vector3(0, 1.6, 5));
  const gameRefs = useRef<GameRefs>({
    velocity: new THREE.Vector3(),
    move: { x: 0, y: 0 },
    shoot: false, aim: false, jump: false, sprint: false, reload: false, grenade: false,
    switchWeapon: 0,
    yaw: 0, pitch: 0,
    lookDelta: { x: 0, y: 0 },
    isDragging: false,
  });

  // Arme courante (1=primary, 2=secondary, 3=tactical)
  const [weaponSlot, setWeaponSlot] = useState<1 | 2 | 3>(1);
  const currentWeaponId = weaponSlot === 1 ? equippedPrimaryId : weaponSlot === 2 ? equippedSecondaryId : equippedTacticalId;
  const currentWeapon = getWeapon(currentWeaponId);

  // État de jeu
  const [health, setHealth] = useState(agent.baseHealth);
  const [armor, setArmor] = useState(agent.baseShield);
  const [ammo, setAmmo] = useState(currentWeapon?.magazineSize ?? 30);
  const [reserveAmmo, setReserveAmmo] = useState((currentWeapon?.magazineSize ?? 30) * 4);
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
  const [gameOver, setGameOver] = useState(false);

  const enemies = useRef<EnemyInstance[]>([]);
  const [, forceUpdate] = useState(0);

  // Rafraîchit le HUD toutes les 250ms pour la minimap (pas trop fréquent pour perf)
  useEffect(() => {
    const interval = setInterval(() => {
      forceUpdate(n => n + 1);
    }, 250);
    return () => clearInterval(interval);
  }, []);
  const enemyIdCounter = useRef(0);

  const diffMult = difficulty === "easy" ? 0.7 : difficulty === "hard" ? 1.4 : 1;

  // Reset ammo quand switch arme
  useEffect(() => {
    const w = getWeapon(currentWeaponId);
    setAmmo(w?.magazineSize ?? 30);
    setReserveAmmo((w?.magazineSize ?? 30) * 4);
  }, [weaponSlot, currentWeaponId]);

  // Switch weapon trigger
  useEffect(() => {
    const interval = setInterval(() => {
      const r = gameRefs.current;
      if (r.switchWeapon > 0) {
        setWeaponSlot(r.switchWeapon as 1 | 2 | 3);
        r.switchWeapon = 0;
      }
      if (r.grenade) {
        // TODO grenade throw
        r.grenade = false;
      }
    }, 50);
    return () => clearInterval(interval);
  }, []);

  // Spawn wave initiale
  useEffect(() => {
    const wave = mission.waves[0];
    if (!wave) return;
    const newEnemies: EnemyInstance[] = [];
    for (const spawn of wave.spawns) {
      const enemyData = ENEMIES.find((e) => e.id === spawn.enemyId);
      if (!enemyData) continue;
      for (let i = 0; i < spawn.count; i++) {
        // Spawn : moitié devant le joueur (visible), moitié dans zones secondaires
        const visible = i < Math.ceil(spawn.count / 2);
        let x, z;
        if (visible) {
          // Devant le joueur (qui regarde -Z par défaut), à distance moyenne
          const angle = (Math.random() - 0.5) * Math.PI * 0.6; // ±54°
          const dist = 8 + Math.random() * 6;
          x = Math.sin(angle) * dist;
          z = -Math.cos(angle) * dist; // devant
        } else {
          const zones = [
            { x: 0, z: -17 }, { x: 0, z: 17 }, { x: 17, z: 0 }, { x: -17, z: 0 },
          ];
          const zone = zones[Math.floor(Math.random() * zones.length)];
          const a = Math.random() * Math.PI * 2;
          const d = Math.random() * 4;
          x = zone.x + Math.cos(a) * d;
          z = zone.z + Math.sin(a) * d;
        }
        newEnemies.push({
          id: enemyIdCounter.current++,
          enemy: enemyData,
          position: new THREE.Vector3(x, 0, z),
          velocity: new THREE.Vector3(),
          health: enemyData.baseHealth * diffMult,
          maxHealth: enemyData.baseHealth * diffMult,
          alive: true,
          lastShot: 0,
          hitFlash: 0,
          attackCooldown: 2 + Math.random() * 2,
          spawnTime: performance.now() / 1000,
        });
      }
    }
    enemies.current = newEnemies;
    forceUpdate(n => n + 1);
  }, [mission, diffMult]);

  // Timer
  useEffect(() => {
    if (paused || gameOver) return;
    const interval = setInterval(() => {
      setTimeLeft((t) => {
        if (t <= 1) {
          handleEnd(false);
          return 0;
        }
        return t - 1;
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [paused, gameOver]);

  // Spawn next wave
  useEffect(() => {
    if (gameOver) return;
    const aliveCount = enemies.current.filter((e) => e.alive).length;
    if (aliveCount === 0 && waveIndex < mission.waves.length) {
      const timer = setTimeout(() => {
        const wave = mission.waves[waveIndex];
        if (!wave) return;
        const newEnemies: EnemyInstance[] = [];
        for (const spawn of wave.spawns) {
          const enemyData = ENEMIES.find((e) => e.id === spawn.enemyId);
          if (!enemyData) continue;
          for (let i = 0; i < spawn.count; i++) {
            const zones = [
              { x: 0, z: -17 }, { x: 0, z: 17 }, { x: 17, z: 0 }, { x: -17, z: 0 },
            ];
            const zone = zones[Math.floor(Math.random() * zones.length)];
            const angle = Math.random() * Math.PI * 2;
            const dist = Math.random() * 4;
            newEnemies.push({
              id: enemyIdCounter.current++,
              enemy: enemyData,
              position: new THREE.Vector3(zone.x + Math.cos(angle) * dist, 0, zone.z + Math.sin(angle) * dist),
              velocity: new THREE.Vector3(),
              health: enemyData.baseHealth * diffMult,
              maxHealth: enemyData.baseHealth * diffMult,
              alive: true,
              lastShot: 0,
              hitFlash: 0,
              attackCooldown: 2 + Math.random() * 2,
              spawnTime: performance.now() / 1000,
            });
          }
        }
        enemies.current.push(...newEnemies);
        forceUpdate(n => n + 1);
        setWaveIndex(w => w + 1);
      }, 2500);
      return () => clearTimeout(timer);
    }
  }, [waveIndex, mission, diffMult, gameOver]);

  // Victory check
  useEffect(() => {
    if (gameOver) return;
    const aliveCount = enemies.current.filter((e) => e.alive).length;
    if (aliveCount === 0 && waveIndex >= mission.waves.length) {
      const timer = setTimeout(() => handleEnd(true), 1500);
      return () => clearTimeout(timer);
    }
  }, [waveIndex, mission, gameOver]);

  // Death
  useEffect(() => {
    if (health <= 0 && !gameOver) {
      setGameOver(true);
      setTimeout(() => handleEnd(false), 500);
    }
  }, [health, gameOver]);

  const handleEnd = useCallback((victory: boolean) => {
    if (gameOver && !victory) return;
    endMission({
      victory,
      missionId: mission.id,
      objectivesCompleted: victory ? mission.objectives.length : Math.floor(mission.objectives.length / 2),
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
  }, [mission, enemiesKilled, timeLeft, damageDealt, damageTaken, shotsFired, shotsHit, language, endMission, gameOver]);

  // Handlers
  const onFire = useCallback(() => {
    setAmmo((a) => Math.max(0, a - 1));
    setShotsFired((s) => s + 1);
  }, []);

  const onHit = useCallback((enemyId: number, damage: number, isHead: boolean) => {
    setShotsHit((s) => s + 1);
    setDamageDealt((d) => d + damage);
    setUltimate((u) => Math.min(1000, u + (isHead ? 10 : 5)));
    setHitMarker({ visible: true, crit: isHead });
    setTimeout(() => setHitMarker({ visible: false, crit: false }), 150);
    const e = enemies.current.find((en) => en.id === enemyId);
    if (e) {
      e.health -= damage;
      e.hitFlash = 1;
      if (e.health <= 0) {
        e.alive = false;
        setEnemiesKilled((k) => k + 1);
        setUltimate((u) => Math.min(1000, u + 50));
        forceUpdate(n => n + 1);
      }
    }
  }, []);

  const onPlayerHit = useCallback((damage: number) => {
    if (gameOver) return;
    setDamageTaken((d) => d + damage);
    setUltimate((u) => Math.min(1000, u + 2));
    // Direction aléatoire
    setDamageDirection({ angle: Math.random() * 360, visible: true });
    setTimeout(() => setDamageDirection(null), 800);
    setArmor((a) => {
      if (a > 0) {
        const absorbed = Math.min(a, damage * 0.6);
        const remaining = damage - absorbed;
        if (remaining > 0) setHealth((h) => Math.max(0, h - remaining));
        return a - absorbed;
      }
      setHealth((h) => Math.max(0, h - damage));
      return 0;
    });
  }, [gameOver]);

  // Auto-reload
  useEffect(() => {
    if (ammo === 0 && reserveAmmo > 0) {
      const timer = setTimeout(() => {
        const w = getWeapon(currentWeaponId);
        const mag = w?.magazineSize ?? 30;
        const need = mag - ammo;
        const take = Math.min(need, reserveAmmo);
        setAmmo(ammo + take);
        setReserveAmmo(reserveAmmo - take);
      }, 1500);
      return () => clearTimeout(timer);
    }
  }, [ammo, reserveAmmo, currentWeaponId]);

  // Regen shield après 4s sans dégât
  const lastDamageRef = useRef(performance.now());
  useEffect(() => {
    const interval = setInterval(() => {
      if (gameOver) return;
      const now = performance.now();
      if (now - lastDamageRef.current > 4000 && armor < agent.baseShield) {
        setArmor(a => Math.min(agent.baseShield, a + 20));
      }
    }, 500);
    return () => clearInterval(interval);
  }, [armor, agent.baseShield, gameOver]);

  const enemiesRemaining = enemies.current.filter((e) => e.alive).length;

  const hudState: HUDState = {
    health,
    maxHealth: agent.baseHealth,
    armor,
    maxArmor: agent.baseShield,
    ammo,
    reserveAmmo,
    weaponName: currentWeapon?.displayName ?? "RIFLE",
    timeLeft,
    enemiesRemaining,
    waveIndex,
    totalWaves: mission.waves.length,
    ultimate,
    objective: mission.objectives[0]?.description ?? mission.displayName,
    hitMarker,
    damageDirection,
    playerPos: { x: playerPos.current.x, z: playerPos.current.z },
    enemyPositions: enemies.current.map(e => ({ x: e.position.x, z: e.position.z, alive: e.alive })),
  };

  return (
    <div className="fixed inset-0 bg-black overflow-hidden touch-none">
      <Canvas
        shadows={false}
        dpr={[1, 1.5]}
        gl={{ antialias: false, powerPreference: "high-performance", preserveDrawingBuffer: true }}
        camera={{ fov: 75, near: 0.1, far: 100, position: [0, 1.6, 5] }}
        frameloop="always"
      >
        <color attach="background" args={["#0a1525"]} />
        <fog attach="fog" args={["#0a1525", 35, 80]} />
        <ambientLight intensity={1.2} color="#5A90B0" />
        <pointLight position={[0, 3.5, 0]} intensity={3.0} color="#1AA1CE" distance={30} />
        <pointLight position={[0, 3.5, -17]} intensity={2.5} color="#1AA1CE" distance={25} />
        <pointLight position={[0, 3.5, 17]} intensity={2.5} color="#6CF42E" distance={25} />
        <pointLight position={[17, 3.5, 0]} intensity={2.5} color="#FFE735" distance={25} />
        <pointLight position={[-17, 3.5, 0]} intensity={2.5} color="#A855F7" distance={25} />
        <hemisphereLight args={["#4A90B0", "#1a2a3e", 0.8]} />
        <directionalLight position={[5, 10, 5]} intensity={0.8} color="#FFFFFF" />

        <ShipEnvironment />

        {enemies.current.map((e) => (
          <EnemyEntity key={e.id} instance={e} playerPos={playerPos} onShootPlayer={onPlayerHit} />
        ))}

        <Player
          gameRefs={gameRefs}
          enemies={enemies}
          onFire={onFire}
          onHit={onHit}
          onPlayerHit={onPlayerHit}
          playerPos={playerPos}
          currentWeaponId={currentWeaponId}
        />
      </Canvas>

      {/* HUD overlay */}
      <HUDScreen hudState={hudState} onPause={() => setPaused(true)} />

      {/* Touch controls (toujours visibles) */}
      <TouchControls gameRefs={gameRefs} weaponSlot={weaponSlot} onSwitchWeapon={(s) => setWeaponSlot(s)} />

      {/* Indicateur arme courante */}
      <div className="absolute top-20 right-3 z-30 bg-k5-panel/80 border border-k5-cyan/40 px-2 py-1 rounded-sm text-[10px] font-display">
        <div className="text-k5-muted">ARME</div>
        <div className="flex gap-1 mt-0.5">
          <span className={weaponSlot === 1 ? "text-k5-cyan" : "text-k5-muted"}>1·PRI</span>
          <span className={weaponSlot === 2 ? "text-k5-cyan" : "text-k5-muted"}>2·SEC</span>
          <span className={weaponSlot === 3 ? "text-k5-cyan" : "text-k5-muted"}>3·TAC</span>
        </div>
      </div>

      {/* Game over */}
      {gameOver && (
        <div className="absolute inset-0 z-50 bg-k5-deep-space/80 flex items-center justify-center">
          <div className="text-center">
            <div className="font-display text-4xl text-k5-red k5-text-glow-red mb-2">ÉLIMINÉ</div>
            <div className="text-k5-muted text-sm">Redémarrage...</div>
          </div>
        </div>
      )}

      {/* Pause */}
      {paused && !gameOver && (
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
