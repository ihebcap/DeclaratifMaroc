# VERIFY — TASK-187

Date : 2026-07-27
Agent : Worker (session Développeur, prompt `DOCS/PROMPT-WORKER-TASK-186-187-27-07-2026.md`)

## ⚠️ Point majeur à lire en premier (voir aussi § « Reste à valider »)

La propagation demandée par la TASK (SQL → `AffectationCandidateRow` → évaluateur →
`AffectationCandidate`/`AffectationADeclarer` → `ConstructeurDeclaration` → `LigneDeclarationEnrichie`
→ `Exporter.cs`) est **livrée intégralement et testée**. Mais en creusant le code réel des deux points
d'entrée d'export **effectivement câblés côté API** (`DeclarationWorkflowService.cs`), j'ai constaté
qu'**aucun des deux ne passe par `ConstructeurDeclaration`** — voir détail § « Reste à valider », point
1. Conséquence concrète : tel quel, le nouveau champ `Reference` **ne s'affichera jamais rempli** dans
un export réel généré depuis l'API aujourd'hui, malgré une implémentation par ailleurs correcte et
testée à chaque étage. Ce n'est pas un défaut de mon implémentation — c'est un écart entre le périmètre
écrit dans la TASK et l'architecture réelle du pipeline d'export, découvert pendant l'implémentation.
Documenté ici plutôt qu'improvisé (pas de modification de schéma `DM_LGTVA` tentée — hors périmètre
strict de TASK-187, décision de compromis explicite).

## Périmètre livré

1. **SQL** : `E.DO_Reference AS Reference` ajouté aux **4** requêtes de
   `Declaration.Selection/SelectionExpliqueeService.cs` (`GetSurensembleFournisseurSql`,
   `GetSurensembleDepenseSql`, `GetSurensembleClientSql`, `GetFactureFirstSql`) — aucun nouveau JOIN
   (`RT_ECHEANCE E` déjà présente dans les 4).
2. **Modèle** : nouveau champ `string? Reference` propagé de bout en bout :
   - `AffectationCandidateRow` (`Declaration.Selection/SelectionExpliqueeEvaluator.cs`)
   - `SelectionExpliqueeEvaluator.EvaluerAsync` : `Reference = r.Reference` sur l'`AffectationADeclarer` produit
   - `AffectationADeclarer` et `LigneDeclarationEnrichie` (`Declaration.Core/Model.cs`)
   - `ConstructeurDeclaration.cs` : `Reference = affectation.Reference` sur chaque `LigneDeclarationEnrichie` générée
3. **Export Excel** (`Declaration.Export.Excel/Exporter.cs`, `CreerFeuilleFacturesControle`, feuille
   « Factures à déclarer ») : nouvelle colonne **« Référence »** insérée en **2ᵉ position, juste après
   « N° Facture »** — décale « N° Règlement » (TASK-162) en 3ᵉ position, et tous les index suivants de
   +1 (Mode Paiement 13→14, Date Paiement 14→15, Date Facture 15→16, Source 16→17).

## Décision documentée — emplacement de la colonne

La TASK proposait « après N° Facture, cohérent avec l'ordre choisi pour N° Règlement (TASK-162) », en
notant explicitement que ce point était à confirmer si le PO a une préférence différente. J'ai retenu
la lecture littérale : **Référence directement après N° Facture** (position 2), donc AVANT N° Règlement
(qui recule en position 3) plutôt qu'après. Alternative possible (Référence après N° Règlement, donc
en position 3) tout aussi défendable — **à confirmer en revue**, changement non structurant si le PO
préfère l'ordre inverse.

## Décision documentée — feuille « Détail » (export officiel clôture, `CreerFeuilleDetail`)

