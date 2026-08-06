# TASK-194 — Filtrer RT_MOUVEMENT par CT_Type (exclure les « règlements type autre »)

Status: ✅ Terminé
Date: 05/08/2026
Module: Declaration.Selection
Fichiers modifiés:
- `Declaration.Selection/GrfEnums.cs`
- `Declaration.Selection/SelectionExpliqueeService.cs`
- `Declaration.Selection.Tests/Task194CtTypeFilteringTests.cs`

---

## 1. Contexte & Constat

Dans la base GRF / Sage (`RT_MOUVEMENT`), certains règlements ont un `CT_Type` (type de tiers) qui ne correspond ni à un client (`0`) ni à un fournisseur (`1`) réel (ex. règlements de trésorerie interne ou type « autre »).

Ces mouvements ne sont **jamais** affectés à une facture et ne peuvent jamais être déclarés. Faute de filtrage sur `CT_Type`, ils entraient dans les surensembles et polluaient la sélection sous forme de bruit permanent (`NonAffecte` non résoluble).

---

## 2. Solution apportée

1. **Ajout des constantes `CT_Type` dans `GrfEnums.cs`** :
   ```csharp
   public const int CtType_Client = 0;
   public const int CtType_Fournisseur = 1;
   ```

2. **Filtrage SQL dans `SelectionExpliqueeService.cs`** :
   - `GetSurensembleFournisseurSql` : ajout du filtre `AND M.CT_Type = @ctTypeFournisseur` (1).
   - `GetSurensembleClientSql` : ajout du filtre `AND M.CT_Type = @ctTypeClient` (0).
   - Passage des paramètres `ctTypeFournisseur` et `ctTypeClient` dans l'objet anonyme Dapper `param`.

3. **Garde-fous respectés (§4 spec)** :
   - Le filtrage a été strictement limité aux deux requêtes explicitement visées (`GetSurensembleFournisseurSql` et `GetSurensembleClientSql`).
   - Aucune extension par anticipation à `GetSurensembleDepenseSql` ni à `GetFactureFirstSql` conformément aux consignes du CDC (§4).

---

## 3. Validation & Non-régression

- `Declaration.Selection.Tests` : **61/61 tests passés** (dont `Task194CtTypeFilteringTests`).
- `Declaration.Orchestration.Tests` : **229/229 tests passés**.
- Aucune régression sur les règlements légitimes (`CT_Type=0` côté Client, `CT_Type=1` côté Fournisseur).
