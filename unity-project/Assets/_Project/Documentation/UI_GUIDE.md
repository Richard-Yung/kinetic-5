# UI GUIDE — KINETICS 5

> Guide complet du système UI : palette, typographie, composants, 33 écrans,
> navigation, mobile touch, accessibilité, i18n.

---

## 1. Palette NON-NÉGOCIABLE

Extrait du PDF source (page 8). Toute déviation doit être validée par le lead design.

### 1.1 Couleurs principales

| Nom | Hex | RGB | Usage |
|---|---|---|---|
| **Main Color** | `#1AA1CE` | (26, 161, 206) | Cyan principal — boutons, accents, glows, borders |
| **Vert néon** | `#6CF42E` | (108, 244, 46) | Succès, validation, rim light, health bar |
| **Jaune** | `#FFE735` | (255, 231, 53) | Alertes, sélections, Volt element, ultimate ready |
| **Rouge** | `#FE0022` | (254, 0, 34) | Danger, dégâts critiques, Explosive, low HP |
| **Bleu nuit** | `#10204B` | (16, 32, 75) | Fonds, séparateurs, secondary panels |
| **Deep space** | `#05060F` | (5, 6, 15) | Background principal |
| **Black void** | `#020207` | (2, 2, 7) | Background secondaire, loader |
| **Blanc** | `#FFFFFF` | (255, 255, 255) | Texte principal, icons |

### 1.2 Couleurs dérivées

| Nom | Hex | Usage |
|---|---|---|
| **Cyan dim** | `#0E6B85` | Hover/focus sur cyan |
| **Cyan glow** | `#5DC8E5` | Glow/outline cyan |
| **Vert dim** | `#4A9B1F` | Hover vert |
| **Gris ardoise** | `#1A1F33` | Cards, panels |
| **Gris border** | `#2A3550` | Borders, separators |
| **Rouge HP critique** | `#FF3D4D` | HP < 25% (pulse) |

### 1.3 Usage strict

- **Boutons primaires** : `#1AA1CE` background, `#FFFFFF` texte.
- **Boutons secondaires** : transparent avec border `#1AA1CE`, texte `#1AA1CE`.
- **Boutons danger** : `#FE0022` background, `#FFFFFF` texte.
- **Cards** : `#1A1F33` background, border `#2A3550`.
- **Health bar** : `#6CF42E` (full), `#FFE735` (mid 50%), `#FE0022` (low < 25% pulse).
- **Shield bar** : `#1AA1CE` (toujours, pour distinguer du health).
- **XP bar** : `#FFE735`.

---

## 2. Typographie

### 2.1 Polices

| Police | Usage | Source |
|---|---|---|
| **Audiowide** | Titres, boutons, chiffres HUD, logos | Google Fonts (SIL OFL) |
| **Rajdhani** | Corps, descriptions courtes | Google Fonts |
| **Inter** | Corps alternatif, longs textes | Google Fonts |
| **JetBrains Mono** | Code, IDs, debug HUD | Google Fonts |

### 2.2 Tailles (UI Toolkit USS)

| Style | Taille | Poids | Usage |
|---|---|---|---|
| `display-xl` | 64px | Audiowide Regular | Splash, titres écran principal |
| `display-l` | 48px | Audiowide Regular | Titres écran |
| `display-m` | 32px | Audiowide Regular | Sous-titres |
| `headline` | 24px | Rajdhani SemiBold | Section headers |
| `body-l` | 20px | Rajdhani Regular | Corps boutons, descriptions |
| `body-m` | 16px | Rajdhani Regular | Corps texte standard |
| `body-s` | 14px | Rajdhani Regular | Notes, captions |
| `mono-m` | 14px | JetBrains Mono | Codes, IDs |
| `mono-s` | 12px | JetBrains Mono | Debug HUD |

### 2.3 Import des fonts

Placer les fichiers `.ttf` dans `Assets/_Project/UI/Fonts/`. TMP_FontAsset généré
via **Window > TextMeshPro > Font Asset Creator** (atlas 1024×1024, 16-64px range).

---

## 3. Composants UI réutilisables

### 3.1 KButton

Bouton KINETICS 5 standard avec animations.

- **Variantes** : `Primary` (cyan filled), `Secondary` (cyan outlined), `Danger` (rouge), `Ghost` (transparent).
- **Tailles** : `SM` (32px h), `MD` (48px h), `LG` (64px h).
- **États** : Normal, Hover (cyan dim), Pressed (scale 0.96), Disabled (alpha 0.4).
- **Animations** : DOTween scale + color tween 0.1s ease-out.
- **Son** : AudioManager.PlaySFX("ui_click") on press.

