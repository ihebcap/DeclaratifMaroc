# TASK-067A — Filtres « valeurs disponibles » (Excel-like), Part A front-only (grilles client-side)

> Scission de TASK-067 (décision PO 13/07/2026) : livrer la valeur front vite, sans dépendance back.
> Voir aussi [TASK-067B](TASK-067B-filtres-valeurs-list-back-server-side.md) (suite, server-side).

## Contexte
Demande PO (13/07/2026, capture écran ② Affectations) : sur **toutes les grilles**, chaque filtre
de colonne (n° règlement, facture, tiers, …) doit proposer **la liste des valeurs réellement
présentes** dans le jeu de l'écran, avec cases à cocher + recherche — « exactement comme Excel ».
Aujourd'hui plusieurs colonnes clés sont en `filterType: 'text'` (LIKE aveugle) alors qu'elles
devraient exposer les valeurs disponibles.

Le composant `declaration-tva-web/src/ExcelFilter.tsx` **gère déjà** le mode `'list'` (cases +
recherche + « Tout sélectionner », plafond 200 avec repli recherche). Le travail porte donc sur
**l'alimentation des options** (`filterOptionsFor`) et le **type de filtre** par colonne, pas sur
le composant lui-même.

**Décision PO (13/07/2026) — cardinalité** : `list` partout, y compris colonnes à très forte
cardinalité (n° pièce) — pas de fallback `text`/LIKE. `ExcelFilter` couvre déjà ce cas (recherche +
plafond 200). Un éventuel repli LIKE ne sera introduit, au cas par cas et documenté en NOTES, que si
la Part B (server-side) révèle un coût `SELECT DISTINCT` inacceptable sur une colonne précise —
non anticipé ici.

## Périmètre STRICT
- **Inclus** : grilles **client-side** (jeu complet déjà en mémoire, sans appel back) :
  - `AffectationsDrill.tsx` : `allRows` en mémoire. `filterOptionsFor` existe déjà pour
    `tiers`/`origine`/`statutConformite`/`tauxTVA` — étendre à `numeroReglement`/`factureNumero`.
  - `ReglementsSelection.tsx` : fetch en boucle jusqu'à `totalCount` → jeu complet en mémoire —
    `numeroReglement`, `tiers` en `list`.
  - `ControlGrid.tsx` : reçoit les `lignes` en prop → jeu complet en mémoire — facture, tiers, IF,
    ICE, désignation en `list`.
  - Colonnes montant → rester `number` (plage) ; date → rester `date` (plage). Aucun changement au
    back, aucun recalcul.
- **Exclu** : grilles server-side (`RapprochementInterrogation`, `FactureInterrogation`,
  `DomainGrid`) → [TASK-067B](TASK-067B-filtres-valeurs-list-back-server-side.md). Refonte
  `ExcelFilter` ; tri ; pagination ; toute écriture ; colonnes montant/date ; fusion GOCOM.

## Objectif
```
Entrée : une grille client-side (jeu complet déjà en mémoire)
Traitement : pour chaque colonne énumérable, dériver la liste des valeurs distinctes présentes
             dans le jeu en mémoire et la fournir à ExcelFilter en filterType 'list'
Sortie : filtre à cases cochables + recherche sur chaque colonne concernée, compteur exact,
         zéro valeur infiltrable
```

## Étapes
1. `AffectationsDrill` : passer `numeroReglement`/`factureNumero` en `list`, ajouter les cas
   correspondants dans `filterOptionsFor` (patron déjà présent pour `tiers`).
2. `ReglementsSelection` : idem pour `numeroReglement`, `tiers`.
3. `ControlGrid` : idem pour facture, tiers, IF, ICE, désignation.
4. Vérifier l'exactitude du compteur après filtrage sur chaque écran (aucune régression TASK-040 —
   invariant qui s'applique par construction ici, jeu déjà complet en mémoire).
5. Tests : build front tsc+vite / oxlint 0 erreur.

## Livrables
- `declaration-tva-web/src/AffectationsDrill.tsx`
- `declaration-tva-web/src/ReglementsSelection.tsx`
- `declaration-tva-web/src/ControlGrid.tsx`
- `VERIFY/TASK-067A_verify.md` : captures Excel-like par écran, preuve compteur exact, build vert.

## Critères de validation
- Chaque colonne énumérable des 3 grilles client-side offre la liste des valeurs disponibles +
  recherche (mode `list`), y compris n° pièce/référence à forte cardinalité.
- Colonnes montant/date inchangées (plage).
- Aucun appel back ajouté, aucune écriture.
- Build front tsc+vite + oxlint 0 erreur.

## Risques / dépendances
- **Aucune dépendance back** — livrable en isolation, faible risque.
- Dépendance UX avec [TASK-068](TASK-068-selecteur-colonnes-persistant-listes.md) (sélecteur de
  colonnes) : indépendantes techniquement, mais touchent les **mêmes fichiers** de grille
  (`AffectationsDrill`, `ReglementsSelection`, `ControlGrid`) → coordonner l'ordre d'édition pour
  éviter les conflits.

## NOTES
- Scission décidée par le PO (13/07/2026) sur recommandation architecte : livrer la valeur front
  vite (faible risque), isoler le risque perf back dans TASK-067B.
- Arbitrage LIKE vs liste sur forte cardinalité : tranché — `list` uniforme, pas de branche `text`
  ajoutée ici (voir Contexte ci-dessus).
</content>
