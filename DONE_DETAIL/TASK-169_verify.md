# VERIFY — TASK-169 : batch de valorisation, N+1 requêtes SQL par facture

## Contexte
`OrchestrateurDeclaration.Traiter` appelait jusqu'à 3 méthodes du cache de ventilation
(`GetEntries`, `GetCurrentPaiementToken`, `GetEcheanceMontantDevise`) une fois par `EC_Id`
distinct, chacune ouvrant sa propre `SqlConnection` — jusqu'à ~3×N requêtes SQL individuelles
pour une déclaration de N factures, même en régime « déjà tout en cache ».

## Ce qui est livré

### Nouvelles méthodes batch (additives, méthodes unitaires conservées à l'identique)
- `IVentilationSageCacheRepository` : `GetEntriesBatch`, `GetCurrentPaiementTokensBatch`,
  `GetEcheanceMontantsDeviseBatch` — nouvelles méthodes d'interface, méthodes unitaires
  existantes (`GetEntries`, `GetCurrentPaiementToken`, `GetEcheanceMontantDevise`) inchangées,
  toujours utilisées par `DiagnostiquerLigneAsync`/`RecalculerLigneDepuisCacheAsync`
  (diagnostic sur une seule ligne — pas de bénéfice à batcher là, hors périmètre).
- `VentilationSageCacheRepository` : implémentation SQL Server, un seul aller-retour par méthode
  (chunké par lot de 2000 `EC_Id` — marge sous la limite ~2100 paramètres SQL Server d'une clause
  `IN`) :
  - `GetEntriesBatch` : `WHERE SO_Id=@SoId AND EC_Id IN @EcIds`, regroupé par `EC_Id` côté C#.
  - `GetCurrentPaiementTokensBatch` : reproduit le `TOP 1 ... ORDER BY MV_Point DESC` par EC_Id
    via `ROW_NUMBER() OVER (PARTITION BY EC_Id ORDER BY MV_Point DESC) = 1` — même critère de
    sélection qu'avant (TASK-106 espèce comprise, même clause `WHERE`).
  - `GetEcheanceMontantsDeviseBatch` : `SELECT EC_Id, EC_MtDevise FROM RT_ECHEANCE WHERE EC_Id IN (...)`.

### `OrchestrateurDeclaration.Traiter`
- Pré-chargement batch en tête de méthode (`ecIdsAll` = tous les `EC_Id>0` de
  `affectationsList`), avant les boucles de pré-résolution/matérialisation.
