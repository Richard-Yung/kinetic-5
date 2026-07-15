"use client";

/**
 * Error Boundary pour le Canvas 3D — empêche un crash du FPS
 * de casser toute l'application React.
 */

import { Component, type ReactNode } from "react";

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}
interface State {
  hasError: boolean;
  error?: Error;
}

export class GameErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    console.error("[K5 GameErrorBoundary]", error, info.componentStack);
  }

  render() {
    if (this.state.hasError) {
      return (
        this.props.fallback ?? (
          <div className="min-h-screen flex flex-col items-center justify-center bg-k5-deep-space text-white p-6">
            <div className="text-k5-red font-display text-2xl mb-2">ERREUR RENDU 3D</div>
            <div className="text-k5-muted text-sm mb-4 text-center max-w-md">
              {this.state.error?.message ?? "Une erreur est survenue lors du rendu de la scène 3D."}
            </div>
            <button
              onClick={() => this.setState({ hasError: false, error: undefined })}
              className="px-4 py-2 bg-k5-cyan text-k5-deep-space font-display rounded-sm"
            >
              RÉESSAYER
            </button>
          </div>
        )
      );
    }
    return this.props.children;
  }
}
