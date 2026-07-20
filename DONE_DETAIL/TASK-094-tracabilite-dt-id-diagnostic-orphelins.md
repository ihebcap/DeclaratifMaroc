# TASK-094 — Traçabilité du tampon `DT_Id` + diagnostic des orphelins

## Origine
Incident PO 14/07/2026 : suppression de `TVA1-2026-01` (`EnCours`) refusée par le garde-fou
TASK-079 (`543 affectation(s) déjà tamponnée(s) (DT_Id)`). Investigation architecte (lecture code
+ requêtes SQL en lecture seule sur `.\sql2022`/`GR_EMA_DISTRIBUTION`) : 3 valeurs distinctes de
`RT_AFFECTATION.DT_Id` (610 lignes au total) ne correspondaient à **aucune** des 2 déclarations
existantes en base — confirmé en recalculant `DeriveDtId` (`Guid.GetHashCode() & int.MaxValue`,
`DeclarationWorkflowService.cs:960`) pour chacune. Correctif de données appliqué directement
(autorisation PO explicite, `UPDATE ... SET DT_Id = NULL` sur les 610 lignes concernées, hors
cycle TASK/VERIFY car aucun code touché) — voir `CHANGELOG.md` 14/07/2026. Cette task documente le
**gap de conception** révélé par l'incident, pas l'incident lui-même (déjà corrigé en données).

## Constat (preuve code)
1. `DeriveDtId(Guid id) => id.GetHashCode() & int.MaxValue` (`DeclarationWorkflowService.cs:960`)
   est **recalculé à la volée** à chaque pose (`CloturerDeclarationAsync`) et retrait
   (`ReouvriDeclarationAsync`) du tampon — jamais stocké.
2. `DM_ENTTVA` (`DeclarationTVA.sql:63-73`) ne porte **aucune colonne** permettant de retrouver la
   déclaration à partir d'une valeur `DT_Id` observée sur `RT_AFFECTATION`. Aucune table de
   correspondance inverse `DT_Id → DeclarationId` n'existe nulle part dans le schéma.
3. Conséquence directe et vérifiée : face à une ligne `RT_AFFECTATION.DT_Id` non nulle dont
   l'origine est inconnue (déclaration disparue, donnée de test, ou tout autre écart), il n'existe
   **aucun moyen applicatif** de l'identifier — seul un recalcul manuel du hash pour toutes les
   déclarations *actuellement existantes* permet d'éliminer les cas connus, et rien ne permet de
   diagnostiquer un tampon dont la déclaration source a été supprimée (le cas de cet incident).
4. Le garde-fou TASK-079 §4 (`CountAffectationsTamponneesAsync`, `DeclarationRepository.cs:121-132`)
   ne fait que **compter** les tampons détectés, sans donner aucune piste d'investigation à
   l'utilisateur (l'architecte a dû faire l'archéologie SQL manuellement pour cet incident).
5. Anomalie annexe constatée pendant l'investigation, non traitée ici : `TVA1-2026-06` porte
   `Statut=Cloturee` mais `DateCloture IS NULL`, et **aucune** de ses lignes ne porte le tampon réel
   attendu (`DeriveDtId` de son propre `Id`) — signe d'un `Statut` positionné hors du workflow
   applicatif normal (`CloturerDeclarationAsync` n'a jamais tourné pour cette déclaration). À
   signaler au PO séparément (donnée de test suspecte, pas nécessairement un bug applicatif).

## Objectif
Rendre un tampon `DT_Id` **diagnosticable** sans archéologie SQL manuelle : identifier sa
déclaration d'origine si elle existe encore, et signaler explicitement un tampon orphelin
(déclaration disparue) plutôt que de laisser un garde-fou bloquer une action sans piste.

## Périmètre proposé — deux options à trancher en conception (risque différent)

### Option A — diagnostic seul (recommandée en 1er lot, faible risque)
Ajouter un outil/endpoint **admin, lecture seule** qui, pour un ensemble de `DT_Id` donné (ou pour
toute la base), recalcule `DeriveDtId` de **toutes** les déclarations existantes et restitue :
correspondance trouvée (déclaration + numéro) ou `Orphelin` explicite. Réutilise `DeriveDtId` tel
quel (`internal`/exposé pour test), aucune modification de schéma. Objectif : transformer
l'archéologie manuelle de cet incident en une requête/outil reproductible.

### Option B — persistance de la correspondance (plus invasif, à ne lancer qu'après décision PO)
Stocker le lien de façon durable, par ex. colonne `DM_ENTTVA.DT_Id` (posée en même temps que le
tampon, à la clôture) permettant un `JOIN` direct `RT_AFFECTATION.DT_Id = DM_ENTTVA.DT_Id`. **Risque
à évaluer avant tout développement** : `RT_AFFECTATION`/`RT_MOUVEMENT` sont des tables **GRF/GRFN
partagées** (portée globale, hors du seul module Déclaration TVA, cf. TASK-028) — une modification
de schéma côté `DM_ENTTVA` seule est sans risque de collision cross-module, mais le bénéfice
(reverse-lookup) n'a de sens que si le tampon **actuel** posé par le module reste ainsi retrouvable
pour l'avenir ; ça ne résout **pas** rétroactivement les orphelins déjà existants (seule l'Option A
le permet).

