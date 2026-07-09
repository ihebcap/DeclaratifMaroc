# TASK-032 — Revue « dépense avec TVA » : filtre TVA + méthode de calcul

> ⏸️ **DIFFÉRÉ (backlog).** Décision PO 09/07/2026 : à traiter plus tard, en même temps que TASK-031 (le client ne gère pas ces cas prioritairement aujourd'hui). Hors chemin critique.

## Contexte
La sélection dépense est présente dans notre code (`Declaration.Selection/SelectionnerAffectationsService.cs:171` `GetDepenseSql`, et `SelectionExpliqueeService.cs:122`). Mais l'analyse d'écart vs GRFN (09/07/2026) révèle **deux divergences** avec l'ancien module qui doivent être levées :

1. **Pas de filtre « avec TVA ».** `GetDepenseSql` ne filtre ni `MV_Tva` ni un montant de TVA > 0 (`Declaration.Selection/SelectionnerAffectationsService.cs:190-198`). Le legacy ne déclare **que** les dépenses `WithTva` (`DeclarationTvaController.cs:866-910`, condition comptabilisé + avec TVA). → risque de remonter des dépenses sans TVA (lignes à 0 / bruit dans le relevé).

2. **Méthode de calcul différente.** Chez nous la dépense passe par `RT_AFFECTATION → RT_ECHEANCE` (facture) puis **ventilation + prorata**. Le legacy valorise la dépense caisse **en direct** : `assiette = depense.MontantDeviseSociete`, `tva = depense.MontantTva`, **sans prorata ni facture**. → écart de calcul possible et silencieux si le modèle de données diffère.

## Périmètre STRICT
- **Inclus** : vérifier sur données réelles si notre traitement de la dépense est équivalent au legacy ; ajouter le filtre « avec TVA » si nécessaire ; aligner la méthode de valorisation (direct vs ventilation) sur le comportement correct.
- **Exclu** : autres domaines ; modification GRFN/base.

## Objectif
```
Entrée : dépenses de la période (RT_MOUVEMENT MV_Domaine=Depense) sur base réelle
Traitement : comparer notre sortie (ventilée) au montant TVA direct de la dépense (modèle legacy)
Sortie : décision — (a) ajouter filtre WithTva, (b) confirmer/corriger la méthode de calcul, tracé + testé
```

## Étapes
1. Sur base réelle, lister les dépenses avec et sans TVA ; mesurer combien remontent aujourd'hui à tort (sans TVA).
2. Comparer, pour un échantillon, notre assiette/TVA (ventilée) vs `MontantDeviseSociete`/`MontantTva` de la dépense (legacy direct).
3. Décider : filtre `WithTva` à ajouter au SQL ; méthode directe vs ventilation.
4. Corriger la sélection/valorisation en conséquence ; aucune dépense sans TVA remontée sans motif.
5. Tests de non-régression (sélection + valorisation).

## Livrables
- Correctif ciblé (filtre + méthode) dans `Declaration.Selection` / `Declaration.Core` selon conclusion.
- `VERIFY/TASK-032_verify.md` : inventaire réel (dépenses avec/sans TVA), comparaison de calcul, décision justifiée.

## Critères de validation
- Aucune dépense sans TVA dans la déclaration (ou exclusion causée).
- Assiette/TVA de la dépense réconciliées avec la source Sage/GRF, arrondi DGI.
- Lecture seule stricte.

## Risques / dépendances
- ⏸️ **Différé** : priorité basse, à confirmer avec TASK-031.
- Dépend de la compréhension du modèle `RT_MOUVEMENT`/`RT_ECHEANCE` pour la dépense (affectée à une facture ou non ?).
- Si le modèle réel diffère du legacy, arbitrer avec le PO (transparence : motif explicite plutôt que saut silencieux).
