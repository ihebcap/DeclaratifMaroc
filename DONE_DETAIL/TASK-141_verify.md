# VERIFY — TASK-141 — Motif du badge « À contrôler » (reste à affecter)

## Résumé
Front-only. Restitution du motif unique du statut `controle` (affectation partielle) via la donnée
`resteAAffecter` déjà présente dans la projection. Aucun endpoint / donnée back modifié.

## Modifications
Fichier : `declaration-tva-web/src/ReglementsSelection.tsx`

1. `StatutBadge` — pour `statut === 'controle'`, ajout d'un `title` HTML sur le `<span>` :
   `Reste à affecter : {formatMoney(resteAAffecter)}`. Symétrique aux sous-motifs de `bloque`.
2. `affecteLabel` — le cas partiel affiche désormais `partiel {pct} % (reste {formatMoney(reste)})`
   pour lever l'effet trompeur de l'arrondi entier (« 100 % » avec reste non nul).

Note unité (correction rejet 1er passage) : `formatMoney` (`utils.tsx:24-26`) utilise
`Intl.NumberFormat(currency:'MAD')` et porte donc déjà le suffixe « MAD » (ex. « 27,40 MAD »).
Aucun littéral « MAD » n'est ajouté dans les deux template strings — sinon doublon « MAD MAD ».

## Garde-fous respectés
- `statutDe` inchangé (règle de statut intouchée).
- 4 sous-motifs de `bloque` inchangés.
- Calcul de `pct` (`affecteLabel`) inchangé — ajout d'info seulement, pas de retouche de l'existant.
- Aucun endpoint modifié ; `resteAAffecter` déjà dans `ReglementRow`.

## Validation
- [x] Build front OK (tsc + vite) — `✓ built in 689ms`, aucune erreur TS.
- [x] Ligne « À contrôler » : motif `Reste à affecter : X MAD` visible sans action (tooltip `title`)
      + texte inline dans la colonne Affecté (`partiel X % (reste X MAD)`), pas de drill.
      Suffixe « MAD » porté une seule fois (par `formatMoney`), pas de doublon.
- [~] Valeur affichée = `row.resteAAffecter` (champ de la projection back), rendu tel quel via
      `formatMoney`. **Non reproduit sur les 2 lignes réelles `RC26040056` / `COF25120089`** :
      contrôle de code uniquement, pas d'exécution contre les données `TVA1-2026-01`. À confirmer
      par un test réel avant clôture si le PO l'exige — non sur-déclaré ici.
- [x] Non-régression badge « Bloqué » et ses 4 sous-motifs — code intact.
- [x] Non-régression badge « Éligible » — pas de `title` (branche non atteinte), pas de tooltip parasite.

## Statut
APPROVE demandé.
