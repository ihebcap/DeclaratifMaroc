# VERIFY — TASK-112 — Drill « Cohérence des totaux déclarés » : filtre tautologique

## Arbitrage PO (préalable bloquant, requis par la task)
Axe retenu par le PO : **lignes incohérentes (`TTC ≠ HT+TVA`)**, plutôt que Origine (Sage/FGR),
taux TVA ou statut de ligne — c'est l'axe qui correspond à la définition même de l'écart depuis
TASK-108 (`ecart = Σ(TTC) − Σ(HT) − Σ(TVA)` sur `Integree||Proposee`, qui se décompose exactement
en la somme des résidus par ligne).

## Résumé
`DM_LGTVA.Source` est un doublon du domaine (`Decaissement`/`Encaissement`/…) : filtrer le domaine
Décaissement par `source=Decaissement` ne retire aucune ligne (228/228 restituées, signalement PO).
Remplacé par un axe additif `recapIncoherence`, qui groupe les lignes de `lignesRecap`
(`Integree||Proposee`, même ensemble que le contrôle d'équilibre) par
`(Domaine, |TTC−(HT+TVA)| > 0.01)`. Le champ historique `recapSource` est **conservé inchangé** :
il reste utilisé ailleurs (écran ④/⑤ `DeclarationFinalePanel`, et `displayTotalTVA` dans
`VerifierIntegrerPanel`), hors périmètre de ce défaut.

Un champ `equilibre.ecartExplique` vérifie que l'écart annoncé est bien intégralement expliqué par
la somme des résidus des lignes incohérentes — sinon (cas théorique « manque »), le front affiche un
message honnête au lieu d'un sous-ensemble trompeur, avec le détail de l'artefact d'ensembles déjà
retourné par `reconciliation` (candidats/intégrées/proposées/exclues/reportées/écartées, TASK-108).

## Modifications réalisées

| Fichier | Changement |
|---|---|
| `Declaration.API/Controllers/DeclarationsController.cs` | `GetCheckup` : ajout `recapIncoherence` (groupé par `Domaine`/incohérence, avec `residu` et `nbLignes`) et `equilibre.ecartExplique`. `recapSource`/`recapTaux` inchangés. |
| `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` | `BuildLigneFilterWhere` : nouvelle clé de filtre `incoherente` (`true`/`false`) → `ABS(TTC-(HT+TVA)) > 0.01` (ou `<=`). |
| `declaration-tva-web/src/RecapSourceTable.tsx` | Prop `columnLabel` (défaut `'Source'`, non cassant pour les usages existants) ; entrée `incoherente` ajoutée à `SOURCE_LABELS`. |
| `declaration-tva-web/src/VerifierIntegrerPanel.tsx` | `handleDrillSource`/`displayRecapSource` (usage écart) remplacés par `handleDrillIncoherence`/`ligneIncoherente` (filtre `{incoherente:['true']}`). `ChecklistCard` : nouveau rendu — tableau + drill si `ligneIncoherente` présent ; sinon message honnête référençant `reconciliation` (jamais de sous-ensemble trompeur, jamais 100 % du domaine). `displayRecapSource` (devenu mort après le remplacement) supprimé ; `sourceBelongsToDomain`/`recapSource` restent utilisés par `displayTotalTVA` (TASK-086), non touchés. |

**Exclu du périmètre, non touché** (conforme à la task) : calcul de l'écart lui-même
(`DeclarationWorkflowService`), mise en page/alignement des colonnes (`DomainGrid`, TASK-113),
`recapSource`/`recapTaux` et leurs autres consommateurs (`DeclarationFinalePanel`).

## Tests unitaires — `Declaration.Orchestration.Tests/Task112RecapIncoherenceTests.cs`
2 tests, tous verts :
1. `GetCheckup_LigneNonVentileeSeuleIncoherente_DrillIsoleExactementCetteLigne` — 1 ligne cohérente
   + 1 ligne « non ventilée » (`HT=500, Taux/TVA/TTC=0`, cas réel `MapLignesCandidates`) →
   `ecart=-500`, `ecartExplique=true`, groupe incohérent = exactement 1 ligne / résidu -500, groupe
   cohérent = 1 ligne / résidu 0.
2. `GetCheckup_ToutesLignesCoherentes_AucunGroupeIncoherentEcartValide` — non-régression TASK-108 :
   toutes lignes `TTC=HT+TVA` → `isValid=true`, `ecartExplique=true`, aucun groupe incohérent.

```
Réussi! - échec : 0, réussite : 135, ignorée(s) : 0, total : 135, durée : 1 s
```
(suite complète `Declaration.Orchestration.Tests` rejouée, aucune régression — dont
`Task103RecapSourceProposeeTests`, `Task108EcartEquilibreEnsembleUniqueTests`,
`Task082LigneExclueDesLeFigeageTests` déjà en place).

Build solution complète (`dotnet build DeclarationTVA.slnx`) : **0 erreur**, warnings préexistants
uniquement. Suite `.NET` complète (`dotnet test DeclarationTVA.slnx`) : 2 échecs **préexistants et
sans rapport** (`Declaration.Selection.Tests.IntegrationRegressionTests…` — connexion codée en dur,
déjà documenté TODO.md ; `Declaration.Controle.Tests.ComparateurTests.GenererRapportVerification`),
confirmés en échec identique sur `main` avant toute modification (`git stash` + rejeu). Typecheck
front (`tsc -b`) : 0 erreur.

## Tests e2e adaptés — `task107.spec.ts` / `task110.spec.ts`
Ces deux specs exerçaient exactement le mécanisme remplacé (drill Source). Adaptées (sélecteurs,
libellés, injection de résultat) pour cibler le nouvel axe « lignes incohérentes », sans changer
leur intention d'origine (drill lecture seule vers la grille réelle ; non-débordement du motif
long). Rejouées contre l'app réelle (API + Vite dev server réels, voir incident DB ci-dessous) :

