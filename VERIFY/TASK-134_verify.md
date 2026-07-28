# TASK-134 Verify — DDP : front (liste, fiche, sélection des lignes, contrôle)

> Implémentation worker. Développement NEUF sur GRF web, AUCUNE réutilisation de DLL/code WinForms
> (seule la sémantique legacy est reproduite, via les services TASK-131/132/133 déjà livrés).
> **Contrainte schéma respectée** : aucune table créée, aucune colonne ajoutée, aucune table
> `apbs-gr_winform` altérée. Le seul ajout côté SQL est **un nom de colonne EXISTANTE dans un `SELECT`**
> (`P_SOCIETE.SO_TypeDecDP`, vérifié en base réelle) — cf. § Preuve de non-modification de schéma.
> Build back `dotnet build DeclarationTVA.slnx` : **0 erreur** (7 warnings, tous préexistants).
> Build front `npx tsc -b` + `npx vite build` : **0 erreur**. `npx oxlint` : **0 erreur**.
> Tests e2e Playwright **task134.spec.ts : 3/3 verts** (cycle complet + les 2 critères structurants).
> **Cycle complet rejoué RÉELLEMENT via HTTP contre `GR_EMA_DISTRIBUTION`** (API démarrée, JWT réel,
> 1 459 échéances examinées, 491 candidates, création → intégration → clôture → contrôle IF/ICE →
> génération → dépôt, **plus** le cas bloquant sur un fournisseur réellement non conforme) —
> **données de test intégralement nettoyées** (4 tables revenues à 0 ligne, vérifié par requête
> indépendante ; fichiers XML/ZIP générés supprimés).

---

## 0. Conformité du fichier TASK (à signaler à l'architecte)

Comme les TASK-127 à 133, `IN_PROGRESS/DDP-TASK-134-…md` suit le **template DDP** (Contexte / Écrans à
livrer / Point corrigé PO / Périmètre STRICT / Étapes / Livrables / Critères de validation / Risques),
**pas** le template `ARCHITECTURE.md §3` : pas de champ `FILES`, `Status`, `Priority`, `Risk`,
`Module`. **Non bloqué** (identique aux 6 TASK DDP déjà implémentées et validées avec ce template) ;
la liste des fichiers a été dérivée des sections « Écrans à livrer » et « Étapes ».

**Écart de périmètre assumé et signalé** : le texte de la TASK dit « front », en supposant l'existence
d'un back HTTP — **qui n'existait pas**. Les VERIFY TASK-131 §9 n°7, TASK-132 §9 n°4 et TASK-133
§Reste à valider n°2 laissent explicitement à TASK-134 la création des endpoints **et** le contrôle
d'autorisation par société. Sans eux, aucun des 4 écrans n'est réalisable. Le contrôleur HTTP fait donc
partie de ce livrable — même situation exacte que TASK-130, qui avait dû créer
`ConventionsDelaiPaiementController` non prévu par son propre texte.

---

## 1. Périmètre livré

### 1.1 Back — `DeclarationsDelaiPaiementController` (le chaînon manquant)

`api/declarations-delai-paiement`, **pur passe-plat** vers `IDeclarationDelaiPaiementService`
(TASK-132), `ISelectionDelaiPaiementService` (TASK-131) et
`IDeclarationDelaiPaiementGenerationService` (TASK-133). **Aucune règle métier réimplémentée** : le
contrôleur route les exceptions métier vers des codes HTTP explicites et applique la garde
d'autorisation.

| Verbe / route | Rôle | Écran |
|---|---|---|
| `GET /` (`?soId=`) | liste des déclarations (+ nb lignes + transitions autorisées) | 1 |
| `GET /parametrage` (`?soId=`) | type de déclaration par défaut (`SO_TypeDecDP`, CDC §7.1) | 2 & 4 |
| `GET /controle` (`?soId=&exercice=&type=&trimestre=`) | lignes hors délai, **bornes CALCULÉES** | 4 |
| `GET /{ddpId}` | entête de la fiche | 2 |
| `POST /` | création (exercice/type/trimestre/libellé) | 2 |
| `PUT /{ddpId}/libelle` | seule modification autorisée après création | 2 |
| `DELETE /{ddpId}` | suppression (gardes TASK-132) | 1 |
| `GET /{ddpId}/lignes` | lignes déjà intégrées | 2 |
| `DELETE /{ddpId}/lignes/{ddplId}` | retrait d'une ligne | 2 |
| `GET /{ddpId}/selection` | candidates **pour la période de la déclaration parente** | 3 |
| `POST /{ddpId}/lignes` | intégration manuelle multi-sélection | 3 |
| `POST /{ddpId}/cloture` \| `/decloture` \| `/depot` | cycle de vie | 2 |
| `GET /{ddpId}/controle-identite-fiscale` | contrôle IF/ICE **informatif** (ne lève pas) | 2 |
| `POST /{ddpId}/generation` \| `/generation/annulation` | TASK-133 | 2 |
| `GET /{ddpId}/fichier` | téléchargement du ZIP généré | 2 |

**Sécurité (le point explicitement laissé par TASK-132 §9 n°4) :**

- `[Authorize]` + `EstSocieteAutorisee` (claim `UT_Admin`=1 ou `SO_Id` dans le claim CSV `Societes`) —
  **dupliquée volontairement**, comme `ConventionsDelaiPaiementController`/`DelaiPaiementParametrageController` :
  il n'existe aucun base controller partagé dans ce dépôt et le refactoriser est hors périmètre
  (constat déjà documenté par TASK-128/130). Sur les routes `{ddpId}`, l'entête est chargée puis
  **`SocieteId` vérifié** avant toute action.
- **Propagation OBLIGATOIRE du claim JWT `UT_Id`** sur toutes les écritures : un jeton sans ce claim
  reçoit un **401 explicite**, jamais un `0` « utilisateur inconnu » (le service TASK-132 refuserait de
  toute façon). Vérifié en base réelle : `UT_Id = UT_IdModif = 1` sur les lignes créées via HTTP.
