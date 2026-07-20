# TASK-120 — Nouvelle teinte signature « noir + vert » (variables CSS cœur + sidebar)

## Contexte
Demande PO (18/07/2026) : re-thémer l'application pour se rapprocher de l'identité visuelle de la
société cliente (référence montrée : bandeau noir, accent vert signature sur un logo/icône, fond
blanc, contenu dense). Clarification architecte (même session) :
- Périmètre = **un seul produit, GRF** (pas de fusion avec GRC_WEB/B-Hub, cf. mémoire
  `grc-vs-declaration-modules-distincts` et `TODO.md` §186) — la référence sert d'inspiration de
  teinte, pas de charte pixel-perfect à copier.
- **Recolorer uniquement** : la structure actuelle du shell (sidebar gauche seule, aucun bandeau
  horizontal) est **conservée telle quelle** — la référence a un bandeau supérieur noir que GRF n'a
  pas ; l'ajouter serait un changement structurel de `App.tsx`, explicitement écarté par le PO pour
  cette task (risque de layout jugé disproportionné par rapport à la demande = teinte).
- Cette task couvre le **cœur** de la palette (variables globales + sidebar), à l'identique du
  périmètre de TASK-083 qu'elle remplace/étend. La généralisation aux badges de statut codés en dur
  dans les composants métier est **hors périmètre ici** → extraite en
  [TASK-121](TASK-121-variabilisation-badges-statut-palette-signature.md) (dépendante de celle-ci).

## Périmètre STRICT
- **Inclus** :
  1. `declaration-tva-web/src/index.css` (`:root`, lignes 3-37) : remplacer les variables
     `--accent-primary`, `--accent-secondary`, `--accent-hover`, `--sidebar-bg`, `--sidebar-text`,
     `--sidebar-active-bg`, `--sidebar-active-text` par la nouvelle palette « noir + vert » (cf.
     tableau ci-dessous). `--bg-*`, `--text-*`, `--border-color`, `--danger`, `--success`,
     `--warning`, ombres/radius : **inchangés** (risque de collision sémantique avec `--success`,
     déjà vert — cf. Risques).
  2. `declaration-tva-web/src/Auth.tsx` : héritage automatique via `var(--accent-primary)` (bouton
     de connexion) — vérification visuelle uniquement, pas de nouveau code attendu.
  3. `declaration-tva-web/src/App.tsx` (sidebar) : le monogramme texte « DM » (TASK-083) est
     **remplacé par l'icône DM** livrée ci-dessous (SVG inline ou `<img>`), même emplacement, même
     taille approximative — pas de changement de structure/layout de l'en-tête.
  4. **Icône application « DM »** (décision PO 18/07/2026, révise l'exclusion « pas de logo externe »
     posée en TASK-083) : utilisée à **deux emplacements** — (a) en-tête sidebar (remplace le
     monogramme texte, point 3), (b) favicon d'onglet navigateur. Fichiers **fournis en livrable**
     par l'architecte (générés, pas à concevoir par le développeur) :
     `TASKS/assets/task-120-icon-dm/icon-dm.svg` (vecteur, source de référence — utilisable
     directement comme favicon `<link rel="icon" type="image/svg+xml">` et en sidebar),
     `icon-dm-512.png` / `icon-dm-192.png` (manifest PWA / apple-touch-icon), `icon-dm-32.png`
     (fallback favicon navigateurs sans support SVG). Design : monogramme pixel-art « DM », fond
     `#1a1a1a` (= nouveau `--sidebar-bg`), vert `#3ddc84` (= nouveau `--sidebar-active-text`) — donc
     déjà aligné sur la palette du tableau ci-dessous, aucun nouveau choix de teinte à faire.
     Intégration attendue : copier les fichiers dans `declaration-tva-web/public/`, ajouter les
     balises `<link rel="icon" .../>` (+ `apple-touch-icon`) dans `index.html`, importer le SVG dans
     `App.tsx` à la place du `<span>DM</span>` actuel.
- **Exclus / hors périmètre** :
  - Tout ajout de bandeau/header horizontal (décision PO explicite ci-dessus).
  - Toute couleur en dur hors `index.css` (badges de statut ok/attention/bloquant dans les
    composants métier) → TASK-121.
  - Aucun changement de layout, de densité, de grille (`DomainGrid`, `RapprochementInterrogation`,
    `AffectationsDrill`, `FactureInterrogation`, etc.) — zéro écran dense retouché.
  - Pas de mode sombre (option déjà écartée en TASK-083, confirmée toujours valable).
  - Toute nouvelle variante graphique de l'icône (couleur, forme) non fournie dans
    `TASKS/assets/task-120-icon-dm/` — si le PO veut un ajustement visuel, retour à l'architecte pour
    régénérer les fichiers, pas de recréation ad hoc par le développeur.

## Cause racine
La palette actuelle (indigo profond, `#2b4c7e`, posée en TASK-083) est une identité propre au
produit mais ne reflète pas la teinte de la société cliente vue en référence (noir/vert). Aucune
règle métier ne l'impose — c'est un choix de branding à faire évoluer, sans lien avec un bug.

## Objectif
```
Entrée : variables CSS globales (index.css) + sidebar (App.tsx, monogramme existant)
Traitement : substitution de palette (accent + fond/texte sidebar), aucune structure/layout modifiée
Sortie : identité visuelle noir/vert cohérente sur boutons/liens/actifs/focus/sidebar,
         AUCUNE régression sur les grilles denses (mêmes positions, tailles, colonnes)
```

