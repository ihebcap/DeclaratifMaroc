# TASK-150 — Traiter (aboutir ou exclure proprement) les lignes FACTURE_NON_VENTILEE confirmées réelles après ré-audit

Status: 🆕 à faire — **BLOQUÉE tant que TASK-149 n'est pas terminée**
Priority: MEDIUM
Risk: MEDIUM (peut nécessiter une décision métier par ligne, pas seulement technique)
Module: Declaration.Application / Declaration.Orchestration

> **Origine :** TASK-143 — anomalie confirmée #1 : 14 lignes `FACTURE_NON_VENTILEE` bloquent
> l'équilibre de 4 des 6 déclarations. TASK-149 doit d'abord établir combien de ces lignes sont de
> vraies anomalies de données (par opposition à des faux positifs du bug TASK-145).

## Objectif

Pour chaque ligne confirmée comme vraie anomalie par TASK-149 :
1. Utiliser le diagnostic en ligne (TASK-144/147) pour identifier la cause exacte (facture
   réellement absente de Sage, TVA non renseignée côté Sage, etc.).
2. Selon la cause, soit :
   - la faire aboutir (si une correction technique légitime existe, ex. re-déclenchement d'une
     lecture OM après confirmation qu'un problème transitoire est résolu) ;
   - soit la faire basculer en `Etat=Exclue` avec un `MotifRejet` explicite et traçable (jamais un
     rejet silencieux — principe déjà en place ailleurs dans le projet, ex. TASK-097).
3. Documenter, ligne par ligne, la décision prise et sa justification (traçabilité PO).

## Garde-fous

- **Ne pas décider seul(e) d'une règle générale d'exclusion** sans preuve individuelle par ligne —
  chaque cas doit être traité avec son diagnostic propre (TASK-144/147), pas par lot.
- Aucun recalcul d'équilibre global silencieux — toute ligne qui change d'état doit se refléter
  dans le contrôle d'équilibre existant, pas de contournement.
- Si une ligne nécessite une décision métier que l'implémenteur ne peut pas trancher seul (ex. :
  facture réellement introuvable des deux côtés, à investiguer avec le client), **documenter et
  laisser en `Proposee`/marquer comme "nécessite arbitrage PO"** plutôt que d'inventer une règle.

## Files

- Dépend du diagnostic produit par TASK-149 — fichiers exacts à déterminer une fois ce rapport disponible.
- `Declaration.Application/Services/DeclarationWorkflowService.cs` (logique d'état de ligne, `MapLignesCandidates`, cohérente avec TASK-097).

## Validation

- [ ] TASK-149 confirmée livrée avant de commencer.
- [ ] Chaque ligne initialement dans les "14" a une décision documentée (aboutie / exclue avec motif / arbitrage PO requis).
- [ ] Aucun rejet silencieux (motif toujours renseigné).
- [ ] L'écart d'équilibre global des déclarations concernées est recalculé et cohérent.

## Dépendances / risques

- Dépend strictement de TASK-149.
- Cette TASK peut légitimement se terminer avec certaines lignes encore ouvertes si elles
  nécessitent un arbitrage humain (PO) — ce n'est pas un échec de la TASK, c'est le comportement
  attendu (pas d'invention de contexte manquant, règle absolue `ARCHITECTURE.md`).
