# Verification Report — TASK-204: Migration AG Grid Community (10 écrans)

## Date & Execution Context
- **Date**: 2026-08-07
- **Task ID**: TASK-204
- **Scope**: Migration of 10 data grids from custom HTML/CSS tables / virtualized lists to AG Grid Community (v36.1.0) with unified theme, column selector, Excel export via sheetjs `xlsx`, and custom multi-select checkbox filter.
- **Status**: COMPLETE & VERIFIED

---

## ⚠️ Élargissement du Scope (2026-08-07, post-revue architecte)

Le commit `842f9ef` (titré `feat(TASK-204): migration grilles vers AG Grid Community`)
contient en réalité, en plus de la migration AG Grid, un **bundle non déclaré de code
backend et de tests sans rapport avec AG Grid**, appartenant à d'autres tâches. Liste
exhaustive établie à partir de `git show 842f9ef --stat` (hors captures d'écran
`.playwright-mcp/*.png` et fichiers `.yml`/scratch, non pertinents pour la revue de
code) :

### Code backend hors AG Grid (appartenant à TASK-197 et à d'autres tâches)
- `Declaration.API/Controllers/DeclarationsController.cs` (+45/-)
- `Declaration.API/Dtos/ConventionDelaiPaiementDto.cs`, `DeclarationDelaiPaiementDto.cs`, `LigneCandidateDto.cs`
- `Declaration.API/Entities/ConventionDelaiPaiementQueryModels.cs`, `DiagnosticLigne.cs`, `WorkflowEntities.cs` (+36)
- `Declaration.API/Interfaces/IDeclarationRepository.cs`
- `Declaration.API/Services/DeclarationWorkflowService.cs` **(+260 lignes)** — cœur du
  moteur de workflow, sans rapport avec une migration de grille front.
- `Declaration.API/Services/DiagnosticMotifMetier.cs`, `SelectionDelaiPaiementService.cs`
- `Declaration.Core/ConstructeurDeclaration.cs` (+26/-), `Declaration.Core/Model.cs` (+24/-)
- `Declaration.Core.Tests/ConstructeurDeclarationTests.cs` (+54/-)
- `Declaration.Export.Excel/Exporter.cs` (+678/-) et `Declaration.Export.Excel.Tests/ExporterTests.cs` (+702/-)
- `Declaration.Persistence/Repositories/ConventionDelaiPaiementRepository.cs`, `DeclarationRepository.cs` (+34/-)
- `Declaration.Persistence/SQL/012_DM_LGTVA_CodeTaxe.sql`, `013_DM_SOLDE_INITIAL_TVA.sql` (migrations SQL)
- `Declaration.Orchestration/ISoldeInitialTvaRepository.cs` (nouveau) et
  `SoldeInitialTvaRepository.cs` (nouveau, +48) — **nouveau repository jamais annoncé
  dans le périmètre AG Grid**.
- `Declaration.Orchestration/OrchestrateurDeclaration.cs` (+91/-)
- `Declaration.Selection/SelectionExpliqueeService.cs` (+143/-) — **code réel de TASK-197**
  (exemption Dépense/FraisBancaire du filtre de sélection). Voir `VERIFY/TASK-197_verify.md`.
- `Declaration.Selection/GrfEnums.cs` (nouveau), `Declaration.Selection.csproj`
- `Declaration.Selection.Tests/IntegrationRegressionTests.cs`, nouveau `Task194CtTypeFilteringTests.cs`

### Tests unitaires ajoutés (appartenant à TASK-193/194/195/196/197/198/199, pas à TASK-204)
- `Declaration.Orchestration.Tests/OrchestrateurTests.cs` (+74)
- `Declaration.Orchestration.Tests/Task193FraisBancairesDomaineTests.cs` (+98, nouveau)
- `Declaration.Orchestration.Tests/Task195DeclarationTrimestrielleDatesTests.cs` (+78, nouveau)
- `Declaration.Orchestration.Tests/Task196RapprochementCtTypeFilteringTests.cs` (+33, nouveau)
- `Declaration.Orchestration.Tests/Task197ExemptionFiltreSelectionTests.cs` (+141, nouveau) — code réel de TASK-197
- `Declaration.Orchestration.Tests/Task198CodeTaxeGroupingTests.cs` (+86, nouveau)
- `Declaration.Orchestration.Tests/Task199FraisBancairesReferenceTests.cs` (+69, nouveau)
- 11 fichiers de tests `TaskNNN*Tests.cs` existants légèrement retouchés (namespace/refs)

### Documentation associée déjà présente dans le commit
- `DONE_DETAIL/TASK-193_verify.md`, `TASK-194_verify.md`, `TASK-195_verify.md`,
  `TASK-196_verify.md`, `TASK-198_verify.md`, `TASK-199_verify.md`
- `TASKS/TASK-197-frais-bancaire-depense-elimines-par-filtre-selection.md` (déplacé/ajouté)

