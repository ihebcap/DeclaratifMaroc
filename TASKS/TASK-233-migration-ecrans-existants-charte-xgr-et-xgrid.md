# TASK-233 — Migration des écrans existants vers la charte xGR et XGrid, retrait d'ApbsGrid / AG Grid

Status: 🆕 à faire
Priority: MEDIUM
Risk: HIGH (régression sur écrans en production, dont 10 grilles)
Module: declaration-tva-web
Dépend de : TASK-231, TASK-232

> **Origine :** PO 16/10/2026 — « exactement comme xGR ». Les écrans V1 (TVA + DDP) doivent adopter la même charte et la même grille que les écrans V2.

## Décisions de conception

1. **Lots livrés séparément**, un VERIFY par lot (une régression ne bloque pas les autres) :
   - **Lot A** — Sélection / Rapprochement : `ReglementsSelection.tsx`, `RapprochementInterrogation.tsx`, `FactureInterrogation.tsx`, `FacturesADeclarerPanel.tsx`.
   - **Lot B** — Vérifier & Intégrer : `VerifierIntegrerPanel.tsx`, `DomainGrid.tsx`, `ControlGrid.tsx`, `RecapSourceTable.tsx`, `DiagnosticModal.tsx`, `DetecterDoublonsModal.tsx`.
   - **Lot C** — Déclarations, génération, finale : `DeclarationList.tsx`, `DeclarationStepper.tsx`, `GenerationPanel.tsx`, `DeclarationFinalePanel.tsx`, `CreateDeclarationModal.tsx`, `ProofModal.tsx`, `Auth.tsx`.
   - **Lot D** — DDP : `DeclarationsDelaiPaiementPanel.tsx`, `ConventionsDelaiPaiementPanel.tsx`, `ControleLignesDelaiPaiementPanel.tsx`, `MiseEnRouteDelaiPaiementModal.tsx`.
   - **Lot E** — Nettoyage : suppression d'`ApbsGrid`, `CustomListFilter`, `agGridSetup`, `agGridFilterModel`, dépendances `ag-grid-*` du `package.json`, mise à jour finale de `UI_STANDARDS.md`.
2. **Comportement fonctionnel strictement identique** : aucune règle métier ni appel d'API modifiés ; seuls présentation et grille changent. Tout écart métier découvert → s'arrêter et signaler.
3. **Les `storageKey` existants sont conservés** ou migrés (préférences utilisateur de colonnes non perdues sans le dire) ; décision et conséquence documentées dans le VERIFY du lot A.
4. Les filtres actuellement ignorés côté back (TASK-208) restent hors périmètre ; ne pas masquer le défaut en migrant.
5. Remplacer les couleurs et styles inline en dur par les tokens/classes de TASK-231 ; utiliser `KpiCard` et tags là où des cartes/statuts existent (ex. récap par source, états de ligne).
6. **TASK-201** (retour à la ligne des en-têtes) devient obsolète si XGrid gère le wrap : la clore dans ce lot, preuve à l'appui.

## Périmètre STRICT

- **Inclus** : les 5 lots ci-dessus, captures avant/après par écran, tests Playwright de non-régression des parcours principaux.
- **Exclu** : toute évolution fonctionnelle, tout changement back, les écrans V2 (construits directement sur XGrid par TASK-225/228/229/230).

## Livrables

- Écrans migrés par lot, `ApbsGrid` et AG Grid supprimés (lot E), documentation à jour.

## Critères de validation (par lot)

- Chaque écran du lot : parcours nominal rejoué avant/après, mêmes données, mêmes totaux, mêmes exports (comparaison de fichiers `.xlsx` sur un jeu identique, écart 0).
- Aucun appel `AgGridReact` restant à la fin du lot E (`grep` joint) ; `ag-grid-*` absent de `package.json`.
- `npm run lint` + `npm run build` 0 erreur à chaque lot ; **checklist UI** de `DOCS/UI_STANDARDS.md` cochée ; preuve par critère datée ; scénarios de test manuel pour le lot B (écran critique, TASK-224).

## Files

- `declaration-tva-web/src/*.tsx`, `src/grid/`, `package.json`, `DOCS/UI_STANDARDS.md`.
- `TASKS/TASK-201-entete-grille-retour-a-la-ligne.md`, `TASKS/TASK-208-filtres-drill-domaingrid-ignores-back.md`.
