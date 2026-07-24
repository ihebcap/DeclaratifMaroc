# TASK-173 Verify — Affectation en masse du code activité (sélection multi-lignes par filtres)

> Dépend de TASK-172 (livrée dans le même lot, cf. `VERIFY/TASK-172_verify.md`) — le sélecteur de code
> activité de l'action de masse réutilise le même référentiel filtré par domaine.

## Correctif suite au rejet architecte (24/07/2026)

**Rejet reçu** : le chemin `LigneIds` (`GetDomainesDistinctsLignesAsync`/`UpdateCodeActiviteBulkByIdsAsync`)
n'était pas scopé par `DeclarationId` — seul `WHERE Id IN (...)` filtrait. Un ligneId appartenant à une
**autre** déclaration (y compris `Clôturée`) était donc pris en compte pour déterminer le domaine effectif
et **écrit** par le bulk, sans jamais passer par le garde-fou de clôture (qui ne vérifie que
`declaration.Statut` de la déclaration de l'URL). C'était exactement le trou que ce nouvel endpoint dédié
devait éviter, et il n'était pas couvert par les 4 réserves déjà documentées ci-dessous.

**Correction appliquée** :
- `IDeclarationRepository.GetDomainesDistinctsLignesAsync` et `.UpdateCodeActiviteBulkByIdsAsync` prennent
  désormais un premier paramètre `Guid declarationId` explicite.
- `DeclarationRepository` : les deux requêtes SQL ajoutent `AND DeclarationId = @DeclarationId` — un
  ligneId d'une autre déclaration ne compte plus dans le calcul du domaine effectif et n'est plus jamais
  écrit par l'`UPDATE`, quel que soit son statut de clôture.
- `DeclarationWorkflowService.ModifierCodeActiviteLignesBulkAsync` passe `declarationId` (celui de l'URL,
  déjà validé non-`Cloturee` par la garde existante) aux deux appels.
- Les 16 fakes `IDeclarationRepository` des tests ont été mis à jour ; `FakeDeclarationRepositoryTask161`
  (le seul avec une implémentation réelle, pas un stub) filtre désormais lui aussi par
  `l.DeclarationId == declarationId`, cohérent avec les autres méthodes du même fake déjà scopées ainsi
  (`GetLignesAsync`/`CountLignesAsync`/`UpdateCodeActiviteBulkAsync`).
- **2 nouveaux tests de non-régression** ajoutés dans `Task161CodeActiviteCascadeTests.cs` reproduisant
  exactement le cas adverse signalé par l'architecte : `LigneIds` contenant l'Id d'une ligne d'une
  déclaration `Clôturée` distincte, appelé avec l'Id de la déclaration `EnCours` dans l'URL —
  `ModifierCodeActiviteLignesBulkAsync_LigneIdsDeclarationClotureeMalicieuse_AucuneEcritureNiErreur`
  (aucune écriture, aucune exception — même repli que « aucune ligne trouvée ») et
  `ModifierCodeActiviteLignesBulkAsync_SelectionMelangeeDeclarations_NeModifieQueLesLignesDeLUrl`
  (sélection mixte propriétaire+étrangère : seule la ligne de la déclaration de l'URL est modifiée).
  Ce test unitaire remplace la vérification en base réelle demandée (« rejouer le test de clôture sur le
  chemin LigneIds ») — comme documenté en réserve #2 avant correctif, aucune déclaration `Clôturée`
  n'existe sur la base de ce poste pour une vérification en conditions réelles ; le test unitaire
  reproduit fidèlement le scénario (deux déclarations distinctes, l'une `Cloturee`, l'autre `EnCours`,
  lignes réparties entre les deux).
- `dotnet build` : 0 erreur. `dotnet test` : `Declaration.Orchestration.Tests` **179/179** (177 + 2
  nouveaux), mêmes 2 échecs préexistants sans rapport (`Declaration.Selection.Tests`,
  `Declaration.Controle.Tests`, cf. section Tests ci-dessous, inchangés).

## Périmètre livré

1. **`IDeclarationRepository`** (`Declaration.Application/Interfaces/IDeclarationRepository.cs`) : 5
   nouvelles méthodes — `GetDomaineCodeActiviteAsync`/`GetDomaineLigneAsync` (partagées avec TASK-172,
   §4), `GetDomainesDistinctsLignesAsync`, `UpdateCodeActiviteBulkByIdsAsync`,
   `UpdateCodeActiviteBulkAsync`.
2. **`DeclarationRepository`** : `UpdateCodeActiviteBulkByIdsAsync` (UPDATE batch `WHERE DeclarationId=@...
   AND Id IN (...)` — scope `DeclarationId` ajouté par le correctif ci-dessus) et
   `UpdateCodeActiviteBulkAsync` (UPDATE batch `WHERE DeclarationId=@... AND Domaine=@... [AND
   filtre facture/tiers]`) — **écriture SQL batch unique dans les deux cas, aucune boucle
   applicative**, patron strictement calqué sur `UpdateLignesEtatBulkByIdsAsync`/
   `UpdateLignesEtatBulkAsync` (TASK-012) déjà en place pour l'État (à la différence, après correctif,
   que le chemin par IDs est désormais scopé par déclaration — contrairement à son homologue État, cf.
   réserve). `GetDomainesDistinctsLignesAsync` fait un `SELECT DISTINCT Domaine ... WHERE
   DeclarationId=@... AND Id IN (...)` sur `DM_LGTVA`.
3. **`DeclarationWorkflowService.ModifierCodeActiviteLignesBulkAsync`** : même garde de clôture que le
   PATCH unitaire (`InvalidOperationException` → 409, reproduite explicitement, pas de raccourci par le
   contrôleur direct-vers-repository comme le fait l'`:bulk` État existant — cf. réserve ci-dessous) ;
   résout le domaine effectif (soit par les `LigneIds` — en rejetant explicitement une sélection mixte
   Encaissement/Décaissement, soit directement via le paramètre `Domaine`) ; réutilise
   **`ValiderDomaineCodeActiviteAsync`** (introduite par TASK-172 §4) pour vérifier que le code choisi
   correspond au domaine résolu, **aucune logique de validation dupliquée** entre unitaire et masse.
4. **`POST {id}/lignes/code-activite:bulk`** (`DeclarationsController.cs`) : même contrat de sélection
   que `POST {id}/lignes:bulk` existant (`LigneIds` **ou** `Domaine`+`Filter`), + `CodeActivite`. 400 si
   ni l'un ni l'autre n'est renseigné (même garde que l'existant), 404/409/400 traduits depuis
   `ArgumentException`/`InvalidOperationException`/`ApplicationException`.
5. **Front** (`DomainGrid.tsx`) : nouveau contrôle « Affecter un code activité… » (select + bouton)
   dans la barre d'outils, **visible uniquement si `codeActiviteOptions` est fourni** (donc uniquement
   depuis le drill « Codes activité » de `VerifierIntegrerPanel.tsx` — jamais sur les 5 autres écrans
   partageant `DomainGrid`) et qu'au moins une ligne est sélectionnée. Réutilise **exactement** l'état
   de sélection déjà existant (`selectedIds`/`selectAllFilters`/`filters`) du mécanisme `:bulk` État —
   **aucun second système de sélection construit**.

