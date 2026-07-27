# TASK-129 Verify — Convention de délai de paiement par tiers (back) + câblage DI socle TASK-127

> Implémentation worker. Développement NEUF sur GRF web (Declaration.Application/Declaration.Core/
> Declaration.Infrastructure), AUCUNE réutilisation de DLL/code WinForms — seule la SÉMANTIQUE legacy
> (`SocieteManager.Complement.cs:471-589`) est reproduite, avec la correction de chevauchement actée PO.
> Contrainte schéma respectée : **aucune modification de schéma, aucune table créée** — `RT_CONVENTIONTIERS`
> est une table existante déjà possédée par GRF (pas winform), réutilisée telle quelle.
> Build back (`dotnet build DeclarationTVA.slnx`) : **0 erreur**. Tests `Declaration.Core.Tests` :
> **88/88 verts** (dont 17 nouveaux pour cette TASK). Câblage DI vérifié en conditions réelles :
> l'API a été démarrée (`dotnet run`, environnement Development → `ValidateOnBuild` actif) et a
> répondu `200 OK` sans aucune exception de résolution DI — voir § Vérification DI/démarrage réel.
> SQL vérifié contre les **données réelles** `GR_EMA_DISTRIBUTION` (insertions/lectures/suppressions
> temporaires, table remise à 0 ligne après coup) — voir § Vérification base réelle.

## Note sur le déroulement de la session (transparence process)

Cette session s'est déroulée en **concurrence réelle** avec d'autres sessions travaillant sur le même
dépôt (TASK-128, TASK-135 — mentionné comme risque connu dans la consigne de départ). Deux incidents
transitoires observés et documentés pour traçabilité, **aucun n'affecte le livrable final** :

1. Mon édition de `Declaration.API/Program.cs` (câblage DI) a été **écrasée une fois** par un commit
   intermédiaire d'une autre session (TASK-128, dont les propres lignes DI ont été committées entre mes
   deux premiers appels à `dotnet build`) — ré-appliquée immédiatement après détection (diagnostiqué via
   `git diff -- Declaration.API/Program.cs`, qui ne montrait alors plus que mon propre bloc, confirmant
   que le reste avait déjà été committé par TASK-128 et n'était donc plus « en danger » d'un commit mixte).
2. `dotnet build DeclarationTVA.slnx` a échoué **transitoirement à deux reprises** pour des causes 100%
   étrangères à mon périmètre (vérifié via `git status` à chaque fois) : d'abord 17 erreurs CS0535 dans
   `Declaration.Orchestration.Tests` (fakes `IDeclarationRepository` pas encore mis à jour par TASK-135,
   en plein ajout de `GetDernieresDatesRapprochementAsync`), puis un fichier temporaire de vérification
   laissé par la même session (`Declaration.Core.Tests/TEMP_Task135RealDataVerification.cs`, référençant
   un namespace absent). Aucun fichier de TASK-135 n'a été modifié par moi ; j'ai simplement rejoué le
   build jusqu'à obtenir un état stable (0 erreur), confirmé juste avant de committer mon propre travail.

Je n'ai commité **que mes propres fichiers**, individuellement (`git add` fichier par fichier, jamais
`git add -A`), pour ne prendre aucun risque de committer le travail en cours d'une autre session.

## Périmètre livré

### 1. Contrôles métier PURS (hors DB) — `Declaration.Core/ConventionDelaiPaiementValidator.cs`

Reproduit `SocieteManager.Complement.cs:471-589` (`ConventionDelaisPaiementTiersCreate`/`Terminer`),
avec la correction PO du contrôle de chevauchement :

- `PlafondJours = 180` + `ValiderPlafond` : délai strictement positif et ≤ 180 (legacy l.490-492).
- `ValiderDatesConvention` : `DateFin ≥ DateDebut` (legacy l.508-509).
- `TrouverChevauchement` : contrôle **CORRIGÉ**, bidirectionnel —
  `chevauchement si (nouvelle.DateDebut ≤ existante.DateFin) ET (existante.DateDebut ≤ nouvelle.DateFin)`,
  testé contre **toutes** les conventions existantes (pas seulement celle dont la date de début
  contiendrait la nouvelle date de début, cf. `// TODO: verifier le chauvochement des date`, l.531,
  jamais corrigé dans le legacy). Retourne la convention en conflit pour un message explicite.
