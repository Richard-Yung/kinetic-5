# KINETICS 5 — Research Report: Similar FPS Mobile Sci-Fi Shooters

> **Task ID:** r2
> **Agent:** Game Research
> **Scope:** Comparative analysis of 8 FPS / sci-fi shooters (mobile + console/PC) to extract game systems and best practices applicable to **KINETICS 5** — a Unity 6000 LTS mobile sci-fi FPS set in inter-stellar warships with 4 agents (VULCAN, XEN, JOLT, XANO), 9 weapons (5 primary + 4 secondary), 4 tactical items, 7 missions, and a fixed cyan #1AA1CE / Audiowide visual identity.
> **Method:** Synthesis of (a) 12+ targeted web searches against the z-ai `web_search` index, (b) project-internal documentation (`worklog.md`, `PROJECT_ARCHITECTURE_3D_UNITY.md`), and (c) consolidated design knowledge of each title.

---

## 0. Executive Summary

Across all 8 studied titles, three patterns dominate the modern mobile FPS in 2024–2026:

1. **Multi-finger claw layouts beat 2-finger defaults** — CoD Mobile, Modern Combat 5, and PUBG Mobile all converge on a 4-finger claw (left thumb = move joystick, left index = scope/crouch, right thumb = look + auto-fire region, right index = fire button + grenade + jump). Touch targets are **44–72 px** minimum (Apple HIG 44 pt, Material 48 dp, NN/g 1 cm²); fire buttons are larger (90–140 px) and placed above the right thumb's natural rest position so the camera-swipe area stays unobstructed.
2. **Enemy AI is overwhelmingly FSM + utility blending** — Pure FSMs (Patrol → Chase → Attack → Retreat) cover 80% of mobile-shooter needs; behavior trees and GOAP appear only on high-end projects (Destiny 2, F.E.A.R.-style clones). The Reddit/Unity-Discussions consensus is **start with FSM, add a thin utility layer for "tactical" decisions** (which cover point, when to flank, when to retreat to heal).
3. **Mission structure is layered, not linear** — Every successful title layers (a) a primary objective path (corridor + arena sequence), (b) wave-based defense/hold beats, (c) a multi-phase boss, and (d) an extraction/checkpoint. KINETICS 5's 7-mission arc already follows this; the gap to close is **objective-type variety per mission** and **boss-phase scripting**.

KINETICS 5's existing assets (EnemyAI.cs, EnemyController.cs, BossController.cs, BossPhaseManager.cs, MissionDirector.cs, ObjectiveTracker.cs, ExtractionZone.cs, EnemySpawner.cs, PlayerController.cs, PlayerCombat.cs, InputManager.cs with floating joystick + swipe-right + UI buttons, HUDController.cs, MissionSO/AgentSO/WeaponSO/EnemySO/RegionSO/MissionSO/ProgressionCurveSO ScriptableObjects) map cleanly to the synthesized patterns below — this report ends with concrete per-file recommendations.

---

## 1. Per-Game Analysis

### 1.1 Destiny 2 (Bungie, 2017–)

**Genre:** FPS RPG shared-world shooter (PC/console, not mobile, but the gold standard for sci-fi FPS mission + enemy design).

**Touch controls layout** — N/A (controller/KB+M). However, Bungie's HUD is the canonical reference for sci-fi FPS diegetic UI: bottom-left ability cooldown radial, bottom-right weapon + ammo, top-right objective tracker, center reticle with hit-marker sub-layer, super energy bar wrapping the class icon. **Translates to mobile** as: keep ability buttons in the right-bottom cluster, weapon/ammo as a single readout bottom-center, objective tracker top-right under the minimap.

**Movement mechanics:** Sprint (always-on toggle), slide (crouch + sprint), double-jump + class-specific glide/burst/catapult (verticality), dodge (Hunter class), barricade (Titan), rift (Warlock). The lesson for KINETICS 5: **each agent should have a class-specific movement verb** (VULCAN = heavy bash/charge; XEN = dash; JOLT = slide-boost; XANO = grappling recon).

**Combat feel:** Generous aim assist (bullet magnetism + reticle friction on console, removed on PC). Hit markers are 4-prong white for body, red for crit/precision, with an audible "ting". Damage numbers are off by default (only in PVE boss fights). Recoil is per-weapon, recovery curves are smooth. **Reload animation** has a 0.5–1.0s "ready window" — Destiny's signature "reload-cancel" via weapon swap. Recommended for K5: implement hit markers (4-prong, cyan for body, #FFE735 yellow for crit, #FE0022 red for kill), keep damage numbers optional via settings.

**Enemy AI behaviors:** Four/five factions (Cabal, Vex, Hive, Fallen, Scorn, Taken). Each has archetypes:
- **Grunts/Thrall/Dreg** — fast melee rushers, low HP, swarm tactics.
- **Acolyte/Legionary/Goblin** — ranged riflemen, take cover, pop out to shoot.
- **Knight/Minotaur/Centurion** — heavy units, suppressive fire, advance under fire.
- **Wizard/Psion/Hobgoblin** — flankers/snipers, perch on high ground, retreat when approached.
- **Hydra/Ogre/Colossus** — mini-boss tier, multiple damage states, AOE attacks.
Critically, Bungie AI uses **squad logic**: when one enemy is engaged, nearby allies are alerted via simulated "radio call" (a 0.5–2s delay). Suppressing fire breaks their advance. Flankers wait until the player is engaged with another unit. **This squad-aware behavior is the single highest-impact pattern for K5 to adopt.**

**Mission structure:** Strikes (15-25 min linear PVE missions), Nightfalls (timed, modifier-stacked Strikes), Patrol zones (open areas with multiple activity types), Public Events (timer-based, transform into Heroic if triggered), Lost Sectors (mini-dungeons), Raids (6-player, multi-encounter, mechanic-heavy). Activities layer: trash → major (yellow-bar) → miniboss → boss (multi-phase). **Strike formula**: zone-in → corridor combat room 1 → arena with 2-wave defense → mechanic room → boss arena (3 phases with damage gates). **K5 missions should follow this 4-beat structure.**

**HUD elements:** Reticle (dynamic, widens on movement/fire), 4-directional hit markers, ammo "21 | 240" (mag/reserve) bottom-right, ability icons bottom-left with radial cooldown, super meter wrapping class icon, objective text top-center-left, minimap top-left with enemy radar (red dots, faded by height), kill feed top-right under objective. **Damage direction indicators** (red arcs at screen edge when hit from off-screen).

**Progression:** Power/Light level (gear-based), XP → season pass ranks (1–100+), weapon level (crafting patterns), triumph score. Loot: RNG drops with curated perks, vendors, crafting. **For K5:** XP → player level (1–60+) unlocks agents; CR (credits) as soft currency; weapon unlocks tied to mission completion or level gates (already in progression.json per worklog).

**Navigation:** Patrol zones are open with fast-travel between landing zones; Strikes are linear corridors between arena rooms; Lost Sectors are A→B→boss. **K5 ship-interior levels:** corridor-first (cohesive ship geometry) with branching arena rooms.

---

### 1.2 Snowbreak: Containment Zone (Seasun, 2023–)

**Genre:** 3D anime sci-fi RPG shooter (Unreal Engine 4, mobile + PC). The closest direct competitor to KINETICS 5 in tone (sci-fi, character-driven, mobile-first).

**Touch controls layout:** Left floating joystick (movement), right-side large fire button, dedicated aim-down-sights (ADS) button, skill button (per-character ultimate), ultimate button, weapon-skill button, dodge/sprint button, swap-character button. Layout is **simpler than CoD Mobile** — fewer buttons because character switching handles tactical variety. **Auto-aim assist is strong** (bullet magnetism to nearest target). On PC, full KB+M with a third-person (over-the-shoulder) camera; mobile uses the same TPS camera with on-screen controls. **Insight for K5:** if K5 adopts a TPS camera for bosses (it currently uses FPS), Snowbreak's 3-character swap-in combat rhythm is a proven template.

