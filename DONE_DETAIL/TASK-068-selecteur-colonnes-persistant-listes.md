# TASK-068 — Sélecteur de colonnes persistant (localStorage) sur toutes les listes

## Contexte
Demande PO (13/07/2026) : sur **toutes les listes**, l'utilisateur doit pouvoir **choisir les
colonnes affichées**, et ce choix doit **persister** (rechargement, session suivante) via
`localStorage`.

Toutes les grilles partagent le même patron : un tableau de définition de colonnes (`COLUMNS` /
`GRID_COLUMNS` / `columns`) rendu en flexbox, entête collant + corps (virtualisé ou non), largeurs
partagées entête/corps. Écrans concernés :
`ReglementsSelection.tsx`, `FactureInterrogation.tsx`, `RapprochementInterrogation.tsx`,
`AffectationsDrill.tsx`, `DomainGrid.tsx`, `ControlGrid.tsx`.

## Périmètre STRICT
- **Inclus** :
  - Un **hook réutilisable** (ex. `useColumnPrefs(storageKey, allColumns)`) : lit/écrit dans
    `localStorage` la liste des `key` de colonnes visibles ; fournit `visibleColumns`, `toggle`,
    `reset` ; fallback = toutes colonnes visibles si clé absente/corrompue.
  - Un **composant `ColumnSelector`** (bouton + popup cases à cocher, patron visuel `ExcelFilter`,
    via `createPortal`) placé dans la barre d'outils de chaque grille.
  - Câblage dans les **6 grilles** : rendre l'entête ET le corps sur `visibleColumns` (pas
    `COLUMNS`), clé de stockage distincte par écran (ex. `grf.cols.rappro`, `grf.cols.factures`, …).
- **Exclu** : réordonnancement des colonnes (drag), redimensionnement, largeurs persistées, filtres
  (TASK-067), tri, pagination. Toute logique métier/back. Aucune colonne « verrou » forcée sauf si
  le PO l'exige (à confirmer : garder au moins 1 colonne visible).

## Objectif
```
Entrée : une grille avec sa définition de colonnes complète
Traitement : filtrer les colonnes rendues (entête + corps) selon la préférence utilisateur,
             lue/écrite dans localStorage sous une clé propre à l'écran
Sortie : sélecteur de colonnes fonctionnel sur chaque liste, choix persistant entre sessions,
         cohérence entête/corps garantie (mêmes colonnes, mêmes largeurs)
```

## Étapes
1. Créer `useColumnPrefs.ts` (hook) + `ColumnSelector.tsx` (UI), front-only, réutilisables.
2. Câbler écran par écran (progressif, 1 grille validée avant la suivante), en dérivant
   `visibleColumns` du hook et en remplaçant les `.map(COLUMNS…)` entête+corps.
3. Vérifier l'alignement entête/corps (largeurs flexbox partagées) après masquage de colonnes,
   y compris grilles virtualisées (`RapprochementInterrogation`, `FactureInterrogation`,
   `ReglementsSelection`, `DomainGrid`).
4. Vérifier persistance : masquer une colonne → recharger → état conservé ; clé corrompue → repli
   sûr (toutes colonnes).
5. Build front tsc+vite + oxlint ; e2e Playwright (masquer/afficher + persistance) sur ≥1 écran.

## Livrables
- `declaration-tva-web/src/useColumnPrefs.ts`, `declaration-tva-web/src/ColumnSelector.tsx`.
- Câblage dans les 6 grilles (clé de stockage distincte par écran).
- `VERIFY/TASK-068_verify.md` : captures avant/après masquage, preuve persistance localStorage
  (rechargement), build/lint/e2e verts.

## Critères de validation
- Sélecteur de colonnes présent et fonctionnel sur **les 6 listes**.
- Choix persistant via `localStorage` (rechargement + nouvelle session), clé propre par écran.
- Entête et corps toujours cohérents (mêmes colonnes visibles, largeurs alignées), y compris en
  virtualisé.
- Repli sûr si préférence absente/corrompue (toutes colonnes visibles).
- Front-only, aucune régression back ; build tsc+vite + oxlint 0 erreur.

## Risques / dépendances
- **Cohérence entête/corps** : le risque principal — chaque grille rend entête et corps par deux
  `.map` séparés ; les deux doivent consommer la **même** `visibleColumns`. Les grilles virtualisées
  reposent sur des largeurs flexbox partagées (position absolue) → tester l'alignement après masquage.
- **Totaux / cellules spéciales** : `AffectationsDrill` a une ligne « Total » et un rendu spécial
  « non valorisé » indexés sur `GRID_COLUMNS[i]` → adapter ces accès à `visibleColumns` sans casser
  les totaux.
- Front-only, indépendant du back → livrable sans TASK-067, mais **mêmes fichiers de grille** que
  TASK-067 → coordonner l'ordre d'édition (éviter conflits).
- Clés `localStorage` : namespacer (`grf.cols.<ecran>`) pour éviter collisions inter-modules.
</content>
