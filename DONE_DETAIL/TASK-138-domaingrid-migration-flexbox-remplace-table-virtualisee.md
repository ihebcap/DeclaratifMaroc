# TASK-138 — `DomainGrid` : remplacer le rendu `<table>` + `<tr position:absolute>` par le pattern flexbox de `AffectationsDrill.tsx`

## Origine

TASK-113 (`DomainGrid.tsx`, colonnes désalignées) a connu **deux tentatives de correction rejetées
en test réel** malgré un build OK et une revue statique favorable à chaque fois :

- **v1** (17-19/07/2026) : `tableLayout:'fixed'` + `<colgroup>` seul → persistant (confirmé PO après
  redéploiement complet + hard refresh, capture 19/07/2026).
- **v2** (19/07/2026) : ajout `width: colMaxWidth(col)` explicite sur `<th>`/`<td>` en plus du
  `<colgroup>` → **persistant à nouveau** (nouvelle capture PO 19/07/2026, même symptôme : 7
  en-têtes affichés pour 9 valeurs de cellules par ligne — `Statut`/`Motif Écartement` apparaissent
  dans le corps sans en-tête correspondant visible).

Deux itérations de correction **dans le même paradigme** (`<table>` + virtualisation par
`<tr position:absolute>`) ont échoué en conditions réelles alors que l'analyse statique + le build
les validaient à chaque fois. Décision PO 19/07/2026 : ne plus patcher ce paradigme, reconstruire
l'affichage des lignes sur un mécanisme qui n'a jamais présenté ce défaut.

## Constat clé

`AffectationsDrill.tsx:614-673` implémente une grille avec en-tête collant + lignes, **sans
`<table>`** : des `<div>` flexbox, largeur de colonne portée par `colStyle(col)` — une fonction
partagée entre l'en-tête (l.618-634) et chaque ligne de données (l.669-673), **sans virtualisation**
(toutes les lignes filtrées sont rendues, pas de fenêtre glissante). Ce composant n'a jamais fait
l'objet d'un signalement de désalignement, contrairement à `DomainGrid.tsx`.

