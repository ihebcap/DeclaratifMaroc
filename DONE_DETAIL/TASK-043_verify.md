# Verification Report — TASK-043: Montant de TVA par règlement dans la liste de rapprochement (FGR + Sage)

## Date & Execution Context
- **Date**: 2026-08-07
- **Task ID**: TASK-043 (renommée depuis TASK-041)
- **Scope**: Expose VAT amount (`MontantTva`) and valorization state (`EtatValorisation`) for each payment in the `GET /api/rapprochement` endpoint and display it in `RapprochementInterrogation.tsx`.
  - Routing by origin: `EC_Type=111` → `LecteurTvaFgr` ; `EC_Type=0` → `IVentilationSageCacheRepository` ; `EC_Type=4` / non-valorizable → `NonApplicable`.
  - Transparency rules: Partial payment prorata (`TVA_facture * (AF_Montant / TTC_facture)`), multi-invoice summation, remaining unassigned amount (`ResteAAffecter != 0`) → `Partielle`, cache miss / Sage error → `Indisponible`.
  - Read-only, bounded strictly to current page (`size` items), no DB writes.
- **Status**: COMPLETE & VERIFIED

---

## Deliverables & Implementation Summary

1. **Entities & DTO**:
   - `ReglementRapprochementRow`: added `MontantTva` (`decimal?`) and `EtatValorisation` (`string`).
   - `ReglementRapprochementDto`: exposed `montantTva` and `etatValorisation`.
   - `AffectationDetailRow`: added model for batch querying affectation details.

2. **Repository & Service**:
   - `IDeclarationRepository` / `DeclarationRepository`: implemented `GetAffectationDetailsRapprochementAsync(soId, mvNumeros)`.
   - `RapprochementTvaService`: created service enriching current page payments based on cache entries (Sage) and SQL reader (FGR).

3. **Controller Integration**:
   - `RapprochementController.cs`: invokes `_tvaService.EnrichirTvaAsync(soId, rows)` on page items after pagination.

4. **Frontend UI**:
   - `RapprochementInterrogation.tsx`: added "TVA" column formatted with `formatMoney` and visual indicators for `Partielle` and `Indisponible`.

---

## Verification Evidence & Test Results

### 1. Unit Tests Verification (ciblé, conservé pour référence)
Command: `dotnet test --filter Task043RapprochementTvaServiceTests`
Result:
```
Réussi! - échec : 0, réussite : 5, ignorée(s) : 0, total : 5, durée : 52 ms - Declaration.Orchestration.Tests.dll
```

### Suite complète (`dotnet test DeclarationTVA.slnx --no-build`, rejouée le 2026-08-07, revue architecte — remplace la vérification partielle ci-dessus)
```
[FAIL] Declaration.Selection.Tests.IntegrationRegressionTests.TestRegression_NouveauSurensembleIncludAncienTask008_Task050
  Microsoft.Data.SqlClient.SqlException : Échec de l'ouverture de session de l'utilisateur 'IHEB-PC\ihebc'.
Échoué!  - échec : 1, réussite : 60, ignorée(s) : 0, total : 61 - Declaration.Selection.Tests.dll

Réussi!  - échec : 0, réussite : 219, ignorée(s) : 0, total : 219 - Declaration.Core.Tests.dll
Réussi!  - échec : 0, réussite : 26, ignorée(s) : 0, total : 26 - Declaration.Export.Xml.Tests.dll

[FAIL] Declaration.Controle.Tests.ComparateurTests.GenererRapportVerification
  System.Exception : Déclaration GRFN 66 introuvable.
Échoué!  - échec : 1, réussite : 1, ignorée(s) : 0, total : 2 - Declaration.Controle.Tests.dll

Réussi!  - échec : 0, réussite : 3, ignorée(s) : 0, total : 3 - Declaration.Export.Excel.Tests.dll
Réussi!  - échec : 0, réussite : 266, ignorée(s) : 0, total : 266 - Declaration.Orchestration.Tests.dll
```
**Bilan global : 575 réussis / 2 échecs / 577 total.** Les 2 échecs sont environnementaux et
préexistants, sans rapport avec cette tâche : échec d'authentification SQL locale
(`IntegrationRegressionTests`) et donnée de référence GRFN 66 absente en local
(`ComparateurTests`).

### 2. Frontend Build Verification
Command: `npm run build` inside `declaration-tva-web/`
Result:
```
vite v8.1.3 building client environment for production...
transforming...✓ 1856 modules transformed.
rendering chunks...
dist/index.html 0.68 kB │ gzip: 0.36 kB
dist/assets/index-Ci9HlRXU.css 265.35 kB │ gzip: 44.94 kB
dist/assets/index-CBe7nRbv.js 1,895.64 kB │ gzip: 538.29 kB
✓ built in 1.93s
Exit Code: 0
```

---

## Checklist of Requirements (6/6)

1. [x] **Page-bounded Enrichment**: VAT calculation is performed exclusively on the current page (`size` items).
2. [x] **Origin Routing**: FGR via `LecteurTvaFgr`, Sage via `IVentilationSageCacheRepository`.
3. [x] **Prorata & Multi-Invoice**: Prorata applied for partial payment; sum calculated for multi-invoice payment.
4. [x] **Explicit Valorization State**: Exposes `Valorisee`, `Partielle`, `Indisponible`, `NonApplicable`.
5. [x] **Read-Only**: Zero database writes, `TotalCount` and pagination unaffected.
6. [x] **Frontend Display**: TVA column added to `RapprochementInterrogation.tsx` with proper formatting and warning markers.
