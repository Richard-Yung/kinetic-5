# SÉCURITÉ — KINETICS 5

> Documentation sécurité : anti-cheat, sauvegarde chiffrée, GDPR/analytics,
> rate limiting, input validation, secure networking (TLS).

---

## 1. Vue d'ensemble

La sécurité KINETICS 5 s'articule autour de 6 axes :

1. **Anti-cheat applicatif** : validation serveur des actions joueurs.
2. **Sauvegarde chiffrée** : AES-128-CBC pour les données sensibles.
3. **RGPD / GDPR** : consentement analytics, droit à l'effacement.
4. **Rate limiting** : protection anti-spam (chat, API).
5. **Input validation** : sanitization HTML, regex, bornes numériques.
6. **Secure networking** : TLS 1.3, certificats épinglés (pinning).

> ⚠️ **Avertissement** : cette documentation décrit une **couche applicative** de
> sécurité. Un anti-cheat complet nécessite également des mesures binaire
> (obfuscation, EAC/BattlEye, integrity check) qui ne sont PAS couvertes ici.

---

## 2. Anti-cheat

### 2.1 Architecture

```
Client (joueur)                 Serveur Nakama (host autoritaire)
─────────────                   ──────────────────────────────────
PlayerAction ────────────────► AntiCheatValidator.ValidatePlayerAction
   (tir, ability, etc.)              │
                                     ├── damage within bounds?
                                     ├── fire rate within cap?
                                     ├── speed within max?
                                     └── headshot rate < threshold?
                                            │
                                     ◄──── ActionValidationResult
                                     si OK → apply damage + broadcast
                                     si KO → log + telemetry + strike
                                              3 strikes → ban temp 7j
                                              6 strikes → ban permanent
```

### 2.2 Règles validées

`AntiCheatValidator` (assembly `KINETICS5.Network`) valide :

| Règle | Seuil | Tolérance | Sévérité |
|---|---|---|---|
| **Damage dealt** | ≤ weapon.DamagePct × 1.5 (×2 headshot, ×1.5 crit) | ×1.15 marge | Confirmed |
| **Movement speed** | ≤ agent.BaseSpeed × 1.15 | ×2 hard cap | Confirmed (hard) / Suspicious (margin) |
| **Fire rate** | ≥ 1 / weapon.FireRatePerSec | ×1.05 marge | Confirmed |
| **Headshot rate** | < 70% sur 20+ tirs | — | Suspicious |
| **Ability origin** | dans la map (≤10km²) | — | Suspicious |
| **Heal/Shield** | ≤ max agent | — | Confirmed |

### 2.3 Détection d'anomalies statistiques

Le `PlayerCheatStats` (par joueur) maintient une fenêtre glissante de 100 tirs
(`RecentShotsWindow`). À chaque tir, on calcule `HeadshotRate = Headshots / ShotsFired`.

Si `ShotsFired ≥ 20` et `HeadshotRate > 70%` → flag `headshot_rate_anomaly` (Suspicious).
Pas de ban immédiat (peut être légitime pour un très bon joueur), mais le pattern
est journalisé pour analyse.

### 2.4 Sanctions

| Strikes | Sanction |
|---|---|
| 1 strike (Suspicious) | Log + télémétrie PostHog |
| 2 strikes (Confirmed) | Log + télémétrie + reset gains match |
| 3 strikes | **Ban temporaire 7 jours** |
| 6 strikes | **Ban permanent** |

```csharp
// Exemple de flow :
validator.CheatDetected += report => {
    Debug.LogWarning($"[AntiCheat] {report.UserId}: {report.Rule} ({report.Severity})");
};
validator.PlayerBanned += (userId, severity) => {
    if (severity == CheatSeverity.BanPermanent) {
        // Kick immédiat + logout
        await NakamaClient.Instance.LogoutAsync();
    }
};
```

### 2.5 Limitations