L'hypothèse retenue après deux échecs : la virtualisation par `<tr position:absolute>` place les
lignes hors du flux normal du tableau d'une façon qui casse la synchronisation de largeur avec
l'en-tête à un niveau que `<colgroup>` et les largeurs explicites par cellule ne suffisent pas à
corriger de façon fiable en pratique (mécanisme exact non reproduit en environnement de test réel
par l'architecte — rôle sans accès UI, cf. NOTES). Le pattern `AffectationsDrill.tsx`, qui n'utilise
ni `<table>` ni positionnement absolu de ligne, écarte structurellement cette classe de défaut.

## Objectif

```
Entrée  : DomainGrid.tsx actuel — <table> + <colgroup> + <tr position:absolute> virtualisés
          (@tanstack/react-virtual), colonnes désalignées de façon persistante et non résolue
          après deux itérations de correction en place
Traitement : remplacer le rendu des lignes (et de l'en-tête) par des <div> flexbox avec largeurs de
          colonne portées par une fonction unique partagée en-tête/lignes (pattern colStyle de
          AffectationsDrill.tsx) ; conserver ou adapter la virtualisation (@tanstack/react-virtual
          fonctionne aussi bien avec des <div> position:absolute qu'avec des <tr>)
Sortie  : alignement strict en-tête/lignes garanti par construction (même mécanisme que
          AffectationsDrill.tsx, jamais mis en défaut), aucune régression fonctionnelle
```

## Périmètre STRICT

- **Inclus** :
  1. `DomainGrid.tsx:262-340` : remplacer `<table>`/`<colgroup>`/`<thead>`/`<tbody>`/`<tr>`/`<td>`
     par une structure `<div>` flexbox (rôles ARIA `role="table"`/`role="row"`/`role="cell"` à
     poser pour conserver la sémantique accessible perdue avec l'abandon du vrai `<table>` —
     `AffectationsDrill.tsx` ne le fait pas actuellement, à évaluer si c'est un manque à combler
     ici plutôt qu'à reproduire tel quel).
  2. Extraire ou dupliquer un helper de largeur de colonne équivalent à `colStyle` (source unique,
     utilisée par l'en-tête et par chaque ligne) — `colMaxWidth` existant peut servir de base.
  3. Conserver la virtualisation `@tanstack/react-virtual` : les items virtualisés peuvent être des
     `<div>` en `position:absolute` sans les problèmes du `<table>`, à condition que l'en-tête et
     les lignes partagent la même source de largeur (pas de dépendance à un calcul de layout de
     tableau).
  4. Colonne case à cocher (mode non-readonly) : migrer vers le même pattern `<div>` flexbox.
- **Exclu** :
  - Toute modification de `AffectationsDrill.tsx` (référence, pas à toucher).
  - Tri (`handleSort`), `ExcelFilter`, sélection, sélecteur de colonnes (TASK-068), troncature +
    `title` (TASK-110) : logique à **préserver à l'identique**, seule la structure DOM change.
  - Pagination / fetch / filtres serveur (`fetchPage`, `filters`, etc.) : hors sujet, non touchés.

## Étapes

1. Écrire la nouvelle structure `<div>` (en-tête + lignes virtualisées), en réutilisant
   `colMaxWidth(col)` comme source unique de largeur pour l'en-tête ET pour les lignes.
2. Rebrancher `handleSort`, `ExcelFilter`, `selectedIds`/`handleToggleAll`/`handleToggleOne`,
   `renderCell`, la troncature + `title` sur la nouvelle structure, sans changer leur logique.
3. Vérifier les deux modes (① non-readonly avec case à cocher, ② drills readonly) et le sélecteur
   de colonnes (TASK-068) avec peu/beaucoup de colonnes visibles.
4. Build : `npx tsc --noEmit -p .` + `npm run build`, 0 erreur.
5. **Test visuel réel obligatoire avant toute VERIFY** : écran ② drill `TVA1-2026-01` (cas exact du
   signalement PO, factures `FC2501717`/`FC2501667`), et écran ① non-readonly. Capture avant/après.
   Vu l'historique de cette task (deux échecs malgré build + revue statique favorables), **aucune
   VERIFY sans capture ne sera acceptée** — la validation statique seule a déjà prouvé son
   insuffisance sur ce composant précis.

## Livrables

- `DomainGrid.tsx` : nouvelle structure `<div>` flexbox, ancien code `<table>` retiré (pas de
  double implémentation conditionnelle).
- Capture avant/après sur le cas réel `TVA1-2026-01` (obligatoire, non négociable — cf. Étape 5).
- `VERIFY/TASK-138_verify.md` : build OK, capture réelle jointe, checklist des points de
  non-régression (tri, filtres, sélection, sélecteur de colonnes, troncature, défilement
  horizontal) **cochée par un test réel**, pas laissée en attente du PO.

## Critères de validation

- Alignement vertical strict en-tête/lignes sur le cas réel, toutes largeurs de colonnes.
- Aucune régression sur tri, `ExcelFilter`, sélection + « tout sélectionner », sélecteur de
  colonnes persistant (TASK-068), troncature + `title` (TASK-110).
- Défilement horizontal conservé et borné.
- Preuve visuelle réelle jointe à la VERIFY — condition de recevabilité, pas une simple
  recommandation cette fois.

## Risques / dépendances

- **Supersede TASK-113** : ne pas travailler les deux en parallèle. TASK-113 reste comme historique
  du diagnostic (deux échecs documentés) mais son périmètre (patch in-place) est abandonné au
  profit de celui-ci.
- `DomainGrid.tsx` est partagé par tous les écrans listés dans TASK-113 (① Affectations,
  ② drills TASK-088/092/107) — la migration doit couvrir tous ces usages, pas seulement le drill
  source du signalement.
- Accessibilité : abandonner `<table>` sémantique retire la navigation clavier/lecteur d'écran
  native des tableaux HTML — à compenser par des rôles ARIA (`role="table"` etc.), point non traité
  par `AffectationsDrill.tsx` aujourd'hui ; à trancher explicitement (combler ici ou accepter la
  régression, mais pas silencieusement).
- Risque de résurgence du même défaut si le nouveau code réintroduit, même involontairement, un
  calcul de largeur non partagé entre en-tête et lignes (ex. deux fonctions dupliquées au lieu
  d'une source unique) — c'est précisément la cause des deux échecs précédents.

## NOTES

Analyse de code uniquement (rôle architecte, aucun accès UI). Décision de changement de paradigme
prise après deux itérations de correction (TASK-113 v1/v2) validées par build + revue statique mais
infirmées à chaque fois par un test réel PO — l'analyse statique seule est démontrée insuffisante
pour ce composant précis, d'où l'exigence de preuve visuelle obligatoire (Étape 5) avant toute
clôture.
