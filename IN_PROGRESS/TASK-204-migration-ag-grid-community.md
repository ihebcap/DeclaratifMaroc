# TASK-204 — Migrer toutes les grilles maison vers AG Grid Community

Status: 🆕 à faire
Priority: HIGH — bloque/précède TASK-201 (remplacée) et TASK-202 (écran ② à construire directement sur AG Grid)
Risk: HIGH — touche 10 écrans à usage quotidien, migration d'un bloc (décision PO), retest complet requis
Module: declaration-tva-web

> **Origine :** proposition PO (07/08/2026) — remplacer les grilles maison (`ExcelFilter.tsx`,
> `ColumnSelector.tsx`, `useColumnPrefs.ts`, virtualisation `@tanstack/react-virtual`) par
> **AG Grid Community** (`ag-grid-community` + `ag-grid-react`), pour bénéficier nativement du tri,
> des filtres par type de colonne (texte/nombre/date/liste à cocher), du choix de colonnes et d'un
> export borné aux lignes filtrées/triées — sans réécrire cette logique à la main à chaque écran.
> **Décisions arbitrées en session (07/08/2026) :**
> 1. **AG Grid Community uniquement** — pas d'Enterprise (licence payante) pour l'instant ; ne pas
>    coder en dur une dépendance à un module Enterprise (regroupement de lignes, export Excel natif
>    stylé) qui rendrait un futur passage à Enterprise coûteux à découpler.
> 2. **Migration des 10 écrans en une fois**, pas de pilote progressif.
> 3. **Cette TASK précède TASK-201 et TASK-202** : TASK-201 est remplacée (cf. bandeau dans son
>    fichier) ; TASK-202 doit construire son écran ② directement avec AG Grid (columnDefs) plutôt
>    que par extension des props `codeActiviteColumns`/`showResynchroniserAction` de `DomainGrid.tsx`
>    — **développer TASK-204 avant de reprendre TASK-202**.

## Écrans concernés (recensement grep, 07/08/2026)

Utilisent aujourd'hui le patron maison (`ExcelFilter` et/ou `ColumnSelector`/`useColumnPrefs` et/ou
virtualisation `@tanstack/react-virtual`) :

1. [DomainGrid.tsx](../declaration-tva-web/src/DomainGrid.tsx) — grille de travail principale (factures/lignes), colonnes dynamiques selon l'appelant, actions en masse, éditable (code activité).
2. [ReglementsSelection.tsx](../declaration-tva-web/src/ReglementsSelection.tsx) — écran ① Sélection.
3. [FactureInterrogation.tsx](../declaration-tva-web/src/FactureInterrogation.tsx)
4. [RapprochementInterrogation.tsx](../declaration-tva-web/src/RapprochementInterrogation.tsx) — écran Rapprochement global.
5. [AffectationsDrill.tsx](../declaration-tva-web/src/AffectationsDrill.tsx) — écran ② Affectations.
6. [ControlGrid.tsx](../declaration-tva-web/src/ControlGrid.tsx)
7. [DeclarationList.tsx](../declaration-tva-web/src/DeclarationList.tsx)
8. [DeclarationsDelaiPaiementPanel.tsx](../declaration-tva-web/src/DeclarationsDelaiPaiementPanel.tsx) — module Délai de Paiement.
9. [ConventionsDelaiPaiementPanel.tsx](../declaration-tva-web/src/ConventionsDelaiPaiementPanel.tsx) — module Délai de Paiement.
10. [ControleLignesDelaiPaiementPanel.tsx](../declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx) — module Délai de Paiement.

Composants partagés à retirer une fois tous les écrans migrés (à condition qu'aucun autre écran non
recensé ci-dessus ne les utilise encore — vérifier avant suppression) :
[ExcelFilter.tsx](../declaration-tva-web/src/ExcelFilter.tsx),
[ColumnSelector.tsx](../declaration-tva-web/src/ColumnSelector.tsx),
[useColumnPrefs.ts](../declaration-tva-web/src/useColumnPrefs.ts).

`@tanstack/react-virtual` (dépendance `package.json`) : AG Grid Community virtualise ses lignes
nativement — retirer la dépendance seulement si plus aucun écran (dans ou hors de cette liste) ne
l'utilise après migration.

## Périmètre STRICT

