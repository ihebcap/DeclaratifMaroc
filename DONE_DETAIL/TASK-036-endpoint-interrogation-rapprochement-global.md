# TASK-036 — Endpoint d'interrogation « Rapprochement bancaire » global (règlement-pivot, lecture seule)

## Contexte
Décision PO (09/07/2026) : l'entrée de menu **« Rapprochement bancaire »** est une **interrogation globale**, indépendante d'une déclaration (choix « Deux entrées indépendantes »). Or aujourd'hui la seule source de lignes (`GET /declarations/{id}/lignes`) est **scellée par déclaration** (figée au 1er appel, périmètre d'une période). Il faut donc une **nouvelle source de lecture** pour interroger le rapprochement sur tout le référentiel, hors d'une déclaration.

Pivot retenu = **le règlement** (fait générateur de la TVA sur décaissement : la TVA se déduit au **paiement**). C'est aussi la lecture que le client connaît déjà (écran GOCOM « Règlements » : Montant / Pointé / Comptabilisé) — réutilisée ici en **lecture seule**, enrichie de l'affectation et de la TVA.

## Périmètre STRICT
- **Inclus** : un endpoint HTTP **lecture seule** exposant les règlements avec leur état de rapprochement banque, leur affectation aux factures (reste à affecter), leur origine (`EC_Type`) et leur état de déclaration (`DT_Id`), filtrable/paginable, indépendant de toute déclaration.
- **Exclu** : **aucune écriture** (SELECT strict), aucun appel de DLL métier GRC en écriture, aucun `UPDATE` sur table pilotée par DLL, aucune logique de *faire* le rapprochement (c'est le rôle de GOCOM). Ne pas toucher au workflow de déclaration ni au tampon `DT_Id` (TASK-028). Aucune string de connexion / port en dur.

## Source de données (réel, vérifié dans le repo)
- `RT_MOUVEMENT` : `MV_Id`, `MV_Numero`, montant, mode/date, tiers ; **`MV_Point`/`MV_PointDate`** = rapproché banque (source de rapprochement **locale** retenue — option B, cf. TODO).
- `RT_AFFECTATION` : `MV_Id` ↔ facture, montant affecté ; **`DT_Id`** = tampon « déclaré » (TASK-028, `NULL` = non déclaré).
- `RT_ECHEANCE` : `EC_Type` (0=Sage/OM, 111=FGR, 4=solde initial — cf. mémoire `grf-echeance-ectype-mapping`), `DO_Numero`.

## Objectif
```
Entrée  : filtres (période bornée, mode(s), rapproché banque O/N, déclaré O/N, tiers), page, size, sort, filter
Traitement : SELECT (lecture seule) joignant RT_MOUVEMENT + RT_AFFECTATION + RT_ECHEANCE,
             agrégé PAR RÈGLEMENT (MV)
Sortie  : liste paginée de règlements : montant, mode, date, tiers, rapproché banque (MV_Point),
          nb factures affectées, montant affecté, RESTE À AFFECTER, origine (EC_Type), déclaré (DT_Id),
          + distincts pour les filtres liste. Aucune ligne masquée.
```

## Étapes
1. Définir un **DTO de réponse explicite** (`[JsonPropertyName]`, même discipline que TASK-034 — vocabulaire métier, pas de camelCase accidentel) : `numeroReglement`, `date`, `mode`, `tiers`, `montant`, `rapprocheBanque` (bool), `nbFacturesAffectees`, `montantAffecte`, `resteAAffecter`, `origine` (Sage/FGR/SoldeInitial), `declare` (bool via `DT_Id`).
2. Repo : méthode `SELECT` paginée (borne période obligatoire, `OFFSET/FETCH`), **lecture seule stricte**, réutilisant `IConnectionFactory` (aucune string en dur). Chunker les `IN` si besoin (pattern TASK-018).
3. Controller : `GET /rapprochement` (route dédiée, hors `/declarations/{id}`) avec `page/size/sort/filter` + `distincts`, miroir de l'existant lignes.
4. **Reste à affecter** = montant règlement − Σ affectations : rendre l'écart **visible** (jamais absorbé silencieusement) — pilier confiance.
5. Tests : au moins un test de projection/mapping + une preuve réelle sur base accessible (`GR_EMA_DISTRIBUTION`, cf. mémoire `grf-acces-db-prod-disponible`).

## Livrables
- Endpoint `GET /rapprochement` lecture seule + DTO explicite.
- `VERIFY/TASK-036_verify.md` : preuve réelle (extrait JSON) montrant des règlements avec rapproché banque O/N, reste à affecter non nul rendu visible, origine `EC_Type` correcte ; build C# 0 erreur ; démonstration SELECT-only (aucune écriture).

## Critères de validation
- Endpoint indépendant de toute déclaration, **lecture seule** (aucun `UPDATE`/`INSERT`, aucun appel DLL en écriture).
- Pivot règlement : 1 ligne = 1 règlement, avec affectations agrégées et **reste à affecter explicite**.
- `rapprocheBanque` reflète `MV_Point`, `declare` reflète `DT_Id`, `origine` reflète `EC_Type`.
- Filtres/tri/pagination opérants ; distincts fournis.
- Contrat DTO explicite (`JsonPropertyName`), aligné pour le front TASK-037.

## Risques / dépendances
- **Débloque TASK-037** (écran front). Chemin critique cœur : **036 → 037**.
- Volume : borner la période obligatoirement (perf) — pas de scan intégral non borné.
- Cohérence de la notion « rapproché » : rester sur la source **locale** `MV_Point` (option B déjà actée), ne pas réintroduire de dépendance externe.
- Ne pas empiéter sur le tampon `DT_Id` (TASK-028) : ici on **lit** `DT_Id`, on ne le modifie jamais.
