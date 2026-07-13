# TASK-063 — Filtres cohérents par type de données sur la liste de rapprochement (parité de filtre par colonne)

## Contexte
Retour PO (12/07/2026) sur l'écran **Rapprochement bancaire** (interrogation, pivot règlement,
`RapprochementInterrogation.tsx` + endpoint `GET /api/rapprochement`, TASK-036/040) :

1. **« Problème de traçage » sur les deux dernières colonnes** (`Origine`, `Déclaré`). Diagnostic
   (lecture code) :
   - `origine` est filtré correctement en **multi-sélection** (tableau sérialisé, WHERE serveur).
   - `declare` (comme `mode`) n'envoie que **la 1re valeur cochée** :
     `RapprochementInterrogation.tsx:141-142` → `params.declare = dc[0]`. Cocher « Oui » **et**
     « Non » ne renvoie que « Oui » → **filtre menteur** (la sélection affichée ≠ résultat).
2. **Filtres manquants ou non adaptés au type de données.** Sur 16 colonnes, seules 7 ont un
   `filterType`, toutes en `list`/`text`. Exigence PO : **chaque colonne doit avoir un filtre
   compatible avec son type** :
   - **date** → toujours **entre deux dates** (plage `du ~ au`) ;
   - **montant / compteur** → **entre deux montants** (plage `min ~ max`) ;
   - **énumération** → **cases à cocher avec recherche** ;
   - **texte** → filtre texte (LIKE).

Le composant `ExcelFilter` **gère déjà** les 4 types (`list`, `text`, `number`, `date`, encodage
plage `min~max`). Le coût réel est **côté serveur** : depuis TASK-040 le filtrage est serveur (pour
que `TotalCount` et la pagination restent exacts, cf. TASK-040) — chaque nouveau filtre exige un
paramètre appliqué **à l'identique** dans `RapprochementFromWhere` (liste **et** COUNT partagés).

## Périmètre STRICT
- **Inclus** :
  1. **Corriger le filtre menteur `declare` et `mode`** : passer en multi-sélection serveur (tableau),
     comme `origine`/`domaine` déjà en place — ou, si le back reste scalaire bool/int, **désactiver la
     multi-sélection** dans le widget pour ces colonnes (radio) afin que l'affichage ne mente pas.
     Décision d'implémentation : privilégier la **multi-sélection serveur** (cohérence avec origine).
  2. **Parité de filtre par colonne**, type-approprié, **filtré côté serveur** (WHERE partagé
     liste+COUNT) :

     | Colonne | `filterType` cible | Paramètre serveur à ajouter |
     |---|---|---|
     | `numeroReglement` | text | déjà `numero` ✅ |
     | `date` | date (plage) | déjà borné par la période globale `Du/Au` — **exclu** (voir Exclu) |
     | `mode` | list (multi) | `mode` → **passer en `int[]`** |
     | `domaine` | list (multi) | déjà `domaine[]` ✅ |
     | `tiers` | text | déjà `tiers` ✅ |
     | `montant` | number (plage) | `montantMin` / `montantMax` |
     | `rapprocheBanque` | list (multi/bool) | déjà `rapprocheBanque` ✅ |
     | `point` | list (bool) | `point` (nouveau, bool `MV_Point`) |
     | `dateRapprochement` | date (plage) | `dateRappMin` / `dateRappMax` (`MV_PointDate`) |
     | `numeroExtrait` | text | `numeroExtrait` (LIKE `MV_...`) |
     | `echeance` | date (plage) | `echeanceMin` / `echeanceMax` |
     | `banqueCode` | list ou text | `banque` (LIKE sur `vBanque` code) |
     | `nbFacturesAffectees` | number (plage) | `nbFacturesMin` / `nbFacturesMax` |
     | `resteAAffecter` | number (plage) | `resteMin` / `resteMax` |
     | `origine` | list (multi) | déjà `origine[]` ✅ |
     | `declare` | list (multi/bool) | déjà `declare` — **corriger multi (point 1)** |

  3. **Front** `RapprochementInterrogation.tsx` : renseigner `filterType` sur chaque colonne selon le
     tableau, brancher les nouveaux paramètres dans `fetchPage` (encodage plage `min~max` → deux
     query params), et alimenter les options `list` manquantes (`point`) depuis `filterOptionsFor`.
  4. **Back** `RapprochementController` + `DeclarationRepository.RapprochementFromWhere` : ajouter les
     paramètres, tous appliqués **à l'identique** dans la liste ET le COUNT (constante partagée), pour
     que `TotalCount` reste exact quelle que soit la page (invariant TASK-040 à ne pas casser).
     Plages `NULL`-safe (`@xMin IS NULL OR col >= @xMin`), montants comparés à la **même expression**
     que la projection (ex. `resteAAffecter` = montant − montant affecté, cf. TASK-036).