- **Inclus** :
  1. Installer `ag-grid-community` + `ag-grid-react`, enregistrer `AllCommunityModule` une seule fois
     au point d'entrée de l'app (pas par écran).
  2. Migrer les 10 écrans listés ci-dessus vers `AgGridReact`, colonnes typées (`agTextColumnFilter`,
     `agNumberColumnFilter`, `agDateColumnFilter`, `agSetColumnFilter` — mapping direct depuis les
     `filterType: 'text'|'number'|'date'|'list'` actuels de chaque écran), `sortable`/`resizable`
     par défaut, pagination si l'écran en a déjà (certains, comme `ReglementsSelection`, chargent le
     jeu complet sans pagination serveur — **conserver ce choix**, ne pas introduire de pagination
     qui romprait le compteur exact déjà en place, cf. leçon TASK-040 documentée dans le code).
  3. **Sélection de lignes** (checkbox + sélection multiple, ex. `ReglementsSelection`, `DomainGrid`)
     : reprendre avec `rowSelection: 'multiple'` + `checkboxSelection`, en conservant les clés
     composites actuelles (`reglementKey`, etc.) via `getRowId` — non-régression sur la persistance
     de sélection entre rafraîchissements de données.
  4. **Choix de colonnes** : rebrancher sur l'état natif AG Grid (`columnApi.setColumnVisible`,
     `getColumnState`/`applyColumnState`) plutôt que sur `useColumnPrefs` (state + localStorage
     maison). Vérifier au moment du développement si le menu de colonnes intégré à AG Grid Community
     suffit tel quel, ou si l'UI actuelle (`ColumnSelector.tsx`, bouton + popover) doit être conservée
     en façade et simplement rebranchée sur l'API AG Grid — **ne pas assumer laquelle des deux avant
     d'avoir vérifié la doc AG Grid en Community à jour**, l'affichage exact du menu colonnes varie
     selon la version.
  5. **En-têtes multi-ligne** : `wrapHeaderText: true` + `autoHeaderHeight: true` dans
     `defaultColDef`/`columnDefs` — règle le bug d'en-tête de TASK-201 (remplacée) sans correctif
     dédié.
  6. **Export Excel** : créer un wrapper unique et partagé (ex. `src/gridExport.ts`) —
     `exportGridToExcel(api, fileName)` via `api.forEachNodeAfterFilterAndSort` + la librairie `xlsx`
     **déjà présente** dans `package.json` (aucune nouvelle dépendance). Ce wrapper remplace toute
     logique d'export client-side existante dans les 10 écrans. **Ne pas confondre** avec les exports
     serveur déjà en place (ex. « Export de contrôle (Excel) » généré côté back par
     `Declaration.Export.Excel/Exporter.cs`, téléchargé en blob) — ceux-là restent inchangés, ce
     n'est pas un export de grille client mais un document métier généré côté serveur.
  7. Actions par ligne / en masse déjà existantes (boutons de `DomainGrid` : Intégrer, Réinitialiser,
     Resynchroniser, saisie TVA, affectation code activité — cf. TASK-202) : reprises via
     `cellRenderer` personnalisés par colonne + la sélection multiple native, sans changement de
     comportement métier.
  8. Éditable en ligne (colonne `codeActivite` de `DomainGrid`, `editable: true` aujourd'hui) :
     reprendre avec `editable: true` sur la `colDef` AG Grid correspondante (`agSelectCellEditor` ou
     équivalent pour un choix parmi `codeActiviteOptions`).
  9. Isoler le wrapper `AgGridReact` dans un module propre (ex. `src/grid/` : un composant
     `ApbsGrid.tsx` de faible périmètre + `gridExport.ts` + éventuels `columnHelpers.ts`) plutôt que
     d'instancier `AgGridReact` séparément dans chacun des 10 écrans — **pas** pour créer une
     abstraction lourde maintenant, mais pour que l'éventuelle extraction future en package partagé
     (`@apbs/ui-grid`, discussion PO du 07/08/2026, hors périmètre de cette TASK) se limite à déplacer
     ce dossier, sans réécrire les 10 écrans une seconde fois.
- **Exclu** :
  - **AG Grid Enterprise** — aucun module Enterprise activé, aucune fonctionnalité qui nécessiterait
    une licence (row grouping, pivot, export Excel natif avec styles, master/detail).
  - **Package npm partagé `@apbs/ui-grid`** — décision transverse hors périmètre GRF (discussion PO
    du 07/08/2026, à formaliser séparément si confirmé). Cette TASK prépare le terrain (point 9
    ci-dessus) sans l'exécuter.
  - Aucun changement de règle métier, de calcul, ou de comportement fonctionnel des écrans — migration
    technique du moteur de grille uniquement.
  - Les exports Excel générés côté serveur (Exporter.cs) restent inchangés (cf. point 6).

## Livrables

- 10 écrans migrés vers `AgGridReact`, tri/filtre/colonnes/sélection fonctionnellement équivalents à
  l'existant (mêmes filtres disponibles par colonne, mêmes actions).
- Wrapper d'export Excel client mutualisé (`gridExport.ts`), utilisé par tous les écrans qui exportent
  côté client (à distinguer des exports serveur, non touchés).
- `ExcelFilter.tsx`, `ColumnSelector.tsx`, `useColumnPrefs.ts` retirés si plus aucun écran ne les
  utilise (vérification exhaustive avant suppression).
- `@tanstack/react-virtual` retiré du `package.json` si plus aucun usage après migration.
- `VERIFY/TASK-204_verify.md` : preuve écran par écran (au moins captures ou description du parcours)
  — tri, chaque type de filtre, choix de colonnes, sélection multiple, export, actions en masse —
  sur au moins les 3 écrans les plus utilisés (`ReglementsSelection`, `DomainGrid`,
  `RapprochementInterrogation`), et confirmation que les 7 autres écrans compilent et s'affichent
  sans régression fonctionnelle constatée.

## Critères de validation

- Chaque écran migré conserve l'intégralité de ses filtres actuels (aucun filtre perdu), son tri, sa
  sélection multiple si elle existait, ses actions de ligne/masse, son export si applicable.
- Bug d'en-tête (TASK-201) résolu sans code spécifique, par la configuration AG Grid standard.
- Aucune régression sur le compteur de lignes/total affiché quand un écran charge le jeu complet sans
  pagination serveur (ex. `ReglementsSelection`) — la logique de comptage exacte doit rester exacte.
- Build front OK, aucune dépendance Enterprise introduite (vérifiable : aucun import
  `ag-grid-enterprise`, aucune clé de licence à configurer).
- Retest fonctionnel réel des écrans à plus fort usage quotidien (①, ②/DomainGrid, Rapprochement)
  avant clôture — pas seulement une vérification de compilation.

## Risques / dépendances

- **Volume de retest très important** — 10 écrans migrés d'un bloc (décision PO explicite malgré le
  risque), à fort usage quotidien. Prioriser le retest sur les 3 écrans cœur avant les 7 autres.
