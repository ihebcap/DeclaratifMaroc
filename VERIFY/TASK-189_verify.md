# VERIFY — TASK-189

Date : 2026-07-27
Agent : Worker (session Développeur, prompt `DOCS/PROMPT-WORKER-TASK-189-27-07-2026.md`)

## Périmètre livré

Comble le trou laissé par TASK-187 (documenté dans `DONE_DETAIL/TASK-187_verify.md` et confirmé
indépendamment par l'architecte) : la colonne `Reference` était propagée jusqu'à
`ConstructeurDeclaration`/`Exporter.cs` mais jamais persistée sur `DM_LGTVA`, donc jamais visible dans
un export réel généré par l'API (`ConstruireModeleExportAsync`/`ConstruireModeleControleAsync`, qui
lisent `LigneCandidate`/`DM_LGTVA` directement, jamais `ConstructeurDeclaration`).

1. **Migration SQL additive et idempotente** sur `DM_LGTVA` :
   - `Declaration.Infrastructure/SQL/011_DM_LGTVA_Reference.sql` (nouveau, même patron que
     `008_DM_ENTTVA_DT_Id.sql`/TASK-094).
   - Même bloc fusionné dans `DeclarationTVA.sql` (script d'installation), juste après le bloc
     `CodeActiviteModifieLe` (TASK-161), avant la création de l'index `IX_DM_LGTVA_DeclarationId_Domaine`.
   - `ALTER TABLE DM_LGTVA ADD Reference NVARCHAR(200) NULL`, gardé par
     `IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS ...)`.
2. **Modèle** : `LigneCandidate.Reference` (`string?`) ajouté (`Declaration.Application/Entities/
   WorkflowEntities.cs`), à côté de `NumeroFacture`.
3. **Persistance** : `DeclarationRepository.SaveLignesCandidatesAsync` — `Reference` ajoutée à la liste
   de colonnes `INSERT` et au paramètre anonyme. `GetLignesAsync` (`SELECT *`) inchangée — confirmé que
   le mapping Dapper se fait automatiquement (colonne SQL + propriété C# de même nom).
4. **Figeage** : `DeclarationWorkflowService.MapLignesCandidates` — `Reference = c.Affectation.Reference`
   ajouté sur les **deux** branches de construction (branche « facture introuvable ou non ventilée » ET
   branche normale « taxesLines »), confirmé qu'il n'y a bien que ces deux branches dans cette méthode.
5. **Lecture pour export** : `ConstruireModeleExportAsync` (export officiel, Clôturée) et
   `ConstruireModeleControleAsync` (export de contrôle, TASK-160) — `Reference = l.Reference` ajouté sur
   chaque `LigneDeclarationEnrichie` construite. Le champ existait déjà sur ce type depuis TASK-187,
   seule l'affectation manquait.

`ConstructeurDeclaration.cs`/`SelectionExpliqueeService.cs`/`SelectionExpliqueeEvaluator.cs`/
`Exporter.cs` **non touchés**, conformément au périmètre strict (déjà corrects depuis TASK-187).

## Point additionnel découvert — recherche exhaustive demandée par la TASK (§ Risques)

La TASK demandait explicitement de reconfirmer par une recherche exhaustive qu'aucun autre module ne
lit/construit `LigneCandidate`/`DM_LGTVA` en dehors des 5 points listés. Recherche faite
(`grep "new LigneCandidate"` sur tout le repo) : un **3ᵉ site de construction** existe,
`DeclarationWorkflowService.RecalculerLigneDepuisCacheAsync` (TASK-147, ~ligne 1678) — reconstruit des
`LigneCandidate` depuis une ligne déjà existante (`reference = lignes[0]`) en conservant à l'identique
les colonnes non-financières (facture/tiers/paiement/source), exactement comme `CodeActivite` l'a fait
pour TASK-161 (commentaire déjà présent à cet effet dans le code).

**Sans intervention, ce chemin aurait introduit une régression silencieuse** : recalculer une ligne déjà
figée (et déjà porteuse d'une vraie `Reference` après cette migration) via cette action TASK-147 aurait
effacé sa `Reference` (jamais copiée depuis `reference.Reference`), alors que l'intention documentée de
cette méthode est justement de préserver ces champs identiques. J'ai ajouté `Reference =
reference.Reference,` à cet endroit, même principe que `CodeActivite` juste en dessous — décision
documentée ici plutôt qu'improvisée silencieusement, car ce point n'était pas dans la liste des 5 du
périmètre strict mais découle directement de la vérification exhaustive demandée par la TASK elle-même.
Aucun autre site de construction de `LigneCandidate` trouvé (les autres occurrences sont des fixtures de
tests). Testé (`RecalculerLigneDepuisCacheAsync_LigneAvecReference_ReferenceConservee`).

La DTO `LigneCandidateDto` (écran ② Vérifier & Intégrer, `Declaration.API/Dtos/LigneCandidateDto.cs`)
n'a pas été étendue — hors périmètre écrit de cette TASK (objectif = export réel, pas la grille écran),
non touchée.

## Décision documentée — type/longueur de la colonne SQL `Reference`

La TASK signalait ce point comme potentiellement ambigu. Vérifié sur `GR_EMA_DISTRIBUTION` : source
réelle `RT_ECHEANCE.DO_Reference` est `NVARCHAR(MAX)`, mais la longueur maximale réellement observée sur
3811 lignes est de **25 caractères** (`SELECT MAX(LEN(DO_Reference))`). Retenu **NVARCHAR(200)** : marge
large sans reprendre le `MAX` de la source, cohérent avec la convention déjà en place sur `DM_LGTVA`
(`MotifRejet` 500, `TiersNom` 255, `NumeroFacture`/`ModePaiement`/`Source` 100). `NULL` (pas `NOT NULL
DEFAULT ''`) : même convention que ces mêmes colonnes texte.

## Fichiers modifiés/créés

- `Declaration.Infrastructure/SQL/011_DM_LGTVA_Reference.sql` — nouveau, migration idempotente.
- `DeclarationTVA.sql` — même migration fusionnée (script d'installation global).
- `Declaration.Application/Entities/WorkflowEntities.cs` — `LigneCandidate.Reference`.
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` — `Reference` dans l'`INSERT` de
  `SaveLignesCandidatesAsync`.
- `Declaration.Application/Services/DeclarationWorkflowService.cs` :
  - `MapLignesCandidates` (2 branches) — `Reference = c.Affectation.Reference`.
  - `ConstruireModeleExportAsync`/`ConstruireModeleControleAsync` — `Reference = l.Reference`.
  - `RecalculerLigneDepuisCacheAsync` (TASK-147, point additionnel ci-dessus) — `Reference =
    reference.Reference`.
- `Declaration.Orchestration.Tests/Task189ReferencePersistanceDmLgtvaTests.cs` — nouveau fichier, 7 tests
  (détail ci-dessous).

## Tests ajoutés (Étape 5 de la TASK)

Nouveau fichier dédié, patron identique à `Task161CodeActiviteCascadeTests.cs` (FakeRepository en
mémoire implémentant réellement `SaveLignesCandidatesAsync`/`GetLignesAsync`, contrairement au
`FakeRepository` de `Task160ExportControleTests.cs` où `SaveLignesCandidatesAsync` reste un stub
`NotImplementedException` — non modifié, pour ne pas changer le comportement des tests existants qui
l'utilisent) :

1. `MapLignesCandidates_BrancheTaxesLignes_ReferencePropagee` — branche normale, `Reference` renseignée.
2. `MapLignesCandidates_BrancheFactureIntrouvable_ReferencePropagee` — 2ᵉ branche (« facture
   introuvable »), `Reference` renseignée.
3. `MapLignesCandidates_ReferenceNull_AucuneExceptionEtRestNull` — `Reference` NULL, aucune exception.
4. `CycleComplet_LigneNouvellementFigee_ReferenceVisibleDansConstruireModeleControleAsync` — **cycle
   complet** : `MapLignesCandidates` → `SaveLignesCandidatesAsync` (simulé) → `ConstruireModeleControleAsync`
   → `Reference` visible sur la `LigneDeclarationEnrichie` produite.
5. `CycleComplet_LigneNouvellementFigeeIntegree_ReferenceVisibleDansConstruireModeleExportAsync` — même
   cycle complet via `ConstruireModeleExportAsync` (export officiel, ligne `Etat=Integree`).
6. `CycleComplet_LigneSansReference_RestNullSansException` — même cycle, `Reference=null`, aucune
   exception, colonne vide.
7. `RecalculerLigneDepuisCacheAsync_LigneAvecReference_ReferenceConservee` — non-régression TASK-147
   (point additionnel ci-dessus).

## Rejeu réel — GR_EMA_DISTRIBUTION / DESKTOP-5BFKKEP

### 1. État avant migration
```
COUNT(*) DM_LGTVA = 5148
Colonne Reference absente (0 ligne dans INFORMATION_SCHEMA.COLUMNS)
```

### 2. Exécution de la migration (1ʳᵉ fois)
`sqlcmd -i Declaration.Infrastructure/SQL/011_DM_LGTVA_Reference.sql` → succès, aucune erreur.
```
SELECT COUNT(*) AS Total, SUM(CASE WHEN Reference IS NULL THEN 1 ELSE 0 END) AS NullCount FROM DM_LGTVA;
Total=5148, NullCount=5148
```
→ Confirmé : les **5148 lignes déjà persistées restent lisibles**, `Reference = NULL` pour toutes
(aucune perte de données, aucune erreur de lecture, exactement le comportement attendu — même réserve
que documentée dans TASK-186/187 pour les déclarations déjà closes).

### 3. Exécution de la migration (2ᵉ fois — preuve d'idempotence)
Rejouée intégralement une seconde fois → **exit code 0, aucune erreur, aucun changement** (le `IF NOT
EXISTS` empêche le `ALTER TABLE` en double). Colonne finale confirmée :
```
DATA_TYPE=nvarchar, CHARACTER_MAXIMUM_LENGTH=200, IS_NULLABLE=YES
```

### 4. Démonstration mécanique de la colonne (insertion/lecture/nettoyage dédiés)
Faute de pouvoir déclencher un cycle de figeage réel via l'API (cf. section suivante), démontré au
niveau SQL pur que la colonne fonctionne comme prévu pour une **ligne dédiée de test** (jamais une ligne
réelle existante) :
```sql
INSERT INTO DM_LGTVA (..., Reference, ...) VALUES ('TASK189-TEST-...', ..., 'FCN-TASK189-REF-TEST', ...);
SELECT Id, NumeroFacture, Reference FROM DM_LGTVA WHERE Id = 'TASK189-TEST-...';
-- → Reference = 'FCN-TASK189-REF-TEST' relue correctement
DELETE FROM DM_LGTVA WHERE Id = 'TASK189-TEST-...';
-- → COUNT après nettoyage = 0 (aucune pollution laissée dans la base réelle)
```

### Point honnête — critère de validation principal (ligne nouvellement figée → export réel via l'API)

**Non démontré via un cycle de figeage réel déclenché par l'application elle-même.** Comme pour
TASK-186/187/188, `Declaration.API.exe` (PID 33068) tourne toujours sur ce poste et verrouille son
propre dossier de sortie — je n'ai pas pu le redémarrer (accès refusé par l'OS, déjà documenté et
revérifié, aucun changement). Déclencher un vrai cycle figeage → persistance → export nécessiterait soit
de faire tourner l'API buildée à neuf (bloqué par ce verrou), soit d'écrire un test d'intégration C#
avec les identifiants `sa`/`1234` en dur dans le code (interdit, `ARCHITECTURE.md §5`).

La preuve apportée à la place, la plus proche possible sans ces deux options :
1. **Mécanique SQL réelle** de la colonne elle-même (insertion/lecture/idempotence, ci-dessus, sur la
   vraie base `GR_EMA_DISTRIBUTION`) — prouve que `DM_LGTVA.Reference` fonctionne exactement comme
   `SaveLignesCandidatesAsync`/`GetLignesAsync` l'utiliseront en production.
2. **Cycle complet applicatif simulé** (tests 4/5/6 ci-dessus) — prouve que le code C# lui-même (
   `MapLignesCandidates` → `SaveLignesCandidatesAsync` → `GetLignesAsync` → `ConstruireModele*Async`)
   propage correctement une `Reference` depuis une ligne **nouvellement figée** (jamais une ligne déjà
   en base avant cette TASK) jusqu'à la `LigneDeclarationEnrichie` que `Exporter.cs` consommera.

Les deux preuves mises bout à bout couvrent l'intégralité de la chaîne réelle (colonne SQL + code
applicatif), mais séparément plutôt qu'en un seul export `.xlsx` réellement généré par l'API tournante.
Jugé équivalent mais **pas identique** à la démonstration littérale demandée — signalé explicitement
plutôt que présenté comme équivalent sans réserve.

## Build

- `Declaration.Core`, `Declaration.Application`, `Declaration.Infrastructure` → build individuel OK,
  0 erreur (warnings préexistants uniquement, aucun nouveau).
- `dotnet build DeclarationTVA.slnx` (solution complète) : **même blocage environnemental que
  TASK-186/187/188** — `Declaration.API.exe` (PID 33068) toujours actif, verrou toujours refusé par
  l'OS (revérifié via `tasklist`, aucun changement).

## Tests

- `Declaration.Core.Tests` → **89/89** verts (inchangé, ce changement ne touche pas ce projet).
- `Declaration.Selection.Tests` → **59/60** verts — même échec préexistant non lié
  (`IntegrationRegressionTests`, auth SQL locale Windows, déjà documenté TASK-186/187/188).
- `Declaration.Orchestration.Tests` → **196/196** verts (189 existants + 7 nouveaux) — buildé/exécuté via
  `dotnet test Declaration.Orchestration.Tests --output <dossier temporaire>` pour contourner le même
  verrou de process (ce projet référence `Declaration.API.csproj` directement).
- `Declaration.Export.Excel.Tests` : non requis par cette TASK (`Exporter.cs` non touché) — non rejoué.

## Validation checklist

- [x] Migration SQL additive et idempotente — rejouée deux fois sans erreur, colonne finale
      `NVARCHAR(200) NULL` confirmée.
- [x] Aucune table `apbs-gr_winform` touchée — seule `DM_LGTVA` (table `DM_*`, possédée par GRF)
      modifiée.
- [x] Lignes déjà persistées (5148) restent lisibles avec `Reference = NULL`, aucune erreur.
- [x] `LigneCandidate.Reference` + `SaveLignesCandidatesAsync` (INSERT) + `MapLignesCandidates` (2
      branches) + `ConstruireModeleExportAsync`/`ConstruireModeleControleAsync` — tous les 5 points du
      périmètre strict livrés et testés.
- [x] Build OK (bibliothèques concernées individuellement — solution complète bloquée par
      l'environnement, pas par le code).
- [x] Tests passés (Core 89/89, Orchestration 196/196 ; Selection 59/60, échec préexistant non lié).
- [x] Aucun bypass sécurité (pas de credential en dur — cf. section « Point honnête » ci-dessus sur le
      test d'intégration non écrit pour cette raison précise).
- [~] Critère de validation principal (ligne nouvellement figée visible dans un export réel généré par
      l'API) : **NON démontré littéralement** — preuve équivalente mais scindée en deux (mécanique SQL
      réelle + cycle applicatif simulé), cf. section dédiée. Documenté honnêtement, pas présenté comme
      validé sans réserve.
- [x] Aucune dette technique silencieuse — point additionnel `RecalculerLigneDepuisCacheAsync` (TASK-147)
      découvert et corrigé plutôt que laissé en régression silencieuse, documenté ci-dessus.

## Impacts détectés

- `LigneCandidateDto` (écran ② Vérifier & Intégrer) non étendue — hors périmètre écrit (objectif =
  export réel, pas la grille). Signalé pour arbitrage futur si le PO veut aussi voir `Reference` sur
  cet écran.
- `RecalculerLigneDepuisCacheAsync` (TASK-147) — corrigé pour éviter une régression silencieuse, cf.
  section dédiée en tête de ce VERIFY.
- Aucun autre module identifié lisant/construisant `LigneCandidate`/`DM_LGTVA` en dehors des points
  listés (recherche exhaustive faite, cf. section dédiée).

## Notes worker

- Le point le plus important de cette TASK — démontrer qu'une ligne **nouvellement figée** porte la
  référence jusqu'à un export réel — n'a pu être prouvé qu'en deux morceaux distincts (SQL réel + cycle
  applicatif simulé), pas en un seul export `.xlsx` généré par l'API tournante, pour la même raison
  environnementale que TASK-186/187/188 (process verrouillé, pas de redémarrage possible). Documenté
  sans détour dans la section « Point honnête » ci-dessus.
- Le point additionnel `RecalculerLigneDepuisCacheAsync` (TASK-147) découle directement de la demande
  explicite de la TASK de « reconfirmer par une recherche exhaustive » — pas une extension de périmètre
  de ma propre initiative, mais la diligence demandée qui a mis au jour un risque de régression réel.

## Reste à valider (NON couvert par ce VERIFY)

1. Export réel `.xlsx` régénéré par l'API tournante sur une ligne nouvellement figée — non produit,
   cf. section « Point honnête » (limite d'environnement identique à TASK-186/187/188).
2. Build de la solution complète non rejoué avec succès — même blocage `Declaration.API.exe` non résolu.
3. `LigneCandidateDto` (écran ②) : décision de ne pas l'étendre documentée, à confirmer par le PO si
   souhaité en dehors du périmètre export.

## Verdict

Les 5 points du périmètre strict sont livrés et testés (migration idempotente vérifiée deux fois sur la
base réelle, 5148 lignes existantes non affectées, cycle applicatif complet figeage → persistance →
relecture → export couvert par 7 tests dédiés). Un point additionnel de régression silencieuse
(`RecalculerLigneDepuisCacheAsync`, TASK-147) a été découvert par la recherche exhaustive demandée et
corrigé. **Réserve principale, honnêtement non résolue** : le critère de validation le plus important de
la TASK (ligne nouvellement figée visible dans un export réel généré par l'API tournante) n'a pas pu
être démontré littéralement — la preuve apportée (mécanique SQL réelle + cycle applicatif simulé,
séparément) est la plus proche possible sans redémarrer le process verrouillé ni coder un secret en dur.
Recommandation : si une preuve littérale est requise avant clôture, elle nécessitera soit un redémarrage
de poste (libère le verrou du process), soit un environnement de test dédié avec des identifiants
configurés hors du code source.
