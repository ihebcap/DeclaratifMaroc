# TASK-065 — Renommer les tables de persistance en `DM_ENTTVA` / `DM_LGTVA` (nommage conforme)

> **Origine :** revue PO 13/07/2026. Les tables de persistance du module (`DeclarationEntete`,
> `LigneCandidate`) sont en PascalCase C# posé tel quel en base, non conforme à la convention legacy
> (`PREFIXE_XX_`). Cible PO : `DM_ENTTVA` (entête déclaration TVA) et `DM_LGTVA` (lignes candidates TVA).

## Constat (preuve code, aucune supposition)
1. **Un seul DDL fait foi = `DeclarationTVA.sql`** (22 occurrences `DeclarationEntete`/`LigneCandidate`) —
   c'est le script réellement déployé (`Declaration.API/Program.cs:70`, `LANCEMENT_DEV.md:36`
   `sqlcmd -i DeclarationTVA.sql`) et le seul aligné sur l'app (Guid stocké en **texte** `NVARCHAR(36)`,
   colonne `NumeroRapprochement`, idempotent, ALTER de rattrapage TASK-055/057).
1bis. **`Declaration.Infrastructure/SQL/001_Schema_TVA.sql` est MORT et incompatible** — référencé nulle
   part (aucun runner/doc/script), non idempotent, et déclare `Id UNIQUEIDENTIFIER` alors que l'app écrit
   le Guid en texte (`DeclarationRepository.cs:25` `Id = id.ToString()`) → son exécution casserait le
   mapping. **Décision architecte : le supprimer** dans cette TASK, ne pas le renommer/maintenir.
2. **Requêtes Dapper** — le nom de table apparaît en littéral SQL, indépendamment du nom de classe C# :
   `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` (22 occurrences,
   ex. `:25` `SELECT * FROM DeclarationEntete`, `:85` `INSERT INTO LigneCandidate`, `:257`
   `UPDATE LigneCandidate`). Dapper mappe par **nom de colonne** → renommer la table ne casse pas le
   mapping tant que les colonnes sont inchangées.
3. **Noms de CLASSES C# ≠ noms de TABLES** — `DeclarationEntete` et `LigneCandidate` sont aussi des
   classes du modèle (`WorkflowEntities.cs`), des DTO (`LigneCandidateDto.cs`) et un type TS
   (`DomainGrid.tsx`). **Ces noms de classes/DTO ne sont PAS concernés** par cette TASK — seul le nom
   de la table SQL change.

## Objectif
Renommer **uniquement les tables SQL** : `DeclarationEntete → DM_ENTTVA`, `LigneCandidate → DM_LGTVA`,
avec migration idempotente des bases déjà déployées, **sans toucher** aux noms de classes C#, DTO ni TS
(Dapper mappe par colonne, on ne change que les littéraux de table dans les requêtes).

## Périmètre
### A. DDL de création — `DeclarationTVA.sql` (script unique qui fait foi)
- `CREATE TABLE dbo.DeclarationEntete` → `dbo.DM_ENTTVA`, `CREATE TABLE dbo.LigneCandidate` → `dbo.DM_LGTVA`.
- Renommer index et contraintes en conséquence : `IX_DeclarationEntete_Numero` → `IX_DM_ENTTVA_Numero`,
  `IX_DeclarationEntete_Unicite` → `IX_DM_ENTTVA_Unicite`,
  `IX_LigneCandidate_DeclarationId_Domaine` → `IX_DM_LGTVA_DeclarationId_Domaine`,
  `FK_LigneCandidate_Declaration` → `FK_DM_LGTVA_DM_ENTTVA`.
- Adapter tous les `IF OBJECT_ID(...)`/`sys.indexes`/`sys.columns` de garde aux nouveaux noms.
- **Colonnes inchangées** : conserver `DeclarationId` etc. tels quels (sinon Dapper casse). Seuls les noms
  d'**objets** (table/index/FK) changent.

