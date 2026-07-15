# DÉPLOIEMENT — KINETICS 5

> CI/CD, keystore Android, provisioning iOS, TestFlight, Google Play Console,
> store listings, release checklist.

---

## 1. Vue d'ensemble du pipeline

```
Développeur (local)
    │ git push origin feature/xyz
    ▼
GitHub (Pull Request)
    │ CI: lint + tests + build APK dev
    ▼
Merge → main
    │ CI: build staging (AAB + IPA) → upload TestFlight + Play Internal
    ▼
QA validation (TestFlight + Play Internal)
    │ approbation manuelle
    ▼
Tag v0.X.0
    │ CI: build production → upload Play Production + App Store
    ▼
Release publique (review store)
```

---

## 2. CI/CD GitHub Actions

### 2.1 Workflow file

`.github/workflows/build.yml` :

```yaml
name: Build & Test

on:
  push:
    branches: [main, develop]
    tags: ['v*']
  pull_request:
    branches: [main]

env:
  UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
  UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
  UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}

jobs:
  test:
    name: EditMode + PlayMode Tests
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { lfs: true }
      - uses: game-ci/unity-test-runner@v4
        with:
          unityVersion: '6000.0.26f1'
          projectPath: 'unity-project'
          testMode: All
          artifactsPath: 'test-results'
      - uses: actions/upload-artifact@v4
        with: { name: test-results, path: test-results }

  build-android:
    needs: test
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/v')
    steps:
      - uses: actions/checkout@v4
        with: { lfs: true }
      - uses: game-ci/unity-builder@v4
        with:
          unityVersion: '6000.0.26f1'
          projectPath: 'unity-project'
          targetPlatform: Android
          androidKeystoreName: 'kinetics5.keystore'
          androidKeystoreBase64: ${{ secrets.ANDROID_KEYSTORE_BASE64 }}
          androidKeystorePass: ${{ secrets.ANDROID_KEYSTORE_PASS }}
          androidKeyaliasName: ${{ secrets.ANDROID_KEY_ALIAS }}
          androidKeyaliasPass: ${{ secrets.ANDROID_KEY_ALIAS_PASS }}
          buildMethod: 'KINETICS5.Editor.BuildPipeline.BuildAndroidRelease'
      - uses: actions/upload-artifact@v4
        with: { name: android-aab, path: build/Android/*.aab }

  build-ios:
    needs: test
    runs-on: macos-latest
    if: startsWith(github.ref, 'refs/tags/v')
    steps:
      - uses: actions/checkout@v4
        with: { lfs: true }
      - uses: game-ci/unity-builder@v4
        with:
          unityVersion: '6000.0.26f1'
          projectPath: 'unity-project'
          targetPlatform: iOS
          buildMethod: 'KINETICS5.Editor.BuildPipeline.BuildIOSRelease'
      - uses: actions/upload-artifact@v4
        with: { name: ios-xcode, path: build/iOS/** }

  deploy-testflight:
    needs: build-ios
    runs-on: macos-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/download-artifact@v4
        with: { name: ios-xcode, path: build/iOS }
      - uses: apple-actions/import-codesign-certs@v2
        with:
          p12-file-base64: ${{ secrets.APPLE_P12_BASE64 }}
          p12-password: ${{ secrets.APPLE_P12_PASS }}
      - run: |
          cd build/iOS
          xcodebuild -project KINETICS5.xcodeproj \
            -scheme KINETICS5 \
            -configuration Release \
            -archivePath build/KINETICS5.xcarchive \
            -destination 'generic/platform=iOS' \
            DEVELOPMENT_TEAM=${{ secrets.APPLE_TEAM_ID }} \
            archive
      - uses: xbuilder/testflight-upload-action@v1
        with:
          app-store-connect-issuer-id: ${{ secrets.ASC_ISSUER_ID }}
          app-store-connect-key-id: ${{ secrets.ASC_KEY_ID }}
          app-store-connect-private-key: ${{ secrets.ASC_PRIVATE_KEY }}
          archive-path: build/iOS/build/KINETICS5.xcarchive

  deploy-play-internal:
    needs: build-android
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
        with: { name: android-aab, path: build/Android }
      - uses: r0adkll/upload-google-play@v1
        with:
          serviceAccountJsonPlainText: ${{ secrets.GOOGLE_PLAY_SERVICE_ACCOUNT }}
          packageName: 'gg.kinetics5.game'
          releaseFiles: build/Android/*.aab
          track: 'internal'
          status: 'completed'
```

### 2.2 Secrets GitHub

À configurer dans **Settings > Secrets and variables > Actions** :