## Garde-fous
1. Ne pas toucher aux triggers d'immuabilité (`003_Verrou_DT_Id.sql`, TASK-028/064) — hors périmètre.
2. Option A doit rester strictement lecture seule (aucun write sur `RT_AFFECTATION`/`RT_MOUVEMENT`/
   `DM_ENTTVA`).
3. Si Option B retenue : colonne additive, nullable, sur `DM_ENTTVA` uniquement (jamais sur les
   tables `RT_*` partagées) — cohérent avec la séparation déjà actée par TASK-080 (§ Risques,
   connexions séparées `GrfConnection`/`PersistenceConnection`).
4. Ne pas fusionner avec le traitement de l'anomalie annexe `TVA1-2026-06` (`DateCloture NULL`) —
   à cadrer séparément si le PO le demande, pas d'implémentation à la discrétion de l'implémenteur.

## Risques / points à trancher en conception
- Périmètre exact de l'Option A (endpoint API vs script admin ponctuel) — à trancher selon l'usage
  attendu (incident ponctuel vs surveillance récurrente).
- Décision PO requise avant de lancer l'Option B (modification de schéma, même additive et à faible
  risque) — ne pas l'entreprendre sans validation explicite, cohérent avec la doctrine du projet
  (pas de modification de schéma improvisée).

## Livrables de preuve (VERIFY)
1. Option A : preuve réelle sur `.\sql2022`/`GR_EMA_DISTRIBUTION` — rejouer le diagnostic sur les
   valeurs `DT_Id` de cet incident (ou équivalent reconstitué) et confirmer que l'outil les
   qualifie explicitement d'orphelins (aucune déclaration existante ne matche leur hash).
2. Si Option B : preuve qu'une clôture réelle pose bien `DM_ENTTVA.DT_Id` en cohérence avec
   `RT_AFFECTATION.DT_Id`, et qu'un `JOIN` direct retrouve la déclaration.
3. Build + tests existants (`Declaration.Selection.Tests`, `Declaration.Infrastructure`) verts,
   aucune régression sur TASK-028/079/080.

## Dépendances
- **S'appuie sur** TASK-028 (verrou `DT_Id`), TASK-079 (garde-fou suppression), TASK-080
  (exclusivité inter-déclarations) — ne remplace aucun des trois, comble un gap d'observabilité.
- **Aucune dépendance bloquante** — peut être planifiée indépendamment, non prioritaire vs le
  chemin critique produit en cours (tunnel 3 étapes, TASK-090/091/092 déjà livrées).

---

## Implémentation (rôle worker exceptionnel, demande explicite PO 14/07/2026)

**Option A seule** implémentée (diagnostic lecture seule) — Option B (persistance `DM_ENTTVA.DT_Id`)
non lancée, décision PO requise non obtenue dans cette session (conforme au garde-fou de la task).

- `DeclarationWorkflowService.DiagnostiquerDtIdAsync(IEnumerable<int>? dtIds = null)` : recalcule
  `DeriveDtId` (réutilisé tel quel, aucune 2ᵉ implémentation) pour **toutes** les déclarations
  existantes (`IDeclarationRepository.GetToutesDeclarationsAsync`, toutes sociétés) et qualifie
  chaque `DT_Id` fourni (ou, par défaut, chaque valeur distincte réellement présente sur
  `RT_AFFECTATION` via `GetDistinctDtIdsAffectationsAsync`) de correspondance trouvée ou
  d'`Orphelin` explicite. Groupement défensif par hash (collision `DeriveDtId` théoriquement
  possible, cf. TASK-028) — ne masque jamais un `DT_Id` ambigu.
