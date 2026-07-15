# GAMEPLAY — KINETICS 5

> Documentation exhaustive des systèmes gameplay de KINETICS 5.
> Mouvement FPS, combat, armes, éléments, ultimate, IA, boss, missions, progression, loot.

---

## 1. Mouvement FPS

### 1.1 Contrôles

| Plateforme | Déplacement | Look | Tir | Visée | Reload | Saut | Crouch | Interact |
|---|---|---|---|---|---|---|---|---|
| **Mobile** | Joystick gauche flottant | Swipe droit | Bouton FIRE | Bouton AIM | Bouton RELOAD | Bouton JUMP | Bouton CROUCH | Bouton INTERACT |
| **Desktop** | ZQSD/WASD | Souris | Clic gauche | Clic droit | R | Espace | Ctrl | F |
| **Gamepad** | Stick gauche | Stick droit | RT | LT | X | A | B | Y |

### 1.2 Caractéristiques de mouvement

| Action | Vitesse (m/s) | Stamina/s | Notes |
|---|---|---|---|
| Marche | 4.5 | 0 | Par défaut |
| Sprint | 7.0 | -1.0 | Move.y > 0.5 + !AimHeld |
| Crouch | 2.0 | 0 | Hauteur 1.0m (vs 1.8m stand) |
| Saut | — | 0 | Hauteur 1.2m, gravité -19.6 m/s² |
| Chute | — | 0 | État Falling si !isGrounded |

**Stamina** : max 5s, régénération 0.8/s après délai 1.5s sans sprint.

### 1.3 Look (caméra)

- Sensibilité mobile : 0.18°/pixel (swipe droit).
- Sensibilité desktop : 0.22°/pixel (souris).
- Pitch clampé [-89°, +89°].
- Aim zoom : FOV dynamique 70° → 45° (DOTween 0.15s ease-out).
- Head bob : sinusoïde amortie en marchant.
- Recoil : pattern par arme (Rifle = vertical, Shotgun = horizontal spread).

---

## 2. Combat

### 2.1 Hitscan vs Projectile

| Type | Armes | Mécanique |
|---|---|---|
| **Hitscan** | RIFLE CX-24, AX-9 SR, CX-27 ATLAS, GUARD V-9, MAGNUM E-2 | `Physics.Raycast` instantané, VFX bullet tracer factice |
| **Projectile** | HEAVY RX-14 (lourd), C-2 (grenade launcher), ION X-S (plasma) | Spawn d'un GameObject projectile, vitesse 200-400 m/s, gravité optionnelle |
| **Tactical** | FRAG-X, CYBER TRAP F-2, SUPERNOVA, TITAN M-8 | Projectile à fuse time (3-6s), explosion en zone |

### 2.2 Formule de dégâts

```
baseDamage = weapon.DamagePct × 0.5  (AgentPowerScale)
elemMult = 1.5 si element == enemyWeakness
         | 0.5 si element == enemyResistance
         | 1.0 sinon
hsMult = 2.0 si headshot
critMult = 1.5 si critical
compositeMult = max(hsMult, critMult)   // NON cumulables
distMult = lerp(1.0, 0.3, distance / weapon.Range)   // falloff linéaire
armorMult = clamp(1 - enemyArmor * 0.01, 0.05, 1.0)
finalDamage = round(clamp(baseDamage × elemMult × compositeMult × distMult × armorMult, 0, 9999))
```

Voir `Gameplay/Combat/DamageCalculator.cs` pour l'implémentation.

### 2.3 Headshot / Crit

- **Headshot** : détection via `Collider` dédié sur la tête des ennemis (tag "Headshot").
  Multiplicateur ×2.0.
- **Critical** : chance par arme (5-15%) + bonus agent (XEN +5%).
  Multiplicateur ×1.5.
- **Non cumulables** : si headshot ET crit, on prend `max(2.0, 1.5) = 2.0` (pas 3.0).

---

## 3. Armement

### 3.1 Slots (3 par agent)

