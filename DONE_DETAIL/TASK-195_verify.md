# Verification Report — TASK-195

## Summary
- **Task:** TASK-195 — Déclaration trimestrielle calculée sur le mauvais intervalle de dates
- **Module:** Declaration.Application
- **Status:** PASSED

## Verification Checklist

- [x] Une déclaration T3 (exercice N) calcule bien `01/07/N → 30/09/N` comme intervalle de sélection.
  - Validé par tests unitaires automatisés dans `Task195DeclarationTrimestrielleDatesTests.cs` (`CalculerIntervalleDates_Trimestrielle_RetourneBornesExactes`).
- [x] Une déclaration mensuelle (n'importe quel mois) — comportement strictement inchangé (non-régression).
  - Validé par tests unitaires automatisés dans `Task195DeclarationTrimestrielleDatesTests.cs` (`CalculerIntervalleDates_Mensuelle_RetourneBornesExactes`).
- [x] Les 5 occurrences utilisent la même fonction centralisée (`declaration.ObtenirIntervalleDates()`).
  - Lignes 265, 418, 543, 1127, 1341 de `DeclarationWorkflowService.cs` remplacées par des appels à `ObtenirIntervalleDates()`.
- [x] Tests unitaires couvrant les 4 trimestres (T1→T4) + cas mensuels + gestion des erreurs (`ArgumentOutOfRangeException`).
  - `Declaration.Orchestration.Tests` : 245/245 tests passés.
- [x] PO informé de l'impact rétroactif sur des déclarations trimestrielles existantes.
  - Documenté dans `DONE_DETAIL/TASK-195-declaration-trimestrielle-periode-calculee-comme-un-mois.md`.
