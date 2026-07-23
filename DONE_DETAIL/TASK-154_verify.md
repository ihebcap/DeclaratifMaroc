# TASK-154 Verify — `F_COMPTET` rattaché via la connexion Sage résolue par `SO_Id`, jamais via GRF

## Périmètre livré

`SelectionExpliqueeService.SelectionnerExpliqueeAsync` et `LireFacturesDepuisPeriodeAsync` reçoivent
désormais un paramètre `sageConnectionString` en plus de la connexion GRF. Les 4 `LEFT JOIN
F_COMPTET` (SQL cross-base statique, jamais valide hors environnement où GRF+Sage sont
co-localisées) ont été retirés. À la place :

1. Lecture GRF normale (inchangée sur le fond, seules les colonnes F_COMPTET sont retirées du
   `SELECT`).
2. Collecte des `TiersNumero` (= `CT_Code`/`CT_Num`) distincts issus des lignes lues.
3. Une seule lecture batchée `SELECT CT_Num, <col IF>, <col ICE>, CT_APE FROM F_COMPTET WHERE
   CT_Num IN @codes` sur la connexion **Sage** (nouvelle méthode privée statique
   `EnrichirTiersDepuisSageAsync`), avec sortie anticipée (aucune connexion ouverte) si la liste de
   codes est vide.
4. Jointure applicative en mémoire (`Dictionary<string, TiersFComptetRow>` par `CT_Num`) qui pose
   `TiersIF`/`TiersICE`/`TiersActivite` sur chaque `AffectationCandidateRow` déjà lue.

Dans `SelectionnerExpliqueeAsync`, les 3 surensembles (fournisseurs/dépenses/encaissements) sont
enrichis en **un seul appel batché** (union des 3 listes) avant `MapAndEvaluate` — une seule
connexion Sage ouverte par appel de méthode, pas une par tiers ni une par surensemble.

`DeclarationWorkflowService` résout `sageInfo = await
_connectionFactory.GetSageConnectionInfoAsync(soId)` juste avant chacun des 5 appels identifiés par
la task (`ConstruireLignesFigeesAsync`, backfill dans `RevaliderLignesFigeesAsync`,
`ReintegrerReglementsLiberesAsync`, `RafraichirValorisationAsync` — le site exact du bug signalé —,
`GetCheckupAsync`), en réutilisant tel quel le pattern déjà en place dans `BuildOrchestrateurAsync`
et `DiagnostiquerLigneAsync`. Aucune nouvelle méthode de résolution introduite.

## Écart documenté par rapport au libellé littéral de la task (assumé, dans le périmètre autorisé)

