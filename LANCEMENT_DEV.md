# LANCEMENT_DEV.md — Déclaration TVA (GRF)

Guide de lancement en **mode développement**. Commandes simples, à copier-coller dans PowerShell.

---

## Déploiement en un clic — `Deploy-All.ps1`

Construit tout (API + front + workers Sage + installeur) et produit
**`installer\DeclaratifMaroc.exe`**, le seul fichier à livrer au client.

```powershell
cd d:\_vibe\GRF
pwsh .\Deploy-All.ps1
```

- **Utiliser `pwsh`** (PowerShell 7+), pas `powershell.exe` (Windows PowerShell 5.1) : le script
  contient des accents/tirets en UTF-8 sans BOM que `powershell.exe` lit avec la mauvaise page de
  code et qui font échouer le parsing (`TerminatorExpectedAtEndOfString`). `pwsh` lit l'UTF-8 par
  défaut, aucun problème.
- Détail des étapes et du contenu produit : voir l'en-tête `.SYNOPSIS` du script, ou la section
  « Déploiement : UN SEUL DOSSIER + service Windows » plus bas dans ce guide.

---

## Fichier de connexion — UN SEUL, partagé

**`D:\_vibe\GRF\connections.json`** = la seule source de la connexion pour tout le système.
On l'édite **une seule fois** ; il est copié automatiquement à côté de chaque exe au build/publish.

```json
{
  "ConnectionStrings": {
    "GrfConnection": "Server=...;Database=GR_EMA_DISTRIBUTION;User Id=...;Password=...;TrustServerCertificate=True;",
    "SageConnection": "Server=...;Database=BASE_SAGE;User Id=...;Password=...;TrustServerCertificate=True;",
    "PersistenceConnection": "Server=...;Database=GR_EMA_DISTRIBUTION;User Id=...;Password=...;TrustServerCertificate=True;"
  },
  "JwtSettings": { "SecretKey": "<cle reelle, generee, distincte de celle du depot>" },
  "WorkerConfig": { "WorkerExePath": "SageTaxReader.Console.exe" }
}
```

### Connexion Sage dynamique par société (TASK-118, 18/07/2026)

Depuis TASK-118, `ConnectionStrings.SageConnection` et la section `SageOM` ci-dessus **ne sont
plus lues** par `Declaration.API`. Depuis TASK-119, `DeclaratifMaroc.exe` (setup) ne les édite
plus non plus — ces clés sont devenues entièrement obsolètes, elles peuvent rester silencieusement
absentes/vides de `connections.json` sans impact. `GrfConnection`/`PersistenceConnection` restent
des valeurs **uniques** (exigence PO explicite).

La connexion Sage est désormais résolue **dynamiquement par `SO_Id`** à chaque traitement :
- `Server`/`User`/`Password` : toujours ceux de `GrfConnection`.
- `Database` : `P_SOCIETE.SO_ErpDb` pour ce `SO_Id`.
- Identifiants Sage OM (`WorkerConfig.User`/`Password`) : `P_SOCIETE.SO_ErpUserApp`/`SO_ErpPasswdApp`.

Échec explicite (exception, jamais de repli silencieux) si le `SO_Id` traité est introuvable
dans `P_SOCIETE` ou si `SO_ErpDb` est vide/NULL. Une seule ligne `P_SOCIETE` (mono-Sage) reproduit
exactement le comportement historique — aucune régression tant qu'un seul `SO_Id` est configuré.

### Clé JWT de production (TASK-114)

`Declaration.API/appsettings.json` contient une clé de signature JWT **de développement**,
committée en clair dans le dépôt — elle est donc **publique** et ne doit jamais signer un JWT
en production. `connections.json` (le fichier édité en prod, chargé **après** `appsettings.json`
donc prioritaire) accepte une section `JwtSettings.SecretKey` qui la remplace, sans dupliquer la
configuration de `Program.cs`.

**Avant tout lancement en production**, générer une clé aléatoire (32+ caractères) et la placer
dans `connections.json` :

```powershell
-join ((48..57)+(65..90)+(97..122) | Get-Random -Count 48 | % { [char]$_ })
```

**Garde-fou** : si l'application démarre en environnement `Production` (c'est la valeur par
défaut de `ASPNETCORE_ENVIRONMENT` quand elle n'est pas positionnée — donc le cas normal du
service Windows créé par `sc.exe`) et que `JwtSettings:SecretKey` vaut encore une des clés de dev
committées, le démarrage échoue immédiatement (`InvalidOperationException`) plutôt que de signer
silencieusement avec une clé connue de tous.

- Colonnes : `Id` en `NVARCHAR`, **dates en `DATETIME2`**, énums en `INT`, montants en `FLOAT`.
- **Toutes les bases sont SQL Server** — y compris la persistance (`PersistenceConnection`),
  qui pointe vers la base GRF où vivent les tables `DeclarationEntete` et `LigneCandidate`.
