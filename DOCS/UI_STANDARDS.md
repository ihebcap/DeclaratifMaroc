# UI_STANDARDS — declaration-tva-web

Règles obligatoires pour tout écran du front (`declaration-tva-web/src/`).
Toute TASK touchant l'UI doit référencer ce fichier. Un écart au standard = REJECT en review.

---

## 1. Grilles — TOUJOURS `ApbsGrid`

**Ne jamais utiliser `AgGridReact` directement.** Le seul point d'entrée est
[src/grid/ApbsGrid.tsx](../declaration-tva-web/src/grid/ApbsGrid.tsx) (wrapper AG Grid v36, thème `legacy` / `ag-theme-alpine`).

### Ce que ApbsGrid fait déjà — NE PAS réimplémenter dans les écrans
- Auto-size des colonnes au premier rendu (une seule passe), puis élargissement de la
  dernière colonne si espace libre. **Ne jamais appeler `sizeColumnsToFit`.**
- Persistance visibilité + ordre des colonnes dans `localStorage` via `storageKey`.
- Sélecteur de colonnes (bouton « Colonnes ») et export Excel (via `gridExport.ts`).
- `defaultColDef` : `sortable`, `resizable`, `filter: 'agTextColumnFilter'`, header wrap.
- `rowHeight` par défaut : 28.

### Conventions d'usage
| Prop | Règle |
|---|---|
| `storageKey` | Obligatoire pour toute grille persistante. Format : `grf.<ecran>.<grille>` (ex: `grf.declarations.liste`) |
| `showExportButton` + `exportFileName` | Obligatoire si la grille présente des données métier. Nom : `<sujet>_<contexte>.xlsx` |
| `height` | Passer une hauteur explicite ou laisser le défaut `500px` ; la grille remplit son conteneur flex |
| `toolbarLeft` | Y placer filtres et compteurs — jamais au-dessus de la grille dans un div séparé |
| Sélection lignes | API v36 : `rowSelection={{ mode, checkboxes }}` — pas les anciennes props `checkboxSelection`/`headerCheckboxSelection` |
| Filtres custom | Utiliser `CustomListFilter` ([src/grid/CustomListFilter.tsx](../declaration-tva-web/src/grid/CustomListFilter.tsx)) pour les filtres liste |

### Formats de colonnes (Maroc / FR)
- **Montants** : `valueFormatter` avec séparateur d'espace et 2 décimales
  (`toLocaleString('fr-FR', { minimumFractionDigits: 2 })`), alignés à droite (`type: 'rightAligned'`).
- **Dates** : affichage `JJ/MM/AAAA`. Le tri doit se faire sur la valeur ISO/Date, pas sur la chaîne formatée.
- **En-têtes** : français, courts ; le wrap est géré par `wrapHeaderText`.

---

## 2. Combobox / Select

Deux cas, pas d'autre variante :

1. **Liste courte et fixe** (exercice, trimestre, type, statut…) :
   `<select className="form-input">` natif. Largeur via `style={{ width }}` si nécessaire.
   Toujours `aria-label` si pas de `<label>` associé.
2. **Liste longue ou recherchable** (tiers, comptes, sociétés…) :
   composant partagé `ApbsSelect` (wrapper `react-select`) — voir TASK dédiée.
   **Ne jamais instancier `react-select` directement dans un écran** : passer par le wrapper
   pour hériter du thème (variables CSS `--bg-secondary`, `--border-color`, `--text-primary`),
   de la taille compacte et du comportement clavier.

Interdits : recréer un dropdown maison (div + onClick), dupliquer les styles inline d'un
select existant, mélanger natif et react-select pour le même type de données.

---

## 3. Style général

- Couleurs/espacement : **uniquement via les variables CSS** de `index.css`
  (`--bg-secondary`, `--border-color`, `--accent-primary`, `--radius-md`, `--shadow-lg`…).
  Jamais de couleur en dur dans un composant.
- Boutons : classes `btn` / `btn-primary` existantes.
- Icônes : `lucide-react` uniquement, taille 13–16 dans les toolbars.
- Textes UI en français.

---

## 4. Checklist review UI (pour VERIFY)

- [ ] Aucune instanciation directe de `AgGridReact` ni de `react-select`
- [ ] `storageKey` unique et nommé selon la convention
- [ ] Montants alignés droite + format fr-FR ; dates JJ/MM/AAAA triables
- [ ] Aucune couleur en dur ; variables CSS respectées
- [ ] `npm run lint` + `npm run build` → 0 erreur
