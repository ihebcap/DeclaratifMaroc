# TASK-175 Verify — 500 au chargement des lignes (`GET {id}/lignes`) quand les deux domaines se chargent en parallèle

Date : 2026-07-24
Agent : Développeur (Claude, autonome, worker exceptionnel)

## Périmètre livré

1. **Backend — `DeclarationsController.GetLignes`** (`Declaration.API/Controllers/DeclarationsController.cs:113-156`) :
   ajout d'un `try/catch (InvalidOperationException ex) → Conflict(new { Message = ex.Message })`
   autour de tout le corps de la méthode, même pattern déjà en place sur `Resynchroniser`
   (ligne ~285). Le rejet du verrou anti-chevauchement `soId` (TASK-156,
   `ExecuterAvecVerrouOMAsync`) redevient un **409** propre sur ce chemin, jamais un 500.

2. **Découverte §2/§6 de la TASK — `GetCheckup` avait la même lacune** : `GetCheckupAsync`
   (`DeclarationWorkflowService.cs:985`) revalide lui aussi les deux domaines
   (`RevaliderLignesFigeesAsync`, lignes ~1138-1139), ce qui peut invoquer
   `ReintegrerReglementsLiberesAsync` → `ExecuterAvecVerrouOMAsync` et lever la même
   `InvalidOperationException`. Le contrôleur `GetCheckup` (`DeclarationsController.cs:357-522`)
   ne catchait que `ArgumentException`, jamais `InvalidOperationException` → même 500 générique
   possible sur ce chemin. **Corrigé avec le même pattern** (ajout d'un second `catch`,
   `Conflict(new { Message = ex.Message })`), bien que ce ne soit pas littéralement listé dans le
   périmètre `Files` de la TASK (justifié par l'instruction explicite du point §6 : « si tu trouves
   la même lacune exposée par un contrôleur, corrige-la »).

   `ReintegrerReglementsLiberesAsync` elle-même n'est appelée par **aucun** autre contrôleur
   directement (recherche exhaustive dans `Declaration.API` : aucune référence directe) — elle
   n'est atteinte qu'à travers `ChargerCandidatesSiNecessaireAsync` (`GetLignes`, corrigé au point 1)
   et `RevaliderLignesFigeesAsync` appelée depuis `GetCheckupAsync` (`GetCheckup`, corrigé au point 2).
   Les deux chemins d'exposition réels sont donc couverts ; aucun 3ᵉ endpoint à corriger.

3. **Front — `VerifierIntegrerPanel.tsx`** :
   - Nouveau helper `est409VerrouSocieteEnCours(e)` (détecte `e.response.status === 409`) et
     `getAvecRetrySiVerrouPris(requete)` — un seul retry automatique après 1500 ms en cas de 409,
     jamais de file d'attente silencieuse (conforme à la décision PO citée par la TASK).
   - `fetchAllLignes` : chaque appel `GET {id}/lignes` (boucle domaines Decaissement/Encaissement ×
     pagination) passe désormais par ce helper.
   - Catch du chargement des lignes (`useEffect` principal) : message spécifique si 409 restant
     après retry (« Un autre traitement est en cours pour cette société, réessayez dans quelques
     secondes. », toast `warning`) au lieu du message générique `Erreur lors du chargement de la
     valorisation`.
   - **Extension découverte en cours d'implémentation** (même fichier, même mécanisme, non listée
     explicitement dans le périmètre `Files` mais directement liée à la découverte du point 2
     ci-dessus) : le fetch `GET {id}/checkup` (2ᵉ `useEffect` du même composant) est exposé à la
     même faille — désormais protégé par le même `getAvecRetrySiVerrouPris` + même message 409
     explicite, plutôt que de laisser un chemin non couvert dans le même écran.

## Fichiers modifiés

- `Declaration.API/Controllers/DeclarationsController.cs` — `GetLignes` (try/catch ajouté) +
  `GetCheckup` (catch `InvalidOperationException` ajouté, découverte §2/§6).
- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` — helper retry 409 + application sur le
  chargement des lignes et sur le chargement du checkup.
- `Declaration.Orchestration.Tests/Task175ConflitVerrouGetLignesTests.cs` (nouveau) — test
  d'intégration réel (voir § Reproduction ci-dessous).

Aucun autre fichier du périmètre `Files` de la TASK modifié.
`DeclarationWorkflowService.cs` (`ExecuterAvecVerrouOMAsync`) : **non modifié**, conforme au
garde-fou de la TASK (référence uniquement).

## Reproduction réelle de la course (obligatoire, pas seulement relecture de code)

Un vrai serveur HTTP + une vraie déclaration de production n'étaient pas disponibles dans cet
environnement d'exécution (pas d'accès réseau au serveur applicatif du client). La reproduction a
donc été faite au niveau le plus proche possible sans mock du mécanisme lui-même : un test xUnit
qui exerce le **vrai** `DeclarationsController.GetLignes` et le **vrai**
`DeclarationWorkflowService` (aucune réimplémentation du verrou), en réutilisant le même dispositif
déterministe (`TaskCompletionSource`, sans `Task.Delay`/polling) déjà éprouvé et approuvé par
l'architecte pour TASK-156 (`Task156ContentionValorisationTests.cs`) :

- `Declaration.Orchestration.Tests/Task175ConflitVerrouGetLignesTests.cs` —
  `DeuxAppelsConcurrents_GetLignes_MemeSoId_DomainesDifferents_SecondRecoit409PasException` :
  1. Premier appel `controller.GetLignes(declarationId, "Decaissement")` démarre le figeage et
     reste bloqué « en cours » (verrou `soId` tenu) via `BlockingSelectionService`.
  2. Second appel **concurrent** `controller.GetLignes(declarationId, "Encaissement")` — même
     déclaration donc même `soId`, domaine différent (donc pas bloqué par le verrou de figeage
     `_figeageLocks`, qui est lui par `(declarationId, domaine)` — seul le verrou `soId` partagé
     est en jeu, exactement le scénario du PO).
  3. Assertion : le second appel retourne un `ConflictObjectResult` (409), **pas** une exception
     non gérée.
  4. Libération du premier → il aboutit normalement (`OkObjectResult`).

- **Contre-preuve exécutée puis annulée** (validation que le test détecte bien la régression) :
  remplacement temporaire de `catch (InvalidOperationException ex)` par `catch (FormatException ex)`
  dans `GetLignes` → le test échoue immédiatement avec
  `System.InvalidOperationException : Traitement de valorisation déjà en cours pour cette société
  (soId=375)...` remontant **non catchée** jusqu'au test, stack trace incluant
  `DeclarationsController.GetLignes` → reproduit fidèlement le 500 générique signalé en
  production. Le fichier a été restauré immédiatement après (diff vérifié nul par rapport à
  l'état corrigé).

- Résultat après restauration du correctif : `dotnet test
  Declaration.Orchestration.Tests/Declaration.Orchestration.Tests.csproj --filter
  "FullyQualifiedName~Task175|FullyQualifiedName~Task156"` → **5/5 réussis** (1 nouveau test +
  4 tests TASK-156 existants, non-régression confirmée sur le même mécanisme de verrou).

## Checklist

- [x] Build back (`dotnet build DeclarationTVA.slnx`) OK — 0 erreur (3 avertissements
      préexistants, non liés).
- [x] Build front (`npx tsc -b` OK, `npx vite build` OK).
- [x] Test réel (niveau contrôleur+service, sans mock du verrou) : le second appel concurrent
      (autre domaine, même `soId`) reçoit un **409** (`ConflictObjectResult`), jamais une
      exception non gérée — reproduit et contre-prouvé (voir § Reproduction).
- [x] Vérifié qu'un rejet de verrou renvoie bien 409 (pas 500) sur `GetLignes` — test automatisé
      ci-dessus, exécuté avec succès.
- [x] Non-régression TASK-156 : les 4 tests `Task156ContentionValorisationTests` passent toujours
      (rejet immédiat, jamais de file d'attente silencieuse) — `dotnet test` rejoué, 5/5.
- [x] Non-régression sur `Resynchroniser` : fichier non modifié à cet endroit (aucune duplication,
      aucune casse).
- [x] Aucun bypass sécurité, aucune écriture SQL inline ajoutée (aucune modification de couche
      données dans cette task).
- [x] Aucun secret codé en dur.
- [ ] **Non vérifié en conditions réelles PO** (voir § Reste à valider) : test manuel « deux
      onglets », test sur base de production, disparition effective du 500 « sur des dizaines de
      tentatives » réelles.

## Reste à valider (NON couvert par ce VERIFY)

1. **Validation manuelle en environnement réel** (écran ③ Vérifier & Intégrer, base de production
   ou au moins une base de test avec une vraie déclaration non figée) : ouvrir l'écran et confirmer
   qu'aucun 500 n'apparaît plus sur des dizaines de tentatives, et que le retry front (1500 ms)
   est bien invisible pour l'utilisateur dans le cas nominal. Nécessite un environnement
   d'exécution avec accès réseau au serveur applicatif — non disponible dans cette session
   (uniquement accès direct SQL Server via `sqlcmd`, pas d'accès HTTP au service
   `Declaration.API` en cours d'exécution). Le test automatisé §Reproduction couvre le mécanisme
   exact au niveau service+contrôleur (contre-preuve incluse), mais pas le comportement front réel
   dans un navigateur (timing React, `Promise.all`/effets concurrents tels qu'observés dans la
   stack trace originale du PO).
2. Le message front (« Un autre traitement est en cours pour cette société, réessayez dans quelques
   secondes. ») n'a pas été relu/validé par le PO — décision de libellé prise par le développeur en
   suivant le texte proposé littéralement par la TASK (§ Objectif point 3), pas une improvisation,
   mais reste à confirmer si le PO souhaite un libellé différent.
3. Le délai de retry (1500 ms) est un choix raisonnable mais arbitraire (pas de valeur imposée par
   la TASK) — à ajuster si le PO observe en pratique que le premier appel met plus longtemps à
   libérer le verrou (figeage complet avec lecture OM réelle, potentiellement plusieurs secondes
   sur un gros volume).

## Notes développeur

- Le fichier TASK a été déplacé de `TASKS/` vers `IN_PROGRESS/` en tout début de session (le
  fichier n'était pas suivi par `git` — déplacement fait par `mv`, pas `git mv`).
- Aucun accès à un serveur `Declaration.API` réellement démarré ni à une base de production n'était
  disponible dans cet environnement (accès confirmé uniquement à `sqlcmd` contre
  `DESKTOP-5BFKKEP`) — la reproduction de la course a donc été faite au niveau test d'intégration
  in-process (service + contrôleur réels, dépendances externes stubbées), pas via deux requêtes
  HTTP concurrentes contre un serveur réellement démarré. C'est le niveau de preuve le plus élevé
  atteignable dans cet environnement, et il exerce le VRAI code de production (aucune
  réimplémentation du verrou) — mais reste un cran en dessous de la vérification manuelle « deux
  onglets » demandée explicitement par la TASK, d'où le point 1 de « Reste à valider » ci-dessus,
  non déclaré satisfait.
- Racine du problème confirmée : `_valorisationLocks` est bien clé par `soId` uniquement (pas par
  domaine), tandis que `_figeageLocks` (verrou de figeage, différent) est clé par
  `(declarationId, domaine)` — c'est ce qui permet à deux domaines de la MÊME déclaration de
  progresser en parallèle jusqu'au verrou `soId` partagé, où l'un des deux est nécessairement
  rejeté. Comportement voulu (TASK-156), seul le traitement de l'exception manquait.
