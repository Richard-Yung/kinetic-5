"use client";

/**
 * KINETICS 5 — Page de sélection de mission (avant loading)
 *
 * Architecture EXACTE d'après l'image de référence :
 * - TOP BAR (haut, pleine largeur) : ◂ MAIN LOBBY (gauche) | XP / CR / ⚙ (droite)
 * - MISSION CARD (haut-gauche) : carte arrondie avec glow cyan, CURRENT MISSION + type + opération + récompenses
 * - NAV (gauche, sous mission card) : MISSIONS / LOADOUT / SHOP empilés
 * - CHARACTER (centre) : VULCAN avec arme
 * - STATS (haut-droite → bas-droite) : CLASS, VULCAN, LEVEL 47, POWER SCORE barre
 * - PLAY BUTTON (bas-droite) : bouton arrondi bleu cyan avec glow
 *
 * Détails cyberpunk :
 * - Cartes avec rétroéclairage (bordures glow)
 * - Boutons transparents avec bordure
 * - Texte grisâtre
 * - Logo deux tons
 * - Lignes fines décoratives
 */

import { useGameStore, useSelectedAgent, useEquippedWeapons } from "@/store/game-store";
import { AgentAvatar } from "@/components/kinetics/visuals";
import { t } from "@/lib/i18n";
import { MISSIONS, formatNumber } from "@/lib/kinetics-data";
import { useState } from "react";
import { ChevronLeft, Settings, Zap } from "lucide-react";

const textColor = "#C8CDD0";

