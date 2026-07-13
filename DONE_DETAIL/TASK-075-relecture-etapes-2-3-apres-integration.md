# TASK-075 — Relecture vide des étapes ② Affectations / ③ Calcul après intégration (réouverture ou accès direct à ④)

> **Origine :** remarque PO (13/07/2026) — « avant la réouverture ok je suis a la phase 4 mais
> normalement je peux voir ce qui est dans step 2 et 3 actuellement c'est vide. » Le PO s'attend à
> pouvoir consulter, depuis l'étape ④ Intégration, le contenu des étapes ② et ③ d'une déclaration déjà
> intégrée — et constate un écran vide au lieu du contenu attendu.

## Constat (preuve code, aucune supposition)
1. **Le stepper compte 6 étapes**, pas 4 : `declaration-tva-web/src/DeclarationStepper.tsx:23-32`
   (`reglements` ①, `affectations` ②, `calcul` ③, `integration` ④, `controle` ⑤, `synthese` ⑥). La
   « phase 4 » du PO correspond à `integration`.
2. **Un mode relecture existe déjà et est censé fonctionner** — une fois `integree = true`
   (`DeclarationStepper.tsx:86`), `readOnlyStep` (ligne 87) autorise la navigation retour vers ①②③ via
   `isUnlocked` (lignes 47-51) avec un bandeau lecture seule.
3. **Mais ce mode réutilise un state local jamais réhydraté** :
   - `selectedKeys`, `selectedRows`, `calcTVA` sont des `useState` initialisés vides
     (`DeclarationStepper.tsx:57-60`), peuplés uniquement par une traversée manuelle ①→②→③ **dans la
     session en cours**.
   - Quand une déclaration est déjà intégrée à l'ouverture (ou à la réouverture), `fetchInfo()` bascule
     directement le stepper sur `integration` (`DeclarationStepper.tsx:64-70`, en particulier ligne
     68-69) **sans jamais faire traverser ①②③** — donc `selectedRows` reste `[]` et `calcTVA` reste
     `{ totalTVA: 0, nbLignes: 0 }`.
4. **Les deux composants d'étape masquent tout si `selectedRows` est vide** :
   - `AffectationsDrill.tsx:329-336` : `if (selectedRows.length === 0)` → placeholder
     « Aucun règlement sélectionné — retournez à l'étape ① ».
   - `CalculTvaPanel.tsx:235-242` : même garde, même placeholder.
   Ce placeholder est un message de guidage pour le cas « rien n'a encore été sélectionné » (TASK-053/054),
   pas un vrai état vide de la déclaration — mais c'est ce que voit le PO en relecture après intégration,
   ce qui est trompeur : la déclaration a bien des données, elles ne sont simplement pas rechargées.
5. **Aucun des deux composants ne re-fetch depuis l'API** en mode lecture seule — ils dépendent
   exclusivement des props reçues du parent (`DeclarationStepper`), elles-mêmes dépendantes du state
   local jamais réhydraté depuis le backend pour une déclaration déjà intégrée.

## Objectif
Permettre au PO de consulter réellement, depuis l'étape ④ (ou en revenant sur ②/③), le contenu figé
d'une déclaration déjà intégrée — sans avoir à la traverser manuellement dans la session en cours.

## Périmètre proposé (à arbitrer, pas décidé ici)
### A. Réhydratation des données de relecture (bloquant)
Quand `isIntegree(info.statut)` est vrai dès le chargement (`fetchInfo`, ligne 64-76), charger
depuis l'API les données nécessaires à ②/③ (affectations réelles de la déclaration, calcul TVA déjà
figé) au lieu de laisser `selectedRows`/`calcTVA` à leur valeur initiale vide. À vérifier : existe-t-il
déjà un endpoint retournant les affectations/le calcul TVA d'une déclaration par `declarationId`
(indépendamment de la sélection de règlements en session) ? Sinon, périmètre à coordonner avec
back (`Declaration.API`).

### B. Distinguer « rien sélectionné » de « rien chargé » (bloquant)
Les gardes `selectedRows.length === 0` dans `AffectationsDrill.tsx:329` et `CalculTvaPanel.tsx:235`
ne doivent pas afficher le même message dans les deux cas. En mode lecture seule après intégration,
un vide réel serait anormal (une déclaration intégrée a nécessairement des affectations) — le message
actuel « retournez à l'étape ① » est incorrect dans ce contexte et risque d'induire le PO en erreur.

### C. Cohérence avec TASK-073 (réouverture)
Si TASK-073 (garde d'accès + traçabilité de la réouverture) avance, vérifier que la relecture ②/③
fonctionne aussi bien pour une déclaration réouverte (`EnCours` après réouverture, encore consultable
si des affectations dé-tamponnées existent) que pour une déclaration intégrée non réouverte.

## Garde-fous
1. **Lecture seule** — ce chantier ne doit pas modifier la logique de calcul TVA ni la logique
   d'affectation elle-même (`ConstructeurDeclaration.cs`, TASK-050/052/060), seulement la façon dont
   ②/③ sont peuplées en mode relecture.
2. **Ne pas casser le parcours normal** ①→②→③→④ pour une déclaration `EnCours` (non intégrée) —
   la garde `selectedRows.length === 0` doit rester pour ce cas.
3. **Vérifier l'existence d'un endpoint de lecture par `declarationId`** avant d'en créer un nouveau —
   possible chevauchement avec ce qu'expose déjà `IntegrationPanel`/`ControleDeclarationPanel`.

## Livrables de preuve (VERIFY)
1. Preuve réelle : ouvrir une déclaration déjà intégrée (statut ≠ `EnCours`) sans être passé par
   ①②③ dans la session → étapes ② et ③ affichent les données réelles (pas le placeholder « aucun
   règlement sélectionné »).
2. Confirmation que le parcours normal (déclaration `EnCours`, traversée ①→②→③→④) n'est pas modifié.
3. Confirmation qu'aucune ligne de `ConstructeurDeclaration.cs`/logique de calcul TVA n'a été modifiée.

## Dépendances / risques
- **Dépend potentiellement d'un nouvel endpoint back** si aucun ne retourne déjà les
  affectations/calcul TVA figés par `declarationId` — à confirmer avant chiffrage.
- **Lié à** TASK-073 (réouverture) : la relecture doit rester cohérente pour les deux statuts
  concernés (intégrée, réouverte).
- **Aucun risque sur le calcul TVA lui-même** — chantier de restitution, pas de logique métier.