- **Declaration.API** lit ce fichier au démarrage — il **écrase** `appsettings.json`.
- Le **reader Sage** (`SageTaxReader.Console`) ne lit rien : l'API lui **transmet** les identifiants Sage
  en arguments. Un seul fichier gouverne donc toute la chaîne.
- `WorkerExePath` doit pointer vers l'exécutable **de la version Sage réelle du client** — voir
  § « A2. Worker Sage — choisir la version » ci-dessous (TASK-101).
- **Frontend** : `declaration-tva-web/.env.development` → `VITE_API_BASE=/api` (convention Vite, comme GRC_WEB).

### Créer les tables + compte SQL applicatif (une seule fois, prod)

Les tables ne sont **pas** créées par l'application. `DeclarationTVA.sql` (racine) est un
script **unique et idempotent** : il fixe lui-même son contexte de base via `USE` (pas besoin de
`-d`), crée/met à jour le schéma complet (tables de persistance, cache Sage, triggers
d'immuabilité TASK-064) **et** le login/compte SQL applicatif à moindre privilège (TASK-114).

**Avant exécution** : personnaliser en tête de fichier `GR_EMA_DISTRIBUTION`/`BASE_SAGE` (noms
réels chez le client) et `decl_tva_app`/le mot de passe du compte applicatif.

```powershell
sqlcmd -S SERVEUR_SQL -U UTILISATEUR -P MOT_DE_PASSE -i DeclarationTVA.sql
```

Reporter ensuite le login/mot de passe créés dans `connections.json` (`User Id`/`Password` de
`GrfConnection`, `SageConnection`, `PersistenceConnection`).

> En **dev**, la sélection GRF/Sage tourne sur des **fixtures** (pas besoin des vraies bases
> GRF/Sage), mais la persistance écrit bien dans SQL Server → les tables doivent exister.

---

## Prérequis (une seule fois)

```powershell
# .NET 10 SDK
dotnet --version              # doit afficher 10.x

# Node.js (pour le front)
node --version                # 18+ recommandé
```

---

## Lancer en DEV

Tu peux lancer les deux morceaux **indépendamment** (le front tourne en mock).

### 1. Backend (API .NET)

```powershell
cd d:\_vibe\GRF\Declaration.API
dotnet run
```

- API : **http://localhost:5000** (port gouverné par `connections.json → ServerConfig.Port`,
  cf. TASK-115 — `Program.cs` force `UseUrls` avec cette valeur, qui écrase l'`applicationUrl`
  de `launchSettings.json`)
- Swagger (test des endpoints) : **http://localhost:5000/swagger**
- En dev → mode **fixtures** automatique : aucune connexion GRF/Sage requise.

### 2. Frontend (React / Vite)

Dans un **second terminal** :

```powershell
cd d:\_vibe\GRF\declaration-tva-web
npm install        # une seule fois
npm run dev
```

- Front : **http://localhost:5173**
- Le front est en **mock complet** (`axios-mock-adapter`) → il fonctionne **sans backend**.

> Arrêter un serveur : `Ctrl + C` dans son terminal.

---

## Vérifier que tout marche

```powershell
# Backend répond
Invoke-RestMethod "http://localhost:5000/swagger/index.html" | Out-Null; "API OK"
```

Ouvrir **http://localhost:5173** → l'écran de déclaration TVA doit s'afficher.

---

## Déploiement : UN SEUL DOSSIER + service Windows

> Résumé aligné sur `GRC_WEB\DEPLOY.md`. À faire quand le dev est validé.

### A. Builder (sur ton PC de dev)

```powershell
# 1. Publier l'API dans un dossier deploy\ (self-contained : le poste client n'a pas forcément
#    le runtime .NET 10 partagé installé — WinSW invoque Declaration.API.exe directement)
cd d:\_vibe\GRF
dotnet publish Declaration.API\Declaration.API.csproj -c Release -o deploy -r win-x64 --self-contained true

# 2. Builder le front et copier dans deploy\wwwroot\
cd declaration-tva-web
npm run build
New-Item -ItemType Directory -Force ..\deploy\wwwroot | Out-Null
Copy-Item dist\* ..\deploy\wwwroot\ -Recurse -Force
cd ..
```

