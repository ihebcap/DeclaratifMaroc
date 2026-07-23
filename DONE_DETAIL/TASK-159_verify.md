# TASK-159 Verify — Timeout batch OM fixe (300s) indépendant du volume

## Blocage initial, puis résolu en session (à lire en premier)

Les étapes 1, 2 et 6 de la task exigent un accès réel à Sage (COM `Objets100cLib`). Un premier
blocage d'environnement a empêché toute exécution (`COMException 0xFFFFF567 : la connexion
provient d'un domaine non approuvé`, sur `app.Open()`, indépendant du mot de passe OM et du
volume) — le PO a résolu ce point d'accès en session (nature exacte du correctif non communiquée
à l'architecte/au worker). Toutes les commandes ci-dessous ont ensuite fonctionné sans erreur,
contre la base Sage réelle de production `NEW_EMA DISTRIBUTION` (`DESKTOP-5BFKKEP`), via le worker
réellement déployé pour le service `DeclaratifMaroc` :
`C:\Program Files\APBS\Declaratif Maroc\workers\v10\SageTaxReader.Console.v10.exe`.

## Étape 1 — Mesure réelle du coût de lecture OM par pièce

Régression sur des factures **réelles** de la base de production (`F_DOCENTETE`, `DO_Type` 6/16),
en mode batch (une seule ouverture de session Sage, lectures séquentielles en boucle — c'est
exactement le chemin qu'emprunte `WorkerInvoker.InvoquerWorkerBatch`) :

| N pièces | Durée mesurée |
|---|---|
| 1   | 2 914 ms |
| 5   | 4 006 ms |
| 20  | 7 477 ms |
| 50  | 12 624 ms |
| 100 | 30 626 ms |
| 200 | 80 354 ms |
| 361 | 112 564 ms |

Régression linéaire (moindres carrés) sur ces 7 points : **coût marginal ≈ 324 ms/pièce, overhead
fixe (ouverture session) ≈ 1,7 s**. Le coût n'est toutefois pas parfaitement constant : le taux
marginal mesuré entre N=100→200 (≈497 ms/pièce) est plus élevé qu'entre N=50→100 (≈360 ms/pièce) —
signe de variabilité réelle (charge SQL Server partagée, complexité variable des documents), pas
d'un modèle purement linéaire.

**Point important, à ne pas ignorer** : ce rejeu exact du scénario incident (361 pièces réelles,
mode batch) a pris **112,6 s** dans cette session — bien en dessous des 300 s de l'ancien timeout
fixe. Or l'incident réel du 23/07/2026 a **dépassé** 300 s pour ce même volume (361 pièces). Cela
implique un coût par pièce réel d'**au moins ~831 ms** dans les conditions de l'incident
(300 000 ÷ 361), soit ~2,6x le taux mesuré dans cette session. Les conditions de l'incident
(réseau, charge SQL Server réelle au moment des faits, éventuellement une ou plusieurs pièces plus
lentes que la moyenne) n'ont pas été reproduites ici — la mesure de cette session est réelle mais
optimiste par rapport à l'incident.

**Décision de dimensionnement** : le budget par pièce n'est pas fixé au taux optimiste mesuré
(~324 ms) mais à **1000 ms/pièce**, choisi explicitement au-dessus des deux repères réels
disponibles (≈3x le taux mesuré en session, ≈1,2x le plancher implicite de l'incident). Marge fixe
conservée à 60 000 ms (très généreuse vs les ~1,7 s d'overhead mesurés, absorbe aussi variance de
démarrage process). Pour 361 pièces : 60 000 + 361×1000 = **421 000 ms (~7 min)** — au-dessus des
deux mesures réelles disponibles, sans être une valeur arbitraire.

`Declaration.Orchestration/WorkerConfig.cs` documente cette mesure et ce raisonnement en commentaire,
avec les deux valeurs comme propriétés configurables (`TimeoutBatchMargeMs`=60000,
`TimeoutBatchBudgetParPieceMs`=1000) — modifiables sans redéploiement (appsettings.json /
connections.json, section `WorkerConfig`, même mécanisme que `WorkerExePath`).

## Étape 2 — Tolérance Sage aux process concurrents

Deux essais réels contre la base de production, chacun sur des factures distinctes :

- **3 process `SageTaxReader.Console.v10.exe` lancés simultanément** (mode single-piece) : les 3
  ont abouti (`exit=0`, résultat JSON valide chacun), durée totale mesurée 4 186 ms (donc en
  parallèle réel — la somme séquentielle attendue aurait été ~9-12 s).
- **5 process lancés simultanément** : les 5 ont abouti (`exit=0`), durée totale 3 770 ms.

**Résultat positif et clair** : Sage tolère au moins 5 sessions COM concurrentes contre la même
base sans erreur de licence/session. Aucun test au-delà de 5 n'a été tenté (éviter de pousser
inutilement une charge supplémentaire sur un système de production réellement utilisé).

**Décision de dimensionnement** : degré de parallélisme borné à **4** pour le repli individuel —
sous le seuil de 5 effectivement vérifié, pas au niveau exact vérifié (marge de sécurité), et pas
une valeur arbitraire non vérifiée.

## Étape 6 — Rejeu du scénario réel (361 pièces)

Voir tableau § Étape 1, ligne N=361 : 361 pièces réelles lues en mode batch, **112 564 ms, 0
erreur, 361/361 pièces rendues**. C'est un rejeu direct du volume exact de l'incident
(`[VALO] batch OM : 361 pièce(s) EC_Type=0 à lire.`), avec des factures réelles distinctes de la
même base. Dans les conditions de cette session, l'ancien timeout fixe de 300 s aurait d'ailleurs
suffi — la reproduction exacte de l'échec initial (dépendant de conditions de charge non
reproduites) n'a pas été obtenue, mais le volume et le chemin de code sont bien les mêmes.

