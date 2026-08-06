# Verification Report — TASK-194

## Summary
- **Task:** TASK-194 — Filtrer RT_MOUVEMENT par CT_Type (exclure les « règlements type autre »)
- **Module:** Declaration.Selection
- **Status:** PASSED

## Checklist Verification

- [x] Un règlement `CT_Type` « autre » (ni 0 ni 1) n'apparaît plus dans les surensembles Client (`CT_Type=0`) ou Fournisseur (`CT_Type=1`).
  - `GetSurensembleFournisseurSql` contient `AND M.CT_Type = @ctTypeFournisseur` (1).
  - `GetSurensembleClientSql` contient `AND M.CT_Type = @ctTypeClient` (0).
- [x] Aucun règlement légitime (`CT_Type=0` côté client, `CT_Type=1` côté fournisseur) perdu.
  - Tous les tests de sélection existants (61/61) sont entièrement au vert.
- [x] Périmètre scopé strictement sans extension non confirmée.
- [x] Build + tests unitaires OK.
  - `Declaration.Selection.Tests` : 61/61 passés.
  - `Declaration.Orchestration.Tests` : 229/229 passés.
