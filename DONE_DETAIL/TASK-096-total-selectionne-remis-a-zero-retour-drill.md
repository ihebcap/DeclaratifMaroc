# TASK-096 — « Total sélectionné » remis à 0 après retour de « Détail des lignes » (écran ① Sélection)

## Contexte

Signalement PO (14/07/2026, capture écran ① Sélection, `TVA1-2026-01`) : 148 règlements
sélectionnés, sélection confirmée (`Sélectionnés : 148` visible en pied de grille), mais
**« Total sélectionné : 0,00 MAD »**. Reproduction : sélectionner des lignes → cliquer
« Détail des lignes » (bascule `showDrill=true`, `DeclarationStepper.tsx:105/153`) → revenir à
l'étape ① (`showDrill=false`) → le total affiché en pied de `ReglementsSelection.tsx` retombe à 0
alors que le compteur de sélection reste correct.

## Cause racine (analyse code, confirmée)

Deux totaux distincts coexistent pour la même sélection — c'est la source du bug :

1. **`DeclarationStepper.tsx:100`** (bandeau bas du tunnel) : `selectedRows.reduce((s, r) => s + r.montant, 0)`.
   `selectedRows` est un **state du parent** (`DeclarationStepper`), jamais réinitialisé par le
   montage/démontage de `ReglementsSelection` → **reste correct** après retour de « Détail des lignes ».
2. **`ReglementsSelection.tsx:360-364`** (pied de la grille elle-même, celui visible sur la capture PO) :
   ```ts
   const selectedTotal = useMemo(() => {
     let sum = 0;
     knownRowsRef.current.forEach((row, key) => { if (selectedKeys.has(key)) sum += row.montant; });
     return sum;
   }, [selectedKeys]);
   ```
   `knownRowsRef` (`useRef`, ligne 189) est un **registre local au composant**, peuplé par
   `fetchAll` (ligne 258) à chaque montage. `DeclarationStepper.tsx:153` **démonte**
   `ReglementsSelection` quand `showDrill` passe à `true` (affiche `AffectationsDrill` à la place)
   et le **remonte** à `false` — `knownRowsRef` repart alors d'une `Map` vide.

   Au remontage, `fetchAll` relance un chargement asynchrone qui repeuple `knownRowsRef` — mais le
   tableau de dépendances du `useMemo` ne contient que `[selectedKeys]`. Or `selectedKeys` est la
   **même référence** que celle détenue par le parent (`DeclarationStepper`, non réinitialisée par
   le remontage) : elle n'a pas changé, donc le `useMemo` **ne recalcule jamais** après le
   chargement — il reste figé sur sa valeur de premier rendu, calculée avant que `fetchAll` n'ait
   fini (donc sur une `Map` encore vide) → **0**.

   `useRef` n'est volontairement pas suivi par React (pas de re-render sur mutation) : le bug est
   un oubli de dépendance, pas un problème de timing réseau.

## Périmètre STRICT

- **Inclus** : corriger le calcul de `selectedTotal` dans `ReglementsSelection.tsx` pour qu'il
  reste exact après un cycle démontage/remontage (aller-retour « Détail des lignes »), **sans**
  dupliquer une deuxième fois la logique déjà correcte du parent.
- **Exclu** :
  - Toute modification du bandeau bas du tunnel (`DeclarationStepper.tsx:96-126`), déjà correct.
  - Toute modification de l'endpoint `/rapprochement` ou de la logique de sélection
    (`toggleRow`/`toggleAllVisible`).
  - Refonte de la gestion d'état de la sélection (`selectedKeys`/`selectedRows`) au-delà de ce qui
    est nécessaire pour fiabiliser ce total.

## Objectif

```
Entrée  : sélection de N règlements sur ①, montants connus (allData/knownRowsRef)
Défaut  : aller sur « Détail des lignes » puis revenir sur ① fait retomber le total local à 0
          (compteur « Sélectionnés : N » reste correct, lui)
Sortie  : le total affiché en pied de ReglementsSelection reste exact quel que soit le nombre
          de montages/démontages du composant, sans dépendre d'un ref non suivi par React
```

## Pistes de correction (au choix de l'implémenteur, à trancher en VERIFY)

- **Option A (minimale)** : ajouter `allData` au tableau de dépendances du `useMemo` de
  `selectedTotal` (ligne 364) — `allData` est déjà un `state` React (donc suivi), mis à jour par
  `fetchAll` à chaque montage ; le `useMemo` recalculera alors correctement dès que les données
  rechargées arrivent, même si `selectedKeys` n'a pas changé.
- **Option B (élimine la duplication)** : passer `selectedRows` (déjà maintenu correctement par
  `DeclarationStepper`) en prop à `ReglementsSelection` et calculer `selectedTotal` à partir de ce
  prop plutôt que de `knownRowsRef`, supprimant le calcul redondant/fragile.
- Ne pas retenir une solution qui réinitialise `selectedKeys`/`selectedRows` au démontage (romprait
  la sélection persistante entre ① et « Détail des lignes », régression fonctionnelle).

## Livrables

- `ReglementsSelection.tsx` corrigé (et éventuellement câblage `DeclarationStepper.tsx` si Option B).
- `VERIFY/TASK-096_verify.md` : reproduction du scénario exact du signalement PO (sélectionner N
  règlements → « Détail des lignes » → retour ① → total non nul et exact), capture avant/après.

## Critères de validation

- Sélectionner des règlements sur ①, cliquer « Détail des lignes », revenir sur ① : le total
  affiché en pied de grille reste identique à celui affiché avant le clic (et cohérent avec le
  bandeau bas du tunnel).
- Aucune régression sur le total lors d'un filtrage (Étal/tiers/numéro) pendant que des lignes
  restent sélectionnées hors filtre.
- Aucune régression sur `Sélectionnés : N` (compteur déjà correct, à ne pas casser).
- Build front (`tsc`/`vite`) vert, aucun warning de dépendance React (`exhaustive-deps`) introduit.

## Risques / dépendances

- Risque faible, front-only, un seul écran concerné (`ReglementsSelection.tsx`, éventuellement
  `DeclarationStepper.tsx` en Option B).
- Aucune dépendance bloquante avec les tasks en cours.
