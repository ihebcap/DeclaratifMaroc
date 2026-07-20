# TASK-118 — Résolution dynamique de la connexion Sage par `SO_Id` (multi-bases Sage, GRF reste unique) + renommage `GRC_VENTILATION_SAGE_CACHE` → `DM_VENTILATION_SAGE_CACHE`

## Contexte
Signalement PO (17/07/2026) : la base **GRF** (applicative, unique) doit pouvoir orchestrer des
déclarations pour des sociétés dont les données comptables vivent dans **plusieurs bases Sage
distinctes**, sélectionnées via `SO_Id`. Contrainte explicite du PO : la chaîne de connexion
« persistante » (GRF) reste **unique** — c'est la chaîne **Sage** qui doit devenir variable selon
la société (`SO_Id`) traitée, pas la chaîne GRF.

Constat code (architecte) : aujourd'hui, **une seule** `SageConnection` existe, globale au
process, indépendamment de tout `SO_Id` :
- `connections.json:6` / `Declaration.API/appsettings.json:9-13` : une seule paire
  `Server`/`Database` pour `SageConnection`, partagée par toute l'instance.
- `Declaration.Infrastructure/Factories/DbConnectionFactory.cs:33-39` (`CreateSageConnection()`)
  et `Declaration.Application/Interfaces/IDbConnectionFactory.cs:9` : aucun paramètre `SO_Id` /
  `soId` dans la signature.
- `Declaration.Application/Services/DeclarationWorkflowService.cs:592-623`
  (`BuildOrchestrateur(string grfConnectionString)`) : lit `_configuration.GetConnectionString
  ("SageConnection")` en dur (ligne 594) et construit le `WorkerConfig` (Server/Database/User/
  Password Sage OM) transmis au worker COM (`SageTaxReader.Console`, via `WorkerInvoker`) —
  **toujours la même base Sage**, quel que soit `declaration.SocieteId`. 4 sites d'appel
  (lignes 223, 472, 510, 572), **tous** avec `declaration.SocieteId` déjà disponible dans le
  scope appelant (ex. ligne 221 `AppliquerExclusiviteInterDeclarationAsync(candidates,
  declaration.SocieteId, declarationId)` juste avant l'appel ligne 223) — le `soId` est donc
  disponible partout où `BuildOrchestrateur` est invoqué, il n'est simplement pas propagé.
- `Declaration.API/Controllers/AuthController.cs` (`Login`, lignes 39-94) : `SO_Id` y sert
  **uniquement** à peupler la claim JWT `Societes` (liste des sociétés autorisées via
  `P_SOCUTILISATEUR`, ligne 58-60) — aucune résolution de connexion Sage n'y a lieu (le login
  n'ouvre aucune connexion Sage aujourd'hui). Le combobox de choix de société (front `Auth.tsx`,
  alimenté par `GET /api/societes` → `SELECT SO_Id, SO_RaisonSocial FROM P_SOCIETE`, TASK-066)
  fixe déjà le `SO_Id` de la session ; ce `SO_Id` doit désormais aussi déterminer **quelle base
  Sage** interroger pour toute la suite de la session.

Cette demande **ne contredit pas** TASK-066 (« une session = une société », multi-société
simultanée exclue) : elle en est le prolongement — jusqu'ici toutes les sociétés visibles étaient
implicitement supposées partager la **même** base Sage physique (modèle de déploiement mono-Sage,
TASK-044) ; ce n'est plus vrai.

**Précision PO (17/07/2026, même session)** — les coordonnées de connexion Sage par société
**existent déjà nativement** dans `P_SOCIETE`, pas besoin d'en créer :
```sql
SELECT SO_Id, SO_ErpServer, SO_ErpDb, SO_ErpUserApp, SO_ErpPasswdApp, SO_UseObjetMetier
FROM P_SOCIETE
```
- `SO_ErpServer` **ignoré** : on utilise toujours le **même serveur SQL** que `GrfConnection`
  (une seule instance SQL Server pour tout, seule la **base** change par société).
