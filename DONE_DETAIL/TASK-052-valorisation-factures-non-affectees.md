# TASK-052 — Valoriser les factures sans règlement (NonAffecte) : compléter le facture-first de TASK-050

> **Origine :** FC2600867 (`EC_Id=23999`, fournisseur, DO_Date 2026-06-19, 6943.26 TTC) toujours
> affichée « non valorisé ». Cause racine **prouvée en base** : la facture n'a **aucune ligne**
> `RT_AFFECTATION` (aucun règlement) → le pivot facture-first la lit bien (LEFT JOIN) mais
> l'évaluateur la classe `NonAffecte` ([SelectionExpliqueeEvaluator.cs:66](../Declaration.Selection/SelectionExpliqueeEvaluator.cs#L66)),
> et la valorisation ne met en cache que `EstValorisable = Eligible ∪ NonRapproche`
> ([SelectionExpliqueeModels.cs:35](../Declaration.Selection/SelectionExpliqueeModels.cs#L35)).
> **NonAffecte est exclu du cache → jamais de HT/TVA.**
>
> Ceci contredit le **Périmètre B de TASK-050** qui exigeait de valoriser « **toutes** les factures
> `EC_Type=0` de la période, **qu'elles aient ou non un règlement** ». La livraison TASK-050 n'a donc
> pas couvert le cas NonAffecte : **c'est une complétion de TASK-050, pas une nouvelle idée.**

## Décision PO (2026-07-11)
**Valoriser les factures NonAffecte pour l'affichage, en les rendant NON déclarables.** Lire le
détail HT/TVA depuis l'OM de la facture (la lecture OM fonctionne — prouvée par FC2600896), les
mettre en cache avec token NULL, afficher HT/TVA au lieu de « non valorisé ». Respecte la règle n°1
du produit : **aucune ligne silencieuse**.

## Ampleur mesurée (base réelle GR_EMA_DISTRIBUTION, SO_Id=1, littéraux `YYYYMMDD`)
- Juin 2026, `EC_Type=0` : **218 factures** au total.
- Sans affectation (NonAffecte) : **157** → toutes actuellement « non valorisé ».
- Avec affectation : 61 ; présentes en cache : 82.
- ⚠️ Corrige la réserve TASK-050 « juin 2026 = 23 factures » : chiffre **erroné** (piège locale
  serveur `dmy`, littéral `'2026-06-01'` mal parsé). Le code .NET n'est pas concerné (paramètres
  `DateTime` Dapper) — seul le diagnostic SQL manuel l'était.

## Périmètre
### A. Étendre EstValorisable à NonAffecte
Ajouter `MotifRejet.NonAffecte` à `EstValorisable`
([SelectionExpliqueeModels.cs:35](../Declaration.Selection/SelectionExpliqueeModels.cs#L35)) :
`EstValorisable => Eligible || NonRapproche || NonAffecte`. Les factures NonAffecte deviennent
valorisables **pour l'affichage uniquement**.

### B. Cache : token NULL pour NonAffecte
`RafraichirValorisationAsync` doit alors envoyer les factures NonAffecte au cache
`GRC_VENTILATION_SAGE_CACHE` avec `Token_MV_Id = NULL` et `Token_MV_Point = NULL` (aucun règlement à
tokeniser), `Source = OM`. Une facture NonAffecte à token NULL est, comme NonRapproche, **valorisée
mais jamais servie comme déclarable**.

## Garde-fous (non négociables — repris de TASK-050)
1. **La déclaration reste strictement gated sur `EstEligible`.** `RecalculerLignesCandidatesAsync`
   n'est **pas** touché : une facture NonAffecte (ni Eligible, ni token) n'entre **jamais** dans une
   déclaration (TVA-sur-encaissement : pas de règlement = pas d'exigibilité). Invariant TASK-028/050.
2. **Token NULL n'est jamais déclarable.** Vérifier que le chemin déclaration ignore toute ligne
   cache à `Token_MV_Id NULL` (déjà le cas TASK-050 pour NonRapproche — étendre la couverture, pas
   la règle).
3. **Lecture seule Sage.** Aucune écriture `RT_*`. Seul le cache `GRC_VENTILATION_SAGE_CACHE` est
   alimenté.
4. **Pas d'invention de valeur.** Si l'OM d'une facture NonAffecte est illisible → trace
   `FACTURE_ILLISIBLE_OM` (jamais un 0 silencieux), conformément à TASK-045/024.

## Livrables de preuve (VERIFY) — base réelle exigée
1. **FC2600867 (`EC_Id=23999`)** : avant = absente du cache ; après = ligne cache `Source=OM`,
   `BaseHT`/`MontantTva` réels recoupés avec l'en-tête (`TotalTtc = EC_Montant = 6943.26`),
   `Token_MV_Id NULL`. Requête `SELECT * FROM GRC_VENTILATION_SAGE_CACHE WHERE EC_Id=23999`.
2. **Comptage juin 2026** : nombre de NonAffecte passées de « hors cache » à « en cache » (cible : les
   157 factures sans affectation, sous réserve d'OM lisible ; tout écart tracé).
3. **Non-régression déclaration** : `SELECT COUNT(*)` de lignes cache `Token_MV_Id NULL` servies
   comme déclarables = **0** (inchangé vs TASK-050).
4. **Non-régression Eligible/NonRapproche** : le compte de lignes déclarables ne change pas.
5. **Tests** : maintenir la suite verte ; ajouter/étendre un test unitaire évaluateur asserant
   `NonAffecte ⇒ EstValorisable=true ∧ non déclarable`.
6. **Contrôle lecture seule** : aucune écriture `RT_*`.

## Dépendances / risques
- **Dépend de** TASK-050 (facture-first, DONE) et TASK-024 (cache).
- **Risque perf** : 157 lectures OM supplémentaires/mois. Réutiliser la session Sage batch (TASK-023)
  déjà en place ; ne pas ouvrir une session par facture.
- **Risque garde-fou** : le seul point sensible est de ne pas laisser une ligne token-NULL devenir
  déclarable. Couvert par garde-fous 1-2 + livrable de preuve 3.