- `ValiderUniciteFacture` : une seule convention par facture (legacy l.526-528).
- `ValiderTerminer` : nouvelle DateFin comprise entre DateDebut et l'ancienne DateFin (legacy l.579-583).

### 2. Entité + repository CRUD — `RT_CONVENTIONTIERS`

- `Declaration.Application/Entities/ConventionDelaiPaiementTiers.cs` : mapping 1:1 des colonnes
  (`CP_Id, SO_Id, CT_No, CT_Code, CP_Date, CP_Numero, CP_DateDebut, CP_DateFin, CP_FileName, CP_File,
  CP_DelaisPaiement, CP_Domaine, CP_Type, CP_FactureNo`).
- `Declaration.Application/Interfaces/IConventionDelaiPaiementTiersRepository.cs` : contrat CRUD complet
  (Create/Get/GetAll/GetAllForTiers/ExisteNumero/EcheanceNonPayeeExiste/UpdateDateFin/Delete) —
  **distinct** de `IConventionDelaiPaiementRepository` (TASK-127, lecture seule filtrée par domaine,
  consommée uniquement par le calculateur).
- `Declaration.Infrastructure/Repositories/ConventionDelaiPaiementRepository.cs` : implémente **les deux**
  contrats (TASK-127 lecture + TASK-129 CRUD) dans une seule classe Dapper, cohérent avec le pattern déjà
  utilisé par `DelaiPaiementReferentielRepository` (TASK-127, qui implémente 2 interfaces référentiel).

### 3. Service d'orchestration — `Declaration.Application/Services/ConventionDelaiPaiementService.cs`

`IConventionDelaiPaiementService` (`CreerAsync`/`GetAsync`/`GetAllAsync`/`TerminerAsync`/`DeleteAsync`),
orchestration pure des lectures/écritures — tout le métier est délégué à `ConventionDelaiPaiementValidator`.
`CreerAsync` reproduit l'enchaînement exact du legacy : arguments obligatoires → cohérence FileName/File
(**jamais leur présence obligatoire**, décision PO 19/07/2026) → plafond 180j → unicité numéro (legacy
l.495-496) → puis, selon le type : Convention (dates + chevauchement corrigé) ou Facture (existence +
NonPayée + unicité facture).

### 4. Implémentation de `IConventionDelaiPaiementRepository` (contrat TASK-127) + câblage DI complet

`ConventionDelaiPaiementRepository.GetConventionsActivesAsync` : charge **toutes** les conventions (tous
types) de la société pour le domaine — comme le legacy `ConventionDelaisPaiementTiersGetAll(domaine)`,
sans filtre de validité supplémentaire (c'est le calculateur pur TASK-127 qui départage Facture exacte /
Convention (intervalle) / défaut). `FactureNumero` résolu par jointure `RT_ECHEANCE.EC_Id = CP_FactureNo`
— **identique au legacy** (`ConventionDelaisPaiementTiersRepository.Script.cs:22-24`).

`Declaration.API/Program.cs` : les 6 enregistrements manquants (identifiés comme « volontairement non
posés » dans le VERIFY TASK-127) sont désormais présents :
```
IJoursReposRepository, IDelaiPaiementParametrageRepository       → DelaiPaiementReferentielRepository
IConventionDelaiPaiementRepository, IConventionDelaiPaiementTiersRepository → ConventionDelaiPaiementRepository
IDelaiPaiementService            → DelaiPaiementService
IConventionDelaiPaiementService  → ConventionDelaiPaiementService
```

## Découvertes en code legacy (déterminantes pour l'implémentation, vérifiées ligne par ligne)

