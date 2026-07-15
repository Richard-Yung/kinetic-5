/**
 * KINETICS 5 — Données de jeu (source unique de vérité)
 * Extraites du PDF de design "shooter mobile game 5 2.pdf"
 * et étendues pour la production (7 missions, ennemis multi-niveaux).
 *
 * Ces données reflètent fidèlement le design de référence :
 * - 4 agents : VULCAN (Tank), XEN (Assault), JOLT (Support), XANO (Recon)
 * - 14 armes (5 primaires, 5 secondaires, 4 tactiques)
 * - 7 missions (de SHADOW FALL à FINAL VECTOR)
 * - 11 ennemis (7 archétypes + 4 bosses)
 */

export type AgentClass = "Tank" | "Assault" | "Recon" | "Support";
export type WeaponCategory = "Primary" | "Secondary" | "Tactical";
export type WeaponType =
  | "AssaultRifle"
  | "SMG"
  | "Shotgun"
  | "Sniper"
  | "Heavy"
  | "Pistol"
  | "Grenade"
  | "Trap"
  | "Special";
export type Rarity = "Common" | "Rare" | "Epic" | "Legendary";
export type Element = "Kinetic" | "Energy" | "Explosive" | "Cryo" | "Volt";
export type MissionType =
  | "Extraction"
  | "Sabotage"
  | "Survival"
  | "Assassination"
  | "Recon"
  | "Defense"
  | "BossRush";
export type EnemyClass =
  | "Grunt"
  | "Soldier"
  | "Elite"
  | "Sniper"
  | "Heavy"
  | "Drone"
  | "Boss";

export interface Agent {
  id: string;
  displayName: string;
  class: AgentClass;
  unlockLevel: number;
  level: number;
  description: string;
  motto: string;
  baseHealth: number;
  baseShield: number;
  baseSpeed: number; // 0-1 (1 = normal)
  basePower: number;
  themeColor: string;
  abilities: { name: string; description: string; cooldown: number }[];
}

export interface Weapon {
  id: string;
  displayName: string;
  category: WeaponCategory;
  type: WeaponType;
  power: number;
  reloadTime: number; // seconds
  damage: number; // %
  fireRate: number; // %
  accuracy: number; // %
  stability: number; // %
  explosionRadius?: number; // % (tactical)
  fuseTime?: number; // seconds (grenades)
  rarity: Rarity;
  element: Element;
  magazineSize: number;
  range: number; // meters
  fireModes: ("Single" | "Burst" | "Auto")[];
  unlockLevel: number;
}

export interface MissionObjective {
  id: string;
  description: string;
  kind: "Primary" | "SpecOps" | "Tactical";
  targetId: string;
  requiredCount: number;
  rewardXp: number;
  rewardCr: number;
}

export interface EnemySpawn {
  enemyId: string;
  count: number;
  delay: number; // seconds after wave start
}

export interface MissionWave {
  id: string;
  delay: number;
  spawns: EnemySpawn[];
}

export interface Mission {
  id: string;
  displayName: string;
  codename: string;
  type: MissionType;
  region: string;
  shipName: string;
  recommendedPower: number;
  timeLimit: number; // seconds, 0 = no limit
  description: string;
  brief: string;
  objectives: MissionObjective[];
  waves: MissionWave[];
  rewards: { xp: number; cr: number };
  environment: {
    shipType: string;
    lighting: "Dim" | "Emergency" | "Standard" | "Hostile";
    atmosphere: "Vacuum" | "Breathable" | "Toxic" | "Unknown";
  };
  bossId?: string;
  isFinale?: boolean;
}

export interface Enemy {
  id: string;
  displayName: string;
  enemyClass: EnemyClass;
  baseHealth: number;
  baseShield: number;
  baseDamage: number;
  moveSpeed: number;
  attackRange: number; // meters
  attackRate: number; // shots per second
  behavior: "Patrol" | "Aggressive" | "Defensive" | "Flanking" | "Berserker" | "Sniper";
  weakness: Element;
  resistance: Element;
  xpReward: number;
  crReward: number;
  description: string;
  isBoss?: boolean;
  phases?: { hpThreshold: number; name: string }[];
}

/* ============================================================
   AGENTS — 4 personnages jouables (PDF page 4-5)
   ============================================================ */
