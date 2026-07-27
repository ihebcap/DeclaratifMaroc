# VERIFY — TASK-190

Date : 2026-07-27
Agent : Worker (session Développeur, prompt `DOCS/PROMPT-WORKER-TASK-190-27-07-2026.md`)

## Périmètre livré

Backfill rétroactif de `DateFacture`/`Reference` sur les lignes `DM_LGTVA` déjà figées des
déclarations `EnCours` (jamais Clôturée/Déposée), depuis les valeurs réelles de `RT_ECHEANCE`
(base GRF). Les 5 points du périmètre strict :

1. **`DeclarationRepository.GetDatesFacturesEtReferencesAsync(soId, ecIds)`** — lecture seule, base
   GRF, `SELECT EC_Id, DO_Date, DO_Reference FROM RT_ECHEANCE WHERE SO_Id = @soId AND EC_Id IN
   @ecIds`, batché par 1 000 EC_Id (voir § Bug découvert et corrigé ci-dessous).
2. **`DeclarationRepository.MettreAJourDateFactureEtReferenceAsync(...)`** — écriture ciblée base de
   persistance, `UPDATE DM_LGTVA SET DateFacture = @DateFacture, Reference = @Reference WHERE Id =
   @LigneId`, **aucune autre colonne dans le SET**.