- **Exclu** :
  - **Aucune écriture** (lecture seule stricte, `DT_Id` intouché — TASK-028).
  - Colonne `date` (règlement) : **pas** de filtre de plage dédié — déjà bornée par la période
    globale obligatoire `Du/Au` (doublon inutile ; garder le tri). Décision réversible si le PO veut
    en plus un sous-intervalle.
  - Pas de refonte du widget `ExcelFilter` (déjà complet).
  - Pas de changement de la règle de période / `DateReference` (**TASK-062**, séquencée avant).
  - Pas de TVA par règlement (**TASK-043**).

## Objectif
```
Chaque colonne de la liste de rapprochement porte un filtre du bon type :
  - date        → plage du ~ au            (dateRapprochement, echeance)
  - montant     → plage min ~ max          (montant, resteAAffecter, nbFacturesAffectees)
  - énumération → cases à cocher + recherche, multi-sélection RÉELLE (mode, declare corrigés)
  - texte       → LIKE                      (numeroExtrait, banqueCode)

Tous les filtres sont appliqués CÔTÉ SERVEUR (WHERE partagé liste + COUNT) →
TotalCount et pagination cohérents avec la grille (invariant TASK-040 préservé).
Plus aucun filtre menteur : cocher « Oui » + « Non » sur Déclaré renvoie bien les deux.
```

## Étapes
1. **Back — paramètres & WHERE** : ajouter à `RapprochementController.GetReglements` et à
   `RapprochementFromWhere`/`RapprochementParams` les paramètres du tableau (plages `NULL`-safe,
   `mode` en `int[]`, `point` bool, montants/reste comparés à l'expression de projection). Répliquer
   à l'identique dans le COUNT.
2. **Back — options `point`** : exposer les valeurs Oui/Non (bool) ; réutiliser `distincts` si besoin.
3. **Front — colonnes** : renseigner `filterType` sur chaque `Col` selon le tableau.
4. **Front — `fetchPage`** : décoder les plages (`min~max` → deux params), passer `mode` en tableau,
   corriger `declare` en tableau (fin du `dc[0]`).
5. **Front — `filterOptionsFor`** : ajouter `point` ; conserver options serveur pour `mode`.
6. **Tests** :
   - back : filtre plage montant/date/nbFactures → liste ET COUNT filtrés identiquement ;
     multi-sélection `mode`/`declare` (2 valeurs) → union correcte ; `NULL`-safe (aucune borne = pas
     de filtre) ;
   - non-régression TASK-040 (compteur = lignes affichées sous filtre, toutes pages).

## Livrables
- Liste de rapprochement : **toutes** les colonnes filtrables, chaque filtre adapté au type
  (dates entre deux dates, montants entre deux montants, énumérations en cases cochables + recherche,
  texte en LIKE).
- Filtres `mode` et `declare` en **multi-sélection réelle** (plus de valeur silencieusement ignorée).
- `TotalCount`/pagination cohérents avec la grille sous n'importe quelle combinaison de filtres.
- `VERIFY/TASK-063_verify.md` : preuve réelle sur `GR_EMA_DISTRIBUTION` —
  - un filtre de chaque type (plage montant, plage date sur `MV_PointDate`, multi `declare` Oui+Non,
    texte `numeroExtrait`) : capture liste + `TotalCount` cohérents ;
  - démonstration que cocher « Oui » + « Non » sur `Déclaré` renvoie l'ensemble (bug corrigé) ;
  - contrôle lecture seule : aucun `UPDATE`, `DT_Id` inchangé.

## Critères de validation
- Chaque colonne a un `filterType` conforme au type de sa donnée (tableau du périmètre).
- Tous les filtres sont **serveur** et appliqués à l'identique liste + COUNT (invariant TASK-040).
- Multi-sélection réelle pour toutes les colonnes `list` (aucune valeur cochée ignorée).
- Plages `NULL`-safe (borne vide ⇒ pas de contrainte).
- Build 0 erreur, tests verts (dont non-régression compteur).
- Aucune écriture en base.

## Risques / dépendances
- **Séquencement (édition concurrente)** : touche `RapprochementFromWhere` (partagé liste+COUNT),
  `RapprochementController`, `RapprochementInterrogation.tsx` — **mêmes fichiers que TASK-062
  (`DateReference`) et TASK-043 (TVA par règlement)**. **Séquencer après TASK-062** (qui réécrit le
  prédicat de période) pour éviter les conflits ; coordonner avec TASK-043 (même `SELECT`/projection).
- **Invariant TASK-040 à préserver** : tout filtre ajouté à la liste sans le mettre dans le COUNT
  recréerait l'incohérence compteur ≠ grille corrigée en TASK-040. Point de vigilance revue.
- **Expression de plage sur colonnes dérivées** (`resteAAffecter`, `nbFacturesAffectees`) : comparer à
  la **même** expression que la projection (sous-requête `A`), pas à une colonne physique inexistante.
- **`banqueCode`** : porté par la vue legacy `vBanque` (LEFT JOIN) → filtre LIKE nullable-safe ; ne pas
  transformer le LEFT JOIN en INNER (exclurait les règlements sans banque — espèce/caisse).
