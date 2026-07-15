# PERFORMANCE — KINETICS 5

> Optimisation mobile-first pour KINETICS 5.
> Objectifs : 60 FPS mid-range (Snapdragon 7 Gen 1), 30 FPS low-end (4 Go RAM, Mali G76).

---

## 1. Cibles de performance

### 1.1 Par device tier

| Tier | Device ref | RAM | GPU | FPS cible | Resolution |
|---|---|---|---|---|---|
| **High-end** | iPhone 15 / Galaxy S24 / Pixel 8 | 8 Go+ | A16 / Adreno 750 | **60** | 1080p native |
| **Mid-range** | iPhone 12 / Galaxy A54 / Pixel 6a | 6 Go | A14 / Adreno 644 | **60** | 1080p (FSR 1.0) |
| **Low-end** | iPhone SE 2020 / Galaxy A32 / Redmi Note 10 | 4 Go | A13 / Adreno 610 | **30** | 720p |
| **Desktop** | Intel i5-10400 / GTX 1660 | 16 Go | — | **144** (cap) | 1080p |

### 1.2 Budgets par frame (16.67 ms @ 60 FPS)

| Catégorie | Budget | Détail |
|---|---|---|
| **CPU main thread** | 8 ms | Gameplay + Input + Physics |
| **CPU worker threads** | 4 ms | Job system, AI, pathfinding |
| **GPU** | 8 ms | Render + post-FX |
| **Audio** | 1 ms | FMOD mixing |
| **Marge** | 3.67 ms | Spikes, GC préventif |

### 1.3 Mémoire

| Tier | Budget total | Textures | Audio | Meshes | Code (IL2CPP) |
|---|---|---|---|---|---|
| **High-end** | 1.5 Go | 600 Mo | 200 Mo | 300 Mo | 80 Mo |
| **Mid-range** | 1.0 Go | 400 Mo | 150 Mo | 200 Mo | 70 Mo |
| **Low-end** | 600 Mo | 200 Mo | 100 Mo | 100 Mo | 60 Mo |

---

## 2. Object pooling (no-GC in hot path)

### 2.1 Pools pré-chauffés au boot

| Pool ID | Type | PreWarm | MaxSize | Rationale |
|---|---|---|---|---|
| `bullets` | Bullet | 200 | 500 | Hitscan VFX + projectiles |
| `vfx_hit` | VFX_Hit | 50 | 150 | Impact à chaque tir touché |
| `vfx_muzzle` | VFX_Muzzle | 32 | 100 | Un par tir |
| `damage_numbers` | DamageNumber | 64 | 200 | Flottants au-dessus ennemis |
| `enemy_grunt` | EnemyAI | 30 | 80 | GRUNT-MK1 standard |
| `enemy_drone` | EnemyAI | 20 | 50 | SNIPER-DRONE / SWARM-BOT |
| `enemy_elite` | EnemyAI | 10 | 30 | ELITE-GUARD / HEAVY-UNIT |
| `loot_pickup` | LootPickup | 32 | 100 | Items au sol |

### 2.2 Assertion no-alloc

Le test `ObjectPoolerTests.Pool_HotPath_GetRelease_PasDAllocationsGC` vérifie que
1000 cycles Get/Release allouent moins de 4 Ko total (< 4 octets/cycle).

```csharp
long gcBefore = System.GC.GetTotalMemory(forceFullCollection: true);
for (int i = 0; i < 1000; i++) {
    var b = _pooler.Get<PoolableBullet>("bullets");
    _pooler.Release(b);
}
long delta = System.GC.GetTotalMemory(false) - gcBefore;
Assert.Less(delta, 4096);
```

### 2.3 Patterns interdits en hot path

- ❌ `foreach` sur `List<T>` (alloc Enumerator) → utiliser `for`.
- ❌ `string.Format` / interpolation → utiliser `StringBuilder` ou concat directe.
- ❌ `LINQ` (`Where`, `Select`, `Any`) → boucles `for` explicites.
- ❌ `Action` allocation (lambda capture) → méthodes statiques nommées.
- ❌ `GetComponent<T>()` répété → cache dans `Awake`.
- ❌ `FindObjectOfType` → ServiceLocator ou champ `[SerializeField]`.

