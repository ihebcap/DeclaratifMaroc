# TASK-131 Verify — DDP : sélection des lignes hors délai + calcul incrémental anti-double-déclaration

> Implémentation worker. Développement NEUF sur GRF web, AUCUNE réutilisation de DLL/code WinForms
> (seule la SÉMANTIQUE legacy est reproduite, à partir de la lecture du code source).
> **Contrainte schéma respectée : LECTURE SEULE STRICTE — aucune table créée, aucune colonne ajoutée,
> aucun INSERT/UPDATE/DELETE nulle part dans le code livré** (cf. § Preuve de lecture seule).
> Build back `dotnet build DeclarationTVA.slnx` : **0 erreur** (24 warnings, tous préexistants).
> Tests `Declaration.Core.Tests` : **115/115 verts** (dont **26 nouveaux** pour cette TASK).
> **Rejeu contre la base réelle `GR_EMA_DISTRIBUTION` effectué** (1 408 échéances, 1 278 affectations)
> — voir § Vérification base réelle : les 3 anomalies corrigées ont été observées sur données réelles,
> **sauf la scission d'affectation partielle (anomalie n°2), inexistante dans le parc réel** →
> couverte par tests unitaires uniquement (documenté honnêtement en § Reste à valider).

---

## 0. Conformité du fichier TASK (à signaler à l'architecte)

Le fichier `TASKS/DDP-TASK-131-...md` suit le **template DDP** (Contexte / Référence legacy /
Anomalies / Objectif / Périmètre STRICT / Étapes / Livrables / Critères de validation / Risques),
**pas** le template `ARCHITECTURE.md §3` : il n'y a **pas de champ `FILES`** explicite (ni `Status`,
`Priority`, `Risk`, `Module`). Identique aux TASK-127/128/129/130 déjà implémentées et vérifiées
selon le même template — je n'ai donc **pas bloqué** sur ce point, et j'ai dérivé la liste des
fichiers de la section **Périmètre STRICT** (« requête de sélection corrigée, calcul incrémental,
intégration du garde-fou TASK-128, lecture seule stricte sur RT_ECHEANCE/RT_AFFECTATION/
RT_MOUVEMENT/RT_DECLARATIONDELAISPAIEMENTLG »). **Signalé plutôt que passé sous silence** : si
l'architecte veut appliquer strictement `ARCHITECTURE.md §3` au périmètre DDP, les 10 fichiers DDP
sont à compléter (décision hors de ma portée).

---

## 1. Périmètre livré

### 1.1 Calculateur PUR (Declaration.Core) — cœur métier

`SelectionDelaiPaiementCalculator.Selectionner(ParametresSelectionDelaiPaiement)` : hors DB, testable
comme `EcheanceLegaleCalculator` (TASK-127) et `DelaiPaiementBootstrapGuard` (TASK-128). Porte les
**3 cas de figure** du legacy (`LigneControleDelaisPaiementController.GetAll`, l.60-293) avec les
**3 corrections** décidées par le PO le 19/07/2026.

**Seuils légaux en dur, point unique de vérité** (`SeuilsLegauxDelaiPaiement`) : `2023-07-01`,
`2024-12-31`, `10 000` (devise société) — les MÊMES constantes sont passées en paramètres à la
requête SQL (jamais deux littéraux indépendants) **et** réappliquées défensivement par le
calculateur (donc couvertes par test unitaire).

**Buckets produits** (traçabilité explicite sur chaque ligne, principe « aucune ligne silencieuse ») :

| Bucket | Cas | Montant de la ligne | Borne actuelle |
|---|---|---|---|
| `HorsPeriodePartAffectee` | cas 2 | `AF_Montant` | date de règlement/rapprochement (plafonnée à fin de période) |
| `HorsPeriodePartNonAffectee` | cas 1 **corrigé** | `EC_Solde` (part non affectée UNIQUEMENT) | fin de période |
| `DansPeriodePartAffectee` | cas 3 | `AF_Montant` | date déterminante (plafonnée à fin de période) |
| `DansPeriodePartNonAffectee` | cas 3 (solde restant legacy) | `EC_Solde` | fin de période |

### 1.2 Correction n°1 — anti-double-déclaration (l'anomalie majeure)

`Depassement` = **borne actuelle − borne de référence**, jamais « écart depuis l'échéance légale ».

Résolution de la **borne de référence**, par échéance, dans cet ordre :

