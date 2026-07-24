# VERIFY — TASK-162

Date: 2026-07-24
Agent: Worker (Claude Code)

## Périmètre livré

Colonne « N° Règlement » (`LigneDeclarationEnrichie.NumeroRapprochement`, déjà valorisée end-to-end
depuis TASK-020/155/160 — aucune nouvelle lecture de données) ajoutée dans les **deux** feuilles
identifiées par la TASK, positionnée juste après « N° Facture » comme demandé :

- `CreerFeuilleDetail` (feuille « Détail », export de dépôt légal TASK-010/155).
- `CreerFeuilleFacturesControle` (feuille « Factures à déclarer », export de contrôle TASK-160).

Tous les index de colonnes suivants (Désignation → Source) décalés de +1 dans les deux méthodes,
symétriquement (le code des deux méthodes était dupliqué à l'identique avant ce lot, cf.
`Exporter.cs:178-182` d'origine — décalage appliqué identiquement des deux côtés).
`ConstruireModeleExportAsync`/`ConstruireModeleControleAsync` (`DeclarationWorkflowService.cs`) :
**non touchés**, conformément au périmètre STRICT — seule l'écriture Excel change.

## Fichiers modifiés

- `Declaration.Export.Excel/Exporter.cs` — `CreerFeuilleDetail` et `CreerFeuilleFacturesControle` :
  en-tête + cellule « N° Règlement » insérés en 2ᵉ colonne, index suivants décalés de +1.
- `Declaration.Export.Excel.Tests/ExporterTests.cs` — fixtures (`GetFixture`/`GetFixtureControle`)
  enrichies d'un `NumeroRapprochement` (dont un cas vide, pour couvrir le règlement absent) ;
  assertions par index de colonne réécrites pour le nouveau décalage.
- `VERIFY/TASK-006_verify.md` — régénéré par effet de bord du test existant `DumpJSON_Verify`
  (`Declaration.Core.Tests`, réécrit le dump JSON à chaque exécution) : reflète simplement le champ
  `Collecte` ajouté par TASK-180 (déjà commitée séparément), aucun contenu de ce lot n'y figure.
  Inclus ici uniquement parce que la suite de tests a été rejouée pendant cette session — pas une
  modification volontaire du contenu.

`Declaration.Orchestration.Tests/Task160ExportControleTests.cs` : **non modifié** — vérifié qu'aucun
test de ce fichier n'indexe une colonne du classeur Excel généré (seul `workbook.Worksheets.Count`
est vérifié), donc aucun impact du décalage de colonnes.

## Preuve réelle (base `GR_EMA_DISTRIBUTION`, déclaration `TVA1-2026-05`)

Export de contrôle régénéré en conditions réelles (même harness que TASK-180, 735 lignes réelles) —
fichier joint : `VERIFY/TASK-162-exemple-export-controle.xlsx`.

- Feuille « Factures à déclarer » : en-têtes `N° Facture | N° Règlement | ...` confirmés en colonnes
  1/2 ; ligne réelle `FF260057 | RF26060115` (numéro de règlement bien peuplé, pas de cellule
  vide sur un cas où le règlement existe).
- Format date-seule (TASK-180) toujours correct après le décalage de colonnes : `Date Paiement`
  (col 14) et `Date Facture` (col 15) affichent bien `"dd/mm/yyyy"` et une valeur formatée
  `"24/06/2026"` sans heure — confirme que le décalage d'index n'a pas cassé le format appliqué par
  TASK-180 sur ces mêmes cellules.
- Cas « règlement absent » (`NumeroRapprochement` vide) : **non rencontré sur cette déclaration
  réelle** (les 735 lignes de `TVA1-2026-05` ont toutes un règlement rattaché) — couvert
  exclusivement par le test unitaire synthétique (`F-002`, `NumeroRapprochement = ""`), qui
  confirme l'absence d'exception et une cellule vide.

## Build

- Status : OK — `dotnet build DeclarationTVA.slnx` → **0 erreur** (solution complète). Même réserve
  qu'en TASK-180 concernant le process `Declaration.API.exe` déjà en cours (build rejoué avec
  `-p:BaseOutputPath=...` pour contourner le verrou de fichier — voir `VERIFY/TASK-180_verify.md`
  § Build pour le détail, non répété ici).

## Tests

- `Declaration.Export.Excel.Tests` : **3/3** (fixtures/assertions adaptées au décalage de colonnes).
- `Declaration.Orchestration.Tests` : **182/182** (non affecté, aucune assertion par index de
  colonne Excel dans ce projet).
- `Declaration.Core.Tests` : non concerné par ce lot (aucun fichier de ce périmètre n'y touche),
  rejoué par ricochet dans la même session (52/52, cf. TASK-180) — pas rerejoué spécifiquement ici.

## Validation checklist

- [x] Colonne « N° Règlement » présente et correctement peuplée dans les deux feuilles concernées
      (`Détail` export dépôt, `Factures à déclarer` export contrôle) — vérifié en test unitaire et
      en conditions réelles (`Factures à déclarer`, `Détail` non exercé par l'export de contrôle
      donc non vérifié en réel sur cette feuille précise, seulement en test unitaire).
- [x] Aucune régression sur les autres colonnes/feuilles (Récap, Détail TVA, Règlements
      sélectionnés) — build + `Declaration.Export.Excel.Tests`/`Declaration.Orchestration.Tests`
      rejoués verts, aucune assertion cassée par le décalage hors de celles corrigées.
- [x] Aucun changement de comportement de `ConstruireModeleExportAsync`/
      `ConstruireModeleControleAsync` — fichiers non touchés (vérifié par `git diff`, aucune
      modification hors `Exporter.cs`/tests).
- [x] Point d'attention signalé par la TASK (décalage d'index cassant un test qui indexerait en
      dur) : vérifié — seuls `ExporterTests.cs` indexait des colonnes par position ; corrigé.
      `Task160ExportControleTests.cs` ne le fait pas (confirmé par lecture complète du fichier).

## Reste à valider

1. **Feuille « Détail » (export de dépôt) non exercée en conditions réelles** dans cette session —
   `ConstruireModeleExportAsync` nécessite une déclaration `Clôturée` (TASK-155), et aucune
   déclaration réelle en base n'est dans cet état au moment de ce VERIFY (même constat que TASK-155
   à sa livraison). Seul le test unitaire (`ExporterExcel_DevraitGenererFichierConforme`) couvre
   cette feuille pour ce lot — le harness de preuve réelle n'a exercé que l'export de contrôle
   (`Factures à déclarer`).
2. **Rendu visuel Excel** : même réserve que TASK-180 — vérification programmatique uniquement
   (ClosedXML), pas d'ouverture visuelle dans Microsoft Excel par ce worker.

## Notes worker

- Commit strictement séparé de TASK-180 (fichier partagé `Exporter.cs`, sections disjointes comme
  anticipé par la TASK — aucun conflit rencontré, TASK-180 déjà committée avant de commencer ce
  lot).
- Traité après TASK-180 (VERIFY propre obtenu au préalable), conformément à la consigne du prompt
  worker.
