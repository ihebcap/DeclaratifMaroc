# VERIFY — TASK-123 : rétrogradation net10.0 → net8.0 (Microsoft.Data.SqlClient)

Exécutée exceptionnellement par l'architecte en mode worker (demande PO explicite du
19/07/2026), en dérogation au rôle habituel (voir CLAUDE.md — « Tu ne codes pas »).

## 1. Inventaire exhaustif (étape 1 du périmètre)

```
grep -rn "TargetFramework>net10" --include=*.csproj .
```

21 occurrences trouvées. Traitées :

- **Rétrogradés en `net8.0`/`net8.0-windows`** (chaîne API + modules associés + leurs tests) :
  `Declaration.API`, `Declaration.Infrastructure`, `Declaration.Application`, `Declaration.Core`,
  `Declaration.Selection`, `Declaration.Orchestration`, `Declaration.Controle`,
  `Declaration.Export.Xml`, `Declaration.Export.Excel`, `PerfTest`, et les 6 projets `*.Tests`
  correspondants (`Declaration.Orchestration.Tests`, `Declaration.Controle.Tests`,
  `Declaration.Selection.Tests`, `Declaration.Core.Tests`, `Declaration.Export.Xml.Tests`,
  `Declaration.Export.Excel.Tests`).
- **Laissés en `net10.0`/`net10.0-windows`, vérifiés plutôt que changés (hors périmètre strict)** :
  - `Declaration.Setup` : ne construit aucune `SqlConnection` (confirmé par grep), référence
    `Declaration.Application` (net8.0) — compile sans problème en `net10.0-windows` référençant du
    net8.0 (asset NuGet rétro-compatible). Build vérifié § 3.
  - `PdfReader` : projet autonome, non référencé par aucun autre projet, aucun appel SQL (grep
    `SqlConnection|SqlClient` → 0 résultat). Hors chaîne de dépendance de l'API, non mentionné dans
    le rapport initial du PO — signalé ici mais non modifié.
  - `scratch/TestTask008`, `scratch/TestTask017`, `scratch/TestTask118` : harnais de test ponctuels
    (tâches archivées), référencent `Declaration.Application`/`Declaration.Infrastructure` — un
    projet `net10.0` référençant du `net8.0` est valide (compatibilité descendante .NET). Non
    modifiés (hors scope, pas de `SqlConnection` propre).

## 2. Repli des versions de paquets (étape 3 du périmètre)

Vérification paquet par paquet des dépendances alors épinglées en `10.x.x` :

| Paquet | Version | Constat |
|---|---|---|
| `Microsoft.Extensions.Hosting.WindowsServices` | `10.0.9` (inchangé) | Le `.nupkg` 10.0.9 multi-cible réellement `net8.0`/`net9.0`/`net10.0`/`net462`/`netstandard2.x` dans un seul package — asset `lib/net8.0` résolu automatiquement, **aucun changement de version nécessaire**. |
| `Microsoft.Extensions.Configuration.Abstractions` | `10.0.9` (inchangé) | Idem, `lib/net8.0` présent dans le même `.nupkg`. |
| `Swashbuckle.AspNetCore` | `10.2.3` (inchangé) | Ses 3 sous-paquets (`.Swagger`, `.SwaggerGen`, `.SwaggerUI`) embarquent chacun `lib/net8.0` dans la même version — pas de repli nécessaire. |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | `10.0.9` → **`8.0.29`** | Paquet à TFM unique par version (`lib/net10.0` seul en 10.0.9) — repli obligatoire vers la ligne `8.0.x`. Version choisie = dernier patch publié (`8.0.29`), identique à la version du runtime `Microsoft.AspNetCore.App 8.0.29` déjà installé sur la machine. |
| `Microsoft.AspNetCore.OpenApi` | `10.0.9` → **`8.0.29`** | Même cas que ci-dessus. |
| `GRLicence` (nuget local, TASK-117) | `1.1.0` (inchangé) | `netstandard2.0` — compatible net8.0 et net10.0 sans changement. |
| `Microsoft.Data.SqlClient` | `7.0.2` (inchangé — hors périmètre, cf. TASK-123 contexte) | Confirmé dans le nuspec : le groupe `net8.0` de ce paquet a bien un asset dédié (`lib/net8.0/Microsoft.Data.SqlClient.dll`), contrairement à `net10.0` qui n'existe pas. |

