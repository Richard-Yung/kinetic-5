# KINETICS 5 — Mobile Sci-Fi FPS (Unity 6000.0 LTS)

> **Projet Unity complet** — FPS mobile sci-fi / cyberpunk / military tech.
> Moteur : **Unity 6000.0 LTS** (URP 17.x). Cible : Android (API 24+), iOS (15+), Desktop (Windows/Mac).
> Documentation : Français. Code : C# 12. Shaders : HLSL URP.

---

## 1. Présentation

**KINETICS 5** est un FPS tactique mobile se déroulant dans des vaisseaux spatiaux.
Vous incarnez un des 4 agents (VULCAN, XEN, JOLT, XANO) et enchaînez des missions
co-op (jusqu'à 4 joueurs) à travers 7 régions distinctes.

### 1.1 Identité visuelle (NON-NÉGOCIABLE)

| Couleur | Hex | Usage |
|---|---|---|
| **Main Color** | `#1AA1CE` | Cyan principal — boutons, accents, glows |
| **Vert néon** | `#6CF42E` | Succès, validation, rim light ennemis |
| **Jaune** | `#FFE735` | Alertes, sélections, Volt element |
| **Rouge** | `#FE0022` | Danger, dégâts critiques, Explosive element |
| **Bleu nuit** | `#10204B` | Fonds, séparateurs |
| **Deep space** | `#05060F` / `#020207` | Backgrounds |
| **Blanc** | `#FFFFFF` | Texte principal |

### 1.2 Typographie

- **Titres / boutons / chiffres :** Audiowide (Google Fonts)
- **Corps :** Rajdhani / Inter
- **Mono :** JetBrains Mono

### 1.3 Agents jouables

| Agent | Classe | Niveau | Power | Spécialité |
|---|---|---|---|---|
| **VULCAN** | Tank | 47 | 2500 | Suppression lourde, alliages expérimentaux |
| **XEN** | Assault | 55 | — | DPS polyvalent, mobilité élevée |
| **JOLT** | Support | — | — | Soin, EMP, utilitaire |
| **XANO** | Recon | 55 | — | Furtif, éliminations silencieuses |

### 1.4 Missions (7 types)

1. **SHADOW FALL** (Extraction) — vaisseau cargo, tutoriel
2. **NEURAL BREACH** (Sabotage) — croiseur lourd, core neural
3. **VOID LOCK** (Survival) — station orbitale, 8 vagues 8 min
4. **IRON HARVEST** (Assassination) — usine drones, boss TITAN
5. **DEEP SIGNAL** (Recon) — épave, stealth
6. **BLACK ECHO** (Defense) — porte-vaisseaux, 10 vagues
7. **FINAL VECTOR** (BossRush) — vaisseau amiral, 3 phases

---

## 2. Prérequis

### 2.1 Logiciels

- **Unity 6000.0.26f1** (ou supérieur 6000.0.x LTS) — via Unity Hub
- **Git** 2.40+ avec **Git LFS** activé
- **.NET SDK 8** (pour IL2CPP et C# 12)
- **Android SDK / NDK** (via Unity Hub modules)
- **Xcode 15+** (pour build iOS, macOS uniquement)

### 2.2 Modules Unity Hub

Cochez ces modules lors de l'installation de Unity :

- **Build Support → Android** (inclut SDK + NDK + JDK)
- **Build Support → iOS**
- **Build Support → Windows IL2CPP** (ou Mac IL2CPP)
- **Documentation → Offline**
- **Language packs → Français** (optionnel)

### 2.3 Matériel recommandé (développement)

- CPU : 8 cœurs x86_64 (Apple Silicon natif supporté)
- RAM : 32 Go (16 Go minimum absolu)
- SSD : 500 Go libres
- GPU : compatible Vulkan / Metal

---

## 3. Installation

### 3.1 Cloner le dépôt

```bash
git clone https://github.com/kinetics5/unity-project.git
cd unity-project
git lfs install   # active LFS pour les assets binaires
git lfs pull      # télécharge les assets binaires
```

### 3.2 Ouvrir le projet

1. Lancez **Unity Hub**.
2. **Open → Add project from disk** → sélectionnez `/home/z/my-project/unity-project`.
3. Attendez l'import initial (5-15 min selon le matériel).
4. À l'invite **"OpenUPM packages missing"**, cliquez **"Install via Package Manager"** :
   - `com.cysharp.unitask` (UniTask)
   - `com.demigiant.dotween` (DOTween)
5. Vérifiez que le **Project Settings → Player → Active Input Handling = Both** (Input System + Legacy).
6. Vérifiez que le **Graphics Settings → Scriptable Render Pipeline Asset = URP_KINETICS5** (à créer si absent).

### 3.3 Configuration OpenUPM (optionnel — si l'UI Unity Hub ne résout pas)

```bash
# Manually edit Packages/manifest.json (déjà pré-configuré dans le projet)
# scopedRegistries est déjà déclaré pour OpenUPM.
# Si problème réseau, configurer npm ou openupm-cli :
npm install -g openupm-cli
cd /home/z/my-project/unity-project
openupm add com.cysharp.unitask com.demigiant.dotween
```

### 3.4 Configuration Nakama (backend multijoueur)

Le serveur Nakama est **optionnel** — le jeu fonctionne en mode hors-ligne si Nakama
n'est pas joignable. Pour activer le multijoueur :

1. **Docker** : `docker run -d --name nakama -p 7350:7350 -p 7351:7351 heroiclabs/nakama:3.18.0`
2. Configurez `Assets/_Project/Network/NakamaClient.cs` (via Inspector sur le prefab) :
   - Scheme : `http` (dév) / `https` (prod)
   - Host : `127.0.0.1` (dév) / `nakama.kinetics5.gg` (prod)
   - Port : `7350`
   - Server Key : `defaultkey` (dév) / clé prod (voir `SECURITY.md`)
3. Le SDK Nakama Unity n'est **pas inclus par défaut** (licence propriétaire Heroic Labs).
   Téléchargez `nakama-unity-x.x.x.unitypackage` depuis https://github.com/heroiclabs/nakama-unity
   et importez-le. L'asmdef `KINETICS5.Network` active automatiquement la define `KINETICS_NAKAMA`
   quand le package est présent.

### 3.5 Configuration FMOD (audio)

Le wrapper FMOD (`KINETICS5.Core/AudioManager.cs`) est conditionné par la define `KINETICS_FMOD`.
Pour activer FMOD Studio :

1. Téléchargez **FMOD Studio API 2.02+** pour Unity depuis https://fmod.com/download.
2. Importez le `.unitypackage` dans le projet.
3. L'asmdef Core détectera `FMODUnity` et activera `KINETICS_FMOD`.
4. Placez les banks `.bank` et `.strings.bank` dans `Assets/_Project/Audio/Banks/`.
5. Configurez le FMOD Settings (`FMOD > Edit Settings`) : Banking Directory = `Assets/_Project/Audio/Banks`.

Sans FMOD installé, AudioManager utilise un fallback `AudioSource` (limité mais fonctionnel).

### 3.6 Configuration Addressables

Les scènes de missions et les prefabs volumineux sont chargés via Addressables :

1. **Window > Asset Management > Addressables > Groups**.
2. Cliquez **Create Addressables Settings**.
3. Les groupes pré-configurés :
   - `Core` (Bootstrap, Base) — toujours en mémoire
   - `Missions` (7 scènes + prefabs) — chargées à la volée
   - `UI` (Atlas, prefabs UI) — chargés par écran
   - `Audio` (banks FMOD si non-streaming)
4. **Build > New Build > Default Build Script** avant chaque build player.

---

## 4. Structure des dossiers

```
unity-project/
├── Assets/
│   ├── _Project/                      # Tout le code KINETICS 5 (asmdef par couche)
│   │   ├── Core/                      # 13 systèmes C# de base (GameManager, EventBus, ...)
│   │   │   └── KINETICS5.Core.asmdef
│   │   ├── Data/                      # ScriptableObjects + JSON data-driven
│   │   │   ├── Scripts/               # DataLoader, DTOs, SOs
│   │   │   ├── Resources/Data/        # 6 fichiers JSON (agents, weapons, ...)
│   │   │   ├── Editor/                # DataValidator
│   │   │   └── KINETICS5.Data.asmdef
│   │   ├── Gameplay/                  # PlayerController, EnemyAI, MissionDirector, ...
│   │   │   ├── Combat/                # DamageCalculator
│   │   │   ├── Player/                # PlayerController (FPS)
│   │   │   ├── Enemies/               # EnemyAI (FSM)
│   │   │   ├── Missions/              # MissionDirector (orchestrateur)
│   │   │   └── KINETICS5.Gameplay.asmdef
│   │   ├── UI/                        # 33 écrans (UGUI + UI Toolkit) — agent 2-c
│   │   ├── Network/                   # Nakama client + MatchManager + AntiCheat
│   │   │   └── KINETICS5.Network.asmdef
│   │   ├── Audio/                     # Banks FMOD + AudioManager wrapper
│   │   ├── Shaders/                   # 6 shaders URP + PostProcess feature
│   │   │   ├── ToonShading.shader
│   │   │   ├── HoloUI.shader
│   │   │   ├── ForceField.shader
│   │   │   ├── MuzzleFlash.shader
│   │   │   ├── DamageNumber.shader
│   │   │   ├── ShipInterior.shader
│   │   │   ├── PostProcessing/        # K5PostProcessFeature.cs + .shader
│   │   │   └── KINETICS5.Shaders.asmdef
│   │   ├── Tests/                     # Unity Test Framework (4 EditMode + 4 PlayMode)
│   │   │   ├── EditMode/
│   │   │   ├── PlayMode/
│   │   │   └── KINETICS5.Tests.asmdef
│   │   └── Documentation/             # 8 docs FR (ARCHITECTURE, GAMEPLAY, ...)
│   └── csc.rsp                        # -langversion:latest (C# 12)
├── Packages/
│   └── manifest.json                  # URP 17.0.3, Cinemachine 3.1.1, UniTask, DOTween, ...
├── ProjectSettings/
│   └── ProjectVersion.txt             # 6000.0.26f1
└── README.md                          # CE FICHIER
```

---

## 5. Comment ouvrir et tester

### 5.1 Lancer le jeu en éditeur

1. Ouvrez la scène `Assets/_Project/Scenes/Boot.unity` (ou `MainMenu.unity`).
2. Cliquez **Play** (Ctrl+P).
3. Le `Bootstrapper` initialise les 13 systèmes Core + Data + Network en parallèle.
4. Le `GameManager` bascule en `MainMenu` après ~1.2 s (splash minimum).

### 5.2 Lancer les tests

- **Window > General > Test Runner**.
- Sélectionnez l'assembly **KINETICS5.Tests**.
- **Run All** pour exécuter les 4 EditMode + 4 PlayMode suites.
- Résultat attendu : **100% vert** (50+ tests).

### 5.3 Build Android

1. **File > Build Settings > Platform = Android**.
2. **Player Settings > Other Settings** :
   - **Scripting Backend = IL2CPP** (obligatoire pour 64-bit)
   - **Target Architectures = ARM64** (ARMv7 déconseillé, x86_64 pour émulateur)
   - **API Compatibility Level = .NET Standard 2.1**
   - **C++ Compiler Configuration = Release**
3. **Player Settings > Publishing Settings** :
   - **Keystore** : voir `DEPLOYMENT.md` section keystore.
   - **Min API Level = 24** (Android 7.0)
   - **Target API Level = Automatic (highest installed)**
4. **Build** (ou **Build And Run** sur device USB).

### 5.4 Build iOS

1. **File > Build Settings > Platform = iOS**.
2. **Player Settings > Other Settings** :
   - **Scripting Backend = IL2CPP**
   - **Target Device = iPhone + iPad**
   - **Minimum iOS Version = 15.0**
   - **Architecture = ARM64**
   - **Camera Usage Description** : "KINETICS 5 utilise la caméra pour le scan QR d'amis."
   - **Microphone Usage Description** : "KINETICS 5 utilise le micro pour le chat vocal d'équipage."
3. **Build** → génère un projet Xcode.
4. Ouvrez le `.xcodeproj` dans Xcode, signez avec votre provisioning profile (voir `DEPLOYMENT.md`).
5. **Archive > Distribute** pour TestFlight ou App Store.

---

## 6. Configuration réseau

| Environnement | Host | Port | TLS | Server Key |
|---|---|---|---|---|
| Dév local | `127.0.0.1` | 7350 | Non (http) | `defaultkey` |
| Staging | `staging.kinetics5.gg` | 443 | Oui (https) | `staging_key_XXXX` |
| Production | `nakama.kinetics5.gg` | 443 | Oui (https) | `prod_key_XXXX` |

Le `NakamaClient` bascule automatiquement en mode **Offline** si le serveur est
injoignable après 5 tentatives (backoff exponentiel 1s → 30s).

---

## 7. Roadmap

| Version | Statut | Contenu |
|---|---|---|
| v0.1.0 | ✅ Initial | Core + Data + Gameplay + Network + Shaders + Tests + Docs |
| v0.2.0 | 🚧 Planifié | UI complète (33 écrans), audio FMOD, addressables streaming |
| v0.3.0 | 📋 Planifié | Multiplayer co-op 4 joueurs, leaderboards, seasons |
| v1.0.0 | 📋 Planifié | Beta ouverte sur TestFlight + Google Play Internal |

Voir `CHANGELOG.md` pour le détail des versions.

---

## 8. Documentation

- [`ARCHITECTURE.md`](Assets/_Project/Documentation/ARCHITECTURE.md) — Couches, patterns, flux de données, asmdef
- [`GAMEPLAY.md`](Assets/_Project/Documentation/GAMEPLAY.md) — Mouvement FPS, combat, éléments, IA, boss, progression
- [`UI_GUIDE.md`](Assets/_Project/Documentation/UI_GUIDE.md) — Palette, typo, composants, 33 écrans, accessibilité
- [`PERFORMANCE.md`](Assets/_Project/Documentation/PERFORMANCE.md) — Optimisation mobile, no-GC, IL2CPP, ASTC
- [`SECURITY.md`](Assets/_Project/Documentation/SECURITY.md) — Anti-cheat, AES, GDPR, TLS
- [`DEPLOYMENT.md`](Assets/_Project/Documentation/DEPLOYMENT.md) — CI/CD, keystore, provisioning, release
- [`CONTRIBUTING.md`](Assets/_Project/Documentation/CONTRIBUTING.md) — Conventions C# 12, PRs, branches
- [`CHANGELOG.md`](Assets/_Project/Documentation/CHANGELOG.md) — Historique des versions

---

## 9. Licence et crédits

- **Code KINETICS 5** : Propriétaire © 2024-2025 KINETICS 5 Studio. Tous droits réservés.
- **Unity 6000.0 LTS** : Licence Unity Personal/Pro.
- **UniTask, DOTween** : MIT License (OpenUPM).
- **FMOD Studio** : FMOD Non-Commercial License (ou Commercial selon volume).
- **Nakama** : Apache 2.0 License (Heroic Labs).

**Audiowide**, **Rajdhani**, **Inter**, **JetBrains Mono** : SIL Open Font License.

---

## 10. Contact

- **Lead Tech** : tech@kinetics5.gg
- **Discord dev** : https://discord.gg/kinetics5-dev
- **Bug reports** : https://github.com/kinetics5/unity-project/issues

> Pour toute question de design/UI, référez-vous au PDF source `upload/shooter mobile game 5 2.pdf`.
