# TASK-160 — VERIFY

## Implémenté en worker exceptionnel

Rôle inversé, demande explicite PO/architecte (23/07/2026, « lance TASK-160 en tant que worker »),
même mode que TASK-101/075/114/117/118/122 — cf. réserve `CLAUDE.md`. Revue architecte (auto-revue,
même session) ci-dessous.

## Périmètre livré

- `Declaration.Application/Services/DeclarationWorkflowService.cs` : nouvelle méthode
  `ConstruireModeleControleAsync(Guid declarationId)` — assemble un `ModeleControle` dédié
  (règlements sélectionnés + lignes `Integree||Proposee` + agrégats taux/activité + contrôle
  d'équilibre), sans réutiliser `ConstruireModeleExportAsync` (qui exige `Clôturée` + `Integree`
  seul). Nouvelle méthode `GenererExcelControleAsync(Guid declarationId)` : glue modèle → exporter
  → `byte[]` en mémoire (aucun fichier disque).
- `Declaration.Core/Model.cs` : nouveaux types `ReglementSelectionneInfo`/`ModeleControle` (à côté
  de `DeclarationModele` existant, non modifié).
- `Declaration.Export.Excel/Exporter.cs` : nouvelle méthode publique
  `ExporterExcelControle(ModeleControle, Stream)` — écrit dans un `Stream` (jamais un chemin
  disque), 3 feuilles (« Règlements sélectionnés », « Factures à déclarer », « Détail TVA »).
  Méthodes privées dédiées, **aucune modification** de `ExporterExcel`/`CreerFeuilleDetail`/
  `CreerFeuilleRecap` existants (périmètre strict de la task : export de dépôt intouché).
- `Declaration.API/Controllers/DeclarationsController.cs` : nouvel endpoint
  `GET {id}/export-controle` — même garde `EstSocieteAutorisee` que les autres endpoints de la
  déclaration, disponible `EnCours` **ou** `Clôturée` (aucun blocage sur le statut), retourne le
  flux via `FileContentResult` (content-type xlsx).
- Front : **livré** (reprise 23/07/2026, cf. section dédiée ci-dessous) — décision PO actée
  entretemps (les deux écrans, cf. `TODO.md`/`TASKS/TASK-160-...md` étape 1).

## Décisions actées en cours de route (non tranchées explicitement par la task)

- **Aucun blocage sur l'identifiant fiscal société** (`P_SOCIETE.SO_Identifiant`), contrairement à
  `ConstruireModeleExportAsync`/TASK-151 : cet export est un contrôle interne ad-hoc, pas l'artefact
  de dépôt légal — `EnTete.IdentifiantSociete` utilise `SocieteId.ToString()` (même choix que
  `GetCheckupAsync`), jamais bloquant.
- **Règlements sélectionnés** : jointure faite en récupérant `GetReglementsRapprochementAsync` sur
  la période de la déclaration (même construction `dateDebut`/`dateFin` que `GetCheckupAsync` :
  `new DateTime(Exercice, Periode, 1)` + fin de mois), puis filtre en mémoire sur les numéros de
  `GetSelectionReglementsAsync` — même pattern que `ReglementsSelection.tsx`/`GetCheckupAsync`
  (filtre applicatif plutôt que le paramètre `Numeros` du `RapprochementFilter`, pour rester
  cohérent avec le seul autre appelant existant de ce filtre sur une sélection).
- **Totaux par code activité** : `LigneCandidate` ne porte aucun champ `CodeActivite` (gap déjà
  connu et accepté par `ConstruireModeleExportAsync`/TASK-155 — jamais un `Designation` non plus).
  La feuille « Détail TVA » expose donc un unique bucket `CodeActivite=""` — pas une régression
  introduite ici, un gap de modèle de données hérité, documenté explicitement plutôt que masqué.

## Tests

- `Declaration.Orchestration.Tests/Task160ExportControleTests.cs` (10 tests, `FakeRepository`
  dédiée implémentant `IDeclarationRepository`) :
  - `ConstruireModeleControleAsync` : déclaration introuvable (`ArgumentException`) ; fonctionne
    sur une déclaration `EnCours` sans erreur (**critère de validation explicite de la task**) ;
    aucun calcul lancé → feuille Factures/Recaps vides sans erreur (**« aucune ligne silencieuse »
    », critère explicite**) ; lignes `Integree`+`Proposee` incluses, `Exclue` rejetée, agrégats
    taux/équilibre cohérents avec les 2 lignes retenues seulement ; jointure règlements sélectionnés
    ↔ rapprochement (règlement non sélectionné absent du résultat) ; libellé d'état de pointage
    explicite rapproché/non rapproché.
  - `GenererExcelControleAsync` : classeur généré valide (3 feuilles, relu via `ClosedXML`).
  - Contrôleur `ExportControle` : déclaration introuvable → 404 ; société non autorisée → 403 ;
    déclaration `EnCours` → `FileContentResult` xlsx non vide.
- `Declaration.Export.Excel.Tests/ExporterTests.cs` (+2 tests) : `ExporterExcelControle` génère
  bien 3 feuilles aux en-têtes/valeurs attendues (relecture `ClosedXML` depuis le `MemoryStream`,
  jamais depuis un fichier disque) ; modèle vide ne lève aucune erreur.