- Routage d'exceptions : `ArgumentException` → 400, `InvalidOperationException` → 409 avec le **message
  serveur TEL QUEL** (tournures legacy conservées), `ApplicationException` de l'exporter → 409.
  **Aucun 500 générique.**

### 1.2 Back — filtre de période : aucune plage libre POSSIBLE, pas seulement « non affichée »

C'est le point corrigé PO du 19/07/2026, traité **structurellement** et non par convention d'UI :

- **aucun endpoint du contrôleur n'accepte de paramètre `dateDebut`/`dateFin`** ;
- `GET /{ddpId}/selection` lit `DDP_DateDebut`/`DDP_DateFin` **en base** — le client ne transmet que
  le `ddpId` ;
- `GET /controle` reçoit `exercice` + `type` (+ `trimestre`) et **calcule** les bornes via
  `DeclarationDelaiPaiementCycleDeVie.CalculerPeriode` — **la même fonction que la création d'une
  déclaration** (TASK-132), donc jamais deux façons de résoudre une période ;
- vérifié réellement : un appel `…/controle?…&dateDebut=2020-01-01&dateFin=2020-12-31` renvoie
  **exactement les mêmes bornes** `01/01/2026..31/03/2026` (paramètres parasites ignorés, aucune
  liaison de modèle vers une plage libre) ;
- `trimestre` manquant sur une trimestrielle ⇒ **400 explicite**, jamais un repli silencieux sur T1.

L'anomalie legacy (`FrmControleLigneDelaisPaiement.cs:56-60`, deux dates libres sans lien avec le
paramétrage société) n'est donc **pas reproduite**, ni côté API ni côté écran.

### 1.3 Back — additions LECTURE SEULE stricictement nécessaires (`SO_TypeDecDP`)

La TASK exige que « le type par défaut proposé vienne du paramétrage société (`SO_TypeDecDP`, §7.1) ».
Cette colonne n'était **lue nulle part** dans le dépôt. Choix retenu, le moins invasif possible :

| Fichier | Modification | Pourquoi ce choix |
|---|---|---|
| `Declaration.Application/Entities/DeclarationDelaiPaiementEntities.cs` | +`SocieteDelaiPaiementInfo.TypeDeclarationParDefautCode` | classe **déjà** lue par `GetSocieteInfoAsync` (TASK-133) ⇒ **aucune méthode ajoutée à `IDeclarationDelaiPaiementRepository`**, donc **aucun faux repository de test à modifier** (les 2 `FakeRepository`/`FauxRepository` d'orchestration restent intacts) |
| `Declaration.Infrastructure/Repositories/DeclarationDelaiPaiementRepository.cs` | `SO_TypeDecDP` ajouté au `SELECT` existant + champ sur la projection privée | `SELECT` **additif** sur une colonne EXISTANTE ; aucune requête existante modifiée |
| `Declaration.Application/Services/DeclarationDelaiPaiementService.cs` | +`GetParametrageSocieteAsync(soId)` (passe-plat) | le contrôleur ne parle **jamais** au repository (ARCHITECTURE §5) |

Le contrôleur ne « corrige » pas une valeur hors 1..2 : il renvoie `typeParDefaut = null` et **l'écran
affiche son propre repli visible** (Trimestrielle) — jamais un défaut silencieux côté serveur.

### 1.4 Front écran 1 — Liste des déclarations (`DeclarationsDelaiPaiementPanel.tsx`)

Colonnes exigées par la TASK : n° déclaration, période (`T1 2026` / `Annuelle 2026`), dates, statut,
**nb lignes**, fichier, dépôt, libellé, actions. Réutilise `ExcelFilter`, `ColumnSelector`,
`useColumnPrefs` (TASK-068) — **aucun composant générique dupliqué** ; même densité et même structure
de grille flexbox que `ConventionsDelaiPaiementPanel.tsx` (TASK-130, référence directe du domaine DDP).

### 1.5 Front écran 2 — Fiche déclaration

Création (exercice / type / trimestre / libellé — **ni numéro ni date**), clôture, déclôture, dépôt
manuel, déclenchement/annulation de génération, téléchargement du ZIP, modification du libellé,
affichage et retrait des lignes intégrées.

**L'état des boutons vient du SERVEUR** (`declaration.actions`) : chaque drapeau est obtenu en appelant
la garde PURE correspondante de TASK-132 (`ValiderCloture`, `ValiderDepot`, …) et en observant si elle
lève — **la même source de vérité, aucune règle de transition dupliquée** ni en C# de présentation ni
en TypeScript. Conséquence vérifiée en réel : après dépôt, les 8 drapeaux sont `false` et l'écran
n'affiche plus aucune action de cycle de vie.

### 1.6 Front écran 3 — Popup de sélection des lignes hors délai

- **Période non modifiable** : les bornes de la déclaration parente sont **affichées** (avec la mention
  « non modifiable ») ; aucun `input[type=date]` n'existe dans la popup (assertion e2e dédiée).
- **Deux listes structurellement séparées** : candidates (cochables) et « antérieures à la mise en
  route — retard réel inconnu » (**la case à cocher n'est même pas rendue** — `onBasculer` absent ⇒
  l'intégration est impossible par construction, pas seulement désactivée).
- Aucun `Depassement` affiché pour une ligne bloquée : un **badge « retard inconnu »**, jamais un 0.
- Colonnes de traçabilité du calcul incrémental TASK-131 exposées : « Déjà déclaré au »
  (`BorneReference`) et « Constaté au » (`BorneActuelle`) à côté du dépassement — l'utilisateur voit
  **pourquoi** le chiffre vaut ce qu'il vaut.
- Compte rendu d'intégration remonté tel quel (intégrées / déjà intégrées / refusées / introuvables).

### 1.7 Front écran 4 — Contrôle des lignes hors délai (`ControleLignesDelaiPaiementPanel.tsx`)

- **Visibilité/reporting pur** : ce fichier **ne contient aucun appel d'intégration** (`grep` :
  aucune occurrence de `integrerLignes` ni de `POST …/lignes`). Assertions e2e : aucun bouton
  « Intégrer », **aucune case à cocher** sur tout l'écran.
- **Période raisonnée** : sélecteurs exercice + type (+ trimestre) uniquement ; bornes **calculées par
  le serveur** et affichées en lecture seule. Assertions e2e : `input[type=date]` et
  `input[type=datetime-local]` en **compte 0** sur l'écran, et **aucune requête ne transporte
  `dateDebut`/`dateFin`**.
- Type par défaut proposé depuis `SO_TypeDecDP`.
- **Badge explicite** « Reprise manuelle requise » + colonne dépassement affichant « inconnu »
  (jamais un chiffre calculé) + **action « Saisie manuelle »** ouvrant la modale
  « déjà déclaré jusqu'au [date] » — exactement ce qu'exige la TASK.
- Candidates et lignes bloquées sont affichées dans **une seule grille** avec une colonne « Statut »
  explicite : un écran de contrôle ne doit rien masquer.

### 1.8 Front — accès à la « date de mise en route » (TASK-128), exigence des VERIFY amont

`MiseEnRouteDelaiPaiementModal.tsx` : **modale dédiée réutilisée par les 3 points d'entrée** (liste,
fiche vide, popup de sélection) + l'écran de contrôle, consommant les endpoints TASK-128 **déjà
livrés** (`GET`/`PUT /api/delai-paiement/parametrage/{soId}`) — **aucun front ne les appelait avant
cette TASK**, et aucun endpoint n'a été ajouté pour cela.

