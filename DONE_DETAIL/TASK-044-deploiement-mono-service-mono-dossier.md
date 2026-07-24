# TASK-044 — Déploiement mono-service / mono-dossier (API unique orchestrant tout)

## ⚠️ Clôture exceptionnelle par décision PO (23/07/2026) — sans VERIFY complet

**Le PO a explicitement demandé de clôturer cette TASK en assumant le risque**, après que
l'architecte a signalé un dossier de preuve incomplet et refusé une approbation normale (aucun
`VERIFY/TASK-044_verify.md` n'a jamais été produit). Décision tracée telle quelle, sans
maquillage : ceci n'est **pas** une clôture avec preuve de fonctionnement réelle.

**Vérifié réellement par l'architecte avant clôture (code source, lecture directe)** :
- `Declaration.API/Program.cs:128-129,162` : `UseDefaultFiles()` + `UseStaticFiles()` (bon ordre)
  + `MapFallbackToFile("index.html")` — le bug 404 sur `GET /` documenté ci-dessous est corrigé
  (déjà livré sous TASK-116).
- `Declaration.Application/Services/DeclarationWorkflowService.cs:739-743` : `WorkerExePath`
  résolu en relatif (`Path.IsPathRooted` + `Path.Combine(AppContext.BaseDirectory, ...)`).
- `Declaration.Application/Services/DeclarationWorkflowService.cs:132-136` : `logs/valorisation.log`
  écrit relatif à `AppContext.BaseDirectory`.
- `Deploy-All.ps1` (racine repo) existe et enchaîne build front + `dotnet publish` API (self-contained
  win-x64) + workers Sage + `Publish-Setup.ps1` (WinSW/DeclaratifMaroc.exe) — couvre en pratique le
  script de publication demandé par cette TASK, fusionné avec TASK-115 par décision PO du 18/07/2026
  (cf. son propre docstring).

**Non vérifié / non livré — assumé explicitement par le PO en clôturant quand même** :
- `connections.json.exemple` (gabarit de déploiement sans secret) : **absent du dépôt**.
- `DOCS/DEPLOIEMENT.md` : **non corrigé**, contient encore la mention obsolète *« TASK-044 n'a pas
  livré de `publish.ps1` unique à ce jour »*, contredisant l'existence de `Deploy-All.ps1`.
- **Aucune preuve runtime réelle** : pas de service installé et démarré hors arbo de build dans
  cette session, pas de `GET /`/`GET /api/...` rejoués en même origine, pas de trace confirmée d'un
  worker invoqué en relatif dans `logs/valorisation.log` sur une instance déployée.
- La TASK n'est jamais passée par `IN_PROGRESS/`, aucun `VERIFY/TASK-044_verify.md` n'a existé.

Voir `DONE.md` (entrée TASK-044, 23/07/2026) pour la trace de clôture et `DONE_DETAIL/TASK-044_verify.md`
pour le constat détaillé de ce qui n'a pas été vérifié.

---

## Contexte
Décision PO (10/07/2026) : **un seul service Windows** — l'API `Declaration.API` — qui **appelle
tout** (y compris le worker OM out-of-process) et **sert le front**, le tout dans **un seul dossier
de déploiement**. Pas de service séparé pour le worker, pas d'hébergement front distinct.

Le socle est déjà en place (livré en marge de la traçabilité, cf. mémoire
`grf-valorisation-tracabilite-blocage-om`) :
- Le front est déjà servi par l'API via `wwwroot` (`app.UseStaticFiles()`).
  ⚠️ **Bug constaté en production (17/07/2026)** : `GET /` renvoie 404. `Program.cs:98` appelle
  `UseStaticFiles()` seul, sans `UseDefaultFiles()` ni `MapFallbackToFile("index.html")` — la
  middleware ne mappe donc jamais `/` vers `wwwroot/index.html`, elle ne sert que les chemins de
  fichiers explicites. Jamais détecté avant car en dev le front tourne sur Vite (`:5173`), jamais
  via `wwwroot`. **À corriger avant toute installation prod** : remplacer la ligne par
  `app.UseDefaultFiles(); app.UseStaticFiles();` ou, plus robuste (couvre aussi un futur routage
  côté front), `app.MapFallbackToFile("index.html");` après `MapControllers()`. Cause aggravante
  constatée en parallèle : `wwwroot/` n'existait même pas dans `deploy/` — l'étape A.2
  (`npm run build` + copie du `dist/`) n'avait pas été faite ; à ne pas confondre avec le bug
  ci-dessus (les deux causent un 404, indépendamment l'une de l'autre).
- Le worker `SageTaxReader.Console.exe` (net48) est lancé par `WorkerInvoker` via `Process.Start`.
- `WorkerExePath` est désormais **résolu relatif à l'exe** (défaut `SageTaxReader.Console.exe`) :
  `if (!Path.IsPathRooted(workerExe)) workerExe = Path.Combine(AppContext.BaseDirectory, workerExe)`.
- `connections.json` est copié à côté de l'exe et chargé via `AddJsonFile(reloadOnChange:true)`.
- Les logs de valorisation s'écrivent dans `logs/valorisation.log` **à côté de l'exe** (visible en
  service, sans console).

Il reste à **industrialiser** l'assemblage en un dossier unique et l'installation en service.