### A bis. Supprimer `Declaration.Infrastructure/SQL/001_Schema_TVA.sql`
Fichier mort et incompatible (cf. constat 1bis). Le retirer du dépôt ; vérifier au préalable qu'il n'est
cité dans aucun `.csproj`/runner/doc de déploiement (grep confirmé à ce jour : aucune référence exécutée).

### B. Requêtes repository (`DeclarationRepository.cs`)
- Remplacer les 22 littéraux `DeclarationEntete`/`LigneCandidate` dans les chaînes SQL par `DM_ENTTVA`/
  `DM_LGTVA`. **Ne pas toucher** aux types génériques Dapper (`QueryAsync<LigneCandidate>` = classe C#,
  reste inchangé).

### C. Migration des bases existantes (idempotente)
Bloc `sp_rename` conditionné (ne s'exécute que si l'ancienne table existe et la nouvelle pas encore) :
```sql
IF OBJECT_ID('dbo.DeclarationEntete','U') IS NOT NULL AND OBJECT_ID('dbo.DM_ENTTVA','U') IS NULL
    EXEC sp_rename 'dbo.DeclarationEntete', 'DM_ENTTVA';
IF OBJECT_ID('dbo.LigneCandidate','U') IS NOT NULL AND OBJECT_ID('dbo.DM_LGTVA','U') IS NULL
    EXEC sp_rename 'dbo.LigneCandidate', 'DM_LGTVA';
-- puis sp_rename des index/contraintes de la même manière (gardés par IF EXISTS)
```
`sp_rename` conserve les données et les FK ; vérifier que les noms d'index/contraintes sont aussi renommés
pour éviter des noms « fantômes » pointant l'ancien libellé.

## Points tranchés (décision architecte 13/07/2026)
1. **Script qui fait foi = `DeclarationTVA.sql`** ; `001_Schema_TVA.sql` supprimé (mort + incompatible
   `UNIQUEIDENTIFIER`). Plus de double DDL à maintenir.
2. **On ne renomme QUE les tables** (+ index/FK), jamais les colonnes → aucun risque de mapping Dapper.

## Garde-fous
1. **Aucun renommage de classe C#/DTO/TS** — churn inutile et risque de régression front. Seuls les
   littéraux SQL de table changent.
2. **Idempotence** : `sp_rename` gardé par `IF OBJECT_ID(...)`, ré-exécutable sans erreur.
3. **Script unique** : après cette TASK, `DeclarationTVA.sql` est le seul DDL ; `001_Schema_TVA.sql` n'existe
   plus. Ne pas recréer un second script.
4. **Vérifier qu'aucune autre requête** hors `DeclarationRepository.cs` ne référence ces tables en littéral
   (grep sur les tables, pas sur les classes) avant clôture.

## Livrables de preuve (VERIFY)
1. Grep `FROM DeclarationEntete|INTO DeclarationEntete|LigneCandidate` en contexte SQL = 0 occurrence
   résiduelle dans le code exécuté (les classes C# restent, c'est attendu).
2. Base **neuve** créée via script → tables `DM_ENTTVA`/`DM_LGTVA` présentes, FK/index renommés.
3. Base **déjà déployée** → `sp_rename` migre sans perte, ré-exécution sans erreur.
4. Non-régression bout-en-bout : créer une déclaration, sauvegarder des lignes candidates, clôturer,
   rouvrir — via l'app, aucune erreur SQL « invalid object name ».

## Dépendances / risques
- **Recouvre TASK-061** (FLOAT→DECIMAL sur la même table `LigneCandidate`/`DM_LGTVA`) : **ordonnancer les
  deux** pour éviter deux migrations concurrentes sur la même table. Recommandation : faire TASK-061 puis
  TASK-065, ou fusionner les deux blocs de migration.
- **Risque déploiement** : si une déclaration est déjà persistée en prod, `sp_rename` est le bon outil
  (conserve données/FK) — mais tester sur copie avant prod.
