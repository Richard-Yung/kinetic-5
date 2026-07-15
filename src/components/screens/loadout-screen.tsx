"use client";

/**
 * KINETICS 5 — Loadout / Armory (PDF page 4-5)
 * Tabs: AGENTS / PRIMARY / SECONDARY / TACTICAL
 * Cartes agents (VULCAN/XEN/JOLT/XANO) avec états locked/unlocked
 * Armes avec stats segmentées, rendu SVG, barre de progression
 */

import { useState } from "react";
import { KButton, KCard, KRarityBadge, KPanel, KProgressBar } from "@/components/kinetics/ui";
import { AgentAvatar, WeaponRender } from "@/components/kinetics/visuals";
import { useGameStore, useSelectedAgent, useEquippedWeapons } from "@/store/game-store";
import { t } from "@/lib/i18n";
import {
  AGENTS,
  WEAPONS,
  getWeaponsByCategory,
  type WeaponCategory,
} from "@/lib/kinetics-data";
import { ChevronLeft, Lock } from "lucide-react";

type Tab = "agents" | "primary" | "secondary" | "tactical";

export function LoadoutScreen() {
  const { language, setScreen, selectAgent, equipWeapon, playerLevel } = useGameStore();
  const agent = useSelectedAgent();
  const equipped = useEquippedWeapons();
  const [tab, setTab] = useState<Tab>("agents");
  const [selectedWeaponId, setSelectedWeaponId] = useState<string>(equipped.primary.id);

  const tabs: { id: Tab; label: string }[] = [
    { id: "agents", label: t(language, "loadout.agents") },
    { id: "primary", label: t(language, "loadout.primary") },
    { id: "secondary", label: t(language, "loadout.secondary") },
    { id: "tactical", label: t(language, "loadout.tactical") },
  ];

  const currentWeapons = tab === "agents" ? [] : getWeaponsByCategory(tab as WeaponCategory);
  const selectedWeapon = WEAPONS.find((w) => w.id === selectedWeaponId) ?? currentWeapons[0];
  const equippedId = tab === "primary" ? equipped.primary.id : tab === "secondary" ? equipped.secondary.id : tab === "tactical" ? equipped.tactical.id : "";

  return (
    <div className="relative min-h-screen w-full overflow-hidden flex flex-col bg-k5-deep-space">
      <div className="absolute inset-0 k5-grid-bg opacity-20" />

      {/* Header */}
      <header className="relative z-10 flex items-center justify-between px-4 py-3 safe-top border-b border-k5-border/50 bg-k5-panel/40">
        <div className="flex items-center gap-3">
          <KButton variant="ghost" size="sm" onClick={() => setScreen("lobby")}>
            <ChevronLeft className="w-4 h-4" />
          </KButton>
          <span className="font-display text-sm text-k5-cyan tracking-wider">
            {t(language, "loadout.title")}
          </span>
        </div>
        <KButton variant="primary" size="sm">{t(language, "lobby.save")}</KButton>
      </header>

      {/* Tabs */}
      <div className="relative z-10 flex gap-1 px-4 py-2 border-b border-k5-border/50 bg-k5-panel/30">
        {tabs.map((tb) => (
          <button
            key={tb.id}
            onClick={() => {
              setTab(tb.id);
              if (tb.id === "primary") setSelectedWeaponId(equipped.primary.id);
              else if (tb.id === "secondary") setSelectedWeaponId(equipped.secondary.id);
              else if (tb.id === "tactical") setSelectedWeaponId(equipped.tactical.id);
            }}
            className={`px-4 py-2 text-xs font-display tracking-wider transition-all border-b-2 ${
              tab === tb.id
                ? "text-k5-cyan border-k5-cyan"
                : "text-k5-muted border-transparent hover:text-white"
            }`}
          >
            {tb.label}
          </button>
        ))}
      </div>

      {/* Corps */}
      <main className="relative z-10 flex-1 grid grid-cols-1 md:grid-cols-[280px_1fr_280px] gap-3 p-3 overflow-hidden">
        {/* Gauche — liste agents/armes */}
        <aside className="overflow-y-auto max-h-full">
          {tab === "agents" ? (
            <div className="grid grid-cols-2 gap-2">
              {AGENTS.map((a) => {
                const locked = playerLevel < a.unlockLevel;
                const selected = a.id === agent.id;
                return (
                  <KCard
                    key={a.id}
                    variant={locked ? "locked" : selected ? "selected" : "default"}
                    accentColor={a.themeColor}
                    onClick={() => !locked && selectAgent(a.id)}
                  >
                    <div className="p-2 flex flex-col items-center">
                      <div className="w-full aspect-[3/4]">
                        <AgentAvatar agent={a} />
                      </div>
                      <div className="text-center mt-1">
                        <div className="font-display text-xs text-white">{a.displayName}</div>
                        <div className="text-[9px] uppercase" style={{ color: a.themeColor }}>{a.class}</div>
                      </div>
                      {locked && (
                        <div className="text-[9px] text-k5-red mt-1 flex items-center gap-1">
                          <Lock className="w-2 h-2" /> LVL {a.unlockLevel}
                        </div>
                      )}
                    </div>
                  </KCard>
                );
              })}
            </div>
          ) : (
            <div className="flex flex-col gap-2">
              {currentWeapons.map((w) => {
                const locked = playerLevel < w.unlockLevel;
                const equipped = w.id === equippedId;
                return (
                  <KCard
                    key={w.id}
                    variant={locked ? "locked" : w.id === selectedWeaponId ? "selected" : "default"}
                    onClick={() => !locked && setSelectedWeaponId(w.id)}
                  >
                    <div className="p-2 flex items-center gap-2">
                      <div className="w-20 h-10 flex-shrink-0">
                        <WeaponRender weapon={w} />
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-1">
                          <span className="font-display text-xs text-white truncate">{w.displayName}</span>
                          {equipped && <span className="text-[8px] text-k5-green font-display">✓</span>}
                        </div>
                        <KRarityBadge rarity={w.rarity} />
                        {locked && (
                          <div className="text-[9px] text-k5-red flex items-center gap-1 mt-1">
                            <Lock className="w-2 h-2" /> LVL {w.unlockLevel}
                          </div>
                        )}
                      </div>
                    </div>
                  </KCard>
                );
              })}
            </div>
          )}
        </aside>

        {/* Centre — aperçu détaillé */}
        <section className="flex flex-col items-center justify-center overflow-hidden">
          {tab === "agents" ? (
            <div className="w-full max-w-md flex flex-col items-center">
              <div className="text-[10px] font-display tracking-wider text-k5-muted mb-1">
                {t(language, "lobby.class")}: <span style={{ color: agent.themeColor }}>{agent.class.toUpperCase()}</span>
              </div>
              <div className="font-display text-3xl text-white k5-text-glow-cyan mb-2">{agent.displayName}</div>
              <div className="w-56 md:w-72 flex-1 max-h-[50vh]">
                <AgentAvatar agent={agent} />
              </div>
              <p className="text-xs text-k5-muted text-center mt-2 max-w-sm leading-relaxed">
                {agent.description}
              </p>
            </div>
          ) : selectedWeapon ? (
            <div className="w-full max-w-lg flex flex-col items-center">
              <div className="font-display text-2xl text-white k5-text-glow-cyan mb-2">
                {selectedWeapon.displayName}
              </div>
              <div className="w-full h-32 flex items-center justify-center">
                <WeaponRender weapon={selectedWeapon} className="w-full max-w-md" />
              </div>
              <div className="flex gap-2 mt-2">
                <KRarityBadge rarity={selectedWeapon.rarity} />
                <span className="text-[10px] font-display px-2 py-0.5 rounded-sm border border-k5-border text-k5-cyan">
                  {selectedWeapon.element.toUpperCase()}
                </span>
              </div>
              <div className="grid grid-cols-2 gap-x-8 gap-y-1 mt-3 text-[10px]">
                <div className="flex justify-between"><span className="text-k5-muted">{t(language, "loadout.power")}</span><span className="font-display text-white">{selectedWeapon.power}</span></div>
                <div className="flex justify-between"><span className="text-k5-muted">{t(language, "loadout.reload")}</span><span className="font-display text-white">{selectedWeapon.reloadTime}s</span></div>
                <div className="flex justify-between"><span className="text-k5-muted">MAG</span><span className="font-display text-white">{selectedWeapon.magazineSize}</span></div>
                <div className="flex justify-between"><span className="text-k5-muted">RANGE</span><span className="font-display text-white">{selectedWeapon.range}m</span></div>
              </div>
            </div>
          ) : null}
        </section>

        {/* Droite — stats détaillées */}
        <aside className="overflow-y-auto">
          {tab === "agents" ? (
            <KPanel>
              <div className="text-[10px] font-display tracking-wider text-k5-cyan mb-3">
                {t(language, "lobby.powerScore")}
              </div>
              <div className="font-display text-2xl text-white text-center mb-2">{agent.basePower}</div>
              <KProgressBar value={agent.basePower} max={3000} color="cyan" segments={20} showValue={false} height="sm" />
              <div className="mt-3 space-y-2">
                <KProgressBar label={t(language, "hud.health")} value={agent.baseHealth} max={5000} color="green" valueText={`${agent.baseHealth}`} segments={15} />
                <KProgressBar label={t(language, "hud.armor")} value={agent.baseShield} max={5000} color="cyan" valueText={`${agent.baseShield}`} segments={15} />
                <KProgressBar label="SPEED" value={agent.baseSpeed * 100} max={130} color="orange" valueText={`${Math.round(agent.baseSpeed * 100)}%`} segments={15} />
              </div>
              <div className="mt-4 pt-3 border-t border-k5-border/50">
                <div className="text-[10px] font-display tracking-wider text-k5-cyan mb-2">COMPÉTENCES</div>
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
          ) : selectedWeapon ? (
            <KPanel>
              <div className="text-[10px] font-display tracking-wider text-k5-cyan mb-3">
                {t(language, "loadout.power")}
              </div>
              <div className="font-display text-2xl text-white text-center mb-2">{selectedWeapon.power}</div>
              <div className="space-y-3">
                <KProgressBar label={t(language, "loadout.damage")} value={selectedWeapon.damage} max={100} color="green" valueText={`${selectedWeapon.damage}%`} segments={20} />
                <KProgressBar label={t(language, "loadout.fireRate")} value={selectedWeapon.fireRate} max={100} color="yellow" valueText={`${selectedWeapon.fireRate}%`} segments={20} />
                <KProgressBar label={t(language, "loadout.accuracy")} value={selectedWeapon.accuracy} max={100} color="cyan" valueText={`${selectedWeapon.accuracy}%`} segments={20} />
                <KProgressBar label={t(language, "loadout.stability")} value={selectedWeapon.stability} max={100} color="purple" valueText={`${selectedWeapon.stability}%`} segments={20} />
                {selectedWeapon.explosionRadius !== undefined && (
                  <KProgressBar label={t(language, "loadout.explosionRadius")} value={selectedWeapon.explosionRadius} max={100} color="orange" valueText={`${selectedWeapon.explosionRadius}%`} segments={20} />
                )}
                {selectedWeapon.fuseTime !== undefined && (
                  <div className="flex justify-between text-[10px] pt-1">
                    <span className="text-k5-muted">{t(language, "loadout.fuseTime")}</span>
                    <span className="font-display text-white">{selectedWeapon.fuseTime}s</span>
                  </div>
                )}
              </div>
              {playerLevel >= selectedWeapon.unlockLevel && (
                <KButton
                  variant={selectedWeapon.id === equippedId ? "tertiary" : "primary"}
                  size="md"
                  className="w-full mt-4"
                  disabled={selectedWeapon.id === equippedId}
                  onClick={() => equipWeapon(tab as "primary" | "secondary" | "tactical", selectedWeapon.id)}
                >
                  {selectedWeapon.id === equippedId ? t(language, "loadout.equipped") : t(language, "loadout.equip")}
                </KButton>
              )}
            </KPanel>
          ) : null}
        </aside>
      </main>
    </div>
  );
}