**Movement mechanics:** Sprint (toggle), dodge roll (i-frames, directional), slide, **cover auto-snap** when walking into low cover. Vaulting is automatic when running into knee-high obstacles. No grapple; verticality is limited (mostly flat arenas with raised platforms). Snowbreak's signature: **dodge-cancel** windows during attack animations — feels fluid on mobile.

**Combat feel:** Strong aim assist + soft lock-on to current target. Hit markers are minimal (small white flash). **Damage numbers are prominent** (anime-staple), color-coded by element (cyan = ice, red = fire, yellow = electric, purple = kinetic). Elemental weakness system rewards damage-type matching. Recoil is minimal (arcade feel). **Reload animations are stylized** (1.5–2.5s with weapon-specific flair).

**Enemy AI behaviors:** Simplified versus Destiny: enemies in **Containment Zone** missions tend to be (a) trash mobs that rush, (b) turreted enemies that hold position, (c) elite mini-bosses with telegraphed AOE attacks. Boss patterns are heavily choreographed (wind-up → telegraph → execute → recovery). **Snowbreak emphasizes predictable telegraphs over emergent AI** — this is a deliberate mobile-friendly choice.

**Mission structure:** Story missions (linear, dialogue-heavy), operations (shorter combat-focused), raids (co-op, multi-stage boss). Each mission: intro cutscene → first combat room → mid-boss/elite → dialogue → final boss arena. **No extraction mechanic** — missions end on boss kill + cutscene.

**HUD elements:** Top-left character portraits (active + 2 reserves) with HP bars, bottom-left active character skill icons + ultimate gauge, top-right minimap (small, optional), bottom-center fire/aim/reload/dodge cluster, center reticle. Damage numbers float up with element color. Combo counter top-center.

**Progression:** Gacha for characters (Operatives) and weapons. Operatives have **Constellation-like duplicates** that unlock constellations/abilities. Operatives are Ballistic (gun-focused), Skill (ability-focused), or Hybrid. Weapons: SMG, shotgun, sniper, AR, pistol — each Operative is locked to one weapon type. **K5 parallel:** 4 agents (VULCAN/XEN/JOLT/XANO) ≈ 4 "Operatives"; weapon types ≈ 5 primaries (HEAVY RX-14, RIFLE CX-24, AX-9 SR, CX-27 ATLAS, C-2).

**Navigation:** Mission select screen → load → linear level with occasional side-room for loot. No patrol zones; no fast travel within mission.

---

### 1.3 Call of Duty Mobile (TiMi / Activision, 2019–)

**Genre:** Military FPS, mobile (multiplayer + battle royale). The control/HUD gold standard for mobile FPS.

**Touch controls layout — the canonical 4-finger claw:**
- **Left thumb:** floating movement joystick (bottom-left, ~140 px diameter, 50% opacity).
- **Left index (top-left):** crouch-prone-jump column, scoreboard, grenade.
- **Right thumb (bottom-right):** fire button (large, ~120 px, positioned above thumb-rest so the lower-right area is free for camera-swipe look), reload (above fire), aim/ADS (left of fire), weapon switch (above reload), grenade (right of fire), jump (top-right edge).
- **Right index (top-right):** scope toggle, prone, melee, killstreak.
- **Center-bottom:** auto-sprint toggle (default on), gyroscope toggle.
- **Auto-fire:** OFF by default in multiplayer, ON (smart auto-fire) in Battle Royale — taps fire when an enemy crosses the reticle. **K5 should default auto-fire ON for accessibility, with toggle in settings.**

CODM offers fully **customizable HUD**: drag/drop, resize, opacity per element, multi-profile (2/3/4/5/6-finger presets). Sensitivity sliders: camera, ADS, gyroscope, per-scope-zoom. **This is the UX bar to meet.**

**Movement mechanics:** Sprint (auto-sprint toggle), slide (crouch while sprinting), jump, mantle (auto over low cover), swim, climb. No dodge/dash. No cover-snap (CoD is run-and-gun).

**Combat feel:** Heavy aim assist (rotation + deceleration bubble) on mobile, configurable. Hit markers: 4-prong, white for body, red (with sound) for headshot, X-mark for kill. **No floating damage numbers** (CoD tradition). Recoil patterns per weapon, with first-shot-accurate tap-fire. Reloads 1.5–3.5s with cancel-via-sprint.

**Enemy AI behaviors (PVE / Zombies):** Zombies mode AI: predictable pathfinding toward player, no cover usage, but variable speeds (walkers, runners, boss zombies with telegraphed slams). Multiplayer bots: aimbot-ish, take cover, throw grenades, rush player on hard difficulty. **Limited squad coordination** vs Destiny.

**Mission structure:** Multiplayer matches (5-10 min, TDM/DOM/Hardpoint/S&D). Battle Royale (20 min, 100 players). Zombies: linear co-op with wave survival between corridor sections. **Spec Ops missions** (the closest to K5 missions): 4-beat structure — infiltrate → engage → objective (hack/defend/extract) → exfil.

**HUD elements:** Minimap top-left (rotating, enemy red dots on fire, directional indicators), HP bar bottom (regen system, no number), ammo "30 | 90" bottom-right under weapon name, kill feed top-right, hit markers center, grenade indicator (where thrown from), damage direction arcs, compass strip top-center (in BR). Scorestreak/killstreak icons bottom-center-left.

**Progression:** Player rank (1–150+), weapon level (per weapon, unlocks attachments), Battle Pass tiers (free + premium), camo challenges, operator unlocks. Multiple currencies: CP (premium), CP fragments, credits.

**Navigation:** Menu → load → match → menu. No persistent world.

---

### 1.4 Shadowgun: DeadZone (Madfinger, 2012–2016)

**Genre:** Sci-fi TPS mobile FPS (PvP-focused). The spiritual predecessor to all mobile sci-fi shooters — and the inspiration for many cover mechanics.

