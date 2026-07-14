# TASK-085 — Colonne TTC dans le tableau des sous-totaux (écran ③ Calcul TVA)

> **Origine** : demande PO 14/07/2026 — capture d'écran du panneau « Calcul TVA » (`③`), tableau
> « Sous-totaux par taux TVA ». Demande explicite : ajouter une colonne TTC = Total HT + Total TVA,
> pour chaque ligne de taux et pour la ligne `Σ Total`.

## Contexte

`declaration-tva-web/src/CalculTvaPanel.tsx` affiche un tableau de sous-totaux agrégés par taux
TVA (ligne 311-336) :

- Entêtes actuelles (ligne 314-317) : `Taux`, `Nb lignes`, `Total HT`, `Total TVA`.
- Lignes de détail par taux (ligne 322-329) : `TauxBadge`, `nbLignes`, `formatMoney(st.totalHT)`,
  `formatMoney(st.totalTVA)`.
- Ligne de total global `Σ Total` (ligne 332-335) : `formatMoney(totalHT)`, `formatMoney(totalTVA)`.
- Type `SousTotalTaux` (ligne 104-110) et fonction d'agrégation `sousTotauxParTaux` (ligne 111+)
  exposent déjà `totalHT` et `totalTVA` par taux ; `totalHT`/`totalTVA` globaux sont calculés
  séparément (via `useMemo`, lignes 230-235 ou proches) pour la ligne `Σ Total`.

Aucune donnée nouvelle n'est nécessaire : le TTC est une dérivation pure (`totalHT + totalTVA`),
déjà disponible en front à l'endroit du rendu. **Aucun appel API, aucun changement de contrat
back**.

## Périmètre STRICT

- **Inclus** :
  1. Ajouter une colonne `Total TTC` dans l'entête du tableau des sous-totaux (ligne 314-317).
  2. Afficher `formatMoney(st.totalHT + st.totalTVA)` sur chaque ligne de taux (après la colonne
     Total TVA, ligne 322-329).
  3. Afficher `formatMoney(totalHT + totalTVA)` sur la ligne `Σ Total` (ligne 332-335).
- **Exclu** :
  - Toute modification du back (`Declaration.*`), du contrat DTO, ou du calcul métier existant.
  - Le second tableau du panneau (« 2 lignes non valorisées », lignes 349-368) n'est pas concerné.
  - Export Excel (TASK-010) : hors périmètre, à traiter séparément si le PO le demande.

## Objectif

```
Entrée  : totalHT et totalTVA déjà calculés (par taux + global), aucune donnée nouvelle requise
Traitement : TTC = HT + TVA, calcul d'affichage pur, aucun état/appel supplémentaire
Sortie  : colonne "Total TTC" visible sur chaque ligne de taux + sur la ligne Σ Total
```

## Étapes

1. `CalculTvaPanel.tsx:317` : ajouter `<th style={thStyle('right')}>Total TTC</th>`.
2. `CalculTvaPanel.tsx:328` : ajouter une `<td>` affichant
   `formatMoney(st.totalHT + st.totalTVA)`, même style que la colonne Total TVA (poids 700,
   `tabular-nums`).
3. `CalculTvaPanel.tsx:335` : ajouter la `<td>` équivalente pour `Σ Total` avec
   `formatMoney(totalHT + totalTVA)`.
4. Vérifier visuellement que le tableau reste lisible (largeur des colonnes, alignement) sur
   l'écran réel du tunnel — pas seulement en lecture de code.

## Livrables

- `CalculTvaPanel.tsx` modifié (3 emplacements : entête, ligne par taux, ligne Σ Total).
- `VERIFY/TASK-085_verify.md` : capture d'écran ou description précise montrant la colonne TTC
  affichée avec des valeurs cohérentes (TTC = HT + TVA vérifié sur au moins une ligne de taux et
  sur `Σ Total`), sur données réelles du tunnel.

## Critères de validation

- Colonne « Total TTC » visible sur chaque ligne de taux (20%, 10%, 9%, 0%) et sur `Σ Total`.
- Valeur TTC = Total HT + Total TVA à l'euro/MAD près (arithmétique flottante identique à
  `formatMoney` existant, pas de nouvel arrondi introduit).
- Aucune régression sur les colonnes existantes (Taux, Nb lignes, Total HT, Total TVA).
- Aucun appel réseau supplémentaire, aucun changement de `sousTotauxParTaux` ni de
  `agregParFactureTaux`.

## Risques / dépendances

- Aucun risque identifié : changement d'affichage pur, dérivé de données déjà présentes en front.
  Pas de dépendance avec une task en cours.