## Palette proposée (à valider visuellement en VERIFY — point de goût, ajustable sans impact sur le reste de la task)
| Variable | Valeur actuelle (TASK-083) | Nouvelle valeur proposée | Usage |
|---|---|---|---|
| `--accent-primary` | `#2b4c7e` (indigo) | `#178a4c` (vert signature, à distinguer visuellement de `--success` `#2e7d32` — cf. Risques) | boutons primaires, liens actifs, monogramme |
| `--accent-secondary` | `#3d6099` | `#2fa868` | variantes secondaires |
| `--accent-hover` | `#1f3a63` | `#0f6b3a` | hover boutons |
| `--sidebar-bg` | `#ffffff` | `#1a1a1a` (noir/anthracite, esprit bandeau de référence) | fond sidebar |
| `--sidebar-text` | `#4a4a4a` | `#e0e0e0` | texte sidebar (contraste sur fond sombre) |
| `--sidebar-active-bg` | `#e8ecf3` | `#0f3d24` (vert très foncé) | fond entrée menu active |
| `--sidebar-active-text` | `#2b4c7e` | `#3ddc84` (vert clair, lisible sur fond sombre) | texte/icône entrée menu active |

> Si le PO préfère une nuance de vert différente (plus proche du logo exact de la référence) ou un
> gris anthracite au lieu du noir pur pour la sidebar, l'ajustement se fait dans ce tableau sans
> impact sur le reste de la task.

## Étapes
1. `index.css` : remplacer les 7 variables du tableau ci-dessus dans `:root`.
2. `grep -rn "#2b4c7e\|#3d6099\|#1f3a63\|#e8ecf3" declaration-tva-web/src/` : confirmer 0 résidu de
   l'ancienne teinte indigo en dur hors variables.
3. Copier `TASKS/assets/task-120-icon-dm/*` dans `declaration-tva-web/public/` ; ajouter les balises
   `<link rel="icon">` / `apple-touch-icon` dans `index.html` ; remplacer le `<span>DM</span>` de
   `App.tsx` (sidebar) par l'icône SVG fournie.
4. Vérification visuelle : contraste texte/fond sidebar sombre (WCAG AA minimum, texte `#e0e0e0` sur
   fond `#1a1a1a` = contraste élevé, à confirmer avec l'outil de contraste habituel) ; onglet
   navigateur affiche bien le favicon DM.
5. Vérification visuelle sur écrans denses (Rapprochement, Affectations, Factures, liste
   déclarations) : mêmes colonnes/largeurs/hauteurs, seule la teinte change.
6. Build front (`npm run build` / `tsc` / oxlint) vert.

## Livrables
- `index.css` avec la nouvelle palette (7 variables).
- Icône DM intégrée (favicon `index.html` + sidebar `App.tsx`), fichiers source dans
  `TASKS/assets/task-120-icon-dm/` (svg + png 32/192/512, déjà fournis par l'architecte).
- `VERIFY/TASK-120_verify.md` : captures avant/après (a) écran de connexion, (b) sidebar (fond
  sombre + icône DM + entrée active), (c) onglet navigateur (favicon visible), (d) au moins un écran
  dense montrant l'absence de régression de layout ; contrôle de contraste sidebar ; sortie build
  front sans erreur.

## Critères de validation
- Nouvelle palette noir/vert visible et cohérente (sidebar, boutons, actifs, focus) — une seule
  source (variables CSS).
- Icône DM visible en sidebar ET en favicon d'onglet, mêmes fichiers source (pas de variante
  recréée ad hoc).
- Contraste sidebar texte/fond conforme (lisibilité confirmée, pas seulement supposée).
- Aucune régression de layout/densité sur les grilles (capture avant/après en VERIFY).
- `--success` (statut "ok", vert `#2e7d32`) reste visuellement distinguable de `--accent-primary`
  (vert signature) — pas de confusion "élément actif" / "statut validé" sur un même écran.
- Build front 0 erreur.

## Risques / dépendances
- **Risque principal (nouveau par rapport à TASK-083)** : le vert signature (`--accent-primary`) et
  le vert de statut existant (`--success`) créent un risque d'ambiguïté visuelle inédit — un badge
  "OK" vert à côté d'un bouton/lien actif vert signature pourrait se lire comme un seul et même
  signal. Aucun écran n'a été audité à ce stade pour vérifier une cohabitation des deux sur une même
  vue ; à faire en VERIFY, avec ajustement de nuance si besoin (ex. vert plus froid/plus chaud pour
  bien séparer les deux usages).
- Risque contraste : passage d'une sidebar claire à une sidebar sombre est un changement plus
  marqué que TASK-083 (simple substitution d'accent) — le contraste doit être vérifié, pas supposé.
- **Dépendance** : [TASK-121](TASK-121-variabilisation-badges-statut-palette-signature.md) reprend
  la palette validée ici — ne pas lancer TASK-121 avant approbation VERIFY de TASK-120 (risque de
  devoir refaire le travail si la teinte change en cours de VERIFY).
- Hors périmètre confirmé : bandeau horizontal noir (changement structurel écarté par le PO), mode
  sombre généralisé, refonte de layout des grilles.
- Icône DM déjà générée (architecte) et déposée dans `TASKS/assets/task-120-icon-dm/` — le
  développeur ne doit **pas** régénérer/redessiner ces fichiers ; si le rendu ne convient pas en
  VERIFY, retour à l'architecte pour ajuster le design plutôt qu'une modification ad hoc en cours
  d'implémentation.
