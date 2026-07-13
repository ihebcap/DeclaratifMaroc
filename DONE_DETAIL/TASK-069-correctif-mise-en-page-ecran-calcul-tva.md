# TASK-069 — Correctif mise en page écran ③ Calcul TVA (bandeau pied rogné) + suppression doublon de total

## Contexte
Sur l'écran ③ Calcul TVA (`CalculTvaPanel.tsx`, TASK-056), le panneau affiche son propre bandeau pied (`CalculTvaPanel.tsx:441-474` : « N lignes agrégées · Total TVA à intégrer ») **au-dessus** de la barre du stepper (`DeclarationStepper.tsx:212-213` : « Étape 3/6 · → Passer à l'intégration »).

**Symptôme constaté (retour PO)** : le bandeau interne du panneau (le total TVA à intégrer) n'est pas visible — il semble rogné/masqué par la barre du stepper en bas d'écran.

**Cause racine identifiée** : le conteneur qui héberge le contenu de chaque étape (`DeclarationStepper.tsx:155`, `<div style={{ flex: 1, overflow: 'hidden' }}>`) est un enfant `flex: 1` d'un parent flex-colonne (`DeclarationStepper.tsx:148`), **sans `minHeight: 0`**. En flexbox colonne, un enfant `flex:1` sans `min-height:0` ne peut pas rétrécir sous la hauteur de son contenu intrinsèque : dès que le contenu de `CalculTvaPanel` (tableau + sous-totaux + bloc non-valorisées) dépasse l'espace disponible, la zone déborde et son propre bandeau pied (`position` normale, pas fixe) se retrouve poussé hors de la fenêtre visible, sous la barre du stepper.

**Second constat (transparence UX)** : le total TVA est aujourd'hui affiché à deux endroits — le bandeau interne du panneau (`CalculTvaPanel.tsx:441-474`) et implicitement repris à l'étape ④ Intégration via `onCalcSummary`. Le PO a validé qu'il faut **améliorer l'écran** ; l'architecte recommande de trancher entre garder les deux bandeaux (redondant) ou n'en garder qu'un — décision PO requise avant implémentation (voir Risques).

## Périmètre STRICT
- **Inclus** :
  - Correctif layout : rendre le bandeau pied de `CalculTvaPanel.tsx` visible en toutes circonstances (résolution de l'écrasement flexbox).
  - Décision + implémentation sur le doublon de total (bandeau panneau vs barre stepper) — une seule des deux options ci-dessous, tranchée par le PO avant codage.
- **Exclu** :
  - Aucun changement de logique de valorisation, d'agrégation (`agregParFactureTaux`, `sousTotauxParTaux`) ni de source de données (`GET /declarations/{id}/lignes`).
  - Aucun changement aux autres écrans du tunnel (①②④⑤⑥) au-delà du conteneur partagé `DeclarationStepper.tsx:148/155` s'il est touché (le correctif layout, s'il est générique, bénéficiera à tous les écrans — à vérifier qu'il ne régresse aucun autre écran).
  - Aucun changement back / API / SQL.

## Cause racine
`DeclarationStepper.tsx:148-155` : conteneur flex-colonne (`flexDirection: 'column'`) avec enfant `flex: 1; overflow: hidden` mais **sans `minHeight: 0`** — anti-pattern flexbox connu (un flex-item ne rétrécit pas sous son contenu par défaut, `min-height: auto` implicite prime sur `flex: 1`).

## Objectif
```
Entrée : écran ③ Calcul TVA avec une sélection de règlements produisant un volume de lignes variable
Traitement : le conteneur d'étape respecte la hauteur allouée (scroll interne au lieu de débordement) ; total TVA affiché une seule fois, toujours visible
Sortie : bandeau pied de CalculTvaPanel (ou son équivalent tranché par le PO) visible sans scroll de page ni recouvrement par la barre du stepper, sur tout volume de données
```

## Décision PO (13/07/2026) — actée
**Option A retenue** : le bandeau interne du panneau (`CalculTvaPanel.tsx:441-474`, total TVA + nb lignes agrégées) est **conservé tel quel**. Seul le correctif layout (`minHeight: 0`) est appliqué. Aucune suppression de bandeau, aucun enrichissement de la barre stepper.

## Étapes
1. `DeclarationStepper.tsx:155` : ajouter `minHeight: 0` au style du conteneur `<div style={{ flex: 1, overflow: 'hidden' }}>` (et `display: 'flex', flexDirection: 'column'` si l'agent l'estime nécessaire pour que `CalculTvaPanel` — qui est lui-même `flex:1` en interne — hérite correctement de la contrainte de hauteur).
2. Vérifier que ce correctif ne régresse pas les autres étapes du stepper (①②④⑤⑥) qui partagent le même conteneur — test manuel sur chaque étape avec un volume de données réaliste.
3. Selon la décision PO (option A ou B ci-dessus) : implémenter le choix retenu.
4. Build front (`npm run build` / `tsc`) vert.

## Livrables
- `DeclarationStepper.tsx` avec conteneur d'étape corrigé (`minHeight: 0`).
- Selon décision PO : `CalculTvaPanel.tsx` inchangé (option A) ou bandeau pied retiré + barre stepper enrichie (option B).
- `VERIFY/TASK-069_verify.md` : captures de l'écran ③ avec un volume de lignes suffisant pour déborder avant correctif (ex. sélection multi-règlements → dizaines de lignes), montrant le bandeau total pleinement visible ; captures des autres étapes du stepper (①②④⑤⑥) sans régression visuelle ; sortie build front sans erreur.

## Critères de validation
- Le total TVA à intégrer (et le nb de lignes agrégées si option A) est visible sans scroll de la fenêtre ni recouvrement par la barre du stepper, quel que soit le volume de lignes.
- Aucune régression visuelle sur les autres étapes du stepper (conteneur partagé).
- Un seul total TVA affiché à l'écran (pas de doublon visuel ambigu) — cohérent avec la décision PO.
- Build front vert, aucun import/symbole orphelin.
- Aucun changement back / API / SQL, aucun changement de logique d'agrégation.

## Risques / dépendances
- Risque faible : correctif CSS ciblé sur un conteneur partagé — bien tester les 6 étapes pour écarter toute régression (notamment ① Règlements et ② Affectations qui ont aussi des grilles volumineuses).
- **Bloquant réel** : le choix entre option A et B doit être acté par le PO avant implémentation — ne pas improviser (règle « ne jamais improviser un contexte manquant »).
- Dépendance : aucune — TASK-056 (③ Calcul TVA) et TASK-053 (stepper/tunnel) déjà livrées.