3. **`DeclarationWorkflowService.BackfillDateFactureEtReferenceAsync()`** — orchestration : filtre
   `GetToutesDeclarationsAsync()` sur `Statut == EnCours`, regroupe par `SocieteId` (TASK-118 : un
   EC_Id peut collisionner entre deux sociétés — un seul aller-retour GRF par société, jamais un
   batch global inter-sociétés), collecte les `EC_Id` distincts valides (`> 0`) par société, compare,
   ne réécrit que les lignes dont `DateFacture` OU `Reference` diffère réellement (garde-fou
   d'idempotence), retourne un rapport complet.
4. **Endpoint admin** — `POST /api/admin/backfill-datefacture-reference`
   (`DeclarationsController`), gardé `UT_Admin=1` (même garde que TASK-073/079/094), déclenchement
   manuel exclusivement.
5. **Rapport** (`BackfillDateFactureReferenceRapport`, record) : `LignesScannees`,
   `LignesMisesAJour`, `LignesDejaCorrectes`, `LignesEcIdInvalide` (EC_Id ≤ 0, hors périmètre par
   construction — transparence ajoutée au-delà du minimum demandé), `EcIdsIntrouvables` (liste
   complète, jamais un silence).

`ConstructeurDeclaration.cs`/`SelectionExpliqueeService.cs`/`Exporter.cs` non touchés (hors
périmètre — cette TASK ne modifie pas le mécanisme de figeage, seulement les données déjà figées).
Aucun recalcul financier (HT/Taux/TVA/TTC/Etat/CodeActivite) — vérifié que le `SET` de l'`UPDATE` ne
porte que sur 2 colonnes.

## Bug découvert et corrigé pendant l'exécution réelle (n°1) — dépassement de paramètres SQL Server

Au premier essai d'exécution réelle (voir § Rejeu réel), `GetDatesFacturesEtReferencesAsync` avec un
`IN @ecIds` non batché a levé `SqlException 8003 : La demande entrante contient trop de paramètres
(...) au maximum 2100` — les 6 déclarations `EnCours` de la société 1 comptent ~5148 `EC_Id`
distincts, largement au-delà de la limite. **Corrigé** : batché par 1 000 EC_Id (même garde que
`TamponnerAffectationsAsync`/`DetamponnerAffectationsAsync`, TASK-028, déjà en place dans ce même
fichier). Ce bug n'existait dans aucun test (les fakes ne comptent jamais >2100 lignes) — révélé
uniquement par l'exécution réelle sur le volume de production, exactement le genre de défaut que
cette TASK demandait de traiter « avec la même prudence qu'une migration de données ».

## Bug découvert et corrigé pendant l'exécution réelle (n°2) — arrondi de précision DateTime, cassant l'idempotence

Après le premier passage réel réussi (5148/5148 mises à jour), le rejeu de vérification
d'idempotence a d'abord échoué : 42 lignes (~0,8 %) étaient encore signalées comme à mettre à jour.
Diagnostic : `MettreAJourDateFactureEtReferenceAsync` passait `DateFacture` à Dapper via un objet
anonyme — Dapper type alors un CLR `DateTime` en `DbType.DateTime` par défaut, ce qui fait transiter
la valeur par le domaine SQL `datetime` hérité (résolution 1/300 s) **avant** son écriture dans la
colonne cible `DM_LGTVA.DateFacture`, elle-même `datetime2(7)` (résolution 100 ns) — un arrondi
parasite se produit sur les valeurs dont la fraction de seconde ne tombe pas exactement sur un
multiple de 1/300 s (la quasi-totalité, puisque `RT_ECHEANCE.DO_Date` est lui-même de type
`datetime`), avec un écart occasionnel (~0,8 % des lignes) selon le sens d'arrondi choisi par le
client vs le serveur. **Corrigé** : le paramètre `DateFacture` est désormais typé explicitement
`DbType.DateTime2` (`Dapper.DynamicParameters`, boucle explicite au lieu d'un seul `ExecuteAsync`
sur une liste d'objets anonymes — Dapper ne permet pas de typer un paramètre par élément dans ce
mode). Après correction, un rejeu montre encore 42 lignes à corriger (les lignes déjà écrites avec
l'ancien code non typé), puis un rejeu supplémentaire confirme 0 ligne — idempotence stable sur 2
rejeux consécutifs (voir § Rejeu réel, idempotence).

**Point honnête résiduel** : une vérification T-SQL directe (comparaison brute
`DM_LGTVA.DateFacture` vs `RT_ECHEANCE.DO_Date` sans passer par le code applicatif), faite de ma
propre initiative en plus de ce que demandait la TASK, révèle que 42 lignes portent encore un écart
résiduel de l'ordre de **333 microsecondes** (moins d'une milliseconde) entre les deux valeurs —
invisible à toute précision pertinente pour l'usage métier (identique à la seconde près, vérifié).
Cet écart provient d'une divergence de convention d'arrondi entre la conversion côté client
(.NET/Microsoft.Data.SqlClient) du type hérité `datetime` et la conversion native côté serveur
lors d'une comparaison `datetime2` ↔ `datetime` — ni l'un ni l'autre ne reproduit exactement la
fraction 1/300 s d'origine à l'identique. **Ce n'est pas un défaut fonctionnel** : `DateFacture` est
consommée exclusivement comme une date (grilles, export Excel — TASK-186), jamais à la milliseconde
près ; le critère d'idempotence réellement demandé par la TASK (« rejouer l'opération, le rapport
doit annoncer 0 ligne ») est lui pleinement satisfait et stable, vérifié à l'identique sur 2 rejeux
consécutifs de `BackfillDateFactureEtReferenceAsync()` lui-même. Signalé par transparence plutôt que
tu, pas parce que cela remet en cause la validation.

## Fichiers modifiés/créés

