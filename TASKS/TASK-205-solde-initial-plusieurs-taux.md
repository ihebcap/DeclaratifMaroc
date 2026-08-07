# TASK-205 — Solde initial GRF : passage à plusieurs taux de TVA (généralisation TASK-025)

> **Origine (PO, 2026-08-07)** : cas réel où un solde initial (EC_Type=4) doit être ventilé sur
> **plusieurs taux de TVA** (ex. une partie à 20 %, une autre à 10 %) — le mécanisme actuel
> (TASK-025) n'accepte qu'un seul couple `Taux`/`MontantTva` par échéance.

## Constat (preuve code)

1. **Persistance mono-taux** — `Declaration.Infrastructure/SQL/013_DM_SOLDE_INITIAL_TVA.sql:15-23` :
   `DM_SOLDE_INITIAL_TVA` a pour clé primaire `(SO_Id, EC_Id)` — une seule ligne `(Taux, MontantTva)`
   possible par échéance.
2. **Repository mono-taux** — `Declaration.Orchestration/ISoldeInitialTvaRepository.cs:19-26` :
   `GetSaisiesBatch` renvoie `IReadOnlyDictionary<int, SaisieSoldeInitialTva>` (une saisie par
   `EC_Id`) ; `EnregistrerSaisie` prend un seul `(taux, montantTva)`.
3. **Reconstruction mono-ligne** — `Declaration.Application/Services/DeclarationWorkflowService.cs:711-754`
   (`EnregistrerSaisieSoldeInitialAsync`) : remplace la ligne de l'échéance par **une seule**
   nouvelle `LigneCandidate`, avec `HT = reference.MontantAffecte - montantTva` (ligne 741) et un
   garde-fou bloquant `montantTva > reference.MontantAffecte` (ligne 728-729).
4. **Front mono-taux** — le composant de saisie (`SaisieSoldeInitialModal`, référencé dans
   `DomainGrid.tsx`, `DiagnosticModal.tsx`, `api.ts`) expose un seul champ taux + un seul champ
   montant TVA.
5. **Motif métier** — `Declaration.Application/Services/DiagnosticMotifMetier.cs:28-39` explique le
   mécanisme actuel au comptable en évoquant *"le taux"* (singulier) — à mettre à jour.

## Décisions PO (arbitrées 2026-08-07)

1. **Une échéance solde initial peut être décomposée en plusieurs "brackets"**, chacun avec son
   propre taux et son propre montant.
2. **Saisie par bracket : au choix du comptable, base HT + taux, OU montant TTC + taux** — dans le
   second cas le HT du bracket est déduit (`HT = TTC ÷ (1 + taux)`), dans le premier cas la TVA du
   bracket est déduite (`TVA = HT × taux`).
3. **Saisie libre, aucun contrôle de cohérence bloquant** entre la somme des brackets et le montant
   total du solde (`RT_ECHEANCE.EC_Montant`/`MontantAffecte`) — le PO a explicitement écarté l'option
   de garder ou d'étendre le garde-fou actuel (ligne 728-729 ci-dessus) : ce garde-fou doit être
   **retiré**, pas seulement étendu à une somme.
4. Chaque bracket produit sa **propre `LigneCandidate`** (une échéance solde initial à 2 taux = 2
   lignes dans le récap), au même titre que les factures normales à plusieurs lignes.

## Périmètre — Inclus

1. **Schéma** — nouvelle migration `Declaration.Infrastructure/SQL/014_DM_SOLDE_INITIAL_TVA_MultiTaux.sql` :
   ajouter la colonne `NumLigne INT NOT NULL DEFAULT 1` à `DM_SOLDE_INITIAL_TVA`, refaire la clé
   primaire `(SO_Id, EC_Id, NumLigne)`. Idempotente (`IF NOT EXISTS` sur la colonne), backfill des
   lignes existantes à `NumLigne = 1` (valeur par défaut suffit, aucune ligne existante n'a de
   second bracket).
2. **`SaisieSoldeInitialTva`** (`ISoldeInitialTvaRepository.cs`) : ajouter un mode de saisie —
   `decimal? BaseHT`, `decimal? MontantTtc` (exactement un des deux renseigné), `decimal Taux`,
   propriétés calculées `HT`/`MontantTva` selon le mode. `GetSaisiesBatch` renvoie désormais
   `IReadOnlyDictionary<int, IReadOnlyList<SaisieSoldeInitialTva>>` (liste de brackets par `EC_Id`).
