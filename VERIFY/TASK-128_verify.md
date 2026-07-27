# TASK-128 Verify — Paramètre "date de mise en route" par société + garde-fou de bascule

> Implémentation worker. Développement NEUF sur GRF web, AUCUNE réutilisation de DLL/code
> WinForms. Contrainte schéma respectée : **aucune modification de `P_SOCIETE`** — deux tables
> **neuves**, propriété exclusive GRF (`DM_PARAM_DELAIPAIEMENT_SOCIETE`, `DM_REPRISE_DELAIPAIEMENT`),
> noms imposés par la TASK. Build back (`dotnet build DeclarationTVA.slnx`) : **0 erreur**.
> Tests Core (`dotnet test Declaration.Core.Tests`) : **88/88 verts** (dont 9 nouveaux pour ce
> garde-fou). Migration rejouée deux fois contre **GR_EMA_DISTRIBUTION** (réelle) : tables créées,
> rejeu idempotent confirmé, upserts testés en base réelle puis données de test nettoyées.
>
> **Session parallèle** : TASK-129 (câblage DI du socle TASK-127 + CRUD conventions) tournait dans
> une autre session sur le même dépôt pendant cette implémentation (fichiers partagés `Program.cs`,
> `DeclarationTVA.sql`). Voir § « Cohabitation avec TASK-129 (session parallèle) » — aucun fichier
> hors périmètre TASK-128 n'a été modifié ni committé par cette session.

## Périmètre livré

Bootstrap "date de mise en route" du module Délai de Paiement Maroc, par société, + reprise
manuelle par échéance — socle nécessaire à TASK-131 (sélection des lignes hors délai) pour ne
jamais calculer silencieusement un retard cumulé sur une échéance jamais suivie par ce système.

1. **Deux tables neuves** (`DeclarationTVA.sql`, section `1i`), idempotentes (`IF OBJECT_ID(...)
   IS NULL`), propriété exclusive GRF :
   - `DM_PARAM_DELAIPAIEMENT_SOCIETE (SO_Id PK, DateMiseEnRoute, UT_Id NULL, DateSaisie)` — une
     ligne par société ayant configuré sa bascule. **Absence de ligne = "pas encore configuré"**
     (jamais une date par défaut arbitraire).
   - `DM_REPRISE_DELAIPAIEMENT (SO_Id, EC_Id, DateDejaDeclareeJusquau, UT_Id NULL, DateSaisie,
     PK(SO_Id, EC_Id))` — reprise manuelle ponctuelle par échéance précise.
   - `SO_Id`/`EC_Id`/`UT_Id` : références **logiques** (`P_SOCIETE.SO_Id`, `RT_ECHEANCE.EC_Id`,
     `P_UTILISATEUR.UT_Id`), **sans contrainte FOREIGN KEY** — même principe déjà appliqué à
     `DM_VENTILATION_SAGE_CACHE.SO_Id` (section `1g`, TASK-118).
   - Droits SQL (section `3b`) : `GRANT SELECT, INSERT, UPDATE, DELETE` accordés au compte
     applicatif `decl_tva_app` sur les 2 tables.

