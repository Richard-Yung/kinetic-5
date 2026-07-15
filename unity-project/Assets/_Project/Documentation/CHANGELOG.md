# CHANGELOG — KINETICS 5

> Historique des versions de KINETICS 5.
> Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/), versionning [SemVer](https://semver.org/lang/fr/).

---

## [Unreleased]

### Planifié v0.2.0

- UI complète (33 écrans UGUI + UI Toolkit)
- Audio FMOD final (banks + middleware)
- Addressables streaming (scènes missions, audio)
- i18n complète (7 langues : FR, EN, JP, CN, KR, ES, DE)
- Tutorial interactif (5 étapes)
- Onboarding FTUE (first-time user experience)

### Planifié v0.3.0

- Multiplayer co-op 4 joueurs (matchmaker Nakama)
- Leaderboards global/friends/crew (seasons 30 jours)
- Battle Pass (free + premium tiers)
- Shop (CR + premium currency)
- Crew management (clans, roles, wars)

### Planifié v1.0.0

- Beta ouverte TestFlight + Google Play Internal
- Anti-cheat complet (EAC/BattlEye integration)
- Server-side occlusion culling (anti-wallhack)
- Obfuscation binaire (anti-tamper)
- 7 missions complètes + 4 agents + 14 armes

---

## [0.1.0] — 2025-01-15

### ✨ Added — Systèmes Core

- **GameManager** : machine à états globale (Boot/MainMenu/Loading/InMission/Paused/Results),
  transitions asynchrones via UniTask.
- **ServiceLocator** : DI container minimaliste, Dictionary<Type,object>, callbacks différés.
- **GameEventBus** : pub/sub type-safe zero-alloc, 7 events struct (DamageDealt, EnemyKilled,
  MissionComplete, WeaponSwitched, PlayerDamaged, ObjectiveUpdated, LootPickup).
- **ObjectPooler** : pools Stack<Component>, IPooledItem callbacks, PreWarmAll dynamique.
- **SaveSystem** : 3 slots, AES-128-CBC chiffrement, PlayerPrefs fallback, auto-save 60s,
  migrations v1→v2→v3.
- **SceneLoader** : Addressables additive load + fallback SceneManager, fade CanvasGroup/DOTween.
- **AudioManager** : wrapper FMOD (#if KINETICS_FMOD) + fallback AudioSource, BGM crossfade A/B,
  SFX pool Stack<AudioSource> max 32 voices, bus Master/Music/SFX/Voice.
- **LocalizationManager** : 7 langues JSON depuis StreamingAssets, fallback EN, runtime switch.
- **InputManager** : multi-device (Mobile/Desktop/Gamepad), EnhancedTouch API, joystick virtuel
  flottant gauche + swipe droit + boutons UI, InputState struct zero-alloc, rebind interactif,
  haptic feedback mobile+gamepad.
- **CameraManager** : Cinemachine POV + BasicMultiChannelPerlin, recoil kick + recovery,
  aim zoom FOV dynamique, head bob sinusoïdal, Shake(amplitude, freq, duration).
- **TimeManager** : Hitstop (file d'attente max 2, freeze frames 0.02-0.15s), SlowMotion 0.3x 0.5s,
  transitions TimeScale lerp smooth, SetGameplayPaused indépendant du TimeScale UI.
- **TelemetryLogger** : portail GDPR (SetConsent persisté), file LRU 1000 events, batch 30s,
  offline queue PlayerPrefs, Unity Analytics + PostHog stub, helpers typés.
- **Bootstrapper** : RuntimeInitializeOnLoadMethod(BeforeSceneLoad), graphe de dépendances
  topologique (12 systèmes), EnsureBaseSceneLoadedAsync, fallback dégradé.

### ✨ Added — Couche Data

- **DataLoader** : chargeur statique data-driven, auto-boot RuntimeInitializeOnLoadMethod,
  lazy-load EnsureLoaded, verrou thread-safe, caches dictionnaires OrdinaryIgnoreCase,
  accesseurs typés (GetAgent/GetWeapon/GetMission/GetEnemy/GetRegion/GetTactical/GetProgressionCurve),
  validation d'intégrité runtime (warnings), hot-reload éditeur (menus).
- **DataValidator** (Editor) : validation stricte (MissingMemberHandling.Error), parsing JSON,
  schéma (champs obligatoires), intégrité référentielle (mission→ennemi/région, talents prérequis),
  balance (ennemis 0 PV, armes 0 power, paliers non croissants, dropChance hors plage),
  détection de doublons. Menus éditeur "Valider les données" + "Valider (runtime)".
- **18 DTOs POCO** : AgentDto, AbilityDto, TalentNodeDto, WeaponDto, ProjectileDto, MissionDto,
  MissionObjectiveDto, EnemySpawnWaveDto, BossPhaseDto, RewardDataDto, LootTableEntryDto,
  EnvironmentDataDto, EnemyDto, LootDropDto, RegionDto, EnvironmentPresetDto, TacticalDto,
  ProgressionCurveDto, ProgressionLevelDto, Vector3Dto.
- **7 ScriptableObjects** `[CreateAssetMenu]` : AgentSO, WeaponSO, MissionSO, EnemySO, RegionSO,
  TacticalSO, ProgressionCurveSO (AnimationCurve + helpers).
- **17 enums** : AgentClass, WeaponCategory, WeaponType, Rarity, Element, FireMode, MissionType,
  ObjectiveKind, EnemyClass, AIBehavior, AbilityEffectType, TacticalEffectType, ShipType,
  Lighting, Atmosphere, TalentType + helper EnumParser.Parse<T>.
- **HexColorConverter** : JsonConverter<Color> tolérant (#RRGGBB, #RRGGBBAA, [r,g,b,a], {r,g,b,a}).
- **6 fichiers JSON** dans `Resources/Data/` :
  - `agents.json` (4 agents : VULCAN Tank L47, XEN Assault L55, JOLT Support, XANO Recon L55).
  - `weapons.json` (14 armes : 5 primaires, 5 secondaires, 4 tactiques — stats PDF respectées).
  - `enemies.json` (11 ennemis : 7 standards + 4 bosses TITAN/NEURAL_CORE/INTERCEPTOR/OVERLORD).
  - `missions.json` (7 missions : 7 types différents, vagues + bossPhases + rewards + environment).
  - `regions.json` (7 régions avec ambientColor palette KINETICS 5).
  - `progression.json` (60 paliers XP croissante 400 × 1.13^(n-1)).

### ✨ Added — Couche Gameplay

- **DamageCalculator** (statique) : formule de dégâts centralisée (base × elem × hs × crit × dist × armor),
  plafond 9999, méthodes `Calculate` (struct result) et `CalculateFast` (hot path).
- **PlayerController** : FPS mobile+desktop, CharacterController, mouvement WASD, sprint
  (stamina 5s, régén 0.8/s après 1.5s), crouch (hauteur 1.8→1.0m), saut (1.2m, gravité -19.6),
  vitals (Health 100, Shield 50), TakeDamage/Heal/Respawn, état FSM (Idle/Walking/Sprinting/
  Crouching/Jumping/Falling/Dead).
- **EnemyAI** : FSM simple (Patrol/Chase/Attack/Flee/Stunned/Dead), waypoints avec pause,
  détection distance+FoV (120°), attack à cadence configurable, fuite à < 30% HP,
  consomme EnemyDto (BaseHealth, MoveSpeed, AttackRange, AttackRate, BaseDamage).
- **MissionDirector** : orchestrateur de mission, spawn vagues avec delay + spawn progressif,
  suivi objectifs (10 ObjectiveKind dont Survive temps), boss phases, CompleteMission/FailMission,
  ComputeRewards (perfect clear +25%), abonnement EnemyKilledEvent via GameEventBus.

### ✨ Added — Couche Network

- **NakamaClient** : wrapper Nakama Unity SDK, auth (email/device/OAuth Google/Apple/Facebook/Steam),
  session management (token, refresh, expiration), connect/reconnect backoff exponentiel
  (1s→30s, 5 tentatives), mode hors-ligne fallback gracieux, événements SessionExpired/
  Authenticated/ConnectionStateChanged.
- **MatchManager** : multijoueur co-op 2-4 joueurs, modèle Host/Client, sync d'état joueur
  (position/health/weapon), RPC actions (shoot/ability/interact), snapshot interpolation
  (180ms buffer, 20Hz tick), migration host, mode offline (solo local).
- **LeaderboardService** : classements Global/Friends/Crew, soumission score, infos season
  (start/end/daysRemaining), cache TTL 30s, fallback offline (1 entrée fictive).
- **ChatService** : chat World/Crew/DM, socket Nakama Realtime Channels, filtre blasphème
  FR+EN (3 regex compilées), rate limiting (5 msg/10s), HTML sanitize (strip balises),
  longueur max 280 chars, émojis supportés.
- **CloudSaveService** : sync cloud Nakama storage, résolution de conflits (last-write-wins
  pour scalaires, merge union pour collections), push debounce 5s, pull avec merge automatique.
- **AntiCheatValidator** : validation serveur (damage within weapon bounds × 1.15 marge,
  movement speed within agent max × 1.15, fire rate within weapon cap × 1.05, headshot rate
  statistical anomaly > 70% sur 20+ tirs), 5 niveaux de sévérité (None/Suspicious/Confirmed/
  BanTemp/BanPermanent), 3 strikes → ban temp 7 jours, 6 strikes → ban permanent, télémétrie
  PostHog sur chaque infraction.

### ✨ Added — Shaders URP

- **ToonShading.shader** : cel-shaded 3-5 ramp bands, rim light Fresnel, outline inverted hull,
  emission optionnelle, URP Forward + SRPDefaultUnlit outline pass. Pour agents + ennemis.
- **HoloUI.shader** : panneaux holographiques sci-fi, scanlines (sin vertical), glitch (blocs
  aléatoires), RGB split (décalage R/B), alpha pulse (sin), vignette interne. Stencil-compatible.
- **ForceField.shader** : bulle de bouclier, Fresnel Schlick, hex grid pattern procédural,
  4 ripples d'impact simultanés, pulse faible, blend additive.
- **MuzzleFlash.shader** : particule additive, soft edge radial, color tint par élément,
  shimmer scintillement, rotation UV. Compatible Particle System.
- **DamageNumber.shader** : billboard text, outline 4-tap, glow 8-tap, color by element
  (5 éléments : Kinetic/Energy/Cryo/Volt/Explosive), billboard camera-facing optionnel.
- **ShipInterior.shader** : surfaces métalliques PBR-lite, normal grid procédural, emissive
  panels masque R, dirt mask, fog URP + ambient, Blinn-Phong specular.
- **K5PostProcessFeature.cs** : ScriptableRendererFeature URP, branche 2 passes
  (BloomPrePass + Composite).
- **K5PostProcess.shader** : composite post-FX (bloom sélectif + chromatic aberration + vignette
  + film grain + LUT color grading 32×32×32), multi_compile_local keywords par effet.
- **K5BloomPrePass.shader** : pré-passe bloom (9-tap box blur, threshold soft knee, downsample ×2).

### ✨ Added — Tests Unity Test Framework

- **DataLoaderTests** (EditMode, 20 tests) : chargement global, 4 agents + 14 armes + 7 missions
  + 11 ennemis + 7 régions + 60 paliers, intégrité référentielle (mission→ennemi/région),
  aucun ennemi 0 HP, bosses > 10000 HP, XP croissante par palier, GetLevelForXp/GetXpToNextLevel.
- **DamageCalculatorTests** (EditMode, 15 tests) : formule base, cap 9999, jamais négatif,
  multiplicateurs élémentaires (weakness ×1.5, resistance ×0.5), headshot ×2.0, crit ×1.5,
  headshot+crit non cumulables (max), distance falloff linéaire, armure réduit, edge cases
  (0 HP léthal, overkill détecté), CalculateFast == Calculate.
- **SaveSystemTests** (EditMode, 12 tests) : roundtrip save/load identique, valeurs par défaut,
  SlotExists/DeleteSlot, migration v1→v3 (Resources créés + HapticsEnabled true), chiffrement
  (fichier non JSON en clair), GetSlotMetadata sans charger complet, save corrompue → recovery
  gracieux, slots invalides.
- **EventBusTests** (EditMode, 14 tests) : subscribe/publish, plusieurs handlers, unsubscribe,
  token Dispose, CountHandlers, isolation par type, souscription pendant publish (pas appelée
  cette frame), exception handler ne crash pas, thread safety (pub from autre thread, subscribe/
  unsubscribe multi-thread), events KINETICS 5 (DamageDealtEvent, MissionCompleteEvent), ClearAll.
- **PlayerControllerTests** (PlayMode, 14 tests) : état initial Idle, déplacement avant Z + latéral X,
  aucun input = immobile, saut s'élève > 0.3m + retombe au sol, collision mur ne traverse pas,
  sprint consomme stamina, sans bouger stamina se régénère, TakeDamage réduit health+shield,
  Heal remonte, dégâts fatals = Dead, Respawn rétablit vitals, MaxTheoreticalSpeed borne anti-cheat.
- **EnemyAITests** (PlayMode, 10 tests) : état initial Patrol/Idle, patrouille bouge entre waypoints,
  atteint waypoint puis change index, chase poursuit joueur, attack quand joueur dans range,
  attack reste immobile, flee à < 30% HP, takeDamage réduit HP, dégâts fatals = Dead, FoV ne
  détecte pas joueur derrière.
- **MissionDirectorTests** (PlayMode, 11 tests) : Start succès pour mission existante, échec pour
  inconnue, WaveStarted event déclenché, CurrentWaveIndex >= 0, UpdateObjective complète, FailMission
  passe Failed + event MissionEnded, CompleteMission passe Complete, perfect clear +25%, ComputeRewards
  échec = moitié, Score incrément après kill, timeLimit présent > 0.
- **ObjectPoolerTests** (PlayMode, 12 tests) : Register cree pool PreWarm, doublon identique ignoré,
  Get retourne actif, Release retourne inactif, Get sans position, pool inconnu retourne null,
  saturation dépasse MaxSize avec warning, ClearPool vide inactifs, hot path zero-alloc (< 4 KB
  pour 1000 cycles — assertion GC), IPooledItem callbacks appelées, release objet externe détruit.

### ✨ Added — Documentation FR

- **README.md** (project root, ~250 lignes) : présentation, prérequis, installation (clone +
  OpenUPM + Unity Hub), configuration Nakama/FMOD/Addressables, structure dossiers, ouverture,
  build Android/iOS, configuration réseau, roadmap, licences.
- **ARCHITECTURE.md** (~280 lignes) : vue d'ensemble, 6 couches (Core/Gameplay/Data/UI/Network/
  Shaders), 13 systèmes Core détaillés, patterns (ServiceLocator, EventBus, State Machines, Object
  Pooling, Data-Driven, UniTask), flux de données (boot, démarrage mission, tir, save), graphe
  asmdef, conventions nommage, performance, évolutions futures.
- **GAMEPLAY.md** (~280 lignes) : mouvement FPS (contrôles 3 plateformes, vitesses, stamina),
  combat (hitscan vs projectile, formule dégâts, headshot/crit non cumulables), armement (3 slots,
  14 armes, 3 modes de tir), 5 éléments (Kinetic/Energy/Explosive/Cryo/Volt) + table de résonance,
  ultimate (4 agents × 3 skills + cooldowns), esquive, IA ennemie (FSM + 6 behaviors + détection
  FoV), boss à 3 phases, 7 types de missions, 10 ObjectiveKind, progression (XP/niveaux/mastery/
  arbres éveil), loot 4 raretés, multiplayer co-op, 3 niveaux difficulté.
- **UI_GUIDE.md** (~310 lignes) : palette NON-NÉGOCIABLE (8 couleurs + dérivées), typo (Audiowide/
  Rajdhani/Inter/JetBrains Mono + 9 tailles USS), 6 composants (KButton/KCard/KProgressBar/KModal/
  KToast/KTooltip), 33 écrans listés (8 PDF + 25 extras), flow navigation, mobile touch controls
  (layout HUD + joystick flottant + 9 boutons droits + swipe droit + haptic), accessibilité
  (contrastes WCAG AA, daltonisme, sous-titres, réduction mouvement), i18n (7 langues + fallback),
  UI Toolkit vs UGUI, conventions nommage USS/UGUI.
- **PERFORMANCE.md** (~280 lignes) : cibles par device tier (4 tiers), budgets par frame (16.67ms),
  mémoire par tier, object pooling (8 pools typés + assertion no-alloc + patterns interdits),
  IL2CPP + ARM64 (config PlayerSettings + link.xml + taille binaire), textures (ASTC 6×6 par défaut
  + tailles max par usage + sprite atlas), LOD 4 niveaux + occlusion culling baké, draw call batching
  (SRP Batcher + GPU instancing + cibles par scène), Addressables streaming (5 groupes), audio FMOD
  (compression par type + 32 voices max + streaming BGM), profiler targets (10 recorders + markers
  personnalisés), optimisations spécifiques (EventBus/InputManager/SaveSystem/DamageCalculator/
  string cache), build size optimization, testing performance (3 devices tiers).
- **SECURITY.md** (~260 lignes) : 6 axes sécurité, anti-cheat applicatif (architecture host/client
  + 6 règles validées + détection statistique headshot + sanctions 3→6 strikes + limitations),
  sauvegarde chiffrée AES-128-CBC (clé via Keystore/Keychain + tests), GDPR (consentement +
  données collectées + droit à l'effacement + export), rate limiting (chat + API Nakama + tentatives
  connexion), input validation (HTML sanitize + profanity filter + validation numérique + noms crew),
  secure networking (TLS 1.3 + certificate pinning + HSTS + JWT tokens), protection assets
  (Addressables auth + textures RW false + IL2CPP stripping), audit sécurité (tests auto + checklist
  release), incident response (détection + procédure + communication).
- **DEPLOYMENT.md** (~290 lignes) : pipeline CI/CD (GitHub Actions + Unity Cloud Build), workflow
  YAML complet (test + build Android AAB + build iOS Xcode + deploy TestFlight + deploy Play
  Internal), secrets GitHub (15 secrets), keystore Android (génération + stockage + rotation +
  config Unity), provisioning iOS (certificats + App ID + provisioning profiles + export P12 +
  App Store Connect API), TestFlight (4 groupes de testeurs + feedback), Google Play Console
  (tracks + store listing + IAP planifiés), store listings (captures + descriptions FR/EN),
  release checklist (pre-release T-7 + build T-2 + beta review T-1 + production T + post-release
  T+1), hotfix (procédure + rollback), monitoring post-release (Crashlytics + analytics + perf).
- **CONTRIBUTING.md** (~290 lignes) : prérequis contributeur, conventions C# 12 (style + nommage
  + membres + attributs Unity + comments FR + async UniTask + events), structure fichiers (1 classe
  par fichier + en-tête + organisation dossiers), workflow Git (6 types de branches + convention
  commits SemVer + PR template + review checklist + approbation 1-2 reviews), code review
  (principes + outils + 4 niveaux sévérité), tests (couverture par couche + nommage + AAA pattern +
  catégories NUnit), asset creation (conventions nommage + métadonnées + LFS), versioning SemVer,
  licences et tiers (code propriétaire + dépendances open-source + fonts SIL + assets tiers),
  communication (Discord + Issues + Discussions + standup), annexes (.editorconfig + .gitignore
  + liens utiles).
- **CHANGELOG.md** (ce fichier, ~260 lignes) : historique v0.1.0 complet + planifié v0.2.0/v0.3.0/v1.0.0.

### ✨ Added — Configuration projet

- **Packages/manifest.json** : 16 packages (URP 17.0.3, Cinemachine 3.1.1, InputSystem 1.11.2,
  Addressables 2.2.2, Newtonsoft.Json 3.2.1, UniTask 2.5.10, DOTween 1.2.745, TextMeshPro, ugui,
  modules physics/particlesystem/imgui/jsonserialize, render-pipelines core/universal/shadergraph).
  Scoped registries OpenUPM pour UniTask + DOTween.
- **ProjectSettings/ProjectVersion.txt** : `m_EditorVersion: 6000.0.26f1`.
- **Assets/csc.rsp** : `-langversion:latest` (C# 12).
- **6 asmdef** : KINETICS5.Core, KINETICS5.Data (+ Editor), KINETICS5.Gameplay, KINETICS5.Network,
  KINETICS5.Shaders, KINETICS5.Tests. Define constraints UNITY_6000_0_OR_NEWER + UNITY_INCLUDE_TESTS.
  Version defines KINETICS_FMOD (si FMODUnity présent) + KINETICS_NAKAMA (si Nakama présent) +
  KINETICS_ADDRESSABLES (si addressables ≥ 1.21.0).

### 📊 Statistiques v0.1.0

- **~7 800 lignes de C#** production-ready (Core 3 215 + Data 1 850 + Gameplay 1 100 + Network 1 200 +
  Tests 850).
- **8 shaders HLSL URP** (~1 600 lignes au total, dont 2 post-process).
- **1 ScriptableRendererFeature** URP (Post-Process).
- **6 fichiers JSON** data-driven (~1 200 lignes).
- **9 fichiers Markdown** FR (~2 700 lignes au total).
- **~70 tests NUnit** (35 EditMode + 35 PlayMode).
- **6 asmdef** + 1 asmdef Editor + 1 manifest.json + 1 ProjectVersion + 1 csc.rsp.

---

## [0.0.1] — 2024-12-01

### ✨ Added — Initialisation projet

- Création du repo Git + LFS.
- Extraction du PDF source (`upload/shooter mobile game 5 2.pdf`) en 10 pages PNG.
- Documentation architecture référence (`upload/PROJECT_ARCHITECTURE_3D_UNITY.md`, 1839 lignes).
- Worklog partagé `worklog.md` pour coordination inter-agents.
- Prototype web Next.js/React Three Fiber (agents 1-a/1-b/1-c, parallèle).
