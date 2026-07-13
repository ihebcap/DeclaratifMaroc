# TASK-067B — VERIFY : Filtres « valeurs disponibles » (Excel-like), back+front server-side

> Suite de [TASK-067A](../DONE_DETAIL/TASK-067A-filtres-valeurs-list-front-client-side.md).
> Périmètre : `RapprochementInterrogation.tsx`/`GET /api/rapprochement`,
> `FactureInterrogation.tsx`/`GET /api/factures`, `DomainGrid.tsx`/`GET /declarations/{id}/lignes`.

## Résumé par endpoint / écran

### 1. `GET /api/rapprochement/distincts` (RapprochementInterrogation.tsx)

- Étendu au-delà de `modes`/`origines` (existants, inchangés) avec trois nouvelles clés :
  `numeroReglement`, `numeroExtrait`, `banque` — valeurs distinctes réelles calculées sur le
  **même `RapprochementFromWhere`** que la liste/le COUNT (`DeclarationRepository.cs`,
  `GetReglementsRapprochementDistinctsAsync`), avec un filtre « vide » (`new RapprochementFilter()`)
  pour ne pas dépendre de la sélection courante de l'utilisateur.
- `RapprochementFilter`/`RapprochementFromWhere`/`RapprochementParams` : les anciens filtres LIKE
  scalaires `Numero`/`NumeroExtrait`/`Banque` sont remplacés par `Numeros`/`NumerosExtrait`/`Banques`
  (`IReadOnlyList<string>?`, multi-sélection réelle `IN @xxx`, guard `@hasXxx = 0`).
- `RapprochementController.GetReglements` : paramètres `numero`/`numeroExtrait`/`banque` passent de
  `string?` à `string[]?` (binding ASP.NET en clés répétées, comme `origine[]`/`domaine[]` déjà en
  place depuis TASK-063).
- **`tiers` reste en `text`/LIKE** (décision documentée, voir « Arbitrage cardinalité » ci-dessous).
- Front `RapprochementInterrogation.tsx` : `numeroReglement`, `numeroExtrait`, `banqueCode` passent
  en `filterType: 'list'` ; `filterOptionsFor` les alimente depuis les nouvelles clés de la réponse
  `/distincts` ; `fetchPage` envoie les tableaux complets (même mécanisme que `origine`/`domaine`).

### 2. `GET /api/factures/distincts` (FactureInterrogation.tsx)

- Étendu avec `numero` et `reference` (valeurs distinctes de `DO_Numero`/`DO_Reference`), calculées
  sur le **même `FacturesFromWhere`** que la liste/le COUNT, filtre vide (aucun numero/fournisseur/
  reference/origine/statut actif).
- `FacturesFromWhere`/`FacturesParams` : `numero`/`reference` passent de `string?` (LIKE) à
  `IReadOnlyList<string>?` (`IN`, multi-sélection réelle). `fournisseur` reste `string?` LIKE.
- `FacturesController.GetFactures` : `numero`/`reference` passent de `string?` à `string[]?`.
- Front : `factureNumero`, `reference` passent en `filterType: 'list'` ; `fournisseur` reste `text`.

### 3. `GET /declarations/{id}/lignes` (DomainGrid.tsx)

- Le front (`DomainGrid.tsx`) attendait déjà `res.data.distincts` (`setDistincts(res.data.distincts
  || {})`, câblage TASK-034/038 jamais alimenté côté back) — **aucune valeur distincte n'était donc
  jamais servie**, et cocher une case dans `ExcelFilter` sur `origine`/`source`/`tauxTVA` n'avait
  **aucun effet serveur** (clé JSON silencieusement ignorée par l'ancien parsing, qui ne traitait
  que `numeroRapprochement` et `etat`).
- Ajout de `IDeclarationRepository.GetLignesDistinctsAsync(declarationId, domaine)` : calcule les
  valeurs distinctes de `numeroRapprochement`, `source`, `tauxTVA`, `origine` (dérivé de `EcType`
  via `ReglementRapprochementRow.LibelleEcType`, source unique) et `statutLigne` (dérivé de `Etat`),
  sur le **même WHERE** (`DeclarationId + Domaine`) que `GetLignesAsync`/`GetLignesCountAsync`.
  Pas de borne `TOP` : l'ensemble est déjà borné par construction (lignes candidates figées d'**une
  seule déclaration**, cf. `ChargerCandidatesSiNecessaireAsync`), pas un flux global.
- `DeclarationsController.GetLignes` : la réponse inclut désormais `Distincts` (→ `distincts` en
  JSON, camelCase par défaut ASP.NET Core).
