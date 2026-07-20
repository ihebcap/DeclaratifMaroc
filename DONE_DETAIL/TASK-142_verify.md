# VERIFY — TASK-142 — Drill « Lignes incohérentes (TTC ≠ HT+TVA) » : rendre l'incohérence visible

Status: ✅ à valider
Module: declaration-tva-web
Type: Front-only (aucun endpoint modifié)

## Résumé

Le drill « Lignes incohérentes (TTC ≠ HT+TVA) » (étape ② Vérifier & Intégrer) instanciait
`<DomainGrid>` sans prop `columns` → repli sur `defaultColumns`, qui n'affiche **pas** `montantTVA`,
2ᵉ opérande de l'égalité que le drill prétend isoler. Impossible pour le PO de vérifier `HT+TVA=TTC`.

Correctif, strictement d'affichage, sur ce seul drill :
1. **Colonne `Montant TVA`** ajoutée (entre `Taux TVA` et `Montant TTC`) — donnée déjà retournée par
   l'API (`LigneCandidateDto.montantTVA`).
2. **Colonne `Écart (HT+TVA−TTC)`** purement dérivée en rendu (aucun appel/recalcul serveur), avec
   mise en évidence rouge (fond + texte gras) dès que l'écart dépasse 0,005 → le PO repère la ligne
   fautive au flash visuel.

## Changements

### `declaration-tva-web/src/VerifierIntegrerPanel.tsx`
- Nouvelle constante module `incoherenceColumns` : `defaultColumns` + `montantTVA` inséré +
  colonne dérivée `ecart` (`derived: true`).
- Site d'appel `<DomainGrid>` du drill (`kind === 'incoherence'`) : passe `columns={incoherenceColumns}`
  et `colsStorageKey='grf.cols.domain.incoherence'`. Les autres drills/écrans restent inchangés
  (spread conditionnel : aucune prop `columns`/`colsStorageKey` transmise hors incohérence).

### `declaration-tva-web/src/DomainGrid.tsx`
- Type `columns` (prop) et `ColumnDef` : ajout de `width?` et `derived?`.
- Nouvelle prop `colsStorageKey` (défaut `'grf.cols.domain'`) → un jeu de colonnes non-standard
  utilise sa propre clé de persistance, sinon il hériterait des préférences enregistrées de la grille
  par défaut (qui ne connaît ni `montantTVA` ni `ecart` → colonnes masquées). **Corrige le risque
  « visible sans action supplémentaire ».**
- `isNumericCol` : ajout de `ecart` (alignement à droite).
- `renderCell` : `montantTVA` et `ecart` ajoutés à la liste des colonnes formatées en monnaie
  (montantTVA n'y était pas → s'affichait en brut).
- Helper `ligneEcart(row) = montantHT + montantTVA − montantTTC`.
- En-tête : colonne `derived` **non triable et non filtrable** (pas de `onClick` sort, pas
  d'`ExcelFilter`) → **aucune clé inexistante envoyée au back** (respect du garde-fou
  `ExcelFilter`/`handleFilterChange`).
- Corps : valeur de la colonne `ecart` calculée en rendu ; fond/texte rouge + gras si
  `|écart| > 0,005`.

## Validation (checklist TASK)

- [x] **Build front OK** — `npm run build` (tsc -b && vite build) : `✓ built in 729ms`, 1847 modules,
      aucune erreur TS. (Warning `INEFFECTIVE_DYNAMIC_IMPORT` préexistant, hors périmètre.)
- [x] **`Montant TVA` visible sans action** sur le drill incohérence — colonne présente dans
      `incoherenceColumns` ; clé de persistance dédiée → non masquée par une préférence antérieure de
      la grille par défaut.
- [x] **Le PO identifie une ligne `HT+TVA ≠ TTC` directement** — colonne `Écart` chiffrée + surlignage
      rouge de la ligne fautive (flash visuel), en plus des 3 montants côte à côte.
- [x] **Non-régression autres écrans** — ① Sélection, Workstation, ProofModal, DeclarationFinalePanel
      n'ont pas de prop `columns`/`colsStorageKey` → `defaultColumns` + clé `grf.cols.domain`
      inchangés. Le spread conditionnel garantit qu'aucune prop n'est passée hors drill incohérence.

## Garde-fous respectés

- **Front-only** : aucun endpoint, DTO ou requête back modifiés (`montantTVA` déjà exposé).
- `defaultColumns` de `DomainGrid` **non modifié** — l'ajout passe par une prop `columns` locale au
  drill.
- Colonne `Écart` **purement dérivée en rendu** ; `derived` empêche l'envoi de la clé `ecart` au
  filtre/tri serveur.

## Dépendances / risques

- Aucun. Restitution pure d'un champ déjà produit côté back + un calcul d'affichage client.