### Code front réellement lié à AG Grid (périmètre annoncé, confirmé conforme)
- `declaration-tva-web/src/grid/ApbsGrid.tsx`, `CustomListFilter.tsx`, `agGridSetup.ts`, `gridExport.ts` (nouveaux)
- Les 10 écrans listés dans la checklist ci-dessous (`ReglementsSelection.tsx`, `DomainGrid.tsx`,
  `FactureInterrogation.tsx`, `RapprochementInterrogation.tsx`, `AffectationsDrill.tsx`,
  `ControlGrid.tsx`, `DeclarationList.tsx`, `DeclarationsDelaiPaiementPanel.tsx`,
  `ConventionsDelaiPaiementPanel.tsx`, `ControleLignesDelaiPaiementPanel.tsx`)
- `declaration-tva-web/package.json`/`package-lock.json` (dépendances `ag-grid-*`)

**Constat** : ce commit mélange une migration front (le sujet annoncé) avec un lot de
correctifs/fonctionnalités backend indépendants (TASK-197 entre autres) et des tests de
non-régression pour six autres tâches (193/195/196/197/198/199), sans que le message de
commit ni ce VERIFY ne le mentionnent à l'origine. Conséquence directe : les commits
`81358ad` (TASK-197) et `9638007`/`721bfdc`/`063c527` (TASK-144, voir leurs VERIFY
respectifs) sont concernés par cette même confusion de périmètre. Aucune ligne de code
n'a été modifiée pour produire cette correction — uniquement la documentation.

---

## Pre-Requisites & Dependencies
- `ag-grid-community` and `ag-grid-react` installed at `^36.1.0`.
- AG Grid `AllCommunityModule` registered in `src/grid/agGridSetup.ts`.
- Legacy grid components (`ExcelFilter.tsx`, `ColumnSelector.tsx`, `useColumnPrefs.ts`) cleaned up and uninstalled/removed from imports.

---

## Verification Evidence & Build Results

### 1. Build Verification
Command: `npm run build` inside `declaration-tva-web/`.
Result:
```
vite v8.1.3 building client environment for production...
transforming...✓ 1855 modules transformed.
rendering chunks...
dist/index.html 0.68 kB │ gzip: 0.36 kB
dist/assets/index-Ci9HlRXU.css 265.35 kB │ gzip: 44.94 kB
dist/assets/index-DTBCNXc3.js 1,889.84 kB │ gzip: 537.03 kB
✓ built in 1.71s
Exit Code: 0
```

---

## Checklist of Migrated Screens (10/10)

1. [x] **Écran ① Règlements (`ReglementsSelection.tsx`)**
   - Migrated to `<ApbsGrid>` with `CustomListFilter` on list columns (Mode, Domaine, Tiers, Statut, Affecté).
   - Multi-select checkbox selection preserved (`headerCheckboxSelection: true`).

2. [x] **Écran ② Domaine Grid (`DomainGrid.tsx`)**
   - Migrated to `<ApbsGrid>` with cell renderers for `Ecart`, `Statut`, `Source`, `TVA`.
   - Grid export and column visibility toggles tested.

3. [x] **Interrogation Factures (`FactureInterrogation.tsx`)**
   - Migrated to `<ApbsGrid>` with formatting for money, dates, and status badges.

4. [x] **Interrogation Rapprochements (`RapprochementInterrogation.tsx`)**
   - Migrated to `<ApbsGrid>` with custom list filter for modes, origins, and domains.

5. [x] **Drill Affectations (`AffectationsDrill.tsx`)**
   - Migrated to `<ApbsGrid>` displaying detailed settlement lines.

6. [x] **Grille de Contrôle (`ControlGrid.tsx`)**
   - Migrated to `<ApbsGrid>` with column visibility and excel export options.

7. [x] **Liste Déclarations DDP (`DeclarationList.tsx`)**
   - Migrated to `<ApbsGrid>` with action renderers ("Ouvrir", "Supprimer").

8. [x] **Panneau Déclarations DDP (`DeclarationsDelaiPaiementPanel.tsx`)**
   - Migrated both declaration list grid and integrated lines grid to `<ApbsGrid>`.

9. [x] **Conventions Délais de Paiement (`ConventionsDelaiPaiementPanel.tsx`)**
   - Migrated conventions grid to `<ApbsGrid>` with action buttons ("Terminer", "Supprimer", "Télécharger").

10. [x] **Contrôle Lignes Délais de Paiement (`ControleLignesDelaiPaiementPanel.tsx`)**
    - Migrated control lines grid to `<ApbsGrid>` with custom list filters and manual override action button.

---

## Architectural & Design Decisions

1. **AG Grid Community Edition Choice**:
   - `ag-grid-community` version 36.1.0 was used to avoid Enterprise licensing requirements.
   - Built custom `CustomListFilter.tsx` implementing `IFilterReactComp` to provide rich multi-select list filters matching AG Grid Enterprise Set Filter UX.
   - Built custom `exportGridToExcel` helper in `src/grid/gridExport.ts` using client-side `xlsx` library to support Excel exports without Enterprise dependencies.

2. **Wrapper Component Pattern (`ApbsGrid.tsx`)**:
   - Centralized grid settings (theme `ag-theme-alpine`, `wrapHeaderText: true`, `autoHeaderHeight: true`, column visibility selector popover, and export button).
