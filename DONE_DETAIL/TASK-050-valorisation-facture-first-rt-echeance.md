# TASK-050 — Valorisation facture-first : lire toutes les factures depuis RT_ECHEANCE (abandon du filtre MV_DECAISSE)

> **Origine :** FC2600896 (`EC_Id=24047`) affichée « non valorisé » alors qu'elle est **soldée,
> comptabilisée, affectée, réglée** (chèque `RF26060124`). Cause racine **prouvée en base** : le
> surensemble de sélection part de `RT_MOUVEMENT` et filtre la direction avec
> `(M.MV_Type = @modeEspece OR M.MV_DECAISSE = @decaisseOui)`. Le règlement de FC2600896 a
> `MV_Type=1` (chèque) **et** `MV_DECAISSE=0` → écarté du surensemble → jamais lu OM.
> **Ampleur mesurée (juin 2026, SO_Id=1) : 467 factures fournisseur `EC_Type=0` exclues**
> (465 FC normales, 13 avoirs). Violation de la règle n°1 (ligne silencieuse).

## Décisions PO (2026-07-11)
1. **`MV_DECAISSE` ne concerne QUE les traites/effets → on NE FILTRE PLUS dessus.** La direction
   du mouvement vient de `MV_Domaine` seul (0=encaissement, 1=décaissement fournisseur, 6=dépense).
   *Remarque à traiter plus tard : cas d'usage exact de `MV_DECAISSE` pour les traites — hors périmètre.*
2. **Pivot facture-first.** On lit **toutes** les factures de la période depuis `RT_ECHEANCE`
   indépendamment du statut ; puis, en 2ᵉ temps, on rattache le statut de **règlement**
   (rapprochement `MV_Point`) et de **déclaration** (`DT_Id`).

## Périmètre
### A. Abandon du filtre MV_DECAISSE (correctif direct)
**Aucun test sur `MV_DECAISSE`, quel que soit le mode de règlement** (espèce, chèque, traite,
virement). Retirer **toute** condition `MV_DECAISSE` des **trois** surensembles de
`SelectionExpliqueeService` (`GetSurensembleFournisseurSql` l.116, `GetSurensembleDepenseSql` l.158,
`GetSurensembleClientSql` l.200) — y compris les prédicats `MV_DECAISSE=@decaisseOui` /
`MV_DECAISSE=@decaisseNon` et le combiné `(MV_Type=@modeEspece OR MV_DECAISSE=@decaisseOui)`. La
nature du mouvement reste bornée par `MV_Domaine` seul (fournisseur/dépense/client) + `MV_Domaine IN
(0,1,6)` déjà en place ailleurs (cf. TASK-039). Nettoyer les constantes devenues inutilisées
(`Decaisse_Oui`/`Decaisse_Non`) si plus référencées.

### B. Valorisation depuis RT_ECHEANCE (facture-first)
La valorisation d'affichage (`RafraichirValorisationAsync`) doit lire OM **toutes** les factures
`EC_Type=0` de la période **présentes dans `RT_ECHEANCE`**, qu'elles aient ou non un règlement,
qu'il soit ou non rapproché. Cible = remplir le cache `GRC_VENTILATION_SAGE_CACHE` (TASK-024) pour
l'ensemble des factures de la période → l'écran Factures affiche HT/TVA au lieu de « non valorisé ».

## Garde-fous (non négociables)
1. **La déclaration reste strictement gated sur le rapprochement.** `RecalculerLignesCandidatesAsync`
   (génération des lignes de déclaration) conserve `EstEligible` (règlement **rapproché**,
   `MV_Point=1`, `DT_Id` null). Une facture sans règlement ou non rapprochée n'entre **jamais** dans
   une déclaration (modèle TVA-sur-encaissement Maroc). Miroir de la décision TASK-049.
2. **Cache non déclarable.** Une ligne écrite pour une facture non rapprochée porte `Token_MV_Id NULL`
   et n'est **jamais** servie comme ventilation déclarable (`TryServireDepuisCache` → null sans
   paiement courant, déjà en place).