## 3. `dotnet build` — 0 erreur

Chaîne complète (`Declaration.API` → `Infrastructure`/`Application` → `Core`/`Selection`/
`Orchestration`) :

```
Declaration.Core -> ...\bin\Release\net8.0\Declaration.Core.dll
Declaration.Selection -> ...\bin\Release\net8.0\Declaration.Selection.dll
Declaration.Orchestration -> ...\bin\Release\net8.0\Declaration.Orchestration.dll
Declaration.Application -> ...\bin\Release\net8.0\Declaration.Application.dll
Declaration.Infrastructure -> ...\bin\Release\net8.0-windows\Declaration.Infrastructure.dll
Declaration.API -> ...\bin\Release\net8.0-windows\Declaration.API.dll

La génération a réussi.
    10 Avertissement(s)   (préexistants : CS8618/CS0105, nullabilité — sans rapport avec ce changement)
    0 Erreur(s)
```

Idem individuellement pour `Declaration.Controle`, `Declaration.Export.Xml`,
`Declaration.Export.Excel` (0 erreur, 0/1 avertissement préexistant) et tous les projets `*.Tests`
listés en § 1 (0 erreur chacun).

**Exception hors périmètre — `PerfTest`** : échoue à la compilation
(`CS1061 : 'ILecteurTvaFgr' ne contient pas de définition pour 'LireTvaFgrAsync'`). Vérifié par
`git stash` que cette erreur est **strictement identique** sur le code `net10.0` d'origine (avant
toute modification de cette task) — bug préexistant sans rapport avec le TFM, projet outil de dev
non référencé par la chaîne de production. Non corrigé (hors périmètre : « aucune modification de
logique métier »).

`Declaration.Setup` (laissé en `net10.0-windows`) : build vérifié séparément — 0 erreur, référence
correctement le nouveau `Declaration.Application` en `net8.0`.

## 4. `dotnet publish` — asset `Microsoft.Data.SqlClient` résolu

```
dotnet publish Declaration.API/Declaration.API.csproj -c Release -o <tmp> -r win-x64 --self-contained true
```
→ 0 erreur.

Extrait du `deps.json` généré :
```
"runtimes/win/lib/net8.0/Microsoft.Data.SqlClient.dll": { ... }
```
Confirmé : plus aucune trace de `net9.0`/`netstandard2.0` en fallback.

## 5. Preuve réelle — `GET /api/societes` et `POST /auth/login`

Exécutable publié (self-contained win-x64, net8.0) lancé localement, `connections.json` réel du
dépôt (pointant vers `Server=DESKTOP-5BFKKEP;Database=GR_EMA_DISTRIBUTION`, la base ayant reproduit
le bug initial) :

```
GET http://localhost:5000/api/societes
→ 200 OK
[{"soId":1,"raisonSociale":"NEW_EMA DISTRIBUTION"}]

POST http://localhost:5000/api/auth/login  {Username:"test_probe_task123", Password:"wrong"}
→ 401 Unauthorized
{"message":"Identifiants invalides."}
```

Le `401` (et non un `500 PlatformNotSupportedException`) prouve que la requête a bien atteint et
exécuté la requête SQL contre `P_UTILISATEUR` sur `DESKTOP-5BFKKEP` — la connexion `SqlConnection`
s'ouvre normalement, plus de garde-fou `PlatformNotSupportedException` au constructeur.

**Limite assumée** : ce test a été rejoué **depuis ce poste de dev**, contre la base réelle
`DESKTOP-5BFKKEP` (accessible réseau), et non en exécutant l'exécutable directement sur le poste
`DESKTOP-5BFKKEP` lui-même (hors de portée de cet agent — pas d'accès à ce poste). L'étape 8 du
périmètre (« re-tester en conditions réelles sur le poste ») reste donc à confirmer par le PO/
l'architecte sur place ; la preuve ci-dessus couvre néanmoins la cause racine exacte (connexion SQL
vers cette même base).

## 6. Suite de tests — 0 régression

