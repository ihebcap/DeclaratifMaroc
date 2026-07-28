# VERIFY — TASK-186

Date : 2026-07-27
Agent : Worker (session Développeur, prompt `DOCS/PROMPT-WORKER-TASK-186-187-27-07-2026.md`)

## Périmètre livré

Correction de l'alias SQL `DateFacture` dans les **3 requêtes règlement-first** de
`Declaration.Selection/SelectionExpliqueeService.cs` :
- `GetSurensembleFournisseurSql`
- `GetSurensembleDepenseSql`
- `GetSurensembleClientSql`

`A.AF_Date AS DateFacture` → `E.DO_Date AS DateFacture` (aucun nouveau JOIN, `RT_ECHEANCE E` déjà
jointe dans les 3 requêtes). Aucune interface changée (même nom de colonne en sortie). Le chemin
facture-first (`GetFactureFirstSql`, TASK-050) n'a pas été touché — il alias déjà correctement
`E.DO_Date AS DateFacture`, vérifié par lecture avant modification.

## Fichiers modifiés

- `Declaration.Selection/SelectionExpliqueeService.cs` — 3 lignes changées, aucune autre modification
  (diff `git diff` : 3 insertions / 3 suppressions, rien d'autre).

## Diff résumé

```diff
-                A.AF_Date AS DateFacture,
+                E.DO_Date AS DateFacture,
```
Répété identiquement aux 3 endroits (lignes ~115, ~153, ~188 avant modification).

## Tests / fixtures existants — vérification (Étape 2 de la TASK)

Recherche exhaustive (`AF_Date`, `DateFacture`, `GetSurensemble*`, `GetFactureFirstSql`) dans tout le
dépôt : **aucun test/fixture ne fixait une valeur sur l'ancien comportement `AF_Date`.**
- Les 3 requêtes SQL de `SelectionExpliqueeService.cs` ne sont exercées par aucun test unitaire direct
  (elles nécessitent une connexion SQL réelle — seul `IntegrationRegressionTests.cs`, hors périmètre
  SQL précis, les invoque en bout en bout).
- `Declaration.Selection.Tests/SelectionExpliqueeEvaluatorTests.cs` fixe `DateFacture = _debut`
  directement sur un `AffectationCandidateRow` (fixture in-memory, indépendante de l'alias SQL qui
  aurait produit cette valeur) — non concerné par la correction.
- Aucun changement de fixture nécessaire.

## Rejeu réel (lecture seule) — GR_EMA_DISTRIBUTION / DESKTOP-5BFKKEP

### Cas principal PO : `FC2501193` (`EC_Id=21466`)

Requête `sqlcmd` rejouant exactement la logique des 3 requêtes corrigées (jointure
`RT_MOUVEMENT M LEFT JOIN RT_AFFECTATION A LEFT JOIN RT_ECHEANCE E`, filtrée sur `E.EC_Id = 21466`,
`M.MV_Domaine = 1` → chemin fournisseur) :

```
SO_Id  MV_Id  MV_Domaine  AF_Id  NumeroFacture   DateFacture_AVANT_correction   DateFacture_APRES_correction
1      10345  1           20522  FC2501193       2026-04-15 11:58:31.677       2025-07-18 00:00:00.000
```

**Confirmé** : la valeur produite passe de **15/04/2026 11:58:31** (`AF_Date`, ancien alias) à
**18/07/2025** (`DO_Date`, nouvel alias) — exactement la date facturée annoncée par le PO, exactement
le cas décrit dans `TASK-186` et `TODO.md`.

### Échantillon complémentaire (4 autres cas cités par la TASK)

```
EC_Id   DO_Numero   DO_Date_reel (APRES)   AF_Date_ancien_alias (AVANT)
20224   FC2501350   2025-08-12             2026-03-04 13:44:47.937
20433   FC2502073   2025-12-09             2026-02-26 15:01:48.373
20503   FC2502186   2025-12-29             2026-02-26 15:03:46.403
20669   FC2502054   2025-07-02             2026-03-10 10:45:31.613
```
Même écart systématique confirmé sur les 4 échantillons — la correction est générale, pas limitée au
seul cas `FC2501193`.

### Point honnête — régénération de l'export Excel d'une déclaration EXISTANTE (NON concluant, documenté ci-dessous)

La TASK demandait aussi de « régénérer le contrôle Excel d'une déclaration existante (ex.
`TVA1-2026-01`) » pour observer la colonne corrigée. **Vérifié en base que ceci ne peut PAS démontrer
la correction sur une déclaration déjà existante** :

