/**
 * KINETICS 5 — Store global (Zustand)
 * Gère l'état du jeu : écran courant, agent/mission sélectionnés,
 * progression joueur, combat en cours, paramètres.
 */

import { create } from "zustand";
import {
  AGENTS,
  MISSIONS,
  WEAPONS,
  type Agent,
  type Mission,
  type Weapon,
} from "@/lib/kinetics-data";

export type ScreenName =
  | "start"
  | "loading"
  | "lobby"
  | "loadout"
  | "armory"
  | "mission"
  | "victory"
  | "defeat"
  | "summary"
  | "settings";

export type Language = "fr" | "en";
export type Difficulty = "easy" | "normal" | "hard";

export interface CombatResult {
  victory: boolean;
  missionId: string;
  objectivesCompleted: number;
  objectivesTotal: number;
  enemiesKilled: number;
  timeElapsed: number;
  damageDealt: number;
  damageTaken: number;
  accuracy: number;
  rewards: { xp: number; cr: number };
  bonuses: { label: string; xp?: number; cr?: number }[];
}

interface GameState {
  screen: ScreenName;
  previousScreen: ScreenName | null;
  playerLevel: number;
  playerXp: number;
  playerCr: number;
  playerPower: number;
  selectedAgentId: string;
  selectedMissionId: string;
  equippedPrimaryId: string;
  equippedSecondaryId: string;
  equippedTacticalId: string;
  currentMissionId: string | null;
  combatResult: CombatResult | null;
  language: Language;
  difficulty: Difficulty;
  musicVolume: number;
  sfxVolume: number;
  graphicsQuality: "low" | "medium" | "high";
  controlLayout: "right" | "left";
  sensitivity: number;
  loadingProgress: number;
  loadingTip: string;

  setScreen: (screen: ScreenName) => void;
  selectAgent: (id: string) => void;
  selectMission: (id: string) => void;
  equipWeapon: (slot: "primary" | "secondary" | "tactical", id: string) => void;
  startMission: (missionId: string) => void;
  endMission: (result: CombatResult) => void;
  setLanguage: (lang: Language) => void;
  setDifficulty: (d: Difficulty) => void;
  setVolumes: (music: number, sfx: number) => void;
  setGraphicsQuality: (q: "low" | "medium" | "high") => void;
  setControlLayout: (l: "right" | "left") => void;
  setSensitivity: (s: number) => void;
  setLoadingProgress: (p: number) => void;
  setLoadingTip: (tip: string) => void;
  applyRewards: (xp: number, cr: number) => void;
  reset: () => void;
}

export const LOADING_TIPS = [
  "RARE LOOT IS OFTEN HIDDEN IN HIGH-RISK HIGH ZONES — BE PREPARED BEFORE ENTERING",
  "Toujours vérifier vos munitions avant d'engager un groupe d'ennemis",
  "Les tirs à la tête infligent 2x dégâts — visez le cockpit",
  "Les grenades FRAG-X ont un délai de 6 secondes — lancez et couvrez",
  "Le bouclier se régénère après 3 secondes sans dégâts",
  "XEN est plus rapide mais fragile — jouez la mobilité",
  "VULCAN encaisse — tenez position et supprimez",
  "Les drones SNIPER reculent si vous approchez — fermez la distance",
];

const XP_PER_LEVEL = 10000;

const initialState = {
  screen: "start" as ScreenName,
  previousScreen: null as ScreenName | null,
  playerLevel: 47,
  playerXp: 663778,
  playerCr: 234326,
  playerPower: 2500,
  selectedAgentId: "vulcan",
  selectedMissionId: "shadow_fall",
  equippedPrimaryId: "heavy_rx14",
  equippedSecondaryId: "guard_v9",
  equippedTacticalId: "frag_x",
  currentMissionId: null as string | null,
  combatResult: null as CombatResult | null,
  language: "en" as Language,
  difficulty: "normal" as Difficulty,
  musicVolume: 70,
  sfxVolume: 85,
  graphicsQuality: "high" as "low" | "medium" | "high",
  controlLayout: "right" as "right" | "left",
  sensitivity: 50,
  loadingProgress: 0,
  loadingTip: LOADING_TIPS[0],
};

export const useGameStore = create<GameState>((set) => ({
  ...initialState,

  setScreen: (screen) => set((s) => ({ previousScreen: s.screen, screen })),
  selectAgent: (id) => set({ selectedAgentId: id }),
  selectMission: (id) => set({ selectedMissionId: id }),
  equipWeapon: (slot, id) => {
    if (slot === "primary") set({ equippedPrimaryId: id });
    else if (slot === "secondary") set({ equippedSecondaryId: id });
    else set({ equippedTacticalId: id });
  },
  startMission: (missionId) =>
    set({
      currentMissionId: missionId,
      screen: "loading",
      loadingProgress: 0,
      loadingTip: LOADING_TIPS[Math.floor(Math.random() * LOADING_TIPS.length)],
    }),
  endMission: (result) =>
    set((s) => {
      const newXp = s.playerXp + result.rewards.xp;
      const newCr = s.playerCr + result.rewards.cr;
      let newLevel = s.playerLevel;
      let remainingXp = newXp;
      while (remainingXp >= XP_PER_LEVEL) {
        remainingXp -= XP_PER_LEVEL;
        newLevel++;
      }
      return {
        combatResult: result,
        screen: result.victory ? "victory" : "defeat",
        playerXp: remainingXp,
        playerCr: newCr,
        playerLevel: newLevel,
        currentMissionId: null,
      };
    }),
  setLanguage: (lang) => set({ language: lang }),
  setDifficulty: (d) => set({ difficulty: d }),
  setVolumes: (music, sfx) => set({ musicVolume: music, sfxVolume: sfx }),
  setGraphicsQuality: (q) => set({ graphicsQuality: q }),
  setControlLayout: (l) => set({ controlLayout: l }),
  setSensitivity: (s) => set({ sensitivity: s }),
  setLoadingProgress: (p) => set({ loadingProgress: p }),
  setLoadingTip: (tip) => set({ loadingTip: tip }),
  applyRewards: (xp, cr) =>
    set((s) => {
      const newXp = s.playerXp + xp;
      const newCr = s.playerCr + cr;
      let newLevel = s.playerLevel;
      let remainingXp = newXp;
      while (remainingXp >= XP_PER_LEVEL) {
        remainingXp -= XP_PER_LEVEL;
        newLevel++;
      }
      return { playerXp: remainingXp, playerCr: newCr, playerLevel: newLevel };
    }),
  reset: () => set({ ...initialState }),
}));

export const useSelectedAgent = (): Agent => {
  const id = useGameStore((s) => s.selectedAgentId);
  return AGENTS.find((a) => a.id === id) ?? AGENTS[0];
};

export const useSelectedMission = (): Mission => {
  const id = useGameStore((s) => s.selectedMissionId);
  return MISSIONS.find((m) => m.id === id) ?? MISSIONS[0];
};

export const useEquippedWeapons = (): {
  primary: Weapon;
  secondary: Weapon;
  tactical: Weapon;
} => {
  const p = useGameStore((s) => s.equippedPrimaryId);
  const sec = useGameStore((s) => s.equippedSecondaryId);
  const t = useGameStore((s) => s.equippedTacticalId);
  return {
    primary: WEAPONS.find((w) => w.id === p) ?? WEAPONS[0],
    secondary: WEAPONS.find((w) => w.id === sec) ?? WEAPONS[5],
    tactical: WEAPONS.find((w) => w.id === t) ?? WEAPONS[10],
  };
};

export { XP_PER_LEVEL };
