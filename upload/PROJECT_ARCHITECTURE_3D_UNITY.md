# ARCHITECTURE & STRUCTURATION CODE — RPG 3D ACTION SCI-FI (UNITY)

> **Projet :** RPG 3D action sci-fantasy dans l'esprit de **Tower of Fantasy**, **Solo Leveling** (anime) et **Wuthering Waves**.
> **Document de planification technique.** Pense-le comme la feuille de route pour coder un vrai univers persistant 3D, avec ses personnages, ses compétences, son économie, son lore — pas juste un "jeu".
> **Priorité absolue :** le **système** (gameplay, progression, monde vivant) avant le design. Le design s'itera en correctifs après.

---

## ⚠️ INSTRUCTIONS POUR L'IA QUI CODE CE PROJET

> À lire avant toute ligne de code.

1. **Priorité système > design.** Code d'abord un univers **fonctionnel et complet** (mouvement, combat, IA, progression, économie, quêtes, dialogues, save). Le polish visuel vient après, en correctifs demandés par l'utilisateur.
2. **Demande les assets visuels à l'utilisateur.** Tu ne peux pas coder/modéliser des personnages, environnements, VFX, voix, musique. **Avant de commencer chaque asset visuel**, demande-le à l'utilisateur via une question claire avec un modèle de prompt à lui fournir (voir section "Assets à demander" plus bas).
3. **Style visuel NON-NÉGOCIABLE.** Ne fais jamais un visuel "carton" (low-poly undetailed, couleurs plates criardes). Vise **anime 3D cell-shaded avec dynamisme Solo Leveling** (voir section Style Visuel).
4. **Contrôles tactiles obligatoires.** Le jeu aura des boutons virtuels à l'écran (joystick + 4-6 boutons d'attaque/skill/esquive). Pas seulement clavier/souris.
5. **Pense univers, pas niveau.** Tout doit être data-driven : personnages, compétences, quêtes, régions, dialogues en JSON/ScriptableObjects. On construit un **monde**, pas une suite de scènes.

---

## 📑 INDEX RAPIDE (tout ce qu'on utilisera)

### Technologies
| Catégorie | Choix | Raison |
|---|---|---|
| **Moteur** | **Unity 2023 LTS (6000.0.x LTS)** | Standard industrie, IL2CPP, export mobile/desktop/console, ToF lui-même est Unity |
| **Langage** | **C# 12 (.NET 8 SDK via Unity)** | Performance, typage strict, écosystème |
| **Rendering** | **URP (Universal Render Pipeline)** + Shader Graph | Cell-shading, post-processing, perf mobile |
| **Animation** | **Animator Controller + Playables + Animancer** (option) | State machines + programme runtime |
| **Netcode** | **Netcode for GameObjects** (small scale) ou **FishNet** (MMO) + **Nakama** (backend) | Autorité serveur, matchmaking, chat |
| **Backend** | **Nakama (Go, open source)** | Auth, parties, leaderboard, chat, matchmaking — built-in |
| **DB** | **PostgreSQL** (via Nakama) + **Redis** (cache temps réel) | Persistance + sessions |
| **UI in-game** | **UI Toolkit** (UI moderne) + **UGUI** (HUD complexe) | UI Toolkit pour menus, UGUI pour HUD |
| **Touch controls** | **Custom joystick + buttons** (assets Rewired ou Unity Input System) | Joystick virtuel + 4-6 boutons |
| **Audio** | **FMOD** (adaptatif) ou **Wwise** | BGM dynamique par combat, SFX spatialisés |
| **Asset pipeline** | **Addressables** + **Scriptable Build Pipeline** | Streaming, DLC, hot updates |
| **Data-driven** | **ScriptableObjects** + **JSON** + **Odin Inspector** | Tout le contenu en data éditable |
| **CI/CD** | **GitHub Actions** + **Unity Cloud Build** | Build auto par commit |
| **Telemetry** | **Unity Analytics** + **PostHog** + **Grafana** | Balance, perf, funnel |
| **Anti-cheat** | **BattlEye-style custom** + **validation serveur** | MMO = cheat obligatoire |
| **Versioning assets** | **Git LFS** + **Plastic SCM** (option) | Gros binaires |

### Librairies / packages Unity (à importer)
- **DOTween** — animations code (tweens)
- **Odin Inspector** — éditeur ScriptableObjects
- **Rewired / Unity Input System** — multi-device input
- **Universal Joystick Controller** ou **Touch Controls Kit** — joystick mobile
- **Ink** (inkle) — dialogues narratifs
- **UniTask** — async/await zero-alloc
- **UniRx** — reactive programming (events, input)
- **Zenject / VContainer** — dependency injection
- **Mirror / FishNet** — netcode (selon échelle)
- **FMOD Unity Integration** — audio adaptatif
- **Shader Graph** + **Post Processing v3** — cell-shading + bloom

### Patterns d'architecture
- **ECS DOTS** (Entities 1.0) — pour les entités massives (ennemis, particules)
- **MVC / MVP** — séparation data / vue / contrôle
- **Event-driven** — bus d'événements global (combat, loot, quêtes)
- **State machines** — IA, animation, UI flow
- **ScriptableObject architecture** — tout le contenu en data
- **Repository pattern** — accès DB isolé
- **Authoritative server** — jamais confiance au client
- **Snapshot interpolation** — netcode MMO

### Références de style visuel (à imiter)
- **Solo Leveling** (anime) : mouvements dynamiques, ombres violet/noir, slow-mo, hitstop, screen shake, slash trails
- **Tower of Fantasy** : UI holographique, cell-shading anime, VFX élémentaux
- **Wuthering Waves** : combat fluide, parades, contre-attaques
- **Genshin Impact** : open-world coloré, personnages charismatiques
- **Honkai Star Rail** : qualité visuelle 3D anime, post-processing

---

## 🎨 STYLE VISUEL — NON-NÉGOCIABLE

### ❌ Ce qu'il ne faut PAS faire (référence : "game type 1.webp")

> L'utilisateur a explicitement rejeté ce style. À bannir.

- **Visuel "carton"** : low-poly avec textures plates, pas de détails
- **Couleurs "caillées"** : palette criarde saturée à 100%, sans gradation
- **Animations raides** : pas de tween, pas d'anticipation, pas de follow-through
- **VFX simplistes** : juste des sprites qui poppent, pas de particules dynamiques
- **Lighting plat** : pas de reflets, pas d'ombres douces, pas de bloom
- **Personnages génériques** : sans charisme, sans expression faciale
- **Environnements vides** : sans props, sans végétation, sans atmosphère

### ✅ Ce qu'il faut viser (référence : "game type 2.webp" — anime 3D Solo Leveling)

**Philosophie : anime en mouvement, pas jeu vidéo raide.**

#### Rendu cell-shaded
- **Toon shader** avec ramp shading (3-5 bandes de couleur, pas de gradient lisse)
- **Outlines** épais et noirs sur personnages (style manga)
- **Bloom** sélectif sur éléments lumineux (yeux, armes, VFX)
- **Color grading** : palette saturée mais cohérente (violets, bleus cyan, oranges néon, noirs profonds)
- **Post-processing** : chromatic aberration légère, vignette, film grain subtil
- **Anti-aliasing** : MSAA 4x + FXAA pour edges propres

