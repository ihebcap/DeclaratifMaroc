Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md` (section
« ✅ TASK-187 approuvée pour son périmètre écrit, mais objectif PO NON atteint en pratique », en tête de
fichier), `D:\_vibe\GRF\DONE_DETAIL\TASK-187-ajout-colonne-reference-facture-export-declaration.md` et
`D:\_vibe\GRF\DONE_DETAIL\TASK-187_verify.md` (contexte complet du trou découvert), puis le fichier
TASK ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Contexte

TASK-187 a livré la colonne « Référence » (`RT_ECHEANCE.DO_Reference`) de bout en bout jusqu'à
`ConstructeurDeclaration`/`Exporter.cs`, correctement et testée — mais le worker de cette session a
découvert, et l'architecte a confirmé indépendamment sur le schéma réel, que **ce chemin n'est jamais
emprunté par un export réellement généré par l'API**. Les deux méthodes réellement câblées
(`DeclarationWorkflowService.ConstruireModeleExportAsync` et `ConstruireModeleControleAsync`) lisent
`LigneCandidate` (persisté depuis `DM_LGTVA`) via `_repository.GetLignesAsync`, jamais
`ConstructeurDeclaration`. `DM_LGTVA` n'a pas de colonne `Reference`, et `MapLignesCandidates` (qui
construit les `LigneCandidate` au figeage) ne mappe pas ce champ. Résultat : la colonne « Référence »
reste vide dans tout export réel aujourd'hui, malgré TASK-187 livrée et approuvée.

## Ta mission

Traiter **TASK-189**
(`D:\_vibe\GRF\TASKS\TASK-189-propagation-reference-persistance-dm-lgtva-export-reel.md`) — combler ce
trou par une migration additive et le câblage du dernier maillon manquant.

Vérifie d'abord que les champs Objectif/Périmètre/Livrables/Critères de validation sont bien remplis
(ils le sont). Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige les
erreurs, puis écris `VERIFY/TASK-189_verify.md` en suivant le même niveau de détail que
`DONE_DETAIL/TASK-186_verify.md`/`DONE_DETAIL/TASK-188_verify.md` (sers-t'en de modèle, l'architecte a
particulièrement apprécié le rejeu SQL réel systématique et les sections « Reste à valider » honnêtes
de ces deux VERIFY) : section Périmètre livré, Fichiers modifiés, Checklist, et une section « Reste à
valider » honnête si tout n'a pas pu être vérifié — ne déclare jamais un point validé si tu ne l'as pas
réellement vérifié.

## Règles de travail (non négociables)

- **Portée exacte** (§Périmètre STRICT de la TASK) :
  1. Migration SQL additive et idempotente sur `DM_LGTVA` (`DeclarationTVA.sql`, même patron que
     TASK-094 sur `DM_ENTTVA.DT_Id` — `IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS ...)
     ALTER TABLE DM_LGTVA ADD Reference NVARCHAR(...) NULL`). `DM_LGTVA` est une table `DM_*`, possédée
     par GRF — **aucune table `apbs-gr_winform` ne doit être touchée**, contrainte non négociable de ce
     projet.
  2. `LigneCandidate` (`Declaration.Application/Entities/WorkflowEntities.cs`) : nouveau champ
     `string? Reference`.
  3. `DeclarationRepository.SaveLignesCandidatesAsync` (`Declaration.Infrastructure/Repositories/
     DeclarationRepository.cs`, ~ligne 169-212) : ajouter `Reference` à la liste de colonnes `INSERT`
     et au paramètre anonyme. `GetLignesAsync` (`SELECT *`) n'a besoin d'aucune modification — le
     mapping Dapper se fera automatiquement une fois la colonne SQL et la propriété C# en place.
  4. `DeclarationWorkflowService.MapLignesCandidates` (~ligne 947) : propager
     `Reference = c.Affectation.Reference` sur les `LigneCandidate` créées — vérifie s'il y a une seule
     ou plusieurs branches de construction dans cette méthode (au moins la branche "facture introuvable"
     et la branche normale), n'en oublie aucune.
  5. `ConstruireModeleExportAsync` (~ligne 1260) et `ConstruireModeleControleAsync` (~ligne 1364) :
     ajouter `Reference = l.Reference` sur chaque `LigneDeclarationEnrichie` construite (le champ
     `Reference` existe déjà sur ce type depuis TASK-187 — seule l'affectation manque).
- Ne touche PAS à `ConstructeurDeclaration.cs`/`SelectionExpliqueeService.cs`/
  `SelectionExpliqueeEvaluator.cs` (déjà corrects depuis TASK-187) ni à `Exporter.cs` (colonne déjà en
  place). Pas de refactor, pas de nettoyage non demandé.
- **Tu as un accès réel à la base de données** : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d
  GR_EMA_DISTRIBUTION -Q "..."` (identifiants complets dans `D:\_vibe\GRF\connections.json`). Utilise-le
  pour :
  - Vérifier que la migration `DM_LGTVA.Reference` est bien idempotente (rejoue-la deux fois, la 2ᵉ ne
    doit rien casser).
  - Confirmer sur la table réelle (5148 lignes `DM_LGTVA` existantes) que les lignes déjà persistées
    restent lisibles avec `Reference = NULL` après la migration (aucune erreur de lecture).
  - Si possible, démontrer qu'une **ligne nouvellement figée** (pas une ligne déjà en base) porte la
    vraie `Reference` jusqu'à l'export — c'est le critère de validation le plus important de cette TASK.
    Si tu ne peux pas déclencher un cycle de figeage réel dans ton environnement (même limite qu'observée
    sur TASK-186/187/188, cf. leurs VERIFY), documente-le honnêtement et apporte la preuve alternative la
    plus proche possible (test d'intégration bout en bout avec une base de test locale si réalisable
    sans identifiants en dur, sinon test unitaire simulant fidèlement le cycle complet).
- Build back (`dotnet build DeclarationTVA.slnx`, ou builds individuels par projet si le process
  `Declaration.API.exe` verrouille encore l'assemblage — documente si c'est encore le cas) ET tests
  (`Declaration.Core.Tests`, `Declaration.Orchestration.Tests`, `Declaration.Selection.Tests`) doivent
  passer avant d'écrire le VERIFY.
- Un seul commit pour TASK-189, message clair, jamais `--no-verify`, **ne jamais pousser (`git push`)**
  — le commit reste local pour revue architecte.
- Respecte les règles universelles de `ARCHITECTURE.md` §5 (pas de SQL inline hors couche repository,
  pas de secret codé en dur, pas de bypass sécurité, pas de dette technique silencieuse — documente tout
  compromis dans le VERIFY).
- Si un point est réellement ambigu (notamment le type/longueur exacte de la colonne SQL `Reference`,
  ou une branche de `MapLignesCandidates` non anticipée par cette TASK) : **arrête-toi sur ce point
  précis, documente-le clairement dans le VERIFY** plutôt que d'inventer une réponse.

## À la fin

Laisse TASK-189 dans `VERIFY/` (ne la déplace pas toi-même vers `DONE_DETAIL/` — c'est le rôle de
l'architecte de la reviewer et de faire la clôture). Termine par un résumé clair : ✅ VERIFY prêt pour
revue / ⏸️ bloquée (et pourquoi) / non commencée (et pourquoi), en précisant explicitement si le
critère de validation principal (référence visible dans un export réel pour une ligne nouvellement
figée) a pu être démontré ou non.
