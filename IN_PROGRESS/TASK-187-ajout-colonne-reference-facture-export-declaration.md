# TASK-187 — Ajout de la colonne « Référence » (`RT_ECHEANCE.DO_Reference`) à la Déclaration (grilles + export Excel)

## Contexte
Demande PO (27/07/2026), formulée pendant le test comptable qui a aussi révélé TASK-186 (date facture
erronée) : afficher la colonne `DO_Ref` (= `RT_ECHEANCE.DO_Reference`) dans l'export Excel de la
Déclaration. Ce champ existe déjà et est exploité ailleurs dans l'application (écran Factures, TASK-041 :
`FacturesController`/`DeclarationRepository.GetFacturesInterrogationAsync`, colonne `DoReference`) mais
n'est lu par **aucune** des requêtes de sélection de la Déclaration (`SelectionExpliqueeService.cs`) —
absent de `AffectationCandidateRow`, `AffectationCandidate`/`AffectationADeclarer`,
`LigneDeclarationEnrichie` et de l'export (`Exporter.cs`).

## Objectif
```
Ajouter la référence de facture (RT_ECHEANCE.DO_Reference) de bout en bout de la chaîne Déclaration :
  SQL (4 requêtes : 3 règlement-first + 1 facture-first) → AffectationCandidateRow → évaluateur
  → AffectationCandidate/AffectationADeclarer → ConstructeurDeclaration → LigneDeclarationEnrichie
  → Exporter.cs (nouvelle colonne, feuille « Factures à déclarer »)
```

## Périmètre STRICT
- **Inclus** :
  1. SQL : ajouter `E.DO_Reference AS Reference` aux 4 requêtes de `SelectionExpliqueeService.cs`
     (`GetSurensembleFournisseurSql`, `GetSurensembleDepenseSql`, `GetSurensembleClientSql`,
     `GetFactureFirstSql`) — `RT_ECHEANCE` (alias `E`) est déjà jointe dans les 4, aucun JOIN
     supplémentaire nécessaire.
  2. Modèle : nouveau champ `Reference` (string, nullable) sur `AffectationCandidateRow`
     (`SelectionExpliqueeModels.cs`), `AffectationCandidate`/`AffectationADeclarer` (`Model.cs`),
     `LigneDeclarationEnrichie` (`Model.cs`) — propagé par `SelectionExpliqueeEvaluator` et
     `ConstructeurDeclaration.cs`, même patron que `NumeroFacture`/`DateFacture` déjà en place.
  3. Export Excel (`Exporter.cs`, `CreerFeuilleFacturesControle`) : nouvelle colonne « Référence »,
     positionnée après « N° Facture » (cohérent avec l'ordre déjà choisi pour « N° Règlement »,
     TASK-162) — décision d'emplacement exact à confirmer en revue si le PO a une préférence différente.
- **Exclu** : toute logique de sélection/éligibilité (champ d'affichage pur, comme `NumeroFacture`) ;
  export XML Simpl-TVA (le schéma DGI ne prévoit pas ce champ, non demandé) ; écran Factures (TASK-041,
  déjà pourvu de ce champ, non concerné).

## Étapes
1. SQL : ajouter la colonne aux 4 requêtes.
2. Modèle : propager le champ de bout en bout (4 classes citées ci-dessus).
3. Export : nouvelle colonne dans `CreerFeuilleFacturesControle` (feuille « Factures à déclarer »
   uniquement — vérifier si la feuille « Détail TVA », qui partage une partie du mapping, doit aussi
   l'exposer ; à trancher si ambigu plutôt que dupliquer sans certitude).
4. Tests : fixture avec `DO_Reference` renseignée/vide (champ nullable en base, cf. `FC2501193` où
   `DO_Reference` est vide dans l'échantillon vérifié) — vérifier l'absence de crash sur valeur NULL/vide.

## Livrables
- SQL + modèle + export mis à jour, colonne visible dans l'Excel généré.
- Tests couvrant le cas référence renseignée et le cas référence vide/NULL.

## Critères de validation
- Colonne « Référence » présente et correcte dans l'export Excel « Factures à déclarer ».
- Aucune régression sur les colonnes existantes de cette feuille ni sur les autres feuilles.
- Build back 0 erreur, tests verts.

## Risques / dépendances
- Aucune dépendance technique. **Peut être livrée avec TASK-186** dans la même session (fichiers
  communs : `SelectionExpliqueeService.cs`, `Model.cs`, `Exporter.cs`, `ConstructeurDeclaration.cs`) —
  cohérent de traiter les deux ensemble pour éviter deux revues successives sur les mêmes fichiers.
- Position exacte de la colonne dans l'export non tranchée par le PO à ce stade — proposition par
  défaut (juste après « N° Facture ») documentée ci-dessus, ajustable sans impact structurel.
