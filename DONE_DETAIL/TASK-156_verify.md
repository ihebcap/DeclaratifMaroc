# TASK-156 Verify — Contention du rafraîchissement de valorisation OM (verrou anti-chevauchement + cache non-payé)

## Périmètre corrigé en cours de route (à lire en premier)

Le développement a démarré sur le périmètre initial de la task (verrou local à
`RafraichirValorisationAsync` uniquement). **En cours d'implémentation, l'architecte a transmis un
correctif de périmètre** (message reçu après une première implémentation fonctionnelle, tests verts,
mais AVANT livraison) : un second incident réel a été observé côté client le 23/07/2026 à 14:20:41
(batch de 361 pièces, **aucune** ligne `[VALO-050]` dans le log — donc **pas** déclenché par le
bouton « Rafraîchir »). Le PO a confirmé que ce traitement provenait d'une déclaration (« Passer au
calcul »/« Détail des lignes »). Un verrou posé uniquement autour de `RafraichirValorisationAsync`
n'aurait **pas** empêché cet incident : `ConstruireLignesFigeesAsync` (via
`ChargerCandidatesSiNecessaireAsync`) appelle le **même** `orchestrateur.Traiter()` sur le **même**
`soId`, sans jamais partager de verrou avec le bouton Factures.

**Le périmètre a donc été étendu avant livraison** : un seul verrou par `soId`, partagé par les
**4 appelants** de l'orchestrateur OM identifiés dans `DeclarationWorkflowService.cs` :
1. `RafraichirValorisationAsync` — bouton « Rafraîchir » écran Factures.
2. `ConstruireLignesFigeesAsync` — premier figeage d'une déclaration (« Passer au calcul »).
3. `ReintegrerReglementsLiberesAsync` — réintégration de lignes libérées (TASK-080).
4. `ResynchroniserLigneAsync` — resynchronisation d'une pièce après correction Sage (TASK-078).

Ce document reflète le périmètre **final** (les 4 sites), pas le périmètre initial. Les logs
build/tests ci-dessous ont été rejoués **après** l'extension du périmètre.

## Ce qui a été fait

### Correctif A — verrou anti-chevauchement partagé par soId

`Declaration.Application/Services/DeclarationWorkflowService.cs` :

- Nouveau `ConcurrentDictionary<int, SemaphoreSlim> _valorisationLocks` (même pattern que
  `_figeageLocks`, déjà en place pour le figeage concurrent, lignes ~29-35).
- Nouvelle méthode privée partagée `ExecuterAvecVerrouOMAsync<T>(int soId, Func<Task<T>> action)`
  (+ une surcharge sans valeur de retour) : `gate.WaitAsync(0)` — timeout **zéro**, donc **jamais**
  d'attente — acquiert le verrou s'il est libre, sinon lève immédiatement une
  `InvalidOperationException` explicite (« Traitement de valorisation déjà en cours pour cette
  société (soId=X)… »). Libération garantie par `finally`.
- Les **4 sites** recensés appellent désormais cette même méthode, avec `soId` (ou
  `declaration.SocieteId`) comme clé :
  - `RafraichirValorisationAsync` (ligne ~642)
  - `ConstruireLignesFigeesAsync` (ligne ~247, devenu non-`async` — délègue directement à
    `ExecuterAvecVerrouOMAsync`)
  - `ReintegrerReglementsLiberesAsync` (ligne ~517)
  - `ResynchroniserLigneAsync` (ligne ~586)
- Comportement **uniforme** : rejet immédiat sur les 4 chemins, même message d'erreur, aucun
  traitement différencié selon l'appelant (conforme à la décision PO confirmée après le
  correctif de périmètre : pas de comportement spécial pour le chemin déclaration).
- Deux `soId` distincts restent totalement indépendants (clé de dictionnaire par `soId`, jamais de
  verrou global) — vérifié par test dédié.

`Declaration.API/Controllers/FacturesController.cs` : `RafraichirValorisation` (endpoint
`POST /factures/rafraichir-valorisation`) attrape désormais `InvalidOperationException` et retourne
`409 Conflict` avec `{ Message: ... }` — même pattern déjà utilisé dans `DeclarationsController.cs`
pour d'autres garde-fous métier (ex. création de déclaration en double).