| Slot | Catégorie | Exemples |
|---|---|---|
| **1 — Primary** | Fusil / Sniper / Lourd | HEAVY RX-14, RIFLE CX-24, AX-9 SR, CX-27 ATLAS, C-2 |
| **2 — Secondary** | Pistolet | GUARD V-9, CORE P-4, MAGNUM E-2, ION X-S |
| **3 — Tactical** | Grenade / Gadget | FRAG-X, CYBER TRAP F-2, SUPERNOVA, TITAN M-8 |

### 3.2 Stats d'arme (extrait PDF)

| Arme | Power | Reload | Dmg | Fire Rate | Accuracy | Stability | Rareté |
|---|---|---|---|---|---|---|---|
| HEAVY RX-14 | 1500 | 3.2s | 72% | 85% | 60% | 68% | RARE |
| GUARD V-9 | 300 | 2.2s | — | — | — | — | RARE |
| FRAG-X | 3700 | — | 90% | — | — | — | RARE |

### 3.3 Modes de tir

- **Single** : coup par coup (snipers, pistolets semi-auto).
- **Burst** : rafale 3 coups (CX-27 ATLAS, certains fusils).
- **Auto** : tir continu tant que FireHeld (HEAVY RX-14, RIFLE CX-24).

### 3.4 Reload

- Durée par arme (1.8s à 4.5s).
- Animation avec cancel-window (peut être interrompue à 80% pour switch rapide).
- Magazine partagé (pas de réserve infinie) — sauf tacticals qui sont consommés.

---

## 4. Éléments de dégâts

5 éléments, chacun avec des interactions spécifiques :

