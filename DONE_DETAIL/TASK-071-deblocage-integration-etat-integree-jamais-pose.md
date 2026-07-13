# TASK-071 — Déblocage intégration : l'état `Integree` n'est jamais posé (clôture structurellement impossible)

Status: TODO
Priority: HIGH
Risk: CRITICAL
Module: Front `declaration-tva-web` (tunnel ④ Intégration / ⑤ Contrôle) + Back `Declaration.Application`/`Declaration.API`

## OBJECTIF
Débloquer le passage à l'état `EtatLigne.Integree` avant clôture. Constat vérifié dans le code : `EtatLigne.Integree` n'est posé **nulle part** dans le flux du tunnel actuel (①→⑥). Or `GetCheckupAsync` (`DeclarationWorkflowService.cs:350`) ne compte que les lignes `Integree` pour calculer l'équilibre comptable et pour lever l'alerte bloquante `AUCUNE_LIGNE_INTEGREE` (`DeclarationWorkflowService.cs:371`), et `CloturerDeclarationAsync` (`DeclarationWorkflowService.cs:403-432`, appelée directement par le bouton « Confirmer intégration » de `IntegrationPanel.tsx:118`) refuse la clôture si cette alerte est présente.

Le seul point d'UI capable de poser `Etat=Intégrée` est le bouton « Intégrer » de `DomainGrid.tsx:218` (`doBulkAction('Intégrée')`), utilisé uniquement par `ControleDeclarationPanel.tsx` (écran **⑤ Contrôle**) et par l'ancien `WorkstationPanel.tsx` (superseded). Or ⑤ est **après** ④ dans le stepper et reste verrouillé (🔒) tant que ④ n'a pas réussi. `AffectationsDrill.tsx` (écran ② Affectations, actuel) n'utilise pas `DomainGrid` et n'expose aucune action « Intégrer ».

**Conséquence : deadlock total.** ④ exige des lignes déjà `Integree` pour passer ses contrôles ; la seule action qui pose `Integree` vit dans ⑤, verrouillé par ④. Aucune déclaration ne peut donc jamais être clôturée dans le tunnel actuel, quel que soit le volume de lignes valorisées — reproduit avec le cas PO (68 règlements, 245 lignes valorisées, 1 anomalie bloquante « déclaration vide »).

Décision retenue par l'architecte (à confirmer en VERIFY) : le bouton « Confirmer intégration » de ④ doit, dans la même opération transactionnelle que `CloturerDeclarationAsync`, faire passer les lignes valorisées éligibles (`Proposee`) à `Integree` **avant** d'exécuter les contrôles bloquants et la pose du tampon `DT_Id` — c'est la lecture cohérente du libellé « ④ Intégration : confirmation avant figeage ». Ne pas dupliquer la logique de transition déjà utilisée par `doBulkAction`/`UpdateLignesEtatBulkAsync`.

## BUSINESS VALUE
Sans ce correctif, **aucune déclaration TVA ne peut jamais être clôturée ni déposée** — bloque l'intégralité du produit en production (environnement de vrais utilisateurs finaux DAF/DG).

## CONTRAINTES
- Ne pas casser le verrou `DT_Id` (TASK-028) ni le trigger d'immuabilité totale (TASK-064).
- Ne pas dupliquer la logique de transition d'état déjà présente (`IDeclarationRepository.UpdateLignesEtatBulkAsync`/`UpdateLigneEtatAsync`) — la réutiliser depuis `CloturerDeclarationAsync`.
- Ne pas transitionner les lignes `Exclue`/`Reportee`/`Ecartee` vers `Integree` — seules les lignes valorisées `Proposee` éligibles.
- Aucun bypass silencieux du contrôle d'équilibre ni des alertes bloquantes existantes (TIERS_SANS_ICE, etc.) : elles doivent continuer à s'évaluer sur l'état post-transition, dans la même transaction, avant tout commit définitif.
- Lecture seule stricte côté Sage — aucun impact.
- Si l'option retenue est finalement différente (ex. action explicite « Intégrer » ajoutée en ② avant ④), documenter le changement de décision dans VALIDATION/NOTES du VERIFY et le faire valider par l'architecte avant clôture de la task.