La TASK ne mentionne QUE `CreerFeuilleFacturesControle` (feuille « Factures à déclarer », export de
contrôle ad-hoc) dans son périmètre strict « Inclus ». Mais `CreerFeuilleDetail` (feuille « Détail »,
utilisée par `ExporterExcel`/`GenererFichiersExportAsync`, l'export **officiel** d'une déclaration
Clôturée) partage un mapping identique dupliqué (commentaire TASK-162 : « code auparavant dupliqué à
l'identique »). L'étape 3 de la TASK demandait explicitement de trancher ce point plutôt que dupliquer
sans certitude — **je n'ai PAS touché `CreerFeuilleDetail`**, conformément au périmètre strict écrit.
Point à confirmer par le PO : si la colonne Référence doit aussi apparaître dans l'export officiel de
dépôt (feuille « Détail »), une TASK de suite symétrique (même patron que TASK-162→TASK-187 pour cette
feuille) sera nécessaire.

## Fichiers modifiés

- `Declaration.Selection/SelectionExpliqueeService.cs` — 4 lignes SQL ajoutées (1 par requête).
- `Declaration.Selection/SelectionExpliqueeEvaluator.cs` — `Reference` sur `AffectationCandidateRow` +
  propagation dans `EvaluerAsync`.
- `Declaration.Core/Model.cs` — `Reference` sur `AffectationADeclarer` et `LigneDeclarationEnrichie`.
- `Declaration.Core/ConstructeurDeclaration.cs` — `Reference` propagée vers `LigneDeclarationEnrichie`.
- `Declaration.Export.Excel/Exporter.cs` — colonne « Référence » dans `CreerFeuilleFacturesControle`
  (`CreerFeuilleDetail` non touchée, cf. décision documentée ci-dessus).
- `Declaration.Core.Tests/ConstructeurDeclarationTests.cs` — nouveau test
  `ConstruireDeclaration_Reference_PropageeRenseigneeEtNull`.
- `Declaration.Selection.Tests/SelectionExpliqueeEvaluatorTests.cs` — nouveau test
  `Evaluer_Reference_PropageeRenseigneeEtNull`.
- `Declaration.Export.Excel.Tests/ExporterTests.cs` — fixture `GetFixtureControle` étendue (2ᵉ ligne
  avec `Reference = null`, cas réel `FC2501193`), assertions de colonnes mises à jour pour le décalage.

## Tests / fixtures existants — vérification (Étape 4 de la TASK)

- Ajout de 3 tests dédiés couvrant explicitement le cas `Reference` renseignée ET le cas NULL (comme
  exigé), sans aucune exception dans les deux cas.
- Tests existants mis à jour pour le décalage de colonnes introduit dans `CreerFeuilleFacturesControle`
  (indices +1 à partir de la position 3) — `CreerFeuilleDetail` (feuille « Détail », non touchée)
  inchangée, ses assertions n'ont pas bougé.

## Rejeu réel (lecture seule) — GR_EMA_DISTRIBUTION / DESKTOP-5BFKKEP

```
EC_Id   NumeroFacture   DateFacture   Reference
18195   FC2600001       2026-01-01    FCN°929-26214   (renseignée)
21466   FC2501193       2025-07-18    (vide/NULL)      (cas réel cité par la TASK-187, confirmé vide)
```
Confirmé : la requête corrigée expose bien `E.DO_Reference` pour les deux cas, populée pour
`EC_Id=18195` et NULL pour `EC_Id=21466` (`FC2501193`, comme anticipé par la TASK elle-même à son
étape 4 : « cf. FC2501193 où DO_Reference est vide dans l'échantillon vérifié »).

## Build

- `Declaration.Selection`, `Declaration.Core`, `Declaration.Export.Excel` → build individuel OK, 0 erreur.
- `dotnet build DeclarationTVA.slnx` (solution complète) : **même blocage environnemental que TASK-186**
  (process `Declaration.API.exe` PID 33068 toujours actif, verrou toujours refusé par l'OS — revérifié,
  aucun changement). Voir `VERIFY/TASK-186_verify.md` pour le détail complet de ce point, non répété ici.

## Tests

- `Declaration.Core.Tests` → **89/89** verts (88 existants + 1 nouveau).
- `Declaration.Export.Excel.Tests` → **3/3** verts (fixture étendue, mêmes tests).
- `Declaration.Selection.Tests` → **59/60** verts (58 existants + 1 nouveau ; le seul échec est le
  même échec préexistant et non lié décrit dans `VERIFY/TASK-186_verify.md`, indépendant de ce
  changement).
- `Declaration.Orchestration.Tests` → **188/188** verts (via `--output`, même contournement que
  TASK-186 — ce changement ne touche aucun fichier référencé par ces tests hors `Declaration.Core`/
  `Declaration.Selection`, déjà couverts ci-dessus).

## Validation checklist

- [x] Build OK (bibliothèques concernées individuellement — solution complète bloquée par le même
      process que TASK-186, pas par le code)
- [x] Tests passés (Core 89/89, Export.Excel 3/3, Orchestration 188/188 ; Selection 59/60, échec
      préexistant non lié)
- [x] Colonne « Référence » présente et correcte dans `CreerFeuilleFacturesControle` — vérifiée par
      test unitaire (cas renseigné + cas NULL) et par rejeu SQL réel (2 cas réels)
- [~] Aucune régression sur les colonnes existantes de cette feuille : **vérifiée pour
      `CreerFeuilleFacturesControle`** (tests mis à jour, décalage assumé et testé) ; `CreerFeuilleDetail`
      non touchée par choix documenté, donc aucune régression possible dessus non plus
- [x] Aucun bypass sécurité
- [~] Aucune dette technique silencieuse : le point majeur (§ ci-dessus) est signalé explicitement, pas
      caché — mais c'est bien une limite fonctionnelle réelle du livrable tel que scopé, pas un simple
      "non vérifié"

## Impacts détectés

- Aucune régression sur le chemin facture-first ni sur les 3 chemins règlement-first pour les champs
  déjà en place (SQL additif uniquement, aucune colonne existante retirée/renommée).
- `DM_LGTVA` (schéma), `LigneCandidate` (modèle persistant), `DeclarationWorkflowService.MapLignesCandidates`
  (écriture des lignes persistées), `ConstruireModeleControleAsync`/`ConstruireModeleExportAsync`
  (lecture pour export) : **non touchés**, car strictement hors périmètre écrit dans TASK-187 — voir
  § « Reste à valider » pour la conséquence fonctionnelle de cette limite.

## Notes worker

- Décision de ne pas étendre le périmètre à `DM_LGTVA`/`LigneCandidate` de ma propre initiative :
  ajouter une colonne à une table de persistance est un changement de schéma, jamais anodin (règle
  ARCHITECTURE.md §5 — toute décision de compromis documentée, jamais de bypass), et la TASK ne
  l'autorise pas explicitement. Signalé pour arbitrage plutôt qu'improvisé.
- Le choix de position de colonne (Référence avant ou après N° Règlement) et le traitement de la
  feuille « Détail » sont deux points ouverts distincts, documentés séparément ci-dessus — aucun des
  deux ne bloque le reste du livrable, tous deux ajustables sans impact structurel.

## Reste à valider (NON couvert par ce VERIFY — bloquant clôture fonctionnelle réelle)

1. **Écart architecture découvert (le point le plus important de ce VERIFY)** : `ConstructeurDeclaration.
   ConstruireDeclaration` (que j'ai correctement étendu) n'est appelé **que** depuis
   `Declaration.Orchestration/OrchestrateurDeclaration.cs:437`, dont le résultat sert à peupler
   `LigneCandidate` (via `DeclarationWorkflowService.MapLignesCandidates`) qui est ensuite **persisté
   dans `DM_LGTVA`**. Or les DEUX méthodes qui alimentent réellement les exports Excel côté API —
   `ConstruireModeleExportAsync` (export officiel, déclaration Clôturée) et `ConstruireModeleControleAsync`
   (export de contrôle ad-hoc, TASK-160, celui qui produit la feuille « Factures à déclarer » que j'ai
   modifiée) — **construisent leurs `LigneDeclarationEnrichie` directement depuis `LigneCandidate`/
   `GetLignesAsync` (lecture DM_LGTVA persistée), sans jamais repasser par `ConstructeurDeclaration`**.
   Vérifié : `DM_LGTVA` (schéma réel, 30 colonnes) **n'a pas de colonne `Reference`**, et
   `MapLignesCandidates` ne mappe pas ce champ vers `LigneCandidate` (qui n'a pas non plus cette
   propriété). Conséquence : générer aujourd'hui un export réel (officiel ou de contrôle) depuis l'API
   affichera **une colonne « Référence » systématiquement vide, quelle que soit la vraie valeur de
   `DO_Reference` en base** — la propagation que j'ai livrée est correcte et testée à chaque étage,
   mais elle s'arrête avant d'atteindre la persistance, qui est le véritable point de passage des deux
   exports réels. **Je n'ai pas corrigé ce point** : cela nécessiterait une migration de schéma
   `DM_LGTVA` + colonne `LigneCandidate` + mise à jour de `MapLignesCandidates` et des deux méthodes
   `ConstruireModele*Async` — un ensemble de changements plus large que le périmètre strict écrit dans
   TASK-187, qui ne mentionne ni `DM_LGTVA` ni `LigneCandidate` ni ces deux méthodes. **Recommandation :
   ouvrir une TASK de suite dédiée** (proposition : TASK-188) si le PO veut que ce champ soit
   effectivement visible dans un export réel généré par l'application.
2. Par conséquence directe du point 1, **je n'ai pas pu produire l'export réel demandé par le prompt
   worker** (« génère un export réel montrant la colonne Référence peuplée sur un cas renseigné et un
   cas vide ») **via l'API/le pipeline de génération réel** — un tel export afficherait la colonne vide
   pour toutes les lignes aujourd'hui. La preuve apportée à la place est (a) le test unitaire bout-en-bout
   `ConstruireDeclaration_Reference_PropageeRenseigneeEtNull` + `ExporterExcelControle_Genere3FeuillesConformes`
   (chaîne complète SQL simulé → modèle → colonne Excel, avec assertions sur les deux cas), et (b) le
   rejeu SQL direct sur données réelles (`EC_Id=18195`/`21466`) montrant que la source de données
   (`RT_ECHEANCE.DO_Reference`) est bien lue correctement par le SQL corrigé. Jugée insuffisante par
   rapport à la demande littérale — signalé explicitement plutôt que présenté comme équivalent.
3. Build de la solution complète non rejoué avec succès — même blocage que TASK-186 (process
   `Declaration.API.exe` verrouillé, non résolu).
4. Position exacte de la colonne (avant/après N° Règlement) et extension éventuelle à la feuille
   « Détail » : décisions par défaut documentées, à confirmer par le PO (voir sections dédiées
   ci-dessus).

## Verdict

Propagation du champ `Reference` livrée et testée de bout en bout **jusqu'à `ConstructeurDeclaration`/
`Exporter.cs`**, exactement dans le périmètre écrit. **Mais découverte critique en cours
d'implémentation : ce chemin n'est actuellement emprunté par AUCUN export réellement généré par
l'API** — les deux méthodes réelles lisent `DM_LGTVA` directement, table qui n'a pas ce champ. Le
livrable est donc fonctionnellement incomplet par rapport à l'intention de la demande PO, pour une
raison structurelle non anticipée par le périmètre écrit de TASK-187, et non par une erreur
d'implémentation. Recommandation : ne pas clôturer TASK-187 sans decision PO sur le point 1 — soit
ouvrir TASK-188 (extension DM_LGTVA/LigneCandidate/MapLignesCandidates/ConstruireModele*Async), soit
accepter le livrable actuel comme fondation prête pour cette extension.