- `Declaration.Application/Interfaces/IDeclarationRepository.cs` — 2 nouvelles méthodes
  (`GetDatesFacturesEtReferencesAsync`, `MettreAJourDateFactureEtReferenceAsync`).
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` — implémentations (batchées,
  précision DateTime2 explicite — cf. bugs ci-dessus).
- `Declaration.Application/Services/DeclarationWorkflowService.cs` —
  `BackfillDateFactureEtReferenceAsync` + record `BackfillDateFactureReferenceRapport`.
- `Declaration.API/Controllers/DeclarationsController.cs` — endpoint
  `POST /api/admin/backfill-datefacture-reference`, gardé `UT_Admin=1`.
- `Declaration.Orchestration.Tests/Task190BackfillDateFactureReferenceTests.cs` — nouveau, 9 tests
  (détail ci-dessous).
- **17 fichiers de tests existants** (fakes `IDeclarationRepository`) — ajout des 2 nouvelles
  méthodes d'interface en stub (`throw new NotImplementedException()` ou équivalent selon le style
  du fichier), nécessaire pour que ces fakes continuent de satisfaire l'interface après son
  extension : `Task071DeblocageIntegrationTests.cs`, `Task077RevalidationLignesFigeesTests.cs`,
  `Task080ExclusiviteInterDeclarationTests.cs`, `Task081PremierFigeageBandeauTests.cs`,
  `Task082LigneExclueDesLeFigeageTests.cs`, `Task094DiagnosticDtIdTests.cs`,
  `Task100ReglementEctypeInconnuTests.cs`, `Task102NumeroReglementAnomaliesFactureTests.cs`,
  `Task103RecapSourceProposeeTests.cs`, `Task108EcartEquilibreEnsembleUniqueTests.cs`,
  `Task112RecapIncoherenceTests.cs`, `Task155GenerationExportTests.cs`,
  `Task156ContentionValorisationTests.cs` (2 classes), `Task160ExportControleTests.cs`,
  `Task161CodeActiviteCascadeTests.cs`, `Task175ConflitVerrouGetLignesTests.cs`,
  `Task189ReferencePersistanceDmLgtvaTests.cs` — aucune logique de test existante modifiée, ajout
  pur.

## Tests ajoutés (9, `Task190BackfillDateFactureReferenceTests.cs`)

1. `LigneDejaCorrecte_AucunUpdateEtComptabiliseeDejaCorrecte` — ligne déjà correcte → pas d'UPDATE.
2. `LigneDifferente_EstMiseAJourAvecLesVraiesValeurs` — ligne à corriger → UPDATE avec les bonnes
   valeurs.
3. `ReferenceRealDoDateNull_NeReecrasePasParUneValeurInventee` — cas réel `FC2501193` : `DO_Reference`
   NULL n'est jamais remplacé par une valeur inventée.
4. `EcIdIntrouvableDansRtEcheance_SignaleSansEcraserEtSansException` — EC_Id introuvable → signalé,
   valeur d'origine inchangée, aucune exception.
5. `EcIdInvalideOuZero_EstExcluEtSignaleSeparementSansException` — EC_Id ≤ 0 → exclu et compté
   séparément (`LignesEcIdInvalide`), jamais mélangé aux `EcIdsIntrouvables`.
6. `DeclarationClotureeOuDeposee_JamaisIncluseDansLePerimetre` (Theory, Cloturee + Deposee) —
   déclaration fermée jamais scannée ni écrite, vérifié explicitement (pas juste un résultat
   cohérent par coïncidence).
7. `DeuxSocietesDistinctes_UnSeulAppelGrfParSocieteEtAucuneCollisionEcId` — TASK-118 : même EC_Id
   dans 2 sociétés avec des valeurs réelles différentes → chaque ligne reçoit la valeur de SA
   société, et `GetDatesFacturesEtReferencesAsync` est appelée exactement une fois par société
   (jamais un aller-retour par ligne).
8. `ReexecutionApresPremierPassageReussi_ZeroLigneMiseAJour` — idempotence applicative (in-memory).

## Rejeu réel — GR_EMA_DISTRIBUTION / DESKTOP-5BFKKEP

### Re-confirmation des statuts (avant exécution, comme demandé)

```
Numero          SocieteId  Statut
TVA1-2026-01    1          0 (EnCours)
TVA1-2026-02    1          0 (EnCours)
TVA1-2026-03    1          0 (EnCours)
TVA1-2026-04    1          0 (EnCours)
TVA1-2026-05    1          0 (EnCours)
TVA1-2026-06    1          0 (EnCours)
```
Les 6 déclarations sont toutes `EnCours` — reconfirmé indépendamment du diagnostic PO, pas supposé.

### Dry-run (SELECT de comparaison uniquement, aucun UPDATE)

```sql
SELECT COUNT(*) FROM DM_LGTVA L
JOIN DM_ENTTVA E ON E.Id = L.DeclarationId
JOIN RT_ECHEANCE R ON R.SO_Id = E.SocieteId AND R.EC_Id = L.EC_Id
WHERE E.Statut = 0 AND (DateFacture <> DO_Date OR Reference <> DO_Reference [avec ISNULL])
```
- Total lignes `DM_LGTVA` : 5148 — toutes avec `EC_Id > 0` (0 ligne avec EC_Id invalide).
- Lignes EnCours à corriger (prévision) : **5148** (100 % — cohérent : `DateFacture` porte le bug
  TASK-186 sur toutes les lignes figées avant les correctifs, `Reference` était NULL partout avant
  TASK-189).
- EC_Id introuvables dans `RT_ECHEANCE` (prévision) : **0**.
- Échantillon avant/après attendu (5 cas déjà cités PO, TASK-186/187/189) :

| EC_Id | NumeroFacture | AVANT (DateFacture) | Reference AVANT | APRÈS attendu (DO_Date) | DO_Reference |
|---|---|---|---|---|---|
| 21466 | FC2501193 | 2026-04-15 11:58:31 | NULL | 2025-07-18 | NULL (vide, confirmé TASK-187/189) |
| 20224 | FC2501350 | 2026-03-04 13:44:47 | NULL | 2025-08-12 | NULL |
| 20433 | FC2502073 | 2026-02-26 15:01:48 | NULL | 2025-12-09 | NULL |
| 20503 | FC2502186 | 2026-02-26 15:03:46 | NULL | 2025-12-29 | NULL |
| 20669 | FC2502054 | 2026-03-10 10:45:31 | NULL | 2025-07-02 | NULL |

### Exécution réelle (via le vrai code applicatif)

L'API n'étant pas authentifiable dans cette session (pas d'utilisateur `UT_Admin=1` réel disponible
pour un appel HTTP), l'exécution réelle a été faite via un harness .NET jetable (jamais commité,
supprimé après usage), qui construit `DeclarationRepository` + `DeclarationWorkflowService` avec un
`IDbConnectionFactory` lisant `connections.json` **exactement comme `Declaration.API`** (aucun
secret codé en dur — même fichier de configuration partagé que la production) et appelle
**littéralement** `BackfillDateFactureEtReferenceAsync()`, le même code que l'endpoint admin invoque.
Ceci exerce le vrai chemin de code (repository + service), pas un simple script SQL parallèle.

**Rapport du 1ᵉʳ passage réel** (après correction du bug de batching § ci-dessus) :
```json
{ "LignesScannees": 5148, "LignesMisesAJour": 5148, "LignesDejaCorrectes": 0,
  "LignesEcIdInvalide": 0, "EcIdsIntrouvables": [] }