- `FC2501193` (`EC_Id=21466`) est déjà présent dans `DM_LGTVA` (2 lignes, `DeclarationId` =
  `TVA1-2026-01`), avec `DateFacture = 2026-04-15 11:58:31.677` — c'est-à-dire la valeur **AVANT**
  correction, déjà persistée.
- `ConstruireModeleControleAsync`/`GenererExcelControleAsync` (export « contrôle » réutilisable à tout
  moment) lisent `GetLignesAsync`, qui relit **la valeur persistée dans `DM_LGTVA`**, jamais une
  nouvelle exécution SQL live de `SelectionExpliqueeService`. Régénérer l'export de `TVA1-2026-01`
  aujourd'hui afficherait donc **encore** 15/04/2026 pour cette ligne — pas parce que la correction est
  inopérante, mais parce que la ligne a été calculée et persistée AVANT la correction et n'est jamais
  recalculée par une simple régénération d'export.
- C'est exactement l'incertitude que `TASK-186` elle-même soulevait dans sa section Risques (« à
  confirmer selon le mécanisme réel de régénération ») — **confirmée ici : la régénération d'export
  seule ne suffit pas**, seule une nouvelle exécution de la sélection (nouveau cycle de valorisation,
  hors périmètre de cette TASK, qui ne modifie que le SQL de sélection) produira la date corrigée.
- La preuve réelle et représentative de la correction reste donc le rejeu direct des 3 requêtes SQL
  corrigées ci-dessus (section précédente), qui reproduit fidèlement ce que `SelectionExpliqueeService`
  produira pour toute **nouvelle** sélection (déclaration future, ou toute ligne pas encore figée).
- Point signalé à toutes fins utiles (hors périmètre correction, cf. TASK-186 § Risques) : les 2 lignes
  `DM_LGTVA` pour `EC_Id=21466` portent toutes deux la date fausse — si le PO souhaite un jour
  retraiter les déclarations existantes, ce cas est un exemple concret à reprendre.

## Build

- `dotnet build Declaration.Core/Declaration.Core.csproj` → OK, 0 erreur.
- `dotnet build Declaration.Selection/Declaration.Selection.csproj` → OK, 0 erreur.
- `dotnet build Declaration.Export.Excel/Declaration.Export.Excel.csproj` → OK, 0 erreur.
- `dotnet build Declaration.Application/Declaration.Application.csproj` → OK, 0 erreur.
- `dotnet build Declaration.Infrastructure/Declaration.Infrastructure.csproj` → OK, 0 erreur.
- **`dotnet build DeclarationTVA.slnx` (solution complète) — bloqué par l'environnement, pas par le
  code** : un process `Declaration.API.exe` (PID 33068, démarré 14:11:17 le 27/07/2026) tournait déjà
  sur ce poste et verrouillait `Declaration.API/bin/Debug/net8.0-windows/*.dll`. Autorisation demandée
  et obtenue du PO pour l'arrêter (`Stop-Process`/`taskkill`), mais **refusée par l'OS** (« Accès
  refusé ») — le process n'appartient pas à une session avec les droits suffisants dans ce contexte
  d'exécution, et ce n'est pas un service Windows (vérifié via `Get-CimInstance Win32_Service`).
  Contournement : chaque projet de bibliothèque a été buildé individuellement (5/5 OK ci-dessus), et
  les suites de tests ont été buildées/exécutées avec `--output <dossier temporaire>` pour éviter la
  collision d'écriture (voir section Tests). **Aucune erreur de compilation détectée sur l'ensemble du
  code touché ou dépendant** — seul l'assemblage final du solution-build (copie de DLL dans le dossier
  de sortie de `Declaration.API`, déjà verrouillé par une instance en cours d'exécution) est affecté,
  pas la compilation elle-même.

## Tests

- `Declaration.Core.Tests` → **88/88** verts.
- `Declaration.Export.Excel.Tests` → **3/3** verts.
- `Declaration.Orchestration.Tests` → **188/188** verts (buildé/exécuté via
  `dotnet test Declaration.Orchestration.Tests --output <dossier temporaire>` pour contourner le
  verrou décrit ci-dessus — ce projet référence `Declaration.API.csproj` directement).
