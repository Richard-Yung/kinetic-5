import type { Metadata, Viewport } from "next";
import { Audiowide, Rajdhani, JetBrains_Mono } from "next/font/google";
import "./globals.css";
import { Toaster } from "@/components/ui/toaster";

const audiowide = Audiowide({
  variable: "--font-audiowide",
  subsets: ["latin"],
  weight: "400",
  display: "swap",
});

const rajdhani = Rajdhani({
  variable: "--font-rajdhani",
  subsets: ["latin"],
  weight: ["300", "400", "500", "600", "700"],
  display: "swap",
});

const jetbrainsMono = JetBrains_Mono({
  variable: "--font-mono",
  subsets: ["latin"],
  weight: ["400", "500", "700"],
  display: "swap",
});

export const metadata: Metadata = {
  title: "KINETICS 5 — Mobile Sci-Fi Shooter",
  description:
    "KINETICS 5 est un shooter FPS sci-fi / cyberpunk mobile. Missions de guerre dans des vaisseaux spatiaux. Combat tactique, agents spécialisés, armement expérimental.",
  keywords: [
    "KINETICS 5",
    "sci-fi shooter",
    "mobile FPS",
    "cyberpunk",
    "military tech",
    "space warfare",
    "Unity",
    "Next.js",
  ],
  authors: [{ name: "KINETICS 5 Team" }],
  icons: {
    icon: "/logo.svg",
  },
  openGraph: {
    title: "KINETICS 5 — Mobile Sci-Fi Shooter",
    description: "Missions de guerre dans des vaisseaux spatiaux. Combat tactique FPS.",
    type: "website",
  },
  twitter: {
    card: "summary_large_image",
    title: "KINETICS 5",
    description: "Mobile Sci-Fi FPS Shooter",
  },
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
  userScalable: false,
  themeColor: "#1AA1CE",
  viewportFit: "cover",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="fr" suppressHydrationWarning className="dark">
      <body
        className={`${audiowide.variable} ${rajdhani.variable} ${jetbrainsMono.variable} antialiased`}
      >
        {children}
        <Toaster />
      </body>
    </html>
  );
}
