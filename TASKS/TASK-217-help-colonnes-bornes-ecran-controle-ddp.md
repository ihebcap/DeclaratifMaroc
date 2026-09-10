# TASK-217 — Tooltips d'aide sur les colonnes de bornes de l'écran Contrôle DDP

## Contexte

Signalement PO (10/09/2026), en préparation du dépôt T3 2026 de la Déclaration Délai de Paiement
(DDP) : les colonnes **« Déjà déclaré au »** et **« Constaté au »** de l'écran de contrôle
(`ControleLignesDelaiPaiementPanel.tsx`) ne sont pas compréhensibles à la lecture pour un utilisateur
métier, y compris pour le PO lui-même (« je n'ai pas compris »). Leur sens n'est explicite que dans le
code (`SelectionDelaiPaiementCalculator.cs:226-244`) :

- **Déjà déclaré au** = `BorneReference` : date jusqu'à laquelle le retard de cette échéance a déjà
  été compté dans une déclaration DDP antérieure (`MAX(DDP_DateFin)` sur
  `RT_DECLARATIONDELAISPAIEMENTLG`), à défaut l'échéance légale (première déclaration), à défaut une
  reprise manuelle saisie (TASK-128).
- **Constaté au** = `BorneActuelle` : date jusqu'à laquelle le retard est constaté pour CETTE
  période — la date de règlement/rapprochement si l'échéance est payée, sinon la fin de la période
  déclarée (le retard continue de courir tant que l'échéance reste impayée).
- **Dépassement (j)** = l'écart INCRÉMENTAL entre les deux bornes ci-dessus (jamais le retard total
  depuis l'échéance légale) — c'est le principe anti-double-déclaration du module (une même journée
  de retard n'est comptée qu'une seule fois, sur une seule déclaration).

Un tooltip existe déjà partiellement dans la barre d'outils sur le texte « dont N déjà déclarée(s) »
(`ControleLignesDelaiPaiementPanel.tsx:253`), mais rien au niveau des en-têtes de colonnes de la
grille elle-même, là où l'utilisateur lit réellement les valeurs ligne par ligne.

## Objectif
```
Entrée  : colonnes "Déjà déclaré au", "Constaté au" et "Dépassement (j)" sans aucune explication
          visible dans la grille — sens non déductible sans lire le code source.
Traitement : ajouter une icône d'aide (ⓘ) à côté de ces en-têtes de colonne, avec un texte au survol
             expliquant le sens exact de la valeur affichée.
Sortie  : un utilisateur du contrôle DDP (PO inclus) comprend, sans assistance, ce que signifie
          chaque borne et pourquoi le dépassement affiché n'est pas "depuis l'échéance légale" mais
          "depuis la dernière déclaration".
```

## Périmètre STRICT

- **Inclus** :
  1. `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx` — colonnes `borneReference`
     (« Déjà déclaré au »), `borneActuelle` (« Constaté au ») et `depassement` (« Dépassement (j) »)
     de `columnDefs` (lignes 147-149) : ajouter une icône d'aide dans le header, avec un texte au
     survol (tooltip) reprenant en langage clair les définitions ci-dessus.
  2. Textes des tooltips en français, orientés utilisateur métier (pas de jargon technique type
     `BorneReference`/`EcId`) — reprendre le style déjà utilisé dans le tooltip existant de la barre
     d'outils (ligne 253) comme référence de ton.
  3. Respect de `DOCS/UI_STANDARDS.md` pour tout composant/pattern introduit (aucune convention
     existante sur les tooltips de header à ce jour — page blanche, donc choix libre du WORKER tant
     que cohérent avec le style général de l'écran, à documenter dans le VERIFY).
- **Exclus / hors périmètre** :
  - Modifier les libellés de colonnes eux-mêmes (« Déjà déclaré au », « Constaté au » restent
    inchangés — seul un complément d'aide est ajouté, pas un renommage).
  - Modifier la logique de calcul (`SelectionDelaiPaiementCalculator.cs`) — TASK strictement UI/UX.
  - Étendre les tooltips à d'autres colonnes non citées par le PO (`Échéance légale`, `Origine du
    délai`, etc.) — hors périmètre signalé, à traiter en TASK séparée si besoin exprimé plus tard.
  - Un bandeau d'aide générale dépliable en haut de l'écran — option écartée par le PO au profit
    d'une aide contextuelle colonne par colonne.

## Étapes
1. Identifier dans `ApbsGrid`/ag-grid le mécanisme standard disponible pour une icône + tooltip de
   header personnalisé (`headerComponentParams`, `headerTooltip`, ou composant de header custom) —
   vérifier ce qui est déjà exploité ailleurs dans le projet pour rester cohérent, sinon documenter
   le choix dans le VERIFY.
2. Rédiger les 3 textes d'aide (français, clair, sans jargon) :
   - **Déjà déclaré au** : « Date jusqu'à laquelle le retard de cette échéance a déjà été signalé
     dans une déclaration précédente. »
   - **Constaté au** : « Date jusqu'à laquelle le retard est compté pour cette période : la date de
     paiement si l'échéance est réglée, sinon la fin de la période en cours tant qu'elle reste
     impayée. »
   - **Dépassement (j)** : « Nombre de jours de retard NOUVEAUX depuis la dernière déclaration
     (colonne "Déjà déclaré au") — pas le retard total depuis l'échéance légale, pour éviter de
     compter deux fois le même retard. »
   (Textes indicatifs, à affiner si besoin tant que le sens reste fidèle au calcul réel.)
3. Implémenter, tester visuellement (survol de chaque header) dans le navigateur.
4. `npm run lint` + `npm run build` dans `declaration-tva-web/` → 0 erreur.

## Livrables
- `ControleLignesDelaiPaiementPanel.tsx` mis à jour avec les 3 tooltips de header.
- `VERIFY/TASK-217_verify.md` : capture d'écran du survol de chaque header montrant le tooltip,
  logs `npm run lint` / `npm run build` (0 erreur), checklist UI de `DOCS/UI_STANDARDS.md` cochée.

## Critères de validation
- Les 3 tooltips sont visibles au survol du header dans l'écran de contrôle DDP, sans navigation
  supplémentaire ni clic.
- Le texte affiché correspond fidèlement à la logique réelle de `SelectionDelaiPaiementCalculator.cs`
  (validée par l'architecte à la review VERIFY).
- Aucun changement de libellé de colonne, aucune régression sur le tri/filtre existant des colonnes
  concernées.
- `npm run lint` + `npm run build` → 0 erreur.

## Risques / dépendances
- Aucun risque fonctionnel (TASK purement UI, read-only, aucune donnée modifiée).
- Dépendance faible : vérifier qu'ag-grid/`ApbsGrid` supporte nativement un header tooltip sans
  nécessiter une dépendance ou un pattern lourd — sinon proposer l'alternative la plus simple
  (ex. `headerTooltip` natif ag-grid) plutôt qu'un composant custom complexe pour 3 colonnes.
