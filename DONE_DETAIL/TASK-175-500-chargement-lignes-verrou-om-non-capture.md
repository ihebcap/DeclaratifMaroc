# TASK-175 — CRITIQUE : 500 au chargement des lignes (`GET {id}/lignes`) quand les deux domaines se chargent en parallèle

Status: 🆕 à faire
Priority: CRITIQUE (bloque l'accès à l'écran ③ Vérifier & Intégrer)
Risk: LOW à corriger (un seul `catch` manquant, pattern déjà en place ailleurs), HIGH tant que non corrigé (écran inutilisable de façon intermittente)
Module: Declaration.API

> **Origine :** signalement PO (24/07/2026), reproduit deux fois sur la base de **production** cliente
> (déclaration `f6e3c681-...` puis, après suppression + réintégration de la déclaration par le PO,
> nouvelle déclaration `6ce51182-...`) — `GET /api/declarations/{id}/lignes?domaine=Encaissement`
> (et, sur la 2ᵉ déclaration, `domaine=Decaissement` aussi) répond **500** de façon intermittente,
> y compris avec le paramètre `filter` (liste de `numeroRapprochement`). Le fait que ça se reproduise
> sur une déclaration **neuve** écarte une corruption de données propre à `f6e3c681` — cause
> applicative, pas un problème ponctuel de base.

## Constat (preuve de code)

C'est exactement le risque déjà anticipé, noté « non vérifié empiriquement » dans la réserve de
**TASK-156** (`TODO.md`) — désormais confirmé en conditions réelles :

1. `VerifierIntegrerPanel.tsx` charge les deux domaines **en parallèle** (`Promise.all`, visible dans
   la stack trace fournie par le PO : `wa` → `Promise.all` → deux appels `GET {id}/lignes`, un par
   domaine, pour le même `soId`).
2. Chaque appel traverse `DeclarationsController.GetLignes` (`Declaration.API/Controllers/
   DeclarationsController.cs:113-143`) → `_workflowService.ChargerCandidatesSiNecessaireAsync(id, domaine)`
   → (premier figeage) `ConstruireLignesFigeesAsync` → `ExecuterAvecVerrouOMAsync(declaration.SocieteId, ...)`
   (`DeclarationWorkflowService.cs:263`).
3. `ExecuterAvecVerrouOMAsync` (`DeclarationWorkflowService.cs:75-93`) est le verrou anti-chevauchement
   **par `soId`** livré par TASK-156 : `WaitAsync(0)` n'attend jamais — si le verrou est déjà pris (par
   l'appel parallèle de l'autre domaine sur le **même** `soId`), il lève immédiatement
   `InvalidOperationException("Traitement de valorisation déjà en cours pour cette société...")`.
   Le commentaire de conception (`DeclarationWorkflowService.cs:58-65`) est explicite : cette exception
   doit être **« propagée en 409 par les contrôleurs »**, de façon **uniforme sur les 4 appelants**
   (`RafraichirValorisationAsync`, `ConstruireLignesFigeesAsync`, `ReintegrerReglementsLiberesAsync`,
   `ResynchroniserLigneAsync`).
4. Or `DeclarationsController.GetLignes` (ligne 113-143) **n'a aucun `try/catch`** autour de l'appel —
   contrairement au contrôleur `Resynchroniser` (ligne 273-289) qui catch bien
   `InvalidOperationException` → `Conflict()` (409). Résultat : l'exception remonte non gérée,
   ASP.NET Core répond **500 générique** (pas de message exploitable côté front) au lieu du 409 prévu.
