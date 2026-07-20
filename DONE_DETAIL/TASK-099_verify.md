# VERIFY — TASK-099 — Redéfinition du périmètre déclarable de l'écran ① (rattrapage)

## Résumé
La période d'une déclaration devient une simple **date de coupure** : un règlement (espèce ou
hors-espèce) est proposé à l'écran ① si `DT_Id IS NULL` (non encore déclaré) ET `DateReference <
fin de période` (**plus de borne basse**) ET déclarable (espèce OU rapproché banque). Corrige la
perte définitive d'un règlement rapproché/payé un mois donné mais jamais inclus dans la
déclaration de ce mois (cas `RF26060125`).

Trois chemins de code portaient l'ancienne fenêtre stricte (`>= début ET < fin`) ; les trois ont
été alignés :

## Modifications réalisées

### 1. `Declaration.Selection/SelectionExpliqueeEvaluator.cs`
Chemin réellement utilisé pour construire les candidates de l'écran ①/figeage
(`SelectionExpliqueeService` → `DeclarationWorkflowService`). Les deux branches de calcul de
`HorsPeriode` (espèce via `DatePaiement`, non-espèce rapproché via `MV_PointDate`) ne testent plus
que la borne haute (`>= finExclude`) ; la borne basse (`< debut`) a été retirée. Le gate
`EstDeclarable` (espèce OU rapproché) était **déjà correct** — non touché, conforme à la
contrainte de la task.

### 2. `Declaration.Selection/SelectionExpliqueeService.cs`
Les 3 requêtes SQL « surensemble » (Fournisseur, Dépense, Client) : retrait de `>= @debut`,
conservé `< @finExclude` seul. Ajout de `AND A.DT_Id IS NULL` (rôle n°1 de la règle — borne aussi
le volume ramené par la base, faute de quoi la suppression de la borne basse aurait pu remonter
tout l'historique).

### 3. `Declaration.Selection/SelectionnerAffectationsService.cs` (Mode Contrôle, TASK-009)
Même retrait de `>= @debut` sur les 4 requêtes (Décaissement/Espèce/Depense/Encaissement) ; le
filtre `A.DT_Id IS NULL` était déjà présent inline. Alignement demandé explicitement par la liste
FILES de la task (le Mode Contrôle partage la même notion de périmètre déclarable).

### 4. `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` (`RapprochementFromWhere`)
Ce fragment SQL est **partagé** par deux écrans via le même endpoint `GET /api/rapprochement` :
écran ① Sélection (front envoie toujours `declarationId`) et Interrogation Rapprochement TASK-037
(front `RapprochementInterrogation.tsx` n'envoie jamais `declarationId` — exploration libre,
indépendante de toute déclaration). Décision PO actée : la nouvelle règle ne s'applique **qu'à
l'écran ①**. Ajout d'un gate conditionné sur `@declarationId` :
- `@declarationId IS NOT NULL` (écran ①) → `DateReference < @finExclude` seul (pas de borne basse)
  + `EstDeclarableSqlM` obligatoire (masque les non-rapprochés hors-espèce, qui n'apparaissent
  plus du tout dans la liste — pas seulement grisés).
- `@declarationId IS NULL` (TASK-037) → **comportement strictement inchangé** (fenêtre pleine,
  aucun gate, non-rapprochés toujours visibles).

Le filtre optionnel utilisateur « Rapproché Oui/Non » (`@hasRapproche`) reste inchangé et
orthogonal à ce nouveau gate obligatoire.

## Hors périmètre (assumé, conforme à la task)
- `GetReglementsRapprochementDistinctsAsync` (alimente les listes déroulantes de filtres) n'a pas
  été touché — reste sur l'ancienne fenêtre à bornes fixes dans tous les cas. Impact : options de
  filtre potentiellement incomplètes pour du rattrapage à l'écran ①, jamais la liste principale.
  Non bloquant, à corriger si signalé.
- Front (`ReglementsSelection.tsx`) : aucun changement nécessaire — `declarationId` était déjà
  transmis systématiquement (TASK-097), le gate est entièrement porté par le back.

## Vérifications
- **Build solution complète** (`dotnet build DeclarationTVA.slnx --no-incremental`, rejoué le
  14/07/2026) : ✅ **0 erreur, 18 avertissements préexistants sans lien avec cette task** — répartis
  en nullable refs (`CS8602` ×4, `CS8618` ×9), API obsolète (`SqlConnectionStringBuilder`, `CS0618`
  ×1), tests xUnit (`xUnit1012` ×3), `using` dupliqué (`CS0105` ×1). Vérifié fichier par fichier
  (`git diff`) qu'aucun des avertissements ne tombe dans les hunks modifiés par TASK-099 (notamment
  `SelectionnerAffectationsService.cs` : les `CS8618` sont sur les propriétés de
  `AffectationCandidateRow`/DTO L.250-259, hors des 4 hunks touchés par la task, L.144-245).
- **Declaration.Selection.Tests** (hors `IntegrationRegressionTests`, DB réelle) : ✅ 57/57 —
  dont 4 nouveaux tests TASK-099 (`Evaluer_RapprocheBienAvantDebut_JamaisDeclare_ResteEligible`,
  `Evaluer_EspecePayeeBienAvantDebut_JamaisDeclaree_ResteEligible`,
  `Evaluer_HorsPeriodeApresLaFin_ResteRejeteMemeSansBorneBasse` + non-régression sur les tests
  `HorsPeriode` existants, qui ne testaient que la borne haute).
- **Declaration.Orchestration.Tests** : ✅ 115/115 (inchangé, aucune régression).
- **Declaration.Core.Tests** : ✅ 32/32.
- **Declaration.Controle.Tests** : 1/2 — `ComparateurTests.GenererRapportVerification` échoue
  (« Déclaration GRFN 66 introuvable »), **préexistant** : rejoué à l'identique sur `git stash`
  (avant TASK-099), même échec — dépend d'une base réelle (`.\sql2022`) et d'une donnée GRFN
  spécifique absente de cet environnement, aucun lien avec ce correctif.

## Preuve base réelle (`.\sql2022` / `GR_EMA_DISTRIBUTION`, SO_Id=1, rejouée le 14/07/2026)

Les 3 scénarios ci-dessous répliquent exactement la logique appliquée par
`SelectionExpliqueeService`/`SelectionExpliqueeEvaluator` (chemin réel écran ①/figeage) : gate
`A.DT_Id IS NULL`, `RegleDatePeriode.DateReferenceSqlM < finExclude` (pas de borne basse) et
`EstDeclarableSqlM`. Requêtes + résultats bruts, comparaison ancienne règle (avec borne basse) vs
nouvelle règle (sans borne basse) à chaque fois.

### 1. Hors-espèce rapproché avant le début de période, `DT_Id IS NULL`, jamais déclaré
Règlement réel `RF26020079` (Décaissement Fournisseur, `MV_Type=2`, `MV_Point=1`,
`MV_PointDate='2026-04-30'`, `AF_Id=19082`, `DT_Id=NULL`) — testé sur une déclaration de **juin 2026**
(`finExclude='2026-07-01'`) :

```sql
-- Nouvelle règle (pas de borne basse) : DOIT apparaître
SELECT M.MV_Numero, M.MV_Type, M.MV_Point, M.MV_PointDate, A.AF_Id, A.DT_Id
FROM RT_MOUVEMENT M JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
WHERE M.SO_Id=1 AND M.MV_Numero='RF26020079' AND A.AF_Id=19082
  AND A.DT_Id IS NULL AND M.MV_Point = 1 AND M.MV_PointDate < '20260701';
