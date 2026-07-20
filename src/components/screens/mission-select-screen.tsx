"use client";

/**
 * KINETICS 5 — Page de sélection de mission (avant le loading)
 *
 * Layout EXACT d'après l'image de référence (3 panneaux + top bar) :
 * - Top bar : back + MAIN LOBBY + currency (XP/CR) + settings
 * - Panneau gauche (25%) : carte mission (CURRENT MISSION) + nav (MISSIONS/LOADOUT/SHOP)
 * - Panneau centre (50%) : personnage (VULCAN) + arme
 * - Panneau droite (25%) : stats agent (CLASS, nom, LEVEL, POWER SCORE barre)
 * - Bouton PLAY bas-centre
 *
 * Détails cyberpunk :
 * - Texte grisâtre #C8CDD0 (pas blanc pur)
 * - Logo deux tons (KINETICS bleu + 5 gris)
 * - Bordures coupées (clip-path) sur cartes/boutons
 * - Lignes fines gris-blanc décoratives
 * - Overlay sombre sur background
 * - Grille subtile
 * - La carte mission change quand on sélectionne une autre mission
 */

import { KButton, KProgressBar } from "@/components/kinetics/ui";
import { AgentAvatar } from "@/components/kinetics/visuals";
import { useGameStore, useSelectedAgent, useEquippedWeapons } from "@/store/game-store";
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

  // Missions disponibles (débloquées selon power)
  const availableMissions = MISSIONS.filter((m) => playerPower >= m.recommendedPower * 0.7);

  return (
    <div className="relative w-full h-screen min-h-[400px] overflow-hidden">
      {/* Background vaisseau */}
      <img
        src="/kinetics/bg-everspace.jpg"
        alt=""
        className="absolute inset-0 w-full h-full object-cover"
        aria-hidden
      />
      {/* Overlay sombre */}
      <div className="absolute inset-0" style={{ background: "rgba(5, 8, 16, 0.75)" }} />
      {/* Teinte bleue froide */}
      <div className="absolute inset-0" style={{ background: "rgba(20, 40, 80, 0.15)", mixBlendMode: "color" }} />
      {/* Grille subtile */}
      <div
        className="absolute inset-0"
        style={{
          backgroundImage: "linear-gradient(rgba(0, 206, 209, 0.05) 1px, transparent 1px), linear-gradient(90deg, rgba(0, 206, 209, 0.05) 1px, transparent 1px)",
          backgroundSize: "40px 40px",
        }}
      />

      {/* Lignes fines décoratives */}
      <div className="absolute left-0 right-0" style={{ top: "10%", height: "1px", background: "linear-gradient(to right, transparent, rgba(200,205,208,0.2) 20%, rgba(200,205,208,0.2) 80%, transparent)", zIndex: 2 }} />
      <div className="absolute left-0 right-0" style={{ bottom: "10%", height: "1px", background: "linear-gradient(to right, transparent, rgba(200,205,208,0.2) 20%, rgba(200,205,208,0.2) 80%, transparent)", zIndex: 2 }} />

      {/* === TOP BAR === */}
      <header className="absolute top-0 left-0 right-0 flex items-center justify-between px-4 py-2.5 z-20" style={{ background: "rgba(5, 8, 16, 0.6)", borderBottom: "1px solid rgba(26, 161, 206, 0.3)" }}>
        <div className="flex items-center gap-3">
          <button onClick={() => setScreen("start")} className="transition-colors" style={{ color: "#1AA1CE" }}>
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="font-display text-xs tracking-wider" style={{ color: textColor }}>{t(language, "lobby.title")}</span>
          {/* Logo deux tons */}
          <span className="font-display text-sm ml-2">
            <span style={{ color: "#1AA1CE" }}>KINETICS</span>
            <span style={{ color: textColor }}>·</span>
            <span style={{ color: textColor }}>5</span>
          </span>
        </div>
        <div className="flex items-center gap-4 text-[10px] font-display">
          <div className="flex items-center gap-1">
            <span style={{ color: "#6CF42E" }}>XP</span>
            <span style={{ color: textColor }}>{formatNumber(playerXp)}</span>
          </div>
          <div className="flex items-center gap-1">
            <span style={{ color: "#FFE735" }}>CR</span>
            <span style={{ color: textColor }}>{formatNumber(playerCr)}</span>
          </div>
          <button onClick={() => setScreen("settings")} style={{ color: textColor }}>
            <Settings className="w-4 h-4" />
          </button>
        </div>
      </header>

      {/* === CORPS : 3 PANNEAUX === */}
      <main className="absolute inset-0 flex" style={{ paddingTop: "44px", paddingBottom: "70px" }}>

        {/* === PANNEAU GAUCHE : mission card + nav === */}
        <aside className="flex flex-col gap-2 p-3" style={{ width: "26%" }}>
          {/* Carte mission courante */}
          <div
            className="p-3"
            style={{
              background: "rgba(10, 20, 35, 0.7)",
              clipPath: "polygon(0 0, calc(100% - 10px) 0, 100% 10px, 100% 100%, 10px 100%, 0 calc(100% - 10px))",
              border: "1px solid rgba(26, 161, 206, 0.4)",
              boxShadow: "0 0 12px rgba(26, 161, 206, 0.15)",
            }}
          >
            <div className="text-[9px] font-display tracking-wider mb-1" style={{ color: "#1AA1CE" }}>
              {t(language, "lobby.currentMission")}
            </div>
            <div className="h-px mb-2" style={{ background: "linear-gradient(to right, #1AA1CE, transparent)" }} />

            {/* Aperçu visuel mission (mini carte) */}
            <div
              className="w-full mb-2 flex items-center justify-center"
              style={{
                height: "60px",
                background: `linear-gradient(135deg, ${selectedMission.environment.shipType === "Cargo" ? "rgba(40, 60, 30, 0.5)" : "rgba(30, 40, 60, 0.5)"}, rgba(10, 15, 25, 0.8))`,
                clipPath: "polygon(0 0, calc(100% - 6px) 0, 100% 6px, 100% 100%, 6px 100%, 0 calc(100% - 6px))",
                border: "1px solid rgba(26, 161, 206, 0.3)",
              }}
            >
              <span className="font-display text-[10px]" style={{ color: textColor }}>
                {selectedMission.region.toUpperCase()}
              </span>
            </div>

            <div className="text-[9px]" style={{ color: textColor }}>
              {t(language, "lobby.missionType")}: <span style={{ color: "#1AA1CE" }}>{selectedMission.type.toUpperCase()}</span>
            </div>
            <div className="font-display text-sm mt-0.5" style={{ color: "#FFFFFF" }}>
              {selectedMission.displayName}
            </div>
            <div className="text-[8px] mt-0.5" style={{ color: textColor, opacity: 0.7 }}>
              {selectedMission.shipName}
            </div>

            {/* Récompenses */}
            <div className="flex justify-between mt-2 text-[10px] font-display">
              <span style={{ color: "#6CF42E" }}>XP {Math.floor(selectedMission.rewards.xp / 1000)}K</span>
              <span style={{ color: "#FFE735" }}>CR {Math.floor(selectedMission.rewards.cr / 1000)}K</span>
            </div>

            {/* Power recommandé */}
            <div className="text-[8px] mt-1" style={{ color: textColor, opacity: 0.6 }}>
              REC. POWER: {selectedMission.recommendedPower}
            </div>
          </div>

          {/* Bouton voir toutes les missions */}
          <button
            onClick={() => setShowAllMissions(!showAllMissions)}
            className="text-[9px] font-display tracking-wider py-1.5 px-2 transition-all"
            style={{
              color: showAllMissions ? "#1AA1CE" : textColor,
              background: "rgba(10, 20, 35, 0.5)",
              clipPath: "polygon(0 0, calc(100% - 6px) 0, 100% 6px, 100% 100%, 6px 100%, 0 calc(100% - 6px))",
              border: "1px solid rgba(26, 161, 206, 0.3)",
            }}
          >
            {showAllMissions ? "▲ HIDE" : "▼ ALL MISSIONS"}
          </button>

          {/* Liste missions (si affichée) */}
          {showAllMissions && (
            <div className="flex flex-col gap-1 max-h-[40%] overflow-y-auto">
              {availableMissions.map((m) => {
                const isActive = m.id === selectedMissionId;
                const locked = playerPower < m.recommendedPower * 0.7;
                return (
                  <button
                    key={m.id}
                    onClick={() => !locked && selectMission(m.id)}
                    disabled={locked}
                    className="text-left p-2 transition-all"
                    style={{
                      background: isActive ? "rgba(26, 161, 206, 0.2)" : "rgba(10, 20, 35, 0.5)",
                      clipPath: "polygon(0 0, calc(100% - 5px) 0, 100% 5px, 100% 100%, 5px 100%, 0 calc(100% - 5px))",
                      border: isActive ? "1px solid #1AA1CE" : "1px solid rgba(26, 161, 206, 0.2)",
                      opacity: locked ? 0.4 : 1,
                      cursor: locked ? "not-allowed" : "pointer",
                    }}
                  >
                    <div className="font-display text-[10px]" style={{ color: isActive ? "#1AA1CE" : textColor }}>
                      {m.displayName}
                    </div>
                    <div className="text-[8px]" style={{ color: textColor, opacity: 0.6 }}>
                      {m.type.toUpperCase()} • PWR {m.recommendedPower}
                      {locked && " 🔒"}
                    </div>
                  </button>
                );
              })}
            </div>
          )}

          {/* Navigation */}
          <div className="mt-auto flex flex-col gap-1">
            <span className="text-[8px] font-display tracking-wider px-1" style={{ color: textColor, opacity: 0.5 }}>MENU</span>
            {[
              { label: t(language, "lobby.missions"), screen: "mission_select" as const, active: true },
              { label: t(language, "lobby.loadout"), screen: "loadout" as const },
              { label: t(language, "lobby.shop"), screen: "armory" as const },
            ].map((item) => (
              <button
                key={item.label}
                onClick={() => item.screen !== "mission_select" && setScreen(item.screen)}
                className="text-[10px] font-display tracking-wider py-2 px-2 text-left transition-all"
                style={{
                  color: item.active ? "#1AA1CE" : textColor,
                  background: item.active ? "rgba(26, 161, 206, 0.15)" : "rgba(10, 20, 35, 0.5)",
                  clipPath: "polygon(0 0, calc(100% - 6px) 0, 100% 6px, 100% 100%, 6px 100%, 0 calc(100% - 6px))",
                  border: item.active ? "1px solid rgba(26, 161, 206, 0.6)" : "1px solid rgba(26, 161, 206, 0.2)",
                }}
              >
                {item.label}
              </button>
            ))}
          </div>
        </aside>

        {/* === PANNEAU CENTRE : personnage + arme === */}
        <section className="relative flex flex-col items-center justify-end" style={{ width: "48%" }}>
          {/* Halo de fond derrière le personnage */}
          <div
            className="absolute inset-0 flex items-center justify-center pointer-events-none"
            style={{ background: `radial-gradient(ellipse at center, ${agent.themeColor}22 0%, transparent 60%)` }}
          />

          {/* Nom agent + classe au-dessus */}
          <div className="relative z-10 text-center mb-1">
            <div className="text-[9px] font-display tracking-[0.3em]" style={{ color: textColor, opacity: 0.7 }}>
              {t(language, "lobby.class")}: <span style={{ color: agent.themeColor }}>{agent.class.toUpperCase()}</span>
            </div>
            <div className="font-display text-2xl" style={{ color: "#FFFFFF", textShadow: `0 0 14px ${agent.themeColor}88` }}>
              {agent.displayName}
            </div>
          </div>

          {/* Avatar personnage */}
          <div className="relative z-10 w-32 md:w-44 flex-1 max-h-[65%]">
            <AgentAvatar agent={agent} />
          </div>

          {/* Arme équipée en bas du personnage */}
          <div className="relative z-10 mt-1 mb-2 w-full max-w-xs">
            <div
              className="p-2"
              style={{
                background: "rgba(10, 20, 35, 0.6)",
                clipPath: "polygon(0 0, calc(100% - 6px) 0, 100% 6px, 100% 100%, 6px 100%, 0 calc(100% - 6px))",
                border: "1px solid rgba(26, 161, 206, 0.3)",
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
        </section>

        {/* === PANNEAU DROITE : stats === */}
        <aside className="flex flex-col gap-2 p-3" style={{ width: "26%" }}>
          <div
            className="p-3"
            style={{
              background: "rgba(10, 20, 35, 0.7)",
              clipPath: "polygon(0 0, calc(100% - 10px) 0, 100% 10px, 100% 100%, 10px 100%, 0 calc(100% - 10px))",
              border: "1px solid rgba(26, 161, 206, 0.4)",
            }}
          >
            <div className="text-[9px] font-display tracking-wider" style={{ color: textColor, opacity: 0.7 }}>
              {t(language, "lobby.class")}: <span style={{ color: agent.themeColor }}>{agent.class.toUpperCase()}</span>
            </div>
            <div className="font-display text-xl" style={{ color: "#FFFFFF" }}>{agent.displayName}</div>
            <div className="text-[10px] font-display" style={{ color: textColor }}>
              {t(language, "lobby.level")} <span style={{ color: "#6CF42E" }}>{playerLevel}</span>
            </div>

            {/* Power score avec barre */}
            <div className="mt-3">
              <div className="flex justify-between text-[9px] font-display mb-1">
                <span style={{ color: textColor, opacity: 0.7 }}>{t(language, "lobby.powerScore")}</span>
                <span style={{ color: "#FFFFFF" }}>{playerPower}</span>
              </div>
              {/* Barre power score (style référence : rouge) */}
              <div className="relative h-2" style={{ background: "rgba(40, 50, 65, 0.6)", clipPath: "polygon(0 0, calc(100% - 4px) 0, 100% 4px, 100% 100%, 4px 100%, 0 calc(100% - 4px))" }}>
                <div
                  className="absolute inset-y-0 left-0"
                  style={{
                    width: `${Math.min(100, (playerPower / 5000) * 100)}%`,
                    background: "linear-gradient(to right, #FE0022, #FF5566)",
                    boxShadow: "0 0 6px rgba(254, 0, 34, 0.6)",
                  }}
                />
              </div>
            </div>

            {/* Stats détaillées (barres segmentées) */}
            <div className="mt-3 space-y-2">
              <KProgressBar label={t(language, "hud.health")} value={agent.baseHealth} max={5000} color="green" valueText={`${agent.baseHealth}`} segments={15} />
              <KProgressBar label={t(language, "hud.armor")} value={agent.baseShield} max={5000} color="cyan" valueText={`${agent.baseShield}`} segments={15} />
              <KProgressBar label="SPEED" value={agent.baseSpeed * 100} max={130} color="orange" valueText={`${Math.round(agent.baseSpeed * 100)}%`} segments={15} />
            </div>

            {/* Compétences */}
            <div className="mt-3 pt-2" style={{ borderTop: "1px solid rgba(26, 161, 206, 0.2)" }}>
              <div className="text-[8px] font-display tracking-wider mb-1" style={{ color: "#1AA1CE" }}>COMPÉTENCES</div>
              {agent.abilities.slice(0, 2).map((ab) => (
                <div key={ab.name} className="mb-1">
                  <div className="flex justify-between text-[9px]">
                    <span style={{ color: "#FFFFFF" }} className="font-display">{ab.name}</span>
                    <span style={{ color: textColor, opacity: 0.6 }}>{ab.cooldown > 0 ? `${ab.cooldown}s` : "ULT"}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Indicateur mission sélectionnée */}
          <div
            className="p-2"
            style={{
              background: "rgba(10, 20, 35, 0.6)",
              clipPath: "polygon(0 0, calc(100% - 6px) 0, 100% 6px, 100% 100%, 6px 100%, 0 calc(100% - 6px))",
              border: "1px solid rgba(255, 231, 53, 0.3)",
            }}
          >
            <div className="text-[8px] font-display tracking-wider" style={{ color: "#FFE735" }}>SELECTED MISSION</div>
            <div className="font-display text-xs mt-0.5" style={{ color: "#FFFFFF" }}>{selectedMission.displayName}</div>
            <div className="text-[8px]" style={{ color: textColor, opacity: 0.6 }}>
              {selectedMission.waves.length} waves • {selectedMission.objectives.length} objectives
            </div>
          </div>
        </aside>
      </main>

      {/* === BOUTON PLAY bas-centre === */}
      <footer className="absolute bottom-0 left-0 right-0 flex justify-center items-center py-3 z-20" style={{ background: "rgba(5, 8, 16, 0.6)", borderTop: "1px solid rgba(26, 161, 206, 0.3)" }}>
        <button
          onClick={() => startMission(selectedMission.id)}
          className="font-display uppercase tracking-wider transition-all duration-150 select-none flex items-center gap-2"
          style={{
            fontSize: "clamp(0.9rem, 2.5vw, 1.4rem)",
            padding: "12px 48px",
            background: "#1AA1CE",
            color: "#FFFFFF",
            clipPath: "polygon(0 0, calc(100% - 12px) 0, 100% 12px, 100% 100%, 12px 100%, 0 calc(100% - 12px))",
            boxShadow: "0 0 20px rgba(26, 161, 206, 0.6), inset 0 0 8px rgba(255,255,255,0.1)",
            animation: "k5-pulse-cyan 2.5s ease-in-out infinite",
          }}
        >
          <Zap className="w-4 h-4" />
          {t(language, "lobby.play")}
        </button>
      </footer>
    </div>
  );
}
