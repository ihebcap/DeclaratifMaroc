# TASK-021 — Sélection des reportées (non-rapprochées) — Gap B de TASK-019

## Contexte
TASK-019 exige une face « ce que je NE déclare PAS », dont les **reportées** : les affectations dont le
**règlement n'est pas encore rapproché** en banque. En régime des décaissements, le droit à déduire naît
au **rapprochement du paiement** → tant que le règlement n'est pas rapproché, la TVA **attend** et sera
déclarée le mois du rapprochement.

**Le manque (Gap B), vérifié dans le code (08/07/2026) :** `SelectionnerAffectationsService` /
`SelectionExpliqueeService` ne ramènent **que les rapprochées** (`M.MV_Point = Oui`, fenêtre sur
`MV_PointDate`). Les non-rapprochées **ne sont jamais lues** → la face « reportées » est aujourd'hui
**impossible**, quelle que soit l'UI. Il faut étendre la **sélection**.

> **Décisions métier — PREMIER JET assumé (PO 08/07/2026), à affiner après premier résultat réel + test.**
> Le PO a explicitement demandé de partir sur des défauts raisonnables et d'itérer sur données réelles,
> **pas** de figer un cadrage parfait maintenant. Les deux hypothèses ci-dessous sont **visibles et
> révisables** — c'est volontaire, pas un contexte improvisé en silence.
>
> - **H1 — Profondeur :** on surface **toutes les non-rapprochées comptabilisées jusqu'à la fin de la
>   période** de déclaration (mêmes filtres métier que les rapprochées, mais `MV_Point = Non`, datées sur
>   `MV_Date` faute de `MV_PointDate`). **Pas** de fenêtre historique profonde au premier tour — on
>   observe d'abord le volume réel, on borne ensuite si nécessaire.
> - **H2 — Reportée jamais rapprochée :** elle **reste `Reportee`** et réapparaît à chaque période
>   jusqu'à rapprochement ou annulation. **Aucune** sortie automatique / statut « abandon » au premier
>   tour ; la sortie se fait par **exclusion manuelle** (déjà prévue TASK-019).

## Périmètre STRICT
- **Uniquement le back**, couche **sélection** : ajouter la lecture des **non-rapprochées** et les marquer
  `EtatLigne.Reportee` (l'enum a déjà `Reportee`).
- **Réutiliser** la structure des requêtes existantes (`SelectionnerAffectationsService` /
  `SelectionExpliqueeService`) : mêmes jointures M↔A↔E, mêmes filtres (domaine, comptabilisé, non annulé,
  non impayé), **seule** la condition de rapprochement bascule (`MV_Point = Non`) + le filtre de date
  passe sur `MV_Date`.
- **Exclu** : le calcul de ventilation/montants (worker OM, orchestrateur — inchangés, les reportées ne
  produisent pas de TVA déclarée ce mois), le front (TASK-019), la définition d'éligibilité des
  rapprochées (pas touchée), tout nouveau statut d'enum.

## Objectif
Faire remonter les reportées dans le jeu de candidates, marquées `Reportee`, **sans casser** l'existant
ni la réconciliation.

### 1. Requête reportées
- Miroir des requêtes rapprochées, par domaine, avec `MV_Point = Non` et fenêtre sur `MV_Date`
  (≤ fin période), mêmes garde-fous (`MV_Compta`, `MV_Annule`, `MV_Impaye`, sens).
- Chaque reportée porte les mêmes champs qu'une affectation (n° règlement `MV_Numero`, facture,
  montant affecté `AF_Montant`, IF/ICE) → cohérente avec l'avenant **TASK-020** (regroupable par
  règlement, conformité affichable).

### 2. Marquage et intégration
- Les lignes reportées sont persistées avec `Etat = Reportee` et **ne comptent pas** dans la TVA à
  déclarer ce mois (elles sont dans la face « je ne déclare pas »).
- **Décision PO 08/07 (remarque 1) — reportées = stock À PART, HORS équilibre du mois.** L'équilibre
  « écart 0 » (règle n°2) porte **uniquement sur les rapprochées** (`intégrées + exclues + écartées +
  proposées = Σ sources rapprochées`). Les reportées ont leur **propre total séparé** (« ce qui attend
  le rapprochement ») — **jamais additionné** à l'équilibre du mois, sinon l'écart paraît faux. Deux
  compteurs distincts, chacun bouclant sur son périmètre.

