# TASK-025_verify — Cadrage TVA du solde initial (`EC_Type = 4`)

> Statut : **APPROUVÉ (09/07/2026) — Décision PO = maintenir l'alerte (statu quo)**  
> Base interrogée : `GR_EMA_DISTRIBUTION` (serveur `.\sql2022`)  
> Date investigation : 2026-07-09

---

## 1. Inventaire réel des lignes `EC_Type = 4`

### 1.1 Volume global

| Métrique | Valeur |
|---|---|
| Nombre de lignes | **34** |
| EC_Id distincts | **34** (1 ligne = 1 EC) |
| Montant total | **854 793,29 MAD** |
| Montant min | 107,96 MAD |
| Montant max | 81 683,85 MAD |
| Société (`SO_Id`) | **1 uniquement** |
| `DO_Type` | **99 uniquement** |

### 1.2 Deux natures distinctes au sein du type 4

| Nature | Préfixe `DO_Numero` | Nb | Montant total | Dates |
|---|---|---|---|---|
| **Coffre fin d'exercice** | `S-COF25XXXXX` | **33** | 854 685,33 MAD | 2025-04-14 → 2025-12-18 |
| **Solde initial "pur"** | `SI- 4411ELEC` | **1** | 107,96 MAD | 2026-05-06 |

> La terminologie de la tâche ("solde initial") couvrait en réalité **deux sous-types très différents**.

---

## 2. Analyse par sous-type

### 2.1 Coffres fin d'exercice (`S-COF25XXXXX`) — 33 lignes / 854 685 MAD

**Nature comptable :**
- `EC_Commentaire` systématiquement = `"Coffre 31/12/2025 # RCxxxx"` → ce sont des **soldes de caisse repris en à-nouveau au 31/12/2025**, matérialisant le solde d'un encaissement client en attente de lettrage définitif.
- CT_Code = client GRF (C0018, C0033, C0065, C0096, C0158, C0160, C0164, C0209, C0211, C0241…)
- `DO_Type = 99` = type « interne GRF », pas Sage.

**Champ TVA :**
- **0 ligne `RT_HISTCOMPTA` rattachée** (`MV_Id = EC_Id`) → pas de ventilation TVA en SQL.
- Via leur affectation (`RT_AFFECTATION`), le mouvement lié (`RT_MOUVEMENT`) affiche `MV_Tva = 0`, `MV_TvaMontant = 0,00`, `MV_TauxTva = 0,00` → **pas de TVA enregistrée côté règlement**.
- Compte général utilisé dans `RT_HISTCOMPTA` du mouvement : `3421000000` (client) et `5111100000`/`5141000000` (trésorerie) → **comptes de trésorerie / client, jamais de compte TVA**.
- Ces lignes sont des **règlements / encaissements clients** (créances), pas des factures. La TVA est portée par les factures d'origine (EC_Type=0 ou 111), déjà intégrées via TASK-022.

**Conclusion :** Les coffres `EC_Type=4` ne portent **aucune TVA déclarable**. Ce sont des à-nouveaux de trésorerie, comptablement hors champ déclaration TVA achat/vente.

### 2.2 Solde initial pur (`SI- 4411ELEC`) — 1 ligne / 107,96 MAD

**Nature comptable :**
- `EC_Commentaire = "FRAIS DE RETARD"` — libellé cohérent avec TASK-022 (exemple cité : frais de retard).
- `CT_Code = 4411ELEC`, `CT_Intitule = SRM-CS` → tiers fournisseur.
- `EC_Etat = 0`, `EC_Solde = 107,96` → **non soldée**, non affectée (`RT_AFFECTATION` : 0 ligne).
- Créée le 2026-05-06 (récente).

**Champ TVA :**
- **0 ligne `RT_HISTCOMPTA`** rattachée.
- Non présente dans `RT_AFFECTATION` → **n'est pas éligible** à la déclaration TVA (ne figure dans aucune sélection d'affectation).
- Les frais de retard sont en règle générale **hors champ TVA** (intérêts moratoires = flux financier, pas livraison de bien/service).
- Montant : 107,96 MAD → impact négligeable même si la règle changeait.

**Conclusion :** Hors champ TVA déclarable, et de toute façon **non affectée** donc invisible dans le flux de déclaration actuel.

---

## 3. Présence dans les déclarations TVA passées

Aucune ligne `EC_Type=4` ne figure dans `RT_LigneDeclarationTva` (vérifié via `DTL_EntityId`/`DTL_MvNumero`).