**Touch controls layout:** Left virtual joystick (movement), right-side fire button + look-swipe area, dedicated **cover button** (appears contextually when near cover), reload, grenade, weapon switch. Two-thumb default; advanced players used 3-finger. **Smaller button footprint than CoD Mobile** (Madfinger was early mobile, screens were 3.5–4.3").

**Movement mechanics:** The defining feature — **contextual cover system**: walk into cover, tap cover button → character snaps to cover, third-person camera shifts to over-the-shoulder, can lean out to fire (pop-and-shoot). TPS camera (not FPS) made cover meaningful. Sprint, no slide, no vault, no dodge. **Strategic pacing** vs CoD's run-and-gun.

**Combat feel:** Aim assist minimal (Madfinger aimed for "skill-based"). Hit markers subtle. Recoil per weapon. Reload animations were cutting-edge for 2012 mobile. No damage numbers.

**Enemy AI behaviors (PvE Deathmatch vs bots):** Bots took cover, popped out to fire, advanced in bounds. **The cover system shaped bot behavior** — they would actually use the same cover points players used. Bot accuracy scaled with difficulty. Limited squad tactics.

**Mission structure:** Deathmatch (12-player FFA) and Zone Defense (zone control). No campaign — pure PvP. The Shadowgun: DeadZone **legacy for K5** is the cover system design, not mission structure. The original Shadowgun (single-player) had linear missions with set-piece encounters.

**HUD elements:** Minimap top-right, HP bar bottom-left, ammo bottom-right, weapon icon, grenade icon, cover indicator (icon when cover is available), objective marker, kill feed top-center-right. **Minimalist** by modern standards.

**Progression:** Weapon unlocks via credits (earned or premium), character skins, skill/perk slots. **Limited RPG progression** — Shadowgun was a competitive shooter.

**Navigation:** Match-based; no persistent world. Lobby → match → results → lobby.

**K5 takeaway:** Implement an **optional contextual cover system** (auto-snap when walking into low cover in certain missions, e.g., DEEP SIGNAL stealth). Not mandatory for all 7 missions — keep the run-and-gun core.

---

### 1.5 N.O.V.A. Legacy (Gameloft, 2017)

**Genre:** Sci-fi mobile FPS (remake of N.O.V.A. 1). The closest free-to-play mobile sci-fi FPS precedent before Snowbreak.

**Touch controls layout:** Left floating joystick, right-side fire button + look-swipe, contextual buttons for interact/reload/grenade/weapon-switch. **Auto-aim assist strong** (the game was designed for casual mobile). 2-finger default; advanced players used 3-finger. Buttons sized ~80–110 px. **Smart auto-fire** when enemy in reticle.

**Movement mechanics:** Sprint (toggle), jump, crouch. No slide, no dodge, no vault, no cover. **Simple movement** — N.O.V.A. Legacy targeted low-end devices.

**Combat feel:** Strong aim assist (rotation + soft lock). Hit markers minimal. **No damage numbers**. Recoil minimal. Reload animations 1.5–2.5s. Energy weapons + ballistic weapons, each with distinct feel.

**Enemy AI behaviors:** Wave-based combat arenas. Three enemy types: (a) **rusher melee** (fast, low HP, swarm), (b) **ranged shooter** (takes cover, pops out, suppression fire), (c) **heavy/special** (mini-boss, telegraphed attacks). **Simpler AI than Destiny** but effective for mobile. Patrol behavior in corridors (waypoint-based), alert behavior when player spotted, then chase-and-shoot. No flanking; no retreat-to-heal.

**Mission structure:** **Campaign mode with linear missions** — each mission is 5–10 minutes with: intro dialogue → corridor combat → arena wave defense → mid-boss → corridor → final arena → mission-complete screen. Multiplayer mode (5v5 TDM/FFA) separate. Mission types: assault, defend, escort, survive, boss. **N.O.V.A. Legacy is the closest structural analog to K5's 7-mission campaign.**

**HUD elements:** Top-left minimap (small), top-right objective tracker, bottom-left HP bar (with shield overlay), bottom-center ammo + weapon name, bottom-right fire/aim/reload/grenade cluster. Hit markers center. **No kill feed in campaign** (single-player). Damage direction indicators.

**Progression:** Mission unlock (linear), weapon unlocks via credits, weapon upgrades (damage, fire rate, accuracy, clip size), armor upgrades, suit ability upgrades. **Per-weapon progression** — each weapon had 5–10 upgrade tiers. **K5 parallel:** weapon stats in weapons.json already support this.

**Navigation:** Mission select screen → load → linear mission → results → next mission. No fast-travel within missions. Some missions had branching paths (rare).

---

### 1.6 Dead Effect 2 (BadFly Interactive, 2015–)

**Genre:** Sci-fi FPS RPG (mobile + PC). The closest analog to a "single-player sci-fi FPS RPG on mobile" — and a useful counterpoint to Destiny 2's online focus.

**Touch controls layout:** Left floating joystick, right-side fire + look-swipe, dedicated ADS, reload, grenade, weapon wheel (hold to open, select weapon), ability buttons (character-specific powers). **More buttons than N.O.V.A. Legacy** because of the RPG ability system. Auto-aim assist configurable.

**Movement mechanics:** Sprint, crouch, jump. No slide, no dodge, no cover. **Movement is more deliberate / horror-paced** than N.O.V.A. — Dead Effect 2 leans into sci-fi horror tone. Slow walk + sprint only.

**Combat feel:** Aim assist configurable (off / soft / strong). **Damage numbers prominent** (RPG-staple) — color-coded by damage type and critical hits. Hit markers present. Recoil per weapon. Reload animations 1.5–3.5s, with magazine-out/magazine-in/check-chamber beats. **Over 100 unique weapons**, each with modular attachments (scope, suppressor, magazine, barrel, stock, laser) — modular attachment system is Dead Effect 2's signature feature.

**Enemy AI behaviors:** Zombies (multiple variants — walkers, runners, crawlers, spitters, exploders, brutes), cyborg soldiers (ranged, take cover, suppress, flank), and bosses (multi-phase with telegraphed attacks). **Enemy variety is Dead Effect 2's strength** — 20+ enemy types across the campaign. Zombies use simple chase AI; soldiers use FSM with cover; bosses use scripted phases.

**Mission structure:** Campaign with **long missions (15–30 min)**, each with multiple objectives: kill X, fetch Y, hack terminal, survive waves, defeat boss. **Side missions** for loot/XP. **No extraction mechanic** — mission ends on final objective. Difficulty tiers (Normal/Hard/Insane) replayable for better loot. **Per-mission objective variety is high** — the game rotates between assault, defend, escort, hack, survive, boss.

**HUD elements:** Bottom-left HP + shield, bottom-right ammo + weapon, top-left minimap (toggleable), top-right objective tracker, center reticle + hit markers, floating damage numbers, ability icons bottom-center-right, weapon wheel (hold-Q style). Loot pickup notifications top-center. Boss HP bar at top-center during boss fights.

**Progression:** Character level (cap 20), weapon level (separate from character), skill tree per character (3 characters with distinct trees), credits + premium currency, weapon mods (loot-based), character skins. **Deep RPG progression** — Dead Effect 2 is more RPG than shooter in its progression depth. **K5 parallel:** the agent-specific progression (VULCAN/XEN/JOLT/XANO unlock paths) maps directly.

**Navigation:** Mission select → load → mission (with in-mission checkpoints) → results → next mission. No fast-travel within missions. Some missions had branching paths and secret areas.

---

### 1.7 Modern Combat 5 (Gameloft, 2014–)

**Genre:** Military mobile FPS — the predecessor to CoD Mobile and the first "console-feel" mobile FPS.

**Touch controls layout:** Same general layout as CoD Mobile (floating joystick left, fire + look-swipe right, ADS, reload, grenade, weapon switch, jump, crouch). **3-finger layout popular** (left thumb move, right thumb aim + fire, right index sprint/crouch). **Gyroscope aiming** was MC5's signature — tilt phone for fine aim adjustment, configurable sensitivity (1–100). Buttons ~80–120 px.

**Movement mechanics:** Sprint (toggle), crouch, prone, lean (peek around corners — distinctive MC5 feature), jump, slide (added in later updates). **Lean mechanics** are rare in mobile FPS and worth noting for K5 stealth missions (DEEP SIGNAL).

**Combat feel:** Strong aim assist (configurable). Hit markers present. **No damage numbers**. Recoil per weapon (predictable patterns). Reload animations 1.5–3s. **MC5 popularized gyroscope aiming** for mobile FPS — fine-tuned gyro + low-sensitivity swipe = precision aim.

**Enemy AI behaviors (campaign):** Soldier AI: patrol waypoints → alert on sight → take cover → pop-out shoot → advance/retreat based on HP → throw grenade → call for reinforcement (squad alert). **More sophisticated than N.O.V.A. Legacy**. Sniper enemies perch on high ground. Heavy enemies suppress with LMG. Closer to Destiny 2's AI complexity than to other mobile contemporaries.

**Mission structure:** Campaign with chapters, each chapter has 3–5 missions. Mission types: assault, defend, snipe, drive (vehicle section), breach (door-kick + slow-mo). **Multiplayer** separate (4v4 TDM, FFA, etc.). **MC5's campaign is the structural template** for what K5's missions should feel like.

**HUD elements:** Top-left minimap (rotating, enemy arrows), top-right objective, bottom-left HP bar (segmented), bottom-right ammo + weapon, center reticle + hit markers, grenade indicator, damage direction arcs. Kill feed top-right in multiplayer. Compass top-center.

**Progression:** Player rank (1–90+), weapon unlocks per class (Assault, Heavy, Recon, Sniper, Sapper — **5 classes**), weapon upgrades (attachments), class-specific abilities, killstreaks. Multiple currencies. **The 5-class system is a direct parallel** to K5's 4-agent system.

**Navigation:** Chapter select → mission select → load → mission → results. No persistent world. Vehicle sections broke up corridor monotony.

---

### 1.8 Apex Legends Mobile (Respawn / Tencent, 2022–2023 — sunset)

**Genre:** Battle royale hero shooter (mobile). The reference for character-ability design in a mobile shooter.

**Touch controls layout:** Apex Mobile's **advanced mode** supported 4-finger claw with full customization. Default layout: left joystick (move), right thumb (look + fire), top-right cluster (abilities + ultimate), top-left (jump + crouch), bottom-right (reload + weapon switch + grenade). **Apex Mobile introduced "tap-to-loot"** (single-tap to grab loot without holding). Gyroscope aiming supported. Buttons 90–140 px. **Smart auto-fire** when reticle on enemy (configurable).

**Movement mechanics:** Apex Mobile's defining feature — **movement depth**: sprint, slide (long, momentum-preserving), slide-hop (slide → jump → slide), wall-run (limited), climb (auto-mantle tall walls), zipline, jump pad. **Per-character movement abilities** (Fade's Slipstream, Octane's stim, Pathfinder's grapple). **The movement skill ceiling is the highest of any mobile shooter** — a key reference for K5 if it wants movement to be a skill differentiator.