L'anti-cheat applicatif **ne peut pas** détecter :

- **Aimbot externe** (input simulé au niveau OS) — nécessite EAC/BattlEye.
- **Wallhack** (ESP) — nécessite server-side occlusion culling.
- **Memory editing** (Cheat Engine) — nécessite obfuscation binaire + integrity check.
- **DLL injection** — nécessite code signing + anti-tamper.

Ces mesures sont **hors scope** du projet v0.1.0 et seront ajoutées en v1.0.0
si le jeu dépasse 100k DAU.

---

## 3. Sauvegarde chiffrée

### 3.1 AES-128-CBC

`SaveSystem` (assembly `KINETICS5.Core`) chiffre les sauvegardes joueur avec :

- **Algorithme** : AES-128-CBC (PKCS7 padding).
- **Clé** : 16 octets (32 hex chars), stockée via `PlayerSettings` ou `ScriptableObject`.
- **IV** : 16 octets fixes (à migrer vers IV aléatoire par save en v0.2.0).
- **Sortie** : Base64 string (compatible PlayerPrefs + fichier texte).

### 3.2 Stockage de la clé

| Plateforme | Mécanisme | Niveau sécurité |
|---|---|---|
| **Android** | Android Keystore (API 23+) | Hardware-backed (TEE) |
| **iOS** | iOS Keychain | Hardware-backed (SEP) |
| **Desktop** | DPAPI (Windows) / Keychain (Mac) | OS-level |

En v0.1.0, la clé est dans un `ScriptableObject` (configuration Inspector). En
production, elle doit être migrée vers Keystore/Keychain via plugin natif.

### 3.3 Vérification

```bash
# Vérifier qu'un fichier de save n'est pas en clair :
cat persistentDataPath/save_slot0.dat | grep "DisplayName"
# Ne doit rien retourner (le JSON est chiffré en Base64).
```

Test automatisé : `SaveSystemTests.Chiffrement_LeFichierSurDisqueNEstPasDuJSONEnClair`.

### 3.4 Backup PlayerPrefs

En plus du fichier disque, la save est backup dans `PlayerPrefs` (clé `K5_Save_Slot{N}`).
Si le fichier est supprimé (désinstallation partielle), on restore depuis PlayerPrefs.

---

## 4. GDPR / RGPD

### 4.1 Consentement analytics

`TelemetryLogger` (assembly `KINETICS5.Core`) exige un **consentement explicite**
avant tout envoi de données analytics :

```csharp
// Au premier lancement, l'écran Onboarding demande :
// "Autorisez-vous KINETICS 5 à collecter des données d'utilisation anonymisées
//  pour améliorer le jeu ? [Oui] [Non]"
TelemetryLogger.Instance.SetConsent(true);  // persisté dans PlayerPrefs
```

Si `HasConsent == false`, **aucun event n'est envoyé** à PostHog/Unity Analytics.
Les events sont juste empilés en mémoire (LRU 1000) et flushés si le joueur
donne son consentement plus tard.

### 4.2 Données collectées

| Type | Exemple | Anonymisée |
|---|---|---|
| Session | start/stop, durée | Oui (session ID random) |
| Mission | missionId, duration, score, success | Oui |
| Combat | weaponId, shots, kills, deaths | Oui |
| UI | screen name, button click | Oui |
| Perf | FPS, GC alloc, memory | Oui |
| **PII** (nom, email) | Jamais envoyées | — |

### 4.3 Droit à l'effacement

Endpoint Nakama (à implémenter côté serveur) :

```
DELETE /v2/account
Authorization: Bearer {session_token}
```

Supprime : compte, storage objects, leaderboard records, friends, crew membership.
Le client appelle cet endpoint via le menu **Settings > Privacy > Delete Account**.

### 4.4 Export de données

Menu **Settings > Privacy > Export My Data** :
- Génère un JSON contenant toutes les données joueur (profil, stats, save).
- Téléchargement local (Share sheet iOS / Intent Android).

