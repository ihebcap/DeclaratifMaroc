# TASK-184 — Feuille « Détail TVA » : supprimer le bloc « Contrôle d'équilibre », ajouter le domaine aux « Totaux par taux »

## Contexte
Le bloc « Contrôle d'équilibre » affiche `Total HT − Total Déclaré TTC`, qui vaut toujours
exactement `−ΣTVA` par construction (HT+TVA=TTC) — ça ne détecte jamais rien. Décision PO : le
retirer, et à la place ajouter dans « Totaux par taux — Collecté/Déductible » une colonne Domaine
d'activité (Achats/Ventes), donnée déjà disponible via le référentiel `P_DECTVAACTIVITE`.

Même fichier, même méthode (`CreerFeuilleDetailTva`), même principe (nettoyer/enrichir ce bloc de
totaux) — une seule TASK.

## Objectif
Dans `Declaration.Export.Excel/Exporter.cs::CreerFeuilleDetailTva` :
1. Supprimer les lignes 308-323 (bloc « Contrôle d'équilibre »).
2. Dans les tableaux « Totaux par taux — Collecté »/« — Déductible », ajouter une colonne
   « Domaine Activité » (Achats/Ventes ; « Non résolu » si code activité non résolu).

## Périmètre STRICT
- Inclus : `Exporter.cs::CreerFeuilleDetailTva`/`EcrireBlocRecapParTaux` ; `RecapParTaux`
  (`Declaration.Core/Model.cs`, ajout d'un champ domaine) ; `DeclarationWorkflowService.cs`
  (`ConstruireModeleControleAsync`, résolution du domaine via `GetReferentielCodesActiviteAsync()`
  déjà existant — pas de nouvelle requête SQL).
- Exclus : `CreerFeuilleRecap` (export de dépôt, non touché) ; « Totaux par code activité » (non
  demandé) ; tout recalcul par rapport aux OM Sage réels (hors sujet, pas de décision PO).

## Étapes
1. Supprimer le bloc « Contrôle d'équilibre » (lignes 308-323).
2. Ajouter le champ domaine à `RecapParTaux`.
3. Résoudre le domaine par `CodeActivite` dans `ConstruireModeleControleAsync` (dictionnaire depuis
   `GetReferentielCodesActiviteAsync()`), inclure dans le `GroupBy` des `RecapsParTaux`.
4. Afficher la colonne dans `EcrireBlocRecapParTaux`.
5. Adapter les tests (`ExporterTests.cs`), régénérer un `.xlsx` réel.

## Livrables
`Exporter.cs`, `Model.cs`, `DeclarationWorkflowService.cs` modifiés + tests + `VERIFY/TASK-184_verify.md`.

## Critères de validation
- Plus de bloc « Contrôle d'équilibre ». Colonne Domaine Activité visible sur « Totaux par taux »,
  somme des sous-totaux = total par taux déjà affiché avant cette task.
- Feuille « Récap » inchangée. Build + tests verts.

## Risques
Faible — donnée déjà disponible, pas de nouvelle requête SQL.
