# TASK-169 — Batch de valorisation : N+1 requêtes SQL par facture au lieu d'un lot, sur `OrchestrateurDeclaration.Traiter`

## Contexte
Demande PO initiale de cette session (« je veux comprendre le mécanisme de valorisation de batch, je
sens qu'on peut l'optimiser ») — jamais conclue par une recommandation concrète à l'époque. Reprise
maintenant : voici l'analyse complète et le correctif proposé.

## Constat (code confirmé, mesure de comptage à l'appui)

`OrchestrateurDeclaration.Traiter` ([OrchestrateurDeclaration.cs](../Declaration.Orchestration/OrchestrateurDeclaration.cs))
appelle **jusqu'à 3 méthodes du cache de ventilation, chacune une fois par `EC_Id` distinct**, dans une
boucle `foreach` — et chacune de ces méthodes ouvre sa **propre `SqlConnection`** dans
`VentilationSageCacheRepository`
([VentilationSageCacheRepository.cs](../Declaration.Orchestration/VentilationSageCacheRepository.cs)) :

1. **`TryServireDepuisCache(ecId)`** ([OrchestrateurDeclaration.cs:66-76](../Declaration.Orchestration/OrchestrateurDeclaration.cs#L66-L76)), appelée une fois par `EC_Id` distinct dans la boucle de pré-résolution :
   - `GetEntries(soId, ecId, ...)` ([VentilationSageCacheRepository.cs:25-38](../Declaration.Orchestration/VentilationSageCacheRepository.cs#L25-L38)) — 1 `SqlConnection` + 1 `SELECT ... WHERE SO_Id=@SoId AND EC_Id=@EcId` **par EC_Id**.
   - Si la ligne n'est pas une sentinelle d'erreur figée : `GetCurrentPaiementToken(ecId, ...)` ([VentilationSageCacheRepository.cs:50-77](../Declaration.Orchestration/VentilationSageCacheRepository.cs#L50-L77)) — 1 `SqlConnection` + 1 `SELECT TOP 1 ... JOIN RT_MOUVEMENT` **par EC_Id**, pour valider la fraîcheur du token de paiement.
2. **`resoudreFacture`** ([OrchestrateurDeclaration.cs:319-374](../Declaration.Orchestration/OrchestrateurDeclaration.cs#L319-L374)), appelée par `ConstructeurDeclaration.ConstruireDeclaration` une fois par affectation mais mémoïsée par `EC_Id` (`echeanceMontantParEcId`) :
   - `GetEcheanceMontantDevise(ecId, ...)` ([VentilationSageCacheRepository.cs:83-90](../Declaration.Orchestration/VentilationSageCacheRepository.cs#L83-L90)) — 1 `SqlConnection` + 1 `SELECT EC_MtDevise FROM RT_ECHEANCE` **par EC_Id** (contrôle croisé TASK-072).

**Total mesuré sur le cas réel `TVA1-2026-06`** (session du 24/07/2026) : jusqu'à **~3 allers-retours SQL
séparés par facture distincte**, soit potentiellement plus de 2 000 requêtes individuelles pour une
déclaration de ~800 lignes/~700 `EC_Id` distincts — **même quand tout est déjà en cache et qu'aucune
lecture Sage OM n'est nécessaire** (le cas le plus fréquent, en régime établi). Chaque requête paie le
coût d'ouverture/acquisition de connexion (pool ADO.NET) + l'aller-retour réseau, même minime,
multiplié par le nombre de factures.

Ce mécanisme est appelé sur les chemins de figeage/rafraîchissement réels (première ouverture d'une
déclaration, `RafraichirValorisationAsync`, `ReintegrerReglementsLiberesAsync`,
`ResynchroniserLigneAsync`) — **pas** sur le simple rechargement d'une déclaration déjà figée
(`RevaliderLignesFigeesAsync`, qui est lui **déjà** correctement batché via
`GetEcIdsEnErreurAsync`/`GetMvPointsActuelsAsync`, aucune action requise là-dessus). Le coût est donc
payé à chaque figeage initial et à chaque rafraîchissement/recalcul en masse — des opérations déjà
identifiées comme sensibles à la contention OM (TASK-156/159), où réduire le temps total d'exécution
réduit aussi la fenêtre de risque de concurrence.

## Objectif
Remplacer les 3 boucles d'appels un-par-un par 3 requêtes en lot (une par méthode, sur l'ensemble des
`EC_Id` distincts de l'appel), sans changer la logique métier ni les résultats produits :

1. **`IVentilationSageCacheRepository`** : ajouter des méthodes batch en complément des méthodes
   unitaires existantes (ne pas les supprimer — utilisées ailleurs, ex. `DiagnostiquerLigneAsync`) :
   - `IReadOnlyDictionary<int, IReadOnlyList<VentilationSageCacheEntry>> GetEntriesBatch(int soId, IEnumerable<int> ecIds, string persistenceConnectionString)` — un seul `SELECT ... WHERE SO_Id=@SoId AND EC_Id IN @EcIds`, regroupé par `EC_Id` côté C#.
   - `IReadOnlyDictionary<int, PaiementToken?> GetCurrentPaiementTokensBatch(IEnumerable<int> ecIds, string grfConnectionString)` — un seul `SELECT` avec jointure sur tous les `EC_Id` d'un coup (`WHERE A.EC_Id IN @EcIds`, une ligne par `EC_Id` avec `ROW_NUMBER()`/agrégation pour ne garder que le `TOP 1` par groupe — même critère de tri qu'aujourd'hui).
   - `IReadOnlyDictionary<int, decimal?> GetEcheanceMontantsDeviseBatch(IEnumerable<int> ecIds, string grfConnectionString)` — un seul `SELECT EC_Id, EC_MtDevise FROM RT_ECHEANCE WHERE EC_Id IN @EcIds`.
2. **`OrchestrateurDeclaration.Traiter`** : remplacer les boucles d'appels unitaires par un appel batch
   en amont (avant les boucles), puis consommer les dictionnaires résultants — **logique de décision
   inchangée** (sentinelle ERREUR, auto-guérison TASK-076/077, comparaison de token TASK-156, contrôle
   croisé TASK-072), seule la source des données change (dictionnaire pré-chargé au lieu d'un appel SQL
   par pièce).
3. Conserver les méthodes unitaires existantes de l'interface (utilisées par `DiagnostiquerLigneAsync`/
   `RecalculerLigneDepuisCacheAsync` sur UNE seule ligne à la fois — pas de bénéfice à batcher là).

## Garde-fous
- **Aucun changement de comportement métier** : mêmes règles de fraîcheur de token (TASK-156), même
  auto-guérison de sentinelle (TASK-076/077), même contrôle croisé TTC (TASK-072) — uniquement le
  nombre de requêtes SQL change, jamais leur résultat logique.
- Découpage en lots si le nombre d'`EC_Id` dépasse une limite raisonnable pour une clause `IN`
  (paramètres SQL Server ~2100 max) — prévoir un chunking si une déclaration peut dépasser ce volume.
- Ne pas toucher à `RevaliderLignesFigeesAsync` (déjà batché correctement, hors périmètre).
- Ne pas modifier les méthodes unitaires existantes de `IVentilationSageCacheRepository` (risque de
  régression sur leurs appelants actuels, ex. diagnostic par ligne) — strictement additif.

## Files
- [Declaration.Orchestration/IVentilationSageCacheRepository.cs](../Declaration.Orchestration/IVentilationSageCacheRepository.cs) (nouvelles méthodes batch).
- [Declaration.Orchestration/VentilationSageCacheRepository.cs](../Declaration.Orchestration/VentilationSageCacheRepository.cs) (implémentation SQL Server des méthodes batch).
- [Declaration.Orchestration/OrchestrateurDeclaration.cs](../Declaration.Orchestration/OrchestrateurDeclaration.cs) (`Traiter`, `TryServireDepuisCache`, `resoudreFacture` — consommation des dictionnaires pré-chargés).
- Tests existants dépendant de `IVentilationSageCacheRepository` (Fakes de test à étendre, pas à réécrire — garde-fou tiré de l'incident TASK-147 : toujours répercuter un changement d'interface sur tous les Fakes).

## Validation
- [ ] Build back OK.
- [ ] Tests `Declaration.Orchestration.Tests` existants rejoués verts (aucune régression de
      comportement — mêmes motifs/valeurs produits qu'avant, notamment TASK-072/076/077/156).
- [ ] Nouveau test mesurant le nombre de requêtes/appels au repository sur un lot de N factures déjà en
      cache (avant : ~3×N appels ; après : au plus 3 appels quel que soit N).
- [ ] Rejeu réel sur une déclaration volumineuse (ex. `TVA1-2026-06`, ~700 `EC_Id`) mesurant le temps
      d'exécution avant/après, en régime « déjà tout en cache » (cas le plus fréquent).
- [ ] Aucune régression sur les méthodes unitaires existantes (toujours utilisées par le diagnostic par
      ligne, non touchées par cette task).

## Risques / dépendances
- Risque de régression comportementale si la logique de sélection du token (`GetCurrentPaiementToken`,
  `TOP 1 ... ORDER BY M.MV_Point DESC`) est mal reproduite en version batch (ex. mauvais groupement par
  `EC_Id`) — à tester spécifiquement avec un cas à affectations multiples pour un même `EC_Id`.
- Aucune dépendance externe — optimisation interne, aucun changement d'API front, aucun changement de
  schéma de base.