- `SO_ErpDb` = nom de la base Sage cible pour ce `SO_Id`.
- `SO_ErpUserApp`/`SO_ErpPasswdApp` = identifiants Sage OM (applicatifs) pour ce `SO_Id`.
- `SO_UseObjetMetier` peut être `0` (société non exploitée en Objets Métier par
  l'**application principale** — l'autre logiciel qui possède `P_SOCIETE`) — **sans rapport** avec
  GRF : GRF peut/doit quand même utiliser `SO_ErpUserApp`/`SO_ErpPasswdApp` pour son propre worker
  OM, indépendamment de ce flag (qui reflète un choix fonctionnel de l'application principale, pas
  une contrainte technique empêchant GRF de s'y connecter).
- **Contrainte explicite** : ajouter `SO_Id` à `GRC_VENTILATION_SAGE_CACHE` (table déjà côté GRF)
  en référence **logique** à `P_SOCIETE.SO_Id`, **sans clé étrangère** — une FK casserait/gênerait
  le modèle EF de l'**application principale** (propriétaire de `P_SOCIETE`), qui ne doit pas être
  impactée par le schéma GRF.

✅ **Colonnes confirmées en base réelle par l'architecte** (18/07/2026,
`GR_EMA_DISTRIBUTION`/`DESKTOP-5BFKKEP`, `INFORMATION_SCHEMA.COLUMNS` + lecture directe) :
`SO_Id` (int, NOT NULL), `SO_ErpServer`/`SO_ErpDb`/`SO_ErpUserApp`/`SO_ErpPasswdApp` (nvarchar,
NULL-able), `SO_UseObjetMetier` (bit, NOT NULL). Donnée réelle observée (seule ligne existante en
dev) :
```
SO_Id=1 | SO_RaisonSocial='NEW_EMA DISTRIBUTION' | SO_ErpServer='.' | SO_ErpDb='NEW_EMA DISTRIBUTION'
SO_ErpUserApp='<Administrateur>' | SO_UseObjetMetier=False | SO_ErpAuth=1
```
`SO_ErpDb` correspond **déjà** exactement à la base Sage actuellement figée en dur dans
`connections.json:6` (`Database=NEW_EMA DISTRIBUTION`) — cohérent avec la lecture PO : server =
celui de `GrfConnection` (ici `DESKTOP-5BFKKEP`, alors que `SO_ErpServer='.'` est une valeur locale
non fiable, confirmant qu'il faut bien l'ignorer), seule la `Database` doit varier par `SO_Id`.

✅ **Confirmation PO (18/07/2026)** — tranché définitivement : de `SO_ErpDb`, GRF n'utilise **que
le nom de la base**. Le **serveur SQL, le login SQL et le mot de passe** de la `SageConnection`
proviennent **exclusivement de la chaîne de connexion GRF déjà configurée** (`GrfConnection`) —
jamais de `P_SOCIETE`. Donc : `SageConnection(soId) = { Server, User, Password } de GrfConnection
+ Database = SO_ErpDb }`. Les colonnes `SO_ErpUser`/`SO_ErpPasswd`/`SO_ErpAuth`/`SO_ErpServer`
trouvées en base sont **hors périmètre GRF** (probablement réservées à l'application
principale) — **non lues, non utilisées** par cette task. Seules `SO_ErpDb` (nom de base SQL) et
`SO_ErpUserApp`/`SO_ErpPasswdApp` (login applicatif Sage OM/COM, distinct de la connexion SQL)
sont consommées par GRF.

⚠️ **Une seule ligne `P_SOCIETE` existe dans l'environnement de dev actuel** (`SO_Id=1`) — le
scénario réel « 2 `SO_Id` → 2 bases Sage physiquement distinctes » ne peut pas encore être testé
en conditions réelles ; le `VERIFY` de cette task devra soit obtenir un second jeu de données
réel, soit documenter explicitement cette limite si non disponible (ne pas la masquer).

