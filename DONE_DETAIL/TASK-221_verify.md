# VERIFY — TASK-221 : Seuil de montant DDP (date limite portée au 31/12/2025 en dur et borne > 10 000)

Date d'exécution : 2026-10-08  
Rôle : WORKER (implémentation stricte, arrêt au dépôt de ce fichier sans clôture ni modification de `DONE.md` / `TODO.md` / `CHANGELOG.md`).

---

## 1. Décisions PO & Traçabilité légale

- [x] **Décision PO tracée** : La règle d'exemption étendue à 2025 pour les montants `<= 10 000` s'applique à **tous** les clients (constante unique dans le code, aucun réglage par société).
- [x] **Écart DGI assumé** : La circulaire DGI n°734 §O-2 p.10 et l'annonce du 21/03/2025 limitent l'exemption `<= 10 000` aux factures antérieures au 01/01/2025. Le PO assume l'extension de l'exemption sur toute l'année 2025 en dur pour tous les clients. Risque d'exposition légale : 5 000 MAD par facture manquante ou inexacte en cas de contrôle (NC 734 p.7).
- [ ] **Décision PO sur les brouillons de déclaration déjà générés** : Les brouillons existants non déposés contiennent l'ancien périmètre (aucune régénération automatique n'est exécutée). Décision réservée au PO pour une éventuelle régénération manuelle par les utilisateurs.

---

## 2. Modifications apportées