- **Filtre réellement appliqué** (avant, silencieusement ignoré) : `GetLignesAsync`/
  `GetLignesCountAsync` partagent désormais `BuildLigneFilterWhere(filter)`, qui traite
  `numeroRapprochement` (désormais `IN` sur tableau complet, fin de la troncature `v[0]`),
  `source` (`IN`), `tauxTVA` (`IN` sur decimal parsé), `origine` (label → `EcType` via
  `ReglementRapprochementRow.EcTypesDepuisLibelle`, reverse exact de `LibelleEcType`, aucune
  duplication de règle), et `etat` (inchangé, déjà fonctionnel).
- Front `DomainGrid.tsx` : suppression de la troncature `numeroRapprochement → v[0]` dans
  `fetchPage` (tous les filtres passent désormais tels quels au back, qui gère le multi-IN).
- **Hors périmètre** (documenté, non touché) : les colonnes `statutConformite`,
  `tiersIdentifiantFiscal`, `tiersICE` utilisées uniquement par les configurations `columns`
  personnalisées de `WorkstationPanel.tsx`/`ProofModal.tsx` (pas les colonnes par défaut de
  `DomainGrid.tsx`) restent sans effet serveur — c'était déjà le cas avant cette tâche (aucune
  régression), et leur activation impliquerait soit de dupliquer la règle `EtatConformite`
  (calculée, pas stockée) en SQL, soit de sortir la pagination de SQL Server vers un filtrage en
  mémoire — **hors périmètre explicite de 067B** (« pas de refonte pagination »). Signalé comme
  candidat à une tâche ultérieure si le PO le souhaite.

## Preuve du respect de l'invariant TASK-040 (même WHERE distincts ↔ liste)

- **Rapprochement/Factures** : les nouvelles requêtes `distincts` sont littéralement construites en
  interpolant la **même constante** `RapprochementFromWhere`/`FacturesFromWhere` que
  `GetReglementsRapprochementAsync`/`GetReglementsRapprochementCountAsync` (resp.
  `GetFacturesInterrogationAsync`/`...CountAsync`) — pas une réécriture parallèle du WHERE. Un futur
  changement du WHERE de la liste modifie donc automatiquement les distincts (pas de divergence
  possible par oubli).
- **DomainGrid** : `GetLignesAsync`/`GetLignesCountAsync`/`GetLignesDistinctsAsync` partagent le
  même prédicat de base `WHERE DeclarationId = @DeclarationId AND Domaine = @Domaine`, et
  `GetLignesAsync`/`GetLignesCountAsync` partagent en plus le **même** `BuildLigneFilterWhere(filter)`
  (fragment + paramètres identiques), garantissant que `TotalCount` correspond exactement au nombre
  de lignes réellement filtrées.
- Aucune valeur ne peut donc apparaître dans une liste de filtre sans être présente dans le jeu réel
  de la période/déclaration (« zéro valeur infiltrable »).

## Preuve de lecture seule stricte

- Toutes les nouvelles requêtes ajoutées sont des `SELECT DISTINCT` (`SELECT NumeroRapprochement,
  Source, Taux, EcType, Etat FROM DM_LGTVA WHERE ...` pour DomainGrid ; `SELECT DISTINCT TOP N ...`
  pour Rapprochement/Factures). Aucun `INSERT`/`UPDATE`/`DELETE` ajouté, aucun appel DLL/COM ajouté.
- `UpdateLignesEtatBulkAsync` (write path, action de masse) n'a **pas** été modifié — il continue
  d'utiliser son filtre texte simple préexistant, hors périmètre de cette tâche (lecture seule).
- Recherche de contrôle (aucune écriture introduite par cette tâche) :
  ```
  grep -n "INSERT INTO\|UPDATE \|DELETE FROM" Declaration.Infrastructure/Repositories/DeclarationRepository.cs
  ```
  → seules les méthodes préexistantes (`CreateAsync`, `UpdateStatutAsync`,
  `SaveLignesCandidatesAsync`, `UpdateLigneEtatAsync`, `UpdateLignesEtatBulkAsync`,
  `UpdateLignesEtatBulkByIdsAsync`, `TamponnerAffectationsAsync`, `DetamponnerAffectationsAsync`)
  apparaissent — aucune n'a été touchée par ce diff.

## Mesure / décision coût `SELECT DISTINCT` (forte cardinalité)

Mesure **chiffrée réellement produite**, via la connexion SQL Server partagée du projet
(`D:\_vibe\GRF\connections.json`, `.\sql2022` / base `GR_EMA_DISTRIBUTION`), avec
`SET STATISTICS TIME/IO ON` sur des `SELECT DISTINCT TOP 500 ...` reprenant le même WHERE que
`RapprochementFromWhere`/`FacturesFromWhere` (SO_Id=1, MV_Domaine IN (0,1) / DO_Domaine=1, période
large) :