## Cause racine
Le modèle de configuration (`connections.json`) a été conçu pour un déploiement **mono-client /
mono-Sage** (TASK-044 : 1 client = 1 dossier `deploy/` = 1 `connections.json` = 1 base Sage
cible). L'introduction de `SO_Id` (TASK-066) a ajouté un filtre logique « société » **au sein
d'une même base GRF/Sage**, sans jamais remettre en cause l'hypothèse mono-Sage sous-jacente.
Cette hypothèse est désormais invalidée par le PO : une même instance GRF doit adresser plusieurs
bases Sage physiques, sélectionnées par `SO_Id`.

## ⚠️ Risque majeur découvert en cours d'analyse — collision de cache inter-bases
Le cache `GRC_VENTILATION_SAGE_CACHE` (TASK-024, `Declaration.Infrastructure/SQL/
002_Cache_Ventilation_Sage.sql:7-34`) a pour **clé primaire `(EC_Id, Taux)`** — `EC_Id` est
l'identifiant interne Sage de l'échéance, **unique seulement à l'intérieur d'une base Sage
donnée**. Si deux `SO_Id` pointent vers deux bases Sage physiquement distinctes, **rien
n'empêche une collision** : le même entier `EC_Id` désignera potentiellement deux factures
totalement différentes selon la base d'origine, et le cache mélangerait leurs ventilations sans
le détecter (aucune colonne d'origine base/société dans la clé ni dans la table). C'est un
**bloquant fonctionnel de premier ordre** pour tout scénario réel à plusieurs bases Sage — pas un
simple détail de configuration. Ce point est **traité comme faisant partie du périmètre** de cette
task (pas une réserve à part), voir Étapes/Livrables ci-dessous.

## Périmètre STRICT
- **Inclus** :
  1. ~~Vérification technique préalable~~ — ✅ **faite et tranchée** (18/07/2026, colonnes
     confirmées en base réelle + confirmation PO sur leur usage exact, cf. Contexte).
  2. **Résolution de la connexion Sage par `SO_Id`** : nouvelle méthode sur `IDbConnectionFactory`
     (ex. `CreateSageConnection(int soId)` / `GetSageConnectionInfo(int soId)`) qui lit `SO_ErpDb`
     (nom de base uniquement) + `SO_ErpUserApp`/`SO_ErpPasswdApp` (login Sage OM) depuis
     `P_SOCIETE` (via `GrfConnection`), et construit la chaîne SQL Sage en réutilisant **Server +
     User + Password de `GrfConnection` tels quels**, seule `Database` étant remplacée par
     `SO_ErpDb`. Échec explicite (pas de repli silencieux) si `SO_Id` introuvable ou `SO_ErpDb`
     vide/NULL. `SO_UseObjetMetier`/`SO_ErpServer`/`SO_ErpUser`/`SO_ErpPasswd`/`SO_ErpAuth` **non
     consultés** (hors périmètre GRF, confirmé PO).
  3. **Propagation du `soId`** dans `DeclarationWorkflowService.BuildOrchestrateur` (les 4 sites
     d'appel disposent déjà de `declaration.SocieteId` dans leur scope) et dans le `WorkerConfig`
     transmis à `SageTaxReader.Console` (Server/Database/User/Password Sage OM désormais
     dépendants du `SO_Id` traité).
  4. **Correction du cache `GRC_VENTILATION_SAGE_CACHE`** (risque ci-dessus) : ajouter une colonne
     `SO_Id` (INT) à la clé primaire composite — **référence logique** à `P_SOCIETE.SO_Id`,
     **sans contrainte `FOREIGN KEY`** (exigence PO explicite, pour ne pas impacter le modèle EF
     de l'application principale propriétaire de `P_SOCIETE`) — migration idempotente des lignes
     déjà en cache (valeur par défaut = le `SO_Id` unique actuellement configuré, seul cas
     possible avant cette task), et audit de tout autre point de code supposant `EC_Id`
     globalement unique (à rechercher explicitement, ne pas se limiter au cache).
  4bis. **Renommage `GRC_VENTILATION_SAGE_CACHE` → `DM_VENTILATION_SAGE_CACHE`** (demande PO
     18/07/2026, conformité au nommage `DM_*` déjà établi par TASK-065 pour les tables propres au
     module — `DM_ENTTVA`/`DM_LGTVA`). Fait dans la **même migration** que l'ajout de `SO_Id`
     (§4) pour éviter deux passes sur le même objet. Périmètre du renommage (repérage exhaustif,
     11 fichiers identifiés par grep) :
     - DDL : `Declaration.Infrastructure/SQL/002_Cache_Ventilation_Sage.sql` (`CREATE TABLE` +
       `PK_GRC_VENTILATION_SAGE_CACHE` + `IX_GRC_VENTILATION_SAGE_CACHE_ECId`) et les scripts
       additifs `003_Cache_Ventilation_Token_Nullable.sql`, `004_Cache_Ventilation_MotifErreur.sql`,
       `005_Cache_Ventilation_MontantsBruts.sql` (tous des `ALTER TABLE GRC_VENTILATION_SAGE_CACHE`).
     - `sp_rename` idempotent (garde `IF OBJECT_ID('DM_VENTILATION_SAGE_CACHE') IS NULL`) plutôt
       qu'un nouveau `CREATE TABLE`, pour préserver les données déjà en cache sur les bases
       existantes (même principe que TASK-065).
     - Littéraux SQL dans le code : `Declaration.Orchestration/VentilationSageCacheRepository.cs`,
       `Declaration.Application/Services/DeclarationWorkflowService.cs`,
       `Declaration.Infrastructure/Repositories/DeclarationRepository.cs`,
       `Declaration.Application/Interfaces/IDeclarationRepository.cs`,
       `Declaration.Orchestration/OrchestrateurDeclaration.cs`, `DeclarationTVA.sql`.
     - Tests : `Declaration.Orchestration.Tests/Task024CacheVentilationSageTests.cs`.
     - **Noms de classes C#** (`VentilationSageCacheRepository`, DTO éventuels) **non concernés** —
       seul le nom de la table SQL change (même règle que TASK-065, Dapper mappe par colonne).
  5. **Mise à jour de `connections.json` / `connections.json.exemple`** (TASK-044) et de la
     documentation (`LANCEMENT_DEV.md`) pour refléter le nouveau modèle — `GrfConnection` et
     `PersistenceConnection` restent des valeurs **uniques** (confirmé PO) ; la section statique
     `SageConnection`/`SageOM` de `connections.json` devient soit un **fallback** (SO_Id sans
     ligne `P_SOCIETE` exploitable), soit obsolète — à trancher à l'implémentation selon ce que
     révèle la vérification technique (étape 1).
  6. **Clarifier le moment de résolution** : le PO dit « au moment de l'authentification » — dans
     le code actuel, `AuthController.Login` ne détermine qu'un `SocieteId` par défaut/claims, la
     base Sage n'est résolue qu'au moment du traitement (`BuildOrchestrateur`). Décider si le
     login doit **valider dès la connexion** que la base Sage du/des `SO_Id` autorisé(s) est
     joignable (fail-fast visible à l'utilisateur), ou si la résolution différée au traitement
     (avec échec explicite à ce moment) suffit.
- **Exclu** :
  - Multi-société **simultanée** dans une même session (toujours hors scope, cf. TASK-066 —
    inchangé par cette task : une session reste bornée à un `SO_Id` à la fois).
  - Migration des données Sage existantes entre bases, réconciliation de données historiques
    entre plusieurs bases Sage.
  - Choix définitif de durcissement des secrets Sage OM par société (coffre-fort, chiffrement) —
    à cadrer séparément si l'option A avec mots de passe par société est retenue ; cette task se
    limite à ne pas aggraver silencieusement l'exposition actuelle (documenter le risque).
  - Refonte de `TASK-101` (multi-version DLL interop COM) — orthogonal : la version d'interop
    dépend du poste/Sage installé, pas du `SO_Id`/base cible.

## Objectif
```
Entrée  : SageConnection unique et globale (connections.json), SO_Id filtrant déjà les données
          au sein d'une seule base GRF/Sage, cache GRC_VENTILATION_SAGE_CACHE clé (EC_Id, Taux),
          coordonnées Sage par société déjà présentes nativement dans P_SOCIETE (SO_ErpDb/
          SO_ErpUserApp/SO_ErpPasswdApp) mais jamais lues par GRF
Étapes  : IDbConnectionFactory résolu par soId (Server/User/Password = GrfConnection, Database =
          SO_ErpDb ; credentials OM = SO_ErpUserApp/SO_ErpPasswdApp) → propagation dans
          BuildOrchestrateur (4 sites) → correctif clé de cache (ajout SO_Id, sans FK)
          → connections.json mis à jour
Sortie  : GrfConnection/PersistenceConnection restent uniques ; SageConnection (+ WorkerConfig
          Sage OM) résolue dynamiquement par SO_Id depuis P_SOCIETE ; aucune collision de cache
          possible entre deux bases Sage distinctes ; échec explicite si SO_Id sans SO_ErpDb
```

## Étapes
1. ~~Vérification technique~~ — ✅ **faite et tranchée** (colonnes confirmées, usage exact validé
   par le PO — une seule ligne `SO_Id=1` en dev, obtenir un 2e jeu réel avant le VERIFY, ou
   documenter la limite si impossible).
2. Modifier `IDbConnectionFactory`/`DbConnectionFactory` : ajouter la résolution par `soId`
   (lecture `P_SOCIETE.SO_ErpDb` via `GrfConnection`, chaîne Sage = Server/User/Password de
   `GrfConnection` + `Database=SO_ErpDb`), échec explicite (exception) si `SO_Id` introuvable ou
   `SO_ErpDb` vide/NULL — pas de repli silencieux sur la `SageConnection` statique. Les
   identifiants Sage OM (`WorkerConfig.User`/`Password`) restent lus depuis `SO_ErpUserApp`/
   `SO_ErpPasswdApp`.
3. Propager `soId` dans `DeclarationWorkflowService.BuildOrchestrateur` et ses 4 appelants
   (lignes 223, 472, 510, 572) via `declaration.SocieteId` déjà en scope.
4. Migration SQL idempotente unique pour `GRC_VENTILATION_SAGE_CACHE` → `DM_VENTILATION_SAGE_CACHE`
   (`sp_rename` + renommage index/contraintes) **et** ajout de la colonne `SO_Id` (INT, **sans**
   contrainte `FOREIGN KEY` vers `P_SOCIETE`) à la clé primaire, back-fill des lignes déjà
   présentes avec le `SO_Id` unique actuellement en usage. Mise à jour des littéraux SQL dans les
   6 fichiers de code identifiés + 1 fichier de tests (cf. Périmètre §4bis).
5. Auditer tout autre usage supposant `EC_Id` (ou tout identifiant Sage) globalement unique hors
   cache (grep ciblé, à documenter dans le VERIFY même si le résultat est « rien trouvé »).
6. Mettre à jour `connections.json.exemple`, `LANCEMENT_DEV.md` : la `SageConnection`/`SageOM`
   statique devient fallback ou obsolète selon ce que révèle l'étape 1.
7. Décider et implémenter le point de validation (login fail-fast vs résolution différée, cf.
   Périmètre §6).
8. `VERIFY/TASK-118_verify.md` avec preuve réelle sur **au moins deux `SO_Id` pointant vers deux
   bases Sage physiquement différentes** (pas seulement deux sociétés dans la même base).

## Livrables
- Vérification technique documentée des colonnes `P_SOCIETE.SO_Erp*` (capture réelle).
- `IDbConnectionFactory`/`DbConnectionFactory` modifiés (résolution par `soId` depuis `P_SOCIETE`).
- `DeclarationWorkflowService.BuildOrchestrateur` et ses 4 appelants mis à jour.
- Migration SQL du cache `GRC_VENTILATION_SAGE_CACHE` → `DM_VENTILATION_SAGE_CACHE` (rename +
  clé composite + colonne `SO_Id`, sans FK) + mise à jour des 6 fichiers de code + 1 test.
- `connections.json.exemple` / `LANCEMENT_DEV.md` à jour.
- `VERIFY/TASK-118_verify.md` : preuve réelle —
  - deux `SO_Id` réels, deux bases Sage physiquement distinctes, valorisation correcte pour
    chacun sans mélange ;
  - `EC_Id` identique dans les deux bases Sage de test → deux ventilations distinctes en cache,
    aucune collision/écrasement ;
  - `SO_Id` sans coordonnées Sage configurées → échec explicite (pas de silence, pas de repli sur
    une autre base) ;
  - `GrfConnection`/`PersistenceConnection` inchangées (une seule valeur, jamais résolues par
    `soId`).

## Critères de validation
- Aucune régression sur le modèle mono-Sage existant (un seul `SO_Id` configuré = comportement
  actuel inchangé).
- `GrfConnection` et `PersistenceConnection` restent des valeurs **uniques**, non paramétrées par
  `SO_Id` (exigence PO explicite).
- Échec **fail-fast et visible**, jamais silencieux, si un `SO_Id` traité n'a pas de coordonnées
  Sage résolues.
- Cache de ventilation Sage sans collision possible entre deux bases Sage distinctes.
- Aucun secret Sage OM exposé plus largement qu'aujourd'hui sans durcissement documenté.

## Risques / dépendances
- ~~Vérification technique préalable~~ — ✅ **levée** (colonnes confirmées en base réelle,
  18/07/2026). **Reste un risque de test** : un seul `SO_Id` existe dans l'environnement de dev
  actuel — le VERIFY ne pourra prouver le scénario multi-bases réel que si un second jeu
  société/base Sage devient disponible ; sinon le documenter explicitement comme réserve non
  bloquante plutôt que de simuler un faux positif.
- ~~Rôle non clarifié de `SO_ErpUser`/`SO_ErpPasswd`/`SO_ErpAuth`~~ — ✅ **tranché** (18/07/2026,
  confirmation PO) : hors périmètre GRF, non utilisées.
- **Cache `GRC_VENTILATION_SAGE_CACHE`** : risque majeur documenté ci-dessus — à traiter dans
  cette même task, pas en dette différée (une collision produirait une TVA valorisée à partir de
  la facture d'une **autre société**, silencieusement).
- **Pas de `FOREIGN KEY`** sur la nouvelle colonne `SO_Id` du cache (exigence PO, pour ne pas
  impacter le modèle EF de l'application principale propriétaire de `P_SOCIETE`) : l'intégrité
  référentielle n'est donc **pas garantie par le SGBD** — un `SO_Id` orphelin est possible côté
  cache si une société est supprimée côté application principale ; à documenter comme dette
  acceptée (contrainte métier), pas une négligence.
- **`SO_ErpUserApp`/`SO_ErpPasswdApp` en base, non chiffrés (a priori)** : ces identifiants Sage OM
  vivent déjà dans `P_SOCIETE` (colonnes de l'application principale, hors périmètre GRF) — GRF
  se contente de les **lire**, mais hérite du niveau de protection existant. Si ces colonnes sont
  en clair, c'est une dette préexistante à l'application principale, pas introduite par GRF ; à
  signaler si constaté, pas à corriger dans cette task (hors périmètre : autre application).
- **Dépend de TASK-066** (déjà livrée) : `SO_Id` source de vérité côté combobox/login/claims — ce
  socle est réutilisé tel quel, non remis en cause.
- **Distinct de TASK-101** (multi-version interop COM) : ne pas confondre « quelle DLL interop »
  (par poste, déjà traité) et « quelle base Sage cible » (par `SO_Id`, objet de cette task).
- **Distinct de TASK-044** (déploiement mono-dossier) : le modèle **mono-service/mono-dossier**
  reste valide (toujours un seul déploiement, un seul `connections.json`) — seul le **contenu**
  de la résolution `SageConnection` change (statique → paramétrée par `SO_Id`), pas l'architecture
  de déploiement.
- **Périmètre potentiellement large** : selon le nombre de points de code qui supposent
  aujourd'hui une base Sage unique (au-delà des 4 sites `BuildOrchestrateur` déjà identifiés,
  cf. étape 5), le chiffrage réel peut dépasser une estimation initiale — l'audit de l'étape 5
  doit être fait et documenté avant de considérer le périmètre clos.