export const AGENTS: Agent[] = [
  {
    id: "vulcan",
    displayName: "VULCAN",
    class: "Tank",
    unlockLevel: 1,
    level: 47,
    description:
      "Heavy suppression operative. The use of experimental alloys has pushed Vulcan's defense to the absolute limit at the cost of maneuverability.",
    motto: "The Wall Never Retreats",
    baseHealth: 5000,
    baseShield: 2500,
    baseSpeed: 0.6,
    basePower: 2500,
    themeColor: "#1AA1CE",
    abilities: [
      { name: "Bulwark", description: "Déploie un bouclier frontal absorbant 2000 dégâts pendant 8s.", cooldown: 18 },
      { name: "Seismic Slam", description: "Impact au sol infligeant 1500 dégâts en zone et étourdissant 2s.", cooldown: 22 },
      { name: "Last Stand", description: "Ultimate — Devient invulnérable 5s et attire les ennemis.", cooldown: 0 },
    ],
  },
  {
    id: "xen",
    displayName: "XEN",
    class: "Assault",
    unlockLevel: 55,
    level: 1,
    description:
      "High-mobility assault operative. Plasma-infused strikes shred armor and leave targets burning.",
    motto: "Strike First, Strike Hard",
    baseHealth: 3500,
    baseShield: 1500,
    baseSpeed: 1.2,
    basePower: 2400,
    themeColor: "#FE0022",
    abilities: [
      { name: "Plasma Dash", description: "Dash rapide infligeant 800 dégâts Energy sur le trajet.", cooldown: 8 },
      { name: "Incinerator", description: "Cône de flammes 1200 dégâts + effet de brûlure 4s.", cooldown: 15 },
      { name: "Overdrive", description: "Ultimate — Cadence de tir x2 et dégâts +50% pendant 8s.", cooldown: 0 },
    ],
  },
  {
    id: "jolt",
    displayName: "JOLT",
    class: "Support",
    unlockLevel: 1,
    level: 1,
    description:
      "Field medic and EMP specialist. Sustains the squad and disables hostile tech with surgical precision.",
    motto: "Stay Charged, Stay Alive",
    baseHealth: 3000,
    baseShield: 2000,
    baseSpeed: 1.0,
    basePower: 2300,
    themeColor: "#FFE735",
    abilities: [
      { name: "Field Stim", description: "Soin 1500 PV instantané + regen 100/s pendant 5s.", cooldown: 16 },
      { name: "EMP Pulse", description: "Désactive drones et boucliers ennemis dans 15m pendant 4s.", cooldown: 20 },
      { name: "Revive Matrix", description: "Ultimate — Ressuscite un allié à 50% PV ou auto-régénère 3000 PV.", cooldown: 0 },
    ],
  },
  {
    id: "xano",
    displayName: "XANO",
    class: "Recon",
    unlockLevel: 55,
    level: 1,
    description:
      "Stealth infiltrator. Silent takedowns and real-time recon data give the squad the edge before engagement.",
    motto: "Seen Too Late",
    baseHealth: 2800,
    baseShield: 1200,
    baseSpeed: 1.3,
    basePower: 2350,
    themeColor: "#6CF42E",
    abilities: [
      { name: "Cloak", description: "Invisibilité 6s, premier coup sortant de furtivité +200% dégâts.", cooldown: 14 },
      { name: "Recon Ping", description: "Révèle tous les ennemis dans 30m pendant 10s.", cooldown: 12 },
      { name: "Shadow Strike", description: "Ultimate — Téléportation + takedown instantané des ennemis faibles.", cooldown: 0 },
    ],
  },
];

/* ============================================================
   WEAPONS — 14 armes (PDF page 5)
   Stats PDF respectées ; valeurs manquantes inférées pour balance.
   ============================================================ */
