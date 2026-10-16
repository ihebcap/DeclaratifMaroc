# TASK-229 — V2 compta : second flux — écritures sur comptes de TVA non rattachées à un tiers (dépenses, frais bancaires, autres)

Status: 🆕 à faire
Priority: MEDIUM
Risk: HIGH (risque d'omission de TVA, famille du bug « frais bancaires » TASK-193/196/197)
Module: Declaration.Compta.Reader + Declaration.Compta.Core + Declaration.API + declaration-tva-web
Dépend de : TASK-226, TASK-228 ; **front = TASK-231 + TASK-232** (grille XGrid, charte xGR)

> **Origine :** PO 16/10/2026 : « un autre flux : on doit contrôler les comptes de taxe qui ne sont pas rattachés à des tiers » (dépenses caisse, frais bancaires avec TVA, OD).

## Décisions de conception

1. **Compte de taxe** = tout compte listé dans `F_TAXE.CG_Num` (déductible `TA_Sens=0`, collecté `TA_Sens=1`).
2. **Flux** = lignes de ces comptes dont la pièce ne comporte **aucune ligne tiers** (`CT_Num` vide partout) : dépense de caisse, frais bancaires, OD de régularisation. Les lignes de pièces **avec** tiers sont traitées par TASK-226/227, jamais ici (aucun double comptage : invariant testé).
3. Chaque pièce de ce flux est **décomposée comme une facture** (HT par `TA_Code` de la charge + TVA sur `F_TAXE.CG_Num`) et passe par le contrôle HT × taux de TASK-228.
4. **Pas de règlement ni de lettrage** : date de déclaration = date de l'opération (`JM_Date + EC_Jour − 1`) — dépenses/frais bancaires « selon la date de la dépense/du frais » (V1 §1quater). Pour un frais bancaire : si le journal exige un rapprochement (`JO_Rappro=2`), la ligne de banque doit porter `EC_TresoPiece`, sinon « à rapprocher » (même règle que TASK-227).
5. **Pièces non décomposables** (TVA sans charge identifiable, TVA sans `TA_Code`, plusieurs taux ambigus) → **alerte listée**, jamais ignorées : le but du flux est de ne rien laisser passer en silence.
6. **Sélection/inclusion** : par défaut incluses dans la déclaration (comme V1 TASK-197) mais **visibles** dès l'écran de contrôle (reprend l'envie PO ROADMAP « afficher Frais bancaires et Dépenses »). Exclusion manuelle d'une pièce possible, avec motif tracé (à confirmer PO).
7. Identité fiscale : pas de tiers → IF/ICE non requis pour ces lignes ; le mode de paiement XML (`<mp>`) suit la règle V1 §3 (espèce=1, opération bancaire=3) — à confirmer PO pour les OD.

## Périmètre STRICT

- **Inclus** : détection des pièces sans tiers touchant un compte de taxe (2 sens), décomposition, intégration au contrôle de TASK-228, liste d'alertes, grille de contrôle, tests sur fixtures + bases réelles (lecture seule).
- **Exclu** : création/édition d'écritures dans Sage (interdit), export XML (TASK-230).

## Livrables

- Lecteur du flux + règles + grille de contrôle + tests.

## Critères de validation

- Aucune pièce de ce flux ne contient de tiers ; aucune pièce de TASK-226/227 n'y figure (requête de preuve sur NEW_EMA et LOTREX).
- Σ TVA du flux + Σ TVA des factures avec tiers = Σ mouvements des comptes de taxe de la période (contrôle d'équilibre inter-comptes, écart 0,00 ou liste exhaustive des écarts).
- Une pièce non décomposable apparaît en alerte (jamais disparue).
- Build + lint 0 erreur ; **checklist UI `DOCS/UI_STANDARDS.md`** ; preuve par critère datée.

## Files

- `Declaration.Compta.Reader/`, `Declaration.Compta.Core/`, `declaration-tva-web/src/` (lire `DOCS/UI_STANDARDS.md`).
- `TODO.md` ROADMAP « Afficher les Frais bancaires (et Dépenses) dans l'onglet Sélection ».