**Décision documentée (choix demandé explicitement par le prompt)** : une **modale partagée** plutôt
qu'un écran de paramétrage à part entière. Justification : le branchement d'un écran au menu est
explicitement TASK-136 (hors périmètre) ; un écran non atteignable n'aurait résolu aucun des blocages
signalés, alors qu'une modale accessible depuis chaque endroit où l'absence de date se manifeste les
résout tous.

En plus de l'accès, l'**explication** est affichée : quand `dateMiseEnRouteSociete == null`, un bandeau
bloquant nomme la cause (« aucun dépassement n'est calculé, toutes les lignes restent en reprise
manuelle requise ») avec le bouton de saisie à côté. C'était le risque n°1 signalé par
VERIFY TASK-131 §9 n°3 et TASK-132 §9 n°2 : « l'écran apparaîtra vide et bloqué au client ».

`RepriseManuelleLigneModal` couvre la reprise **par échéance** (`POST /api/delai-paiement/reprise`,
TASK-128), déclenchée depuis le badge de l'écran de contrôle.

### 1.9 Front — contrôle IF/ICE bloquant : affiché, jamais rejoué

Le prompt et VERIFY TASK-132 §8 n°9 sont explicites : la longueur exigée (8/15) est un **point ouvert
PO/fiscaliste** que le worker ne doit pas trancher. En conséquence :

- **aucune validation de longueur IF/ICE n'existe dans le front** (`grep` sur les 3 fichiers front
  livrés : aucune occurrence de `8`/`15` appliquée à un IF/ICE, aucun contrôle de format) ;
- en cas de refus, le front affiche le **message serveur tel quel** *et* la liste structurée des
  fournisseurs fautifs avec **les motifs déjà libellés par le back** (`motifsLibelles`), jamais
  reformulés ;
- un bouton « Contrôler IF/ICE » permet le contrôle **informatif** avant de tenter la génération
  (`ControlerIdentiteFiscaleAsync`, qui ne lève pas) — évite de découvrir le blocage au dernier moment.

Pour que le front puisse afficher les fautifs **ligne par ligne**, le contrôleur joint
`FournisseursFautifs` à la réponse 409 de `/generation`, en **relisant** le verdict via le contrôle
informatif (aucun effet de bord, aucune règle réévaluée localement).

---

## 2. Fichiers créés

Back :
- `Declaration.API/Controllers/DeclarationsDelaiPaiementController.cs` — 18 endpoints, garde société,
  propagation `UT_Id`, routage d'exceptions.
