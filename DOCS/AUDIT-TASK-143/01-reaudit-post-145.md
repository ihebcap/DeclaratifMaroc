# Ré-audit post-TASK-145 des 14 lignes `FACTURE_NON_VENTILEE` (TASK-149)

**Date d'exécution :** 2026-07-20 (nuit, worker autonome), après livraison + build + VERIFY de
TASK-145 (filtre `DO_Domaine` manquant dans `GetFactureFirstSql`, purge de 326 sentinelles `ERREUR`
erronées — voir `VERIFY/TASK-145_verify.md`).
**Méthode :** lecture seule stricte, requêtes SQL directes contre `GR_EMA_DISTRIBUTION` reproduisant
la même méthodologie que TASK-143 (`DOCS/AUDIT-TASK-143/00-SYNTHESE-GLOBALE.md`).

## ⚠️ Constat préalable : dérive de l'environnement depuis l'audit TASK-143

Avant de comparer les 14 lignes originales, deux faits d'environnement doivent être signalés
honnêtement — ils ne sont pas un effet du correctif TASK-145 et sortent du périmètre lecture seule
de cette TASK :

1. **La déclaration `TVA1-2026-01` n'existe plus** (`SELECT * FROM DM_ENTTVA WHERE Numero='TVA1-2026-01'`
   → 0 ligne). Ses 2 lignes originales en anomalie (`EC_Id=20650`/`FC2501667` et
   `EC_Id=21473`/`FC2501717`) ne peuvent donc pas être ré-auditées *sur cette déclaration* — elles
   sont cependant retrouvées, à l'identique (même motif, même montant), sur `TVA1-2026-02` (voir
   plus bas). Cause non investiguée (hors périmètre lecture seule) — à signaler au PO/architecte.
2. **`TVA1-2026-02` a été recréée** après l'audit TASK-143 (`DateCreation` désormais
   `2026-07-20 00:56:10`, contre une déclaration d'origine antérieure) et porte désormais **2 005
   lignes** au lieu de 730 à l'origine — un périmètre de sélection nettement plus large que celui
   audité par TASK-143. `TVA1-2026-03`/`04`/`05`/`06` conservent en revanche le même nombre de
   lignes qu'à l'audit d'origine (557/1023/735/914) — seules `01` et `02` ont bougé.