1. **`CP_FactureNo` référence `RT_ECHEANCE.EC_Id`, PAS `EC_No`.** Repéré via
   `SocieteManager.Complement.cs:523` (`EcheanceGetAll(erpDomaine, tiersNo, Etat.NonPaye)?.FirstOrDefault(x
   => x.No == factureNo.Value)`) où `Echeance.No` est annoté `[Key][Column("EC_Id")]`
   (`Tresorerie.EF.Migrations/Models/Echeance.cs:12-14`), et confirmé indépendamment par le repository
   legacy lui-même (`ConventionDelaisPaiementTiersRepository.Script.cs:24` :
   `LEFT JOIN RT_ECHEANCE e ON e.EC_Id = c.CP_FactureNo`). Mon repository (`EcheanceNonPayeeExisteAsync`,
   `GetConventionsActivesAsync`) utilise `EC_Id`, jamais `EC_No`.
2. **Deux mappings `DomaineDelaiPaiement` DIFFÉRENTS selon la colonne cible** (documenté en commentaire
   de classe dans `ConventionDelaiPaiementRepository.cs`, à ne jamais confondre) :
   - `RT_CONVENTIONTIERS.CP_Domaine` : mapping **direct** (Achat=0/Vente=1 = Fournisseur=0/Client=1,
     confirmé `Tresorerie.Core/Models/ConventionDelaisPaiementTiers.cs:25-29`).
   - `RT_ECHEANCE.DO_Domaine` (utilisé uniquement par le contrôle facture NonPayée) : mapping **inversé**
     (Vente=0/Achat=1, enum legacy `ErpDomaine`), confirmé par `SocieteManager.Complement.cs:520`
     (`domaine == DomaineConvention.Client ? ErpDomaine.Vente : ErpDomaine.Achat`) — vérifié en base
     réelle (§ ci-dessous, `FF260002`/Achat → `DO_Domaine=1`, `FA2600089`/Vente → `DO_Domaine=0`).
3. Le contrôle d'unicité de **numéro** de convention (legacy l.495-496,
   `Get(Societe.No, tiersNo, numero, domaine) != null → "existe déjà"`) est reproduit à l'identique
   (`ExisteNumeroAsync`), bien que non explicitement cité dans la section « Anomalie à corriger » de la
   TASK — il fait partie du « comportement à reproduire » (référence legacy l.471-589 dans son ensemble).

## Vérification base réelle (GR_EMA_DISTRIBUTION, 127.0.0.1, SO_Id=1)

