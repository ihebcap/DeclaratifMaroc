# VERIFY — TASK-108 — Écart d'équilibre = artefact d'ensembles à l'étape ②

## Résumé
Correctif d'un seul terme du contrôle d'équilibre (`DeclarationsController.cs:271`,
`GetCheckup`) : `totalTva` était calculé sur les seules lignes `Integree`, alors que
`TotalDeclareTtc`/`TotalMontantAffecte` (produits par `DeclarationWorkflowService.GetCheckupAsync`)
sont déjà agrégés sur `Integree || Proposee` (`lignesRecap`, déjà défini en TASK-103). Sur une
déclaration `EnCours` (0 ligne `Integree`), l'écart affiché valait donc `Σ_Proposee(TVA)` — la TVA
totale de la déclaration mal étiquetée « écart détecté ». Un seul ensemble de vérité pour les
trois termes corrige le faux positif.

## Modification réalisée
| Fichier | Avant | Après |
|---|---|---|
| `Declaration.API/Controllers/DeclarationsController.cs:271` | `var totalTva = integrees.Sum(l => l.TVA);` (variable `integrees` = `Integree` seul) | `var totalTva = lignesRecap.Sum(l => l.TVA);` (même ensemble `Integree\|\|Proposee` que `recapSource`/`recapTaux`) |

La variable locale `integrees` (devenue inutilisée) a été supprimée ; `integreesCount` (utilisé
par le bloc `reconciliation`, périmètre distinct — TASK-058) est inchangé.

**Exclu du périmètre, non touché** (conforme au périmètre STRICT de la task) : `recapSource`,
`recapTaux`, `DeclarationWorkflowService.GetCheckupAsync` (calcul de `TotalDeclareTtc`/
`TotalMontantAffecte`), tout composant front.

## Tests ajoutés — `Declaration.Orchestration.Tests/Task108EcartEquilibreEnsembleUniqueTests.cs`
3 tests, tous verts :
1. `GetCheckup_DeclarationEnCoursLignesProposeeEquilibrees_EcartQuasiNul` — déclaration `EnCours`,
   lignes `Proposee` avec `TTC=HT+TVA` (cas réel TVA1-2026-06/-07) → `equilibre.isValid=true`,
   `ecart≈0`.
2. `GetCheckup_DeclarationClotureeLignesIntegreeEquilibrees_EcartQuasiNulInchange` —
   non-régression écran ⑤ : lignes `Integree`, ensembles déjà coïncidents avant/après → `ecart≈0`
   inchangé.
3. `GetCheckup_LigneReellementIncoherenteTtcDifferentDeHtPlusTva_EcartNonNulPersiste` — un vrai
   résidu `TTC≠HT+TVA` (ligne `HT=1000/TVA=200/TTC=1300` au lieu de 1200) → `isValid=false`,
   `ecart=100` : le contrôle reste sensible aux vraies anomalies.

```
Réussi! - échec : 0, réussite : 133, ignorée(s) : 0, total : 133, durée : 436 ms
```
(suite complète `Declaration.Orchestration.Tests` rejouée, aucune régression — dont
`Task103RecapSourceProposeeTests` et `Task082LigneExclueDesLeFigeageTests` déjà en place).

Build solution complète (`Declaration.API`) : 0 erreur, warnings préexistants uniquement.

## Preuve sur données réelles — `GR_EMA_DISTRIBUTION` (`DESKTOP-5BFKKEP`)
API lancée en local (`dotnet run`, port 5099) avec `connections.json` réel, login `Admin`/`Admin`,
appel réel `GET /api/declarations/{id}/checkup` sur les deux déclarations `EnCours` présentes en
base :

| Déclaration | Statut | Écart **avant correctif** (référence : TASK-103/108, valeur = TVA totale) | Écart **après correctif** (mesuré ce jour) |
|---|---|---|---|
| `TVA1-2026-06` | `EnCours` (0) | 1 538,27 | **0,000000** — `isValid=True` |
| `TVA1-2026-07` | `EnCours` (0) | 1 480,50 | **0,000000** — `isValid=True` |

Sortie brute du script (`scratch/verify_task108.ps1`) :
```
=== TASK-108 VERIFY - ecart equilibre etape 2 sur donnees reelles ===
Login OK.
--------------------------------------------------------
TVA1-2026-06 (Statut=0) - id=4ade2191-235c-43de-9801-64826dd197d5
  equilibre.isValid = True ; ecart = 0,000000
--------------------------------------------------------
TVA1-2026-07 (Statut=0) - id=214a50b1-168b-46f8-be80-f3021c1e202f
  equilibre.isValid = True ; ecart = 0,000000
```

`TVA1-2026-01` (référence du signalement d'origine, 341 155,01) n'est plus présente dans la base
de test actuelle (`GET /api/declarations?societeId=1` ne retourne que les deux déclarations
`EnCours` de juin/juillet 2026) — non bloquant : la preuve porte sur les deux déclarations
`EnCours` réellement disponibles, cas identique (0 ligne `Integree`, lignes `Proposee`).

**Non-régression écran ⑤ (`Cloturee`)** : aucune déclaration `Cloturee` n'est présente dans la
base de test actuelle pour rejouer une preuve réelle — couverte par le test unitaire n°2
ci-dessus (scénario déjà validé en réel par TASK-087, cf. capture `task087-step5-no-regression.png`
dans `VERIFY/`, comportement du code inchangé sur ce chemin).

**Aucun montant déclaré, aucune ligne, aucune sélection modifiés** — vérifié : seul le libellé
« écart » change de valeur (0 au lieu de la TVA totale) ; `recapSource`, `recapTaux`,
`controleEquilibre` (bruts back), `reconciliation` retournés à l'identique.

## Critères de validation
- [x] Sur une déclaration `EnCours` équilibrée, l'étape ② n'affiche plus de faux « écart détecté »
      (preuve réelle `TVA1-2026-06`/`-07` : `ecart=0`).
- [x] L'écran ⑤ (`Cloturee`) reste inchangé (test unitaire non-régression, code du chemin
      `Cloturee` non modifié).
- [x] Un vrai résidu `TTC≠HT+TVA` déclenche encore le contrôle (test unitaire n°3, `ecart=100`).
- [x] Aucun montant déclaré, aucune ligne, aucune sélection modifiés (correctif d'un seul calcul
      d'affichage/contrôle, 1 ligne).

## Statut
Prête pour revue architecte.
