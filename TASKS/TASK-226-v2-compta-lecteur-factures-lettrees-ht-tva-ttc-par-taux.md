# TASK-226 — V2 compta : lecteur de factures lettrées (achats ET ventes) → HT / TVA / TTC par taux

Status: 🆕 à faire
Priority: HIGH
Risk: HIGH (cœur du calcul déclaré)
Module: Declaration.Compta.Reader + Declaration.Compta.Contracts + Declaration.Compta.Core
Dépend de : TASK-225

> **Origine :** PO 16/10/2026. Modèle d'écriture vérifié en spike (CADRAGE §2) : achat = tiers au crédit TTC, charges au débit HT, TVA au débit ; vente = miroir.

## Décisions de conception

1. **Un moteur, deux sens** : paramètre `SensDeclaration` {Decaissement, Encaissement} ; journaux `F_JOURNAUX.JO_Type` 0 (achats) / 1 (ventes) ; `F_TAXE.TA_Sens` 0 / 1 ; tiers `F_COMPTET.CT_Type` 1 / 0. Aucun code dupliqué.
2. **Taux = `F_TAXE.TA_Taux`, détecté par le compte général de la ligne de TVA (`F_TAXE.CG_Num`)** — arbitrage PO 16/10/2026 — ce qui couvre les ventes et frais bancaires sans `TA_Code`. Le `TA_Code` de la ligne de charge/produit, quand il existe, sert de **départage** si plusieurs codes partagent le même compte (NEW_EMA : `D07`/`D18` sur 6111400000) ; sans départage possible → alerte « taux ambigu », jamais de choix silencieux.
3. HT par taux = Σ lignes de charge/produit de même `TA_Code` ; TVA constatée = Σ lignes sur `F_TAXE.CG_Num` ; TTC = montant de la ligne tiers.
4. **Facture « lettrée »** = ligne tiers avec `EC_Lettre=1`. Le lettrage est équilibré ; un lettrage **déséquilibré** (Σ débits ≠ Σ crédits sur le couple `CT_Num` + `EC_Lettrage`) est une **anomalie bloquante**, jamais corrigée en silence.
5. **Identité** = `cbMarq` de chaque ligne ; regroupement d'une pièce = (`JO_Num`, `JM_Date`, `EC_Piece`) — le test de non-collision sur les 2 bases fait partie des critères.
6. Tiers reconnus par `F_COMPTET.CT_Type` + compte collectif réel de la ligne, **pas par préfixe de numéro** (LOTREX n'a pas de tiers `F%`).
7. Anomalies typées (non exhaustif) : `TA_Code` absent de `F_TAXE` (1 cas LOTREX), `TA_TTaux≠0` (taxe non exprimée en pourcentage : ex. `DOUAN` de HM_PHARMA_PROD avec `TA_Taux`=508 822, à exclure du calcul en %, **jamais** lue comme un taux ; `TA_Type` n'est pas un critère de rejet, PO 16/10/2026), pièce sans ligne de TVA, TVA sur un compte ≠ `F_TAXE.CG_Num`, pièce sans ligne de charge, déséquilibre de pièce (Σ débits ≠ Σ crédits).
8. **Avoirs (pièces de sens opposé) : hors périmètre (PO 16/10/2026, « après »)**. Ils sont détectés, identifiés comme tels et exposés en anomalie « avoir non traité » ; jamais intégrés comme des factures.

## Périmètre STRICT

- **Inclus** : requêtes SQL lecture seule paramétrées (Dapper, jamais de concaténation de valeur), DTO `FactureComptable` (identité, tiers, date, TTC, lignes par taux {TA_Code, taux, HT, TVA constatée, comptes}, anomalies), lecteur par dossier + période, tests sur fixtures **et** tests d'intégration sur LOTREX/NEW_EMA (lecture seule, marqués catégorie « base réelle »).
- **Exclu** : règlements, rapprochement, prorata (TASK-227) ; contrôle HT × taux (TASK-228) ; comptes de taxe sans tiers (TASK-229) ; persistance de lignes déclarées.

## Livrables

- Lecteur + DTO + anomalies typées + tests. Rapport de spike mis à jour dans `DOCS/` si les bases révèlent un cas non prévu.

## Critères de validation

- NEW_EMA : l'écriture d'achat exemple (HT 290,80 / D20 / TVA 58,16 / TTC 348,96) est lue exactement.
- Total par taux d'un mois entier = rejeu SQL indépendant (écart 0,00), sur NEW_EMA (achats) **et** LOTREX (achats + ventes).
- Pièces multi-taux : NEW_EMA (335 achats, 1 296 ventes) lues sans perte ni doublon (Σ HT + Σ TVA = Σ TTC, sinon anomalie listée).
- Aucune pièce comptée deux fois ; aucune collision de clé de pièce (requête de preuve jointe).
- Aucune écriture SQL émise vers Sage. Build 0 erreur ; preuve par critère datée dans le VERIFY.

## Files

- `Declaration.Compta.Reader/`, `Declaration.Compta.Contracts/`, `Declaration.Compta.Core/`, tests associés.
- `DOCS/CADRAGE_V2_COMPTA_SOURCE_UNIQUE.md`.

## Compléments issus de l'analyse TASK-235 (rejeux de l'architecte du 16/10/2026)

1. **La clé (`JO_Num`, `JM_Date`, `EC_Piece`) n'est pas une identité de pièce fiable sur LOTREX** : 74 groupes d'achats et 279 groupes de ventes y portent plusieurs tiers (numéro `1` ou vide saisi), 145 groupes ont un `EC_Piece` vide. NEW_EMA et HM_PHARMA_PROD : 0 collision sur les factures. Règle : si le groupe contient **plus d'un tiers**, la décomposition se fait par (`clé` + `CT_Num`) et la pièce est signalée « pièce ambiguë » (alerte, jamais décomposée en silence) ; `EC_Piece` vide → alerte.
2. **Détection du taux par le compte général** (PO 16/10/2026, `F_TAXE.CG_Num` → `TA_Taux`) : le `TA_Code` de la ligne de charge sert de départage quand plusieurs codes partagent le compte (cas `D07`/`D18` en NEW_EMA). Indispensable pour les ventes et frais bancaires sans `TA_Code`.
3. **Colonnes IF/ICE variables** : standard `CT_Identifiant`/`CT_Siret` sur HM_PHARMA_PROD, colonne `ICE` personnalisée sur NEW_EMA et LOTREX → confirme le paramétrage par dossier (TASK-225).