> `deploy\` = le seul dossier à livrer.

### A2. Worker Sage — choisir la version (TASK-101)

Le parc client utilise plusieurs versions de Sage 100 (v7/v9/v10/v11/v12 identifiées à ce jour ;
v8 prévue mais aucun DLL interop v8 fourni par le PO pour l'instant). `SageTaxReader.Console`
est compilé en **liaison précoce** contre `Interop.Objets100cLib.dll` (`EmbedInteropTypes`) : il
faut donc **une variante d'exécutable par version Sage**, chacune produite à part — on ne peut
pas se contenter de remplacer le DLL sur le poste cible après coup.

```powershell
cd d:\_vibe\GRF
.\publish-sagetaxreader-workers.ps1
```

Produit dans `deploy\workers\<version>\` un dossier autonome par variante (exe + `SageTaxReader.Core.dll`
propre à cette version + dépendances) :

| Dossier | Couvre |
|---|---|
| `deploy\workers\v7\` | Sage 100 v7 |
| `deploy\workers\v9\` | Sage 100 v9 |
| `deploy\workers\v10\` | Sage 100 v10 **et** v11 (DLL interop confirmés bit-à-bit identiques, SHA256 égal) |
| `deploy\workers\v12\` | Sage 100 v12 (référence historique, comportement par défaut inchangé) |

**Chez CE client**, copier uniquement le dossier de sa version à côté de `Declaration.API.exe`,
puis pointer `WorkerConfig.WorkerExePath` (dans `connections.json`) vers l'exe qu'il contient —
ex. `C:\declaration-tva\workers\v10\SageTaxReader.Console.v10.exe`. Aucun autre champ de
configuration n'est nécessaire.

⚠️ Prérequis **hors build** : le composant COM natif Sage de la version cible (ex. `objets100c.dll`
v10.10) doit être **installé et enregistré** sur la machine qui exécute le worker — remplacer
seulement l'assembly interop managé ne suffit pas (`REGDB_E_CLASSNOTREG` sinon, cf. TASK-101).

### B/C/D/E. Installer ou mettre à jour chez le client — `DeclaratifMaroc.exe` (TASK-115/TASK-119)

Les étapes manuelles historiques (copie à la main, `notepad connections.json`, `sc.exe
create`/`start`/`stop`) sont remplacées par un **unique exécutable GUI**, livré dans `deploy\`
aux côtés de l'API/worker/front : **`DeclaratifMaroc.exe`** (ex-`Declaration.Setup.exe`, renommé
TASK-119). Détail complet (détection install/mise à jour, champs du formulaire, gestion du
service via WinSW) : voir `DOCS/DEPLOIEMENT.md`.

Résumé :

```powershell
# Depuis le dossier deploy\ (ou une copie de ce dossier sur le poste client)
.\DeclaratifMaroc.exe
```

1. Choisir/confirmer le **dossier cible** — le setup détecte automatiquement **installation**
   (dossier vide) vs **mise à jour** (un `connections.json` y existe déjà déjà) et pré-remplit le
   formulaire en conséquence (jamais les mots de passe, toujours vides à la relecture).
2. Renseigner connexions SQL (GRF/Persistance — Sage n'est plus saisi ici depuis TASK-119, résolu
   par société via `SO_Id`/TASK-118), secret JWT (bouton « Générer aléatoirement »),
   **port d'écoute**, version Sage installée, licence ApLicence (TASK-117).
3. Valider : le setup écrit `connections.json`, copie les binaires (préservés en mode mise à
   jour), installe/démarre le service Windows **`DeclaratifMaroc`** via WinSW-x64 (redémarrage
   auto sur crash, logs redirigés — remplace `sc.exe` brut).

Ouvrir **http://localhost:\<port choisi au formulaire\>** (5000 par défaut).

> ⚠️ Un service `DeclarationTVA` créé via l'ancienne procédure `sc.exe` (avant TASK-115) n'est
> **pas** automatiquement renommé/migré par ce setup — question distincte, à cadrer avec le PO
> si une telle installation est déjà en prod (cf. TASK-115 §Risques).

---

## Erreurs fréquentes

| Erreur | Cause | Correction |
|---|---|---|
| Le service démarre puis s'arrête | Runtime .NET 10 absent, chaîne SQL invalide, ou `JwtSettings:SecretKey` encore égal à la clé de dev (garde-fou TASK-114) | `dotnet --list-runtimes` ; vérifier `connections.json` (`ConnectionStrings` + `JwtSettings.SecretKey`) |
| `Login failed for user` | Droits SQL insuffisants | Exécuter `DeclarationTVA.sql` (crée le compte `decl_tva_app` à moindre privilège, TASK-114) puis reporter son login/mot de passe dans `connections.json` |
| Page blanche | `wwwroot\` vide | Refaire l'étape A.2 (`npm run build` + copie) |
| Port déjà utilisé | Autre process sur le port | Le setup GUI (TASK-115) bloque avec un message clair avant toute installation ; sinon changer `ServerConfig.Port` dans `connections.json` |
| `npm run dev` échoue | Dépendances manquantes | `npm install` dans `declaration-tva-web` |
