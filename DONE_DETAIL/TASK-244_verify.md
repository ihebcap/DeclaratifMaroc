# VERIFY — TASK-244 : DDP — Exclusion des factures d'avoir (montant négatif)

**Date d'exécution :** 2026-10-27  
**Rôle :** WORKER  
**Module :** `Declaration.Infrastructure` (`SelectionDelaiPaiementRepository`)  
**Statut :** ✅ Validé avec preuve sur base réelle SQL Server (`GR_EMA_DISTRIBUTION`)

---

## 1. Contexte & Problème résolu

- **Règle métier PO (27/10/2026) :** Dans le module **Déclaration Délai de Paiement (DDP)**, une facture d'avoir (`EC_Montant < 0`) n'est **jamais** déclarée — elle doit être exclue du périmètre de sélection, sans exception.
- **Cause racine du bug :** Dans `SelectionDelaiPaiementRepository.GetEcheancesCandidatesAsync` :
  ```sql
  WHERE SO_Id = @SoId
    AND DO_Domaine = @DoDomaine
    AND EC_Type NOT IN (@TypeGain, @TypePerte)
    AND DE_Id = @DeviseSocieteId
    AND DO_Date >= @DateDebutLoi
    AND (DO_Date >= @DateApresLimite OR EC_Montant > @SeuilMontant)
  ```
  Pour un avoir (`EC_Montant < 0`), la branche `EC_Montant > @SeuilMontant` est fausse, mais dès le 01/01/2026 (`DO_Date >= @DateApresLimite`, avec `@DateApresLimite = 2026-01-01`), la condition était vraie pour tout avoir récent. L'avoir était donc sélectionné à tort comme candidat DDP.

---

## 2. Modifications apportées

### Backend (.NET)
- `Declaration.Infrastructure/Repositories/SelectionDelaiPaiementRepository.cs` :
  Ajout du filtre de périmètre strict `AND EC_Montant >= 0` dans la clause `WHERE` de la méthode `GetEcheancesCandidatesAsync`, avant la condition de seuil légal :
  ```sql
  WHERE SO_Id = @SoId
    AND DO_Domaine = @DoDomaine
    AND EC_Type NOT IN (@TypeGain, @TypePerte)
    AND DE_Id = @DeviseSocieteId
    AND EC_Montant >= 0
    AND DO_Date >= @DateDebutLoi
    AND (DO_Date >= @DateApresLimite OR EC_Montant > @SeuilMontant)
  ```

---

## 3. Analyse des autres chemins de lecture DDP

- `SelectionDelaiPaiementService.SelectionnerAsync` :
  - **Étape 1 :** Appel de `_selection.GetEcheancesCandidatesAsync(...)`. C'est l'**unique source** des échéances candidates.
  - **Étape 2 :** `GetAffectationsAsync(soId, ecIds)` filtre par `WHERE AF.EC_Id IN @EcIds`.
  - **Étape 3 :** `GetDernieresBornesDeclareesAsync(soId, ecIds)` filtre par `WHERE LG.EC_Id IN @EcIds`.
  - **Étape 4 :** `SelectionDelaiPaiementCalculator.Selectionner(...)` n'itère que sur `parametres.Echeances`.
- **Conclusion :** Aucun autre chemin de lecture DDP ne réintroduit d'échéance négative. Le filtre en amont côté SQL dans `GetEcheancesCandidatesAsync` garantit l'exclusion totale des avoirs.

---

## 4. Preuves & Validation

### 4.1 Build .NET
- Commande : `dotnet build DeclarationTVA.slnx`
- Date : `2026-10-27T11:08:22Z`
- Résultat : **0 Erreur**, 23 avertissements préexistants.

### 4.2 Suites de tests automatisés
- `Declaration.Core.Tests` : **227/227 réussis** (0 échec, 0 ignoré)
- `Declaration.Orchestration.Tests` : **272/272 réussis** (0 échec, 0 ignoré)
- `Declaration.Selection.Tests` : **61/61 réussis** (0 échec, 0 ignoré)

### 4.3 Preuve réelle sur base de données SQL Server (`GR_EMA_DISTRIBUTION`)
- Exécution du script de preuve `scratch/Ddp244Proof/Program.cs` contre l'instance SQL Server locale (`.\SQL2022`, base `GR_EMA_DISTRIBUTION`, `SO_Id = 1`, `DeviseSocieteId = 4`) :

```text
=== TASK-244 : Verification DDP Exclusion Factures d'Avoir (Montant Negatif) ===
SO_Id = 1, Devise Societe ID = 4
SQL AVANT FIX : 1117 echeances candidates dont 26 avoirs (EC_Montant < 0)
C# REPO APRES FIX : 1091 echeances candidates dont 0 avoirs (Montant < 0)
Ecart : 26 echeances exclues (exactement 26 avoirs)
SUCCES : 100% des avoirs negatifs sont exclus de GetEcheancesCandidatesAsync !
```

- **Exemples d'avoirs négatifs exclus par le fix** (auparavant inclus à tort car `DO_Date >= 2026-01-01`) :
  - `EC_Id = 23985`, Facture `FCR2600020`, Date `13/06/2026`, Montant : `-20,00 MAD`
  - `EC_Id = 23939`, Facture `FCR2600017`, Date `18/05/2026`, Montant : `-508,89 MAD`
  - `EC_Id = 23646`, Facture `FCR2600018`, Date `16/05/2026`, Montant : `-426,00 MAD`
  - `EC_Id = 22243`, Facture `FCR2600009`, Date `07/05/2026`, Montant : `-672,00 MAD`
  - `EC_Id = 21886`, Facture `AV2500073`,  Date `17/04/2026`, Montant : `-10 080,00 MAD`
  - `EC_Id = 21845`, Facture `FCR2600005`, Date `10/04/2026`, Montant : `-7 420,00 MAD`
  - `EC_Id = 22123`, Facture `AV2500074`,  Date `09/04/2026`, Montant : `-1 796,04 MAD`

---

## 5. Checklist VALIDATION de la tâche

- [x] `dotnet build DeclarationTVA.slnx` → 0 erreur (2026-10-27T11:08:22Z, 0 erreur)
- [x] Preuve de l'exclusion d'un avoir récent — vérification réelle DB sur `GR_EMA_DISTRIBUTION` (1 117 → 1 091 lignes, 26 avoirs exclus, 0 avoir résiduel)
- [x] Confirmation qu'aucun autre chemin de lecture DDP ne réintroduit un montant négatif

---

## 6. Revue architecte (clôture)

Revue distincte du worker ayant implémenté (conforme à la règle de séparation implémentation/clôture,
`CLAUDE.md`). Diff réel relu (`git diff`) : une seule ligne ajoutée, exactement au bon endroit, aucun
scope creep. Script de preuve `scratch/Ddp244Proof/Program.cs` consulté (DI réelle, connexion SQL Server
réelle, pas un mock qui ne testerait que lui-même). `scratch/` confirmé ignoré par git (pas de fuite de
credential local). **APPROVE** — 2026-10-27.
