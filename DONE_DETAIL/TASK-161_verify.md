# TASK-161 — VERIFY

> ⚠️ **CORRECTION TASK-179 (24/07/2026)** : le niveau 2 "défaut par tiers" (mapping
> `P_SOCIETECODEACTIVITETIERS`, méthodes `GetMappingCodeActiviteTiersAsync`/
> `ChargerMappingCodeActiviteTiersAsync` décrites ci-dessous) a été **retiré** de la cascade —
> arbitrage PO (TASK-171 §1, option B) : jamais fonctionnel en réel (`SCAT_ErpIntitule` n'est pas le
> nom du tiers, TASK-171) et cause d'un crash 500 en production chez un client sans la colonne
> `SCAT_NumeroTiers`. `CodeActiviteResolver.Resoudre` ne prend plus que `surchargeManuelle`/
> `codeActiviteSage` en paramètres. Ce document décrit le périmètre livré à l'origine (4 niveaux) —
> conservé pour l'historique, ne reflète plus le code actuel.

## Implémenté en worker exceptionnel

Rôle inversé, demande explicite du PO/architecte (session du 23/07/2026, prompt dédié « lance
TASK-161 en tant que worker »), même mode que TASK-101/075/114/117/118/122/160 — cf. réserve
`CLAUDE.md`. Ce document est écrit par le worker ; il appelle une revue architecte indépendante
avant tout déplacement vers `DONE_DETAIL/` (rôle du PO/architecte, pas du worker).

## Périmètre livré

- **Résolution en cascade, point unique** : `Declaration.Core/CodeActiviteResolver.cs`
  (`CodeActiviteResolver.Resoudre(surchargeManuelle, tiersNumero, tiersNom, codeActiviteSage,
  mappingParNumero, mappingParNom)`) — 4 niveaux stricts, jamais bloquant : surcharge ligne >
  défaut tiers (numéro puis nom en repli) > colonne Sage (`F_COMPTET.CT_APE`, déjà câblée avant
  cette task) > `""`. Fonction pure, testée isolément (`Declaration.Core.Tests/
  CodeActiviteResolverTests.cs`, 9 tests) et réutilisée par **tous** les points qui construisent un
  `CodeActivite` :
  - `Declaration.Selection/SelectionExpliqueeEvaluator.cs` et `SelectionnerAffectationsService.cs`
    (niveaux 3/4 uniquement — ces deux évaluateurs n'ont pas accès au mapping tiers ni à une
    surcharge ligne, qui n'existent qu'au niveau `DM_LGTVA`) : remplace l'ancien `?? ""` inline
    dupliqué dans les deux fichiers par un appel à la même fonction (point d'attention explicite de
    la task : « éviter toute divergence », historique TASK-103/108/112).
  - `Declaration.Application/Services/DeclarationWorkflowService.cs` (`MapLignesCandidates`,
    appelée par `ConstruireLignesFigeesAsync` et `ReintegrerReglementsLiberesAsync`) : niveaux 2/3/4
    complets, mapping tiers chargé une seule fois par société via la nouvelle méthode privée
    `ChargerMappingCodeActiviteTiersAsync` (jamais un aller-retour GRF par ligne).
