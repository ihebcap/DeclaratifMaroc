# TASK-141 — Rendre visible le motif du badge « À contrôler » (reste à affecter)

Status: DONE — approuvée 19/07/2026 (voir DONE_DETAIL/TASK-141_verify.md, DONE.md, CHANGELOG.md)
Priority: MEDIUM
Risk: LOW
Module: declaration-tva-web

> **Origine :** capture PO — écran ① Sélection (`TVA1-2026-01`), 2 lignes en statut « À contrôler »
> sans aucune explication à l'écran. Le PO ne peut pas savoir pourquoi ces lignes s'écartent du
> statut « Éligible ».

## Constat (preuve code)

- `statutDe` (`declaration-tva-web/src/ReglementsSelection.tsx:70-76`) : le statut `controle` n'a
  **qu'une seule cause possible** — `Math.abs(row.resteAAffecter ?? 0) > 0.005` (affectation
  partielle). La donnée existe déjà dans la projection (`ReglementRow.resteAAffecter`, ligne 35).
- `StatutBadge` (lignes 84-105) affiche un libellé enrichi pour le statut `bloque` (4 sous-motifs :
  Impayé, Hors périmètre, Non affecté, Déjà déclaré), mais **rien** pour `controle` — juste le
  libellé générique « À contrôler ». Aucun `title`/tooltip nulle part sur la ligne.
- Aggravant réel, visible dans la capture PO : la colonne « Affecté » (`affecteLabel`, lignes
  109-116) arrondit `montantAffecte/montant` en pourcentage entier — une 2ᵉ ligne affiche
  « partiel 100 % » alors qu'un reste non nul subsiste (sinon le statut serait `Éligible`). Le PO
  voit « 100 % » et ne comprend pas pourquoi ce n'est pas éligible.
- Root cause unique : le motif (« reste à affecter = X MAD ») existe déjà en donnée, il n'est
  simplement jamais restitué à l'écran.

## Objectif — strictement la visibilité

Afficher le **montant réel du reste à affecter** partout où le badge « À contrôler » apparaît,
symétrique à ce qui existe déjà pour « Bloqué ». Pas de redesign, pas de nouveau statut, pas de
nouvelle donnée back — uniquement rendre visible ce qui est déjà calculé.

Proposition minimale (arbitrage front libre sur la forme exacte, périmètre figé sur le fond) :
- `title` HTML sur le badge (`StatutBadge`, `controle`) : `Reste à affecter : {resteAAffecter} MAD`.
- Et/ou un libellé complémentaire directement dans la colonne Affecté à côté de « partiel X % »,
  ex. `partiel 100 % (reste 27,40 MAD)`, pour éviter l'effet trompeur de l'arrondi entier.

## Garde-fous

- **Front-only**, aucun endpoint modifié, aucune donnée back ajoutée — `resteAAffecter` est déjà
  dans la projection consommée par `ReglementsSelection.tsx`.
- Ne pas toucher à `statutDe` (règle de statut inchangée) ni aux 4 sous-motifs déjà corrects de
  `bloque`.
- Ne pas résoudre l'arrondi de `affecteLabel` en changeant son calcul (ex. plus de décimales) sans
  besoin — le sujet est d'ajouter l'info manquante, pas de retoucher l'existant qui fonctionne.

## Files

- `declaration-tva-web/src/ReglementsSelection.tsx` (`statutDe` ligne 70, `StatutBadge` lignes
  84-105, `affecteLabel` lignes 109-116).

## Validation

- [ ] Build front OK (tsc + vite).
- [ ] Sur une ligne en statut « À contrôler », le motif « reste à affecter : X MAD » est visible
      sans action supplémentaire (tooltip ou texte inline — pas caché derrière un clic/drill).
- [ ] Cas réel de la capture PO (`TVA1-2026-01`, lignes `RC26040056` et `COF25120089`) : le reste
      affiché correspond au calcul (`montant - montantAffecte`).
- [ ] Non-régression : badge « Bloqué » et ses 4 sous-motifs inchangés.
- [ ] Non-régression : badge « Éligible » inchangé (pas de tooltip parasite).

## Dépendances / risques

- Aucune dépendance bloquante (donnée déjà disponible).
- Aucun risque de sécurité/données — restitution pure d'un calcul déjà produit côté front.
