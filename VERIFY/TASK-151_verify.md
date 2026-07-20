# VERIFY — TASK-151

Date: 2026-07-20
Agent: worker nocturne (Claude Code)

## BUILD

- Status: N/A — aucun code modifié (TASK diagnostique, lecture seule).

## FICHIERS MODIFIÉS

- Nouveau : `DOCS/AUDIT-TASK-143/03-if-manquant-zf-food.md` — rapport de cause racine.
- `DOCS/AUDIT-TASK-143/06-TVA1-2026-06.md` : **non modifié** (référence, conforme au garde-fou).

## DIFF RÉSUMÉ

Diagnostic (lecture seule) de l'IF manquant du tiers ZF FOOD (`TVA1-2026-06`, 3 lignes bloquantes
export XML). Vérifié : (1) le mapping colonne IF/ICE (`P_SOCIETE.SO_DecTvaColNameIdentifiantFrs=
'IF'`) fonctionne correctement — d'autres tiers ont bien un IF renseigné via ce même mapping ; (2)
requête directe sur la fiche tiers Sage réelle (`F_COMPTET`, `CT_Num=C0174`, base `NEW_EMA
DISTRIBUTION`) : la colonne `[IF]` est une **chaîne vide réelle** (`LEN=0`), pas une valeur non lue.
**Verdict : donnée Sage manquante (cas (a) de la TASK), pas un bug applicatif.**

## VALIDATION CHECKLIST

- [x] Cause racine identifiée et documentée : donnée Sage manquante (fiche tiers ZF FOOD, colonne
      `[IF]` vide), confirmé par requête directe sur `F_COMPTET`.
- [x] Rapport transmis (ce document) pour correction côté Sage — **aucun code modifié côté GRF**
      pour ce cas, conforme au garde-fou.
- [x] Mapping applicatif vérifié sain (non suspecté à tort) : d'autres tiers de la même base ont un
      IF correctement lu via la même configuration dynamique.

## RESTE À VALIDER (honnête, non silencieux)

- Aucun point technique restant. Reste une action **humaine hors périmètre code** : le PO/service
  comptable doit faire compléter l'IF de ZF FOOD dans Sage avant l'export de `TVA1-2026-06`.

## IMPACTS DÉTECTÉS

- Aucun — TASK strictement diagnostique, aucune écriture, aucun code touché.
