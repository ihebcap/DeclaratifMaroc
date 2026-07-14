# TASK-081 — Bandeau incohérence absent au premier figeage d'une déclaration (régression fonctionnelle)

Status: ✅ APPROUVÉE (2026-07-14)
Priority: CRITICAL
Risk: LOW (lecture seule stricte — même requête que l'existant, juste rendue exécutable un appel plus tôt)
Module: `Declaration.Application/Services/DeclarationWorkflowService.cs`

## OBJECTIF
Constat PO (13/07/2026) : sur la déclaration `FC2501717`/`EC_Id=21473` (recréée après suppression
via TASK-079), **aucun bandeau d'incohérence n'apparaît en haut de l'écran ② Affectations**, alors
que la ligne est bien incohérente (cas TASK-072 : `DO_TotalHT` incohérent avec `DO_TotalTTC`,
sentinelle `CodeTaxe='ERREUR'` déjà en cache `GRC_VENTILATION_SAGE_CACHE`).

## CAUSE RACINE
`ChargerCandidatesSiNecessaireAsync` (`DeclarationWorkflowService.cs`) avait deux branches :
- **Figeage déjà fait** → délègue à `RevaliderLignesFigeesAsync`, qui interroge
  `GetEcIdsEnErreurAsync` (cache `CodeTaxe='ERREUR'`) et produit l'alerte
  `LIGNE_FIGEE_A_REVERIFIER` consommée par le bandeau front (TASK-077/078).
- **Premier figeage** → exécutait le pipeline (`orchestrateur.Traiter`, qui matérialise la
  sentinelle `ERREUR` en cache le cas échéant), **puis retournait inconditionnellement
  `Array.Empty<Alerte>()`**, avec un commentaire faux (« rien à revalider »).

## CORRECTIF IMPLÉMENTÉ
Après `SaveLignesCandidatesAsync`, appel à `RevaliderLignesFigeesAsync(declarationId, domaine)` au
lieu de retourner un tableau vide — elle relit `GetLignesAsync`, donc voit les lignes qui viennent
d'être sauvegardées. Même détection réutilisée, aucune règle dupliquée. Lecture seule stricte
préservée (aucune ligne/total modifié).

## VALIDATION
- Test unitaire dédié `Declaration.Orchestration.Tests/Task081PremierFigeageBandeauTests.cs`
  (2 tests) : alerte émise dès le premier appel quand l'`EC_Id` est déjà en sentinelle `ERREUR` ;
  aucune fausse alerte si rien n'est en erreur.
- Build back 0 erreur, `tsc --noEmit`/`vite build` front 0 erreur.
- `Declaration.Orchestration.Tests` : 105/105 verts (99 préexistants + tests d'autres tâches déjà
  présentes + les 2 nouveaux), aucune régression sur la branche « déjà figé ».

## RÉSERVE LEVÉE PAR TASK-082
Ce correctif seul ne suffisait pas pour le cas réel signalé par le PO : sur `FC2501717`, la ligne
est créée directement en `Etat=Exclue` (incohérence détectée **pendant** la lecture du cache au
figeage, avant de devenir `Proposee`) — un cas hors du périmètre de filtre de
`RevaliderLignesFigeesAsync` à l'époque de ce correctif. Complété immédiatement après par
**[TASK-082](TASK-082-lignes-exclue-figeage-alerte-incoherence.md)**, qui couvre spécifiquement ce
cas. Les deux tasks ont été validées ensemble, `VERIFY/TASK-081_verify.md` archivé.

## NOTES
Origine : signalement PO 13/07/2026, « on a fait une régression sur step 2 pourtant y'a une
incohérence, aucun flag n'est affiché en haut », sur `FC2501717` après suppression/recréation de la
déclaration (TASK-079). Confirmé en réel par le PO le 14/07/2026 après rebuild/redémarrage de l'API
et application conjointe de TASK-082.