- **Bloque/précède TASK-202** : ne pas reprendre le développement de TASK-202 (écran ② Factures à
  déclarer) avant que cette TASK ait livré `DomainGrid.tsx` en AG Grid — sinon double travail sur le
  même fichier avec deux moteurs de grille différents.
- **Remplace TASK-201** : ne pas développer TASK-201, cf. bandeau ajouté en tête de son fichier.
- **Interaction avec TASK-200** : `ReglementsSelection.tsx` fait partie des 10 écrans migrés ici — si
  TASK-200 (écran vide + bouton Intégrer) est développée en parallèle, coordonner pour éviter des
  éditions concurrentes du même fichier. Recommandé : migrer d'abord (TASK-204), puis appliquer
  TASK-200 sur la version AG Grid.
- Nécessite de vérifier, au moment du développement et pas avant, la doc AG Grid Community à jour pour
  les points marqués « à vérifier » ci-dessus (menu colonnes, persistance de state) — les capacités
  exactes de la version Community évoluent d'une release à l'autre.

## Files

- Les 10 écrans listés en §Écrans concernés.
- [declaration-tva-web/src/ExcelFilter.tsx](../declaration-tva-web/src/ExcelFilter.tsx), [ColumnSelector.tsx](../declaration-tva-web/src/ColumnSelector.tsx), [useColumnPrefs.ts](../declaration-tva-web/src/useColumnPrefs.ts) (à retirer si plus utilisés).
- [declaration-tva-web/package.json](../declaration-tva-web/package.json) (ajout `ag-grid-community`/`ag-grid-react`, retrait potentiel de `@tanstack/react-virtual`).