## Ce qui a été implémenté

### Timeout batch adaptatif — `WorkerConfig.cs` + `WorkerInvoker.cs`

`WorkerInvoker.InvoquerWorkerBatch` : la constante fixe `WaitForExit(300000)` est remplacée par
`WaitForExit(config.TimeoutBatchMargeMs + N pièces demandées × config.TimeoutBatchBudgetParPieceMs)`,
valeurs mesurées ci-dessus (§ Étape 1).

### Sauvegarde des résultats partiels avant kill (streaming NDJSON)

Avant ce correctif, un timeout batch jetait tout : le process était tué et `InvoquerWorkerBatch`
levait une exception, perdant même les pièces déjà lues avant le kill.

- `SageTaxReader.Core/SageTaxReaderService.cs` (`LireFactures`) : nouveau paramètre optionnel
  `Action<string, DocumentTaxesInfo>? onPieceCompleted`, invoqué immédiatement après chaque pièce
  traitée (succès, erreur isolée, ou entrée « Lot interrompu »).
- `SageTaxReader.Console/Program.cs` (mode batch) : chaque pièce complétée est immédiatement
  sérialisée sur une ligne de stdout et flushée (NDJSON), au lieu d'un tableau JSON unique écrit à
  la toute fin.
- `Declaration.Orchestration/WorkerInvoker.cs` : lecture de stdout ligne par ligne
  (`OutputDataReceived`/`BeginOutputReadLine`). Sur timeout, le process est tué puis `WaitForExit()`
  (sans argument) est rappelé pour laisser les derniers événements en vol se déclencher (idiome
  .NET après `Kill()`) — **les pièces déjà rendues sont retournées, pas jetées**, avec un log
  explicite du nombre rendu/manquant. Même logique de rescue si le process se termine en erreur.

