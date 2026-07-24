# TASK-164 — 197 lignes intégrées en anomalie « Facture introuvable » bloquent TVA1-2026-06 + le bouton Diagnostiquer ne les voit jamais

Status: 🆕 à faire
Priority: CRITIQUE
Risk: CRITICAL (déclaration en cours totalement bloquée ; risque que d'autres déclarations soient affectées par la même cause)
Module: Declaration.Orchestration / Declaration.Application / declaration-tva-web (VerifierIntegrerPanel/DiagnosticModal)

> **Origine** : signalement PO (23-24/07/2026) sur la facture `FC2600515` puis `FC2600517`
> (déclaration `TVA1-2026-06`, `DT_Id=63`), diagnostiquées en session par l'architecte. En testant
> l'écran réel (`http://localhost:5280/`, étape ② Vérifier & Intégrer de `TVA1-2026-06`), constat
> **bien plus large que les 2 factures signalées** : **197 lignes sur 787** (25 %, ~1 244 003,93 MAD
> de HT) portent le motif identique `Ligne en anomalie de recalcul (...) : Facture introuvable (DTO
> non fourni)`. Écran bloqué : « 197 contrôles bloquants — intégration impossible », bouton
> « Confirmer intégration » désactivé. Capture d'écran disponible sur demande (session Playwright).

## Constat (2 problèmes distincts, à traiter ensemble car le second cache le premier)

### A. Cause racine des 197 échecs « Facture introuvable » — NON ÉLUCIDÉE, à investiguer

- Le message exact (`"Facture introuvable (DTO non fourni)."`,
  [ConstructeurDeclaration.cs:82](../Declaration.Core/ConstructeurDeclaration.cs#L82)) ne sort que
  quand la résolution facture renvoie **`null`** (pas un `DocumentTaxesInfo.EnErreur=true` avec motif
  Sage précis) — cf. [OrchestrateurDeclaration.cs:282-311](../Declaration.Orchestration/OrchestrateurDeclaration.cs#L282-L311)
  (`resoudreFactureBrute`) et [WorkerInvoker.cs:51-62](../Declaration.Orchestration/WorkerInvoker.cs#L51-L62)
  (le worker individuel renvoie `null` sans exception sur code de sortie ≠ 0).
- **2 cas déjà investigués individuellement en session, causes DIFFÉRENTES à chaque fois** :
  - `FC2600515` (`EC_Id` non noté, réglé) : cache de ventilation jamais alimenté ; un rafraîchissement
    facture-first ciblé (`2026-04-17→2026-04-17`) l'a corrigée du premier coup (`[VALO-050] === Fin :
    6 traitée(s), 0 en erreur ===`) — semble avoir été un problème de fraîcheur de cache, pas un vrai
    échec Sage permanent.
  - `FC2600517` (`EC_Id=21887`, `DT_Id=63`) : **`DT_Id` déjà posé** →
    [SelectionExpliqueeEvaluator.cs:67](../Declaration.Selection/SelectionExpliqueeEvaluator.cs#L67)
    (`MotifRejet.DejaDeclare`, testé **avant tout autre motif**) → exclue de
    `EstValorisable` ([SelectionExpliqueeModels.cs:55-56](../Declaration.Selection/SelectionExpliqueeModels.cs#L55-L56))
    → le rafraîchissement facture-first l'ignore **volontairement** (par construction, pas un bug) →
    reste indéfiniment au motif générique « cache absent »
    ([FactureInterrogation.cs:159](../Declaration.Application/Entities/FactureInterrogation.cs#L159)),
    qui ne dit RIEN de la cause réelle.
  - **Donc à ce stade : deux mécanismes différents ont produit le même message pour deux factures
    différentes.** Rien ne prouve que les 197 lignes partagent une cause commune — c'est
    l'hypothèse de travail n°1 ci-dessous, à vérifier, pas à supposer.
- **Piste d'investigation prioritaire (hypothèse, PAS une certitude)** : les 197 lignes ont toutes le
  statut `Intégrée` (`statutLigne=1`, `DT_Id=63`) — elles ont donc été valorisées (ou pas) au moment
  d'une intégration en masse. Vérifier si elles partagent une même fenêtre temporelle d'intégration
  et si cette fenêtre correspond à un incident de timeout du batch OM déjà documenté
  ([TASK-159](../DONE_DETAIL/TASK-159-*.md) — incident 361 pièces du 23/07/2026, ancien timeout fixe
  300s indépendant du volume) : un batch interrompu en masse produirait exactement ce symptôme
  (beaucoup de pièces jamais rendues → `null` → message générique), sans rapport avec un vrai défaut
  Sage pièce par pièce. **Ne pas s'arrêter à cette hypothèse sans preuve** : vérifier réellement les
  timestamps (`DM_VENTILATION_SAGE_CACHE.DateLecture` si présent, logs `[VALO]`/`[WORKER BATCH]` de la
  période d'intégration) avant de conclure.
- Objectif de cette investigation : pour un échantillon d'au moins 10 des 197 lignes, déterminer la
  cause RÉELLE (requête SQL type donnée dans TASK-145/session : jointure `RT_ECHEANCE` →
  `RT_AFFECTATION` → `RT_MOUVEMENT`, + vérification du `DO_Type`/`DO_Domaine` Sage réel de la pièce
  côté client). Documenter les causes trouvées dans le VERIFY, même si elles diffèrent d'une ligne à
  l'autre — **ne jamais fabriquer une cause unique par commodité**.

### B. Le bouton « Diagnostiquer » (TASK-144/147) n'apparaît JAMAIS pour une ligne déjà intégrée en anomalie

- Vérifié en conditions réelles (build front actuellement servi par `Declaration.API`, pas une
  supposition) : sur `TVA1-2026-06`, les 197 lignes anormales n'affichent **aucun** bouton
  « Diagnostiquer » — seulement une liste texte brute « Anomalies bloquantes » sans action possible.
- Cause : [VerifierIntegrerPanel.tsx:79](../declaration-tva-web/src/VerifierIntegrerPanel.tsx#L79)
  ```js
  const NON_VALORISE = new Set([2, 3, 4]); // Exclue, Reportée, Écartée
  ```
  Le tableau « lignes non valorisées » (et donc le bouton Diagnostiquer,
  [VerifierIntegrerPanel.tsx:806-815](../declaration-tva-web/src/VerifierIntegrerPanel.tsx#L806-L815))
  n'est rendu QUE pour les lignes dont `statutLigne ∈ {2,3,4}`. Les 197 lignes ont `statutLigne=1`
  (**Intégrée** — [VerifierIntegrerPanel.tsx:70](../declaration-tva-web/src/VerifierIntegrerPanel.tsx#L70)),
  jamais inclus dans `NON_VALORISE`. Elles ne remontent que via la liste générique
  `model.Alertes`/« Anomalies bloquantes » (back-end,
  [DeclarationWorkflowService.cs:1023-1036](../Declaration.Application/Services/DeclarationWorkflowService.cs#L1023-L1036)),
  sans aucun bouton attaché.
- **Conséquence métier** : une ligne déjà intégrée avec une anomalie non résolue n'a **aucun chemin de
  réparation dans l'app** — ni le rafraîchissement en masse (l'exclut par construction, cf. §A), ni le
  diagnostic/recalcul par ligne (invisible pour ce statut). C'est exactement le cas d'usage que
  TASK-144/147 a été construit pour couvrir (« le comptable comprend PAR LUI-MÊME pourquoi une ligne
  est en anomalie et quoi faire »), qui échoue silencieusement dès que la ligne est déjà intégrée.
- **Point à vérifier avant de coder** : le composant `DiagnosticModal`/l'action
  `recalculerLigneDepuisCache` (back-end, TASK-147) sont-ils fonctionnellement compatibles avec une
  ligne `statutLigne=1` (déjà verrouillée par `DT_Id`), ou le back-end lui-même refuse-t-il de
  recalculer une ligne déjà intégrée (auquel cas il faut aussi un correctif back, pas seulement
  afficher le bouton) ? Ne pas se contenter d'étendre `NON_VALORISE` côté front sans vérifier que
  l'action déclenchée fonctionne réellement pour ce statut.

## Objectif

1. **Investigation (§A)** : sur un échantillon ≥10 des 197 lignes de `TVA1-2026-06`, déterminer la
   cause réelle de `FACTURE_INTROUVABLE`/`DejaDeclare`-sans-cache. Si une cause commune et corrigeable
   émerge (ex. incident batch TASK-159), corriger le code correspondant. Si les causes sont
   hétérogènes ou nécessitent un arbitrage métier (facture réellement absente côté Sage), documenter
   et escalader au PO/service comptable — **ne pas inventer de correctif si aucune cause de code
   n'est prouvée** (même garde-fou que TASK-150).
2. **Front (§B)** : étendre la visibilité du tableau « lignes non valorisées » + bouton
   « Diagnostiquer » aux lignes `statutLigne=1` (Intégrée) portant un `motif` non vide — pas
   seulement 2/3/4. Vérifier au préalable (back) que `recalculerLigneDepuisCache` fonctionne pour ce
   statut ; sinon l'étendre aussi pour accepter une ligne intégrée en anomalie (sans jamais permettre
   de modifier une ligne intégrée SANS anomalie — périmètre strictement borné aux lignes à motif
   non vide).
3. Une fois §1/§2 en place, rejouer `TVA1-2026-06` réellement (pas une fixture) et vérifier la
   réduction du nombre de lignes bloquantes — documenter le nombre résiduel avec sa cause dans le
   VERIFY, ne pas prétendre à zéro anomalie si certaines nécessitent un arbitrage PO (cf. précédent
   TASK-150).

## Garde-fous

- Lecture seule pour tout diagnostic SQL (aucun `UPDATE`/`DELETE` en dehors du mécanisme de recalcul
  existant déjà validé par TASK-147).
- Ne jamais permettre au recalcul/diagnostic d'une ligne intégrée de changer son `DT_Id` ou de la
  faire sortir de la déclaration — seul le contenu valorisé (HT/TVA/motif) peut être rafraîchi.
- Ne jamais fabriquer un montant pour une ligne dont la cause réelle reste indéterminée — si une
  ligne reste irrémédiable après investigation, le motif doit rester visible et bloquant, pas
  contourné.
- Si l'hypothèse TASK-159 (incident batch) se confirme comme cause, vérifier si **d'autres
  déclarations** (pas seulement `TVA1-2026-06`) ont été intégrées dans la même fenêtre — champ
  d'impact potentiellement plus large que cette seule déclaration.

## Files

- [Declaration.Orchestration/OrchestrateurDeclaration.cs](../Declaration.Orchestration/OrchestrateurDeclaration.cs) (résolution facture, §A).
- [Declaration.Orchestration/WorkerInvoker.cs](../Declaration.Orchestration/WorkerInvoker.cs) (retour `null` silencieux sur échec worker, §A).
- [Declaration.Selection/SelectionExpliqueeEvaluator.cs](../Declaration.Selection/SelectionExpliqueeEvaluator.cs) / [SelectionExpliqueeModels.cs](../Declaration.Selection/SelectionExpliqueeModels.cs) (`DejaDeclare`/`EstValorisable`, §A).
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`RecalculerLigneDepuisCache` existant TASK-147, alertes §B).
- [declaration-tva-web/src/VerifierIntegrerPanel.tsx](../declaration-tva-web/src/VerifierIntegrerPanel.tsx) (`NON_VALORISE`, tableau + bouton Diagnostiquer, §B).
- [declaration-tva-web/src/DiagnosticModal.tsx](../declaration-tva-web/src/DiagnosticModal.tsx) (panneau de diagnostic/recalcul existant TASK-144/147, §B).

## Validation

- [ ] Build back + front OK.
- [ ] Échantillon ≥10 lignes parmi les 197 diagnostiquées individuellement, cause réelle documentée
      pour chacune dans le VERIFY (même si hétérogène).
- [ ] Si cause corrigeable trouvée : correctif appliqué, rejeu réel de `TVA1-2026-06` montrant une
      réduction mesurée du nombre de lignes bloquantes (avant/après, capture ou export).
- [ ] Bouton « Diagnostiquer » visible et fonctionnel sur au moins une ligne réelle `statutLigne=1`
      en anomalie (capture d'écran ou test Playwright réel, pas une fixture).
- [ ] `recalculerLigneDepuisCache` vérifié fonctionnel (ou corrigé) pour une ligne `statutLigne=1`.
- [ ] Aucune régression sur le comportement existant pour les statuts 2/3/4 (tests
      `Declaration.Orchestration.Tests` existants rejoués verts).
- [ ] Si des lignes restent irrémédiables : listées explicitement dans le VERIFY avec leur motif réel,
      signalées au PO pour arbitrage — jamais présentées comme résolues sans preuve.

## Dépendances / risques

- Dépend potentiellement de l'ampleur réelle de l'incident TASK-159 (si confirmé comme cause) —
  pourrait révéler un impact sur d'autres déclarations déjà intégrées, hors périmètre strict de cette
  TASK mais à signaler immédiatement au PO si constaté.
- Risque de sous-estimer l'hétérogénéité des causes : ne pas clore cette TASK en supposant qu'un seul
  correctif résout les 197 lignes sans les avoir échantillonnées.
