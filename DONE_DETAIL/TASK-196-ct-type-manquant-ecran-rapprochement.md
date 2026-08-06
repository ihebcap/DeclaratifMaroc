# TASK-196 — Filtre `CT_Type` manquant sur l'écran « Rapprochement bancaire »

## Problème
Après la livraison de TASK-194 (filtrage `CT_Type` dans `SelectionExpliqueeService.cs`), des règlements `CT_Type=2` (« type autre ») apparaissaient toujours sur l'écran de rapprochement bancaire (`ReglementsSelection.tsx` / `RapprochementController.cs`).
L'écran de rapprochement repose sur des requêtes séparées dans `DeclarationRepository.cs` (`RapprochementFromWhere` et `GetReglementsRapprochementDistinctsAsync`) qui filtraient `MV_Domaine IN (0, 1)` mais ne restreignaient pas `CT_Type`.

## Solution apportée
1. **Ajout du filtre `CT_Type` aux requêtes de `DeclarationRepository.cs`** :
   - `RapprochementFromWhere` (lignes 670–674) : ajout de `AND ((M.MV_Domaine = 0 AND M.CT_Type = 0) OR (M.MV_Domaine = 1 AND M.CT_Type = 1))` (aligné sur `GrfEnums.CtType_Client` [0] pour l'encaissement et `GrfEnums.CtType_Fournisseur` [1] pour le décaissement).
   - `GetReglementsRapprochementDistinctsAsync` (lignes 971–985) : ajout du même filtre sur la sélection distincte des modes de règlement (`MV_Type`) et des origines d'échéances (`EC_Type`).

2. **Impact de l'alignement** :
   - Les requêtes de liste, de comptage `COUNT(*)`, des distincts, et de pagination partagent le fragment `RapprochementFromWhere` et sont désormais totalement cohérentes.
   - Les règlements de tiers « type autre » (`CT_Type != 0` et `CT_Type != 1`) sont désormais éliminés de l'écran Rapprochement.

3. **Validation & Tests automatisés** :
   - Ajout de `Task196RapprochementCtTypeFilteringTests.cs` dans `Declaration.Orchestration.Tests`.
   - Rejeu des tests unitaires `Declaration.Orchestration.Tests` : 253/253 tests passés.
