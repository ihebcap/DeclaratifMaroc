# TASK-067B — Filtres « valeurs disponibles » (Excel-like), Part B back+front (grilles server-side)

> Suite de [TASK-067A](TASK-067A-filtres-valeurs-list-front-client-side.md) (scission décidée PO
> 13/07/2026). **Séquencer après 067A** (patron `filterOptionsFor` validé côté client-side d'abord).

## Contexte
Même demande PO que TASK-067A, pour les grilles **server-side** (page courante seule en mémoire,
filtrage serveur) : peupler les options depuis `data` (page) **mentirait** (valeur absente de la
page = infiltrable ; 1er filtre vide la liste des choix). Décision déjà prise et documentée en
TASK-040 (invariant compteur exact). Ces écrans exigent un **endpoint de valeurs distinctes** côté
serveur, calculé sur **le même WHERE/période** que la liste :
- `RapprochementInterrogation.tsx` (`GET /api/rapprochement`, `/rapprochement/distincts` n'expose
  aujourd'hui que `modes`).
- `FactureInterrogation.tsx` (`GET /api/factures`, `/factures/distincts`).
- `DomainGrid.tsx` (`GET /declarations/{id}/lignes`).

**Décision PO (13/07/2026) — cardinalité** : `list` par défaut, y compris n° pièce. Un repli
LIKE/`text` par colonne n'est envisagé **que si la mesure de coût `SELECT DISTINCT` (étape 4
ci-dessous) le justifie sur une colonne précise** — à documenter en NOTES du VERIFY si ce cas se
présente, pas décidé a priori.

## Périmètre STRICT
- **Inclus** :
  - Étendre les endpoints `distincts` existants (`RapprochementRepository`, `FacturesController`)
    et créer l'équivalent pour `DomainGrid`, pour renvoyer les valeurs distinctes par colonne
    demandée, **sur le même WHERE que la liste** (lecture seule stricte, `SELECT DISTINCT` borné).
  - Brancher `filterOptionsFor` de ces 3 écrans sur ces valeurs (`filterType: 'list'`).
  - Contrat clair `{ colonne: [valeurs] }`.
- **Exclu** : refonte `ExcelFilter` ; tri ; pagination ; toute écriture ; colonnes montant/date ;
  fusion GOCOM ; grilles déjà traitées en Part A.

## Objectif
```
Entrée : une grille server-side (page courante seule en mémoire, filtrage serveur)
Traitement : pour chaque colonne énumérable demandée, exécuter un SELECT DISTINCT borné sur le
             même WHERE/période que la liste principale, retourner { colonne: [valeurs] }
Sortie : filtre à cases cochables + recherche sur chaque colonne concernée, compteur exact,
         zéro valeur infiltrable et zéro liste de choix « menteuse »
```

## Étapes
1. Étendre `RapprochementRepository`/`FacturesController` `distincts` (et créer pour `DomainGrid`) :
   `SELECT DISTINCT` par colonne, **même `FromWhere`** que la liste (invariant TASK-040), borné en
   volume.
2. Brancher `filterOptionsFor` des 3 écrans server-side sur ces valeurs.
3. **Mesurer le coût `SELECT DISTINCT`** sur base réelle pour les colonnes à très forte cardinalité
   (n° pièce/facture). Si coût inacceptable sur une colonne précise : documenter l'exception en
   NOTES du VERIFY et proposer un repli `text`/LIKE **pour cette colonne uniquement** (pas de
   décision a priori — voir TASK-067A Contexte).
4. Vérifier l'exactitude du compteur après filtrage (aucune régression TASK-040) sur base réelle.
5. Tests : front build tsc+vite / oxlint ; back tests sur les endpoints `distincts` étendus.

## Livrables
- Back : endpoints `distincts` étendus (rappro/factures) + nouvel endpoint distincts `DomainGrid`,
  SELECT-only, même WHERE que la liste.
- Front : `RapprochementInterrogation.tsx`, `FactureInterrogation.tsx`, `DomainGrid.tsx` (options +
  filterType).
- `VERIFY/TASK-067B_verify.md` : captures Excel-like par écran, preuve compteur exact, preuve
  lecture seule (aucun write/DLL), mesure perf `SELECT DISTINCT` sur forte cardinalité, build/tests
  verts.

## Critères de validation
- Chaque colonne énumérable des 3 grilles server-side offre la liste des valeurs disponibles +
  recherche.
- Options = valeurs de la **requête complète** (période/WHERE), pas de la page ; compteur exact,
  aucune valeur infiltrable (invariant TASK-040 préservé).
- Lecture seule stricte (aucun `INSERT/UPDATE/DELETE`, aucun appel DLL/COM ajouté).
- Build front tsc+vite + oxlint 0 erreur ; tests back verts.

## Risques / dépendances
- **Dépend de TASK-067A** livrée (patron `filterOptionsFor` établi côté client-side).
- **Perf back** : `SELECT DISTINCT` bornés au même WHERE — vérifier le coût sur base réelle
  (risque principal de cette task, isolé volontairement ici plutôt que dans 067A).
- Dépendance UX avec [TASK-068](TASK-068-selecteur-colonnes-persistant-listes.md) (sélecteur de
  colonnes) : indépendantes techniquement, mais touchent les mêmes fichiers de grille → coordonner
  l'ordre d'édition pour éviter les conflits.

## NOTES
- Scission décidée par le PO (13/07/2026) sur recommandation architecte : isoler le risque perf
  back de la valeur front rapide (TASK-067A).
- Arbitrage LIKE vs liste sur forte cardinalité : `list` par défaut (décision PO) ; repli LIKE
  seulement si mesure (étape 3) l'impose, par colonne, documenté ici après coup — pas de branche
  ajoutée par anticipation.
</content>
