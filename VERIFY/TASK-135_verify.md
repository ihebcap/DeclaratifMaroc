# TASK-135 Verify — Mesure du délai de paiement fournisseur : extension de l'écran Factures

> Implémentation worker. Développement NEUF sur GRF web, AUCUNE réutilisation de DLL/code WinForms.
> Extension de projection en LECTURE SEULE stricte (aucune modification de schéma, aucune table
> créée/modifiée). Build back (`dotnet build DeclarationTVA.slnx`) et front (`npx tsc -b` +
> `npx vite build`) : **0 erreur**. Tests : `Declaration.Core.Tests` 88/88, `Declaration.Orchestration.Tests`
> 188/188 (dont 6 nouveaux TASK-135), aucune régression. Rejeu contre les **données réelles**
> `GR_EMA_DISTRIBUTION` effectué (voir § Vérification base réelle). **Un point de câblage DI reste
> ouvert, hérité de TASK-127/TASK-129** (non résolu par cette session, hors périmètre) — voir
> § « Reste à valider », point 1, **le plus important de ce document**.

## Périmètre livré

Extension de la projection `GET /api/factures` (TASK-041, `FacturesController.cs`) avec 2 colonnes,
conformément au périmètre strict de la TASK (aucun écran neuf, aucune logique de déclaration,
aucun export) :

- **Échéance légale** — socle TASK-127 (`EcheanceLegaleCalculator.Calculer`), résolue pour chaque
  facture fournisseur (domaine Achat) à partir de : conventions par tiers (TASK-129, contrat lu mais
  toujours sans donnée réelle chargée — `RT_CONVENTIONTIERS` vide en base, cf. TASK-127 verify),
  jours de repos société (`P_JOURSREPOS`) et délai par défaut société (`P_SOCIETE.SO_NbJoursDelaiPaiement`).
- **Écart (jours)** — calcul CDC §3.3 :
  - facture **soldée** (solde restant ≤ 0, payée à 100 %) : écart = dernière date de rapprochement
    bancaire pertinente (même mécanisme que le module TVA : `RT_MOUVEMENT.MV_Point`/`MV_PointDate`,
    source locale GRF — jamais le mécanisme Sage de l'ancien module RAS) **−** échéance légale.
    **NULL** si aucune affectation rattachée n'est encore rapprochée banque (jamais un écart inventé
    contre une référence absente — règle n°1 du projet).
  - **solde restant > 0** (non payée ou partielle) : écart = date du jour **−** échéance légale,
    toujours calculable, explicitement **provisoire** (affiché `(prov.)` côté front).

Indicateur de pilotage interne (retard fournisseur) — affiché mais **non lié au workflow DDP**
(qui garde son propre calcul incrémental, TASK-131) ni source de vérité réglementaire, conformément
au périmètre.

### Choix de conception : consommation directe du socle plutôt que `IDelaiPaiementService`

`FacturesController` consomme **directement** `IConventionDelaiPaiementRepository` +
`IJoursReposRepository` + `IDelaiPaiementParametrageRepository` + le calculateur pur
`EcheanceLegaleCalculator` (Declaration.Core), plutôt que `IDelaiPaiementService`
(Declaration.Application) proposé par TASK-127. Raison : `IDelaiPaiementService.ResoudreDelaiAsync`
recharge conventions/jours de repos/délai par défaut à **chaque appel** — adapté à une résolution
facture par facture (TASK-131/134), mais coûteux en boucle sur une grille paginée (jusqu'à `size`
factures par page, potentiellement 3×`size` allers-retours DB). La consommation retenue charge ces
trois référentiels **une seule fois par page**, puis applique le calculateur **pur** (aucun I/O,
même fonction statique que `IDelaiPaiementService`) ligne par ligne en mémoire — aucune logique
métier dupliquée, juste une orchestration différente, mieux adaptée à ce point d'entrée précis.

## Fichiers modifiés / créés

