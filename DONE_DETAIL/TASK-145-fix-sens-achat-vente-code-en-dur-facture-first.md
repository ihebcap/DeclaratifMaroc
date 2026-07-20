# TASK-145 — Fix critique : `Sens` Achat/Vente codé en dur dans la lecture facture-first (fausses erreurs "introuvable")

Status: 🆕 à faire
Priority: HIGH
Risk: CRITICAL (corrige la source des fausses anomalies "Facture introuvable" — bug de fond touchant potentiellement toutes les déclarations)
Module: Declaration.Selection / Declaration.Application

> **Origine :** diagnostic en session (20/07/2026) suite aux logs `[VALO] EC_Id=21069 pièce
> FA2600559 non mise en cache — OM en erreur : Facture d'achat FA2600559 introuvable.` (et 5 autres
> lignes identiques) alors que le PO a vérifié directement en base que ces factures existent bien
> dans Sage (`RT_ECHEANCE` + `F_DOCREGL`). Investigation confirmée par lecture de code, pas une
> hypothèse.

## Constat (preuve de code)

- **Erreur de terminologie initiale corrigée en session** : `RT_ECHEANCE.DO_Domaine` suit l'enum
  Sage `ErpDomaine` (`Tresorerie.Core.Enum.ErpDomaine` — `scratch/decompiled/Core/Tresorerie.Core.Enum/ErpDomaine.cs`) :
  **`Vente=0`, `Achat=1`, `Stock=2`, `Ticket=3`, `Interne=4`**. `FA2600619` a `DO_Domaine=0` → c'est
  une **facture de vente**, confirmé par le PO qui la connaît directement.
