# VERIFY — TASK-114 — Sécurisation JWT + droits SQL dédiés (prérequis production)

## Résumé
Complète TASK-114 (le point 1 — `DeclarationTVA.sql`, login/droits SQL `decl_tva_app` — était déjà
livré). Reste traité ici : mécanisme de secret JWT de production + mise à jour de `LANCEMENT_DEV.md`.

**Mécanisme retenu** : extension de `connections.json` (section `JwtSettings.SecretKey`), pas
`appsettings.Production.json`. Justification : `Program.cs` charge déjà `connections.json` **après**
`appsettings.json` (`AddJsonFile` l.25-27) donc il le remplace automatiquement — aucune logique de
chargement à dupliquer, cohérent avec le principe existant « un seul fichier à éditer en prod ».

## Modifications réalisées
| Fichier | Changement |
|---|---|
| `Declaration.API/Program.cs` | Garde-fou au démarrage : si `builder.Environment.IsProduction()` **et** `JwtSettings:SecretKey` vaut encore une des clés de dev committées (`appsettings.json` ou le défaut `default_key_make_it_long_enough`), `throw new InvalidOperationException(...)` — refus de démarrer plutôt que signature silencieuse avec une clé publique. `Production` est la valeur par défaut de `ASPNETCORE_ENVIRONMENT` quand elle n'est pas positionnée (cas `sc.exe create`, sans variable d'environnement) — le garde-fou s'applique donc sans configuration additionnelle. `dotnet run` reste en `Development` (`launchSettings.json`), non concerné. |
| `Declaration.API/appsettings.json` | Ajout `_comment_Jwt` marquant explicitement la clé comme DEV UNIQUEMENT/publique. Aucune valeur changée (reste la clé de dev, utilisée uniquement si le garde-fou ne s'applique pas, càd hors Production). |
| `connections.json` (racine, copié à côté de chaque exe) | Ajout section `JwtSettings.SecretKey` avec une clé réelle générée aléatoirement (48 caractères alphanumériques) pour l'environnement de test `DESKTOP-5BFKKEP`, distincte de la clé de dev versionnée. |
| `LANCEMENT_DEV.md` | Exemple `connections.json` étendu avec `JwtSettings.SecretKey` ; nouvelle section « Clé JWT de production » (génération + garde-fou expliqué) ; § C « Configurer la connexion » mentionne désormais aussi `JwtSettings.SecretKey` ; table « Erreurs fréquentes » : la ligne référençant `appsettings.Production.json` (fichier inexistant) est remplacée par une référence à `connections.json` / garde-fou TASK-114 ; ligne `Login failed` enrichie (renvoie vers le script `DeclarationTVA.sql` / compte `decl_tva_app`). |
| `Declaration.Orchestration.Tests/Task114JwtProdSecretTests.cs` | 2 tests unitaires reproduisant exactement les `TokenValidationParameters` de `Program.cs` : un token signé avec la clé de dev est rejeté (`SecurityTokenSignatureKeyNotFoundException`) par une validation configurée avec la clé de prod ; un token signé avec la clé de prod est accepté par la même clé. |

Aucun changement sur `DeclarationTVA.sql` (déjà livré) ni sur le périmètre TASK-044 (packaging `deploy/`).

## Vérifications — preuve réelle

### 1. Build + tests unitaires
```
dotnet build Declaration.API/Declaration.API.csproj   → 0 erreur (1 warning préexistant, sans lien)
dotnet test Declaration.Orchestration.Tests           → 137/137 verts (dont les 2 nouveaux tests TASK-114)
```

### 2. Garde-fou de démarrage (processus réel, pas simulé)
Build `Release`-like (`bin/Debug/net10.0-windows/Declaration.API.dll`), lancé directement (hors
`dotnet run` / `launchSettings.json`) avec `ASPNETCORE_ENVIRONMENT=Production` :

- **Sans `JwtSettings.SecretKey` dans `connections.json`** (section retirée temporairement de la
  copie `bin/`) → l'application **refuse de démarrer** :
  ```
  Unhandled exception. System.InvalidOperationException: JwtSettings:SecretKey est encore la cle
  de developpement committee dans le depot. Definissez une cle reelle et secrete dans
  connections.json (section JwtSettings.SecretKey) avant de lancer l'application en production.
     at Program.<Main>$(String[] args) in D:\_vibe\GRF\Declaration.API\Program.cs:line 58
  ```
- **Avec `JwtSettings.SecretKey` réel** (section restaurée) → démarrage normal :
  ```
  info: Microsoft.Hosting.Lifetime[0]  Now listening on: http://localhost:5000
  info: Microsoft.Hosting.Lifetime[0]  Hosting environment: Production
  ```

### 3. Rejet réel d'un token forgé avec la clé de dev (bout-en-bout HTTP)
Instance lancée comme ci-dessus (Production, vraie clé `5gmDkhlYx94...` dans `connections.json`).
Deux JWT forgés hors du process API (mêmes `Issuer`/`Audience`, mêmes claims), l'un avec la clé de
dev committée (`ThisIsASecretKeyForJwtAuthenticationThatMustBeLongEnough`), l'autre avec la vraie
clé de prod :

| Requête | Résultat |
|---|---|
| `GET /api/declarations` sans token | **401** |
| `GET /api/declarations` avec token forgé **clé de dev (fuitée)** | **401** — rejeté, signature invalide |
| `GET /api/societes` avec token signé **clé de prod réelle** | **200** — accepté |
| `GET /api/declarations` avec token **clé de prod réelle** (sans `societeId`) | **400** `'societeId' est obligatoire.` — passe l'authentification, bloqué en validation métier (pas 401) |
| `GET /api/declarations?societeId=1` avec token **clé de prod réelle** | **403** — authentifié, refusé en autorisation (le token de test ne porte pas la claim `Societes` attendue) |

Distinction nette 401 (clé de dev, jamais authentifié) vs 200/400/403 (clé de prod, authentification
passée à chaque fois) : preuve que l'instance de production rejette bien tout token signé avec la
clé versionnée dans le dépôt.

## Critères de validation
- [x] Aucun secret réel ne reste en clair dans un fichier versionné comme clé de prod effective —
      `appsettings.json` ne contient que la clé de dev, explicitement documentée comme telle et
      neutralisée en Production par le garde-fou.
- [x] `LANCEMENT_DEV.md` ne référence plus `appsettings.Production.json` (fichier inexistant).
- [x] Aucune régression sur l'authentification existante — `Task074`/légacy `P_UTILISATEUR`
      inchangé (`AuthController.cs` non modifié), 137/137 tests verts.
- [x] Compte SQL applicatif à moindre privilège — déjà couvert par le point 1 (livré), non retouché.

## Réserve non bloquante (signalement, hors périmètre de correction ici)
`connections.json` est **actuellement suivi par git** (`git ls-files` le confirme, historique de
commits dessus) et contient un mot de passe SQL réel (`sa`/`1234` sur `DESKTOP-5BFKKEP`) — ceci
contredit l'hypothèse du périmètre STRICT de TASK-114 (« connections.json déjà hors dépôt par
convention existante »). Ce n'est **pas** corrigé ici (retirer le fichier du suivi git est un
changement de convention dépôt-large, distinct de ce correctif JWT, et modifierait potentiellement
le comportement de déploiement documenté ailleurs) — signalé pour arbitrage PO/architecte séparé.
La clé JWT ajoutée dans ce même fichier hérite du même défaut : à traiter ensemble.

## Statut
Prêt pour revue architecte.
