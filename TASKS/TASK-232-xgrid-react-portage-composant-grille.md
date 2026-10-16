# TASK-232 — XGrid React : portage du composant grille xGR (remplace ApbsGrid / AG Grid)

Status: 🆕 à faire
Priority: HIGH (bloque le front des TASK-225/228/229/230 et TASK-233)
Risk: HIGH (composant central, 10 écrans consommateurs, export de montants déclaratifs)
Module: declaration-tva-web (`src/grid/` → nouveau `src/components/commun/XGrid/`)
Dépend de : TASK-231 (tokens)

> **Origine :** PO 16/10/2026 — « on utilise xgrid, on doit utiliser le même principe ». Arbitrage : **port React** de `D:\_vibe\xGR\xGR\Tresorerie.Vue\src\components\commun\XGrid.vue` (2 761 lignes) ; spécification : `XGRID.md` (1 664 lignes, v3.0). **À lire en entier avant tout code**, y compris `XGridAfNode.vue`.

## Décisions de conception

1. **Même contrat que xGR** : mêmes noms de props, ColDef, emits (devenus callbacks `onXxx`), méthodes publiques (ref impérative), slots (devenus `renderXxx`/props de rendu), mêmes raccourcis clavier, mêmes libellés français et mêmes clés de persistance au format `grf.<ecran>.<grille>`. Écart au comportement xGR = à documenter dans le VERIFY avec raison.
2. **Zéro dépendance commerciale** (comme XGrid) : virtual scrolling natif ; export Excel via `xlsx`/`exceljs` en **import dynamique** (déjà dans le projet : `gridExport.ts`, `gridExportValues.ts` à réutiliser pour garantir les vraies dates et les largeurs de colonnes, cf. TASK-223).
3. **Fonctionnalités à porter** (sommaire XGRID.md §8) : tri simple/multi, filtres par colonne (8 opérateurs texte, 9 numérique/date), Advanced Filter AND/OR, Quick Filter global, regroupement multi-niveaux + sélection de groupes + groupes collants, colonnes épinglées, sélection de lignes, cellules personnalisées, master-detail, ligne de totaux épinglée, persistance (visibilité/ordre/largeur), navigation clavier, presse-papiers, menu contextuel, auto-size, export Excel et CSV, pinnedTop, édition inline, pagination, colonne de statut, Find & Highlight, Status Bar, ColGroupDef.
4. **Parité fonctionnelle avec `ApbsGrid` avant bascule** (rien de ce qui marche aujourd'hui ne régresse) : sélecteur de colonnes, `toolbarLeft`, bouton export, formats Maroc/FR (montants `fr-FR` 2 décimales alignés à droite ; dates `JJ/MM/AAAA` triées sur la valeur ISO), filtres liste (équivalent `CustomListFilter`), sélection par cases à cocher, en-têtes sur plusieurs lignes, `rowHeight` compact.
5. **Filtrage/tri côté serveur** : conserver la possibilité de filtres pilotés par le back (mode serveur) car plusieurs écrans l'utilisent (`BuildLigneFilterWhere`) — contrat à décrire avant code ; ne pas perdre cette capacité.
6. Tests : logique pure (tri, filtres, groupement, totaux, persistance) en tests unitaires `node --test` ; rendu en test Playwright (déjà installé) sur une page de démonstration.
7. Après livraison, `DOCS/UI_STANDARDS.md` §1 remplacé : **toute grille = XGrid**, `ApbsGrid` marqué déprécié (retrait par TASK-233).

## Périmètre STRICT

- **Inclus** : composant, types TS, utilitaires purs, thème xGR (via tokens TASK-231), page de démonstration couvrant chaque fonctionnalité, tests, documentation `XGRID-REACT.md` (mapping API Vue → React), mise à jour de `UI_STANDARDS.md`.
- **Exclu** : migration des écrans existants (TASK-233) ; modification du back ; composants admin xGR (`AdminConfigTable`, hors périmètre).

## Livrables

- `src/components/commun/XGrid/` (composant + utilitaires + styles), tests, page de démonstration, doc de mapping, `UI_STANDARDS.md` mis à jour.

## Critères de validation

- Chaque fonctionnalité du §3 démontrée sur la page de démonstration, avec comparaison côte à côte avec XGrid xGR sur un même jeu de données (captures jointes).
- Performance : 50 000 lignes, défilement fluide (mesure jointe) ; pas de rendu complet du DOM.
- Export Excel : vraies dates, montants numériques, largeurs de colonnes définies (même résultat que TASK-223), vérifié en relisant le `.xlsx`.
- Filtres/tri serveur fonctionnels sur une grille de démonstration branchée sur un mock.
- Tests unitaires verts ; `npm run lint` + `npm run build` 0 erreur ; preuve par critère datée dans le VERIFY ; **scénarios de test manuel** séparés (composant critique UX) dans `DOCS/`.

## Files

- `D:\_vibe\xGR\xGR\Tresorerie.Vue\src\components\commun\XGrid.vue`, `XGridAfNode.vue`, `XGRID.md`, `src/assets/DevExtremeGlobalDxGrid.css`.
- `declaration-tva-web/src/grid/ApbsGrid.tsx`, `CustomListFilter.tsx`, `gridExport.ts`, `gridExportValues.ts`, `agGridFilterModel.ts` (comportements à reproduire).
- `DOCS/UI_STANDARDS.md`.
