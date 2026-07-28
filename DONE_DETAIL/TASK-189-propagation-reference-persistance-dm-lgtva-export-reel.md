# TASK-189 — Rendre la colonne « Référence » (TASK-187) visible dans les exports réels (extension `DM_LGTVA`/`LigneCandidate`)

## Contexte
Suite directe de TASK-187, découverte par le worker pendant l'implémentation et **confirmée
indépendamment par l'architecte** (lecture code + vérification base réelle
`GR_EMA_DISTRIBUTION`) : la propagation du champ `Reference` livrée par TASK-187 (SQL →
`AffectationCandidateRow` → `SelectionExpliqueeEvaluator` → `AffectationADeclarer` →
`ConstructeurDeclaration` → `LigneDeclarationEnrichie` → `Exporter.cs`) est correcte et testée, mais
**ne s'affichera jamais dans un export réel généré par l'application** :

- Les deux méthodes réellement câblées côté API pour produire un export Excel —
  `DeclarationWorkflowService.ConstruireModeleExportAsync` (export officiel, déclaration Clôturée) et
  `ConstruireModeleControleAsync` (export de contrôle ad-hoc, TASK-160) — construisent leurs
  `LigneDeclarationEnrichie` **directement depuis `LigneCandidate`** (via `_repository.GetLignesAsync`,
  lignes ~1253-1255 et ~1356-1358 de `DeclarationWorkflowService.cs`), **jamais** via
  `ConstructeurDeclaration` (qui n'est appelé que par
  `Declaration.Orchestration/OrchestrateurDeclaration.cs:437`, dont le résultat sert uniquement à
  peupler `LigneCandidate` **au moment du figeage**, avant persistance en base).
- `LigneCandidate` (`Declaration.Application/Entities/WorkflowEntities.cs`) et la table `DM_LGTVA`
  (schéma réel vérifié : `Id, DeclarationId, Etat, Domaine, MotifRejet, NumeroFacture, TiersNom,
  TiersIdentifiantFiscal, TiersICE, HT, Taux, TVA, TTC, ModePaiement, DatePaiement, DateFacture, Source,
  NumeroRapprochement, EcType, Prorata, MontantAffecte, EC_Id, MV_Id, IncoherenceValidee,
  IncoherenceValideePar, IncoherenceValideeLe, CodeActivite, CodeActiviteModifieManuellement,
  CodeActiviteModifiePar, CodeActiviteModifieLe`) **n'ont pas de colonne `Reference`**.
- `DeclarationWorkflowService.MapLignesCandidates` (ligne ~947, construit les `LigneCandidate` au
  figeage depuis les `AffectationCandidate` de `ConstructeurDeclaration`) ne mappe pas non plus ce
  champ.

