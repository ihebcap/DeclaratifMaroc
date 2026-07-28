# TASK-135 — Mesure du délai de paiement fournisseur : extension de l'écran Factures

## Contexte
Sous-fonctionnalité 3 du périmètre (CDC §3.3, "redéfinie" par décision PO §5.A-1) : **remplace
entièrement** les deux anciens calculs DMP du code legacy (pondération montant payé côté dashboard,
pondération montant facturé côté écrans détaillés — les deux abandonnés, CDC §4.1). Aucun écran dédié
n'était prévu au CDC §6 pour cette règle redéfinie — décision prise en session d'analyse (19/07/2026) :
**étendre l'écran Factures existant** (`FactureInterrogation.tsx`, TASK-041, `GET /api/factures`) plutôt
que créer un écran neuf, car c'est le seul écran déjà **facture-pivot** capable de montrer aussi bien les
factures payées que les factures **non payées** en retard — contrairement à l'écran Rapprochement
(TASK-037, écran **règlement-pivot**, qui ne montre que ce qui a été effectivement réglé et raterait donc
les factures jamais payées mais en retard, qui doivent pourtant apparaître dans la DDP, cf. bucket 1
TASK-131).

## Règle validée (§3.3)
Le délai à mesurer pour un règlement = écart entre la **date de rapprochement bancaire** du règlement
(mécanisme déjà en place côté module TVA : `RT_MOUVEMENT.MV_Point`/`MV_PointDate`, source locale GRF
validée fiable — TASK-001 — et non le mécanisme Sage de l'ancien module RAS ; décision explicite PO
19/07/2026 : réutiliser le même principe que la TVA) et l'**échéance légale** de la facture (TASK-127 :
date facture + délai résolu via convention fournisseur ou défaut société).

## Objectif
```
Entrée  : facture (ligne existante de l'écran Factures, TASK-041)
Colonnes ajoutées :
  - Échéance légale = TASK-127 (date facture + délai résolu, ajusté jour ouvré)
  - Écart (jours) = si facture soldée (payée à 100%) : dernière date de rapprochement bancaire pertinente
                    − échéance légale
                  = si solde restant > 0 (non payée ou partiellement payée) : date du jour − échéance
                    légale (retard "à ce jour", provisoire tant que non soldée)
Sortie  : indicateur de pilotage interne (retard fournisseur), affiché mais non lié au workflow DDP
```

## Périmètre STRICT
- **Inclus** : 2 colonnes ajoutées à l'écran Factures existant (back : projection étendue de
  `GET /api/factures` ; front : colonnes dans `FactureInterrogation.tsx`).
- **Exclu** : toute logique de déclaration (DDP reste seule responsable de ses propres seuils légaux et
  de son propre calcul incrémental, TASK-131 — cette extension est un indicateur de pilotage, pas une
  source de vérité réglementaire), écran dédié neuf (explicitement écarté au profit de l'extension),
  export Excel/XML de cet indicateur (hors périmètre).

## Étapes
1. Back : étendre la projection de `GET /api/factures` (`FacturesController.cs`, `FacturesFromWhere`
   partagé liste/COUNT, cf. TASK-041) avec `EcheanceLegale` (TASK-127) et `EcartJours` (calcul ci-dessus).
2. Front : 2 colonnes dans `FactureInterrogation.tsx`, cohérentes avec les colonnes existantes (mise en
   forme couleur si retard positif, comme les indicateurs de statut déjà en place ailleurs dans l'app).
3. Lecture seule stricte (même garde que le reste de l'écran Factures, TASK-041).
4. Tests : back (calcul échéance légale + écart, cas soldé vs solde restant) ; front (affichage,
   tri/filtre sur la nouvelle colonne si le mécanisme `ExcelFilter` le permet nativement).

## Livrables
- Projection back étendue + 2 colonnes front.
- Tests back (calcul) + vérification manuelle front.

## Critères de validation
- Les 2 colonnes s'affichent correctement pour une facture soldée et une facture avec solde restant,
  sur données réelles ou fixture.
- Aucune régression sur les colonnes/tri/filtre existants de l'écran Factures (TASK-041).
- Build back+front 0 erreur.

## Risques / dépendances
- Dépend de TASK-127 (résolution délai/échéance légale).
- Indépendant de DDP (TASK-131 à 134) — peut être développé en parallèle, aucune dépendance croisée.
