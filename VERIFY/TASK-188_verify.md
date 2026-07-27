# VERIFY — TASK-188

Date : 2026-07-27
Agent : Worker (session Développeur, prompt `DOCS/PROMPT-WORKER-TASK-186-187-188-27-07-2026.md`)

## Périmètre livré

Ajout de 2 colonnes « N° Pièce » (`RT_MOUVEMENT.MV_Piece`, nouveau) et « Échéance »
(`RT_MOUVEMENT.MV_Echeance`, déjà lu ailleurs — simplement relié jusqu'à l'export) à la feuille
« Règlements sélectionnés » de l'export Excel de contrôle.

1. **SQL** : `M.MV_Piece AS MvPiece` ajouté au `SELECT` de `GetReglementsRapprochementAsync`
   (`Declaration.Infrastructure/Repositories/DeclarationRepository.cs`) — même requête que celle qui
   expose déjà `MvEcheance`/`MvExtraitNum`, aucun JOIN supplémentaire.
2. **Modèle** :
   - `ReglementRapprochementRow.MvPiece` (`Declaration.Application/Entities/ReglementRapprochement.cs`).
   - `ReglementSelectionneInfo.Piece` (string) et `.Echeance` (DateTime?) (`Declaration.Core/Model.cs`).
3. **Mapping** : `DeclarationWorkflowService.ConstruireModeleControleAsync` — `Piece = r.MvPiece ?? ""`,
   `Echeance = r.MvEcheance`.
4. **Export** (`Exporter.cs`, `CreerFeuilleReglementsSelectionnes`) : colonnes 8 (« N° Pièce ») et 9
   (« Échéance ») ajoutées **à la suite** des 7 colonnes existantes (aucune réorganisation) — format
   `FormatDateSeule` (`dd/mm/yyyy`) appliqué à « Échéance », identique aux autres colonnes date de cette
   feuille (TASK-180).

## Décision documentée — sentinel `MV_Piece`

La TASK demandait de vérifier sur un échantillon réel plus large que les 5 lignes de l'architecte si un
sentinel vide/blanc existe. Vérifié sur **la table entière** (`GR_EMA_DISTRIBUTION`) :

```sql
SELECT COUNT(*) AS Total,
       SUM(CASE WHEN LTRIM(RTRIM(MV_Piece)) = '' THEN 1 ELSE 0 END) AS VideOuBlanc,
       SUM(CASE WHEN MV_Piece <> LTRIM(RTRIM(MV_Piece)) THEN 1 ELSE 0 END) AS AvecEspacesParasites
FROM RT_MOUVEMENT;
```
→ `Total=1462`, `VideOuBlanc=110` (**~7,5 %**), `AvecEspacesParasites=0`.

**Correction du diagnostic préliminaire de l'architecte** : la valeur vide N'EST PAS un cas rare —
7,5 % des lignes portent `MV_Piece = ''`. Ce n'est cependant **pas un sentinel fictif** au sens du
`1753-01-01` des colonnes date (qui pourrait être confondu avec une vraie date) : une chaîne vide dans
une colonne texte se traduit déjà naturellement par une cellule Excel vide, sans risque de lecture
trompeuse. **Décision : aucun traitement `NULLIF`/`TRIM` appliqué** — `MV_Piece` est exposé tel quel.
Documenté ici comme demandé, avec le chiffre réel (7,5 %) plutôt que l'hypothèse initiale « aucun cas
observé ».

## Décision documentée — position des colonnes

La TASK autorisait « à la suite des colonnes existantes sauf préférence PO contraire ». Retenu : position
8/9, en fin de tableau, sans toucher à l'ordre des 7 colonnes existantes (moindre impact, aucune
renumérotation des colonnes déjà en place, contrairement à TASK-187 où la nouvelle colonne s'insérait au
milieu). Si le PO préfère un regroupement logique différent (ex. « N° Pièce » à côté de « Numéro »), à
confirmer en revue — changement non structurant.

## Fichiers modifiés

- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` — 1 ligne SQL ajoutée.
- `Declaration.Application/Entities/ReglementRapprochement.cs` — champ `MvPiece` ajouté.
- `Declaration.Core/Model.cs` — champs `Piece`/`Echeance` ajoutés sur `ReglementSelectionneInfo`.
- `Declaration.Application/Services/DeclarationWorkflowService.cs` — mapping `Piece`/`Echeance` dans
  `ConstruireModeleControleAsync`.
- `Declaration.Export.Excel/Exporter.cs` — 2 colonnes ajoutées dans `CreerFeuilleReglementsSelectionnes`.
- `Declaration.Export.Excel.Tests/ExporterTests.cs` — fixture étendue (cas renseigné REG-001 + cas vide
  REG-002), assertions colonnes 8/9 ajoutées.
- `Declaration.Orchestration.Tests/Task160ExportControleTests.cs` — fixture `Reglement(...)` étendue
  (`MvPiece`/`MvEcheance`), assertions ajoutées au test de jointure existant, + nouveau test dédié
  `ConstruireModeleControleAsync_PieceVideEtEcheanceNull_AucuneException`.

## Tests / fixtures existants — vérification (Étape 3 de la TASK)

- `Task160ExportControleTests.cs` : la fixture `Reglement(...)` (factory partagée par plusieurs tests)
  a été étendue avec des valeurs par défaut (`MvPiece = "CB AUTO"`, `MvEcheance = 2026-08-01`) — les
  tests existants qui ne vérifiaient pas ces champs continuent de passer sans modification, seul le
  test de jointure (`ConstruireModeleControleAsync_ReglementsSelectionnes_JointureSelectionEtRapprochement`)
  a reçu 2 assertions supplémentaires.
- `ExporterTests.cs` : fixture `GetFixtureControle` étendue avec un cas renseigné et un cas vide
  (`Piece`/`Echeance` par défaut sur REG-002, cf. record C# — pas de valeur explicite = `""`/`null`).
- Nouveau test dédié ajouté pour couvrir explicitement le cas réel (~7,5 %) `MvPiece=""`/`MvEcheance=null`
  bout en bout via `ConstruireModeleControleAsync` (pas seulement au niveau de l'export).

## Rejeu réel (lecture seule) — GR_EMA_DISTRIBUTION / DESKTOP-5BFKKEP

Requête reproduisant exactement le fragment SQL corrigé (`MV_Piece AS MvPiece`,
`NULLIF(MV_Echeance,'17530101') AS MvEcheance`) sur 2 `MV_Id` réels choisis pour couvrir les deux cas
(renseigné / vide) :

```
MV_Id   MvNumero      MvPiece    MvEcheance
12030   RC26070004    VIREMENT   2026-06-23
12092   RF26070043    (vide)     2026-05-30
```

Confirmé : la requête corrigée expose bien `MV_Piece` tel quel (renseigné pour `MV_Id=12030`, vide pour
`MV_Id=12092`, `MV_Echeance` correctement neutralisé du sentinel 1753 et présent dans les deux cas
indépendamment de `MV_Piece`) — cohérent avec le critère de validation de la TASK (« N° Pièce » = valeur
exacte, « Échéance » = valeur exacte avec neutralisation sentinel).

### Point honnête — export réel régénéré depuis une déclaration existante

Comme pour TASK-186/187, je n'ai **pas** pu faire tourner `ConstruireModeleControleAsync` en direct
contre la base réelle via un test d'intégration C# : le seul test de ce type existant
(`IntegrationRegressionTests.cs`, `Server=.;...;Integrated Security=True`) échoue sur ce poste avec
`Échec de l'ouverture de session de l'utilisateur 'IHEB-PC\ihebc'` — problème d'authentification
Windows local, indépendant de tout code (déjà documenté dans `VERIFY/TASK-186_verify.md`). Écrire un
nouveau test avec les identifiants `sa`/`1234` en dur aurait introduit un secret dans le code source
(interdit, ARCHITECTURE.md §5) — non fait. La preuve apportée à la place est **équivalente mais pas
identique** à un export réellement régénéré par l'API : (a) rejeu SQL direct du fragment exact sur 2 cas
réels (ci-dessus), et (b) test unitaire bout-en-bout (fixture → `ConstruireModeleControleAsync` →
`ReglementSelectionneInfo` → colonnes Excel) avec des valeurs calquées sur ces cas réels. Signalé
explicitement plutôt que présenté comme un export réel généré par l'application.

## Build

- `Declaration.Application`, `Declaration.Infrastructure`, `Declaration.Export.Excel` → build individuel
  OK, 0 erreur (warnings préexistants uniquement, aucun nouveau).
- `dotnet build DeclarationTVA.slnx` (solution complète) : **même blocage environnemental que
  TASK-186/187** — process `Declaration.API.exe` (PID 33068) toujours actif, verrou toujours refusé par
  l'OS (revérifié, aucun changement depuis TASK-186). Détail complet déjà documenté dans
  `VERIFY/TASK-186_verify.md`, non répété ici.

## Tests

- `Declaration.Core.Tests` → **89/89** verts (inchangé, ce changement ne touche pas ce projet).
- `Declaration.Export.Excel.Tests` → **3/3** verts (fixture étendue, mêmes tests, nouvelles assertions).
- `Declaration.Selection.Tests` → **59/60** verts — même échec préexistant non lié
  (`IntegrationRegressionTests`, auth SQL locale), déjà documenté dans TASK-186/187.
- `Declaration.Orchestration.Tests` → **189/189** verts (188 existants + 1 nouveau,
  `ConstruireModeleControleAsync_PieceVideEtEcheanceNull_AucuneException`) — buildé/exécuté via
  `dotnet test Declaration.Orchestration.Tests --output <dossier temporaire>` pour contourner le même
  verrou de process que TASK-186/187 (ce projet référence `Declaration.API.csproj` directement).

## Validation checklist

- [x] Build OK (bibliothèques concernées individuellement — solution complète bloquée par
      l'environnement, pas par le code, cf. ci-dessus)
- [x] Tests passés (Core 89/89, Export.Excel 3/3, Orchestration 189/189 ; Selection 59/60, échec
      préexistant non lié)
- [x] « N° Pièce » = `RT_MOUVEMENT.MV_Piece` exact — vérifié par test unitaire + rejeu SQL réel (2 cas)
- [x] « Échéance » = `RT_MOUVEMENT.MV_Echeance` exact avec neutralisation sentinel 1753 — vérifié (le
      `NULLIF` était déjà en place pour `MvEcheance`, seul le maillon de propagation manquait)
- [x] Aucune régression sur les colonnes existantes de la feuille « Règlements sélectionnés » — colonnes
      1-7 non modifiées, tests existants (dates/montants/tiers/mode/état pointage) toujours verts
- [x] Aucune régression sur les autres feuilles de l'export (« Factures à déclarer », « Détail TVA »,
      « Détail ») — fichiers non touchés par ce changement
- [x] Aucun bypass sécurité (pas de credential en dur, cf. décision documentée ci-dessus sur le test
      d'intégration non écrit)
- [x] Aucune dette technique silencieuse — le point sur le sentinel `MV_Piece` (7,5 %, pas 0 comme
      supposé initialement) est corrigé et documenté explicitement, pas laissé tel quel

## Impacts détectés

- Aucun autre module ne lit `ReglementRapprochementRow`/`ReglementSelectionneInfo` en dehors du chemin
  déjà couvert par les tests mis à jour (`ConstruireModeleControleAsync`, écran Rapprochement — ce
  dernier utilise `MvEcheance` directement depuis `ReglementRapprochementRow`, jamais via
  `ReglementSelectionneInfo`, donc non affecté par cet ajout).
- `GetReglementsRapprochementDistinctsAsync`/le COUNT (`RapprochementFromWhere` seul, sans le nouveau
  `SELECT MvPiece`) : non modifiés, hors périmètre — cohérent, ces requêtes ne projettent pas
  `ReglementRapprochementRow` complet.

## Notes worker

- Choix de position (fin de tableau) et absence de traitement sur `MV_Piece` (malgré le taux de 7,5 %
  de valeurs vides, supérieur à l'hypothèse initiale) : deux décisions distinctes documentées
  séparément ci-dessus, aucune des deux ne bloque le reste du livrable.
- Contrairement à TASK-187, ce chemin (`ConstruireModeleControleAsync` → `Exporter.ExporterExcelControle`)
  EST le chemin réellement câblé par l'API pour l'export de contrôle — pas de gap architecture
  équivalent à celui découvert sur TASK-187 (`DM_LGTVA`/`ConstructeurDeclaration`) : `Piece`/`Echeance`
  sont calculés à la demande depuis `RT_MOUVEMENT` (via `GetReglementsRapprochementAsync`), jamais
  persistés, donc aucune dépendance à une donnée figée en base.

## Reste à valider (NON couvert par ce VERIFY)

1. Export réel régénéré via l'API (pas seulement rejeu SQL + test unitaire) — non produit, cf. section
   dédiée ci-dessus (limite du même ordre que TASK-186/187 : pas d'accès test d'intégration local
   fonctionnel sans introduire un secret en dur).
2. Build de la solution complète non rejoué avec succès — même blocage que TASK-186/187 (process
   `Declaration.API.exe` verrouillé, non résolu depuis).
3. Position exacte des colonnes (fin de tableau vs regroupement logique) : décision par défaut
   documentée, à confirmer par le PO si une préférence existe.

## Verdict

Les 2 colonnes sont livrées, testées unitairement de bout en bout (SQL → modèle → export), et
vérifiées contre des données réelles (rejeu SQL direct sur 2 `MV_Id`, cas renseigné et cas vide).
Contrairement à TASK-187, ce chemin est bien celui réellement emprunté par l'export de contrôle
existant — aucun gap architecture équivalent découvert. Décision sur le sentinel `MV_Piece` documentée
avec un chiffre réel corrigé (7,5 %, pas 0). Deux réserves non bloquantes : export réel via API non
produit (même limite d'environnement que les 2 TASKs précédentes) ; build solution complète toujours
bloqué par le même process verrouillé.