export const WEAPONS: Weapon[] = [
  // === PRIMARY ===
  {
    id: "heavy_rx14",
    displayName: "HEAVY RX-14",
    category: "Primary",
    type: "Heavy",
    power: 1500,
    reloadTime: 3.2,
    damage: 72,
    fireRate: 85,
    accuracy: 60,
    stability: 68,
    rarity: "Rare",
    element: "Kinetic",
    magazineSize: 60,
    range: 80,
    fireModes: ["Auto"],
    unlockLevel: 1,
  },
  {
    id: "rifle_cx24",
    displayName: "RIFLE CX-24",
    category: "Primary",
    type: "AssaultRifle",
    power: 1200,
    reloadTime: 2.5,
    damage: 65,
    fireRate: 78,
    accuracy: 75,
    stability: 72,
    rarity: "Rare",
    element: "Kinetic",
    magazineSize: 30,
    range: 100,
    fireModes: ["Auto", "Single"],
    unlockLevel: 1,
  },
  {
    id: "ax9_sr",
    displayName: "AX-9 SR",
    category: "Primary",
    type: "SMG",
    power: 900,
    reloadTime: 2.0,
    damage: 55,
    fireRate: 92,
    accuracy: 50,
    stability: 60,
    rarity: "Common",
    element: "Kinetic",
    magazineSize: 40,
    range: 50,
    fireModes: ["Auto"],
    unlockLevel: 5,
  },
  {
    id: "cx27_atlas",
    displayName: "CX-27 ATLAS",
    category: "Primary",
    type: "Sniper",
    power: 2800,
    reloadTime: 3.5,
    damage: 98,
    fireRate: 25,
    accuracy: 95,
    stability: 80,
    rarity: "Epic",
    element: "Energy",
    magazineSize: 5,
    range: 200,
    fireModes: ["Single"],
    unlockLevel: 15,
  },
  {
    id: "c2_lmg",
    displayName: "ATLAS C-2",
    category: "Primary",
    type: "Heavy",
    power: 1700,
    reloadTime: 4.5,
    damage: 70,
    fireRate: 88,
    accuracy: 55,
    stability: 65,
    rarity: "Epic",
    element: "Kinetic",
    magazineSize: 100,
    range: 90,
    fireModes: ["Auto"],
    unlockLevel: 25,
  },
  // === SECONDARY ===
  {
    id: "guard_v9",
    displayName: "GUARD V-9",
    category: "Secondary",
    type: "Pistol",
    power: 300,
    reloadTime: 2.2,
    damage: 72,
    fireRate: 60,
    accuracy: 80,
    stability: 75,
    rarity: "Rare",
    element: "Kinetic",
    magazineSize: 12,
    range: 40,
    fireModes: ["Single"],
    unlockLevel: 1,
  },
  {
    id: "striker_p12",
    displayName: "STRIKER P-12",
    category: "Secondary",
    type: "Pistol",
    power: 250,
    reloadTime: 1.8,
    damage: 60,
    fireRate: 70,
    accuracy: 75,
    stability: 70,
    rarity: "Common",
    element: "Kinetic",
    magazineSize: 15,
    range: 35,
    fireModes: ["Single", "Burst"],
    unlockLevel: 1,
  },
  {
    id: "core_p4",
    displayName: "CORE P-4",
    category: "Secondary",
    type: "Pistol",
    power: 400,
    reloadTime: 2.5,
    damage: 80,
    fireRate: 50,
    accuracy: 85,
    stability: 78,
    rarity: "Epic",
    element: "Energy",
    magazineSize: 10,
    range: 45,
    fireModes: ["Single"],
    unlockLevel: 10,
  },
  {
    id: "magnum_e2",
    displayName: "MAGNUM E-2",
    category: "Secondary",
    type: "Pistol",
    power: 550,
    reloadTime: 3.0,
    damage: 92,
    fireRate: 35,
    accuracy: 88,
    stability: 65,
    rarity: "Epic",
    element: "Explosive",
    magazineSize: 6,
    range: 50,
    fireModes: ["Single"],
    unlockLevel: 20,
  },
  {
    id: "ion_xs",
    displayName: "ION X-S",
    category: "Secondary",
    type: "Special",
    power: 450,
    reloadTime: 2.8,
    damage: 75,
    fireRate: 55,
    accuracy: 82,
    stability: 72,
    rarity: "Legendary",
    element: "Volt",
    magazineSize: 8,
    range: 45,
    fireModes: ["Single"],
    unlockLevel: 30,
  },
  // === TACTICAL ===
  {
    id: "frag_x",
    displayName: "FRAG-X",
    category: "Tactical",
    type: "Grenade",
    power: 3700,
    reloadTime: 0,
    damage: 90,
    fireRate: 0,
    accuracy: 0,
    stability: 0,
    explosionRadius: 85,
    fuseTime: 6,
    rarity: "Rare",
    element: "Explosive",
    magazineSize: 3,
    range: 30,
    fireModes: ["Single"],
    unlockLevel: 1,
  },
  {
    id: "cyber_trap_f2",
    displayName: "CYBER TRAP F-2",
    category: "Tactical",
    type: "Trap",
    power: 1500,
    reloadTime: 0,
    damage: 40,
    fireRate: 0,
    accuracy: 0,
    stability: 0,
    explosionRadius: 60,
    fuseTime: 0,
    rarity: "Rare",
    element: "Volt",
    magazineSize: 2,
    range: 20,
    fireModes: ["Single"],
    unlockLevel: 8,
  },
  {
    id: "supernova",
    displayName: "SUPERNOVA",
    category: "Tactical",
    type: "Grenade",
    power: 5200,
    reloadTime: 0,
    damage: 98,
    fireRate: 0,
    accuracy: 0,
    stability: 0,
    explosionRadius: 95,
    fuseTime: 4,
    rarity: "Legendary",
    element: "Energy",
    magazineSize: 1,
    range: 35,
    fireModes: ["Single"],
    unlockLevel: 35,
  },
  {
    id: "titan_m8",
    displayName: "TITAN M-8",
    category: "Tactical",
    type: "Grenade",
    power: 6500,
    reloadTime: 0,
    damage: 100,
    fireRate: 0,
    accuracy: 0,
    stability: 0,
    explosionRadius: 100,
    fuseTime: 5,
    rarity: "Legendary",
    element: "Explosive",
    magazineSize: 1,
    range: 40,
    fireModes: ["Single"],
    unlockLevel: 45,
  },
];