### 4.5 Cookies et tracking

Pas de cookies web (pas de WebView). Pas de SDK publicitaire (AdMob, Facebook)
en v0.1.0 — seront ajoutés en v0.3.0 avec opt-in explicite.

---

## 5. Rate limiting

### 5.1 Chat (ChatService)

| Action | Limite | Fenêtre |
|---|---|---|
| Envoi message | 5 messages | 10 secondes |
| Longueur message | 280 caractères | — |
| Join/leave canal | 10 par minute | 60 secondes |

```csharp
// Implémentation (ChatService.CheckRateLimit) :
while (_sendTimestamps.Count > 0 && (now - _sendTimestamps.Peek()).TotalSeconds > _rateLimitWindow)
    _sendTimestamps.Dequeue();
if (_sendTimestamps.Count >= _rateLimitCount) return false;
```

### 5.2 API Nakama

Le serveur Nakama a ses propres limites (config `nakama.config.yml`) :

```yaml
socket.outgoing_msg_size: 4096         # max 4 KB par message
session.max_message_size_bytes: 1048576 # max 1 MB par RPC
rate_limits:
  - name: leaderboard_submit
    limit: 10
    period: 60
  - name: chat_message
    limit: 30
    period: 60
```

### 5.3 Tentatives de connexion

`NakamaClient` : 5 tentatives max avec backoff exponentiel (1s, 2s, 4s, 8s, 16s),
puis passage en mode Offline. Pas de retry infini (évite DoS involontaire).

---

## 6. Input validation

### 6.1 Sanitization HTML (chat)

`ChatService.Sanitize()` strip les balises HTML avant envoi/affichage :

```csharp
private static readonly Regex HtmlTagPattern = new(@"<[^>]+>", RegexOptions.Compiled);
input = HtmlTagPattern.Replace(input, string.Empty);
```

Empêche les attaques XSS via TextMeshPro rich text (`<color>`, `<size>`, `<sprite>`).

### 6.2 Profanity filter

`ChatService.CensorProfanity()` remplace les mots bannis (FR + EN) par des `*` :

```csharp
foreach (var pattern in BannedPatterns)
    input = pattern.Replace(input, match => new string('*', match.Length));
```

3 patterns regex compilés (FR, EN, slurs). Extensible via un fichier JSON `profanity.json`.

### 6.3 Validation numérique

Tous les IDs externes (chat, DM) sont validés via regex avant usage :

```csharp
// UserId Nakama : UUID v4 ou hex 32 chars.
private static readonly Regex UserIdPattern = new(@"^[a-f0-9\-]{32,36}$", RegexOptions.Compiled);
```

### 6.4 Validation des noms d'équipage

- 3-32 caractères.
- Alphanumérique + espaces + tirets.
- Pas de mots bannis.
- Pas d'URL (anti-phishing).

---

## 7. Secure networking (TLS)

### 7.1 TLS 1.3

Le serveur Nakama production doit être configuré en TLS 1.3 uniquement :

```nginx
# Reverse proxy nginx devant Nakama
ssl_protocols TLSv1.3;
ssl_ciphers TLS_AES_256_GCM_SHA384:TLS_AES_128_GCM_SHA256;
ssl_prefer_server_ciphers off;
```

### 7.2 Certificate pinning

Le client Nakama Unity doit épingler le certificat du serveur production pour
empêcher les MITM (man-in-the-middle) même si l'OS trust un CA malveillant.

```csharp
// Configuration NakamaClient (à implémenter en v0.2.0) :
var client = new Nakama.Client("https", "nakama.kinetics5.gg", 443, serverKey,
    new UnityWebRequestAdapter(
        new CertificatePinner()
            .Add("nakama.kinetics5.gg", "sha256/ABC123...")
            .Add("staging.kinetics5.gg", "sha256/DEF456...")
    ));
```

