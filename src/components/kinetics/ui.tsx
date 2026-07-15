"use client";

/**
 * KINETICS 5 — Composants UI du design system
 * Reproduisent fidèlement le PDF de design :
 * - KButton : boutons Audiowide, états Default/Pressed/Selected/Locked
 * - KCard : cartes avec variants Default/Selected/Locked (PDF page 9)
 * - KProgressBar : barres segmentées (PDF page 4-5)
 * - KPanel : panneau sci-fi avec bordure cyan
 */

import { cn } from "@/lib/utils";
import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from "react";

/* ============================================================
   KButton — bouton KINETICS (PDF page 9 : Audiowide, cyan glow)
   ============================================================ */
type KButtonVariant = "primary" | "secondary" | "tertiary" | "danger" | "ghost";
type KButtonState = "default" | "pressed" | "selected" | "locked" | "disabled";

interface KButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: KButtonVariant;
  state?: KButtonState;
  size?: "sm" | "md" | "lg" | "xl";
  glow?: boolean;
  children: ReactNode;
}

const variantStyles: Record<KButtonVariant, string> = {
  primary:
    "bg-k5-cyan text-k5-deep-space hover:bg-k5-cyan-glow border-k5-cyan",
  secondary:
    "bg-k5-panel/80 text-white hover:bg-k5-panel-light border-k5-border",
  tertiary:
    "bg-transparent text-k5-cyan hover:bg-k5-cyan/10 border-k5-border",
  danger:
    "bg-k5-red text-white hover:brightness-110 border-k5-red",
  ghost:
    "bg-transparent text-k5-muted hover:text-white border-transparent",
};

const stateStyles: Record<KButtonState, string> = {
  default: "",
  pressed: "scale-95 brightness-125",
  selected: "ring-2 ring-k5-cyan ring-offset-2 ring-offset-k5-deep-space k5-glow-cyan",
  locked: "opacity-40 cursor-not-allowed grayscale",
  disabled: "opacity-30 cursor-not-allowed",
};

const sizeStyles = {
  sm: "px-3 py-1.5 text-xs min-h-[36px]",
  md: "px-4 py-2 text-sm min-h-[44px]",
  lg: "px-6 py-3 text-base min-h-[52px]",
  xl: "px-8 py-4 text-lg min-h-[60px]",
};

export const KButton = forwardRef<HTMLButtonElement, KButtonProps>(
  ({ className, variant = "primary", state = "default", size = "md", glow, children, disabled, ...props }, ref) => {
    return (
      <button
        ref={ref}
        className={cn(
          "font-display tracking-wider uppercase rounded-sm border-2 transition-all duration-150 select-none no-select",
          "active:scale-95 focus:outline-none focus-visible:ring-2 focus-visible:ring-k5-cyan",
          variantStyles[variant],
          stateStyles[state],
          sizeStyles[size],
          glow && "k5-glow-cyan",
          className
        )}
        disabled={disabled || state === "locked" || state === "disabled"}
        {...props}
      >
        {state === "locked" && <span className="mr-1">🔒</span>}
        {children}
      </button>
    );
  }
);
KButton.displayName = "KButton";

/* ============================================================
   KPanel — panneau sci-fi (bordure cyan, fond dégradé, coins clippés)
   ============================================================ */
interface KPanelProps {
  children: ReactNode;
  className?: string;
  glow?: boolean;
  scanlines?: boolean;
  clip?: boolean;
}

export function KPanel({
  children,
  className,
  glow,
  scanlines,
  clip = true,
}: KPanelProps) {
  return (
    <div
      className={cn(
        "k5-panel p-4",
        clip && "k5-clip",
        glow && "k5-glow-cyan",
        scanlines && "k5-scanlines",
        className
      )}
    >
      {children}
    </div>
  );
}

/* ============================================================
   KProgressBar — barre segmentée (PDF page 4-5)
   Style : 10-20 segments, couleur par type, glow sur fill
   ============================================================ */
type BarColor = "cyan" | "green" | "yellow" | "red" | "orange" | "purple";

const barColorMap: Record<BarColor, string> = {
  cyan: "#1AA1CE",
  green: "#6CF42E",
  yellow: "#FFE735",
  red: "#FE0022",
  orange: "#FF8C00",
  purple: "#A855F7",
};

interface KProgressBarProps {
  value: number; // 0-100
  max?: number;
  segments?: number;
  color?: BarColor;
  label?: string;
  valueText?: string;
  showValue?: boolean;
  className?: string;
  height?: "sm" | "md" | "lg";
}