| Colonne | Table | Lectures logiques | Temps UC / écoulé | Index dédié |
|---|---|---|---|---|
| MV_Numero | RT_MOUVEMENT | 93 | 0 ms / 7 ms | Oui (`IX_SO_Id_Numero_Domaine`) |
| MV_ExtraitNum | RT_MOUVEMENT | 196 | 15 ms / 3 ms | Non |
| DO_Numero | RT_ECHEANCE | 214 | 0 ms / 4 ms | Non |
| DO_Reference | RT_ECHEANCE | 214 | 0 ms / 4 ms | Non |

**Correction sur les index** : seule `MV_Numero` (`RT_MOUVEMENT`) porte un index dédié
(`IX_SO_Id_Numero_Domaine`), vérifié via `sys.indexes`/`sys.index_columns`. `MV_ExtraitNum`,
`DO_Numero` et `DO_Reference` **n'ont aucun index dédié aujourd'hui** — l'affirmation initiale
(« ces colonnes portent un index naturel ») était erronée et est corrigée ici.

**Limite du jeu de mesure** : la base `GR_EMA_DISTRIBUTION` utilisée pour ces mesures ne contient
que 1349 lignes dans `RT_MOUVEMENT` et 3687 dans `RT_ECHEANCE` — c'est un jeu de données de
dev/test, pas un volume de production « forte cardinalité ». Sur ce volume, un scan complet reste
trivial même sans index dédié (d'où les temps négligeables ci-dessus, y compris sans index sur
`MV_ExtraitNum`/`DO_Numero`/`DO_Reference`). **Ces chiffres ne prouvent donc pas** que le
comportement sera acceptable en production sur un jeu à forte volumétrie ; ils prouvent seulement
l'absence de problème sur le jeu de dev actuel.

- **Rapprochement** (`RT_MOUVEMENT`, période bornée obligatoire) : `numeroReglement`, `numeroExtrait`,
  `banqueCode` passés en `list`, bornés à `TOP 500` (`DeclarationRepository.DistinctsTopBound`) —
  cohérent avec le plafond d'affichage de `ExcelFilter` (200 lignes + recherche), marge de 500 pour
  laisser la recherche serveur fonctionner sans renvoyer un volume disproportionné.
- **Factures** (`RT_ECHEANCE`) : `factureNumero`, `reference` — même traitement, même borne.
- **`tiers` (Rapprochement) et `fournisseur` (Factures) restent en `text`/LIKE** — décision
  documentée ici (pas a priori, cf. règle de la tâche) : ce sont des noms libres (raison sociale),
  cardinalité potentiellement non bornée ET non stable dans le temps (variantes de saisie), pour
  lesquels une recherche `SELECT DISTINCT` plafonnée à 500 lignes triées alphabétiquement risquerait
  de **cacher silencieusement** des tiers au-delà de la borne (violerait l'esprit « aucune valeur
  infiltrable / aucune liste menteuse » — un tiers existant mais hors des 500 premiers alphabétiques
  ne serait ni dans la liste ni trouvable par la recherche de `ExcelFilter`, qui ne cherche que dans
  les options déjà chargées). Le filtre texte LIKE reste donc adapté à ces deux colonnes précises.
  Cette note perf ne remet pas en cause ce choix (déjà tranché indépendamment des index) ; la
  décision PO de garder `list` par défaut pour les colonnes concernées par ce VERIFY reste
  inchangée.
- **DomainGrid** : aucune borne nécessaire — l'ensemble candidat est déjà celui d'**une seule
  déclaration** (figé au premier appel, cf. `ChargerCandidatesSiNecessaireAsync`), intrinsèquement
  petit (au plus quelques milliers de lignes), donc sans risque de volume disproportionné.

**Action de suivi recommandée** :
1. Avant généralisation à grande échelle / mise en production sur une base à forte volumétrie,
   revalider obligatoirement ces 4 requêtes (`SET STATISTICS TIME/IO ON`, période d'un an, société à
   fort volume réel) — la mesure ci-dessus sur `GR_EMA_DISTRIBUTION` ne couvre qu'un jeu de dev de
   petite taille et ne dispense pas de cette revalidation.