export function MissionSelectScreen() {
  const { language, setScreen, selectMission, selectedMissionId, startMission, playerLevel, playerXp, playerCr, playerPower } = useGameStore();
  const agent = useSelectedAgent();
  const equipped = useEquippedWeapons();
  const [showAllMissions, setShowAllMissions] = useState(false);

  const selectedMission = MISSIONS.find((m) => m.id === selectedMissionId) ?? MISSIONS[0];
  const availableMissions = MISSIONS.filter((m) => playerPower >= m.recommendedPower * 0.7);

  return (
    <div className="relative w-full h-screen min-h-[400px] overflow-hidden">
      {/* Background */}
      <img src="/kinetics/bg-everspace.jpg" alt="" className="absolute inset-0 w-full h-full object-cover" aria-hidden />
      <div className="absolute inset-0" style={{ background: "rgba(5, 8, 16, 0.75)" }} />
      <div className="absolute inset-0" style={{ background: "rgba(20, 40, 80, 0.15)", mixBlendMode: "color" }} />
      <div className="absolute inset-0" style={{
        backgroundImage: "linear-gradient(rgba(0, 206, 209, 0.05) 1px, transparent 1px), linear-gradient(90deg, rgba(0, 206, 209, 0.05) 1px, transparent 1px)",
        backgroundSize: "40px 40px",
      }} />

      {/* Lignes fines décoratives horizontales */}
      <div className="absolute left-0 right-0" style={{ top: "12%", height: "1px", background: "linear-gradient(to right, transparent, rgba(200,205,208,0.2) 20%, rgba(200,205,208,0.2) 80%, transparent)", zIndex: 2 }} />
      <div className="absolute left-0 right-0" style={{ bottom: "12%", height: "1px", background: "linear-gradient(to right, transparent, rgba(200,205,208,0.2) 20%, rgba(200,205,208,0.2) 80%, transparent)", zIndex: 2 }} />

      {/* === TOP BAR (pleine largeur) === */}
      <header className="absolute top-0 left-0 right-0 flex items-center justify-between px-4 py-2.5 z-20" style={{ background: "rgba(5, 8, 16, 0.7)", borderBottom: "1px solid rgba(26, 161, 206, 0.3)" }}>
        <div className="flex items-center gap-2">
          <button onClick={() => setScreen("start")} style={{ color: "#1AA1CE" }}>
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="font-display text-xs tracking-wider" style={{ color: textColor }}>{t(language, "lobby.title")}</span>
        </div>
        <div className="flex items-center gap-4 text-[10px] font-display">
          <span className="flex items-center gap-1">
            <span style={{ color: "#6CF42E" }}>XP</span>
            <span style={{ color: textColor }}>{formatNumber(playerXp)}</span>
          </span>
          <span className="flex items-center gap-1">
            <span style={{ color: "#FFE735" }}>CR</span>
            <span style={{ color: textColor }}>{formatNumber(playerCr)}</span>
          </span>
          <button onClick={() => setScreen("settings")} style={{ color: textColor }}>
            <Settings className="w-4 h-4" />
          </button>
        </div>
      </header>

      {/* === MISSION CARD (haut-gauche) === */}
      <div
        className="absolute"
        style={{
          top: "14%",
          left: "3%",
          width: "30%",
          zIndex: 10,
          background: "rgba(10, 20, 35, 0.8)",
          borderRadius: "10px",
          border: "1px solid rgba(26, 161, 206, 0.5)",
          boxShadow: "0 0 16px rgba(26, 161, 206, 0.25), inset 0 0 12px rgba(26, 161, 206, 0.08)",
          padding: "12px",
        }}
      >
        <div className="text-[9px] font-display tracking-wider mb-1" style={{ color: "#1AA1CE" }}>
          {t(language, "lobby.currentMission")}
        </div>
        <div className="h-px mb-2" style={{ background: "linear-gradient(to right, #1AA1CE, transparent)" }} />

        {/* Aperçu visuel */}
        <div
          className="w-full mb-2 flex items-center justify-center"
          style={{
            height: "50px",
            background: "linear-gradient(135deg, rgba(30, 40, 60, 0.6), rgba(10, 15, 25, 0.9))",
            borderRadius: "6px",
            border: "1px solid rgba(26, 161, 206, 0.25)",
          }}
        >
          <span className="font-display text-[10px]" style={{ color: textColor }}>{selectedMission.region.toUpperCase()}</span>
        </div>

        <div className="text-[9px]" style={{ color: textColor, opacity: 0.8 }}>
          {t(language, "lobby.missionType")}: <span style={{ color: "#1AA1CE" }}>{selectedMission.type.toUpperCase()}</span>
        </div>
        <div className="font-display text-base mt-0.5" style={{ color: "#FFFFFF" }}>{selectedMission.displayName}</div>
        <div className="text-[8px] mt-0.5" style={{ color: textColor, opacity: 0.6 }}>{selectedMission.shipName}</div>

        <div className="flex justify-between mt-2 text-[10px] font-display">
          <span style={{ color: "#6CF42E" }}>XP {Math.floor(selectedMission.rewards.xp / 1000)}K</span>
          <span style={{ color: "#FFE735" }}>CR {Math.floor(selectedMission.rewards.cr / 1000)}K</span>
        </div>
        <div className="text-[8px] mt-1" style={{ color: textColor, opacity: 0.6 }}>REC. POWER: {selectedMission.recommendedPower}</div>

        {/* Bouton transparent voir toutes les missions */}
        <button
          onClick={() => setShowAllMissions(!showAllMissions)}
          className="w-full mt-2 text-[9px] font-display tracking-wider py-1.5 transition-all"
          style={{
            color: showAllMissions ? "#1AA1CE" : textColor,
            background: "transparent",
            borderRadius: "4px",
            border: "1px solid rgba(26, 161, 206, 0.4)",
          }}
        >
          {showAllMissions ? "▲ HIDE" : "▼ ALL MISSIONS"}
        </button>

        {showAllMissions && (
          <div className="flex flex-col gap-1 mt-2 max-h-32 overflow-y-auto">
            {availableMissions.map((m) => {
              const isActive = m.id === selectedMissionId;
              const locked = playerPower < m.recommendedPower * 0.7;
              return (
                <button
                  key={m.id}
                  onClick={() => !locked && selectMission(m.id)}
                  disabled={locked}
                  className="text-left p-1.5 transition-all"
                  style={{
                    background: isActive ? "rgba(26, 161, 206, 0.2)" : "transparent",
                    borderRadius: "4px",
                    border: isActive ? "1px solid #1AA1CE" : "1px solid rgba(26, 161, 206, 0.2)",
                    opacity: locked ? 0.4 : 1,
                  }}
                >
                  <div className="font-display text-[10px]" style={{ color: isActive ? "#1AA1CE" : textColor }}>{m.displayName}</div>
                  <div className="text-[8px]" style={{ color: textColor, opacity: 0.6 }}>
                    {m.type.toUpperCase()} • PWR {m.recommendedPower}{locked && " 🔒"}
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </div>

      {/* === NAV (gauche, sous mission card) === */}
      <nav className="absolute flex flex-col gap-1.5" style={{ bottom: "14%", left: "3%", width: "30%", zIndex: 10 }}>
        <span className="text-[8px] font-display tracking-wider px-1" style={{ color: textColor, opacity: 0.5 }}>MENU</span>
        {[
          { label: t(language, "lobby.missions"), screen: "mission_select" as const, active: true },
          { label: t(language, "lobby.loadout"), screen: "loadout" as const },
          { label: t(language, "lobby.shop"), screen: "armory" as const },
        ].map((item) => (
          <button
            key={item.label}
            onClick={() => item.screen !== "mission_select" && setScreen(item.screen)}
            className="text-[10px] font-display tracking-wider py-2 px-3 text-left transition-all"
            style={{
              color: item.active ? "#1AA1CE" : textColor,
              background: item.active ? "rgba(26, 161, 206, 0.15)" : "transparent",
              borderRadius: "6px",
              border: item.active ? "1px solid rgba(26, 161, 206, 0.6)" : "1px solid rgba(26, 161, 206, 0.25)",
              boxShadow: item.active ? "0 0 8px rgba(26, 161, 206, 0.2)" : "none",
            }}
          >
            {item.label}
          </button>
        ))}
      </nav>

      {/* === CHARACTER (centre) === */}
      <div className="absolute flex flex-col items-center justify-center" style={{ top: "14%", bottom: "14%", left: "33%", right: "33%", zIndex: 5 }}>
        {/* Halo de fond */}
        <div className="absolute inset-0 flex items-center justify-center pointer-events-none" style={{ background: `radial-gradient(ellipse at center, ${agent.themeColor}22 0%, transparent 60%)` }} />

        {/* Nom + classe au-dessus */}
        <div className="relative z-10 text-center mb-1">
          <div className="text-[9px] font-display tracking-[0.3em]" style={{ color: textColor, opacity: 0.7 }}>
            {t(language, "lobby.class")}: <span style={{ color: agent.themeColor }}>{agent.class.toUpperCase()}</span>
          </div>
          <div className="font-display text-2xl" style={{ color: "#FFFFFF", textShadow: `0 0 14px ${agent.themeColor}88` }}>
            {agent.displayName}
          </div>
        </div>

        {/* Avatar */}
        <div className="relative z-10 w-28 md:w-40 flex-1 max-h-[70%]">
          <AgentAvatar agent={agent} />
        </div>

        {/* Arme équipée en bas du personnage */}
        <div className="relative z-10 mt-1 w-full max-w-[200px]">
          <div
            className="p-2"
            style={{
              background: "rgba(10, 20, 35, 0.7)",
              borderRadius: "6px",
              border: "1px solid rgba(26, 161, 206, 0.3)",
              boxShadow: "0 0 8px rgba(26, 161, 206, 0.1)",
            }}
          >
            <div className="text-[8px] font-display tracking-wider" style={{ color: textColor, opacity: 0.6 }}>PRIMARY WEAPON</div>
            <div className="font-display text-xs" style={{ color: "#1AA1CE" }}>{equipped.primary.displayName}</div>
            <div className="flex justify-between text-[8px] mt-1" style={{ color: textColor }}>
              <span>PWR {equipped.primary.power}</span>
              <span>DMG {equipped.primary.damage}%</span>
              <span>FR {equipped.primary.fireRate}%</span>
            </div>
          </div>
        </div>
      </div>

      {/* === STATS (haut-droite → bas-droite) === */}
      <div
        className="absolute"
        style={{
          top: "14%",
          right: "3%",
          width: "30%",
          zIndex: 10,
          background: "rgba(10, 20, 35, 0.8)",
          borderRadius: "10px",
          border: "1px solid rgba(26, 161, 206, 0.5)",
          boxShadow: "0 0 16px rgba(26, 161, 206, 0.25), inset 0 0 12px rgba(26, 161, 206, 0.08)",
          padding: "12px",
        }}
      >
        <div className="text-[9px] font-display tracking-wider" style={{ color: textColor, opacity: 0.7 }}>
          {t(language, "lobby.class")}: <span style={{ color: agent.themeColor }}>{agent.class.toUpperCase()}</span>
        </div>
        <div className="font-display text-xl" style={{ color: "#FFFFFF" }}>{agent.displayName}</div>
        <div className="text-[10px] font-display" style={{ color: textColor }}>
          {t(language, "lobby.level")} <span style={{ color: "#6CF42E" }}>{playerLevel}</span>
        </div>

        {/* Power score avec barre rouge */}
        <div className="mt-3">
          <div className="flex justify-between text-[9px] font-display mb-1">
            <span style={{ color: textColor, opacity: 0.7 }}>{t(language, "lobby.powerScore")}</span>
            <span style={{ color: "#FFFFFF" }}>{playerPower}</span>
          </div>
          <div className="relative h-2 rounded-full overflow-hidden" style={{ background: "rgba(40, 50, 65, 0.6)" }}>
            <div
              className="absolute inset-y-0 left-0 rounded-full"
              style={{
                width: `${Math.min(100, (playerPower / 5000) * 100)}%`,
                background: "linear-gradient(to right, #FE0022, #FF5566)",
                boxShadow: "0 0 6px rgba(254, 0, 34, 0.6)",
              }}
            />
          </div>
        </div>

        {/* Stats détaillées */}
        <div className="mt-3 space-y-2">
          <StatBar label={t(language, "hud.health")} value={agent.baseHealth} max={5000} color="#6CF42E" />
          <StatBar label={t(language, "hud.armor")} value={agent.baseShield} max={5000} color="#1AA1CE" />
          <StatBar label="SPEED" value={agent.baseSpeed * 100} max={130} color="#FF8C00" suffix="%" />
        </div>

        {/* Compétences */}
        <div className="mt-3 pt-2" style={{ borderTop: "1px solid rgba(26, 161, 206, 0.2)" }}>
          <div className="text-[8px] font-display tracking-wider mb-1" style={{ color: "#1AA1CE" }}>SKILLS</div>
          {agent.abilities.slice(0, 2).map((ab) => (
            <div key={ab.name} className="mb-1">
              <div className="flex justify-between text-[9px]">
                <span style={{ color: "#FFFFFF" }} className="font-display">{ab.name}</span>
                <span style={{ color: textColor, opacity: 0.6 }}>{ab.cooldown > 0 ? `${ab.cooldown}s` : "ULT"}</span>
              </div>
            </div>
          ))}
        </div>

        {/* Mission sélectionnée */}
        <div className="mt-2 pt-2" style={{ borderTop: "1px solid rgba(26, 161, 206, 0.2)" }}>
          <div className="text-[8px] font-display tracking-wider" style={{ color: "#FFE735" }}>SELECTED MISSION</div>
          <div className="font-display text-xs mt-0.5" style={{ color: "#FFFFFF" }}>{selectedMission.displayName}</div>
          <div className="text-[8px]" style={{ color: textColor, opacity: 0.6 }}>
            {selectedMission.waves.length} waves • {selectedMission.objectives.length} objectives
          </div>
        </div>
      </div>

      {/* === PLAY BUTTON (bas-droite) === */}
      <button
        onClick={() => startMission(selectedMission.id)}
        className="absolute font-display uppercase tracking-wider transition-all duration-150 select-none flex items-center gap-2"
        style={{
          bottom: "14%",
          right: "3%",
          zIndex: 15,
          fontSize: "clamp(0.9rem, 2.2vw, 1.3rem)",
          padding: "14px 40px",
          background: "#1AA1CE",
          color: "#FFFFFF",
          borderRadius: "10px",
          border: "1px solid #4FC8E8",
          boxShadow: "0 0 20px rgba(26, 161, 206, 0.6), inset 0 0 10px rgba(255,255,255,0.15)",
          animation: "k5-pulse-cyan 2.5s ease-in-out infinite",
        }}
      >
        <Zap className="w-4 h-4" />
        {t(language, "lobby.play")}
      </button>
    </div>
  );
}

/* Composant StatBar local (barre simple arrondie) */
function StatBar({ label, value, max, color, suffix = "" }: { label: string; value: number; max: number; color: string; suffix?: string }) {
  const pct = Math.min(100, (value / max) * 100);
  return (
    <div>
      <div className="flex justify-between text-[9px] mb-0.5">
        <span style={{ color: textColor, opacity: 0.7 }}>{label}</span>
        <span style={{ color: "#FFFFFF" }}>{Math.round(value)}{suffix}</span>
      </div>
      <div className="relative h-1.5 rounded-full overflow-hidden" style={{ background: "rgba(40, 50, 65, 0.6)" }}>
        <div
          className="absolute inset-y-0 left-0 rounded-full"
          style={{ width: `${pct}%`, background: color, boxShadow: `0 0 4px ${color}99` }}
        />
      </div>
    </div>
  );
}
