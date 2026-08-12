# TASK-216 — Conventions délai de paiement : Statut toujours « Expirée », Délai (j) toujours à 0 (champs JSON inexistants)

## Contexte

Signalement PO 12/08/2026 (capture d'écran) : écran « Conventions de délai de paiement fournisseurs »
(`ConventionsDelaiPaiementPanel.tsx`) — les 399 conventions affichent toutes le badge « Expirée » et
`Délai (j) = 0`, y compris des conventions dont la date de fin (31/12/2029, 19/07/2027) est largement
postérieure à aujourd'hui (12/08/2026) et devraient donc être « Valide ». Même famille de bug que
TASK-215 (DDP), détectée par lecture de code avant tout correctif.

## Diagnostic

Le DTO backend (`Declaration.API/Dtos/ConventionDelaiPaiementDto.cs`) expose `Valide` et
`NombreJoursDelaisPaiement` (confirmé `api.ts:179,185` : `nombreJoursDelaisPaiement`/`valide`). La
grille lisait des noms inexistants, toujours `undefined` :

- `ConventionsDelaiPaiementPanel.tsx:138` : `valueGetter: (p) => p.data?.delaiJours ?? 0` → toujours 0.
- `ConventionsDelaiPaiementPanel.tsx:139` : `cellRenderer` avec `p.data.estValide` → toujours
  `undefined` → falsy → badge « Expirée » systématique, quelle que soit la vraie date de fin.
- `ConventionsDelaiPaiementPanel.tsx:137` : `p.data.numeroFacture` pour le type « Facture » → devrait
  être `factureNumero` (champ réel du DTO) ; invisible sur la capture (aucune ligne « Facture »
  affichée) mais bug réel — le N° de facture ne s'affichait jamais pour ce type de convention.
- `ConventionsDelaiPaiementPanel.tsx:171` : bouton « Terminer » conditionné par `row.estValide` →
  jamais affiché pour aucune convention, même valide.

## Correctif appliqué

4 corrections dans `ConventionsDelaiPaiementPanel.tsx` : `delaiJours` → `nombreJoursDelaisPaiement`,
`estValide` → `valide` (2 occurrences : badge Statut + condition bouton « Terminer »),
`numeroFacture` → `factureNumero`.

## Fichiers livrés

- `declaration-tva-web/src/ConventionsDelaiPaiementPanel.tsx`

## Vérification

- `npx tsc -b` (déclaration-tva-web) : 0 erreur.
- `grep` de contrôle : 0 occurrence résiduelle de `estValide`/`delaiJours`/`numeroFacture` dans ce
  fichier (les occurrences de `estValide` restant ailleurs dans le projet, `App.tsx`, appartiennent à
  un DTO différent — statut de licence — sans lien).

## Note process

Corrigé directement par Claude à la demande explicite du PO (dérogation ponctuelle déjà actée sur
cette session pour la même famille de bug, cf. TASK-215), correctif de renommage de champ à risque
nul, aucune logique métier touchée. Pas de revue par un 2ᵉ agent indépendant.