```csharp
var btn = KButton.Create(variant: KButtonVariant.Primary, size: KButtonSize.MD);
btn.Label = "JOUER";
btn.OnClick += () => GameManager.Instance.StartMissionAsync("SHADOW_FALL");
```

### 3.2 KCard

Card générique pour agents, armes, missions.

- **Background** : `#1A1F33`, border 1px `#2A3550`, radius 8px.
- **Header** : image 16:9 + titre Audiowide 24px.
- **Body** : description Rajdhani 16px.
- **Footer** : actions (boutons, badges).
- **Hover** : border `#1AA1CE`, slight lift (translateY -2px), glow cyan.

### 3.3 KProgressBar

Barre de progression (health, XP, reload).

- **Style** : fond `#2A3550` height 8px, fill color configurable.
- **Animation** : DOTween value tween 0.2s ease-out.
- **Variantes** : `Health` (vert→jaune→rouge selon %), `Shield` (cyan), `XP` (jaune), `Reload` (cyan pulse).
- **Texte optionnel** : "45 / 100" à droite.

### 3.4 KModal

Fenêtre modale (settings, confirmations).

- **Overlay** : `#000000AA` (alpha 165).
- **Container** : `#1A1F33` 600×400 min, radius 12px, border 1px `#1AA1CE`.
- **Header** : titre Audiowide 24px + bouton close (×).
- **Body** : contenu scrollable.
- **Footer** : boutons Cancel (Secondary) + Confirm (Primary).
- **Animation** : fade + scale 0.95 → 1.0 (0.15s ease-out).

### 3.5 KToast

Notification temporaire (succès, erreur).

- **Position** : top-center, animée en from-top.
- **Durée** : 3s par défaut.
- **Variantes** : `Success` (vert), `Warning` (jaune), `Error` (rouge), `Info` (cyan).
- **Animation** : translateY -50 → 0 + fade in, fade out + translateY -50.

### 3.6 KTooltip

Infobulle au survol.

- **Délai** : 0.5s.
- **Position** : au-dessus de la cible.
- **Style** : `#05060F` fond, `#1AA1CE` border 1px, texte Rajdhani 14px blanc.

---

## 4. Écrans (33 au total)

### 4.1 Écrans du PDF (8 principaux)

| # | Écran | Description | État |
|---|---|---|---|
| 1 | **Start Screen** | NEW GAME / CONTINUE / LOAD GAME / OPTIONS / QUIT | ✅ |
| 2 | **Mission Loading** | Tip + barre progression (55%) | ✅ |
| 3 | **Main Lobby** | Character focus central, XP/CR, Current Mission, sidebar | ✅ |
| 4 | **Agents & Loadout** | Cards agents (VULCAN/XEN/JOLT/XANO), progression | ✅ |
| 5 | **Armory** | Armes avec attributs dynamiques, locked/unlocked | ✅ |
| 6 | **Combat HUD** | Vitals bas, ammo, minimap, time, RIFLE | ✅ |
| 7 | **Victory / Defeat** | VICTORY (Continue/Rematch/Settings/Save) / FAILED | ✅ |
| 8 | **Operation Summary** | Objectifs multi-colonnes + rewards + level up | ✅ |
| 9 | **Settings** | Language, Music, SFX, Difficulty, Graphics, Save | ✅ |

### 4.2 Écrans extras (25)

| # | Écran | Catégorie | Description |
|---|---|---|---|
| 10 | **Inventory** | Meta | Inventaire complet (armes, gadgets, ressources) |
| 11 | **Shop** | Meta | Achat armes/skins/boosts avec CR ou premium |
| 12 | **Mailbox** | Meta | Messages système + cadeaux + rewards |
| 13 | **Battle Pass** | Meta | Progression BP, tiers, récompenses |
| 14 | **Profile / Stats** | Meta | Stats joueur (K/D, missions, temps de jeu) |
| 15 | **Leaderboard** | Meta | Classement global/friends/crew + seasons |
| 16 | **Friends / Crew** | Social | Liste amis, équipage, inviter |
| 17 | **Mission Select** | Meta | Carte des régions + sélection mission |
| 18 | **Tutorial** | Onboarding | Tutoriel interactif (5 étapes) |
| 19 | **Onboarding** | Onboarding | First-time user experience (FTUE) |
| 20 | **Credits** | Meta | Équipe, licences, remerciements |
| 21 | **Pause Menu** | In-mission | Resume / Settings / Quit Mission |
| 22 | **Map** | In-mission | Carte mission avec objectifs |
| 23 | **Codex / Lore** | Meta | Lore agents, ennemis, régions |
| 24 | **Crafting** | Meta | Craft armes/items avec ressources |
| 25 | **Daily Login** | Meta | Récompenses journalières (7 jours cycle) |
| 26 | **Achievements** | Meta | Succès (50+), progress bars |
| 27 | **Chat** | Social | World chat / crew chat / DM |
| 28 | **Crew Management** | Social | Gestion équipage (membres, rôles, kick) |
| 29 | **Crew War** | Social | Affrontements d'équipages (seasons) |
| 30 | **Match Summary (multi)** | Multi | Récap match multijoueur |
| 31 | **Damage Breakdown** | Multi | Détail dégâts par arme/élément |
| 32 | **Settings Controls** | Settings | Rebind des contrôles par device |
| 33 | **Settings Graphics** | Settings | Qualité graphique, FPS cap, FOV |

