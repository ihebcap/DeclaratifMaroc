# TASK-151 — Compléter l'Identifiant Fiscal (IF) manquant du tiers ZF FOOD (bloque l'export XML de TVA1-2026-06)

Status: 🆕 à faire
Priority: MEDIUM
Risk: LOW (donnée de configuration tiers, pas de logique de calcul)
Module: Données Sage / GRF (tiers) — pas nécessairement du code applicatif

> **Origine :** TASK-143 — anomalie confirmée #2 : `TVA1-2026-06` est arithmétiquement équilibrée
> mais 3 lignes du tiers **ZF FOOD** n'ont pas d'Identifiant Fiscal renseigné, ce qui bloque l'export
> XML de la déclaration (le format DGI exige un IF valide par ligne).

## Objectif

1. Confirmer par requête (lecture seule) quelles lignes exactement portent ce tiers sans IF
   (`F_COMPTET`/champ IF du tiers, ou équivalent selon la structure Sage — à identifier précisément,
   TASK-143 n'a fait que constater le blocage, pas investigué la table exacte).
2. Déterminer si l'IF de ZF FOOD est : (a) simplement absent de la fiche tiers Sage (correction de
   données, pas de code) ; ou (b) présent en base mais mal lu/mappé côté application (auquel cas
   c'est un bug de code, périmètre à documenter séparément).
3. Si (a) : documenter clairement dans le rapport que la correction doit se faire **côté Sage**
   (fiche tiers), pas dans le code de l'application — ne pas contourner par un champ codé en dur
   côté GRF.
4. Si (b) : ouvrir le correctif de code nécessaire avec preuve (requête SQL montrant que l'IF existe
   bien en base mais n'est pas remonté).

## Garde-fous

- Lecture seule pour le diagnostic.
- Ne jamais insérer une valeur d'IF arbitraire/placeholder dans le code ou en base pour "débloquer"
  l'export — l'IF est une donnée légale, une valeur incorrecte serait pire qu'un blocage.

## Files

- À déterminer selon (a)/(b) — commencer par une requête sur `F_COMPTET`/tiers Sage pour le tiers ZF FOOD.
- `DOCS/AUDIT-TASK-143/06-TVA1-2026-06.md` (référence du constat original, ne pas modifier).

## Validation

- [ ] Cause racine identifiée et documentée (donnée Sage manquante vs bug de mapping applicatif).
- [ ] Si donnée Sage manquante : rapport transmis au PO pour correction côté Sage, pas de code modifié côté GRF pour ce cas.
- [ ] Si bug applicatif : correctif livré avec preuve avant/après (export XML de TVA1-2026-06 ne bloque plus sur ce motif).

## Dépendances / risques

- Indépendante des autres TASKs de cette liste — peut être traitée en parallèle.
- Risque : si la cause est réellement côté Sage (donnée manquante), cette TASK ne peut pas être
  "complétée" par un worker de code — elle doit se terminer par un rapport clair, pas par du code
  produit à tout prix.