La task liste `TiersIF`/`TiersICE`/`TiersActivite`/`CT_Intitule` comme colonnes dont l'origine peut
changer. Dans `GetFactureFirstSql` (facture-first), l'ancien SQL utilisait DEUX alias F_COMPTET
distincts (`T` via `RT_MOUVEMENT.CT_Code`, `T2` via `RT_ECHEANCE.CT_Code`) : `TiersIF`/`TiersICE`
venaient uniquement de `T` (donc `NULL` si la facture n'a pas encore de règlement rapproché), alors
que `TiersActivite` (`CT_APE`) faisait `COALESCE(T.CT_APE, T2.CT_APE)`. La jointure applicative
retenue ici utilise une seule clé (`TiersNumero`, déjà `COALESCE(M.CT_Code, E.CT_Code)` dans le
SQL) pour les 3 colonnes — ce qui, pour une facture sans règlement rapproché, peuple maintenant
aussi `TiersIF`/`TiersICE` (auparavant `NULL` dans ce cas précis). C'est un changement de
**l'origine de la donnée** (autorisé explicitement par la task), pas de la logique d'évaluation
(`SelectionExpliqueeEvaluator`/`MapAndEvaluate` non touchés) — mais il mérite d'être signalé
explicitement plutôt que passé sous silence, conformément à la consigne de ne jamais s'écarter en
silence. `CT_Intitule` n'a pas été touché : il vient de `RT_MOUVEMENT.CT_Intitule` /
`RT_ECHEANCE.CT_Intitule` (colonnes GRF, jamais F_COMPTET) dans les 4 requêtes, avant et après ce
correctif.

## Fichiers modifiés

- `Declaration.Selection/ISelectionExpliqueeService.cs` — signatures des 2 méthodes (+
  `sageConnectionString`).
- `Declaration.Selection/SelectionExpliqueeService.cs` — 4 SQL sans `F_COMPTET`, nouvelle méthode
  privée `EnrichirTiersDepuisSageAsync` + DTO interne `TiersFComptetRow`, câblage dans les 2
  méthodes publiques.
- `Declaration.Application/Services/DeclarationWorkflowService.cs` — 5 sites d'appel, résolution
  `GetSageConnectionInfoAsync(soId)` ajoutée avant chacun.
- `Declaration.Application/Services/FixtureSelectionExpliqueeService.cs` — signature mise à jour
  (paramètre ignoré, fixture sans base réelle).
- `Declaration.Orchestration.Tests/Task080ExclusiviteInterDeclarationTests.cs`,
  `Task094DiagnosticDtIdTests.cs`, `Task100ReglementEctypeInconnuTests.cs` — Fakes inline mis à
  jour.
- `Declaration.Selection.Tests/IntegrationRegressionTests.cs` — appel réel adapté (même chaîne pour
  GRF et Sage, `GR_EMA_DISTRIBUTION` contient déjà les deux jeux de tables — commentaire ajouté).

## Build

`dotnet build DeclarationTVA.slnx` (solution complète, tous projets y compris tests) :

```
La génération a réussi.
    11 Avertissement(s)
    0 Erreur(s)
```

Les 11 warnings restants sont tous préexistants (nullabilité `CS8618`/`CS8602`/`CS8604` sur du code
non touché par cette task, `NU1510` sur `Declaration.Setup`, `xUnit1012`, `CS0105` dans
`Declaration.API/Program.cs`) — aucun n'est introduit par ce correctif. Vérifié explicitement avec
la solution complète (`.slnx`), pas un `.csproj` isolé — conformément au garde-fou tiré de
l'incident TASK-147 (un build scopé avait masqué des `CS0535` dans les projets de test lors d'un
changement de signature similaire).

## Tests

`dotnet test Declaration.Orchestration.Tests --no-build` :

```
Réussi! - échec : 0, réussite : 137, ignorée(s) : 0, total : 137, durée : 424 ms
```

`dotnet test Declaration.Selection.Tests --no-build` :

```
Échoué! - échec : 1, réussite : 58, ignorée(s) : 0, total : 59, durée : 162 ms
```

Le seul échec est `IntegrationRegressionTests.TestRegression_NouveauSurensembleIncludAncienTask008_Task050` :

```
Microsoft.Data.SqlClient.SqlException : Échec de l'ouverture de session de l'utilisateur 'IHEB-PC\ihebc'.
  at ... SqlConnection.InternalOpenAsync(...)
  at Declaration.Selection.SelectionExpliqueeService.SelectionnerExpliqueeAsync(...) :line 31
  at Declaration.Selection.Tests.IntegrationRegressionTests.TestRegression_...() :line 40
```

Ce n'est **pas une régression introduite par ce correctif** : l'échec se produit à la ligne 31 du
service, sur le tout premier `connection.OpenAsync()` — celui de la connexion **GRF** elle-même
(inchangée par cette task), avant même d'atteindre le nouveau code de résolution Sage. C'est un
échec d'authentification Windows intégrée de ce poste de build vers `GR_EMA_DISTRIBUTION`, pas une
erreur de code. Le commentaire du test lui-même l'anticipe explicitement (« Let it fail if db is
not available »). L'autre test de la même classe
(`TestIntegration_SelectionnerAffectationsVente_Task084`) « passe » silencieusement dans ce même
environnement uniquement parce qu'il appelle l'ANCIEN service (`SelectionnerAffectationsService`),
qui avale les exceptions et retourne une liste vide (`catch (Exception) { return result; }`,
`SelectionnerAffectationsService.cs:83-89`) — pas parce que la connexion a réussi. Vérifié en
isolant les 2 tests (`--filter FullyQualifiedName~IntegrationRegressionTests`) : 1 échec / 1 réussite
sur les 2, confirmant que le comportement est bien celui décrit ci-dessus et pas un flake.

