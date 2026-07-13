# TASK-067A Verify — Filtres « valeurs disponibles » (Excel-like), front-only

> Grilles client-side uniquement (jeu complet déjà en mémoire) : `AffectationsDrill.tsx`,
> `ReglementsSelection.tsx`, `ControlGrid.tsx`. Aucun appel back ajouté, aucune écriture.

## Build

```
npx tsc --noEmit        → 0 erreur
npx oxlint <3 fichiers>  → 0 erreur (2 warnings react-refresh + 1 exhaustive-deps préexistants,
                            non liés aux colonnes modifiées)
npx vite build           → ✓ built (dist générée)
```

## Modifications par écran

| Fichier | Colonnes passées en `list` | Source des options |
|---|---|---|
| `AffectationsDrill.tsx` | `numeroReglement`, `factureNumero` (déjà : `tiers`, `origine`, `statutConformite`, `tauxTVA`) | `allRows` (jeu complet en mémoire, `filterOptionsFor`) |
| `ReglementsSelection.tsx` | `numeroReglement`, `tiers` (déjà : `mode`, `rapprocheBanque`, `statut`) | `allData` — filtres appliqués côté client (retirés des params serveur `/rapprochement`) |
| `ControlGrid.tsx` | `factureNumero`, `designation`, `tiers`, `identifiantFiscal`, `ice` (déjà : `tauxTVA`, `modePaiement`, `source`) | `distincts` (mécanisme générique existant, aucune modification requise) |

## Critères de validation

| Critère | Résultat |
|---|---|
| Chaque colonne énumérable des 3 grilles offre la liste des valeurs disponibles + recherche (mode `list`), y compris n° pièce/référence à forte cardinalité | ⬜ À confirmer visuellement (captures ci-dessous) |
| Colonnes montant/date inchangées (plage) | ✅ Non touchées (revue du diff) |
| Aucun appel back ajouté, aucune écriture | ✅ `ReglementsSelection` : `tiers`/`numeroReglement` retirés des query params `/rapprochement`, filtrage déplacé côté client sur `allData` |
| Build front tsc+vite + oxlint 0 erreur | ✅ voir section Build |
| Compteur exact après filtrage (aucune régression TASK-040) | ⬜ À confirmer visuellement par écran |

## Captures Excel-like par écran

- [ ] `AffectationsDrill` — filtre `Règlement` et `Facture` en liste à cases cochables + recherche
- [ ] `ReglementsSelection` — filtre `N° Règlement` et `Fournisseur` en liste, compteur `X sur Y` exact
- [ ] `ControlGrid` — filtre `N° Facture` / `Désignation` / `Tiers` / `IF` / `ICE` en liste, compteur exact

_(captures à ajouter ici après vérification manuelle dans le navigateur)_

## Notes

- `ReglementsSelection` : les filtres `tiers`/`numeroReglement` étaient auparavant des LIKE serveur
  (query params `tiers`/`numero` sur `/rapprochement`). Passage en `list` → filtrage désormais
  effectué côté client sur `allData` (même patron que le filtre `statut`, déjà client-side).
  Les query params correspondants ont été retirés de `fetchAll`.
- `ControlGrid` : aucune modification de la logique de filtrage/distinct nécessaire — le mécanisme
  générique (`distincts` sur toute colonne `filterType === 'list'`) couvrait déjà ce cas.