1. **historique présent** (max `RT_DECLARATIONDELAISPAIEMENT.DDP_DateFin` parmi les lignes
   `RT_DECLARATIONDELAISPAIEMENTLG` de cet `EC_Id`, **toutes déclarations confondues**, y compris
   celles produites par l'ancien applicatif) → `OrigineBorneReference.DerniereDeclaration` ;
2. sinon **échéance légale** (« première déclaration ») → `OrigineBorneReference.EcheanceLegale` ;
3. sinon **reprise manuelle** TASK-128 → `OrigineBorneReference.RepriseManuelle` ;
4. sinon → **`RepriseManuelleRequise`**, aucun chiffre calculé.

**Ligne exclue si borne de référence ≥ borne actuelle** (rien de nouveau à déclarer) — jamais un
`Depassement` nul ou négatif proposé. Critère TASK-131 « `Depassement > 0` uniquement » : garanti par
construction (test d'invariant global + invariant vérifié sur les 498/581 lignes réelles).

Scénario T1/T2 du PO rejoué en test dédié : T1 = 60 j, T2 = **15** (pas 75), et
`60 + 15 = 75 = retard réel total` — assertion explicite dans le test.

### 1.3 Correction n°2 — affectation partielle scindée

Le `// TODO: verifier les affectations (une affectation peut etre dans la periode` (l.110, jamais
résolu) est traité : une échéance hors période produit désormais **une ligne cas 2 par affectation
réglée/rapprochée pendant la période** (montant = `AF_Montant`) **plus** une ligne cas 1 pour le
**seul solde non affecté** (montant = `EC_Solde`). Le legacy émettait une ligne cas 1 sur l'état
global `Etat == NonPaye` (sans montant de portion) **et** une ligne cas 2 pour **toutes** les
affectations de l'échéance, y compris celles rapprochées bien avant la période (neutralisées
ensuite par un `Depassement` négatif). Ici les affectations rapprochées avant `dateDebut` ne
produisent **plus aucune ligne** (test dédié).

### 1.4 Correction n°3 — `Depassement` du cas 1 plus jamais constant

Le legacy calculait `dateFin - Max(dateDebut, echeanceLegale)` alors que ce bucket filtre déjà
`echeanceLegale < dateDebut` : le retard valait **toujours la longueur de la période**. Remplacé par
le calcul incrémental. **Observé sur données réelles** : sur les 7 lignes réelles de ce bucket, le
legacy aurait affiché `89` pour les 7 ; le nouveau calcul produit `204, 211, 281, 295, 309, 323, 349`
(chacune = `2026-03-31 − échéance légale`, vérifiée à la main sur EC_Id=19909 :
`2025-02-15 + 60 j = 2025-04-16` puis `2026-03-31 − 2025-04-16 = 349`).

### 1.5 Garde-fou de mise en route (TASK-128) — branché, jamais contourné

Le garde-fou est appelé **pour chaque échéance** via `DelaiPaiementBootstrapGuard.Resoudre(...)`
(aucune règle réimplémentée). Les lignes bloquées sont **restituées séparément**
(`ResultatSelectionDelaiPaiement.LignesRepriseManuelleRequise`) avec `Depassement = null` et
`BorneReference = null` : visibles pour TASK-134, **non intégrables** par TASK-132.

Affinage assumé (documenté, testé) : une ligne bloquée dont la **borne actuelle n'atteint même pas
l'échéance légale** (facture réglée dans les délais) est **exclue** — on peut l'affirmer sans rien
inventer, et le legacy la filtrait aussi. Effet mesuré sur données réelles : 501 → 498 lignes, soit
exactement le même cardinal que la sélection candidate quand le garde-fou est inactif (preuve que le
garde-fou ne change que le statut/le chiffre, jamais la sélection).

### 1.6 Arbitrage documenté : TASK-131 §Anomalie 1 vs garde-fou TASK-128 implémenté

**Divergence réelle entre les deux TASK, tranchée dans le sens conservateur — à confirmer par le PO.**

- TASK-131 dit : « si une ligne antérieure existe → borne = max `DDP_DateFin` » (sans condition sur
  la date de mise en route).
- Le garde-fou TASK-128 **tel qu'implémenté et documenté** (décision worker n°2 de
  `VERIFY/TASK-128_verify.md`) court-circuite sur `dateMiseEnRoute == null` : société non configurée
  ⇒ calcul automatique désactivé **inconditionnellement, même avec historique**.

Choix retenu : **le garde-fou est appelé en premier et son verdict fait foi**, avec
`aHistoriqueDeclarationLegacy = (historique présent)`. Conséquence : tant qu'une société n'a pas
saisi sa date de mise en route, **aucune ligne ne porte de chiffre** (constaté sur la base réelle :
498 lignes toutes en `RepriseManuelleRequise`). C'est la direction sûre (jamais un chiffre
silencieux), mais **c'est une lecture combinée de deux TASK, pas une consigne explicite** — signalée
ici plutôt qu'arbitrée en silence. Si le PO veut que l'historique prime sur l'absence de
paramétrage, la correction est locale (un `switch` dans le calculateur, aucun changement de contrat).

