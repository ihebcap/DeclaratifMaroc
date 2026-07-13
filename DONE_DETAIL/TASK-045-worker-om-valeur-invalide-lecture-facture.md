# TASK-045 — Worker OM : « Valeur invalide ! » sur 100 % des lectures de facture Sage (EC_Type=0)

> **BLOQUANT valorisation famille B.** C'est le point qui empêche les factures Sage de se valoriser.
> Investigation **worker séparée**, à mener **contre Sage réel** — à lancer dans une session dédiée.

## Contexte (faits mesurés, traçabilité TASK récente)
Depuis que la valorisation est **entièrement tracée** (mémoire `grf-valorisation-tracabilite-blocage-om` :
`logs/valorisation.log` + rapport HTTP `{ facturesTraitees, nbErreurs, erreurs[] }`), un run réel
janvier 2026 donne :
- **200 factures traitées → 170/170 lectures OM `EC_Type=0` échouent** avec le motif exact
  **« Valeur invalide ! »** (alerte `FACTURE_ILLISIBLE_OM`, `DocumentTaxesInfo.EnErreur=true` +
  `MotifErreur`), matérialisé par ligne dans le log (ex. `EC_Id=18219 pièce FC2600025 non mise en
  cache — OM en erreur : Valeur invalide !`) et `[VALO] batch OM rendu : 170 pièce(s), 170 en erreur.`
- Les **30 `TIERS_SANS_ICE` + 30 `TIERS_SANS_IF`** sont de la **qualité de données** (tiers sans
  identifiant), **pas** des échecs de valorisation — hors périmètre de cette tâche.

**Conclusion isolée :** l'échec est **systémique** (100 %, pas un cas limite comptabilisée), **côté
worker** `SageTaxReader.Core/SageTaxReaderService.cs`. Le message « Valeur invalide ! » est une
**COMException Sage** levée dans le bloc de lecture par pièce, capturée en `catch (Exception ex) {
pieceError = ex; }` (`LireFactures`, ~ligne 449) puis remontée en motif. Reste à déterminer **quelle
étape** la lève.

## Localisation du code (worker net48, thread STA, session réutilisée TASK-023)
`SageTaxReader.Core/SageTaxReaderService.cs`, méthode `LireFactures` (batch) et jumelles unitaires
`LireFactureVente` (~145) / `LireFactureAchat` (~183). Séquence par pièce suspecte :
1. `docFactory.ExistPiece(docTypeToUse, piece)` — fallback type `7`/`17` si absent (Program-like).
2. `(IBODocumentVente3|IBODocumentAchat3)docFactory.ReadPiece(docTypeToUse, piece)`.
3. `doc.Valorisation` (déclenche le calcul Sage — **candidat n°1** pour « Valeur invalide ! »).
4. `ExtraireTaxes(doc.Valorisation, sens, doc, app.CptaApplication, deviseInfo.Decimales, cache)`.

## Objectif de la tâche
```
Entrée  : ≥3 pièces EC_Type=0 réelles connues échouant « Valeur invalide ! » (ex. FC2600025)
Étape 1 : DIAGNOSTIC — isoler l'appel COM exact qui lève, et pourquoi (100 % → cause structurelle)
Étape 2 : CORRECTIF ciblé (lecture correcte de la valorisation OM) SANS fabriquer de valeur
Sortie  : ces pièces rendent HT/TVA/TTC réels ; le cache TASK-024 se remplit ; factures valorisées
```

## Étapes
1. **Repro isolée** (aucune modif) : lancer le worker en **unitaire** sur une pièce KO connue
   (`SageTaxReader.Console.exe` mode unitaire, cf. `Program.cs` ~117-121) et **capturer la stack COM
   complète** (HRESULT, `ex.ToString()`), pas seulement `ex.Message`. Confirmer que le batch et
   l'unitaire échouent de la **même** façon.
