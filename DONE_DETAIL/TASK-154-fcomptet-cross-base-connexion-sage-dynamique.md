# TASK-154 — `F_COMPTET` lu en cross-base statique sur la connexion GRF au lieu de la connexion Sage résolue par `SO_Id`

Status: 🆕 à faire
Priority: HIGH
Risk: MEDIUM (touche 4 requêtes SQL + signature d'interface + tous les Fakes de test, mais réutilise un pattern déjà approuvé TASK-118/TASK-140)
Module: Declaration.Selection / Declaration.Application / Declaration.Infrastructure / Declaration.Selection.Tests / Declaration.Orchestration.Tests

> **Origine :** signalement PO en session (23/07/2026) — erreur en exploitation :
> `Erreur lors de la lecture facture-first (SO_Id=1) : Nom d'objet 'F_COMPTET' non valide.`
> (`SqlException` 208, levée dans `SelectionExpliqueeService.LireFacturesDepuisPeriodeAsync` →
> `DeclarationWorkflowService.RafraichirValorisationAsync` → `FacturesController.RafraichirValorisation`).
> Diagnostic architecte (lecture de code, aucune écriture) : ce n'est **pas** un problème de
> connexion à Sage qui échouerait — au contraire, le code n'ouvre **jamais** de connexion Sage pour
> cette lecture, alors qu'il devrait.

## Constat (preuve de code — recherche read-only effectuée cette session)

- **`F_COMPTET` est interrogé via la connexion GRF, pas via Sage.**
  `DeclarationWorkflowService.RafraichirValorisationAsync` (l.553/560-561) récupère
  `_connectionFactory.GetGrfConnectionString()` et la passe telle quelle à
  `SelectionExpliqueeService.LireFacturesDepuisPeriodeAsync`, qui exécute son SQL sur cette même
  connexion (`SelectionExpliqueeService.cs:228`). Le SQL fait `LEFT JOIN F_COMPTET T ON T.CT_Num = ...`
  (l.324 et l.327) — `F_COMPTET` est une table **Sage** (comptes tiers), pas une table GRF.
- **Le même défaut existe dans les 3 autres requêtes du même fichier**, toutes appelées avec
  `grfConnectionString` depuis `DeclarationWorkflowService` (l.191-192, 336, 453, 797) :
  `GetSurensembleFournisseurSql` (l.114), `GetSurensembleDepenseSql` (l.156),
  `GetSurensembleClientSql` (l.195) — chacune fait `LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code`.
  Ce n'est pas un bug isolé à la lecture facture-first : **toutes les lectures de
  `SelectionExpliqueeService` dépendent d'un objet `F_COMPTET` qui n'existe pas dans la base GRF.**
- **Aucun script ne crée cet objet.** `grep SYNONYM` sur tous les `.sql` du dépôt : 0 résultat.
  `TODO.md` (section « Test du nouvel environnement client `DESKTOP-5BFKKEP`, 16/07/2026 »)
  documentait déjà ce trou : *« synonyme cross-base `F_COMPTET` (jamais documenté/scripté nulle
  part — dette à combler, cf. `LANCEMENT_DEV.md`) »* — un synonyme avait été créé **à la main** sur
  ce poste pour débloquer un test, jamais reporté dans un script ni dans la doc
  (`grep F_COMPTET LANCEMENT_DEV.md` : 0 résultat, dette toujours ouverte).
- **Un simple synonyme cross-base ne serait de toute façon pas la bonne correction.** Un synonyme
  pointe vers **une seule** base cible. Or TASK-118 a déjà tranché que la base Sage d'une société
  est résolue **dynamiquement par `SO_Id`** (`P_SOCIETE.SO_ErpDb`, Server/User/Password toujours
  ceux de `GrfConnection` — voir `IDbConnectionFactory.GetSageConnectionInfoAsync`,
  `Declaration.Infrastructure/Factories/DbConnectionFactory.cs:56-83`). Un synonyme statique dans la
  base GRF ne peut désigner qu'**une** base Sage : dès qu'une société a un `SO_ErpDb` différent
  (cas multi-Sage déjà couvert ailleurs dans l'app, cf. section « Multi-bases Sage par `SO_Id` »
  de `TODO.md`), les colonnes tiers (`CT_Intitule`, IF, ICE, `CT_APE`) lues via ce synonyme
  seraient **silencieusement fausses** pour ces sociétés — un risque de donnée, pas seulement de
  disponibilité.
- **Un pattern « deux connexions Dapper + jointure applicative » est déjà établi et approuvé dans
  ce module**, pour exactement ce type de cas (données GRF + données résolues séparément) :
  TASK-140 (`TODO.md`, ligne 89-90) — *« résolution applicative batchée (`DT_Id → Numero`) entre les
  deux connexions Dapper distinctes, **jamais de JOIN SQL trois-parties** »*. Même principe déjà en
  usage direct dans `DeclarationWorkflowService` pour résoudre la connexion Sage par `SO_Id` :
  `_connectionFactory.GetSageConnectionInfoAsync(soId)` (l.604, l.933 — verdict `F_DOCREGL` de
  TASK-151).
- **Angle mort du test de non-régression existant** : `IntegrationRegressionTests.cs:12` pointe les
  deux services (`SelectionnerAffectationsService` et `SelectionExpliqueeService`) vers **une seule**
  base (`GR_EMA_DISTRIBUTION`) qui contient déjà les tables GRF et Sage ensemble — dans cet
  environnement de test, `F_COMPTET` existe nativement dans la même base, donc le test ne peut pas
  détecter ce défaut. C'est un environnement où GRF+Sage sont co-localisés ; l'environnement qui a
  levé l'erreur signalée ne l'est pas.

## Objectif

Faire lire `F_COMPTET` sur la **bonne base Sage, résolue par `SO_Id`**, jamais sur la connexion GRF,
dans les 4 requêtes de `SelectionExpliqueeService` — sans réintroduire un synonyme statique.

1. `SelectionExpliqueeService.SelectionnerExpliqueeAsync` et `LireFacturesDepuisPeriodeAsync`
   doivent recevoir, en plus de la connexion GRF actuelle, une **chaîne de connexion Sage déjà
   résolue** (le service `Declaration.Selection` ne référence pas `Declaration.Infrastructure` — ne
   pas y ajouter cette dépendance ; c'est à l'appelant, `DeclarationWorkflowService`, qui a déjà
   `_connectionFactory.GetSageConnectionInfoAsync(soId)` en usage (l.604/933), de résoudre la
   connexion Sage et de la transmettre en paramètre, comme il le fait déjà ailleurs).
2. À l'intérieur du service, retirer les 4 `LEFT JOIN F_COMPTET` du SQL. Collecter les `CT_Code`/
   `CT_Num` distincts issus de la lecture GRF, puis faire une lecture **batchée** de `F_COMPTET`
   (`WHERE CT_Num IN @codes`) sur la connexion Sage, et joindre le résultat **en mémoire** — pattern
   déjà utilisé pour `DT_Id → Numero` (TASK-140). Pas de connexion ouverte par tiers individuel (coût).
3. Répercuter le nouveau paramètre dans tous les appels de `DeclarationWorkflowService`
   (l.191-192, 336, 453, 560-561, 797) : résoudre `sageInfo = await
   _connectionFactory.GetSageConnectionInfoAsync(soId)` avant l'appel, comme déjà fait l.604/933.
4. **Mettre à jour `ISelectionExpliqueeService` et TOUTES ses implémentations** (garde-fou tiré de
   l'incident TASK-147 : une signature d'interface changée sans répercuter tous les Fakes casse le
   build avec des `CS0535` non détectés par un `dotnet build` scopé à un seul projet) :
   - `Declaration.Application/Services/FixtureSelectionExpliqueeService.cs`
   - Fakes inline dans `Declaration.Orchestration.Tests/Task080ExclusiviteInterDeclarationTests.cs`,
     `Task100ReglementEctypeInconnuTests.cs`, `Task094DiagnosticDtIdTests.cs`
   - Appel réel dans `Declaration.Selection.Tests/IntegrationRegressionTests.cs:38` (peut continuer à
     passer la même chaîne pour les deux paramètres vu que `GR_EMA_DISTRIBUTION` contient déjà les
     deux jeux de tables — ne pas casser ce test, juste l'adapter à la nouvelle signature).

## Garde-fous

- **Lecture seule stricte** : aucune écriture sur `F_COMPTET` ni sur aucune table Sage. Uniquement
  `SELECT`.
- **Jamais de JOIN SQL trois-parties** (`Database.Schema.Table`) entre connexions — jointure
  applicative uniquement (cohérence avec TASK-140).
- **Jamais de synonyme cross-base statique** réintroduit comme substitut — incompatible avec le
  multi-Sage par `SO_Id` déjà acquis (TASK-118).
- `dotnet build DeclarationTVA.slnx` (solution complète, pas un seul `.csproj`) doit passer à 0
  erreur — TASK-147 a déjà montré qu'un build scopé à `Declaration.API.csproj` masque des `CS0535`
  dans les projets de test.
- Ne pas modifier la logique métier d'évaluation (`SelectionExpliqueeEvaluator`,
  `MapAndEvaluate`) — uniquement l'origine des colonnes `TiersIF`/`TiersICE`/`TiersActivite`/
  `CT_Intitule`.

## Files

- `Declaration.Selection/SelectionExpliqueeService.cs` (4 méthodes SQL : l.86, 128, 167, 289 ; méthodes
  publiques l.12 et l.215).
- `Declaration.Selection/ISelectionExpliqueeService.cs` (signatures des 2 méthodes).
- `Declaration.Application/Services/DeclarationWorkflowService.cs` (tous les appels : l.191-192, 336,
  453, 560-561, 797 ; réutiliser le pattern déjà en place l.602-606 et l.933-935).
- `Declaration.Application/Services/FixtureSelectionExpliqueeService.cs` (Fake à mettre à jour).
- `Declaration.Orchestration.Tests/Task080ExclusiviteInterDeclarationTests.cs`,
  `Task100ReglementEctypeInconnuTests.cs`, `Task094DiagnosticDtIdTests.cs` (Fakes inline).
- `Declaration.Selection.Tests/IntegrationRegressionTests.cs` (appel réel, nouvelle signature).
- `Declaration.Infrastructure/Factories/DbConnectionFactory.cs` /
  `Declaration.Application/Interfaces/IDbConnectionFactory.cs` (référence — `GetSageConnectionInfoAsync`
  existe déjà, ne pas le modifier sauf besoin avéré).

## Validation

- [ ] `dotnet build DeclarationTVA.slnx` → 0 erreur (solution complète).
- [ ] `dotnet test` sur `Declaration.Orchestration.Tests` et `Declaration.Selection.Tests` → tous
      verts (y compris `IntegrationRegressionTests`, si l'environnement de build a accès à
      `GR_EMA_DISTRIBUTION`, sinon documenter que le test est « skip DB indisponible » comme avant).
- [ ] Rejeu réel du cas signalé : `RafraichirValorisation?soId=1` (ou l'action front équivalente,
      bouton « Rafraîchir » écran Factures) sur un environnement où GRF et Sage sont des bases
      **distinctes** (pas `GR_EMA_DISTRIBUTION` en local) → aucune `SqlException 208`, tiers
      (nom/IF/ICE) correctement renseignés dans le résultat.
- [ ] Confirmer par lecture de code qu'aucun `LEFT JOIN F_COMPTET` ni aucun nom trois-parties
      (`[Base].dbo.F_COMPTET`) ne subsiste dans `SelectionExpliqueeService.cs`.
- [ ] Confirmer qu'aucun script `.sql` ne crée de `SYNONYM` pour `F_COMPTET` (pas de retour à
      l'ancienne rustine).
- [ ] Grep `GetSageConnectionInfoAsync` dans `SelectionExpliqueeService.cs` doit renvoyer 0 (le
      service ne doit pas dépendre de `Declaration.Infrastructure` — la résolution reste faite par
      l'appelant).

## Dépendances / risques

- Dépend de `IDbConnectionFactory.GetSageConnectionInfoAsync` (TASK-118), déjà en place — pas de
  nouvelle dépendance à créer.
- Risque principal si mal implémenté : oublier un des 4 sites d'appel dans
  `DeclarationWorkflowService` (191, 336, 453, 560, 797) et laisser une des 4 requêtes sur l'ancienne
  connexion GRF — même symptôme reviendrait de façon incohérente (parfois marche, parfois pas, selon
  le chemin appelé). Vérifier les 5 lignes une par une, pas seulement celle du rapport d'erreur
  initial (facture-first).
- Risque secondaire : régression silencieuse sur `IntegrationRegressionTests` si la nouvelle
  signature n'est pas répercutée avant de lancer les tests (échec de compilation, pas d'exécution
  silencieuse — donc détectable, mais à ne pas laisser passer en `VERIFY`).
- N'inclut pas la création/documentation d'un script de provisionnement si le PO souhaite malgré
  tout un accès Sage direct pour d'autres besoins futurs (hors périmètre — cette task corrige
  uniquement le défaut signalé).