## Décisions actées en cours de route

- **§4 de la TASK (sélection mixte Encaissement/Décaissement)** : recommandation architecte déjà
  actée dans le prompt de mission (« bloquer, comme `:bulk` le fait déjà pour l'État ») — appliquée
  telle quelle : `ModifierCodeActiviteLignesBulkAsync` interroge
  `GetDomainesDistinctsLignesAsync(ligneIds)` et rejette en **400** (`ApplicationException`, message
  explicite) si plus d'un domaine distinct est présent dans la sélection par IDs. Le cas
  `Domaine`+`Filter` ne peut structurellement pas être mixte (un seul `Domaine` en paramètre, comme
  l'existant État) — aucune vérification supplémentaire nécessaire pour cette branche.
- **Choix de conception non explicitement tranché par la TASK, décidé ici** : plutôt que d'étendre
  `BulkUpdateEtatRequest`/l'endpoint `:bulk` existant avec un champ `CodeActivite` optionnel (qui
  aurait mélangé deux actions différentes — État vs code activité — dans un seul contrat, avec un risque
  de confusion sur laquelle des deux est appliquée), un **second endpoint dédié**
  (`POST {id}/lignes/code-activite:bulk`) et un **DTO dédié** (`BulkUpdateCodeActiviteRequest`) ont été
  créés, reprenant à l'identique les 3 champs de sélection (`LigneIds`/`Domaine`/`Filter`). Conforme au
  garde-fou de la TASK (« réutiliser strictement le mécanisme de sélection », pas « réutiliser le même
  endpoint pour deux actions différentes ») — signalé pour revue au cas où le PO aurait préféré
  l'extension du contrat existant.
- **Réserve découverte en lisant le code existant, hors périmètre de correction ici** : l'endpoint
  `POST {id}/lignes:bulk` (État, TASK-012) appelle le **repository directement depuis le contrôleur**,
  sans passer par `DeclarationWorkflowService` — **aucune garde de clôture** n'y est appliquée
  aujourd'hui (une déclaration `Clôturée` accepterait donc un changement d'État en masse). TASK-173
  route délibérément son propre endpoint via le workflow service pour poser la garde de clôture
  demandée explicitement par cette TASK — mais cela crée une **incohérence avec l'endpoint frère**
  (État bulk, lui, reste sans garde). Signalé ici tel quel, **non corrigé** (hors périmètre des fichiers
  de TASK-173, aucune modification non demandée de `UpdateLignesBulk`) — à ouvrir séparément si le PO
  le juge utile.