3. **Transparence (règle n°1).** Toute facture lue est affichée avec sa valeur **et** son statut réel
   (Non déclarable / Partiel / Total). Toute facture réellement illisible OM reste tracée
   `FACTURE_ILLISIBLE_OM` — jamais un 0 muet, jamais une valeur fabriquée.
4. **Lecture seule stricte.** Aucune écriture `RT_*`, `DT_Id` lu jamais écrit (verrou TASK-028 intact).

## Objectif
```
Entrée  : société + période (bornée)
Change  : (A) suppression du prédicat MV_DECAISSE dans les 3 surensembles
          (B) valorisation d'affichage pilotée par RT_ECHEANCE (toutes factures EC_Type=0 période)
Effet   : les 465+ factures fournisseur réglées par chèque/traite/virement réapparaissent et se
          valorisent ; FC2600896 passe de « non valorisé » à HT/TVA réels
Invariant: déclaration inchangée (gated rapprochement), aucune ligne token-NULL déclarée
```

## Point à confirmer avant implémentation
- **Axe de la période côté valorisation.** Facture-first ⇒ borne probable = **date facture**
  (`DO_Date`/`RT_ECHEANCE`) pour l'écran Factures (cohérent TASK-041), alors que la **déclaration**
  reste bornée par le paiement (encaissement). Trancher : une seule lecture OM par période-facture
  suffit (le cache est clé `EC_Id`), la déclaration relit ensuite le cache validé par token.
- **Volume/perf.** Lire toutes les factures période (et non plus les seules réglées-rapprochées)
  augmente les lectures OM au 1er passage, puis cache (session réutilisée TASK-023). Acceptable,
  mais chiffrer le delta (nb factures période vs nb réglées) pour valider.

## Livrables
- Suppression du prédicat `MV_DECAISSE` dans les 3 requêtes de `SelectionExpliqueeService`
  (+ commentaire remarque « MV_DECAISSE = traites uniquement, à revoir »).
- Source de valorisation facture-first (lecture `RT_ECHEANCE` des `EC_Type=0` de la période) alimentant
  `RafraichirValorisationAsync` / le cache TASK-024, statut règlement/déclaration rattaché en 2ᵉ temps.
- Tests : (a) un règlement `MV_Type∈{1,2,3}` + `MV_DECAISSE=0` n'est plus écarté ; (b) une facture
  sans règlement rapproché est valorisée (cache token NULL) mais **non déclarable** ;
  (c) non-régression : aucune facture non rapprochée déclarée.
- `VERIFY/TASK-050_verify.md` : preuve réelle `GR_EMA_DISTRIBUTION` —
  - **FC2600896** passe de « non valorisé » à HT/TVA réels (recoupés OM/en-tête) ;
  - comptage **avant/après** sur juin 2026 : les 465+ factures exclues réapparaissent au cache ;
  - **non-régression déclaration** : comptage des lignes déclarées inchangé (gate rapprochement) ;
  - contrôle lecture seule (aucune écriture `RT_*`).

## Critères de validation
- Aucune facture fournisseur/dépense/client écartée par `MV_DECAISSE`.
- Toutes les factures `EC_Type=0` de la période lues OM (ou tracées `FACTURE_ILLISIBLE_OM`), cache rempli.
- Déclaration : aucune facture non rapprochée déclarée (`EstEligible` inchangé).
- Aucune ligne token-NULL servie comme déclarable.
- Build + tests verts ; lecture seule stricte (Sage OM + cache).

## Dépendances
- S'appuie sur TASK-045 (lecture OM corrigée), TASK-049 (`EstValorisable`, déjà livré) et le cache
  token-NULL (TASK-024).
- Coordonner avec TASK-042/043 (mêmes surensembles / projection) : la suppression du filtre
  `MV_DECAISSE` change le volume de la population espèce/traite qu'ils quantifient.
- Indépendant de TASK-046/047 (rollup HT / TTC OM) et TASK-048 (ICE/IF).
