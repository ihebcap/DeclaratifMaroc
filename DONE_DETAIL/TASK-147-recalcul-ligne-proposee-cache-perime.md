# TASK-147 — Détecter et permettre de recalculer une ligne "Proposée" dont le cache Sage a été relu avec succès après la création de la déclaration

Status: 🆕 à faire
Priority: HIGH
Risk: MEDIUM (nouvelle action déclenchant un recalcul, à borner strictement à une ligne à la fois)
Module: Declaration.API / Declaration.Application / declaration-tva-web

> **Origine :** diagnostic en session (20/07/2026) sur `TVA1-2026-02`, ligne `FA2600106`/`EC_Id=21849`
> (règlement `RC26040198`) : l'architecte a vérifié directement en base que Sage OM avait **déjà
> relu avec succès** cette facture (`DM_VENTILATION_SAGE_CACHE` : `TotalHT=69974,23`,
> `TotalTva=9232,56`, aucune erreur, `DateLecture=2026-07-19 23:17:36`) — mais la déclaration
> (`DateCreation=2026-07-19 17:41:35`, donc **antérieure** de ~5h30) affiche toujours « Facture
> introuvable (DTO non fourni) ». Même constat pour 7 autres lignes de la même déclaration. La
> donnée existe et est bonne ; la déclaration affiche un instantané périmé.

## Constat (preuve de données, session du 20/07/2026)

- `DM_ENTTVA.DateCreation` pour `TVA1-2026-02` = `2026-07-19 17:41:35`.
- `DM_VENTILATION_SAGE_CACHE` pour 8 des 9 échéances en anomalie de cette déclaration montre des
  lectures **réussies** (`MotifErreur IS NULL`, montants réels non nuls, `Source='OM'`) datées
  `2026-07-19 23:17` à `23:19` — **après** la création de la déclaration.
- `MapLignesCandidates` ([DeclarationWorkflowService.cs:633-678](../Declaration.Application/Services/DeclarationWorkflowService.cs:633))
  construit `MotifRejet` une seule fois, au moment de la génération des lignes candidates ; rien ne
  déclenche une relecture automatique du cache une fois la ligne créée avec un motif de rejet.
- Un mécanisme équivalent existe déjà mais est réservé à un autre contexte : `ResynchroniserLigneAsync`
  ([DeclarationWorkflowService.cs:501-541](../Declaration.Application/Services/DeclarationWorkflowService.cs:501))
  relit Sage pour un `EC_Id` donné et réécrit le cache — mais n'est câblé aujourd'hui que sur
  `AffectationsDrill.tsx` (TASK-078, lignes déjà figées/intégrées), pas sur les lignes `Proposee` de
  l'étape ② Vérifier & Intégrer.

## Objectif

1. **Diagnostic** (à intégrer dans le panneau `DiagnosticModal` livré par TASK-144, `Declaration.Application/Services/DiagnosticMotifMetier.cs`
   et `Declaration.Application/Entities/DiagnosticLigne.cs`) : comparer `DM_VENTILATION_SAGE_CACHE.DateLecture`
   (la plus récente pour cet `EC_Id`) à `DM_ENTTVA.DateCreation` de la déclaration consultée. Si le
   cache est **plus récent** ET **sans erreur** (`MotifErreur IS NULL`, montants non nuls) alors que
   la ligne affiche encore un motif de rejet : afficher un message explicite du type « Sage a relu
   cette facture avec succès le [date] — après la création de cette déclaration. Un recalcul de
   cette ligne devrait résoudre l'anomalie. » plutôt que de laisser croire que la facture est
   réellement introuvable.
2. **Action** : exposer sur ce cas précis un bouton « Recalculer cette ligne » qui réutilise
   `ResynchroniserLigneAsync` (ou une variante), applicable à une ligne `Proposee` (pas seulement
   figée/intégrée comme aujourd'hui), qui reconstruit la ligne candidate à partir du cache à jour
   sans redéclencher de nouvelle lecture OM Sage (le cache est déjà bon).

## Garde-fous

- Action déclenchée **une ligne à la fois**, jamais en masse (cohérent avec TASK-144).
- Ne pas dupliquer `OrchestrateurDeclaration` — réutiliser le cache déjà persisté ; ne redéclencher
  une lecture OM Sage QUE si le cache n'a effectivement pas de lecture plus récente que la
  déclaration (sinon utiliser directement les valeurs déjà en cache, coût quasi nul).
- Ne pas modifier le comportement existant de `ResynchroniserLigneAsync` pour son usage actuel
  (`AffectationsDrill.tsx`, TASK-078) — étendre son applicabilité ou ajouter une variante, ne pas
  changer sa sémantique pour l'écran post-figeage.
- Aucun recalcul d'équilibre global ni de tampon `DT_Id` — cette action ne touche qu'une ligne.

## Files

- `Declaration.Application/Services/DiagnosticMotifMetier.cs` et `Declaration.Application/Entities/DiagnosticLigne.cs` (livrés par TASK-144 — étendre avec la comparaison de dates).
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`ResynchroniserLigneAsync` l.501-541, `MapLignesCandidates` l.633-678, `DiagnostiquerLigneAsync` livré par TASK-144).
- `declaration-tva-web/src/DiagnosticModal.tsx` (livré par TASK-144 — ajouter le bloc staleness + bouton).
- `Declaration.API/Controllers/DeclarationsController.cs` (endpoint diagnostic TASK-144, éventuel nouvel endpoint d'action recalcul).

## Validation

- [ ] Build back + front OK.
- [ ] Rejeu réel sur `TVA1-2026-02`/`FA2600106`/`EC_Id=21849` : le diagnostic affiche bien le message
      de staleness (cache plus récent, sans erreur) et le bouton "Recalculer cette ligne" fait
      disparaître l'anomalie sans nouvelle lecture OM Sage (vérifiable via `DateLecture` du cache
      inchangée après le clic).
- [ ] Non-régression : `ResynchroniserLigneAsync` continue de fonctionner à l'identique pour
      `AffectationsDrill.tsx` (TASK-078).
- [ ] Le cas où le cache est réellement en erreur (`MotifErreur` non nul, pas de staleness) continue
      d'afficher le message TASK-144 existant, pas le message de staleness.

## Dépendances / risques

- Complète TASK-144 (le worker de TASK-144 avait explicitement noté ce point comme hors périmètre
  initial — cf. VERIFY TASK-144, "Reste à valider" point 4).
- Risque : après le fix TASK-145 (Achat/Vente), certaines lignes actuellement "en erreur" pourraient
  devenir "en cache périmé mais valide" — cette TASK est complémentaire, pas redondante, avec
  TASK-145 (qui empêche la corruption future) et TASK-149 (qui ré-audite l'existant).