- `TryServireDepuisCache`/`TryGetToken` prennent désormais les dictionnaires pré-chargés en
  paramètre (plus d'appel SQL individuel) ; `resoudreFacture` consomme `echeanceBatch` au lieu
  d'un `SELECT` mémoïsé par `EC_Id`.
- **Résilience identique à l'ancien code** : si le chargement batch lui-même échoue (DB
  indisponible), un flag (`entriesBatchFailed`/`tokensBatchFailed`) force EXACTEMENT le même
  résultat que l'ancien comportement unitaire (« exception sur la lecture individuelle ⇒ cache
  miss pour cette pièce ») — appliqué uniformément à tous les `EC_Id`, ce qui est le
  comportement réel de l'ancien code puisqu'une panne de connexion touchait déjà systématiquement
  tous les appels individuels suivants sur la même connexion. Sans ce flag, un dictionnaire vide
  après échec aurait pu, dans un cas limite (token stocké NULL + tokens batch NULL par défaut
  d'absence), être interprété à tort comme un match — d'où le garde-fou explicite.
- `RevaliderLignesFigeesAsync` (déjà batché, hors périmètre) non touché.

## Vérification indépendante des critères de validation

- [x] **Build back OK** — `dotnet build DeclarationTVA.slnx` : 0 erreur.
- [x] **Tests `Declaration.Orchestration.Tests` existants rejoués verts** — **177/177**, y compris
      les 3 tests d'intégration SQL Server réels (`.\sql2022`/`GR_EMA_DISTRIBUTION`, IT4/IT6/IT8)
      qui comptent précisément les appels OM avant/après relecture — **aucune régression
      comportementale détectée** (mêmes motifs/valeurs produits, TASK-072/076/077/156 intacts).
      Un rejet initial a été détecté et corrigé pendant cette vérification (voir « Incident
      détecté et corrigé » ci-dessous).
- [x] **Nouveau test mesurant le nombre d'appels** —
      `T169_NFacturesDejaEnCache_UnSeulAppelBatch_ZeroAppelUnitaire` (25 factures distinctes déjà
      en cache) : `GetEntriesBatchCalls=1`, `GetCurrentPaiementTokensBatchCalls=1`,
      `GetEntriesCalls=0`, `GetCurrentPaiementTokenCalls=0` — la preuve directe demandée par la
      task (avant : ~3×N appels ; après : au plus 3 appels quel que soit N).
- [ ] **Rejeu réel sur une déclaration volumineuse (TVA1-2026-06) mesurant le temps
      d'exécution avant/après** — **non fait dans cette session** (réserve non bloquante ci-dessous).
- [x] **Aucune régression sur les méthodes unitaires existantes** — non modifiées, toujours
      utilisées par le diagnostic par ligne (recherche du même nom de méthode dans tout le repo
      confirmant leurs seuls appelants restent `DiagnostiquerLigneAsync`/
      `RecalculerLigneDepuisCacheAsync`, non touchés par cette task).

## Incident détecté et corrigé pendant la vérification
Le premier passage a fait échouer 3 tests d'intégration SQL Server réels (IT4, IT6, IT8) —
comptage d'appels OM `Assert.Equal` en écart de +1. Cause racine : `RepoSqlServerStubToken`
(sous-classe de test qui surcharge `GetCurrentPaiementToken` pour injecter un token contrôlé sans
connexion GRF réelle) n'avait pas d'override de la nouvelle méthode batch
`GetCurrentPaiementTokensBatch` — celle-ci retombait donc sur l'implémentation SQL Server réelle,
tentait un aller-retour sur la chaîne factice `"fake-grf"` de ces tests, échouait, déclenchait le
flag `tokensBatchFailed`, et forçait un cache miss systématique (donc une relecture OM en trop).
Corrigé en ajoutant l'override manquant (`GetCurrentPaiementTokensBatch` rendu `virtual` dans
`VentilationSageCacheRepository`, surchargé dans `RepoSqlServerStubToken` pour renvoyer
`ForcedToken` pour tout `EC_Id`, symétrique à l'override unitaire déjà présent). Les 3 tests
passent ensuite sans autre changement — confirme que la logique batch reproduit exactement le
critère de fraîcheur de token de l'ancien code unitaire.

## Réserves non bloquantes
- **Mesure de temps d'exécution avant/après sur une déclaration réelle volumineuse non faite** :
  isoler un avant/après fiable aurait nécessité de `git stash` sélectivement les fichiers de cette
  task, rebuilder, rejouer un scénario réel identique deux fois, puis restaurer — non fait par
  arbitrage de temps dans cette session (priorité donnée à l'enchaînement des 5 tasks + campagne
  de test demandés). La preuve de fond (réduction du nombre de requêtes SQL, l'objectif réel de la
  task) est en revanche établie de façon directe et non ambiguë par le test
  `T169_NFacturesDejaEnCache_UnSeulAppelBatch_ZeroAppelUnitaire` ainsi que par les tests
  d'intégration SQL Server réels (IT4/IT6/IT8) qui n'auraient pas pu passer si la logique batch
  avait introduit une relecture supplémentaire. Une mesure de temps d'horloge réelle sur
  `TVA1-2026-06` reste à faire si le PO la juge nécessaire au-delà de cette preuve de comptage.