---

## 3. IL2CPP + ARM64

### 3.1 Configuration Player Settings

```yaml
Android:
  ScriptingBackend: IL2CPP
  TargetArchitectures: [ARM64]      # ARMv7 déprécié, x86_64 pour émulateur
  ApiCompatibilityLevel: NET Standard 2.1
  C++CompilerConfiguration: Release
  StripEngineCode: true
  ManagedStrippingLevel: High       # supprime code non utilisé
  Il2CppStacktraceInformation: MethodOnly  # réduit taille binaire

iOS:
  ScriptingBackend: IL2CPP
  Architecture: ARM64
  ApiCompatibilityLevel: NET Standard 2.1
  C++CompilerConfiguration: Release
  ManagedStrippingLevel: High
```

### 3.2 link.xml (preserve types)

Pour éviter que le stripping ne casse la réflexion (Newtonsoft, SOs), on déclare
dans `Assets/_Project/link.xml` les assemblies à préserver :

```xml
<linker>
  <assembly fullname="Newtonsoft.Json" preserve="all"/>
  <assembly fullname="KINETICS5.Data">
    <type fullname="KINETICS5.Data.*" preserve="all"/>
  </assembly>
</linker>
```

### 3.3 Taille binaire attendue

- Android APK (AAB) : ~120-180 Mo (sans expansion file).
- iOS IPA : ~150-200 Mo.
- Addressables (téléchargés) : ~500 Mo additionnels (scènes missions, audio).

---

## 4. Textures et compression

### 4.1 Format par plateforme

| Plateforme | Format | Notes |
|---|---|---|
| **Android (Adreno/Mali)** | **ASTC 6×6** | Default. 0.5 byte/pixel. |
| **Android fallback (vieux)** | ETC2 | Pour API < 24 (non supporté officiellement). |
| **iOS (PowerVR)** | **ASTC 6×6** | Identique Android pour simplifier pipeline. |
| **Desktop** | BC7 | Haute qualité, pas de contrainte taille. |

### 4.2 Tailles max par usage

| Usage | Max size | Mip maps |
|---|---|---|
| UI atlas | 2048×2048 | Non |
| Character albedo | 1024×1024 | Oui |
| Character normal | 1024×1024 | Oui |
| Enemy albedo | 512×512 | Oui |
| Weapon albedo | 512×512 | Oui |
| Environment albedo | 1024×1024 | Oui |
| Skybox | 2048×1024 | Non |
| LUT post-process | 32×32×32 (32×1024) | Non |

### 4.3 Sprite atlas

- **UI** : 1 atlas 2048×2048 (tous les icons + boutons).
- **HUD** : 1 atlas 1024×1024 (vitals, minimap, crosshair).
- Activé via **Sprite Atlas** (Sprite Atlas v2) + `Late Binding` pour réduire mémoire initiale.

---

## 5. LOD et occlusion culling

### 5.1 LOD groups

Chaque mesh environment a 4 LODs :

| LOD | Distance | Triangle ratio | Screen size |
|---|---|---|---|
| LOD0 | 0-15m | 100% | > 30% |
| LOD1 | 15-30m | 50% | 15-30% |
| LOD2 | 30-60m | 25% | 5-15% |
| LOD3 (billboard) | 60m+ | 5% | < 5% |

### 5.2 Occlusion culling

- **Baké** via **Window > Rendering > Occlusion Culling > Bake**.
- Cell size : 1m (vaisseaux = couloirs étroits).
- Smallest hole : 0.5m.
- Backface threshold : 60%.

### 5.3 Frustum culling

Activé par défaut. Vérifier que les Mesh Renderers ont `Culling Mode = Automatic`.

---

## 6. Draw call batching

### 6.1 SRP Batcher

Activé par défaut dans URP Asset. Compatible avec nos shaders (tous utilisent
`CBUFFER_START(UnityPerMaterial)`).

