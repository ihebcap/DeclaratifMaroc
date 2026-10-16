# TASK-227 — V2 compta : règlements lettrés, rapprochement, date d'opération, allocation au prorata (décaissement ET encaissement)

Status: 🆕 à faire
Priority: HIGH
Risk: CRITICAL (détermine la période et le montant de TVA déclarés)
Module: Declaration.Compta.Reader + Declaration.Compta.Core
Dépend de : TASK-226, TASK-234
Arbitrages PO du 16/10/2026 : tous levés — rapprochement banque seule, avoirs plus tard, report des règlements non déclarés, lettrage seul pour les clients, **imputation proportionnelle** des lettrages groupés.

> **Origine :** PO 16/10/2026 : « si la facture est lettrée avec une écriture de banque, elle doit être rapprochée ; autre type de règlement, pas de rapprochement » ; date = écriture de banque ; prorata, pas de lettrage partiel.

## Décisions de conception

1. **Règlement** = autre(s) ligne(s) du même lettrage (`CT_Num` + `EC_Lettrage`) hors facture. Sa **pièce** = (`JO_Num`, `JM_Date`, `EC_Piece`) ; la **ligne de banque/caisse** de cette pièce (compte de trésorerie, sens opposé au tiers) porte la preuve `EC_TresoPiece`.
2. **Date d'opération = `DATEADD(day, EC_Jour − 1, JM_Date)`** de la pièce de règlement. **Jamais `EC_DateRappro`** (en NEW_EMA elle précède la date d'opération : 28/02/2026 pour une opération du 08/06/2026). Valable pour tous les modes de règlement.
3. **Rapprochement exigé uniquement pour la banque (arbitrage PO 16/10/2026)** : règlement porté par une ligne sur un **compte de trésorerie bancaire** (compte du journal `F_JOURNAUX.CG_Num`, préfixe bancaire configurable par dossier, défaut `514`). Tout autre règlement (caisse, effets, OD) : aucun rapprochement requis. Règlement bancaire avec `EC_TresoPiece` vide → **non déclarable**, listé en « à rapprocher » (182 cas en NEW_EMA), jamais silencieux. **Toute configuration Sage incohérente avec cette règle déclenche une alerte système** (journal à rapprocher sur compte non bancaire, journal bancaire non rapproché, `EC_TresoPiece` posé hors compte de trésorerie du journal) — spécifiée et livrée par **TASK-234**, dont cette TASK consomme le résultat.
4. **Prorata** (une seule opération, arrondi `MidpointRounding.AwayFromZero`, aucun ratio intermédiaire arrondi — bug GRFN §2) :
   - 1 facture / 1 règlement → 100 % à la date du règlement ;
   - 1 facture / n règlements → `TVA_taux × règlement / TTC_facture` à la date de chaque règlement ;
   - n factures / m règlements (lettrage groupé) → **imputation proportionnelle (arbitrage PO 16/10/2026)** : `part(règlement r, facture f) = r × TTC_f / Σ TTC des factures du lettrage`, puis `TVA_taux(f, r) = TVA_taux(f) × part / TTC_f`. Résultat indépendant de l'ordre des règlements. Arrondi : `AwayFromZero` au niveau de chaque part, le reliquat d'arrondi est reporté sur la part la plus grande de la facture pour que Σ parts = TVA facture exactement, et tracé. Exemple de test imposé : A=300, B=700 ; P1=600 (juin), P2=400 (juillet) → P1 : A 180 / B 420 ; P2 : A 120 / B 280. Un lettrage groupé contenant un avoir est exclu et listé (avoirs hors périmètre).
   Invariant testé : Σ parts = TVA facture (reliquat d'arrondi affecté au dernier règlement, tracé).
5. **Avoirs : hors périmètre de cette TASK (PO 16/10/2026, « on va les voir après »)**. Ils sont néanmoins **détectés** (pièce de sens opposé à la facture normale) et listés en alerte « avoir non traité » ; jamais ignorés en silence ni mélangés aux factures. TASK dédiée à ouvrir après validation du reste.
6. **Période et report (confirmé PO)** : un règlement non déclaré **apparaît toujours jusqu'à sa déclaration** : sélection = date d'opération ≤ fin de période **et** non déjà déclaré (lien d'idempotence `LigneDeclareeCompta`, TASK-225), quelle que soit sa période d'origine.
7. Lettrage déséquilibré / règlement sans ligne de trésorerie / date d'opération hors exercice → anomalie bloquante typée.
8. Symétrie : mêmes règles côté encaissement (tiers au crédit, banque au débit). **Test encaissement = LOTREX** (EMA n'a aucun client lettré).

## Périmètre STRICT

- **Inclus** : lecture des règlements, détection de la preuve de rapprochement, calcul de date d'opération, allocation (`AllocateurReglements` pur dans `Declaration.Compta.Core`), liste « lettrées non rapprochées », tests unitaires exhaustifs (1:1, 1:n, n:1, n:m, avoir, arrondi, déséquilibre) et d'intégration lecture seule.
- **Exclu** : contrôle HT × taux (TASK-228), persistance (TASK-230), UI (TASK-230).

## Livrables

- Allocateur pur + lecteur de règlements + liste des non rapprochés + jeu de tests.

## Critères de validation

- NEW_EMA décaissement : 335 règlements rapprochés retenus ; 182 listés « à rapprocher » ; 51 règlements de caisse retenus sans rapprochement.
- LOTREX : décaissement ET encaissement ; chaque règlement a sa date `JM_Date + EC_Jour − 1` (rejeu SQL indépendant).
- Pour 100 % des factures de la période : Σ TVA allouée sur tous les règlements = TVA facture (écart 0,00), prouvé par requête.
- Aucun règlement alloué deux fois ; relancer le calcul donne un résultat identique (déterminisme).
- Build 0 erreur ; tests verts ; preuve par critère datée ; **scénarios de test manuel séparés** (epic CRITICAL) dans `DOCS/`.

## Files

- `Declaration.Compta.Core/AllocateurReglements.cs`, `Declaration.Compta.Reader/`, tests.
- [MODULE_DECLARATION_TVA.md](../MODULE_DECLARATION_TVA.md) §1quater, §2 (bugs d'arrondi).
