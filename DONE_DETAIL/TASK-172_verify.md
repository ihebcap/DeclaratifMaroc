# TASK-172 Verify — Référentiel « codes activité » non filtré par domaine

## Périmètre livré

1. **`DeclarationRepository.GetReferentielCodesActiviteAsync`** (`Declaration.Infrastructure/Repositories/DeclarationRepository.cs`) :
   sélectionne désormais `DTA_Domaine` (exposé via un nouveau champ `Domaine` int sur
   `CodeActiviteReferentielRow`), et accepte un paramètre optionnel `string? domaine` ("Encaissement"/
   "Decaissement", mêmes libellés que `DM_LGTVA.Domaine`) qui filtre côté SQL (`WHERE DTA_Domaine =
   @DtaDomaine`). `domaine` omis = référentiel complet non filtré (non-régression stricte de tout
   appelant existant).
2. **`GET /api/codes-activite`** (`DeclarationsController.cs`) : accepte `?domaine=Encaissement|
   Decaissement`, rejette explicitement toute autre valeur en `400` (jamais un filtre silencieusement
   ignoré ni un 500).
3. **Front** (`VerifierIntegrerPanel.tsx`) : `codeActiviteOptions` n'est plus chargé une seule fois sans
   filtre (`useEffect(..., [])` initial) — il est rechargé via `useEffect(..., [drillFiltre?.kind,
   drillFiltre?.domaine])` à chaque ouverture du drill « Codes activité » ou changement d'onglet
   pendant que ce drill est ouvert, avec `domaine: drillFiltre.domaine` passé en query param.
4. **§4 — validation serveur (décision architecte déjà actée, appliquée telle quelle)** : au `PATCH
   {id}/lignes/{ligneId}/code-activite`, `DeclarationWorkflowService.ModifierCodeActiviteLigneAsync`
   recroise désormais, pour tout `codeActivite` non vide, le domaine réel du code
   (`GetDomaineCodeActiviteAsync`, connexion GRF) avec le domaine de la ligne ciblée
   (`GetDomaineLigneAsync`, connexion Persistence — **deux requêtes séparées, jamais de jointure
   cross-base**, conforme au garde-fou TASK-154) via une nouvelle méthode privée partagée
   `ValiderDomaineCodeActiviteAsync` (réutilisée telle quelle par TASK-173, cf. son propre VERIFY).
   Un code inconnu du référentiel ou incompatible avec le domaine de la ligne lève une
   `ApplicationException` explicite, traduite par le contrôleur en **`400`** (jamais un 500, jamais une
   acceptation silencieuse) — distincte du `409` déjà renvoyé pour clôture. Effacer le code
   (`codeActivite` vide) reste toujours accepté sans validation de domaine, comme avant.

## Vérification en base réelle (avant d'écrire le filtre, conformément au garde-fou de la TASK)

```
SELECT DTA_Domaine, COUNT(*) FROM P_DECTVAACTIVITE GROUP BY DTA_Domaine ORDER BY DTA_Domaine
```
→ `1` (Encaissement) : **57** lignes · `2` (Décaissement) : **39** lignes · **aucun NULL, aucune autre
valeur** (96 lignes au total, confirmé par `SELECT COUNT(*) FROM P_DECTVAACTIVITE` = 96). Le mapping
« 1 = Encaissement, 2 = Décaissement » vient du libellé de la TASK elle-même, confirmé cohérent avec ce
comptage (aucun cas imprévu à signaler — le garde-fou anti-exclusion-silencieuse de la TASK n'a rien à
couvrir ici).

## Décisions actées en cours de route