| Secret | Description |
|---|---|
| `UNITY_LICENSE` | Licence Unity (ULF file content) |
| `UNITY_EMAIL` | Email compte Unity |
| `UNITY_PASSWORD` | Mot de passe compte Unity |
| `ANDROID_KEYSTORE_BASE64` | Keystore `.keystore` en base64 |
| `ANDROID_KEYSTORE_PASS` | Mot de passe keystore |
| `ANDROID_KEY_ALIAS` | Alias de la clé |
| `ANDROID_KEY_ALIAS_PASS` | Mot de passe alias |
| `APPLE_P12_BASE64` | Certificat Apple Developer en P12 base64 |
| `APPLE_P12_PASS` | Mot de passe P12 |
| `APPLE_TEAM_ID` | Team ID Apple Developer |
| `ASC_ISSUER_ID` | App Store Connect Issuer ID |
| `ASC_KEY_ID` | App Store Connect API Key ID |
| `ASC_PRIVATE_KEY` | App Store Connect API private key |
| `GOOGLE_PLAY_SERVICE_ACCOUNT` | Service account JSON Google Play |

### 2.3 Unity Cloud Build (alternative)

En complément de GitHub Actions, Unity Cloud Build peut être utilisé pour des
builds on-demand depuis le dashboard Unity :

1. **dashboard.unity.com > Cloud Build > Set up project**.
2. Connecter le repo GitHub.
3. Configurer 3 targets : `android-staging`, `ios-staging`, `android-prod`.
4. Branches : `develop` → staging, `main` → prod.

---

## 3. Keystore Android

### 3.1 Génération

```bash
keytool -genkeypair -v \
  -keystore kinetics5.keystore \
  -alias kinetics5 \
  -keyalg RSA -keysize 2048 -validity 10000 \
  -dname "CN=KINETICS 5 Studio, OU=Mobile, O=KINETICS 5, L=Paris, C=FR" \
  -storepass $KEYSTORE_PASS \
  -keypass $KEY_PASS
```

### 3.2 Stockage

- **JAMAIS** dans le repo Git (ajouter `*.keystore` à `.gitignore`).
- Stocker dans un gestionnaire de secrets (1Password, AWS Secrets Manager, GitHub Secrets).
- Backup chiffré sur 2 supports physiques distincts (clé USB + cloud storage).

### 3.3 Rotation

- Le keystore Android **ne doit jamais être perdu** (Google Play utilise l'empreinte
  pour authentifier les mises à jour).
- Si perte : procédure `Google Play Console > App Integrity > Request key upgrade`
  (lente, ~7 jours, nécessite validation propriété).

### 3.4 Configuration Unity

**Project Settings > Player > Android > Publishing Settings** :

```yaml
Custom Keystore: kinetics5.keystore
Custom Keystore Password: $KEYSTORE_PASS
Custom Key Alias: kinetics5
Custom Key Password: $KEY_PASS
```

Ou via `BuildPipeline.BuildAndroidRelease` :

```csharp
PlayerSettings.Android.keystoreName = "kinetics5.keystore";
PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("KEYSTORE_PASS");
PlayerSettings.Android.keyaliasName = "kinetics5";
PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("KEY_PASS");
```

---

## 4. Provisioning iOS

### 4.1 Certificats

2 types nécessaires :

1. **Distribution Certificate** (App Store / Ad Hoc).
2. **Development Certificate** (device debug).

Générés via **developer.apple.com > Certificates, IDs & Profiles**.

### 4.2 App ID

- **Bundle ID** : `gg.kinetics5.game` (doit correspondre exactement au `applicationIdentifier` Unity).
- **Capabilities** : Push Notifications, Game Center, In-App Purchase, Sign in with Apple.

### 4.3 Provisioning Profile

- **App Store** profile (pour release TestFlight + App Store).
- **Ad Hoc** profile (pour QA sur devices spécifiques).
- **Development** profile (pour debug device).

### 4.4 Export P12

```bash
# Export du certificate + private key en P12 (depuis Trousseau d'accès macOS)
# puis base64 pour stockage GitHub Secret :
base64 -i developer_id.p12 -o developer_id.p12.b64
```

### 4.5 App Store Connect API

Pour automatiser l'upload TestFlight :

1. **appstoreconnect.apple.com > Users and Access > Keys**.
2. Générer une clé API (Admin role).
3. Télécharger le fichier `.p8` (une seule fois — non récupérable).
4. Stocker `Issuer ID`, `Key ID`, `private key` dans les secrets GitHub.

---

## 5. TestFlight

### 5.1 Configuration

- **App Store Connect > My Apps > KINETICS 5 > TestFlight**.
- **Test Information** : description courte (FR + EN), email contact, capture d'écran.
- **Beta App Review** : nécessaire pour redistribution > 25 testers externes.

