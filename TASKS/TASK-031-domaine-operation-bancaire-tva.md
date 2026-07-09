# TASK-031 — Domaine manquant : Opération bancaire (frais bancaire) avec TVA

> ⏸️ **DIFFÉRÉ (backlog).** Décision PO 09/07/2026 : **le client ne gère pas ce cas aujourd'hui** dans ses déclarations. À traiter plus tard, hors chemin critique. Aucune dépendance ne doit être bloquée par cette tâche.

## Contexte
L'ancien module GRFN déclare **trois** sources de déduction, pas deux : décaissement, dépense **et frais/commissions bancaires avec TVA** via `DeclarationTvaController.GetDeclarationCommissionBancaire` (`scratch/decompiled/UIDeclarationTva/Tresorerie.UIDeclarationTva.Stuctures/DeclarationTvaController.cs:917`). Ces lignes portent le mode de paiement `<mp><id>=3` (Opération bancaire) dans le relevé de déductions Simpl-TVA.

Constat (analyse d'écart 09/07/2026) : **aucune trace** de ce domaine dans notre code. `Declaration.Selection` ne couvre que Fournisseur / Espèce / Dépense / Client ; `SourceAffectation` n'a pas de valeur Opération/Frais bancaire ; le workflow et les exports l'ignorent. C'est le seul domaine de déduction **entièrement absent**.

## Périmètre STRICT
- **Inclus** : sélection des opérations bancaires avec TVA de la période, ventilation/valorisation de leur TVA, intégration au modèle de déclaration, mapping XML `<mp><id>=3`, restitution front (poste 4 interrogations) et exports (Excel/XML) au même titre que les autres sources.
- **Exclu** : opérations bancaires **sans** TVA ; toute modification de GRFN/base (lecture seule).

## Référence legacy (comportement à reproduire, corrigé)
`GetDeclarationCommissionBancaire` (lignes ~917-1000) :
- Source : `previsionnelManager.GetAllOperationBancaireToDeclaration(exercice.Debut, declaration.DateFin)`.
- Type d'opération → taxe ERP (`typeOperation.ErpTaxeNo`) ; ignore si pas de taxe ou taxe non « à taux ».
- `AssietteDeclaration = operation.Montant`, `MontantDeclaration = operation.MontantTva * (Encaissement ? +1 : -1)`.
- Domaine = Encaissement ou Décaissement selon `typeOperation.Sens`. `EntityType = OperationBancaire`.
- **Pas de prorata facture** (calcul direct, comme la dépense).

## Objectif
```
Entrée : société + période
Traitement : sélectionner les opérations bancaires avec TVA (RT_MOUVEMENT MV_Domaine=OperationBancaire ou table prévisionnelle équivalente), valoriser assiette/TVA, sens selon type d'opération
Sortie : lignes de déclaration source=FraisBancaire, intégrées comme les autres sources, mode paiement Simpl-TVA=3
```

## Étapes
1. Cartographier en base la source réelle des opérations bancaires (table `RT_MOUVEMENT` / prévisionnel) + le lien type d'opération → code taxe → taux (2 connexions comme TASK-022).
2. Ajouter la valeur `SourceAffectation.FraisBancaire` (ou `OperationBancaire`) et le mapping `MapperModePaiementSimplTVA` = 3.
3. Requête de sélection (miroir des autres domaines : garde-fous universels, `DT_Id IS NULL`, date de période, sens).
4. Valorisation **directe** (assiette = montant, TVA = montant TVA), sans prorata, avec arrondi `AwayFromZero`.
5. Brancher dans le workflow (candidates, checkup, clôture) + exports Excel/XML + poste 4 interrogations.
6. Aucune ligne silencieuse : cas exclus → motif explicite (pas de TVA, taxe non à taux, non rapproché).

## Livrables
- Sélection + valorisation opération bancaire dans `Declaration.Selection` / `Declaration.Core`.
- Tests (sélection hors DB + valorisation) et intégration au pipeline.
- `VERIFY/TASK-031_verify.md` : preuve sur base réelle (ou fixture si le client n'en a pas), montants réconciliés.

## Critères de validation
- Les frais bancaires avec TVA apparaissent dans la déclaration avec `<mp><id>=3`.
- Aucune opération sans TVA remontée ; tout exclu est causé.
- Lecture seule stricte ; arrondi conforme DGI.

## Risques / dépendances
- ⏸️ **Différé** : le client n'utilise pas ce cas → aucune urgence, valeur à confirmer avant réalisation.
- Localiser la source exacte des opérations bancaires en base prod (peut différer du modèle prévisionnel legacy).
- Cohérence sens Encaissement/Décaissement avec le mapping domaines XML (TASK-014).