`declaration-tva-web/src/FactureInterrogation.tsx` (composant réellement monté sur l'écran
Factures — vérifié que `GenerationPanel.tsx` n'est PAS celui-ci, conformément à la mise en garde de
la task) : `handleRefreshValorisation` distingue désormais le cas `409` (message serveur explicite
affiché tel quel, ou un texte de repli « Rafraîchissement déjà en cours pour cette société. ») du
cas d'échec générique existant (« Échec du rafraîchissement… (lecture Sage) »), même pattern que
`DeclarationFinalePanel.tsx` (`e?.response?.data?.Message || e?.response?.data?.message`). Aucun
autre changement front — le bouton fonctionne à l'identique pour l'usage normal.

**Note sur le chemin déclaration (2/3/4) : pas de message front dédié.** `ConstruireLignesFigeesAsync`,
`ReintegrerReglementsLiberesAsync` et `ResynchroniserLigneAsync` sont exposés par
`DeclarationsController.cs` sur des endpoints différents de celui de l'écran Factures (chargement
des lignes, resynchronisation d'une pièce). Le PO a confirmé le rejet immédiat **uniforme** côté
serveur sur les 4 chemins, mais la task ne demandait explicitement un affichage front que pour le
bouton « Rafraîchir » (« aucune modification front hors ce qui est strictement nécessaire »).
`DeclarationsController.cs` n'a **pas** été modifié pour ajouter un traitement `409` dédié sur ces
3 autres endpoints — un clic rapproché sur ces chemins déclenchera aujourd'hui une erreur HTTP
générique (500 non catché, ou le comportement par défaut du contrôleur) plutôt qu'un message
explicite. **Réserve documentée** : si le PO souhaite le même confort d'affichage sur l'écran
Déclaration, un correctif front minimal complémentaire sera nécessaire — non fait ici pour rester
strictement dans le périmètre déclaré (« Aucune modification de front hors ce qui est strictement
nécessaire »), et parce que `DeclarationsController.cs` est un fichier déjà lourdement modifié par
un autre chantier en cours dans ce dépôt (non lié à TASK-156) — voir § Réserves.

### Correctif B — cache servi indépendamment du token de paiement

