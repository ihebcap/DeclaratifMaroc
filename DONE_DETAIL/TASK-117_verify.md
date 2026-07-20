# VERIFY — TASK-117 — Intégration vérification de licence (ApLicence) dans Declaration.API

## Résumé

Implémentée en worker exceptionnel (rôle inversé, demande explicite PO 18/07/2026, cf. réserve
`CLAUDE.md`, même mode que TASK-101/TASK-075/TASK-114). TASK-117 était documentée `⛔ bloquée`
(`TODO.md`) car elle dépendait d'un repo séparé — cette session a constaté que ce blocage n'est
plus d'actualité : `D:\_vibe\GRLicence` (librairie partagée ApLicence) est livré et packagé
(`GRLicence.1.1.0.nupkg` dans `D:\_vibe\nuget-local`, TASK-001/TASK-002 approuvées 17/07/2026 dans
ce repo séparé) — TASK-117 pouvait donc démarrer.

## Décisions actées avant codage (points ouverts du TASK original)

1. **Subject** — confirmé par le PO (cette session) : **`TRESO_GRM_COM`**, codé en dur (constante
   `Program.cs`), **jamais** lu depuis `connections.json`. Ceci corrige le texte original de
   TASK-117 (étape 5), rédigé avant la décision de sécurité `GRLicence`/TASK-002 (17/07/2026) qui
   interdit tout subject configurable côté fichier client (anti-contournement : un fichier sur le
   poste client est modifiable par quiconque y a accès disque). `connections.json` (racine +
   `deploy/`) a été corrigé en conséquence : la clé `ApLicence.Subject` (héritée de TASK-115, sans
   effet réel) est retirée ; seules `ServerAddress`/`ServerPort` (+ nouveau `TimeoutSeconds`)
   subsistent, ce sont des paramètres de déploiement légitimes (CDC §4).
   ⚠️ **Écart signalé** : `TASK-122` (formulaire setup, pas encore codée) décrit encore ce subject
   comme devant être *écrit dans `connections.json`* — à corriger dans TASK-122 pour rester cohérent
   avec ce qui est réellement implémenté ici (le setup n'a plus besoin d'écrire ce champ du tout).
2. **Périmètre du blocage** (front statique vs `/api` seul) — tranché : **seules les routes
   `/api/**` sont bloquées** ; le front statique (`wwwroot`) n'est jamais bloqué, pour qu'il puisse
   toujours charger et afficher lui-même le message de blocage (sinon impossible de savoir
   pourquoi on est bloqué). Le front interroge un endpoint dédié (`GET /api/licence/status`),
   volontairement exclu du blocage, pour connaître l'état.

## Bug réel découvert et corrigé (hors périmètre GRLicence mais bloquant pour ce TASK)