- **Bug confirmé** : [Declaration.Selection/SelectionExpliqueeService.cs:215-329](../Declaration.Selection/SelectionExpliqueeService.cs:215)
  (`LireFacturesDepuisPeriodeAsync` / `GetFactureFirstSql`, utilisée par le bouton « Rafraîchir
  valorisation » de l'écran Factures et par `DeclarationWorkflowService.RafraichirValorisationAsync`) :
  - Le `WHERE` de `GetFactureFirstSql` (lignes 321-329) filtre par `E.SO_Id`, `E.EC_Type`,
    `E.DO_Date` — **il ne filtre JAMAIS par `E.DO_Domaine`**. Le filtre `MV_Domaine IN
    (@domaineFournisseur, @domaineDepense)` (ligne 315) ne s'applique qu'à la jointure
    `RT_MOUVEMENT` (pour retrouver le règlement), pas à la sélection de base des échéances.
  - Ligne 263 : `await MapAndEvaluate(result, rows, dateDebut, dateFin, SensAffectation.Achat,
    null);` — **`Sens` est codé en dur à `Achat` pour TOUTES les lignes lues**, avec le commentaire
    (ligne 259) « sens=Achat (EC_Type=0 = factures fournisseur/dépense) » — postulat faux : `EC_Type=0`
    signifie seulement « échéance lue via Sage OM », pas « facture fournisseur ». `FA2600619`
    (`EC_Type=0`, `DO_Domaine=0`=Vente) le prouve.
- **Conséquence tracée bout en bout** :
  `AffectationCandidate.Sens=Achat` → [OrchestrateurDeclaration.cs:81](../Declaration.Orchestration/OrchestrateurDeclaration.cs:81)
  convertit en chaîne `"Achat"` → [WorkerInvoker.cs:68](../Declaration.Orchestration/WorkerInvoker.cs:68)
  transmet au worker → [SageTaxReaderService.cs:456-463](../SageTaxReader/SageTaxReader.Core/SageTaxReaderService.cs:456)
  appelle `docFactoryAchat.ExistPiece(DocumentTypeAchatFacture, "FA2600619")` — qui répond
  honnêtement `false` puisque `FA2600619` est un document de **vente**, jamais cherché dans
  `docFactoryVente`. D'où le message « Facture d'achat FA2600619 introuvable » alors que la pièce
  existe réellement (côté vente).
- **Risque de contamination du cache (hypothèse forte, à confirmer par l'implémenteur)** : le
  commentaire de `ResynchroniserLigneAsync`
  ([DeclarationWorkflowService.cs:515-520](../Declaration.Application/Services/DeclarationWorkflowService.cs:515))
  indique que la sentinelle `ERREUR` déjà en cache fait considérer la pièce comme définitivement
  réglée par `TryServireDepuisCache`, empêchant toute nouvelle lecture Sage. Si `Rafraîchir
  valorisation` écrit une sentinelle `ERREUR` erronée (« Facture d'achat introuvable ») pour une
  échéance de vente authentique, cela **pourrait bloquer durablement** toute tentative ultérieure et
  correcte de valorisation « Vente » de cette même échéance ailleurs dans le pipeline (chemin
  encaissement, `SelectionnerAffectationsService`/`SelectionExpliqueeService.SelectionnerExpliqueeAsync`).
  À vérifier/confirmer pendant l'implémentation.

## Objectif

1. Corriger `GetFactureFirstSql` : cette fonction est documentée comme « achat/dépense uniquement »
   (commentaires lignes 253-255 : « Les encaissements client restent gérés par
   `SelectionnerExpliqueeAsync` ») — ajouter le filtre manquant `AND E.DO_Domaine = @achatDomaine`
   (valeur `1` = `ErpDomaine.Achat`) au `WHERE` de la requête, pour que cette lecture facture-first
   ne remonte **jamais** d'échéance de vente. Ne PAS tenter d'étendre cette fonction pour aussi
   couvrir les ventes — ce n'est pas son périmètre (déjà géré par le chemin encaissement existant),
   ça duplicaquerait une logique existante (anti-pattern interdit par `ARCHITECTURE.md` §5).
2. Vérifier qu'aucune sentinelle `ERREUR` erronée n'a été écrite en cache pour des échéances de
   vente par le passé (requête de diagnostic sur `DM_VENTILATION_SAGE_CACHE` croisée avec
   `RT_ECHEANCE.DO_Domaine=0` et `CodeTaxe='ERREUR'`) — si des entrées erronées existent, les purger
   (lecture seule stricte pour le diagnostic ; la purge elle-même doit être un `DELETE` ciblé et
   documenté dans le VERIFY, jamais un `TRUNCATE`/reset global).
3. Documenter clairement dans le code (commentaire) la distinction entre les deux notions de
   "domaine" du projet — `RT_MOUVEMENT.MV_Domaine` (type de mouvement de trésorerie :
   `Domaine_ReglementFournisseur=1`, `Domaine_Depense=6`, etc. — voir `GrfEnums.cs`) et
   `RT_ECHEANCE.DO_Domaine` (type de document Sage : enum `ErpDomaine`, Vente=0/Achat=1/...) — pour
   éviter que l'erreur de ce TASK (et celle que l'architecte a faite en session) ne se reproduise.
4. **Point critique découvert en session, à investiguer explicitement (pas seulement supposer que
   le fix suffit)** : la branche `Sens=Vente` du worker (`SageTaxReaderService.cs:437-455`,
   `docFactoryVente.ExistPiece`/`ReadPiece`) est structurellement complète mais **n'a aucune preuve
   de fonctionnement réel** dans ce projet — les seuls tests qui l'exercent (`OrchestrateurTests.cs`)
   utilisent un worker fictif, jamais le vrai Sage COM. Pire : le cache `TryServireDepuisCache`
   ([OrchestrateurDeclaration.cs:347-400](../Declaration.Orchestration/OrchestrateurDeclaration.cs:347))
   est indexé **uniquement par `EC_Id`**, jamais par `Sens`, et une sentinelle `ERREUR` avec bruts
   déjà capturés n'est **plus jamais rejouée** (TASK-072, "pièce durablement cassée"). Si le bug
   Achat de cette TASK a écrit une sentinelle erronée en premier pour un `EC_Id` de vente, le chemin
   encaissement (`SelectionnerExpliqueeAsync`, `Sens=Vente` correct) n'a peut-être **jamais eu
   l'occasion de tourner réellement** pour cet `EC_Id` — masquant une éventuelle défaillance propre
   à la branche Vente elle-même. **Incohérence non résolue à noter** : parmi les échéances
   `DO_Domaine=0` observées en session, certaines (`FA2600106`, `FA2502324`...) ont été trouvées avec
   succès par `docFactoryAchat.ExistPiece` (avant ce fix), d'autres (`FA2600559`...) non — le
   `DO_Domaine` seul n'explique pas cette différence. Ne pas clore cette TASK sans avoir testé
   positivement au moins une lecture Vente réelle (voir Validation).

## Garde-fous

- Lecture seule pour le diagnostic (étape 2) ; toute purge de cache doit cibler EXACTEMENT les
  entrées `ERREUR` dont l'`EC_Id` correspond à une échéance `DO_Domaine != 1` (vente/stock/ticket/interne),
  jamais un flush global du cache.
- Ne pas toucher au chemin encaissement/vente existant (`SelectionnerAffectationsService.cs`,
  `SelectionExpliqueeService.SelectionnerExpliqueeAsync`) — il gère déjà `Sens=Vente` correctement
  pour son propre périmètre (règlements encaissement).
- Ne pas modifier `SageTaxReaderService.cs` (le worker Sage lui-même) préventivement — le bug de
  cette TASK est en amont (Sens mal calculé). Mais **ne pas supposer** que la branche `Vente` du
  worker fonctionne correctement une fois le bon `Sens` envoyé — c'est justement ce qui doit être
  vérifié (point 4 du Constat), pas présumé.

## Files

- [Declaration.Selection/SelectionExpliqueeService.cs](../Declaration.Selection/SelectionExpliqueeService.cs) (`GetFactureFirstSql` l.282-329, appel `MapAndEvaluate` l.263).
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`RafraichirValorisationAsync` l.551-587 — appelant, pas de changement de logique attendu ici, juste vérifier la propagation).
- `Declaration.Orchestration/Repositories/VentilationSageCacheRepository.cs` (purge ciblée éventuelle, si des sentinelles erronées sont trouvées).
- `Declaration.Selection/GrfEnums.cs` (ajouter une constante explicite type `ErpDomaine_Achat = 1` si absente, pour éviter un magic number `1` dans le SQL).

## Validation

- [ ] Build back OK.
- [ ] Rejeu réel : `Rafraîchir valorisation` sur la période contenant `FA2600619`/`FA2600559`/`FA2600564`/`FA2600582`/`FA2600600`/`FR2600077` (`EC_Id` 21069-21074) — ces échéances ne doivent PLUS apparaître dans le rapport d'erreurs de ce rafraîchissement (elles doivent être silencieusement exclues de cette lecture achat-only, pas "en erreur").
- [ ] Confirmer par requête SQL que `GetFactureFirstSql` ne retourne plus aucune ligne avec `DO_Domaine <> 1`.
- [ ] Si des sentinelles `ERREUR` erronées existaient en cache pour des `EC_Id` de domaine vente, elles sont purgées et documentées (liste des `EC_Id` purgés dans le VERIFY).
- [ ] **Preuve positive de la branche Vente réelle (obligatoire, ne pas se contenter de supposer)** :
      purger le cache pour `EC_Id=21849` (`FA2600106`, `TVA1-2026-02`) et `EC_Id=24098` (`FA2502718`,
      `TVA1-2026-03`) — les deux ont une vraie affectation/règlement encaissement et passent par
      `SelectionnerExpliqueeAsync` (`Sens=Vente`) — puis relancer le chargement de candidates de ces
      déclarations et vérifier dans `DM_VENTILATION_SAGE_CACHE` qu'une lecture **fraîche** (nouveau
      `DateLecture`) réussit avec `Source='OM'`, sans erreur, montants non nuls. Si l'un des deux
      échoue encore ("introuvable" ou autre), **documenter précisément l'échec dans le VERIFY** — ce
      serait la preuve qu'un problème distinct affecte réellement la branche Vente du worker
      (`SageTaxReaderService.cs:437-455`), à traiter comme un nouveau blocage, pas comme un succès de
      cette TASK.
- [ ] Aucune duplication de la logique `SensAffectation` déjà présente dans `SelectionnerAffectationsService.cs`.

## Dépendances / risques

- **TASK-149** (ré-audit des 14 lignes `FACTURE_NON_VENTILEE` de TASK-143) doit être exécutée
  **après** celle-ci — le nombre réel de lignes non valorisables pourrait diminuer une fois ce bug
  corrigé, certaines des 14 lignes étant peut-être des victimes de ce même bug plutôt que de
  véritables anomalies de données.
- Risque : si la contamination de cache (point 2 du Constat) est confirmée et étendue, l'impact
  réel de ce bug pourrait dépasser les 6 `EC_Id` observés en session — élargir la requête de
  diagnostic à toute la base, pas seulement à la période observée.
