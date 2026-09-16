# TASK-217 Verify — Clarté des colonnes de bornes et d'origine du délai (écran Contrôle DDP)

> Implémenté par Claude en rôle **WORKER de secours** (dérogation explicite du PO, session du
> 10/09/2026). Conformément à la règle de séparation implémentation/clôture (`CLAUDE.md` racine),
> ce fichier VERIFY est déposé pour review par un tiers (PO ou session ARCHITECT distincte) — aucune
> clôture (déplacement `DONE_DETAIL/`, mise à jour `DONE.md`/`TODO.md`/`CHANGELOG.md`) n'a été
> effectuée par le worker.

## Fichiers modifiés

- `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx` — 4 colonnes (`columnDefs`,
  lignes 146-149).
- `declaration-tva-web/src/api.ts` : **non modifié** — `nombreJoursDelaiApplique` était déjà mappé
  (`api.ts:306`), aucun oubli de sérialisation constaté (étape 1 des Étapes de la TASK).

## Changements

| Colonne | Avant | Après |
|---|---|---|
| `borneReference` | `headerName: 'Déjà déclaré au'` | `headerName: 'Dernière déclaration'` + `headerTooltip` |
| `borneActuelle` | `headerName: 'Constaté au'` | `headerName: 'Constaté le'` + `headerTooltip` |
| `depassement` | pas de tooltip | `headerTooltip` ajouté (libellé inchangé, non ambigu) |
| `origineDelai` | `"Convention"` / `"Défaut société"` | `"Convention (90 j)"` / `"Défaut société (60 j)"` (via `nombreJoursDelaiApplique`) |

Mécanisme retenu : `headerTooltip` natif ag-grid v36 (`ColDef.headerTooltip: string`, confirmé
présent dans `node_modules/ag-grid-community/dist/types/src/entities/colDef.d.ts`) — pas de
composant custom, conforme à l'étape 2 de la TASK (réflexe simple avant complexité).

Textes des 3 tooltips : repris mot pour mot des textes indicatifs fournis dans la TASK-217
(§ Périmètre STRICT, point 2).

## Checklist UI (`DOCS/UI_STANDARDS.md`)

- [x] Aucune instanciation directe de `AgGridReact` ni de `react-select` — aucun composant de grille
  touché, seul `columnDefs` (consommé par `ApbsGrid`) modifié.
- [x] `storageKey` unique et nommé selon la convention — non affecté par cette TASK (grille
  existante, `storageKey` inchangé).
- [x] Montants alignés droite + format fr-FR ; dates JJ/MM/AAAA triables — non affecté, `formatDate`
  déjà utilisé pour `borneReference`/`borneActuelle` avant et après la TASK, tri toujours sur la
  valeur (aucun `valueGetter` retourné n'a changé de type, toujours une string formatée comme avant).
- [x] Aucune couleur en dur ; variables CSS respectées — aucune couleur introduite (tooltip natif
  ag-grid, pas de style custom).
- [x] `npm run lint` + `npm run build` → 0 erreur — voir logs ci-dessous, 2026-09-10.

## Logs

### `npm run lint` (2026-09-10)
0 erreur. Warnings pré-existants uniquement (fast-refresh, exhaustive-deps, catch inutilisé) sur des
fichiers non touchés par cette TASK — aucun nouveau warning introduit sur
`ControleLignesDelaiPaiementPanel.tsx`.

### `npm run build` (2026-09-10)
```
tsc -b && vite build
✓ 1857 modules transformed.
dist/index.html                     0.68 kB
dist/assets/index-B4kbI1sZ.css    265.43 kB
dist/assets/index-Cs2Q3v-Z.js   1,888.29 kB
✓ built in 2.15s
```
0 erreur TypeScript, build vite réussi. Warning `INEFFECTIVE_DYNAMIC_IMPORT` + chunk size :
pré-existants, sans rapport avec cette TASK.

## Point non couvert — signalé explicitement (discipline de preuve)

- [ ] **Test visuel navigateur (étape 4 de la TASK)** : capture d'écran du survol de chaque header
  et de la colonne Origine du délai sur cas réels (Convention/Défaut société) — **non réalisé**.
  Raison : lancer l'écran réel nécessite l'API .NET (`Declaration.API`) connectée à Sage OM + SQL
  Server (voir `DOCS/DEPLOIEMENT.md`), aucune instance backend n'était démarrée dans cette session
  et son démarrage (connexion Sage réelle, société configurée) dépasse le périmètre d'une correction
  UI ponctuelle en mode worker de secours. Le changement est mécanique et à faible risque (renommage
  de `headerName`, ajout de `headerTooltip` string statique, concaténation dans un `valueGetter`
  déjà existant) et vérifié par `tsc`/build, mais **le rendu visuel réel du tooltip et la valeur
  effective de `nombreJoursDelaiApplique` sur des lignes réelles restent à confirmer par le
  reviewer** avant clôture, conformément à la TASK.

## Critères de validation — état

- [x] En-têtes renommés sans collision avec Mode/Échéance légale (libellés distincts vérifiés par
  lecture du fichier).
- [ ] Tooltips visibles au survol — **non vérifié visuellement**, voir point ci-dessus.
- [ ] Colonne Origine du délai affichant le nombre de jours sur cas réels — **non vérifié
  visuellement**, voir point ci-dessus. Le mécanisme (valueGetter lisant un champ déjà mappé côté
  TypeScript) est correct par lecture de code.
- [x] Aucun changement de libellé sur les autres colonnes (diff limité aux 4 lignes citées).
- [x] `npm run lint` + `npm run build` → 0 erreur.