export function KProgressBar({
  value,
  max = 100,
  segments = 20,
  color = "cyan",
  label,
  valueText,
  showValue = true,
  className,
  height = "md",
}: KProgressBarProps) {
  const pct = Math.max(0, Math.min(100, (value / max) * 100));
  const filledCount = Math.round((pct / 100) * segments);
  const barColor = barColorMap[color];
  const heights = { sm: "h-1.5", md: "h-2.5", lg: "h-4" };

  return (
    <div className={cn("w-full", className)}>
      {(label || showValue) && (
        <div className="flex justify-between items-center mb-1">
          {label && (
            <span className="text-[10px] uppercase tracking-wider text-k5-muted font-display">
              {label}
            </span>
          )}
          {showValue && valueText && (
            <span
              className="text-xs font-display tabular-nums"
              style={{ color: barColor }}
            >
              {valueText}
            </span>
          )}
        </div>
      )}
      <div
        className={cn("flex gap-px w-full", heights[height])}
        role="progressbar"
        aria-valuenow={value}
        aria-valuemax={max}
      >
        {Array.from({ length: segments }).map((_, i) => (
          <div
            key={i}
            className="flex-1 rounded-[1px] transition-all duration-300"
            style={{
              background: i < filledCount ? barColor : "rgba(26, 74, 110, 0.4)",
              boxShadow:
                i < filledCount ? `0 0 4px ${barColor}` : "none",
            }}
          />
        ))}
      </div>
    </div>
  );
}

/* ============================================================
   KCard — carte (PDF page 9 : Default/Selected/Locked)
   ============================================================ */
type KCardVariant = "default" | "selected" | "locked";

interface KCardProps {
  children: ReactNode;
  variant?: KCardVariant;
  onClick?: () => void;
  className?: string;
  accentColor?: string;
  selected?: boolean;
}

export function KCard({
  children,
  variant = "default",
  onClick,
  className,
  accentColor,
}: KCardProps) {
  const isLocked = variant === "locked";
  const isSelected = variant === "selected";

  return (
    <div
      onClick={!isLocked ? onClick : undefined}
      role={onClick ? "button" : undefined}
      tabIndex={!isLocked && onClick ? 0 : undefined}
      onKeyDown={!isLocked && onClick ? (e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); onClick(); } } : undefined}
      className={cn(
        "relative bg-gradient-to-br from-k5-panel to-k5-panel-light border-2 transition-all duration-200 k5-clip-sm",
        !isLocked && onClick && "cursor-pointer hover:border-k5-cyan hover:-translate-y-0.5",
        isSelected
          ? "border-k5-cyan k5-glow-cyan"
          : isLocked
          ? "border-k5-border/50 opacity-60"
          : "border-k5-border",
        className
      )}
      style={accentColor && isSelected ? { borderColor: accentColor, boxShadow: `0 0 20px ${accentColor}66` } : undefined}
    >
      {isSelected && (
        <div
          className="absolute -inset-px pointer-events-none k5-clip-sm"
          style={{
            background: `linear-gradient(135deg, ${accentColor || "#1AA1CE"}22, transparent 50%)`,
          }}
        />
      )}
      <div className="relative">{children}</div>
      {isLocked && (
        <div className="absolute inset-0 flex items-center justify-center bg-k5-deep-space/60 backdrop-blur-[2px]">
          <span className="text-2xl">🔒</span>
        </div>
      )}
    </div>
  );
}

/* ============================================================
   KRarityBadge — badge de rareté
   ============================================================ */
import { RARITY_COLORS, type Rarity } from "@/lib/kinetics-data";

export function KRarityBadge({ rarity, className }: { rarity: Rarity; className?: string }) {
  return (
    <span
      className={cn(
        "px-2 py-0.5 text-[10px] font-display uppercase tracking-wider rounded-sm border",
        className
      )}
      style={{
        color: RARITY_COLORS[rarity],
        borderColor: RARITY_COLORS[rarity],
        background: `${RARITY_COLORS[rarity]}22`,
      }}
    >
      {rarity}
    </span>
  );
}

/* ============================================================
   KCurrency — affichage devise (XP / CR)
   ============================================================ */
export function KCurrency({
  type,
  value,
  className,
}: {
  type: "xp" | "cr";
  value: number;
  className?: string;
}) {
  const color = type === "xp" ? "#6CF42E" : "#FFE735";
  const label = type === "xp" ? "XP" : "CR";
  return (
    <div className={cn("flex items-center gap-1.5", className)}>
      <span
        className="text-xs font-display tabular-nums"
        style={{ color }}
      >
        {label}
      </span>
      <span className="text-sm font-display tabular-nums text-white">
        {value.toLocaleString("fr-FR")}
      </span>
    </div>
  );
}

/* ============================================================
   KLogo — logo KINETICS 5
   ============================================================ */
export function KLogo({ className, size = "md" }: { className?: string; size?: "sm" | "md" | "lg" }) {
  const sizes = {
    sm: "text-xl",
    md: "text-3xl",
    lg: "text-5xl",
  };
  return (
    <div className={cn("font-display leading-none", sizes[size], className)}>
      <span className="text-white k5-text-glow-cyan">KINETICS</span>
      <span className="text-k5-cyan k5-text-glow-cyan ml-2">5</span>
    </div>
  );
}
