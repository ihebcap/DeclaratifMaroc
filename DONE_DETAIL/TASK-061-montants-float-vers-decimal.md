# TASK-061 — Aligner les montants persistés en DECIMAL(18,6) au lieu de FLOAT

> **Origine :** revue de schéma juillet 2026. Les colonnes de montants/taux de `dbo.LigneCandidate`
> sont déclarées en `FLOAT` alors que le modèle C# correspondant est déjà en `decimal`. On écrit donc
> du `decimal` dans une colonne à virgule flottante binaire → arrondis binaires possibles + conversion
> `decimal`↔`double` à chaque lecture/écriture Dapper. Pour des montants de TVA (base d'une déclaration
> fiscale), le type flottant est proscrit.

## Constat (preuve code, aucune supposition)
1. **Colonnes `FLOAT`** — `DeclarationTVA.sql:49-54` (`CREATE TABLE dbo.LigneCandidate`) :
   `HT`, `Taux`, `TVA`, `TTC`, `Prorata`, `MontantAffecte` sont toutes en `FLOAT NULL`.
   Idem les deux `ALTER TABLE ... ADD` de rattrapage `DeclarationTVA.sql:70,74` (`Prorata`,
   `MontantAffecte`) et le commentaire d'en-tête `DeclarationTVA.sql:8` (`montants -> FLOAT`).
2. **Le modèle C# est déjà en `decimal`** — `Declaration.Application/Entities/WorkflowEntities.cs:56-67`
   (`LigneCandidate`) : `HT`, `Taux`, `TVA`, `TTC`, `Prorata`, `MontantAffecte` sont des `decimal`.
   Même chose dans `Declaration.Core/Model.cs:50-54` et les DTO API (`LigneCandidateDto.cs:76,79`).
3. **Désalignement de type, pas simple cosmétique** : `decimal` (base 10, exact) stocké/relu via une
   colonne `FLOAT` (base 2, approché) → risque d'arrondi et conversion implicite Dapper à chaque I/O.

## Objectif
Passer les 6 colonnes de montants/taux de `dbo.LigneCandidate` de `FLOAT` à `DECIMAL(18,6)`, aligné sur
le `decimal` côté C#, et **couvrir les bases déjà déployées** (le script est idempotent : un simple
changement du `CREATE TABLE` ne migre pas une base existante).

## Périmètre
### A. Script de création (`DeclarationTVA.sql`)
- Remplacer `FLOAT` → `DECIMAL(18,6)` sur `HT`, `Taux`, `TVA`, `TTC`, `Prorata`, `MontantAffecte` dans
  le `CREATE TABLE` (`:49-54`) et dans les deux `ALTER ... ADD` (`:70,74`).
- Mettre à jour le commentaire d'en-tête (`:8`) : `montants -> DECIMAL(18,6)`.

### B. Migration des bases existantes (idempotente)
Ajouter un bloc `ALTER COLUMN` en fin de script, conditionné pour ne s'exécuter que si le type courant
n'est pas déjà `decimal` (via `sys.columns`/`sys.types`), pour ne pas re-migrer inutilement :
```sql
ALTER TABLE dbo.LigneCandidate ALTER COLUMN HT             DECIMAL(18,6) NULL;
ALTER TABLE dbo.LigneCandidate ALTER COLUMN Taux           DECIMAL(18,6) NULL;
ALTER TABLE dbo.LigneCandidate ALTER COLUMN TVA            DECIMAL(18,6) NULL;
ALTER TABLE dbo.LigneCandidate ALTER COLUMN TTC            DECIMAL(18,6) NULL;
ALTER TABLE dbo.LigneCandidate ALTER COLUMN Prorata        DECIMAL(18,6) NULL;
ALTER TABLE dbo.LigneCandidate ALTER COLUMN MontantAffecte DECIMAL(18,6) NULL;
```

## Choix de précision (à confirmer en revue de TASK)
- **`DECIMAL(18,6)` recommandé** : ~10¹² avant la virgule, 6 décimales, 9 octets. Largement suffisant
  pour des montants MAD et des taux/prorata.
- `DECIMAL(24,6)` (13 octets) seulement si des cumuls > 10¹² sont attendus — **non justifié** ici a
  priori. Trancher avant implémentation, ne pas laisser l'implémenteur choisir seul.

## Garde-fous
1. **Aucune modification de la logique C#** : le modèle est déjà en `decimal`, la TASK ne touche que le
   DDL SQL. Ne pas modifier `WorkflowEntities.cs`, `Model.cs`, ni le repository.
2. **Idempotence préservée** : le bloc de migration `ALTER COLUMN` doit pouvoir se ré-exécuter sans
   erreur (garde `IF` sur le type courant).
3. **Vérifier l'absence d'index/contrainte** bloquant l'`ALTER COLUMN` sur ces colonnes (aucun index sur
   ces colonnes dans le script actuel — à re-confirmer sur la base réelle avant migration prod).

## Script qui fait foi (décision architecte 13/07/2026)
- Cible **unique = `DeclarationTVA.sql`** (seul DDL déployé, cf. `Program.cs:70`/`LANCEMENT_DEV.md:36`).
  L'autre fichier `Declaration.Infrastructure/SQL/001_Schema_TVA.sql` (déjà en `DECIMAL(18,4)` mais MORT et
  incompatible `UNIQUEIDENTIFIER`) est **supprimé par TASK-065** — ne pas le prendre comme référence ni le
  modifier ici. **Séquencer TASK-061 puis TASK-065** (ou fusionner les blocs de migration : même table).

## Hors périmètre (à noter, pas dans cette TASK)
- Deux modèles C# encore en `double` sur la même famille de montants, **non liés à cette table** :
  `Declaration.Orchestration/IVentilationSageCacheRepository.cs:12,15` (`Taux`, `TTC`) et
  `SageTaxReader/SageTaxReader.Contracts/DTOs.cs:7,10` (`Taux`, `TTC`). Harmonisation éventuelle à
  traiter dans une TASK dédiée si l'on veut du `decimal` de bout en bout (cache Sage + contrat reader).

## Livrables de preuve (VERIFY)
1. `DeclarationTVA.sql` diffé : 6 colonnes + 2 `ALTER ADD` + commentaire en `DECIMAL(18,6)`, plus le
   bloc de migration `ALTER COLUMN` idempotent.
2. Exécution du script sur une base **neuve** → `sys.columns` montre `decimal(18,6)` sur les 6 colonnes.
3. Exécution du script sur une base **déjà déployée en FLOAT** → migration effective vers `decimal(18,6)`
   sans perte de données, et ré-exécution sans erreur (idempotence).
4. Non-régression : lecture/écriture d'une `LigneCandidate` via l'app (aucune erreur de conversion Dapper).

## Dépendances / risques
- **Aucune dépendance** de code applicatif (modèle déjà en `decimal`).
- **Risque données** : `ALTER COLUMN FLOAT → DECIMAL` sur une base peuplée peut arrondir/tronquer des
  valeurs déjà entachées d'imprécision flottante — vérifier sur une copie avant migration prod.