- Endpoint `GET /api/diagnostic/dt-id?dtIds=...` (`DeclarationsController.cs`), réservé
  `UT_Admin=1` (même garde que réouverture/suppression, TASK-073/079). Strictement lecture seule :
  aucun write sur `RT_AFFECTATION`/`RT_MOUVEMENT`/`DM_ENTTVA` (garde-fou §Garde-fous #2 respecté).
- 2 méthodes ajoutées à `IDeclarationRepository`/`DeclarationRepository`
  (`GetToutesDeclarationsAsync`, `GetDistinctDtIdsAffectationsAsync`) + entité
  `DiagnosticDtIdResultat`. Les 5 fakes de test existants (`Task071/077/080/081/082...Tests.cs`)
  mis à jour en conséquence (méthodes non exercées → `NotImplementedException`, cohérent avec le
  patron déjà en place dans ces fichiers).
- Aucune modification de schéma, aucun trigger touché (garde-fou §Garde-fous #1/#3 respectés).

### Vérification

- Build solution complète (`DeclarationTVA.slnx`) : **0 erreur**.
- Tests : `Declaration.Orchestration.Tests` **112/112** (dont 4 nouveaux
  `Task094DiagnosticDtIdTests` — match trouvé / orphelin / périmètre par défaut / stabilité du
  hash), `Declaration.Core.Tests` 32/32, `Declaration.Export.Xml.Tests` 5/5,
  `Declaration.Export.Excel.Tests` 1/1. `Declaration.Selection.Tests` 55/56 et
  `Declaration.Controle.Tests` 1/2 : les 2 échecs sont des tests d'intégration **pré-existants**
  nécessitant une connexion SQL réelle non disponible dans cet environnement de test (échec
  d'authentification Windows sur `.\sql2022`, sans rapport avec cette task — aucun fichier touché
  par TASK-094 n'est dans ces 2 projets).
- **Preuve réelle sur `.\sql2022`/`GR_EMA_DISTRIBUTION`** (harnais console jetable, hors dépôt,
  supprimé après usage — même patron que TASK-080/094 : aucune trace laissée) : le diagnostic
  qualifie explicitement d'`Orphelin` les 4 des 5 valeurs `DT_Id` de l'incident 14/07/2026
  (`900191953`, `1538151731`, `425466408`, `1748146852`) — reproduit le critère de preuve exigé
  par la task (« l'outil les qualifie explicitement d'orphelins »).

### ⚠️ Constat significatif découvert pendant la vérification — à signaler au PO, non corrigé ici

Le recalcul réel (aujourd'hui, runtime `.NET 10`, **identique** à celui utilisé en production par
`Declaration.API`) donne, pour les 2 déclarations actuellement en base :
- `DeriveDtId(TVA1-2026-06.Id)` = **830393939** (et non `1748146852` comme noté dans
  `CHANGELOG.md` le 14/07/2026)
- `DeriveDtId(TVA1-2026-01.Id)` = **1767909464** (et non `425466408`)

Confirmé par un contrôle direct (`Guid.GetHashCode() & int.MaxValue` sur les `Id` lus en base,
sans passer par le diagnostic) — écarte tout bug de regroupement dans
`DiagnostiquerDtIdAsync`. Les deux `Id` (`615dd0be-...` pour `TVA1-2026-06`,
`e4d555cb-...` pour `TVA1-2026-01`) sont **identiques** à ceux présents en base au moment de
l'incident (même jour, aucune recréation de déclaration entre-temps).

Or **`830393939` est précisément l'une des 3 valeurs `DT_Id` traitées comme « orphelines » par le
correctif de données de l'incident** (`UPDATE RT_AFFECTATION SET DT_Id = NULL WHERE DT_Id IN
(830393939, 900191953, 1538151731)`, 10 lignes pour cette valeur précise). Le recalcul manuel fait
pendant l'investigation de l'incident (probablement via un outil/runtime différent — ex.
PowerShell Windows/.NET Framework, dont l'implémentation de `Guid.GetHashCode()` diverge de celle
de .NET Core/.NET 5+, différence documentée) semble avoir produit une valeur incorrecte pour
`TVA1-2026-06`, faisant passer à tort son propre tampon réel (10 lignes) pour un orphelin.

**Ce que ceci implique, sans que ce soit corrigé dans cette task** :
1. C'est exactement le type d'erreur que TASK-094 (Option A) est censée éliminer — recalcul
   manuel ad hoc remplacé par un outil reproductible tournant **dans le même runtime que
   l'application**, seule garantie de fiabilité (`Guid.GetHashCode()` n'est PAS documenté comme
   stable entre implémentations .NET Framework / .NET Core, bien qu'il soit stable et
   déterministe — non aléatoire — au sein d'une même version de runtime, condition nécessaire au
   fonctionnement de TASK-028 pose/retrait de tampon).
2. Les 10 lignes `RT_AFFECTATION` correspondant à `830393939` ont potentiellement été détamponnées
   à tort (elles appartenaient réellement à `TVA1-2026-06`, une déclaration `Cloturee`) —
   possible perte de traçabilité sur une déclaration close. Aucune ré-écriture n'a été effectuée
   par cette task (garde-fou §Garde-fous #2 : strictement lecture seule) — **décision PO requise**
   avant toute action corrective sur ces 10 lignes.
3. `900191953` (56 lignes) et `1538151731` (544 lignes) restent des orphelins **confirmés** même
   après ce recalcul correct — aucune déclaration existante ne les explique, cohérent avec
   l'anomalie déjà notée (3ᵉ déclaration disparue, non identifiable).
4. L'anomalie annexe déjà notée au §5 (« `TVA1-2026-06` ... ne porte nulle part son propre tampon
   réel ») est donc probablement **fausse** — invalidée par ce recalcul correct, elle mérite
   d'être re-vérifiée par le PO à la lumière de ce nouveau constat plutôt que traitée comme acquise.

Non traité ici par discipline de périmètre (garde-fou #4 de la task originale : pas de traitement
de l'anomalie annexe à la discrétion de l'implémenteur) — signalé pour décision PO, cf. `TODO.md`.

---

## Implémentation — Option B (demande PO explicite 14/07/2026, suite à la découverte ci-dessus)

Décision PO : ajouter la colonne persistée plutôt que de s'en tenir au recalcul seul. Périmètre
strictement additif sur `DM_ENTTVA` (garde-fou §Garde-fous #3), **jamais rétroactif** (déclarations
closes avant la migration : `DT_Id` reste `NULL`, seules les clôtures/réouvertures FUTURES le
posent/l'effacent).

- Migration `Declaration.Infrastructure/SQL/008_DM_ENTTVA_DT_Id.sql` (idempotente) : `DM_ENTTVA`
  gagne une colonne `DT_Id INT NULL`. **Appliquée réellement** sur `.\sql2022`/`GR_EMA_DISTRIBUTION`
  (rejouée deux fois pour confirmer l'idempotence).
- `DeclarationEntete.DT_Id` (nullable) ; `CloturerDeclarationAsync` pose la **même valeur**
  (`dtId`) que celle tamponnée sur `RT_AFFECTATION`, inconditionnellement (même sans affectation à
  tamponner) ; `ReouvriDeclarationAsync` l'efface inconditionnellement (symétrique). Nouvelle
  méthode `IDeclarationRepository.SetDtIdDeclarationAsync`.
- `DiagnostiquerDtIdAsync` (Option A) modifié pour donner la **priorité à la valeur persistée**
  (`d.DT_Id ?? DeriveDtId(d.Id)`) — immunise tout diagnostic futur contre exactement le type de
  divergence runtime constaté ci-dessus : une déclaration close APRÈS cette migration reste
  identifiable par sa valeur réellement posée, jamais par un recalcul potentiellement erroné.

### Bug trouvé et corrigé pendant la vérification (avant tout usage réel)

Propriété initialement nommée `DtId` (sans underscore) sur `DeclarationEntete` — Dapper ne
normalise **pas** les underscores par défaut dans le mapping POCO automatique (`SELECT * ...` vers
propriétés), contrairement à l'hypothèse implicite. La colonne SQL `DT_Id` ne matchait donc jamais
la propriété `DtId` : les écritures (`SetDtIdDeclarationAsync`) réussissaient silencieusement (0
exception) mais les lectures via `GetByIdAsync` renvoyaient toujours `NULL`, aucune régression
visible dans les tests unitaires (les fakes de test n'utilisent pas Dapper). **Découvert lors de la
preuve réelle** (harnais jetable, clôture/réouverture d'une déclaration test sur
`.\sql2022`/`GR_EMA_DISTRIBUTION`) — diagnostiqué par élimination (SQL brut idempotent ✓, ADO.NET
brut multi-connexions ✓, seule la combinaison Dapper+nommage `DtId` échouait). Corrigé en renommant
la propriété `DT_Id` (avec underscore, même convention que `LigneCandidate.EC_Id`/`MV_Id` déjà en
place dans ce fichier) — aucune autre entité affectée par ce piège dans le code existant.

### Vérification (Option B)

- Build solution complète : 0 erreur. `Declaration.Orchestration.Tests` **115/115** (3 nouveaux :
  pose à la clôture, effacement à la réouverture, priorité valeur persistée sur le recalcul —
  ce dernier reproduisant explicitement le scénario de divergence runtime).
- **Preuve réelle rejouée sur `.\sql2022`/`GR_EMA_DISTRIBUTION`** (harnais jetable, supprimé après
  usage, déclaration/ligne 100 % synthétiques — aucune donnée existante touchée) : création d'une
  déclaration test → clôture → `DM_ENTTVA.DT_Id` confirmé = `DeriveDtId` (concordance exacte,
  valeur réelle observée : `1819647816`) → réouverture → `DT_Id` confirmé effacé (`NULL`) → statut
  `EnCours` confirmé → suppression → environnement restauré à l'identique (0 résidu confirmé).
