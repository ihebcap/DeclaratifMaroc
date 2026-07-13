# TASK-081 — Bandeau incohérence absent au premier figeage d'une déclaration (régression fonctionnelle)

Status: À FAIRE
Priority: CRITICAL
Risk: LOW (lecture seule stricte — même requête que l'existant, juste rendue exécutable un appel plus tôt)
Module: `Declaration.Application/Services/DeclarationWorkflowService.cs`

## OBJECTIF
Constat PO (13/07/2026) : sur la déclaration `FC2501717`/`EC_Id=21473` (recréée après suppression
via TASK-079), **aucun bandeau d'incohérence n'apparaît en haut de l'écran ② Affectations**, alors
que la ligne est bien incohérente (cas TASK-072 : `DO_TotalHT` incohérent avec `DO_TotalTTC`,
sentinelle `CodeTaxe='ERREUR'` déjà en cache `GRC_VENTILATION_SAGE_CACHE`).

## CAUSE RACINE (identifiée par lecture de code, non corrigée)
`ChargerCandidatesSiNecessaireAsync` (`DeclarationWorkflowService.cs:125-186`) a deux branches :
- **Figeage déjà fait** (`GetLignesCountAsync > 0`) → délègue à `RevaliderLignesFigeesAsync`, qui
  interroge `GetEcIdsEnErreurAsync` (cache `CodeTaxe='ERREUR'`) et produit l'alerte
  `LIGNE_FIGEE_A_REVERIFIER` consommée par le bandeau front (TASK-077/078).
- **Premier figeage** (`GetLignesCountAsync == 0`, lignes 140-185) → exécute le pipeline
  (`orchestrateur.Traiter`, qui matérialise la sentinelle `ERREUR` en cache le cas échéant), **puis
  retourne inconditionnellement `Array.Empty<Alerte>()`** (ligne 185), avec le commentaire
  « rien à revalider — le pipeline vient d'être appliqué en direct ».

Ce commentaire est **faux** pour le cas d'incohérence Sage : la sentinelle vient d'être écrite en
cache par ce même appel, mais rien ne la relit pour émettre l'alerte avant de retourner. Sur une
déclaration recréée (donc toujours en premier figeage au moment du test PO), le garde-fou visuel —
raison d'être de TASK-077/078 — ne se déclenche jamais tant que l'utilisateur n'a pas déclenché un
second appel `/lignes` passant par la branche « déjà figé ».

**Différence avec TASK-077** : TASK-077 corrigeait la revalidation des lignes **déjà anciennement
figées** (avant le garde-fou). Ce trou-ci est symétrique et non couvert : les lignes **fraîchement**
figées après le garde-fou n'émettent pas non plus l'alerte, dès le tout premier appel.

## BUSINESS VALUE
Le bandeau (TASK-077/078) existe précisément pour que le comptable n'ait **rien à chercher** :
« il attend que le système lui remonte tout seul l'incohérence ». Ce trou annule cette garantie sur
toute déclaration fraîchement créée — exactement le scénario qui se produit après une suppression/
recréation (TASK-079), ou plus généralement à chaque nouvelle déclaration mensuelle.

## CONTRAINTES
- Aucune nouvelle règle de détection : réutiliser `GetEcIdsEnErreurAsync`/`GetMvPointsActuelsAsync`
  déjà en place (même contrat qu'en revalidation), pas de duplication.
- Lecture seule stricte, aucune ligne/total modifié — même invariant que TASK-077/078.
- Ne pas casser le chemin rapide (verrou `_figeageLocks`, idempotence du figeage concurrent).

## PISTE (à charge du développeur, non prescriptive)
Après `SaveLignesCandidatesAsync` (ligne 175), avant le `return Array.Empty<Alerte>()` (ligne 185),
appeler la même détection que `RevaliderLignesFigeesAsync` sur les lignes qui viennent d'être
insérées (`lignes`, déjà en mémoire) plutôt que retourner un tableau vide par hypothèse. Vérifier si
un appel direct à `RevaliderLignesFigeesAsync(declarationId, domaine)` après le figeage frais est
suffisant (elle relit `GetLignesAsync`, donc verrait les lignes qui viennent d'être sauvegardées) ou
s'il faut factoriser la portion détection (lignes 308-345) pour éviter une requête redondante.

## FILES
- `Declaration.Application/Services/DeclarationWorkflowService.cs`
  (`ChargerCandidatesSiNecessaireAsync`, ligne 182-185)

## VALIDATION
- [ ] Preuve réelle : créer une déclaration **fraîche** sur une période contenant `FC2501717`
      (`EC_Id=21473`) → bandeau « N ligne(s) incohérente(s) détectée(s) » visible **dès le premier
      chargement** de l'écran ② Affectations, sans recharger une deuxième fois.
- [ ] Non-régression : le chemin « déjà figé » (`RevaliderLignesFigeesAsync` existant) continue de
      fonctionner à l'identique (99/99 `Declaration.Orchestration.Tests`).
- [ ] Test unitaire dédié : figeage frais d'une déclaration contenant une ligne dont l'`EC_Id` est
      déjà en sentinelle `ERREUR` en cache → l'alerte `LIGNE_FIGEE_A_REVERIFIER` doit être retournée
      par `ChargerCandidatesSiNecessaireAsync` dès le premier appel (aucun test existant ne couvre
      ce cas — TASK-077/078 ne testent que la branche revalidation).
- [ ] Build 0 erreur, `tsc --noEmit` 0 erreur (aucun changement front attendu — le contrat `Alertes`
      de `GET /{id}/lignes` est inchangé).

## ARCHITECTURE RULES APPLICABLES
- Aucun rejet silencieux (`ARCHITECTURE_PROJECT.md`) — un garde-fou de détection qui existe mais ne
  se déclenche pas au bon moment est un rejet silencieux de facto.
- Lecture seule stricte, aucune valeur recalculée.

## NOTES
Origine : signalement PO 13/07/2026, « on a fait une régression sur step 2 pourtant y'a une
incohérence, aucun flag n'est affiché en haut », sur `FC2501717` après suppression/recréation de la
déclaration (TASK-079). Investigation menée par lecture de code par l'architecte — cause identifiée
avec précision, correction non implémentée (hors périmètre du rôle architecte, cf. `CLAUDE.md`).