---

## 5. Flow de navigation

```
START ─── NEW GAME ─── ONBOARDING ─── TUTORIAL ─── LOBBY
       └── CONTINUE ── LOAD SLOT ────────────────────┘
       └── LOAD GAME ── SLOT SELECT ─────────────────┘
       └── OPTIONS ──── SETTINGS ──── (back)
       └── QUIT ─────── CONFIRM MODAL ── EXIT APP

LOBBY ─── PLAY ───── MISSION SELECT ── MISSION LOADING ── COMBAT HUD
       ├── AGENTS ──── AGENTS & LOADOUT ── TALENT TREE
       ├── LOADOUT ─── ARMORY ──── (weapon detail)
       ├── SHOP ────── SHOP ────── (item detail)
       ├── MAILBOX ─── MAILBOX ─── (message detail)
       ├── BATTLE PASS BATTLE PASS ── (tier rewards)
       ├── PROFILE ─── PROFILE / STATS ── ACHIEVEMENTS
       ├── CREW ────── CREW MANAGEMENT ── CHAT
       └── OPTIONS ─── SETTINGS ──── (back)

COMBAT HUD ─── PAUSE MENU ─── (settings / quit mission)
            └── (mission complete) ── VICTORY / DEFEAT
                                       └── OPERATION SUMMARY ─── LOBBY
```

---

## 6. Mobile touch controls

### 6.1 Layout HUD (page 6 PDF)

```
┌─────────────────────────────────────┐
│ [A]            [HIDE MAP]   [B]     │  <- minimap top-right, points A/B
│                                     │
│        (FPS VIEW 3D)                │
│                                     │
│ 20 | 60       12:39 TIME LEFT       │  <- ammo + time
│ HEALTH ████░░  ARMOR ████░░         │  <- vitals bottom
│ RIFLE                               │
└─────────────────────────────────────┘
```

### 6.2 Joystick gauche flottant

- **Zone** : 40% gauche de l'écran (`leftZoneRatio = 0.4`).
- **Rayon** : 110px (`joystickRadius`).
- **Origine** : position du premier touch dans la zone.
- **Visuel** : cercle cyan `#1AA1CE` 80% transparent + stick central.
- **Sortie** : `InputState.Move = Vector2(-1..1, -1..1)`.

### 6.3 Boutons droits

| Bouton | Taille | Position | Action |
|---|---|---|---|
| **FIRE** | 96×96 | bottom-right | Tir maintenu |
| **AIM** | 72×72 | à gauche FIRE | Visée (toggle) |
| **RELOAD** | 64×64 | au-dessus AIM | Reload |
| **JUMP** | 64×64 | à droite FIRE | Saut |
| **CROUCH** | 64×64 | à gauche JUMP | Crouch toggle |
| **INTERACT** | 56×56 | centre-bas | Interact contextuel |
| **GRENADE** | 56×56 | top-right | Lance grenade |
| **TACTICAL** | 56×56 | à gauche GRENADE | Active tactical |
| **SWITCH** | 56×56 | top-right-2 | Switch weapon (1↔2↔3) |

### 6.4 Swipe droit (look)

- **Zone** : 60% droite de l'écran (hors boutons).
- **Delta** : `InputState.LookDelta = touch.deltaPosition × lookSensitivity`.
- **Sensibilité** : 0.18°/pixel (mobile), 0.22°/pixel (desktop souris).

### 6.5 Haptic feedback

- Tir : vibration courte 20ms amplitude 0.3.
- Dégâts subis : vibration moyenne 80ms amplitude 0.6.
- Mort : vibration longue 300ms amplitude 1.0.
- Désactivable dans Settings.

---

## 7. Accessibilité

### 7.1 Contrastes

- Tous les textes respectent **WCAG AA** (ratio ≥ 4.5:1).
- Texte blanc sur `#05060F` : ratio 19:1 (AAA).
- Texte cyan `#1AA1CE` sur `#05060F` : ratio 6.2:1 (AA).