**Confirmé : 0 ligne EC_Type=4 jamais déclarée TVA.**

---

## 4. Réponses aux questions de TASK-025

### Q1 — Les lignes `EC_Type = 4` portent-elles de la TVA déclarable ?

**Non.**
- **Coffres (33/34) :** à-nouveaux de trésorerie client, montants bruts, pas de TVA dans `RT_MOUVEMENT` ni dans `RT_HISTCOMPTA`.
- **SI- (1/34) :** frais de retard fournisseur, hors champ TVA, non affecté.

### Q2 — Où est le détail TVA si oui ?

**Question caduque** (cf. Q1 : pas de TVA). Pour mémoire : `RT_HISTCOMPTA` est vide pour ces 34 lignes ; le mouvement lié (coffres) ne porte pas de TVA.

### Q3 — Comportement cible : intégrer, exclure avec motif, ou reporter ?

**Exclure avec motif explicite** — voir section 5.

---

## 5. Décision recommandée (à valider PO)

### Proposition de règle

| Sous-type | Critère | Comportement recommandé |
|---|---|---|
| Coffre (`S-COF…`) | `EC_Type=4` ET `DO_Numero LIKE 'S-COF%'` | **Exclure** — à-nouveau trésorerie, hors champ TVA. Log info (non alerte). |
| Solde initial pur (`SI-…`) | `EC_Type=4` ET `DO_Numero LIKE 'SI-%'` | **Exclure** — frais/à-nouveau hors champ TVA. Log info (non alerte). |
| Autres `EC_Type=4` (inconnus) | `EC_Type=4` ET aucun des deux préfixes | **Alerte explicite** maintenue (cas non vu sur prod — prudence). |

> **Règle simplifiée acceptable pour PO** : tout `EC_Type=4` est exclu avec motif
> _"à-nouveau GRF, hors champ TVA"_ — sans distinction de préfixe — tant qu'aucun cas TVA n'est prouvé.

### Implémentation si décision PO = EXCLURE (mineure)

1. Dans le dispatcher TASK-022, remplacer l'**alerte** `EC_Type=4` par un **log info** « exclu - à-nouveau GRF hors champ TVA ».
2. Modification : 2-3 lignes dans l'orchestrateur existant (TASK-007/022). **Pas de nouvelle tâche.**
3. **Pas de lecteur FGR** requis : aucun détail `RT_HISTCOMPTA` à lire.

---

## 6. Critères de validation

- [x] Inventaire réel chiffré : 34 lignes, 854 793,29 MAD, 2 sous-types identifiés.
- [x] Absence de TVA confirmée sur données réelles (`RT_HISTCOMPTA` vide, `MV_Tva=0`).
- [x] Aucune ligne `EC_Type=4` dans les déclarations TVA passées.
- [x] Alerte TASK-022 maintenue en attendant décision PO.
- [x] **Décision PO (09/07/2026) : MAINTENIR L'ALERTE (statu quo)** — aucune exclusion, l'alerte explicite `SOLDE_INITIAL_NON_GERE` (TASK-022) reste active pour tous les `EC_Type=4`. Aucune implémentation requise. Motif : prudence, aucun cas TVA prouvé mais aucune ligne rendue silencieuse (règle n°1).

---

## 7. Requêtes d'investigation (reproductibles)

```sql
-- Volume
SELECT COUNT(*), SUM(EC_Montant) FROM RT_ECHEANCE WHERE EC_Type = 4;

-- Par nature (préfixe DO_Numero)
SELECT LEFT(DO_Numero, 5) AS prefix, COUNT(*), SUM(EC_Montant)
FROM RT_ECHEANCE WHERE EC_Type = 4
GROUP BY LEFT(DO_Numero, 5);

-- Présence RT_HISTCOMPTA (0 pour tous)
SELECT E.EC_Id, COUNT(H.HC_Id) AS nb_histo
FROM RT_ECHEANCE E
LEFT JOIN RT_HISTCOMPTA H ON H.MV_Id = E.EC_Id
WHERE E.EC_Type = 4
GROUP BY E.EC_Id;

-- TVA sur mouvement lié (coffres via RT_AFFECTATION)
SELECT A.EC_Id, M.MV_Tva, M.MV_TvaMontant, M.MV_TauxTva
FROM RT_AFFECTATION A
JOIN RT_MOUVEMENT M ON M.MV_Id = A.MV_Id
WHERE A.EC_Id IN (SELECT EC_Id FROM RT_ECHEANCE WHERE EC_Type = 4);
```