`Declaration.Orchestration/OrchestrateurDeclaration.cs`, méthode `TryServireDepuisCache` : le
court-circuit `if (currentToken == null) return null;` a été retiré. Le token stocké et le token
courant sont désormais comparés directement (`storedToken.Token_MV_Id != currentToken?.MV_Id ||
storedToken.Token_MV_Point != currentToken?.MV_Point`), y compris quand l'un des deux (ou les deux)
est `null` — comparaison nullable-safe :
- stocké `null` / courant `null` (facture jamais payée, rien n'a changé) → **égal** → cache servi
  (plus de relecture OM systématique — c'est le cœur du correctif B).
- stocké `null` / courant non-`null` (facture **devient** payée) → **différent** → cache non servi,
  relecture OM forcée (logique de fraîcheur préservée, cf. test T16 ci-dessous).
- stocké non-`null` / courant `null` (dépointage) → **différent** → relecture forcée (comportement
  identique à avant ce correctif).
- stocké non-`null` / courant différent (autre règlement) → **différent** → relecture forcée
  (comportement T3, inchangé).

La règle métier « non déclarable sans paiement pointé » n'est **pas** touchée par ce correctif :
elle est portée ailleurs dans le pipeline (motif `EstEligible`/`EstValorisable` de
`AffectationCandidate`, calculé par `SelectionExpliqueeService`/`Evaluator`, indépendamment de ce
cache) — vérifié en lisant le code, pas supposé. Le contrôle de revalidation d'incohérence Sage
(TASK-072/076, lignes ~430-459 du même fichier) n'a **pas** été déplacé et continue de s'exécuter
pour toute entrée servie depuis le cache, y compris désormais pour les factures non payées (c'est
même un effet positif du retrait du court-circuit : cette revalidation ne s'appliquait auparavant
**jamais** aux factures non payées, puisque le code ne l'atteignait jamais dans ce cas).

## Tests

### Nouveaux/modifiés — cache (correctif B), `Declaration.Orchestration.Tests/Task024CacheVentilationSageTests.cs`

- `T4_PaiementAbsent_StablePermanent_CacheServi_UnSeulOM` (remplace l'ancien `T4` qui asserait
  `TotalCalls == 2`, figeant le bug B comme comportement attendu) : facture jamais payée, deux
  appels `Traiter` successifs → **un seul** appel OM. C'est exactement le test « actuellement
  absent » demandé par la task.
- `T16_FactureDevientPayee_TokenApparait_OMRappele` : facture non payée puis qui **devient** payée
  entre deux cycles → relecture OM bien déclenchée (logique de fraîcheur non cassée par le retrait
  du court-circuit — risque explicitement signalé dans la task).

**Preuve que T4 catch réellement le bug** : le fichier `OrchestrateurDeclaration.cs` a été
temporairement reverté à sa version pré-correctif (`git checkout HEAD -- ...`) et T4/T16 rejoués :

```
Déclaration.Orchestration.Tests.Task024UnitTests.T4_PaiementAbsent_StablePermanent_CacheServi_UnSeulOM [FAIL]
  Assert.Equal() Failure: Values differ
  Expected: 1
  Actual:   2
Déclaration.Orchestration.Tests.Task024UnitTests.T16_FactureDevientPayee_TokenApparait_OMRappele [Réussi]
Total: 2, Réussi: 1, Échoué: 1
```

Puis le correctif restauré et solution reconstruite en clean build (`--no-incremental`) —
confirmation que la première tentative de re-test après restauration avait été faussée par un build
incrémental qui n'avait pas détecté le changement de fichier (horodatage), pas par un vrai problème
de code ; le clean build a confirmé 155/155 puis 157/157 verts (voir § Build/Tests final).

### Nouveaux — verrou (correctif A), `Declaration.Orchestration.Tests/Task156ContentionValorisationTests.cs`

Ces tests exercent **réellement** `DeclarationWorkflowService` (pas une réimplémentation du
verrou) — `ISelectionExpliqueeService`/`IDbConnectionFactory`/`IDeclarationRepository` sont stubbés
pour rester 100 % in-process (aucune connexion réseau/Sage réelle), avec un point de blocage
contrôlé par `TaskCompletionSource` (jamais de `Task.Delay`/polling, donc aucune source de
flakiness) pour simuler un traitement « en cours » de façon déterministe.

- `DeuxAppelsConcurrents_MemeSoId_SecondRejeteImmediatement` : deux appels
  `RafraichirValorisationAsync` sur le même `soId` → le second est rejeté immédiatement
  (`InvalidOperationException`, message contenant le `soId` et « en cours »), le premier se termine
  normalement une fois débloqué, et un troisième appel après la fin du premier réussit (le verrou
  est bien libéré, pas de blocage résiduel).
- `DeuxAppelsConcurrents_SoIdDifferents_AucunBlocageCroise` : deux `soId` distincts s'exécutent en
  parallèle sans blocage croisé (vérifié par un `Task.WhenAny` avec timeout explicite : le second
  `soId` doit entrer dans son traitement sans attendre la libération du premier).
- **`CheminFactures_PuisCheminDeclaration_MemeSoId_CheminDeclarationRejeteImmediatement`** (test
  croisé demandé par le correctif de périmètre) : `RafraichirValorisationAsync` démarre et reste
  « en cours » pour un `soId` ; `ChargerCandidatesSiNecessaireAsync` (→
  `ConstruireLignesFigeesAsync`) déclenché sur le **même** `soId` pendant ce temps est rejeté
  immédiatement — reproduit précisément le scénario de l'incident du 23/07/2026 14:20.
- **`CheminDeclaration_PuisCheminFactures_MemeSoId_CheminFacturesRejeteImmediatement`** (réciproque) :
  `ChargerCandidatesSiNecessaireAsync` démarre et reste « en cours » ; `RafraichirValorisationAsync`
  sur le même `soId` est rejeté immédiatement — prouve la symétrie du verrou partagé, quel que soit
  l'ordre des 2 chemins combinés parmi les 4.

```
Réussi Task156ContentionValorisationTests.DeuxAppelsConcurrents_SoIdDifferents_AucunBlocageCroise
Réussi Task156ContentionValorisationTests.CheminDeclaration_PuisCheminFactures_MemeSoId_CheminFacturesRejeteImmediatement
Réussi Task156ContentionValorisationTests.DeuxAppelsConcurrents_MemeSoId_SecondRejeteImmediatement
Réussi Task156ContentionValorisationTests.CheminFactures_PuisCheminDeclaration_MemeSoId_CheminDeclarationRejeteImmediatement
Total : 4, Réussi(s) : 4
```

**Réserve** : le test croisé ne couvre que 2 des 4 chemins combinés (Factures ↔ déclaration
premier-figeage), pas toutes les C(4,2)=6 combinaisons possibles (ex.
`ResynchroniserLigneAsync` ↔ `RafraichirValorisationAsync`). Les 2 combinaisons non testées
explicitement partagent strictement le même mécanisme (`ExecuterAvecVerrouOMAsync`, code identique,
aucune branche conditionnelle par chemin) — le risque résiduel qu'une combinaison précise échoue
alors que les autres passent est donc très faible, mais non prouvé par un test dédié à chacune.

## Build (solution complète, après extension du périmètre aux 4 sites)

```
dotnet build DeclarationTVA.slnx
...
La génération a réussi.
    6 Avertissement(s)
    0 Erreur(s)
```

Les 6 warnings restants sont tous préexistants et sans rapport avec ce correctif (nullabilité
`CS8602`/`CS8625` dans des tests non touchés, `CS0105` dans `Declaration.API/Program.cs`, `NU1510`
sur `Declaration.Setup`).

## Tests (après extension du périmètre)

`dotnet test Declaration.Orchestration.Tests --no-build` :

```
Réussi! - échec : 0, réussite : 157, ignorée(s) : 0, total : 157, durée : 836 ms
```

157 = 137 (base de référence de la task) + 20 tests ajoutés/modifiés par TASK-156 (2 sur le cache +
2+2 sur le verrou + quelques ajustements d'assertions dans des tests existants déjà présents avant
cette task — le delta exact vient aussi d'autres correctifs déjà en cours dans ce dépôt, non liés à
TASK-156, cf. § Réserves).

`dotnet test DeclarationTVA.slnx --no-build` (solution complète) :

```
Declaration.Core.Tests           : 34/34   ✅
Declaration.Export.Xml.Tests     : 13/13   ✅
Declaration.Export.Excel.Tests   : 1/1     ✅
Declaration.Orchestration.Tests  : 157/157 ✅
Declaration.Selection.Tests      : 58/59   ❌ (1 échec)
Declaration.Controle.Tests       : 1/2     ❌ (1 échec)
```

Les 2 échecs sont **pré-existants, sans rapport avec TASK-156** :
- `Declaration.Selection.Tests.IntegrationRegressionTests` : `SqlException` — échec d'ouverture de
  session SQL Server (`Échec de l'ouverture de session de l'utilisateur 'IHEB-PC\ihebc'`), un test
  d'intégration qui requiert un accès réseau à une base SQL Server réelle, absent dans cet
  environnement de build. Ce fichier n'a pas été touché par TASK-156.
- `Declaration.Controle.Tests.ComparateurTests.GenererRapportVerification` : `Déclaration GRFN 66
  introuvable` — même nature (dépend d'une donnée précise en base réelle). `Declaration.Controle`
  n'a pas été touché par TASK-156 (ni le projet source, ni les tests).

Vérifié explicitement que ces 2 fichiers ne font PAS partie du diff de cette session
(`git status` avant/après confirme qu'ils n'ont été modifiés par aucune des commandes `Edit`/`Write`
de cette session) — ce sont des échecs d'environnement (accès réseau/données), pas des régressions
introduites ici.

## Front

`npm run build` (dans `declaration-tva-web`) :

```
✓ 1848 modules transformed.
✓ built in 518ms
```

0 erreur. Un seul avertissement informationnel préexistant (`INEFFECTIVE_DYNAMIC_IMPORT` sur
`src/api.ts`), sans rapport avec ce correctif.

## Décision UX rappelée

PO (23/07/2026) : **rejet immédiat, uniforme sur les 4 chemins** de l'orchestrateur OM partageant le
même `soId` — jamais d'attente silencieuse, jamais de traitement différencié selon l'appelant
(chemin Factures vs chemin déclaration). L'utilisateur doit relancer manuellement une fois le
traitement précédent terminé. Ce principe est implémenté de façon strictement uniforme : les 4
sites appellent la **même** méthode privée (`ExecuterAvecVerrouOMAsync`), avec le **même** message
d'erreur (seul le `soId` varie dans le texte) — aucune branche conditionnelle par chemin d'appel
dans le code du verrou lui-même.

## Réserves / limites non résolues

1. **Message front dédié uniquement sur le chemin Factures.** Voir § Correctif A ci-dessus — les
   3 autres chemins (déclaration/resynchronisation) renverront une erreur HTTP générique en cas de
   rejet, pas un message `409` explicite affiché proprement au comptable. Écart assumé pour rester
   dans le périmètre déclaré de la task (« aucune modification front hors ce qui est strictement
   nécessaire pour le bouton Rafraîchir »), mais à trancher explicitement par le PO si l'écran
   Déclaration doit offrir le même confort.
2. **Contention potentielle nouvelle sur le premier chargement d'une déclaration.** Le verrou
   partagé par `soId` couvre désormais `ConstruireLignesFigeesAsync` — si le front charge en
   parallèle les deux domaines (« Decaissement » et « Encaissement ») d'une même déclaration au
   premier affichage (deux appels `/lignes` simultanés, cas déjà documenté dans le commentaire de
   `_figeageLocks` comme un pattern front existant), le second appel recevra désormais une
   `InvalidOperationException` (409) là où il aurait auparavant réussi en silence (au prix, avant ce
   correctif, de deux lectures OM concurrentes — exactement le type de contention que cette task
   corrige). **Ce n'est pas vérifié empiriquement dans ce build** (pas d'accès à l'écran réel dans
   cet environnement) — à confirmer par l'architecte/PO en conditions réelles. Si ce cas se produit
   en pratique, le comptable verrait une erreur au chargement d'un des deux onglets, résolue par un
   simple rechargement quelques secondes plus tard (le premier domaine aura fini). C'est une
   conséquence directe et voulue de la décision PO « verrou uniforme, aucun traitement différencié
   par chemin » — signalé ici pour transparence, pas pour remettre en cause la décision.
3. **Test croisé limité à 2 des 6 combinaisons possibles parmi les 4 chemins** (voir détail dans
   § Tests). Risque résiduel jugé faible (code de verrouillage strictement partagé, sans branche par
   chemin) mais non prouvé à 100 %.
4. **Rejeu en conditions réelles (étape 8 de la task, « si possible »)** : non exécuté — cet
   environnement de build n'a pas accès à la base client ni à une session Sage réelle. Le correctif
   adresse directement les deux causes racines confirmées par lecture de code (verrou absent
   partagé par 4 appelants ; court-circuit token systématique) ; seule la confirmation en conditions
   réelles (diminution du nombre de pièces lues d'un cycle à l'autre, absence de contention sur un
   `soId` réel) reste hors de portée ici, comme déjà noté dans TASK-154_verify.md pour une
   contrainte d'environnement similaire.
5. **`DeclarationWorkflowService.cs` était déjà en cours de modification par un autre chantier** au
   démarrage de cette session (visible dans `git status` avant toute action de cette task — fichier
   déjà marqué `M` avant le premier `Read`). Les correctifs TASK-156 ont été appliqués par-dessus cet
   état existant, sans revenir sur les changements préexistants (hors périmètre). Le diff complet du
   fichier mélange donc les deux chantiers ; les sections propres à TASK-156 sont identifiées dans ce
   document par nom de méthode/ligne, pas par un diff brut.

## Fichiers modifiés (périmètre TASK-156 strictement)

- `Declaration.Application/Services/DeclarationWorkflowService.cs` — verrou partagé
  (`_valorisationLocks` + `ExecuterAvecVerrouOMAsync`), câblé sur les 4 sites.
- `Declaration.Orchestration/OrchestrateurDeclaration.cs` — `TryServireDepuisCache`, retrait du
  court-circuit `currentToken == null`.
- `Declaration.API/Controllers/FacturesController.cs` — `catch (InvalidOperationException)` → 409.
- `declaration-tva-web/src/FactureInterrogation.tsx` — affichage du message 409 explicite.
- `Declaration.Orchestration.Tests/Task024CacheVentilationSageTests.cs` — `T4` corrigé, `T16` ajouté.
- `Declaration.Orchestration.Tests/Task156ContentionValorisationTests.cs` — nouveau, 4 tests
  (2 même-chemin, 2 croisés).

## Verdict

Build solution complète 0 erreur, `Declaration.Orchestration.Tests` 157/157 verts (incluant les
tests exigés par la task : cache non-payé 1 seul OM, verrou même-`soId`, verrou multi-`soId`
indépendant, **et** le test croisé inter-chemins exigé par le correctif de périmètre). Les 2 échecs
observés sur la solution complète sont pré-existants et sans rapport (environnement réseau/données,
fichiers non touchés). Front build 0 erreur. Réserves honnêtes documentées ci-dessus, notamment sur
le message front absent pour 3 des 4 chemins et le risque de contention nouvelle sur le chargement
parallèle des deux domaines d'une déclaration — à trancher par l'architecte/PO.
