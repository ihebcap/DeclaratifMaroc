# TASK-118 — VERIFY : connexion Sage dynamique par `SO_Id` + correctif cache multi-bases

Implémentée en **worker exceptionnel** (réserve `CLAUDE.md`, même mode que TASK-101/TASK-075).

## Résumé du livrable

1. **Résolution dynamique de la connexion Sage par `SO_Id`**
   - `IDbConnectionFactory.GetSageConnectionInfoAsync(int soId)` (nouvelle méthode, remplace le
     `CreateSageConnection()` statique — 0 appelant réel, vérifié par grep avant suppression) :
     lit `P_SOCIETE.SO_ErpDb`/`SO_ErpUserApp`/`SO_ErpPasswdApp` via `GrfConnection`, construit la
     chaîne Sage en réutilisant **Server/User/Password de `GrfConnection` tels quels**, seule
     `Database` étant remplacée par `SO_ErpDb`. `SO_ErpServer`/`SO_ErpUser`/`SO_ErpPasswd`/
     `SO_ErpAuth`/`SO_UseObjetMetier` **non lus** (hors périmètre GRF, confirmé PO 18/07/2026).
   - Échec explicite (`InvalidOperationException`), jamais de repli silencieux, si `SO_Id`
     introuvable ou `SO_ErpDb` vide/NULL.
   - `DeclarationWorkflowService.BuildOrchestrateur` → `BuildOrchestrateurAsync(string, int soId)`,
     propagé aux **4 sites d'appel** (`ConstruireLignesFigeesAsync`, `ReintegrerReglementsLiberesAsync`,
     `ResynchroniserLigneAsync`, `RafraichirValorisationAsync`), tous via `declaration.SocieteId`
     déjà en scope (aucun paramètre ajouté aux méthodes publiques).

