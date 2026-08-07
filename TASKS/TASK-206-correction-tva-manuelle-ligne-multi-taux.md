# TASK-206 — Action "Corriger la TVA" : saisie manuelle multi-taux sur une ligne intégrée (cas douane)

> **Origine (PO, 2026-08-07)** : cas réel de factures de douane où le détail TVA lu depuis Sage est
> incohérent avec le HT (ex. HT = 5 000, TVA = 15 000 — la TVA douanière n'est pas assise sur le HT
> facture mais sur une valeur en douane distincte). Le comptable doit pouvoir décomposer la ligne en
> plusieurs taux, en choisissant par tranche soit (base HT + taux), soit (montant TTC + taux).

## Constat

- Le modèle actuel (`Declaration.Application/Entities/WorkflowEntities.cs:89-114`, classe
  `LigneCandidate`) porte **un seul** couple `HT`/`Taux`/`TVA`/`TTC`/`CodeTaxe` par ligne — une
  échéance normale ne peut donc pas être ventilée sur plusieurs taux aujourd'hui.
- Le seul mécanisme de saisie manuelle existant est celui du solde initial (TASK-025, généralisé en
  TASK-205) — strictement réservé aux échéances `EC_Type = 4`. Aucune action manuelle n'existe pour
  une facture normale valorisée par Sage/OM/FGR.
- Le code taxe Sage est déjà lu et exposé en dropdown potentiel via `F_TAXE` (colonnes `TA_Code`,
  `TA_Taux`, `TA_Intitule` — cf. `Declaration.Orchestration/LecteurTvaFgr.cs:133` et
  `Declaration.Selection/SelectionExpliqueeService.cs:187`), mais **aucun endpoint** n'expose
  aujourd'hui la liste des taxes Sage pour un sélecteur front — à créer dans cette task.

## Décisions PO (arbitrées 2026-08-07)

1. **Disponible sur n'importe quelle ligne intégrée**, via une action "Corriger la TVA" — pas
   seulement sur les lignes en anomalie de valorisation.
2. **Saisie libre, aucun contrôle de cohérence bloquant** entre la somme des brackets saisis et le
   HT/TVA/TTC d'origine (Sage) — le comptable est seul responsable de l'exactitude de sa saisie.
3. **Par bracket : taux + (base HT ou montant TTC, au choix)** — même mécanique de calcul que
   TASK-205 (`TVA = HT × taux` ou `HT = TTC ÷ (1 + taux)` selon le mode choisi).
4. **Le code taxe de chaque bracket est choisi dans le référentiel Sage `F_TAXE`** (pas de saisie
   libre en texte) — via un sélecteur alimenté par un nouvel endpoint listant `F_TAXE`
   (`TA_Code`/`TA_Taux`/`TA_Intitule`).

## Périmètre — Inclus

1. **Nouvelle table** `DM_CORRECTION_TVA_LIGNE` (migration `Declaration.Infrastructure/SQL/015_DM_CORRECTION_TVA_LIGNE.sql`,
   idempotente comme les migrations existantes) : clé `(SO_Id, EC_Id, MV_Id, NumLigne)` — `EC_Id`/
   `MV_Id` sont déjà les identifiants Sage snapshotés sur `LigneCandidate` (TASK-077,
   `WorkflowEntities.cs:140-141`), colonnes `Taux`, `BaseHT` (nullable), `MontantTtc` (nullable),
   `CodeTaxe`, `SaisiPar`, `SaisiLe`.
