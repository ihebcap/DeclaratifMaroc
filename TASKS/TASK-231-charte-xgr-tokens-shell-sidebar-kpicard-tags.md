# TASK-231 — Charte graphique xGR : tokens, shell (topbar + sidebar), KpiCard, tags, dark mode

Status: 🆕 à faire
Priority: HIGH (bloque le front des TASK-225/228/229/230)
Risk: MEDIUM (transversal, régression visuelle)
Module: declaration-tva-web (`index.css`, `App.tsx`, `App.css`, `src/components/commun/`)

> **Origine :** PO 16/10/2026 — « changement de charte graphique exactement comme D:\_vibe\xGR\xGR : sidebar, couleurs, tout ». Référence normative : `D:\_vibe\xGR\xGR\Necessaire\DESIGN_REFERENCE.md` (+ `Tresorerie.Vue/tailwind.config.js`, `src/assets/layout/sidebar/themes/_menu_dark.scss`, `KpiCard.vue`, `useKpiColor.ts`). **À relire en entier avant tout code.**

## Règle de conformité (PO 16/10/2026 : « on est la même boîte, on suit à la lettre la charte graphique APBS »)

- La charte APBS telle qu'appliquée dans xGR est **normative, pas indicative**. Sources, par ordre de priorité : `Necessaire/DESIGN_REFERENCE.md`, `Necessaire/ADMIN_PATTERNS.md`, `Necessaire/ACTION_PATTERNS.md`, `Necessaire/FILTER_PATTERNS.md` (xGR), puis le code de référence (`KpiCard.vue`, `_menu_dark.scss`, `tailwind.config.js`, `XGrid.vue`).
- **Aucune interprétation** : couleurs, dégradés, rayons, ombres, tailles de police, espacements, libellés de boutons, icônes, ordre des zones d'écran, comportement des filtres et des actions sont repris à l'identique. Toute différence (contrainte technique React, cas non couvert par xGR) est **listée dans le VERIFY, justifiée et validée par le PO** ; jamais décidée par le worker.
- **Ressources réutilisées telles quelles**, pas redessinées : logos et badge noir/vert, favicon, `manifest.webmanifest`, images (`Tresorerie.Vue/public`, `src/assets/images`), polices (Roboto, `src/assets/layout/fonts`), échelle de couleurs.
- Cas non couvert par xGR → **s'arrêter et poser la question au PO** (règle « ne jamais improviser »), pas d'invention de style.
- **xGR est la seule référence** (confirmé par le PO le 16/10/2026 : il n'existe pas d'autre charte APBS officielle). En cas de doute, la réponse est « comme dans xGR ».
- **Revue de conformité** : checklist point par point (couleurs, sidebar, topbar, cards, tags, tableaux, boutons, formulaires, tiroirs, filtres, actions, polices, logos), chaque ligne comparée à un écran xGR de référence avec captures côte à côte. Un écart non justifié = REJECT.

## Décisions de conception

1. **Port, pas réécriture** : GRF reste React (arbitrage PO 16/10/2026). On reprend la charte de xGR (Vue/PrimeVue/Tailwind) en **variables CSS** dans `index.css` ; aucun PrimeVue, aucune dépendance Vue.
2. **Principe d'inversion APBS** : fond noir + texte vert `#00DC00` pour topbar, sidebar, bandeau de card, tfoot, badge compteur ; item de menu actif = fond `#00DC00` + texte `#000000`.
3. **Tokens** (source unique = `index.css`) : vert `#00DC00` (+ échelle 50–950 de xGR), vert valeur `#00A000`/`#00B000`, noir `#000000`/`#1A1A1A`, warning `#FF9500`, danger `#FF3B30`, info `#007AFF`, surfaces dark (`#0A0A0A`, `#1A1A1A`, `#252525`, bordure `#333333`) et light (`#F5F5F5`, `#FFFFFF`, bordure `#E0E0E0`). Les variables actuelles (`--accent-primary`, `--bg-secondary`, `--border-color`, `--radius-md`, `--shadow-lg`…) **gardent leur nom** et changent de valeur : les écrans existants suivent sans retouche.
4. **Sidebar** « B-Hub noir » : `--menu-bg #000`, sections en majuscules gris `#555` (0.857rem, 700), items `rgba(255,255,255,.85)`, survol `rgba(255,255,255,.06)`, séparateurs topbar en dégradé vert (cf. §1 DESIGN_REFERENCE). Structure existante (groupes pliables, mode replié, groupe DDP, verrous) **conservée**.
5. **Composants communs React** (équivalents des composants xGR) : `KpiCard` (props `icon`, `title`, `value`, `sub`, `colorMode`, `numericValue`, seuils, `clickable`, `active`, `suffix`), `useKpiColor` (modes engagement/balance/positive/alert/info/neutral), tags `tag-xs`/`tag-sm` (vert, orange, rouge, teal, gris, mode). Interdit de recréer ces classes dans un écran.
6. **Dark mode** : bascule explicite (classe sur `:root`, comme `app-dark` côté xGR) + respect de `prefers-color-scheme` par défaut ; persistance `localStorage` protégée par try/catch.
7. **Icônes** : `lucide-react` conservé (pas de PrimeIcons) ; mapping documenté.
8. Mettre à jour `DOCS/UI_STANDARDS.md` (nouvelle section charte + KpiCard + tags) et `CLAUDE.md` du projet. **L'ancien thème `ag-theme-alpine` n'est plus la référence.**

## Périmètre STRICT

- **Inclus** : tokens + dark mode, shell/topbar/sidebar restylés, `KpiCard` + `useKpiColor`, tags, page « Charte » de démonstration (route dev) montrant tous les composants en clair/sombre, mise à jour de la documentation, correction des couleurs en dur trouvées dans les écrans quand elles empêchent le thème (liste tracée dans le VERIFY).
- **Exclu** : grille (TASK-232), migration écran par écran (TASK-233), toute logique métier, nouvelles bibliothèques UI lourdes.

## Livrables

- `index.css` (tokens), `App.tsx`/`App.css` (shell), `src/components/commun/KpiCard.tsx`, `useKpiColor.ts`, `tags.css`, page de démonstration, `DOCS/UI_STANDARDS.md` mis à jour.

## Critères de validation

- Tous les écrans existants s'affichent sans régression fonctionnelle en clair et en sombre (parcours de chaque entrée de la sidebar, captures avant/après jointes).
- Sidebar : item actif vert/noir, sections grises, replié/déplié, groupes pliables inchangés.
- Contraste lisible (texte vert sur noir ; texte noir sur vert) ; focus clavier visible.
- Aucune couleur hexadécimale nouvelle hors `index.css` dans les fichiers modifiés.
- `npm run lint` + `npm run build` 0 erreur ; preuve par critère datée dans le VERIFY.

## Files

- `declaration-tva-web/src/index.css`, `App.tsx`, `App.css`, `src/components/commun/`.
- `D:\_vibe\xGR\xGR\Necessaire\DESIGN_REFERENCE.md`, `ACTION_PATTERNS.md`, `FILTER_PATTERNS.md`, `Tresorerie.Vue/public/` et `src/assets/images` (logos, favicon, manifest), `src/assets/layout/fonts` (Roboto), `Tresorerie.Vue/src/components/commun/KpiCard.vue`, `src/composables/useKpiColor.ts`, `src/assets/layout/sidebar/themes/_menu_dark.scss`, `tailwind.config.js`.
- `DOCS/UI_STANDARDS.md`.