- `Declaration.API/Dtos/DeclarationDelaiPaiementDto.cs` — DTOs (liste/fiche, `Actions` dérivées des
  gardes TASK-132, lignes de sélection, lignes intégrées, compte rendu d'intégration, verdict IF/ICE).

Front :
- `declaration-tva-web/src/DeclarationsDelaiPaiementPanel.tsx` — écrans 1 + 2 + 3 (liste, fiche, popup
  de sélection) + modales création/libellé.
- `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx` — écran 4.
- `declaration-tva-web/src/MiseEnRouteDelaiPaiementModal.tsx` — modales TASK-128 (date de mise en
  route + reprise manuelle par échéance) et éléments de modale partagés.
- `declaration-tva-web/src/task134-harness.tsx` + `declaration-tva-web/task134.html` — harnais de test
  (servi par vite dev uniquement, **absent du build de prod** : `vite build` n'a qu'`index.html` en
  entrée), même pattern que task130/138/139.
- `declaration-tva-web/tests/task134.spec.ts` — 3 tests e2e Playwright.
- `VERIFY/task134-A1|A2|A3|B1|B2|C-*.png` — 6 captures Chromium.

## 3. Fichiers modifiés

- `Declaration.Application/Entities/DeclarationDelaiPaiementEntities.cs` — +1 propriété (additif).
- `Declaration.Application/Services/DeclarationDelaiPaiementService.cs` — +1 méthode passe-plat lecture
  seule (additif).
- `Declaration.Infrastructure/Repositories/DeclarationDelaiPaiementRepository.cs` — 1 nom de colonne
  EXISTANTE ajouté à un `SELECT` existant + 1 champ de projection (additif).
- `declaration-tva-web/src/api.ts` — types + 21 fonctions HTTP DDP (additif, en fin de fichier ;
  aucune fonction existante modifiée).
- `TASKS/DDP-TASK-134-…md` → `IN_PROGRESS/DDP-TASK-134-…md` (déplacement demandé).

**`Declaration.API/Program.cs` NON touché, NON commité** — aucune inscription DI n'était nécessaire :
les 3 services consommés (`IDeclarationDelaiPaiementService`, `ISelectionDelaiPaiementService`,
`IDeclarationDelaiPaiementGenerationService`) sont déjà enregistrés. **⚠ Voir § Reste à valider n°1** :
l'enregistrement de `IDeclarationDelaiPaiementGenerationService` est présent dans l'arbre de travail
mais **pas encore commité** — dépendance inter-fils à signaler.

### Cohabitation avec les sessions parallèles

Modifications d'un **autre fil de travail** présentes dans l'arbre (correctif TVA TASK-186→190) :
`CHANGELOG.md`, `DONE.md`, `TODO.md`, `LANCEMENT_DEV.md`, `TASKS/TASK-029-…md`,
`DOCS/GUIDE_PROCESS_DECLARATION_TVA.html`, `VERIFY/TASK-006|007|024_verify.md`,
`declaration-tva-web/.env.development`, `declaration-tva-web/vite.config.ts`,
`DOCS/PROMPT-WORKER-*.md`, 8 captures `VERIFY/task138-*|task139-*.png`, renommages
`IN_PROGRESS`→`DONE_DETAIL` TASK-186→190 déjà indexés, **plus `Declaration.API/Program.cs`**.
**Aucun de ces fichiers n'a été modifié ni stagé par cette session** : commit fait avec des
**pathspecs explicites** (jamais `git add -A`, jamais `git commit -a`), `git diff --cached` relu
fichier par fichier avant validation.

---

## 4. BUILD

| Commande | Résultat |
|---|---|
| `dotnet build DeclarationTVA.slnx` | **0 erreur**, 7 warnings — **tous préexistants** (`NU1510` Setup, `CS8604`/`CS8602` Export.Xml, `CS8618` Selection, `xUnit1012`, `CS0105` Program.cs) ; **aucun dans les fichiers TASK-134** |
| `npx tsc -b` (front) | **0 erreur** |
| `npx vite build` (front) | **0 erreur** (462 kB, 1 848 modules) |
| `npx oxlint` | **0 erreur** ; le seul warning sur un fichier neuf est `only-export-components` sur `task134-harness.tsx`, **identique** à task130/138/139-harness (nature d'un harnais) |

## 5. Tests

### 5.1 Non-régression back (aucun test cassé par les 3 diffs additifs)

| Projet | Résultat |
|---|---|
| `Declaration.Core.Tests` | **215/215 verts** |
| `Declaration.Orchestration.Tests` | **225/225 verts** (les 2 `FakeRepository`/`FauxRepository` n'ont **pas** eu besoin d'être touchés — c'était l'objectif du choix §1.3) |
| `Declaration.Export.Xml.Tests` | **26/26 verts** |
| `Declaration.Export.Excel.Tests` | **3/3 verts** |
| `Declaration.Selection.Tests` | 59/60 — l'unique échec est **préexistant et sans rapport** (`IntegrationRegressionTests`, chaîne codée en dur `Server=.;Integrated Security=True`) |
| `Declaration.Controle.Tests` | 1/2 — échec **préexistant et sans rapport** (`ComparateurTests`, `Server=.\sql2022` inexistant) |

Aucun fichier de ces deux derniers projets n'a été touché — constat identique aux VERIFY TASK-131/132.
**Aucun test unitaire neuf** n'a été ajouté : le livrable n'apporte aucune règle métier testable hors
base (le contrôleur est un passe-plat, les DTO des projections) — la couverture demandée par la TASK
est l'**e2e**, faite ci-dessous.

### 5.2 e2e Playwright — `tests/task134.spec.ts` : **3/3 verts**

Harnais `task134.html` rendant les **VRAIS** composants dans Chromium, `/api/**` mocké au niveau
réseau par un dispatcher unique **STATEFUL** qui reproduit les contrats du contrôleur (y compris le
recalcul des drapeaux `actions` selon les gardes TASK-132 et le 409 `Message` + `FournisseursFautifs`).

**Test A — cycle complet exigé par la TASK**, en 9 étapes :
création (aucun champ de date dans le formulaire — assertion) → ouverture de la fiche → popup de
sélection (période affichée `01/01/2026 → 31/03/2026`, mention « non modifiable », **0
`input[type=date]`**, ligne bloquée visible et **sans case à cocher**) → intégration multi-sélection
(2 lignes) → clôture → **génération BLOQUÉE** (message serveur affiché tel quel + `[4411INWI]` et
`[FOUBREL]` avec leurs motifs, et **le bouton de dépôt reste absent**) → correction → contrôle IF/ICE
conforme → génération réussie → dépôt → **plus aucune action de cycle de vie proposée** (3 assertions).

**Test B — écran de contrôle** : `input[type=date]`/`datetime-local` en **compte 0** ; bornes annuelles
puis T2 **calculées par le serveur** ; **aucune requête ne contient `dateDebut`/`dateFin`** (inspection
de toutes les requêtes réellement émises) ; badge « Reprise manuelle requise » + dépassement
« inconnu » ; **aucun bouton « Intégrer », aucune case à cocher** ; saisie de reprise manuelle →
`{ ecId: 17001, date: '2025-12-31' }` réellement envoyé et badge disparu après rechargement.

**Test C — société sans date de mise en route** : bandeau explicatif présent, accès direct à la saisie,
`PUT` réellement émis, bandeau disparu ensuite. C'est le cas réel constaté en base (§6).

Captures : `VERIFY/task134-A1-fiche-lignes-integrees.png`, `-A2-generation-bloquee-ifice.png`,
`-A3-deposee.png`, `-B1-controle-periode-raisonnee.png`, `-B2-reprise-manuelle-saisie.png`,
`-C-mise-en-route.png`.

---

## 6. Vérification RÉELLE via HTTP contre la base `GR_EMA_DISTRIBUTION`

> Contrairement aux VERIFY TASK-127/131/132, `DESKTOP-5BFKKEP` **est résolvable dans cette session** :
> `sqlcmd -S DESKTOP-5BFKKEP` fonctionne. **`Declaration.API` a été réellement démarrée**
> (`http://localhost:5000`), un **JWT réel** obtenu par `POST /api/auth/login`, et **tous les endpoints
> livrés ont été appelés en HTTP** — c'est la première fois que le domaine DDP est exercé bout en bout
> à travers l'API (les VERIFY TASK-131/132/133 utilisaient un harnais console, faute d'endpoints).

**Prérequis rencontré et traité (à signaler)** : les tables `DM_PARAM_DELAIPAIEMENT_SOCIETE` et
`DM_REPRISE_DELAIPAIEMENT` (TASK-128) étaient **ABSENTES** de la base, alors que les VERIFY
TASK-131/132 les décrivaient présentes — la base a manifestement été reconstruite depuis. Elles ont
été créées **en copiant à l'identique le DDL de `DeclarationTVA.sql` §1i** (script du dépôt, déjà
approuvé, tables `DM_*` **propriété exclusive GRF** — aucune table `apbs-gr_winform` concernée).
`DeclarationTVA.sql` n'a **pas** été modifié : c'est une application du script existant à une base de
dev, pas une écriture de schéma. **Point à signaler au PO/architecte** : la base de dev n'était pas à
jour du script de schéma (cf. § Reste à valider n°2).