## Tests

- `dotnet build DeclarationTVA.slnx` → **0 erreur** (mêmes 6 avertissements préexistants que TASK-172,
  sans rapport).
- `dotnet test DeclarationTVA.slnx` (état post-correctif) : `Declaration.Orchestration.Tests`
  **179/179** (177 initiaux + 2 nouveaux tests de non-régression du correctif), `Declaration.Core.Tests`
  54/54, `Declaration.Export.Xml.Tests` 13/13, `Declaration.Export.Excel.Tests` 3/3 ;
  `Declaration.Selection.Tests` 58/59 et `Declaration.Controle.Tests` 1/2 — mêmes 2 échecs préexistants
  sans rapport (login SQL Windows / déclaration GRFN de test absente), déjà documentés dans des VERIFY
  antérieurs, cf. TASK-172. Les 16 fakes `IDeclarationRepository` du dossier de tests ont été complétés
  avec les 5 nouvelles méthodes d'interface (14 stubs mécaniques `Task.CompletedTask`/liste vide + 2
  fakes enrichis avec un comportement réel : `Task156ContentionValorisationTests` en `throw NotUsed()`
  cohérent avec le reste du fichier, et `Task161CodeActiviteCascadeTests.FakeDeclarationRepositoryTask161`
  avec une implémentation réelle contre sa liste `Lignes` en mémoire, car ce fake est directement
  exercé par `ModifierCodeActiviteLigneAsync` dans des tests existants désormais soumis à la nouvelle
  validation de domaine TASK-172 §4 — sans cette mise à jour réaliste, ces tests auraient
  échoué). **Aucun test existant modifié dans son intention** — uniquement des fakes complétés pour
  satisfaire l'interface, plus les 2 nouveaux tests dédiés au correctif (cf. section correctif ci-dessus).
- Front : `npx tsc -b` → 0 erreur. `npx vite build` → 0 erreur (aucun changement front dans le correctif).