### 1.7 Orchestration (Declaration.Application) — LECTURE SEULE

`ISelectionDelaiPaiementService` / `SelectionDelaiPaiementService` : 5 lectures, puis délégation
totale au calculateur pur. Aucune logique métier dupliquée.
Retour : `ResultatSelectionDelaiPaiement { Lignes, LignesRepriseManuelleRequise,
DateMiseEnRouteSociete, NombreEcheancesExaminees, bornes de période }`.

### 1.8 Repository (Declaration.Infrastructure) — SELECT uniquement

`SelectionDelaiPaiementRepository` (4 requêtes, Dapper, `CreateGrfConnection`) :

1. **devise société** — `P_SOCIETE.SO_DeviseErpNo` → `P_SOCIETEDEVISE.SD_No` → `DV_Id` (reproduit
   `Societe.GetDefaultDeviseSociete()` → `GetDeviseErp`). **Échec explicite** si introuvable (jamais
   un repli qui élargirait la sélection à des devises étrangères) ;
2. **échéances candidates** — `SO_Id`, `DO_Domaine = 1` (**Achat ; enum `ErpDomaine` INVERSÉ**, même
   constat que `ConventionDelaiPaiementRepository.ToDoDomaineErp`, TASK-129), `EC_Type NOT IN (90,91)`
   (Gain/Perte exclus comme `EcheanceRepository.GetAll` l.215-216), `DE_Id = devise société`,
   `DO_Date >= 2023-07-01`, `(DO_Date > 2024-12-31 OR EC_Montant >= 10000)` ;
3. **affectations + règlements** — `RT_AFFECTATION` ⋈ `RT_MOUVEMENT`, batché par 1 000 `EC_Id`
   (limite SQL Server 2 100 paramètres, même garde que `DeclarationRepository.TamponnerAffectationsAsync`) ;
4. **historique** — `RT_DECLARATIONDELAISPAIEMENTLG` ⋈ `RT_DECLARATIONDELAISPAIEMENT`,
   `MAX(DDP_DateFin) GROUP BY EC_Id`, scopé `D.SO_Id`, **sans filtre de statut** (une ligne intégrée
   dans une déclaration encore `EnCours` compte donc comme déjà déclarée — voir § Décisions).

### 1.9 Ajouts ADDITIFS sur TASK-127/128 (anti N+1, aucun changement de comportement)

| Fichier | Ajout | Pourquoi |
|---|---|---|
| `DelaiPaiementService.cs` (TASK-127) | `ChargerContexteAsync(soId, domaine)` → `ContexteDelaiPaiement` | `ResoudreDelaiAsync` fait **3 requêtes SQL par échéance** ⇒ 4 224 requêtes pour 1 408 échéances. Le contexte charge le référentiel **une fois** et délègue au MÊME `EcheanceLegaleCalculator` (zéro logique dupliquée). |
| `IRepriseDelaiPaiementRepository.cs` + `DelaiPaiementBootstrapRepository.cs` + `DelaiPaiementBootstrapService.cs` (TASK-128) | `GetAllAsync/GetToutesReprisesAsync(soId)` | `GetAsync(soId, ecId)` par échéance = N+1. Alternative rejetée : dupliquer le SQL `DM_REPRISE_DELAIPAIEMENT` dans un second repository (ARCHITECTURE §5). |

Les 4 diffs sont **strictement additifs** (`git diff --stat` : 65 insertions, **0 suppression**).

---

## 2. Fichiers créés

- `Declaration.Core/SelectionDelaiPaiementCalculator.cs` — calculateur PUR + `SeuilsLegauxDelaiPaiement`
  + types d'E/S (`EcheanceDelaiPaiement`, `AffectationDelaiPaiement`, `LigneSelectionDelaiPaiement`,
  `ParametresSelectionDelaiPaiement`) + enums (`TypeReglementDelaiPaiement`,
  `EtatEcheanceDelaiPaiement`, `BucketDelaiPaiement`, `StatutLigneDelaiPaiement`,
  `OrigineBorneReference`).
- `Declaration.Core/ContexteDelaiPaiement.cs` — référentiel de délai chargé une fois (anti N+1).
- `Declaration.Application/Interfaces/ISelectionDelaiPaiementRepository.cs` — contrat LECTURE SEULE.
- `Declaration.Application/Services/SelectionDelaiPaiementService.cs` — `ISelectionDelaiPaiementService`
  + impl + `ResultatSelectionDelaiPaiement`.
- `Declaration.Infrastructure/Repositories/SelectionDelaiPaiementRepository.cs` — 4 `SELECT`.
- `Declaration.Core.Tests/SelectionDelaiPaiementCalculatorTests.cs` — 26 tests.

## 3. Fichiers modifiés