### 3. Traçabilité du motif
- Motif clair et uniforme sur les reportées (ex. « Règlement non rapproché au JJ/MM/AAAA ») — règle n°1
  (aucune ligne silencieuse), cohérent avec les motifs des écartées.

## Contraintes techniques
- **Ne pas modifier** le comportement des rapprochées (aucune régression sur la sélection actuelle,
  tests TASK-008/015/017 doivent rester verts).
- **Track B — vraie base** : à prouver sur `GR_EMA_DISTRIBUTION` / `SO_Id=1` (accès disponible via
  TASK-001/017). Isolation `RT_*` respectée comme TASK-017.
- `net10.0`, Clean Architecture GRC_WEB ; tests d'intégration sur la nouvelle requête.
- Volume : les reportées peuvent être nombreuses (H1 large) → **surveiller le volume au premier résultat**
  (c'est précisément le point que le PO veut observer avant de borner — cf. H1).
- **Transparence intacte** : on n'agrège rien en silence ; chaque reportée reste une ligne avec motif.

## Étapes
1. Ajouter la/les requête(s) **non-rapprochées** (miroir, `MV_Point = Non`, date sur `MV_Date`) par
   domaine dans la couche sélection.
2. Mapper ces affectations en lignes `Reportee` + motif, avec n° règlement / IF/ICE (aligné TASK-020).
3. Les intégrer au jeu de candidates figé sans casser la réconciliation ni les rapprochées.
4. Tests d'intégration : une non-rapprochée réelle remonte en `Reportee` ; une rapprochée reste comme
   avant ; **la réconciliation boucle** (Σ candidates = 5 statuts).
5. **Mesurer le volume réel** des reportées sur `GR_EMA_DISTRIBUTION` (input pour décider si H1 doit être
   bornée) — le consigner dans le VERIFY.

## Livrables
- Back sélection étendu (requêtes non-rapprochées + marquage `Reportee` + motif).
- Tests d'intégration (reportée remonte, rapprochée intacte, réconciliation boucle).
- `VERIFY/TASK-021_verify.md` : preuve réelle qu'une non-rapprochée apparaît en `Reportee` avec motif,
  que les rapprochées sont inchangées, que la réconciliation boucle, **et le volume observé des
  reportées** (pour arbitrer H1 ensuite).

## Critères de validation
- Les non-rapprochées comptabilisées ≤ fin période remontent en `Reportee` avec **motif clair**.
- Les rapprochées et leurs montants sont **inchangés** (aucune régression TASK-008/015/017).
- Une reportée porte n° règlement + IF/ICE (regroupable / conforme à TASK-020).
- **Réconciliation bouclée** (candidates = intégrées + exclues + reportées + écartées + proposées).
- Prouvé sur `GR_EMA_DISTRIBUTION` / `SO_Id=1` ; volume des reportées mesuré et consigné.
- Build + tests verts.

## Risques / dépendances
- **Cohérence avec TASK-020** : les reportées doivent porter les mêmes champs (règlement, IF/ICE) →
  faire TASK-020 d'abord ou en parallèle proche, pour un modèle de ligne homogène.
- **H1 / volume (le vrai risque)** : « toutes les non-rapprochées jusqu'à fin période » peut être
  volumineux ou remonter du très ancien non pertinent. **C'est assumé au premier tour** (le PO veut voir
  le résultat réel avant de borner). À réviser après mesure — ne pas optimiser prématurément.
- **H2 / reportées éternelles** : sans sortie automatique, une reportée jamais rapprochée s'accumule.
  Toléré au premier tour (exclusion manuelle possible). À rediscuter si le volume gêne.
- **Sémantique `MV_Point`/`MV_Date`** : confirmer sur la vraie base que `MV_Point = Non` isole bien les
  non-rapprochées et que `MV_Date` est la bonne date de rattachement en l'absence de `MV_PointDate`.
- **Ne débloque la face « reportées » de TASK-019 que combiné à TASK-020** (n° règlement + conformité).