**Résultats réels observés :**

| Point | Résultat réel |
|---|---|
| Login JWT réel | `Admin` / `UT_Admin=1` ⇒ claim `UT_Id=1` propagé |
| `GET /parametrage?soId=1` | `SO_TypeDecDP = 1` ⇒ `typeParDefaut = "Annuelle"` — **colonne réellement lue**, valeur confirmée par `sqlcmd` |
| Société NON configurée (état initial) | `GET /controle?exercice=2026&type=trimestrielle&trimestre=1` ⇒ **1 459 échéances examinées, 0 candidate, 491 en reprise manuelle requise**, `dateMiseEnRouteSociete = null` ⇒ **le scénario « écran vide et bloqué » des VERIFY amont est REPRODUIT sur données réelles**, et l'écran l'explique désormais (§1.8) |
| **Plage libre impossible** | `…/controle?…&dateDebut=2020-01-01&dateFin=2020-12-31` ⇒ bornes **inchangées** `01/01/2026..31/03/2026` |
| Trimestre manquant | **400** « 'trimestre' (1..4) est obligatoire pour une déclaration trimestrielle. » |
| Après `PUT /delai-paiement/parametrage/1` (`2023-07-01`) | **491 candidates, 0 en reprise manuelle** ⇒ le déblocage par la saisie de la date est **vérifié de bout en bout via l'UI-API** |
| Bornes annuelles calculées | `type=annuelle` ⇒ `01/01/2026..31/12/2026`, **1 125 candidates** (jamais une saisie de dates) |
| Création T1 2026 | `DDP_Numero = DDP26070001`, `DateDebut 2026-01-01 00:00:00`, `DateFin 2026-03-31 23:59:59`, `Statut 0`, `Type 2`, `Periode 1`, libellé transmis |
| `actions` à la création | `peutIntegrerLignes/peutModifierLibelle/peutSupprimer = true`, `peutCloturer = false` (0 ligne) — **cohérent avec les gardes TASK-132** |
| Doublon de période | **409** « Déclaration existe déjà pour la même période (déclaration [DDP26070001], 01/01/2026 - 31/03/2026). » |
| Clôture sans ligne | **409** « La déclaration ne contient aucune ligne. » |
| `GET /{ddpId}/selection` | période = **`01/01/2026..31/03/2026` lue en base**, 491 candidates |
| Intégration multi-sélection (3 clés) | 3 intégrées / 0 refusée / 0 introuvable ; **3 lignes réellement en base** (`DDPL_Id 1651-1653`, `Depassement 29.000000`, `EcheanceLegale 2026-03-02`) |
| Rejeu du même lot | **0 intégrée, 3 déjà intégrées** (garde anti-double-intégration TASK-132 traversée par HTTP) |
| Audit trail | `UT_Id = UT_IdModif = 1` en base — **claim JWT réellement propagé** |
| Clôture ⇒ `actions` | `peutGenererFichier = true`, `peutDeposer = false` |
| Contrôle IF/ICE (3 tiers conformes) | conforme, 3 fournisseurs / 3 lignes, 0 fautif |
| Dépôt sans fichier | **409** « Le fichier du déclaration n'est pas généré. » |
| Génération | **OK** ⇒ `DDP_IsGeneretedFile = 1` en base |
| `GET /{ddpId}/fichier` | ZIP réel **671 octets**, contenant `DDP26070001-2026-T1.xml` |
| Dépôt | `DDP_IsDepose = 1` ; **les 8 drapeaux `actions` passent à `false`** |
| **Cas BLOQUANT IF/ICE sur données réelles** | 2ᵉ déclaration T2 2026 (`DDP26070002`, 571 candidates) avec 2 lignes dont **`4411INWI`** (IF **et** ICE vides dans `F_COMPTET`, vérifié par `sqlcmd` sur la base Sage) ⇒ génération **409**, message listant `[4411INWI] Tiers à créer : identifiant fiscal absent ; ICE absent (1 ligne concernée)`, `FournisseursFautifs` **structuré renvoyé (1 élément)**, et **`DDP_IsGeneretedFile` reste 0** |
| Téléchargement sans génération | **404** explicite « Fichier non généré pour la déclaration [DDP26070002] — lancez d'abord la génération. » |
| Blocage levé après correction | déclôture → retrait de la ligne fautive → reclôture → génération **OK** |
| Annulation de génération | `fichierGenere = false`, `peutGenererFichier = true`, et le téléchargement retombe en **404** ⇒ **fichiers physiques réellement supprimés** (TASK-133) |
| Garde société / auth | `soId=0` ⇒ **400** ; `ddpId` inexistant ⇒ **404** ; **sans jeton ⇒ 401** |
| **Nettoyage** | `RT_DECLARATIONDELAISPAIEMENT` **0**, `…LG` **0**, `DM_PARAM_DELAIPAIEMENT_SOCIETE` **0**, `DM_REPRISE_DELAIPAIEMENT` **0** (vérifié par `sqlcmd` indépendant après le run) ; les 2 fichiers `DDP26070001-2026-T1.xml`/`.zip` supprimés du dossier `exports` |

