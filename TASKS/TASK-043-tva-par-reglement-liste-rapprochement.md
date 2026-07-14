# TASK-043 — Montant de TVA par règlement dans la liste de rapprochement (FGR + Sage)

> **Renommée de TASK-041 → TASK-043 (09/07/2026)** : collision de numéro avec « Écran Factures » (TASK-041).
> **Séquencement : après TASK-039 (filtre `MV_Domaine`) et TASK-040** — ne pas lancer avant.

## Contexte
Sur l'écran « Rapprochement bancaire » (front TASK-037, endpoint TASK-036), le PO demande
(09/07/2026) d'afficher, **pour chaque règlement**, le **montant de TVA** correspondant, quelle
que soit son origine — **facture Sage** (`EC_Type=0`) **ou FGR** (`EC_Type=111`).

Aujourd'hui la projection de rapprochement (`ReglementRapprochementRow`) est **volontairement**
un `SELECT` léger, pivot = règlement, agrégeant les affectations (nombre, montant affecté,
`EC_Type` min/max, reste à affecter) **sans aucun recalcul de TVA** (doc de l'entité :
« aucun recalcul de TVA »). La TVA n'existe pour l'instant que dans le **workflow de déclaration**
(`DeclarationWorkflowService` → `tl.Tva`), produit par la valorisation Sage/FGR.

**Décision de périmètre PO : FGR + Sage complet** (couverture des deux origines), et non FGR seul.

## Sources de TVA par origine (rappel modèle — mémoire `grf-echeance-ectype-mapping`)
- `EC_Type=111` **FGR** : TVA lue en **SQL pur** via `ILecteurTvaFgr.LireTvaFgr(ecId, …)`
  (`Declaration.Core`, base `RT_HISTOCOMPTA`) — **coût faible**.
- `EC_Type=0` **Sage** : TVA via valorisation OM. Le coût COM par facture est le **goulot
  historique** (`grf-tva-perf-lecture-sage`). Deux atouts déjà livrés le neutralisent :
  - **TASK-024** `IVentilationSageCacheRepository.GetEntries(ecId, …)` → buckets TVA en cache
    SQL (relu 100 % SQL, validé par le token de paiement local) ;
  - **TASK-023** lecture Sage **en session réutilisée** (fallback en cas de cache miss, borné,
    isolé par pièce).
- `EC_Type=4` **SoldeInitial** : 0 TVA déclarable (TASK-025) → afficher « — ».
- **SansAffectation / Mixte** : voir règles de transparence ci-dessous.

## Périmètre STRICT
- **Inclus** :
  1. Exposer dans la projection, par règlement, la **liste de ses affectations valorisables**
     (`EC_Id`, `EC_Type`, `AF_Montant`, et le `TTC` facture nécessaire au prorata) — ou une
     structure équivalente permettant l'enrichissement TVA hors du `SELECT` d'agrégat.
  2. **Enrichir la page courante uniquement** (bornée à `size` lignes) avec un **montant TVA par
     règlement**, calculé après le `SELECT` paginé, en routant par `EC_Type` :
     - `111` → `LecteurTvaFgr` ;
     - `0` → cache TASK-024 d'abord, **fallback** lecture Sage session réutilisée (TASK-023) ;
     - `4` / non valorisable → pas de TVA (« — »).
  3. Ajouter au **DTO** un champ `MontantTva` (nullable) **et** un champ d'**état de valorisation**
     explicite (`Valorisee` / `Partielle` / `Indisponible` / `NonApplicable`) — la TVA n'est
     jamais un `0` muet.
  4. **Front (TASK-037 / `RapprochementInterrogation.tsx`)** : nouvelle colonne « TVA » alignée à
     droite, affichant le montant + un marqueur visuel quand l'état ≠ `Valorisee` (même principe
     que le reste à affecter orange).
- **Exclu** :
  - **Aucune écriture**, aucun `DT_Id` touché (TASK-028), lecture seule stricte.
  - La TVA **n'entre ni dans le `WHERE`, ni dans le `COUNT`, ni dans le tri** de l'endpoint
    (sinon on valoriserait toute la période → régression perf interdite).
  - Pas de valorisation de la période entière : **jamais** au-delà de la page affichée.
  - Pas de nouvelle ventilation facture-par-facture à l'écran (ça reste le poste Déclaration,
    décision TASK-037) : ici = **un montant agrégé par règlement**.
  - Pas de recalcul modifiant les buckets en cache ; réutilisation stricte des lecteurs existants.

## Règles de transparence (piliers confiance — mémoire `grf-objectif-confiance-transparence`)
1. **Affectation partielle** (règlement partiel) : la TVA attribuée au règlement =
   `TVA_facture × (AF_Montant / TTC_facture)`. Ne jamais imputer 100 % de la TVA facture à un
   règlement qui ne solde qu'une partie.
2. **Multi-facture** : un règlement affecté à plusieurs factures → TVA = **somme** des parts.
3. **Reste à affecter ≠ 0** : la part non affectée **n'a pas de TVA** ; l'état de valorisation le
   reflète (`Partielle`) — l'écart n'est jamais absorbé dans le montant TVA.