## Vérifié indépendamment en conditions réelles

Même instance de test que TASK-172 (`connections.json` local du build, port 5299, worker v10, service
`DeclaratifMaroc` réel jamais touché — cf. détails complets dans `VERIFY/TASK-172_verify.md`).

- **Bulk par sélection mixte** (`ligneIds` = 1 ligne `Decaissement` + 1 ligne `Encaissement`, même
  déclaration réelle `TVA1-2026-01`) → **400** :
  `"Sélection mixte Encaissement/Décaissement : l'affectation en masse du code activité doit porter sur
  un seul domaine à la fois."` — vérifié par relecture SQL directe après coup : **aucune écriture** sur
  la ligne Encaissement.
- **Bulk par `LigneIds` (un seul domaine, code compatible)** → **204** — écriture confirmée
  (`CodeActivite`, `CodeActiviteModifieManuellement=1`, `CodeActiviteModifiePar='Admin'`) sur l'unique
  ligne ciblée.
- **Bulk par `Domaine`+`Filter`** (filtre = numéro de facture réel `FC2501350`) → **204** — écriture
  batch confirmée sur les **3 lignes réelles** partageant cette facture (même pièce, 3 taux de TVA
  différents — pattern déjà connu depuis TASK-161 « une facture, plusieurs lignes ») : les 3 ont bien
  reçu le même code, la même traçabilité qui/quand, **en un seul appel** (SQL batch confirmé, pas une
  boucle : le filtre `LIKE` correspond par construction à toutes les lignes de la facture en une seule
  requête `UPDATE`).
- **Front réel (Playwright)** : après sélection de 2 lignes dans le drill « Codes activité » (onglet
  Encaissement), le nouveau contrôle « Affecter un code activité… » + bouton « Affecter » apparaît bien
  dans la barre d'outils, à côté des actions d'État existantes (capture réelle). Le clic
  « Affecter » lui-même **n'a volontairement pas été rejoué au navigateur** (réserve ci-dessous) — la
  chaîne HTTP exacte qu'il déclenche (`POST .../code-activite:bulk` avec `ligneIds`/`filter`/`domaine`)
  a déjà été testée bout-en-bout par les appels API directs ci-dessus, contre les mêmes lignes réelles.
- **Nettoyage post-test** : toutes les écritures de test (ligne unitaire + 3 lignes de la facture
  `FC2501350`) restaurées à l'identique par relecture/réécriture SQL directe (`CodeActivite=''`,
  `CodeActiviteModifieManuellement=0`, `CodeActiviteModifiePar=NULL`, `CodeActiviteModifieLe=NULL`).
  Vérification finale partagée avec TASK-172 : `SELECT COUNT(*) FROM DM_LGTVA WHERE
  CodeActiviteModifieManuellement = 1` → **0** sur toute la base. Process de test arrêté,
  `connections.json` restauré, fichiers temporaires supprimés — cf. détail complet dans
  `VERIFY/TASK-172_verify.md` (même session de nettoyage pour les deux tasks).

## Checklist (reprise du fichier TASK)

- [x] Build back + front OK.
- [x] Sélection par filtre (facture réelle) puis affectation en masse → toutes les lignes du filtre
      reçoivent le même code, **vérifié en base réelle** (`DM_LGTVA`, 3 lignes) — non vérifié au
      navigateur pour le clic final (réserve ci-dessous), mais la requête HTTP identique a été
      exécutée et vérifiée en base.
- [x] Traçabilité qui/quand posée sur chaque ligne affectée, identique au comportement unitaire
      TASK-161 — confirmé (`CodeActiviteModifiePar`/`CodeActiviteModifieLe` posés sur les 3 lignes).