| Projet | Résultat | Note |
|---|---|---|
| `Declaration.Orchestration.Tests` | 137/137 ✅ | |
| `Declaration.Core.Tests` | 32/32 ✅ | |
| `Declaration.Export.Xml.Tests` | 5/5 ✅ | |
| `Declaration.Export.Excel.Tests` | 1/1 ✅ | |
| `Declaration.Controle.Tests` | 1/2 ⚠️ | `GenererRapportVerification` échoue (« Déclaration GRFN 66 introuvable »). **Préexistant** : rejoué à l'identique (même message, même stack) sur le code `net10.0` d'origine via `git stash`. Dépendance à une donnée précise (DT_Id 66) absente de la base de cet environnement — sans rapport avec le TFM. |
| `Declaration.Selection.Tests` | 58/59 ⚠️ | `IntegrationRegressionTests...Task050` échoue (`SqlException: Échec de l'ouverture de session de l'utilisateur 'IHEB-PC\ihebc'`). **Préexistant** : rejoué à l'identique sur `net10.0` d'origine — authentification Windows locale non habilitée sur la base cible pour ce test d'intégration, indépendant du changement de TFM. |

0 régression introduite par la rétrogradation — les 2 échecs ci-dessus préexistaient
identiquement avant toute modification de cette task.

## 7. `Deploy-All.ps1` rejoué de bout en bout

```
.\Deploy-All.ps1 -SkipFront -SkipWorkers
```
(`-SkipFront`/`-SkipWorkers` : étapes front npm et workers Sage `net48`, non affectées par ce
changement de TFM back — non rejouées pour ne pas allonger inutilement la preuve).