2. **Calculateur PUR (Declaration.Core)** — `DelaiPaiementBootstrapGuard.Resoudre(...)` : garde-fou
   de bascule, hors DB, testable comme `EcheanceLegaleCalculator` (TASK-127). Règle appliquée
   exactement telle que décrite par le PO :
   - Pas de date de mise en route configurée → calcul automatique **désactivé** pour toute
     échéance de la société (décision assumée, documentée ci-dessous — pas une date arbitraire).
   - Échéance légale ≥ date de mise en route, OU échéance déjà historisée dans
     `RT_DECLARATIONDELAISPAIEMENTLG` → `CalculAutomatique`.
   - Échéance antérieure, jamais historisée, sans reprise → `AnterieureRetardInconnu` (statut
     affiché TASK-134 : "Antérieure à la mise en route — retard réel inconnu"), **exclue**.
   - Échéance antérieure, jamais historisée, **avec** reprise saisie →
     `AnterieureAvecRepriseSaisie`, borne = `DateDejaDeclareeJusquau` (solde d'ouverture).

3. **Service d'orchestration (Declaration.Application)** — `IDelaiPaiementBootstrapService` /
   `DelaiPaiementBootstrapService` : lecture/écriture du paramétrage société + de la reprise,
   et `ResoudreBasculeAsync(...)` qui résout les deux lectures puis délègue au calculateur pur.
   L'indicateur d'historique legacy (`RT_DECLARATIONDELAISPAIEMENTLG`) est **fourni par
   l'appelant** (TASK-131) — sa lecture relève de l'algorithme de sélection, explicitement
   **hors périmètre STRICT** de TASK-128 (cf. TASK-128 §Périmètre : "l'algorithme de sélection
   lui-même... TASK-131 consomme cette donnée").

4. **Repository (Declaration.Infrastructure)** — `DelaiPaiementBootstrapRepository` implémente
   `IParametrageDelaiPaiementSocieteRepository` + `IRepriseDelaiPaiementRepository`, upsert
   idempotent via `MERGE` (même pattern que `VentilationSageCacheRepository.UpsertEntries`,
   TASK-072/076), connexion `CreatePersistenceConnection` (même base que `DM_ENTTVA`/`DM_LGTVA`).

5. **Endpoint API minimal** — `DelaiPaiementParametrageController` (`[Authorize]` + garde société,
   même pattern que `DeclarationsController.EstSocieteAutorisee`) :
   - `GET /api/delai-paiement/parametrage/{soId}` → `{ soId, dateMiseEnRoute }` (`dateMiseEnRoute`
     `null` si pas configuré).
   - `PUT /api/delai-paiement/parametrage/{soId}` (body `{ dateMiseEnRoute }`) → upsert.
   - `GET /api/delai-paiement/reprise/{soId}/{ecId}` → reprise ou 404 explicite.
   - `POST /api/delai-paiement/reprise` (body `{ soId, ecId, dateDejaDeclareeJusquau }`) → upsert.
   - `UT_Id` (créateur/modificateur) résolu depuis le claim JWT `"UT_Id"` (posé au login,
     `AuthController`, TASK-074) — jamais codé en dur, `null` si absent/invalide.

6. **Tests unitaires (Declaration.Core.Tests, 9 nouveaux, hors DB)** — couvrent exactement les 4
   scénarios demandés par la TASK + bornes :
   - Société sans date de mise en route configurée → comportement par défaut (calcul auto
     désactivé), y compris avec historique legacy (décision documentée, non un oubli).
   - Échéance postérieure ou égale à la mise en route → calcul automatique (borne stricte testée).
   - Échéance antérieure avec historique legacy → calcul automatique malgré l'absence de reprise.
   - Échéance antérieure sans historique, sans reprise → exclue (`AnterieureRetardInconnu`).
   - Échéance antérieure sans historique, avec reprise → incluse, borne = date saisie.
   - Partie heure ignorée (échéance et reprise), comme `EcheanceLegaleCalculator` (TASK-127).

## Fichiers créés

- `DeclarationTVA.sql` — section `1i` (2 `CREATE TABLE` idempotents) + `GRANT` section `3b` +
  mise à jour des 2 commentaires d'en-tête recensant les tables/migrations.
- `Declaration.Core/DelaiPaiementBootstrapGuard.cs` — calculateur pur + `StatutBasculeEcheance` +
  `ResultatBasculeEcheance`.
- `Declaration.Application/Entities/DelaiPaiementBootstrapEntities.cs` —
  `ParametrageDelaiPaiementSociete`, `RepriseDelaiPaiement`.
- `Declaration.Application/Interfaces/IParametrageDelaiPaiementSocieteRepository.cs`
- `Declaration.Application/Interfaces/IRepriseDelaiPaiementRepository.cs`
- `Declaration.Application/Services/DelaiPaiementBootstrapService.cs` —
  `IDelaiPaiementBootstrapService` + impl.
- `Declaration.Infrastructure/Repositories/DelaiPaiementBootstrapRepository.cs`
- `Declaration.API/Controllers/DelaiPaiementParametrageController.cs`
- `Declaration.Core.Tests/DelaiPaiementBootstrapGuardTests.cs` (9 tests)

## Fichiers modifiés

- `DeclarationTVA.sql` (détail ci-dessus).
- `Declaration.API/Program.cs` — **uniquement** l'ajout de l'enregistrement DI des 3 nouveaux
  types TASK-128 (`IParametrageDelaiPaiementSocieteRepository`, `IRepriseDelaiPaiementRepository`,
  `IDelaiPaiementBootstrapService`), juste après le bloc `DeclarationWorkflowService` existant.
  **Aucun autre enregistrement DI (TASK-129, socle TASK-127) n'a été ajouté ni modifié par cette
  session** — voir § Cohabitation ci-dessous, le fichier était édité en parallèle par TASK-129.

## Cohabitation avec TASK-129 (session parallèle)

Conformément à l'avertissement de la TASK ("TASK-129/135 tournent peut-être en parallèle... ne
touche pas à leurs fichiers"), une session TASK-129 tournait effectivement en parallèle sur le même
dépôt pendant cette implémentation, modifiant `Declaration.API/Program.cs` (câblage DI du socle
TASK-127 + services conventions), `DeclarationRepository.cs`, `FactureInterrogation.cs`,
`FacturesController.cs`, `FactureInterrogationDto.cs`, `IDeclarationRepository.cs`, et créant
plusieurs fichiers `ConventionDelaiPaiement*`, en plus de renommer `TASKS/DDP-TASK-135-...` vers
`IN_PROGRESS/`.

**Aucun de ces fichiers n'a été touché ni committé par cette session TASK-128**, à la seule
exception de `Program.cs`, partagé par nécessité (les deux tâches y ajoutent leur câblage DI).
Le commit de cette session ne contient **que** le hunk TASK-128 de `Program.cs` (3 lignes
`AddScoped` + commentaire) — vérifié explicitement via `git diff --cached` avant commit pour
m'assurer qu'aucune ligne TASK-129 n'y figure. Le reste du travail TASK-129 (câblage DI
`IJoursReposRepository`/`IDelaiPaiementParametrageRepository`/`IConventionDelaiPaiementRepository`/
etc.) reste dans l'arborescence de travail, non committé par moi, pour que la session TASK-129
le committe elle-même.

Un premier `dotnet build DeclarationTVA.slnx` a échoué de façon **transitoire** (3 erreurs
`CS0535` dans `Declaration.Orchestration.Tests`, mocks de test n'implémentant pas encore une
nouvelle méthode `IDeclarationRepository.GetDernieresDatesRapprochementAsync` ajoutée par la
session TASK-129 en cours d'écriture) — confirmé **hors périmètre TASK-128** (`Declaration.API`
seul compilait déjà 0 erreur/0 warning à ce moment, cf. build isolé). Un second `dotnet build` peu
après (la session TASK-129 avait entretemps complété ses mocks) est repassé **0 erreur** — résultat
retenu ci-dessus.

## Checklist

- [x] Build back `dotnet build DeclarationTVA.slnx` — **0 erreur** (2 warnings NU1510 préexistants,
      sans rapport avec TASK-128).
- [x] Tests Core `dotnet test Declaration.Core.Tests` — **88/88 verts**, dont 9 nouveaux pour ce
      garde-fou ; aucune régression sur les tests existants (61 TASK-127 + ceux de la session
      TASK-129 en parallèle).
- [x] Migration idempotente : rejouée deux fois contre `GR_EMA_DISTRIBUTION` réelle
      (`sqlcmd -S 127.0.0.1 -U sa -P 1234 -C -i DeclarationTVA.sql`) — les deux exécutions créent
      les tables puis s'arrêtent au même point préexistant (placeholder `BASE_SAGE` non personnalisé,
      section 3c, **sans rapport avec TASK-128** : la section 1i s'exécute intégralement avant ce
      point les deux fois, confirmée par lecture directe de `INFORMATION_SCHEMA.COLUMNS`).
