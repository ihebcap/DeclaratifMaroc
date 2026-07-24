# TASK-176 — Resynchronisation en masse des lignes (au lieu d'une resynchronisation ligne par ligne)

Status: 🆕 à faire
Priority: MEDIUM (confort d'usage, pas bloquant — un contournement ligne par ligne existe)
Risk: LOW (réutilisation stricte du pipeline unitaire déjà livré, même pattern que les autres actions
en masse déjà en place)
Module: Declaration.API / declaration-tva-web

> **Origine :** signalement PO (24/07/2026) : *« le bouton resynchronisé les factures je dois pas faire
> ligne par ligne »* — sur une déclaration comptant des dizaines de lignes en anomalie (151 lignes
> sélectionnées dans le cas signalé), le PO doit aujourd'hui cliquer « Resynchroniser » **une ligne à la
> fois**.

## Constat (preuve de code)

- L'endpoint `POST {id}/lignes/resynchroniser` (`Declaration.API/Controllers/DeclarationsController.cs:273-289`)
  ne prend qu'**un seul `EcId`** en paramètre (`ValiderIncoherenceRequest.EcId`, un `int`) — appelle
  `_workflowService.ResynchroniserLigneAsync(id, request.EcId)` pour cette unique pièce.
- Les 3 points d'entrée front existants vers cet endpoint (`relireDepuisSage` dans `api.ts:94`) sont
  tous unitaires : `DiagnosticModal.tsx:89`, `DomainGrid.tsx:265` (bouton par ligne livré par TASK-170),
  `AffectationsDrill.tsx`. **Aucun chemin en masse n'existe** vers `ResynchroniserLigneAsync`.
- À l'inverse, l'app a déjà DEUX endpoints « bulk » pour d'autres actions sur les lignes, sur le même
  contrôleur : `POST {id}/lignes:bulk` (changement d'état, `UpdateLignesBulk`, TASK-012) et
  `POST {id}/lignes/code-activite:bulk` (`UpdateCodeActiviteBulk`, TASK-173) — tous deux acceptent soit
  une liste explicite de `LigneIds`, soit un `Domaine`+`Filter` (sélection large). C'est le patron déjà
  validé et utilisé par le PO (151 lignes sélectionnées dans son signalement montrent qu'il utilise déjà
  ce mécanisme de sélection multiple pour d'autres actions) — à réutiliser à l'identique pour la
  resynchronisation, pas un nouveau design.

## Objectif

1. Ajouter un endpoint `POST {id}/lignes/resynchroniser:bulk`, même contrat de sélection que
   `UpdateLignesBulk`/`UpdateCodeActiviteBulk` (`LigneIds` explicite OU `Domaine`+`Filter`), qui appelle
   `ResynchroniserLigneAsync` pour chaque `EC_Id` résolu par la sélection — **séquentiellement**, pas en
   parallèle (une resynchronisation relit Sage/OM ; un parallélisme interne recréerait exactement la
   contention `soId` déjà documentée par TASK-156/TASK-175, sur le même `soId` en plus).
2. Retour agrégé exploitable par le front : nombre de lignes traitées, nombre encore en anomalie après
   resynchronisation (avec le motif), pour affichage synthétique — pas une simple liste de 151 réponses
   individuelles à corréler côté front.
3. Front (`VerifierIntegrerPanel.tsx`, cohérent avec la sélection multiple déjà utilisée pour les autres
   actions en masse de cet écran) : bouton « Resynchroniser la sélection » à côté des actions en masse
   existantes, avec une confirmation avant lancement si la sélection dépasse un seuil (le volume
   d'appels OM/Sage successifs peut prendre du temps — cf. réserve déjà notée par TASK-170 sur le risque
   de sollicitation Sage en volume) et un indicateur de progression (même traitement séquentiel côté
   back : un retour de progression par lot, pas un blocage silencieux de plusieurs dizaines de secondes).

## Garde-fous

- Réutilisation stricte de `ResynchroniserLigneAsync` (TASK-078/167) pour chaque ligne — aucune nouvelle
  règle de lecture Sage, aucun nouveau verrou.
- Traitement **séquentiel** des `EC_Id` dans le nouvel endpoint bulk — jamais de `Parallel.ForEach`/
  `Task.WhenAll` sur plusieurs `ResynchroniserLigneAsync` du même `soId` (créerait une auto-contention
  contre le propre verrou TASK-156 de l'appelant).
- Si le verrou `soId` est pris par un AUTRE traitement en cours (bouton Rafraîchir, chargement d'un autre
  onglet, etc.) pendant le bulk : le comportement attendu est un rejet propre et explicite (409),
  jamais un blocage silencieux ni une file d'attente — cohérent avec la décision PO déjà actée sur
  TASK-156 (rejet immédiat, uniforme).
- Ne pas dupliquer la logique déjà présente dans `DiagnosticModal.tsx`/`AffectationsDrill.tsx`/
  `DomainGrid.tsx` — factoriser l'appel API bulk, garder les 3 chemins unitaires existants intacts pour
  le cas d'une seule ligne.

## Files

- [Declaration.API/Controllers/DeclarationsController.cs:273-301](../Declaration.API/Controllers/DeclarationsController.cs) (nouvel endpoint bulk, à poser à côté de `Resynchroniser`/`UpdateLignesBulk`).
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`ResynchroniserLigneAsync`, ligne ~722 — réutilisation en boucle, aucune modification de la méthode elle-même attendue).
- [declaration-tva-web/src/api.ts](../declaration-tva-web/src/api.ts) (nouvel appel bulk, à côté de `relireDepuisSage`).
- [declaration-tva-web/src/VerifierIntegrerPanel.tsx](../declaration-tva-web/src/VerifierIntegrerPanel.tsx) (bouton sur la sélection multiple déjà existante).

## Validation

- [ ] Build back + front OK.
- [ ] Test réel : sélectionner plusieurs dizaines de lignes en anomalie (cas réel PO, ~151 lignes) →
      un seul clic → toutes resynchronisées, retour synthétique correct (traitées / toujours en
      anomalie avec motif).
- [ ] Vérifier qu'un rejet du verrou `soId` pendant le bulk (autre traitement concurrent démarré entre
      temps) est signalé proprement, sans planter le traitement des lignes déjà passées.
- [ ] Non-régression sur les 3 chemins unitaires existants (`DiagnosticModal`, `AffectationsDrill`,
      `DomainGrid`/TASK-170).
- [ ] Aucun changement de `DT_Id`/périmètre de déclaration observé après un bulk.

## Dépendances / risques

- Dépend implicitement de la correction de TASK-175 (le chargement de la déclaration ne doit plus 500
  sous contention `soId`) pour être testable sereinement en conditions réelles — sans lien de blocage
  technique direct, mais tester un bulk de resynchronisation sur un écran qui échoue déjà à charger
  serait peu concluant.
- Risque de volume : resynchroniser 151 lignes séquentiellement relit Sage/OM autant de fois — temps de
  traitement à mesurer en conditions réelles, seuil de confirmation front (§3) à caler sur ce constat.