- [x] Blocage confirmé si la déclaration est `Clôturée` (409 explicite) — **non rejoué en conditions
      réelles** (aucune déclaration `Clôturée` disponible dans la base de ce poste, toutes les 6
      `DM_ENTTVA` sont `Statut=0`/EnCours) ; couvert par les tests unitaires existants
      (`Task161CodeActiviteCascadeTests.ModifierCodeActiviteLigne_DeclarationCloturee_RefuseLaModification`,
      logique de garde identique réutilisée par le chemin bulk, non dupliquée).
- [x] **Cas adverse** (ajouté au correctif du 24/07/2026) : des `LigneIds` désignant des lignes d'une
      **autre** déclaration (y compris `Clôturée`) ne sont ni comptées ni écrites, quel que soit le statut
      de cette autre déclaration — `GetDomainesDistinctsLignesAsync`/`UpdateCodeActiviteBulkByIdsAsync`
      scopés par `DeclarationId` de l'URL, vérifié par 2 tests dédiés (cf. section correctif).
- [x] Sélecteur de code activité de l'action de masse ne propose que les codes du domaine concerné —
      hérite directement de `codeActiviteOptions` (TASK-172, déjà vérifié au navigateur dans son
      propre VERIFY).
- [x] Décision PO tracée sur le blocage domaine mixte (§4) : décision déjà actée par l'architecte
      (bloquer, comme `:bulk` le fait pour l'État), appliquée et vérifiée en conditions réelles (400
      confirmé ci-dessus).
- [x] Aucune régression sur l'action de masse existante (changement d'État) : `UpdateLignesBulk`/
      `BulkUpdateEtatRequest` non modifiés, diff nul sur ces éléments.

## Réserves non bloquantes (documentées, non silencieuses)

1. **Incohérence pré-existante signalée, non corrigée** : l'endpoint État `POST {id}/lignes:bulk` n'a
   aucune garde de clôture (bypass du workflow service) — voir « Décisions actées » ci-dessus. TASK-173
   n'introduit pas ce défaut mais ne le corrige pas non plus (hors périmètre des fichiers listés).
2. Garde de clôture du chemin bulk code activité (déclaration de l'URL elle-même `Clôturée`, ainsi que
   le cas adverse cross-déclaration corrigé le 24/07/2026) **non rejouée en conditions réelles** faute de
   déclaration `Clôturée` disponible sur ce poste — couverte par tests unitaires (garde partagée avec le
   PATCH unitaire pour le premier cas ; 2 tests dédiés ajoutés au correctif pour le second, cf. section
   correctif en tête de ce document).
3. Le clic réel du bouton « Affecter » **n'a pas été rejoué au navigateur** (choix délibéré pour éviter
   une seconde mutation de données réelles à restaurer en plus des tests API directs déjà effectués et
   vérifiés bout-en-bout sur les mêmes lignes) — seule l'apparition du contrôle UI a été vérifiée à
   l'écran.
4. Second endpoint créé plutôt qu'extension du contrat `:bulk` existant — voir « Décisions actées »,
   signalé pour arbitrage si le PO préfère un contrat unique.

## Verdict

Conforme au périmètre et aux garde-fous de la TASK, dépendance TASK-172 respectée (livrée dans le même
lot, sélecteur de code déjà filtré par domaine). Écriture SQL batch confirmée (pas de boucle
applicative), traçabilité qui/quand identique à l'unitaire, blocage domaine mixte vérifié en conditions
réelles. **Correctif du 24/07/2026 appliqué** suite au rejet architecte : le chemin `LigneIds` est
désormais scopé par `DeclarationId`, empêchant toute écriture cross-déclaration (et donc tout
contournement du garde-fou de clôture via des IDs étrangers) — vérifié par 2 nouveaux tests unitaires
dédiés, build et suite de tests complète repassés (179/179 sur `Declaration.Orchestration.Tests`, mêmes 2
échecs préexistants sans rapport ailleurs). Deux réserves non bloquantes restent documentées ci-dessus
(garde de clôture bulk non rejouée en conditions réelles faute de donnée `Clôturée` sur ce poste,
incohérence pré-existante sur l'endpoint État frère) — resoumis pour revue architecte.