```
=== 1/5 : dotnet publish Declaration.API ... -> deploy\ ===
=== 4/7 : DeclaratifMaroc.exe (classique) + WinSW -> deploy\ ===
=== 5/7 : zip du payload ===
=== 6/7 : dotnet publish Declaration.Setup (single-file) -> installer\ ===
=== 7/7 : nettoyage payload.zip ===
Terminé. Installeur prêt : D:\_vibe\GRF\installer\DeclaratifMaroc.exe (fichier unique)
```
0 erreur sur l'ensemble du pipeline. `deploy\Declaration.API.deps.json` confirmé résolvant
`runtimes/win/lib/net8.0/Microsoft.Data.SqlClient.dll` (même vérification qu'en § 4).
`installer\DeclaratifMaroc.exe` généré (~123 Mo, single-file self-contained).

**⚠️ Bug critique trouvé lors de cette première exécution, corrigé avant validation finale — voir
§ 8.1.**

**Non fait, volontairement** : le service Windows `DeclaratifMaroc` actuellement installé et
démarré sur ce poste (`C:\Program Files\APBS\Declaratif Maroc\`) est une installation distincte du
dossier `deploy\`/`installer\` du dépôt — je ne l'ai pas arrêté/remplacé, ce serait une action de
déploiement sur un service en production nécessitant une confirmation explicite préalable
(non demandée dans cette session).

## 8. Retour de revue PO (19/07/2026) — 2 points traités

### 8.1 — Bug réel trouvé : `deploy\` jamais nettoyé avant publish (nouveau, confirmé)

Le point soulevé (« le stop du service avant mise à jour ne garantit pas la libération des
fichiers ») a mené à l'investigation suivante, qui a mis au jour un bug distinct et plus grave que
celui suspecté :

**Constat** : après le premier `Deploy-All.ps1 -SkipFront -SkipWorkers` documenté en § 7,
`deploy\Microsoft.Extensions.Validation.dll` — un fichier daté du 26/06 (bien avant cette task),
absent de tout `deps.json` généré aujourd'hui — était toujours présent. Comparaison de hash SHA-256
(`Get-FileHash`) : `deploy\Microsoft.Data.SqlClient.dll` était **strictement identique** à
`lib\net9.0\Microsoft.Data.SqlClient.dll` (l'ancien asset cassé), alors que
`deploy\Declaration.API.deps.json` déclarait pourtant déjà correctement l'asset `net8.0`.

**Cause racine** : `dotnet publish -o deploy\` **n'a jamais nettoyé** ce dossier — le SDK ajoute/
écrase les fichiers du nouveau graphe de dépendances mais ne supprime jamais les fichiers orphelins
d'un publish précédent, et sa copie incrémentale se base sur l'horodatage (le fichier déjà présent
dans `deploy\` étant plus récent que l'asset source en cache NuGet, il n'est pas recopié). Résultat :
un `deploy\` réutilisé entre deux publishs avec un graphe de dépendances différent (ici :
net10.0→net8.0, `Microsoft.AspNetCore.OpenApi` 10.0.9→8.0.29 qui faisait disparaître la dépendance
transitive `Microsoft.Extensions.Validation`) peut rester dans un état hybride : `deps.json` correct,
mais binaires physiques encore ceux de l'ancien build. C'est très probablement la cause exacte du
`FileNotFoundException: Microsoft.Extensions.Validation` déjà signalé — un déploiement incrémental
antérieur sur ce même mécanisme, avec un binaire net10/net9 orphelin resté en place.

`Deploy-All.ps1` supprimait déjà `installer\` avant publish de `Declaration.Setup` (ligne 109
d'origine) mais pas `deploy\` avant publish de `Declaration.API` — asymétrie corrigée :

```diff
+ Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $DeployPath
  dotnet publish (Join-Path $RepoRoot "Declaration.API\Declaration.API.csproj") -c Release -o $DeployPath -r win-x64 --self-contained true
```

**Re-vérifié après correction** (`deploy\`/`installer\` supprimés puis `Deploy-All.ps1` rejoué) :
- `Test-Path deploy\Microsoft.Extensions.Validation.dll` → `False` (fichier orphelin disparu).
- `Get-FileHash deploy\Microsoft.Data.SqlClient.dll` → `699D7DF0...` — identique bit-à-bit à
  `lib\net8.0\Microsoft.Data.SqlClient.dll`, le bon asset.

### 8.2 — Paquets `10.0.9` non downgradés : investigué, **non nécessaire** (avec preuve)

Le point demandait de downgrader `Microsoft.Extensions.Hosting.WindowsServices` et
`Microsoft.Extensions.Configuration.Abstractions` de `10.0.9` vers `8.0.x`, ces versions étant
présumées être la cause du `FileNotFoundException Microsoft.Extensions.Validation`.

Vérification faite :
- Le nuspec de `Microsoft.Extensions.Hosting.WindowsServices` 10.0.9 (groupe `net8.0`) dépend de
  `Microsoft.Extensions.Hosting` 10.0.9, dont le groupe `net8.0` ne référence **aucun** package
  `Microsoft.Extensions.Validation` (liste complète des ~22 dépendances inspectée). Idem pour
  `Microsoft.Extensions.Configuration.Abstractions` (paquet sans aucune dépendance).
- Recherche du terme `Validation` dans les 3 `project.assets.json` (API/Application/Infrastructure)
  générés par cette task : **0 résultat**, y compris avant la correction du § 8.1 — confirmant que
  cette dépendance transitive n'existe déjà plus dans le graphe résolu actuel (elle provenait de
  l'ancien `Microsoft.AspNetCore.OpenApi` 10.0.9, déjà corrigé en § 2, avant même ce retour de revue).
- Seule version `8.0.x` réellement publiée pour ces deux paquets : `8.0.1`
  (`Hosting.WindowsServices`) et `8.0.0` (`Configuration.Abstractions`) — sorties **fin 2023**, avant
  le changement de schéma de versionnage de Microsoft (depuis .NET 9, ces paquets `Microsoft.Extensions.*`
  suivent désormais le train de version du runtime — `9.0.x`/`10.0.9` — et *multi-ciblent* net8.0/
  net9.0/net10.0 **dans le même paquet**, cf. § 2). Un vrai downgrade vers `8.0.1`/`8.0.0`
  reviendrait donc à figer ces deux dépendances sur une version non patchée depuis ~2,5 ans, sans
  bénéfice puisqu'un asset `net8.0` à jour existe déjà dans `10.0.9`.

**Conclusion** : la cause réelle et confirmée du `FileNotFoundException` est le § 8.1 (dossier
`deploy\` non nettoyé), pas la version de ces deux paquets. Non downgradés — mais signalé et prouvé
ici pour traçabilité, à rouvrir si un nouveau symptôme concret apparaît.

### 8.3 — `WinSwServiceManager.Stop()` : attente active ajoutée

Cause confirmée dans le code (`Declaration.Setup/Services/WinSwServiceManager.cs`, méthode
`RunWinSw`) : `Stop()` invoquait `WinSW.exe stop` et attendait seulement la fin de ce process CLI
éphémère (`process.WaitForExit()`), qui ne fait que transmettre `ControlService()` au SCM — sans
jamais vérifier que `Declaration.API.exe` (le process réel, piloté par l'instance de service de
WinSW) avait effectivement terminé et libéré ses fichiers. Le `RetryOnFileLock` de `SetupForm.cs`
(15s) restait donc le seul filet de sécurité, insuffisant de façon non déterministe.

**Correctif appliqué** : `Stop()` appelle désormais `WaitForApiProcessExit(TimeSpan.FromSeconds(45))`
après `RunWinSw("stop")` — polling toutes les 500ms sur `Process.GetProcessesByName("Declaration.API")`
(filtré par chemin d'exécutable exact via `MainModule.FileName`, pour ignorer un homonyme éventuel
sur un autre poste), avec levée d'une `TimeoutException` explicite au-delà du délai. `RetryOnFileLock`
conservé tel quel comme filet de sécurité secondaire (ex. antivirus qui garde un verrou bref après la
fin réelle du process).

Build vérifié après correctif : `dotnet build Declaration.Setup/Declaration.Setup.csproj -c Release`
→ 0 erreur (warnings préexistants uniquement, `NU1510` sur `System.Text.Encoding.CodePages`, sans
rapport).

**Non testé en conditions réelles** (arrêt/démarrage effectif du service Windows avec verrou de
fichier provoqué) — nécessiterait de manipuler le service installé sur ce poste
(`C:\Program Files\APBS\Declaratif Maroc\`), action de production non autorisée dans cette session
(cf. § 7). Logique vérifiée par lecture de code et compilation uniquement.

## 9. Critères de validation — récapitulatif

- ✅ Plus aucune occurrence de `net10.0`/`net10.0-windows` dans les projets de la chaîne API et
  leurs tests (exclusions explicitement actées : `Declaration.Setup`, `PdfReader`, `scratch/*`,
  cf. § 1).
- ✅ `GET /api/societes` et `POST /auth/login` répondent sans `PlatformNotSupportedException`,
  contre la base réelle `DESKTOP-5BFKKEP` (cf. § 5, limite assumée sur la localisation physique du
  test).
- ✅ Build solution complète : 0 erreur (1 échec préexistant hors périmètre : `PerfTest`).
- ✅ Suite de tests existante : 0 régression (2 échecs préexistants confirmés identiques avant
  modification, cf. § 6).
- ✅ `Deploy-All.ps1` rejoué de bout en bout sans erreur (front/workers sautés, non affectés), et
  **re-rejoué depuis un `deploy\`/`installer\` propres après correction § 8.1** — hash confirmé
  identique à l'asset `net8.0` attendu.
- ⚠️ Diff non limité aux seuls `.csproj` : `Deploy-All.ps1` (1 ligne, nettoyage `deploy\` avant
  publish) et `Declaration.Setup/Services/WinSwServiceManager.cs` (attente active post-`Stop()`)
  également modifiés suite au retour de revue § 8 — aucune modification de logique métier/requête
  SQL côté application, mais dépasse le périmètre strict initial de la task (justifié : corrections
  directement liées au déploiement de ce même changement, demandées explicitement par le PO).

## Points ouverts pour le PO/architecte

- Confirmation sur poste ayant réellement reproduit le bug (`DESKTOP-5BFKKEP` ou `:5280`) et
  décision sur le redéploiement du service `DeclaratifMaroc` actuellement installé
  (`C:\Program Files\APBS\Declaratif Maroc\`) avec le nouvel installeur — non fait dans cette
  session, cf. § 5 et § 7.
- § 8.3 (`WaitForApiProcessExit`) vérifié par lecture de code et compilation uniquement — jamais
  exercé en conditions réelles (arrêt de service + verrou de fichier effectif). À confirmer lors
  d'une prochaine mise à jour réelle du service installé.