Back (modifiés) :
- `Declaration.Application/Entities/FactureInterrogation.cs` — `FactureInterrogationRow` : nouveau
  champ `TiersNo` (projection `CT_No`, nécessaire à l'appariement convention/tiers), nouveaux champs
  `EcheanceLegale`/`EcartJours` + méthode `AppliquerDelaiPaiement(echeanceLegale, derniereDateRapprochement)`
  (logique pure, testable hors DB — même patron que `AppliquerCacheB`, TASK-024).
- `Declaration.Application/Interfaces/IDeclarationRepository.cs` — nouvelle méthode
  `GetDernieresDatesRapprochementAsync(soId, ecIds)` (lecture seule, batchée).
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` — `E.CT_No AS TiersNo` ajouté au
  SELECT de `GetFacturesInterrogationAsync` ; implémentation de
  `GetDernieresDatesRapprochementAsync` (MAX de la même expression `DateRapprochement` que le module
  TVA/Rapprochement — `RapprochementDateRappExpr`, reproduite ici à l'identique pour l'écran Factures).
- `Declaration.API/Controllers/FacturesController.cs` — constructeur étendu (3 nouvelles dépendances,
  cf. § câblage DI ci-dessous), méthode privée `AppliquerDelaiPaiementAsync` appelée depuis
  `GetFactures` après matérialisation de la page et avant mapping DTO.
- `Declaration.API/Dtos/FactureInterrogationDto.cs` — `echeanceLegale`/`ecartJours` (JSON).
- 16 `Fake.../Unused.../Task175MinimalRepository` de `Declaration.Orchestration.Tests` (13 fichiers
  `class FakeDeclarationRepository` + `Task156ContentionValorisationTests.cs` [2 classes] +
  `Task175ConflitVerrouGetLignesTests.cs`) : stub `GetDernieresDatesRapprochementAsync` ajouté
  (`NotImplementedException`/`NotUsed()` selon le style local du fichier), aucun n'exerçant cette
  méthode dans ses tests existants — nécessaire pour satisfaire l'interface étendue (leçon TASK-147,
  `TODO.md` : ne jamais oublier les fakes lors d'une extension d'interface).

Back (nouveaux) :
- `Declaration.Orchestration.Tests/Task135DelaiPaiementFactureTests.cs` — 6 tests unitaires purs sur
  `AppliquerDelaiPaiement` (cas soldé/non soldé/écart null/négatif/zéro).

Front (modifié) :
- `declaration-tva-web/src/FactureInterrogation.tsx` — 2 colonnes (`echeanceLegale`, `ecartJours`),
  famille `D` ajoutée au type `Col`, composant `CelluleD` (mise en forme couleur si retard positif,
  mention `(prov.)` si facture non soldée, `—` explicite si écart non calculable — jamais un 0/silence).

Fichiers **non touchés** (hors périmètre strict) : aucun écran neuf, aucun export Excel/XML, aucune
logique de déclaration, `ProofModal.tsx`/`AffectationsDrill.tsx`/`DeclarationsController.cs` non
modifiés.

## Vérification base réelle (GR_EMA_DISTRIBUTION, DESKTOP-5BFKKEP → 127.0.0.1)

Rejeu du calculateur réel (pas seulement lu par confiance) contre des factures fournisseur réelles
(`SO_Id=1`, `DO_Domaine=1`), via un test temporaire (`TEMP_Task135RealDataVerification.cs`, exécuté
puis **supprimé, jamais commité** — même pratique que TASK-127) :

- **Facture soldée avec rapprochement bancaire réel** : `EC_Id=23474` / `FC2600741`, tiers `CT_No=294`,
  `DO_Date=2026-05-23`, `EC_Montant=Regle=696.35` (soldée). `RT_CONVENTIONTIERS` toujours vide
  (`SO_Id=1`) ⇒ défaut société (60 j, `OrigineDelai.Defaut`). `P_JOURSREPOS` ne contient **aucune**
  entrée 2026 (seulement 2025, cf. TASK-127 verify point 3) ⇒ aucun décalage férié, seul le week-end
  compterait. Échéance légale calculée = **2026-07-22** (mercredi, ouvré — vérifié indépendamment via
  `date -d "2026-05-23 + 60 days"`). Dernière date de rapprochement bancaire pertinente réelle (`MAX`
  sur `RT_AFFECTATION`→`RT_MOUVEMENT`, même expression que `RapprochementDateRappExpr`) = **2026-05-31**.
  Écart = **-52 jours** (payée bien avant l'échéance légale par défaut — cohérent avec l'absence de
  convention réelle chargée à ce jour, cf. point 2 ci-dessous).
- **Facture non soldée (impayée)** : `EC_Id=24072` / `FC2600902`, tiers `CT_No=307`, `DO_Date=2026-06-27`,
  `Regle=0`. Échéance légale = **2026-08-26** (mercredi). Écart calculé contre `DateTime.Today`
  (27/07/2026 au moment du rejeu) = **-30 jours** (pas encore en retard, échéance dans le futur) —
  cohérent, marqué `(prov.)` côté front.
- **Confirmation SQL directe** (hors test C#, `sqlcmd`) : le SELECT étendu
  (`E.CT_No AS TiersNo` ajouté à `GetFacturesInterrogationAsync`) exécuté tel quel contre la base
  réelle renvoie des `TiersNo` cohérents (ex. `EC_Id=24062 → TiersNo=294`, fournisseur MARJANE F0027).
  La requête `GetDernieresDatesRapprochementAsync` (MAX groupé par `EC_Id`) exécutée directement en
  SQL sur un échantillon de 5 factures soldées confirme le comportement NULL attendu quand aucune
  affectation n'est encore rapprochée banque (5/5 `NULL` sur l'échantillon initial, avant d'élargir la
  recherche pour trouver un cas positif — les deux cas réels existent bien en base).

## Checklist

- [x] Build back `dotnet build DeclarationTVA.slnx` — **0 erreur** (warnings préexistants uniquement,
      hors périmètre TASK-135).
- [x] Build front `npx tsc -b` — 0 erreur ; `npx vite build` — build réussi (1 warning préexistant,
      import dynamique `api.ts`, sans rapport).
- [x] Tests `Declaration.Core.Tests` — 88/88 verts (aucune régression).
- [x] Tests `Declaration.Orchestration.Tests` — 188/188 verts (182 préexistants + 6 nouveaux
      TASK-135), aucune régression.
- [x] Aucune modification de schéma / aucune table créée (extension de projection en lecture seule
      stricte, comme prévu par la TASK).
- [x] Lecture seule stricte : uniquement des `SELECT` ajoutés (`E.CT_No`, `RT_AFFECTATION`⋈`RT_MOUVEMENT`).
      Aucun INSERT/UPDATE/DELETE.
- [x] Pas de SQL inline hors couche repository (ARCHITECTURE §5) : le SQL neuf est confiné à
      `DeclarationRepository.GetDernieresDatesRapprochementAsync` ; `FacturesController`/l'entité
      restent sans SQL.
- [x] Pas de secret codé en dur, pas de bypass sécurité.
- [x] Aucune ligne silencieuse : écart `NULL` explicite (jamais 0) quand la référence de rapprochement
      manque ; front affiche `—` avec tooltip explicite plutôt qu'un vide muet.
- [x] Les 2 colonnes affichées côté front, avec mise en forme couleur (retard positif) et mention
      `(prov.)` distinguant écart définitif vs provisoire.
- [x] Périmètre strict respecté : aucun écran neuf, aucun export, aucune logique DDP touchée.
- [x] 16 fichiers de fakes/doubles de test mis à jour (17 implémentations) pour satisfaire
      l'interface `IDeclarationRepository` étendue (build solution complète vérifié via
      `dotnet build DeclarationTVA.slnx`, pas seulement le projet API — leçon TASK-147).
- [x] Rejeu contre données réelles (facture soldée + facture non soldée), test temporaire supprimé
      avant commit (non committé).

## Reste à valider (NON couvert / hors périmètre worker)

1. **⚠️ CÂBLAGE DI TOUJOURS OUVERT au moment de cette session — impact réel sur `FacturesController`
   ENTIER, pas seulement les 2 nouvelles colonnes.** `FacturesController` dépend désormais, en plus de
   `IDeclarationRepository`/`DeclarationWorkflowService` (déjà câblés), de trois nouvelles interfaces :
   `IConventionDelaiPaiementRepository`, `IJoursReposRepository`, `IDelaiPaiementParametrageRepository`.
   Au moment où cette TASK a été développée, **aucune des trois n'était enregistrée dans
   `Declaration.API/Program.cs`** (vérifié par grep direct sur `Program.cs` — aucune occurrence des
   trois noms). Conséquence : **tant que ce câblage n'est pas posé, TOUTE requête vers
   `FacturesController` (GetFactures, GetDistincts, RafraichirValorisation) échoue à la résolution DI**
   (ASP.NET Core ne peut pas instancier le contrôleur) — pas seulement l'affichage des 2 nouvelles
   colonnes. C'est une **régression temporaire réelle et assumée**, pas une simple limite de
   vérification — décision explicite du donneur d'ordre pour cette session (« ne pas bloquer TASK-135
   sur TASK-129, ne pas implémenter le câblage DI depuis cette session, documenter la dépendance »).
   Je n'ai **pas** modifié `Program.cs` (hors périmètre, risque de collision avec une session parallèle
   sur ce même fichier, cf. point 2).
   **Observation additionnelle** (état du dépôt au moment de la rédaction de ce VERIFY, information
   seulement — je n'ai ni vérifié ni utilisé ces fichiers) : une session parallèle semble avoir déjà
   avancé sur TASK-129 pendant cette session (fichiers non commités observés dans l'arbre de travail
   partagé : `Declaration.Infrastructure/Repositories/ConventionDelaiPaiementRepository.cs` implémentant
   `IConventionDelaiPaiementRepository` **et** `IConventionDelaiPaiementTiersRepository`) — si cette
   session aboutit et câble `Program.cs`, ce point se refermera de lui-même sans action supplémentaire
   côté TASK-135. Je n'ai pas attendu ni vérifié cet aboutissement (hors périmètre, pourrait ne pas
   être terminé/committé).
   **Test manuel HTTP de bout en bout non exécuté** pour cette raison (l'API ne démarrerait pas
   proprement pour `/api/factures` tant que ce câblage manque) — seule la vérification directe du
   calcul (SQL réel + calculateur réel, § ci-dessus) a pu être faite, pas le cycle HTTP complet
   `GET /api/factures` → JSON avec les 2 nouveaux champs. **Ne pas déclarer ce point résolu sans
   nouveau test HTTP réel une fois TASK-129 câblée.**

2. **Travail parallèle sur le même dépôt (non isolé)** : au moment de cette session, un `git status`
   montre des modifications non commitées à `Declaration.API/Program.cs` et `DeclarationTVA.sql`, ainsi
   que plusieurs nouveaux fichiers liés à TASK-128/TASK-129 (bootstrap, paramétrage, conventions CRUD).
   Ces fichiers n'ont **ni été lus en détail, ni modifiés, ni stagés** par cette session — le commit
   TASK-135 ne contient que les fichiers listés en § « Fichiers modifiés / créés » ci-dessus. Si un
   conflit apparaissait plus tard entre les deux lots de travail (ex. sur `Program.cs`), il relève de
   la coordination entre sessions, pas d'un défaut de TASK-135.

3. **`P_JOURSREPOS` sans aucune entrée pour 2026** (déjà signalé par TASK-127 verify, confirmé de
   nouveau ici) : toute échéance légale calculée en 2026 ne peut décaler que sur un week-end, jamais
   sur un jour férié réel de l'année en cours — écart de complétude du référentiel, pas un défaut du
   code TASK-135 (ni de TASK-127). À signaler au PO si la précision des échéances 2026 devient un
   enjeu (le calcul reste correct vis-à-vis des données actuellement disponibles).

4. **Tri/filtre sur les 2 nouvelles colonnes : volontairement NON implémenté.** L'étape 4 de la TASK
   conditionnait cet ajout à « si le mécanisme `ExcelFilter` le permet nativement ». `EcheanceLegale`/
   `EcartJours` sont calculés **après** la pagination SQL (`OFFSET`/`FETCH`) et après le chargement de
   la page — contrairement aux colonnes déjà triables (`date`, `ttc`, `solde`, `numero`), qui
   correspondent à une expression SQL whitelistée triée **avant** pagination. Ajouter un tri/filtre
   serveur sur ces 2 colonnes demanderait de recalculer l'échéance/l'écart pour **l'intégralité** du
   jeu de factures de la période avant de trier/paginer — réingénierie de la pagination, hors
   périmètre strict de cette TASK (documenté ici plutôt qu'improvisé).

5. **Performance non mesurée en conditions réelles** : `AppliquerDelaiPaiementAsync` ajoute, par page,
   3 requêtes (conventions/jours de repos/délai défaut, une seule fois) + 1 requête batchée
   (rapprochements, uniquement si des factures soldées sont présentes sur la page) — soit un surcoût
   constant par page, indépendant du nombre de lignes (contrairement à un appel par ligne via
   `IDelaiPaiementService`, cf. § choix de conception). Non mesurable en charge réelle tant que le
   point 1 (câblage DI) n'est pas résolu et que `RT_CONVENTIONTIERS` reste vide.

6. **Écran de détail (`FactureDetail`, panneau latéral)** : les 2 nouvelles colonnes ne sont **pas**
   reprises dans le panneau de détail à la demande — périmètre strict de la TASK (« 2 colonnes ajoutées
   à l'écran Factures existant »), pas d'anticipation. À évaluer séparément si le PO le souhaite.

## Verdict

Projection back étendue et testée (calcul pur, 6 tests unitaires + rejeu réel sur facture soldée et
facture non soldée, résultats conformes), 2 colonnes front livrées avec mise en forme honnête
(jamais d'écart inventé). Build back+front 0 erreur, 276 tests verts au total, aucune régression sur
les colonnes/tri/filtre existants de l'écran Factures (aucune requête existante modifiée, seule une
colonne ajoutée au SELECT). **Point bloquant pour une vérification HTTP de bout en bout (point 1) :
câblage DI de TASK-129, hors périmètre de cette session, potentiellement déjà en cours en parallèle**
— la logique métier elle-même est vérifiée indépendamment de ce câblage (calculateur pur + SQL réel
rejoués directement).
