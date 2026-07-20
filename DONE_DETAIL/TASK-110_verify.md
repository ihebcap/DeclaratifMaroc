# VERIFY — TASK-110 — Grille de drill « Vérifier & Intégrer » : mise en page dégradée

> Implémentée en **worker exceptionnel** (demande explicite PO/architecte, 17/07/2026) — l'architecte
> ne code pas d'ordinaire (cf. `CLAUDE.md`), rôle inversé pour cette task uniquement. Ce document est
> soumis pour revue, pas auto-approuvé.

## Résumé
Front-only, un seul fichier modifié : `declaration-tva-web/src/DomainGrid.tsx`. Reprend le pattern
déjà validé dans `AffectationsDrill.tsx` (`GridCell`/`colStyle`, l.674-681/607-621) : troncature
`overflow:hidden` + `textOverflow:ellipsis` + largeur de colonne bornée. Aucune modification du
mécanisme de filtre (`handleFilterChange`, back `BuildLigneFilterWhere`, `ExcelFilter`) — conforme
au périmètre strict de la task, ce point restant non confirmé côté code (cf. task originale, §Risques).

## Modifications
- **Type `ColumnDef`** : ajout d'un champ optionnel `width?: string` (défaut `'200px'` via
  `colMaxWidth()`), colonne `motif` fixée à `'280px'` (large mais bornée, cf. cas réel PO).
- **En-têtes (`<th>`)** : `maxWidth: colMaxWidth(col)` sur le `<th>`, libellé enveloppé dans un
  `<span>` avec `overflow:hidden`/`textOverflow:ellipsis`/`whiteSpace:nowrap` — empêche un libellé
  de colonne de forcer une largeur de colonne excessive (défensif, les libellés actuels sont courts).
- **Cellules (`<td>`)** : `maxWidth: colMaxWidth(col)` sur le `<td>`, contenu enveloppé dans un
  `<div>` avec les mêmes styles de troncature + attribut `title` (texte brut complet) quand la
  valeur est une chaîne — le texte intégral reste consultable au survol, conformément au critère
  « ne pas perdre l'information ».
- **Non touché** : `AffectationsDrill.tsx` (référence, non modifié), mécanisme de filtre/back,
  `minWidth: '1000px'` sur la `<table>` (conservé — sans lien avec le bug, cf. task originale).

## Build
```
npm run build   (tsc -b && vite build)   → 0 erreur
npm run lint    (oxlint)                 → aucun nouveau warning (résidus pré-existants sur
                                            d'autres fichiers, non liés à ce changement ; le seul
                                            warning DomainGrid.tsx préexistant — catch 'e' inutilisé
                                            l.172 — n'est pas dans le code touché)
```

## Tests Playwright (régression)
Nouveau test dédié + suite existante rejouée sans modification, contre la base réelle
(`GR_EMA_DISTRIBUTION` / `NEW_EMA DISTRIBUTION`, `.\sql2022`, `reset.ps1`/`make_eligible.ps1`) :

| Test | Résultat |
|---|---|
| `tests/task110.spec.ts` (nouveau) | ✅ passed |
| `tests/declaration.spec.ts` (parcours complet, `DomainGrid` non-readonly ① Affectations) | ✅ passed |
| `tests/task088.spec.ts` (drill anomalie readonly ②) | ✅ passed |
| `tests/task107.spec.ts` (drill source readonly ②, même écran que ce bug) | ✅ passed |
| `tests/column-selector.spec.ts` (sélecteur de colonnes persistant, TASK-068) | ✅ passed |

Aucune régression détectée sur le tri, les filtres `ExcelFilter`, la sélection de lignes/actions de
masse (mode non-readonly), ni sur le sélecteur de colonnes.

