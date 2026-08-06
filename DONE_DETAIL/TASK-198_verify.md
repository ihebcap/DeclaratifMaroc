# Verification Report — TASK-198

## Summary
- **Task:** TASK-198 — Distinguer les taux de TVA par code (TA_Code), pas seulement par pourcentage
- **Module:** Declaration.Core / Declaration.Orchestration / Declaration.Application / Declaration.Export.Excel
- **Status:** PASSED

## Checklist Verification

- [x] Décision PO (05/08/2026) : Portée **interne uniquement** — pas d'impact sur l'export XML dépôt Simpl-TVA (`DeclarationXmlExporter.cs` inchangé).
- [x] Deux lignes au même taux mais `TA_Code` différent restent distinguables de bout en bout (grilles, DTOs, récapitulatif par taux, export Excel).
  - Propriété `CodeTaxe` ajoutée sur `LigneDeclarationEnrichie`, `RecapParTaux`, `LigneCandidate`, `LigneCandidateDto`, `AffectationADeclarer`.
  - Regroupement `RecapsParTaux` mis à jour pour grouper par `(Taux, CodeTaxe, Collecte)`.
- [x] Non-régression sur le total TVA déclarée (montants inchangés, seule la granularité de regroupement change).
- [x] Build + tests unitaires OK.
  - Test unitaire automatisé [`Task198CodeTaxeGroupingTests.cs`](file:///D:/_vibe/GRF/Declaration.Orchestration.Tests/Task198CodeTaxeGroupingTests.cs) passe avec succès.