Conséquence concrète : générer aujourd'hui un export réel (officiel ou de contrôle) affiche une colonne
« Référence » **systématiquement vide**, quelle que soit la vraie valeur de `RT_ECHEANCE.DO_Reference`.
La demande initiale du PO (afficher la référence facture dans l'export) **n'est donc pas satisfaite en
pratique** tant que cette TASK n'est pas faite — TASK-187 reste néanmoins approuvée pour son périmètre
strict écrit (fondation correcte, non régressive), cf. `DONE_DETAIL/TASK-187_verify.md`.

## Objectif
```
Combler le trou entre la sélection (TASK-187, déjà livrée) et la persistance/lecture réelle :
  MapLignesCandidates (figeage)     : Reference = c.Affectation.Reference  → nouveau champ LigneCandidate
  SaveLignesCandidatesAsync (INSERT): colonne DM_LGTVA.Reference (migration additive)
  GetLignesAsync (SELECT *)         : déjà automatique une fois la colonne SQL + la propriété C# ajoutées
  ConstruireModeleExportAsync       : Reference = l.Reference  → LigneDeclarationEnrichie (déjà le champ existant, TASK-187)
  ConstruireModeleControleAsync     : idem
```

## Périmètre STRICT
- **Inclus** :
  1. **Migration SQL additive et idempotente** sur `DM_LGTVA` (`DeclarationTVA.sql`, même patron que
     TASK-094 sur `DM_ENTTVA.DT_Id` : `IF NOT EXISTS (... INFORMATION_SCHEMA.COLUMNS ...) ALTER TABLE
     DM_LGTVA ADD Reference NVARCHAR(...) NULL`). `DM_LGTVA` est une table **DM_**, possédée par GRF,
     jamais par `apbs-gr_winform` — cohérent avec la contrainte de schéma déjà appliquée sur ce projet
     (aucune table winform touchée).
  2. `LigneCandidate` (`WorkflowEntities.cs`) : nouveau champ `string? Reference`.
  3. `DeclarationRepository.SaveLignesCandidatesAsync` : ajouter `Reference` à la liste de colonnes
     `INSERT` + au paramètre anonyme (ligne ~173-210).
  4. `DeclarationWorkflowService.MapLignesCandidates` (ligne ~947) : `Reference =
     c.Affectation.Reference` sur les `LigneCandidate` créées (les deux branches — ligne non ventilée
     ET ligne normale, à vérifier laquelle existe plus bas dans la méthode).
  5. `ConstruireModeleExportAsync` (ligne ~1260) et `ConstruireModeleControleAsync` (ligne ~1364) :
     ajouter `Reference = l.Reference` sur chaque `LigneDeclarationEnrichie` construite (le champ
     `Reference` existe déjà sur ce type depuis TASK-187, seule l'affectation manque ici).
- **Exclu** : toute modification de `ConstructeurDeclaration.cs`/`SelectionExpliqueeService.cs`/
  `SelectionExpliqueeEvaluator.cs` (déjà corrects depuis TASK-187, non concernés) ; positionnement de
  la colonne dans l'export (déjà fixé par TASK-187, `Exporter.cs` non modifié ici) ; toute donnée
  `DM_LGTVA` déjà persistée avant cette migration (une ligne déjà figée gardera `Reference = NULL`
  jusqu'à un nouveau cycle de figeage — même logique que la réserve documentée dans TASK-186).

## Étapes
1. Migration SQL additive sur `DM_LGTVA` (idempotente, testée rejouée deux fois sans erreur).
2. `LigneCandidate.Reference` + `SaveLignesCandidatesAsync` (colonne INSERT).
3. `MapLignesCandidates` : propager `Reference` depuis `AffectationADeclarer`.
4. `ConstruireModeleExportAsync`/`ConstruireModeleControleAsync` : lire `l.Reference` vers
   `LigneDeclarationEnrichie.Reference`.
5. Tests : fixture de bout en bout (figeage → persistance simulée → lecture → export) couvrant
   référence renseignée et vide/NULL — même patron que les tests déjà ajoutés par TASK-187.
6. Rejeu réel (lecture seule pour la vérification de lecture, écriture seulement sur une déclaration de
   test dédiée si un nouveau cycle de figeage est nécessaire pour la preuve) : démontrer qu'une **ligne
   nouvellement figée** porte la vraie `DO_Reference` jusqu'à l'export généré par l'API — pas seulement
   un test unitaire in-memory.

## Livrables
- Migration SQL + code, testés unitairement bout en bout.
- Preuve réelle : au moins une ligne nouvellement figée (pas une ligne déjà existante en base avant
  cette TASK) dont l'export réel affiche la bonne `Reference`.

## Critères de validation
- Colonne « Référence » visible et correcte dans un export réel généré par l'API (officiel ET contrôle),
  pour toute ligne figée **après** cette migration.
- Migration idempotente (rejouable sans erreur), aucune table winform touchée.
- Build 0 erreur, tests verts, aucune régression sur les lignes déjà persistées (elles restent NULL,
  pas d'erreur de lecture).

## Risques / dépendances
- Dépend de TASK-187 (déjà livrée et approuvée — fondation nécessaire).
- Changement de schéma sur une table déjà en production chez au moins un client réel
  (`GR_EMA_DISTRIBUTION`, 5148 lignes `DM_LGTVA` existantes) — additive et nullable, donc sans risque de
  perte de données, mais à documenter clairement dans le VERIFY (nombre de lignes existantes qui
  resteront `Reference = NULL` tant qu'elles ne sont pas refigées).
- Aucun autre module identifié comme lisant `DM_LGTVA`/`LigneCandidate` en dehors des chemins listés
  ci-dessus (à reconfirmer par le worker par une recherche exhaustive avant de conclure).