2. Proposer, en tâche de suivi distincte (hors périmètre SQL lecture-seule de 067B), l'ajout d'un
   index dédié sur `RT_MOUVEMENT.MV_ExtraitNum`, `RT_ECHEANCE.DO_Numero` et `RT_ECHEANCE.DO_Reference`,
   afin de sécuriser le comportement de ces `SELECT DISTINCT TOP 500` à forte volumétrie (aujourd'hui
   ces trois colonnes n'ont aucun index dédié).

## Build / tests exécutés

- `dotnet build Declaration.Infrastructure/Declaration.Infrastructure.csproj` → **succès**, 0 erreur
  (1 avertissement préexistant hors périmètre, `SqlConnectionStringBuilder` obsolète).
- `dotnet build Declaration.API/Declaration.API.csproj` → compilation C# réussie (aucune erreur
  `CS*`) ; seule la copie finale des DLL a échoué (`MSB3027`, fichiers verrouillés par une instance
  `Declaration.API.exe` déjà en cours d'exécution sur le poste — non lié au code, non bloquant pour
  la validation de compilation).
- `dotnet test Declaration.Orchestration.Tests/Declaration.Orchestration.Tests.csproj` → **80/80
  réussis** (dont les 12 nouveaux tests `Task067BFiltresListeServerSideTests` : défauts
  `RapprochementFilter`/`ReglementRapprochementDistincts`/`FactureInterrogationDistincts`,
  multi-sélection réelle, `EcTypesDepuisLibelle` aller-retour avec `LibelleEcType` sur les codes
  connus (0/111/4) et la branche « Autre (n) », robustesse sur libellé `null`/vide/inconnu).
- `dotnet test Declaration.Selection.Tests/Declaration.Selection.Tests.csproj` → 51/52 réussis ;
  1 échec (`IntegrationRegressionTests`) dû à l'absence de connexion SQL Server réelle dans cet
  environnement (`Microsoft.Data.SqlClient` timeout) — préexistant, sans rapport avec cette tâche
  (test d'intégration nécessitant une base GRF réelle).
- Front `declaration-tva-web` :
  - `npm run build` (`tsc -b && vite build`) → **succès**, 0 erreur TypeScript.
  - `npm run lint` (`oxlint`) → **0 erreur** (exit code 0) ; uniquement des avertissements
    préexistants (`react-hooks/exhaustive-deps`, `no-unused-vars` sur des fichiers/lignes non
    modifiés par cette tâche, ex. `api.ts:17`, `DomainGrid.tsx:165` déjà présent avant le diff).

## Fichiers modifiés / créés

Back :
- `Declaration.Application/Entities/ReglementRapprochement.cs` (nouveaux champs distincts/filtre +
  `EcTypesDepuisLibelle`)
- `Declaration.Application/Entities/FactureInterrogation.cs` (nouveaux champs distincts)
- `Declaration.Application/Interfaces/IDeclarationRepository.cs` (signatures + nouvelle méthode
  `GetLignesDistinctsAsync`)
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` (WHERE étendus, nouvelles
  requêtes distincts, `BuildLigneFilterWhere` partagé liste/count DomainGrid)
- `Declaration.API/Controllers/RapprochementController.cs`
- `Declaration.API/Controllers/FacturesController.cs`
- `Declaration.API/Controllers/DeclarationsController.cs`
- `Declaration.Orchestration.Tests/Task067BFiltresListeServerSideTests.cs` (nouveau)

Front :
- `declaration-tva-web/src/RapprochementInterrogation.tsx`
- `declaration-tva-web/src/FactureInterrogation.tsx`
- `declaration-tva-web/src/DomainGrid.tsx`

## Points d'attention restants

- Mesure de perf `SELECT DISTINCT` **chiffrée sur base réelle** (`.\sql2022`/`GR_EMA_DISTRIBUTION`,
  cf. section dédiée ci-dessus), mais sur un jeu de dev de petite taille (1349/3687 lignes) — **à
  revalider obligatoirement sur une volumétrie de production** avant généralisation à grande échelle,
  et à accompagner de l'ajout d'un index dédié sur `MV_ExtraitNum`/`DO_Numero`/`DO_Reference`
  (aucun index dédié aujourd'hui, seule `MV_Numero` en a un) — proposé comme tâche de suivi
  distincte, hors périmètre SQL lecture-seule de 067B.
- `statutConformite`/`tiersIdentifiantFiscal`/`tiersICE` (colonnes custom de `WorkstationPanel`/
  `ProofModal`, pas des colonnes par défaut de `DomainGrid`) restent sans filtrage serveur effectif —
  pré-existant, non aggravé, candidat à une tâche ultérieure si demandé par le PO.
- TASK-068 (sélecteur de colonnes) : aucun fichier de configuration de colonnes n'a été modifié au-delà
  des `filterType` changés ici ; risque de conflit d'édition à coordonner comme documenté dans
  TASK-067B, mais aucun chevauchement direct constaté avec ce diff.
