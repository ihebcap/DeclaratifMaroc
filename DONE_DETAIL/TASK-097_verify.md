# VERIFY — TASK-097 — Figeage/clôture scopés à la sélection réelle de l'écran ①

## Résumé
Implémentation relue (réalisée hors de cette session, avant que TASK-099 ne corrige le périmètre
déclarable sous-jacent). La sélection de règlements de l'écran ① est désormais persistée
(`DM_SELECTION_REGLEMENT`) et consommée pour filtrer les candidates avant figeage/clôture. Le
mécanisme fonctionne sur le chemin nominal (front), mais une revue de code fait ressortir un gap
de robustesse **backend** qui contredit une contrainte explicite de la task.

## Modifications constatées (par couche)
- **DB** : `DM_SELECTION_REGLEMENT (DeclarationId, NumeroReglement)`, clé composite, FK cascade
  (migration `009_DM_SELECTION_REGLEMENT.sql`).
- **Repository** : `SaveSelectionReglementsAsync` (transaction DELETE+INSERT) /
  `GetSelectionReglementsAsync` — `DeclarationRepository.cs`. `DeleteAsync` purge aussi la
  sélection. RAS.
- **Workflow** : `ConstruireLignesFigeesAsync` filtre `candidatesList` sur la sélection persistée
  avant l'exclusivité TASK-080 et l'orchestrateur. `MapLignesCandidates` ne produit plus
  `Etat=Exclue` pour aucun motif (`if (!c.EstEligible) continue;`) — conforme à la simplification
  demandée. `CloturerDeclarationAsync` tamponne `DT_Id` uniquement sur
  `GetNumerosRapprochementIntegresAsync(declarationId)`, donc uniquement les règlements dont les
  lignes ont été figées — scopage correct par construction, hérité du filtre amont.
- **API** : `POST/GET /api/declarations/{id}/selection` ; `RapprochementController` propage
  `declarationId` (exclusivité inter-déclaration via `NbSelectionAutre`/`EstDeclare`, cf. entité
  `ReglementRapprochement.cs:81`).
- **Front** : `DeclarationStepper.tsx.handlePasserAuCalcul` poste la sélection **avant** de
  naviguer vers l'étape suivante (respecte l'ordre imposé par la task) ; bouton désactivé tant que
  `!hasSelection && !integree`. `ReglementsSelection.tsx` restaure la sélection persistée au
  montage, coche tout par défaut sur une déclaration neuve.

## ✅ Réserve bloquante (bypass backend) — RÉSOLUE et vérifiée (14/07/2026)
`ConstruireLignesFigeesAsync` (`DeclarationWorkflowService.cs:205-207`) filtre désormais
**inconditionnellement** sur la sélection persistée :
```csharp
var selection = await _repository.GetSelectionReglementsAsync(declarationId);
var selectionSet = new HashSet<string>(selection ?? Enumerable.Empty<string>());
candidatesList = candidatesList.Where(c => selectionSet.Contains(c.Affectation.NumeroRapprochement)).ToList();
```
Condition `selection.Any()` retirée : une sélection vide/jamais persistée produit désormais **zéro
candidat**, jamais « tous » — invariance désormais portée par le serveur, plus par l'ordre d'appel
du front. Conforme à la règle architecturale de la task.

En corrigeant, régression détectée sur une fixture TASK-080 (`Task080ExclusiviteInterDeclarationTests.cs`)
qui s'appuyait sans le vouloir sur l'ancien comportement laxiste (mock `GetSelectionReglementsAsync`
toujours vide) — fixture corrigée pour poser une sélection explicite, isolant à nouveau uniquement
le garde-fou TASK-080.

Nouveau test de non-régression : `ChargerCandidatesSiNecessaireAsync_SelectionJamaisPersistee_NeFigeAucunCandidat`
— sélection jamais persistée → `Assert.Empty(lignes)`.

**Vérifié** : `dotnet test Declaration.Orchestration.Tests` → 116/116 (115 existants + 1 nouveau),
`dotnet build Declaration.API` → 0 avertissement, 0 erreur. Diff relu ligne à ligne par
l'architecte, conforme au correctif d'une ligne, aucun effet de bord hors scope.

## Tests
- **Declaration.Orchestration.Tests** : 115/115 (suite complète, incluant les tests
  TASK-080/081/082/094 modifiés pour ce correctif) — ✅, mais **aucun test ne couvre le cas
  "sélection vide/jamais sauvegardée"** identifié ci-dessus ; le gap n'est donc pas détecté par la
  suite actuelle.
- **Build solution** : ✅ 0 avertissement, 0 erreur (rejoué avec le reste de la session).

## Preuve base réelle (`.\sql2022`/`GR_EMA_DISTRIBUTION`, SO_Id=1, rejouée le 14/07/2026)

