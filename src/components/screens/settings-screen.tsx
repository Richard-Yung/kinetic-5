"use client";

/**
 * KINETICS 5 — Settings (PDF page 7-8)
 * Language, Music, SFX, Difficulty (Easy/Normal/Hard), Graphics, Controls
 */

import { KButton, KPanel } from "@/components/kinetics/ui";
import { StarfieldBackground } from "@/components/kinetics/visuals";
import { useGameStore, type Language, type Difficulty } from "@/store/game-store";
import { t } from "@/lib/i18n";
import { ChevronLeft, Volume2, VolumeX, Monitor, Gamepad2, Globe, Gauge } from "lucide-react";
import { useState } from "react";

export function SettingsScreen() {
  const {
    language, setLanguage,
    difficulty, setDifficulty,
    musicVolume, sfxVolume, setVolumes,
    graphicsQuality, setGraphicsQuality,
    controlLayout, setControlLayout,
    sensitivity, setSensitivity,
    setScreen,
  } = useGameStore();
  const [localMusic, setLocalMusic] = useState(musicVolume);
  const [localSfx, setLocalSfx] = useState(sfxVolume);

  return (
    <div className="relative min-h-screen w-full overflow-hidden flex flex-col bg-k5-deep-space">
      <StarfieldBackground density={30} />

      <header className="relative z-10 flex items-center justify-between px-4 py-3 safe-top border-b border-k5-border/50 bg-k5-panel/40">
        <div className="flex items-center gap-3">
          <KButton variant="ghost" size="sm" onClick={() => setScreen("start")}>
            <ChevronLeft className="w-4 h-4" />
          </KButton>
          <span className="font-display text-sm text-k5-cyan tracking-wider">
            {t(language, "settings.title")}
          </span>
        </div>
      </header>

      <main className="relative z-10 flex-1 overflow-y-auto px-4 py-4">
        <div className="max-w-2xl mx-auto space-y-3">
          {/* Language */}
          <KPanel>
            <div className="flex items-center gap-2 mb-3">
              <Globe className="w-4 h-4 text-k5-cyan" />
              <span className="text-xs font-display tracking-wider text-k5-cyan">{t(language, "settings.language")}</span>
            </div>
            <div className="flex gap-2">
              {([
                { id: "fr", label: "FRANÇAIS", flag: "🇫🇷" },
                { id: "en", label: "ENGLISH", flag: "🇬🇧" },
              ] as { id: Language; label: string; flag: string }[]).map((l) => (
                <button
                  key={l.id}
                  onClick={() => setLanguage(l.id)}
                  className={`flex-1 px-4 py-2 text-xs font-display tracking-wider rounded-sm border-2 transition-all ${
                    language === l.id
                      ? "bg-k5-cyan text-k5-deep-space border-k5-cyan k5-glow-cyan"
                      : "bg-k5-panel/60 text-k5-muted border-k5-border hover:border-k5-cyan"
                  }`}
                >
                  <span className="mr-2">{l.flag}</span>
                  {l.label}
                </button>
              ))}
            </div>
          </KPanel>

          {/* Audio */}
          <KPanel>
            <div className="flex items-center gap-2 mb-3">
              <Volume2 className="w-4 h-4 text-k5-cyan" />
              <span className="text-xs font-display tracking-wider text-k5-cyan">AUDIO</span>
            </div>
            <div className="space-y-3">
              <div>
                <div className="flex justify-between text-[10px] mb-1">
                  <span className="text-k5-muted">{t(language, "settings.music")}</span>
                  <span className="font-display text-k5-cyan">{localMusic}%</span>
                </div>
                <input
                  type="range" min={0} max={100} value={localMusic}
                  onChange={(e) => { setLocalMusic(Number(e.target.value)); setVolumes(Number(e.target.value), localSfx); }}
                  className="w-full h-2 bg-k5-panel rounded-sm appearance-none cursor-pointer accent-k5-cyan"
                />
              </div>
              <div>
                <div className="flex justify-between text-[10px] mb-1">
                  <span className="text-k5-muted">{t(language, "settings.sfx")}</span>
                  <span className="font-display text-k5-cyan">{localSfx}%</span>
                </div>
                <input
                  type="range" min={0} max={100} value={localSfx}
                  onChange={(e) => { setLocalSfx(Number(e.target.value)); setVolumes(localMusic, Number(e.target.value)); }}
                  className="w-full h-2 bg-k5-panel rounded-sm appearance-none cursor-pointer accent-k5-cyan"
                />
              </div>
            </div>
          </KPanel>

          {/* Difficulty */}
          <KPanel>
            <div className="flex items-center gap-2 mb-3">
              <Gauge className="w-4 h-4 text-k5-cyan" />
              <span className="text-xs font-display tracking-wider text-k5-cyan">{t(language, "settings.difficulty")}</span>
            </div>
            <div className="grid grid-cols-3 gap-2">
              {([
                { id: "easy", label: t(language, "settings.easy"), color: "#6CF42E" },
                { id: "normal", label: t(language, "settings.normal"), color: "#1AA1CE" },
                { id: "hard", label: t(language, "settings.hard"), color: "#FE0022" },
              ] as { id: Difficulty; label: string; color: string }[]).map((d) => (
                <button
                  key={d.id}
                  onClick={() => setDifficulty(d.id)}
                  className={`px-3 py-3 text-xs font-display tracking-wider rounded-sm border-2 transition-all ${
                    difficulty === d.id
                      ? "text-k5-deep-space"
                      : "bg-k5-panel/60 text-k5-muted border-k5-border hover:border-k5-cyan"
                  }`}
                  style={difficulty === d.id ? { background: d.color, borderColor: d.color, boxShadow: `0 0 16px ${d.color}66` } : {}}
                >
                  {d.label}
                </button>
              ))}
            </div>
          </KPanel>

          {/* Graphics */}
          <KPanel>
            <div className="flex items-center gap-2 mb-3">
              <Monitor className="w-4 h-4 text-k5-cyan" />
              <span className="text-xs font-display tracking-wider text-k5-cyan">{t(language, "settings.graphics")}</span>
            </div>
            <div className="grid grid-cols-3 gap-2">
              {([
                { id: "low", label: t(language, "settings.low") },
                { id: "medium", label: t(language, "settings.medium") },
                { id: "high", label: t(language, "settings.high") },
              ] as const).map((g) => (
                <button
                  key={g.id}
                  onClick={() => setGraphicsQuality(g.id)}
                  className={`px-3 py-2 text-xs font-display tracking-wider rounded-sm border-2 transition-all ${
                    graphicsQuality === g.id
                      ? "bg-k5-cyan text-k5-deep-space border-k5-cyan"
                      : "bg-k5-panel/60 text-k5-muted border-k5-border hover:border-k5-cyan"
                  }`}
                >
                  {g.label}
                </button>
              ))}
            </div>
          </KPanel>

          {/* Controls */}
          <KPanel>
            <div className="flex items-center gap-2 mb-3">
              <Gamepad2 className="w-4 h-4 text-k5-cyan" />
              <span className="text-xs font-display tracking-wider text-k5-cyan">{t(language, "settings.controls")}</span>
            </div>
            <div className="space-y-3">
              <div>
                <div className="flex justify-between text-[10px] mb-1">
                  <span className="text-k5-muted">{t(language, "settings.sensitivity")}</span>
                  <span className="font-display text-k5-cyan">{sensitivity}%</span>
                </div>
                <input
                  type="range" min={10} max={100} value={sensitivity}
                  onChange={(e) => setSensitivity(Number(e.target.value))}
                  className="w-full h-2 bg-k5-panel rounded-sm appearance-none cursor-pointer accent-k5-cyan"
                />
              </div>
              <div>
                <div className="text-[10px] text-k5-muted mb-1">{t(language, "settings.layout")}</div>
                <div className="grid grid-cols-2 gap-2">
                  {([
                    { id: "right", label: t(language, "settings.rightHanded") },
                    { id: "left", label: t(language, "settings.leftHanded") },
                  ] as const).map((l) => (
                    <button
                      key={l.id}
                      onClick={() => setControlLayout(l.id)}
                      className={`px-3 py-2 text-xs font-display tracking-wider rounded-sm border-2 transition-all ${
                        controlLayout === l.id
                          ? "bg-k5-cyan text-k5-deep-space border-k5-cyan"
                          : "bg-k5-panel/60 text-k5-muted border-k5-border hover:border-k5-cyan"
                      }`}
                    >
                      {l.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          </KPanel>
        </div>
      </main>

      <footer className="relative z-10 px-4 py-4 safe-bottom border-t border-k5-border/50 bg-k5-panel/40 backdrop-blur-sm">
        <div className="max-w-2xl mx-auto flex gap-3">
          <KButton variant="primary" size="lg" className="flex-1" onClick={() => setScreen("start")}>
            {t(language, "lobby.save")}
          </KButton>
        </div>
      </footer>
    </div>
  );
}