2. **Instrumentation temporaire** : logguer autour de chaque étape (ExistPiece OK ? type retenu ?
   ReadPiece OK ? l'accès à `doc.Valorisation` lève-t-il ? sinon `ExtraireTaxes` ?). Déterminer
   **précisément** le point de levée.
3. **Hypothèses à trancher** (probables, à confirmer sur Sage réel) :
   - **H1 — Domaine/type de document** : le type forcé (`DocumentTypeVenteFacture` / `7`, `Achat` /
     `17`) ne correspond pas au domaine réel des pièces `EC_Type=0` du client → `ReadPiece` rend un
     document dont `Valorisation` est invalide. Vérifier le vrai `DO_Domaine`/`DO_Type` des pièces.
   - **H2 — Valorisation non calculable en lecture** : `doc.Valorisation` exige un contexte (devise,
     dépôt, exercice, droits) absent dans la session ouverte → « Valeur invalide ! ». Comparer avec le
     PoC historique `DISTRI_DEMO` qui, lui, **fonctionnait** (TASK-002/023 : 12/12) → qu'est-ce qui
     diffère entre `DISTRI_DEMO` et `[NEW_EMA DISTRIBUTION]` (société, plan, devise, version Sage) ?
   - **H3 — Numéro de pièce** : le `piece` transmis (`DO_Piece` ? `EC_...` ?) n'est pas la clé
     attendue par `ExistPiece/ReadPiece` pour cette base → mauvaise pièce lue. Vérifier la source du
     numéro côté sélection.
   - **H4 — Devise / décimales** : `ObtenirDeviseSociete` rend une valeur qui fait échouer
     `ExtraireTaxes`/`Valorisation` sur cette société.
4. **Correctif ciblé** selon le diagnostic — **sans jamais fabriquer de valeur** ni avaler l'erreur :
   la transparence prime (mémoire `grf-objectif-confiance-transparence`). Si une classe de pièces
   reste réellement illisible, elle **doit** rester `FACTURE_ILLISIBLE_OM` (jamais un 0 muet).
5. **Re-run** sur janvier 2026 : mesurer le nouveau taux de succès ; le cache `GRC_VENTILATION_SAGE_CACHE`
   doit se remplir pour les pièces corrigées ; l'écran Factures doit passer de « non valorisé » à HT/TVA
   réels.

## Livrables
- Diagnostic écrit : **l'appel COM exact** qui lève + la cause racine (H1..H4 tranchées, preuves).
- Correctif dans `SageTaxReaderService.cs` (et/ou paramétrage type/domaine), **net48 pur**, aucune
  valeur forfaitaire, aucune erreur avalée.
- `VERIFY/TASK-045_verify.md` : preuve réelle —
  - avant : `nbErreurs` sur la période (170/170) ;
  - après : pièces échantillons (dont `FC2600025`) rendant HT/TVA/TTC **vérifiés vs Sage** ;
  - taux de succès global recalculé ; cache rempli (comptage SQL) ;
  - contrôle : **aucune écriture GRFN**, worker toujours out-of-process, isolation par pièce conservée.

## Critères de validation
- Cause racine **identifiée et prouvée** (pas une rustine à l'aveugle sur un message).
- Les factures `EC_Type=0` valorisables rendent des montants **réels et exacts** (recoupés Sage).
- Toute pièce réellement illisible reste **tracée** `FACTURE_ILLISIBLE_OM` — jamais de 0 silencieux,
  jamais de valeur fabriquée.
- Perf préservée : session réutilisée (TASK-023), pas de `Parallel.ForEach`, timeout par pièce.
- Lecture seule stricte côté GRF/Sage ; aucune écriture dans la base GRFN.

## Risques / dépendances
- **Contre Sage réel obligatoire** : le diagnostic exige des lectures OM live (le PO était prudent
  sur les sondes worker — **cadrer avant de sonder** ; lecture seule, pièces échantillons).
- **Écart environnement** : `DISTRI_DEMO` (PoC OK) ≠ `[NEW_EMA DISTRIBUTION]` (prod KO) — la comparaison
  des deux sociétés est probablement la clé (H2). Prévoir d'accéder aux deux.
- **Débloque** : la valorisation famille B (écran Factures, cache TASK-024) et le fallback Sage de
  **TASK-043** (TVA par règlement, origine Sage). Tant que 045 n'est pas résolue, ces surfaces
  restent en état « Indisponible » honnête, pas faux.
- **Indépendant de TASK-044** (déploiement) : les deux avancent en parallèle.