- [x] Droits SQL confirmés en base réelle : `GRANT SELECT/INSERT/UPDATE/DELETE` présents pour
      `decl_tva_app` sur les 2 nouvelles tables (`sys.database_permissions`).
- [x] Upsert `MERGE` testé en base réelle (insert path + update path) sur les 2 tables, données de
      test nettoyées après vérification (`DELETE` explicite, table remise à 0 ligne).
- [x] Aucune modification de schéma sur une table possédée par `apbs-gr_winform` — seules 2 tables
      neuves `DM_*`, aucun `ALTER TABLE` sur `P_SOCIETE`/`RT_ECHEANCE`/`P_UTILISATEUR`.
- [x] `SO_Id`/`EC_Id`/`UT_Id` : références logiques, **sans FOREIGN KEY**, documenté dans le SQL.
- [x] Pas de SQL inline hors couche repository (ARCHITECTURE §5) : SQL confiné à
      `DelaiPaiementBootstrapRepository` ; Core et Application sans SQL.
- [x] Pas de secret codé en dur, pas de bypass sécurité : endpoints `[Authorize]` + garde société.
- [x] Aucune valeur de repli silencieuse : absence de paramétrage société → `null` explicite
      (jamais une date arbitraire) ; absence de reprise → `AnterieureRetardInconnu` explicite
      (jamais un calcul silencieux depuis la date de facture).
- [x] Critère de validation TASK-128 : "aucune échéance antérieure à la mise en route n'est incluse
      automatiquement... sans reprise manuelle explicite" — couvert par les tests
      `EcheanceAnterieure_SansHistorique_SansReprise_AnterieureRetardInconnu` et
      `EcheanceAnterieure_SansHistorique_AvecReprise_...`.