---

## 7. Preuve de non-modification de schéma

- `DeclarationTVA.sql` **non modifié** (absent du `git status` de cette session).
- **Aucun `CREATE TABLE`/`ALTER TABLE`/`CREATE INDEX`/`DROP`/`TRUNCATE`/`MERGE` dans les fichiers
  livrés** ; aucun SQL du tout dans le contrôleur ni les DTO (ARCHITECTURE §5 : SQL uniquement en
  couche repository).
- Le seul ajout côté SQL est **un nom de colonne EXISTANTE dans un `SELECT` existant**
  (`P_SOCIETE.SO_TypeDecDP`, `int` — existence confirmée par `INFORMATION_SCHEMA.COLUMNS`). `P_SOCIETE`
  est **lue uniquement** (aucun `UPDATE` ajouté).
- Écritures **exclusivement** via les services TASK-132/133 sur `RT_DECLARATIONDELAISPAIEMENT`/`…LG`
  (tables EXISTANTES) et TASK-128 sur `DM_PARAM_DELAIPAIEMENT_SOCIETE`/`DM_REPRISE_DELAIPAIEMENT`
  (tables `DM_*` **neuves, propriété GRF**). **Aucune table `apbs-gr_winform` altérée**, aucune
  contrainte/index posé. **Aucun besoin de colonne supplémentaire n'a été rencontré.**
- La création des 2 tables `DM_*` en base de dev (§6) applique **le DDL déjà présent dans le script du
  dépôt**, à l'identique — ce n'est pas un schéma écrit par cette TASK.
- Aucun secret codé en dur ; aucun bypass de sécurité (tous les endpoints `[Authorize]` + garde
  société + `UT_Id` obligatoire en écriture).

---

## 8. Checklist

- [x] Build back `dotnet build DeclarationTVA.slnx` — **0 erreur** (7 warnings, tous préexistants).
- [x] Build front `npx tsc -b` **et** `npx vite build` — **0 erreur**.
- [x] `npx oxlint` — **0 erreur**.
- [x] Tests back non régressés : Core **215/215**, Orchestration **225/225**, Export.Xml **26/26**,
      Export.Excel **3/3** ; les 2 seuls échecs de la solution sont **préexistants** (chaînes de
      connexion codées en dur vers des instances SQL inexistantes), fichiers non touchés.
- [x] **Écran 1 (liste)** livré : CRUD, ouverture fiche, colonnes statut/période/nb lignes/dépôt.
- [x] **Écran 2 (fiche)** livré : création exercice/type/trimestre/libellé, clôture/déclôture, dépôt
      manuel, déclenchement génération TASK-133, affichage des lignes intégrées (+ retrait de ligne,
      nécessaire pour pouvoir supprimer une déclaration).
- [x] **Écran 3 (popup de sélection)** livré : période = bornes de la déclaration parente,
      **jamais** une plage libre ; intégration manuelle multi-sélection.
- [x] **Écran 4 (contrôle)** livré : visibilité/reporting pur, **aucun chemin d'intégration**
      (vérifié par assertion e2e ET par absence de tout appel d'intégration dans le fichier) ; période
      raisonnée exercice/type/trimestre calculée comme la création ; badge + action de saisie manuelle.
- [x] **Critère TASK « aucun filtre de date libre visible nulle part dans ce périmètre »** — garanti
      **structurellement** : aucun endpoint n'accepte de borne de date (vérifié en réel avec des
      paramètres parasites ignorés), et 4 assertions e2e comptent 0 champ de date sur les écrans/popup.
- [x] **Critère TASK « parcours complet testé de bout en bout (création à dépôt), y compris le cas de
      blocage IF/ICE avec message explicite »** — couvert (a) par l'e2e Playwright en 9 étapes **et**
      (b) **rejoué réellement via HTTP contre la base réelle**, y compris le blocage sur un fournisseur
      réellement non conforme (`4411INWI`) avec `DDP_IsGeneretedFile` restant à 0.
- [x] Reprise manuelle « date de mise en route » exposée (exigence VERIFY TASK-131 §9 n°3 /
      TASK-132 §9 n°2), **avec l'explication du blocage**, depuis 4 points d'entrée. Choix
      « modale partagée vs écran dédié » documenté (§1.8).
- [x] Contrôle d'autorisation par société implémenté (point laissé par VERIFY TASK-132 §9 n°4) :
      `EstSocieteAutorisee` sur **tous** les endpoints, vérification via `SocieteId` de l'entête sur
      les routes `{ddpId}`. Vérifié en réel (401 sans jeton, 400 sur `soId=0`, 404 sur `ddpId` inconnu).
- [x] Claim JWT `UT_Id` propagé sur **toutes** les écritures ; **401 explicite** si absent, jamais un
      `0` « utilisateur inconnu ». Vérifié en base (`UT_Id = UT_IdModif = 1`).
- [x] **Aucune règle métier dupliquée** : les transitions affichées sont obtenues en interrogeant les
      gardes PURES de TASK-132 ; les bornes de période viennent de `CalculerPeriode` ; le verdict IF/ICE
      et ses libellés viennent de TASK-132.
- [x] **Aucune validation de longueur IF/ICE côté front** (point ouvert PO/fiscaliste non tranché par
      le worker) — le message et les fournisseurs fautifs du back sont affichés tels quels.