- `Declaration.Selection.Tests` → **58/59** verts, **1 échec préexistant et non lié à cette
  correction** :
  `TestRegression_NouveauSurensembleIncludAncienTask008_Task050` (`IntegrationRegressionTests.cs`)
  échoue avec `Microsoft.Data.SqlClient.SqlException: Échec de l'ouverture de session de l'utilisateur
  'IHEB-PC\ihebc'` — échec d'authentification **avant** toute exécution de requête (connexion
  `Integrated Security=True` refusée pour cet utilisateur Windows sur cette instance SQL Server), donc
  strictement indépendant du texte SQL modifié par cette TASK. Ce test échouerait à l'identique avec ou
  sans la correction (vérifié par analyse : l'échec se produit dans `connection.OpenAsync()`, avant que
  la moindre requête ne soit envoyée). Point d'hygiène signalé, hors périmètre de correction : ce test
  d'intégration n'est pas exécutable en l'état sur ce poste avec le compte Windows courant — à
  reconfigurer (accès SQL pour `IHEB-PC\ihebc`, ou connexion par login SQL) si l'on souhaite le rendre
  exécutable localement.

## Validation checklist

- [x] Build OK (bibliothèques concernées, individuellement — solution complète bloquée par
      l'environnement, cf. ci-dessus, pas par le code)
- [x] Tests passés (Core 88/88, Export.Excel 3/3, Orchestration 188/188 ; Selection 58/59, le seul
      échec étant préexistant et non lié)
- [x] `DateFacture` produite par le chemin règlement-first = `RT_ECHEANCE.DO_Date`, jamais
      `RT_AFFECTATION.AF_Date` — vérifié sur 5 cas réels (`FC2501193` + 4 échantillons)
- [x] Aucune régression sur le chemin facture-first (non modifié, vérifié par lecture du diff : 0 ligne
      touchée dans `GetFactureFirstSql`)
- [x] Aucun bypass sécurité
- [x] Aucune dette technique silencieuse introduite (le seul compromis — contournement du build
      solution complète — est documenté explicitement ci-dessus, pas caché)

## Impacts détectés

- Toute nouvelle sélection (déclaration future, ou ligne pas encore figée dans une déclaration
  `EnCours`) via le chemin règlement-first affichera désormais la vraie date de facture.
- Aucune donnée `DM_LGTVA` existante n'est modifiée par cette correction (confirmé : les 2 lignes
  `EC_Id=21466` déjà persistées dans `TVA1-2026-01` gardent leur ancienne valeur fausse tant qu'elles
  ne sont pas recalculées par un nouveau cycle de valorisation — hors périmètre de cette TASK).
- Aucun autre module identifié comme dépendant de cet alias SQL.

## Notes worker

- Décision : ne pas tenter de contourner le refus OS d'arrêter le process `Declaration.API.exe` par un
  moyen plus intrusif (élévation de privilèges) — hors périmètre, risque disproportionné pour un simple
  blocage de build local. Documenté honnêtement plutôt que masqué.
- Le point « régénération d'un export réel montrant la date corrigée » n'a pas pu être satisfait tel
  que formulé littéralement dans la TASK, pour la raison structurelle expliquée ci-dessus (lecture de
  données déjà persistées, pas de recalcul live). La preuve apportée à la place (rejeu SQL direct des 3
  requêtes corrigées sur le cas réel + échantillon) est jugée équivalente et strictement plus fiable
  pour démontrer que **le SQL corrigé, exécuté tel qu'il le sera pour toute nouvelle sélection**,
  produit la bonne date — mais ce point mérite une lecture PO avant clôture, car ce n'est pas
  exactement ce qui était demandé littéralement.

## Reste à valider (NON couvert par ce VERIFY)

1. **Rejeu littéral d'un export Excel régénéré** montrant la date corrigée n'a pas été produit — voir
   justification structurelle ci-dessus. Si le PO juge cette preuve alternative insuffisante, il faudra
   soit (a) déclencher un nouveau cycle de valorisation réel sur une donnée non encore figée (hors
   périmètre SQL strict de cette TASK), soit (b) accepter la preuve SQL directe comme équivalente.
2. Build de la solution complète (`DeclarationTVA.slnx`) non rejoué avec succès sur ce poste tant que
   le process `Declaration.API.exe` verrouillant reste actif — à revérifier une fois ce process arrêté
   (redémarrage normal du poste, ou arrêt avec des droits suffisants).
3. Retraitement rétroactif des déclarations déjà closes/déposées avec la mauvaise date : **hors
   périmètre par décision explicite de la TASK**, non traité ici — simplement confirmé qu'au moins
   `TVA1-2026-01` (`EnCours`, non close) contient des lignes concernées (`EC_Id=21466` notamment).

## Verdict

Correction SQL conforme au périmètre strict de TASK-186, prouvée sur données réelles (5 cas). Aucune
régression de test attribuable au changement. Deux réserves non bloquantes documentées ci-dessus
(preuve d'export littérale non produite pour raison structurelle expliquée ; build solution complète
non rejoué à cause d'un verrou de process local) — à trancher par l'architecte/PO avant clôture finale.