**Combat feel:** Aim assist moderate (more than PC, less than CoD Mobile). Hit markers present (white body, red headshot). **No damage numbers in-world** (only on-screen damage breakdown post-fight). Recoil per weapon, with recoil-smoothing during slides. Reload animations 1.5–3.5s.

**Enemy AI behaviors:** Apex Mobile was PvP-only — no PVE AI enemies. However, **legend abilities** (Fade, Rhapsody, Loba, Octane, etc.) provide a **character-ability design template**:
- **Fade (Mobile exclusive):** Passive = Slipstream (speed boost after slide), Tactical = Flash (go invulnerable + phase briefly), Ultimate = Void Phase (teleport backwards).
- **Rhapsody:** Passive = amplified footstep audio (hear enemies further), Tactical = projectable wall of flash (cover/escape), Ultimate = speed boost + shield regen aura for squad.
- **K5 parallel:** JOLT (support) ≈ Rhapsody; XEN (DPS) ≈ Fade; XANO (recon) ≈ Bloodhound; VULCAN (tank) ≈ Gibraltar.

**Mission structure:** Battle royale only — no campaign. Squad-based 60-player matches. **Not a structural reference for K5**, but the **legend kit design** (passive + tactical + ultimate per character) is the template for K5 agent abilities.

**HUD elements:** Bottom-center health + shields (segmented bars), bottom-right ammo + weapon icon, top-right minimap (large, expandable), top-left squad health (3 teammate bars), center reticle + hit markers, ability icons bottom-right with cooldown radials, ultimate charge bar, ping system (tap to ping location/item/enemy). **The ping system** is Apex's defining innovation — worth a simplified version in K5 co-op.

**Progression:** Battle Pass tiers, legend unlocks (premium currency), legend perks (per-legend skill tree — Apex Mobile exclusive), weapon skins. **Legend perks** = 3 perks + 3 finisher perks + 3 ability perks per legend. **K5 parallel:** each agent should have a small perk tree.

**Navigation:** Drop → loot → rotate → fight → ring closes → final fight. Not applicable to K5 missions, but the **"rotations" concept** (moving between combat zones with purpose) is a useful mental model for K5 level pacing.

---

## 2. Synthesized Best Practices for KINETICS 5

Drawing from the 8 games above + the 4 best-practice searches (mobile FPS touch controls, FPS enemy AI state machines, mobile shooter HUD design patterns, Unity FPS mobile controller assets), here are the synthesized best practices:

### 2.1 Touch Controls Best Practices

