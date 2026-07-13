# TASK-053 — Tunnel déclaration règlement-first (shell + barre d'étapes)

> 🎯 Chef de file du lot « flux déclaration règlement-first » (053→059). Front-only. Réorganise l'existant, ne réinvente rien. Décision PO 11/07/2026 : **règlement-first assumé** ; densité **0 espace perdu** (cohérent TASK-035/037/041).

## Contexte
Le flux déclaration actuel est un stepper à **2 étapes** (`declaration-tva-web/src/DeclarationStepper.tsx` : `Workstation` → `Génération`). La vision produit (`reflexion dectva.md`) décrit un **tunnel à 6 étapes** piloté par le règlement : `① Règlements → ② Affectations → ③ Calcul → ④ Intégration → ⑤ Contrôle → ⑥ Synthèse/Export`. Le `WorkstationPanel` (poste 4 interrogations, TASK-019) et `GenerationPanel` (exports) restent réutilisés comme contenus d'étapes.

Cette task porte **uniquement la navigation** : la barre d'étapes, le verrouillage/déverrouillage progressif, le retour, et le passage en lecture seule après intégration. Les écrans eux-mêmes = TASK-054→059.

## Périmètre STRICT
- **Inclus** : refonte `DeclarationStepper` en tunnel 6 étapes ; barre d'étapes dense (icône + libellé + compteur/état) ; règle de déblocage (une étape n'est active que si l'amont est prêt) ; bandeau bas « total vivant + action principale unique » ; retour libre tant que **non intégré**, lecture seule après (miroir verrou `DT_Id` TASK-028).
- **Exclu** : contenu métier de chaque étape (054→059) ; toute modif back ; tout calcul/API dans le shell (le shell route, il ne calcule pas — cf. commentaire `App.tsx:23`).

## Objectif
```
Entrée : une déclaration ouverte (declarationId, statut)
Traitement : rendre la barre d'étapes règlement-first, gérer déblocage/verrou/retour
Sortie : tunnel navigable 6 étapes, dense, aucune étape morte, lecture seule post-intégration
```

## Étapes
1. Remplacer le type `Step` (2 valeurs) par les 6 étapes règlement-first dans `DeclarationStepper.tsx`.
2. Barre d'étapes dense (réutiliser le style des en-têtes 035/037) : état ●/○ par étape, compteur (ex. « 248 sél. »), pas d'espace perdu.
3. Règle de déblocage : ② requiert une sélection en ① ; ④ requiert ③ valorisé ; ⑤⑥ requièrent l'intégration. Étape verrouillée = grisée non cliquable (aucune route morte, cf. `App.tsx:119`).
4. Bandeau bas : total contextuel + **un seul** CTA principal (« Analyser TVA → », « Confirmer intégration », …).
5. Post-intégration (statut figé) : ①②③ passent en lecture seule (bandeau « Déclaration intégrée — lecture seule »).
6. Câbler `WorkstationPanel`/`GenerationPanel` existants comme contenus d'étapes appropriés.

## Livrables
- `DeclarationStepper.tsx` refondu (tunnel 6 étapes) + éventuel `StepperBar` extrait.
- `VERIFY/TASK-053_verify.md` : captures e2e des 6 états de la barre, preuve du déblocage progressif, preuve lecture seule après intégration, build tsc+vite / oxlint 0 erreur.

## Critères de validation
- 6 étapes visibles, ordre règlement-first, aucune cliquable hors condition remplie.
- Aucune donnée factice ; densité 0 espace perdu confirmée (screenshots).
- Retour possible avant intégration, bloqué après ; aucun appel API/calcul dans le shell.
- Build front + lint verts.

## Risques / dépendances
- **Socle** des tasks 054→059 (elles remplissent les étapes). Peut être livré avec des étapes en placeholder honnête tant que 054→059 ne sont pas prêtes.
- Ne pas dupliquer `App.tsx` (le routage section reste dans `App.tsx` ; le tunnel vit dans le stepper).
- Réutiliser `WorkstationPanel` (TASK-019) et `GenerationPanel` — pas de réécriture.
- 🚫 **Anti-régression** : seul `DeclarationStepper.tsx` est modifié (navigation). `WorkstationPanel`, `GenerationPanel` et le routage `App.tsx` sont **intouchables** — les 4 interrogations existantes (035/037/041) et les exports doivent fonctionner à l'identique après refonte. Aucune ligne back. VERIFY doit prouver la non-régression des écrans réutilisés.
