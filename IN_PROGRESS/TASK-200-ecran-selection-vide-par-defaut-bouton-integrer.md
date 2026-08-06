# TASK-200 — Écran ① Sélection : liste vide par défaut + bouton « Intégrer » (au lieu du chargement/pré-cochage automatique)

Status: 🆕 à faire
Priority: MEDIUM
Module: declaration-tva-web (écran ① `ReglementsSelection.tsx`)

> **Origine :** demande PO (06/08/2026), session de clarification en 4 échanges (voir historique) — le
> PO trouve le fonctionnement actuel trop automatique/opaque : il veut déclencher lui-même le calcul de
> proposition, pas le recevoir déjà pré-rempli et pré-coché à l'ouverture de l'onglet.

## Contexte

L'écran ① Sélection (`declaration-tva-web/src/ReglementsSelection.tsx`) est le point d'entrée du
tunnel « règlement-first » (TASK-053/054). Aujourd'hui :

1. Au **montage du composant**, un `useEffect` (ligne ~353-357) appelle automatiquement `fetchAll` →
   `GET /api/rapprochement` avec les bornes `debut`/`fin` dérivées de la période de la déclaration
   (`periodeBounds(exercice, type, periode)`, ligne 232) — **sans action de l'utilisateur**.
2. Une fois les données chargées, un second `useEffect` (ligne ~365-402) **coche automatiquement
   toutes les lignes éligibles** si c'est une nouvelle déclaration sans sélection sauvegardée
   (« Nouvelle déclaration : coche tout par défaut », ligne 388-399).

Résultat perçu par le PO : la liste « arrive préremplie » — chargée et déjà cochée sans qu'il ait rien
demandé. Il veut inverser ce comportement : **écran vide à l'ouverture**, un **bouton « Intégrer »**
explicite qui déclenche le calcul (identique à l'appel `fetchAll` existant, filtré sur la période de
la déclaration en cours), puis une **sélection manuelle** (aucune ligne pré-cochée) avant validation.

**Ce qui existe déjà et n'est PAS concerné par cette TASK :**
- Le filtre par colonne « Date rapprochement » (`ExcelFilter`, colonne `dateRapprochement`, ligne 183)
  existe déjà et est déjà câblé sur les paramètres serveur `dateRappMin`/`dateRappMax` (ligne 313) — pas
  de nouveau filtre à créer, il est simplement masqué par le fait que la liste s'affiche déjà pleine.
- Les bornes de période (`debut`/`fin`) sont déjà dérivées de la période de la déclaration et déjà
  réactives à un changement de période (`useMemo` ligne 232, dépendances `exercice/type/periode`) —
  si l'utilisateur change la période de la déclaration, `debut`/`fin` changent automatiquement. Aucun
  changement requis ici.
- Ajout et retrait de règlements après une première intégration restent possibles tant que la
  déclaration n'est pas clôturée/déposée — comportement actuel non touché par cette TASK.

## Périmètre STRICT