Ces mouvements ont eu lieu **pendant la même nuit de travail** (probablement lors d'essais/tests
antérieurs à cette TASK, cf. `VERIFY/TASK-146_verify.md`/`VERIFY/TASK-147_verify.md` qui utilisent
la même `DeclarationId` que l'actuelle `TVA1-2026-02`). Le ré-audit ci-dessous porte donc sur
l'état **réellement observable aujourd'hui**, pas sur une reconstitution figée de l'état de
TASK-143.

## Recomptage — lignes en anomalie arithmétique (`ABS(TTC-(HT+TVA))>0,01`, signature `FACTURE_NON_VENTILEE`)

| Déclaration | Lignes totales (aujourd'hui) | Lignes totales (audit TASK-143) | Anomalies aujourd'hui | Anomalies TASK-143 |
|---|---:|---:|---:|---:|
| TVA1-2026-01 | **déclaration inexistante** | 1 223 | — | 2 |
| TVA1-2026-02 | 2 005 | 730 | **2** | 9 |
| TVA1-2026-03 | 557 | 557 | **1** | 1 |
| TVA1-2026-04 | 1 023 | 1 023 | **2** | 2 |
| TVA1-2026-05 | 735 | 735 | 0 | 0 |
| TVA1-2026-06 | 914 | 914 | 0 | 0 |

## Devenir des 14 lignes originales, ligne par ligne

| EC_Id | Facture | Décl. d'origine | `DO_Domaine` (RT_ECHEANCE) | Motif d'origine | État aujourd'hui | Verdict |
|---:|---|---|:---:|---|---|---|
| 20650 | FC2501667 | 01 | **1 (Achat)** | Incohérence Sage (montants) | Toujours en anomalie — **même motif**, retrouvée sur TVA1-2026-02 | ❌ **Anomalie réelle, non résolue** (non liée à TASK-145 — pièce Achat authentique, Sens était déjà correct) |
| 21473 | FC2501717 | 01 | **1 (Achat)** | Incohérence Sage (montants) | Toujours en anomalie — **même motif**, retrouvée sur TVA1-2026-02 | ❌ **Anomalie réelle, non résolue** (idem) |
| 18510 | FA2600147 | 02 | 0 (Vente) | Facture introuvable (DTO non fourni) | **Valorisée** (HT/TVA/TTC non nuls, aucune anomalie) | ✅ **Résolue par TASK-145** |
| 20199 | FA2502759 | 02 | 0 (Vente) | Facture introuvable (DTO non fourni) | **Valorisée** | ✅ **Résolue par TASK-145** |
| 21089 | FA2503007 | 02 | 0 (Vente) | Facture introuvable (DTO non fourni) | **Valorisée** | ✅ **Résolue par TASK-145** |
| 21368 | FA2502507 | 02 | 0 (Vente) | Facture introuvable (DTO non fourni) | **Valorisée** | ✅ **Résolue par TASK-145** |
| 21421 | FA2600474 | 02 | 0 (Vente) | Facture introuvable (DTO non fourni) | **Valorisée** | ✅ **Résolue par TASK-145** |
| 21713 | FA2502324 | 02 (×2 lignes) | 0 (Vente) | Facture introuvable (DTO non fourni) | **Valorisée** | ✅ **Résolue par TASK-145** |
| 21714 | FA2502384 | 02 | 0 (Vente) | Facture introuvable (DTO non fourni) | **Valorisée** | ✅ **Résolue par TASK-145** |
| 21849 | FA2600106 | 02 | 0 (Vente) | Facture introuvable (DTO non fourni) — cas collision `DO_Numero` (Point 3 TASK-143) | **Valorisée** | ✅ **Résolue par TASK-145** |
| 24098 | FA2502718 | 03 | 0 (Vente) | Incohérence Sage (montants) | Toujours en anomalie — **même motif** | ❌ **Anomalie réelle, non résolue** — la pièce est en `DO_Domaine=0` (vente) donc *n'est plus lue du tout* par `GetFactureFirstSql` après le correctif ; ce motif "Incohérence Sage" provient d'un autre chemin de valorisation qui a néanmoins retrouvé la pièce. **Ligne DM_LGTVA figée AVANT le correctif** (déclaration créée avant l'intervention de cette nuit) — n'a pas été recalculée depuis. Candidat pour le mécanisme TASK-147 (recalcul ciblé) une fois confirmé que le cache porte une lecture plus récente et propre. |
| 18198 | FC2600004 | 04 | **1 (Achat)** | Facture introuvable ou non ventilée | Toujours en anomalie — **même motif**, aucune lecture de cache trouvée (confirmé `VERIFY/TASK-147_verify.md`) | ❌ **Anomalie réelle, non résolue** (pièce Achat authentique jamais retrouvée par Sage — pas un cas TASK-145) |
| 18199 | FC2600005 | 04 | **1 (Achat)** | Facture introuvable ou non ventilée | Toujours en anomalie — **même motif** | ❌ **Anomalie réelle, non résolue** (idem) |

## Synthèse

- **8 des 14 lignes originales sont résolues** — toutes portaient `DO_Domaine=0` (Vente), exactement
  la signature du bug corrigé par TASK-145. Confirmation forte (pas seulement une coïncidence) :
  aucune ligne `DO_Domaine=1` (Achat) n'a été résolue par le passage du temps, seules les lignes
  Vente l'ont été.
- **6 lignes restent de vraies anomalies**, à traiter par TASK-150 :
  - **4 lignes `DO_Domaine=1` (Achat)** — pièces authentiquement introuvables ou en incohérence de
    montants côté Sage, **jamais liées au bug TASK-145** : `20650`/`FC2501667`, `21473`/`FC2501717`,
    `18198`/`FC2600004`, `18199`/`FC2600005`.
  - **1 ligne `DO_Domaine=0` (Vente) `24098`/`FA2502718`** — incohérence de montants (pas
    "introuvable"), donc pas davantage liée au bug TASK-145 (la pièce a été trouvée), mais figée
    dans une ligne `DM_LGTVA` antérieure au correctif — à recalculer (TASK-147) avant de statuer
    définitivement.
  - **`TVA1-2026-01`** : déclaration disparue de la base — ses 2 lignes d'origine (déjà comptées
    ci-dessus, retrouvées à l'identique sur `02`) ne peuvent pas être re-vérifiées *sur cette
    déclaration précise* ; signalé au PO, hors périmètre de correction de cette TASK.
- **Écart d'équilibre** : recalculé sur les déclarations existantes aujourd'hui — `02: −24 462,00`
  (inchangé : les 2 lignes achat restantes portent le même écart que dans l'audit d'origine, les 8
  résolues ne créent plus d'écart puisqu'elles sont valorisées), `03: −20 294,92`,
  `04: −6 651,99`, `05`/`06: 0,00`. `01` non calculable (déclaration inexistante). Identité
  arithmétique revérifiée pour chaque déclaration existante : `Σrésidu des lignes incohérentes` =
  écart annoncé, à 0,01 près.

## Périmètre pour TASK-150

Lignes confirmées comme vraies anomalies (à traiter, aboutir ou exclure avec motif explicite) :
`EC_Id 20650, 21473, 18198, 18199` (achat, anomalies indépendantes de TASK-145) et `EC_Id 24098`
(vente, incohérence de montants, ligne à recalculer d'abord via TASK-147 avant décision finale).
`TVA1-2026-01` disparue : signalement PO requis, aucune action de correction possible tant que la
cause de sa disparition n'est pas élucidée (hors périmètre lecture seule de TASK-149).