Conformément à la checklist de la task (« si l'environnement de build a accès à
`GR_EMA_DISTRIBUTION`, sinon documenter que le test est « skip DB indisponible » comme avant ») :
**cet environnement n'a pas accès à `GR_EMA_DISTRIBUTION`** (login Windows refusé) — documenté ici
explicitement, pas de vérification inventée.

## Checklist de validation TASK-154

- [x] `dotnet build DeclarationTVA.slnx` → 0 erreur (solution complète) — voir § Build.
- [~] `dotnet test` sur `Declaration.Orchestration.Tests` (137/137 verts) et
      `Declaration.Selection.Tests` (58/59 verts) — le seul rouge est
      `IntegrationRegressionTests`, bloqué par l'absence d'accès réseau/auth à
      `GR_EMA_DISTRIBUTION` sur ce poste de build, pas par le correctif (voir § Tests). Ce point de
      la checklist ne peut pas être coché à 100 % dans cet environnement — documenté, pas simulé.
- [ ] Rejeu réel `RafraichirValorisation?soId=1` sur un environnement où GRF et Sage sont des bases
      **distinctes** : **non exécuté** — cet environnement de build n'a pas accès à une base Sage
      distincte réelle (ni même à `GR_EMA_DISTRIBUTION`, cf. ci-dessus). Ne peut être vérifié que
      par l'architecte/PO avec un accès réseau réel à l'environnement client concerné (celui qui a
      levé l'erreur d'origine, `SqlException 208`). Le correctif adresse directement la cause
      racine identifiée par le diagnostic de la task (connexion GRF utilisée là où Sage était
      requis) ; seule la confirmation en conditions réelles reste hors de portée ici.
- [x] Confirmé par lecture de code : aucun `LEFT JOIN F_COMPTET` ni nom trois-parties
      (`[Base].dbo.F_COMPTET`) ne subsiste dans `SelectionExpliqueeService.cs` — grep vide sur les
      deux motifs.
- [x] Confirmé : aucun script `.sql` du dépôt ne crée de `SYNONYM` pour `F_COMPTET` (grep
      `SYNONYM` sur `*.sql` : 0 résultat).
- [x] Grep `GetSageConnectionInfoAsync` dans `SelectionExpliqueeService.cs` → 0 résultat (le
      service ne référence pas `Declaration.Infrastructure`, la résolution reste faite par
      l'appelant `DeclarationWorkflowService`).

## Garde-fous respectés

- Lecture seule stricte : la nouvelle requête F_COMPTET est un `SELECT` batché, aucune écriture
  ajoutée sur F_COMPTET ni sur aucune table Sage.
- Aucun JOIN SQL trois-parties introduit — jointure applicative en mémoire uniquement
  (`Dictionary<string, TiersFComptetRow>`), même principe que TASK-140.
- Aucun synonyme cross-base créé ni référencé.
- Logique métier d'évaluation non touchée : `SelectionExpliqueeEvaluator.cs` et `MapAndEvaluate`
  sont inchangés — seule l'origine des colonnes tiers a changé (voir § écart documenté ci-dessus).

## Verdict

Build et tests unitaires (hors dépendance base réelle) verts. Le point bloquant explicitement
identifié par la task elle-même (rejeu réel sur environnement GRF/Sage distincts) n'a pas pu être
exécuté ici par absence d'accès réseau à un tel environnement — à confirmer par l'architecte/PO
avant clôture, comme le prévoyait déjà la task (« si l'environnement de build a accès… »).