### 7.3 HSTS

Le serveur envoie `Strict-Transport-Security: max-age=63072000; includeSubDomains; preload`.

### 7.4 Auth tokens

- Session token Nakama : JWT, expiration 1h, refresh automatique.
- Pas de stockage du mot de passe (jamais, ni en clair ni hashé côté client).
- Logout invalide le token côté serveur (`SessionLogoutAsync`).

---

## 8. Protection des assets

### 8.1 Addressables

- Groupes `Missions` et `Audio` ne sont PAS listés publiquement.
- Catalogue Addressables chiffré via `Addressables.RuntimeCatalogBytes` encrypt.
- Authentification CDN (CloudFront signed URLs) pour les assets premium.

### 8.2 Anti-extraction textures

- Textures marquées `Read/Write = false` (pas de `Texture2D.GetRawTextureData` côté runtime).
- Format ASTC (difficile à décoder sans tool spécifique).

### 8.3 Code IL2CPP

- Stripping **High** réduit la quantité de code reversable.
- Pas de symboles debug dans les builds release (`Debug Symbols = Off`).
- Pour une protection renforcée : **Unity Burst** pour les hot paths (compile en ASM natif).

---

## 9. Audit de sécurité

### 9.1 Tests automatisés

| Test | Fichier | Vérifie |
|---|---|---|
| `SaveSystemTests.Chiffrement_*` | EditMode | Save chiffrée sur disque |
| `SaveSystemTests.Save_Corrompue_*` | EditMode | Recovery gracieux |
| `ChatServiceTests.*` (à étendre) | EditMode | Rate limit, profanity, HTML sanitize |
| `AntiCheatValidatorTests.*` (à étendre) | EditMode | Bornes damage/speed/fire-rate |

### 9.2 Audit manuel (checklist release)

- [ ] Clé AES n'est pas dans le repo Git (mais dans Keystore/Keychain).
- [ ] Server key Nakama production est unique (pas `defaultkey`).
- [ ] TLS 1.3 actif côté serveur (test via `openssl s_client`).
- [ ] Certificate pinning configuré côté client.
- [ ] GDPR consent screen présente à l'onboarding.
- [ ] Politique de confidentialité accessible depuis le menu Settings.
- [ ] Endpoint `/v2/account` DELETE implémenté côté serveur.
- [ ] Profanity filter couvre FR + EN + slurs courants.
- [ ] Rate limiting chat actif (5/10s).
- [ ] Logs serveur ne contiennent pas de PII (email, IP).

---

## 10. Incident response

### 10.1 Détection

- Alerte PostHog : pic de `anticheat_violation` events (>10/joueur/heure).
- Alerte serveur : pic de requêtes API (>1000/min/joueur).
- Alerte community : signalement joueurs via ticket Discord.

### 10.2 Procédure

1. **Identification** : corréler logs serveur + télémétrie client.
2. **Containment** : ban temporaire 7 jours du joueur suspect.
3. **Investigation** : replay des actions via les logs Nakama.
4. **Remediation** : si cheat confirmé → ban permanent + reset classement.
5. **Post-mortem** : documentation de l'incident + patch anti-cheat si nouvelle technique détectée.

### 10.3 Communication

- **Joueur banni** : email explicatif (raison, durée, voie de recours).
- **Communauté** : post Discord si incident majeur (>10 joueurs affectés).
- **Autorités** : si données bancaires compromises (PCI-DSS), déclaration CNIL sous 72h.

---

## 11. Références

- **Nakama security docs** : https://heroiclabs.com/docs/security/
- **OWASP Mobile Top 10** : https://owasp.org/www-project-mobile-top-10/
- **GDPR guidance** : https://edpb.europa.eu/edpb_en
- **Unity security best practices** : https://docs.unity3d.com/6000.0/Documentation/Manual/SecurityBestPractices.html