Vérification : **Frame Debugger > "SRP Batcher"** doit afficher "Batched".

### 6.2 Dynamic batching

Désactivé (le SRP Batcher est plus performant sur mobile).

### 6.3 GPU instancing

Activé sur :
- Particules (muzzle flash, hit VFX).
- Ennemis multiples du même type (`#pragma multi_compile_instancing`).
- Decals (impacts, blood).

### 6.4 Cible draw calls

| Scène | Draw calls | Tris |
|---|---|---|
| Lobby | < 50 | < 30k |
| Mission standard | < 150 | < 100k |
| Boss fight | < 200 | < 150k |

---

## 7. Addressables streaming

### 7.1 Groupes

| Groupe | Load mode | Compression | Notes |
|---|---|---|---|
| `Core` | All at boot | LZ4 | Bootstrap + Base scene + GameManager |
| `Missions` | On demand | LZ4 | 7 scènes (5-15 Mo chacune) |
| `UI` | Per-screen | LZ4 | 33 écrans (1-3 Mo chacun) |
| `Audio` | Streaming | Vorbis (Android) / AAC (iOS) | Banks FMOD |
| `Shaders` | All at boot | LZ4 | Variant collections |

### 7.2 Pré-chargement

Au boot, on pré-charge en arrière-plan :
- `Core` (synchrone, obligatoire).
- `Missions/SHADOW_FALL` (asynchrone, pendant MainMenu) — pour réduire le temps de loading du premier PLAY.

### 7.3 Déchargement

- À la sortie d'une mission : `Addressables.ReleaseInstance(sceneInstance)`.
- À la sortie d'un écran UI : `Addressables.Release(assetHandle)`.

---

## 8. Audio (FMOD)

### 8.1 Compression

| Type | Format | Bitrate | Channels |
|---|---|---|---|
| **BGM** | Vorbis (Android) / AAC (iOS) | 128 kbps | Stéréo |
| **SFX court** | Vorbis | 96 kbps | Mono |
| **SFX long** | Vorbis | 128 kbps | Mono/Stéréo |
| **Voice** | Opus | 64 kbps | Mono |

### 8.2 Voix simultanées

- **Max SFX voices** : 32 (configuré dans AudioManager).
- Priorité : Voice > SFX important > SFX > BGM.
- Les SFX en dessous du seuil sont virtualisés (FMOD gère).

### 8.3 Streaming BGM

BGM streamée depuis le disque (pas en RAM). 2 buffers de 2s chacun.

---

## 9. Profiler targets

### 9.1 Profiler Recorder (runtime)

À monitorer en permanence sur device :

| Recorder | Seuil alerte |
|---|---|
| `Frame.Time` | > 16.67 ms (60 FPS) |
| `Memory.TotalAllocated` | > 600 Mo (low-end) |
| `Memory.GC.Alloc` | > 0 bytes/frame |
| `DrawCalls.Count` | > 200 |
| `Batch.Count` | > 100 |
| `SetPass.Count` | > 50 |
| `Triangles.Count` | > 150k |
| `Audio.TotalCPU` | > 5% |
| `Physics.SimulateTime` | > 2 ms |

### 9.2 Profiler markers personnalisés

```csharp
using Unity.Profiling;
public class PlayerController : MonoBehaviour {
    static readonly ProfilerMarker k_UpdateMarker = new("K5.PlayerController.Update");
    private void Update() {
        k_UpdateMarker.Begin();
        // ...
        k_UpdateMarker.End();
    }
}
```

Marqueurs à ajouter : `K5.PlayerController.Update`, `K5.EnemyAI.Update`,
`K5.MissionDirector.Update`, `K5.GameEventBus.Publish`, `K5.ObjectPooler.Get`,
`K5.DamageCalculator.Calculate`.

### 9.3 Frame Debugger

Vérifier après chaque changement shader/material :
1. **Window > Analysis > Frame Debugger**.
2. Activer, jouer une frame.
3. Vérifier : SRP Batcher batches actifs, pas de `Not Batched (Different Materials)`.