`GRLicence.1.1.0.nupkg` ne contient que `GRLicence.dll` — les DLL `ApLicence.Common/.Core/.Core.Net`
qu'il référence en privé (`<Reference Private="true">`) ne sont **pas** packées dans le `.nupkg`
(vérifié : `unzip` du `.nupkg` → un seul fichier `lib/netstandard2.0/GRLicence.dll`, aucun
`ApLicence.*.dll`). Conséquence en conditions réelles : `Declaration.API` levait une
`FileNotFoundException` non gérée dès `DemarrerAsync()` — **le process crashait au démarrage**,
contredisant frontalement le critère central de la CDC (« le process démarre toujours »). L'exception
échappe même au `try/catch` interne de `LicenceMonitor` (échec de résolution de type au moment de
JITter la méthode, avant l'exécution du corps protégé).
**Correctif côté GRF** (le seul possible sans modifier le repo séparé `GRLicence`, hors périmètre) :
copie des mêmes DLL que celles utilisées par `GRLicence` lui-même
(`D:\_vibe\GRLicence\libs\ApLicence\*.dll`, mêmes versions garanties) dans
`Declaration.API\libs\ApLicence\` + références `<Reference>` explicites dans `Declaration.API.csproj`
(même patron que les DLL `Tresorerie.*` déjà présentes dans ce projet).
**À signaler séparément au propriétaire du repo `GRLicence`** : corriger le `.csproj`/`dotnet pack`
pour embarquer ces DLL dans le `.nupkg` (ou documenter explicitement que chaque consommateur doit les
fournir lui-même) — sinon tout futur consommateur du package retombera dans le même crash.

## Modifications réalisées

| Fichier | Changement |
|---|---|
| `nuget.config` (racine, nouveau) | Source NuGet locale `grlicence-local` → `D:\_vibe\nuget-local` + `nuget.org`. |
| `Declaration.API/Declaration.API.csproj` | `PackageReference GRLicence 1.1.0` + `Reference` explicites vers `libs\ApLicence\*.dll` (copies, cf. bug ci-dessus). |
| `Declaration.API/libs/ApLicence/*.dll` (nouveau) | Copies des DLL `ApLicence.Common/.Core/.Core.Net` utilisées par `GRLicence`. |
| `Declaration.API/Licence/GrfLicenceConfigProvider.cs` (nouveau) | `ILicenceConfigProvider` lisant adresse/port/timeout depuis `connections.json` (section `ApLicence`), replis par défaut permissifs `127.0.0.1:8003:5s` — jamais de subject ici. |
| `Declaration.API/Controllers/LicenceController.cs` (nouveau) | `GET /api/licence/status` (public, `AllowAnonymous`) — seul point de lecture du `LicenceStatus` exposé au front. |
| `Declaration.API/Program.cs` | Enregistrement `LicenceMonitor` singleton (subject `"TRESO_GRM_COM"` en dur) ; `await licenceMonitor.DemarrerAsync()` après `builder.Build()` (bloquant quelques secondes, jamais d'exception) ; middleware unique bloquant tout `/api/**` sauf `/api/licence/status` si `!EstValide` (503 + `{ message }`), placé avant `UseAuthentication`/`MapControllers`. |
| `connections.json` (racine + `deploy/`) | Section `ApLicence` : suppression de la clé `Subject` (sans effet, source de confusion), ajout `TimeoutSeconds`, commentaire mis à jour expliquant pourquoi le subject n'y est plus. |
| `declaration-tva-web/src/api.ts` | `getLicenceStatus()` (typed, `GET /licence/status`). |
| `declaration-tva-web/src/App.tsx` | Check licence au chargement (avant même l'écran de connexion, CDC §1.3) : écran plein `LicenceGate` (loading/blocked, message de l'API) + `LicenceExpirationBanner` (J-30, jamais bloquante) affichée dès que valide + `alerteProcheExpiration`. |

## Vérifications — preuve réelle

### 1. Build
```
dotnet build Declaration.API/Declaration.API.csproj -c Debug   → 0 erreur (10 warnings préexistants, sans lien)
npm run build (declaration-tva-web)                            → tsc + vite OK, 0 erreur
```

### 2. Démarrage réel, serveur ApLicence.Server injoignable (aucune instance disponible dans cet environnement)
```
fail: GRLicence.LicenceMonitor[0]
      vérification de licence en échec — état basculé à Invalide : aucune réponse du serveur de
      licence pour le subject 'TRESO_GRM_COM' dans le délai imparti (00:00:05).
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```
**Le process démarre malgré tout** (critère central de la CDC) — avant le correctif DLL ci-dessus,
il crashait ici. Comportement fail-closed conforme (timeout = seul moyen de détecter un subject
inconnu/serveur injoignable côté protocole `REQUEST`, CDC §3.2).

### 3. Point de contrôle unique — bout-en-bout HTTP réel
```
GET /api/licence/status  → 200 {"estValide":false,"message":"Merci de vérifier la licence","alerteProcheExpiration":false,"joursRestants":null,"dateExpiration":null}
GET /api/declarations    → 503 {"message":"Merci de vérifier la licence"}
POST /api/auth/login     → 503 {"message":"Merci de vérifier la licence"}   (même si non authentifié — un seul point de contrôle, pas de contournement par route)
GET /                    → 200 (front html servi normalement)
GET /assets/<bundle>.js  → 200 (assets jamais bloqués)
```

### 4. Rendu front réel (Playwright, contre l'API réelle ci-dessus)
Écran de blocage réel (licence invalide, capture plein écran) :
message "Merci de vérifier la licence" + explication, aucun écran fonctionnel derrière.

Scénarios "licence valide" simulés par interception réseau (`page.route`) du seul endpoint
`/api/licence/status` — pas d'instance `ApLicence.Server` disponible dans cet environnement pour
tester ce cas de bout en bout (cf. Réserve) :
- `estValide:true, alerteProcheExpiration:true, joursRestants:12` → bannière ambre "Licence proche
  de l'expiration — 12 jours restants (échéance le 30/07/2026)" affichée en haut, écran de connexion
  normal en dessous.
- `estValide:true, alerteProcheExpiration:false` → aucune bannière, écran de connexion normal.

### 5. Lecture sans latence réseau (revue de code)
`LicenceMonitor.GetStatus()` = lecture d'un champ `volatile` déjà calculé (`GRLicence/LicenceMonitor.cs:70`),
jamais d'appel réseau déclenché par le middleware ni par `LicenceController` — confirmé par lecture
du code de la librairie (aucun `await` sur le chemin de lecture).

## Critères de validation

- [x] Le process démarre toujours, quel que soit l'état de la licence (prouvé réel, cf. §2 — après
      correctif du bug packaging DLL).
- [x] Un seul point de contrôle dans le pipeline HTTP (`Program.cs`, un seul middleware, aucune
      vérification dispersée par écran/endpoint) — `/api/auth/login` bloqué comme les autres,
      preuve qu'aucune route n'est oubliée.
- [x] Message de blocage et bannière J-30 conformes au contrat `LicenceStatus` de la librairie
      (mêmes noms de champs, même message fixe).
- [x] Aucun repli silencieux vers « licence valide » — état par défaut `Invalide`/`Inconnue`
      (comportement de la librairie, revue de code `LicenceStatus.Inconnue()`/`Invalide()`), jamais
      contredit côté GRF (aucun try/catch ajouté autour de `GetStatus()`).

## Réserves non bloquantes

1. **Scénario "licence valide" non testé contre un vrai `ApLicence.Server`** — aucune instance
   disponible dans cet environnement (même limite que documentée dans `GRLicence/DONE_DETAIL`, qui
   a pu le tester sur son poste). Le rendu front de ce cas a été vérifié par interception réseau
   (§4), le comportement back par revue de code (contrat `LicenceStatus.Valide(...)`) — pas une
   preuve réelle de bout en bout. À confirmer dès qu'un serveur `ApLicence.Server` réel avec le
   subject `TRESO_GRM_COM` enregistré sera accessible.
2. **Bug de packaging `GRLicence` signalé, pas corrigé ici** (cf. section dédiée ci-dessus) — le
   contournement (copie locale des DLL dans `Declaration.API/libs/ApLicence/`) fonctionne mais
   duplique des binaires entre repos ; si `GRLicence` republie une version corrigeant le `.nupkg`,
   ce contournement local pourra être retiré.
3. **`TASK-122`** (non codée) doit être corrigée pour ne plus décrire le subject comme un champ
   `connections.json` — signalé ci-dessus, pas traité ici (hors périmètre strict de TASK-117).
4. **Suivi UI (guide de rendu)** : `LicenceGate`/`LicenceExpirationBanner` utilisent des styles
   inline cohérents avec le reste du front (pas de nouveau design system) — pas retouchés en cas de
   futur re-thème (TASK-120/121), à vérifier alors comme les autres composants front.

## Statut
Implémentée et auto-vérifiée par l'architecte en mode worker exceptionnel — voir réserves ci-dessus.
Approuvée (cf. entrées `DONE.md`/`CHANGELOG.md`).