## Preuve réelle — capture avant/après
**Cas réel d'origine non rejouable** : `TVA1-2026-01` (à l'origine du signalement PO 17/07/2026,
motif `FC2501667`) n'existe plus dans la base actuelle (même constat que TASK-105, base réinitialisée
depuis). Pour reproduire fidèlement le cas signalé sans fabriquer une fausse incohérence Sage en
base (hors périmètre), le motif exact du signalement PO a été injecté par interception réseau sur
`GET /declarations/*/lignes` (même technique déjà utilisée par `tests/task102.spec.ts`/TASK-105 :
route interceptée, réponse réelle relue puis les champs `motif`/`factureNumero`/`statutLigne` mutés
sur la 1ʳᵉ ligne réelle) — le texte injecté est **identique** à celui du signalement :
« Incohérence Sage : Σ(HT+TVA+Parafiscale)=3960,00 ≠ TTC=3762,00 (écart -198,00) », `FC2501667`.
Déclaration réelle `TVA1-2026-06` (`NEW_EMA DISTRIBUTION`), drill « Répartition par source » ouvert
depuis ② Vérifier & Intégrer (même chemin que le signalement : `RecapSourceTable` → clic ligne →
`DomainGrid` readonly).

- `VERIFY/task110-drill-motif-long-AVANT.png` — **avant correctif** (`DomainGrid.tsx` remis à l'état
  d'origine via `git stash` pour la capture, puis restauré) : le motif long s'affiche en une seule
  ligne non tronquée, forçant la grille à défiler horizontalement — les colonnes `N° Facture`/
  `Tiers`/`Origine` sortent du cadre visible, exactement le symptôme décrit par le PO
  (« l'affichage n'est pas joli », défilement disproportionné).
- `VERIFY/task110-drill-motif-long-APRES.png` — **après correctif** : le motif s'affiche tronqué
  (« Incohérence Sage : Σ(HT+TVA+Parafi... ») dans une colonne bornée à 280px, toutes les colonnes
  restent visibles sans défilement disproportionné. Mesure automatisée dans le test :
  largeur de la cellule motif = **280px** (< 400px, seuil de l'assertion), contre une largeur
  largement supérieure à l'écran avant correctif (assertion `toBeLessThan(400)` échoue sur le code
  d'origine — confirmé en rejouant le test sur le `DomainGrid.tsx` non corrigé).
- Texte complet toujours accessible : attribut `title` posé sur la cellule (survol souris),
  vérifié dans le code (`DomainGrid.tsx`, wrapper de cellule).

## Critères de validation (task originale)
- [x] Un motif d'écartement long ne déborde plus horizontalement au-delà d'une largeur de colonne
      raisonnable (280px pour `motif`, 200px par défaut pour les autres colonnes texte) — preuve
      avant/après ci-dessus.
- [x] Le texte complet reste accessible (attribut `title` au survol).
- [x] Aucune régression sur le tri, les filtres (`ExcelFilter`), la sélection de lignes (mode
      non-readonly), ni sur le sélecteur de colonnes persistant (TASK-068) — cf. §Tests Playwright.
- [x] Le mécanisme de filtre Source n'a **pas** été modifié (aucune ligne touchée dans
      `handleFilterChange`/back/`ExcelFilter`) — le signalement PO sur ce point reste **non
      confirmé côté code** et hors périmètre de ce correctif, conformément à la task originale.

## Réserves (signalées, non corrigées — hors périmètre de cette task)
- **Signalement PO « le filtre n'a pas bien fonctionné »** : toujours non reproduit ni confirmé
  côté code (parcours tracé bout en bout dans la task originale, aucune anomalie trouvée). Reste à
  clarifier avec le PO (quelle colonne, quel comportement observé) avant tout correctif éventuel.
- **Cas réel d'origine non rejouable** : capture réalisée avec le motif exact du signalement injecté
  par interception réseau sur une déclaration réelle différente (`TVA1-2026-06`, base actuelle),
  faute de pouvoir rejouer `TVA1-2026-01` (absente de la base) — même limitation déjà documentée
  pour TASK-105/107.
- **Largeur des autres colonnes texte** (`tiers`, `factureNumero`) bornée par défaut à 200px comme
  effet de bord du correctif générique (`colMaxWidth`) — non demandé explicitement par la task
  (périmètre = « au minimum motif ») mais cohérent avec le pattern `AffectationsDrill.tsx` et sans
  troncature visible sur les valeurs réelles observées (tiers/n° facture courts). Signalé pour
  information, pas un défaut.

## Statut
**Soumise pour revue** (worker exceptionnel — ne s'auto-approuve pas, conformément à la séparation
des rôles `CLAUDE.md`).
