# Verification Report — TASK-193

## Summary
- **Task:** TASK-193 — Frais bancaires (TVA) jamais inclus dans une déclaration — filtre domaine incomplet
- **Module:** Declaration.Application
- **Status:** PASSED

## Checklist Verification

- [x] Un frais bancaire éligible (TVA non nulle, non déclaré, dans la période) apparaît dans les lignes figées lors du calcul d'une déclaration.
  - `ConstruireLignesFigeesAsync` et `RevaliderLignesFigeesAsync` filtrent `FraisBancaire && Sens == Achat` pour Decaissement et `FraisBancaire && Sens == Vente` pour Encaissement.
- [x] Non-régression : les 4 sources existantes (Decaissement, Espece, Depense, Encaissement) inchangées.
  - Les conditions d'origine sont préservées intactes.
- [x] Build + tests unitaires OK.
  - `Declaration.Orchestration.Tests` : 229/229 tests passés (dont `Task193FraisBancairesDomaineTests`).
- [x] VERIFY documente explicitement l'impact rétroactif et la décision PO.
  - Documenté dans `DONE_DETAIL/TASK-193-frais-bancaires-exclus-filtre-domaine-declaration.md`.