2. **Nouveau repository** `ICorrectionTvaLigneRepository` (même patron que
   `ISoldeInitialTvaRepository`) : `GetCorrectionsBatch` (liste de brackets par `(EC_Id, MV_Id)`),
   `EnregistrerCorrections` (remplace tous les brackets d'une ligne en une transaction),
   `SupprimerCorrection` (retour à la valorisation Sage d'origine).
3. **Nouvelle méthode** `EnregistrerCorrectionTvaAsync` (`DeclarationWorkflowService.cs`, même
   patron que `EnregistrerSaisieSoldeInitialAsync`) : remplace la `LigneCandidate` de la ligne visée
   par **une `LigneCandidate` par bracket**, portant un nouveau flag `CorrectionManuelle = true`
   (nouveau champ sur `LigneCandidate`, propagé jusqu'au DTO front) pour traçabilité et affichage
   (badge visuel distinct de la valorisation Sage normale).
4. **Réversibilité** : une resynchronisation Sage classique (`ResynchroniserLigneAsync` existant)
   sur une ligne corrigée doit supprimer les brackets de `DM_CORRECTION_TVA_LIGNE` et recharger la
   valorisation Sage d'origine — pas de état intermédiaire incohérent.
5. **Nouvel endpoint** `GET /taxes` (ou équivalent, à nommer selon convention REST du contrôleur
   existant) exposant `F_TAXE` (`TA_Code`, `TA_Taux`, `TA_Intitule`) pour alimenter le sélecteur.
6. **Nouvelle action front "Corriger la TVA"** (bouton par ligne, `DomainGrid.tsx` et/ou
   `DiagnosticModal.tsx`) ouvrant une modale de saisie multi-bracket : par bracket, sélecteur
   `F_TAXE` (remplit `Taux` + `CodeTaxe` + `IntituleTaxe`), puis choix HT ou TTC + calcul en direct
   de la valeur déduite. Bouton "Annuler la correction" pour revenir à Sage (point 4).
7. **Recap/export** : chaque bracket alimente `RecapParTaux` comme une ligne normale — aucun
   changement requis sur `Exporter.cs`/`RecapParTaux` (le regroupement `(Taux, CodeTaxe)` existant
   suffit, `CodeTaxe` étant désormais toujours renseigné puisque choisi dans `F_TAXE`, cf. décision 4).

## Périmètre — Exclu

- Aucune modification de la lecture Sage/OM/FGR normale (`ConstructeurDeclaration.cs`,
  `LecteurTvaFgr.cs`) — la correction est une **surcouche manuelle post-valorisation**, jamais une
  modification de la logique de lecture Sage.
- Pas de fusion avec le mécanisme solde initial (TASK-205) — tables et repositories distincts (une
  ligne solde initial n'a par définition aucune valorisation Sage à laquelle revenir).
- Aucun contrôle de cohérence bloquant (conforme décision PO 2) — ne pas ajouter de validation
  "somme = HT Sage" de sa propre initiative.

## À documenter dans le VERIFY (pas d'improvisation autorisée)

- Convention de nommage exacte retenue pour l'endpoint `GET /taxes` (aligner sur le style des
  endpoints existants du contrôleur choisi).
- Comportement affiché quand une ligne "corrigée manuellement" est ensuite reportée/exclue puis
  réintégrée (TASK-098) — la correction doit-elle survivre ? Documenter le choix pris et la
  justification si le fichier TASK ne le précise pas explicitement au moment du développement.
- Confirmation que le badge "correction manuelle" est visible dans l'écran ④ Confirmer (TASK-202,
  une fois celle-ci développée) sans avoir dû modifier son périmètre.

## Fichiers impactés

- Nouveau : `Declaration.Infrastructure/SQL/015_DM_CORRECTION_TVA_LIGNE.sql`
- Nouveau : `Declaration.Orchestration/ICorrectionTvaLigneRepository.cs` + implémentation SQL
- [Declaration.Application/Entities/WorkflowEntities.cs:89-114](../Declaration.Application/Entities/WorkflowEntities.cs#L89-L114) (ajout `CorrectionManuelle`)
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs)
- [Declaration.API/Controllers/DeclarationsController.cs](../Declaration.API/Controllers/DeclarationsController.cs) (nouvel endpoint correction + nouvel endpoint `F_TAXE`)
- [Declaration.API/Dtos/LigneCandidateDto.cs](../Declaration.API/Dtos/LigneCandidateDto.cs) (propagation `CorrectionManuelle`)
- Front : `declaration-tva-web/src/DomainGrid.tsx`, `declaration-tva-web/src/DiagnosticModal.tsx`, `declaration-tva-web/src/api.ts`

## Critères de validation

- Une ligne intégrée valorisée par Sage, "corrigée" en 2 brackets à taux différents (un en mode
  HT+taux, l'autre en mode TTC+taux), produit 2 `LigneCandidate` correctement sommées dans le récap,
  chacune avec son `CodeTaxe`/`IntituleTaxe` issu de `F_TAXE`.
- "Annuler la correction" restaure exactement la valorisation Sage d'origine (même HT/TVA/TTC
  qu'avant la correction).
- Build + tests back et front verts.

## Dépendances / risques

- **Indépendant de TASK-204/TASK-202** dans son back-end ; le bouton front "Corriger la TVA" devra
  être ajouté au composant grille en vigueur au moment du développement (`DomainGrid.tsx` actuel, ou
  AG Grid si TASK-204 est déjà livrée — vérifier l'état du repo avant de coder, ne pas assumer).
- **Réutilise le motif de saisie multi-bracket de TASK-205** (composant/logique de calcul HT↔TTC) —
  factoriser si TASK-205 est développée en premier, sans bloquer si l'ordre est inversé.
- **Risque de conformité** : une correction manuelle peut rendre la déclaration incohérente avec la
  comptabilité Sage — c'est un choix PO assumé (décision 2), pas un défaut à corriger côté dev.
