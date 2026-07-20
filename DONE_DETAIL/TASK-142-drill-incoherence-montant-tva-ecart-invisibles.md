# TASK-142 — Drill « Lignes incohérentes (TTC ≠ HT+TVA) » : impossible de voir où est l'incohérence

Status: DONE — approuvée 20/07/2026 (voir DONE_DETAIL/TASK-142_verify.md, DONE.md, CHANGELOG.md)
Priority: MEDIUM
Risk: LOW
Module: declaration-tva-web

> **Origine :** capture PO — étape ② Vérifier & Intégrer, `TVA1-2026-02`, drill « Retour au
> contrôle → Lignes incohérentes (TTC ≠ HT+TVA) » (556 résultats). Le PO signale : « où se trouve
> l'incohérence ? ça manque de visibilité ». La grille affichée montre `Montant HT`, `Taux TVA`,
> `Montant TTC` mais **pas** `Montant TVA` — impossible de vérifier soi-même l'égalité
> `HT + TVA = TTC` que le drill prétend pourtant isoler.

## Constat (preuve code)

- Le drill est déclenché par `handleDrillIncoherence`
  (`declaration-tva-web/src/VerifierIntegrerPanel.tsx:408-415`), libellé littéralement
  `'Lignes incohérentes (TTC ≠ HT+TVA)'` — la donnée `montantTVA` est le 2ᵉ opérande de la
  comparaison que l'écran affirme isoler.
- Le rendu du drill (`VerifierIntegrerPanel.tsx:526-533`) instancie `<DomainGrid>` **sans prop
  `columns`** → repli sur `defaultColumns` (`DomainGrid.tsx:63-75`), qui ne contient pas
  `montantTVA` (colonnes : `factureNumero`, `tiers`, `origine`, `montantHT`, `tauxTVA`,
  `montantTTC`, `source`, `statutLigne`, `motif`).
- La donnée existe déjà côté back, aucune modification serveur nécessaire :
  `Declaration.API/Dtos/LigneCandidateDto.cs:71-72` expose déjà `montantTVA` (`JsonPropertyName
  "montantTVA"`) pour chaque ligne — c'est un pur défaut d'affichage front.
- Aggravant : aucune colonne/indicateur ne calcule l'écart lui-même
  (`(HT + TVA) − TTC`) — même avec les 3 montants affichés côte à côte, le PO doit faire le calcul
  mental ligne par ligne sur 556 résultats pour repérer la ou les lignes fautives.

## Objectif — strictement la visibilité

Rendre visible, dans ce drill précisément, ce qui compose déjà la donnée retournée par l'API —
aucun nouveau calcul serveur, aucune nouvelle colonne à valoriser côté back.

Minimal (obligatoire) :
- Ajouter `montantTVA` (« Montant TVA ») aux colonnes affichées de ce drill, entre `tauxTVA` et
  `montantTTC` — via une prop `columns` explicite passée à `<DomainGrid>` au site d'appel
  incohérence (`VerifierIntegrerPanel.tsx:526`), pour ne pas changer l'affichage des autres écrans
  utilisant `DomainGrid` sans prop `columns` (① Sélection, `WorkstationPanel`, `ProofModal`,
  `DeclarationFinalePanel` — cf. recherche des usages, aucun ne passe `columns` aujourd'hui).

Recommandé (forme libre au front, non bloquant pour la validation) :
- Une colonne (ou un style de cellule) « Écart » calculée en pur affichage côté client à partir des
  3 champs déjà chargés (`montantHT + montantTVA − montantTTC`), mise en évidence (couleur) quand
  elle est non nulle — pas d'appel API supplémentaire, pas de recalcul serveur. Objectif : que le
  PO repère la ligne fautive au flash visuel plutôt qu'en lisant 3 colonnes par ligne sur 556
  résultats.

## Garde-fous

- **Front-only**, aucun endpoint modifié, `montantTVA` déjà exposé par `LigneCandidateDto`.
- Ne changer les colonnes **que** pour ce drill (site d'appel incohérence de
  `VerifierIntegrerPanel.tsx`) — ne pas modifier `defaultColumns` de `DomainGrid.tsx`, qui est
  partagé par tous les autres écrans/drills et n'a pas été signalé comme problématique.
- Si une colonne « Écart » est ajoutée : purement dérivée en rendu (aucune clé qui n'existe pas
  côté API ne doit être envoyée au filtre serveur — cf. `ExcelFilter`/`handleFilterChange` qui
  transmettent la clé de colonne telle quelle au back).

## Files

- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` (`handleDrillIncoherence` l.408-415, rendu
  du drill l.526-533).
- `declaration-tva-web/src/DomainGrid.tsx` (`defaultColumns` l.63-75, `renderCell` l.196-216,
  `isNumericCol` l.62) — lecture seule pour vérifier l'impact, modification uniquement si le point
  « recommandé » (colonne Écart) est retenu et nécessite un cas particulier de rendu.

## Validation

- [ ] Build front OK (tsc + vite).
- [ ] Sur le drill « Lignes incohérentes (TTC ≠ HT+TVA) » (`TVA1-2026-02` ou équivalent réel),
      `Montant TVA` est visible sans action supplémentaire (pas caché derrière la sélection de
      colonnes).
- [ ] Le PO peut identifier au moins une ligne où `HT + TVA ≠ TTC` directement à l'écran (visuel ou
      calcul immédiat).
- [ ] Non-régression : les autres écrans utilisant `DomainGrid` sans prop `columns` (① Sélection,
      Workstation, ProofModal, DeclarationFinalePanel) gardent leur jeu de colonnes inchangé.

## Dépendances / risques

- Aucune dépendance bloquante (donnée déjà disponible côté API).
- Aucun risque de sécurité/données — restitution pure d'un champ déjà produit côté back.