- [x] Périmètre STRICT respecté : aucun algorithme de sélection (TASK-131) implémenté ; l'indicateur
      d'historique `RT_DECLARATIONDELAISPAIEMENTLG` est un paramètre d'entrée fourni par l'appelant,
      pas lu par ce code.

## Décisions worker documentées (pas d'exigence PO explicite, à signaler si besoin contraire)

1. **Pas d'historique de versions sur `DM_REPRISE_DELAIPAIEMENT`** : une nouvelle saisie de reprise
   pour une échéance donnée **écrase** la précédente (`MERGE` sur PK `(SO_Id, EC_Id)`), pas de table
   d'audit multi-versions. La TASK ne demande pas d'historisation explicite ; à startpoint si un
   besoin d'audit ("qui a changé la reprise et quand, valeur précédente") émerge côté TASK-134/PO.
2. **Société sans date de mise en route configurée → calcul automatique désactivé
   INCONDITIONNELLEMENT**, même si l'échéance a par ailleurs un historique
   `RT_DECLARATIONDELAISPAIEMENTLG` (testé explicitement,
   `SansDateMiseEnRoute_MemeAvecHistoriqueLegacy_CalculAutoResteDesactive`). Lecture assumée du texte
   TASK-128 ("calcul automatique désactivé par défaut... traite l'absence de ligne comme 'pas
   encore configuré'") — interprété comme un interrupteur société global, pas une exception
   au cas par cas. Si le PO souhaite qu'un historique legacy prime malgré l'absence de paramétrage
   société, c'est un changement d'une ligne dans `DelaiPaiementBootstrapGuard.Resoudre` (retirer le
   court-circuit sur `dateMiseEnRouteSociete == null`) — signalé ici plutôt qu'arbitré seul.
3. **Endpoint `PUT parametrage/{soId}` permet aussi une correction** (pas seulement "saisie une
   fois") — la TASK dit "saisie une fois, par société" pour l'objectif métier, mais ne interdit pas
   explicitement une correction ultérieure (erreur de saisie). Le `MERGE` accepte une réécriture ;
   aucune UI n'est livrée par cette tâche (TASK-134), donc ce point n'a pas d'impact utilisateur
   immédiat — à confirmer si une correction de date de mise en route doit être bloquée/tracée
   différemment une fois que TASK-131 aura commencé à consommer la valeur.

## Reste à valider (NON couvert par ce VERIFY)

1. **Endpoints non testés via appel HTTP réel** (pas de client HTTP/Swagger lancé dans cette
   session) — seule la compilation + le câblage DI ont été vérifiés statiquement. Le repository
   sous-jacent, lui, a été testé directement contre la base réelle (MERGE insert/update confirmés).
2. **`RT_DECLARATIONDELAISPAIEMENTLG`** : table mentionnée par la TASK comme le référentiel legacy
   partagé pour l'indicateur d'historique — son existence/son schéma réel n'a **pas** été vérifié en
   base dans cette session (hors périmètre STRICT, cf. TASK-128 §Périmètre : lecture confiée à
   TASK-131). À vérifier par TASK-131 avant de consommer `aHistoriqueDeclarationLegacy`.
3. **Décision worker point 2 ci-dessus** (calcul auto désactivé inconditionnellement sans date de
   mise en route configurée, même avec historique) : interprétation raisonnable mais non validée
   explicitement par le PO — à confirmer avant que TASK-131 ne s'appuie dessus en production.
4. **Câblage DI TASK-129 pour le socle TASK-127** (`IConventionDelaiPaiementRepository`,
   `IDelaiPaiementService`, etc.) : vu en cours d'écriture par la session parallèle pendant cette
   implémentation, non revu ni validé par ce VERIFY (hors périmètre TASK-128 — sera couvert par le
   VERIFY de TASK-129).

## Verdict

Livré : 2 tables neuves idempotentes + droits SQL, garde-fou pur testé (9/9), service
d'orchestration, repository (upsert réel vérifié en base), endpoints API minimaux protégés. Build
0 erreur, tests Core 88/88. Aucune modification de schéma sur une table winform. Points 1-4 de
« Reste à valider » sont informatifs (tests HTTP manuels, dépendance TASK-131 sur
`RT_DECLARATIONDELAISPAIEMENTLG`, une décision d'interprétation à confirmer, DI TASK-129 hors
périmètre) — aucun ne bloque la livraison du socle TASK-128 lui-même.