- [x] Composants génériques réutilisés (`ExcelFilter`, `ColumnSelector`, `useColumnPrefs`), style et
      densité alignés sur `ConventionsDelaiPaiementPanel.tsx` (TASK-130) — **aucune duplication**.
- [x] Aucune modification de schéma, aucune table winform altérée (§7).
- [x] Pas de SQL hors couche repository ; le contrôleur ne parle qu'aux services.
- [x] Périmètre STRICT respecté : **aucun branchement au menu** (TASK-136 — `App.tsx` n'est pas touché,
      les nouveaux composants ne sont importés que par le harnais de test), aucune modification de
      l'écran conventions (TASK-130) ni de la mesure du délai (TASK-135), aucun refactor.
- [x] Données de test en base réelle **intégralement nettoyées** (4 tables à 0 ligne, vérifié
      indépendamment) ; fichiers XML/ZIP générés supprimés.
- [x] Aucun fichier d'un autre fil de travail modifié ni stagé (`git diff --cached` relu).
- [ ] **Test manuel dans l'application réelle par un utilisateur** : NON fait — les écrans ne sont pas
      atteignables depuis le menu (TASK-136, hors périmètre). Ils ont été exercés (a) dans Chromium via
      le harnais et (b) endpoint par endpoint contre la base réelle, mais **jamais par un humain dans
      l'app assemblée**.

---

## 9. Décisions worker documentées (pas d'exigence PO explicite — à confirmer)

1. **Le contrôleur HTTP fait partie de ce livrable** alors que le texte de la TASK dit « front ».
   Justifié par les 3 VERIFY amont qui le laissent explicitement à TASK-134 ; sans lui aucun écran
   n'existe. Même précédent que TASK-130 (§0).
2. **`Actions` (transitions autorisées) calculées côté serveur en interrogeant les gardes TASK-132.**
   Alternative rejetée : réimplémenter la logique de boutons en TypeScript — c'eût été une **seconde
   source de vérité** sur le cycle de vie (interdit, ARCHITECTURE §5). Le mécanisme retenu appelle la
   garde et observe si elle lève : légèrement inhabituel (flux de contrôle par exception) mais garantit
   qu'un durcissement futur d'une garde se reflète **automatiquement** dans l'UI. Le serveur revalide
   de toute façon à l'exécution.
3. **Plage d'exercices proposée = 2023 → année courante + 1**, côté front. 2023 vient de
   `SeuilsLegauxDelaiPaiement.DateDebutDeclarationLoi` (2023-07-01, aucune facture antérieure n'est
   déclarable) ; `+1` couvre une déclaration anticipée. Ce n'est **pas** une plage de dates libre mais
   une liste d'exercices ; le serveur valide de toute façon l'exercice
   (`CalculerPeriode` lève un message métier explicite hors bornes). **À confirmer PO** si un client a
   besoin d'exercices plus anciens (ils ne produiraient aucune ligne).
4. **Repli front `Trimestrielle`** quand `SO_TypeDecDP` n'est ni 1 ni 2, ou quand la lecture du
   paramétrage échoue. Choix : le type le plus fréquent au Maroc, **visible et modifiable** par
   l'utilisateur, plutôt qu'un formulaire sans valeur. Le serveur ne fabrique **aucun** défaut.
5. **`FournisseursFautifs` joint à la réponse 409 de `/generation`** en relisant le contrôle informatif.
   Alternative rejetée : parser le message texte côté front (fragile, et duplication de la mise en
   forme des motifs). Coût : une lecture `F_COMPTET` supplémentaire **uniquement dans le cas d'un
   refus**.
6. **Écran de contrôle : une seule grille pour les candidates ET les lignes bloquées**, avec une
   colonne « Statut » explicite (badge). Un écran de contrôle ne doit rien masquer ; la popup de
   sélection, elle, sépare physiquement les deux listes parce que l'une est cochable et l'autre pas.
7. **Modale partagée pour la date de mise en route** plutôt qu'un écran de paramétrage (§1.8) —
   décision demandée explicitement par le prompt, justifiée par l'exclusion du branchement au menu.
8. **Aucun test unitaire neuf.** Le livrable n'introduit aucune règle métier testable hors base : le
   contrôleur est un passe-plat et les DTO des projections. Ajouter des tests d'orchestration pour un
   passe-plat aurait testé le framework MVC, pas du métier. La couverture est l'e2e (§5.2) + le rejeu
   HTTP réel (§6). **Signalé plutôt que passé sous silence** : si l'architecte veut des tests
   d'intégration `WebApplicationFactory` sur les codes HTTP, c'est un ajout séparé (aucun projet de ce
   type n'existe dans le dépôt aujourd'hui).

---

## 10. Reste à valider (NON couvert par ce VERIFY)

1. **⚠ DÉPENDANCE INTER-FILS À TRAITER AVANT MERGE — `Declaration.API/Program.cs` non commité.**
   Le contrôleur livré injecte `IDeclarationDelaiPaiementGenerationService`, dont l'inscription DI
   (`builder.Services.AddScoped<IDeclarationDelaiPaiementGenerationService, DeclarationDelaiPaiementGenerationService>()`)
   est **présente dans l'arbre de travail mais absente du dépôt** : le commit TASK-133 (`dac9360`) ne
   l'a pas incluse, et le VERIFY TASK-133 classe `Program.cs` comme « appartenant au correctif TVA ».
   Le prompt de cette session m'interdisant de toucher/commiter `Program.cs`, **je ne l'ai pas
   commité**. Conséquence : le commit TASK-134 seul, appliqué sur un arbre propre, provoquerait une
   **erreur de résolution DI au démarrage** sur le nouveau contrôleur. **Rien n'est cassé
   localement** (la ligne est là, builds et rejeu réel OK). **Action attendue de l'architecte** :
   faire commiter ce hunk de 7 lignes par le fil qui l'a produit (TASK-133 — ce n'est **pas** du TVA),
   ou m'autoriser explicitement à le commiter. **Point à traiter avant de considérer TASK-134
   déployable.**
