# ARCHITECTURE — KINETICS 5

> Architecture technique détaillée du projet Unity 6000.0 LTS.
> Couleurs, typo, patterns, flux de données, dépendances entre assemblies.

---

## 1. Vue d'ensemble

KINETICS 5 adopte une architecture **en couches** avec **inversion de dépendances**
via `ServiceLocator` (DI minimaliste) et **communication événementielle** via
`GameEventBus` (publish/subscribe type-safe, zero-alloc). Chaque couche est isolée
dans son propre assembly (asmdef) pour garantir la compilabilité parallèle et
empêcher les dépendances cycliques.

### 1.1 Principes directeurs

1. **Data-driven** : Aucune donnée de gameplay en dur dans le code. Tout passe par
   `Resources/Data/*.json` chargé par `DataLoader` ou par les `*SO` (authoring éditeur).
2. **Mobile-first** : Pas de GC en hot path, object pooling systématique, IL2CPP ARM64.
3. **Dégradation gracieuse** : Si Nakama/FMOD/Addressables est absent, le jeu reste
   jouable (stubs locaux, fallbacks).
4. **Testabilité** : Tout système est testable en EditMode (NUnit) ou PlayMode.
5. **FR-first** : Commentaires, doc XML, messages utilisateur en français.

---

## 2. Couches

```
┌────────────────────────────────────────────────────────────────┐
│                        UI (UGUI + UI Toolkit)                  │  KINETICS5.UI
├────────────────────────────────────────────────────────────────┤
│                  Gameplay (Player/Enemies/Missions/Combat)     │  KINETICS5.Gameplay
├────────────────────────────────────────────────────────────────┤
│              Network (Nakama + Match + AntiCheat + Chat)       │  KINETICS5.Network
├────────────────────────────────────────────────────────────────┤
│           Data (ScriptableObjects + JSON + DataLoader)         │  KINETICS5.Data
├────────────────────────────────────────────────────────────────┤
│         Core (GameManager + EventBus + Save + Input + ...)     │  KINETICS5.Core
├────────────────────────────────────────────────────────────────┤
│                     Shaders (URP HLSL)                         │  KINETICS5.Shaders
├────────────────────────────────────────────────────────────────┤
│                    Audio (FMOD wrapper)                        │  (partie de Core)
└────────────────────────────────────────────────────────────────┘
```

### 2.1 Couche Core

Assembly `KINETICS5.Core`. Contient les 13 systèmes fondamentaux :

| Système | Rôle | Pattern |
|---|---|---|
| `ServiceLocator` | DI container minimaliste | Singleton + Dictionary<Type,object> |
| `GameEventBus` | Pub/sub type-safe zero-alloc | Singleton + struct events + HandlerWrapper |
| `GameManager` | Machine à états globale | Singleton + enum GameState + UniTask transitions |
| `ObjectPooler` | Pools d'objets réutilisables | Singleton + Stack<Component> + IPooledItem |
| `SaveSystem` | Sauvegarde chiffrée AES-128 | Singleton + 3 slots + migrations |
| `SceneLoader` | Chargement Addressables + SceneManager | Singleton + DOTween fade |
| `AudioManager` | Wrapper FMOD + fallback AudioSource | Singleton + crossfade A/B + SFX pool |
| `LocalizationManager` | 7 langues JSON | Singleton + fallback EN + runtime switch |
| `InputManager` | Multi-device (Mobile/Desktop/Gamepad) | Singleton + EnhancedTouch + struct InputState |
| `CameraManager` | Cinemachine 3.x + recoil + shake | Singleton + POV + BasicMultiChannelPerlin |
| `TimeManager` | Hitstop + slow-motion | Singleton + Queue<Hitstop> |
| `TelemetryLogger` | GDPR-compliant analytics | Singleton + LRU 1000 events + PostHog stub |
| `Bootstrapper` | Orchestration du boot | RuntimeInitializeOnLoadMethod + topo sort |

### 2.2 Couche Data

Assembly `KINETICS5.Data` (dépend de Core + Newtonsoft.Json).

- **18 POCOs DTO** (`DataDtos.cs`) désérialisés depuis `Resources/Data/*.json`.
- **7 ScriptableObjects** (`ScriptableObjects/`) pour l'authoring éditeur équivalent.
- **`DataLoader`** : chargeur statique, lazy-load via `EnsureLoaded()`, validation
  d'intégrité runtime (warnings) + menus éditeur.
- **`DataValidator`** (Editor only) : validation stricte (schéma + références + balance).

