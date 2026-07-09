# TASK-012 — API TVA (ASP.NET Core, Clean Architecture, alignée GRC_WEB)

## Contexte
Le module est piloté par un **front web workflow** (TASK-013, décision PO 08/07/2026). Un front
(React/axios) implique une **API HTTP**. On s'aligne sur l'architecture existante **`D:\_vibe\GRC_WEB`**
(Clean Architecture .NET 10, JWT, Dapper, hébergement Windows Service, API qui sert le front en
`wwwroot`) pour rester homogène et faciliter la réorganisation/fusion ultérieure.

**⚠️ Révision (cadrage workflow)** — la v1 de cette task exposait un modèle **one-shot**
(`POST /declarations/preview` → tout le `DeclarationModele` d'un coup). Le workflow TASK-013 impose :
- une **persistance propre** de la déclaration (le process/état est **à nous**, sources GRF/Sage en
  **lecture seule**, **jamais** d'écriture dans les `RT_*` GRFN) ;
- des **endpoints paginés par domaine** (volume réel) ;
- un **état par ligne** persisté (Proposée/Intégrée/Exclue/Reportée) ;
- des **agrégats de checkup** (sans renvoyer les lignes) ;
- une **génération** produisant N fichiers XML par domaine + Excel checkup + rapport d'anomalies.

### Architecture de référence (GRC_WEB) à reproduire
| Couche | Projet GRC | TargetFramework | Rôle |
|---|---|---|---|
| Domain | `GRC.Domain` | net10.0 | entités pures |
| Application | `GRC.Application` | net10.0 | interfaces (`IDbConnectionFactory`) + services (use-cases) |
| Infrastructure | `GRC.Infrastructure` | net10.0-**windows** | SQL Dapper, Sage/Tresorerie |
| API | `GRC.API` | net10.0-**windows** | Controllers, JWT Bearer, OpenAPI, Windows Service, sert `wwwroot` |

### Mapping de nos briques existantes sur ces couches
- **Domain/calcul** = `Declaration.Core` (ventilation + modèle, déjà pur net10).
- **Application** = orchestration (TASK-007) + éligibilité + **use-cases workflow** (cycle de vie,
  intégration par ligne, checkup) — *nouveau*.
- **Infrastructure** = sélection SQL **lecture seule** GRF/Sage (TASK-008), invocation worker OM,
  contrôle vs GRFN (TASK-009), exports (TASK-010/011), **+ persistance déclaration** (*nouveau*).
- **API** = cette task.

> La **réorganisation physique** (fusion GRC_WEB vs solution TVA parallèle) est un choix « réorg après »
> assumé par le PO. Cette task construit l'**API TVA** aux mêmes conventions ; le rattachement se
> décidera ensuite.

## Périmètre STRICT
- **Uniquement** : la couche API (controllers, auth, DI/composition root, config multi-client,
  hébergement) **+** les **use-cases workflow** et la **persistance déclaration** qui les portent.
- **Exclu** : le calcul de ventilation/contrôle (déjà `Declaration.Core`), la sélection SQL brute
  (TASK-008), la génération de fichiers elle-même (TASK-010/011) — l'API les **orchestre et expose**,
  elle ne les réimplémente pas. Front = TASK-013.

## Persistance propre (nouveau — cœur de la révision)
Store **détenu par le module**, configurable **par client**, **isolé de GRFN** (aucune écriture `RT_*`).
- **En-tête** : `Declaration` — numéro `TVA{Societe}-{Exercice}-{Periode}`, société, exercice, période,
  type (mensuel/trimestriel), **statut** (EnCours/Clôturée/Générée/Déposée), dates. **Unicité**
  (société, exercice, période, type).
- **Lignes candidates figées** : `LigneCandidate` — snapshot des lignes proposées par la
  sélection/orchestration au chargement d'un domaine, avec **état** (Proposée/Intégrée/Exclue/Reportée),
  domaine, et les champs déclaratifs (facture, tiers/IF/ICE, HT/taux/TVA/TTC, mode, dates, source).
- **Lignes écartées + motif (règle transparence TASK-013 n°1)** : les lignes que la
  sélection/algorithme **exclut** (chèque non rapproché, hors période, non affecté, non comptabilisé,
  taxe non à taux, facture introuvable) ne sont **jamais silencieuses** → persistées avec état
  *Écartée* + **motif en clair**, exposées au même titre que les candidates. ⇒ le pipeline
  (sélection TASK-008 / orchestration TASK-007) doit **remonter le motif** de chaque rejet, pas
  `continue` en silence.
- **Réconciliation au rechargement** : re-run sélection → **ajoute** les nouvelles candidates,
  **conserve** les décisions existantes, **signale** les disparues (anomalie). On ne perd jamais une
  décision utilisateur.
- Choix du support (base dédiée SQL Server par client vs schéma dédié) = point d'archi à trancher au
  démarrage ; **règle non négociable** : jamais dans les tables GRFN.

## Objectif — endpoints (contrat consommé par TASK-013)
| Endpoint | Rôle | Brique sous-jacente |
|---|---|---|
| `POST /auth/login` | authentification → JWT Bearer | aligné GRC.API |
| `GET /societes` | sociétés + config connexion (multi-client) | config |
| `POST /declarations` | créer (société, exercice, période, type) → n° + id + statut ; **409** si existe | persistance + numérotation |
| `GET /declarations` | liste (filtre société/exercice/statut) paginée | persistance |
| `GET /declarations/{id}` | en-tête + statut + **avancement par domaine** (compteurs/totaux) | persistance (agrégat léger) |
| `GET /declarations/{id}/lignes` | `?domaine=&page=&size=&sort=&filtre[...]` → **page** de lignes | **pagination/filtre/tri SERVEUR** (persistance + sélection TASK-008) |
| `PATCH /declarations/{id}/lignes/{ligneId}` | changer l'état d'une ligne | persistance |
| `POST /declarations/{id}/lignes:bulk` | décision en masse (ids ou {domaine, filtres}) | persistance |
| `GET /declarations/{id}/checkup` | **agrégats** (source/taux/activité + équilibre + anomalies typées) — **ne renvoie pas les lignes** | `Declaration.Core` (TASK-005/006) sur lignes intégrées |
| `POST /declarations/{id}/cloture` | EnCours → Clôturée (refuse si anomalie **bloquante**) | use-case |
| `POST /declarations/{id}/generation` | produit XML par domaine + Excel checkup + rapport anomalies | TASK-010/011 |
| `GET /declarations/{id}/fichiers/{domaine\|checkup\|anomalies}` | télécharge un artefact généré | TASK-010/011 |
| `GET /controle` | (société, période) → rapport d'écarts vs GRFN | TASK-009 |

> `POST /declarations/preview` (v1) est **retiré du contrat public** : la logique preview devient le
> chargement/figeage des candidates d'un domaine (`GET .../lignes` sur une déclaration EnCours).

## Contraintes techniques
- **`net10.0-windows`** pour API + Infrastructure (Sage/worker) ; Domain/Application en `net10.0` pur.
- **JWT Bearer** (`Microsoft.AspNetCore.Authentication.JwtBearer`) comme GRC.API.
- **Dapper** + `System.Data.SqlClient` ; `IDbConnectionFactory` injecté (pattern GRC.Application) pour
  **3 connexions distinctes** : GRF (lecture seule), Sage (lecture seule via worker), **persistance
  module (lecture/écriture)**.
- **Pagination/filtre/tri côté serveur** sur `GET .../lignes` (le front n'itère jamais l'ensemble).
- **Multi-client** : connexions et paramètres **par client**, en configuration — jamais en dur.
- **Worker OM out-of-process** (x86) invoqué par l'orchestration → l'API reste AnyCPU/x64.
- **Hébergement Windows Service** + service statique `wwwroot` (front), comme GRC.
- OpenAPI activé (contrat du front).

## Étapes
1. **Squelette solution** (Domain/Application/Infrastructure/API) + DI câblant `Declaration.*`,
   orchestration, sélection, **persistance**.
2. **Persistance déclaration** : schéma (en-tête + lignes candidates + état), migrations, repository
   Dapper ; numérotation `TVA{Societe}-{Exercice}-{Periode}` + contrainte d'unicité.
3. **Auth JWT** (login + middleware) aligné GRC.API.
4. **CRUD déclaration** : `POST /declarations` (unicité 409), `GET /declarations`, `GET /{id}`.
5. **Lignes par domaine** : `GET .../lignes` **paginé/filtré/trié serveur** (figeage des candidates au
   premier chargement + réconciliation au rechargement) ; `PATCH` + `bulk` sur l'état.
6. **Checkup** : `GET .../checkup` → agrégats + anomalies via `Declaration.Core` (TASK-005/006).
7. **Clôture + génération** : `POST .../cloture` (garde anomalie bloquante), `POST .../generation` +
   `GET .../fichiers/...` (délèguent TASK-010/011).
8. **`controle`** (délègue TASK-009). **Config multi-client** + `wwwroot` + Windows Service.
9. **Tests d'intégration** : auth ; création + unicité ; lignes paginées + changement d'état persisté ;
   checkup agrégé ; clôture bloquée sur anomalie ; génération renvoie les fichiers.

## Livrables
- Solution/couches API TVA alignées GRC_WEB (Clean Arch, JWT, Dapper, Windows Service) **+ persistance
  déclaration**.
- Contrat OpenAPI documenté (base du front TASK-013).
- `VERIFY/TASK-012_verify.md` : appels réels (login → créer → lister lignes paginées → intégrer →
  checkup → clôture → génération), captures des réponses, mapping couches, preuve isolation GRFN
  (aucune écriture `RT_*`).

## Critères de validation
- API démarre, sert OpenAPI, auth JWT fonctionnelle.
- Création de déclaration : numéro `TVA{Societe}-{Exercice}-{Periode}` correct + **unicité** (409).
- `GET .../lignes` : **pagination/filtre/tri serveur** prouvés (une page par requête).
- **État par ligne persisté** (survit au reload) ; actions unitaires **et** en masse.
- Checkup : agrégats source/taux/activité + équilibre + anomalies typées ; **clôture refusée** si
  anomalie bloquante.
- Génération : **un XML par domaine** + Excel checkup + rapport anomalies téléchargeables.
- **Aucune écriture** dans les tables GRFN (`RT_*`) ; persistance isolée dans le store module.
- Séparation des couches respectée (API n'implémente pas le calcul ni la sélection SQL).
- Config multi-client (aucune connexion en dur) ; worker invoqué out-of-process.

## Risques / dépendances
- **Prérequis** : TASK-007 (orchestration) ; `Declaration.Core` (TASK-005/006) pour le checkup ;
  TASK-010/011 pour la génération ; TASK-009 pour `controle`.
- **Partiellement bloqué** : les lignes **réelles** nécessitent TASK-008 (sélection SQL) → base prod +
  credentials. Le squelette + auth + persistance + workflow **sur fixtures** démarrent **sans** la base.
- **Persistance = nouveau composant** : support (base dédiée par client vs schéma) à trancher au
  démarrage ; règle absolue = **hors GRFN**.
- **⚠️ Motifs de rejet à exposer (transparence)** : TASK-007/008 (✅ faits) filtrent aujourd'hui les
  lignes non éligibles **en silence**. La règle n°1 de TASK-013 (aucune ligne silencieuse) impose que
  le pipeline **retourne les écartées + motif**, pas qu'il les jette. ⇒ **petit avenant TASK-007/008**
  probable (sortie enrichie : éligibles **et** écartées-avec-motif) — à cadrer avant l'endpoint lignes.
- **⚠️ Format XML encaissement inconnu** (cf. TASK-013) : seul le relevé de déductions (décaissement)
  est documenté (§3). La génération multi-domaine dépend du **mapping domaine → formulaire DGI** à
  confirmer (encaissement/vente = autre schéma). ⇒ ne pas figer la génération avant ce cadrage.
- Décision réorg (fusion GRC vs solution parallèle) à trancher plus tard — ne pas bloquer.