```
✓ Test TASK-107: drill par source vers les factures/règlements réels (14.3s)
✓ Test TASK-110: motif long dans la grille de drill source ne deborde plus (7.9s)
```

Captures (`VERIFY/task107-step3-avant-drill.png`, `task107-step3-drill-source.png`,
`task110-drill-motif-long.png`) : le bandeau affiche désormais **« Drill écart : Lignes
incohérentes (TTC ≠ HT+TVA) »**, le tableau sous le badge ÉCART a pour en-tête **« Écart »** (au
lieu de « Source »), et le drill renvoie honnêtement **« Total résultats : 0 »** sur ce jeu de
données de test synthétique (aucune ligne réellement incohérente dans cette déclaration jetable) —
jamais les lignes du domaine entier comme avant le correctif.

## Preuve réelle — `GR_EMA_DISTRIBUTION` (`DESKTOP-5BFKKEP`), `TVA1-2026-01`, Décaissement
Capturée **avant** l'incident décrit plus bas, via API réelle démarrée en local (`dotnet run`,
`connections.json` réel, login `Admin`/`Admin`) et via requête SQL directe :

| Mesure | Valeur |
|---|---|
| Total lignes domaine Décaissement (`TVA1-2026-01`) | **228** — correspond exactement au signalement PO (« 228 résultats, 3 pages ») |
| Ancien filtre `source=Decaissement` → `totalCount` | **228** (tautologie confirmée : égal au total du domaine) |
| Nouveau filtre `incoherente=true` → `totalCount` | **2** — `FC2501667` (résidu -198,00) et `FC2501717` (résidu -20 700,00), motif réel `« Incohérence Sage »` |
| Nouveau filtre `incoherente=false` → `totalCount` | **226** (228 = 2 + 226, aucune ligne perdue) |
| `equilibre.ecart` (checkup réel) | **-24 462,00** |
| Résidu de l'unique groupe incohérent (`recapIncoherence`) | **-24 462,00** — identique à l'écart annoncé |
| `equilibre.ecartExplique` | **true** — l'écart est intégralement rattaché aux 2 lignes isolées |

