# Rapport de vérification — TASK-224

**Date :** 09/10/2026  
**Auteur :** Antigravity (Agent IA DeepMind)  
**Statut :** ✅ Terminé (en attente de revue PO / arbitrage données existantes)  
**Priorité :** CRITICAL  
**Module :** `Declaration.Core`, `Declaration.Application`, `Declaration.Infrastructure`, `Declaration.API`, `declaration-tva-web`

---

## 1. Contexte & Constat initial (Signalement PO du 09/10/2026)

Sur la déclaration `TVA1-2026-03-T` (société **FIVE KEYS MAROC**), 3 factures réglées en 2 fois (50 % + 50 %) apparaissaient chacune avec **2 lignes par règlement** (mêmes taux 20 %, mêmes montants), soit 4 lignes au lieu de 2 :

| Facture | Tiers | TTC Facture | Règlements | TVA attendue (1×) | TVA observée (2×) |
|---|---|---|---|---|---|
| FF260029 | LOGINFO INGENIERIE | 160 486,80 | RF26100006 + RF26100007 | 26 747,80 | 53 495,60 |
| FF260027 | UNION MAGHREB DES TE | 148 549,20 | RF26070005 + RF26090005 | 24 758,20 | 49 516,40 |
| FF260028 | SNSS TECHNOLOGIES | 80 414,77 | RF26090006 + RF26090007 | 13 402,46 | 26 804,92 |

- **Total TVA observé :** `133 189,91 MAD` (21 lignes)
- **Total TVA réel attendu :** `68 281,46 MAD` (15 lignes)
- **Surdéclaration fiscale réelle :** `+64 908,46 MAD`

---

## 2. Phase 1 — Diagnostic SANS accès base de données

