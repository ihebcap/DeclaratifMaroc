# TASK-044 — Déploiement mono-service / mono-dossier (API unique orchestrant tout)

## Contexte
Décision PO (10/07/2026) : **un seul service Windows** — l'API `Declaration.API` — qui **appelle
tout** (y compris le worker OM out-of-process) et **sert le front**, le tout dans **un seul dossier
de déploiement**. Pas de service séparé pour le worker, pas d'hébergement front distinct.

Le socle est déjà en place (livré en marge de la traçabilité, cf. mémoire
`grf-valorisation-tracabilite-blocage-om`) :
- Le front est déjà servi par l'API via `wwwroot` (`app.UseStaticFiles()`).
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
