# TASK-042 — Rapprochement : colonnes de preuve bancaire (point, date rappro, n° extrait, échéance, banque) et règle espèce (auto-rapproché)

## Contexte
Retour PO (09/07/2026, complété 12/07/2026) sur l'écran **Rapprochement bancaire**
(front TASK-037, endpoint TASK-036). Demandes, sur le même pivot **règlement** (`RT_MOUVEMENT`) :

1. **Afficher les colonnes de preuve** aujourd'hui absentes de la projection. Jeu complet
   demandé par le PO (12/07/2026) : `MV_Point`, `MV_PointDate`, `MV_ExtraitNum`,
   `MV_Echeance`, `BanqueCode` :
   - **Rapproché (point)** = `RT_MOUVEMENT.MV_Point` — déjà lu (`MvPoint`), aujourd'hui exposé
     uniquement en booléen `rapprocheBanque` ; à rendre visible comme colonne dédiée.
   - **Date de rapprochement** = `RT_MOUVEMENT.MV_PointDate` (posée par le pointage sur extrait
     `RT_EXTRAITLIGNE` — source locale fiable, cf. TASK-001 / `CAHIER_DES_CHARGES.md`).
   - **N° d'extrait** = `RT_MOUVEMENT.MV_ExtraitNum` (numéro de l'extrait bancaire de pointage).
   - **Échéance** = `RT_MOUVEMENT.MV_Echeance` (date d'échéance de la pièce ; confirmée par la
     vue legacy `vReglementsFournisseurs` → alias `PieceEcheance`).
   - **Code banque** = `BanqueCode`, **non porté par `RT_MOUVEMENT`** : à récupérer via la vue
     `dbo.vBanque` sur `BN_Id` (source confirmée par `vReglementsFournisseurs.tlp`, cf. Étapes).