### 5.2 Groupes de testeurs

| Groupe | Count | Description |
|---|---|---|
| **Internal** | 5 (dev team) | Builds automatiquement disponibles |
| **QA External** | 20 | Beta review requise pour 1er build |
| **Closed Beta** | 100 | Beta publique fermée (opt-in) |
| **Open Beta** | 1000 | Beta publique ouverte (lien public) |

### 5.3 Feedback

- Testers peuvent envoyer feedback via l'app TestFlight (screenshot + commentaire).
- Emails arrivent à `qa@kinetics5.gg`.
- Tickets auto-créés dans le board GitHub Issues (`beta-feedback` label).

---

## 6. Google Play Console

### 6.1 Configuration app

- **Package name** : `gg.kinetics5.game`.
- **App name** : `KINETICS 5`.
- **Developer account** : `KINETICS 5 Studio` (compte validé, $25 fee unique).
- **App signing** : Google Play App Signing (recommandé — let Google manage the key).

### 6.2 Tracks

| Track | Usage |
|---|---|
| **Internal** | Dev team + trusted testers (~100) |
| **Closed (Alpha)** | Beta fermée (opt-in link) |
| **Open (Beta)** | Beta publique ouverte |
| **Production** | Release publique |

### 6.3 Store listing

À compléter pour la release publique :

- **App name** : KINETICS 5
- **Short description** (80 chars) : "FPS sci-fi mobile. Incarnez 4 agents, 7 missions co-op."
- **Full description** (4000 chars) : lore + features + gameplay details.
- **App icon** : 512×512 PNG, 32-bit.
- **Feature graphic** : 1024×500 PNG.
- **Phone screenshots** : 2-8 screenshots, min 320px, max 3840px.
- **Tablet screenshots** : optionnel mais recommandé.
- **App category** : Action.
- **Content rating** : PEGI 16 (violence réaliste).
- **Target audience** : 13+ (violence stylisée).
- **Privacy Policy URL** : https://kinetics5.gg/privacy
- **Terms of Service URL** : https://kinetics5.gg/tos

### 6.4 In-app products

Pour la v0.3.0 (post-launch) :

| Product ID | Type | Prix | Description |
|---|---|---|---|
| `cr_500` | Consumable | 4,99 € | 500 crédits |
| `cr_1500` | Consumable | 9,99 € | 1500 crédits + 10% bonus |
| `premium_pass` | Subscription | 9,99 €/mois | Battle pass premium |

---

## 7. Store listings (App Store + Play)

### 7.1 Captures d'écran requises

| Plateforme | Format | Min | Recommandé |
|---|---|---|---|
| iPhone 6.7" | 1290×2796 | 3 | 5 |
| iPhone 6.1" | 1170×2532 | 3 | 5 |
| iPad 12.9" | 2048×2732 | 3 | 5 |
| Android phone | 1080×1920 min | 2 | 4 |

### 7.2 Captures recommandées (FR + EN)

1. Splash + logo KINETICS 5
2. Lobby principal avec VULCAN
3. Combat HUD (FPS view)
4. Boss fight (TITAN, IRON HARVEST)
5. Operation Summary (rewards)

### 7.3 Description courte (FR, 80 chars)

> FPS sci-fi mobile. 4 agents, 7 missions co-op, boss multi-phases.

### 7.4 Description complète (FR, 4000 chars)

```
KINETICS 5 — FPS sci-fi mobile.

Incarnez VULCAN, XEN, JOLT ou XANO et enchaînez des missions co-op (2-4 joueurs)
à travers 7 régions de l'espace. Affrontez des boss multi-phases, maîtrisez 5
éléments de dégâts (Kinetic, Energy, Cryo, Volt, Explosive) et progressez jusqu'au
niveau 60.

✦ 4 AGENTS JOUABLES ✦
- VULCAN (Tank) : suppression lourde, alliages expérimentaux.
- XEN (Assault) : DPS polyvalent, mobilité élevée.
- JOLT (Support) : soin, EMP, utilitaire.
- XANO (Recon) : furtif, éliminations silencieuses.

✦ 7 MISSIONS CO-OP ✦
- SHADOW FALL (Extraction) : vaisseau cargo.
- NEURAL BREACH (Sabotage) : croiseur lourd.
- VOID LOCK (Survival) : station orbitale.
- IRON HARVEST (Assassination) : boss TITAN.
- DEEP SIGNAL (Recon) : épave, furtif.
- BLACK ECHO (Defense) : porte-vaisseaux.
- FINAL VECTOR (BossRush) : vaisseau amiral.

✦ 14 ARMES ✦
5 primaires (HEAVY RX-14, RIFLE CX-24, AX-9 SR, CX-27 ATLAS, C-2),
4 secondaires (GUARD V-9, CORE P-4, MAGNUM E-2, ION X-S),
4 tactiques (FRAG-X, CYBER TRAP F-2, SUPERNOVA, TITAN M-8).

✦ PROGRESSION ✦
- 60 niveaux joueur, XP croissante.
- Mastery par arme (1-100).
- Arbres d'éveil par agent (4 nœuds de talent).
- Loot 4 raretés (Common, Rare, Epic, Legendary).

✦ MULTIPLAYER ✦
- Co-op 2-4 joueurs (matchmaker intégré).
- Leaderboards global / friends / crew (seasons 30 jours).
- Chat world / crew / DM avec filtre anti-insultes.
- Anti-cheat applicatif (validation serveur).

✦ OPTIMISATION MOBILE ✦
- 60 FPS mid-range (Snapdragon 7 Gen 1+).
- Object pooling, no-GC hot path, IL2CPP ARM64.
- ASTC 6×6 textures, addressables streaming.
- Support tactile + manette + clavier/souris.

KINETICS 5 — The Wall Never Retreats.
```