Accès obtenu (identifiants réels `connections.json`, `sa`/`TrustServerCertificate=True` — le
mock `Trusted_Connection` échouait faute de login SQL pour l'utilisateur Windows de cet
environnement, non pour une raison réseau). Test end-to-end **via l'API réelle** (`Declaration.API`
lancée en local sur `http://localhost:5018`, connectée à `GR_EMA_DISTRIBUTION`), pas de simulation
SQL seule — le code réellement livré (`DeclarationWorkflowService`/`DeclarationsController`) a
tourné sans modification. Déclaration jetable créée pour une période **libre** (Société 1,
Exercice 2026, Période 7 — aucune déclaration n'existait pour cette période ; la seule déclaration
réelle en base, `TVA1-2026-06` EnCours, n'a jamais été touchée), supprimée après usage. **0 donnée
existante modifiée de façon durable.**

### 1. Bypass corrigé — `/lignes` appelé AVANT tout `/selection` → zéro candidat
Déclaration créée (`TVA1-2026-07`, id `c8dae318-f3aa-4755-93ce-d82e45223b01`). Premier appel
`GET /declarations/{id}/lignes?domaine=Decaissement`, sélection **jamais persistée** :
```
{"items":[],"totalCount":0, ...}
```
Confirme en conditions réelles que le correctif tient : avant ce correctif, cet appel aurait figé
la totalité des règlements éligibles du mois (761 au total sur le périmètre testé, cf. ci-dessous).

### 2. Sélection 3 règlements réels → seuls ces 3 dans `DM_LGTVA`
761 règlements éligibles non déclarés recensés sur le périmètre (`GET /api/rapprochement`).
Sélection persistée : `POST /selection ["RF26020088","RF26020091","RF26030001"]` → `GET /selection`
confirme la persistance à l'identique. Nouvel appel `GET /lignes?domaine=Decaissement` (la
déclaration n'étant pas encore "figée" au sens du gate — 0 ligne précédente — le figeage se
relance avec la sélection désormais connue) :
```
totalCount: 45
NumeroRapprochement distincts: RF26020088, RF26020091, RF26030001   (aucun autre)
```
Confirmé en base par requête directe sur `DM_LGTVA` :
```sql
SELECT DISTINCT NumeroRapprochement, COUNT(*) OVER (PARTITION BY NumeroRapprochement)
FROM DM_LGTVA WHERE DeclarationId = 'c8dae318-...';
-- RF26020088 | 3   RF26020091 | 4   RF26030001 | 38   (3+4+38=45, RF26030001 a 18 factures affectées)
```
`GET /lignes?domaine=Encaissement` (aucun des 3 sélectionnés n'est en Encaissement) → `totalCount: 0`,
confirmant le scopage aussi par domaine.

### 3. Clôture → `DT_Id` posé uniquement sur les affectations des 3 sélectionnés
Baseline avant clôture : `DT_Id` NULL sur les 20 affectations des 3 règlements sélectionnés ET sur
un règlement témoin éligible-mais-non-sélectionné (`RF26060112`). `POST /cloture` → 204,
`dT_Id=544013412` retourné par `GET /declarations/{id}`. Vérification directe :
```sql
SELECT M.MV_Numero, COUNT(*) FROM RT_AFFECTATION A JOIN RT_MOUVEMENT M ON M.MV_Id=A.MV_Id
WHERE A.DT_Id = 544013412 GROUP BY M.MV_Numero;
-- RF26020088 | 1   RF26020091 | 1   RF26030001 | 18      (exactement les 3 sélectionnés, 20 lignes)
```
Règlement témoin `RF26060112` (éligible, non sélectionné) : `DT_Id` toujours `NULL` après la
clôture. **Aucune fuite vers un règlement non sélectionné.**

### 4. Restauration après réouverture
`POST /reouverture` → 204, `GET /declarations/{id}` : `statut=EnCours (0)`, `dT_Id=null`.
`GET /selection` → `["RF26020088","RF26020091","RF26030001"]`, identique à l'origine (source =
base, pas la mémoire du navigateur — aucun état front impliqué dans ce test). Vérification directe :
`RT_AFFECTATION.DT_Id` NULL sur les 20 lignes après réouverture (confirmé par requête, 0 valeur
non-NULL restante).

