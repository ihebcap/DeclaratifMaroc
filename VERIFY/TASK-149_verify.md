# VERIFY — TASK-149

Date: 2026-07-20
Agent: worker nocturne (Claude Code)

## BUILD

- Status: N/A — TASK purement lecture seule (aucun code modifié).

## FICHIERS MODIFIÉS

- Nouveau : `DOCS/AUDIT-TASK-143/01-reaudit-post-145.md` — rapport de ré-audit complet.
- `DOCS/AUDIT-TASK-143/00-SYNTHESE-GLOBALE.md` : **non modifié** (conforme au garde-fou).

## DIFF RÉSUMÉ

Ré-audit des 14 lignes `FACTURE_NON_VENTILEE` originales (TASK-143) après le correctif TASK-145,
ligne par ligne, par requête SQL directe (lecture seule stricte). Résultat : **8/14 résolues**
(toutes portaient `DO_Domaine=0`/Vente, signature exacte du bug corrigé), **6/14 restent de vraies
anomalies** (4 en `DO_Domaine=1`/Achat jamais liées au bug, 1 en incohérence de montants
`DO_Domaine=0` mais ligne figée avant le correctif, 2 non re-vérifiables — déclaration
`TVA1-2026-01` disparue de la base pendant la nuit).

## VALIDATION CHECKLIST

- [x] TASK-145 confirmée livrée et vérifiée avant de commencer celle-ci (`VERIFY/TASK-145_verify.md`,
      build OK, purge documentée).
- [x] Les 14 lignes originales sont **toutes** recomptées individuellement (aucune omise) — voir
      tableau détaillé du rapport.
- [x] Le rapport distingue explicitement « résolues par TASK-145 » (8, toutes `DO_Domaine=0`) vs
      « anomalie de données réelle restante » (6) — avec la preuve (`DO_Domaine`, motif inchangé ou
      non) pour chaque ligne.
- [x] L'écart d'équilibre recalculé (`02: −24 462,00`, `03: −20 294,92`, `04: −6 651,99`,
      `05`/`06: 0,00`) est cohérent avec la liste de lignes restantes — identité arithmétique
      revérifiée par déclaration.

## RESTE À VALIDER (honnête, non silencieux)

1. **`TVA1-2026-01` a disparu de la base** entre l'audit TASK-143 et cette ré-vérification — fait
   constaté, cause **non investiguée** (hors périmètre lecture seule de cette TASK). Signalement
   explicite au PO nécessaire.
2. **`TVA1-2026-02` a été recréée/élargie** (730 → 2 005 lignes) pendant la même nuit, avant même
   cette TASK — l'attribution de la résolution des 8 lignes au correctif TASK-145 repose sur une
   preuve indirecte forte (100% des lignes résolues sont `DO_Domaine=0`, 0% des lignes
   `DO_Domaine=1` ne l'est) mais pas sur un avant/après contrôlé de bout en bout (pas de rejeu
   `RafraichirValorisationAsync` exécuté par cette TASK elle-même — l'état observé résulte
   d'activité antérieure dans la même session, non tracée précisément).
3. La ligne `EC_Id=24098`/`FA2502718` reste ambiguë : incohérence de montants sur une pièce
   `DO_Domaine=0`, mais figée dans une ligne `DM_LGTVA` créée avant le correctif TASK-145 — son
   verdict définitif nécessite un recalcul (mécanisme TASK-147) avant que TASK-150 ne puisse
   trancher aboutir/exclure en connaissance de cause.

## IMPACTS DÉTECTÉS

- Périmètre exact de TASK-150 clarifié : 5 `EC_Id` (`20650, 21473, 18198, 18199, 24098`) au lieu des
  14 initiaux, plus le signalement PO sur `TVA1-2026-01`.
