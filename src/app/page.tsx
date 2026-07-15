"use client";

/**
 * KINETICS 5 — Routeur principal
 * Affiche l'écran courant selon l'état du store Zustand.
 * C'est la seule route visible (src/app/page.tsx).
 */

import { useGameStore } from "@/store/game-store";
import { StartScreen } from "@/components/screens/start-screen";
import { LoadingScreen } from "@/components/screens/loading-screen";
import { LobbyScreen } from "@/components/screens/lobby-screen";
import { LoadoutScreen } from "@/components/screens/loadout-screen";
import { FPSGame } from "@/components/game/fps-game";
import { VictoryDefeatScreen } from "@/components/screens/victory-defeat-screen";
import { OperationSummaryScreen } from "@/components/screens/operation-summary-screen";
import { SettingsScreen } from "@/components/screens/settings-screen";
import { ScanlineOverlay } from "@/components/kinetics/visuals";
import { Suspense } from "react";
import { GameErrorBoundary } from "@/components/game/error-boundary";

export default function Home() {
  const screen = useGameStore((s) => s.screen);
  const setScreen = useGameStore((s) => s.setScreen);

  const renderScreen = () => {
    switch (screen) {
      case "start":
        return <StartScreen />;
      case "loading":
        return <LoadingScreen />;
      case "lobby":
        return <LobbyScreen />;
      case "loadout":
      case "armory":
        return <LoadoutScreen />;
      case "mission":
        return (
          <GameErrorBoundary>
            <Suspense fallback={<div className="min-h-screen flex items-center justify-center text-k5-cyan font-display">INITIALISATION...</div>}>
              <FPSGame onExit={() => setScreen("lobby")} />
            </Suspense>
          </GameErrorBoundary>
        );
      case "victory":
      case "defeat":
        return <VictoryDefeatScreen />;
      case "summary":
        return <OperationSummaryScreen />;
      case "settings":
        return <SettingsScreen />;
      default:
        return <StartScreen />;
    }
  };

  return (
    <>
      {renderScreen()}
      <ScanlineOverlay />
    </>
  );
}
