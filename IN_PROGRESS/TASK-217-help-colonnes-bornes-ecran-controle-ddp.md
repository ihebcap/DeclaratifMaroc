# TASK-217 — Clarté des colonnes de bornes et d'origine du délai (écran Contrôle DDP)

## Contexte

Échanges PO (10/09/2026), en préparation du dépôt T3 2026 de la Déclaration Délai de Paiement
(DDP) : les colonnes **« Déjà déclaré au »** et **« Constaté au »** de l'écran de contrôle
(`ControleLignesDelaiPaiementPanel.tsx`) ne sont pas compréhensibles à la lecture pour un utilisateur
métier, y compris pour le PO lui-même (« je n'ai pas compris »). Leur sens n'est explicite que dans le
code (`SelectionDelaiPaiementCalculator.cs:226-244`) :

- **`borneReference`** (colonne actuelle « Déjà déclaré au ») : date jusqu'à laquelle le retard de
  cette échéance a déjà été compté dans une déclaration DDP antérieure (`MAX(DDP_DateFin)` sur
  `RT_DECLARATIONDELAISPAIEMENTLG`), à défaut l'échéance légale (première déclaration), à défaut une
  reprise manuelle saisie (TASK-128).
- **`borneActuelle`** (colonne actuelle « Constaté au ») : date jusqu'à laquelle le retard est
  constaté pour CETTE période — la date de règlement/rapprochement si l'échéance est payée, sinon la
  fin de la période déclarée (le retard continue de courir tant que l'échéance reste impayée).
- **`depassement`** (« Dépassement (j) ») : l'écart INCRÉMENTAL entre les deux bornes ci-dessus
  (jamais le retard total depuis l'échéance légale) — principe anti-double-déclaration du module.

Par ailleurs, la colonne **« Origine du délai »** (`origineDelai`, valeurs `ConventionFacture` /
`Convention` / `Defaut` → libellés `LIBELLES_ORIGINE_DELAI`, ligne 57-61) affiche uniquement la
*source* du délai appliqué (convention ou défaut société) sans jamais montrer le nombre de jours réel
retenu — alors que ce nombre est déjà calculé et déjà présent dans le DTO
(`NombreJoursDelaiApplique`, `DeclarationDelaiPaiementDto.cs:164/204`,
`SelectionDelaiPaiementCalculator.cs:223/562`), simplement non affiché côté front. Un utilisateur qui
veut vérifier « pourquoi 60 jours et pas 90 ? » doit aujourd'hui aller chercher la convention
ailleurs — l'information est déjà calculée mais cachée.

Décisions PO à date :
- Renommer les 2 colonnes de bornes pour éviter le jargon opaque, SANS collision avec les colonnes
  déjà existantes sur cet écran (« Mode » = mode de règlement, « Échéance légale » = échéance déjà
  affichée) :
  - « Déjà déclaré au » → **« Dernière déclaration »**
  - « Constaté au » → **« Constaté le »**
- Compléter « Origine du délai » avec le nombre de jours réellement appliqué, pas seulement la source.
- Un champ « commentaire » généré automatiquement par ligne est une demande **distincte**, traitée en
  TASK-218 (voir ce fichier) — ne pas la mélanger ici.

## Objectif
```
Entrée  : "Déjà déclaré au"/"Constaté au" incompréhensibles sans lire le code ; "Origine du délai"
          affiche seulement "Convention"/"Défaut société" sans le nombre de jours réellement calculé
          (donnée déjà disponible côté backend, NombreJoursDelaiApplique).
Traitement : renommer les 2 colonnes de bornes (sans collision avec Mode/Échéance légale), ajouter
             une icône d'aide (ⓘ) + tooltip sur leur en-tête, et afficher le nombre de jours retenu
             dans la colonne Origine du délai.
Sortie  : un utilisateur du contrôle DDP (PO inclus) comprend, sans assistance, ce que signifie
          chaque borne et connaît directement le nombre de jours de délai appliqué sans devoir
          consulter un autre écran.
```

## Périmètre STRICT

- **Inclus** :
  1. `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx`, `columnDefs` (lignes 145-149) :
     - Colonne `borneReference` : `headerName` → **« Dernière déclaration »** + icône d'aide/tooltip.
     - Colonne `borneActuelle` : `headerName` → **« Constaté le »** + icône d'aide/tooltip.
     - Colonne `depassement` (« Dépassement (j) ») : ajouter aussi une icône d'aide/tooltip (pas de
       renommage nécessaire, le libellé actuel n'est pas ambigu en lui-même).
     - Colonne `origineDelai` (« Origine du délai ») : `valueGetter` complété pour inclure
       `nombreJoursDelaiApplique` — ex. affichage `"Convention (90 j)"` / `"Défaut société (60 j)"`
       au lieu de `"Convention"` / `"Défaut société"` seuls. Vérifier que `nombreJoursDelaiApplique`
       est bien exposé côté client dans le type TypeScript consommé par ce composant (`api.ts`) —
       sinon l'ajouter au mapping front (le champ existe déjà côté DTO backend, aucune modification
       backend attendue a priori).
  2. Textes des 3 tooltips (« Dernière déclaration », « Constaté le », « Dépassement (j) ») en
     français, orientés utilisateur métier (pas de jargon technique type `BorneReference`/`EcId`) —
     textes indicatifs, à affiner tant que le sens reste fidèle au calcul réel :
     - **Dernière déclaration** : « Date jusqu'à laquelle le retard de cette échéance a déjà été
       signalé dans une déclaration DDP précédente. Vide si l'échéance n'a jamais été déclarée. »
     - **Constaté le** : « Date jusqu'à laquelle le retard est compté pour cette période : la date de
       paiement si l'échéance est réglée, sinon la fin de la période en cours tant qu'elle reste
       impayée. »
     - **Dépassement (j)** : « Nombre de jours de retard NOUVEAUX depuis la dernière déclaration —
       pas le retard total depuis l'échéance légale, pour éviter de compter deux fois le même
       retard. »
  3. Respect de `DOCS/UI_STANDARDS.md` pour tout composant/pattern introduit (aucune convention
     existante sur les tooltips de header à ce jour — choix libre du WORKER tant que cohérent avec
     le style général de l'écran, à documenter dans le VERIFY).
- **Exclus / hors périmètre** :
  - Le champ « commentaire » généré automatiquement par ligne (texte explicatif complet du pourquoi
    de chaque ligne) — traité en TASK-218, ne pas l'implémenter ici.
  - Modifier la logique de calcul (`SelectionDelaiPaiementCalculator.cs`,
    `EcheanceLegaleCalculator.cs`) — TASK strictement UI/UX, `NombreJoursDelaiApplique` est déjà
    calculé et n'a besoin d'aucun changement backend pour être affiché (sauf si l'étape 1 des
    Étapes ci-dessous révèle qu'il manque au mapping TypeScript, auquel cas ajouter uniquement ce
    mapping, jamais recalculer la valeur).
  - Renommer d'autres colonnes non citées par le PO (`Échéance légale`, `Statut`, etc.).
  - Un bandeau d'aide générale dépliable en haut de l'écran — option écartée par le PO au profit
    d'une aide contextuelle colonne par colonne.

## Étapes
1. Vérifier dans `declaration-tva-web/src/api.ts` (types de retour de `getControleLignesDdp`) que
   `nombreJoursDelaiApplique` est bien mappé côté TypeScript ; sinon l'ajouter (le DTO backend
   l'expose déjà, `DeclarationDelaiPaiementDto.cs:164/204` — ne toucher au backend QUE si ce champ
   est absent de la réponse JSON réellement envoyée, ce qui serait un oubli distinct à signaler).
2. Identifier dans `ApbsGrid`/ag-grid le mécanisme standard disponible pour une icône + tooltip de
   header personnalisé (`headerTooltip` natif ag-grid recommandé en premier réflexe pour rester
   simple — un composant custom uniquement si `headerTooltip` s'avère insuffisant visuellement).
3. Appliquer les renommages + tooltips + l'ajout du nombre de jours dans `origineDelai`.
4. Test visuel dans le navigateur : survol de chaque header (tooltip visible), lecture de la colonne
   Origine du délai sur des lignes ConventionFacture / Convention / Defaut (nombre de jours cohérent
   avec la convention réellement configurée pour le tiers, à vérifier sur 2-3 cas réels).
5. `npm run lint` + `npm run build` dans `declaration-tva-web/` → 0 erreur.

## Livrables
- `ControleLignesDelaiPaiementPanel.tsx` (et `api.ts` si mapping manquant) mis à jour.
- `VERIFY/TASK-217_verify.md` : capture d'écran du survol de chaque header (tooltip visible), capture
  de la colonne Origine du délai montrant le nombre de jours sur au moins un cas Convention et un cas
  Défaut société, logs `npm run lint` / `npm run build` (0 erreur), checklist UI de
  `DOCS/UI_STANDARDS.md` cochée.

## Critères de validation
- Les en-têtes affichent « Dernière déclaration » et « Constaté le » (plus « Déjà déclaré au » /
  « Constaté au »), sans collision de sens avec les colonnes « Mode » et « Échéance légale »
  existantes.
- Les 3 tooltips sont visibles au survol du header, texte fidèle à la logique réelle du calculateur
  (validée par l'architecte à la review VERIFY).
- La colonne Origine du délai affiche le nombre de jours réellement appliqué (`NombreJoursDelaiApplique`),
  cohérent avec la convention ou le délai par défaut société réellement configuré, sur au moins 2 cas
  vérifiés manuellement.
- Aucun changement de libellé sur les autres colonnes, aucune régression sur le tri/filtre existant.
- `npm run lint` + `npm run build` → 0 erreur.

## Risques / dépendances
- Aucun risque fonctionnel (TASK purement UI, read-only, aucune donnée modifiée).
- Dépendance faible : vérifier qu'ag-grid/`ApbsGrid` supporte nativement un header tooltip sans
  nécessiter une dépendance ou un pattern lourd — sinon proposer l'alternative la plus simple.
- Si `nombreJoursDelaiApplique` s'avère absent du contrat JSON réel envoyé au front (malgré sa
  présence dans le DTO backend), signaler explicitement dans le VERIFY plutôt que d'improviser un
  recalcul front — ce serait un défaut de sérialisation distinct à documenter, pas à corriger
  silencieusement dans le cadre de cette TASK UI.