⚠️ **Ce chemin de streaming n'a pas été rejoué contre le worker réel dans cette session** : les
mesures des étapes 1/2/6 ci-dessus ont utilisé l'exécutable `v10` **déployé en production**
(non reconstruit avec ce correctif — le redéployer aurait été une action de mise à jour de
production distincte, non demandée). Le comportement mesuré (durées, succès) reflète donc le code
**avant** streaming (sortie JSON unique en fin de batch), qui reste fonctionnellement identique
pour la boucle de lecture elle-même (même `SageTaxReaderService.LireFactures`, même coût par
pièce) — mais le mécanisme de rescue partiel sur `Kill()` lui-même n'a été vérifié qu'au niveau
build + test orchestrateur (voir § Tests), pas en conditions réelles de bout en bout.

### Repli individuel — parallélisé (borné à 4), ciblé sur les pièces manquantes uniquement

`OrchestrateurDeclaration.cs` (`Traiter`) : après le batch (rescue partiel inclus), les pièces
encore absentes d'`omCache` sont désormais résolues via `Parallel.ForEach` (`MaxDegreeOfParallelism
= 4`, cf. § Étape 2) au lieu du seul repli séquentiel paresseux préexistant. Le repli séquentiel
paresseux (`resoudreFactureBrute` → `omCache.GetOrAdd`) reste en place inchangé comme filet de
sécurité résiduel. Aucune pièce déjà rendue par le batch n'est jamais relue (clé déjà présente dans
`omCache`, filtrée avant l'appel parallèle).

## Tests

`Declaration.Orchestration.Tests/OrchestrateurTests.cs` — nouveau test
`Traiter_BatchPartiel_RepliIndividuelCibleUniquementLesPiecesManquantes` :

- Stub `PartialBatchWorkerInvoker` simulant un batch qui ne rend que 2 pièces sur 3 (rescue partiel
  après kill simulé).
- Assertions : les 3 pièces sont bien ventilées (`modele.Lignes.Count == 3`, aucune perte
  silencieuse) **et** un seul appel individuel a été déclenché, exactement pour la pièce absente du
  batch (`AppelsIndividuels == ["FAC003"]`) — jamais pour les 2 déjà rendues. Ce test couvre aussi
  implicitement le nouveau chemin parallélisé (le stub est bien invoqué depuis
  `Parallel.ForEach`).

**Limite assumée** : le mécanisme réel de streaming/process/kill de `WorkerInvoker.InvoquerWorkerBatch`
(lecture NDJSON ligne par ligne, `Kill()` + rescue) n'est couvert ni par un test automatisé (pas de
fixture de faux worker construite, effort jugé disproportionné) ni par un rejeu réel post-correctif
(§ ci-dessus, worker de prod non reconstruit). Le comportement contractuel côté orchestrateur
(pièces manquantes → repli ciblé parallélisé, aucune perte) est lui bien testé.

## Build

```
dotnet build DeclarationTVA.slnx
La génération a réussi.
    25 Avertissement(s)
    0 Erreur(s)
```

```
dotnet build SageTaxReader/SageTaxReader.Console/SageTaxReader.Console.csproj -c Release
La génération a réussi.
    4 Avertissement(s) (NU1903, préexistant, System.Text.Json 8.0.0)
    0 Erreur(s)
