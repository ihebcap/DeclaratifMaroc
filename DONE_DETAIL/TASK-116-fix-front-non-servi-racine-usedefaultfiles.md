# TASK-116 — Fix bloquant : front non servi sur `/` (404 en production, `UseDefaultFiles` manquant)

## Contexte
Incident constaté en **production** (17/07/2026, PO) pendant l'installation en cours : `GET /`
renvoie `HTTP 404` (page navigateur, pas une page 404 applicative) sur le service Windows
`DeclarationTVA` alors que l'API tourne (le port répond).

Diagnostic confirmé en deux temps :
1. **Cause écartée après test** : `deploy\wwwroot\` était vide (étape A.2 de `LANCEMENT_DEV.md` —
   `npm run build` + copie du front — non faite). Le PO a copié le build manuellement → **le
   problème persiste**, ce qui confirme la deuxième cause ci-dessous comme bloquante réelle.
2. **Bug confirmé dans le code** : `Declaration.API/Program.cs:98` appelle
   ```csharp
   app.UseStaticFiles(); // Sert wwwroot (pour le front React/TASK-013)
   ```
   `UseStaticFiles()` seul ne sert que les chemins de fichiers **explicites**
   (`/index.html`, `/assets/xxx.js`...). Il ne mappe **jamais** `/` vers `wwwroot/index.html` — il
   manque `app.UseDefaultFiles()` **avant** `UseStaticFiles()` (ou `app.MapFallbackToFile("index.html")`
   après `app.MapControllers()`). Jamais détecté avant cet incident car en développement le front
   tourne sur le serveur Vite (`:5173`), jamais via `wwwroot` de l'API — ce chemin de code n'était
   donc jamais exercé avant une installation « mono-dossier » réelle (TASK-044).

Déjà signalé dans `TASKS/TASK-044-...md` §Contexte (note ajoutée le 17/07/2026) ; ce fichier
documente le correctif de façon autonome et actionnable, TASK-044 restant focalisé sur le
packaging/l'assemblage du dossier `deploy/`.

## Périmètre STRICT
- **Inclus** :
  1. Correction de `Program.cs` : ajouter `app.UseDefaultFiles();` avant `app.UseStaticFiles();`
     (ligne 98). Vérifier qu'aucune route front (le front n'utilise pas de routeur côté client
     aujourd'hui — pas de `react-router` détecté) n'exige en plus un `MapFallbackToFile` — sinon
     l'ajouter également par sécurité/robustesse.
  2. Vérification que `GET /` sert bien `index.html` une fois `wwwroot/` peuplé, en local **et**
     après republication (`dotnet publish` + copie `deploy\` → dossier d'installation).
- **Exclu** :
  - Packaging/assemblage du dossier `deploy/`, copie du worker net48, `publish.ps1` : **TASK-044**.
  - Setup GUI / WinSW / port paramétrable : **TASK-115**.
  - Toute évolution du front (routage, nouvelles pages) : hors périmètre, c'est un fix serveur.

## Objectif
```
Entrée  : Program.cs:98 — UseStaticFiles() seul, GET / → 404 même avec wwwroot peuplé
Étapes  : ajouter UseDefaultFiles() (+ MapFallbackToFile si nécessaire), republier, retester
Sortie  : GET / sert index.html, l'écran de déclaration TVA s'affiche, en dev (wwwroot local)
          et en prod (service Windows, dossier d'installation client)
```

## Étapes
1. Éditer `Declaration.API/Program.cs` ligne 98 : insérer `app.UseDefaultFiles();` avant
   `app.UseStaticFiles();`.
2. `dotnet build` (ou `dotnet publish -c Release -o deploy`) pour valider la compilation.
3. Test local : peupler un `wwwroot/` de test (copie du build front), lancer l'API, vérifier
   `GET /` → `index.html` servi (pas de 404).
4. Republier (`dotnet publish Declaration.API.csproj -c Release -o deploy`), copier vers le dossier
   d'installation prod, redémarrer le service (`sc.exe stop/start DeclarationTVA`), retester
   `http://<hôte>:<port>/` réellement en production.
5. Retirer la note d'avertissement ajoutée dans `TASKS/TASK-044-...md` §Contexte une fois ce fix
   validé (elle référence ce TASK-116).

## Livrables
- `Program.cs` corrigé.
- `VERIFY/TASK-116_verify.md` : preuve réelle —
  - capture ou log de `GET /` renvoyant `200` et le contenu de `index.html` (pas 404) ;
  - test rejoué après un cycle complet `dotnet publish` → copie → redémarrage service, pas
    seulement en `dotnet run` local ;
  - confirmation qu'aucune régression sur les routes API existantes (`GET /api/...` toujours
    fonctionnel, Swagger toujours accessible en dev).

## Critères de validation
- `GET /` sert le front (`index.html`) sans 404, dans le dossier `deploy/` republié et dans
  l'installation service Windows réelle — pas seulement testé en `dotnet run`.
- Aucune régression sur les endpoints `/api/...` ni sur `/swagger` (dev).
- Le correctif est minimal (une ou deux lignes ajoutées), aucune restructuration du pipeline HTTP.

## Risques / dépendances
- **Bloquant pour l'installation prod en cours** — priorité immédiate, indépendant de TASK-044
  (packaging) et TASK-115 (setup GUI), qui peuvent continuer en parallèle.
- Si le front venait à adopter un routeur côté client à l'avenir (actuellement absent), vérifier
  que `MapFallbackToFile("index.html")` est bien en place (pas seulement `UseDefaultFiles()`, qui
  ne couvre que `/` exactement) — anticiper ce cas dans le correctif si le coût est nul.
