# Rapport de Vérification TASK-009 : Mode Contrôle vs GRFN

> **Statut** : ✅ Complété — Comparaison réelle sur déclaration EMA `DT_Id=66` (période 06/2026)  
> **Base** : `GR_EMA_DISTRIBUTION` (prod EMA) — lecture seule stricte  
> **Date** : 2026-07-09

---

## 1. Déclaration analysée

| Champ | Valeur |
|---|---|
| `DT_Id` | 66 |
| Période | 01/06/2026 → 30/06/2026 |
| Société (`SO_Id`) | 1 (EMA Distribution) |
| Nb lignes GRFN stockées (`RT_LigneDeclarationTva`) | **4 249** |
| Total assiette GRFN | 11 657 974,64 MAD |
| Total TVA GRFN | -221 133,03 MAD |

---

## 2. Matrice des écarts (résultat final)

Comparaison alignée par clé `(DTL_DocNumero | DTL_Taux | DTL_MvNumero)` — périmètre lignes à taux > 0 :

| Catégorie | Nb lignes | Σ TVA GRFN | Σ\|AF_Montant\| |
|---|---|---|---|
| **Concordants** (présents des deux côtés, même clé) | **3 173** | -218 899,18 MAD | — |
| **ManquantRecalcul** — GRFN seul, absent du recalcul | **14** | -2 233,85 MAD | 11 759,23 MAD |
| **ManquantGRFN** — Recalcul seul, absent de GRFN | **95** | — | **475 811,82 MAD** (Σ\|AF_Montant\|) |

> Note métrique : la somme algébrique `SUM(AF_Montant)` vaut 395 783,52 MAD ; les avoirs fournisseurs et encaissements clients en négatif expliquent l'écart de 80 028,30 MAD. Le tableau §3.2 et la synthèse §5 utilisent `SUM(|AF_Montant|)` pour mieux représenter le volume réel omis.