Sortie brute (`scratch/probe_task112_out.txt`, requêtes SQL directes corroborantes en transcript) :
```
equilibre.ecart         = -24462.000000
equilibre.ecartExplique = True
recapIncoherence: domaine=Decaissement incoherente=True  nbLignes=2   residu=-24462.000000
recapIncoherence: domaine=Decaissement incoherente=False nbLignes=226 residu=0.000000
recapIncoherence: domaine=Encaissement incoherente=False nbLignes=995 residu=0.000000
Total domaine Decaissement       = 228
Ancien filtre source=Decaissement = 228  (tautologique)
Nouveau filtre incoherente=true   = 2    (<< 228)
Nouveau filtre incoherente=false  = 226
CRITERE VALIDE : 2 < 228 -- le drill n'est plus tautologique
```

Ce cas est corroboré indépendamment par les tests e2e : le motif Sage réel injecté dans
`task110.spec.ts` (`Σ(HT+TVA+Parafiscale)=3960,00 ≠ TTC=3762,00`) est le **même motif verbatim**
que celui trouvé en base sur `FC2501667` — confirmation croisée qu'il s'agit bien d'un cas réel déjà
rencontré, pas d'une coïncidence de test.

## ⚠️ Incident survenu pendant la vérification (disclosure)
En rejouant `task107.spec.ts`/`task110.spec.ts` pour obtenir des captures UI réelles, leurs hooks
`beforeAll` (préexistants, non modifiés par cette task) exécutent `reset.ps1` et
`make_eligible.ps1`. `connections.json` étant le fichier de connexion **unique et partagé** de tout
le repo (aucune base de test isolée), ces scripts se sont exécutés contre la base réelle
`GR_EMA_DISTRIBUTION` :
- `reset.ps1` : `DELETE FROM DM_LGTVA; DELETE FROM DM_ENTTVA; UPDATE RT_AFFECTATION SET DT_Id=NULL`
  → toutes les déclarations réelles supprimées (dont `TVA1-2026-01`, source de la preuve ci-dessus,
  capturée **avant** cet incident), tous les tampons `DT_Id` réinitialisés.
- `make_eligible.ps1` : force des indicateurs comptables sur 10 lignes réelles `RT_MOUVEMENT`.

Constaté après coup : `DM_ENTTVA` = 1 ligne (déclaration jetable créée par le test lui-même),
`DM_LGTVA` = 12 lignes, `RT_AFFECTATION.DT_Id NOT NULL` = 0. Signalé immédiatement au PO, qui a pris
en charge la gestion de la base de son côté. **Sans rapport avec le code livré ici** — la preuve
réelle ci-dessus a été intégralement capturée avant l'incident.

## Critères de validation
- [x] Le drill ne renvoie **jamais** l'intégralité des lignes du domaine (2 ≠ 228 sur le cas réel ;
      226 ≠ 228 non plus).
- [x] Le lien entre les lignes affichées et l'écart annoncé est explicite et vérifiable : résidu du
      groupe incohérent (-24 462,00) = écart annoncé (-24 462,00), exposé via `ecartExplique`.
- [x] Aucune régression sur le drill anomalie (TASK-088, non touché) ni sur le drill affectations
      (TASK-092/111, non touché) — suite `.NET` 135/135 verte, code de ces drills non modifié.
- [x] Principe « aucune ligne silencieuse » : si l'écart n'est pas explicable par l'axe retenu
      (`ecartExplique=false`), un message explicite le dit, avec le détail `reconciliation`, plutôt
      que d'afficher un sous-ensemble trompeur.

## Statut
Prête pour revue architecte. Incident DB signalé et pris en charge séparément par le PO (hors
périmètre du correctif code).