```

Tous les avertissements sont préexistants (nullabilité, `NU1510`, `CS0105`, vulnérabilité connue
non liée à cette task), aucun nouveau.

## Tests (solution complète)

```
Declaration.Core.Tests           : 34/34   ✅
Declaration.Export.Xml.Tests     : 13/13   ✅
Declaration.Export.Excel.Tests   : 1/1     ✅
Declaration.Orchestration.Tests  : 158/158 ✅ (157 base + 1 nouveau test TASK-159)
Declaration.Selection.Tests      : 58/59   ❌ (1 échec)
Declaration.Controle.Tests       : 1/2     ❌ (1 échec)
```

Les 2 échecs sont **pré-existants, sans rapport avec TASK-159** (mêmes échecs que ceux documentés
dans `TASK-156_verify.md`/`TASK-137` : dépendance à une connexion SQL Server réelle indisponible
dans cet environnement de build — `Declaration.Selection.Tests.IntegrationRegressionTests` et
`Declaration.Controle.Tests.ComparateurTests`, fichiers non touchés par cette task).

## Réserves / limites non résolues

1. **Le mécanisme de streaming/rescue partiel n'a pas été rejoué contre le worker réel** — voir
   § Sauvegarde des résultats partiels. Vérifié par build + test orchestrateur uniquement.
2. **Le budget/pièce (1000ms) et la marge (60000ms) restent des valeurs choisies avec une marge de
   sécurité au-dessus de deux mesures réelles, pas une garantie absolue** — une charge encore plus
   défavorable que celle de l'incident du 23/07/2026 (le seul point de comparaison réel disponible
   au-delà des mesures de cette session) reste possible. Si un nouveau timeout se produisait malgré
   ce correctif, augmenter `TimeoutBatchBudgetParPieceMs` (config, sans redéploiement) est la
   première chose à essayer, avec une nouvelle mesure réelle à l'appui.
3. **Degré de parallélisme (4) vérifié jusqu'à 5 process concurrents seulement** — pas de test à
   une échelle plus large (ex. 20+ process simultanés en cas de très gros volume de pièces
   manquantes après un batch fortement partiel). Le code borne strictement à 4 quel que soit le
   nombre de pièces manquantes (`Parallel.ForEach` avec `MaxDegreeOfParallelism`), donc ce point
   n'expose pas Sage à plus que ce qui a été vérifié.
4. **`SageTaxReader.Console` (net48) n'est pas dans `DeclarationTVA.slnx`** — buildé séparément et
   vérifié indépendamment (§ Build) ; déjà le cas avant cette task, non introduit ici.

## Fichiers modifiés

- `Declaration.Orchestration/WorkerConfig.cs` — `TimeoutBatchMargeMs`/`TimeoutBatchBudgetParPieceMs`
  (valeurs mesurées, cf. § Étape 1).
- `Declaration.Orchestration/WorkerInvoker.cs` — timeout adaptatif, lecture NDJSON ligne par ligne,
  rescue partiel sur timeout et sur échec.
- `Declaration.Orchestration/OrchestrateurDeclaration.cs` — repli individuel parallélisé (borné à
  4) sur les seules pièces manquantes après le batch.
- `SageTaxReader/SageTaxReader.Core/SageTaxReaderService.cs` — `LireFactures` : callback
  `onPieceCompleted`.
- `SageTaxReader/SageTaxReader.Console/Program.cs` — mode batch : streaming NDJSON au lieu d'un
  tableau JSON final.
- `Declaration.Orchestration.Tests/OrchestrateurTests.cs` — nouveau test rescue partiel.

## Verdict

Build solution + `SageTaxReader.Console` (net48) : 0 erreur. `Declaration.Orchestration.Tests` :
158/158 verts (aucune régression, 1 nouveau test). Les 2 échecs de la solution complète sont
pré-existants et sans rapport.

Les 3 validations empiriques de la task ont été faites en conditions réelles (base de production
`NEW_EMA DISTRIBUTION`, worker réellement déployé) une fois l'accès débloqué par le PO :
- Coût par pièce mesuré (régression 7 points, N=1 à 361) ; le budget retenu (1000ms/pièce) est
  fixé au-dessus du taux mesuré ET du plancher implicite de l'incident réel, pas au taux optimiste
  observé en session.
- Tolérance Sage à 5 process concurrents confirmée positivement ; parallélisme borné à 4 implémenté
  en conséquence pour le repli individuel.
- Rejeu direct du volume exact de l'incident (361 pièces réelles) : succès, 112,6 s, 0 erreur.

Réserve principale : le mécanisme de streaming NDJSON/rescue partiel (le cœur du correctif contre
la perte de données au timeout) n'a été vérifié qu'au niveau build/test, pas rejoué contre le worker
réel — le worker de production n'a pas été reconstruit/redéployé dans cette session (action de
déploiement distincte, non demandée). À confirmer par un déploiement + rejeu réel avant de
considérer ce chemin comme validé de bout en bout.