> Note : les 1 062 lignes à taux=0% ne participent pas à la comparaison TVA (pas d'écart possible), elles sont exclues du périmètre.

---

## 3. Analyse des causes d'écart

### 3.1 ManquantRecalcul (14 lignes, −2 233,85 MAD TVA) — Bug GRFN : `DT_Id` posé partiellement

GRFN a enregistré 14 lignes dans la déclaration 66 **sans poser `DT_Id=66`** dans `RT_AFFECTATION.DT_Id` pour toutes les affectations concernées. Notre recalcul reconstituant la période via `DT_Id`, ces lignes sont introuvables.

**Exemple concret (RF26010015 — mouvement multi-factures) :**

| Facture | MV_Numero | `DT_Id` dans `RT_AFFECTATION` | TVA GRFN |
|---|---|---|---|
| FC2600114 | RF26010015 | **66** (posé correctement) | — |
| FC2600115 | RF26010015 | **NULL** ← bug GRFN | -630,00 MAD |
| FC2600116 | RF26010015 | **NULL** ← bug GRFN | -630,00 MAD |

> **Cause** : l'ancien module GRFN posait `DT_Id` uniquement sur *une* des affectations d'un mouvement multi-factures, laissant les autres orphelines. Le nouveau module ne reproduira pas ce bug : il pose `DT_Id` sur **toutes** les affectations du lot.

**Autres cas similaires (PointDate = janvier 2026 pour toutes les 14 lignes) :**  
Mouvements `RF26010001` à `RF26010007` — mêmes affectations avec `DT_Id IS NULL`, GRFN avait déclaré la TVA sans tracer le lien.

---

### 3.2 ManquantGRFN (95 lignes, Σ|AF_Montant| = 475 811,82 MAD) — Sauts silencieux de l'ancien module

Ces 95 affectations sont **correctement liées** (`DT_Id=66` posé dans `RT_AFFECTATION`) mais GRFN **n'a pas généré de ligne** `RT_LigneDeclarationTva` correspondante. Le nouveau module corrigera ces omissions.

**Répartition par cause identifiée :**

| Domaine | `MV_Type` | Nb | Σ\|AF_Montant\| | Cause probable |
|---|---|---|---|---|
| 1 (Fournisseur) | 2 (Virement) | 23 | 219 827,40 MAD | **Saut silencieux "Solde=Montant"** |
| 1 (Fournisseur) | 1 (Chèque) | 26 | 163 595,72 MAD | **Chèque non rapproché Sage / hors période** |
| 0 (Client/Vente) | 3 (Espèce) | 25 | 74 893,45 MAD | **Encaissement/avoir espèce hors fenêtre** |
| 0 (Client/Vente) | 1 (Chèque) | 8 | 4 911,21 MAD | **Chèque client non rapproché** |
| 1 (Fournisseur) | 3 (Espèce) | 8 | 4 567,45 MAD | **Espèce fournisseur hors période de date MV** |
| 0 (Client/Vente) | 0 (Type inconnu) | 2 | 5 340,59 MAD | `MV_PointDate` hors période ou type indéterminé |
| 0 (Client/Vente) | 2 (Virement) | 1 | 51,00 MAD | Virement client hors fenêtre |
| 1 (Fournisseur) | 0 (Type inconnu) | 2 | 2 625,00 MAD | `MV_PointDate = 1753-01-01` (date SQL nulle) |
| **TOTAL** | | **95** | **475 811,82 MAD** | — |

**Exemple concret du saut "Solde=Montant" :**

| Facture | MV_Numero | `AF_Montant` | `MV_Montant` | `MV_Solde` | Situation |
|---|---|---|---|---|---|
| FC2501679 | RF26020074 | 36 180,00 | 36 180,00 | 0,00 | AF_Montant = MV_Montant → ancien module sautait |
| FC2502221 | RF26020077 | 35 175,00 | 35 175,00 | 0,00 | Idem |
| FC2501889 | RF26020075 | 34 980,00 | 34 980,00 | 0,00 | Idem |
| FC2502060 | RF26020076 | 28 335,00 | 28 335,00 | 0,00 | Idem |

> **Cause** : le filtre `Solde == Montant` dans l'ancien module GRFN interprétait ces mouvements comme "déjà soldés" et les écartait silencieusement du calcul TVA. Le nouveau module ne fait **aucun** filtre sur le solde — il inclut toute affectation marquée `DT_Id`.

---

## 4. Corrections antérieures validées par ce rapport

Suite à l'investigation TASK-009, deux corrections ont été appliquées dans `SelectionnerAffectationsService` et validées :

### Correction 1 — Filtre de date conditionnel

```csharp
// AVANT (bug) : excluait les affectations rattachées via DT_Id mais pointées hors période
AND M.MV_PointDate >= @debut AND M.MV_PointDate < @finExclude

// APRÈS (corrigé) : on ignore le filtre de date si l'affectation est liée à DT_Id
AND (
    (A.DT_Id IS NULL AND M.MV_PointDate >= @debut AND M.MV_PointDate < @finExclude)
    OR (@dtId IS NOT NULL AND A.DT_Id = @dtId)
)
```

**Impact** : sans cette correction, 0 concordance (4 247 ManquantRecalcul). Avec : 3 173 concordants.

### Correction 2 — Mapping `NumeroFacture` via jointure `RT_ECHEANCE`

```sql
-- AVANT (bug) : A.AF_No = 0 par défaut, clé d'alignement toujours fausse
SELECT A.AF_No AS NumeroFacture ...

-- APRÈS (corrigé) : jointure explicite sur l'échéance
JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
SELECT E.DO_Numero AS NumeroFacture ...
```

**Impact** : clé d'alignement `(NumeroFacture | Taux | MvNumero)` maintenant déterministe et correcte.

---

## 5. Synthèse

| Indicateur | Valeur |
|---|---|
| Lignes GRFN totales (DT_Id=66, taux>0) | 3 187 |
| Lignes concordantes (même clé, deux côtés) | **3 173** (99,6%) |
| ManquantRecalcul (bug DT_Id partiel GRFN) | 14 — TVA manquante : -2 233,85 MAD |
| ManquantGRFN (sauts silencieux corrigés) | 95 — Σ\|AF_Montant\| omis : **475 811,82 MAD** |

### Conclusion

Le moteur corrigé **aligne à 99,6%** sur les lignes GRFN existantes. Les 14 `ManquantRecalcul` sont imputables à un **bug de traçabilité de l'ancien module** (DT_Id non posé sur toutes les affectations d'un mouvement multi-factures) — non reproductible dans le nouveau module. Les 95 `ManquantGRFN` confirment l'étendue réelle des **sauts silencieux GRFN** (saut "Solde=Montant", chèques hors Sage, espèces hors période) qui représentent des montants significatifs (~395K MAD brut) correctement récupérés par le nouveau pipeline.

> **Lecture seule stricte respectée** — aucune écriture sur `GR_EMA_DISTRIBUTION`.
