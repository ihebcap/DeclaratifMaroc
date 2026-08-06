# TASK-201 — En-têtes de grille (écran ① Sélection + Rapprochement) : autoriser le retour à la ligne

Status: ❌ **REMPLACÉE PAR TASK-204** (arbitrage PO 07/08/2026) — ne pas développer.

> **Raison du remplacement :** décision de migrer toutes les grilles maison (`ExcelFilter`,
> `ColumnSelector`, virtualisation `@tanstack/react-virtual`) vers **AG Grid Community**
> (`TASKS/TASK-204-migration-ag-grid.md`). AG Grid gère son propre rendu d'en-tête
> (`wrapHeaderText`/`autoHeaderHeight`) — le bug de chevauchement décrit ici disparaît de lui-même une
> fois `ReglementsSelection.tsx` et `RapprochementInterrogation.tsx` migrés, sans correctif dédié.
> Conservée ici pour trace, non développée.

Status (historique, avant remplacement) : 🆕 à faire
Priority: LOW (confort visuel, aucune perte de donnée)
Module: declaration-tva-web

> **Origine :** signalement PO (06/08/2026, capture d'écran) — sur l'écran ① Sélection, les libellés
> d'en-tête de colonne (« Date règlement », « Échéance », « Date règlement (espèces) », etc.) restent
> forcés sur une seule ligne et se retrouvent écrasés/superposés avec l'icône de tri et l'icône de
> filtre Excel-like quand la colonne est étroite. Le PO demande que l'en-tête puisse revenir à la
> ligne (texte sur 2 lignes) au lieu de rester sur une seule ligne tronquée.

## Cause identifiée (preuve de code)

Dans les deux grilles qui partagent le même patron de rendu d'en-tête (`ReglementsSelection.tsx`,
écran ① Sélection — et `RapprochementInterrogation.tsx`, écran Rapprochement, TASK-037), la cellule
d'en-tête porte explicitement `whiteSpace: 'nowrap'` :

- [declaration-tva-web/src/ReglementsSelection.tsx:571](../declaration-tva-web/src/ReglementsSelection.tsx#L571)
- [declaration-tva-web/src/RapprochementInterrogation.tsx:390](../declaration-tva-web/src/RapprochementInterrogation.tsx#L390)

Cette propriété interdit tout retour à la ligne du libellé, quelle que soit la largeur réelle de la
colonne (`colStyle(col)`, largeurs fixes en `px` par colonne) — c'est la cause directe du chevauchement
visible sur la capture PO.

## Périmètre STRICT

- **Inclus** :
  1. Retirer (ou remplacer par `whiteSpace: 'normal'`) la propriété `whiteSpace: 'nowrap'` sur la
     cellule d'en-tête des deux fichiers ci-dessus.
  2. Ajuster la hauteur de la ligne d'en-tête (actuellement calée sur du texte 1 ligne, `padding:
     '0.5rem 0.75rem'`) pour accueillir un libellé sur 2 lignes sans que l'icône de tri/filtre ne se
     retrouve mal positionnée (vérifier `alignItems`/`justifyContent` une fois le texte multi-ligne).
  3. Vérifier visuellement (capture avant/après) que les libellés les plus longs (« Date règlement
     (espèces) », « Date rapprochement ») s'affichent proprement sur 2 lignes sans chevaucher les
     icônes.
- **Exclu** :
  - Aucun changement de largeur de colonne, de contenu de cellule de données, ni de logique de tri/
    filtre — uniquement le rendu du libellé d'en-tête.
  - Pas de changement sur d'autres grilles du produit hors des deux fichiers identifiés, sauf si le
    dev en repère d'autres partageant le même patron exact (`ColumnSelector`/`ExcelFilter` en-tête) —
    à signaler si trouvé, ne pas étendre le périmètre sans le dire.

## Livrables

- En-têtes de `ReglementsSelection.tsx` et `RapprochementInterrogation.tsx` capables de revenir à la
  ligne, sans chevauchement avec les icônes de tri/filtre, sur les colonnes actuelles.
- Capture d'écran avant/après dans le `VERIFY/TASK-201_verify.md`.

## Critères de validation

- Aucun libellé d'en-tête ne chevauche visuellement l'icône de tri ou de filtre, y compris sur les
  libellés les plus longs, sur une résolution d'écran usuelle du PO.
- Non-régression : tri par clic sur l'en-tête, filtre Excel-like par colonne, largeur de colonne —
  tous inchangés.

## Files

- [declaration-tva-web/src/ReglementsSelection.tsx](../declaration-tva-web/src/ReglementsSelection.tsx) (ligne 571).
- [declaration-tva-web/src/RapprochementInterrogation.tsx](../declaration-tva-web/src/RapprochementInterrogation.tsx) (ligne 390).
