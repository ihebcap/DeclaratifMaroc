# TASK-051 — Suppression du fallback SQLite (code mort) + fail-fast sur chaîne de connexion manquante

## Contexte
Diagnostic 11/07/2026 (à partir de l'incident « Erreur de chargement du checkup » sur TVA001-2026-03) : le module **n'utilise pas SQLite**. C'est un vestige du scaffolding initial (clean architecture, TASK-012) resté en place comme repli.

Preuves :
- Un seul package `Microsoft.Data.Sqlite` (v10.0.9), référencé dans `Declaration.Infrastructure/Declaration.Infrastructure.csproj:10`.
- Un seul fichier l'utilise : `Declaration.Infrastructure/Factories/DbConnectionFactory.cs`, **uniquement dans les branches de repli** (`string.IsNullOrEmpty(cs) ? new SqliteConnection(...) : new SqlConnection(cs)` pour Grf/Sage ; test `.db` / `Data Source=` pour Persistence).
- **Aucun fichier `tva.db`** n'existe → ces branches n'ont jamais été exécutées en production.
- Le SQL réel du repository est **du T-SQL pur** (13 occurrences `OFFSET/FETCH`, `GETDATE`, `TOP`, `[dbo]`, `OUTPUT INSERTED`… dans `DeclarationRepository.cs`) → il **planterait** sur SQLite. Le fallback n'est donc même pas fonctionnel.

**Dommage réel constaté** : ce fallback silencieux, combiné à `optional:true` sur `connections.json` (`Program.cs:25-27`), a **masqué** l'incident : `connections.json` non chargé (absent à côté de l'exe) → le factory a fabriqué une connexion `(localdb)\mssqllocaldb` bidon (défauts dummy d'`appsettings.json`) au lieu de `.\sql2022` → obscure *SqlException error 52 – Local Database Runtime introuvable* sur `/lignes` et `/checkup`, au lieu d'un échec net « GrfConnection non configurée ».

## Périmètre STRICT
- **Inclus** :
  1. Supprimer les branches de repli SQLite dans `DbConnectionFactory` (`CreateGrfConnection`, `CreateSageConnection`, `CreatePersistenceConnection`, `GetGrfConnectionString`).
  2. Remplacer par un **fail-fast** explicite : si la chaîne de connexion demandée est vide/absente → `throw` clair au moment de l'usage (message nommant la clé, ex. `GrfConnection`).
  3. Retirer le `PackageReference Microsoft.Data.Sqlite` de `Declaration.Infrastructure.csproj` (bonus : supprime le warning de sécurité `NU1903 – SQLitePCLRaw.lib.e_sqlite3`) et le `using Microsoft.Data.Sqlite;`.
- **Exclu** :
  - Aucun changement de logique métier, de requêtes SQL, de repository ou de contrat d'API.
  - Ne pas modifier `connections.json` ni les valeurs `appsettings.json` (le correctif opérationnel de l'incident — remettre `connections.json` à côté de l'exe + restart — est **hors** de cette tâche, traité séparément).
  - Ne pas changer `optional:true` sur `AddJsonFile(connections.json)` (`Program.cs:26`) : le fail-fast au niveau du factory couvre déjà le cas ; toucher au chargement de config est un choix distinct à ne pas embarquer ici.

## Cause racine (de la dette)
Repli de démarrage jamais retiré après le branchement effectif de SQL Server. Absence de garde-fou → une config manquante devient une connexion invalide silencieuse au lieu d'une erreur immédiate et lisible.

## Objectif
```
Entrée : DbConnectionFactory demande une chaîne de connexion (Grf/Sage/Persistence)
Traitement : si la clé est renseignée → SqlConnection ; si vide/absente → exception explicite nommant la clé
Sortie : plus aucune référence SQLite dans le module ; une config manquante échoue immédiatement avec un message clair
```

## Étapes
1. `DbConnectionFactory.cs` : pour chaque `Create*Connection()`, remplacer le repli `SqliteConnection` par `throw new InvalidOperationException($"Chaîne de connexion '<clé>' non configurée (connections.json / appsettings.json).")` quand `string.IsNullOrEmpty(cs)`. `GetGrfConnectionString()` : idem (plus de défaut `tva.db`).
2. Supprimer le `using Microsoft.Data.Sqlite;` (ligne 3) et toute la logique de détection `.db` de `CreatePersistenceConnection` (redevient un simple `SqlConnection(cs)` avec garde).
3. `Declaration.Infrastructure.csproj` : retirer `<PackageReference Include="Microsoft.Data.Sqlite" ... />`.
4. Build solution complet (0 erreur) ; vérifier par grep qu'il ne reste **aucune** occurrence `Sqlite` dans `Declaration.*` (hors éventuel commentaire historique).
5. Vérifier les tests existants (`Declaration.Infrastructure`/`Controle.Tests`…) : aucun ne doit dépendre du fallback SQLite. Corriger un test qui s'appuierait sur `tva.db` le cas échéant (à signaler s'il en existe).

## Livrables
- `DbConnectionFactory` sans SQLite, avec fail-fast nommant la clé manquante.
- `Declaration.Infrastructure.csproj` sans package Sqlite ; warning `NU1903` disparu du build.
- `VERIFY/TASK-051_verify.md` : (a) build solution 0 erreur + suite de tests verte ; (b) grep `Sqlite` = 0 occurrence de code dans `Declaration.*` ; (c) preuve du fail-fast — démarrer/appeler l'API sans `connections.json` et avec `appsettings` vidé de la clé → exception explicite « GrfConnection non configurée » (et non plus l'erreur 52 localdb) ; (d) chemin nominal inchangé — `/lignes` et `/checkup` répondent 200 avec `connections.json` (`.\sql2022`) en place.

## Critères de validation
- Build solution complet : 0 erreur, warning `NU1903` (SQLitePCLRaw) disparu.
- Aucune référence `Microsoft.Data.Sqlite` / `SqliteConnection` dans le code du module.
- Chaîne de connexion absente → exception immédiate et explicite nommant la clé (pas de connexion localdb fantôme, pas d'erreur 52 opaque).
- Comportement nominal (SQL Server `.\sql2022`) strictement inchangé : lignes/checkup/persistance identiques à avant.
- Aucune modification de requête SQL, de repository, de contrat d'API ni de `connections.json`.

## Risques / dépendances
- **Risque faible** : suppression de code mort + garde. Seul point d'attention → vérifier qu'aucun **test** ne s'appuie sur le repli `tva.db` (auquel cas le convertir en connexion SQL Server mockée / marquée `RequiresDb`). À confirmer au build des projets de tests.
- **Indépendant** du correctif opérationnel de l'incident (remettre `connections.json` à côté de l'exe + restart API) — celui-ci reste à faire à part et n'est pas bloqué par cette tâche.
- Sans rapport avec `optional:true` du chargement `connections.json` (`Program.cs:26`) : décision de config distincte, non embarquée ici.