### 7.2 Tailles cibles tactiles

- Boutons principaux : ≥ 96×96 px (recommandation Apple HIG 44pt = ~88px).
- Boutons secondaires : ≥ 64×64 px.
- Espacement minimum entre boutons : 8px.

### 7.3 Daltonisme

- Couleurs de danger (rouge) toujours accompagnées d'une **icône** (⚠️ ou ☠).
- Health bar a un **texte** "45/100" en plus de la couleur.
- Éléments (Kinetic/Energy/Cryo/Volt/Explosive) ont des **icônes** distinctes.

### 7.4 Sous-titres

- Toute ligne de dialogue vocale a un sous-titre optionnel (activé par défaut).
- Style : Rajdhani 18px blanc, fond `#000000AA`, durée min 3s.

### 7.5 Réduction de mouvement

- Setting "Réduire les animations" désactive les tweens DOTween (remplace par instant).
- Screen shake réduit à 30% si activé.

---

## 8. Internationalisation (i18n)

### 8.1 Langues supportées (7)

| Code | Langue | Statut |
|---|---|---|
| `fr` | Français | ✅ Default |
| `en` | English | ✅ |
| `ja` | 日本語 | ✅ |
| `zh-CN` | 简体中文 | ✅ |
| `ko` | 한국어 | ✅ |
| `es` | Español | ✅ |
| `de` | Deutsch | ✅ |

### 8.2 Fichiers JSON

Placés dans `Assets/StreamingAssets/Localization/{lang}.json`. Format :

```json
{
  "ui.start.new_game": "NOUVELLE PARTIE",
  "ui.start.continue": "CONTINUER",
  "ui.lobby.play": "JOUER",
  "mission.shadow_fall.name": "SHADOW FALL",
  "mission.shadow_fall.desc": "Infiltrez le cargo..."
}
```

### 8.3 Runtime switch

```csharp
await LocalizationManager.Instance.SetLanguageAsync("en");
// Re-render tous les composants localisés via LanguageChanged event.
```

### 8.4 Fallback

Si une clé manque dans `{lang}.json`, on retombe sur `en.json`, puis sur la clé brute.

---

## 9. UI Toolkit vs UGUI

### 9.1 Choix par écran

| Type | Techno | Raison |
|---|---|---|
| Menus statiques (Start, Lobby, Settings) | **UI Toolkit (USS/UXML)** | Style CSS-like, perf, data-binding |
| HUD combat (dynamic) | **UGUI (Canvas Screen Space)** | World space nécessaire pour damage numbers, intégration VFX |
| Damage numbers flottants | **UGUI World Space** | Doivent suivre les ennemis 3D |
| Tooltips | **UGUI** | Simple, déjà câblé |

### 9.2 Performance UI Toolkit

- **Atlas** : un seul `VisualElementAsset` partagé.
- **USS** : un seul fichier `Kinetix5.uss` (pas de sheets multiples).
- **Animations** : USS transitions (GPU), pas de tweens CPU pour les hover.

---

## 10. Conventions de nommage UI

### 10.1 USS classes

```css
.k5-button              /* base */
.k5-button--primary     /* modificateur variante */
.k5-button--secondary
.k5-button--danger
.k5-button--sm          /* modificateur taille */
.k5-button--md
.k5-button--lg
.k5-button__label       /* élément enfant */
.k5-button__icon
```

### 10.2 GameObjects UGUI

```
Canvas/
├── HUD_Container/
│   ├── TopBar/
│   │   ├── Minimap_Panel/
│   │   └── Timer_Text
│   ├── Crosshair/
│   ├── BottomBar/
│   │   ├── Health_Bar/
│   │   ├── Shield_Bar/
│   │   └── Ammo_Text
│   └── TouchControls/
│       ├── Joystick_Left/
│       └── Buttons_Right/
```

### 10.3 Noms d'écrans

- `Screen_StartMenu`
- `Screen_Lobby`
- `Screen_MissionLoading`
- `Screen_CombatHUD`
- `Screen_PauseMenu`
- `Screen_Victory`
- `Screen_Defeat`
- `Screen_OperationSummary`

---

## 11. Références

- **PDF source** : `upload/shooter mobile game 5 2.pdf` (layout HUD page 6, palette page 8).
- **Composants UI** : `Assets/_Project/UI/Components/` (à créer par agent 2-c).
- **Localisation** : `Assets/StreamingAssets/Localization/` (7 fichiers JSON).
- **Fonts** : `Assets/_Project/UI/Fonts/` (Audiowide, Rajdhani, Inter, JetBrains Mono).
- **UI Toolkit docs** : https://docs.unity3d.com/6000.0/Documentation/Manual/UIElements.html