### 2.3 Couche Gameplay

Assembly `KINETICS5.Gameplay` (dépend de Core + Data).

- **`PlayerController`** : FPS mobile + desktop, CharacterController, stamina, vitals.
- **`EnemyAI`** : FSM simple (Patrol → Chase → Attack → Flee), consomme `EnemyDto`.
- **`MissionDirector`** : orchestrateur de vagues + objectifs + boss + rewards.
- **`DamageCalculator`** : formule de dégâts centralisée (utilisé par combat + anti-cheat).

### 2.4 Couche Network

Assembly `KINETICS5.Network` (dépend de Core + Data + UniTask + Newtonsoft).
Activate conditionnelle : `KINETICS_NAKAMA` si le package Nakama est installé.

- **`NakamaClient`** : auth (email/device/OAuth), sessions, reconnect backoff, offline fallback.
- **`MatchManager`** : host/client model, sync d'état, RPC actions, snapshot interpolation.
- **`LeaderboardService`** : global/friends/crew, seasons, cache TTL 30s.
- **`ChatService`** : world/crew/DM, profanity filter, rate-limit 5/10s, HTML sanitize.
- **`CloudSaveService`** : sync cloud, merge inventory/progress, conflict resolution.
- **`AntiCheatValidator`** : validation serveur (damage/speed/fire-rate/headshot-rate), bans.

### 2.5 Couche UI

Assembly `KINETICS5.UI` (dépend de Core + Data). Voir `UI_GUIDE.md` pour le détail.

- **UGUI** pour HUD combat (Canvas Screen Space Overlay + world space pour damage numbers).
- **UI Toolkit** pour menus (Start Screen, Lobby, Settings) — performant, style USS.
- **33 écrans** au total (8 du PDF + 25 extras).

### 2.6 Couche Shaders

Assembly `KINETICS5.Shaders` (dépend de URP Runtime).

| Shader | Usage |
|---|---|
| `ToonShading.shader` | Cel-shaded personnages + outline inverted hull |
| `HoloUI.shader` | Panneaux holographiques (scanlines + glitch + RGB split) |
| `ForceField.shader` | Bulles de bouclier (Fresnel + hex grid + ripples) |
| `MuzzleFlash.shader` | Particules additives (soft edge + shimmer) |
| `DamageNumber.shader` | Nombres de dégâts billboardés (outline + glow + element color) |
| `ShipInterior.shader` | Surfaces métalliques (PBR-lite + grid normals + emissive) |
| `K5PostProcess.shader` + `K5BloomPrePass.shader` | Post-FX URP (bloom + CA + vignette + grain + LUT) |
| `K5PostProcessFeature.cs` | `ScriptableRendererFeature` URP |

---

## 3. Patterns architecturaux

### 3.1 Service Locator (DI minimaliste)

Pas de Zenject/VContainer pour éviter la surcharge de réflexion runtime.
`ServiceLocator` est un MonoBehaviour singleton avec un `Dictionary<Type, object>`.

```csharp
// Enregistrement (Bootstrapper) :
ServiceLocator.Instance.Register<IInputService>(new InputManager());
ServiceLocator.Instance.Register<AudioManager>(audioMgr);

// Résolution (runtime, thread principal) :
var input = ServiceLocator.Instance.Get<InputManager>();
```

Avantages : zéro réflexion, zéro génération de code, ~0 ms au boot.
Inconvénients : pas de constructeur injecté (les dépendances sont résolues dans Awake/Start).

### 3.2 Event Bus (publish/subscribe type-safe)

`GameEventBus` publie des **struct** events (zero-alloc) typés. Les handlers sont
enveloppés dans `HandlerWrapper<T>` (struct) stockés dans `List<IEventHandler>`.

```csharp
// Souscription :
var token = GameEventBus.Instance.Subscribe<DamageDealtEvent>(OnDamageDealt);
// Publication (in = pas de copie) :
GameEventBus.Instance.Publish(new DamageDealtEvent(src, tgt, 50f, false, elem, hitPos));
// Désinscription (token IDisposable) :
token.Dispose();
```

7 événements KINETICS 5 : `DamageDealtEvent`, `EnemyKilledEvent`, `MissionCompleteEvent`,
`WeaponSwitchedEvent`, `PlayerDamagedEvent`, `ObjectiveUpdatedEvent`, `LootPickupEvent`.

### 3.3 State Machines

Deux niveaux :

