# Verification Report — TASK-203: Intitulé taxe (`F_TAXE.TA_Intitule`) au récap et à l'export de contrôle

## Date & Execution Context
- **Date**: 2026-08-07
- **Task ID**: TASK-203
- **Scope**: Propagation of `F_TAXE.TA_Intitule` alongside `TA_Code` and `TA_Taux` across the entire pipeline (`LecteurTvaFgr.cs`, `SelectionExpliqueeService.cs`, `Ventilateur.cs`, `ConstructeurDeclaration.cs`, `LigneCandidateDto.cs`, `Exporter.cs` Excel export, and `VerifierIntegrerPanel.tsx` sub-totals table).
- **Status**: COMPLETE & VERIFIED

---

## Pre-Requisites & Verification
- Sage SQL table `F_TAXE` column `TA_Intitule` confirmed and queried in `LecteurTvaFgr.cs` and `SelectionExpliqueeService.cs`.
- Grouping key strictly preserved as `(Taux, CodeTaxe, Collecte)` — no changes to grouping calculations or amounts.
- `Task203IntituleTaxeGroupingTests.cs` created and passed (100% success).

---

## Verification Evidence & Build Results

### 1. Backend Build & Tests Verification
Command: `dotnet build DeclarationTVA.slnx`
Result:
```
La génération a réussi.
0 Erreur(s)
```

Command: `dotnet test Declaration.Orchestration.Tests/Declaration.Orchestration.Tests.csproj --filter Task203IntituleTaxeGroupingTests`
Result:
```
Réussi! - échec : 0, réussite : 1, ignorée(s) : 0, total : 1, durée : 51 ms
```

### 2. Frontend Build Verification
Command: `npm run build` inside `declaration-tva-web/`.
Result:
```
vite v8.1.3 building client environment for production...
transforming...✓ 1855 modules transformed.
rendering chunks...
dist/index.html 0.68 kB │ gzip: 0.36 kB
dist/assets/index-Ci9HlRXU.css 265.35 kB │ gzip: 44.94 kB
dist/assets/index-BM0Hog9u.js 1,892.05 kB │ gzip: 537.65 kB
✓ built in 1.65s
Exit Code: 0
```

---

## Checklist of Requirements (5/5)

1. [x] **SQL & Service Query Update**
   - Query in `LecteurTvaFgr.cs` updated to `SELECT TA_Code, TA_Taux, TA_Intitule FROM F_TAXE`.
   - Query in `SelectionExpliqueeService.cs` updated to `SELECT TA_No, TA_Taux, TA_Code, TA_Intitule FROM F_TAXE WHERE TA_No IN @nos`.

2. [x] **DTO & Core Model Propagation**
   - `TaxeDetail.Intitule`, `LigneDeclaration.IntituleTaxe`, `LigneDeclarationEnrichie.IntituleTaxe`, `RecapParTaux.IntituleTaxe`, and `LigneCandidate.IntituleTaxe` added and populated.
   - `LigneCandidateDto` exposes `intituleTaxe` via `[JsonPropertyName("intituleTaxe")]`.

3. [x] **Excel Export Column Split**
   - `EcrireBlocRecapParTaux` in `Exporter.cs` splits the old `Taux (CodeTaxe)` text column into 3 distinct columns: `Taux`, `Ta_Code`, and `Ta_Intitule`. Applies to both deposit export and control export sheets.

4. [x] **Screen ④ Sub-Totals Table Column**
   - Added `Intitulé taxe` header and cell rendering in `VerifierIntegrerPanel.tsx` sub-totals table ("Sous-totaux par taux TVA").

5. [x] **Non-Regression & Grouping Parity**
   - Grouping key remains strictly `(Taux, CodeTaxe)`. All totals HT, TVA, and TTC remain identical.