/* ============================================================
   ENEMIES — 11 ennemis (7 archétypes + 4 bosses)
   ============================================================ */
export const ENEMIES: Enemy[] = [
  {
    id: "grunt_mk1",
    displayName: "GRUNT MK-1",
    enemyClass: "Grunt",
    baseHealth: 800,
    baseShield: 0,
    baseDamage: 80,
    moveSpeed: 2.5,
    attackRange: 30,
    attackRate: 1,
    behavior: "Aggressive",
    weakness: "Kinetic",
    resistance: "Explosive",
    xpReward: 50,
    crReward: 25,
    description: "Soldat d'infanterie basique. Faiblement blindé, chargé en nombre.",
  },
  {
    id: "assault_droid",
    displayName: "ASSAULT DROID",
    enemyClass: "Soldier",
    baseHealth: 1200,
    baseShield: 300,
    baseDamage: 120,
    moveSpeed: 3.0,
    attackRange: 40,
    attackRate: 1.5,
    behavior: "Flanking",
    weakness: "Volt",
    resistance: "Kinetic",
    xpReward: 100,
    crReward: 50,
    description: "Droïde d'assaut polyvalent. Manœuvre de flanquement, tir soutenu.",
  },
  {
    id: "elite_guard",
    displayName: "ELITE GUARD",
    enemyClass: "Elite",
    baseHealth: 2500,
    baseShield: 800,
    baseDamage: 180,
    moveSpeed: 2.8,
    attackRange: 35,
    attackRate: 1.2,
    behavior: "Defensive",
    weakness: "Energy",
    resistance: "Kinetic",
    xpReward: 200,
    crReward: 100,
    description: "Garde d'élite lourdement blindé. Tient position, suppressif.",
  },
  {
    id: "sniper_drone",
    displayName: "SNIPER DRONE",
    enemyClass: "Sniper",
    baseHealth: 600,
    baseShield: 200,
    baseDamage: 350,
    moveSpeed: 4.0,
    attackRange: 120,
    attackRate: 0.3,
    behavior: "Sniper",
    weakness: "Explosive",
    resistance: "Cryo",
    xpReward: 150,
    crReward: 75,
    description: "Drone sniper aérien. Longue portée, faible résistance. Recule si approché.",
  },
  {
    id: "heavy_unit",
    displayName: "HEAVY UNIT",
    enemyClass: "Heavy",
    baseHealth: 4000,
    baseShield: 1000,
    baseDamage: 250,
    moveSpeed: 1.5,
    attackRange: 50,
    attackRate: 0.8,
    behavior: "Defensive",
    weakness: "Cryo",
    resistance: "Kinetic",
    xpReward: 300,
    crReward: 150,
    description: "Unité lourde à canon rotatif. Lent mais dévastateur. Faiblesse refroidissement.",
  },
  {
    id: "swarm_bot",
    displayName: "SWARM BOT",
    enemyClass: "Drone",
    baseHealth: 200,
    baseShield: 0,
    baseDamage: 40,
    moveSpeed: 5.0,
    attackRange: 15,
    attackRate: 2,
    behavior: "Berserker",
    weakness: "Volt",
    resistance: "Explosive",
    xpReward: 25,
    crReward: 10,
    description: "Micro-drone d'essaim. Très rapide, attaque en groupe, explosion à proximité.",
  },
  {
    id: "stealth_cloaker",
    displayName: "STEALTH CLOAKER",
    enemyClass: "Soldier",
    baseHealth: 1000,
    baseShield: 400,
    baseDamage: 200,
    moveSpeed: 3.5,
    attackRange: 25,
    attackRate: 1,
    behavior: "Flanking",
    weakness: "Energy",
    resistance: "Kinetic",
    xpReward: 180,
    crReward: 90,
    description: "Infiltrateur furtif. Devient invisible, frappe dans le dos.",
  },
  // === BOSSES ===
  {
    id: "boss_titan",
    displayName: "TITAN",
    enemyClass: "Boss",
    baseHealth: 50000,
    baseShield: 10000,
    baseDamage: 400,
    moveSpeed: 1.8,
    attackRange: 60,
    attackRate: 0.8,
    behavior: "Berserker",
    weakness: "Explosive",
    resistance: "Kinetic",
    xpReward: 5000,
    crReward: 3000,
    description: "Mécha lourd de forge. Armement lourd, blindage composite. 3 phases.",
    isBoss: true,
    phases: [
      { hpThreshold: 100, name: "Phase 1 — Suppressif" },
      { hpThreshold: 66, name: "Phase 2 — Enragé" },
      { hpThreshold: 33, name: "Phase 3 — Désespéré" },
    ],
  },
  {
    id: "boss_neural_core",
    displayName: "NEURAL CORE",
    enemyClass: "Boss",
    baseHealth: 80000,
    baseShield: 20000,
    baseDamage: 350,
    moveSpeed: 0,
    attackRange: 100,
    attackRate: 1.2,
    behavior: "Defensive",
    weakness: "Volt",
    resistance: "Energy",
    xpReward: 8000,
    crReward: 5000,
    description: "Cœur neural du vaisseau amiral. Statique mais défendu par des drones. EMP sensible.",
    isBoss: true,
    phases: [
      { hpThreshold: 100, name: "Phase 1 — Boucliers actifs" },
      { hpThreshold: 66, name: "Phase 2 — Drones invoqués" },
      { hpThreshold: 33, name: "Phase 3 — Surcharge" },
    ],
  },
  {
    id: "boss_voidwarden",
    displayName: "VOIDWARDEN",
    enemyClass: "Boss",
    baseHealth: 90000,
    baseShield: 15000,
    baseDamage: 450,
    moveSpeed: 2.5,
    attackRange: 80,
    attackRate: 1.0,
    behavior: "Flanking",
    weakness: "Cryo",
    resistance: "Kinetic",
    xpReward: 9000,
    crReward: 6000,
    description: "Gardien du néant. Téléportation, attaques dimensionnelles.",
    isBoss: true,
    phases: [
      { hpThreshold: 100, name: "Phase 1 — Patrouille" },
      { hpThreshold: 66, name: "Phase 2 — Téléportations" },
      { hpThreshold: 33, name: "Phase 3 — Déchirure dimensionnelle" },
    ],
  },
  {
    id: "boss_overlord",
    displayName: "OVERLORD PRIME",
    enemyClass: "Boss",
    baseHealth: 120000,
    baseShield: 30000,
    baseDamage: 550,
    moveSpeed: 2.0,
    attackRange: 100,
    attackRate: 1.5,
    behavior: "Berserker",
    weakness: "Energy",
    resistance: "Kinetic",
    xpReward: 15000,
    crReward: 10000,
    description: "Commandant suprême. Boss final. Toutes les mécaniques combinées.",
    isBoss: true,
    phases: [
      { hpThreshold: 100, name: "Phase 1 — Régne" },
      { hpThreshold: 75, name: "Phase 2 — Colère" },
      { hpThreshold: 50, name: "Phase 3 — Désespoir" },
      { hpThreshold: 25, name: "Phase 4 — Néant" },
    ],
  },
];

