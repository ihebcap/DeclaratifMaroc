# TASK-158 — Popup « Colonnes » ouvert hors écran quand le bouton est proche du bas

Status: 🆕 à faire
Priority: LOW
Risk: LOW (front seul, aucun changement de donnée)
Module: declaration-tva-web

> **Origine :** signalement PO (capture écran, écran ① Sélection de `TVA1-2026-01`, 23/07/2026) — clic
> sur le bouton « Colonnes » (pied de grille) : le popup de sélection de colonnes s'ouvre **vers le
> bas** et se retrouve caché par le bas de la fenêtre/barre des tâches, inutilisable.

## Constat (preuve de code)

- `declaration-tva-web/src/ColumnSelector.tsx:26-33` (`computePosition`) calcule uniquement `left`
  (avec correction si ça déborde à droite) mais `top` est **toujours** `rect.bottom + 4` — jamais
  d'alternative « au-dessus du bouton ». Aucun test de l'espace restant entre le bas du bouton et
  `window.innerHeight`.
- Le popup peut atteindre jusqu'à ~340px de haut (`maxHeight: 280px` de la liste l.68 + padding +
  pied « Tout afficher » l.81-85). Si le bouton est à moins de ~340px du bas du viewport — cas
  visible sur la capture, bouton « Colonnes » en pied de grille, proche du bas de fenêtre — le popup
  déborde sous la fenêtre et devient inaccessible (aucun scroll de page ne le ramène, il est en
  `position: fixed` via `createPortal` sur `document.body`).
- Composant partagé par 6 écrans (`Grep` : `ReglementsSelection.tsx` — celui de la capture —,
  `FactureInterrogation.tsx`, `RapprochementInterrogation.tsx`, `AffectationsDrill.tsx`,
  `ControlGrid.tsx`, `DomainGrid.tsx`) : correctif à faire une seule fois, dans `ColumnSelector.tsx`,
  bénéficie aux 6.

## Objectif

Dans `computePosition` (`ColumnSelector.tsx`), calculer l'espace disponible **sous** le bouton
(`window.innerHeight - rect.bottom`) et, s'il est insuffisant pour la hauteur réelle du popup (mesurer
via `popupRef` après un premier rendu, ou une estimation cohérente avec `maxHeight`+paddings), ouvrir
le popup **au-dessus** du bouton à la place (`top` calculé en remontant depuis `rect.top`, popup
ancré par son bas). Comportement standard de dropdown « flip » — même logique horizontale déjà en
place pour `left` (l.29-31), à appliquer symétriquement en vertical.

Cas limite à couvrir : ni au-dessus ni en dessous n'offre assez de place (fenêtre très basse) — dans
ce cas, choisir le côté offrant le plus d'espace et laisser le `overflowY: 'auto'` existant (l.68)
absorber le reste, pas de nouveau mécanisme de scroll à inventer.

## Garde-fous

- Ne toucher que `ColumnSelector.tsx` — ne pas dupliquer la logique dans les 6 écrans appelants.
- Ne pas changer `POPUP_WIDTH`/la logique horizontale existante (l.29-31), qui fonctionne déjà.
- Front seul, aucun endpoint ni donnée concernés.

## Files

- `declaration-tva-web/src/ColumnSelector.tsx` (`computePosition` l.26-33, popup l.58-88).

## Validation

- [ ] Build front (`tsc -b && vite build`) OK.
- [ ] Reproduction du cas réel : bouton « Colonnes » proche du bas de la fenêtre (écran ① Sélection,
      comme la capture PO) → popup s'ouvre au-dessus du bouton, entièrement visible, toutes les
      cases à cocher accessibles au clic.
- [ ] Non-régression : bouton « Colonnes » en position haute d'écran (assez d'espace en dessous) →
      comportement inchangé (popup toujours en dessous).
- [ ] Non-régression sur les 5 autres écrans utilisant `ColumnSelector` (`FactureInterrogation`,
      `RapprochementInterrogation`, `AffectationsDrill`, `ControlGrid`, `DomainGrid`).

## Dépendances / risques

- Aucune dépendance. Risque faible, isolé à un seul composant partagé.