- Schéma `RT_CONVENTIONTIERS` confirmé colonne par colonne (15 colonnes dont `RowVersion` timestamp,
  non exploitée — pas de gestion d'accès concurrentiel optimiste demandée par la TASK) ; `CP_Id` IDENTITY
  confirmée ; **0 ligne** (comme constaté par TASK-127).
- Mapping `DO_Domaine` vérifié sur données réelles : `EC_Id=18237` (`FF260002`, DO_Domaine=1) = facture
  fournisseur (Achat) ; `EC_Id=18251` (`FA2600089`, DO_Domaine=0) = facture client (Vente) — cohérent
  avec le mapping inversé documenté au point 2 ci-dessus.
- **Round-trip CRUD complet exécuté puis nettoyé** (aucune donnée résiduelle, table revérifiée à 0 ligne
  après chaque test) :
  - INSERT (type Convention, CP_Id=2) → SELECT (requête exacte `GetAsync`) → valeurs conformes → UPDATE
    `CP_DateFin` (requête exacte `UpdateDateFinAsync`) → DELETE (requête exacte `DeleteAsync`) → table à 0.
  - INSERT (type Facture, CP_FactureNo=18251, CP_Id=3) → requête exacte `GetConventionsActivesAsync`
    (jointure `RT_ECHEANCE`) → `FactureNumero` résolu à `FA2600089` (conforme) → DELETE → table à 0.
  - `ExisteNumeroAsync` et `EcheanceNonPayeeExisteAsync` rejoués avec leurs requêtes SQL exactes contre
    les lignes ci-dessus / échéances réelles → résultats conformes.

## Vérification DI/démarrage réel

`dotnet run --project Declaration.API` lancé en environnement **Development** (`ValidateOnBuild` actif
par défaut sur `WebApplicationBuilder.Build()` dans cet environnement — toute dépendance non résolvable
aurait fait échouer `Build()` avant même `Application started`). Log observé :
```
info: GRLicence.LicenceMonitor[0]  licence valide ...
info: Microsoft.Hosting.Lifetime[0]  Application started. Press Ctrl+C to shut down.
```
`GET /api/licence/status` → `200 OK`. Aucune exception de résolution DI, aucun crash. Processus arrêté
proprement après vérification (logs temporaires supprimés, aucun résidu).

Aucun contrôleur ne consomme encore `IConventionDelaiPaiementService`/`IDelaiPaiementService` (hors
périmètre TASK-129, UI = TASK-130) — la preuve porte donc sur la **résolvabilité DI** (`ValidateOnBuild`),
pas sur un appel HTTP de bout en bout à ces services.

## Fichiers créés

- `Declaration.Core/ConventionDelaiPaiementValidator.cs`
- `Declaration.Core.Tests/ConventionDelaiPaiementValidatorTests.cs` (17 tests)
- `Declaration.Application/Entities/ConventionDelaiPaiementTiers.cs`
- `Declaration.Application/Interfaces/IConventionDelaiPaiementTiersRepository.cs`
- `Declaration.Application/Services/ConventionDelaiPaiementService.cs`
- `Declaration.Infrastructure/Repositories/ConventionDelaiPaiementRepository.cs`

## Fichiers modifiés

- `Declaration.API/Program.cs` : ajout des 6 lignes DI listées ci-dessus (bloc `// TASK-129`, inséré
  après le bloc `// TASK-128`, avant `// TASK-117`). **Seul mon propre bloc est dans cette TASK** — le
  reste du fichier était déjà committé par TASK-128 au moment de mon édition (vérifié via `git diff`
  isolé sur ce fichier avant de committer).

Aucun autre fichier existant touché. Aucun fichier de TASK-128/TASK-135 modifié.

## Checklist

- [x] Build back `dotnet build DeclarationTVA.slnx` — **0 erreur** (état stable confirmé juste avant
      commit, après deux échecs transitoires étrangers à mon périmètre — voir § Note process).
- [x] Tests `Declaration.Core.Tests` — **88/88 verts** (17 nouveaux, aucune régression).
- [x] Les 4 configurations de chevauchement (contenue, englobante, partielle gauche, partielle droite)
      + 2 cas négatifs (aucune intersection, adjacentes) testés et verts.
- [x] Plafond 180j : 180 passe, 181 lève, 0/négatif lèvent `ArgumentException`.
- [x] Unicité facture : doublon lève, facture libre passe.
- [x] Clôture anticipée : bornes invalides (avant début, après ancienne fin) lèvent ; bornes valides
      (dans la plage, égale à début) passent.
- [x] Aucune modification de schéma / aucune table créée (`DeclarationTVA.sql` non touché).
- [x] SQL confiné à la couche repository (ARCHITECTURE §5) : `ConventionDelaiPaiementRepository` est le
      seul point d'accès SQL pour `RT_CONVENTIONTIERS` côté TASK-129 ; Core/Application sans SQL.
- [x] Pas de secret codé en dur, pas de bypass sécurité.
- [x] `IDelaiPaiementService` (+ ses 3 dépendances) et `IConventionDelaiPaiementService` réellement
      résolvables via DI (API démarrée avec succès, `ValidateOnBuild` actif, `200 OK`).
- [x] SQL de chaque méthode repository rejoué manuellement contre `GR_EMA_DISTRIBUTION` réelle
      (round-trip Create/Get/Update/Delete + jointure FactureNumero), table remise à 0 ligne après coup.
- [x] Suppression (`DeleteAsync`) reproduite **sans garde**, décision assumée (point suivant).

## Décisions/points assumés (documentés, pas de dette silencieuse)

1. **Suppression sans garde** (Étape 4 de la TASK) : `DeleteAsync`/`ConventionDelaiPaiementService.DeleteAsync`
   font un `DELETE` direct par `CP_Id`, sans vérifier si la convention est référencée/utilisée ailleurs —
   reproduit le legacy à l'identique (`ConventionDelaisPaiementTiersDelete`, l.554-562, qui ne fait qu'un
   `Get` puis `Delete`, aucune garde). Décision déjà actée par la TASK, non durcie ici.
