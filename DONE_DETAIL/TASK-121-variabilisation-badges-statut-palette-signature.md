# TASK-121 — Variabilisation des badges de statut vers la palette signature

## Contexte
Suite de [TASK-120](TASK-120-nouvelle-teinte-signature-noir-vert-sidebar.md) (nouvelle teinte
signature noir/vert). Décision PO (18/07/2026) : le re-thème doit couvrir **l'ensemble** de
l'application, pas seulement `index.css`/sidebar — sinon les badges de statut (ok/attention/
bloquant) resteraient visuellement figés sur l'ancienne palette (indigo era) pendant que le reste
du produit change, créant une incohérence.

Analyse code (architecte) : **197 couleurs hex codées en dur**, réparties sur 18 fichiers
`.tsx`/`.css`, jamais passées par variable CSS depuis TASK-083 (qui n'avait traité que `index.css`
+ `App.tsx`). Concentration principale : `VerifierIntegrerPanel.tsx` (46 occurrences — badges
ok/attention/bloquant en ambre/vert/rouge type Tailwind, ex. `#fffbeb`/`#fde68a`/`#92400e` pour
"attention", `#dcfce7`/`#15803d` pour "ok", `#fee2e2`/`#b91c1c` pour "bloquant"),
`DeclarationFinalePanel.tsx` (29), `FactureInterrogation.tsx` (28), `RapprochementInterrogation.tsx`
(18), `AffectationsDrill.tsx` / `DomainGrid.tsx` (12 chacun), `ReglementsSelection.tsx` /
`WorkstationPanel.tsx` (8 chacun), le reste (`DeclarationStepper.tsx`, `DeclarationList.tsx`,
`GenerationPanel.tsx`, `Auth.tsx`, `App.tsx`, `ExcelFilter.tsx`, `CreateDeclarationModal.tsx`) à 1-5
occurrences.

## Périmètre STRICT
- **Inclus** :
  1. Introduire des variables CSS **sémantiques de statut** dans `index.css` (`:root`), distinctes
     de `--danger`/`--success`/`--warning` (qui restent les couleurs "brutes") : ex.
     `--status-ok-bg`/`--status-ok-text`, `--status-warning-bg`/`--status-warning-text`,
     `--status-blocking-bg`/`--status-blocking-text` — alignées sur la palette signature validée en
     TASK-120 (pas de nouvelle teinte inventée ici, seule la structuration en variables change).
  2. Remplacer, fichier par fichier, les couleurs hex en dur des badges de statut
     (ok/attention/bloquant) par ces variables — en commençant par `VerifierIntegrerPanel.tsx`
     (lignes 824-828, 1238-1248, plus fort volume), puis les 17 autres fichiers listés ci-dessus.
  3. Conserver à l'identique le **rendu visuel actuel** des badges (mêmes teintes ambre/vert/rouge
     déjà en place) sauf si le PO demande explicitement un ajustement en VERIFY — l'objectif de
     cette task est la **variabilisation** (source unique), pas un nouveau design de badge.
- **Exclus / hors périmètre** :
  - Toute couleur hex qui n'est **pas** un badge de statut (ex. `backgroundColor: 'white'` en dur
    sur un `<select>`, bordures neutres, etc.) — à laisser telle quelle sauf si elle duplique une
    variable déjà existante (`--border-color`, `--bg-secondary`...).
  - Tout changement de layout/structure des composants touchés — édition des valeurs de couleur
    uniquement (`style={{...}}` ou classes CSS), pas de refactor de logique.
  - Toute nouvelle nuance de couleur non déjà validée en TASK-120.

## Cause racine
TASK-083 a posé les variables CSS globales mais n'a jamais audité/traité les couleurs en dur des
composants métier (hors périmètre à l'époque, cf. TASK-083 §Périmètre STRICT point "Exclus"). Ces
197 occurrences se sont accumulées au fil des tasks front successives sans jamais être centralisées.

## Objectif
```
Entrée : 18 fichiers avec couleurs de badge codées en dur (197 occurrences)
Traitement : introduction de variables CSS sémantiques de statut + substitution fichier par fichier
Sortie : rendu visuel des badges inchangé pour l'utilisateur, mais source unique (variables CSS) —
         un futur changement de palette ne touchera plus que index.css, plus aucun fichier métier
```

## Étapes
1. `index.css` : ajouter les variables `--status-ok-bg`/`--status-ok-text`,
   `--status-warning-bg`/`--status-warning-text`, `--status-blocking-bg`/`--status-blocking-text`,
   avec les valeurs hex **actuelles** des badges (aucun changement visuel à ce stade — la
   variabilisation précède l'harmonisation avec la palette signature).
2. `grep -rn "#fffbeb\|#fde68a\|#92400e\|#b45309\|#dcfce7\|#15803d\|#fee2e2\|#b91c1c" declaration-tva-web/src/`
   pour lister exhaustivement les occurrences à traiter, fichier par fichier.
3. Remplacer chaque occurrence par la variable correspondante, un fichier à la fois, en commençant
   par `VerifierIntegrerPanel.tsx` (plus gros volume, sert de gabarit pour les 17 autres).
4. Après chaque fichier : capture visuelle avant/après (aucune différence de rendu attendue à ce
   stade).
5. Une fois les 18 fichiers variabilisés et 0 résidu au grep : proposer au PO (question ouverte,
   pas décidée dans cette task) si les teintes de badge doivent elles-mêmes évoluer pour mieux
   s'accorder avec la palette signature noir/vert (TASK-120) — si oui, ajustement des valeurs de
   variables uniquement, sans nouveau chantier de fichiers.
6. Build front (`npm run build` / `tsc` / oxlint) vert.

## Livrables
- `index.css` avec les nouvelles variables sémantiques de statut.
- 18 fichiers `.tsx` variabilisés (couleurs de badge uniquement).
- `VERIFY/TASK-121_verify.md` : grep final (0 résidu hex de badge en dur), captures avant/après sur
  au moins 3 écrans représentatifs (`VerifierIntegrerPanel`, `DeclarationFinalePanel`,
  `FactureInterrogation`) confirmant un rendu visuel identique, build front 0 erreur.

## Critères de validation
- 0 couleur hex de badge de statut restant en dur (grep final propre).
- Rendu visuel des badges strictement identique avant/après (pas de régression perceptible).
- Variables sémantiques utilisées de façon cohérente dans les 18 fichiers (pas de nouvelle couleur
  en dur introduite par erreur pendant la migration).
- Build front 0 erreur.

## Risques / dépendances
- **Dépendance stricte** : ne pas démarrer avant approbation VERIFY de
  [TASK-120](TASK-120-nouvelle-teinte-signature-noir-vert-sidebar.md) (risque de re-travail si la
  palette signature change entre-temps).
- Volume élevé (18 fichiers, 197 occurrences) → risque d'erreur mécanique (mauvaise variable
  appliquée à une teinte visuellement proche mais sémantiquement différente, ex. confondre
  "attention" ambre et "bloquant" rouge foncé) — capture avant/après par fichier recommandée plutôt
  qu'en fin de task uniquement, pour isoler rapidement une erreur.
- Risque de découverte en cours de route : d'autres couleurs en dur non répertoriées dans l'analyse
  initiale (hors "badge de statut" strict, ex. graphiques/indicateurs) pourraient apparaître à la
  lecture fine de chaque fichier — à documenter dans le VERIFY si trouvées, pas à corriger
  silencieusement hors périmètre sans validation PO.
