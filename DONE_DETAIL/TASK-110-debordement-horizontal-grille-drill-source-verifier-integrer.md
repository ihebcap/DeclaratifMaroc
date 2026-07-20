# TASK-110 — Grille de drill « Vérifier & Intégrer » : mise en page dégradée (colonne Motif Écartement déborde horizontalement)

## Origine

Signalement PO 17/07/2026 (capture écran, `TVA1-2026-01`, onglet Décaissement, écran ② « Vérifier
& Intégrer ») : ouverture du drill « Répartition par source » (clic sur une ligne de
`RecapSourceTable`, TASK-107) affiche une grille dont « l'affichage n'est pas joli » — un motif
d'écartement long (`FC2501667` : « Incohérence Sage : Σ(HT+TVA+Parafiscale)=3960,00 ≠
TTC=3762,00 (écart -198,00) ») déborde de sa colonne et force un défilement horizontal
disproportionné. Le PO signale aussi que « le filtre apparemment n'a pas bien fonctionné » —
**aucune anomalie de code identifiée à ce stade sur ce point** (cf. Risques), à clarifier avec le
PO avant tout correctif de ce côté.

## Contexte

- Le bandeau drill « ← Retour au contrôle » / « Drill source : Source : … » est rendu par
  `VerifierIntegrerPanel.tsx:466-492` ; la grille est montée juste après (`:494-503`) via
  `<DomainGrid ... initialFilters={drillFiltre.filtre} readonly={true} />`.
- Chaîne du filtre (tracée bout en bout, **aucune incohérence trouvée**) : clic sur
  `RecapSourceTable.tsx:85` (transmet `r.source` brut) → `handleDrillSource`
  (`VerifierIntegrerPanel.tsx:377-384`) construit `{ source: [source] }` → `DomainGrid` l'injecte
  dans son état `filters` via le `useEffect` de `DomainGrid.tsx:109-111` → `GET
  /declarations/{id}/lignes` avec `filter={"source":[...]}` → back `BuildLigneFilterWhere`
  (`DeclarationRepository.cs:417-425`) applique `AND Source IN @Sources`, même portée que
  `GetLignesDistinctsAsync` (l.531-554). Pas de troncature multi-sélection (type TASK-063/067B),
  pas de mismatch casse/accents.
- Bug de mise en page confirmé : le rendu des cellules (`DomainGrid.tsx:312`) applique uniquement
  `whiteSpace: 'nowrap'`, **sans** `overflow: 'hidden'` ni `textOverflow: 'ellipsis'`, et aucune
  colonne n'a de largeur maximale (en-têtes `DomainGrid.tsx:268-284` idem, pas de `width`/`maxWidth`
  sur `visibleColumns.map`). Combiné à `minWidth: '1000px'` sur la `<table>` (`DomainGrid.tsx:260`),
  un motif long étire la ligne entière. Le composant `AffectationsDrill.tsx` (utilisé pour le drill
  affectations de l'écran ①) a déjà résolu ce même problème avec son composant
  `GridCell`/`colStyle` (`AffectationsDrill.tsx:674-681`, `607-621`) : `overflow: 'hidden'` +
  `textOverflow: 'ellipsis'` + largeur bornée (`col.width` ou `minWidth: '160px'` par défaut).
  `DomainGrid` (composant plus ancien, réutilisé en lecture seule pour les drills
  TASK-088/092/107) n'a jamais reçu cet alignement.

## Périmètre STRICT

- **Inclus** :
  1. `DomainGrid.tsx` (cellules `<td>` l.312 + en-têtes `<th>` l.268-284) : troncature propre
     (`overflow:hidden` + `textOverflow:ellipsis`) sur les colonnes texte longues (au minimum
     `motif`), avec largeur de colonne raisonnable par défaut, en reprenant le pattern déjà validé
     dans `AffectationsDrill.tsx`. Le texte complet doit rester consultable (attribut `title=` au
     survol, ou équivalent) — ne pas perdre l'information, seulement l'affichage en ligne.
- **Exclu** (signalé, hors périmètre tant que non confirmé) :
  - Toute modification du mécanisme de filtre (`handleFilterChange`, `BuildLigneFilterWhere`,
    `ExcelFilter`) — aucune anomalie de code identifiée dans ce parcours ; à ne traiter que si le
    PO fournit une reproduction précise (quelle colonne, comportement attendu vs observé).
  - `AffectationsDrill.tsx` (déjà conforme, sert de référence, non touché).

## Objectif

```
Entrée  : Drill "Répartition par source" ouvert depuis ② Vérifier & Intégrer (DomainGrid readonly),
          motif d'écartement long dans la colonne Motif Écartement
Traitement : troncature visuelle propre (ellipsis) + largeur de colonne bornée, cohérente avec
             AffectationsDrill.tsx
Sortie  : grille lisible sans défilement horizontal disproportionné, information complète toujours
          accessible (title/tooltip), aucune régression sur les autres usages de DomainGrid
          (écrans non-readonly, TASK-016/034/067B/068)
```

## Livrables

- `DomainGrid.tsx` : style des `<td>`/`<th>` corrigé (ellipsis + largeur bornée), a minima sur la
  colonne `motif`.
- Capture avant/après reproduisant le cas réel signalé (motif Sage long, capture PO 17/07/2026).
- `VERIFY/TASK-110_verify.md` : build OK, aucune régression sur les usages non-readonly de
  `DomainGrid` (écran ① Affectations, écran ② drill anomalie TASK-088), preuve visuelle avant/après.

## Critères de validation

- Un motif d'écartement long ne déborde plus horizontalement au-delà d'une largeur de colonne
  raisonnable ; le texte complet reste accessible (survol ou équivalent).
- Aucune régression sur le tri, les filtres (`ExcelFilter`), la sélection de lignes (mode
  non-readonly), ni sur le sélecteur de colonnes persistant (TASK-068).
- Le mécanisme de filtre Source n'est **pas** modifié tant qu'aucune reproduction concrète du
  problème signalé par le PO n'est fournie.

## Risques / dépendances

- `DomainGrid.tsx` est partagé par plusieurs écrans (① Affectations non-readonly, ② drill
  source/anomalie readonly TASK-088/092/107) — tout changement de style doit être vérifié sur
  l'ensemble de ces usages, pas seulement le drill source.
- Le signalement PO sur le filtre Source reste **non confirmé côté code** (parcours tracé bout en
  bout : `RecapSourceTable` → `handleDrillSource` → `DomainGrid` → `BuildLigneFilterWhere`,
  cohérent à chaque étape). Risque que le PO ait observé autre chose (ex. confusion avec le filtre
  `ExcelFilter` de la colonne, actionnable même en mode drill, dont l'état initial pourrait sembler
  désynchronisé de `initialFilters` à l'affichage). À clarifier avant tout correctif sur ce point.

## NOTES

Découvert en répondant à un signalement PO (capture écran 17/07/2026, écran ② « Vérifier &
Intégrer », onglet Décaissement `TVA1-2026-01`). Analyse de code uniquement (architecte, lignes
vérifiées directement : `VerifierIntegrerPanel.tsx:377-384/466-503`, `DomainGrid.tsx:48-60/98-111/
260-319`, `AffectationsDrill.tsx:607-621/674-681`, `RecapSourceTable.tsx:85`,
`DeclarationRepository.cs:417-425/531-554`) — non reproduit visuellement en environnement réel (pas
d'accès UI depuis ce rôle). Le point « filtre Source » reste à confirmer par le PO avant correctif
— ne pas l'inclure dans le VERIFY tant qu'aucun repro concret n'est fourni.
