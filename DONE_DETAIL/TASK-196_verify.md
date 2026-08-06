# Verification Report — TASK-196

## Summary
- **Task:** TASK-196 — Filtre `CT_Type` manquant aussi sur l'écran « Rapprochement bancaire » (règlements « type autre » toujours visibles)
- **Module:** Declaration.Infrastructure
- **Status:** PASSED

## Verification Checklist

- [x] Un règlement `CT_Type` « autre » (ex. `CT_Type=2`) n'apparaît plus sur l'écran Rapprochement (`GET /api/rapprochement`).
  - Filtre `((M.MV_Domaine = 0 AND M.CT_Type = 0) OR (M.MV_Domaine = 1 AND M.CT_Type = 1))` ajouté dans `RapprochementFromWhere` (`DeclarationRepository.cs:673`).
- [x] Non-régression : les règlements `CT_Type∈{0,1}` légitimes (Client=0, Fournisseur=1) restent visibles.
  - Validé par tests unitaires `Task196RapprochementCtTypeFilteringTests.cs`.
- [x] `TotalCount`/pagination et filtres distincts (`MV_Type`, `EC_Type`) sont totalement alignés.
  - Le filtre s'applique au fragment commun `RapprochementFromWhere` (qui sert à la liste, au `COUNT`, et aux filtres distincts) ainsi qu'aux requêtes de distincts pour les modes et origines (`GetReglementsRapprochementDistinctsAsync`).
- [x] Tests + build OK.
  - `Declaration.Infrastructure.csproj` : Build 0 erreur.
  - `Declaration.Orchestration.Tests` : 253/253 tests passés.