- **Repository, lecture seule stricte** (`Declaration.Infrastructure/Repositories/
  DeclarationRepository.cs`, interface `IDeclarationRepository`) :
  - `GetMappingCodeActiviteTiersAsync(soId)` : `SELECT` (jamais d'écriture) joignant
    `P_SOCIETECODEACTIVITETIERS` → `P_DECTVAACTIVITE` (`CAT_Id = DTA_Id`) sur `CreateGrfConnection()`
    — un JOIN SQL classique intra-base GRF, **pas** le garde-fou TASK-154 (qui vise uniquement les
    jointures cross-base GRF/Sage, jamais respecté ici : aucune table Sage impliquée).
  - `GetReferentielCodesActiviteAsync()` : `SELECT` sur `P_DECTVAACTIVITE` (référentiel complet,
    table non scopée par société — vérifié sur le schéma réel, aucune colonne `SO_Id`), alimente la
    liste déroulante front.
  - `UpdateCodeActiviteLigneAsync(ligneId, codeActivite, utilisateur)` : seule écriture ajoutée,
    cible `DM_LGTVA.Id` (jamais `EC_Id` — cf. cas confirmé PO ci-dessous), trace qui/quand.
- **Persistance** : 4 colonnes additives sur `DM_LGTVA` (`CodeActivite`,
  `CodeActiviteModifieManuellement`, `CodeActiviteModifiePar`, `CodeActiviteModifieLe`) — migration
  fusionnée dans `DeclarationTVA.sql` (script canonique auto-exécuté par `Declaration.Setup`,
  TASK-126) **et** dupliquée dans son fragment historique
  `Declaration.Infrastructure/SQL/010_DM_LGTVA_CodeActivite.sql`, même convention que les 9
  fragments précédents (007 = TASK-078, etc.).
- **Gap TASK-155/TASK-160 comblé** : `LigneCandidate` porte désormais `CodeActivite` (+ 3 champs de
  traçabilité) ; `ConstruireModeleExportAsync` (export de dépôt) et `ConstruireModeleControleAsync`
  (export de contrôle, TASK-160) reportent la valeur réelle dans `LigneDeclarationEnrichie
  .CodeActivite` ; le `RecapsParActivite` de `ConstruireModeleControleAsync` fait désormais un vrai
  `GroupBy(l => l.CodeActivite ?? "")` au lieu de l'ancien bucket `""` unique et systématique.
- **Endpoint de surcharge manuelle** : `PATCH /api/declarations/{id}/lignes/{ligneId}/code-activite`
  (`DeclarationsController.UpdateCodeActiviteLigne`) — cible **la ligne précise** (jamais l'EC_Id,
  contrairement à `valider-incoherence`/TASK-078), 404 si déclaration introuvable, **409 si
  déclaration Clôturée** (garde explicite ajoutée dans `DeclarationWorkflowService
  .ModifierCodeActiviteLigneAsync`, absente du pattern TASK-078 qui n'a aucune garde de statut —
  décision volontaire, justifiée ci-dessous).
- **Endpoint référentiel** : `GET /api/codes-activite` (`DeclarationsController.GetCodesActivite`),
  même pattern que `GET /api/societes` (route absolue hors `[controller]`), alimente la liste
  déroulante front.
- **Front** (`declaration-tva-web/src`) :
  - `DomainGrid.tsx` : extension additive du contrat `ColumnDef` (`editable?: boolean`) + nouvelle
    prop optionnelle `codeActiviteOptions` + rendu conditionnel d'un `<select>` dans la cellule
    `codeActivite` **uniquement** si la colonne le demande explicitement (`editable: true`), la
    grille n'est pas en lecture seule, et des options sont fournies — aucune des 4 autres grilles
    existantes (① Sélection, Workstation, ProofModal, DeclarationFinalePanel) ne passe cette colonne
    ni `editable: true` : **aucune régression possible sur ces écrans** (vérifié par lecture, elles
    n'utilisent que `defaultColumns`, jamais modifié).
  - `VerifierIntegrerPanel.tsx` : référentiel chargé une fois (`GET /codes-activite`), nouveau
    bouton « Codes activité » (pied de page, à côté d'« Export de contrôle ») ouvrant un nouveau
    drill (`kind: 'codeActivite'`, réutilise le mécanisme `drillFiltre` déjà en place pour le drill
    incohérence TASK-112/142) : `DomainGrid` sur **toutes** les lignes du domaine actif (Proposee +
    Integree, aucun filtre — contrairement au drill anomalie qui ne montre que les lignes en
    erreur), colonne « Code activité » éditable tant que la déclaration n'est pas
    intégrée/confirmée (`readonly={isReadOnly}`, même variable que le bouton « Confirmer
    intégration »), lecture seule sinon.

## Décisions actées en cours de route (non tranchées explicitement par la task)

- **Colonnes de traçabilité de la surcharge** (`CodeActiviteModifieManuellement/Par/Le`) : la task
  demandait « même pattern que IncoherenceValidee » comme analogie sur le fait de stocker
  directement sur la ligne (pas de table de mapping) ; le critère de validation demandait
  séparément « traçable (même rigueur que TASK-078) ». J'ai tranché pour les 3 colonnes complètes
  (comme TASK-078), pas seulement `CodeActivite` seul, pour satisfaire littéralement ce second
  critère.
- **Blocage après clôture** : contrairement à `ValiderIncoherenceLigneAsync` (TASK-078, **aucune**
  garde de statut dans le code existant — vérifié par lecture), j'ai ajouté une garde explicite
  (`InvalidOperationException` → 409) sur `ModifierCodeActiviteLigneAsync` pour une déclaration
  `Cloturee`. Justification : la task exige explicitement « figée telle quelle au moment de la
  clôture (stable ensuite) », ce que seule une garde active peut garantir de façon fiable (sans
  garde, rien n'empêcherait techniquement un appel PATCH direct après clôture). Décision non
  couverte par l'analogie TASK-078 fournie — assumée, documentée ici plutôt que silencieuse.
- **Ambiguïté de matching par nom (`SCAT_ErpIntitule`)** : texte libre, saisi manuellement côté
  WinForms — comparaison choisie **insensible à la casse** (`StringComparer.OrdinalIgnoreCase`) côté
  dictionnaire de mapping par nom, alors que le matching par numéro tiers reste strict
  (`StringComparer.Ordinal`, un numéro Sage est un identifiant exact). Un doublon de clé dans le
  mapping (paramétrage WinForms ambigu) retient arbitrairement la première occurrence — jamais une
  exception qui bloquerait tout le figeage pour un problème de paramétrage tiers en amont.
- **Emplacement du contrôle front** : la task ne précisait pas où, dans l'écran ② Vérifier &
  Intégrer (qui n'affiche par défaut que des sous-totaux HTML, pas de grille de lignes détaillée —
  vérifié par lecture), l'édition manuelle devait apparaître. J'ai choisi un nouveau bouton dédié
  « Codes activité » ouvrant le mécanisme de drill déjà en place (même famille que le drill
  anomalie/incohérence), plutôt que d'ajouter une colonne éditable à un tableau HTML natif qui
  n'existait pas pour ce cas d'usage (pas de tableau de lignes individuelles hors drill dans cet
  écran). Aucun écran existant modifié dans sa forme actuelle — uniquement un bouton + un nouveau
  contenu de drill additifs.
- **`SCAT_NumeroTiers` non fusionné dans `DeclarationTVA.sql`** : `P_SOCIETECODEACTIVITETIERS`
  appartient à `apbs-gr_winform` (EF Migrations), pas à GRF — même principe déjà appliqué par
  `DeclarationTVA.sql` lui-même pour `P_SOCIETE` (jamais altérée par ce script, cf. commentaire
  `OrchestrateurDeclaration`/TASK-118 : « référence LOGIQUE... volontairement SANS FK... base
  propriétaire de l'application principale »). Livré en script séparé
  (`P_SOCIETECODEACTIVITETIERS-numero-tiers.sql`, racine du dépôt), **non exécuté automatiquement**
  par `Declaration.Setup` — à exécuter manuellement, en coordination avec le propriétaire
  d'`apbs-gr_winform` (cf. Risques).

## Tests

- `Declaration.Core.Tests/CodeActiviteResolverTests.cs` (9 tests, fonction pure) : les 4 niveaux de
  la cascade isolément (surcharge prioritaire ; défaut tiers par numéro prioritaire sur le nom ;
  repli nom si numéro absent du mapping ; repli Sage si aucun mapping tiers ; `""` si rien ne
  résout, jamais `null`) ; surcharge vide/blanche non retenue comme une vraie surcharge ; appel
  sans mapping fourni (cas des évaluateurs de sélection) ne lève jamais.
- `Declaration.Orchestration.Tests/Task161CodeActiviteCascadeTests.cs` (7 tests) :
  - `MapLignesCandidates` (fonction réellement utilisée au premier figeage) : mapping par numéro
    tiers résolu ; repli Sage si aucun mapping ; `""` non bloquant si rien ne résout (ligne reste
    `Proposee`, aucune exception).
  - **Cas confirmé PO explicite** : une facture (`EC_Id` unique) avec deux lignes de taux différents
    (20 % / 10 %) — `ModifierCodeActiviteLigneAsync` sur l'`Id` de la ligne 10 % ne modifie **que**
    cette ligne (valeur + 3 champs de traçabilité) ; la ligne 20 % (même EC_Id) reste totalement
    intacte.
  - Déclaration `Cloturee` : `ModifierCodeActiviteLigneAsync` lève `InvalidOperationException`, la
    ligne reste inchangée (ni valeur ni trace).
  - **Non-régression TASK-160** : `ConstruireModeleControleAsync` sur deux lignes de codes activité
    différents (`"80"` et `""`) produit bien **2** `RecapParActivite` distincts avec les bons totaux
    — confirme que le regroupement reflète des valeurs réelles et non plus un bucket unique.
  - **Persistance après figeage** : une ligne déjà figée (`CodeActivite="80"`) traverse
    `ChargerCandidatesSiNecessaireAsync` (branche « déjà figé », `RevaliderLignesFigeesAsync`) sans
    modification, **même si** le mapping tiers du repository change entre-temps — confirme que la
    revalidation ne recalcule jamais `CodeActivite`.
- Non-régression `TASK-147` (`RecalculerLigneDepuisCacheAsync`) : la reconstruction depuis le cache
  préserve désormais explicitement `CodeActivite`/les 3 champs de traçabilité de la ligne existante
  (colonne non-financière, même traitement que `TiersNom`/`ModePaiement` déjà préservés) — pas de
  nouveau test dédié (comportement direct, déjà couvert indirectement par les tests `Task147*`
  existants qui continuent de passer sans modification).

## Vérifié indépendamment (même session, auto-revue worker exceptionnel)

- **Réel accès base de données**, `sqlcmd` direct sur `GR_EMA_DISTRIBUTION` (`DESKTOP-5BFKKEP`) :
  - Schémas réels lus avant toute écriture : `P_SOCIETECODEACTIVITETIERS` (colonnes
    `SCAT_Id/SCAT_ErpIntitule/CAT_Id/SO_Id/RowVersion`, **aucun** `SO_Id` sur `P_DECTVAACTIVITE` —
    contrairement à ce que suggérait la task, vérifié plutôt que supposé) et `P_DECTVAACTIVITE`
    (`DTA_Id/DTA_Code/DTA_Intitule/.../DTA_Prorata`).
  - Migrations réellement appliquées et rejouées (idempotentes, colonnes confirmées présentes après
    coup par requête sur `sys.columns`) : `010_DM_LGTVA_CodeActivite.sql` (4 colonnes) et
    `P_SOCIETECODEACTIVITETIERS-numero-tiers.sql` (1 colonne).
  - **Cascade vérifiée contre la seule donnée réelle existante** (une société, `SO_Id=1`, un seul
    mapping tiers en base) : la requête exacte de `GetMappingCodeActiviteTiersAsync` exécutée
    directement en SQL retourne `NumeroTiers=NULL, ErpIntitule="146-Achat à l'intérieur",
    CodeActivite="146"` — confirme le JOIN `P_SOCIETECODEACTIVITETIERS → P_DECTVAACTIVITE` fonctionne
    réellement, `SCAT_NumeroTiers` bien NULL pour ce mapping créé avant la migration (comportement
    attendu, repli sur le nom).
  - **Constat honnête, non silencieux** : `SCAT_ErpIntitule="146-Achat à l'intérieur"` est en réalité
    la concaténation `{DTA_Code}-{DTA_Intitule}` de l'activité elle-même (`DTA_Id=55 → Code=146`),
    pas un nom de tiers réel — recherche dans `F_COMPTET` (base Sage) : aucun tiers dont
    `CT_Intitule`/`CT_Num` contient « 146 » ; échantillon de tiers réels avec `CT_APE` renseigné :
    **aucun** (colonne vide pour tous les tiers testés). **Conclusion** : sur les données réelles de
    cet environnement de dev, ni le niveau 2 (mapping tiers) ni le niveau 3 (Sage `CT_APE`) ne
    résolvent quoi que ce soit pour une vraie facture aujourd'hui — la cascade retombe
    systématiquement sur `""`, exactement le comportement non bloquant attendu (décision PO point
    3). La donnée de paramétrage réelle est insuffisante pour démontrer une résolution de bout en
    bout sur une facture réelle ; la mécanique elle-même (SQL, jointure, priorité) est, elle,
    vérifiée réellement (ci-dessus) et par les tests unitaires (cascade isolée, tous les niveaux).
- `dotnet build DeclarationTVA.slnx` → 0 erreur (2 avertissements `NU1510` préexistants, sans
  rapport).
- `dotnet test` solution complète → 43/43 `Declaration.Core.Tests` (dont les 9 nouveaux), 175/175
  `Declaration.Orchestration.Tests` (dont les 7 nouveaux), 13/13 `Declaration.Export.Xml.Tests`,
  3/3 `Declaration.Export.Excel.Tests` → **exactement** les 2 échecs préexistants déjà documentés
  dans TASK-154/155/156/159/160 (`Declaration.Selection.Tests.IntegrationRegressionTests` : échec
  d'authentification Windows sur la connexion GRF locale, sans rapport avec ce correctif ;
  `Declaration.Controle.Tests.ComparateurTests.GenererRapportVerification` : donnée de test absente
  en base) — aucun nouvel échec.
- `npx tsc -b` (front) → 0 erreur. `npx vite build` → build réussi, seul avertissement
  `INEFFECTIVE_DYNAMIC_IMPORT` préexistant (`api.ts`, sans rapport, déjà documenté TASK-160).
- **Garde-fous confirmés par grep** : aucune occurrence de `P_DECTVASOCTAXEACTIVITE` en dehors d'un
  commentaire de documentation qui confirme explicitement qu'elle n'est PAS lue ; aucun
  `INSERT/UPDATE/DELETE` contre `P_DECTVAACTIVITE` ou `P_SOCIETECODEACTIVITETIERS` dans le code
  livré (les deux seules méthodes qui les touchent, `GetMappingCodeActiviteTiersAsync`/
  `GetReferentielCodesActiviteAsync`, ne contiennent qu'un `SELECT`, sur `CreateGrfConnection()` —
  jamais `CreateSageConnection`) ; aucune connexion Sage ouverte par le code livré pour cette task.
- **Non-régression `apbs-gr_winform`** : lecture complète de
  `SocieteCodeActiviteTiersRepository1.cs` (le seul fichier qui requête
  `P_SOCIETECODEACTIVITETIERS` côté WinForms) — les 5 requêtes (`QueryGetAll`/`QueryCreate`/
  `QueryDelete`/`QueryIsErpIntituleUsed`/`QueryExist`) utilisent toutes une liste de colonnes
  explicite (`SCAT_Id, SO_Id, CAT_Id, SCAT_ErpIntitule`), **jamais** `SELECT *` ni de dépendance au
  nombre de colonnes — l'ajout additif de `SCAT_NumeroTiers` ne casse aucune requête existante.
  Aucun fichier sous `D:\_vibe\apbs-gr_winform` modifié (lecture seule stricte, confirmé — je n'ai
  utilisé aucun outil d'écriture sur ce chemin).
- **Non-régression front** : lecture de `ReglementsSelection.tsx`, `ProofModal`/
  `DeclarationFinalePanel`/écran Workstation — aucun ne passe la clé `codeActivite` dans son
  `columns`, donc le nouveau rendu conditionnel de `DomainGrid` ne s'y déclenche jamais ; seule
  extension additive du contrat `ColumnDef` (`editable?`) et d'une prop optionnelle.

## Réserves non bloquantes, documentées non silencieuses

- **Dépendance cross-applicatif à trancher par le PO** (signalée dans la task elle-même,
  section Risques) : `SCAT_NumeroTiers` (`P_SOCIETECODEACTIVITETIERS-numero-tiers.sql`, livré mais
  **non exécuté automatiquement**) n'a de valeur que si l'écran WinForms
  `UcSocieteCodeActiviteTiers` (`apbs-gr_winform`) est lui-même mis à jour pour capturer ce numéro à
  la création/modification d'un mapping — hors périmètre du dépôt GRF (interdiction explicite de
  toucher `apbs-gr_winform` dans cette task). Sans ce second correctif, tout nouveau mapping créé
  restera matché uniquement par `SCAT_ErpIntitule` (texte libre). **Qui porte ce correctif côté
  `apbs-gr_winform` reste une question ouverte pour le PO.**
- **Qualité des données réelles de paramétrage, constatée pendant la vérification** (cf. section
  ci-dessus) : le seul mapping tiers existant en base (`SO_Id=1`) porte une valeur `SCAT_ErpIntitule`
  qui ressemble à un artefact de saisie (concaténation code+libellé de l'activité elle-même) plutôt
  qu'à un vrai nom de tiers Sage, et aucun tiers réel n'a de `CT_APE` renseigné. La cascade est donc
  invérifiable de bout en bout sur une facture réelle **avec les données actuelles** — non bloquant
  (comportement `""` attendu et correct), mais signalé pour que le PO puisse, si utile, faire
  contrôler/re-saisir ce mapping via l'écran WinForms existant.
- **Aucune vérification HTTP live réelle** (API démarrée + navigateur/Playwright) pour les 2
  nouveaux endpoints : une instance `Declaration.API` tourne déjà sur `:5280` dans cet environnement
  mais répond `503 {"message":"Merci de vérifier la licence"}` (garde-fou `GRLicence`, sans rapport
  avec cette task) et exécute de toute façon un binaire antérieur à ce correctif — je n'ai pas
  arrêté/relancé ce processus (action jugée hors périmètre d'un correctif applicatif, potentiellement
  perturbatrice pour un usage en cours). Vérification faite par tests d'intégration (repository
  réel contre base réelle pour la lecture, fakes pour le reste) + lecture différentielle du code,
  même limite déjà documentée par TASK-155/TASK-160 pour un environnement sans accès réseau/service
  dédié.
- **Contrôle serveur de la valeur soumise au PATCH** : `UpdateCodeActiviteLigne` accepte n'importe
  quelle chaîne (y compris hors référentiel `P_DECTVAACTIVITE`) — le front contraint la saisie à la
  liste déroulante du référentiel, mais rien ne l'impose côté back (même niveau de confiance
  qu'`UpdateLigneEtatAsync`/`ValiderIncoherenceAsync` existants, qui ne valident pas non plus leurs
  paramètres contre un référentiel). Non bloquant, cohérent avec le reste du code livré, mais
  signalé explicitement plutôt que supposé validé.
- **Dette déjà tracée par la task, non retraitée ici** (décision PO point 6) : aucun écran GRF pour
  peupler `P_DECTVAACTIVITE`/`P_SOCIETECODEACTIVITETIERS` pour un futur client 100 % GRF web sans
  WinForms — inchangé, toujours non bloquant tant qu'aucun client de ce type ne se présente.

## Non modifié (confirmé par périmètre)

- Aucune écriture Sage, aucune écriture dans `P_DECTVAACTIVITE` — lecture seule confirmée (grep +
  relecture des 2 méthodes repository ajoutées, ci-dessus).
- `P_DECTVASOCTAXEACTIVITE` : aucune référence dans le code livré (grep, ci-dessus).
- Aucun nouvel écran web GRF de gestion du référentiel/mapping (décision PO point 2) — le nouveau
  bouton « Codes activité » n'expose qu'une **surcharge par ligne** dans l'écran ② existant, jamais
  un CRUD sur `P_DECTVAACTIVITE`/`P_SOCIETECODEACTIVITETIERS`.
- `DeclarationTVA.sql` : seul changement de schéma côté base GRF pour la persistance (4 colonnes
  `DM_LGTVA`), aucun retrait/renommage de colonne existante.