2. **Traiter le règlement espèce comme rapproché** :
   - Un règlement **espèce** (`MV_Type = 0`) n'a **par nature aucun rapprochement bancaire** :
     il n'y a pas d'extrait, donc en base `MV_Point = 0` et `MV_PointDate` est nul.
     Ce fait est déjà acté dans le code (`CHANGELOG.md` l.241 : « les espèces ont par nature
     `MV_Point = 0` … la requête espèce s'appuie sur `MV_Type = 0` + `MV_Date` »).
   - Le PO demande que, **dans notre liste**, une espèce soit **toujours affichée « rapproché »**
     (l'encaissement/décaissement espèce est soldé immédiatement, il n'a pas à attendre un extrait),
     et que sa **date de rapprochement affiche `MV_Date`** (faute de `MV_PointDate`).

> **Observation liée (à ne pas confondre)** : le PO signale par ailleurs un écart de **volume**
> sur les décaissements 2026 — **595** (appli de référence 595) vs **352** (GRF). Cet écart est un
> problème de **périmètre/comptage**, distinct de la présente demande (qui porte sur l'**affichage**
> d'un indicateur, pas sur le nombre de lignes). Piste : les **espèces** ont historiquement été
> écartées des preuves de synchro (mémoire `grf-acces-db-prod-disponible` : « 352/352, 1008/1008
> **hors espèces** »). Il est **plausible** que les ~243 lignes manquantes soient la population
> espèce. → À **quantifier** dans le VERIFY ; si l'espèce n'explique pas le gap, ouvrir une TASK de
> diagnostic dédiée (ne pas l'absorber ici).

## Périmètre STRICT
- **Inclus** :
  1. Remonter dans la projection les champs bruts : `M.MV_Point` (déjà lu), `M.MV_PointDate`,
     `M.MV_ExtraitNum`, `M.MV_Echeance`, et `B.BanqueCode` via `LEFT JOIN dbo.vBanque`.
  2. Exposer au DTO/front : **Point** (indicateur brut), **DateRapprochement**, **NumeroExtrait**,
     **Echeance**, **BanqueCode**.
  3. **Dérivation espèce (source unique, côté domaine `ReglementRapprochementRow`)** :
     - `EstRapprocheBanque` : `MV_Point == 1` **OU** `MV_Type == 0` (espèce = auto-rapproché).
     - `DateRapprochement` : `MV_Type == 0 ? MvDate : MvPointDate`.
  4. **Cohérence du filtre serveur** (`@rapproche`) : la clause `WHERE` doit refléter EXACTEMENT
     la valeur affichée (sinon le filtre « rapproché = Oui » masquerait des espèces montrées comme
     rapprochées — le « filtre qui ment » déjà proscrit pour origine/domaine, cf.
     `RapprochementFromWhere` l.296-299).
  5. **Front** (`RapprochementInterrogation.tsx`) : deux colonnes lisibles
     (date rapprochement, n° extrait), placées près de l'indicateur « rapproché banque ».
- **Exclu** :
  - **Aucune écriture** (lecture seule stricte, `DT_Id` non touché — TASK-028). On **n'écrit pas**
    `MV_Point`/`MV_PointDate` en base : la règle espèce est une **dérivation d'affichage**, jamais
    une modification de la source Sage/GRF.
  - Pas de correction du gap de volume 595/352 (voir observation ci-dessus → traçage séparé).
  - Pas de recalcul TVA (indépendant de TASK-041 TVA).

## Transparence (piliers confiance — mémoire `grf-objectif-confiance-transparence`)
- La règle « espèce = rapproché » est une **dérivation métier explicite et documentée**, pas un
  masquage : la colonne **n° extrait reste vide** pour une espèce (aucun extrait), ce qui rend
  visible que le rapproché est **d'origine espèce (automatique)** et non un pointage bancaire.
  → Recommandation d'affichage : marqueur discret (ex. « rapproché (espèce) » ou icône) pour ne
  pas laisser croire à un pointage sur extrait. À trancher au front, mais **ne jamais** afficher un
  n° d'extrait fictif.
- `MV_PointDate` affichée = `MV_Date` **uniquement** pour l'espèce ; pour tout autre mode, la vraie
  `MV_PointDate` est affichée telle quelle (nulle si non rapproché → « — »).

## Objectif
```
Entrée : GET /api/rapprochement?debut&fin&… (filtres existants, dont rapprocheBanque)
Projection : SELECT + M.MV_Point, M.MV_PointDate, M.MV_ExtraitNum, M.MV_Echeance, B.BanqueCode
             FROM RT_MOUVEMENT M … LEFT JOIN dbo.vBanque B ON B.No = M.BN_Id AND B.SocieteNo = M.SO_Id
Dérivation (source unique) :
  EstRapprocheBanque = (MV_Point = 1) OR (MV_Type = 0)
  DateRapprochement  = (MV_Type = 0) ? MV_Date : MV_PointDate
Filtre @rapproche : CASE WHEN M.MV_Point = 1 OR M.MV_Type = 0 THEN 1 ELSE 0 END = @rapproche
Sortie : DTO + colonnes front (Point, DateRapprochement, NumeroExtrait, Echeance, BanqueCode) ;
         lecture seule ; COUNT cohérent (jointure vBanque dans le fragment partagé)
```

## Étapes
1. **Entité** `ReglementRapprochementRow` : champs bruts `MvPointDate` (`DateTime?`),
   `MvExtraitNum` (`string?`), `MvEcheance` (`DateTime?`), `BanqueCode` (`string?`) ;
   indicateurs dérivés `DateRapprochement` et `EstRapprocheBanque` (ajout de la branche
   `MvType == 0`). Doc de l'entité mise à jour.
2. **Projection** `DeclarationRepository.GetReglementsRapprochementAsync` : ajouter au `SELECT`
   `M.MV_PointDate AS MvPointDate`, `M.MV_ExtraitNum AS MvExtraitNum`,
   `M.MV_Echeance AS MvEcheance`, `B.BanqueCode AS BanqueCode`.
   **Jointure banque** : ajouter dans le fragment partagé `RapprochementFromWhere`
   (donc appliquée à l'identique liste + `COUNT`) un
   `LEFT JOIN dbo.vBanque B ON B.[No] = M.BN_Id AND B.SocieteNo = M.SO_Id`
   (source confirmée `vReglementsFournisseurs.tlp` l.99 : `vBanque.[No] = Rc.[BN_Id]
   AND vBanque.[SocieteNo] = Rc.[SO_Id]`). `LEFT JOIN` obligatoire → un règlement sans banque
   (ex. espèce/caisse) reste présent, `BanqueCode` nul rendu « — » (aucune ligne masquée).
3. **Filtre** `RapprochementFromWhere` (l.290) : remplacer
   `CASE WHEN M.MV_Point = 1 THEN 1 ELSE 0 END`
   par `CASE WHEN M.MV_Point = 1 OR M.MV_Type = 0 THEN 1 ELSE 0 END`
   (appliqué à l'IDENTIQUE liste + `COUNT`, via la constante partagée → automatique).
4. **DTO** `ReglementRapprochementDto` : `Point` (`bool`, = `MvPoint == 1` brut),
   `DateRapprochement` (`DateTime?`), `NumeroExtrait` (`string`), `Echeance` (`DateTime?`),
   `BanqueCode` (`string`) ; `RapprocheBanque` inchangé de nom (valeur dérivée maj à la source).
5. **Front** `RapprochementInterrogation.tsx` : colonnes « Rapproché (point) », « Date rapproché »,
   « N° extrait », « Échéance », « Code banque » ; dates via `formatDate` (nul → « — ») ;
   marqueur « espèce » sur le rapproché auto (n° extrait vide).

## Livrables
- Endpoint : chaque ligne porte `point`, `dateRapprochement`, `numeroExtrait`, `echeance`,
  `banqueCode` ; `rapprocheBanque = true` pour toute espèce ; filtre `rapprocheBanque` cohérent
  avec l'affichage (liste = `COUNT`) ; le `LEFT JOIN vBanque` ne change pas `TotalCount`.
- Front : cinq colonnes lisibles (point, date rappro, n° extrait, échéance, code banque),
  espèce distinguée sans n° d'extrait fictif, `BanqueCode` nul → « — ».
- Tests : étendre `Task036RapprochementProjectionTests` —
  - espèce (`MV_Type=0`, `MV_Point=0`, `MV_PointDate=NULL`) → `EstRapprocheBanque=true`,
    `DateRapprochement = MvDate`, `NumeroExtrait` vide ;
  - non-espèce rapproché (`MV_Point=1`) → `DateRapprochement = MvPointDate`, n° extrait présent ;
  - non-espèce non rapproché (`MV_Point=0`, `MV_Type≠0`) → `EstRapprocheBanque=false`,
    `DateRapprochement` nulle ;
  - filtre `rapprocheBanque=true` inclut les espèces ; `=false` les exclut (liste = `COUNT`).
- `VERIFY/TASK-042_verify.md` : preuve réelle sur `GR_EMA_DISTRIBUTION` —
  - confirmation nom/type de `MV_ExtraitNum`, `MV_Echeance` sur `RT_MOUVEMENT` (SELECT TOP 1) ;
  - confirmation vue `dbo.vBanque` (colonnes `No`/`BanqueCode`/`SocieteNo`) + non-duplication `COUNT` ;
  - un règlement espèce réel affiché rapproché + `DateRapprochement = MV_Date`, n° extrait vide ;
  - un règlement chèque/virement rapproché avec `MV_PointDate` + `MV_ExtraitNum` + `BanqueCode`
    + `MV_Echeance` réels ;
  - **quantification du gap 595/352** : nombre d'espèces sur les décaissements 2026 et
    réconciliation (l'espèce explique-t-elle l'écart ? oui/non + chiffres) ;
  - contrôle lecture seule : aucun `UPDATE` de `MV_Point`/`MV_PointDate`, `DT_Id` inchangé.

## Critères de validation
- `DateRapprochement` et `NumeroExtrait` exposés et corrects par mode.
- Espèce **toujours** rapprochée à l'affichage **et** au filtre (aucune divergence liste/`COUNT`).
- Aucune écriture en base (la règle espèce est une dérivation, jamais un `UPDATE`).
- Source unique de dérivation (`ReglementRapprochementRow`) répliquée à l'identique par le SQL du
  filtre — pas de logique dupliquée qui puisse diverger.
- Transparence : n° extrait vide pour l'espèce (jamais de valeur fictive), rapproché auto distingué.

## Risques / dépendances
- **Séquencement (édition concurrente)** : touche `RapprochementFromWhere`, la projection, le DTO et
  le front — **mêmes fichiers que TASK-039** (filtre `MV_Domaine`) **et TASK-041** (TVA par
  règlement, déjà « séquencer après 039/040 »). **Séquencer après TASK-039**, coordonner avec
  TASK-041 pour éviter des edits concurrents du `SELECT`/DTO.
- **Existence colonne `MV_ExtraitNum` / `MV_Echeance`** : `MV_PointDate` est déjà utilisée en prod
  (TASK-001, `SelectionExpliqueeService`) → sûre. `MV_Echeance` est confirmée par la vue legacy
  `vReglementsFournisseurs` (alias `PieceEcheance`). `MV_ExtraitNum` **existe bien dans
  `RT_MOUVEMENT`** (confirmé PO 12/07/2026 ; absente de la vue, mais présente en table) →
  vérifier son **nom/type exact** au 1er point du VERIFY avant câblage.
- **Jointure `dbo.vBanque` (BanqueCode)** : `BanqueCode` n'est **pas** dans `RT_MOUVEMENT` ; il vient
  de la vue `dbo.vBanque` via `BN_Id`/`SO_Id` (cf. `vReglementsFournisseurs.tlp` l.99). Vérifier au
  VERIFY : (a) la vue `dbo.vBanque` existe sur `GR_EMA_DISTRIBUTION` et expose bien `No`,
  `BanqueCode`, `SocieteNo` ; (b) `RT_MOUVEMENT.BN_Id` est renseigné pour les règlements bancaires ;
  (c) le `LEFT JOIN` **ne modifie pas** le `TotalCount` (pas de duplication de lignes : `vBanque.No`
  unique par société). Si duplication possible → agréger ou `OUTER APPLY TOP 1`.
- **Gap 595/352** : problème de **comptage** distinct ; si le VERIFY montre que l'espèce ne
  l'explique pas, **ne pas** bricoler ici → ouvrir une TASK de diagnostic dédiée.
- **Cohérence sémantique** : « rapproché » recouvre désormais deux réalités (pointage bancaire vs
  espèce auto). Le marqueur front doit l'expliciter pour ne pas induire le comptable en erreur.