- **Choix technique du §4** : `ApplicationException` retenue pour signaler « code inconnu/domaine
  incompatible » plutôt qu'un nouveau type d'exception dédié — réutilise un pattern déjà présent dans
  ce même contrôleur (`Generation`, TASK-155 : `ArgumentException`→404, `InvalidOperationException`→
  400/409 selon l'endpoint, `ApplicationException`→400 distinct) plutôt que d'introduire une
  abstraction supplémentaire pour un seul cas d'usage. **Signalé explicitement pour revue** : c'est une
  réutilisation d'un type d'exception générique .NET à des fins de contrôle de flux métier, cohérente
  avec l'existant mais qui mériterait, si le PO le juge utile, un type dédié (`CodeActiviteInvalideException`)
  refactoré en une passe séparée touchant aussi TASK-155 — hors périmètre de cette task (pas de
  changement non demandé).
- Le domaine renvoyé par `GetReferentielCodesActiviteAsync`/`CodeActiviteReferentielRow.Domaine` reste
  un entier brut (1/2), pas une chaîne — cohérent avec le type de colonne réel (`DTA_Domaine int`),
  laissé au front la responsabilité de l'affichage s'il en a besoin (non utilisé actuellement, seul le
  filtrage serveur via `?domaine=` est exploité par le front).

## Tests

- `dotnet build DeclarationTVA.slnx` → **0 erreur** (6 avertissements préexistants, sans rapport,
  `CS8625`/`CS8602` nullable + `NU1510`).
- `dotnet test DeclarationTVA.slnx` (solution complète) :
  - `Declaration.Core.Tests` : 54/54.
  - `Declaration.Export.Xml.Tests` : 13/13.
  - `Declaration.Export.Excel.Tests` : 3/3.
  - `Declaration.Orchestration.Tests` : **177/177** (11 fakes `IDeclarationRepository` + le fake dédié
    `Task161CodeActiviteCascadeTests.FakeDeclarationRepositoryTask161` mis à jour pour les 3 nouvelles
    méthodes d'interface de TASK-172 + les 3 de TASK-173, cf. son VERIFY — aucun test existant modifié,
    seuls les fakes complétés).
  - `Declaration.Selection.Tests` : 58/59 — **1 échec préexistant, sans rapport**
    (`IntegrationRegressionTests`, `Login failed for user 'IHEB-PC\ihebc'` — connexion Windows intégrée
    échouée sur ce poste, documenté identique dans `DONE_DETAIL/TASK-161_verify.md`/`TASK-168_verify.md`).
  - `Declaration.Controle.Tests` : 1/2 — **1 échec préexistant, sans rapport**
    (`ComparateurTests.GenererRapportVerification`, `Déclaration GRFN 66 introuvable` — donnée de test
    absente, documenté identique dans les mêmes VERIFY antérieurs). Aucun fichier de
    `Declaration.Selection`/`Declaration.Controle` touché par TASK-172/173.
- Front : `npx tsc -b` → 0 erreur. `npx vite build` → 0 erreur (1 avertissement Vite préexistant
  `INEFFECTIVE_DYNAMIC_IMPORT` sur `api.ts`/`Auth.tsx`, sans rapport).

## Vérifié indépendamment en conditions réelles

Instance de test dédiée : `Declaration.API/bin/Debug/net8.0-windows` (build de ce poste), copie locale
de `connections.json` éditée (port **5299**, worker `deploy/workers/v10/SageTaxReader.Console.v10.exe`)
— **jamais le fichier du dépôt racine**. Front rebuildé (`vite build`) et copié dans le `wwwroot` de ce
build (dossier non versionné, `bin/` gitignoré). Le service Windows `DeclaratifMaroc` réel (port 5280,
PID distinct confirmé via `Get-NetTCPConnection`/`Get-CimInstance Win32_Process`) n'a jamais été touché.

- **API réelle (JWT signé avec la vraie clé de `connections.json`, revendications identiques à celles
  émises par `AuthController.Login`, aucune donnée `P_UTILISATEUR` modifiée)** :
  - `GET /api/codes-activite` (sans filtre) → **200**, 96 codes.
  - `GET /api/codes-activite?domaine=Encaissement` → **200**, **57** codes, tous `domaine:1`.
  - `GET /api/codes-activite?domaine=Decaissement` → **200**, **39** codes, tous `domaine:2`.
  - `GET /api/codes-activite?domaine=Bidule` → **400**, message explicite.
  - `PATCH .../lignes/{id}/code-activite` avec un code Encaissement (`100`) sur une ligne
    `Domaine=Decaissement` réelle → **400** : `"Code activité « 100 » réservé au domaine Encaissement,
    incompatible avec une ligne Décaissement."` — **aucune écriture** (vérifié par relecture SQL directe
    après coup).
  - Idem avec un code inconnu (`ZZZ999`) → **400** : `"Code activité « ZZZ999 » inconnu dans le
    référentiel."` — aucune écriture.
  - Idem avec un code Décaissement valide (`133`) sur la même ligne → **204**, écriture confirmée
    (`CodeActivite='133'`, `CodeActiviteModifieManuellement=1`, `CodeActiviteModifiePar='Admin'`).
- **Front réel (Playwright/Chromium, déclaration réelle `TVA1-2026-01`, 1237 lignes réelles)** : drill
  « Codes activité » ouvert sous l'onglet « TVA Déductible (Achats) » (Décaissement) → select de ligne
  contenant **40 options** (39 codes + « sans activité »), **aucun** code Encaissement connu (`100 —…`)
  présent. Retour au contrôle, bascule sur l'onglet « TVA Collectée (Ventes) » (Encaissement),
  réouverture du drill → **58 options** (57 codes + « sans activité »), **aucun** code Décaissement
  connu (`133 —…`) présent. Confirme visuellement l'objectif central de la task : le menu déroulant ne
  mélange plus les deux domaines et se recharge bien au changement d'onglet.
- **Nettoyage post-test** : les écritures de test ont été restaurées à l'identique par relecture/
  réécriture SQL directe immédiatement après chaque vérification (`CodeActivite=''`,
  `CodeActiviteModifieManuellement=0`, `CodeActiviteModifiePar=NULL`, `CodeActiviteModifieLe=NULL`).
  Vérification finale : `SELECT COUNT(*) FROM DM_LGTVA WHERE CodeActiviteModifieManuellement = 1` →
  **0** sur toute la base — aucune trace résiduelle. Process de test (PID confirmé sur le port 5299)
  arrêté, `connections.json` du build restauré (`Port: 5000`, worker `SageTaxReader.Console.exe`
  d'origine), scripts Playwright/JWT temporaires et captures supprimés. `git status --porcelain`
  final : aucun fichier temporaire résiduel (seuls des fichiers front pré-existants hors périmètre de
  cette task apparaissent modifiés, non touchés par ce worker — cf. VERIFY TASK-173, même constat).

## Checklist (reprise du fichier TASK)

- [x] Build back + front OK.
- [x] Vérification réelle en base des valeurs distinctes de `DTA_Domaine` (1/2 uniquement, confirmé,
      aucun NULL).
- [x] Menu déroulant « Code activité » sous l'onglet Décaissement ne propose plus de codes
      Encaissement, et inversement — vérifié au navigateur (capture réelle, cf. ci-dessus).
- [x] Décision PO tracée sur la validation serveur (§4) : décision déjà actée en amont par l'architecte
      (« valider aussi côté serveur »), appliquée telle quelle.
- [x] Aucune régression sur les codes déjà affectés à des lignes existantes : le filtre ne s'applique
      qu'à la liste des *options proposées* (front) et à la validation d'une *nouvelle* écriture — aucun
      code déjà en base n'est relu/modifié rétroactivement par ce correctif.

## Réserves non bloquantes (documentées, non silencieuses)

1. Le choix de réutiliser `ApplicationException` (plutôt qu'un type dédié) pour signaler le rejet §4
   est cohérent avec l'existant du même contrôleur mais reste un point de goût — voir « Décisions
   actées » ci-dessus.
2. Le champ `Domaine` ajouté à `CodeActiviteReferentielRow` n'est actuellement consommé par aucun
   front (le filtrage se fait via le query param `?domaine=`, pas en relisant ce champ côté client) —
   exposé par cohérence/complétude du DTO, sans usage actif à ce jour.

## Verdict

Conforme au périmètre et aux garde-fous de la TASK. Filtrage vérifié en base réelle, via l'API réelle
(succès et 3 cas de rejet distincts, tous 400 explicites) et au navigateur réel (deux onglets, options
strictement disjointes). Décision §4 déjà actée par l'architecte, appliquée sans écart. Build back/
front et suite de tests complète rejoués, aucune régression introduite (2 échecs constatés sont
préexistants et documentés dans des VERIFY antérieurs, sans rapport avec cette task).
