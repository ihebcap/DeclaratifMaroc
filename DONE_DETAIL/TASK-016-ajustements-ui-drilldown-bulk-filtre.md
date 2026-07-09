# TASK-016 — Ajustements UI : drill-down anomalies + action de masse sur filtre

## Contexte
Le front workflow (TASK-013, ✅ livré) implémente déjà : stepper par domaine, grille serveur
(pagination/tri/filtre `ExcelFilter` + virtualisation), actions de masse, statut `Écartée`+motif, et
l'écran **Checkup** complet (équilibre, réconciliation, récaps source/taux/activité, anomalies
bloquant/avertissement, clôture gated). **Ce n'est pas une refonte** — deux manques ciblés subsistent
par rapport au principe directeur « erreurs actionnables » (TASK-013).

## Périmètre STRICT
- **Uniquement** : deux ajustements du front `declaration-tva-web` + un point de sélection back lié au
  récap activité (signalé, à confirmer).
- **Exclu** : toute refonte visuelle, tout nouvel écran.

## Objectif

### 1. Drill-down anomalie → grille filtrée (front)
Dans [CheckupPanel.tsx](declaration-tva-web/src/CheckupPanel.tsx), les anomalies sont statiques.
- Rendre chaque anomalie **cliquable** → navigue vers l'onglet du **domaine concerné** avec la **grille
  pré-filtrée** sur les lignes fautives (ex. `statutLigne=Écartée` + motif, ou `tiers=X`, selon le type
  d'anomalie).
- L'objet anomalie doit porter de quoi cibler : `domaine` + critère de filtre (clé/valeur). Si l'API ne
  le fournit pas encore, l'ajouter au payload checkup (côté mock d'abord, cf. `mockServer.ts`).
- Boucle attendue : Checkup → clic anomalie → grille filtrée → correction/exclusion → retour Checkup
  recalculé.

### 2. Action de masse sur tout le résultat filtré (front + contrat)
Dans [DomainGrid.tsx](declaration-tva-web/src/DomainGrid.tsx), `handleToggleAll` ne coche que la page
courante (`data`), pas l'ensemble filtré.
- Ajouter un mode **« tout sélectionner (N résultats) »** distinct du « toute la page ».
- L'action de masse doit alors s'appliquer au **filtre courant**, pas à une liste d'ids : appeler
  `POST /lignes:bulk` avec **le critère de filtre** (`{domaine, filtres, statutLigne}`) au lieu de
  `ligneIds` — pour ne pas transférer 625+ ids. ⇒ **avenant contrat** `:bulk` (accepter *soit*
  `ligneIds` *soit* `filtre`).
- Feedback clair : « 625 lignes marquées Intégrée ».

## Point connexe à confirmer (hors UI)
- **Récap par activité vide** : la sélection renvoie `TiersActivite = NULL` en dur
  ([SelectionnerAffectationsService.cs:116](Declaration.Selection/SelectionnerAffectationsService.cs#L116)).
  ⇒ le code activité n'est pas alimenté → récap activité inexploitable. À traiter **côté sélection**
  (d'où vient le code activité tiers ?) — décision PO : brancher ou retirer le récap activité.

## Contraintes techniques
- Réutiliser l'existant (`DomainGrid`, `CheckupPanel`, `ExcelFilter`, navigation stepper) — pas de
  nouveau composant lourd.
- Développable sur mock (`mockServer.ts`) ; l'avenant `:bulk` par filtre doit être répercuté au contrat
  TASK-012.
- Respecter le principe « simple avant beau » : pas de sur-UI, juste rendre les erreurs actionnables.

## Étapes
1. Enrichir le payload checkup (mock) : chaque anomalie porte `domaine` + critère de filtre.
2. Rendre les anomalies cliquables → navigation stepper + application du filtre à `DomainGrid`.
3. Ajouter « tout sélectionner (N) » + bascule mode sélection dans `DomainGrid`.
4. `:bulk` par filtre (mock + contrat TASK-012) + feedback.
5. e2e Playwright : anomalie → grille filtrée → action de masse → Checkup recalculé.

## Livrables
- Front ajusté (drill-down + bulk-sur-filtre).
- Contrat `:bulk` mis à jour (ids **ou** filtre) dans TASK-012.
- `VERIFY/TASK-016_verify.md` : captures du drill-down et de l'action sur filtre complet.

## Critères de validation
- Cliquer une anomalie ouvre la grille du bon domaine, filtrée sur les lignes concernées.
- « Tout sélectionner (N) » + action de masse s'applique aux N lignes du filtre (pas seulement la page).
- Le Checkup se recalcule après action.
- Décision prise sur le récap activité (alimenté ou retiré).
- e2e Playwright vert.

## Risques / dépendances
- **Contrat `:bulk`** : l'ajout du mode « par filtre » touche TASK-012 (à répercuter).
- **Récap activité** : dépend de la source du code activité (sélection TASK-008/015) — décision PO.
- Ne pas dériver vers une refonte visuelle : périmètre = 2 manques + 1 décision.
