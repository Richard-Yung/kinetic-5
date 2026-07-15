# CONTRIBUTING — KINETICS 5

> Conventions de code, nommage, PRs, code review, branches, commits.

---

## 1. Prérequis contributeur

- **Unity 6000.0.26f1** LTS installé (version exacte, pas de variation mineure).
- **Git 2.40+** avec **Git LFS** configuré (`git lfs install`).
- **.NET SDK 8** (pour C# 12 et IL2CPP).
- Compte GitHub avec accès au repo `kinetics5/unity-project`.
- Acceptation de la **CLA** (Contributor License Agreement) — à signer numériquement.

---

## 2. Conventions de code (C# 12)

### 2.1 Style général

- **Langue** : C# 12 (activé via `Assets/csc.rsp` : `-langversion:latest`).
- **Encoding** : UTF-8 sans BOM.
- **Indentation** : 4 espaces (PAS de tabulations).
- **Line endings** : LF (Unix, pas CRLF).
- **Max line length** : 140 caractères (mais privilégier la lisibilité).
- **Namespace** : `KINETICS5.{Layer}.{Sublayer}` (ex: `KINETICS5.Gameplay.Combat`).
- **File-scoped namespaces** : `namespace KINETICS5.Gameplay.Combat;` (C# 10+).

### 2.2 Nommage

| Élément | Convention | Exemple |
|---|---|---|
| Namespace | PascalCase | `KINETICS5.Gameplay.Player` |
| Class / Struct / Record | PascalCase | `PlayerController`, `DamageInput` |
| Interface | `I` + PascalCase | `IPooledItem` |
| Enum | PascalCase | `EnemyAIState`, `MissionType` |
| Enum value | PascalCase | `EnemyAIState.Patrol` |
| Method | PascalCase verbe | `TakeDamage`, `GetInterpolatedState` |
| Property | PascalCase | `Health`, `MovementState` |
| Field (private) | `_camelCase` | `_maxHealth`, `_currentSlot` |
| Field (public) | PascalCase (rare) | `Target` |
| Field (const) | PascalCase | `DamageCap`, `HeadshotMultiplier` |
| Local variable | camelCase | `initialHealth`, `distToTarget` |
| Parameter | camelCase | `amount`, `sourceId` |
| Event | PascalCase passé | `MissionEnded`, `CheatDetected` |
| Async method | suffix `Async` | `LoadMissionAsync` |
| Generic type | `T` prefix | `TComponent`, `TEvent` |

### 2.3 Membres et ordre

```csharp
namespace KINETICS5.Gameplay.Combat;

using System;
using UnityEngine;
using KINETICS5.Data;

/// <summary>Calculateur statique de dégâts.</summary>
public static class DamageCalculator
{
    // 1. Constantes
    public const float DamageCap = 9999f;

    // 2. Champs statiques
    private static readonly Dictionary<string, float> _cache = new();

    // 3. Properties
    public static int Version => 1;

    // 4. Méthodes publiques
    public static DamageResult Calculate(in DamageInput input, float enemyHealth, float armorPct) { ... }

    // 5. Méthodes privées
    private static float ComputeElemental(Element atk, Element weak, Element res) { ... }
}
```

### 2.4 Attributs Unity

Toujours utiliser les attributs `[Header]`, `[Tooltip]`, `[SerializeField]`, `[Range]` :

```csharp
[Header("Mouvement")]
[Tooltip("Vitesse de marche (m/s).")]
[SerializeField] private float _walkSpeed = 4.5f;
[Range(0.5f, 8f)][SerializeField] private float _sprintSpeed = 7.0f;
```

### 2.5 Comments

- **XML doc comments** (en **FR**) sur toutes les classes et méthodes publiques :
  ```csharp
  /// <summary>
  /// Calcule le résultat complet d'un coup.
  /// </summary>
  /// <param name="input">Statistiques d'entrée du calcul.</param>
  /// <param name="enemyCurrentHealth">HP actuel de l'ennemi (pour détection overkill).</param>
  /// <returns>Le résultat décomposé du calcul.</returns>
  ```
- **Commentaires inline** : pour le *pourquoi*, pas pour le *quoi*. En français.
- ❌ Ne pas commenter du code évident : `i++; // incrémente i`.

### 2.6 Async / UniTask

- Toujours utiliser `UniTask` (pas `Task`/`async void`).
- Passer `CancellationToken` quand possible.
- Suffix `Async` sur les méthodes.
- `Forget()` pour fire-and-forget (avec logging dans le catch).

```csharp
public async UniTask<AuthResult> AuthenticateEmailAsync(string email, string password)
{
    try { ... }
    catch (Exception ex)
    {
        Debug.LogWarning($"[NakamaClient] Auth échec: {ex.Message}");
        return EnterOfflineMode(email);
    }
}
```

### 2.7 Events

- **Publier** via `GameEventBus.Instance.Publish<T>(in T evt)`.
- **Souscrire** via `Subscribe<T>(handler)` qui retourne un `IDisposable`.
- **Toujours** désinscrire dans `OnDisable` ou `OnDestroy` (évite fuites).

```csharp
private IDisposable _token;

private void OnEnable() => _token = GameEventBus.Instance.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
private void OnDisable() => _token?.Dispose();
```

---

## 3. Structure des fichiers

### 3.1 Un fichier = une classe (en général)

- 1 classe publique par fichier (sauf si DTOs groupés logiquement).
- Nom du fichier = nom de la classe principale.
- Max 500 lignes par fichier. Au-delà, envisager un split.

### 3.2 En-tête de fichier

Chaque fichier `.cs` commence par :

```csharp
// ============================================================================
//  KINETICS 5 — <Rôle court du fichier>
//  Task <X-y> — <Auteur>
// ----------------------------------------------------------------------------
//  <Description longue, 3-5 lignes>
// ============================================================================
```

### 3.3 Organisation des dossiers

```
Assets/_Project/<Layer>/
├── KINETICS5.<Layer>.asmdef
├── <Sublayer1>/
│   ├── ClassA.cs
│   └── ClassB.cs
├── <Sublayer2>/
│   ├── ClassC.cs
│   └── ClassD.cs
└── Editor/   (si nécessaire)
    └── KINETICS5.<Layer>.Editor.asmdef
```

---

## 4. Workflow Git

### 4.1 Branches

| Branche | Usage | Durée de vie |
|---|---|---|
| `main` | Production-ready | Permanent |
| `develop` | Intégration features | Permanent |
| `feature/<name>` | Nouvelle fonctionnalité | Fusionnée |
| `bugfix/<name>` | Correction de bug | Fusionnée |
| `hotfix/<name>` | Correction urgente prod | Fusionnée |
| `release/v0.X.0` | Préparation release | Tagguée puis archivée |

### 4.2 Convention de commits

Format : `<type>(<scope>): <description FR>`

| Type | Usage |
|---|---|
| `feat` | Nouvelle fonctionnalité |
| `fix` | Correction de bug |
| `refactor` | Refactoring sans changement de comportement |
| `perf` | Amélioration de performance |
| `docs` | Documentation uniquement |
| `test` | Ajout/modification de tests |
| `chore` | Tâches techniques (build, deps, config) |
| `ci` | Configuration CI/CD |
| `style` | Formatage uniquement (sans changement logique) |

Exemples :

```
feat(combat): ajout multiplicateur élémentaire Cryo
fix(network): reconnect backoff exponentiel corrigé
perf(pooler): zero-alloc Get/Release via struct wrapper
docs(gameplay): formule de dégâts documentée
test(eventbus): couverture thread safety ajoutée
chore(manifest): mise à jour UniTask 2.5.10 → 2.5.11
```

### 4.3 Pull Requests

#### 4.3.1 Titre et description

```markdown
## feat(combat): ajout multiplicateur élémentaire Cryo

### Contexte
Le système de dégâts ne gérait pas le ralentissement Cryo.

### Changements
- `DamageCalculator.Calculate` applique un slow 50% pendant 3s si element == Cryo.
- Nouvel enum `ElementalEffect` dans `Enums.cs`.
- 8 tests EditMode ajoutés dans `DamageCalculatorTests.cs`.

### Tests
- [x] Tests unitaires verts (50+).
- [x] Build Android OK.
- [x] Pas de GC alloc dans hot path (verifié via profiler).

### Breaking changes
Aucun — `CalculateFast` inchangé, seule `Calculate` étendue.

### Screenshots (si UI)
N/A
```

#### 4.3.2 Review checklist (pour le reviewer)

- [ ] Le code suit les conventions (section 2).
- [ ] Pas de `TODO`/`FIXME` laissés sans issue.
- [ ] Tests ajoutés pour les nouvelles fonctionnalités.
- [ ] Pas de dépendance cyclique entre asmdefs.
- [ ] Pas de `Debug.Log` oublié en dehors de `[Conditional("DEBUG")]`.
- [ ] Commentaires XML FR sur les méthodes publiques.
- [ ] Pas de secret (clé API, mot de passe) dans le code.
- [ ] Performance : pas de GC en hot path.
- [ ] Documentation mise à jour (si besoin).

#### 4.3.3 Approbation

- **2 reviews** requises pour `main` (1 lead + 1 peer).
- **1 review** pour `develop` (peer).
- Les reviews doivent être **constructives** : proposer des solutions, pas juste pointer les problèmes.

---

## 5. Code review

### 5.1 Principes

- **Respectueux** : critiquer le code, pas la personne.
- **Constructif** : proposer une alternative si on rejette.
- **Pédagogique** : expliquer le *pourquoi* d'une suggestion.
- **Pragmatique** : ne pas bloquer pour des détails de style (PR review séparée via linter).

### 5.2 Outils

- **GitHub PR Review** : commentaires inline, suggestions de code.
- **Linqueur automatique** : `dotnet format` (Roslyn analyzer) via CI.
- **Analyzer rules** : `Microsoft.Unity.Analyzers` (Unity-specific rules).

### 5.3 Niveaux de sévérité

| Niveau | Action |
|---|---|
| 🔴 **Blocking** | Le PR ne peut pas être mergé (bug, sécurité, perf critique). |
| 🟡 **Suggestion** | Devrait être corrigé avant merge, mais pas bloquant. |
| 🟢 **Nitpick** | Détail de style, à corriger dans un PR séparé. |
| 💬 **Question** | Demandé clarification, pas de correction nécessaire. |

---

## 6. Tests

### 6.1 Couverture attendue

| Couche | Couverture min |
|---|---|
| Core | 80% |
| Data | 70% |
| Gameplay | 60% |
| Network | 50% (stubs, sans serveur réel) |
| UI | 30% (tests visuels principalement) |

### 6.2 Nommage des tests

```csharp
[TestCase(50, 50, 25)]
public void DamageCalculator_Headshot_DoubleLesDegats(float base, float expected, float mult)
{
    // Arrange
    var input = new DamageInput(...);
    // Act
    var result = DamageCalculator.Calculate(input, ...);
    // Assert
    Assert.AreEqual(expected, result.FinalDamage);
}
```

Convention : `<Classe>_<Méthode>_<Scénario>_<Résultat>`.

### 6.3 AAA pattern

- **Arrange** : setup des données de test.
- **Act** : appel de la méthode testée.
- **Assert** : vérification du résultat.

### 6.4 Catégories NUnit

```csharp
[TestFixture]
[Category("Combat")]
public sealed class DamageCalculatorTests { ... }

[TestFixture]
[Category("PlayMode")]
public sealed class PlayerControllerTests { ... }
```

Permet de filtrer dans le Test Runner et de paralléliser.

---

## 7. Asset creation

### 7.1 Nommage des assets

| Type | Convention | Exemple |
|---|---|---|
| Scene | `PascalCase.unity` | `Mission_ShadowFall.unity` |
| Prefab | `PascalCase.prefab` | `Enemy_GruntMK1.prefab` |
| Material | `Mat_<Name>.mat` | `Mat_Vulcan_Body.mat` |
| Texture | `Tex_<Name>_<Type>.png` | `Tex_Vulcan_Albedo.png`, `Tex_Vulcan_Normal.png` |
| Sprite | `Spr_<Name>.png` | `Spr_UI_ButtonPrimary.png` |
| Audio | `SFX_<Name>.wav` / `BGM_<Name>.wav` | `SFX_Weapon_HeavyRX14_Fire.wav` |
| ScriptableObject | `SO_<Type>_<Name>.asset` | `SO_Weapon_HEAVY_RX_14.asset` |
| Animator | `Anim_<Name>.controller` | `Anim_Player.controller` |
| Animation clip | `Clip_<Name>.anim` | `Clip_Player_Run.anim` |

### 7.2 Métadonnées

Tous les assets ont un `.meta` associé. **NE JAMAIS** committer un asset sans son `.meta`.

### 7.3 LFS

Les fichiers binaires > 100 Ko sont stockés via Git LFS :

```gitattributes
# .gitattributes
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.fbx filter=lfs diff=lfs merge=lfs -text
*.prefab filter=lfs diff=lfs merge=lfs -text
*.unity filter=lfs diff=lfs merge=lfs -text
*.bank filter=lfs diff=lfs merge=lfs -text
*.keystore filter=lfs diff=lfs merge=lfs -text
```

---

## 8. Versioning sémantique

KINETICS 5 suit **SemVer** : `MAJOR.MINOR.PATCH`.

| Incrément | Quand |
|---|---|
| **MAJOR** | Breaking changes (save format incompatible, API runtime cassée) |
| **MINOR** | Nouvelle fonctionnalité rétro-compatible (nouveau mission, agent, etc.) |
| **PATCH** | Bug fix rétro-compatible |

Tags Git : `v0.1.0`, `v0.1.1`, `v0.2.0`, ..., `v1.0.0`.

---

## 9. Licences et tiers

### 9.1 Code propriétaire

Tout code KINETICS 5 (`KINETICS5.*` namespaces) est **propriétaire**. Pas de
redistribution hors du repo sans accord explicite.

### 9.2 Dépendances open-source

| Package | Licence | Compatible commercial ? |
|---|---|---|
| UniTask | MIT | ✅ |
| DOTween | MIT (OpenUPM) / Free (Asset Store) | ✅ (version pro requise pour some features) |
| Newtonsoft.Json | MIT | ✅ |
| Nakama | Apache 2.0 | ✅ |
| Cinemachine | Unity Companion License | ✅ (avec licence Unity) |
| Input System | Unity Companion License | ✅ |
| Addressables | Unity Companion License | ✅ |
| URP | Unity Companion License | ✅ |

### 9.3 Fonts

- Audiowide, Rajdhani, Inter, JetBrains Mono : SIL Open Font License (libre usage commercial).

### 9.4 Assets tiers

Tout asset graphique/audio tiers doit être :
1. **Licence claire** (CC0, CC-BY, purchased, etc.).
2. **Documenté** dans `Assets/_Project/THIRD_PARTY.md` (auteur, source, licence).
3. **Crédité** dans l'écran Credits.

---

## 10. Communication

- **Discord** `#dev` : questions rapides, partage de screenshots.
- **GitHub Issues** : bugs, features requests (avec template).
- **GitHub Discussions** : questions longues, design proposals.
- **Standup hebdo** (jeudi 10h) : tour de table, blockers.

---

## 11. Annexes

### 11.1 Linter configuration

`.editorconfig` (à la racine du repo) :

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_style_var_for_built_in_types = false:error
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
dotnet_sort_system_directives_first = true
```

### 11.2 .gitignore essentiel

```gitignore
# Unity
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/

# MonoDevelop / Visual Studio
*.csproj
*.unityproj
*.sln
*.suo
*.tmp
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db

# Unity meta (ne PAS ignorer — nécessaires)
!*.meta

# Secrets
*.keystore
*.p12
*.p8
secrets.json
```

### 11.3 Liens utiles

- **Repo** : https://github.com/kinetics5/unity-project
- **Wiki** : https://github.com/kinetics5/unity-project/wiki
- **Discord** : https://discord.gg/kinetics5-dev
- **Style guide Microsoft** (référence C#) : https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions
