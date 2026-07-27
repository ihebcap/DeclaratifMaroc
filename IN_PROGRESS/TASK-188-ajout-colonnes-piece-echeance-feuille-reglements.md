# TASK-188 — Ajout des colonnes « N° Pièce » (`MV_Piece`) et « Échéance » (`MV_Echeance`) à la feuille « Règlements sélectionnés »

## Contexte
Demande PO (27/07/2026), même session que TASK-186/187 : sur la feuille « Règlements sélectionnés » de
l'export Excel de contrôle (`CreerFeuilleReglementsSelectionnes`, `Exporter.cs`), ajouter deux colonnes
issues de `RT_MOUVEMENT` : `MV_Piece` et `MV_Echeance`.

Diagnostic architecte (lecture code) : les deux champs ne partent pas du même point.
- **`MV_Echeance` est déjà lu** — `ReglementRapprochementRow.MvEcheance`
  (`Declaration.Application/Entities/ReglementRapprochement.cs:29`) est déjà alimenté par
  `DeclarationRepository.GetReglementsRapprochementAsync` (`NULLIF(M.MV_Echeance, '17530101') AS
  MvEcheance`, même neutralisation du sentinel SQL Server 1753 que `MvPointDate`, TASK-042) et déjà
  utilisé côté écran Rapprochement (`RapprochementInterrogation.tsx`, colonne « Échéance »). **Aucune
  requête SQL à modifier** — il manque uniquement le maillon final : `r.MvEcheance` n'est pas repris
  dans `ReglementSelectionneInfo` (`ConstruireModeleControleAsync`,
  `DeclarationWorkflowService.cs:1337-1348`), et `Exporter.cs` n'a pas de colonne pour l'afficher.
- **`MV_Piece` n'est lu nulle part** dans ce chemin : absent du `SELECT` de
  `GetReglementsRapprochementAsync` (`DeclarationRepository.cs`, requête `RapprochementFromWhere`
  associée, lignes ~856-888), absent de `ReglementRapprochementRow`. Ajout `SELECT` neuf nécessaire
  (colonne native `RT_MOUVEMENT.MV_Piece`, nvarchar — vérifier en base réelle si un sentinel
  vide/blanc existe à neutraliser, comme pour les colonnes date, avant de décider si un `NULLIF`
  équivalent est nécessaire).

## Objectif
```
Feuille « Règlements sélectionnés » (export Excel de contrôle) : 2 colonnes supplémentaires
  - « N° Pièce »  = RT_MOUVEMENT.MV_Piece
  - « Échéance »  = RT_MOUVEMENT.MV_Echeance (déjà lu ailleurs dans le pipeline, à relier)
```

## Périmètre STRICT
- **Inclus** :
  1. SQL : ajouter `M.MV_Piece AS MvPiece` au `SELECT` de `GetReglementsRapprochementAsync`
     (`DeclarationRepository.cs`) — même requête que celle qui expose déjà `MvEcheance`/`MvExtraitNum`.
     Vérifier en base réelle la valeur brute de `MV_Piece` sur un échantillon (vide/NULL/renseigné) pour
     décider si un traitement de valeur vide est nécessaire (documenter dans le VERIFY).
  2. Modèle : nouveau champ `MvPiece` (string?) sur `ReglementRapprochementRow`.
  3. `Declaration.Core/Model.cs` : nouveaux champs `Piece` (string) et `Echeance` (DateTime?) sur
     `ReglementSelectionneInfo`.
  4. `DeclarationWorkflowService.ConstruireModeleControleAsync` : mapper `r.MvPiece` → `Piece` et
     `r.MvEcheance` → `Echeance` dans la construction de `ReglementSelectionneInfo` (lignes ~1337-1348).
  5. `Exporter.cs` (`CreerFeuilleReglementsSelectionnes`) : 2 colonnes supplémentaires, « N° Pièce »
     (texte) et « Échéance » (date, même format `FormatDateSeule` que les autres colonnes date de cette
     feuille — cohérence TASK-180), position à la suite des colonnes existantes sauf préférence PO
     contraire (documenter le choix dans le VERIFY).
- **Exclu** : écran Rapprochement (`RapprochementInterrogation.tsx`, `MvEcheance` déjà exposé, non
  concerné) ; toute autre feuille de l'export (« Factures à déclarer », « Détail TVA », non demandées
  ici) ; export XML.

## Étapes
1. SQL + modèle : ajouter `MvPiece` (nouveau) et relier `MvEcheance` (déjà existant) jusqu'à
   `ReglementSelectionneInfo`.
2. Export : 2 colonnes dans `CreerFeuilleReglementsSelectionnes`.
3. Tests : ajuster les tests qui vérifient le nombre/l'ordre des colonnes ou une égalité stricte de
   `ReglementSelectionneInfo`/`headers` (`Declaration.Export.Excel.Tests/ExporterTests.cs`,
   `Declaration.Orchestration.Tests/Task160ExportControleTests.cs` si applicable).
4. Rejeu réel (lecture seule) sur `GR_EMA_DISTRIBUTION` : régénérer l'export de contrôle d'une
   déclaration existante et vérifier que les 2 colonnes sont peuplées de façon cohérente avec les
   valeurs réelles `RT_MOUVEMENT.MV_Piece`/`MV_Echeance` du règlement correspondant.

## Livrables
- SQL + modèle + export mis à jour, 2 colonnes visibles dans l'Excel généré.
- Preuve réelle (rejeu Excel) sur au moins un règlement réel, comparée directement aux valeurs SQL
  brutes de `RT_MOUVEMENT` pour ce même `MV_Id`.

## Critères de validation
- « N° Pièce » = `RT_MOUVEMENT.MV_Piece` exact, « Échéance » = `RT_MOUVEMENT.MV_Echeance` exact
  (neutralisation sentinel 1753 si applicable, comme les autres colonnes date de cette même requête).
- Aucune régression sur les colonnes existantes de la feuille « Règlements sélectionnés » ni sur les
  autres feuilles de l'export.
- Build back 0 erreur, tests verts.

## Vérification préliminaire (architecte, 27/07/2026)
Échantillon réel `RT_MOUVEMENT` (`GR_EMA_DISTRIBUTION`, `SO_Id=1`, `MV_Domaine=1`, `MV_Point=1`) :
`MV_Piece` est renseigné et exploitable tel quel (`CB AUTO`, `VIREMENT`, `9814065`, ...) — aucun
sentinel vide/blanc observé sur cet échantillon de 5 lignes récentes. À reconfirmer sur un échantillon
plus large pendant l'implémentation, mais ceci suggère qu'aucun traitement `NULLIF`/`TRIM` spécifique
n'est probablement nécessaire (contrairement aux colonnes date, qui portent un sentinel `1753-01-01`
connu, TASK-042).

## Risques / dépendances
- Aucune dépendance technique avec TASK-186/187, mais **fichiers communs** (`Exporter.cs`,
  `DeclarationWorkflowService.cs`) — à traiter dans la même session pour un VERIFY consolidé, en commit
  strictement séparé (une TASK = un commit).
- Sentinel éventuel sur `MV_Piece` (valeur vide/blanche non NULL) non vérifié à la rédaction de cette
  TASK — à trancher par le worker sur données réelles avant de choisir un traitement (`NULLIF`,
  `TRIM`, ou aucun) plutôt que de l'assumer.