2. **La base de dev n'était pas à jour du script de schéma — à signaler au PO.**
   `DM_PARAM_DELAIPAIEMENT_SOCIETE` et `DM_REPRISE_DELAIPAIEMENT` (TASK-128) étaient **absentes** de
   `GR_EMA_DISTRIBUTION`, alors que les VERIFY TASK-131/132 les décrivaient présentes avec des données.
   Sans elles, **tout l'écran DDP tombe en erreur 500** dès le premier appel de sélection. Recréées
   depuis le DDL de `DeclarationTVA.sql` §1i. **À vérifier avant toute installation client** : le
   script de schéma doit être appliqué, sinon le module DDP est inutilisable. (Constat lié : les
   compteurs d'identité `RT_DECLARATIONDELAISPAIEMENTLG` repartaient de ~1650, cohérent avec la trace
   d'historique supprimé déjà signalée en VERIFY TASK-132 §9 n°6.)
3. **Aucun test manuel humain dans l'application assemblée** (checklist §8, dernière ligne). Les écrans
   ne sont pas atteignables depuis le menu : c'est **TASK-136**. Une passe de recette utilisateur reste
   à faire après TASK-136 — notamment la lisibilité des libellés métier français (« Déjà déclaré au »,
   « Constaté au », libellés de bucket), que je ne peux pas auto-valider.
4. **Longueur exigée pour l'IF/ICE : toujours un point ouvert PO/fiscaliste** (VERIFY TASK-132 §8 n°9 /
   §9 n°1). **Rien n'a été tranché ici** et rien ne sera à changer côté front si le PO assouplit la
   règle (aucune longueur n'est codée dans le front). Impact concret **mesuré à nouveau sur données
   réelles** dans cette session : `4411INWI`, `4411ORANGE`, `4411ASSU`, `4411AUTOHALL`… ont IF **et**
   ICE vides dans `F_COMPTET` ⇒ une part importante du parc bloquera à l'export DDP. **À trancher avant
   mise en production.**
5. **Volumétrie de l'écran de contrôle non éprouvée.** L'écran affiche la sélection **complète** sans
   pagination (491 lignes en T1, **1 125 en annuelle 2026** sur la base réelle — filtre/tri 100 %
   client, comme `ConventionsDelaiPaiementPanel`). Fluide à ce volume, mais **aucun test au-delà** :
   sur un parc de plusieurs dizaines de milliers d'échéances, il faudra une pagination serveur (le
   service TASK-131 renvoie tout en une fois — changement de contrat, hors périmètre). **Signalé, pas
   contourné.**
6. **Colonnes de la popup/de l'écran de contrôle non validées par le PO.** J'ai exposé les colonnes de
   traçabilité du calcul incrémental (`BorneReference`/`BorneActuelle`/bucket/origine du délai) parce
   qu'elles sont indispensables pour comprendre un dépassement, mais le CDC ne les liste pas. À
   confirmer/élaguer avec le PO (`ColumnSelector` permet déjà à l'utilisateur de les masquer, et la
   préférence est persistée).
7. **Suppression d'une ligne intégrée : action livrée sans exigence explicite de la TASK.** Nécessaire
   pour pouvoir supprimer une déclaration (garde TASK-132 : suppression interdite avec des lignes) et
   pour corriger une intégration erronée. **Signalé** au cas où le PO voudrait la restreindre.
8. **Concurrence non testée.** La fenêtre de course résiduelle sur le numéro/la période (VERIFY
   TASK-132 §8 n°1, faute d'index unique autorisé) n'est **pas** refermée par ce livrable et n'a pas
   été éprouvée avec deux utilisateurs simultanés sur l'écran.
9. **Correction IF/ICE dans l'ERP non exercée en écriture.** Le scénario « correction → génération
   réussie » a été prouvé (a) en e2e et (b) en réel **en retirant la ligne fautive** de la déclaration
   — je n'ai **pas** écrit d'IF/ICE dans `F_COMPTET` (table Sage, hors périmètre et hors contrainte
   d'écriture autorisée). La correction réelle se fait dans l'ERP par l'utilisateur.

---

## Verdict

Les **4 écrans** demandés sont livrés, **plus le contrôleur HTTP du domaine DDP qui n'existait pas** et
que les VERIFY TASK-131/132/133 laissaient explicitement à cette TASK (endpoints CRUD/cycle de
vie/sélection/intégration/génération + **contrôle d'autorisation par société** + propagation du claim
`UT_Id`). Builds back et front à **0 erreur**, oxlint 0 erreur, **e2e Playwright 3/3 verts**, et — pour
la première fois sur ce périmètre — **cycle complet rejoué réellement via HTTP contre
`GR_EMA_DISTRIBUTION`** (création → intégration de lignes réelles → clôture → contrôle IF/ICE →
génération → téléchargement du ZIP → dépôt, **plus** le cas bloquant sur un fournisseur réellement non
conforme où `DDP_IsGeneretedFile` reste 0), données nettoyées.

Le critère structurant « **aucun filtre de date libre** » est tenu **structurellement** et non par
convention : aucun endpoint n'accepte de borne de date (prouvé en réel avec des paramètres parasites
ignorés), et l'écran de contrôle comme la popup ne contiennent aucun champ de date (prouvé par
assertions e2e). Le blocage « date de mise en route » qui aurait rendu l'écran vide et
incompréhensible est désormais **expliqué et adressable** depuis 4 points d'entrée.

**Points nécessitant l'architecte / le PO** : (1) **la ligne DI de `Program.cs` non commitée par le fil
TASK-133 — à traiter avant merge**, sinon le contrôleur ne se résout pas au démarrage ; (2) la base de
dev n'était pas à jour du script de schéma (2 tables `DM_*` manquantes) ; (3) la recette humaine, qui
nécessite TASK-136 ; (4) la règle de longueur IF/ICE, toujours ouverte et **de nouveau mesurée comme
massivement bloquante** sur le parc réel.
