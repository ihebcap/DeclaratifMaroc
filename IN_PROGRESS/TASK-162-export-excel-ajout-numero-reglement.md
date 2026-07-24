# TASK-162 — Export Excel : ajouter la colonne « N° Règlement » (feuilles Factures/Détail)

## Contexte
Demande PO (23/07/2026) : dans l'export Excel « factures à déclarer », ajouter le numéro de règlement
dans le fichier.

**Constat code (cartographie architecte)** : la donnée existe déjà intégralement dans le modèle et n'est
**jamais perdue** — c'est un pur oubli d'affichage dans l'exporteur, pas une donnée manquante.
- `LigneDeclarationEnrichie.NumeroRapprochement` (`Declaration.Core/Model.cs:52`) porte déjà le numéro de
  règlement (`RT_MOUVEMENT.MV_Numero`, cf. `Declaration.Selection/SelectionExpliqueeService.cs:121/159/
  194/321` — alias SQL `AS NumeroRapprochement`) et est déjà propagé de bout en bout : sélection →
  figeage (`DeclarationWorkflowService.cs`, ~15 occurrences) → persistance (`DM_LGTVA.NumeroRapprochement`,
  `DeclarationRepository.cs:174-189`) → DTO front (`LigneCandidateDto.cs:24/59`, déjà affiché dans les
  grilles interactives, ex. `ReglementsSelection.tsx`/`RapprochementInterrogation.tsx`).
- `Declaration.Export.Excel.Exporter` (`Exporter.cs`) lit `LigneDeclarationEnrichie` dans **deux**
  méthodes et ignore `NumeroRapprochement` dans les deux :
  1. `CreerFeuilleDetail` (export de dépôt légal, feuille « Détail », TASK-010/155) — lignes 29-62.
  2. `CreerFeuilleFacturesControle` (export de contrôle ad-hoc, feuille « Factures à déclarer »,
     TASK-160) — lignes 223-260. **C'est cette feuille que vise la demande PO** (le fichier utilisé en
     cours de traitement, avant clôture, pour vérification/partage).
- Les deux méthodes ont un jeu de colonnes identique (« N° Facture », « Désignation », ... « Source ») —
  même trou dans les deux, par construction (code dupliqué depuis TASK-160, cf. commentaire
  `Exporter.cs:178-182`).

## Décision de périmètre
Ajouter la colonne dans les **deux** feuilles (Détail + Factures à déclarer), pas seulement celle
explicitement citée par le PO : même modèle de données, même trou, coût marginal nul, et laisser
l'export de dépôt légal incohérent avec l'export de contrôle sur une colonne aussi basique créerait une
confusion inutile (un utilisateur comparant les deux fichiers s'attendrait aux mêmes colonnes). Aucune
réouverture du périmètre TASK-010/155/160 : simple ajout d'une colonne à un export déjà existant, aucun
changement de comportement, aucune nouvelle lecture de données.

## Périmètre STRICT
- **Inclus** :
  - `Declaration.Export.Excel/Exporter.cs` : ajouter l'en-tête « N° Règlement » (positionné juste après
    « N° Facture », avant « Désignation » — regroupement logique facture+règlement) et la cellule
    `ligne.NumeroRapprochement` correspondante, dans `CreerFeuilleDetail` **et**
    `CreerFeuilleFacturesControle`. Décalage des index de colonnes suivants (+1 partout après
    l'insertion).
  - Mise à jour des tests existants qui vérifient position/contenu des colonnes par index
    (`Declaration.Export.Excel.Tests/ExporterTests.cs`, `Declaration.Orchestration.Tests/
    Task160ExportControleTests.cs` si ce test vérifie des indices de colonnes du classeur généré).
- **Exclu** :
  - Toute modification de `ConstruireModeleExportAsync`/`ConstruireModeleControleAsync`
    (`DeclarationWorkflowService.cs`) — la donnée est déjà dans le modèle, aucun changement de requête
    ou d'agrégation nécessaire.
  - Toute modification du front (grilles React) — hors périmètre, la demande porte exclusivement sur le
    fichier Excel exporté.
  - La feuille « Règlements sélectionnés » (TASK-160) — porte déjà une colonne « Numéro » (numéro du
    règlement lui-même), sans rapport avec ce manque.

## Étapes
1. `Exporter.cs::CreerFeuilleDetail` : insérer l'en-tête + la cellule « N° Règlement »
   (`ligne.NumeroRapprochement`) en 2ᵉ colonne.
2. `Exporter.cs::CreerFeuilleFacturesControle` : même insertion (code actuellement dupliqué à
   l'identique de `CreerFeuilleDetail`).
3. Mettre à jour les tests d'assertions par index de colonne impactés par le décalage.
4. Régénérer/joindre un exemple `.xlsx` au VERIFY montrant la colonne peuplée sur un cas réel
   (numéro de règlement non vide) et un cas règlement absent (`NumeroRapprochement` vide — ne doit
   jamais lever d'exception, comportement déjà garanti par le `= ""` par défaut du modèle).

## Livrables
- `Declaration.Export.Excel/Exporter.cs` modifié (2 méthodes).
- Tests mis à jour (`ExporterTests.cs` + tout test dépendant de l'index de colonne côté
  `Task160ExportControleTests.cs`).
- `VERIFY/TASK-162_verify.md` avec un `.xlsx` d'exemple (les deux feuilles concernées) + build/tests
  rejoués.

## Critères de validation
- Colonne « N° Règlement » présente et correctement peuplée dans les deux feuilles concernées
  (`Détail` de l'export de dépôt, `Factures à déclarer` de l'export de contrôle).
- Aucune régression sur les autres colonnes/feuilles (Récap, Détail TVA, Règlements sélectionnés) —
  build + suite `Declaration.Export.Excel.Tests`/`Declaration.Orchestration.Tests` (au moins
  `Task155GenerationExportTests`/`Task160ExportControleTests`) rejoués verts.
- Aucun changement de comportement de `ConstruireModeleExportAsync`/`ConstruireModeleControleAsync` —
  uniquement l'écriture Excel modifiée.

## Risques / dépendances
- **Risque quasi nul** : donnée déjà présente et déjà validée end-to-end par TASK-020/155/160 (le champ
  `NumeroRapprochement` est déjà exercé par de nombreux tests existants) — seul le rendu Excel change.
- **Point d'attention VERIFY** : vérifier que le décalage d'index de colonne ne casse pas un test qui
  indexerait une colonne en dur après « N° Facture » sans passer par l'en-tête nommé.