From the design literature (NN/g, Smashing Magazine, UX Movement, Apple HIG, Material Design):
- **Minimum touch target: 44×44 pt (iOS) / 48×48 dp (Android) / 1 cm² physical.** Below this, fat-finger errors dominate.
- **Comfortable target: 60×60 px**, with **fire button at 90–140 px** (it's the most-used and most-missed).
- **Maximum useful target: 72×72 px** — beyond this, no benefit (UX Movement study).
- **Spacing between targets: ≥8 px** to prevent double-taps.
- **Opacity: 50–70%** for non-action buttons (so the 3D scene shows through), 80–100% for fire/critical buttons.

From mobile FPS games specifically:
- **Floating joystick (dynamic origin)**: joystick appears where the left thumb first touches. Reduces reach strain. N.O.V.A. Legacy, CoD Mobile, MC5, Snowbreak all use this.
- **Look-swipe area** should occupy **the entire right half of the screen below the buttons**, with the fire button as an island within it. The fire button does not block look — swiping starting on the fire button still rotates the camera (CoD Mobile pattern).
- **Auto-fire ON by default** for accessibility (N.O.V.A. Legacy, Snowbreak, Apex Mobile BR mode). Toggle in settings for skilled players.
- **4-finger claw is the high-skill ceiling** (CoD Mobile, MC5). Provide a 4-finger preset but default to 2-finger for new players.
- **Gyroscope aiming toggle** with separate sensitivity (MC5, CoD Mobile, PUBG Mobile). Off by default, since many players dislike it.
- **Customizable HUD** with drag-drop, resize, opacity per element, multi-profile (CoD Mobile, MC5). **K5 should provide at minimum 3 presets (2/3/4-finger) + custom.**

### 2.2 Enemy AI Best Practices

From the Unity FSM tutorials and the Reddit/Unity-Discussions consensus:
- **Start with Finite State Machine (FSM)** for 80% of enemy needs. States: Idle, Patrol, Alert, Chase, Attack, TakeCover, Flank, Retreat, Dead.
- **Add a thin utility layer** for tactical decisions (which cover point, when to flank, when to retreat to heal). Pure FSM feels scripted; utility adds variety.
- **Behavior Trees (BT)** for boss AI — bosses benefit from scripted-phase FSM + BT within phases.
- **GOAP** is overkill for mobile (computational cost + debugging complexity). Reserve for PC/console.
- **Squad logic**: when one enemy is alerted, broadcast an alert event to nearby allies with a 0.5–2s delay (simulated "radio call"). This single feature elevates AI from "dumb" to "tactical" (Destiny 2's signature).
- **Cover usage**: pre-place cover points in the level (CoverPoint MonoBehaviour with IsOccupied, IsGoodAgainst player position). Enemies query the nearest free cover point when in TakeCover state.
- **Telegraphed attacks**: every boss/champion attack must have a 0.5–1.5s wind-up animation + audio cue. Mobile players need reaction time.
- **Difficulty scaling**: scale AI by adjusting (a) reaction time, (b) accuracy, (c) aggression, (d) HP — not by changing the FSM. (Destiny 2, MC5 pattern.)

### 2.3 HUD Design Patterns

From Caliber's HUD design article, Pluralsight, and the multiplayer FPS community:
- **Corners and edges are for status info** (health bottom-left, ammo bottom-right, minimap top-right, objective top-left). Center is for reticle + hit markers + damage numbers.
- **Health/armor bars** bottom-left, segmented (10-segment or shield-overlay pattern). K5 HUD already specifies Health 5000 + Armor — use the existing pattern from the worklog.
- **Ammo readout** bottom-right (magazine | reserve), large numeric, with weapon name under it (K5 already has "20 | 60 RIFLE").
- **Minimap** top-right (CoD Mobile, MC5) or top-left (Destiny 2). K5 worklog shows top-right — keep. Make it **collapsible** (HIDE MAP button already specified).
- **Kill feed** top-right (under minimap) or top-center (CoD). For K5 single-player, replace kill feed with **enemy-engaged indicator** (small icon + count of alerted enemies).
- **Objective tracker** top-left or top-center: 2-line max, fades after 4s unless updated. Persistent tracker for current objective with distance + direction.
- **Compass strip** top-center (CoD BR, MC5): 1-line horizontal compass with N/E/S/W + objective markers. Lightweight, useful for navigation.
- **Damage direction indicators**: red arcs at screen edge pointing to source of damage taken. Universal pattern — K5 must implement.
- **Boss HP bar**: top-center, wide, with phase ticks. K5's BossPhaseManager.cs already exists — wire it to a top-center HP bar.
- **Damage numbers**: optional, toggleable. Color-code: cyan for body, #FFE735 yellow for crit, #FE0022 red for kill. (Snowbreak/Dead Effect 2 pattern.)

### 2.4 Unity Mobile FPS Controller Assets (researched)

From the Unity Asset Store searches:
- **Joystick Pack (Fenerax Studios)** — free, virtual joysticks, dynamic + fixed variants. Solid baseline for K5's left floating joystick.
- **FPS Controls for mobile devices** — $4.99, full FPS touch control suite.
- **Touch Control for Mobile** — $19.99, comprehensive mobile control kit.
- **NeoFPS** — commercial FPS controller with built-in touchscreen support (Input Manager + Input System versions).
- **Easy Mobile Joystick Controller** — position-dynamic, resolution-independent joysticks.

**Recommendation for K5:** K5 already has `InputManager.cs` (327 lines, multi-device, EnhancedTouch API, floating joystick + swipe right + UI buttons). Do **NOT** introduce a third-party asset — K5's InputManager is purpose-built and integrates with the GameEventBus, ServiceLocator, and haptic feedback. Instead, use these assets as **design references** for layout and behavior, but keep the implementation in-house.

---

## 3. Specific Recommendations for KINETICS 5

### 3.1 Touch Control Layout — Exact Positions & Sizes

All positions in **viewport coordinates (0–1)** where (0,0) is bottom-left and (1,1) is top-right. Sizes in **px at 1080p portrait reference** (K5's HUD is portrait-friendly; landscape also supported). All buttons 60% opacity unless noted. Button diameter in px; positions are button centers.

#### Default 2-finger layout (new players)
```
Element                Size(px)  Position(vp)  Notes
─────────────────────  ────────  ────────────  ─────────────────────
Movement Joystick      180       (0.18, 0.20)  Floating origin (appears at first touch)
Look/Touch area        (full)    (0.5–1.0, 0–1.0)  Right half of screen, EXCEPT where buttons are
Fire Button            140       (0.78, 0.22)  90% opacity; primary fire
ADS (Aim) Button       100       (0.66, 0.18)  Toggles aim-down-sights
Reload Button          90        (0.70, 0.34)  Above fire
Jump Button            100       (0.92, 0.30)  Right edge
Crouch Button          90        (0.62, 0.10)  Tap to crouch, hold to prone
Grenade (Tactical)     90        (0.86, 0.42)  Above fire
Switch Weapon          90        (0.74, 0.46)  Above reload
Interact/Use           90        (0.50, 0.20)  Contextual — appears when prompt
Agent Ability          110       (0.30, 0.10)  Agent-specific (dash/shield/etc.)
Auto-fire Toggle       N/A       (settings)    ON by default
```

#### 4-finger claw preset (advanced)
```
Element                Size(px)  Position(vp)  Notes
─────────────────────  ────────  ────────────  ─────────────────────
Movement Joystick      180       (0.18, 0.18)  Floating
Look/Touch area        (full)    (right half)
Fire Button (main)     130       (0.78, 0.22)
Fire Button (top-L)    100       (0.30, 0.78)  Left-index trigger
ADS                    100       (0.66, 0.18)
Reload                 90        (0.70, 0.34)
Jump                   100       (0.92, 0.30)
Crouch                 90        (0.30, 0.66)  Left-index
Grenade                90        (0.86, 0.42)
Switch Weapon          90        (0.74, 0.46)
Interact               90        (0.50, 0.20)
Agent Ability          110       (0.30, 0.10)
Ultimate               120       (0.70, 0.62)  Right-index trigger
Scoreboard/Map Toggle  80        (0.10, 0.90)  Top-left, left-index
```

#### Cross-layout rules
- **Fire button is always larger than other buttons** (it's the most-missed).
- **Look-swipe area extends underneath all right-side buttons** — swiping starting on any right-side button still rotates the camera, unless the button is held. (CoD Mobile pattern.)
- **Auto-fire ON by default** with toggle in settings (Settings screen → Combat → Auto-Fire: ON/OFF/Smart).
- **Gyroscope**: optional, off by default, separate sensitivity (Camera Sensitivity + Gyro Sensitivity + ADS Sensitivity × per-zoom).
- **Haptic feedback** on fire (light tick), grenade (medium), hit received (strong), enemy kill (medium pulse) — K5's `InputManager.cs` already supports haptics per worklog.
- **All buttons support opacity slider + drag-relocate** in Settings → Controls → Customize.

### 3.2 Enemy AI Behaviors to Implement

K5 already has `EnemyAI.cs`, `EnemyController.cs`, `EnemyCombat.cs`, `EnemySpawner.cs`, `BossController.cs`, `BossPhaseManager.cs`. The recommended FSM architecture:

#### Base FSM (all enemies)
States: `Idle → Patrol → Alert → Chase → Attack → TakeCover → Flank → Retreat → Stunned → Dead`
Transitions driven by:
- `CanSeePlayer()` (raycast + FOV check, with last-known-position memory)
- `DistanceToPlayer()`
- `CurrentHP` ( Retreat when HP < 30% if enemy is "ranged" type)
- `SquadAlertLevel` (broadcast via GameEventBus when one enemy spots player)

#### Per-archetype tuning
| Archetype | Patrol | Chase | Attack | Cover | Flank | Retreat | Special |
|---|---|---|---|---|---|---|---|
| **Grunt** (fast melee) | waypoint | direct | melee swing | no | no | below 20% HP | suicide rush at 10% HP |
| **Rifleman** (mid-range) | waypoint + cover | bounds | burst-fire | yes (priority 1) | below 50% HP | below 25% HP | suppressive fire when ally flanks |
| **Heavy** (LMG/slow) | slow waypoint | direct | suppressive | yes (low cover only) | no | no | minigun spin-up 0.7s telegraph |
| **Sniper** | perch | retreat to perch | single shot, 1.5s charge | no | no | always retreat to perch | laser-sight telegraph |
| **Shotgunner** | waypoint | direct sprint | blast, knockback | no | yes (priority 1) | no | charge attack with knockback |
| **Suicide Bomber** | waypoint | direct sprint | explode on contact | no | no | no | 1.5s beep telegraph, weakspot = backpack |
| **Shieldbearer** | slow waypoint | direct | melee + shield | shield blocks front | no | no | weakspot = back |
| **Drone** | fly waypoint | aerial chase | energy bolt | no | yes (lateral) | no | can perch on ceiling |
| **Boss** | arena center | arena bounds | phase-scripted | no | no | no | see Boss section below |

#### Squad behavior
- **Squad alert system**: when any enemy transitions to Alert/Chase, it broadcasts `EnemyAlerted` event with `position` + `alertLevel`. Nearby enemies within radius R (default 30m) receive the event with a 0.5–2.0s simulated-radio delay, then transition to Alert → investigate last-known-position.
- **Flanking coordination**: if 2+ enemies engaged with player, one (the flanker type) attempts to circle to the player's side/rear while others suppress from front.
- **Cover coordination**: cover points have an `IsOccupied` flag. Two enemies don't take the same cover.
- **Suppressing fire**: heavy/rifleman enemies fire bursts at the player's last-known position even when not visible, forcing the player to keep moving.

#### Boss phases (per K5's BossPhaseManager.cs)
Every boss has 3 phases (matching Destiny 2 / Dead Effect 2 / MC5 patterns):
- **Phase 1 (100–66% HP):** basic attacks (melee swing + ranged burst). One telegraphed special attack every 8–12s.
- **Phase 2 (66–33% HP):** adds a new attack pattern + summons 2–3 minor adds every 15s. Arena hazard activates (e.g., floor panels ignite, vents spawn poison).
- **Phase 3 (33–0% HP):** enrages — attack speed +25%, all prior attacks + new desperation attack (e.g., ground slam AOE). No more add spawns.
- **Phase transition:** short invulnerability window + cinematic camera (0.8–1.5s) + boss roar audio + screen shake. Use K5's `TimeManager.Hitstop()` + `CameraManager.Shake()` for the moment.

#### Difficulty scaling (Easy/Normal/Hard from Settings screen)
Per-Difficulty modifiers applied to all enemies:
| Difficulty | HP | Damage | Reaction Time | Accuracy | Aggression |
|---|---|---|---|---|---|
| Easy | 0.75× | 0.70× | 1.5× | 0.60× | 0.70× |
| Normal | 1.0× | 1.0× | 1.0× | 1.0× | 1.0× |
| Hard | 1.35× | 1.30× | 0.65× | 1.30× | 1.30× |

### 3.3 Mission Structure for 7 Missions

K5 already has 7 missions defined (per worklog). Below, each mission gets a full 4-beat structure inspired by Destiny 2 Strikes + MC5 chapters + N.O.V.A. Legacy linear pacing + Dead Effect 2 objective variety.

#### Mission 1 — SHADOW FALL (Extraction, tutorial)
- **Theme:** Cargo ship infiltration. Tutorial pacing.
- **Beat 1 (Infiltrate):** Spawning point → corridor with 2 grunt squads (3 each) → first weapon pickup (RIFLE CX-24). Teaches: move, look, fire, reload.
- **Beat 2 (Arena wave defense):** Cargo bay arena. 2 waves: Wave 1 = 6 grunts + 2 riflemen; Wave 2 = 4 riflemen + 1 heavy. Teaches: cover, grenade (FRAG-X), tactical item.
- **Beat 3 (Mid-boss):** A heavy soldier with shield (Shieldbearer archetype). Teaches: flank, weakspot, switch to secondary weapon.
- **Beat 4 (Extraction):** Hold extraction zone for 60s against 3 waves while extraction timer counts down. Reach extraction point → victory.
- **Boss:** None. Mission 1 has no boss.
- **Rewards:** +2000 CR, +1500 XP, RIFLE CX-24 unlocked, Mission 2 unlocked.

#### Mission 2 — NEURAL BREACH (Sabotage)
- **Theme:** Heavy cruiser, neural core. Multi-objective.
- **Beat 1 (Breach):** Door-kick breach (cinematic) → corridor with sniper perches (2 snipers, 4 riflemen). Teaches: snipers, lean/peek (if implemented).
- **Beat 2 (Hack 1):** Hack terminal A — defend for 45s vs 3 waves (each wave: 4 grunts + 1 shotgunner). Teaches: defense.
- **Beat 3 (Hack 2):** Hack terminal B — same as A but with drone adds. Teaches: aerial threats.
- **Beat 4 (Sabotage core + escape):** Plant charge on neural core → 90s escape timer → sprint back through reversed corridor with spawning enemies → extraction.
- **Boss:** None. Mid-boss = Shieldbearer heavy at Hack 2.
- **Rewards:** +3000 CR, +2500 XP, AX-9 SR (sniper) unlocked, Mission 3 unlocked.

#### Mission 3 — VOID LOCK (Survival/waves)
- **Theme:** Orbital station, endless-wave survival for fixed time.
- **Beat 1 (Setup):** Player enters station → first 30s no enemies → loot 2 supply caches (ammo + armor refill). Teaches: exploration.
- **Beat 2 (Survival wave 1–3):** 3 escalating waves over 4 minutes. Wave 1 = 8 grunts; Wave 2 = 6 riflemen + 2 drones; Wave 3 = 4 shotgunners + 2 heavies + 1 suicide bomber. Teaches: ammo management, weapon switching.
- **Beat 3 (Mid-wave supply drop):** At 2:00 mark, supply drop lands → 15s window to grab ammo + heal before Wave 4.
- **Beat 4 (Final wave + boss intro):** Wave 4 = mini-boss (Shieldbearer champion variant) + 6 riflemen. Kill mini-boss → extraction unlocks.
- **Boss:** None full, but mini-boss Shieldbearer Champion.
- **Rewards:** +3500 CR, +3000 XP, GUARD V-9 (secondary pistol) unlocked, CYBER TRAP F-2 (tactical) unlocked, Mission 4 unlocked.

#### Mission 4 — IRON HARVEST (Assassination, boss mission)
- **Theme:** Drone factory. Boss: TITAN (heavy mech).
- **Beat 1 (Factory approach):** Outdoor/indoor hybrid. 2 drone squads (3 drones each) + 1 heavy sentinel.
- **Beat 2 (Assembly line):** Moving conveyor set-piece. Player must traverse while 4 riflemen + 2 drones attack. Time-pressure platforming.
- **Beat 3 (Boss arena approach):** Final corridor with 2 Shieldbearers + 3 shotgunners → enter arena.
- **Beat 4 (Boss — TITAN, 3 phases):**
  - Phase 1 (100–66%): TITAN uses minigun sweep + missile pod. Weakspot = back power core.
  - Phase 2 (66–33%): TITAN summons 3 drone adds every 20s. New attack: charge-stomp AOE.
  - Phase 3 (33–0%): TITAN enrages. All prior attacks + new laser-sweep (telegraphed 1.5s). Arena floor hazards (electrified panels).
  - Phase transitions: 1.0s cinematic + roar + screen shake.
- **Boss:** TITAN (3-phase heavy mech).
- **Rewards:** +6000 CR, +5000 XP, HEAVY RX-14 unlocked, TITAN M-8 (tactical) unlocked, Mission 5 unlocked.

#### Mission 5 — DEEP SIGNAL (Recon/stealth)
- **Theme:** Derelict wreck. Stealth-forward.
- **Beat 1 (Infiltrate, stealth):** Enter wreck via airlock. No enemies alerted initially. 4 patrolling drones + 2 riflemen on fixed patrol routes. Stealth-kill (melee from behind) or sneak past. Alarm raises if seen.
- **Beat 2 (Recover data):** Reach data terminal → 30s hack → during hack, 1 wave of 3 drones spawns (alerted).
- **Beat 3 (Set charges):** Plant 3 charges in 3 rooms. Each room has 1–2 enemies. Optional stealth; combat triggers if caught.
- **Beat 4 (Extraction + alarm triggered):** Charges set → 90s escape timer → full alert → all remaining enemies converge → sprint to extraction point.
- **Boss:** None. Stealth set-pieces + scripted encounters.
- **Rewards:** +4000 CR, +3500 XP, CORE P-4 (secondary) unlocked, SUPERNOVA (tactical) unlocked, Mission 6 unlocked. Optional stealth bonus: +1500 CR if no alarm raised.

#### Mission 6 — BLACK ECHO (Defense)
- **Theme:** Carrier ship. Defend a critical console/asset against horde.
- **Beat 1 (Pre-defense setup):** Player has 60s to place 2 automated turrets (pick from supply cache) + choose firing position. Map shows 3 entry points to defend.
- **Beat 2 (Defense wave 1–3):** 3 waves over 5 minutes. Wave 1 = 10 grunts; Wave 2 = 8 riflemen + 4 shotgunners; Wave 3 = 6 riflemen + 3 heavies + 2 drones + 1 Shieldbearer.
- **Beat 3 (Mid-defense breach):** One entry point is breached → player must relocate to secondary console (cutscene prompt) → reset defense.
- **Beat 4 (Final wave + boss intro):** Wave 4 = mini-boss (Drone Commander) + 4 drones. Kill → extraction.
- **Boss:** None full, but Drone Commander mini-boss.
- **Rewards:** +4500 CR, +4000 XP, CX-27 ATLAS unlocked, ION X-S (secondary) unlocked, Mission 7 unlocked.

#### Mission 7 — FINAL VECTOR (Boss finale, multi-phase)
- **Theme:** Enemy flagship. Final confrontation with fleet admiral / flagship AI.
- **Beat 1 (Boarding):** Breach flagship airlock → corridor with mixed enemies (3 riflemen + 2 shotgunners + 1 drone).
- **Beat 2 (Bridge approach):** 2 rooms. Room 1 = 2 Shieldbearers + 4 riflemen. Room 2 = 2 heavies + 2 drones + 1 sniper perch.
- **Beat 3 (Bridge — Boss Phase 1):** Boss = ADMIRAL NYX (cyborg commander) + flagship defense system.
  - Phase 1 (100–66%): NYX uses rapid-fire pistol + 2 escort Shieldbearers. Arena: bridge with cover pillars. Damage gate at 66% — NYX retreats, defense system activates.
  - Phase 2 (66–33%): Defense system = ceiling turret + floor lasers. NYX returns with melee dash attack. Player must damage NYX while dodging turret.
  - Phase 3 (33–0%): NYX merges with flagship AI — becomes mega-form. All prior attacks + AOE energy blast (telegraphed 2s). Adds: 3 drones every 25s.
- **Beat 4 (Escape + extraction):** NYX defeated → flagship self-destruct sequence → 120s escape through reversed corridor with collapsing geometry + spawning enemies → reach escape pod → victory.
- **Boss:** ADMIRAL NYX (3-phase, with escort + environment mechanics).
- **Rewards:** +10000 CR, +8000 XP, MAGNUM E-2 (secondary) unlocked, XEN agent unlocked (if level ≥ 55), credits + ending cutscene.

### 3.4 HUD Elements to Add (Beyond Current Worklog Spec)

The K5 worklog already specifies: minimap top-right (collapsible), ammo "20 | 60" + "RIFLE" + "12:39 TIME LEFT", Health 5000 + Armor bars. Below are **additional HUD elements** to add, based on synthesized best practices:

| Element | Position | When shown | Implementation |
|---|---|---|---|
| **Compass strip** | Top-center, 1-line, 60% width | Always in mission | Horizontal compass with N/NE/E/SE/S/SW/W/NW + objective marker direction |
| **Damage direction indicators** | Screen edge arcs | When player takes damage from off-screen | 3 red arcs at screen edge pointing to damage source, fade over 1.5s |
| **Hit markers** | Center (reticle) | On hit | 4-prong, cyan #1AA1CE for body, #FFE735 yellow for crit, #FE0022 red for kill. Audio "ting" on crit. |
| **Damage numbers (optional)** | Above hit enemy | When damage dealt | Toggleable in settings. Cyan/yellow/red per hit type. Float up 0.8s, fade. |
| **Boss HP bar** | Top-center, full width minus margins | During boss phase | Cyan #1AA1CE fill, with phase ticks (33% / 66%) marked. Boss name + portrait left of bar. |
| **Objective tracker** | Top-left, under minimap (collapsible) | Always in mission | "» OBJECTIVE: <text>" with distance + direction arrow. Fades 4s after update unless pinned. |
| **Enemy engaged indicator** | Top-right, under minimap | When ≥1 enemy alerted | "⚠ ENGAGED x N" counter, fades when no alerted enemies. |
| **Ability cooldown radials** | Bottom-right, around ability button | Always in mission | Radial sweep on cooldown, percentage text under icon. |
| **Ultimate charge bar** | Bottom-center, thin | Always in mission | Fills as player deals damage. Glows when full. |
| **Weapon swap wheel** | Center, transient | When weapon-switch held | 0.3s radial wheel with primary/secondary icons; release to swap. |
| **Grenade arc indicator** | Transient, follows reticle | When grenade button held | Predicted arc line + landing circle. |
| **Loot pickup prompt** | Center-bottom, above fire button | When near loot | "Hold [INTERACT] to pick up <item>" prompt. |
| **Checkpoint indicator** | Top-center, transient | On checkpoint | "CHECKPOINT" + checkpoint name, fades 2s. |
| **Low-ammo warning** | Bottom-right, around ammo | When magazine ≤ 25% | Pulsing red #FE0022 outline. |
| **Low-HP vignette** | Screen edge | When HP ≤ 25% | Red radial vignette pulsing with heartbeat audio. |
| **Subtitles** | Bottom-center, above fire button | During dialogue | Cyan-bordered text, Audiowide for speaker name, Rajdhani for dialogue. |

### 3.5 Movement & Combat Mechanics — Final Spec

#### Movement
- **Walk:** joystick deflection 0–0.7 → walk at agent.baseMoveSpeed × 0.5.
- **Sprint:** joystick deflection 0.7–1.0 → sprint at agent.baseMoveSpeed × 1.0; consumes stamina (optional — toggle in settings; if off, infinite sprint). FOV widens +5° during sprint.
- **Crouch:** tap crouch button → crouch at 0.5× speed, reduced hitbox, improved accuracy. Hold to prone (mission-dependent).
- **Jump:** tap jump button → standard jump. Mantle auto-triggers when running into knee-high cover.
- **Slide:** crouch while sprinting → slide 1.5m, then crouch. Slide preserves momentum for 0.8s.
- **Dodge:** agent-ability button (XEN's signature). Directional dash on 3s cooldown, with i-frames for 0.3s.
- **Cover snap (mission-dependent):** in DEEP SIGNAL + BLACK ECHO, walking into low cover auto-snaps to cover. Tap fire to lean out. (Shadowgun: DeadZone pattern.)
- **Vault:** auto when running at waist-high obstacles; can be disabled per-mission.

#### Combat
- **Auto-fire:** ON by default. When reticle crosses enemy, weapon fires automatically. Smart mode: only fires on enemies in reticle (not friendlies/civilians).
- **Aim assist:** rotation (camera nudges toward nearest enemy in reticle bubble) + deceleration (camera slows when crossing enemy). Configurable: Off / Soft / Strong. Default: Soft.
- **Recoil:** per-weapon recoil pattern (defined in WeaponSO). Recoil recovery curve: 60% recovery in 0.2s, 100% in 0.5s. Recoil-smoothing during slide (Apex pattern).
- **Reload:** tap reload → reload animation (weapon.reloadTime). Reload-cancel: swap weapon during reload → reload completes instantly when swapping back (Destiny pattern).
- **Hit markers:** 4-prong reticle expansion on hit. Color: cyan body, #FFE735 crit, #FE0022 kill. Audio: soft tick on hit, "ting" on crit, "thunk" on kill.
- **Damage numbers:** toggleable. Float up from hit point, color-coded. Critical hits bigger font.
- **Headshot multiplier:** 1.5× damage. Legshot: 0.8×. Armshot: 1.0×.
- **Elemental system (K5's ElementalResolver.cs already exists):** matching element to enemy weakness = 1.5× damage. Mismatch = 0.75×.
- **Hitstop:** on heavy hits (grenade, shotgun point-blank, melee), `TimeManager.Hitstop(0.04s)` for impact feel.
- **Screen shake:** on grenade explosion (amplitude 1.0, 0.4s), heavy weapon fire (0.2, 0.1s per shot), boss attacks (0.6, 0.6s). `CameraManager.Shake()`.
- **Combo chain (K5's ComboChain.cs already exists):** consecutive kills within 3s window increment combo counter. Combo bonuses: 2× = +10% damage, 3× = +20%, 5× = +30%, 10× = +50% + special VFX. Combo counter HUD element top-center.
- **Discharge system (K5's DischargeSystem.cs already exists):** ultimate ability, charged by dealing damage. When full, tap ultimate button to discharge. Per-agent discharge: VULCAN = AOE shockwave; XEN = time-slow + damage aura; JOLT = team heal + speed boost; XANO = recon pulse revealing all enemies.

---

## 4. Implementation Notes for K5 Unity Code

Mapping the recommendations to K5's existing Unity files (from project tree):

| K5 File | Recommended Action |
|---|---|
| `InputManager.cs` | Add 3 preset profiles (2/3/4-finger) + per-button opacity/size settings + gyro toggle. Already has floating joystick + swipe + haptics — extend with preset loader. |
| `PlayerController.cs` | Add slide, dodge (per-agent), cover-snap (mission-flag), mantle auto-trigger. |
| `PlayerCombat.cs` | Wire hit markers (4-prong reticle expansion) + damage numbers (optional) + elemental multiplier (already in ElementalResolver). |
| `EnemyAI.cs` | Extend FSM with Squad, Flank, Retreat-to-heal states. Add `EnemyAlertedEvent` broadcast via GameEventBus. |
| `EnemyController.cs` | Add cover-point query system (pre-placed CoverPoint MonoBehaviours). |
| `EnemySpawner.cs` | Add wave-definition ScriptableObject (WaveSO with enemyList + spawnDelay + spawnPoint). Used by MissionDirector. |
| `BossController.cs` + `BossPhaseManager.cs` | Implement 3-phase pattern with damage gates (33%, 66%). Phase transition = invuln + cinematic + roar + shake. |
| `MissionDirector.cs` | Add 4-beat structure (Infiltrate → Arena → Mid-boss → Boss/Extraction) per mission. Wire ObjectiveTracker. |
| `ObjectiveTracker.cs` | Support multi-objective (primary + secondary). Distance + direction to next objective. |
| `ExtractionZone.cs` | 60s hold-out timer + spawning enemies during extraction. |
| `HUDController.cs` | Add 15 HUD elements from §3.4 table. Boss HP bar, compass, damage direction, hit markers, etc. |
| `PlayerHUD.cs` | Per-player HUD instance (HP, armor, ammo, ability cooldowns). |
| `FloatingDamage.cs` | Implement damage number floating system with color-coding. |
| `HitstopController.cs` | Already exists — wire to heavy hits, grenade, melee, boss attacks. |
| `ScreenShake.cs` | Already exists — wire per-event amplitudes. |
| `ComboChain.cs` | Already exists — wire to HUD combo counter top-center. |
| `DischargeSystem.cs` | Already exists — wire to ultimate button + per-agent discharge behavior. |
| `WeaponSO.cs` | Add recoilPattern (Vector2 curve), reloadCancelAllowed (bool), headshotMultiplier (float, default 1.5). |
| `EnemySO.cs` | Add archetype enum (Grunt, Rifleman, Heavy, Sniper, Shotgunner, SuicideBomber, Shieldbearer, Drone, Boss), coverUsagePriority, flankPriority, retreatHPPct. |
| `MissionSO.cs` | Add 4-beat structure (Beats array), difficultyModifiers, bossRef, extractionTimer. |
| `AgentSO.cs` | Add passiveAbility, tacticalAbility, ultimate (Discharge). |
| `ProgressionCurveSO.cs` | Already exists — wire XP curve + agent unlock levels. |

---

## 5. Sources (Web Search Index)

The following sources were retrieved via `z-ai web_search` and informed this report. (URLs condensed for readability.)

- **Destiny 2 systems**: grindout.com/destiny-2/guides/beginners-guide; greenmangaming.com Destiny 2 Beginner's Guide; reddit.com/r/destiny2 New Player's Guide; digitaltrends.com Destiny 2 beginner's guide; shacknews.com All Strikes and Nightfalls; destinypedia.com Public Event; screenrant.com Destiny 2 Nightfall Missions Guide; reddit.com/r/DestinyTheGame The Destiny Raid Formula; gamedesignskills.com Boss Design.
- **Snowbreak**: play.google.com Snowbreak: Containment Zone; store.steampowered.com Snowbreak; snowbreak.gg tier-list; snowbreak.fandom.com/wiki/Weapons; thegamer.com Snowbreak combat guide; steamcommunity.com Builds for every Snowbreak character.
- **CoD Mobile**: callofduty.com Warzone Mobile controls blog; reddit.com/r/CallOfDutyMobile Controller vs Touch; reddit.com/r/CallOfDutyMobile Best codm layout (4-finger claw); activision blog Getting a Grip on COD Mobile Controls; youtube.com Best COD Mobile HUD & Finger Setup (2024–2026).
- **Shadowgun**: en.wikipedia.org/wiki/Shadowgun; mmos.com/review/shadowgun-deadzone; tvtropes.org Shadowgun; pocketgamer.com Shadowgun DeadZone hands-on; steemit.com Shadowgun Deadzone review.
- **N.O.V.A. Legacy**: nova.fandom.com N.O.V.A. Legacy; gameloft.com NOVA Legacy in One Minute; softonic.com N.O.V.A. Legacy APK; youtube.com NOVA Legacy walkthroughs.
- **Dead Effect 2**: grokipedia.com Dead Effect 2; steamcommunity.com Dead Effect 2 weapons lists; reddit.com/r/oculus Dead Effect 2 weapons and upgrades.
- **Modern Combat 5**: youtube.com MC5 Controls/Sensitivity Tips; youtube.com How to Turn On Gyroscope in MC5; youtube.com How to Improve Your Aim in MC5; xdaforums.com MC5 MOGA Controller Fix; steamcommunity.com Gyro and Flick Stick Layout.
- **Apex Legends Mobile**: apexlegends.fandom.com Rhapsody; pocketgamer.com Apex Legends Mobile Rhapsody guide; inverse.com Apex Legends Mobile Fade abilities; ginx.tv Apex Legends Mobile Fade abilities; ea.com Apex Legends abilities guide.
- **Mobile FPS best practices**: designmonks.co Perfect Mobile Button Size; linkedin.com Touch Target Sizing Guidelines; capiproduct.com Touch Targets Best Practices; smashingmagazine.com Accessible Tap Target Sizes; nngroup.com Touch Targets on Touchscreens; mobilefreetoplay.com Touch Control Design; uxmovement.com Optimal Size and Spacing for Mobile Buttons; docs.neofps.com Touchscreen Input.
- **Enemy AI / FSM**: youtube.com Advanced FPS Enemy AI with Patrol Chase Attack States; youtube.com Patrolling Let's Make a FPS in Unity; youtube.com Shooting/Follow/Retreat Enemy AI Unity; reddit.com/r/Unity3D Tactical Cover & Retreat AI System; medium.com How to Build a Simple AI Enemy in Unity; github.com Intelligent Opponents Unity FSM; discussions.unity.com Released Enemy AI; pavcreations.com FSM for AI Enemy Controller; youtube.com GOAP Enemy AI Unity; tonogameconsultants.com Game AI Planning; reddit.com/r/gamedev Behavior Trees for FPS enemy AI.
- **HUD design**: playcaliber.com/en/news/638 About our approach to HUD design; pluralsight.com Designing a HUD That Works; reddit.com/r/Battalion1944 minimap and killfeed.
- **Unity mobile FPS assets**: assetstore.unity.com Joystick Pack (Fenerax); assetstore.unity.com FPS Controls for mobile devices; assetstore.unity.com Touch Control for Mobile; docs.neofps.com Touchscreen Input; discussions.unity.com Mobile Touchpad using New Input System; discussions.unity.com Android FPS Controller; discussions.unity.com Easy Mobile Joystick Controller.
- **Boss design**: gamedesignskills.com Boss Design How to Make an Unforgettable Boss Battle; reddit.com/r/DestinyTheGame The Destiny Raid Formula.

---

## 6. Conclusion — Top 10 Actions for K5

1. **Implement 4-finger claw preset** in `InputManager.cs` (positions/sizes per §3.1) on top of the existing 2-finger default.
2. **Extend `EnemyAI.cs`** with squad-alert broadcast (`EnemyAlertedEvent` via GameEventBus) + Flank + Retreat-to-heal states.
3. **Add `CoverPoint` MonoBehaviour** + cover-query system for Rifleman/Heavy archetypes.
4. **Implement 3-phase boss pattern** in `BossPhaseManager.cs` with damage gates (33% / 66%) + cinematic transitions using `TimeManager.Hitstop()` + `CameraManager.Shake()`.
5. **Add 9 enemy archetypes** (Grunt, Rifleman, Heavy, Sniper, Shotgunner, SuicideBomber, Shieldbearer, Drone, Boss) as `EnemySO` presets, each with tuned FSM parameters.
6. **Wire 15 new HUD elements** (§3.4) into `HUDController.cs` + `PlayerHUD.cs`: compass, damage direction, hit markers, boss HP bar, objective tracker, etc.
7. **Define 7-mission structure** (§3.3) as `MissionSO` assets with 4-beat beats + boss refs + extraction timers. Per-mission WaveSO assets for spawning.
8. **Add agent abilities** (passive + tactical + ultimate/Discharge) to `AgentSO.cs`: VULCAN = shield/barricade, XEN = dash, JOLT = team heal/speed, XANO = recon pulse.
9. **Implement movement verbs**: slide (crouch while sprinting), dodge (per-agent ability), cover-snap (mission-flag for DEEP SIGNAL + BLACK ECHO), mantle (auto on waist-high obstacles).
10. **Difficulty modifiers** (Easy/Normal/Hard from Settings) applied via `DifficultyManager.cs` (already exists): HP, damage, reaction time, accuracy, aggression multipliers per §3.2 table.

These 10 actions, layered on top of K5's existing 90+ C# files and ScriptableObjects, bring the game to feature parity with the synthesized best practices from the 8 studied titles while preserving K5's distinct cyan/Audiowide visual identity and ship-interior sci-fi setting.

---

*End of report — 5,200+ words.*
