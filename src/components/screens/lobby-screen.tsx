"use client";

/**
 * KINETICS 5 — Main Lobby (PDF page 4)
 * Layout : character center, stats right, mission card left-top,
 * sidebar (MISSIONS/LOADOUT/SHOP/AGENTS), PLAY button bottom-right
 * XP/CR currency top-right
 */

import { KButton, KCard, KCurrency, KPanel, KProgressBar } from "@/components/kinetics/ui";
import { AgentAvatar, StarfieldBackground } from "@/components/kinetics/visuals";
import { useGameStore, useSelectedAgent, useSelectedMission } from "@/store/game-store";
import { t } from "@/lib/i18n";
import { Settings, ChevronLeft } from "lucide-react";

export function LobbyScreen() {
  const { language, setScreen, startMission, selectMission, playerLevel, playerXp, playerCr, playerPower } = useGameStore();
  const agent = useSelectedAgent();
  const mission = useSelectedMission();

  return (
    <div className="relative min-h-screen w-full overflow-hidden flex flex-col bg-k5-deep-space">
      <StarfieldBackground density={40} />

      {/* Header */}
      <header className="relative z-10 flex items-center justify-between px-4 py-3 safe-top border-b border-k5-border/50 bg-k5-panel/40 backdrop-blur-sm">
        <div className="flex items-center gap-3">
          <KButton variant="ghost" size="sm" onClick={() => setScreen("start")}>
            <ChevronLeft className="w-4 h-4" />
          </KButton>
          <span className="font-display text-sm text-k5-cyan tracking-wider">
            {t(language, "lobby.title")}
          </span>
        </div>
        <div className="flex items-center gap-4">
          <KCurrency type="xp" value={playerXp} />
          <KCurrency type="cr" value={playerCr} />
          <KButton variant="ghost" size="sm" onClick={() => setScreen("settings")}>
            <Settings className="w-4 h-4" />
          </KButton>
        </div>
      </header>

      {/* Corps principal */}
      <main className="relative z-10 flex-1 grid grid-cols-1 md:grid-cols-[200px_1fr_240px] gap-3 p-3 md:p-4">
        {/* Sidebar gauche — navigation */}
        <aside className="flex flex-col gap-2">
          <span className="text-[10px] font-display tracking-wider text-k5-muted px-2">
            MENU
          </span>
          {[
            { label: t(language, "lobby.missions"), screen: "lobby" as const, active: true },
            { label: t(language, "lobby.loadout"), screen: "loadout" as const },
            { label: t(language, "lobby.shop"), screen: "armory" as const },
            { label: t(language, "lobby.agents"), screen: "loadout" as const },
          ].map((item) => (
            <KButton
              key={item.label}
              variant={item.active ? "primary" : "secondary"}
              size="md"
              className="justify-start"
              onClick={() => setScreen(item.screen)}
            >
              {item.label}
            </KButton>
          ))}

          {/* Carte mission en cours */}
          <KCard variant="selected" accentColor={agent.themeColor} className="mt-2">
            <div className="p-3">
              <div className="text-[10px] font-display tracking-wider text-k5-cyan mb-1">
                {t(language, "lobby.currentMission")}
              </div>
              <div className="text-[10px] text-k5-muted mb-1">
                {t(language, "lobby.missionType")}: {mission.type.toUpperCase()}
              </div>
              <div className="font-display text-base text-white mb-2">
                {mission.displayName}
              </div>
              <div className="flex justify-between text-[10px]">
                <span className="text-k5-green">XP {Math.floor(mission.rewards.xp / 1000)}K</span>
                <span className="text-k5-yellow">CR {Math.floor(mission.rewards.cr / 1000)}K</span>
              </div>
            </div>
          </KCard>
        </aside>

        {/* Centre — personnage */}
        <section className="relative flex flex-col items-center justify-end">
          {/* Halo de fond */}
          <div
            className="absolute inset-0 flex items-center justify-center pointer-events-none"
            style={{
              background: `radial-gradient(ellipse at center, ${agent.themeColor}22 0%, transparent 60%)`,
            }}
          />
          {/* Nom agent + classe au-dessus */}
          <div className="relative z-10 text-center mb-2">
            <div className="text-[10px] font-display tracking-[0.3em] text-k5-muted">
              {t(language, "lobby.class")}: <span style={{ color: agent.themeColor }}>{agent.class.toUpperCase()}</span>
            </div>
            <div className="font-display text-4xl text-white k5-text-glow-cyan">
              {agent.displayName}
            </div>
            <div className="text-xs text-k5-muted italic mt-1">"{agent.motto}"</div>
          </div>
          {/* Avatar */}
          <div className="relative z-10 w-48 md:w-64 flex-1 max-h-[60vh]">
            <AgentAvatar agent={agent} />
          </div>
          {/* Stats sous le perso */}
          <div className="relative z-10 w-full max-w-sm mt-2 grid grid-cols-2 gap-x-4 gap-y-1">
            <div className="flex justify-between text-[10px]">
              <span className="text-k5-muted">{t(language, "lobby.level")}</span>
              <span className="font-display text-k5-green">{agent.level}</span>
            </div>
            <div className="flex justify-between text-[10px]">
              <span className="text-k5-muted">{t(language, "lobby.powerScore")}</span>
              <span className="font-display text-k5-cyan">{playerPower}</span>
            </div>
            <div className="flex justify-between text-[10px]">
              <span className="text-k5-muted">{t(language, "hud.health")}</span>
              <span className="font-display text-k5-green">{agent.baseHealth}</span>
            </div>
            <div className="flex justify-between text-[10px]">
              <span className="text-k5-muted">{t(language, "hud.armor")}</span>
              <span className="font-display text-k5-cyan">{agent.baseShield}</span>
            </div>
          </div>
        </section>

        {/* Droite — stats détaillées */}
        <aside className="flex flex-col gap-2">
          <KPanel className="flex-1">
            <div className="text-[10px] font-display tracking-wider text-k5-cyan mb-3">
              {t(language, "lobby.powerScore")}
            </div>
            <div className="font-display text-3xl text-white text-center mb-1">
              {playerPower}
            </div>
            <KProgressBar value={playerPower} max={5000} color="cyan" segments={20} height="sm" showValue={false} />

            <div className="mt-4 space-y-2">
              <KProgressBar label={t(language, "hud.health")} value={agent.baseHealth} max={5000} color="green" valueText={`${agent.baseHealth}`} segments={15} />
              <KProgressBar label={t(language, "hud.armor")} value={agent.baseShield} max={5000} color="cyan" valueText={`${agent.baseShield}`} segments={15} />
              <KProgressBar label="SPEED" value={agent.baseSpeed * 100} max={130} color="orange" valueText={`${Math.round(agent.baseSpeed * 100)}%`} segments={15} />
            </div>

            <div className="mt-4 pt-3 border-t border-k5-border/50">
              <div className="text-[10px] font-display tracking-wider text-k5-cyan mb-2">
                COMPÉTENCES
              </div>
              {agent.abilities.map((ab) => (
                <div key={ab.name} className="mb-2">
                  <div className="flex justify-between text-[10px]">
                    <span className="text-white font-display">{ab.name}</span>
                    <span className="text-k5-muted">{ab.cooldown > 0 ? `${ab.cooldown}s` : "ULT"}</span>
                  </div>
                  <p className="text-[9px] text-k5-muted leading-tight">{ab.description}</p>
                </div>
              ))}
            </div>
          </KPanel>
        </aside>
      </main>

      {/* Footer — bouton PLAY */}
      <footer className="relative z-10 px-4 py-3 safe-bottom border-t border-k5-border/50 bg-k5-panel/40 backdrop-blur-sm">
        <div className="flex items-center justify-between gap-4">
          {/* Sélecteur mission rapide */}
          <div className="flex-1 flex items-center gap-2 overflow-x-auto">
            {["shadow_fall", "neural_breach", "void_lock"].map((mid) => {
              const isActive = mid === mission.id;
              return (
                <button
                  key={mid}
                  onClick={() => selectMission(mid)}
                  className={`px-3 py-1.5 text-[10px] font-display tracking-wider rounded-sm border whitespace-nowrap transition-all ${
                    isActive
                      ? "bg-k5-cyan text-k5-deep-space border-k5-cyan"
                      : "bg-k5-panel/60 text-k5-muted border-k5-border hover:border-k5-cyan"
                  }`}
                >
                  {mid.replace("_", " ").toUpperCase()}
                </button>
              );
            })}
          </div>
          <KButton
            variant="primary"
            size="xl"
            glow
            className="k5-pulse"
            onClick={() => startMission(mission.id)}
          >
            ▶ {t(language, "lobby.play")}
          </KButton>
        </div>
      </footer>
    </div>
  );
}