---

## 8. Release checklist

### 8.1 Pre-release (T-7 jours)

- [ ] Toutes les features prévues sont mergées dans `main`.
- [ ] Tests automatisés verts (EditMode + PlayMode, 50+ tests).
- [ ] Build staging sur 3 devices (low/mid/high) — 60 FPS / 30 FPS OK.
- [ ] Memory profiling : < 600 Mo sur low-end.
- [ ] Audit sécurité complet (voir `SECURITY.md` section 9.2).
- [ ] Localisation : 7 langues complètes, pas de clés manquantes.
- [ ] GDPR consent screen présente.
- [ ] Privacy Policy + ToS publiés sur https://kinetics5.gg.
- [ ] Store listings complétés (description FR + EN, screenshots, icons).

### 8.2 Build (T-2 jours)

- [ ] Tag `v0.X.0` poussé sur GitHub.
- [ ] CI build Android (AAB) + iOS (IPA) en succès.
- [ ] Upload TestFlight (iOS) —Internal track.
- [ ] Upload Google Play Internal track.
- [ ] Smoke test sur build release (5 min de jeu sans crash).

### 8.3 Beta review (T-1 jour)

- [ ] Beta App Review Apple (24-48h) approuvée.
- [ ] Google Play Internal review (1-3h) approuvée.
- [ ] Testers externes notifiés (TestFlight + Play Internal).
- [ ] Monitoring crashes : Firebase Crashlytics actif.

### 8.4 Production release (T)

- [ ] Build production tagué `v0.X.0-prod`.
- [ ] Upload Google Play Production track (rolling 10% → 100% sur 7 jours).
- [ ] Submit App Store Review (24-48h).
- [ ] Une fois approuvé : release automatique ou phased (7 jours).
- [ ] Communication : Discord + Twitter + email newsletter.

### 8.5 Post-release (T+1 jour)

- [ ] Monitoring crashes < 0.5% sessions.
- [ ] Monitoring ANR < 0.1% sessions.
- [ ] Réponses reviews store (positives et négatives).
- [ ] Hotfix branch créée si bugs critiques détectés.

---

## 9. Hotfix

### 9.1 Procédure

1. Créer branch `hotfix/v0.X.1` depuis `main`.
2. Cherry-pick les commits de fix.
3. Tests automatisés verts.
4. Tag `v0.X.1` → CI build → upload stores sur track Production (expedited review).
5. Apple : ** Expedited Review** (max 2/an — réserver pour critique).

### 9.2 Rollback

- **Android** : Google Play Console > Track > Roll back to previous version.
- **iOS** : pas de rollback direct — publier une nouvelle version avec fix.

---

## 10. Monitoring post-release

### 10.1 Crashlytics

- **Firebase Crashlytics** intégré (Android + iOS).
- Seuil alerte : > 0.5% sessions avec crash.
- Stack traces dé-obfusqués via mapping files uploadés.

### 10.2 Analytics

- **PostHog** : events custom KINETICS 5.
- **Unity Analytics** : audience + funnel.
- Dashboards : DAU, retention J1/J7/J30, session duration, crash-free.

### 10.3 Performance monitoring

- **Unity Performance Reporting** : frames drop, memory spikes.
- Alerting email si > 5% sessions en dessous de 30 FPS sur mid-range.

---

## 11. Références

- **GameCI** : https://game.ci/docs/github/getting-started
- **Apple App Store Connect API** : https://developer.apple.com/app-store-connect/api/
- **Google Play Developer API** : https://developers.google.com/android-publisher
- **Firebase Crashlytics** : https://firebase.google.com/products/crashlytics
