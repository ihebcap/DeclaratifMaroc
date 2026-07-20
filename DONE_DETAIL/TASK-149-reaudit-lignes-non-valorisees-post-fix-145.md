# TASK-149 — Ré-audit des 14 lignes FACTURE_NON_VENTILEE (TASK-143) après correctif TASK-145

Status: 🆕 à faire — **BLOQUÉE tant que TASK-145 n'est pas terminée et vérifiée**
Priority: HIGH
Risk: LOW (lecture seule, même méthode que TASK-143)
Module: Audit / lecture seule

> **Origine :** TASK-143 (audit exhaustif approuvé le 20/07/2026) a identifié 14 lignes
> `FACTURE_NON_VENTILEE` (01:2·02:9·03:1·04:2) expliquant 100 % de l'écart d'équilibre global
> (−100 576,90 MAD). TASK-145 (cette même session) a découvert que le mécanisme même qui produit ce
> motif (`Sens` codé en dur à `Achat`, absence de filtre `DO_Domaine`) peut générer de **fausses**
> anomalies sur des factures de vente authentiques. Le périmètre réel des 14 lignes doit être
> revérifié une fois ce bug corrigé — certaines pourraient disparaître automatiquement, d'autres
> rester de vraies anomalies de données.

## Objectif

Une fois TASK-145 livrée et vérifiée (build + non-régression + purge cache si applicable) :

1. Reproduire la méthode d'audit de TASK-143 (`DOCS/AUDIT-TASK-143/`) sur les 6 déclarations
   `TVA1-2026-01` à `06` : recompter les lignes `FACTURE_NON_VENTILEE` et leur motif détaillé.
2. Comparer au recensement original (14 lignes, 01:2·02:9·03:1·04:2) : combien disparaissent
   (résolues par le fix Achat/Vente), combien restent (vraies anomalies de données Sage) ?
3. Produire un rapport court (`DOCS/AUDIT-TASK-143/01-reaudit-post-145.md` ou équivalent) listant,
   pour chaque ligne restante, le motif réel confirmé (pas de nouvelle investigation manuelle
   nécessaire si TASK-144/147 exposent déjà le diagnostic en ligne — réutiliser ces outils plutôt que
   des scripts ad hoc).
4. Mettre à jour l'écart d'équilibre global (−100 576,90 MAD) avec la valeur réelle après
   correction, si elle a changé.

## Garde-fous

- Lecture seule stricte, aucune écriture (même méthode que TASK-143 — pas de nouvelle règle de
  détection dupliquée, réutiliser `DiagnosticModal`/`DiagnostiquerLigneAsync` de TASK-144/147 si déjà
  livrées à ce stade de la nuit).
- Ne pas relancer de valorisation OM en masse pour cet audit — s'appuyer sur le cache déjà à jour
  après TASK-145.

## Files

- `DOCS/AUDIT-TASK-143/00-SYNTHESE-GLOBALE.md` (référence, ne pas modifier — le nouveau rapport est un fichier séparé).
- Nouveau : `DOCS/AUDIT-TASK-143/01-reaudit-post-145.md` (ou nom équivalent choisi par l'implémenteur).

## Validation

- [ ] TASK-145 confirmée livrée et vérifiée avant de commencer celle-ci.
- [ ] Les 14 lignes originales sont toutes recomptées (aucune omise silencieusement).
- [ ] Le rapport distingue explicitement « résolues par TASK-145 » vs « anomalie de données réelle
      restante ».
- [ ] L'écart d'équilibre global recalculé est cohérent avec la nouvelle liste de lignes restantes
      (même principe d'identité arithmétique que TASK-143).

## Dépendances / risques

- **Dépend strictement de TASK-145** — ne pas exécuter avant.
- Les lignes confirmées comme vraies anomalies après ce ré-audit alimentent le périmètre exact de
  TASK-150.
