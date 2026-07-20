# TASK-089 — Suppression de `SummaryPanel.tsx` (code mort)

> **Origine** : analyse architecte 14/07/2026, en creusant la redondance des récaps par taux TVA
> (③/④/⑤) signalée par le PO. `SummaryPanel.tsx` n'est câblé nulle part dans le tunnel réel
> (`DeclarationStepper.tsx`) : il lit `DeclarationModele` de `mockData.ts` (donnée figée de démo)
> et n'apparaît jamais à l'utilisateur. Seule référence trouvée hors lui-même : un commentaire
> dans `CalculTvaPanel.tsx` le citant comme « intouchable », ce qui a induit une confusion dans
> une version antérieure de TASK-087.

## Contexte

`grep SummaryPanel` sur `declaration-tva-web/src/` ne remonte que :
- `SummaryPanel.tsx` (le fichier lui-même)
- `CalculTvaPanel.tsx` (un commentaire le mentionnant, aucun import)

Aucun `import SummaryPanel` dans `DeclarationStepper.tsx` ni ailleurs. Conforme à la règle projet
« pas de dette silencieuse » (`CLAUDE.md`) et à la demande PO de simplifier au maximum : un
composant non branché, sur données mock, n'a aucune valeur et complique la lecture du code (source
de confusion déjà constatée).

## Périmètre STRICT

- **Inclus** :
  1. Confirmer par un grep exhaustif (`SummaryPanel`, et son éventuel usage dans des tests
     Playwright/e2e) qu'aucune référence active n'existe.
  2. Supprimer `SummaryPanel.tsx`.
  3. Retirer le commentaire obsolète dans `CalculTvaPanel.tsx` qui le mentionne.
  4. Si `mockData.ts` n'est plus utilisé par personne d'autre après cette suppression, le signaler
     dans le VERIFY (sans le supprimer sans vérification supplémentaire — hors périmètre si un
     autre usage existe).
- **Exclu** :
  - Toute modification des écrans réellement câblés (③④⑤).

## Objectif

```
Entrée  : SummaryPanel.tsx confirmé non câblé (0 import réel)
Traitement : suppression du fichier + nettoyage du commentaire qui y référait
Sortie  : build front inchangé (0 erreur), un fichier de moins, aucune confusion résiduelle sur
          "quel est l'écran ⑤ réel"
```

## Livrables

- `SummaryPanel.tsx` supprimé.
- `CalculTvaPanel.tsx` : commentaire obsolète retiré.
- `VERIFY/TASK-089_verify.md` : grep exhaustif prouvant l'absence de référence avant suppression,
  build tsc+vite vert après suppression.

## Critères de validation

- Build front (`tsc` + `vite build`) 0 erreur après suppression.
- Aucun test e2e Playwright ne référence `SummaryPanel`.
- `git grep SummaryPanel` ne renvoie plus rien après la task.

## Risques / dépendances

- Risque quasi nul (suppression de code confirmé mort). Indépendante de TASK-086/087/088, peut
  être livrée seule ou en même temps.
