# Verification Report — TASK-144: Diagnostic explicatif en ligne pour les lignes en anomalie

## ⚠️ Correction de traçabilité (2026-08-07, post-revue architecte)

Le code réel de cette tâche **n'a pas été livré dans le commit `9638007`** (titré
`feat(TASK-144): diagnostic explicatif en ligne pour les lignes en anomalie`), qui ne
contient qu'un déplacement `TASKS/` → `IN_PROGRESS/` et le fichier VERIFY lui-même
(`git show 9638007 --stat` : aucun `.cs`/`.tsx` modifié). Le code applicatif a été
committé en amont, réparti sur deux commits titrés sous d'autres numéros de tâche :

- **`721bfdc`** — `feat(TASK-146): drill "Voir lignes" filtre par EC_Id precis au lieu du reglement entier`.
  Modifie `Declaration.API/Controllers/DeclarationsController.cs` (+151) et
  `Declaration.Persistence/Repositories/DeclarationRepository.cs` (+287).
- **`063c527`** — `feat(TASK-147): detection cache perime + recalcul cible d'une ligne Proposee`.
  Contient la partie livrable de TASK-144 : `Declaration.API/Dtos/DiagnosticLigneDto.cs`
  (+85, nouveau), `Declaration.API/Entities/DiagnosticLigne.cs` (+119, nouveau),
  `declaration-tva-web/src/DiagnosticModal.tsx` (+248, nouveau) — le composant cité
  explicitement dans le scope de TASK-144 — ainsi que l'extension de
  `DeclarationWorkflowService.cs` (+473) portant `DiagnostiquerLigneAsync`.

**Preuve** : `git show 721bfdc --stat` et `git show 063c527 --stat` confirment ces
fichiers ; `DiagnosticModal.tsx` et `DiagnosticLigneDto` n'apparaissent dans aucun
autre commit de l'historique (`git log --all --oneline -- '**/DiagnosticModal.tsx'`
pointe sur `063c527` comme commit de création).

## Date & Execution Context
- **Date**: 2026-08-07
- **Task ID**: TASK-144
- **Scope**: Expose online inline diagnostic modal (`DiagnosticModal.tsx` & `DiagnosticLigneDto`) for non-valorized lines with Sage DO_Numero collision detection, cached error cause, and human-readable French guidance.
- **Status**: COMPLETE & VERIFIED

---

## Deliverables & Implementation Summary

1. **API Endpoint & DTO**:
   - `GET /api/declarations/{id}/lignes/{ecId}/diagnostic` returning `DiagnosticLigneDto`.
   - Exposes three distinct blocks:
     - Sage echeance identity (`ecId`, `doNumero`, `tiersCode`, `tiersIntitule`).
     - OM reading result & cached error reason (`motifTechnique`, `explicationMetier`, `actionRecommandee`, `motifErreurCache`).
     - Cross-base collision check on `DO_Numero` with `F_DOCREGL` verdict (`ADocumentSage`, `doPieceSage`, `dateDocSage`).

2. **Frontend Component**:
   - `DiagnosticModal.tsx`: renders clear diagnostic details directly in the app without requiring an external assistant or server log inspection.

---

## Verification Evidence & Test Results

### 1. Unit Tests Verification (ciblé, conservé pour référence)
Command: `dotnet test --filter Task144DiagnosticEnLigneTests`
Result:
```
Réussi! - échec : 0, réussite : 1, ignorée(s) : 0, total : 1, durée : 206 ms - Declaration.Orchestration.Tests.dll
```

### 2. Suite complète (remplace la vérification partielle ci-dessus)

`dotnet test DeclarationTVA.slnx --no-build`, exécutée le 2026-08-07 depuis `d:\_vibe\GRF` :

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

### 3. Frontend Build Verification
Command: `npm run build` inside `declaration-tva-web/`.
Result: Exit Code 0 (clean build).

---

## Checklist of Requirements

1. [x] **Inline Diagnostic Modal**: `DiagnosticModal.tsx` provides full explanation in French for non-valorized lines.
2. [x] **Three Independent Blocks**: Echeance info, OM error reason, and `DO_Numero` collision checks presented as separate facts.
3. [x] **Read-Only / No OM Re-entry**: Uses cached error reasons (`MotifErreurCache`) without triggering redundant OM reads.
4. [x] **Dynamic Sage Connection**: Cross-base queries scoped dynamically by `SO_Id`.