5. Ceci correspond mot pour mot à la réserve non bloquante déjà tracée dans `TODO.md` sous TASK-156 :
   *« le rejet 409 explicite côté front n'existe que sur le bouton Rafraîchir — les 3 autres chemins
   (chargement d'une déclaration, réintégration, resynchronisation) renverront une erreur générique en
   cas de rejet »* et *« risque de contention nouvelle au premier chargement d'une déclaration si le
   front appelle en parallèle Decaissement et Encaissement pour le même soId »* — **confirmé en
   production**, avec un 500 (pas même un 409) faute de `try/catch` sur ce chemin précis.

Le fait que la seconde déclaration (`6ce51182-...`, neuve) échoue tantôt sur Decaissement, tantôt sur
Encaissement, tantôt les deux (logs PO) est cohérent avec une **course** : le domaine qui perd la course
au verrou échoue, celui qui gagne réussit — pas un défaut propre à un domaine.

## Objectif

1. Dans `DeclarationsController.GetLignes` (`Declaration.API/Controllers/DeclarationsController.cs:113`),
   ajouter le même `try/catch (InvalidOperationException ex) → Conflict(new { Message = ex.Message })`
   que `Resynchroniser` (ligne 285-288), pour que le rejet du verrou `soId` redevienne un **409** propre
   sur ce chemin, jamais un 500.
2. Vérifier si `ReintegrerReglementsLiberesAsync` (3ᵉ appelant cité par le commentaire TASK-156) est
   exposé par un contrôleur avec la même lacune — corriger de la même façon si c'est le cas (même
   défaut structurel, même correctif).
3. Côté front (`VerifierIntegrerPanel.tsx`), traiter explicitement un 409 sur ce chargement (au lieu du
   message générique `error loading lines`) : message clair (« un autre traitement est en cours pour
   cette société, réessayez dans quelques secondes ») + **retry automatique unique** après un court
   délai, plutôt que de laisser l'écran en erreur — le cas est **attendu** (les deux domaines se
   chargent volontairement en parallèle), pas une panne.

## Garde-fous

- Ne pas retirer ni affaiblir le verrou `soId` (TASK-156) — le correctif est uniquement de capturer
  l'exception déjà prévue pour l'être, pas de changer la politique de rejet immédiat actée par le PO.
- Ne pas transformer l'appel parallèle Decaissement/Encaissement en séquentiel côté front sans decision
  PO explicite (ça éliminerait la contention mais doublerait le temps de chargement perçu) — le retry
  côté front est le correctif minimal cohérent avec la décision PO déjà actée (rejet immédiat, jamais
  une file d'attente silencieuse côté back).
- Aucun changement de `ExecuterAvecVerrouOMAsync` ni des 4 appelants existants.

## Files

- [Declaration.API/Controllers/DeclarationsController.cs:113-143](../Declaration.API/Controllers/DeclarationsController.cs) (`GetLignes` — ajout du `try/catch`).
- [Declaration.Application/Services/DeclarationWorkflowService.cs:75-93](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`ExecuterAvecVerrouOMAsync` — référence, aucune modification attendue).
- [declaration-tva-web/src/VerifierIntegrerPanel.tsx](../declaration-tva-web/src/VerifierIntegrerPanel.tsx) (gestion du 409 au chargement + retry).

## Validation

- [ ] Build back (`dotnet build DeclarationTVA.slnx`) OK.
- [ ] Test réel : ouvrir l'écran ③ Vérifier & Intégrer sur une déclaration non encore figée → les deux
      appels parallèles Decaissement/Encaissement aboutissent tous les deux (soit directement, soit
      après le retry front), plus aucun 500 observé sur des dizaines de tentatives.
- [ ] Vérifier qu'un rejet de verrou renvoie bien 409 (pas 500) — reproductible en forçant deux appels
      concurrents (test d'intégration si possible, sinon vérification manuelle via deux onglets).
- [ ] Non-régression TASK-156 : un second appel concurrent reste bien rejeté (jamais silencieusement
      mis en file d'attente).
- [ ] Non-régression sur `Resynchroniser` (409 déjà géré, ne pas dupliquer/casser ce chemin).

## Dépendances / risques

- Aucune dépendance. Risque de régression faible : ajout d'un `catch` déjà éprouvé sur un autre
  contrôleur du même service.