- Exemple concret : `VERIFY/TASK-160-exemple-export-controle.xlsx` (3 feuilles, généré via
  `Exporter.ExporterExcelControle` sur un jeu de données fictif — 2 règlements sélectionnés dont un
  non rapproché, 2 factures, totaux par taux 20 %, contrôle d'équilibre HT/TTC).

## Vérifié indépendamment par l'architecte (même session, auto-revue)

- Lecture directe des 4 fichiers modifiés/ajoutés confirmant chaque point ci-dessus, notamment
  l'absence de toute touche à `ExporterExcel`/`ConstruireModeleExportAsync`/
  `GenererFichiersExportAsync`/`/generation`/`/fichiers/{type}` (grep + lecture différentielle).
- `dotnet build DeclarationTVA.slnx` → 0 erreur (25 avertissements, tous préexistants hors
  périmètre — mêmes qu'avant cette task).
- `dotnet test Declaration.Orchestration.Tests` → 168/168 (dont les 10 nouveaux `Task160*`, les 15
  `Task155*` toujours au vert — aucune régression sur l'export de dépôt existant).
- `dotnet test Declaration.Export.Excel.Tests` → 3/3 (1 existant + 2 nouveaux).
- `dotnet test` solution complète → exactement les 2 échecs préexistants déjà documentés dans
  TASK-154/155/156/159 (`Declaration.Selection.Tests.IntegrationRegressionTests` : échec
  d'authentification Windows sur la connexion GRF locale ; `Declaration.Controle.Tests.
  ComparateurTests.GenererRapportVerification` : donnée de test absente en base) — confirmés
  ci-dessus par la pile d'appel (aucun rapport avec `Declaration.Application`/`Export.Excel`/`API`
  touchés par cette task).

## Réserves non bloquantes, documentées non silencieuses

- **Front non développé** — attendu, cf. étape 1 de la task : emplacement du bouton (① Sélection,
  ② Vérifier & Intégrer, ou les deux) à confirmer par le PO avant tout développement.
- **Totaux par code activité toujours à un seul bucket vide** — gap de modèle de données hérité
  (`LigneCandidate` sans `CodeActivite`), pas un défaut introduit par cette task ; se corrigera de
  lui-même si ce champ est un jour ajouté au modèle (même correction bénéficierait à TASK-155).
- **Aucun test end-to-end HTTP réel** (API démarrée + base réelle) — vérifié par tests d'intégration
  contrôleur (mock repository, fixtures réalistes), même approche que TASK-155 ; pas d'accès
  réseau/SQL Server réel dans cet environnement (confirmé par l'échec préexistant
  `IntegrationRegressionTests` ci-dessus). **Levé pour la partie front** par le test end-to-end réel
  décrit ci-dessous (reprise 23/07/2026).

## Front livré (reprise 23/07/2026) — étapes 5-6 de la task

Décision PO actée entretemps (cf. `TODO.md`) : bouton dupliqué sur **les deux écrans**
(① Sélection **et** ② Vérifier & Intégrer), même endpoint, contenu identique.

- `declaration-tva-web/src/ReglementsSelection.tsx` : bouton « Export de contrôle (Excel) » ajouté
  dans le pied de grille (à côté de « Total sélectionné » / `ColumnSelector`) — `api.get(
  \`/declarations/${declarationId}/export-controle\`, { responseType: 'blob' })`, même pattern de
  téléchargement (`URL.createObjectURL` + `<a download>`) que `DeclarationFinalePanel.tsx`. Aucune
  autre logique de l'écran touchée (sélection, filtres, tri, colonnes).
- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` : même bouton ajouté dans le pied de page,
  regroupé avec le bouton « Confirmer intégration » existant (visible que la déclaration soit encore
  modifiable ou déjà intégrée/lecture seule — cohérent avec le périmètre back : disponible dès que la
  déclaration existe, `EnCours` ou `Clôturée`). Aucune autre logique de l'écran touchée (checkup,
  drill, confirmation d'intégration).
- Aucun fichier back retouché — uniquement les deux composants front listés ci-dessus.

### Tests (front)

- `npm run build` (`tsc -b && vite build`) → **0 erreur** TypeScript, build production généré
  (seul avertissement : `INEFFECTIVE_DYNAMIC_IMPORT` sur `api.ts`, préexistant, sans rapport avec ce
  changement).
- **Vérification manuelle réelle end-to-end** (API réelle + base réelle + front buildé, piloté par
  Playwright) : déclaration `TVA1-2026-06` ouverte, écran ① Sélection atteint — bouton visible à
  côté de « Total sélectionné », clic → téléchargement réel d'un `.xlsx` valide (22 758 octets).
  Écran ② Vérifier & Intégrer atteint — bouton visible à côté de « Confirmer intégration », clic →
  téléchargement réel d'un second `.xlsx` valide (37 967 octets). `curl` direct sur
  `GET /api/declarations/{id}/export-controle` → 200, `Content-Type`
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, corps ZIP/xlsx valide.
- **Non-régression vérifiée** : écran ① — 25 lignes visibles, cases à cocher et compteur de
  sélection fonctionnels (165/172 sélectionnés), boutons « Détail des lignes »/« Passer au calcul »
  intacts. Écran ② — bouton « Confirmer intégration » toujours présent et fonctionnel, inchangé.
- **Écart pré-existant sans rapport, constaté pendant le test** : écran ② affiche un toast rouge
  « Erreur lors du chargement du checkup » sur cette base de dev — `SqlException: Nom d'objet
  'DM_VENTILATION_SAGE_CACHE' non valide` (`DeclarationRepository.GetEcIdsEnErreurAsync`), table
  absente du schéma SQL Server local. Sans rapport avec ce correctif : les deux appels
  `export-controle` eux-mêmes ne touchent jamais cette table et ont réussi (200) malgré ce toast.
  Aucune action requise ici — signalement pour information si le PO veut vérifier ce schéma de dev.