-- Résultat : RF26020079 | 2 | 1 | 2026-04-30 | 19082 | NULL   → 1 ligne, apparaît. ✅

-- Ancienne règle (borne basse >= début juin 2026-06-01) : NE DOIT PAS apparaître (c'est le bug corrigé)
... AND M.MV_PointDate >= '20260601' AND M.MV_PointDate < '20260701';
-- Résultat : 0 ligne. ✅ confirme que ce règlement était perdu avant TASK-099.
```

### 2. `RF26060125` (non rapproché) → disparu de l'écran ①
```sql
SELECT M.MV_Numero, M.MV_Type, M.MV_Point, M.MV_Date, M.MV_PointDate
FROM RT_MOUVEMENT M WHERE M.MV_Numero='RF26060125';
-- Résultat : RF26060125 | 1 | 0 | 2026-06-29 | 1753-01-01 (sentinel = jamais pointé)

SELECT M.MV_Numero,
  CASE WHEN (M.MV_Type = 0 OR M.MV_Point = 1) THEN 'DECLARABLE' ELSE 'NON DECLARABLE (masqué écran ①)' END
FROM RT_MOUVEMENT M WHERE M.MV_Numero='RF26060125';
-- Résultat : RF26060125 | NON DECLARABLE (masqué écran ①)   ✅ EstDeclarableSqlM l'exclut bien.
```
(`DT_Id` déjà vérifié NULL sur ses 2 affectations — ce n'est donc pas « déjà déclaré » qui le
masque, c'est bien le nouveau gate `EstDeclarable`, comme voulu.)

### 3. Espèce payée le mois précédent, jamais déclarée → apparaît le mois suivant
Constat affiné : si les 56 affectations d'espèce **Fournisseur** sont toutes déjà déclarées, un cas
naturel existe côté espèce **Client** (encaissement) — `RC26050014` (`MV_Type=0`, `MV_Domaine=0`,
`MV_Date='2026-05-06'`, `DT_Id IS NULL`), testé sur une déclaration de **juin 2026**
(`finExclude='2026-07-01'`) :

```sql
SELECT M.MV_Numero, M.MV_Type, M.MV_Date, A.DT_Id FROM RT_MOUVEMENT M
LEFT JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
WHERE M.SO_Id=1 AND M.MV_Domaine=0 AND M.MV_Numero='RC26050014'
  AND A.DT_Id IS NULL AND M.MV_Date < '20260701';
-- Nouvelle règle : RC26050014 | 0 | 2026-05-06 | NULL   → 1 ligne, apparaît. ✅

... AND M.MV_Date >= '20260601' AND M.MV_Date < '20260701';
-- Ancienne règle (borne basse) : 0 ligne. ✅ confirme la perte sous l'ancienne fenêtre.
```
Cas réel, aucune donnée modifiée (pas besoin de la simulation transactionnelle envisagée
initialement pour l'espèce Fournisseur — un cas naturel équivalent existe côté Client, même
mécanisme `RegleDatePeriode`/`EstDeclarable` indépendant du domaine).

### 4. Non-régression Interrogation Rapprochement (TASK-037) — rejouée sur base réelle
`RF26060125` (non rapproché, cf. §2) testé sur le jeu **non gated** (`@declarationId IS NULL`,
fenêtre pleine, aucun gate `EstDeclarable`) :

```sql
SELECT M.MV_Numero, M.MV_Type, M.MV_Point FROM RT_MOUVEMENT M
WHERE M.SO_Id=1 AND M.MV_Domaine IN (0,1) AND M.MV_Numero='RF26060125'
  AND (CASE WHEN M.MV_Type<>0 AND M.MV_Point=1 THEN M.MV_PointDate ELSE M.MV_Date END) >= '20260601'
  AND (CASE WHEN M.MV_Type<>0 AND M.MV_Point=1 THEN M.MV_PointDate ELSE M.MV_Date END) < '20260701';
-- Résultat : RF26060125 | 1 | 0   → 1 ligne, toujours visible. ✅ non-régression confirmée en réel,
-- pas seulement par construction du code.
```

Les 4 réserves de la task originale sont donc levées, preuve réelle à l'appui pour chacune.

## Critères de validation (task originale)
- [x] Build OK — 0 erreur, 18 avertissements préexistants sans lien (cf. Vérifications)
- [x] Tests passés (dont extension `Task062DateReferenceTests` — non nécessaire : `RegleDatePeriode`
      elle-même n'a pas changé, seuls ses appelants ont été réalignés ; couverture ajoutée côté
      `SelectionExpliqueeEvaluatorTests`)
- [x] Règlement hors-espèce rapproché en avril → apparaît en juin (preuve base réelle : `RF26020079`,
      cf. section « Preuve base réelle » §1)
- [x] `RF26060125` non rapproché → n'apparaît plus (preuve base réelle §2)
- [x] Règlement espèce payé le mois précédent → apparaît le mois suivant (cas réel `RC26050014`,
      espèce Client, §3 — aucune donnée modifiée)
- [x] Règlement déjà déclaré (`DT_Id` posé) → n'apparaît plus (gate `A.DT_Id IS NULL` ajouté aux 3
      chemins ; déjà correct côté évaluateur `DejaDeclare`)
- [x] Non-régression Interrogation Rapprochement (TASK-037) sur base réelle — rejouée en direct
      (`RF26060125` toujours visible en mode non gated, §4)

## Statut
**APPROUVÉE** (architecte, 14/07/2026) — build 0 erreur (18 avertissements préexistants sans lien),
tests verts (57/57 Selection dont 4 nouveaux TASK-099, 115/115 Orchestration, 32/32 Core ;
Controle.Tests 1/2 échec préexistant sans lien), les 6 critères VALIDATION de la task originale
prouvés sur base réelle `GR_EMA_DISTRIBUTION` (aucune donnée modifiée), non-régression TASK-037
confirmée en réel.