## FILES
- `Declaration.Application/Services/DeclarationWorkflowService.cs` (`GetCheckupAsync` L330-401, `CloturerDeclarationAsync` L403-432)
- `Declaration.Application/Interfaces/IDeclarationRepository.cs` (méthodes de transition d'état existantes)
- `Declaration.API/Controllers/DeclarationsController.cs` (`UpdateLigneEtat` L91-96, `UpdateLignesBulk` L98-108, `GetCheckup` L115-203)
- `declaration-tva-web/src/AffectationsDrill.tsx` (② — aucune action Intégrer actuellement, à vérifier si un changement de contrat y est nécessaire)
- `declaration-tva-web/src/IntegrationPanel.tsx` (④ — appelle `POST /cloture` directement, L118)
- `declaration-tva-web/src/DomainGrid.tsx` (L218 — seul point d'UI existant posant `Etat=Intégrée`, réutilisé par ⑤)
- `declaration-tva-web/src/ControleDeclarationPanel.tsx` (⑤ — ne doit pas régresser)

## VALIDATION
- [ ] Build OK
- [ ] Tests passés
- [ ] Une déclaration `EnCours` avec des lignes valorisées `Proposee` peut atteindre « Confirmer intégration » sans que l'alerte `AUCUNE_LIGNE_INTEGREE` bloque systématiquement
- [ ] Le bouton « Confirmer intégration » (④) réussit sur un cas réel comparable au cas PO (68 règlements / 245 lignes) sans aller-retour manuel vers un écran verrouillé
- [ ] Le contrôle d'équilibre comptable et les alertes bloquantes restent évalués et bloquants sur l'état réel post-transition (aucun bypass)
- [ ] Aucune régression sur ⑤ Contrôle (`DomainGrid`/`doBulkAction` toujours fonctionnel pour d'éventuels ajustements post-intégration)
- [ ] Le verrou `DT_Id` (TASK-028) et le trigger d'immuabilité (TASK-064) restent intacts — preuve rejouée
- [ ] Preuve réelle fournie (pas seulement build vert) : capture/logs d'une clôture réussie de bout en bout

## ARCHITECTURE RULES APPLICABLES
- Pas de logique métier dans l'UI (`ARCHITECTURE.md` §5) — la transition d'état et les contrôles restent back.
- Pas de bypass d'un contrôle bloquant, même temporairement (`ARCHITECTURE.md` §5/§6).
- Transaction obligatoire sur toute écriture critique (`ARCHITECTURE.md` §5) — transition d'état + pose du tampon `DT_Id` dans une même opération cohérente.
- Pas de dette technique silencieuse — si la sémantique retenue diffère de la décision par défaut ci-dessus, la documenter explicitement.

## NOTES
Origine : signalé par le PO (13/07/2026) sur l'écran ④ Intégration d'une déclaration réelle (68 règlements, 245 lignes valorisées, tous contrôles amont OK) qui affichait pourtant « 1 anomalie bloquante — déclaration vide » et un bouton « Confirmer intégration » définitivement grisé. Investigation code par l'architecte : `EtatLigne.Integree` n'est référencé qu'en lecture dans tout `Declaration.Application`/`Declaration.API` (`grep` confirmé) — jamais assigné, sauf via `doBulkAction('Intégrée')` de `DomainGrid.tsx`, lui-même seulement câblé dans `ControleDeclarationPanel.tsx` (⑤, verrouillé par ④) et l'ancien `WorkstationPanel.tsx` (superseded par TASK-019, cf. `grf-vs-declaration-modules-distincts`/tunnel 053-059). Le tunnel actuel (`DeclarationStepper.tsx`, ①→⑥) n'a donc, structurellement, aucun chemin permettant à une déclaration d'atteindre l'état `Integree`.