/* ============================================================
   MISSIONS — 7 missions de SHADOW FALL à FINAL VECTOR
   ============================================================ */
export const MISSIONS: Mission[] = [
  {
    id: "shadow_fall",
    displayName: "SHADOW FALL",
    codename: "EXTRACTION",
    type: "Extraction",
    region: "Cargo Ship",
    shipName: "MV Tarnhelm",
    recommendedPower: 2000,
    timeLimit: 600,
    description: "Infiltrez le cargo MV Tarnhelm, récupérez la datacore et exfilmez.",
    brief:
      "Le cargo MV Tarnhelm dérive au-delà du périmètre. La datacore à bord contient des schematics critiques. Récupérez-la et exfilez avant l'arrivée des renforts ennemis.",
    objectives: [
      { id: "obj1", description: "NEURAL CORE SECURED", kind: "Primary", targetId: "datacore", requiredCount: 1, rewardXp: 4500, rewardCr: 0 },
      { id: "obj2", description: "SPEC-OPS CLEAR", kind: "SpecOps", targetId: "elite_guard", requiredCount: 3, rewardXp: 2500, rewardCr: 0 },
      { id: "obj3", description: "STEALTH DATA RECOVERY", kind: "Tactical", targetId: "terminal", requiredCount: 2, rewardXp: 1000, rewardCr: 45 },
    ],
    waves: [
      { id: "w1", delay: 0, spawns: [{ enemyId: "grunt_mk1", count: 4, delay: 0 }] },
      { id: "w2", delay: 30, spawns: [{ enemyId: "grunt_mk1", count: 4, delay: 0 }, { enemyId: "assault_droid", count: 2, delay: 5 }] },
      { id: "w3", delay: 60, spawns: [{ enemyId: "elite_guard", count: 3, delay: 0 }, { enemyId: "sniper_drone", count: 2, delay: 10 }] },
    ],
    rewards: { xp: 8000, cr: 5000 },
    environment: { shipType: "Cargo", lighting: "Dim", atmosphere: "Breathable" },
  },
  {
    id: "neural_breach",
    displayName: "NEURAL BREACH",
    codename: "SABOTAGE",
    type: "Sabotage",
    region: "Heavy Cruiser",
    shipName: "Kriegsonde",
    recommendedPower: 2800,
    timeLimit: 720,
    description: "Sabotez le core neural du croiseur lourd Kriegsonde.",
    brief: "Le croiseur Kriegsonde coordonne les défenses ennemies. Détruisez son core neural pour aveugler leur réseau.",
    objectives: [
      { id: "obj1", description: "CORE SHIELD DISABLED", kind: "Primary", targetId: "core_shield", requiredCount: 1, rewardXp: 5000, rewardCr: 0 },
      { id: "obj2", description: "NEURAL CORE DESTROYED", kind: "Primary", targetId: "neural_core", requiredCount: 1, rewardXp: 6000, rewardCr: 0 },
      { id: "obj3", description: "DEFENSE TURRETS NEUTRALIZED", kind: "SpecOps", targetId: "turret", requiredCount: 4, rewardXp: 3000, rewardCr: 200 },
    ],
    waves: [
      { id: "w1", delay: 0, spawns: [{ enemyId: "assault_droid", count: 4, delay: 0 }] },
      { id: "w2", delay: 40, spawns: [{ enemyId: "elite_guard", count: 3, delay: 0 }, { enemyId: "sniper_drone", count: 2, delay: 5 }] },
      { id: "w3", delay: 80, spawns: [{ enemyId: "heavy_unit", count: 1, delay: 0 }, { enemyId: "grunt_mk1", count: 6, delay: 5 }] },
      { id: "w4", delay: 120, spawns: [{ enemyId: "stealth_cloaker", count: 3, delay: 0 }, { enemyId: "elite_guard", count: 2, delay: 10 }] },
      { id: "w5", delay: 160, spawns: [{ enemyId: "elite_guard", count: 5, delay: 0 }, { enemyId: "heavy_unit", count: 1, delay: 8 }] },
    ],
    rewards: { xp: 12000, cr: 8000 },
    environment: { shipType: "Cruiser", lighting: "Emergency", atmosphere: "Toxic" },
  },
  {
    id: "void_lock",
    displayName: "VOID LOCK",
    codename: "SURVIVAL",
    type: "Survival",
    region: "Orbital Station",
    shipName: "Hexgrid-9",
    recommendedPower: 3500,
    timeLimit: 480,
    description: "Survivez 8 minutes aux vagues ennemies sur la station Hexgrid-9.",
    brief: "La station Hexgrid-9 est encerclée. Tenez la position jusqu'à l'arrivée de l'extraction. 8 vagues croissantes.",
    objectives: [
      { id: "obj1", description: "SURVIVE 8 MINUTES", kind: "Primary", targetId: "timer", requiredCount: 480, rewardXp: 8000, rewardCr: 0 },
      { id: "obj2", description: "HOLD ALL CONTROL POINTS", kind: "SpecOps", targetId: "control_point", requiredCount: 3, rewardXp: 4000, rewardCr: 300 },
    ],
    waves: Array.from({ length: 8 }, (_, i) => ({
      id: `w${i + 1}`,
      delay: i * 60,
      spawns: [
        { enemyId: "grunt_mk1", count: 4 + i, delay: 0 },
        { enemyId: "assault_droid", count: 2 + Math.floor(i / 2), delay: 5 },
        ...(i >= 3 ? [{ enemyId: "swarm_bot", count: 6, delay: 10 }] : []),
        ...(i >= 5 ? [{ enemyId: "elite_guard", count: 2, delay: 15 }] : []),
        ...(i === 7 ? [{ enemyId: "heavy_unit", count: 2, delay: 20 }] : []),
      ],
    })),
    rewards: { xp: 15000, cr: 10000 },
    environment: { shipType: "Station", lighting: "Emergency", atmosphere: "Vacuum" },
  },
  {
    id: "iron_harvest",
    displayName: "IRON HARVEST",
    codename: "ASSASSINATION",
    type: "Assassination",
    region: "Drone Factory",
    shipName: "Forge Epsilon",
    recommendedPower: 4200,
    timeLimit: 600,
    description: "Éliminez le TITAN dans la forge de drones Epsilon.",
    brief: "La forge Epsilon produit des méchas TITAN. Détruisez le prototype avant qu'il ne soit déployé au combat.",
    objectives: [
      { id: "obj1", description: "TITAN DESTROYED", kind: "Primary", targetId: "boss_titan", requiredCount: 1, rewardXp: 12000, rewardCr: 0 },
      { id: "obj2", description: "PRODUCTION LINE DISABLED", kind: "SpecOps", targetId: "production_line", requiredCount: 3, rewardXp: 4000, rewardCr: 250 },
      { id: "obj3", description: "DRONES SCRAPPED", kind: "Tactical", targetId: "swarm_bot", requiredCount: 20, rewardXp: 2000, rewardCr: 100 },
    ],
    waves: [
      { id: "w1", delay: 0, spawns: [{ enemyId: "swarm_bot", count: 8, delay: 0 }] },
      { id: "w2", delay: 30, spawns: [{ enemyId: "assault_droid", count: 4, delay: 0 }, { enemyId: "swarm_bot", count: 6, delay: 5 }] },
      { id: "w3", delay: 60, spawns: [{ enemyId: "elite_guard", count: 3, delay: 0 }, { enemyId: "heavy_unit", count: 1, delay: 8 }] },
      { id: "w4", delay: 90, spawns: [{ enemyId: "boss_titan", count: 1, delay: 0 }, { enemyId: "swarm_bot", count: 10, delay: 5 }] },
    ],
    rewards: { xp: 20000, cr: 12000 },
    environment: { shipType: "Factory", lighting: "Hostile", atmosphere: "Toxic" },
    bossId: "boss_titan",
  },
  {
    id: "deep_signal",
    displayName: "DEEP SIGNAL",
    codename: "RECON",
    type: "Recon",
    region: "Derelict",
    shipName: "Voidlight Wreck",
    recommendedPower: 3800,
    timeLimit: 540,
    description: "Récupérez les données de la boîte noire du Voidlight, en furtivité.",
    brief: "L'épave Voidlight détient des données de vol critiques. Évitez la détection — alarme = échec de bonus furtif.",
    objectives: [
      { id: "obj1", description: "BLACK BOX RECOVERED", kind: "Primary", targetId: "black_box", requiredCount: 1, rewardXp: 9000, rewardCr: 0 },
      { id: "obj2", description: "STEALTH MAINTAINED", kind: "Tactical", targetId: "stealth", requiredCount: 1, rewardXp: 3000, rewardCr: 500 },
      { id: "obj3", description: "SCANNED 3 SECTORS", kind: "SpecOps", targetId: "scan", requiredCount: 3, rewardXp: 3000, rewardCr: 150 },
    ],
    waves: [
      { id: "w1", delay: 0, spawns: [{ enemyId: "stealth_cloaker", count: 2, delay: 0 }, { enemyId: "grunt_mk1", count: 3, delay: 0 }] },
      { id: "w2", delay: 60, spawns: [{ enemyId: "sniper_drone", count: 3, delay: 0 }, { enemyId: "stealth_cloaker", count: 2, delay: 10 }] },
      { id: "w3", delay: 120, spawns: [{ enemyId: "elite_guard", count: 4, delay: 0 }] },
    ],
    rewards: { xp: 18000, cr: 9000 },
    environment: { shipType: "Derelict", lighting: "Dim", atmosphere: "Unknown" },
  },
  {
    id: "black_echo",
    displayName: "BLACK ECHO",
    codename: "DEFENSE",
    type: "Defense",
    region: "Carrier",
    shipName: "Achlys Vanguard",
    recommendedPower: 4800,
    timeLimit: 600,
    description: "Défendez le portail du carrier Achlys Vanguard pendant 10 vagues.",
    brief: "Le carrier Achlys Vanguard est bordé. Défendez le portail principal — si l'ennemi le franchit, c'est fini.",
    objectives: [
      { id: "obj1", description: "PORTAL DEFENDED", kind: "Primary", targetId: "portal", requiredCount: 1, rewardXp: 14000, rewardCr: 0 },
      { id: "obj2", description: "ALL WAVES CLEARED", kind: "SpecOps", targetId: "wave", requiredCount: 10, rewardXp: 6000, rewardCr: 400 },
    ],
    waves: Array.from({ length: 10 }, (_, i) => ({
      id: `w${i + 1}`,
      delay: i * 50,
      spawns: [
        { enemyId: "grunt_mk1", count: 5 + i, delay: 0 },
        { enemyId: "assault_droid", count: 3 + Math.floor(i / 2), delay: 5 },
        ...(i >= 2 ? [{ enemyId: "sniper_drone", count: 2, delay: 10 }] : []),
        ...(i >= 4 ? [{ enemyId: "elite_guard", count: 2, delay: 12 }] : []),
        ...(i >= 6 ? [{ enemyId: "heavy_unit", count: 1, delay: 15 }] : []),
        ...(i >= 8 ? [{ enemyId: "stealth_cloaker", count: 3, delay: 18 }] : []),
      ],
    })),
    rewards: { xp: 25000, cr: 15000 },
    environment: { shipType: "Carrier", lighting: "Emergency", atmosphere: "Breathable" },
  },
  {
    id: "final_vector",
    displayName: "FINAL VECTOR",
    codename: "BOSS RUSH",
    type: "BossRush",
    region: "Flagship",
    shipName: "Overlord Prime",
    recommendedPower: 6000,
    timeLimit: 900,
    description: "Assaut final sur le vaisseau amiral Overlord Prime. 3 boss consécutifs.",
    brief: "L'Overlord Prime coordonne toute la flotte ennemie. Trois boss gardent le pont. Mettez fin à cette guerre.",
    objectives: [
      { id: "obj1", description: "VOIDWARDEN DEFEATED", kind: "Primary", targetId: "boss_voidwarden", requiredCount: 1, rewardXp: 10000, rewardCr: 0 },
      { id: "obj2", description: "NEURAL CORE SHATTERED", kind: "Primary", targetId: "boss_neural_core", requiredCount: 1, rewardXp: 12000, rewardCr: 0 },
      { id: "obj3", description: "OVERLORD PRIME ELIMINATED", kind: "Primary", targetId: "boss_overlord", requiredCount: 1, rewardXp: 20000, rewardCr: 0 },
      { id: "obj4", description: "FLAGSHIP SECURED", kind: "SpecOps", targetId: "bridge", requiredCount: 1, rewardXp: 8000, rewardCr: 1000 },
    ],
    waves: [
      { id: "w1", delay: 0, spawns: [{ enemyId: "boss_voidwarden", count: 1, delay: 0 }] },
      { id: "w2", delay: 120, spawns: [{ enemyId: "boss_neural_core", count: 1, delay: 0 }, { enemyId: "swarm_bot", count: 8, delay: 10 }] },
      { id: "w3", delay: 300, spawns: [{ enemyId: "boss_overlord", count: 1, delay: 0 }] },
    ],
    rewards: { xp: 50000, cr: 30000 },
    environment: { shipType: "Flagship", lighting: "Hostile", atmosphere: "Toxic" },
    isFinale: true,
  },
];

/* ============================================================
   Helpers
   ============================================================ */
export const getAgent = (id: string) => AGENTS.find((a) => a.id === id);
export const getWeapon = (id: string) => WEAPONS.find((w) => w.id === id);
export const getMission = (id: string) => MISSIONS.find((m) => m.id === id);
export const getEnemy = (id: string) => ENEMIES.find((e) => e.id === id);
export const getWeaponsByCategory = (cat: WeaponCategory) =>
  WEAPONS.filter((w) => w.category === cat);

export const RARITY_COLORS: Record<Rarity, string> = {
  Common: "#6B8CAE",
  Rare: "#1AA1CE",
  Epic: "#A855F7",
  Legendary: "#FFD700",
};

export const ELEMENT_COLORS: Record<Element, string> = {
  Kinetic: "#FFFFFF",
  Energy: "#1AA1CE",
  Explosive: "#FF8C00",
  Cryo: "#60A5FA",
  Volt: "#FFE735",
};

export const formatNumber = (n: number): string => {
  if (n >= 1000000) return `${(n / 1000000).toFixed(1)}M`;
  if (n >= 1000) return `${(n / 1000).toFixed(1)}K`;
  return n.toString();
};

export const formatTime = (seconds: number): string => {
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
};
