# TASK-086 — Récapitulatif de l'intégration vide (écran ④) : robustesse front

> **🔀 ABSORBÉE par TASK-090 (14/07/2026 — décision PO tunnel 3 étapes)** : la fusion ③+④
> supprime le pipe d'état ③→④ (`calcTVA`/`onCalcSummary`), cause racine du symptôme, et branche
> le `RecapCard` sur `/checkup` directement dans le nouvel écran « Vérifier & Intégrer ». Les
> critères de validation ci-dessous (récap rempli après F5/ouverture directe) sont repris tels
> quels par TASK-090. **Ne pas implémenter séparément.** Conservée pour traçabilité de l'analyse.

> **Origine** : analyse architecte 14/07/2026, suite à une question PO sur l'écran ④ Intégration
> (capture d'écran fournie). Le bloc « Récapitulatif de l'intégration » en haut de l'écran
> s'affiche vide (ou à 0) dans certains cas.

## Contexte

`declaration-tva-web/src/IntegrationPanel.tsx` (bloc `RecapCard`, lignes 286-345) affiche
`nbReglements`, `nbLignes`, `totalTVA`, `reconciliation.integrees`.

Ces valeurs proviennent **exclusivement** de deux `useState` définis dans
`DeclarationStepper.tsx` (lignes 58-60), initialisés à `{ totalTVA: 0, nbLignes: 0 }` / `[]`, et
remplis **uniquement** si l'utilisateur passe physiquement par l'étape ③ « Calcul » dans la
session courante (callback `onCalcSummary`, ligne 181).

Or `fetchInfo()` (lignes 64-76) redirige automatiquement l'utilisateur vers l'étape ④ dès que la
déclaration est déjà intégrée (`statut !== 0`), **sans repasser par ③**. Résultat : un rechargement
de page sur l'étape ④, ou l'ouverture directe d'une déclaration déjà avancée, laisse `calcTVA` et
`selectedRows` à leur valeur par défaut → le bloc affiche des zéros, d'où l'impression de bloc
vide.

**Aucun fallback API n'existe** alors que le backend a déjà tout : l'endpoint
`GET /declarations/{id}/checkup` (`DeclarationsController.cs`, lignes 201-280+) renvoie
`recapSource` et `recapTaux` (lignes 220-242), qui couvrent les mêmes agrégats (nb lignes, totaux
HT/TVA/TTC). Ces champs sont déjà consommés par `SummaryPanel.tsx` et
`ControleDeclarationPanel.tsx` (étape ⑤) — donc le contrat existe et fonctionne, il n'est
simplement pas branché sur l'écran ④.

## Périmètre STRICT

- **Inclus** :
  1. `IntegrationPanel.tsx` : source des chiffres du `RecapCard` = réponse `/checkup` (déjà
     chargée pour les contrôles pré-intégration, cf. `ChecklistCard`), **pas** les props
     `nbLignes`/`totalTVA`/`nbReglements` transmises depuis ③.
  2. Étendre l'interface `CheckupResult` côté front pour typer `recapSource`/`recapTaux` (déjà
     renvoyés par l'API, non déclarés côté TS).
  3. Conserver les props ③ uniquement comme valeur d'affichage immédiate le temps du chargement
     réseau (aucun changement de comportement pendant la navigation normale du tunnel).
- **Exclu** :
  - Toute modification du contrat API (`recapSource`/`recapTaux` existent déjà, lignes 220-242 du
    contrôleur).
  - Le comportement de redirection automatique de `fetchInfo()` (hors périmètre — pas un bug en
    soi, juste une cause du symptôme).

## Objectif

```
Entrée  : réponse /checkup déjà chargée par IntegrationPanel (recapSource + recapTaux présents)
Traitement : RecapCard lit ces champs au lieu des props volatiles de l'étape ③
Sortie  : bloc "Récapitulatif de l'intégration" toujours rempli, y compris après rechargement de
          page ou ouverture directe d'une déclaration déjà avancée
```

## Livrables

- `IntegrationPanel.tsx` modifié (source des données du `RecapCard`).
- `VERIFY/TASK-086_verify.md` : preuve que le bloc reste rempli après un rechargement de page sur
  l'étape ④ (F5), pas seulement en navigation normale depuis ③.

## Critères de validation

- Bloc « Récapitulatif de l'intégration » non vide dans les deux cas : navigation normale
  ①→②→③→④, **et** rechargement/ouverture directe sur ④.
- Aucune régression sur les contrôles pré-intégration (`ChecklistCard`).
- Aucun nouvel appel réseau (les données sont déjà dans la réponse `/checkup` existante).

## Risques / dépendances

- Aucun risque backend (lecture seule d'un contrat déjà exposé). Peut être livré indépendamment de
  TASK-087/TASK-088 (même fichier, zones distinctes du composant — à séquencer si livrées
  ensemble pour éviter les conflits d'édition).
