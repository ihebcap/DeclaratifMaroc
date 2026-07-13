# TASK-078 — Workflow de décision sur une incohérence signalée (écran ② Affectations)

Status: IMPLÉMENTÉ (non vérifié en réel, non clôturé)
Priority: HIGH
Risk: MEDIUM (nouvelle action d'écriture sur `DM_LGTVA` — traçabilité uniquement, aucun montant)
Module: `declaration-tva-web/src/AffectationsDrill.tsx` + `Declaration.API/Controllers/DeclarationsController.cs`
+ `Declaration.Application/Services/DeclarationWorkflowService.cs`

> **Note process** : cette task a été implémentée directement en session (worker exceptionnel),
> avant rédaction du fichier — documentée ici rétroactivement pour traçabilité, conformément à la
> règle « une TASK non documentée ne doit pas être créée » (le contenu existe, la forme suit).

## OBJECTIF
Suite à TASK-077 (signalement seul des lignes déjà figées devenues incohérentes) : le PO, en
testant en réel, a demandé un vrai workflow de décision plutôt qu'un message d'alerte texte —
« il faut se mettre à la place d'un comptable qui ne comprend rien en informatique [...] il attend
que le système lui remonte tout seul l'incohérence ». Besoin exprimé : bandeau visible
automatiquement (aucune recherche requise), filtre au clic sur les seules lignes concernées, et
deux actions explicites par pièce signalée :
1. **Valider l'incohérence** — l'accepter en connaissance de cause (décision tracée qui/quand).
2. **Corriger/Resynchroniser** — après correction côté Sage, relire la pièce et réévaluer.

## BUSINESS VALUE
Sans ce workflow, une incohérence signalée (TASK-077) reste un message texte parmi d'autres
avertissements, sans action possible ni traçabilité de la décision prise — le comptable doit
comprendre seul la portée technique du signalement pour savoir quoi faire.

## CONTRAINTES
- Aucune ligne ni total jamais modifié par la validation (traçabilité pure : qui/quand).
- La resynchronisation relit réellement Sage (effet de bord voulu, coûteux — jamais déclenchée
  automatiquement, uniquement sur action explicite de l'utilisateur).
- Réutilise le pipeline existant (`OrchestrateurDeclaration.Traiter`, `MarquerEnErreur`/
  `UpsertEntries`) — aucune règle de détection dupliquée.
- Le bandeau doit être visible sans action de recherche (pas de filtre à activer manuellement pour
  le découvrir).

## FILES
- `Declaration.Infrastructure/SQL/007_DM_LGTVA_Incoherence_Validee.sql` (migration)
- `Declaration.Application/Entities/WorkflowEntities.cs` (`IncoherenceValidee/Par/Le`)
- `Declaration.Application/Interfaces/IDeclarationRepository.cs` +
  `Declaration.Infrastructure/Repositories/DeclarationRepository.cs`
  (`ValiderIncoherenceAsync`, `ReinitialiserValidationIncoherenceAsync`)
- `Declaration.Application/Services/DeclarationWorkflowService.cs`
  (`ValiderIncoherenceLigneAsync`, `ResynchroniserLigneAsync`, gate dans `RevaliderLignesFigeesAsync`)
- `Declaration.API/Controllers/DeclarationsController.cs`
  (`POST /{id}/lignes/valider-incoherence`, `POST /{id}/lignes/resynchroniser`)
- `Declaration.API/Dtos/LigneCandidateDto.cs` (`ecId`, `incoherenceValidee`)
- `declaration-tva-web/src/AffectationsDrill.tsx` (bandeau, filtre, actions par ligne)

## VALIDATION
- [x] Build back 0 erreur, `tsc --noEmit` front 0 erreur
- [x] 99/99 tests `Declaration.Orchestration.Tests` (non-régression, gate validée testé)
- [ ] **Non encore vérifié visuellement en réel** (bandeau + actions cliquées sur un cas réel)
- [ ] Fichier `VERIFY/TASK-078_verify.md` à rédiger après confirmation PO
- [ ] Test de non-régression dédié à `ValiderIncoherenceAsync`/`ResynchroniserLigneAsync`
      (actuellement couverts uniquement par la gate dans `RevaliderLignesFigeesAsync`, pas par un
      test ciblé sur les 2 nouveaux endpoints)

## ARCHITECTURE RULES APPLICABLES
- Aucune valeur inventée ni recalcul silencieux — la validation ne touche que la traçabilité.
- Lecture seule stricte sauf action explicite (validation, resynchronisation) — jamais automatique.

## NOTES
Origine : suivi direct de TASK-077, demande PO le 13/07/2026 en testant le signalement en réel sur
l'écran ② Affectations (déclaration TVA1-2026-01, cas `EC_Id=21473`/`FC2501717`).
