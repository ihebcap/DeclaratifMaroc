# TASK-185 — Correction TASK-184 : remplacer la colonne « Domaine Activité » par une ligne Total par bloc

## Contexte
TASK-184 a ajouté une colonne « Domaine Activité » (Achats/Ventes) aux tableaux « Totaux par taux »
— mauvaise interprétation d'une demande PO qui voulait en réalité une simple ligne de total par bloc
(capture d'écran fournie par le PO : ligne « Total Collecté » sous le tableau Collecté, « Total
Deductible » sous le tableau Déductible — même valeurs que « Totaux par code activité »). La colonne
Domaine Activité (et son bucket « Non résolu » qui a semé la confusion) ne correspond à aucune
demande réelle : **à retirer entièrement**.

## Objectif
Dans `Declaration.Export.Excel/Exporter.cs::CreerFeuilleDetailTva` (feuille « Détail TVA »
uniquement) :
1. Retirer la colonne « Domaine Activité » (revenir à 4 colonnes : Taux/Total HT/Total TVA/Total TTC).
2. Ajouter une ligne « Total Collecté » en bas du tableau « Totaux par taux — Collecté » (somme
   HT/TVA/TTC des taux du bloc), et « Total Deductible » en bas du tableau « — Déductible ».

## Périmètre STRICT
- Inclus : `Exporter.cs::EcrireBlocRecapParTaux`/`CreerFeuilleDetailTva` uniquement.
- Exclus : `RecapParTaux.Domaine` (`Model.cs`) et sa résolution dans
  `DeclarationWorkflowService.cs::ConstruireModeleControleAsync` — supprimer aussi (plus consommés
  nulle part une fois la colonne retirée, ne pas laisser du code mort) ; `CreerFeuilleRecap` (export
  dépôt, non concerné) ; « Totaux par code activité » (non touché).

## Étapes
1. Retirer le paramètre `afficherDomaineActivite`/la colonne dans `EcrireBlocRecapParTaux`.
2. Ajouter la ligne Total (somme des `TotalHT`/`TotalTva`/`TotalTtc` du bloc) à la fin de chaque
   tableau, dans `CreerFeuilleDetailTva` uniquement (pas dans `CreerFeuilleRecap`).
3. Supprimer `RecapParTaux.Domaine` et la résolution associée (`ResoudreDomaineActiviteRecap`,
   chargement du référentiel) devenus inutiles.
4. Adapter les tests (`ExporterTests.cs`).
5. Régénérer un `.xlsx` réel montrant le nouveau rendu.

## Livrables
`Exporter.cs`, `Model.cs`, `DeclarationWorkflowService.cs` modifiés + tests + `VERIFY/TASK-185_verify.md`.

## Critères de validation
- Tableaux « Totaux par taux » : 4 colonnes (plus de Domaine Activité), ligne « Total Collecté »/
  « Total Deductible » en bas de chaque bloc, valeur égale à la somme des taux du bloc.
- Feuille « Récap » inchangée. Build + tests verts.

## Risques
Nul — retrait d'un ajout non demandé + ligne de somme simple.