2. **Pas de valeur par défaut de `DateFin` proposée à l'écran** : hors périmètre back — le service accepte
   n'importe quelle `DateFin ≥ DateDebut`, aucune date par défaut n'est calculée/imposée côté serveur.
   Ce choix (ergonomie de saisie) est explicitement laissé à TASK-130 (front), comme demandé.
3. **Pièce jointe PDF optionnelle** : seule la cohérence `FileName`/`File` est contrôlée (l'un sans
   l'autre lève une erreur), jamais leur présence — décision PO 19/07/2026, reproduite du legacy (l.489).
4. **Aucun endpoint API (contrôleur) ajouté** : la TASK liste comme livrables « Service + repository
   conventions » et le câblage DI, pas de contrôleur REST — je n'en ai donc pas ajouté (périmètre HTTP
   probablement du ressort de TASK-130, non tranché explicitement mais cohérent avec le périmètre
   strictement listé dans « Livrables »). **Point à confirmer si TASK-130 attend un contrôleur déjà posé
   par TASK-129** — voir § Reste à valider, point 1.
5. **`RowVersion` (timestamp) non exploité** : colonne présente sur `RT_CONVENTIONTIERS` (concurrence
   optimiste) mais non demandée par la TASK — ni lue ni comparée. Pas un risque fonctionnel identifié
   pour ce périmètre (table encore vide en production, aucun usage concurrent connu), mais à signaler si
   un futur consommateur (TASK-130 ou au-delà) a besoin de détecter les écritures concurrentes.

## Reste à valider (NON couvert / hors décision worker)

1. **Contrat HTTP pour TASK-130 non défini par ce VERIFY.** La TASK-129 ne demandait pas de contrôleur ;
   TASK-130 (front) devra soit qu'un contrôleur soit ajouté (par TASK-130 elle-même ou par une TASK dédiée),
   soit que `IConventionDelaiPaiementService` soit exposé autrement. Je n'ai pas tranché ce point — c'est
   une décision d'architecture pour la suite, pas un oubli de ma part (le périmètre listé dans TASK-129
   ne l'incluait pas).
2. **Rejeu sur conventions réelles à grande échelle** : `RT_CONVENTIONTIERS` reste vide en base de
   production/dev à l'issue de cette TASK (mes tests d'intégration manuels ont été insérés puis
   nettoyés). Le chevauchement corrigé, l'unicité facture et la clôture anticipée sont validés par tests
   unitaires + SQL manuel ciblé, pas par un jeu de données réel volumineux (aucun disponible).
3. **Interaction avec un futur contrôle de concurrence (`RowVersion`)** : signalé au point 5 ci-dessus,
   non traité, à réévaluer si un besoin réel émerge.
4. **Endpoint HTTP non testé de bout en bout** (aucun n'existe dans ce périmètre) — seule la résolution
   DI en mémoire a été vérifiée (`ValidateOnBuild` + démarrage réel de l'API).

## Verdict

Convention de délai de paiement par tiers livrée (validator pur + entité/repository CRUD + service
d'orchestration), avec la correction PO du contrôle de chevauchement (4 configurations couvertes par
test), et câblage DI complet du socle TASK-127 (les 6 enregistrements manquants sont posés, l'API
démarre sans exception). SQL vérifié contre données réelles (round-trip + mapping domaine confirmé).
Build 0 erreur, tests 88/88 verts. Seul point ouvert non bloquant pour TASK-129 elle-même : l'absence
d'endpoint HTTP (hors périmètre explicite de cette TASK) à trancher avant/pendant TASK-130.