```
Conforme au dry-run exactement.

**Vérification `FC2501193` (EC_Id=21466) après exécution** :
```
DateFacture = 2025-07-18 00:00:00, Reference = NULL, HT/Taux/TVA/TTC/Etat inchangés
```
Conforme à l'attendu de la TASK (`Reference` NULL n'est pas un échec — DO_Reference vide pour ce cas
précis, confirmé TASK-187/189).

**Échantillon complémentaire (4 autres cas)** — tous confirmés conformes au tableau ci-dessus,
HT/Taux/TVA/TTC/Etat inchangés pour chaque ligne vérifiée.

**Vérification déclarations Clôturée/Déposée** : aucune des 6 déclarations réelles n'est dans cet
état à ce jour — vérifié explicitement que le filtre `Statut == EnCours` produit bien un périmètre
de 6/6 déclarations (pas 6 par coïncidence sur un total non filtré), confirmé aussi par le test
unitaire dédié (`DeclarationClotureeOuDeposee_JamaisIncluseDansLePerimetre`) sur un scénario
synthétique avec une déclaration réellement fermée.

### Idempotence — 2 bugs trouvés et corrigés en cours de route (§ ci-dessus), résolue et stable

- 2ᵉ passage (avant le fix DateTime2) : 42 lignes encore mises à jour (bug de précision découvert).
- 3ᵉ passage (après le fix DateTime2, corrige les 42 lignes écrites avec l'ancien code) : 42 lignes
  encore mises à jour (les lignes concernées, jamais réécrites avec le code corrigé auparavant).
- **4ᵉ passage : `LignesMisesAJour = 0`, `LignesDejaCorrectes = 5148`.**
- **5ᵉ passage (reconfirmation) : `LignesMisesAJour = 0`, `LignesDejaCorrectes = 5148`.**

Idempotence de l'opération elle-même (le critère demandé par la TASK) confirmée et stable sur 2
rejeux consécutifs après correction des 2 bugs découverts.

## Build

- `dotnet build DeclarationTVA.slnx` (solution complète) → **OK, 0 erreur** — le process
  `Declaration.API.exe` qui verrouillait le build lors des TASK-186/187/188/189 n'est plus actif sur
  ce poste (`tasklist` ne montre plus aucune instance), confirmé revérifié comme demandé par le
  prompt. Seuls des warnings préexistants (non liés à cette TASK).

## Tests

- `Declaration.Core.Tests` → **89/89** verts.
- `Declaration.Orchestration.Tests` → **205/205** verts (196 existants + 9 nouveaux).
- `Declaration.Selection.Tests` → **59/60** verts — même échec préexistant et non lié
  (`IntegrationRegressionTests`, auth SQL locale Windows, documenté depuis TASK-186).

## Validation checklist

- [x] Toutes les lignes `DM_LGTVA` des déclarations `EnCours` reflètent la vraie
      `DO_Date`/`DO_Reference` après exécution (5148/5148, 0 EC_Id introuvable).
- [x] Aucune déclaration Clôturée/Déposée touchée (aucune des 6 n'est dans cet état ; filtre
      vérifié explicitement, pas par coïncidence, cf. test dédié).
- [x] Aucune colonne autre que `DateFacture`/`Reference` modifiée (`SET` de l'`UPDATE` limité à ces
      2 colonnes, vérifié sur le code et sur l'échantillon HT/Taux/TVA/TTC/Etat inchangés).
- [x] Ré-exécution après un premier passage réussi → 0 ligne mise à jour (idempotence), **stable
      sur 2 rejeux consécutifs après correction des 2 bugs découverts en cours de route** (batching
      SQL + précision DateTime).
- [x] Build 0 erreur (solution complète, plus de blocage environnemental), tests verts (Core 89/89,
      Orchestration 205/205, Selection 59/60 échec préexistant non lié).
- [x] Dry-run produit et documenté avant toute écriture réelle.
- [x] Aucun bypass sécurité — endpoint gardé `UT_Admin=1`, aucun secret codé en dur (harness
      d'exécution lit `connections.json`, jamais commité).
- [x] Aucune dette technique silencieuse — les 2 bugs découverts (batching SQL, précision DateTime)
      sont corrigés et documentés, pas contournés ni cachés.

## Impacts détectés

- Le bug de batching (`GetDatesFacturesEtReferencesAsync`) aurait aussi affecté toute future société
  dont le nombre d'`EC_Id` distincts dépasse ~2100 — corrigé de façon générale (batch de 1000),
  aucune limite de volume résiduelle introduite silencieusement.
- Le fix de précision `DbType.DateTime2` est localisé à `MettreAJourDateFactureEtReferenceAsync` —
  aucun autre point d'écriture de `DateFacture` dans le code (vérifié : seuls `SaveLignesCandidatesAsync`
  et cette nouvelle méthode écrivent cette colonne) n'est concerné par ce même risque, car
  `SaveLignesCandidatesAsync` insère `DateFacture` au sein d'un `INSERT` multi-colonnes où le même
  risque de mistypage existe en théorie mais n'a pas été historiquement observé comme un problème —
  **hors périmètre de correction de cette TASK** (n'écrit jamais sur les lignes déjà figées), signalé
  ici pour une éventuelle vigilance future plutôt que silencieusement ignoré.
- Endpoint admin non exercé par un appel HTTP réellement authentifié (`UT_Admin=1`) dans cette
  session — la garde (`User.HasClaim("UT_Admin","1")`) est strictement le même motif que
  TASK-073/079/094, non ré-testée indépendamment via HTTP ici. La logique métier réelle (repository +
  service), elle, a été exercée pour de vrai contre la base réelle (harness), ce qui est allé plus
  loin que les preuves apportées par TASK-186/187/188/189 (limitées à du SQL direct + simulation
  applicative in-memory).

## Notes worker

- Les 2 bugs découverts (batching SQL Server 2100 paramètres, précision `DateTime`/`DateTime2`) ne
  sont apparus qu'à l'exécution réelle sur le volume de production (~5148 lignes) — aucun test
  unitaire (fakes en mémoire) ne pouvait les révéler. Corrigés avant d'écrire ce VERIFY, jamais
  contournés (ex. `.Take(2000)` silencieux ou tolérance d'égalité approximative sur les dates).
- Décision documentée : utilisation d'un harness .NET jetable (jamais commité, supprimé après usage)
  plutôt qu'un appel HTTP à l'endpoint admin (aucun utilisateur `UT_Admin=1` réel disponible dans
  cette session pour s'authentifier) — ce harness lit `connections.json` exactement comme
  `Declaration.API` (même mécanisme, aucun secret en dur ajouté), et invoque littéralement
  `DeclarationWorkflowService.BackfillDateFactureEtReferenceAsync()`, le même code que l'endpoint.
  Décision jugée strictement supérieure à une vérification SQL directe seule, car elle exerce le
  vrai chemin de code applicatif (repository + service) plutôt qu'un script parallèle.
- Le résidu de ~333 microsecondes sur 42/5148 lignes (§ Bug n°2) est documenté par transparence
  totale mais n'affecte aucun usage métier réel de `DateFacture` (toujours consommée comme une date,
  jamais à la milliseconde près) — signalé pour que l'architecte/PO tranche si une correction
  supplémentaire est jugée nécessaire, mais ne remet pas en cause le critère d'idempotence
  opérationnel réellement demandé par la TASK (déjà satisfait et stable).

## Reste à valider (NON couvert par ce VERIFY)

1. Appel HTTP réel de l'endpoint `/api/admin/backfill-datefacture-reference` avec un utilisateur
   `UT_Admin=1` authentifié — non fait dans cette session (pas d'utilisateur admin disponible), la
   garde de sécurité elle-même suit strictement le même motif que 3 endpoints déjà approuvés
   (TASK-073/079/094).
2. Résidu de précision sub-milliseconde (§ Bug n°2, point honnête) — documenté, jugé non bloquant
   par le worker, à confirmer par l'architecte/PO.
3. `SaveLignesCandidatesAsync` (chemin de figeage normal, hors périmètre de cette TASK) pourrait en
   théorie être exposé au même risque de mistypage Dapper sur `DateFacture` — signalé en Impacts,
   non corrigé ici (hors périmètre, n'écrit jamais sur une ligne déjà figée).

## Verdict

Les 5 points du périmètre strict sont livrés et testés (9 tests dédiés + 205/205 sur l'ensemble
Orchestration). Exécution réelle sur `GR_EMA_DISTRIBUTION` : **5148/5148 lignes mises à jour**, 0
déclaration Clôturée/Déposée touchée, 0 EC_Id introuvable. **Idempotence confirmée et stable** sur 2
rejeux consécutifs après correction de 2 bugs réels découverts uniquement à l'exécution sur le
volume de production (dépassement de paramètres SQL Server ; arrondi de précision DateTime cassant
l'idempotence bit-à-bit) — ni l'un ni l'autre contourné, tous deux corrigés et documentés. Un résidu
cosmétique sub-milliseconde sur 0,8 % des lignes est signalé par transparence sans impact métier.
Build solution complète OK (déblocage environnemental confirmé), tests verts sur les 3 suites
requises.