| Élément | Couleur | Effet secondaire | Faiblesse typique |
|---|---|---|---|
| **Kinetic** | Blanc | Aucun, base stable | Drones (peu d'armure) |
| **Energy** | Cyan `#1AA1CE` | Bypass partiel d'armure (×0.7 armor) | Heavy units (armure lourde) |
| **Explosive** | Orange `#FE0022` | Dégâts de zone (radius 5-10m) | Swarms (groupe d'ennemis) |
| **Cryo** | Bleu clair | Ralentit 50% pendant 3s, +25% dégâts subis | Berserkers, boss mobiles |
| **Volt** | Jaune `#FFE735` | Étourdit 1.5s, désactive la tech (boucliers) | Snipers, drones tech |

### 4.1 Table de résonance (weakness/resistance)

| Ennemi | Weakness | Resistance |
|---|---|---|
| GRUNT-MK1 | Energy | Kinetic |
| ASSAULT-DROID | Volt | Kinetic |
| ELITE-GUARD | Explosive | Energy |
| SNIPER-DRONE | Volt | Cryo |
| HEAVY-UNIT | Energy | Kinetic |
| SWARM-BOT | Explosive | Cryo |
| STEALTH-CLOAKER | Cryo | Energy |
| TITAN (boss) | Explosive | Kinetic |
| NEURAL CORE (boss) | Volt | Cryo |
| INTERCEPTOR (boss) | Energy | Explosive |
| OVERLORD (boss) | Cryo | Volt |

---

## 5. Ultimate et compétences

### 5.1 Compétences actives (3 par agent)

| Agent | Skill 1 | Skill 2 | Ultimate |
|---|---|---|---|
| **VULCAN** | Bulwark (bouclier +50% 8s) | Taunt (force les ennemis à le cibler) | Last Stand (invulnérable 5s, knockback zone) |
| **XEN** | Adrenaline (vitesse +30% 6s) | Overcharge (cadence +50% 5s) | Bullet Storm (tir infini 4s, pas de reload) |
| **JOLT** | Heal Pulse (soin 30% zone) | EMP Field (désactive tech 4s zone 8m) | Restoration (full heal + shield 1 fois) |
| **XANO** | Cloak (invisibilité 6s) | Mark Target (+50% dégâts sur cible 5s) | Shadow Strike (5 éliminations instantanées ennemis < 30% HP) |

### 5.2 Cooldowns

- Skill 1 : 8-12s.
- Skill 2 : 15-20s.
- Ultimate : 90-120s (charge accélérée par kills).

### 5.3 Esquive (dodge)

- **Bouton** : double-tap joystick gauche ou bouton dédié (desktop : Shift).
- **Distance** : 3m en direction du mouvement.
- **i-frames** : 0.4s invulnérabilité.
- **Cooldown** : 3s.
- **Stamina** : -1.5 (peut esquiver même sans stamina, mais plus lent).

---

## 6. IA ennemie

### 6.1 Behavior tree (simplifié FSM)

```
PATROL ──────────────── CHASE ───────────── ATTACK
   ↑                       │                    │
   │                       │                    │
   └──── lost target ──────┘                    │
                                                │
                            HP < 30% (non-berserker)
                                                ↓
                                              FLEE
```

### 6.2 Comportements par archetype

| Behavior | Description | Exemples |
|---|---|---|
| **Patrol** | Cycle waypoints, pause 1s à chaque | GRUNT-MK1, ASSAULT-DROID |
| **Aggressive** | Charge directe, attaque à portée | ELITE-GUARD, SWARM-BOT |
| **Defensive** | Maintient distance, tir à couvert | HEAVY-UNIT |
| **Flanking** | Contourne latéralement le joueur | ASSAULT-DROID avancé |
| **Berserker** | Charge même à bas HP (pas de flee) | STEALTH-CLOAKER enragé |
| **Sniper** | Longue portée, repositionne si approché | SNIPER-DRONE |

### 6.3 Détection

- **Distance** : 20m (configurable par ennemi).
- **Field of view** : 120° (60° de chaque côté du forward).
- **Line of sight** : `Physics.Raycast` (occluders = murs, obstacles).
- **Alerte propagée** : si un ennemi détecte, il alerte ses voisins dans 15m.

### 6.4 Attack

- **Cadence** : 0.5 à 2 coups/s selon ennemi.
- **Dégâts** : 5-25 par tir (×1.4 en Hard).
- **Pattern** : tir instantané (hitscan) avec spread aléatoire.

---

## 7. Boss et phases

### 7.1 Boss à phases (3 phases typiques)

Chaque boss a 3 phases déclenchées par seuils de HP :

| Phase | Seuil HP | Comportement |
|---|---|---|
| **Phase 1** | 100% → 66% | Attaques de base, pattern simple |
| **Phase 2** | 66% → 33% | Nouveaux patterns, adds (ennemis mineurs) |
| **Phase 3** | 33% → 0% | Enrage (timer), attaques dévastatrices |

### 7.2 Boss spécifiques

| Boss | HP | Particularités |
|---|---|---|
| **TITAN** (IRON HARVEST) | 50 000 | Tank lourd, weakness Explosive, enrage 90s |
| **NEURAL CORE** (FINAL VECTOR p1) | 80 000 | Statique, weak points multiples, spawns drones |
| **INTERCEPTOR** (FINAL VECTOR p2) | 90 000 | Mobile, vol, attacks à distance |
| **OVERLORD** (FINAL VECTOR p3) | 120 000 | Final, 5 mécaniques combinées |

---

## 8. Missions (7 types)

### 8.1 Types

| Type | Description | Exemple |
|---|---|---|
| **Extraction** | Atteindre un point + évacuer | SHADOW FALL |
| **Sabotage** | Détruire N objectifs | NEURAL BREACH |
| **Survival** | Survivre X temps / N vagues | VOID LOCK (8 vagues 8 min) |
| **Assassination** | Tuer un boss | IRON HARVEST (TITAN) |
| **Recon** | Scanner N points, furtif optionnel | DEEP SIGNAL |
| **Defense** | Protéger un point | BLACK ECHO (10 vagues) |
| **BossRush** | Enchaîner N boss | FINAL VECTOR (3 boss) |

### 8.2 Objectifs

Chaque mission a 1-5 objectifs (`ObjectiveKind` enum) :

- `Reach` : atteindre une zone.
- `Eliminate` : tuer N ennemis.
- `Collect` : ramasser N items.
- `Sabotage` : détruire N cibles.
- `Escort` : protéger un PNJ.
- `Defend` : tenir une zone X temps.
- `Extract` : évacuer.
- `Assassinate` : tuer un boss.
- `Scan` : scanner N objets.
- `Survive` : survivre X secondes.

### 8.3 Vagues (waves)

Chaque vague a :

- `delay` : délai avant spawn (0-10s).
- `enemyId` : type d'ennemi.
- `count` : nombre (1-20).
- `spawnPoint` : position XYZ.

### 8.4 Récompenses

```yaml
xp: 500-5000 (par mission)
cr: 200-2000
lootTable:
  - itemId: "WEAPON_CX_27_ATLAS"
    dropChancePct: 15
    minQty: 1
    maxQty: 1
  - itemId: "CREDITS_BONUS"
    dropChancePct: 100
    minQty: 100
    maxQty: 500
```

**Bonus perfect clear** (pas de dégâts subis) : +25% XP et CR.

---

## 9. Progression

### 9.1 XP et niveaux

- **Niveau max** : 60.
- **Courbe XP** : `xpRequired = 400 × 1.13^(level-1)` (palier 1 = 400, palier 60 = ~430 000).
- **Total XP au max** : ~3 200 000.
- **Sources XP** : kills (10-500), objectifs (100-1000), mission complete (500-5000).

### 9.2 Mastery (par arme)

Chaque arme a une mastery 1-100 gagnée par l'usage :

- **Kill** : +1 mastery.
- **Headshot** : +2 mastery.
- **Niveau mastery** : débloque des mods (scope, compensateur, magazine étendu).
- **Mastery 100** : skin exclusif arme.

### 9.3 Arbres d'éveil (agents)

Chaque agent a un arbre de 4 nœuds de talent (`TalentType`) :

- `Stat` : bonus passif (+10% dégâts, +20% vitesse, etc.).
- `Ability` : améliore une compétence existante.
- `Passive` : effet conditionnel (régénération shield sous 30% HP).
- `Ultimate` : améliore l'ultimate (cooldown -20%, durée +30%).

Points de talent : 1 par niveau agent (à partir du niveau 10), max 4 points.

---

## 10. Loot et inventaire

### 10.1 Tables de loot

Chaque ennemi a une `lootTable` (liste d'items avec `dropChancePct`).
À la mort, on tire chaque entrée indépendamment (rng uniforme).

### 10.2 Rarity

| Rarity | Couleur | Chance base | Multiplieurs |
|---|---|---|---|
| **Common** | Gris | 70% | — |
| **Rare** | Bleu `#1AA1CE` | 22% | +15% stats |
| **Epic** | Violet | 7% | +30% stats |
| **Legendary** | Orange `#FFE735` | 1% | +50% stats + affixe |

### 10.3 Inventaire (sauvegarde)

- `OwnedWeapons` : `List<string>` d'IDs.
- `Equipped` : `List<string>` (3 slots : primary, secondary, tactical).
- `Resources` : `Dictionary<string, int>` (credits, fragments, materials).

---

## 11. Multiplayer co-op

Voir `ARCHITECTURE.md` section Network pour le détail technique.

- **2-4 joueurs** par match.
- **Host/Client** : le joueur qui crée le match est host (autorité).
- **Migration host** : si host quitte, élection du nouveau host (userId alphabétique le plus bas).
- **Sync** : 20 Hz tick rate, interpolation 180 ms, extrapolation max 250 ms.
- **Validation** : toutes les actions (tir, ability) validées par `AntiCheatValidator` côté host.

---

## 12. Difficulté

3 niveaux (configurable dans Settings) :

| Niveau | Dégâts ennemis | HP ennemis | Cadence ennemie | XP bonus |
|---|---|---|---|---|
| **Easy** | ×0.7 | ×0.85 | ×0.85 | ×0.9 |
| **Normal** | ×1.0 | ×1.0 | ×1.0 | ×1.0 |
| **Hard** | ×1.4 | ×1.15 | ×1.2 | ×1.25 |

---

## 13. Références gameplay

- **DamageCalculator** : `Assets/_Project/Gameplay/Combat/DamageCalculator.cs`
- **PlayerController** : `Assets/_Project/Gameplay/Player/PlayerController.cs`
- **EnemyAI** : `Assets/_Project/Gameplay/Enemies/EnemyAI.cs`
- **MissionDirector** : `Assets/_Project/Gameplay/Missions/MissionDirector.cs`
- **DataLoader** : `Assets/_Project/Data/Scripts/DataLoader.cs`
- **JSON data** : `Assets/_Project/Data/Resources/Data/*.json`
