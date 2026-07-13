# TASK-040 — Le compteur « Règlements : N » ne reflète pas les filtres appliqués sur la grille

## Contexte
Sur l'écran « Rapprochement bancaire » (front TASK-037, endpoint TASK-036), l'en-tête affiche
`Règlements : 1300`. Après application d'un filtre de colonne sur la grille (ex. filtre sur
`N° Règlement`), la liste affichée se réduit mais **le compteur reste à 1300**. Signalé par le
PO le 09/07/2026 : « le nombre de règlement affiché doit correspondre au nombre d'éléments de la
liste après les filtres appliqués sur la grid ».

## Cause racine
Le compteur affiche `total` (`RapprochementInterrogation.tsx:263`), alimenté par
`res.data.totalCount` (`RapprochementInterrogation.tsx:159`), c'est-à-dire le **total serveur
global** de la période, ne tenant compte que des **filtres serveur** (`mode`, `rapprocheBanque`,
`declare`, `tiers`).

Or trois filtres — `numeroReglement`, `origine`, `domaine` — sont appliqués **côté client,
uniquement sur la page courante** (`RapprochementInterrogation.tsx:146-157`) et ne sont **jamais
répercutés dans `total`** :

- `numeroReglement` : « Pas de filtre serveur … on ne les envoie pas » (commentaire ligne 141),
  filtré localement lignes 146-149.
- `origine` : filtré localement lignes 150-153.
- `domaine` : filtré localement lignes 154-157.

Conséquence : dès qu'un de ces trois filtres est actif, la grille affiche moins de lignes que le
nombre annoncé. Le mismatch est **structurellement garanti**, et aggravé par la pagination
serveur (`size = 100`) : ces filtres client ne portent que sur la page visible, jamais sur les
1300 lignes réelles.

> Note : ce n'est **pas** qu'un problème d'affichage. Afficher naïvement `data.length` serait
> tout aussi faux, car `data` est plafonné à la page (100). Le compteur honnête suppose que le
> filtrage soit connu du serveur.

## Périmètre STRICT
- **Inclus** : déplacer les trois filtres `numeroReglement`, `origine`, `domaine` **côté serveur**
  dans l'endpoint `GET /api/rapprochement` (TASK-036), afin que `totalCount`, la pagination et la
  liste soient tous cohérents avec les filtres. Le compteur `total` redevient alors exact
  mécaniquement, sans changer la ligne d'affichage.
- Le front cesse de filtrer sur la page (`RapprochementInterrogation.tsx:146-157`) et transmet ces
  trois filtres en paramètres de requête, comme déjà fait pour `tiers`/`mode`/`rapprocheBanque`/
  `declare`.
- `origine`/`domaine` sont **multi-sélection** (ExcelFilter) : le contrat serveur doit accepter
  une liste de valeurs (contrairement aux filtres scalaires actuels qui ne retiennent que la 1re).
- **Exclu** : aucun recalcul TVA, aucune écriture, aucun changement de la sémantique du DTO
  (mêmes champs). Ne pas toucher au tampon DT_Id (TASK-028). Ne pas modifier le tri/pagination
  existants. Rester en lecture seule stricte.

> Dépendance : cette task suppose l'endpoint TASK-036 en place. Elle **doit être séquencée après
> TASK-039** (filtre `MV_Domaine`) pour éviter deux éditions concurrentes de `RapprochementFromWhere`.

## Objectif
```
Entrée : GET /api/rapprochement?debut&fin&numero=…&origine=…&domaine=…  (+ filtres existants)
Traitement : la projection applique numero/origine/domaine dans le MÊME WHERE que le COUNT
Sortie : TotalCount = nombre réel de règlements filtrés ; la grille et le compteur coïncident
         quelle que soit la page
```

## Étapes
1. **Back — `DeclarationRepository.cs`** : ajouter les prédicats `numeroReglement` (LIKE),
   `origine` (IN sur les libellés/`EC_Type` dérivés) et `domaine` (IN sur `MV_Domaine`) au
   `WHERE` partagé de `RapprochementFromWhere`, appliqués **identiquement** à la liste et au
   `COUNT`. Attention : `origine` (Sage/FGR/Mixte/SansAffectation) est **dérivée** de `EC_Type` —
   le filtre doit porter sur la même expression de dérivation que la projection, sinon il ment.
2. **Back — DTO/contrôleur** (`RapprochementController.cs`, DTO de requête) : exposer les trois
   nouveaux paramètres (numero texte, origine[] et domaine[] listes).
3. **Front — `RapprochementInterrogation.tsx`** : envoyer `numero`, `origine`, `domaine` dans
   `params` (`fetchPage`, lignes 122-143) et **supprimer** le filtrage client lignes 146-157.
   Le compteur `total` (ligne 263) reste inchangé — il devient exact automatiquement.
4. **Front — options de filtres** : `filterOptionsFor` (lignes 197-207) construit aujourd'hui les
   valeurs `origine`/`domaine` à partir de `data` (page courante). À réévaluer : soit les tirer de
   l'endpoint `distincts` (TASK-036) pour couvrir tout le périmètre, soit assumer explicitement la
   portée page. **Décision à confirmer** — a minima documenter le choix.

## Livrables
- Endpoint `GET /api/rapprochement` acceptant et appliquant `numero`/`origine`/`domaine` dans le
  WHERE partagé liste+count.
- Front sans filtrage client résiduel sur ces trois colonnes.
- `VERIFY/TASK-040_verify.md` : preuve réelle — filtrer sur `N° Règlement` (et sur `origine`,
  `domaine`) puis vérifier que `Règlements : N` == nombre de lignes réellement retournées après
  filtre, sur une période multi-pages ; capture avant/après.

## Critères de validation
- Après application d'un filtre `numeroReglement`, `origine` ou `domaine`, le compteur affiché
  == nombre total de règlements correspondant au filtre (pas seulement la page courante).
- Pagination cohérente : `totalPages` recalculé sur le total filtré.
- Aucun filtrage client résiduel divergeant du serveur.
- Multi-sélection `origine`/`domaine` respectée (pas de perte des valeurs au-delà de la 1re).
- Aucune écriture, aucun recalcul TVA, aucun changement de sémantique DTO.

## Risques / dépendances
- **Dépendance de séquencement** : après TASK-039 (même méthode `RapprochementFromWhere`) pour
  éviter une collision d'édition.
- Filtre `origine` dérivé de `EC_Type` : le prédicat SQL doit répliquer exactement l'expression de
  dérivation de la projection, sinon incohérence liste/filtre. Point de vigilance du VERIFY.
- `distincts` vs page pour peupler les options de filtre : trancher (étape 4).
