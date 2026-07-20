# TASK-113 — `DomainGrid` : colonnes désalignées (lignes virtualisées en `position:absolute` dans un tableau en layout auto)

## Origine

Signalement PO 17/07/2026 (même capture que TASK-112, `TVA1-2026-01`, onglet Décaissement, écran ②
« Vérifier & Intégrer », drill ouvert) : « l'écran est très mal formé, le traçage de grid ». Sur la
capture, les séparateurs verticaux ne sont alignés ni avec l'en-tête ni entre les lignes, et le
décalage s'accroît ligne à ligne (`MAXI LV` / `COPRALIM` / `GHALI WHEELS LOGITR…` ne produisent pas
la même largeur de colonne).

Défaut **distinct** de TASK-112 (filtre) : ne pas mélanger les deux.

> **Reconfirmation PO 19/07/2026** : nouvelle capture sur le même mécanisme, cette fois via le
> drill « Lignes incohérentes (TTC ≠ HT+TVA) » de l'écran ② Vérifier & Intégrer
> (`VerifierIntegrerPanel.tsx` → `DomainGrid.tsx`, instance readonly). Confirme que le défaut
> touche bien les drills readonly, pas seulement l'écran ① — cohérent avec le périmètre déjà posé
> ci-dessous, aucune extension nécessaire.

> **REJET VERIFY 19/07/2026 — le fix `tableLayout:fixed` + `<colgroup>` livré ne corrige PAS le
> défaut, cause racine mal posée.** Confirmé par le PO après build propre + redéploiement complet
> (`Deploy-All.ps1`) + hard refresh navigateur (Ctrl+F5) : capture reproduisant le même
> désalignement, avec en plus des cellules de données (`statutLigne`/`motif`) sans en-tête
> correspondant visible.
>
> **Cause racine corrigée** : un `<tr>` en `position: absolute` n'est pas seulement « hors du flux
> normal » — le passage en position non-statique **blockifie** son `display` calculé
> (`table-row` → `block`, cf. CSS Display Module §Blockification). Ses `<td>` enfants gardent
> `display: table-cell`, mais leur parent réel n'est plus `table-row` : le navigateur doit alors
> générer une **anonymous table** dédiée pour reconstituer un contexte de tableau valide autour de
> CHAQUE ligne virtualisée, indépendamment de la vraie `<table>` porteuse du `tableLayout:fixed` et
> du `<colgroup>`. Cette anonymous table par ligne recalcule ses largeurs de colonnes **en layout
> auto, à partir de son seul contenu** — exactement le symptôme d'origine, et exactement pourquoi le
> `<colgroup>` de la vraie `<table>` (qui ne s'applique qu'à ce qui reste dans son propre contexte
> de boîte) ne l'atteint jamais. `tableLayout:fixed` + `<colgroup>` sur la table ancêtre est donc
> **structurellement insuffisant** tant que les lignes virtualisées restent des `<tr position:
> absolute>` — peu importe le soin apporté à leur implémentation.
>
> **Piste de correction à explorer par le prochain worker** (à trancher en étape 1, pas figé ici) :
> 1. Fixer une largeur explicite (`width`, pas seulement `maxWidth`) directement sur chaque
>    `<td>`/`<th>` à partir de `colMaxWidth(col)` — indépendant du `<colgroup>`, donc appliqué même
>    dans l'anonymous table générée par ligne. Solution la plus proche du périmètre actuel.
> 2. Ou, si (1) s'avère fragile/insuffisant en essai réel : abandonner la virtualisation par
>    `<tr position:absolute>` au profit du pattern déjà éprouvé et **sans ce défaut** dans
>    `AffectationsDrill.tsx:614-673` (lignes en `<div>` flexbox, largeurs via `colStyle(col)`
>    partagé entre en-tête et corps, pas de sémantique `<table>` à casser). Écarte le risque
>    d'anonymous table par construction ; changement plus large (redéfinit le périmètre STRICT
>    ci-dessous, à re-soumettre au PO avant d'engager).
>
> `VERIFY/TASK-113_verify.md` existant invalidé — ne pas repartir de ce diagnostic pour la
> prochaine tentative. Toute nouvelle VERIFY devra inclure une preuve visuelle réelle (capture
> avant/après sur `TVA1-2026-01`), la validation navigateur n'étant plus soluble par la seule
> lecture de code après cet échec.

> **REJET v2 + SUPERSEDE 19/07/2026** : correction v2 (`width: colMaxWidth(col)` explicite sur
> `<th>`/`<td>`, en plus du `<colgroup>`) — build OK, revue statique favorable, **persistant en
> test réel** (nouvelle capture PO, même symptôme : 7 en-têtes pour 9 valeurs de cellules par
> ligne). Deux itérations de correction dans le paradigme `<table>` + `<tr position:absolute>`
> ont maintenant échoué malgré une analyse statique qui les validait à chaque fois — signal que
> l'analyse de code seule est insuffisante pour ce composant précis et que le paradigme lui-même
> (pas seulement son implémentation) est en cause.
>
> **Décision PO 19/07/2026** : abandon du patch in-place, migration de `DomainGrid.tsx` vers le
> pattern flexbox sans `<table>` déjà éprouvé dans `AffectationsDrill.tsx` (jamais affecté par ce
> défaut). Périmètre repris intégralement dans **TASK-138**
> (`TASK-138-domaingrid-migration-flexbox-remplace-table-virtualisee.md`) — TASK-113 clôturée ici
> comme historique du diagnostic (deux tentatives + cause probable), ne plus retravailler dans ce
> fichier.

