# TASK-033 — Cohérence des ports dev (front ↔ API) : `ERR_CONNECTION_REFUSED` au premier lancement

## Contexte
Au premier lancement en dev, le front échoue sur `:5005/api/auth/login` → `net::ERR_CONNECTION_REFUSED`. Diagnostic (09/07/2026) : ce **n'est pas un bug de code**. Le design retenu (décision PO) est que **tout est paramétrable des deux côtés** :
- **API** : port configurable via `--urls` / `launchSettings.json` (`applicationUrl`).
- **Front** : URL de l'API configurable via `VITE_API_BASE` (`api.ts:3`), appel **direct** en URL absolue (cross-origin), autorisé par le CORS ouvert de l'API (`Program.cs:60-62` `AllowAnyOrigin`, `app.UseCors()` `:82`).

Le défaut réel = les **valeurs par défaut livrées dans le repo divergent**, donc un clone frais plante :

| Élément (défaut sur disque) | Valeur | 
|---|---|
| API en écoute — `launchSettings.json` `applicationUrl` | **5018** |
| Front — `.env.development` → `VITE_API_BASE` | `http://localhost:5005/api` |
| Proxy `vite.config.ts` → target `/api` | `http://localhost:5005` |

Conséquence : le front appelle `http://localhost:5005/api/...` (URL absolue → **court-circuite le proxy Vite**) alors que l'API écoute sur **5018** → rien sur 5005 → connexion refusée. Le port 5005 n'existe nulle part côté back.

## Périmètre STRICT
- **Inclus** : rendre les **défauts** du repo cohérents pour qu'un lancement dev « à froid » fonctionne. Les deux paramètres restent surchargables (rien n'est figé en dur).
- **Exclu** : aucun changement de logique applicative, d'authentification, ni de code C#. Ne pas toucher `.env.test` (`/api`, déjà correct). Ne pas modifier la string de connexion SQL ni `connections.json`. Ne pas restreindre le CORS (hors périmètre — note prod séparée).

## Cause racine
Deux paramètres indépendants, tous deux légitimes, mais dont les **valeurs par défaut** livrées ne correspondent pas : front par défaut sur `:5005`, API par défaut sur `:5018`. Aucune règle ne les tient synchronisés.

## Objectif
```
Entrée : dev lance `dotnet run` + `npm run dev`, sans rien surcharger
Traitement : le défaut du front cible le même port que le défaut de l'API
Sortie : /api/auth/login répond, plus aucun ERR_CONNECTION_REFUSED sur un clone frais ; ports restent surchargables
```

## Étapes
1. Choisir **un** port par défaut commun (le plus simple : garder l'API sur **5018**, déjà documenté `LANCEMENT_DEV.md:67`, aucun changement back).
2. Aligner le défaut front : `declaration-tva-web/.env.development` `VITE_API_BASE=http://localhost:5005/api` → **`http://localhost:5018/api`**.
3. `vite.config.ts` : mettre le target du proxy sur le **même** port (`http://localhost:5018`) pour éviter un défaut mort/incohérent — ou supprimer le proxy si l'appel direct est le mode assumé. Recommandation : synchroniser sur 5018 (moins invasif).
4. Vérifier qu'aucune autre référence `5005` ne subsiste côté front (grep) et documenter dans `LANCEMENT_DEV.md` que le port est surchargable (`VITE_API_BASE` côté front, `--urls` côté API).

## Livrables
- Défauts repo cohérents : `.env.development` et `vite.config.ts` sur le port de l'API (5018), les deux côtés restant paramétrables.
- `VERIFY/TASK-033_verify.md` : preuve qu'un lancement « à froid » (`dotnet run` + `npm run dev`, sans surcharge) → login OK, capture réseau de `/api/auth/login` en 200 ; + preuve qu'une surcharge (ex. API `--urls :5005` **et** `VITE_API_BASE=:5005`) marche aussi.

## Critères de validation
- Premier lancement dev « à froid » sans `ERR_CONNECTION_REFUSED`.
- `/api/auth/login` répond (200 / 401 selon creds), pas de connexion refusée.
- Les deux ports restent surchargables sans toucher au code (front via `VITE_API_BASE`, API via `--urls`).
- Aucune modification de code C# ni de la chaîne de connexion ; `.env.test` et le CORS inchangés.

## Risques / dépendances
- Faible risque, périmètre purement configuration dev. Aucune dépendance bloquante.
- CORS actuellement `AllowAnyOrigin` (`Program.cs:62`) : OK pour dev/appel direct, mais **à durcir en prod** (origines explicites). Hors périmètre de cette tâche — à noter comme dette prod distincte.
- Vérifier qu'un éventuel `GOCOM_CONFIG.API_BASE` injecté en prod (`api.ts:3`) n'est pas impacté — cette tâche ne touche que le dev.
