# AUDIT EXHAUSTIF — 6 déclarations TVA 2026 (TASK-143)

**Périmètre :** société SO_Id=1 (`NEW_EMA DISTRIBUTION`), déclarations `TVA1-2026-01` → `TVA1-2026-06`,
toutes au statut **En cours** (Statut=0). **5 182 lignes** au total (`DM_LGTVA`).
**Nature :** lecture seule stricte (aucune écriture, aucune génération XML).
**Sources :** base GRF `GR_EMA_DISTRIBUTION` (persistance + ERP) et base Sage `NEW_EMA DISTRIBUTION`
(`F_DOCREGL`), serveur `DESKTOP-5BFKKEP`.
**Date d'exécution :** 2026-07-20.

> **Note méthode.** L'agrégation a été produite directement sur les données **persistées** (`DM_LGTVA`,
> `DM_ENTTVA`, `RT_ECHEANCE`, `RT_AFFECTATION`, `F_DOCREGL`) plutôt que via l'endpoint
> `/api/declarations/{id}/checkup`. L'instance API en cours d'exécution (port 5280) a été démarrée avec
> une clé de signature JWT capturée au démarrage, non rejouable depuis les `connections.json` disponibles ;
> l'authentification aux endpoints protégés n'était donc pas possible sans redémarrer le service (hors
> périmètre lecture seule). La logique du checkup a été **reproduite à l'identique** en SQL à partir du
> code lu (`DeclarationsController.GetCheckup` l.216+ et `DeclarationWorkflowService.GetCheckupAsync`
> l.723+) : mêmes ensembles de lignes, mêmes formules d'équilibre et d'incohérence. Chaque chiffre
> ci-dessous est vérifiable par requête SQL directe. La seule classe d'alerte non reproductible hors
> API vivante (`REGLEMENT_EXCLU`, issue d'une re-sélection Sage OM en direct) est **quantifiée** au
> point 5 par comparaison sélection ↔ lignes.

---

## Verdict comptable

| Déclaration | Lignes | Lignes propres | Anomalies (bloquantes) | Écart d'équilibre | Signable en l'état ? |
|-------------|-------:|---------------:|-----------------------:|------------------:|:--------------------:|
| TVA1-2026-01 | 1 223 | 1 221 | 2 | −24 462,00 | ❌ NON |
| TVA1-2026-02 |   730 |   721 | 9 | −49 167,99 | ❌ NON |
| TVA1-2026-03 |   557 |   556 | 1 | −20 294,92 | ❌ NON |
| TVA1-2026-04 | 1 023 | 1 021 | 2 |  −6 651,99 | ❌ NON |
| TVA1-2026-05 |   735 |   735 | 0 |       0,00 | ⚠️ OUI (arithmétiquement équilibrée) |
| TVA1-2026-06 |   914 |   911 | 0 (+3 IF manquant) |       0,00 | ⚠️ NON (3 lignes IF manquant, bloquant export) |
| **TOTAL** | **5 182** | **5 165** | **14 + 3** | **−100 576,90** | — |

**Conclusion :** aucune des 6 déclarations n'est signable/exportable en l'état.
- **4 déclarations (01-04)** portent **14 lignes** dont la valorisation TVA a échoué : le HT reste
  affecté mais TVA=0 / TTC=0. Ces 14 lignes expliquent **à 100 %** l'écart d'équilibre global de
  **−100 576,90 MAD** (aucune ligne silencieuse).
- **TVA1-2026-06** est arithmétiquement équilibrée mais contient **3 lignes du tiers ZF FOOD sans
  Identifiant Fiscal** → l'export XML DGI serait bloqué (`ValiderPourExport`).
- **TVA1-2026-05** est la seule totalement propre sur les contrôles automatiques.

---

## Point 1 — Agrégation des alertes par code (comble TASK-060)

Toutes les lignes sont à l'état **Proposée** (Etat=0) : aucune ligne Intégrée, Exclue, Reportée ni
Écartée. Le checkup évalue donc l'ensemble des 5 182 lignes comme éligibles (`lignesRecap`).

| Code d'alerte | Niveau | Nb lignes | Répartition |
|---------------|--------|----------:|-------------|
| `FACTURE_NON_VENTILEE` | 🔴 bloquant | **14** | 01:2 · 02:9 · 03:1 · 04:2 · 05:0 · 06:0 |
| `TIERS_SANS_IF` | 🔴 bloquant (export) | **3** | 06:3 (tiers ZF FOOD) |
| `TIERS_SANS_ICE` | 🔴 bloquant | 0 | — (100 % des ICE valides, 15 car.) |
| `AUCUNE_LIGNE_INTEGREE` | 🔴 bloquant | 0 | — (toutes les déclarations ont des lignes) |
| `LIGNE_EXCLUE` | ⚪ info | 0 | — (aucune ligne à l'état Exclue) |
| `REGLEMENT_EXCLU` | 🟠 avert. | 32 (règlements) | voir Point 5 — sélectionnés mais non déclarés |

Détail des 14 `FACTURE_NON_VENTILEE` par motif réel :

| Motif réel (persisté dans `MotifRejet`) | Nb | Déclarations |
|-----------------------------------------|---:|--------------|
| Facture introuvable (DTO non fourni) — OM Sage muet | 9 | 02 |
| Facture introuvable ou non ventilée | 2 | 04 |
| Incohérence Sage : Σ(HT+TVA+Parafiscale) ≠ TTC — pièce exclue de la valorisation | 3 | 01 (×2), 03 (×1) |

---

## Point 2 — Cohérence arithmétique (TTC = HT + TVA)

14 lignes en incohérence arithmétique (`ABS(TTC−(HT+TVA)) > 0,01`). **Elles coïncident exactement**
avec les 14 lignes `FACTURE_NON_VENTILEE` — donc **chaque incohérence est expliquée par son motif réel**,
jamais un écart chiffré sans cause. Sur ces 14 lignes, TVA=0 et TTC=0 alors que HT>0 (valorisation
échouée, HT affecté conservé). Le résidu d'une ligne vaut donc `−HT`.

| Déclaration | Nb incohérentes | Σ résidu (= écart d'équilibre) | ecartExplique |
|-------------|----------------:|-------------------------------:|:-------------:|
| 01 | 2 | −24 462,00 | ✅ vrai |
| 02 | 9 | −49 167,99 | ✅ vrai |
| 03 | 1 | −20 294,92 | ✅ vrai |
| 04 | 2 |  −6 651,99 | ✅ vrai |
| 05 | 0 |       0,00 | ✅ vrai |
| 06 | 0 |       0,00 | ✅ vrai |
| **Global** | **14** | **−100 576,90** | ✅ vrai |

Identité vérifiée : `ΣTTC − (ΣHT + ΣTVA) = 14 523 193,78 − (12 743 528,93 + 1 880 241,75) = −100 576,90`,
égal à `−Σ HT des 14 lignes en anomalie`. Le détail ligne à ligne est dans chaque fiche déclaration.

---

## Point 3 — Nouveau contrôle : numéro de pièce Sage dupliqué entre tiers

Contrôle exécuté sur **l'ensemble des 2 460 `EC_Id` distincts** référencés par les 5 182 lignes
(aucun `EC_Id` NULL). Groupement `RT_ECHEANCE` par `DO_Numero` + `DO_Domaine` ayant
`COUNT(DISTINCT CT_No) > 1`.

**1 seul cas trouvé : `FA2600106`** (le cas diagnostiqué manuellement — confirmé unique).

| EC_Id | EC_No | CT_No | Tiers | EC_Etat | Montant | Doc `F_DOCREGL` (jointure `EC_No = DR_No`) | Référencé par déclaration ? |
|------:|------:|------:|-------|:-------:|--------:|:------------------------------------------:|:---------------------------:|
| 18608 | 1172 | 188 | LA MAROCAINE DE GESTION ET SOU… | 0 | 79 206,79 | **AUCUN (orphelin)** | ❌ non (0 ligne) |
| 21849 | 4947 | 166 | LA BUVETTE DU MAROC | 1 | 79 206,79 | ✅ DR_No=4947, DO_Piece=FA2600106, 16/01/2026 | ✅ oui (5 lignes, TVA1-2026-02) |

**Verdict :** collision réelle de `DO_Numero` entre 2 tiers, avec **une échéance orpheline**
(EC_Id=18608, aucun document Sage) — **PAS** un cas de « 2 documents valides des deux côtés ».
Point important : **la déclaration référence la bonne échéance** (21849, celle qui a un document Sage
réel). L'échéance orpheline n'est utilisée par aucune ligne. Le module a donc résolu correctement la
collision. L'anomalie `FA2600106` (ligne EC_Id=21849, HT=25 206,79, TVA1-2026-02) provient d'un
**échec de valorisation OM distinct** (« DTO non fourni »), pas d'une confusion de tiers.

> Risque résiduel signalé au PO : ce module de rapprochement se fonde sur `DO_Numero` ; tant qu'une
> collision inter-tiers existe côté Sage, une future affectation pourrait pointer la mauvaise échéance.
> Correctif éventuel (affichage/désambiguïsation du tiers) = TASK distincte (hors périmètre audit).

---

## Point 4 — Contrôle du tampon DT_Id (verrou de déclaration)

- `RT_AFFECTATION` : 3 207 affectations, **0 avec tampon DT_Id** (aucune valeur non NULL).
- `DM_ENTTVA` : **0 déclaration** avec DT_Id posé.
- Aucun tampon orphelin, aucune collision de DT_Id entre déclarations.

**Cohérent** : les 6 déclarations sont *En cours* (jamais clôturées), donc aucun verrou n'a été posé
(TASK-028/064). Rien à signaler.

---

## Point 5 — Contrôle règlements / affectations (exclusivité inter-déclaration)

- **Aucun `NumeroRapprochement` n'apparaît sur plus d'une déclaration** → aucune double-déclaration.
- Toutes les 5 182 lignes portent un `NumeroRapprochement` (aucune ligne sans règlement).

| Déclaration | Règlements sélectionnés | Règlements avec lignes | Sélectionnés SANS ligne (exclus) |
|-------------|------------------------:|-----------------------:|---------------------------------:|
| 01 | 145 | 144 | 1 |
| 02 | 126 | 125 | 1 |
| 03 | 101 |  95 | 6 |
| 04 | 169 | 159 | 10 |
| 05 | 144 | 138 | 6 |
| 06 | 225 | 217 | 8 |
| **Total** | **910** | **878** | **32** |

Les **32 règlements sélectionnés mais non déclarés** correspondent à l'advisory `REGLEMENT_EXCLU` :
règlements écartés à la valorisation (hors périmètre / non éligibles). Conformément à la règle projet,
une exclusion = « règlement simplement pas déclaré cette période », **non bloquant**. Ils sont listés
pour transparence, pas comme un défaut à réparer.

---

## Point 6 — Qualité des tiers (ICE / IF)

- **ICE : 100 % conforme** — les 5 182 lignes ont un ICE de 15 caractères, sans espace, non vide.
- **IF : 5 179 / 5 182 conformes** (8 caractères). **3 lignes sans IF**, toutes du même tiers :

| Tiers | ICE | Nb lignes | Déclaration | Impact |
|-------|-----|----------:|-------------|--------|
| **ZF FOOD** | 003809780000075 | 3 | TVA1-2026-06 | 🔴 IF vide → **export XML bloqué** (`ValiderPourExport`) |

Un seul fournisseur à dossier incomplet (IF manquant). À régulariser avant clôture/export de la
déclaration de juin.

---

## Anomalies confirmées → TASK de correction à ouvrir (hors périmètre de cet audit)

1. **14 lignes non valorisées (FACTURE_NON_VENTILEE)** sur 01-04 : faire aboutir la valorisation OM
   (facture introuvable / incohérence Sage) ou décider de l'exclusion — sinon écart d'équilibre
   persistant de −100 576,90 MAD.
2. **ZF FOOD sans IF** (TVA1-2026-06) : compléter l'IF fournisseur.
3. **Collision `DO_Numero` FA2600106** : désambiguïsation du tiers dans le rapprochement (préventif —
   la résolution actuelle est correcte, mais le risque structurel demeure).

*Fin de la synthèse. Détail par déclaration : fichiers `01`…`06` de ce dossier.*