- `Declaration.Application/Services/DelaiPaiementService.cs` — +`ChargerContexteAsync` (additif).
- `Declaration.Application/Services/DelaiPaiementBootstrapService.cs` — +`GetToutesReprisesAsync` (additif).
- `Declaration.Application/Interfaces/IRepriseDelaiPaiementRepository.cs` — +`GetAllAsync` (additif).
- `Declaration.Infrastructure/Repositories/DelaiPaiementBootstrapRepository.cs` — +`GetAllAsync` (additif).
- `Declaration.API/Program.cs` — **uniquement** 2 `AddScoped` TASK-131 + commentaire
  (`git diff` : 8 insertions, 0 suppression, aucun autre hunk).
- `TASKS/DDP-TASK-131-...md` → `IN_PROGRESS/DDP-TASK-131-...md` (déplacement demandé).

**Aucun fichier hors périmètre touché.** `DeclarationTVA.sql` **non modifié**. Aucun controller API
ajouté (l'UI est TASK-134, le cycle de vie TASK-132) — le service est néanmoins **enregistré en DI**
pour que TASK-132/134 le consomment sans échec DI latent.

### Cohabitation avec les sessions parallèles

Des modifications non committées d'**autres agents** sont présentes dans l'arbre
(`ConventionDelaiPaiementService.cs`, `IConventionDelaiPaiementTiersRepository.cs`,
`ConventionDelaiPaiementRepository.cs`, `ConventionsDelaiPaiementController.cs`,
`ConventionDelaiPaiementDto.cs`, `ConventionDelaiPaiementQueryModels.cs`, front `task130*`,
`CHANGELOG.md`/`DONE.md`/`TODO.md`, `VERIFY/TASK-006|007|024`, `.env.development`, `api.ts`,
`vite.config.ts`, `DOCS/PROMPT-WORKER-*`). **Aucun de ces fichiers n'a été modifié ni stagé par cette
session** — `git diff --cached` relu fichier par fichier avant commit.

---

## 4. Couverture de tests (26 nouveaux, hors DB, `Declaration.Core.Tests` 115/115)

**Anomalie n°1 (anti-double-déclaration) — 4 tests**
- Scénario T1/T2 du PO : 60 puis **15** ; assertion explicite `60 + 15 = 75 = retard réel total`.
- Non-régression : la borne utilisée en T2 est bien l'historique, `Depassement ≠ 75`.
- Borne déclarée = borne actuelle → ligne **exclue** (pas de `Depassement` nul).
- Borne déclarée postérieure à la période → ligne **exclue** (pas de `Depassement` négatif).

**Anomalie n°2 (scission part affectée / part non affectée) — 4 tests**
- Facture 100 000 hors période, 40 000 rapprochés dans la période, 60 000 restants → **2 lignes** :
  cas 2 (montant 40 000, borne = date de rapprochement) + cas 1 (montant **60 000 = solde seul**,
  borne = fin de période), avec deux `Depassement` **différents**.
- Affectation rapprochée AVANT la période → aucune ligne (seul le solde en produit une).
- Chèque affecté mais **non rapproché** → borne = fin de période.
- Chèque rapproché **après** la période → borne plafonnée à la fin de période.

**Anomalie n°3 (retard constant) — 1 test**
- Deux factures d'anciennetés différentes → deux `Depassement` **différents**, et **aucun** égal à la
  longueur de la période (ce que le legacy produisait pour les deux).

**Garde-fou TASK-128 — 6 tests**
- Antérieure sans reprise → `RepriseManuelleRequise`, `Depassement = null`, `BorneReference = null`.
- Antérieure **avec** reprise → candidate, borne = date de reprise, `Depassement` calculé depuis elle.
- Antérieure **avec historique** → candidate, borne = historique.
- Société **sans** date de mise en route → aucune ligne chiffrée (même avec historique).
- Ligne bloquée **sans aucun retard réel** → non signalée (bruit inutile évité).
- Ligne bloquée **avec** retard réel → reste visible (jamais de suppression silencieuse).

**Cas 3 / bornes — 5 tests** : espèce payée après / avant l'échéance légale, règlement non
comptabilisé dans les délais (inclus par le legacy puis filtré par `Depassement > 0` — reproduit),
solde restant non affecté, échéance légale postérieure à la période.

**Seuils légaux — 3 tests** : antérieur au 2023-07-01 jamais éligible ; seuil 10 000 applicable
**uniquement** jusqu'au 2024-12-31 inclus (bornes exactes) ; échéance sous seuil exclue par le
calculateur.

**Gardes / invariants — 3 tests** : échéance sans échéance légale résolue → **erreur explicite**
(aucune échéance ignorée silencieusement) ; période invalide → erreur ; invariant global
« candidate ⇒ `Depassement > 0` et `BorneReference != null` / bloquée ⇒ les deux `null` ».

---

## 5. Vérification base réelle (`GR_EMA_DISTRIBUTION`, `127.0.0.1`)

> Le nom d'hôte `DESKTOP-5BFKKEP` de `connections.json` **n'est pas résolvable** depuis cette session
> (`sqlcmd` : erreur 53 / timeout). `127.0.0.1` fonctionne et pointe la même instance — même constat
> que `VERIFY/TASK-127_verify.md`. **Aucune écriture** n'a été faite en base pendant ces vérifications.

Harness temporaire (projet console dans le scratchpad, **hors dépôt, non committé**) instanciant la
chaîne réelle `SelectionDelaiPaiementRepository` → `DelaiPaiementService` → `DelaiPaiementBootstrapService`
→ `SelectionDelaiPaiementService`.

**Ce qui a été RÉELLEMENT vérifié en base :**

| Point | Résultat réel |
|---|---|
| Devise société résolue | `DV_Id = 4` (`SO_DeviseErpNo=1` → `SD_No=1`) — conforme |
| Échéances candidates (seuils légaux + devise) | **1 408** lues, mapping Dapper/enums OK (`Etat`, montants, tiers, dates) |
| Affectations + règlements | **1 278** ; par type : Virement 157 (130 rapprochés), Espèce 56 (0 rapproché), Chèque 719 (448), Traite 346 (168) |
| `MV_Point`/`MV_PointDate` | **Piège réel confirmé** : des lignes `MV_Point = 0` portent un `MV_PointDate` **futur** (jusqu'au 2026-12-06) → seul `MV_Point <> 0` fait foi (convention retenue, cf. § Décisions n°2) |
| Historique `RT_DECLARATIONDELAISPAIEMENTLG` | **0 ligne** (table vide, comme `RT_DECLARATIONDELAISPAIEMENT`) |
| `DM_PARAM_DELAIPAIEMENT_SOCIETE` / `DM_REPRISE_DELAIPAIEMENT` | présentes (TASK-128), **0 ligne** |
| Référentiel délai | 0 convention, **16 jours de repos**, défaut **60 j** |
| Sélection réelle T1 2026-01-01..03-31, société **non configurée** | 1 408 examinées → **0 candidate, 498 `RepriseManuelleRequise`** (`Depassement = null` sur les 498) ⇒ **critère TASK-131 « aucune ligne antérieure à la mise en route sans reprise n'apparaît avec un chiffre calculé » vérifié sur données réelles** |
| Sélection réelle avec mise en route injectée **dans le calculateur pur** (aucune écriture) | **498 lignes** — buckets : `DansPeriodePartAffectee` 358 (dep 1..89), `DansPeriodePartNonAffectee` 6 (4..60), `HorsPeriodePartNonAffectee` **7 (204..349)**, `HorsPeriodePartAffectee` 127 (30..358) |
| **Anomalie n°3 sur données réelles** | les 7 lignes du bucket cas 1 auraient valu **89 j (= longueur de période) pour les 7** avec le legacy ; elles valent 204/211/281/295/309/323/349 — recalculé à la main sur EC_Id=19909 (`DO_Date 2025-02-15 +60 j → EL 2025-04-16`, `2026-03-31 − 2025-04-16 = 349`) ✔ |
| **Anomalie n°1 sur données réelles** | T2 2026-04-01..06-30 : **581 lignes**, somme des `Depassement` = **32 624 SANS** anti-double-déclaration vs **24 676 AVEC** l'historique T1 ⇒ **7 948 jours de double-déclaration évités** sur un seul trimestre |
| Invariant PO « cumul déclaré ≤ retard réel total » | **OK sur les 581 lignes réelles** de T2 (`max(dep T1 par EC) + dep T2 ≤ borne actuelle − échéance légale`) |
| Idempotence de période | T2 rejouée avec un historique borné à sa propre fin de période → **0 ligne** pour les EC historisés (rien de nouveau à déclarer) |
| Invariants de sortie | `Depassement > 0` sur 100 % des lignes candidates ; `Depassement = null` sur 100 % des lignes bloquées |

**Ce qui n'a PAS pu être vérifié en base réelle** (voir § Reste à valider) : la **scission
d'affectation partielle** (anomalie n°2) — requête de contrôle dédiée exécutée :
`0` échéance achat/devise société/≥2023-07-01 avec `EC_Etat = 0` **et** au moins une affectation.
Le parc réel ne contient **aucune facture partiellement payée** : ce cas est couvert par 4 tests
unitaires uniquement.

---

## 6. Preuve de lecture seule stricte

- Les 4 requêtes de `SelectionDelaiPaiementRepository` sont des `SELECT` ; la 5ᵉ lecture
  (`GetAllAsync` sur `DM_REPRISE_DELAIPAIEMENT`) est un `SELECT`.
- `grep -nE "INSERT|UPDATE|DELETE|MERGE|ALTER TABLE|CREATE TABLE|DROP|TRUNCATE"` sur les 6 fichiers
  créés : **2 occurrences, toutes deux dans un commentaire de documentation** (« AUCUN
  INSERT/UPDATE/DELETE… ») — **aucune instruction SQL d'écriture**.
- `DeclarationTVA.sql` non modifié → aucune table créée, aucune colonne ajoutée.
- Aucune table possédée par `apbs-gr_winform` (`P_SOCIETE`, `P_SOCIETEDEVISE`, `RT_*`) n'est écrite
  ni altérée. **Aucun besoin de colonne/contrainte supplémentaire n'a été rencontré** (voir toutefois
  la limite structurelle documentée en § Décisions n°1, qui découle directement de cette contrainte).
- Requêtes 100 % **paramétrées** (Dapper) ; les seules valeurs interpolées en C# sont des constantes
  privées non issues d'une entrée utilisateur (sentinel `'17530101'`) — ARCHITECTURE §5 respecté.
- Aucun SQL hors couche repository (Core et Application n'en contiennent aucun).
- Aucun secret codé en dur, aucun bypass sécurité (pas de nouvel endpoint dans ce périmètre).

---

## 7. Checklist

- [x] Build back `dotnet build DeclarationTVA.slnx` — **0 erreur** (24 warnings, tous préexistants,
      aucun dans les fichiers TASK-131).
- [x] `dotnet test Declaration.Core.Tests` — **115/115 verts**, dont 26 nouveaux ; aucune régression.
- [x] `Declaration.Orchestration.Tests` 205/205, `Declaration.Export.Xml.Tests` 13/13,
      `Declaration.Export.Excel.Tests` 3/3 — verts.
- [x] Les 2 seuls tests rouges de la solution sont **préexistants et sans rapport** :
      `Declaration.Selection.Tests.IntegrationRegressionTests` (chaîne codée en dur
      `Server=.;Integrated Security=True`) et `Declaration.Controle.Tests.ComparateurTests`
      (`Server=.\sql2022`) — instances SQL inexistantes sur ce poste ; **aucun fichier de ces deux
      projets n'a été touché par TASK-131**.
- [x] LECTURE SEULE STRICTE : uniquement des `SELECT` (cf. § 6).
- [x] Aucune table créée / aucune modification de schéma / aucune table winform altérée.
- [x] Aucune réutilisation de DLL/code WinForms — code neuf, seule la sémantique legacy est reproduite.
- [x] Seuils légaux `2023-07-01` / `2024-12-31` / `10 000` reproduits à l'identique, point unique de
      vérité partagé SQL ↔ calculateur, couverts par tests.
- [x] Anomalie n°1 corrigée — testée (4 tests) **et** mesurée sur données réelles (7 948 jours de
      double-déclaration évités sur un trimestre).
- [x] Anomalie n°2 corrigée — testée (4 tests). **Non observable sur le parc réel** (0 facture
      partiellement payée) → cf. § Reste à valider n°1.
- [x] Anomalie n°3 corrigée — testée (1 test) **et** observée sur les 7 lignes réelles du bucket.
- [x] Garde-fou TASK-128 branché via `DelaiPaiementBootstrapGuard` (aucune règle réimplémentée),
      6 tests, **et** vérifié sur données réelles (498/498 lignes sans chiffre, société non configurée).
- [x] Critère TASK-131 « aucun `Depassement` cumulatif supérieur au retard réel total » — test dédié
      T1/T2 **+** invariant vérifié sur 581 lignes réelles.
- [x] Critère TASK-131 « `Depassement > 0` uniquement » — par construction + test d'invariant global
      + vérifié sur données réelles.
- [x] Pas de SQL inline hors couche repository, requêtes paramétrées (ARCHITECTURE §5).
- [x] Périmètre STRICT respecté : aucune écriture, aucun cycle de vie déclaration (TASK-132), aucun
      export XML (TASK-133), aucune UI (TASK-134), aucun controller API.
- [x] Diffs sur fichiers TASK-127/128/Program.cs : **additifs uniquement** (0 suppression), vérifiés
      via `git diff --stat`.
- [x] Aucun fichier d'une autre session parallèle stagé (relecture `git diff --cached`).
- [ ] **Endpoint API / bout-en-bout applicatif** : NON applicable/NON fait — aucun controller n'est
      livré (hors périmètre). La chaîne complète a été exercée par un harness console contre la base
      réelle, mais l'API n'a pas été démarrée (aucun consommateur runtime dans ce périmètre).

---

## 8. Décisions worker documentées (pas d'exigence PO explicite — à confirmer)

1. **`DDP_DateFin` comme marqueur « déclaré jusqu'au » : approximation conservatrice structurelle.**
   `RT_DECLARATIONDELAISPAIEMENTLG` n'a **aucune colonne** « borne déclarée » et la contrainte de
   schéma interdit d'en ajouter une. La borne de référence est donc `MAX(DDP_DateFin)`, alors que la
   ligne réellement déclarée pouvait s'arrêter à une **date de règlement antérieure** à la fin de
   période (buckets « part affectée »). Effet : le marqueur **surestime** ce qui a été déclaré ⇒ le
   calcul suivant **sous-déclare** au pire, **jamais ne double-déclare**. Sans impact pratique dans
   ce cas (l'échéance est alors soldée, plus rien à déclarer), mais **c'est une limite à connaître
   pour TASK-132** : si le PO veut une borne exacte par ligne, il faudra une table `DM_*` neuve
   (hors périmètre TASK-131, à arbitrer).
2. **`MV_Point` seul fait foi pour le rapprochement** (et non `MV_PointDate` brut comme le legacy).
   Justification empirique : en base réelle, des mouvements `MV_Point = 0` portent un `MV_PointDate`
   **futur** (jusqu'au 2026-12-06) — le legacy aurait interprété ces dates comme des pointages.
   Convention retenue : `EstRapproche = (MV_Point <> 0)`, `DateRapprochement = MV_PointDate` purgé du
   sentinel `1753-01-01`, espèce/autre (`MV_Type` 0/4) auto-rapprochées sur `MV_Date` — **exactement
   la convention déjà en place côté TVA/TASK-135** (`DeclarationRepository`), conformément à la
   décision PO du 19/07/2026 (« réutilise le mécanisme déjà en place côté TVA »).
   Conséquence assumée : une **pièce affectée mais non rapprochée** est traitée comme « pas encore
   réglée » ⇒ borne actuelle = fin de période (le retard court toujours), au lieu de dépendre d'un
   `MV_PointDate` résiduel. Aligné sur le cas 3 legacy (`!IsPointe → dateFin`).
3. **Aucun filtre de statut sur l'historique.** `MAX(DDP_DateFin)` est pris **toutes déclarations
   confondues**, y compris `EnCours`. Effet voulu : une ligne déjà intégrée dans la déclaration en
   cours disparaît naturellement de la liste des candidats (borne de référence ≥ borne actuelle) —
   protection anti-double-intégration **dans la même période**, utile à TASK-132. Si le PO veut que
   les lignes de la déclaration courante restent visibles/re-sélectionnables, il faudra un paramètre
   d'exclusion `ddpIdCourante` (une ligne à ajouter, signalée plutôt qu'anticipée).
4. **Domaine Achat uniquement** (`DO_Domaine = 1`), comme le legacy
   (`EcheanceFournisseurHelper`/`ErpDomaine.Achat`). Le CDC DDP ne porte que les délais fournisseur.
5. **Périodes bornées par l'appelant.** `SelectionnerAsync(soId, dateDebut, dateFin)` ne lit **pas**
   `RT_DECLARATIONDELAISPAIEMENT` pour retrouver les bornes : le cycle de vie de la déclaration est
   explicitement TASK-132, qui passera `DDP_DateDebut`/`DDP_DateFin`. Une période invalide lève une
   erreur explicite (comme le legacy « Période invalide. »).
6. **Trou legacy NON corrigé (hors des 3 anomalies arbitrées) — signalé, pas résolu.** Les états
   `EC_Etat`/`EC_Solde` sont **courants**, pas « à la date de fin de période ». Une facture hors
   période **soldée en espèce après** `dateFin` ne produit donc aucune ligne (ni cas 1 —
   `Etat = TotalementPaye`, ni cas 2 — l'espèce hors période est exclue par la règle legacy), alors
   qu'elle était bien en retard pendant toute la période. Comportement **identique au legacy** ;
   corriger cela demanderait de reconstituer le solde à date (somme des affectations ≤ `dateFin`),
   ce qui dépasse le périmètre STRICT de TASK-131. **À arbitrer par le PO** (impact : sous-déclaration
   sur les périodes rejouées rétroactivement).

---

## 9. Reste à valider (NON couvert par ce VERIFY)

1. **Anomalie n°2 (scission d'affectation partielle) : validée en tests unitaires seulement.**
   Le parc réel `GR_EMA_DISTRIBUTION` ne contient **aucune facture partiellement payée** (requête de
   contrôle : 0 échéance avec `EC_Etat = 0` et au moins une affectation). Un rejeu sur une base
   client comportant de vraies affectations partielles reste **pertinent avant mise en production**.
2. **Calcul incrémental : historique réel jamais exercé.** `RT_DECLARATIONDELAISPAIEMENT` et
   `RT_DECLARATIONDELAISPAIEMENTLG` sont **vides** en base. La correction n°1 a été vérifiée
   (a) par tests unitaires et (b) par un **rejeu T1→T2 sur données réelles avec historique injecté**
   dans le calculateur pur — mais **jamais** avec des lignes réellement présentes dans la table (ce
   qui n'est possible qu'après TASK-132, seule habilitée à écrire).
3. **Arbitrage §1.6 (garde-fou TASK-128 prioritaire sur l'historique) : à confirmer par le PO.**
   Divergence réelle entre le texte de TASK-131 et le garde-fou TASK-128 tel qu'implémenté. Tranché
   dans le sens conservateur ; conséquence concrète immédiate : **tant que la date de mise en route
   d'une société n'est pas saisie, TASK-131 ne renvoie AUCUNE ligne chiffrée** (constaté : 498/498
   lignes en `RepriseManuelleRequise` sur la base réelle). **TASK-134 devra donc impérativement
   exposer la saisie de cette date**, sinon l'écran de contrôle apparaîtra « vide » au client.
4. **Risque PO signalé par la TASK (non résolu, hors portée worker)** : si un client a des
   déclarations passées faites via l'ancien applicatif dans une base **différente** de celle utilisée
   par GRF, `RT_DECLARATIONDELAISPAIEMENTLG` ne contiendra pas cet historique et le garde-fou de mise
   en route (TASK-128) devient la **seule** protection anti-double-déclaration. **À vérifier client
   par client avec le PO avant mise en route réelle.** Ce point est aggravé par la décision n°1
   (`DDP_DateFin` comme unique marqueur disponible sans changement de schéma).
5. **Décision n°6 (trou legacy « état courant vs état à date de fin de période ») : à arbitrer PO.**
   Reproduit à l'identique du legacy, hors des 3 anomalies décidées. Impact : sous-déclaration
   possible sur des périodes rejouées rétroactivement.
6. **Performance non mesurée à l'échelle.** Sur 1 408 échéances / 1 278 affectations, la sélection est
   instantanée (4 requêtes + calcul en mémoire, batché par 1 000 `EC_Id`). Aucun test de charge sur un
   parc de plusieurs dizaines de milliers d'échéances n'a été fait ; la résolution de l'échéance
   légale reste un balayage linéaire des conventions (0 en base aujourd'hui) — à surveiller si un
   client cumule beaucoup de conventions.
7. **Aucun endpoint API livré** (périmètre : UI = TASK-134). Le service est enregistré en DI et sa
   chaîne d'instanciation a été exercée par le harness contre la base réelle, mais l'API n'a pas été
   démarrée dans cette session.

---

## 10. Message explicite pour TASK-132 (consommateur direct)

- Consommer `ISelectionDelaiPaiementService.SelectionnerAsync(soId, DDP_DateDebut, DDP_DateFin)`.
- **N'intégrer QUE `Lignes`** ; `LignesRepriseManuelleRequise` est à afficher (TASK-134) et à
  **refuser à l'intégration** (aucun `Depassement` calculé, par décision PO).
- Persister `EC_Id`, `AF_Id` (nullable — les buckets « part non affectée » n'en ont pas),
  `DDPL_Depassement = Depassement`, `DDPL_EcheanceLegale = EcheanceLegale`.
- **Limite à connaître** (décision n°1) : la borne « déjà déclaré jusqu'au » réutilisée par la période
  suivante sera `DDP_DateFin` de la déclaration, **pas** la `BorneActuelle` réelle de la ligne. Si
  TASK-132 a besoin d'une borne exacte par ligne, cela impose une table `DM_*` neuve → arbitrage PO.
- Une ligne déjà intégrée dans la déclaration **EnCours** disparaît des candidats au rechargement
  (décision n°3) : si l'écran doit permettre de revoir/dé-sélectionner ce qui est déjà intégré, il
  faudra un paramètre d'exclusion de la déclaration courante.

---

## Verdict

Cœur métier livré : calculateur PUR (3 cas de figure + 3 anomalies corrigées + garde-fou TASK-128),
service d'orchestration, repository LECTURE SEULE (4 `SELECT`), 26 tests unitaires. Build 0 erreur,
tests Core 115/115. **Les anomalies n°1 et n°3 ont été mesurées sur données réelles** (7 948 jours de
double-déclaration évités sur un trimestre ; retard du cas 1 passé de « 89 j constant » à 204-349 j
réels). Aucune modification de schéma, aucune écriture.

**Points ouverts nécessitant l'architecte/le PO** : arbitrage §1.6 (garde-fou vs historique — impacte
directement ce que TASK-134 affichera), décisions n°1/n°3/n°6, et les limites de vérification n°1/n°2
(scission partielle et historique réel non observables sur la base actuelle).
