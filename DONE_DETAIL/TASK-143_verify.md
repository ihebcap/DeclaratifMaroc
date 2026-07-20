# VERIFY — TASK-143 — Audit exhaustif des 6 déclarations contre les données réelles Sage

## Nature
Audit **lecture seule stricte** (diagnostic, aucun correctif). **Aucun fichier de code de production
modifié.** Livrables produits (documentation) :
- `DOCS/AUDIT-TASK-143/00-SYNTHESE-GLOBALE.md` (synthèse + agrégations par code)
- `DOCS/AUDIT-TASK-143/01..06-TVA1-2026-0X.md` (une fiche par déclaration)

## Méthode & preuves
Requêtes `SELECT` uniquement sur `GR_EMA_DISTRIBUTION` (persistance + ERP) et `NEW_EMA DISTRIBUTION`
(Sage `F_DOCREGL`), serveur `DESKTOP-5BFKKEP`. Scripts d'audit dans le scratchpad de session
(`survey.ps1`, `survey2.ps1`, `anomalies.ps1`, `collision.ps1`, `docregl.ps1`, `dt_aff.ps1`,
`tiers.ps1`, `sel2.ps1`, `final.ps1`). Logique du checkup reproduite à l'identique en SQL à partir de
`DeclarationsController.GetCheckup` (l.216+) et `DeclarationWorkflowService.GetCheckupAsync` (l.723+).

> **Limite tracée :** l'endpoint `/api/declarations/{id}/checkup` (port 5280) n'a pas pu être appelé —
> l'instance tourne avec une clé JWT capturée au démarrage, non rejouable depuis les `connections.json`
> disponibles ; forcer l'auth aurait exigé un redémarrage (hors périmètre lecture seule). Contournement
> retenu : agrégation directe sur les données **persistées** (plus auditable). Seule l'alerte
> `REGLEMENT_EXCLU` (re-sélection Sage OM en direct) n'est pas reproduite individuellement — elle est
> **quantifiée** (32 règlements sélectionnés sans ligne, point 5).

## Checklist VALIDATION (task)

- [x] **6 déclarations couvertes, aucune des 5 182 lignes exclue sans justification.**
  Comptage vérifié : 1223+730+557+1023+735+914 = 5 182 (= `SELECT DeclarationId,COUNT(*) FROM DM_LGTVA`).
  Toutes les lignes sont à l'état *Proposée* (Etat=0) → toutes dans `lignesRecap` du checkup.
- [x] **Agrégation par code d'alerte, par déclaration + cumul.**
  `FACTURE_NON_VENTILEE`=14 (01:2,02:9,03:1,04:2,05:0,06:0) ; `TIERS_SANS_IF`=3 (06) ;
  `TIERS_SANS_ICE`=0 ; `AUCUNE_LIGNE_INTEGREE`=0 ; `LIGNE_EXCLUE`=0 ; `REGLEMENT_EXCLU`=32 règlements.
- [x] **Chaque incohérence arithmétique expliquée par son motif réel.**
  14 lignes `ABS(TTC−(HT+TVA))>0,01` = exactement les 14 lignes à `MotifRejet` non vide.
  `ecartExplique` = vrai sur les 6 (résidu des lignes incohérentes = écart d'équilibre annoncé).
- [x] **Contrôle "numéro de pièce dupliqué entre tiers" sur tous les EC_Id référencés.**
  Exécuté sur 2 460 `EC_Id` distincts (0 NULL). 1 seul cas : `FA2600106`
  (EC_Id 21849/CT_No 166 avec doc `F_DOCREGL` DR_No=4947 — référencé ; EC_Id 18608/CT_No 188 orphelin,
  0 `F_DOCREGL`, non référencé). Verdict `F_DOCREGL` fourni pour chaque échéance. Aucune collision
  "2 documents valides".
- [x] **Contrôle DT_Id exécuté, aucune anomalie non tracée.**
  `RT_AFFECTATION` : 3 207 lignes, 0 tampon DT_Id. `DM_ENTTVA` : 0 DT_Id. Cohérent (6 déclarations
  *En cours*, jamais clôturées).
- [x] **Rapport livré (une fiche par déclaration + synthèse), lisible par un comptable, chiffres vérifiables.**
  7 fichiers Markdown, chaque anomalie citée avec EC_Id / n° facture / tiers / montant / motif.
- [x] **Aucune écriture en base, aucun appel de génération XML/déclaration.**
  Uniquement des `SELECT`. Aucun `INSERT/UPDATE/DELETE`, aucun appel worker OM en écriture, aucun XML.
  `RT_AFFECTATION.DT_Id` non touché (verrou TASK-028/064 préservé).

## Résultat de l'audit (résumé)
- **Aucune des 6 déclarations n'est signable/exportable en l'état.**
- 14 lignes non valorisées (HT retenu, TVA/TTC=0) sur 01-04 → écart global **−100 576,90 MAD**,
  100 % expliqué.
- TVA1-2026-06 : 3 lignes ZF FOOD sans IF → export XML bloqué.
- TVA1-2026-05 : seule déclaration propre sur tous les contrôles automatiques.
- 3 pistes de correction identifiées → **TASK distinctes** (hors périmètre de cet audit-diagnostic).

**Statut : À REVOIR (APPROVE attendu — diagnostic complet, lecture seule respectée).**
