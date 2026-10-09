# TASK-224 — Jeu de données de simulation (reconstitué depuis la capture PO du 09/10/2026)

> Source : capture du drill « Détail des affectations — TVA1-2026-03-T » (FIVE KEYS MAROC).
> Valeurs relevées sur la capture : les montants des 3 factures scindées sont vérifiés par calcul
> (TTC ÷ 1,2 × 0,2) ; les 9 lignes simples sont cohérentes avec le total de la capture (Σ = 3 373,00)
> mais leurs numéros de facture exacts sont à confirmer sur l'écran. Ce jeu sert de fixture de test
> (Declaration.Core.Tests / Declaration.Orchestration.Tests) — jamais de données écrites en base réelle.

## A. Factures (DocumentTaxesInfo — une seule ligne de taxe à 20 %, `Type = "TaxeTypeTVADebit"`, `Sens = Achat`)

| Facture | Tiers | HT | TVA 20 % | TTC |
|---|---|---|---|---|
| FF260029 | LOGINFO INGENIERIE | 133 739,00 | 26 747,80 | 160 486,80 |
| FF260027 | UNION MAGHREB DES TE | 123 791,00 | 24 758,20 | 148 549,20 |
| FF260028 | SNSS TECHNOLOGIES | 67 012,31 | 13 402,46 | 80 414,77 |
| FF260030 | MAROC TELECOM | 1 033,33 | 206,67 | 1 240,00 |
| FF260031 | MAROC TELECOM | 433,30 | 86,66 | 519,96 |
| FF260026 | MAJAL INVEST | 4 155,00 | 831,00 | 4 986,00 |
| FF260009 | MAROC TELECOM | 433,30 | 86,66 | 519,96 |
| FF260008 | MAROC TELECOM | 1 033,33 | 206,67 | 1 240,00 |
| FF260007 | MAJAL INVEST | 4 155,00 | 831,00 | 4 986,00 |
| FF260005 | MAROC TELECOM | 1 033,33 | 206,67 | 1 240,00 |
| FF260006 | MAROC TELECOM | 433,30 | 86,66 | 519,96 |
| FF260001 | MAJAL INVEST | 4 155,00 | 831,00 | 4 986,00 |

## B. Règlements / affectations (AffectationADeclarer — `Source = Decaissement`, `Tiers` avec ICE 15 car. + IF 8 car.)

### B1. Factures réglées en 2 fois (le cas du bug) — 6 règlements

| Règlement | Facture | Montant affecté | Date de paiement | Prorata attendu | TVA attendue |
|---|---|---|---|---|---|
| RF26100006 | FF260029 | 80 243,40 | 2026-10-xx (mois 10) | 50 % | 13 373,90 |
| RF26100007 | FF260029 | 80 243,40 | 2026-10-xx (mois 10) | 50 % | 13 373,90 |
| RF26070005 | FF260027 | 74 274,60 | 2026-07-xx (mois 07) | 50 % | 12 379,10 |
| RF26090005 | FF260027 | 74 274,60 | 2026-09-xx (mois 09) | 50 % | 12 379,10 |
| RF26090006 | FF260028 | 40 207,38 | 2026-09-xx (mois 09) | 50 % | 6 701,23 |
| RF26090007 | FF260028 | 40 207,39 | 2026-09-xx (mois 09) | 50 % | 6 701,23 |

### B2. Factures réglées en 1 fois — 9 règlements (prorata 100 %)

RF26100004→FF260030 (1 240,00) · RF26100005→FF260031 (519,96) · RF26090004→FF260026 (4 986,00) ·
RF26090002→FF260009 (519,96) · RF26090003→FF260008 (1 240,00) · RF26080003→FF260007 (4 986,00) ·
RF26080001→FF260005 (1 240,00) · RF26080002→FF260006 (519,96) · RF26070001→FF260001 (4 986,00).

## C. Résultats attendus (assertions des tests)

- **15 règlements → 15 lignes** (pas 21) ; **0 groupe** `(facture, règlement, taux)` en double.
- ΣTVA B2 = `3 373,00` ; ΣTVA B1 = `64 908,46` ; **total = `68 281,46`** (et non `133 189,91`).
- Par facture B1 : ΣTVA lignes = TVA de la facture (± 0,01) ; Σ montants affectés = TTC (± 0,01).
- Garde-fou `TVA_FACTURE_SURDECLAREE` : **absent** sur ce jeu ; **présent** si on injecte une 2e copie
  de la ligne RF26100006→FF260029 (ΣTVA FF260029 = 40 121,70 > 26 747,80).

## D. Variantes à simuler en plus (scénarios 1 à 7 de la TASK)

1. **Séquence juillet → août → septembre** : facture du 10/07 (160 486,80) réglée 80 243,40 le 15/08 puis 80 243,40 le 15/09.
2. **Facture multi-taux** (10 % et 20 %) réglée en 2 fois : 2 règlements × 2 taux = 4 lignes.
3. **3 règlements dont 1 hors période** : seule la part dans la période est déclarée.
4. **Rejeu** : appliquer 3× « Resynchroniser » / revalidation / réintégration sur le jeu A+B → comptage de lignes inchangé.
5. **Ordre inverse** : sélectionner d'abord RF26090007 puis RF26090006.