## Périmètre STRICT
- **Inclus** :
  1. **Procédure de publication** produisant un dossier `deploy/` unique contenant :
     - `Declaration.API.exe` + ses dépendances .NET 10 ;
     - `SageTaxReader.Console.exe` + **toutes** ses dépendances net48 (interop Sage COM incluses) ;
     - `wwwroot/` (build front `declaration-tva-web` : `npm run build` → copie du `dist/`) ;
     - `connections.json` (voir ci-dessous, **hors versionnement**, secret) ;
     - `logs/` (créé au runtime, mais présent/inscriptible).
  2. **`connections.json` de déploiement** : `WorkerExePath` en **relatif** (`SageTaxReader.Console.exe`)
     ou **absent** (défaut). Aucun chemin absolu dev (`D:\_vibe\...`).
  3. **Installation en service Windows** documentée (`sc create` ou `New-Service`), lancée sous un
     **compte ayant accès à Sage OM** (le worker a besoin de l'utilisateur applicatif Sage) et à
     `.\sql2022`. Démarrage auto, répertoire de travail = dossier deploy.
  4. **Script de publication reproductible** (ex. `publish.ps1`) enchaînant : build front → `dotnet
     publish` API → `dotnet build` worker net48 → copie dans `deploy/` → rappel de déposer
     `connections.json`.
  5. **Kestrel/URL** : port d'écoute fixé pour le service (aujourd'hui `:5005`), documenté ; le front
     servi en même origine (pas de CORS en prod puisque même hôte).
- **Exclu** :
  - Aucun changement de logique métier, aucune modification worker (c'est TASK-045).
  - Pas d'IIS / reverse-proxy imposé (option, pas obligation) : le service Kestrel auto-hébergé suffit.
  - Pas de conteneurisation (le worker COM Sage exige Windows + Sage installé localement).
  - Pas de secret en clair versionné : `connections.json` reste **hors dépôt**.

## Objectif
```
Entrée  : sources GRF (API .NET 10 + worker net48 + front Vite)
Étapes  : publish.ps1 → deploy/ mono-dossier → install service Windows (compte Sage)
Sortie  : 1 service Windows démarré ; http://<hote>:5005 sert le front + l'API ; le worker OM
          est lancé à la demande depuis le même dossier ; logs/valorisation.log inscriptible
```

## Étapes
1. **Script `publish.ps1`** (racine repo) : build front, `dotnet publish -c Release` API, build worker
   net48 en Release, assemblage `deploy/`.
2. **Gabarit `connections.json.exemple`** de déploiement (sans secret réel) : `WorkerExePath` relatif
   ou omis, connexions `.\sql2022`, section `SageOM` (utilisateur applicatif Sage). Documenter que le
   fichier réel est déposé à la main.
3. **Vérifier la résolution des chemins au runtime** depuis un dossier arbitraire (hors bin/Debug) :
   worker trouvé, `wwwroot` servi, `logs/` écrit — tous relatifs à `AppContext.BaseDirectory`.
4. **Doc d'install service** (`DOCS/DEPLOIEMENT.md`) : `New-Service`/`sc create`, compte de service,
   droits Sage + SQL, port, démarrage, désinstallation, emplacement des logs.
5. **Runbook de vérification** post-install (voir Livrables).

## Livrables
- `publish.ps1` + `deploy/` reproductible (arborescence documentée).
- `connections.json.exemple` (déploiement) + `DOCS/DEPLOIEMENT.md`.
- `VERIFY/TASK-044_verify.md` : preuve réelle —
  - le service démarre depuis un dossier **hors** arbo de build ;
  - `GET /` renvoie le front, `GET /api/...` répond (JWT) depuis la même origine ;
  - un `rafraichir-valorisation` déclenche le worker **trouvé en relatif** (log worker présent
    dans `logs/valorisation.log`) — indépendamment de TASK-045 (le worker est *invoqué*, même s'il
    rend « Valeur invalide ! ») ;
  - `connections.json` sans chemin absolu ; aucun secret versionné.

## Critères de validation
- **Un seul** service Windows ; le worker n'est **pas** un service séparé (lancé par l'API).
- **Un seul** dossier deploy autosuffisant ; aucun chemin absolu dev résiduel.
- Front + API servis en **même origine** ; logs visibles sans console.
- Compte de service disposant des droits **Sage OM** et **SQL** ; démarrage auto vérifié.
- Aucune régression métier ; aucune modification du worker.

## Risques / dépendances
- **Dépendances net48 du worker** : l'interop COM Sage doit être copiée intégralement dans `deploy/`
  (piège classique : DLL interop manquante → worker KO au runtime). À vérifier explicitement.
- **Compte de service** : un service sous `LocalSystem` peut ne **pas** voir la licence/session Sage
  OM. Prévoir un compte utilisateur applicatif Sage (mémoire `grf-valorisation-tracabilite-blocage-om`).
- **Indépendant de TASK-045** : ce déploiement fonctionne même tant que le worker rend « Valeur
  invalide ! » ; il ne masque pas ce blocage (la traçabilité le remonte). Ne pas coupler les deux.
- **Secret** : `connections.json` contient les identifiants Sage OM — hors dépôt, traité comme secret.