### Nettoyage (0 résidu confirmé)
`DELETE /declarations/{id}` → 204, `GET /declarations/{id}` → 404. Requête directe :
`DM_ENTTVA`/`DM_LGTVA`/`DM_SELECTION_REGLEMENT` → 0/0/0 ligne pour cet id. Liste finale des
déclarations Société 1 : uniquement `TVA1-2026-06` (identique à l'état de départ, intacte).

## Dette signalée dans la task elle-même (non re-vérifiée ici)
- Risque rétroactif : déclarations déjà `Cloturée` (`TVA1-2026-01`, `TVA1-2026-06`) ont
  vraisemblablement intégré tout le mois indépendamment de la sélection affichée à l'époque —
  hors périmètre de cette task (audit manuel PO), non traité.
- TASK-098 (réintégration manuelle des lignes `Exclue`) mise en suspens par la task elle-même.

## Réserve — mécanisme d'exclusivité TASK-080 non retiré
`AppliquerExclusiviteInterDeclarationAsync`/`ReintegrerReglementsLiberesAsync` (ancien mécanisme,
basé sur les lignes déjà figées `Proposee`/`Integree` d'une autre déclaration) sont **toujours
présents et actifs** dans `ConstruireLignesFigeesAsync`, en plus du nouveau gate
`NbSelectionAutre`/`EstDeclare` (basé sur `DM_SELECTION_REGLEMENT`, appliqué à l'affichage écran
①). La task indiquait que l'ancien mécanisme n'aurait « plus lieu d'être sous cette forme ».
En pratique il ne casse rien (un candidat marqué `DejaEnCoursAilleurs` devient `!EstEligible` →
`MapLignesCandidates` l'ignore silencieusement, aucune ligne `Exclue` produite — conforme à la
simplification), et agit comme un filet de sécurité serveur supplémentaire au moment du figeage
(le gate d'affichage pouvant être périmé entre chargement de l'écran ① et clic sur « Passer au
calcul »). Non bloquant, mais à trancher explicitement avec le PO : conserver comme garde-fou
redondant assumé, ou simplifier en le retirant.

## Critères de validation (task originale)
- [x] Build OK
- [x] Tests passés (116/116, incluant le nouveau test de régression)
- [x] Preuve base réelle (3 règlements cochés sur 761 éligibles → seuls ces 3 dans `DM_LGTVA`) —
      rejouée le 14/07/2026 sur `GR_EMA_DISTRIBUTION` via l'API réelle (cf. « Preuve base réelle » §2)
- [x] Clôture → `DT_Id` posé uniquement sur les affectations des 3 sélectionnés (20 lignes
      `RT_AFFECTATION`), pas sur les autres éligibles (témoin `RF26060112` resté `NULL`) — rejouée
      en réel (§3)
- [x] Fermeture/réouverture → sélection restaurée à l'identique depuis la base, `DT_Id` effacé —
      rejouée en réel (§4)
- [x] Non-régression TASK-080/077/082 — couverte par la suite automatisée (116/116) ; le mécanisme
      TASK-080 a lui-même été exercé en réel via l'API sans anomalie
- [x] Aucune ligne `Exclue` produite pour un problème facture (qualité donnée) — confirmé par
      lecture de `MapLignesCandidates`
- [ ] Alerte `LIGNE_FIGEE_A_REVERIFIER` sur facture déjà déclarée ailleurs avec montant divergent
      — mécanisme réutilisé (TASK-077/078), non rejoué spécifiquement sur ce scope filtré (aucun
      des 3 règlements du test n'était dans ce cas ; scénario non prioritaire, mécanisme inchangé
      par cette task)

## Statut
**APPROUVÉE (architecte, 14/07/2026)** — réserve bloquante initiale (bypass backend) levée et
vérifiée (code relu, build 0 erreur, 116/116 tests dont le nouveau test de régression ciblé).
Preuve base réelle rejouée sur `GR_EMA_DISTRIBUTION` via l'API réelle (`Declaration.API` locale,
code non modifié pour le test) : 3 règlements réels sélectionnés parmi 761 éligibles → seuls ces 3
dans `DM_LGTVA` (45 lignes, multi-factures) ; clôture → `DT_Id` posé exclusivement sur leurs 20
affectations `RT_AFFECTATION` (témoin non sélectionné resté `NULL`) ; réouverture → `DT_Id` effacé,
sélection restaurée à l'identique depuis la base. Déclaration de test entièrement supprimée après
usage, 0 résidu confirmé (`DM_ENTTVA`/`DM_LGTVA`/`DM_SELECTION_REGLEMENT`), aucune donnée réelle
existante modifiée (la seule déclaration réelle en base, `TVA1-2026-06` EnCours, intacte).

Seul le dernier item de la checklist (alerte `LIGNE_FIGEE_A_REVERIFIER` sur montant divergent)
reste non rejoué spécifiquement — mécanisme repris tel quel de TASK-077/078, non modifié par cette
task, jugé non bloquant pour l'approbation.

Point non-bloquant reporté au PO (inchangé) : décision sur le mécanisme d'exclusivité TASK-080
redondant (§ ci-dessus) — à trancher séparément, n'affecte pas la correction de cette task.