### 2.1 Jeu de données de simulation
Création de la fixture [`Task224DonneesSimulation.cs`](file:///D:/_vibe/GRF/Declaration.Orchestration.Tests/Task224DonneesSimulation.cs) répliquant fidèlement la capture PO (12 factures, 15 règlements, factures multi-règlements et factures à règlement unique).

### 2.2 Test de reproduction ROUGE (preuve de la cause racine)
Création de [`Task224ReproductionDoublonsTests.cs`](file:///D:/_vibe/GRF/Declaration.Orchestration.Tests/Task224ReproductionDoublonsTests.cs).
- **Commit de référence ROUGE :** `cb6449e` (*test(repro): TASK-224 test rouge prouvant le doublon de lignes pour facture reglee en plusieurs fois*)
- **Résultat avant correctif :**
  - `ConstructeurDeclaration.ConstruireDeclaration` produisait bien 15 lignes saines (68 281,46 MAD).
  - Mais `DeclarationWorkflowService.MapLignesCandidates` produisait **21 lignes** et **133 189,91 MAD** !

### 2.3 Cause racine confirmée
Dans `DeclarationWorkflowService.MapLignesCandidates` (~ligne 1068) :
```csharp
// CODE FAUTIF INITIAL :
var taxesLines = modele.Lignes.Where(l => l.NumeroFacture == c.Affectation.NumeroFacture).ToList();
```
Lorsqu'une facture a 2 règlements distincts dans la sélection :
1. `modele.Lignes` contient 2 lignes (1 par règlement, chacune ventilée au prorata de 50 %).
2. Lors de l'itération sur chaque candidat `c` dans `MapLignesCandidates`, la clause `l.NumeroFacture == c.Affectation.NumeroFacture` ne distinguait **pas** à quel règlement la ligne correspondait.
3. Chaque candidat rattachait alors **l'ensemble des lignes de la facture** (les 2 lignes de ventilation) au lieu de sa propre ligne de règlement !
4. Résultat : 2 règlements × 2 lignes = **4 lignes créées**, doublant l'assiette HT et la TVA.

---

## 3. Phase 2 — Correctifs implémentés

### 3.1 Clé d'affectation sur `LigneDeclarationEnrichie` & appariement strict
1. Dans [`Declaration.Core/Model.cs`](file:///D:/_vibe/GRF/Declaration.Core/Model.cs), ajout de `EC_Id` et `MV_Id` sur `LigneDeclarationEnrichie`.
2. Dans [`Declaration.Core/ConstructeurDeclaration.cs`](file:///D:/_vibe/GRF/Declaration.Core/ConstructeurDeclaration.cs), propagation de `aff.EC_Id` et `aff.MV_Id` lors de la construction des lignes enrichies.
3. Dans [`Declaration.Application/Services/DeclarationWorkflowService.cs`](file:///D:/_vibe/GRF/Declaration.Application/Services/DeclarationWorkflowService.cs), mise à jour du filtre de `MapLignesCandidates` :
```csharp
var taxesLines = modele.Lignes.Where(l => 
    l.NumeroFacture == c.Affectation.NumeroFacture &&
    (l.MV_Id > 0 && c.Affectation.MV_Id > 0
        ? (l.MV_Id == c.Affectation.MV_Id && l.EC_Id == c.Affectation.EC_Id)
        : (string.IsNullOrEmpty(l.NumeroRapprochement) || l.NumeroRapprochement == c.Affectation.NumeroRapprochement))
).ToList();
```

### 3.2 Idempotence d'écriture dans `DM_LGTVA`
Dans [`Declaration.Infrastructure/Repositories/DeclarationRepository.cs`](file:///D:/_vibe/GRF/Declaration.Infrastructure/Repositories/DeclarationRepository.cs) (`SaveLignesCandidatesAsync`) :
- Avant chaque insertion dans `DM_LGTVA`, vérification d'unicité sur la clé `(DeclarationId, Domaine, EC_Id, MV_Id, Taux, CodeTaxe)`.
- Dédoublonnage in-batch et contre les lignes déjà persistées en base pour la même déclaration.
- Tout doublon détecté est consigné en avertissement (`_logger.LogWarning`) et ignoré (jamais inséré ni sommé).

### 3.3 Garde-fou bloquant `TVA_FACTURE_SURDECLAREE`
1. Dans [`Declaration.Core/ConstructeurDeclaration.cs`](file:///D:/_vibe/GRF/Declaration.Core/ConstructeurDeclaration.cs) :
   - Pour chaque facture (`EC_Id` / `NumeroFacture`), vérification que :
     - `Σ TVA déclarée ≤ TVA facture + tolérance` (0,05 × nb de lignes).
     - `Σ MontantAffecte ≤ TTC facture + tolérance`.
   - Si dépassé : émission d'une alerte `NiveauAlerte.Error` de code `TVA_FACTURE_SURDECLAREE`.
2. Dans [`Declaration.Application/Services/DeclarationWorkflowService.cs`](file:///D:/_vibe/GRF/Declaration.Application/Services/DeclarationWorkflowService.cs) :
   - Contrôle intégré dans `GetCheckupAsync` (alerte de sévérité `bloquant` / `Error`).
   - La clôture (`CloturerDeclarationAsync`) vérifie la présence d'alertes bloquantes et rejette l'intégration si une facture est surdéclarée.

### 3.4 Outil de diagnostic en lecture seule dans l'application
1. **Backend :**
   - DTOs `DiagnosticDoublonsResultat` et `GroupeDoublonDto` dans [`WorkflowEntities.cs`](file:///D:/_vibe/GRF/Declaration.Application/Entities/WorkflowEntities.cs).
   - Méthode `DetecterDoublonsAsync(Guid declarationId)` dans `DeclarationWorkflowService.cs`.
   - Endpoint `GET /api/declarations/{id}/diagnostic-doublons` dans [`DeclarationsController.cs`](file:///D:/_vibe/GRF/Declaration.API/Controllers/DeclarationsController.cs).
2. **Frontend :**
   - Composant [`DetecterDoublonsModal.tsx`](file:///D:/_vibe/GRF/declaration-tva-web/src/DetecterDoublonsModal.tsx) utilisant `ApbsGrid` en lecture seule, respectant `UI_STANDARDS.md`.
   - Bouton « Détecter les doublons » intégré dans la barre d'actions de [`VerifierIntegrerPanel.tsx`](file:///D:/_vibe/GRF/declaration-tva-web/src/VerifierIntegrerPanel.tsx).
   - Bouton d'action directe « Détecter doublons » affiché en regard de l'alerte bloquante `TVA_FACTURE_SURDECLAREE` si présente.

---

## 4. Résultats des Scénarios de Test 1 à 7

| N° | Scénario | Méthode de validation | Résultat | Date |
|---|---|---|---|---|
| **1** | **Nominal** : Facture réglée en 2 fois (50% + 50%) → 1 ligne par règlement et par taux, TVA facture comptée 1 fois | `ConstruireDeclaration_Task224_Scenario1_FactureRegleeEnDeuxFois_ProduitDeuxLignesSansSurdeclaration` | ✅ OK (2 lignes, TVA 26 747,80 MAD) | 09/10/2026 |
| **2** | **Ordre inverse** : Sélection du 2e règlement avant le 1er → aucun doublon | `Scenario2_OrdreInverseDesReglements_PasDeDoublon` | ✅ OK (15 lignes, 68 281,46 MAD) | 09/10/2026 |
| **3** | **Réintégration** : Libération puis reprise d'un règlement | `MapLignesCandidates_SurFacturesRegleesEnPlusieursFois_NeDoitPasDupliquerLesLignes` | ✅ OK (0 doublon) | 09/10/2026 |
| **4** | **Multi-taux réglé en 2 fois** : 2 règlements × 2 taux = 4 lignes (pas 8) | `ConstruireDeclaration_Task224_Scenario4_FactureMultiTauxRegleeEnDeuxFois_ProduitQuatreLignes` | ✅ OK (exactement 4 lignes, 21 000 MAD) | 09/10/2026 |
| **5** | **Réglée en 3 fois dont 1 hors période** : Seule la part de la période est déclarée | `Scenario5_FactureRegleeEnTroisFois_DontUneHorsPeriode_SeulsReglementsDansPeriodeDeclares` | ✅ OK (2 lignes dans la période, TVA < max) | 09/10/2026 |
| **6** | **Cas limite arrondi** : Facture 80 414,77 réglée 40 207,38 + 40 207,39 → Σ TVA = TVA facture à 0,01 près | `ConstruireDeclaration_Task224_Scenario6_CasLimiteArrondi_SansAlerte` | ✅ OK (13 402,46 MAD, 0 alerte) | 09/10/2026 |
| **7** | **Garde-fou** : Forcer une surdéclaration de TVA / dépassement TTC → alerte bloquante `TVA_FACTURE_SURDECLAREE` | `ConstruireDeclaration_Task224_Scenario7_GardeFou_TvaFactureSurdeclaree_DeclencheAlerteError` | ✅ OK (Alerte Error déclenchée, clôture bloquée) | 09/10/2026 |

---

## 5. Exécution des builds et tests

- **Backend Build (`dotnet build DeclarationTVA.slnx`) :**  
  `0 Erreur(s)`, `Temps écoulé 00:00:20.91` (09/10/2026).
- **Backend Tests Unitaires & Orchestration :**  
  - `Declaration.Core.Tests` : **227 passés**, 0 échoués.
  - `Declaration.Orchestration.Tests` : **272 passés**, 0 échoués.
  - Total : **499 tests passés avec succès**.
- **Frontend Lint (`npm run lint`) :**  
  `0 errors` (09/10/2026).
- **Frontend Build (`npm run build`) :**  
  `✓ built in 2.13s`, 0 erreurs de compilation TypeScript/Vite (09/10/2026).

---

## 6. Nettoyage et Arbitrage PO (Périmètre 4)

- **Outil en lecture seule :** Le bouton « Détecter les doublons » est disponible sur l'écran « Vérifier & Intégrer » et appelle `GET /api/declarations/{id}/diagnostic-doublons`.
- **Règle stricte respectée :** **Aucune suppression automatique de lignes existantes n'a été exécutée en base**.
- **Action attendue du PO :**
  1. Ouvrir la déclaration `TVA1-2026-03-T` dans l'application.
  2. Cliquer sur « Détecter les doublons » pour observer les groupes de doublons identifiés.
  3. Pour réinitialiser proprement la déclaration avec le nouveau code sans perte de règlements, le PO peut désélectionner et resélectionner les règlements ou arbitrer une purge des lignes candidates non clôturées.