---

## 10. Optimisations spécifiques

### 10.1 EventBus zero-alloc

- Events en `struct` (pas de boxing).
- `HandlerWrapper<T>` est une struct, évite l'allocation de l'Action.
- `Publish(in T)` passe par référence (pas de copie).
- Vérifié par `EventBusTests` (no-leak après 1000 publish).

### 10.2 InputManager zero-alloc

- `InputState` est une struct mutée en place (`input.CurrentState.Move = ...`).
- Pas d'`Action` allocation (lecture directe de l'InputAction).

### 10.3 SaveSystem IO différé

- `MarkDirty()` ne fait que poser un flag (pas d'IO).
- `SaveImmediate()` est appelé au pire toutes les 60s (pas à chaque changement).
- Écriture atomic : write temp file → rename (évite corruption si crash).

### 10.4 DamageCalculator statique

Pas d'instance, pas d'allocation. `CalculateFast` retourne un float (pas de struct).
`Calculate` (struct result) alloue le readonly struct sur la pile (pas le heap).

### 10.5 String cache

- IDs d'armes/ennemis/missions : `string` internés via `string.Intern()` dans DataLoader.
- Comparaison par référence (`ReferenceEquals`) au lieu de valeur.

---

## 11. Build size optimization

### 11.1 Managed stripping

- **High** sur Android + iOS.
- `link.xml` préserve Newtonsoft + Data DTOs + SOs.
- Vérifier : build → il2cpp output → aucun type KINETICS5 manquant.

### 11.2 Engine code stripping

- **Strip Engine Code** : activé.
- Vérifier : `UnityEditor.PlayerSettings.GetStrippingLevel(BuildTargetGroup.Android)`.

### 11.3 Resource auditing

```bash
# Liste les assets les plus gros :
find Assets -type f -size +5M -exec ls -lh {} \;
# Vérifie les textures sans compression :
# (via menu Editor > KINETICS 5/Audit > Textures non compressées)
```

### 11.4 Texture compression audit

Menu éditeur à créer : `KINETICS 5 > Audit > Textures` qui liste :
- Textures sans ASTC.
- Textures > 2048×2048.
- Textures sans mip maps (pour usage 3D).
- Atlas possibles (textures < 256×256 utilisées ensemble).

---

## 12. Testing performance

### 12.1 Unity Test Framework (auto)

- **PlayMode test** : `PerformanceTests.cs` (à étendre) :
  - `Frame_Time_Lobby_LessThan16ms`
  - `GC_Alloc_Mission_Standard_LessThan1KBPerFrame`
  - `Draw_Calls_Boss_Fight_LessThan200`

### 12.2 Profiler via script

```csharp
[Test]
public void FrameTime_Lobby_LessThan16ms()
{
    var recorder = ProfilerRecorder.GetNew(ProfilerCategory.Render, "Frame.Time");
    // ... load Lobby scene, attendre 60 frames ...
    double avg = recorder.AverageValue / 1_000_000.0; // ns → ms
    Assert.Less(avg, 16.0, "Lobby doit tenir 60 FPS.");
}
```

### 12.3 Device tests (manuel)

Sur 3 devices minimum (low/mid/high), lancer la build release :
1. **Lobby** : 60 FPS pendant 2 min (pas de GC).
2. **Mission SHADOW FALL** : 60 FPS pendant 5 min.
3. **Boss IRON HARVEST** : 30+ FPS, < 100 MB mémoire additionnelle.

---

## 13. Références

- **Unity 6000 perf docs** : https://docs.unity3d.com/6000.0/Documentation/Manual/performance-profiling.html
- **URP optimization** : https://blog.unity.com/technology/optimize-your-mobile-game-performance-tips-from-the-team-behind-fps-sample
- **IL2CPP** : https://docs.unity3d.com/6000.0/Documentation/Manual/scripting-backends-il2cpp.html
- **ASTC** : https://developer.arm.com/technologies/graphics-technologies/astc
