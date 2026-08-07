# Verification Report — TASK-197: Exemption pour Dépense et Frais bancaire du filtre de sélection (TASK-097)

## ⚠️ Correction de traçabilité (2026-08-07, post-revue architecte)

Le code réel de cette tâche **n'a pas été livré dans le commit `81358ad`** (titré
`feat(TASK-197): exemption depense et frais bancaire du filtre de selection`). Cette
tâche a été committée à tort dans le commit `842f9ef`, titré
`feat(TASK-204): migration grilles vers AG Grid Community`, qui contient en réalité un
bundle non déclaré de changements sans rapport avec la migration AG Grid.

**Preuve (`git show 842f9ef --stat`)** : le commit `842f9ef` ajoute
`Declaration.Orchestration.Tests/Task197ExemptionFiltreSelectionTests.cs` (141 lignes) et
modifie `Declaration.Application/Services/DeclarationWorkflowService.cs` (260 lignes) — c'est
**ce dernier fichier**, pas `SelectionExpliqueeService.cs`, qui porte le correctif réel de
cette tâche (l'exemption `selectionSet.Contains(...)` pour `Depense`/`FraisBancaire`, aux 3
occurrences visées par le périmètre : lignes ~297, ~1236, et ~1483 non modifiée — voir note
ci-dessous). `SelectionExpliqueeService.cs` est bien modifié (143 lignes) dans le même commit,
mais ce diff appartient à TASK-031/194/032 (valorisation directe frais bancaire, filtres
CT_Type/MV_Tva) — sans rapport avec TASK-197. Le commit `81358ad`, lui, ne contient
**aucun** fichier `.cs` : `git show 81358ad --stat` ne montre qu'un déplacement
`TASKS/TASK-197-*.md` → `IN_PROGRESS/` et l'ajout du fichier `VERIFY/TASK-197_verify.md`
lui-même — pas de code.

**Note sur la 3ᵉ occurrence** (`DeclarationWorkflowService.cs:1483`,
`selectionSet.Contains(r.MvNumero)`) : laissée **non exemptée**, car ce point construit
`modele.ReglementsSelectionnes` pour l'affichage du rapport final à partir de
`GetReglementsRapprochementAsync`, qui ne produit jamais de règlements `Depense`/`FraisBancaire`
— l'exemption y est donc sans objet. Décision jugée correcte à la revue, mais absente de tout
commentaire dans le code : à documenter d'un commentaire `// TASK-197 : non concerné, source
jamais présente dans ReglementsSelectionnes` lors d'un prochain passage sur ce fichier.

Voir aussi la correction apportée à `VERIFY/TASK-204_verify.md` (scope élargi pour
lister exhaustivement le contenu réel de `842f9ef`).

## Date & Execution Context
- **Date**: 2026-08-07
- **Task ID**: TASK-197
- **Scope**: Exemption of `SourceAffectation.Depense` and `SourceAffectation.FraisBancaire` from the `selectionSet` filter in `DeclarationWorkflowService.cs`.
- **Status**: COMPLETE & VERIFIED

---

## Verification Evidence & Test Results

### Unit Tests Verification (ciblé, conservé pour référence)
Command: `dotnet test --filter Task197ExemptionFiltreSelectionTests`
Result:
```
Réussi! - échec : 0, réussite : 2, ignorée(s) : 0, total : 2, durée : 28 ms - Declaration.Orchestration.Tests.dll
```

### Suite complète (`dotnet test DeclarationTVA.slnx --no-build`, exécutée le 2026-08-07 depuis `d:\_vibe\GRF`, remplace la vérification partielle ci-dessus)

```
Série de tests pour Declaration.Selection.Tests.dll (net8.0)
Série de tests pour Declaration.Core.Tests.dll (net8.0)
Série de tests pour Declaration.Export.Xml.Tests.dll (net8.0)
Série de tests pour Declaration.Controle.Tests.dll (net8.0)
Série de tests pour Declaration.Export.Excel.Tests.dll (net8.0)
Série de tests pour Declaration.Orchestration.Tests.dll (net8.0-windows)

[FAIL] Declaration.Selection.Tests.IntegrationRegressionTests.TestRegression_NouveauSurensembleIncludAncienTask008_Task050
  Microsoft.Data.SqlClient.SqlException : Échec de l'ouverture de session de l'utilisateur 'IHEB-PC\ihebc'.
Échoué!  - échec : 1, réussite : 60, ignorée(s) : 0, total : 61 - Declaration.Selection.Tests.dll (net8.0)

Réussi!  - échec : 0, réussite : 219, ignorée(s) : 0, total : 219 - Declaration.Core.Tests.dll (net8.0)

Réussi!  - échec : 0, réussite : 26, ignorée(s) : 0, total : 26 - Declaration.Export.Xml.Tests.dll (net8.0)

[FAIL] Declaration.Controle.Tests.ComparateurTests.GenererRapportVerification
  System.Exception : Déclaration GRFN 66 introuvable.
Échoué!  - échec : 1, réussite : 1, ignorée(s) : 0, total : 2 - Declaration.Controle.Tests.dll (net8.0)

Réussi!  - échec : 0, réussite : 3, ignorée(s) : 0, total : 3 - Declaration.Export.Excel.Tests.dll (net8.0)

Réussi!  - échec : 0, réussite : 266, ignorée(s) : 0, total : 266 - Declaration.Orchestration.Tests.dll (net8.0)
```

**Bilan global : 575 réussis / 2 échecs / 577 total.**

Les 2 échecs sont **environnementaux, non liés au code** :
1. `Declaration.Selection.Tests.IntegrationRegressionTests.TestRegression_NouveauSurensembleIncludAncienTask008_Task050` — échec d'authentification SQL Server locale (`Échec de l'ouverture de session de l'utilisateur 'IHEB-PC\ihebc'`), poste de dev sans les droits/instance attendus.
2. `Declaration.Controle.Tests.ComparateurTests.GenererRapportVerification` — la déclaration de référence GRFN 66 (`DT_Id`) est absente de la base locale.

### Full Solution Build
Command: `dotnet build DeclarationTVA.slnx`
Result: Build succeeded with 0 errors.

---

## Checklist of Requirements

1. [x] **Exemption of Dépense & FraisBancaire**: Both sources are kept during candidate filtering even if not present in `selectionSet`.
2. [x] **Non-Regression on Decaissement / Encaissement / Espece**: Manual selection filtering remains active for these sources.
3. [x] **Verified by Unit Tests**: `Task197ExemptionFiltreSelectionTests` passes cleanly.