### Backend (.NET)
- [`Declaration.Core/SelectionDelaiPaiementCalculator.cs`](file:///D:/_vibe/GRF/Declaration.Core/SelectionDelaiPaiementCalculator.cs) :
  - `DateLimiteSeuilMontant` : `2024-12-31` → `2025-12-31`.
  - `EstEligibleSeuilLegal` : `montant >= SeuilMontant` → `montant > SeuilMontant` (strictement supérieur).
  - Commentaires XML alignés avec la note DGI 734 §O-2 et l'arbitrage PO 2025.
- [`Declaration.Infrastructure/Repositories/SelectionDelaiPaiementRepository.cs`](file:///D:/_vibe/GRF/Declaration.Infrastructure/Repositories/SelectionDelaiPaiementRepository.cs) :
  - Prédicat SQL aligné : `DO_Date >= @DateApresLimite OR EC_Montant > @SeuilMontant` avec `DateApresLimite = dateLimiteSeuilMontant.Date.AddDays(1)` (`2026-01-01`).
  - Comparaison insensible à l'heure : une facture du 31/12/2025 à 14:00 est correctement traitée comme jour limite (soumise au seuil en SQL comme en C#).
- [`Declaration.Core.Tests/SelectionDelaiPaiementCalculatorTests.cs`](file:///D:/_vibe/GRF/Declaration.Core.Tests/SelectionDelaiPaiementCalculatorTests.cs) :
  - Tests `SeuilsLegaux` mis à jour (`10_000m` passe de `True` à `False`, `10_000.01m` → `True`).
  - Ajout des 14 scénarios de test complets (`TASK221_Scenarios_EligibiliteSeuilLegal` et `TASK221_Scenario14_DateMiseEnRoutePosterieure_FactureExclue`).
- [`Declaration.Orchestration.Tests/Task133GenerationFichierDelaiPaiementTests.cs`](file:///D:/_vibe/GRF/Declaration.Orchestration.Tests/Task133GenerationFichierDelaiPaiementTests.cs) :
  - Fixture d'échéance candidate 2025 alignée à 15 000 MAD (> 10 000) pour cohérence avec la nouvelle règle.

### Frontend
- [`declaration-tva-web/tests/task134.spec.ts`](file:///D:/_vibe/GRF/declaration-tva-web/tests/task134.spec.ts) :
  - Fixture mock candidate 2025 (FA260007) alignée à 14 500 MAD (> 10 000).

---

## 3. Preuves par critère (méthode + date : 2026-10-08)

### Greps de contrôle
- `git grep -n ">= SeuilMontant" -- "*.cs"` : **0 résultat** (prouvé le 2026-10-08).
- `git grep -n "EC_Montant >=" -- "*.cs"` : **0 résultat** (prouvé le 2026-10-08).
- `git grep -n "2024-12-31"` : **0 résultat vivant** hors historique `DONE_DETAIL/DDP-TASK-131*`, scenario test 4 et documents de suivi (prouvé le 2026-10-08).

### Parité SQL / C# (14 scénarios)
| # | DoDate | Montant (MAD) | Attendu | Résultat C# | Résultat SQL (`@DateApresLimite = 2026-01-01`) |
|---|---|---|---|---|---|
| 1 | 2023-06-30 | 1 000 000,00 | Non | `False` | Exclu (`DO_Date < DateDebutLoi`) |
| 2 | 2023-07-01 | 10 000,00 | Non | `False` | Exclu (`DO_Date < 2026-01-01` ET `EC_Montant <= 10000`) |
| 3 | 2023-07-01 | 10 000,01 | Oui | `True` | Inclus (`EC_Montant > 10000`) |
| 4 | 2024-12-31 | 9 999,99 | Non | `False` | Exclu (`DO_Date < 2026-01-01` ET `EC_Montant <= 10000`) |
| 5 | 2025-01-01 | 9 999,99 | Non | `False` | Exclu (`DO_Date < 2026-01-01` ET `EC_Montant <= 10000`) |
| 6 | 2025-04-23 | 800,00 | Non | `False` | Exclu (`DO_Date < 2026-01-01` ET `EC_Montant <= 10000`) |
| 7 | 2025-12-31 | 10 000,00 | Non | `False` | Exclu (`DO_Date < 2026-01-01` ET `EC_Montant <= 10000`) |
| 8 | 2025-12-31 | 10 000,01 | Oui | `True` | Inclus (`EC_Montant > 10000`) |
| 9 | 2025-12-31 14:00 | 800,00 | Non | `False` | Exclu (`DO_Date < 2026-01-01` ET `EC_Montant <= 10000`) |
| 10 | 2026-01-01 | 0,01 | Oui | `True` | Inclus (`DO_Date >= 2026-01-01`) |
| 11 | 2026-01-01 | 800,00 | Oui | `True` | Inclus (`DO_Date >= 2026-01-01`) |
| 12 | 2025-06-01 | -10 080,00 | Non | `False` | Exclu (`DO_Date < 2026-01-01` ET `EC_Montant <= 10000`) |
| 13 | 2026-02-01 | -10 080,00 | Oui | `True` | Inclus (`DO_Date >= 2026-01-01`) |
| 14 | 2025-03-15 (MiseEnRoute=2025-06-01) | 50 000,00 | Non | `False` | Exclu par le filtre `DoDate < DateMiseEnRoute` (TASK-220) |

---

## 4. Mesure sur données réelles (Base `GR_EMA_DISTRIBUTION`, SO_Id=1)

Exécutée en LECTURE SEULE stricte le 2026-10-08 via SQL Server local (`localhost\SQL2022`) :

| Mesure | Valeur constatée |
|---|---|
| **Lignes `RT_ECHEANCE` avec composante heure (`DO_Date <> CAST(DO_Date AS date)`)** | **96** (confirme la pertinence du fix `@DateApresLimite`) |
| **Échéances de 2025 de montant <= 10 000 qui SORTENT** | **291** |
| **Montant total des échéances 2025 sortantes** | **951 438,78 MAD** |
| Dont échéances **non soldées** | **9** |
| Dont déjà présentes dans `RT_DECLARATIONDELAISPAIEMENTLG` | **0** |
| **Échéances de montant exactement égal à 10 000,00 MAD** | **0** (sur 2025 comme sur toute la base) |
| **Échéances candidates globales lues** | **1 408** (Avant) → **1 117** (Après) (écart = **-291**) |
| **Lignes produites écran Contrôle pour T3 2026 (2026-07-01..2026-09-30)** | Avant : 118 lignes → Après : 118 lignes (écart : 0) |

### Requêtes SQL de reproduction (lecture seule)
```sql
-- 1. Échéances avec composante heure
SELECT COUNT(*) FROM RT_ECHEANCE WHERE DO_Date <> CAST(DO_Date AS date);

-- 2. Échéances 2025 sortantes (devise société SO_Id=1, domaine Achat)
SET DATEFORMAT ymd;
SELECT 
    COUNT(*) AS NbSortantes,
    SUM(EC_Montant) AS MontantTotalMAD,
    SUM(CASE WHEN EC_Solde <> 0 THEN 1 ELSE 0 END) AS NbNonSoldees,
    SUM(CASE WHEN EC_Montant = 10000.00 THEN 1 ELSE 0 END) AS NbExactement10000
FROM RT_ECHEANCE E
WHERE E.SO_Id = 1
  AND E.DO_Domaine = 1
  AND E.EC_Type NOT IN (90, 91)
  AND E.DE_Id = 4
  AND E.DO_Date >= '2025-01-01'
  AND E.DO_Date < '2026-01-01'
  AND E.EC_Montant <= 10000;

-- 3. Vérification présence dans RT_DECLARATIONDELAISPAIEMENTLG
SELECT COUNT(*) 
FROM RT_ECHEANCE E
INNER JOIN RT_DECLARATIONDELAISPAIEMENTLG LG ON LG.EC_Id = E.EC_Id
WHERE E.SO_Id = 1 AND E.DO_Domaine = 1 AND E.EC_Type NOT IN (90, 91) AND E.DE_Id = 4
  AND E.DO_Date >= '2025-01-01' AND E.DO_Date < '2026-01-01'
  AND E.EC_Montant <= 10000;
```

---

## 5. Résultats des builds et tests

- [x] **`dotnet build DeclarationTVA.slnx`** : **0 erreur** (6 avertissements préexistants).
- [x] **`dotnet test`** :
  - `Declaration.Core.Tests` : **223/223 réussis** (0 échec).
  - `Declaration.Orchestration.Tests` : **266/266 réussis** (0 échec).
  - `Declaration.Export.Excel.Tests` : **3/3 réussis** (0 échec).
  - `Declaration.Export.Xml.Tests` : **26/26 réussis** (0 échec).
  - **Total .NET ciblé** : **518 tests exécutés, 518 réussis (100%)**.
- [x] **Front React (`declaration-tva-web`)** :
  - `npm run lint` : **0 erreur**, 18 avertissements préexistants.
  - `npm run build` : **0 erreur** (build Vite/tsc terminé avec succès en 3.48s).
- [x] **Playwright (`task134.spec.ts`, `task136.spec.ts`)** :
  - Tests B et C de `task134` : **VERTS**.
  - Tests A et `task136` : échecs préexistants identiques à ceux documentés dans `DONE_DETAIL/TASK-220_verify.md` (aucun lien avec le seuil DDP).

---

## 6. Point d'arrêt

Conformément à la consigne, le WORKER s'arrête ici après dépôt de ce document.
Aucun déplacement vers `DONE_DETAIL/`, aucune modification de `DONE.md`, `TODO.md` ou `CHANGELOG.md`, aucun commit d'approbation.
La tâche est prête pour la revue par l'architecte / tiers.
