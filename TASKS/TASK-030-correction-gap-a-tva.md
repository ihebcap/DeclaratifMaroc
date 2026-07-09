# TASK-030 : Correction Gap A partiel (Enrichissement TVA)

## Objectif
Corriger le problème de filtrage de la TVA (Gap A partiel) qui bloque l'enrichissement de la TVA depuis Sage. Le filtre restrictif du type de taxe empêchait l'intégration des lignes FGR et OM, ce qui entraînait un taux de TVA de 0% et un montant TTC de 0,00 MAD.

## Actions
- Modification de `Ventilateur.cs` pour ajuster le filtre `Type` du Ventilateur.
- Modification de `LecteurTvaFgr.cs` pour appliquer la condition `Type="TaxeTypeTVA"`.

## Contexte
Ce correctif a été diagnostiqué initialement lors de la TASK-019 (voir `TASKS/VERIFY/TASK-019_diagnostics.md`), mais le correctif n'était pas correctement tracé. Il est maintenant formalisé dans cette tâche dédiée.