3. **`EnregistrerSaisie` → `EnregistrerSaisies`** (pluriel) : remplace **tous** les brackets d'une
   échéance en une transaction (DELETE puis INSERT des brackets fournis) — idempotent, upsert complet
   côté échéance.
4. **`EnregistrerSaisieSoldeInitialAsync`** (`DeclarationWorkflowService.cs`) : accepte une liste de
   brackets, retire le garde-fou de la ligne 728-729, génère **une `LigneCandidate` par bracket**
   (mêmes champs snapshotés que l'actuelle : `NumeroFacture`, `Reference`, `TiersNom`, `Domaine`,
   etc. — seuls `HT`/`Taux`/`TVA`/`TTC`/`Id` diffèrent par bracket).
5. **Front `SaisieSoldeInitialModal`** : liste de brackets (ajouter/retirer une ligne), par bracket un
   sélecteur de mode (HT+taux / TTC+taux) et le calcul en direct de la valeur déduite ; affichage du
   total TTC saisi à titre indicatif (non bloquant, cf. décision 3).
6. **`DiagnosticMotifMetier.cs:28-39`** : mettre à jour le libellé ("un ou plusieurs taux").

## Périmètre — Exclu

- Le code taxe (`CodeTaxe`/`IntituleTaxe`) des lignes solde initial reste inchangé (vide) — aucune
  demande PO de le renseigner ici (contrairement à TASK-206).
- Aucune modification du calcul pour les échéances **non** solde initial (`EC_Type ≠ 4`).

## À documenter dans le VERIFY (pas d'improvisation autorisée)

- Confirmation que la migration 014 est bien idempotente et ne perd aucune saisie existante (test
  avant/après sur une base contenant des lignes `DM_SOLDE_INITIAL_TVA` déjà saisies).
- Capture de la double saisie (HT+taux sur un bracket, TTC+taux sur un autre) pour la même échéance
  aboutissant à 2 `LigneCandidate` visibles dans le récap.

## Fichiers impactés

- [Declaration.Infrastructure/SQL/013_DM_SOLDE_INITIAL_TVA.sql](../Declaration.Infrastructure/SQL/013_DM_SOLDE_INITIAL_TVA.sql) (référence, ne pas modifier — nouvelle migration 014 à créer)
- [Declaration.Orchestration/ISoldeInitialTvaRepository.cs](../Declaration.Orchestration/ISoldeInitialTvaRepository.cs)
- [Declaration.Orchestration/SoldeInitialTvaRepository.cs](../Declaration.Orchestration/SoldeInitialTvaRepository.cs)
- [Declaration.Application/Services/DeclarationWorkflowService.cs:711-754](../Declaration.Application/Services/DeclarationWorkflowService.cs#L711-L754)
- [Declaration.API/Controllers/DeclarationsController.cs:311-320,903](../Declaration.API/Controllers/DeclarationsController.cs#L311-L320) (`SaisieSoldeInitialRequest`)
- [Declaration.Application/Services/DiagnosticMotifMetier.cs:28-39](../Declaration.Application/Services/DiagnosticMotifMetier.cs#L28-L39)
- Front : `SaisieSoldeInitialModal` (composant), `declaration-tva-web/src/api.ts`

## Critères de validation

- Une échéance solde initial saisie avec 2 brackets (taux différents) produit 2 `LigneCandidate`
  distinctes, correctement sommées dans `RecapParTaux`.
- La saisie reste modifiable avant clôture (ré-appel = remplacement complet des brackets), comme
  aujourd'hui.
- Build + tests back (`dotnet build`/`dotnet test`) et front (`npm run build`) verts.
- Aucun garde-fou de somme n'est appliqué (conforme décision PO 3).

## Dépendances / risques

- **Aucune dépendance** sur TASK-204 (AG Grid) ni TASK-202 — indépendant des chantiers grille en
  cours.
- **Risque de régression** : toute déclaration déjà clôturée avec un solde initial mono-taux doit
  continuer à s'afficher correctement après la migration 014 (colonne `NumLigne` par défaut `1`).
- **TASK-206** réutilise le même principe de saisie multi-bracket (HT/TTC + taux) — envisager un
  composant front partagé si TASK-206 est développée après, mais **ne pas bloquer** TASK-205 sur
  cette factorisation (chaque table/repository reste distincte, cf. TASK-206).