- **Inclus** :
  1. Retirer le chargement automatique au montage (`fetchAll` déclenché par `useEffect` ligne
     353-357) : l'écran affiche un **état vide** avec message explicite (« Cliquez sur Intégrer pour
     charger les règlements de la période ») au lieu de la grille, tant que l'utilisateur n'a pas agi.
  2. Ajouter un bouton **« Intégrer »** (zone d'action de l'écran, à côté d'« Export de contrôle »
     ou en zone haute — à la discrétion du dev, cohérence visuelle avec le reste de l'écran) qui
     déclenche `fetchAll` (le calcul existant, inchangé) filtré sur les bornes courantes `debut`/`fin`
     de la déclaration.
  3. Supprimer le pré-cochage automatique (bloc « Nouvelle déclaration : coche tout par défaut »,
     lignes 388-399) : après chargement, **aucune ligne n'est cochée**, l'utilisateur sélectionne
     manuellement (le mécanisme de sélection/case à cocher par ligne, déjà existant, n'est pas modifié).
  4. Conserver la restauration d'une **sélection déjà sauvegardée** (`savedSelection`, lignes 375-386)
     quand on revient sur une déclaration existante : dans ce cas, le comportement actuel de rechargement
     automatique + sélection restaurée reste voulu (ce n'est pas une « proposition », c'est l'état déjà
     validé par l'utilisateur — à confirmer avec le PO si le chargement doit malgré tout attendre un
     clic sur « Intégrer », ou se faire automatiquement puisqu'il n'y a rien à décider de nouveau).
  5. Le bouton « Intégrer » doit pouvoir être ré-utilisé pour **recharger/rafraîchir** la liste (ex. si
     l'utilisateur change la période de la déclaration après un premier chargement) — pas seulement au
     premier clic.
- **Exclu** :
  - Aucun nouveau filtre serveur ou nouvelle colonne : le filtre « Date rapprochement » existe déjà.
  - Aucun changement au calcul lui-même (`fetchAll`, `GET /api/rapprochement`, statuts éligible/à
    contrôler/bloqué) — uniquement le déclenchement et le pré-cochage.
  - Aucun changement au mécanisme d'ajout/retrait de règlements après intégration (déjà correct,
    cf. Contexte).
  - Aucun changement aux écrans ②/③ (Affectations, Vérifier & Intégrer).

## Décision PO (06/08/2026)

Tranché : une **déclaration existante qui a déjà des lignes intégrées** doit les afficher directement,
sans exiger de clic sur « Intégrer » — le chargement automatique + restauration de `savedSelection`
(lignes 375-386) est **conservé tel quel**. Le bouton « Intégrer » et l'écran vide par défaut ne
concernent que le cas **sans sélection sauvegardée** (nouvelle déclaration, ou déclaration existante
sans ligne encore intégrée).

## Étapes

1. `ReglementsSelection.tsx` : transformer `fetchAll` en action déclenchée par clic (bouton
   « Intégrer ») plutôt que par `useEffect` de montage, pour le cas « nouvelle déclaration / pas de
   sélection sauvegardée ».
2. Ajouter un état d'écran « vide, jamais chargé » distinct de « chargé, 0 résultat » (message
   différent : invite à cliquer sur Intégrer vs. « Aucun règlement pour cette période »).
3. Retirer le bloc de pré-cochage automatique (lignes 388-399) ; conserver uniquement la restauration
   de `savedSelection` (lignes 375-386).
4. Vérifier que le bouton reste actionnable plusieurs fois (rechargement après changement de période).
5. Non-régression : sélection manuelle ligne par ligne, « tout cocher/décocher » visible, filtres
   Excel-like (dont Date rapprochement), export de contrôle — tous inchangés une fois la liste chargée.

## Livrables

- Écran ① affichant un état vide explicite tant que « Intégrer » n'a pas été cliqué (nouvelle
  déclaration).
- Bouton « Intégrer » déclenchant le calcul existant (`GET /api/rapprochement`) filtré sur la période
  courante de la déclaration.
- Aucune ligne pré-cochée après chargement (nouvelle déclaration) — sélection 100 % manuelle.
- `VERIFY/TASK-200_verify.md` : preuve sur cas réel — nouvelle déclaration (écran vide → clic
  Intégrer → liste affichée non cochée → sélection manuelle → total sélectionné cohérent) et
  déclaration existante avec sélection sauvegardée (comportement conforme au point tranché ci-dessus).

## Critères de validation

- À l'ouverture de l'écran ① sur une **nouvelle** déclaration : aucune requête réseau vers
  `/api/rapprochement`, liste vide, message d'invite visible.
- Clic sur « Intégrer » : déclenche le chargement filtré sur la période courante, résultat affiché
  **sans aucune ligne cochée**.
- Changement de la période de la déclaration puis nouveau clic sur « Intégrer » : liste rechargée sur
  les nouvelles bornes.
- Ajout/retrait de règlements après une première intégration : comportement inchangé (non-régression).
- Filtres existants (dont « Date rapprochement »), tri, export de contrôle : non-régression complète.

## Risques / dépendances

- **Risque de régression sur la restauration de sélection** (`savedSelection`) : bien distinguer le
  cas « nouvelle déclaration » (comportement à changer) du cas « déclaration existante » (comportement
  à documenter/trancher, cf. §Point à trancher) — ne pas casser la reprise d'une déclaration en cours.
- Écran à fort usage (point d'entrée du tunnel de sélection) : prévoir un retest fonctionnel réel avec
  le PO avant clôture, pas seulement une vérification de compilation.

## Files

- [declaration-tva-web/src/ReglementsSelection.tsx](../declaration-tva-web/src/ReglementsSelection.tsx) (écran concerné, `fetchAll` ligne ~284, chargement auto ligne ~353, pré-cochage ligne ~365-402).
