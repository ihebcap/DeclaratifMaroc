# TASK-234 — V2 compta : alertes système sur le paramétrage de trésorerie Sage (banque / rapprochement)

Status: 🆕 à faire
Priority: HIGH
Risk: MEDIUM
Module: Declaration.Compta.Reader + Declaration.Compta.Core + Declaration.API + declaration-tva-web
Dépend de : TASK-225 ; front = TASK-231 + TASK-232

> **Origine :** PO 16/10/2026 — « uniquement la banque, et le rapprochement doit être fait sur compte de trésorerie ; s'il y a un paramétrage autre que ça, afficher une alerte sur le système ».

## Décisions de conception

1. **Règle de référence** : le rapprochement ne concerne que la banque, porté par la ligne sur le **compte de trésorerie du journal** (`F_JOURNAUX.CG_Num`). Compte bancaire = préfixe configurable par dossier (défaut `514`, enregistré dans la configuration de TASK-225).
2. **Contrôles (lecture seule de `F_JOURNAUX`, `F_ECRITUREC`)**, chacun produit une alerte typée, avec dossier, journal, compte, volume concerné :
   - **A1** : journal `JO_Rappro = 2` dont le compte de trésorerie n'est pas bancaire (observé sur LOTREX : `CAIS`, `CA` en 516…, `CEP` en 5111…).
   - **A2** : journal bancaire (compte `514…`) avec `JO_Rappro ≠ 2` (rapprochement non activé).
   - **A3** : `EC_TresoPiece` renseigné sur une ligne dont le compte ≠ compte de trésorerie du journal.
   - **A4** : journal de type trésorerie sans compte de trésorerie renseigné.
   - **A5** : plusieurs journaux partageant le même compte de trésorerie (observé : `CAP`/`BQ1` en 51410000, `EAP`/`BQ4` en 51410003) — risque de rapprochement ambigu, à signaler (information, non bloquant).
3. **Niveau de gravité** : A1, A3, A4 = alerte **bloquante pour la clôture** tant que non acquittée ; A2 = avertissement ; A5 = information. L'acquittement est une action explicite, tracée (utilisateur, date, motif) et stockée dans la base `DeclaratifMaroc`.
4. **Visibilité système** : bandeau persistant en tête de l'application (charte xGR, composants TASK-231) tant qu'une alerte A1/A3/A4 non acquittée existe pour le dossier actif, plus un écran « Contrôle du paramétrage » listant chaque alerte (grille XGrid).
5. **Aucune correction automatique** et aucune écriture dans Sage : l'alerte décrit le constat et le paramètre à corriger côté Sage.
6. Exécuté à la sélection du dossier, à chaque chargement de déclaration, et sur demande (bouton « Relancer le contrôle »).

## Périmètre STRICT

- **Inclus** : les 5 contrôles, entité d'acquittement EF, endpoints (contrôler, lister, acquitter), bandeau, écran, tests sur fixtures et sur LOTREX / NEW_EMA (lecture seule).
- **Exclu** : correction du paramétrage Sage, avoirs, logique de prorata.

## Livrables

- Contrôles + persistance de l'acquittement + API + bandeau + écran + tests.

## Critères de validation

- LOTREX : A1 détecte `CAIS`, `CA`, `CEP` ; NEW_EMA : aucune alerte A1/A3 (`BQE` = 5141 cohérent), `CAI` n'est pas signalé (`JO_Rappro=0` sur compte 516, conforme).
- Une alerte bloquante empêche la clôture ; son acquittement la lève et reste tracé après redémarrage.
- Aucune requête d'écriture vers Sage. Build + lint + build front 0 erreur ; **checklist UI** ; preuve par critère datée dans le VERIFY.

## Files

- `Declaration.Compta.Reader/`, `Declaration.Compta.Core/`, `Declaration.API/Controllers/`, `declaration-tva-web/src/`.
- `DOCS/CADRAGE_V2_COMPTA_SOURCE_UNIQUE.md` §3 (constats LOTREX), TASK-227.