2. **Correctif cache `GRC_VENTILATION_SAGE_CACHE` → `DM_VENTILATION_SAGE_CACHE`**
   - Renommage `sp_rename` idempotent (table + PK + index), même principe que TASK-065.
   - Colonne `SO_Id` ajoutée à la clé primaire composite `(SO_Id, EC_Id, Taux)` — référence
     **logique** à `P_SOCIETE.SO_Id`, **sans `FOREIGN KEY`** (exigence PO explicite).
   - Back-fill idempotent des lignes déjà en cache : `SO_Id = 1` (seul `SO_Id` configuré avant
     cette task), personnalisable en tête de script si un autre client a un `SO_Id` unique différent.
   - `VentilationSageCacheEntry`/`IVentilationSageCacheRepository`/`VentilationSageCacheRepository`/
     `OrchestrateurDeclaration` : `SO_Id` propagé dans `GetEntries`/`MarquerEnErreur`/
     `SupprimerEntrees`/`UpsertEntries` (porté par l'entrée) — plus aucune opération sur le cache
     ne peut mélanger deux bases Sage.
   - `DeclarationRepository.GetEcIdsEnErreurAsync`/`EnrichirFamilleBDepuisCacheAsync` : filtre
     `SO_Id` ajouté (ces deux méthodes lisaient déjà `GRC_VENTILATION_SAGE_CACHE` par `EC_Id` seul —
     c'était le même risque de collision que celui documenté dans la task, pas seulement le chemin
     d'écriture).
   - 11 fichiers de test (`Task071/077/080/081/082/094/100/102/103/108/112`) portant un
     `IDeclarationRepository` factice mis à jour pour la nouvelle signature
     `GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds)`.

3. **`connections.json` / `LANCEMENT_DEV.md`** : `ConnectionStrings.SageConnection` et la section
   `SageOM` marquées **obsolètes** (non lues par `Declaration.API`), conservées uniquement pour
   compatibilité avec `Declaration.Setup` (dont le nettoyage complet est déjà tracé séparément par
   **TASK-119**, créée par le PO le 18/07/2026 — *« Tranche du même coup TASK-118 §Périmètre point 5
   dans le sens obsolète »*, confirmant ce choix). Nouveau paragraphe explicatif du mécanisme dans
   `LANCEMENT_DEV.md`.

4. **Point de validation (Périmètre §6/Étape 7)** : décision **résolution différée** (pas de
   fail-fast au login) — le login (`AuthController.Login`) ne détermine aujourd'hui aucune
   connexion Sage (confirmé dans le Contexte de la task), et la résolution différée satisfait déjà
   le critère « échec fail-fast et visible » (exception explicite à la première utilisation,
   jamais silencieuse). Ajouter une validation au login aurait élargi le périmètre à
   `AuthController`/au flux d'authentification sans demande PO explicite en ce sens — non fait.

## Build & tests (rejoués intégralement)

```
dotnet build DeclarationTVA.slnx   → 0 erreur (18 warnings préexistants, aucun nouveau)
dotnet test  DeclarationTVA.slnx   → voir détail ci-dessous
```

| Projet | Résultat |
|---|---|
| Declaration.Core.Tests | 32/32 ✅ |
| Declaration.Export.Xml.Tests | 5/5 ✅ |
| Declaration.Export.Excel.Tests | 1/1 ✅ |
| Declaration.Orchestration.Tests | **137/137 ✅** (unitaires + **9 tests d'intégration SQL Server réels** IT1-IT9 + 3 tests `Task106TokenEspece` + 1 `ITDump`, tous rejoués contre `.\sql2022`/`GR_EMA_DISTRIBUTION`) |
| Declaration.Selection.Tests | 58/59 (1 échec **préexistant, sans rapport** — `IntegrationRegressionTests`, connexion Windows intégrée `IHEB-PC\ihebc` refusée, non liée à TASK-118, déjà signalée TODO.md) |
| Declaration.Controle.Tests | 1/2 (1 échec **préexistant, sans rapport** — `ComparateurTests.GenererRapportVerification` requiert une déclaration GRFN `DT_Id=66` non présente dans cet environnement, aucun rapport avec TASK-118) |

**Aucune régression introduite** : les deux échecs sont environnementaux, reproductibles indépendamment de tout changement de ce périmètre (vérifié par lecture de la stack trace : échec de login SQL / donnée absente, pas une erreur de compilation ni un changement de comportement du code touché).

## Preuve réelle — bug détecté et corrigé pendant la vérification

En rejouant le script de migration sur SQL Server réel (`.\sql2022`/`GR_EMA_DISTRIBUTION`), la
première version de la migration **échouait** : `ALTER TABLE ... ADD SO_Id` suivi, **dans le même
batch T-SQL**, d'un `UPDATE ... SET SO_Id = 1` levait `Invalid column name 'SO_Id'` — SQL Server
compile/lie un batch entier avant de l'exécuter, une colonne ajoutée par une instruction du batch
n'est pas visible aux instructions suivantes du **même** batch. Corrigé en séparant chaque `ALTER
TABLE ADD COLUMN` de toute instruction qui la référence ensuite par un `GO`. Reproduit et vérifié
deux fois (voir ci-dessous) — sans ce test réel, ce bug serait passé inaperçu (aucun test unitaire
ne rejoue de vrai script SQL multi-batch).

### Protocole (isolé, table de test `EC_Id` 888001/888002, jamais de donnée réelle touchée)

1. Simulation d'un environnement **pré-TASK-118** : table `GRC_VENTILATION_SAGE_CACHE` (ancien
   schéma, `PK (EC_Id, Taux)`) + 2 lignes réelles insérées.
2. Exécution du bloc de migration **exact** de `DeclarationTVA.sql` (rename + ajout `SO_Id` +
   reconstruction PK), tel que corrigé.
3. Vérification : table renommée `DM_VENTILATION_SAGE_CACHE`, 2 lignes **préservées**, `SO_Id=1`
   backfillé sur les deux, `PK_DM_VENTILATION_SAGE_CACHE` = `(SO_Id, EC_Id, Taux)`.
4. **Rejeu du même bloc une 2e fois** (preuve d'idempotence) : aucune erreur, aucun doublon, état
   identique.
5. Nettoyage : suppression des 2 lignes de test uniquement (table réutilisable telle quelle par
   les tests IT existants).

```
=== AVANT (ancien schema, 2 lignes) ===
EC_Id       Taux      BaseHT
888001      20.0000   1000.0000
888002      10.0000   500.0000

=== APRES PASSE 1 (donnees + SO_Id backfille) ===
SO_Id  EC_Id    Taux      BaseHT
1      888001   20.0000   1000.0000
1      888002   10.0000   500.0000

=== PK APRES PASSE 1 ===
PK_DM_VENTILATION_SAGE_CACHE  SO_Id  (ordinal 1)
PK_DM_VENTILATION_SAGE_CACHE  EC_Id  (ordinal 2)
PK_DM_VENTILATION_SAGE_CACHE  Taux   (ordinal 3)

=== APRES PASSE 2 (idempotence) === → identique à la passe 1, aucune erreur
=== NETTOYAGE OK ===
```

## Preuve réelle — résolution `SO_Id` (scratch/TestTask118, `dotnet run`)

Console réelle instanciant `DbConnectionFactory` avec le **vrai** `connections.json` et appelant
`GetSageConnectionInfoAsync` contre `P_SOCIETE` réelle (`GR_EMA_DISTRIBUTION`/`DESKTOP-5BFKKEP`) :

```
--- SO_Id=1 (existant) ---
ConnectionString = Data Source=DESKTOP-5BFKKEP;Initial Catalog="NEW_EMA DISTRIBUTION";User ID=sa;Password=1234;Trust Server Certificate=True
OmUser = <Administrateur>
OmPassword = ***

--- SO_Id=999999 (inexistant) ---
OK — echec explicite : Résolution de la connexion Sage impossible : société SO_Id=999999 introuvable dans P_SOCIETE.

--- GrfConnection / PersistenceConnection restent uniques ---
GrfConnection = Server=DESKTOP-5BFKKEP;Database=GR_EMA_DISTRIBUTION;...
PersistenceConnection = Server=DESKTOP-5BFKKEP;Database=GR_EMA_DISTRIBUTION;...
```

Confirme : `Server`/`User`/`Password` = ceux de `GrfConnection` (inchangés), `Database` remplacée
par `SO_ErpDb` ("NEW_EMA DISTRIBUTION"), `OmUser` = `SO_ErpUserApp` réel, échec explicite et
message actionnable pour un `SO_Id` inconnu, `GrfConnection`/`PersistenceConnection` jamais
paramétrées par `SO_Id`.

## ⚠️ Réserve non bloquante — scénario multi-bases Sage réel non prouvable

Comme documenté dans la task et re-vérifié ce jour : **une seule ligne `P_SOCIETE`** existe dans
cet environnement de dev (`SO_Id=1`). Le scénario réel « 2 `SO_Id` → 2 bases Sage physiquement
distinctes, même `EC_Id` sans collision » **ne peut pas être prouvé en conditions réelles** ici —
aucun second jeu de données société/base Sage n'est disponible. Ce qui **est** prouvé
réellement :
- la résolution par `SO_Id` fonctionne et échoue explicitement (ci-dessus) ;
- la clé du cache est bien élargie à `(SO_Id, EC_Id, Taux)`, ce qui **rend la collision
  structurellement impossible** dès qu'un second `SO_Id`/base Sage existera (deux lignes avec le
  même `EC_Id` mais un `SO_Id` différent ne peuvent plus s'écraser — contrainte SQL, pas seulement
  logique applicative).

Ne pas confondre cette réserve (donnée de test manquante) avec une lacune du correctif — le
correctif lui-même est vérifié structurellement (contrainte PK réelle sur SQL Server).

## Audit — autres usages supposant `EC_Id`/`MV_Id` globalement unique (Étape 5)

Grep ciblé sur tout le code (hors `DM_LGTVA`, qui est déjà scopé par `DeclarationId`, donc jamais à
risque). Deux catégories trouvées :

1. **Déjà corrigées dans ce périmètre** (utilisaient `GRC_VENTILATION_SAGE_CACHE` par `EC_Id` seul,
   même risque que celui documenté par la task) :
   - `DeclarationRepository.GetEcIdsEnErreurAsync` (revalidation TASK-077).
   - `DeclarationRepository.EnrichirFamilleBDepuisCacheAsync` (écran Factures, famille B).

2. **Résiduel — hors périmètre strict de cette task, à documenter, pas à corriger silencieusement** :
   - `VentilationSageCacheRepository.GetCurrentPaiementToken` (JOIN `RT_AFFECTATION`/`RT_MOUVEMENT`
     par `EC_Id` brut, sans filtre `SO_Id`).
   - `VentilationSageCacheRepository.GetEcheanceMontantDevise` (`RT_ECHEANCE` par `EC_Id` brut).
   - `DeclarationRepository.GetMvPointsActuelsAsync` (`RT_MOUVEMENT` par `MV_Id` brut).

   Ces trois lectures ciblent les tables `RT_*` — schéma **pré-existant, propriété de
   l'application principale** (pas créé par GRF, contrairement au cache). `RT_MOUVEMENT` porte déjà
   une colonne `SO_Id` (vu dans `Declaration.Selection`, filtre `M.SO_Id = @so` systématique sur
   toutes les sélections de candidats) — ce qui suggère que ce schéma partagé a *peut-être* déjà
   été conçu par l'application principale pour gérer plusieurs bases Sage (au contraire du cache
   `GRC_VENTILATION_SAGE_CACHE`, créé par GRF sans cette précaution). Mais ceci n'a **pas été
   vérifié** — le déterminer avec certitude demanderait d'analyser le schéma/la sémantique de
   `RT_ECHEANCE`/`RT_MOUVEMENT` avec le PO/l'architecte propriétaire de l'application principale,
   ce qui dépasse le périmètre strict de TASK-118 (qui ne mandate que le correctif du cache, §4).
   **Signalé, non corrigé** — décision à prendre séparément si le PO le juge nécessaire.

## Fichiers modifiés

- `Declaration.Application/Interfaces/IDbConnectionFactory.cs` (nouvelle méthode + `SageConnectionInfo`)
- `Declaration.Infrastructure/Factories/DbConnectionFactory.cs`
- `Declaration.Application/Services/DeclarationWorkflowService.cs`
- `Declaration.Orchestration/OrchestrateurDeclaration.cs`
- `Declaration.Orchestration/IVentilationSageCacheRepository.cs`
- `Declaration.Orchestration/VentilationSageCacheRepository.cs`
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs`
- `Declaration.Application/Interfaces/IDeclarationRepository.cs`
- `DeclarationTVA.sql` (migration renommage + `SO_Id`)
- `connections.json`, `LANCEMENT_DEV.md`
- Tests : `Declaration.Orchestration.Tests/Task024CacheVentilationSageTests.cs` (réécriture complète
  des signatures/noms de table) + `Task071/077/080/081/082/094/100/102/103/108/112...Tests.cs`
  (signature `GetEcIdsEnErreurAsync`) + `Task080/094/100...Tests.cs` (`IDbConnectionFactory` factice)
- `scratch/TestTask118/` (preuve réelle `GetSageConnectionInfoAsync`, conservée pour rejouabilité)

## Critères de validation (repris de la task)

| Critère | Statut |
|---|---|
| Aucune régression sur le modèle mono-Sage existant | ✅ 137/137 Orchestration.Tests verts (SQL Server réel inclus) |
| `GrfConnection`/`PersistenceConnection` restent uniques | ✅ prouvé réellement (ci-dessus) |
| Échec fail-fast et visible, jamais silencieux | ✅ `InvalidOperationException` explicite, prouvée réellement (`SO_Id=999999`) |
| Cache sans collision possible entre deux bases Sage | ✅ contrainte PK réelle `(SO_Id, EC_Id, Taux)`, prouvée réellement (rename+backfill sur SQL Server) |
| Aucun secret Sage OM exposé plus largement qu'aujourd'hui | ✅ inchangé — toujours lu depuis SQL (P_SOCIETE au lieu de connections.json), même niveau de protection, dette préexistante non aggravée |