#### Dynamisme Solo Leveling
- **Hitstop** : freeze 3-8 frames à chaque hit significatif (jusqu'à 0.1s sur un boss kill)
- **Screen shake** directionnel et dégressif (pas juste un tremblement aléatoire)
- **Slow-motion** : 0.3x pendant 0.5s sur ultimes, parades, dodge parfaits
- **Slash trails** : trails colorés sur armes pendant les attaques
- **Particle bursts** : explosions de particules élémentaires à chaque hit
- **Camera kick** : la caméra recule légèrement à l'impact
- **Camera zoom** : zoom dynamique sur les criticaux / finishes
- **Motion blur** : sur mouvements rapides et attaques
- **Frame freeze** : 1 frame blanche à l'impact (flash hit)
- **Aerial combos** : les ennemis sont liftés en l'air, juggle possible
- **Dramatic angles** : caméra qui tourne autour sur les ultras

#### Animation
- **Animation rigs** : Humanoid rig avec IK pour pieds/mains
- **Animator** : blend trees pour mouvement (idle/walk/run/jump)
- **Animation events** : déclencheurs VFX/SFX précis sur frames
- **Anticipation** : 3-5 frames de wind-up avant chaque attaque
- **Follow-through** : 5-10 frames de recovery après chaque attaque
- **Squash & stretch** : sur impacts et sauts
- **Facial expressions** : blend shapes pour émotions (joie, colère, surprise, douleur)
- **Lip sync** : synchro bouche sur voix (Oculus LipSync ou custom)

#### Palette de couleurs (à coder dans `ThemeManager.cs`)
```csharp
public static class ToFAnimePalette {
    // Backgrounds
    public static readonly Color Void = new("#020207");
    public static readonly Color DeepSpace = new("#05060f");
    public static readonly Color PanelDark = new("#0c0a2e");
    // Primaries
    public static readonly Color Cyan = new("#22d3ee");
    public static readonly Color CyanGlow = new("#67e8f9");
    public static readonly Color Purple = new("#a855f7");
    public static readonly Color PurpleDeep = new("#6d28d9");
    // Elementals
    public static readonly Color Flame = new("#f97316");
    public static readonly Color Ice = new("#60a5fa");
    public static readonly Color Volt = new("#facc15");
    public static readonly Color Physical = new("#94a3b8");
    public static readonly Color Altered = new("#a855f7");
    public static readonly Color Shadow = new("#1e1b4b");  // Solo Leveling
    // Status
    public static readonly Color Hp = new("#22c55e");
    public static readonly Color HpLow = new("#ef4444");
    public static readonly Color Shield = new("#38bdf8");
    public static readonly Color Xp = new("#a855f7");
    // Accent
    public static readonly Color Gold = new("#fbbf24");
    public static readonly Color Warning = new("#f97316");
    public static readonly Color Danger = new("#dc2626");
}
```

#### Typos
- **Titres** : Orbitron (700/900) — néon futuriste
- **Sous-titres** : Rajdhani (500/600) — angulaire sci-fi
- **Corps** : Inter (400/500/600)
- **Données/chiffres** : JetBrains Mono (tabular)
- **Dialogues** : Noto Sans JP (si voice acting JP)

---

## 🎮 ÉTUDE DE TOWER OF FANTASY + SOLO LEVELING — SYSTÈMES À RÉPLIQUER

### 1. Système de combat (CRITIQUE — le cœur du jeu)
- **3 armes équipées simultanément** (slot 1/2/3), switch en combat = combo
- **Discharge skill** : jauge de charge 0→1000, libère compétence ultime quand pleine
- **Compétence d'arme** : cooldown court (8-15s)
- **Attaque normale** : combo auto enchaîné (5-7 hits)
- **Charged attack** : maintien 1.5s = attaque lourde
- **Éléments** : Flame / Ice / Volt / Physical / Altered / **Shadow** (Solo Leveling)
- **Résonance élémentaire** : 2 armes même élément → buff (+25% ATK élément, etc.)
- **Résonance d'arme** : 3 armes même type (Tank/DPS/Support/Benediction) → bonus
- **Esquive** : dash avec i-frames, slow-mo si timing parfait (parade)
- **Parade** : blocage juste avant impact = stagger ennemi + contre-attaque
- **Aerial combo** : lift + juggle (style Solo Leveling)
- **Ultimate** : cinématique courte + gros burst damage (0.5s slow-mo)
- **Hitstop + screenshake + VFX** à chaque impact (signature anime)

### 2. Simulacra (personnages jouables)
- Chaque Simulacrum = skin 3D + arme unique + 2 talents (awakening) + voix
- **Gacha SSR/SR/R/N** — taux 0.75% / 7% / 70% / 22.25%
- **Pity system** : 80 pulls garanti SSR, hard pity 120
- **Duplicates** → fragments d'âme → upgrade talents
- **Awakening** : arbre de talents à débloquer (50+ nodes par perso)
- **Bond system** : affection 1-10, dialogues, skins alt
- **Switch simulacra actif** en combat (change arme + apparence)

### 3. Équipement & progression
- **Armor** : 7 slots (Head/Hands/Feet/Chest/Legs/Shoulders/Belt) — 4 raretés N/R/SR/SSR
- **Matrices** (chips) : 4 pièces par set, set bonus 2/4 pièces
- **Reliques** : objets activables (jetpack, bouclier, hologramme)
- **Shards de reliques** : farm pour up
- **Enhancement** : +0 → +30 avec matériaux
- **Advancement** : fusion de dupes pour monter étoiles (1→6★)
- **Gear score** : somme de tous les niveaux = power level
- **Skill tree** par simulacrum (50+ nodes)
- **Mastery** : niveaux par élément (Flame Mastery, Ice Mastery, etc.)

### 4. Open world & exploration
- **Régions** : Astra, Banges, Navia, Crown, Warren, Artificial Island, Miasmic Swamp… (6-10 régions)
- **Streaming** : additive scene loading par chunks (Addressables)
- **Points d'intérêt** : Spatial rifts, supply pods, scenic points, mushrooms puzzles
- **Énigmes environnementales** : activateurs, orbes, surcharges
- **Mounts** : véhicules (motorcycle, mecha, hoverboard) — craft + skins
- **World bosses** : respawn timer, loot partagé multi-joueurs
- **Dungeons instanciés** : raids 4-8 joueurs, world bosses 30 joueurs
- **Weather** : pluie, tempête, day/night cycle, éclairs
- **Verticalité** : saut, vol (jetpack), escalade

### 5. Multiplayer & social
- **Serveurs partagés** : 1 monde persistant pour N joueurs simultanés
- **Groupes** : 4 joueurs PvE, raid 8
- **Crews** (guilds) : jusqu'à 120 membres, events hebdo, crew battle
- **World chat / privé / crew** + voice chat (option)
- **Trading post** : économie régulée, taxe 5%
- **PvP** : arena 1v1, 3v3, battleground 6v6, open-world PvP flag
- **Spectator mode** : pour esports

### 6. Monétisation (à transposer éthiquement)
- **Premium currency** : Tanium (payante) + Dark Crystals (farmable)
- **Gacha** : Gold Nucleus / Red Nucleus / Proof of Purchase
- **Battle Pass** : 50 niveaux hebdo
- **Cosmetics** : skins simulacra, mounts, chat bubbles, nameplates
- **Cash shop** : bundles, monthly card, growth fund
- **Limites éthiques** : plafond mensuel, taux affichés, pity counter visible

### 7. Quêtes & narrative
- **Main quest** (MSQ) — gating par region, 5 acts
- **Side quests** — lore et mats
- **Daily quests** — 5/jour, reset 04:00
- **Weekly quests** — crew + perso
- **Hidden quests** — exploration triggered
- **Events limités** — crossover collabs
- **Dialogue system** (Ink) avec choices, branches, conditions
- **Cutscenes** : Timeline + Cinemachine
- **Voice acting** : FR/EN/JP/CN/KR

### 8. HUD/UI (signature ToF + Solo Leveling)
- **Top-left** : portrait simulacra + barres HP/charge + buffs
- **Top-right** : minimap + objective tracker
- **Bottom-center** : 3 slots d'armes avec cooldowns + icon skill + ultimate
- **Bottom-right** : inventory quick access + mount button
- **Floating damage numbers** colorés par élément + crit (style Solo Leveling : gros chiffres, pop animations)
- **Combat VFX overlay** : slash trails, elemental bursts, hitstop flash
- **On-screen touch controls** (mobile) : joystick gauche + 6 boutons droite
- **Menus modaux** : Inventory, Simulacra, Crew, Mail, Shop — tous glassmorphism + glitch

---

## 📱 CONTRÔLES TACTILES À L'ÉCRAN (OBLIGATOIRES)

### Layout mobile (paysage)
```
┌──────────────────────────────────────────────────┐
│  [Portrait]              [Minimap]   [Settings]  │
│  HP ████░░░░                                      │
│  Charge ████████                                  │
│  Buffs [icons]                                    │
│                                                   │
│                                                   │
│                  [3D WORLD]                       │
│                                                   │
│                                                   │
│                                                   │
│  ┌──────┐                          [⚡] [🎯]    │
│  │      │                          Skill  Ult   │
│  │  ◉   │  Joystick                [⚔️] [🛡️]    │
│  │      │  mouvement               Atk   Dodge  │
│  └──────┘                          [🔄] [🎒]    │
│                                     Swap  Inv   │
└──────────────────────────────────────────────────┘
```

### Spécifications techniques
- **Joystick gauche** : flottant ( Dynamic Joystick), 0-1 magnitude, snap-back au release
- **6 boutons droits** :
  1. **Attaque normale** (carré) — combo auto
  2. **Skill d'arme** (triangle) — cooldown 8-15s
  3. **Discharge / Ultimate** (rond, glow quand jauge pleine) — ultime
  4. **Esquive / Dash** (croix) — i-frames
  5. **Swap arme** (L1/R1 style) — switch slot 1↔2↔3
  6. **Inventaire quick access** (carré long press) — radial menu
- **Boutons supplémentaires contextuels** :
  - **Interagir** (triangle quand près d'un PNJ/objet)
  - **Monter véhicule** (quand près d'un mount)
  - **Sauter** (en mode exploration)
- **Gestes tactiles** :
  - **Swipe gauche/droite** : switch arme rapide
  - **Swipe haut** : saut
  - **Swipe bas** : ramasser
  - **Long press** : visée libre (sniper)
- **Haptic feedback** : vibration courte sur hit, longue sur ultime
- **Taille des boutons** : 80-100px (44px min Apple, 48px min Google)
- **Opacité** : 70% au repos, 100% au press
- **Réglages** : taille, opacité, position (gaucher/droitier), sensibilité joystick

### Contrôles desktop (clavier/souris)
- **ZQSD/WASD** : déplacement
- **Souris** : rotation caméra
- **Clic gauche** : attaque normale
- **Clic droit** : visée
- **Espace** : saut / dash
- **A/1, Z/2, E/3** : switch arme 1/2/3
- **Shift gauche** : skill d'arme
- **R** : ultimate (quand jauge pleine)
- **F** : interagir
- **Tab** : inventaire
- **Échap** : pause

### Contrôles manette (Xbox/PS5)
- **Stick gauche** : déplacement
- **Stick droit** : caméra
- **A/Croix** : saut
- **B/Cercle** : dash
- **X/Carré** : attaque
- **Y/Triangle** : skill
- **LB/L1** : swap arme gauche
- **RB/R1** : swap arme droite
- **LT/L2** : visée
- **RT/R2** : ultimate
- **Start** : pause

---

## 🏗️ ARCHITECTURE CODE COMPLÈTE — STRUCTURE DES FICHIERS

```
project-root/
├── Assets/
│   ├── _Project/
│   │   ├── Core/                           # systèmes fondamentaux
│   │   │   ├── GameManager.cs              # singleton root
│   │   │   ├── EventBus.cs                 # pub/sub global
│   │   │   ├── SaveSystem.cs               # JSON + PlayerPrefs + cloud
│   │   │   ├── SceneLoader.cs              # additive loading
│   │   │   ├── ObjectPooler.cs             # pools pour VFX/projectiles
│   │   │   ├── AudioManager.cs             # FMOD wrapper
│   │   │   ├── LocalizationManager.cs      # i18n
│   │   │   ├── InputManager.cs             # multi-device
│   │   │   ├── CameraManager.cs            # Cinemachine wrapper
│   │   │   ├── TimeManager.cs              # slow-mo, hitstop
│   │   │   └── TelemetryLogger.cs          # analytics
│   │   │
│   │   ├── Gameplay/
│   │   │   ├── Player/
│   │   │   │   ├── PlayerController.cs     # mouvement, jump, dash
│   │   │   │   ├── PlayerCombat.cs         # attaques, combos
│   │   │   │   ├── PlayerWeaponManager.cs  # 3 slots + switch
│   │   │   │   ├── PlayerStats.cs          # stats calculées
│   │   │   │   ├── PlayerAnimator.cs       # animator wrapper
│   │   │   │   ├── PlayerInventory.cs      # équipement
│   │   │   │   └── PlayerHUD.cs            # overlay HUD
│   │   │   │
│   │   │   ├── Combat/
│   │   │   │   ├── DamageCalculator.cs     # formules dégâts
│   │   │   │   ├── ElementalResolver.cs    # éléments, résistances
│   │   │   │   ├── DischargeSystem.cs      # jauge 1000
│   │   │   │   ├── ComboChain.cs           # enchaînements
│   │   │   │   ├── ResonanceCalc.cs        # bonus élément/type
│   │   │   │   ├── HitstopController.cs    # freeze frames
│   │   │   │   ├── ScreenShake.cs          # caméra shake
│   │   │   │   ├── FloatingDamage.cs       # chiffres flottants
│   │   │   │   └── VFXSpawner.cs           # particules, slash trails
│   │   │   │
│   │   │   ├── Enemies/
│   │   │   │   ├── EnemyController.cs      # base
│   │   │   │   ├── EnemyAI.cs              # behavior tree
│   │   │   │   ├── EnemySpawner.cs         # spawn zones
│   │   │   │   ├── BossController.cs       # boss avec phases
│   │   │   │   └── BossPhaseManager.cs     # patterns par phase
│   │   │   │
│   │   │   ├── NPCs/
│   │   │   │   ├── NPCController.cs
│   │   │   │   ├── DialogueTrigger.cs
│   │   │   │   ├── QuestGiver.cs
│   │   │   │   ├── Vendor.cs               # marchands
│   │   │   │   └── Companion.cs            # PNJ qui suivent
│   │   │   │
│   │   │   ├── World/
│   │   │   │   ├── WorldStreamer.cs        # chunks additive
│   │   │   │   ├── RegionManager.cs
│   │   │   │   ├── POIManager.cs           # points d'intérêt
│   │   │   │   ├── WeatherSystem.cs
│   │   │   │   ├── DayNightCycle.cs
│   │   │   │   ├── MountSystem.cs          # véhicules
│   │   │   │   ├── FishingSystem.cs        # mini-jeux
│   │   │   │   ├── GatheringSystem.cs      # ressources
│   │   │   │   └── FastTravelSystem.cs
│   │   │   │
│   │   │   └── Progression/
│   │   │       ├── XPManager.cs
│   │   │       ├── LevelSystem.cs
│   │   │       ├── SkillTreeManager.cs
│   │   │       ├── AwakeningManager.cs
│   │   │       ├── GearScoreCalculator.cs
│   │   │       └── MasteryManager.cs       # maîtrise par élément
│   │   │
│   │   ├── Data/                           # ScriptableObjects (data-driven)
│   │   │   ├── Simulacra/
│   │   │   │   ├── SimulacrumData.cs       # scriptableObject schema
│   │   │   │   ├── Characters/
│   │   │   │   │   ├── Shiro.asset
│   │   │   │   │   ├── Nemesis.asset
│   │   │   │   │   └── ...
│   │   │   │   └── AwakeningTrees/
│   │   │   │
│   │   │   ├── Weapons/
│   │   │   │   ├── WeaponData.cs
│   │   │   │   ├── Flame/
│   │   │   │   │   ├── MeltingSword.asset
│   │   │   │   │   └── ...
│   │   │   │   ├── Ice/
│   │   │   │   ├── Volt/
│   │   │   │   ├── Physical/
│   │   │   │   ├── Altered/
│   │   │   │   └── Shadow/                 # Solo Leveling style
│   │   │   │
│   │   │   ├── Enemies/
│   │   │   │   ├── EnemyData.cs
│   │   │   │   ├── MobFamily/
│   │   │   │   └── Bosses/
│   │   │   │
│   │   │   ├── Regions/
│   │   │   │   ├── RegionData.cs
│   │   │   │   ├── Astra.asset
│   │   │   │   ├── Banges.asset
│   │   │   │   └── ...
│   │   │   │
│   │   │   ├── Quests/
│   │   │   │   ├── QuestData.cs
│   │   │   │   ├── MainQuests/
│   │   │   │   ├── SideQuests/
│   │   │   │   ├── DailyQuests/
│   │   │   │   └── HiddenQuests/
│   │   │   │
│   │   │   ├── Items/
│   │   │   │   ├── ItemData.cs
│   │   │   │   ├── Consumables/
│   │   │   │   ├── Materials/
│   │   │   │   ├── Equipment/
│   │   │   │   └── Cosmetics/
│   │   │   │
│   │   │   ├── Matrices/
│   │   │   │   ├── MatrixSetData.cs
│   │   │   │   └── Sets/
│   │   │   │
│   │   │   ├── Relics/
│   │   │   │   └── RelicData.cs
│   │   │   │
│   │   │   ├── Skills/
│   │   │   │   ├── SkillData.cs
│   │   │   │   ├── NormalAttacks/
│   │   │   │   ├── WeaponSkills/
│   │   │   │   └── Discharges/
│   │   │   │
│   │   │   ├── LootTables/
│   │   │   │   └── LootTable.cs
│   │   │   │
│   │   │   ├── Economy/
│   │   │   │   ├── ShopData.cs
│   │   │   │   ├── GachaRates.asset
│   │   │   │   └── EnhancementCosts.asset
│   │   │   │
│   │   │   ├── Dialogues/
│   │   │   │   ├── ink/                    # fichiers Ink
│   │   │   │   │   ├── main_quest_act1.ink
│   │   │   │   │   └── ...
│   │   │   │   └── VoiceLines/
│   │   │   │
│   │   │   ├── BattlePass/
│   │   │   │   └── BattlePassSeason.asset
│   │   │   │
│   │   │   └── Balance/
│   │   │       ├── DamageFormula.asset
│   │   │       └── EconomyConfig.asset
│   │   │
│   │   ├── UI/
│   │   │   ├── HUD/
│   │   │   │   ├── HUDRoot.cs               # container HUD
│   │   │   │   ├── HealthBarUI.cs
│   │   │   │   ├── ChargeBarUI.cs
│   │   │   │   ├── WeaponSlotsUI.cs
│   │   │   │   ├── MinimapUI.cs
│   │   │   │   ├── ObjectiveTrackerUI.cs
│   │   │   │   ├── FloatingDamageUI.cs
│   │   │   │   ├── BuffBarUI.cs
│   │   │   │   ├── BossHealthUI.cs
│   │   │   │   └── ComboCounterUI.cs
│   │   │   │
│   │   │   ├── Menus/
│   │   │   │   ├── MainMenuUI.cs
│   │   │   │   ├── InventoryUI.cs
│   │   │   │   ├── SimulacraListUI.cs
│   │   │   │   ├── GachaScreenUI.cs
│   │   │   │   ├── ShopUI.cs
│   │   │   │   ├── CrewPanelUI.cs
│   │   │   │   ├── QuestLogUI.cs
│   │   │   │   ├── MailBoxUI.cs
│   │   │   │   ├── SettingsUI.cs
│   │   │   │   ├── CharacterSheetUI.cs
│   │   │   │   ├── SkillTreeUI.cs
│   │   │   │   ├── DialogueUI.cs
│   │   │   │   └── PauseMenuUI.cs
│   │   │   │
│   │   │   ├── TouchControls/              # mobile
│   │   │   │   ├── TouchJoystick.cs        # joystick flottant
│   │   │   │   ├── TouchButton.cs          # bouton multi-tap
│   │   │   │   ├── TouchInputBinder.cs     # bind actions → boutons
│   │   │   │   ├── TouchGestureDetector.cs # swipes, long press
│   │   │   │   ├── HapticFeedback.cs       # vibration
│   │   │   │   └── TouchControlsCanvas.prefab
│   │   │   │
│   │   │   └── Components/
│   │   │       ├── HoloPanel.cs
│   │   │       ├── GlitchButton.cs
│   │   │       ├── RarityBadge.cs
│   │   │       ├── ElementIcon.cs
│   │   │       ├── CurrencyDisplay.cs
│   │   │       ├── LoadingBar.cs
│   │   │       └── Toast.cs
│   │   │
│   │   ├── Network/
│   │   │   ├── NetworkClient.cs            # socket client
│   │   │   ├── PacketHandlers.cs
│   │   │   ├── LagCompensation.cs
│   │   │   ├── NetworkedTransform.cs       # sync position/rotation
│   │   │   ├── NetworkAnimator.cs
│   │   │   └── InterestManagement.cs       # culling distant entities
│   │   │
│   │   ├── Render/
│   │   │   ├── Shaders/
│   │   │   │   ├── Toon.shader             # cell-shading
│   │   │   │   ├── ToonOutline.shader
│   │   │   │   ├── Hologram.shader
│   │   │   │   ├── Glitch.shader
│   │   │   │   └── ElementalVFX.shader
│   │   │   ├── PostProcessing/
│   │   │   │   ├── BloomSelective.cs
│   │   │   │   ├── ChromaticAberration.cs
│   │   │   │   └── HitstopFlash.cs
│   │   │   └── VFX/
│   │   │       ├── SlashTrail.cs
│   │   │       ├── ElementalBurst.cs
│   │   │       └── HitParticles.cs
│   │   │
│   │   ├── Audio/
│   │   │   ├── MusicPlayer.cs              # BGM adaptatif
│   │   │   ├── SfxBank.cs
│   │   │   ├── VoiceLinePlayer.cs
│   │   │   └── AudioMixerController.cs
│   │   │
│   │   ├── Localization/
│   │   │   ├── fr.json
│   │   │   ├── en.json
│   │   │   ├── jp.json
│   │   │   ├── cn.json
│   │   │   └── kr.json
│   │   │
│   │   └── Tests/
│   │       ├── EditMode/
│   │       │   ├── DamageCalculatorTests.cs
│   │       │   ├── GachaServiceTests.cs
│   │       │   └── ...
│   │       └── PlayMode/
│   │           ├── PlayerControllerTests.cs
│   │           └── ...
│   │
│   ├── Plugins/                            # third-party
│   │   ├── FMOD/
│   │   ├── Odin/
│   │   ├── DOTween/
│   │   └── ...
│   │
│   ├── Settings/
│   │   ├── URP/                            # render pipeline assets
│   │   ├── InputActions.inputactions       # Unity Input System
│   │   └── ProjectSettings/
│   │
│   └── AddressableAssetsData/              # Addressables config
│
├── server/                                 # backend Nakama
│   ├── main.go                             # bootstrap
│   ├── modules/
│   │   ├── auth.go
│   │   ├── inventory.go
│   │   ├── gacha.go
│   │   ├── matchmaker.go
│   │   ├── leaderboard.go
│   │   ├── chat.go
│   │   └── trade.go
│   ├── lua/                                # scripts serveur Nakama
│   │   ├── combat_validate.lua
│   │   └── economy.lua
│   └── docker-compose.yml
│
├── tools/
│   ├── data-validator/                     # valide .asset vs schemas
│   ├── balance-simulator/                  # simu combat
│   ├── map-editor/                         # éditeur régions
│   └── asset-importer/                     # batch import FBX
│
├── docs/
│   ├── GDD.md                              # Game Design Document
│   ├── CombatSystem.md
│   ├── GachaSpec.md
│   ├── NetworkProtocol.md
│   ├── DataSchemas.md
│   ├── AntiCheat.md
│   ├── StyleGuide.md                       # références visuelles
│   ├── TouchControls.md
│   └── adr/                                # Architecture Decision Records
│       ├── 0001-scriptableobject-pattern.md
│       ├── 0002-authoritative-server.md
│       └── ...
│
├── .github/workflows/
│   ├── ci.yml                              # lint+test+build
│   └── deploy.yml
│
└── README.md
```

---

## 📋 ÉTAPES DE CODAGE — PLAN COMPLET EN 16 PHASES

> Chaque phase doit être **validée** (lint + tests + review utilisateur) avant de passer à la suivante. Le système prime sur le design — on construit d'abord un univers fonctionnel, puis on itère le visuel.

### PHASE 0 — Pré-production (2-4 semaines)

| Tâche | Détails |
|---|---|
| 0.1 Game Design Document | Document de 50+ pages : vision, pillars, target audience, monétisation éthique |
| 0.2 Style guide visuel | Palette ToF + Solo Leveling, typos, ref artistique, moodboards par région |
| 0.3 Story bible | Lore, factions, personnages, dialogues-clés, 5 acts |
| 0.4 **Demande assets visuels à l'utilisateur** | Voir section "Assets à demander" ci-dessous |
| 0.5 Wireframes UI | Tous les écrans (login, hub, HUD, menus) en Figma |
| 0.6 Prototype graybox | 1 zone test, 1 personnage, 1 ennemi, 1 arme — prouve le game feel |
| 0.7 Validation technique | Build mobile + desktop OK, 60 FPS atteint |

### PHASE 1 — Socle Unity (1-2 semaines)

1. Créer projet Unity 6000.0 LTS avec URP
2. Configurer Git LFS pour gros binaires (.fbx, .png, .wav)
3. Installer packages : Input System, Cinemachine, Addressables, TextMeshPro, Odin, DOTween, UniTask
4. Setup dossiers `_Project/` (Core, Gameplay, Data, UI, Network, Render, Audio)
5. **GameManager.cs** singleton + EventBus + ObjectPooler
6. **InputManager.cs** avec Input System (KB/mouse + manette + touch)
7. **SaveSystem.cs** JSON + PlayerPrefs
8. CI GitHub Actions : build auto sur chaque PR
9. **Validation** : build PC + Android + WebGL sans erreur

### PHASE 2 — Authentification & compte (1 semaine)

1. Backend **Nakama** en Docker local
2. Inscription email/mot de passe + JWT
3. OAuth2 Google / Apple
4. Schémas DB : `User`, `PlayerProfile`, `Session`
5. UI Login React ou Unity UI Toolkit
6. Validation serveur stricte (schema Zod-like en Go)
7. **Validation** : login + register + session persistante

### PHASE 3 — Mouvement & caméra (1-2 semaines)

**Objectif :** Personnage qui court, saute, dash, grimpe dans une scène graybox.

1. **PlayerController.cs** — CharacterController ou Rigidbody
2. **CameraManager.cs** — Cinemachine virtual camera (3rd person)
3. Animations de mouvement (idle/walk/run/jump/fall) — *demander à l'utilisateur*
4. **InputManager** — bind ZQSD + souris + touch joystick
5. **Dash** avec i-frames + slow-mo sur perfect dodge
6. **Saut** + double saut + wall jump
7. **Grimpe** + ledge grab
8. **Mount system** — base (sans véhicule encore)
9. **Touch controls** — joystick flottant + bouton saut/dash
10. **Validation** : 60 FPS stable, controls responsive < 50ms

### PHASE 4 — Combat de base (2-3 semaines)

**Objectif :** Attaquer, prendre des dégâts, tuer 1 ennemi, mourir, respawn.

1. **PlayerCombat.cs** — attaque normale combo 5 hits
2. **PlayerWeaponManager.cs** — 3 slots + switch animé
3. **EnemyController.cs** + **EnemyAI.cs** — 1 type ennemi basique
4. **DamageCalculator.cs** — formule de base (ATK - DEF)
5. **Health system** — HP, mort, respawn
6. **HitstopController.cs** — freeze 3-5 frames sur hit
7. **ScreenShake.cs** — caméra shake directionnel
8. **FloatingDamage.cs** — chiffres flottants colorés
9. **VFXSpawner.cs** — slash trails, hit particles
10. **Drop & pickup** — loot, magnet, inventory
11. **HUD** — barre HP + compteur score + 3 slots d'armes
12. **Validation** : combat feel bon, hitstop + shake ressentis

### PHASE 5 — Combat enrichi ToF (3-4 semaines)

**Réplique du système ToF complet.**

1. **5 éléments** : Flame / Ice / Volt / Physical / Altered (+ Shadow en bonus SL)
2. **ElementalResolver.cs** — résistances, faiblesses, calculs
3. **WeaponSkill** — cooldown 8-15s par arme
4. **DischargeSystem** — jauge 0→1000, ultimate skill
5. **ComboChain** — enchaînements par arme
6. **ResonanceCalc** :
   - Élémentaire : 2 armes même élément → +25% ATK élément
   - Type arme : 3 DPS → +15% crit, 3 Tank → +20% DEF, etc.
7. **Parade** — blocage timing = stagger + contre
8. **Aerial combo** — lift + juggle
9. **Crit system** — taux + multiplicateur + icône
10. **Buffs/Debuffs** — 50+ types (poison, burn, freeze, stun, ATK up, etc.)
11. **Status effects** — freeze = slow, burn = DoT, etc.
12. **VFX élémentaux** — *demander à l'utilisateur* (flame bursts, ice shards, etc.)
13. **SFX combat** — *demander à l'utilisateur*
14. **Tests balance** — simu 1000 combats, vérif deltas

### PHASE 6 — Progression & inventaire (3 semaines)

1. ScriptableObject `ItemData`, `WeaponData`, `ArmorData`, `MatrixData`
2. **PlayerInventory.cs** — 100+ slots, tri, filtre
3. 7 slots d'armor + 3 slots d'arme + 4 slots de matrice
4. **Enhancement** — +0 à +30 avec matériaux (data-driven)
5. **Advancement** — fusion dupes → étoiles 1-6
6. **Gear score** — power level calculé
7. **Matrices** — sets 2/4 pièces, set bonus
8. **Reliques** — 8 slots, activation skill
9. **UI Inventory** complète (filter, sort, compare, lock, dismantle)
10. **Loot tables** — JSON par source (mob, boss, chest)
11. **Anti-cheat** — tout loot server-side, signature cryptographique

### PHASE 7 — Simulacra & gacha (2-3 semaines)

1. ScriptableObject `SimulacrumData` (stats, skills, awakening tree, voice lines, lore)
2. 10+ simulacra en data — *demander visuels à l'utilisateur*
3. **GachaService** — RNG seedé (audit), pity 80 / hard pity 120
4. Taux configurables dans `GachaRates.asset`
5. **Fragments d'âme** — dupes converties, awakening
6. UI gacha avec animations (pull card, reveal SSR) — *demander VFX à l'utilisateur*
7. **Éthique** : taux affichés, limite mensuelle, pity counter visible
8. UI Simulacra List (tri, filtre, détails, awakening tree)
9. Switch simulacra actif en combat
10. **Bond system** — affection 1-10, dialogues spéciaux

### PHASE 8 — Open world & exploration (4-6 semaines)

1. **WorldStreamer.cs** — additive scene loading par chunks (Addressables)
2. 6 régions ScriptableObjects (Astra, Banges, Navia, Crown, Warren, Artificial Island) — *demander visuels à l'utilisateur*
3. **POIManager** — scenic points, supply pods, rifts
4. **Énigmes** — activateurs, orbes, puzzles mushroom
5. **WeatherSystem** — pluie, tempête, day/night
6. **DayNightCycle** — 24h game = 2h real
7. **World bosses** — scheduling, multi-joueur, loot partagé
8. **Mounts** — 8 véhicules, craft + skins
9. **Minimap** — zoom, POI, ping crew
10. **Fast travel** — unlock scenic points
11. **Hidden quests** — trigger par zone/objet
12. **Gathering** — ressources (mining, fishing, foraging)
13. **VFX environnement** — *demander à l'utilisateur*

### PHASE 9 — Netcode MMO (3-4 semaines)

1. **Nakama** backend部署 (Docker, multi-pod)
2. **NetworkClient.cs** — connexion WebSocket
3. **NetworkedTransform.cs** — sync position/rotation
4. **MovementHandler** — client-side prediction + réconciliation serveur
5. **Snapshot interpolation** — buffer 100ms, lerp positions
6. **LagCompensation** — rewind hitboxes pour hit detection
7. **CombatHandler** — validation dégâts server-side
8. **Interest management** — n'envoyer que les entités proches
9. **Rate limit** — 20 Hz update positions, 10 Hz autres events
10. **Reconnect** — état conservé 5 min
11. Tests charge : 1000 joueurs simulés par région

### PHASE 10 — Quêtes & narrative (2-3 semaines)

1. ScriptableObject `QuestData` (steps, conditions, rewards, dialogue)
2. **QuestSystem** — tracking, completion events
3. **Main quest** — 5 acts gating regions — *demander dialogues à l'utilisateur*
4. **Side quests** — lore + mats
5. **Daily / weekly quests** — reset job 04:00
6. **Hidden quests** — exploration triggered
7. **Dialogue system** avec Ink — typewriter, choices, voice lines
8. **CutsceneScene** — Timeline + Cinemachine
9. **Mailbox** — récompenses, messages PNJ, gifts crew
10. **Voice acting** — *demander à l'utilisateur* (FR/EN/JP)

### PHASE 11 — Multiplayer & social (3-4 semaines)

1. **Crews** (guilds) — création, inviter, ranks, crew bank
2. **Crew events** — hebdo bosses, battle, rewards
3. **Groupes PvE** — 4 joueurs, sync instance dungeon
4. **Raids** — 8 joueurs, instances lockout hebdo
5. **Trading post** — ventes entre joueurs, taxe 5%
6. **Chat** — world/crew/private/region, moderation, mute
7. **Friends list** — status, invite, join session
8. **PvP Arena** — 1v1, 3v3 matchmaking ELO
9. **Battleground** — 6v6 capture objectives
10. **Leaderboards** — Redis sorted sets, reset mensuel

### PHASE 12 — Monétisation éthique (1-2 semaines)

1. Schéma DB : `Currency`, `Transaction`, `Purchase`
2. **Premium** : Tanium (payante), Dark Crystals (farmable)
3. **Battle Pass** — 50 niveaux, free + premium track
4. **Cash shop** — cosmetics uniquement (skins, mounts, chat)
5. **Monthly card** — 30 jours login rewards
6. **Stripe / Apple / Google** payment integration
7. **Limiter** : plafond mensuel d'achat configurable par user
8. UI shop avec previews 3D
9. **Anti-fraud** — détection comportements anormaux

### PHASE 13 — Live ops & telemetry (1-2 semaines)

1. **Unity Analytics** + **PostHog** — funnel retention, balance
2. **Grafana dashboards** — CCU, latency, errors, economy
3. **Alerting** — Discord webhook si CCU chute ou erreur spike
4. **A/B testing** — feature flags par user segment
5. **Events limités** — config JSON, schedule cron
6. **Crossover collabs** — système de skins invités
7. **Patch hotfix** — deploy sans downtime (blue/green)
8. **Backup** — PostgreSQL daily + Redis hourly
9. **GDPR** — export/delete user data on request

### PHASE 14 — Polish visuel & anime dynamisme (3-4 semaines)

**C'est ici qu'on applique le style Solo Leveling à fond.**

1. **Toon shader** URP — ramp shading 3-5 bandes
2. **Outlines** — shader outline épais noir
3. **Post-processing** — bloom sélectif, chromatic aberration, vignette, film grain
4. **Hitstop tuning** — 3-8 frames selon type de hit
5. **Screen shake** — directionnel, dégressif, paramétrable
6. **Slow-motion** — 0.3x sur ultimates, parades, dodge parfaits
7. **Slash trails** — VFX Graph sur armes
8. **Particle bursts** — élémentaux, colorés
9. **Camera kick + zoom** — sur criticaux et finishes
10. **Motion blur** — sur mouvements rapides
11. **Frame freeze** — 1 frame blanche à l'impact
12. **Facial animations** — blend shapes émotions
13. **Lip sync** — Oculus LipSync ou custom
14. **VFX cutscenes** — pour ultras et boss kills
15. **Color grading** — LUT par région (chaude/froide/sombre)

### PHASE 15 — Polish UX & accessibility (2 semaines)

1. **Tutorial** — onboarding interactif 5 min
2. **Accessibility** :
   - Colorblind modes (protanopia, deuteranopia, tritanopia)
   - Subtitles pour voix
   - Remap keys
   - Font scaling
   - Reduce motion (désactive hitstop/shake)
3. **Localization** — FR/EN/JP/CN/KR avec QA natifs
4. **Audio adaptatif** — BGM calme/exploration/combat/boss
5. **Settings** : graphismes (low/med/high/ultra), audio, controls, language
6. **Bug bash** — 1 semaine avec 50 testeurs
7. **Stress test** — 10k comptes simulés
8. **Soft launch** — 1 region (Canada/SEA), itère 2 semaines

### PHASE 16 — Launch & post-launch (continu)

1. **Global launch** — rollback plan + on-call rotation
2. **Live ops calendar** — events, banners, updates
3. **Community management** — Discord, Reddit, social
4. **Balance patches** — basés sur telemetry
5. **New content** — simulacra, régions, modes, tous les mois

---

## 📋 ASSETS À DEMANDER À L'UTILISATEUR

> L'IA ne peut pas coder ces éléments. **Avant de commencer chaque phase nécessitant ces assets, l'IA DOIT demander à l'utilisateur** via une question claire.

### Modèles 3D (characters, enemies, bosses)
**L'IA doit demander :**
> « Pour la phase X, j'ai besoin du modèle 3D de [personnage/ennemi]. Peux-tu me fournir :
> - Fichier FBX ou Blender (.blend) du modèle riggé Humanoid
> - Textures (albedo, normal, metallic, emission) en PNG 4K
> - Palette de couleurs souhaitée (ou référence image)
> - Si pas de modèle, fournis-moi une **référence visuelle** (image, concept art) et je te dirai quel asset store / artiste contacter. »

**Liste des modèles à demander :**
- [ ] 10+ personnages jouables (Simulacra) — modèle + textures + rig
- [ ] 20+ types d'ennemis (mobs + élites)
- [ ] 5+ boss (avec phases visuelles)
- [ ] 10+ PNJ (marchands, quest givers, companions)
- [ ] 8+ véhicules (mounts)

### Environnements 3D
**L'IA doit demander :**
> « Pour la région [X], j'ai besoin de :
> - Tileset/modulaire (murs, sols, props) en FBX
> - Textures PBR (albedo, normal, roughness, AO)
> - Skybox (HDRI ou cubemap)
> - Végétation (arbres, herbes, rochers)
> - Référence visuelle de l'ambiance souhaitée »

**Liste :**
- [ ] 6+ régions (Astra, Banges, Navia, Crown, Warren, Artificial Island)
- [ ] 10+ dungeons (instances)
- [ ] 5+ cities/hubs
- [ ] Skyboxes par région et par time-of-day

### Animations
**L'IA doit demander :**
> « Pour [personnage], j'ai besoin des animations :
> - idle, walk, run, jump, fall, land
> - attack combo 1-5, charged attack, ultimate
> - dash, dodge, parry, hit reaction, death
> - Fichiers FBX séparés, rig Humanoid compatible
> - Si pas d'animation, je peux utiliser Mixamo / Unreal marketplace comme base, à valider avec toi. »

**Liste :**
- [ ] Animations par simulacrum (20+ animations chacun)
- [ ] Animations ennemis (10+ par type)
- [ ] Animations boss (30+ par boss avec phases)

### VFX & particules
**L'IA doit demander :**
> « Pour les VFX de combat, j'ai besoin de références visuelles pour :
> - Slash trails par élément (flame, ice, volt, shadow)
> - Hit bursts par élément
> - Ultimate cutscenes (court, 1-2s)
> - Status effect particles (burn, freeze, poison, stun)
> - Fournis-moi des GIFs ou vidéos de référence (Solo Leveling, ToF, Genshin) »

### Audio
**L'IA doit demander :**
> « Pour l'audio, j'ai besoin de :
> - BGM par région (3-5 tracks par région, loopables, sans droits)
> - BGM combat (calme, intensif, boss)
> - SFX combat (hits, slashes, parries, ultimates, deaths)
> - SFX UI (clicks, hovers, confirmations)
> - Voice lines par simulacrum (FR/EN/JP — 50+ lignes par perso)
> - Fournis-moi des références de style (compositeur, OST existante) »

### UI / 2D art
**L'IA doit demander :**
> « Pour l'UI, j'ai besoin de :
> - Icons d'objets (100+ items, weapons, consumables)
> - Portraits de simulacra (splash art)
> - Logos et branding
> - Frames/borders pour UI (rareté N/R/SR/SSR)
> - Fournis-moi le style guide visuel ou des références (ToF, Genshin) »

### Dialogues & scénario
**L'IA doit demander :**
> « Pour les dialogues, j'ai besoin de :
> - Script complet de la main quest (5 acts)
> - Side quests (50+)
> - Lore bible (factions, histoire, géographie)
> - Character backstories (10+ simulacra)
> - Soit tu écris le texte, soit je peux le générer en style générique à valider avec toi. »

### Cosmetics
**L'IA doit demander :**
> « Pour les cosmetics, j'ai besoin de :
> - Skins alternatifs par simulacra (3+ skins par perso)
> - Mount skins
> - Chat bubbles, nameplates, emotes
> - Références visuelles ou assets 3D »

---

## ⚙️ PARAMÈTRES À CODER — INVENTAIRE EXHAUSTIF

### A. Paramètres joueur (PlayerProfile)
```csharp
[System.Serializable]
public class PlayerProfile {
    public string Id;
    public string UserId;
    public string DisplayName;
    public string AvatarUrl;
    public int Level;                    // 1-80
    public long Xp;
    public long XpToNext;
    public int PowerLevel;               // gear score calculé
    public StatsBlock BaseStats;
    public Currencies Currencies;
    public List<string> SimulacraUnlocked;
    public string SimulacraActive;
    public string[] WeaponsEquipped = new string[3];
    public Dictionary<ArmorSlot, string> ArmorEquipped = new();
    public string[] MatricesEquipped = new string[4];
    public Dictionary<int, string> RelicSet = new();
    public string CrewId;
    public DateTime LastLoginAt;
    public long PlaytimeSeconds;
    public DateTime CreatedAt;
}

[System.Serializable]
public class StatsBlock {
    public float Hp;
    public float Attack;
    public float Defense;
    public float CritRate;               // 0-1
    public float CritDamage;             // 0-10
    public Dictionary<Element, float> ElementalMastery = new();
    public float MoveSpeed;
    public float DashStamina;
    public float AttackSpeed;
    public float CooldownReduction;
}

[System.Serializable]
public class Currencies {
    public long DarkCrystals;            // farmable premium
    public long Tanium;                  // paid premium
    public long Gold;
    public long BlackGold;               // crew currency
    public Dictionary<string, long> EventTokens = new();
}

public enum ArmorSlot { Head, Hands, Feet, Chest, Legs, Shoulders, Belt }
public enum Element { Flame, Ice, Volt, Physical, Altered, Shadow }
public enum WeaponType { Tank, DPS, Support, Benediction }
public enum Rarity { N, R, SR, SSR }
```

### B. Paramètres Simulacrum
```csharp
[CreateAssetMenu(fileName = "NewSimulacrum", menuName = "Game/Simulacrum")]
public class SimulacrumData : ScriptableObject {
    [Header("Identity")]
    public string Id;
    public string DisplayName;
    public Rarity Rarity;
    public Element Element;
    public WeaponType WeaponType;
    public string WeaponId;
    [TextArea] public string LoreText;
    public Sprite SplashArt;
    public GameObject ModelPrefab;
    public Sprite Portrait;
    
    [Header("Voice")]
    public AudioClip[] VoiceLinesJp;
    public AudioClip[] VoiceLinesEn;
    public AudioClip[] VoiceLinesFr;
    
    [Header("Stats")]
    public StatsBlock BaseStats;
    
    [Header("Skills")]
    public SkillData NormalAttack;
    public SkillData WeaponSkill;
    public SkillData DischargeSkill;
    
    [Header("Awakening")]
    public AwakeningNode[] AwakeningTree;  // 50+ nodes
    public int MaxAwakeningLevel = 6;
    
    [Header("Talents")]
    public TalentSpec PassiveTalent;
    public TalentSpec ActiveTalent1;
    public TalentSpec ActiveTalent2;
    
    [Header("Bond")]
    public BondDialogue[] BondDialogues;   // par niveau d'affection
}

[System.Serializable]
public class SimulacrumInstance {
    public string Id;
    public string SimulacrumId;
    public string OwnerId;
    public int DupesCount;
    public int AwakeningLevel;
    public List<string> UnlockedTalents = new();
    public int AscensionStars;             // 1-6
    public int BondLevel;                  // 1-10
}
```

### C. Paramètres Arme
```csharp
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Game/Weapon")]
public class WeaponData : ScriptableObject {
    public string Id;
    public string DisplayName;
    public Rarity Rarity;
    public Element Element;
    public WeaponType WeaponType;
    public float BaseAttack;
    public GameObject ModelPrefab;
    public Sprite Icon;
    
    [Header("Normal Combo")]
    public ComboStep[] NormalCombo;        // 5-7 hits
    
    [Header("Weapon Skill")]
    public SkillData WeaponSkill;
    
    [Header("Discharge (Ultimate)")]
    public SkillData DischargeSkill;
    
    [Header("Stats")]
    public float ShatterEffect;            // break gauge dmg
    public float ChargeRate;               // jauge 0-1000
    public string[] ResonanceTags;
}

[System.Serializable]
public class ComboStep {
    public string AnimationTrigger;
    public float DamageMultiplier;
    public float StaminaCost;
    public float ChargeGenerated;
    public VfxSpec VfxOnHit;
    public SfxSpec SfxOnHit;
    public bool LiftsEnemy;                // pour aerial combo
    public bool CausesStagger;
}

[System.Serializable]
public class WeaponInstance {
    public string Id;
    public string WeaponId;
    public string OwnerId;
    public int EnhancementLevel;           // 0-30
    public int AdvancementStars;           // 1-6
    public string[] InsertedMatrices = new string[4];
}
```

### D. Paramètres combat
```csharp
[System.Serializable]
public class CombatState {
    public string AttackerId;
    public string TargetId;
    public string WeaponUsed;
    public SkillType SkillType;            // normal, skill, discharge, dash, parry
    public Element Element;
    public float BaseDamage;
    public float FinalDamage;              // après def/res/crit
    public bool IsCrit;
    public bool IsWeakness;                // élément favorable
    public bool IsResisted;
    public float ShatterDamage;
    public float ChargeGenerated;
    public List<BuffInstance> BuffsApplied = new();
    public List<BuffInstance> DebuffsApplied = new();
    public Vector3 AttackerPos;
    public Vector3 TargetPos;
    public long Timestamp;
    
    [Header("VFX")]
    public VfxSpec HitVfx;
    public HitstopSpec Hitstop;
    public ScreenShakeSpec Shake;
}

public enum SkillType { Normal, WeaponSkill, Discharge, Dash, Parry, ChargedAttack }

[System.Serializable]
public class BuffInstance {
    public string Id;
    public BuffType Type;                  // 50+ types
    public int Stacks;
    public float Duration;
    public float Potency;
    public string Source;                  // simulacrum/weapon/relic
}

public enum BuffType {
    // Positive
    AttackUp, DefenseUp, SpeedUp, CritUp, CritDamageUp,
    Shield, HealOverTime, Rage, ElementalUp,
    // Negative
    Poison, Burn, Freeze, Stun, Slow, Weakness,
    DefenseDown, AttackDown, Bleed, Confuse, Silence
}
```

### E. Paramètres ennemis
```csharp
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Game/Enemy")]
public class EnemyData : ScriptableObject {
    public string Id;
    public string DisplayName;
    public string Family;
    public int Level;
    
    [Header("Stats")]
    public float Hp;
    public float Attack;
    public float Defense;
    public Dictionary<Element, float> ElementalResistances = new();  // -1 immune, 0 normal, 1 weak
    
    [Header("Break System")]
    public float ShatterThreshold;
    public float ShatterRecovery;
    
    [Header("AI")]
    public AIArchetype AiArchetype;        // melee, ranged, caster, boss, summoner
    public BehaviorTreeData BehaviorTree;
    public float AggroRange;
    public float LeashRange;
    public AttackPattern[] AttackPatterns;
    
    [Header("Loot")]
    public string LootTableId;
    public long XpReward;
    public long CreditReward;
    
    [Header("Visuals")]
    public GameObject ModelPrefab;
    public GameObject DeathEffect;
    public GameObject HitEffect;
    public AnimationClip[] Animations;
}

public enum AIArchetype { Melee, Ranged, Caster, Boss, Summoner, Turret }
```

### F. Paramètres région / monde
```csharp
[CreateAssetMenu(fileName = "NewRegion", menuName = "Game/Region")]
public class RegionData : ScriptableObject {
    public string Id;
    public string DisplayName;
    public Vector2Int LevelRange;
    
    [Header("Map")]
    public string ScenePath;
    public Vector2Int Size;                // tiles
    public int ChunkSize = 32;
    
    [Header("Climate")]
    public WeatherType[] WeatherTypes;
    public string AmbientMusicId;
    public string[] AmbientSfxIds;
    
    [Header("POIs")]
    public POI[] ScenicPoints;
    public POI[] SupplyPods;
    public POI[] Rifts;
    
    [Header("Spawning")]
    public MobSpawn[] MobSpawns;
    public BossSpawn[] BossSpawns;
    
    [Header("Gating")]
    public string RequiredQuestId;
    
    [Header("Visuals")]
    public Color FogColor;
    public float FogDensity;
    public Material SkyboxMaterial;
    public string[] ParticleEffectIds;
}
```

### G. Paramètres quêtes
```csharp
[CreateAssetMenu(fileName = "NewQuest", menuName = "Game/Quest")]
public class QuestData : ScriptableObject {
    public string Id;
    public QuestType Type;
    public string Title;
    [TextArea] public string Description;
    public string GiverNpcId;
    public string[] Prerequisites;
    public QuestStep[] Steps;
    public Reward[] Rewards;
    public bool Repeatable;
    public ResetSchedule ResetSchedule;
    public bool AutoAccept;
    public int SuggestedLevel;
}

public enum QuestType { Main, Side, Daily, Weekly, Hidden, Event }
public enum ResetSchedule { None, Daily, Weekly }

[System.Serializable]
public class QuestStep {
    public string Id;
    public QuestStepType Type;
    public string Target;                  // mob/item/NPC/zone ID
    public int Count;
    public bool Optional;
    public string DialogueTrigger;
    public string NextStepId;
}

public enum QuestStepType { Kill, Collect, Talk, Reach, Interact, Defend, Escort }
```

### H. Paramètres économie
```csharp
[CreateAssetMenu(fileName = "EconomyConfig", menuName = "Game/Economy")]
public class EconomyConfig : ScriptableObject {
    [Header("Drop Rates")]
    public float GlobalDropRateMultiplier = 1f;
    
    [Header("Enhancement")]
    public int[] EnhancementCosts = new int[30];
    public string[] EnhancementMats = new string[30];
    public float[] EnhancementSuccessRate = new float[30];
    
    [Header("Advancement")]
    public AdvancementCost[] AdvancementCosts = new AdvancementCost[6];
    
    [Header("Gacha")]
    public GachaRates GachaRates;
    public int PityThreshold = 80;
    public int HardPityThreshold = 120;
    
    [Header("Trading")]
    public float TradeTaxRate = 0.05f;
    public long TradeMinPrice = 100;
    public long TradeMaxPrice = 1000000;
    
    [Header("Daily Login")]
    public Reward[] DailyLoginRewards;
}

[System.Serializable]
public class GachaRates {
    public float SSR = 0.0075f;
    public float SR = 0.07f;
    public float R = 0.70f;
    public float N = 0.2225f;
}
```

### I. Paramètres UI / HUD
```csharp
[System.Serializable]
public class UIConfig {
    public UITheme Theme = UITheme.Cyan;
    [Range(0, 1)] public float HudOpacity = 1f;
    public bool DamageNumbersEnabled = true;
    public DamageNumberSize DamageNumbersSize = DamageNumberSize.Medium;
    [Range(0, 1)] public float ScreenshakeIntensity = 1f;
    public bool HitstopEnabled = true;
    public float FloatingCombatTextDuration = 1.5f;
    
    [Header("Minimap")]
    public float MinimapScale = 1f;
    public bool MinimapShowPlayers = true;
    public bool MinimapShowMobs = true;
    public bool MinimapShowCrew = true;
    
    [Header("Touch Controls (mobile)")]
    public float JoystickSize = 120f;
    public float JoystickOpacity = 0.7f;
    public float ButtonSize = 90f;
    public float ButtonOpacity = 0.7f;
    public bool LeftHandedMode = false;
    public float JoystickSensitivity = 1f;
    
    [Header("Controls")]
    public Dictionary<string, string> Keybinds = new();
    
    [Header("Accessibility")]
    public ColorblindMode ColorblindMode = ColorblindMode.Off;
    public float FontScale = 1f;
    public bool SubtitlesEnabled = true;
    public bool ReduceMotion = false;
}

public enum UITheme { Cyan, Volt, Flame, Ice, Altered, Shadow }
public enum DamageNumberSize { Small, Medium, Large }
public enum ColorblindMode { Off, Protanopia, Deuteranopia, Tritanopia }
```

### J. Paramètres netcode
```csharp
[System.Serializable]
public class NetworkConfig {
    public int TickRate = 20;              // Hz positions
    public int SnapshotBufferSizeMs = 100;
    public int InterpolationDelayMs = 100;
    public int MaxReconnectTimeSec = 300;
    
    [Header("Interest Management")]
    public float ViewDistance = 100f;      // tiles
    public float RelevancyThreshold = 0.1f;
    
    [Header("Rate Limits")]
    public int MaxPacketsPerSecond = 60;
    public int ChatRateLimit = 5;          // msgs / 10s
    
    [Header("Anti-cheat")]
    public float MaxSpeedThreshold = 15f;  // tiles/sec
    public float MaxDamagePerHit = 100000f;
    public bool AuditLogEnabled = true;
}
```

### K. Paramètres cosmétiques
```csharp
[CreateAssetMenu(fileName = "NewCosmetic", menuName = "Game/Cosmetic")]
public class CosmeticData : ScriptableObject {
    public string Id;
    public CosmeticType Type;
    public Rarity Rarity;
    public CosmeticSource Source;
    public CurrencyPrice Price;
    public GameObject AssetPrefab;
    public string PreviewAnimation;
}

public enum CosmeticType { Skin, Mount, ChatBubble, Nameplate, Portrait, Emote }
public enum CosmeticSource { Shop, Event, Achievement, BattlePass }
```

---

## 🔐 ANTI-CHEAT — STRATÉGIE OBLIGATOIRE

| Risque | Mitigation |
|---|---|
| Speed hack | Validation vitesse serveur (max 1.2x moveSpeed) |
| Damage hack | Tout dégât calculé serveur, client n'envoie que "I attacked with weapon X at time T" |
| Duplication | Transactions DB atomiques + inventory locks |
| Packet replay | Nonces + timestamps, drop si delta > 2s |
| Aimbot | Valider hitbox server-side (lag compensation rewind) |
| Wall hack | Culling serveur (interest management) |
| Bot farming | Captcha périodique + détection patterns + rate limits |
| Memory editing | Cheat Engine → données critiques serveur uniquement |
| Trade scam | Confirmation 2FA + trade log audit |
| Gacha manipulation | RNG seedé serveur, audit trail complet |

---

## 📊 TELEMÉTRIE & BALANCE

### Métriques à tracker (Unity Analytics + PostHog + Grafana)
- **Acquisition** : DAU/MAU, retention J1/J7/J30, LTV, CAC
- **Engagement** : session length, quests/day, gacha pulls/day, regions visited
- **Economy** : gold flow, dark crystals velocity, trade volume, inflation
- **Combat balance** : win rate par simulacrum/weapon/boss, TTK, death causes
- **Performance** : FPS, latency, crash rate, OOM, battery drain (mobile)
- **Monetization** : ARPU, ARPPU, conversion rate, top items
- **Social** : crew size, group rate, chat messages, PvP participation

### A/B tests à planifier
- Taux gacha (0.75% vs 1%)
- Coût enhancement
- Difficulté boss
- UI onboarding flow
- Notifications push timing
- Battle pass rewards curve

---

## 🚀 DÉPLOIEMENT & INFRA

### Environnements
| Env | Usage | Branch | URL |
|---|---|---|---|
| local | Dev | main | localhost |
| dev | Tests auto | PR | dev.game.example |
| staging | QA interne | develop | staging.game.example |
| preprod | Soak tests | release/* | preprod.game.example |
| prod | Live | main tagged | play.game.example |

### Stack cloud
- **Compute** : Kubernetes (EKS/GKE/AKS)
- **DB** : RDS PostgreSQL (multi-AZ, read replicas)
- **Cache** : ElastiCache Redis (cluster mode)
- **CDN** : CloudFront pour assets Addressables
- **Realtime** : Nakama cluster (Go binaries)
- **Storage** : S3 pour saves/screenshots/uploads
- **DNS** : Route53 + Cloudflare (DDoS protection)
- **Monitoring** : Datadog (APM) + Grafana + Sentry (errors)
- **CI/CD** : GitHub Actions → Unity Cloud Build → ECR → ArgoCD

### Scaling prévisionnel
- 1k joueurs simultanés : 3 pods Nakama (4 vCPU/8 Go chacun)
- 10k joueurs : 15 pods + 2 read replicas + Redis cluster 6 nodes
- 100k joueurs : 50 pods + sharding par région + CDN edge

---

## ✅ CHECKLIST FINALE — AVANT DE COMMENCER À CODER

- [ ] GDD validé par toute l'équipe
- [ ] Style guide visuel approuvé (ToF + Solo Leveling)
- [ ] Schémas ScriptableObjects définis et validés
- [ ] Pipeline asset testé end-to-end (FBX → Unity → Addressables)
- [ ] Repo Git initialisé avec Git LFS
- [ ] Unity 6000.0 LTS installé + URP configuré
- [ ] CI GitHub Actions verte (build + tests)
- [ ] DB schema Nakama validé
- [ ] 1 simulacrum + 1 arme + 1 ennemi en data
- [ ] Prototype graybox 1 zone jouable
- [ ] Anti-cheat spec écrit et review
- [ ] Plan de tests (unit + integration + e2e + balance)
- [ ] Budget et timeline validés
- [ ] Legal review (RGPD, loot box disclosure, age rating)
- [ ] **Assets visuels demandés à l'utilisateur** (modèles 3D, VFX, audio, dialogues)

---

## 🎨 CRÉATION DE PERSONNAGE 3D — MÉTHODE CHOISIE

> **Question :** Peut-on créer un vrai personnage 3D pour le jeu depuis une image 2D ?
> **Réponse :** Oui, via un **pipeline Image-to-3D API**. C'est la meilleure option pour la qualité.

### ❌ Méthodes écartées

| Méthode | Pourquoi écartée |
|---|---|
| Mesh procédural C# dans Unity | Blocqueux, qualité type Roblox, pas niveau ToF |
| Sculpter à la main dans Blender | Je ne peux pas ouvrir Blender/sculpter |
| Générer un `.psd` | Ce n'est pas un fichier 3D, juste un concept art |
| Acheter sur asset store | Pas personnalisé au personnage de l'utilisateur |

### ✅ Méthode choisie : Pipeline Image-to-3D (Meshy.ai + Mixamo + Unity)

**Flow complet** (script Python + Unity) :

```
Image 2D de l'utilisateur
       ↓
[1] Script Python : appelle Meshy.ai API
       → envoie l'image
       → reçoit un .glb (mesh + textures PBR, ~10k polys)
       (durée : 1-3 minutes, coût : ~1$ ou gratuit avec quota)
       ↓
[2] Auto-rig via Mixamo (semi-manuel, 5 min)
       → upload du .glb sur mixamo.com
       → auto-détection des joints
       → download .fbx riggé Humanoid
       ↓
[3] Import dans Unity
       → .fbx placé dans Assets/_Project/Characters/Player/Models/
       → Animation Type = Humanoid
       → Apply
       ↓
[4] Application du Toon Shader URP (déjà codé)
       → StellarOdyssey/Toon shader appliqué
       → Cell-shading + outlines noirs manga
       → Palette personnalisée (noir/cyan/bleu)
       ↓
[5] Animations procédurales (déjà codées dans PlayerAnimator3D.cs)
       → idle, walk, run, jump, attack combos, dash, hit reaction
       → Pas besoin de fichiers .anim externes
       ↓
[6] Prefab Unity assemblé via config JSON
       → CharacterGenerator + PlayerController3D + PlayerAnimator3D + PlayerCombat3D
       → Personnage prêt à jouer
```

### Fichiers fournis

| Fichier | Rôle |
|---|---|
| `unity-project/tools/generate_character_3d.py` | Script Python qui pilote Meshy.ai API + génère config Unity |
| `unity-project/Assets/_Project/Characters/Player/CharacterGenerator.cs` | Générateur de personnage procédural (fallback si pas d'API) |
| `unity-project/Assets/_Project/Characters/Player/PlayerAnimator3D.cs` | Animations procédurales dynamiques |
| `unity-project/Assets/_Project/Characters/Player/PlayerController3D.cs` | Contrôleur mouvement 3rd person |
| `unity-project/Assets/_Project/Characters/Player/PlayerCombat3D.cs` | Système de combat (combos + hitstop + VFX) |
| `unity-project/Assets/_Project/Rendering/ToonShader.shader` | Shader URP cell-shaded + outlines |

### Comment lancer le pipeline

```bash
# 1. Installer les dépendances
pip install requests

# 2. Créer un compte Meshy.ai (gratuit, 50 crédits/mois)
#    → https://meshy.ai
#    → Récupérer la clé API dans le dashboard

# 3. Lancer le pipeline avec ton image
cd unity-project/tools
python generate_character_3d.py \
  --image ./mon_personnage.jpg \
  --name "Shiro" \
  --api-key MESHY_xxxxxxx

# 4. Suivre les instructions de rigging (RIGGING_INSTRUCTIONS.txt)
# 5. Importer le .fbx dans Unity
```

### Qualité attendue

| Aspect | Niveau |
|---|---|
| Polycount | ~10 000 polys (mobile-friendly) |
| Textures | PBR (albedo + normal + metallic) |
| Rig | Humanoid (compatible Unity Animator) |
| Cell-shading | Appliqué via ToonShader URP |
| Animations | Procédurales dynamiques (déjà codées) |
| Dynamisme | Hitstop + screen shake + slow-mo (déjà codés) |

### Limites assumées

- La qualité ne sera pas 100% Tower of Fantasy (ToF = 3D artists pro pendant des semaines)
- Le mesh peut nécessiter des retouches dans Blender (cleanup topology)
- Le rig Mixamo est générique (pas parfait pour postures exotiques)
- Les textures PBR seront converties en cell-shaded via le toon shader

### Améliorations possibles (phases ultérieures)

1. **Retopology** dans Blender (ZRemesher) pour mesh plus propre
2. **Custom rig** dans Blender (Auto-Rig Pro) pour animations plus naturelles
3. **Texture painting** custom pour style anime plus poussé
4. **Blend shapes** pour expressions faciales
5. **Multiple LODs** pour perf mobile

---

## 🤖 IA NIVEAU 1 — SYSTÈME COMPLET

> **Question :** Peut-on mettre une IA en arrière-plan pour gérer personnages et mouvements ?
> **Réponse :** Oui. Niveau 1 = Behavior Trees (gratuit, déterministe, performant).
> Code un système complet prenant en compte le maximum de paramètres.

### Architecture du système IA Niveau 1

```
                    ┌─────────────────────┐
                    │   EnemyAI3D.cs      │  (cerveau, assemble tout)
                    └──────────┬──────────┘
           ┌───────────────────┼───────────────────┐
           ↓                   ↓                   ↓
  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐
  │ BehaviorTree   │  │ Perception     │  │ EmotionSystem  │
  │ (runtime BT)   │  │ (vue+ouïe+mémoire)│  │ (peur/colère)  │
  └────────────────┘  └────────────────┘  └────────────────┘
           ↓                   ↓                   ↓
  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐
  │ GroupTactics   │  │ Pathfinding    │  │ EnemyHealth3D  │
  │ (flanking/help)│  │ (A*+steering)  │  │ (status+shatter)│
  └────────────────┘  └────────────────┘  └────────────────┘
```

### Paramètres pris en compte (inventaire exhaustif)

#### A. Behavior Tree (BT runtime complet)
- **Composites** : Sequence (AND), Selector (OR), Parallel
- **Decorators** : Inverter, Repeater, Retry, Cooldown, Timeout
- **Leaves** : ActionNode (Func<NodeState>), ConditionNode (Func<bool>)
- **Blackboard** : mémoire partagée (lastKnownPos, emotions, status, etc.)

#### B. Perception (vue + ouïe + mémoire)
| Paramètre | Valeur par défaut | Description |
|---|---|---|
| `viewDistance` | 15 m | Distance de vision |
| `viewAngle` | 120° | Cône de vision |
| `eyeHeight` | 1.6 m | Hauteur des yeux |
| `visionBlockMask` | obstacles | Couches qui bloquent la vue |
| `hearingRadius` | 8 m | Rayon d'audition (pas, dialogues) |
| `combatNoiseRadius` | 15 m | Rayon pour bruits de combat |
| `memoryDuration` | 5 s | Durée de mémoire après perte de vue |
| `investigateRadius` | 4 m | Zone d'investigation |
| `detectionSpeed` | 2/s | Vitesse de remplissage jauge détection |
| `detectionThreshold` | 1.0 | Seuil d'alerte |
| `loseSightSpeed` | 3/s | Vitesse de perte de détection |

**Fonctionnalités** :
- Détection progressive (jauge qui se remplit, pas binaire)
- Occlusion (raycast pour vérifier les obstacles)
- Alertes propagées aux alliés proches
- Mémoire de dernière position connue
- Investigation de la dernière position

#### C. Émotions (peur, colère, curiosité, confiance)
| Émotion | Plage | Effets |
|---|---|---|
| `fear` | 0-1 | >0.7 → fuite, >0.5 → prudent |
| `anger` | 0-1 | >0.8 → berserk (+50% attack speed, +30% damage) |
| `curiosity` | 0-1 | >0.6 → investigation des bruits |
| `confidence` | 0-1 | <0.3 → cautious approach, esquive fréquente |

**Triggers émotionnels** :
- `OnTakeDamage(amount, isCrit)` → +peur, +colère, -confiance
- `OnAllyDied()` → +peur, -confiance
- `OnSuccessfulAttack()` → +confiance, +colère
- `OnPlayerSpotted()` → +colère, -curiosité
- `OnLostPlayer()` → +curiosité
- `OnKillPlayer()` → confidence=1, anger=0, fear=0

#### D. Tactique de groupe
| Paramètre | Description |
|---|---|
| `factionId` | Identifiant de faction (ennemis d'une même faction collaborent) |
| `allyDetectionRadius` | Rayon de détection des alliés |
| `callHelpThreshold` | HP < 30% → appelle à l'aide |
| `callHelpRadius` | Portée de l'appel à l'aide |
| `maxAttackersSimultaneous` | Max 3 ennemis attaquant en même temps (anti swarm) |
| `useFlanking` | Active le flanking (encerclement) |
| `flankSpreadAngle` | Angle de dispersion en cercle |
| `preferredDistance` | Distance préférée selon le rôle |

**Rôles de combat** :
- `Tank` → front, garde distance courte (1.5m)
- `DPS` → côtés, distance moyenne (3m)
- `Ranged` → arrière, distance longue (6m)
- `Support` → arrière, très longue distance (8m), soigne/buff

**Slots d'attaque** :
- Attribution dynamique (slot 0, 1, 2)
- Libération après attaque
- Limite d'attaquants simultanés réglable

#### E. Pathfinding & steering
| Behavior | Description |
|---|---|
| `Seek` | Se déplace vers un point |
| `Flee` | Fuit un point (panique si proche) |
| `Arrive` | Seek avec décélération à l'arrivée |
| `Wander` | Déplacement aléatoire (patrouille) |
| `ObstacleAvoidance` | Évite les obstacles (spherecast) |
| `Separation` | Évite les autres ennemis (anti-stack) |

#### F. Système de santé & status effects
| Status | Effet |
|---|---|
| `Burn` | DoT 5 HP/s |
| `Freeze` | Stun + ralentissement |
| `Poison` | DoT 8 HP/s |
| `Stun` | Immobile, incapable d'attaquer |
| `Slow` | -50% vitesse |
| `Weakness` | -30% dégâts |
| `Bleed` | DoT + saignement visuel |
| `Confuse` | Mouvements aléatoires |
| `Silence` | Ne peut pas utiliser compétences |

**Shatter gauge** (système de break) :
- S'accumule avec les dégâts
- À 100% → stun 2s + ouverture aux critiques
- Récupération progressive

#### G. Résistances élémentaires
| Élément | Effet |
|---|---|
| -1 | Immunisé (0 dégâts) |
| 0 | Normal |
| +1 | Faiblesse (dégâts x2) |

6 éléments : Flame, Ice, Volt, Physical, Altered, Shadow

#### H. Difficulté scalable
| Niveau | Aggro Range | Damage | Cooldown | Reaction |
|---|---|---|---|---|
| Easy | x0.6 | x0.6 | x1.5 | x0.5 |
| Normal | x1.0 | x1.0 | x1.0 | x1.0 |
| Hard | x1.4 | x1.4 | x0.7 | x1.5 |
| Nightmare | x1.8 | x1.8 | x0.5 | x2.0 |

#### I. Day/Night behavior
- `dayAggroMultiplier` = 1.0 (jour)
- `nightAggroMultiplier` = 1.4 (nuit, ennemis plus agressifs)
- Détection via `isNighttime` bool

### Arbre de comportement typique (Root)

```
ROOT (Selector priority)
├── 1. DEAD → do nothing
├── 2. STUNNED → wait (status: stun/freeze)
├── 3. FLEE → if fear > 0.7 AND anger < 0.8
│   └── Flee player + Seek spawn + CallForHelp
├── 4. BERSERK → if anger > 0.8
│   └── Charge player + Attack (ignore fear)
├── 5. INVESTIGATE → if heard/saw but lost player
│   └── Move to lastKnownPos + search pattern
├── 6. ATTACK → if can see + in range + cooldown ok + slot available
│   └── Face player + Deal damage + Release slot
├── 7. CHASE → if can see player, not in range
│   └── Flank position + Arrive + ObstacleAvoid
├── 8. RETREAT → if too far from spawn (> leashDistance)
│   └── Arrive spawn
└── 9. PATROL (default)
    └── Wander + ObstacleAvoid + Return to spawn if far
```

### Fichiers fournis

| Fichier | Rôle |
|---|---|
| `AI/BehaviorTree.cs` | Runtime BT (Sequence, Selector, Parallel, Decorators, Blackboard) |
| `AI/PerceptionSystem.cs` | Vue cône + ouïe + mémoire + alertes |
| `AI/EmotionSystem.cs` | Peur/colère/curiosité/confiance + triggers |
| `AI/GroupTactics.cs` | Faction, flanking, call help, attack slots, roles |
| `AI/PathfindingAgent.cs` | A* + Seek/Flee/Arrive/Wander/Avoidance/Separation |
| `AI/EnemyHealth3D.cs` | HP + status effects + shatter gauge + résistances |
| `Enemies/EnemyAI3D.cs` | Assemblage complet + BT construction + difficulty scaling |
| `Enemies/Boss3D.cs` | Boss avec 3 phases + 5 patterns d'attaque |
| `Enemies/BossProjectile3D.cs` | Projectiles boss (avec homing optionnel) |
| `Enemies/FloatingDamage3D.cs` | Chiffres flottants colorés (crit = orange) |

### Ce que ce système gère automatiquement (sans coder chaque event)

✅ Détection progressive du joueur (pas binaire)
✅ Perte de vue → investigation de la dernière position
✅ Dégâts reçus → peur + colère + perte de confiance
✅ HP bas → appel à l'aide aux alliés
✅ Allié tué → peur augmente chez les témoins
✅ Trop d'ennemis autour du joueur → slots d'attaque limités (anti-swarm)
✅ Évitement d'obstacles dynamique
✅ Séparation entre ennemis (anti-stack)
✅ Flanking (encerclement du joueur)
✅ Rôles différents (tank front, ranged back, support très back)
✅ Status effects (burn/freeze/poison/stun) modifient le BT
✅ Shatter gauge → stun sur break
✅ Résistances élémentaires
✅ Day/night : aggro plus forte la nuit
✅ 4 niveaux de difficulté (Easy → Nightmare)
✅ Émotions influencent vitesse, dégâts, comportement

### Ce que ce système NE gère pas (niveau 2+)

❌ Dialogues générés par LLM (niveau 3)
❌ Apprentissage par renforcement (niveau 4, ML-Agents)
❌ Quêtes dynamiques émergentes (niveau 3, Utility AI)
❌ PNJ non combattants vivants (niveau 2, Utility AI)

---

## 🎯 PRIORITÉS POUR L'IA QUI CODE

> **Lis cette section en premier avant chaque phase.**

1. **SYSTÈME > DESIGN.** Code un univers fonctionnel avant de polir le visuel.
2. **DEMANDE les assets visuels.** Ne crée pas des assets bidon, demande à l'utilisateur.
3. **DATA-DRIVEN.** Tout contenu en ScriptableObjects, jamais hardcodé.
4. **AUTHORITÉ SERVEUR.** Tout ce qui compte (HP, dégâts, loot, monnaie) est validé serveur.
5. **TOUCH CONTROLS.** Toujours implémenter les contrôles tactiles à l'écran, pas seulement KB/mouse.
6. **ANIME DYNAMISME.** Hitstop, screen shake, slow-mo, slash trails = obligatoires dès le combat de base.
7. **TESTE en continu.** Tests unitaires sur formules, tests playmode sur gameplay.
8. **DOCUMENTE.** Chaque système a son `.md` dans `/docs/`.
9. **BALANCE.** Simulateur de combat pour valider la balance avant release.
10. **L'UTILISATEUR DEMANDE des correctifs design.** Le design s'itère après, sur demande.

---

> **Note finale** : ce document est une **carte de navigation**, pas une doc exhaustive. Chaque section méritera son propre fichier détaillé dans `/docs/` au fur et à mesure. La règle d'or : **autorité serveur sur tout ce qui compte** (HP, dégâts, loot, monnaie) — le client n'est qu'un terminal d'affichage. Le style visuel **anime 3D cell-shaded dynamique type Solo Leveling** est non-négociable. L'IA **doit demander** les assets visuels qu'elle ne peut pas coder.