## Contexte

- `DomainGrid.tsx:298-308` : chaque `<tr>` du virtualiseur est rendu en `position: 'absolute'` +
  `transform: translateY(...)`, à l'intérieur d'une `<table>` en **layout auto**
  (`DomainGrid.tsx:264` — `width:100%`, `minWidth:1000px`, pas de `tableLayout`).
- Une ligne sortie du flux du tableau ne participe plus à l'algorithme de calcul de largeur des
  colonnes : chaque `<tr>` dimensionne ses `<td>` en fonction de **son seul contenu**,
  indépendamment du `<thead>` (resté dans le flux) et des autres lignes. D'où le décalage croissant
  observé, proportionnel à la variabilité du contenu (libellés tiers de longueurs très différentes).
- `maxWidth` sur `<th>`/`<td>` (`DomainGrid.tsx:273/316`) ne verrouille rien : c'est un plafond, pas
  une largeur imposée. La virtualisation en lignes absolues n'est correcte qu'avec des largeurs
  **fixées** (`tableLayout: 'fixed'` + `<colgroup>`), ou hors `<table>` (grille CSS).
- TASK-110 a traité la **troncature** du contenu (ellipsis + `title`, `DomainGrid.tsx:317-322`) —
  bien présente aujourd'hui — mais pas l'**alignement** des colonnes : le sujet est resté ouvert.
- `AffectationsDrill.tsx` (`607-621`, `674-681`) ne présente pas le défaut : il n'utilise pas de
  `<table>` avec lignes absolues.

## Périmètre STRICT

- **Inclus** :
  1. `DomainGrid.tsx:264-329` : rendre le layout déterministe — `tableLayout: 'fixed'` +
     `<colgroup>` alimenté par `colMaxWidth`/`DEFAULT_COL_WIDTH` (`DomainGrid.tsx:47-63`), en
     couvrant aussi la colonne case à cocher (`width: 40px`, l.268/311) en mode non-readonly.
- **Exclu** :
  - Toute modification du filtre / du drill → **TASK-112**.
  - Réécriture du composant en grille CSS ou changement de virtualiseur : hors périmètre (solution
    simple d'abord). À n'envisager que si le `tableLayout: fixed` ne suffit pas — le documenter dans
    le VERIFY plutôt que d'élargir le périmètre sans arbitrage.
  - `AffectationsDrill.tsx` (non concerné).

## Objectif

```
Entrée  : DomainGrid affichant des lignes à contenu de longueurs très variables (drill readonly ET
          écrans non-readonly)
Traitement : largeurs de colonnes fixées et déterministes, compatibles avec des <tr> absolus
Sortie  : séparateurs verticaux alignés entre en-tête et toutes les lignes, quel que soit le contenu ;
          aucune régression sur tri / ExcelFilter / sélection / sélecteur de colonnes (TASK-068)
```

## Livrables

- `DomainGrid.tsx` : layout fixe + `<colgroup>`.
- Capture avant/après sur le cas réel signalé (drill `TVA1-2026-01` Décaissement, 228 lignes,
  tiers de longueurs variables).
- `VERIFY/TASK-113_verify.md` : build OK, preuve visuelle avant/après, vérification des **deux**
  modes (readonly drill + non-readonly écran ①).

## Critères de validation

- Alignement vertical strict en-tête/lignes sur les 3 pages du cas réel.
- Aucune régression : tri (`handleSort`), filtres (`ExcelFilter`), sélection de lignes et
  « tout sélectionner » (mode non-readonly), sélecteur de colonnes persistant (TASK-068),
  troncature + `title` livrés par TASK-110.
- Défilement horizontal conservé et borné (`minWidth: 1000px` respecté ou justifié si modifié).

## Risques / dépendances

- `DomainGrid.tsx` est partagé par plusieurs écrans (① Affectations non-readonly, ② drills readonly
  TASK-088/092/107) — vérifier **tous** les usages, pas seulement le drill source.
- `tableLayout: 'fixed'` impose que **toutes** les colonnes aient une largeur exploitable : une
  colonne sans `width` prendra une part égale du reste. Vérifier `DEFAULT_COL_WIDTH` et le cas
  « peu de colonnes visibles » (sélecteur TASK-068) — risque de colonnes exagérément larges.
- Interaction avec TASK-112 : les deux touchent le même écran. Livrer séparément et vérifier
  l'absence d'interférence si les deux sont en cours simultanément.

## NOTES

Analyse de code uniquement (rôle architecte, aucun accès UI ; diagnostic établi à partir de la
capture PO + lecture du code). Lignes vérifiées directement : `DomainGrid.tsx:47-63/264-329`,
`VerifierIntegrerPanel.tsx:493-503`, `AffectationsDrill.tsx:607-621/674-681`.
Complète TASK-110 (troncature livrée) sur le volet alignement, non traité à l'époque.
