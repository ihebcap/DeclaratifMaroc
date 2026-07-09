# LANCEMENT_DEV.md — Déclaration TVA (GRF)

Guide de lancement en **mode développement**. Commandes simples, à copier-coller dans PowerShell.

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
  "WorkerConfig": { "WorkerExePath": "SageTaxReader.Console.exe" }
}
```

- Colonnes : `Id` en `NVARCHAR`, **dates en `DATETIME2`**, énums en `INT`, montants en `FLOAT`.
- **Toutes les bases sont SQL Server** — y compris la persistance (`PersistenceConnection`),
  qui pointe vers la base GRF où vivent les tables `DeclarationEntete` et `LigneCandidate`.
- **Declaration.API** lit ce fichier au démarrage — il **écrase** `appsettings.json`.
- Le **reader Sage** (`SageTaxReader.Console`) ne lit rien : l'API lui **transmet** les identifiants Sage
  en arguments. Un seul fichier gouverne donc toute la chaîne.
- **Frontend** : `declaration-tva-web/.env.development` → `VITE_API_BASE=/api` (convention Vite, comme GRC_WEB).

### Créer les tables (une seule fois)

Les tables ne sont **pas** créées par l'application. Exécuter le script dans la base GRF :

```powershell
sqlcmd -S SERVEUR_SQL -d GR_EMA_DISTRIBUTION -U UTILISATEUR -P MOT_DE_PASSE -i DeclarationTVA.sql
```

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

- API : **http://localhost:5018**
- Swagger (test des endpoints) : **http://localhost:5018/swagger**
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
Invoke-RestMethod "http://localhost:5018/swagger/index.html" | Out-Null; "API OK"
```

Ouvrir **http://localhost:5173** → l'écran de déclaration TVA doit s'afficher.

---

## Déploiement : UN SEUL DOSSIER + service Windows

> Résumé aligné sur `GRC_WEB\DEPLOY.md`. À faire quand le dev est validé.

### A. Builder (sur ton PC de dev)

```powershell
# 1. Publier l'API dans un dossier deploy\
cd d:\_vibe\GRF
dotnet publish Declaration.API\Declaration.API.csproj -c Release -o deploy

# 2. Builder le front et copier dans deploy\wwwroot\
cd declaration-tva-web
npm run build
New-Item -ItemType Directory -Force ..\deploy\wwwroot | Out-Null
Copy-Item dist\* ..\deploy\wwwroot\ -Recurse -Force
cd ..
```

> `deploy\` = le seul dossier à livrer.

### B. Installer chez le client

```powershell
New-Item -ItemType Directory -Force "C:\declaration-tva"
Copy-Item "deploy\*" "C:\declaration-tva\" -Recurse -Force
```

### C. Configurer la connexion — SEUL fichier à éditer

```powershell
notepad "C:\declaration-tva\connections.json"
```

Renseigner `GrfConnection` et `SageConnection` (Server / Database / User Id / Password).

> L'exe lit `connections.json` placé dans son dossier au démarrage. Rien d'autre à configurer.

### D. Lancer comme service Windows

```powershell
# Créer (une seule fois)
sc.exe create DeclarationTVA binPath="C:\declaration-tva\Declaration.API.exe" DisplayName="Declaration TVA" start=auto

# Démarrer / Arrêter
sc.exe start DeclarationTVA
sc.exe stop  DeclarationTVA

# Supprimer si besoin de recréer
sc.exe delete DeclarationTVA
```

Ouvrir **http://localhost:5000** (ou le port défini dans `appsettings.json` → `"Urls"`).

### E. Mise à jour

```powershell
sc.exe stop DeclarationTVA
Copy-Item "deploy\*" "C:\declaration-tva\" -Recurse -Force
sc.exe start DeclarationTVA
```

---

## Erreurs fréquentes

| Erreur | Cause | Correction |
|---|---|---|
| Le service démarre puis s'arrête | Runtime .NET 10 absent ou chaîne SQL invalide | `dotnet --list-runtimes` ; vérifier `appsettings.Production.json` |
| `Login failed for user` | Droits SQL insuffisants | Donner accès aux bases à l'utilisateur SQL |
| Page blanche | `wwwroot\` vide | Refaire l'étape A.2 (`npm run build` + copie) |
| Port déjà utilisé | Autre process sur le port | Changer `"Urls"` dans `appsettings.json` |
| `npm run dev` échoue | Dépendances manquantes | `npm install` dans `declaration-tva-web` |