1. **Globale** : `GameManager.GameState` (Boot/MainMenu/Loading/InMission/Paused/Results).
   Transitions asynchrones via UniTask, hookent SceneLoader + TimeManager.
2. **Locale** :
   - `PlayerController.PlayerMovementState` (Idle/Walking/Sprinting/Crouching/Jumping/Falling/Dead).
   - `EnemyAI.EnemyAIState` (Idle/Patrol/Chase/Attack/Flee/Stunned/Dead).
   - `MissionDirector.MissionRuntimeState` (NotStarted/Loading/Active/WaveBreak/BossPhase/Complete/Failed).

### 3.4 Object Pooling

`ObjectPooler` pré-chauffe des `Stack<Component>` au boot. `Get<T>`/`Release<T>`
sans allocation après warmup. Implémente `IPooledItem` pour callbacks spawn/return.

Pools typiques : Bullets (200), VFX_Hit (50), VFX_MuzzleFlash (32), DamageNumbers (64),
Enemies (50 par type).

### 3.5 Data-Driven (SO + JSON)

Authoring éditeur via ScriptableObjects (`*SO`). Runtime via JSON (`DataLoader`).
Les deux représentent les mêmes données (DTO ↔ SO).

```csharp
// Runtime :
var weapon = DataLoader.GetWeapon("HEAVY_RX_14");
Debug.Log(weapon.DamagePct);  // 72
```

Validation stricte : `DataValidator` (menu éditeur) + validation runtime (warnings).

### 3.6 Async / UniTask

Tout l'IO réseau, save, scene loading, boot utilise **UniTask** (zero-alloc async).
`RuntimeInitializeOnLoadMethod` pour le boot automatique.

---

## 4. Flux de données

### 4.1 Boot

```
RuntimeInitializeOnLoadMethod(BeforeSceneLoad)
    → Bootstrapper.InitializeCore()
        → topo sort des 12 systèmes
        → ServiceLocator.Register pour chacun
        → SceneLoader.EnsureBaseSceneLoadedAsync()
    → DataLoader.AutoLoad() [parallel]
    → NakamaClient.AuthenticateDeviceAsync() [parallel, fallback offline]
    → GameManager.BootAsync(minBootDurationMs=1200)
        → await UniTask.Delay(1200)
        → RequestStateChangeAsync(MainMenu)
```

### 4.2 Démarrage de mission

```
UI : bouton "PLAY" sur écran Lobby
    → GameManager.StartMissionAsync("SHADOW_FALL")
        → RequestStateChangeAsync(Loading, "SHADOW_FALL")
            → TransitionToAsync
                → SceneLoader.LoadMissionAsync("SHADOW_FALL")
                    → Addressables.LoadSceneAsync(scene, LoadSceneMode.Additive)
                    → ProgressChanged event → UI Loading bar
                → onCompleted → GameManager.OnMissionLoaded()
                    → RequestStateChange(InMission)
                    → MissionDirector.StartMission("SHADOW_FALL")
                        → spawns wave 0 after delay
```

### 4.3 Tir joueur (combat flow)

```
InputManager.Update() → CurrentState.FireHeld = true
    → PlayerCombat.Update()
        → if FireHeld && nextFireTime <= Time.time
            → ObjectPooler.Get<Bullet>("bullets", muzzlePos, muzzleRot)
            → Bullet.Fire(dir) → Physics.Raycast or projectile travel
            → on hit : DamageCalculator.CalculateFast(weaponId, enemyId, ...)
            → enemyAI.TakeDamage(amount, sourceId)
            → GameEventBus.Publish(DamageDealtEvent)
            → UI DamageNumber (pooled) spawns at hit point
            → AudioManager.PlaySFX("weapon_heavy_rx_14_fire")
            → CameraManager.RecoilKick(weapon.RecoilPattern)
            → TelemetryLogger.TrackWeaponUsed(weaponId, shotsFired++)
```

### 4.4 Sauvegarde

```
Player meurt / mission finit
    → SaveSystem.MarkDirty()
    → Update() check : if dirty && time - lastSave >= 60s
        → SaveImmediate()
            → JsonConvert.SerializeObject(ActiveData)
            → AES-128-CBC encrypt
            → File.WriteAllText(persistentDataPath/save_slotN.dat, encrypted)
            → PlayerPrefs.SetString(backup)
            → CloudSaveService.SchedulePush() [debounce 5s]
                → UniTask.Delay(5s)
                → PushAsync() → Nakama storage write
```

---

## 5. Graphe des dépendances (asmdef)