4. **Cache miss + facture illisible OM** (alerte `FACTURE_ILLISIBLE_OM`, TASK-023) : état
   `Indisponible`, **jamais** un `0` silencieux.
5. **Origine Mixte** : sommer les parts par origine ; si une part est indisponible → `Partielle`.

## Objectif
```
Entrée : GET /api/rapprochement?debut&fin&page&size (+ filtres existants)
Traitement : SELECT paginé inchangé → enrichissement TVA de la SEULE page
             (FGR via LecteurTvaFgr, Sage via cache TASK-024 + fallback TASK-023),
             prorata AF_Montant/TTC, somme multi-facture
Sortie : chaque ligne porte MontantTva + EtatValorisation ; COUNT/tri/pagination inchangés ;
         aucune écriture ; perf bornée à `size` factures/page
```

## Étapes
1. **Projection** (`DeclarationRepository.GetReglementsRapprochementAsync` + `ReglementRapprochementRow`) :
   ajouter la remontée des affectations valorisables du règlement (`EC_Id`, `EC_Type`,
   `AF_Montant`, `TTC` facture). Selon le coût, soit un second `SELECT` borné aux `MV_Id` de la
   page, soit une extension de la requête existante. **Ne pas** alourdir le `COUNT`.
2. **Service d'enrichissement TVA** (couche Application/Orchestration, réutilisant
   `ILecteurTvaFgr` et `IVentilationSageCacheRepository`) : pour la page, calcule
   `MontantTva` + `EtatValorisation` par règlement selon les règles de transparence. Le fallback
   Sage passe par la **session réutilisée** (TASK-023), borné et isolé par pièce.
3. **DTO** (`ReglementRapprochementDto`) : exposer `MontantTva` (nullable) et `EtatValorisation`.
4. **Contrôleur** (`RapprochementController.GetReglements`) : brancher l'enrichissement sur les
   `Items` de la page **après** pagination (jamais avant).
5. **Front** (`RapprochementInterrogation.tsx`) : colonne « TVA » (montant + marqueur d'état),
   sans tri/filtre serveur sur ce champ.

## Livrables
- Endpoint `GET /api/rapprochement` : chaque ligne de la page porte `MontantTva` + état, sans
  changer `TotalCount`, tri, pagination.
- Front : colonne TVA lisible, états non-`Valorisee` rendus visibles.
- Tests : couverture des règles de transparence (partiel, multi-facture, mixte, indisponible,
  solde initial) sur données réelles `GR_EMA_DISTRIBUTION` — étendre
  `Task036RapprochementProjectionTests` et/ou `LecteurTvaFgrTests`.
- `VERIFY/TASK-043_verify.md` : preuve réelle sur la base —
  - un règlement **FGR** (ex. `FF260070`/`EC_Id=22297`, TVA connue TASK-022) : montant TVA exact ;
  - un règlement **Sage** servi par le cache TASK-024 : montant TVA = valorisation OM ;
  - un règlement **partiel** : prorata vérifié (TVA imputée < TVA facture) ;
  - un règlement **multi-facture** : somme vérifiée ;
  - **perf** : temps d'enrichissement d'une page (`size=50`) mesuré, cache chaud vs froid ;
  - contrôle **lecture seule** : aucun `INSERT/UPDATE` GRF, `DT_Id` inchangé.

## Critères de validation
- Montant TVA correct par origine (FGR **et** Sage), prorata partiel appliqué, somme multi-facture.
- Aucun `0` TVA silencieux : tout cas non valorisé porte un état explicite visible au front.
- `TotalCount`, tri et pagination **inchangés** ; la TVA n'influe sur aucun d'eux.
- Enrichissement **borné à la page** : aucune valorisation de la période entière ; cache TASK-024
  privilégié, fallback COM borné/isolé (TASK-023).
- Lecture seule stricte : aucune écriture GRF, tampon `DT_Id` non touché.
- Réutilisation des lecteurs existants (`ILecteurTvaFgr`, `IVentilationSageCacheRepository`) — pas
  de duplication de logique de valorisation.

## Risques / dépendances
- **Dépendance de séquencement** : touche la projection et le DTO de rapprochement, comme
  **TASK-039** (`MV_Domaine`) et **TASK-040** (filtres serveur). **Séquencer après 039 et 040**
  pour éviter des éditions concurrentes de `RapprochementFromWhere` / du DTO.
- **Perf (risque principal)** : un taux de **cache miss Sage** élevé sur une page reporte le coût
  COM (TASK-023) sur le rendu. Mitigations : cache-first, borne à `size`, isolation par pièce,
  état `Indisponible` plutôt que blocage. À **mesurer** dans le VERIFY (cache froid vs chaud).
- **Prorata** : nécessite le `TTC` facture fiable ; si absent/incohérent pour une origine, l'état
  doit tomber en `Partielle`/`Indisponible`, jamais un montant faux.
- **Cohérence avec le poste Déclaration** : le montant TVA affiché ici (agrégat règlement) doit
  être **réconciliable** avec la ventilation détaillée de la déclaration (même sources), sans la
  dupliquer. Point de vigilance du VERIFY.
