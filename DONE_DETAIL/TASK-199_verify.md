# Verification Report — TASK-199

## Summary
- **Task:** TASK-199 — Frais bancaire : colonne Référence vide, utiliser RT_PREVISIONNELLE.MV_PieceBq
- **Module:** Declaration.Selection
- **Status:** PASSED

## Checklist Verification

- [x] La colonne Référence d'une ligne Frais bancaire affiche `MV_PieceBq`.
  - La méthode de mapping `SelectionExpliqueeService.MapFraisBancaireRows` extrait `P.MV_PieceBq` de `FraisBancaireRow` et l'affecte à `AffectationADeclarer.Reference`.
- [x] Si `MV_PieceBq` est NULL/vide en base pour une ligne donnée, `Reference` reste `""` (jamais de valeur inventée, pas d'exception).
  - Testé avec un cas réel `MV_PieceBq = null` dans `Task199FraisBancairesReferenceTests.cs`.
- [x] Non-régression sur les autres sources (Decaissement/Encaissement/Depense).
  - Le mapping `Reference` pour les autres sources reste inchangé.
- [x] Build + tests unitaires OK.
  - Test unitaire automatisé [`Task199FraisBancairesReferenceTests.cs`](file:///D:/_vibe/GRF/Declaration.Orchestration.Tests/Task199FraisBancairesReferenceTests.cs) passe avec succès.