```
KINETICS5.Core
    ├── UniTask
    ├── Cinemachine
    ├── Unity.InputSystem
    ├── Unity.TextMeshPro
    ├── Unity.Addressables
    ├── Unity.RenderPipelines.Universal.Runtime
    ├── Unity.ResourceManager
    ├── DOTween
    ├── Newtonsoft.Json
    └── FMODUnity (optional, via versionDefines)

KINETICS5.Data
    ├── KINETICS5.Core
    └── Newtonsoft.Json

KINETICS5.Gameplay
    ├── KINETICS5.Core
    ├── KINETICS5.Data
    ├── Newtonsoft.Json
    ├── UniTask
    ├── Cinemachine
    ├── Unity.InputSystem
    └── Unity.TextMeshPro

KINETICS5.Network
    ├── KINETICS5.Core
    ├── KINETICS5.Data
    ├── Newtonsoft.Json
    ├── UniTask
    └── Nakama (optional, via versionDefines → KINETICS_NAKAMA)

KINETICS5.Shaders
    ├── KINETICS5.Core
    ├── Unity.RenderPipelines.Universal.Runtime
    ├── Unity.RenderPipelines.Universal.Editor
    ├── Unity.RenderPipelines.Core.Runtime
    └── Unity.RenderPipelines.Core.Editor

KINETICS5.UI
    ├── KINETICS5.Core
    ├── KINETICS5.Data
    └── Unity.TextMeshPro

KINETICS5.Tests (defineConstraints: UNITY_INCLUDE_TESTS)
    ├── KINETICS5.Core
    ├── KINETICS5.Data
    ├── KINETICS5.Gameplay
    ├── KINETICS5.Network
    ├── UnityEngine.TestRunner
    ├── UnityEditor.TestRunner
    └── nunit.framework
```

**Règle** : aucune dépendance cyclique. UI ne référence jamais Network directement
(passe par EventBus). Gameplay ne référence jamais UI.

---

## 6. Conventions de nommage

- **Namespaces** : `KINETICS5.{Layer}.{Sublayer}` (ex: `KINETICS5.Gameplay.Combat`).
- **Classes** : PascalCase (ex: `PlayerController`, `DamageCalculator`).
- **Méthodes publiques** : PascalCase verbe (ex: `TakeDamage`, `GetInterpolatedState`).
- **Champs privés** : `_camelCase` (ex: `_maxHealth`, `_currentSlot`).
- **Properties publiques** : PascalCase (ex: `Health`, `MovementState`).
- **Interfaces** : `I` prefix (ex: `IPooledItem`).
- **Events** : nom passé (ex: `MissionEnded`, `CheatDetected`).
- **Async** : suffix `Async` (ex: `LoadMissionAsync`).
- **Stubs / fallbacks** : méthode interne `BuildOfflineResponse`, `EnterOfflineMode`.

---

## 7. Performance et scalabilité

Voir `PERFORMANCE.md` pour le détail. Points clés :

- **Hot paths zero-alloc** : `for` loops sans LINQ, structs, `in` parameters.
- **Object pooling** : tous les VFX/projectiles poolés, vérifiés par tests GC.
- **IL2CPP ARM64** : build Android/iOS, C++ compiler config Release.
- **Addressables** : scènes missions chargées à la volée (pas en mémoire au boot).
- **ASTC 6x6** : compression textures mobile, fallback ETC2 pour Android ancien.
- **Profiler targets** : 60 FPS mid-range (Snapdragon 7 Gen 1), 30 FPS low-end (4 Go RAM).

---

## 8. Évolutions futures

- **v0.2.0** : UI complète (33 écrans), audio FMOD final, addressables streaming.
- **v0.3.0** : Multiplayer co-op 4 joueurs (matchmaker Nakama), seasons, battle pass.
- **v0.4.0** : Battle Royale mode (60 joueurs), serveurs dédiés Nakama.
- **v1.0.0** : Release publique Android + iOS.

---

## 9. Références

- **PDF source** : `upload/shooter mobile game 5 2.pdf` (10 pages spec UI/UX).
- **Architecture référence** : `upload/PROJECT_ARCHITECTURE_3D_UNITY.md` (1839 lignes).
- **Unity 6000 LTS docs** : https://docs.unity3d.com/6000.0/Documentation/Manual/
- **URP docs** : https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/
- **Nakama docs** : https://heroiclabs.com/docs/
- **UniTask docs** : https://github.com/Cysharp/UniTask
