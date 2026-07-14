# TASK-083 — Thème visuel sobre + touche couleur signature (« Déclaratif Maroc »)

## Contexte
Demande PO (13/07/2026) : affiner un peu le design — rester **simple**, mais choisir un thème **élégant**, sans rien casser sur les écrans denses existants (grilles 0-espace-perdu : Rapprochement, Affectations, Factures, etc. — cf. `TODO.md` §UX grilles). Décision PO (clarification architecte) : direction retenue = **« Sobre + touche couleur signature »** — la base sobre actuelle (fond gris clair, cartes blanches, Inter) est conservée, mais la couleur d'accent générique « bleu Material » (`#1976d2`, identique à de nombreux produits Axelor-like) est remplacée par une teinte propre à **Déclaratif Maroc**, complétée d'un petit élément de marque dans la sidebar (monogramme, pas de logo image).

Périmètre = **variables CSS + un composant** (sidebar header). Aucune refonte de layout, aucune grille touchée.

## Périmètre STRICT
- **Inclus** :
  1. `declaration-tva-web/src/index.css` (`:root`, lignes 3-37) : remplacer les valeurs de `--accent-primary`, `--accent-secondary`, `--accent-hover`, `--sidebar-active-bg`, `--sidebar-active-text` par la nouvelle teinte signature (indigo profond, cf. tableau ci-dessous). Toutes les autres variables (`--bg-*`, `--text-*`, `--border-color`, `--danger`, `--success`, `--warning`, ombres, radius) sont **inchangées**.
  2. `declaration-tva-web/src/Auth.tsx` : les styles inline qui référencent `var(--accent-primary)` (bouton de connexion, ligne 126) héritent automatiquement de la nouvelle teinte — aucune modification de code nécessaire au-delà du point 1, à vérifier visuellement.
  3. `declaration-tva-web/src/App.tsx` (sidebar, lignes 126-143) : ajouter un monogramme simple (« DM », texte ou petit cercle coloré avec initiales — **pas d'image/logo externe**) à côté de l'icône `LayoutDashboard` existante en en-tête de sidebar, en complément (pas en remplacement) du libellé de module actif (`TVA`, hors périmètre TASK-082).
- **Exclus / hors périmètre** :
  - Aucun changement de layout, de densité, de grille (`DomainGrid`, `RapprochementInterrogation`, `AffectationsDrill`, `FactureInterrogation`, etc.) — **zéro écran dense retouché**, seule la couleur d'accent et les ombres/radius déjà existants s'y répercutent via les variables CSS (pas de nouveau code dans ces fichiers).
  - Pas de mode sombre (option écartée, cf. clarification PO — direction « clair/sombre » non retenue).
  - Pas de logo image, favicon, ou charte graphique externe — monogramme texte/CSS uniquement, cohérent avec la contrainte « rester simple ».
  - `App.css` (styles des grilles denses) non touché sauf s'il définit lui-même des couleurs en dur au lieu des variables (cf. étape 3 ci-dessous — à vérifier, corriger uniquement si trouvé).

## Cause racine
La teinte d'accent actuelle (`#1976d2`, bleu Material par défaut) est un choix par défaut hérité du thème de référence GRC (cf. mémoire `grc-vs-declaration-modules-distincts` : GRC = simple référence de style, pas à copier tel quel) — jamais reconsidéré comme identité propre au produit.

## Objectif
```
Entrée : variables CSS globales (index.css) + en-tête sidebar (App.tsx)
Traitement : substitution de teinte d'accent + ajout monogramme, aucune structure/layout modifiée
Sortie : accent visuel cohérent et propre à Déclaratif Maroc sur boutons/liens/actifs/focus,
         sidebar identifiable par un monogramme, AUCUNE régression sur les grilles denses (mêmes
         positions, mêmes tailles, mêmes colonnes, seule la couleur d'accent change)
```

## Palette proposée (à valider visuellement en VERIFY, ajustable ±)
| Variable | Valeur actuelle | Nouvelle valeur proposée | Usage |
|---|---|---|---|
| `--accent-primary` | `#1976d2` | `#2b4c7e` (indigo profond) | boutons primaires, liens actifs |
| `--accent-secondary` | `#2196f3` | `#3d6099` | variantes secondaires |
| `--accent-hover` | `#1565c0` | `#1f3a63` | hover boutons |
| `--sidebar-active-bg` | `#e3f2fd` | `#e8ecf3` | fond entrée menu active |
| `--sidebar-active-text` | `#1976d2` | `#2b4c7e` | texte entrée menu active |

> La teinte exacte reste un point de goût — si le PO préfère une nuance différente d'indigo (ou une autre famille, ex. vert émeraude `#1f6f5c` vu en clarification), l'ajustement se fait dans ce même tableau sans impact sur le reste de la task.

## Étapes
1. `index.css` : remplacer les 5 variables du tableau ci-dessus dans `:root`.
2. `grep -rn "#1976d2\|#2196f3\|#1565c0\|#e3f2fd" declaration-tva-web/src/` : identifier toute couleur en **dur** (hors `var(--...)`) qui contournerait les variables (ex. `Auth.tsx:115` a déjà `backgroundColor: 'white'` en dur pour le `<select>`, sans rapport avec l'accent — à laisser). Corriger uniquement les couleurs dupliquant l'ancien accent en dur.
3. `App.tsx` : ajouter le monogramme (ex. `<span style={{fontWeight:700, fontSize:'0.7rem', color:'var(--accent-primary)', border:'1px solid var(--accent-primary)', borderRadius:'4px', padding:'1px 4px'}}>DM</span>`) à côté de l'icône existante ligne 132, sans changer la structure flex/layout de l'en-tête.
4. Vérification visuelle sur un échantillon d'écrans denses (Rapprochement, Affectations, Factures, liste déclarations) : mêmes colonnes/largeurs/hauteurs, seule la teinte d'accent (boutons, lignes actives, focus) change.
5. Build front (`npm run build` / `tsc` / oxlint) vert.

## Livrables
- `index.css` avec la nouvelle palette d'accent.
- `App.tsx` avec monogramme sidebar.
- `VERIFY/TASK-083_verify.md` : captures avant/après sur (a) écran de connexion, (b) sidebar avec monogramme, (c) au moins un écran dense (ex. Rapprochement) montrant l'absence de régression de layout ; sortie build front sans erreur.

## Critères de validation
- Nouvelle teinte d'accent visible sur boutons primaires, éléments actifs, focus — cohérente sur tout le produit (une seule source : les variables CSS).
- Monogramme sidebar présent, lisible, replié proprement en mode sidebar réduite (`collapsed`) sans casser l'icône existante.
- **Aucune régression de layout/densité** sur les écrans de grille (mêmes colonnes, largeurs, pagination, filtres — capture avant/après en VERIFY).
- Aucune couleur en dur dupliquant l'ancien accent oubliée (`grep` étape 2 = 0 résidu pertinent).
- Build front 0 erreur.

## Risques / dépendances
- Risque faible-à-moyen : variable CSS globale → tout composant utilisant `var(--accent-primary)` est impacté d'un coup. Le risque réel n'est pas la casse (une variable CSS ne peut pas rompre un layout), mais un **contraste insuffisant** sur certains badges/états (ex. `--sidebar-active-bg`/`--sidebar-active-text`) — à vérifier visuellement en VERIFY plutôt qu'en supposant.
- **Dépendance avec TASK-082** (renommage titre auth) : les deux touchent `Auth.tsx`, zones disjointes (texte du titre vs. héritage de couleur du bouton) — pas de conflit d'édition, mais séquencer les deux VERIFY ensemble évite une double capture d'écran de connexion à quelques heures d'écart.
- Hors périmètre confirmé : mode sombre (écarté par le PO), refonte de layout des grilles (formellement exclue ci-dessus).
