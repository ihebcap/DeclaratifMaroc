# TASK-132 Verify — DDP : cycle de vie de la déclaration + contrôle IF/ICE bloquant

> Implémentation worker. Développement NEUF sur GRF web, AUCUNE réutilisation de DLL/code WinForms
> (seule la SÉMANTIQUE legacy est reproduite, à partir de la lecture du code source).
> **Contrainte schéma respectée : AUCUNE table créée, AUCUNE colonne ajoutée, AUCUN index/contrainte
> posé.** `RT_DECLARATIONDELAISPAIEMENT` / `RT_DECLARATIONDELAISPAIEMENTLG` sont réutilisées TELLES
> QUELLES ; `DeclarationTVA.sql` n'est **pas modifié** (cf. § Preuve de non-modification de schéma).
> Build back `dotnet build DeclarationTVA.slnx` : **0 erreur** (24 warnings, tous préexistants, aucun
> dans les fichiers TASK-132).
> Tests : `Declaration.Core.Tests` **196/196** (dont **81 nouveaux**), `Declaration.Orchestration.Tests`
> **217/217** (dont **12 nouveaux**) → **93 tests neufs, tous verts**.
> **Cycle de vie complet rejoué contre la base réelle `GR_EMA_DISTRIBUTION`** (création → intégration
> de 25 lignes réelles → clôture/déclôture → contrôle IF/ICE conforme → génération → dépôt, PLUS le
> cas bloquant sur 2 fournisseurs réellement non conformes), **données de test intégralement nettoyées**
> (les 4 tables reviennent à 0 ligne, vérifié par requête indépendante après le run).

---

## 0. Conformité du fichier TASK (à signaler à l'architecte)

Comme les TASK-127 à 131, `TASKS/DDP-TASK-132-…md` suit le **template DDP** (Contexte / Référence
legacy / Contrôle bloquant / Objectif / Périmètre STRICT / Étapes / Livrables / Critères de validation
/ Risques), **pas** le template `ARCHITECTURE.md §3` : pas de champ `FILES`, `Status`, `Priority`,
`Risk`, `Module`. **Non bloqué** (identique aux 5 TASK DDP déjà implémentées et validées avec ce
template) ; la liste des fichiers a été dérivée du **Périmètre STRICT** et des **Étapes 1 à 6**.
Champs réellement présents et exploitables : Objectif (Entrée/Traitement/Sortie), Périmètre
(Inclus/Exclu), Étapes, Critères de validation, Risques. **Signalé plutôt que passé sous silence.**

---

## 1. Périmètre livré

### 1.1 Cycle de vie PUR (`Declaration.Core`) — gardes de transition

`DeclarationDelaiPaiementCycleDeVie` : hors DB, testable comme `ConventionDelaiPaiementValidator`
(TASK-129) et `DelaiPaiementBootstrapGuard` (TASK-128). Reproduit
`SocieteManager.Complement.cs:601-969` avec **le même ordre d'évaluation** et **les mêmes messages
français** (tournures d'origine incluses, ex. « Le fichier du déclaration n'est pas généré. ») — un
utilisateur habitué à l'ancien applicatif retrouve exactement les mêmes blocages.

| Transition | Gardes, dans l'ordre legacy | Source legacy |
|---|---|---|
| Modifier libellé | déposée → clôturée | l.686-706 |
| Intégrer / supprimer une ligne | déposée → clôturée | l.891-969 |
| Clôturer | déposée → clôturée → **0 ligne** | l.708-732 |
| Déclôturer | déposée → **fichier généré** → pas clôturée | l.734-758 |
| Générer le fichier | déjà généré → déposée → pas clôturée → 0 ligne | l.790-827 |
| Annuler la génération | pas généré → déposée → pas clôturée → 0 ligne | l.829-866 |
| Déposer | **pas généré** → déjà déposée → pas clôturée → 0 ligne | l.760-788 |
| Supprimer | déposée → clôturée → **contient des lignes** | l.868-889 |

Bornes de période calculées automatiquement (l.617-653, reproduites à l'identique) : annuelle
`01-01 → 12-31`, T1 `01-01 → 03-31`, T2 `04-01 → 06-30`, T3 `07-01 → 09-30`, T4 `10-01 → 12-31`.

**Une seule divergence assumée** : le legacy lève `ApplicationException`, ici
`InvalidOperationException` — convention déjà en place dans le dépôt GRF
(`ConventionDelaiPaiementService`, TASK-129).

### 1.2 Contrôle IF/ICE BLOQUANT et RÉUTILISABLE (`Declaration.Core`)

`ControleIdentiteFiscaleDelaiPaiement.Controler(IEnumerable<IdentiteFiscaleFournisseurDeclare>)` →
`ResultatControleIdentiteFiscaleDelaiPaiement { EstConforme, NombreFournisseursExamines,
NombreLignesExaminees, FournisseursFautifs[], MessageBloquant }`. **Calculateur PUR : aucun couplage
à la génération, aucune écriture, aucune dépendance base** — exactement ce que demande la note de la
TASK (« conçu comme une fonction/service RÉUTILISABLE […] car TASK-133 devra l'appeler avant toute
écriture de fichier »).

**Pourquoi le bloc legacy ne pouvait PAS être simplement décommenté** (constat de lecture de code, à
connaître) : `DeclarationDelaisPaiementFileGenerator.cs:78-91` référence `ligne.TiersIdentifiant`,
`ligne.TiersIce` et une variable `nbLigne` **qui n'existent nulle part** — le modèle
`LigneDeclarationDelaisPaiement` ne porte que `TiersNo`/`TiersCode`/`TiersIntitule`, et le compteur de
la boucle s'appelle `counter`. Les vraies valeurs vivent sur `IErpTiersIce`
(`infoFournisseur.TiersIdentifiant`/`.TiersIce`), rattachées **en mémoire** par `TiersCode`
(générateur l.52 + l.103). Le contrôle a donc été **réécrit**, pas réactivé.

**Différence de fond voulue** (exigence explicite de la TASK) : le legacy validait ligne par ligne
**à l'intérieur** de la boucle d'écriture ⇒ un fichier partiel était déjà sur le disque avant
l'échec. Ici **toutes** les lignes sont validées d'abord, et le verdict complet est retourné.

Motifs explicites, **jamais un « invalide » global** :

| Motif | Déclenchement |
|---|---|
| `FournisseurIntrouvableDansReferentiel` | aucune ligne `F_COMPTET` pour ce code tiers, **ou** échéance sans `CT_Code` (legacy : « Impossible de charger les informations fournisseur […] ») |
| `IdentifiantFiscalAbsent` / `IceAbsent` | NULL, vide ou uniquement des blancs |
| `IdentifiantFiscalLongueurInvalide` / `IceLongueurInvalide` | longueur ≠ 8 / ≠ 15 (la longueur réelle est citée dans le message) |
| `IdentifiantFiscalAvecEspace` / `IceAvecEspace` | contient un espace |

**Aucune règle dupliquée (ARCHITECTURE §5)** : le verdict est aligné sur
`ValidationIdentiteFiscale.EstIfValide` / `EstIceValide` (déjà dans `Declaration.Core`, autorité
unique « 8 / 15 caractères, sans espace »). Les motifs ne servent qu'à **expliquer** le refus, et un
**test d'invariant paramétré (11 cas)** garantit qu'ils ne peuvent pas diverger du verdict.

Exemple de message bloquant **réellement produit sur données réelles** (cf. § 5) :

```
Génération du fichier impossible : 2 fournisseurs ont une identité fiscale invalide (identifiant
fiscal de 8 caractères sans espace, ICE de 15 caractères sans espace). Corrigez la fiche tiers dans
l'ERP puis relancez la génération.
 - [4411INWI] Tiers à créer : identifiant fiscal absent ; ICE absent (1 ligne concernée)
 - [FOUBREL] Tiers à créer : identifiant fiscal absent ; ICE absent (1 ligne concernée)
```

### 1.3 Où vivent réellement l'IF et l'ICE fournisseur (source documentée, comme demandé)

**Ce ne sont PAS des colonnes GRF.** Chaîne complète, vérifiée en code legacy **et** en base réelle :

1. `P_SOCIETE.SO_DecTvaColNameIdentifiantFrs` et `SO_DecTvaColNameIceFrs` contiennent le **NOM** de la
   colonne Sage portant l'IF et l'ICE (legacy `Tresorerie.Dapper/Mapping/SocieteMapping.cs:226` et
   `:232`). Valeurs réelles pour `SO_Id=1` : **`IF`** et **`ICE`**.
2. Ces noms sont injectés dans un `SELECT … FROM F_COMPTET` (legacy
   `Sage.v16.Dapper/ErpTiersIceRepository.cs:27-36` + substitution l.115-134, 10 copies une par
   version Sage) — table de la base **SAGE** (`P_SOCIETE.SO_ErpDb` = `NEW_EMA DISTRIBUTION`), jamais
   de la base GRF.
3. Clé de rattachement : `F_COMPTET.CT_Num` = `RT_ECHEANCE.CT_Code`.
4. Le générateur legacy ne joint **rien** en SQL : il charge tous les tiers une fois
   (`_fournisseurHelper.GetAllInfoFournisseur(true)`) et apparie en mémoire par `TiersCode` — c'est
   précisément **pourquoi** le contrôle IF/ICE ne pouvait pas être écrit contre `ligne.*` et a fini
   commenté.

Implémentation retenue ici : **réutilisation intégrale de l'existant GRF**, aucune mécanique neuve —
`IdentiteFiscaleFournisseurConfig` (TASK-048, lit les 2 noms de colonnes dans `P_SOCIETE`, les valide
par **whitelist `^[A-Za-z0-9_]+$`** puis les quote entre crochets) + connexion Sage résolue
**dynamiquement par `SO_Id`** via `IDbConnectionFactory.GetSageConnectionInfoAsync` (TASK-118) +
**jointure applicative en mémoire** (TASK-154 : `F_COMPTET` n'est plus JAMAIS lu via la connexion
GRF). Un seul `SELECT … WHERE CT_Num IN @Codes`, batché par 500 codes.

Qualité de données réelle mesurée (`NEW_EMA DISTRIBUTION`, `CT_Type = 1` = fournisseurs, 357 fiches) :
**175 IF vides / 168 de longueur 8**, **160 ICE vides / 192 de longueur 15** ⇒ le contrôle bloquant a
un effet massif et **réellement observable** sur ce parc, pas seulement en théorie.

### 1.4 Numérotation `DDP_Numero` (assumée côté serveur — point à connaître)

L'objectif TASK-132 prend en entrée « société, exercice, type, trimestre, libellé » — **pas de
numéro** — or `DDP_Numero` est `nvarchar(30) NOT NULL`. Dans le legacy, le numéro était calculé par le
**contrôleur d'écran** (`ListDeclarationDelaisPaiementController.InitView`) puis passé à
`DeclarationDelaisPaiementCreate`. Il est ici attribué par le **service** (ARCHITECTURE §5 : « logique
métier interdite dans la couche UI »).

`NumerotationDeclarationDelaiPaiement` (PUR) reproduit
`SocieteRepository.GetNumeroPieceCourante` (l.143-312) + `IncrementNumero` (l.543-555) : racine
`SO_DecDPPrefix + [aa] + [MM]`, compteur sur `SO_DecDPNumCount` chiffres, repris du
`MAX(DDP_Numero) LIKE 'racine[0-9]…'`. Paramétrage réel `SO_Id=1` : `DDP` + année + mois + 4 chiffres
⇒ **`DDP26070001`**, confirmé en base réelle.

**Durcissements assumés** là où le legacy produisait silencieusement des numéros cassés (chacun testé) :
compteur à 0 chiffre refusé (le legacy générait alors un numéro non incrémentable, redonnant le même à
chaque fois), suffixe non numérique refusé, **débordement du compteur refusé** (le legacy passait à 5
chiffres, le motif `LIKE` ne matchait plus et la numérotation **repartait à 1 en créant un doublon**),
dépassement des 30 caractères refusé.

### 1.5 Intégration des lignes — consomme TASK-131, ne réimplémente rien

`IntegrerLignesAsync(ddpId, utilisateurId, selection = null)` :

- appelle `ISelectionDelaiPaiementService.SelectionnerAsync(SO_Id, DDP_DateDebut, DDP_DateFin)` — les
  bornes viennent **toujours** de la déclaration, jamais d'une plage libre (assertion dédiée en test) ;
- **n'intègre QUE `Lignes`** ; `LignesRepriseManuelleRequise` est **refusée** à l'intégration et
  **reportée** (`ClesRefuseesRepriseManuelleRequise`) — conforme au message TASK-131 §10 ;
- persiste exactement les 5 colonnes du legacy : `DDP_Id`, `EC_Id`, `AF_Id` (**nullable**, buckets
  « part non affectée »), `DDPL_Depassement = Depassement`, `DDPL_EcheanceLegale = EcheanceLegale` ;
- `selection = null` ⇒ toutes les candidates ; sinon uniquement les clés `(EC_Id, AF_Id)` demandées
  (nécessaire à l'écran TASK-134, sans rien anticiper de son UI) ;
- **compte rendu EXHAUSTIF** `ResultatIntegrationLignesDelaiPaiement` : `NombreCandidates`,
  `NombreIntegrees`, `ClesDejaIntegrees`, `ClesRefuseesRepriseManuelleRequise`,
  `ClesIntrouvablesDansSelection`, `NombreRepriseManuelleRequiseDisponibles`,
  `DateMiseEnRouteSociete`. **Aucune ligne écartée silencieusement** (principe TASK-131 repris) ;
- **invariant vérifié** : une candidate sans `Depassement` lève une erreur explicite plutôt que
  d'écrire un `0` — jamais un chiffre inventé en base.

Deux **corrections d'anomalies legacy** ajoutées ici (documentées, testées) :

1. **Transaction unique** pour tout le lot. Le legacy `LigneControleDelaisPaiementController.IntergerLigne`
   bouclait en appelant `DeclarationDelaisPaiementLigneAjouter` **sans transaction englobante** : un
   échec au milieu laissait une intégration **partielle** en base.
2. **Garde anti-double-intégration** dans une même déclaration (le legacy n'en avait **aucune**, et il
   n'existe aucun index unique — impossible à ajouter, table winform). Les clés déjà présentes sont
   **ignorées et reportées**, jamais réécrites (rejeu vérifié en base réelle : 25 lignes → rejeu →
   toujours 25).

### 1.6 Autorisation de génération — le point d'entrée de TASK-133

Deux méthodes explicitement séparées, pour que TASK-133 n'ait aucune ambiguïté :

- `VerifierGenerationFichierAutoriseeAsync(ddpId)` → garde d'ÉTAT **puis** contrôle IF/ICE ; lève
  `InvalidOperationException(MessageBloquant)` si un fournisseur est fautif. **Aucun effet de bord.**
  → **à appeler AVANT d'écrire le premier octet.**
- `MarquerFichierGenereAsync(ddpId, utilisateurId)` → **re-valide intégralement** (état + IF/ICE) puis
  pose `DDP_IsGeneretedFile`. Défense en profondeur : le flag ne peut jamais être posé sur une
  déclaration qui échouerait au contrôle (vérifié en base réelle : le flag **reste 0**).

La garde d'état est évaluée **avant** le contrôle IF/ICE : un test vérifie qu'aucune lecture Sage
n'est faite quand la déclaration n'est même pas clôturée (`AppelsIdentitesErp == 0`).

### 1.7 Dépôt = flag manuel, aucun appel externe

`MarquerDeposeAsync` pose `DDP_IsDepose` après un `ValiderDepot` qui exige d'abord **fichier généré**.
Aucun appel réseau, aucune plateforme externe, nulle part dans le code livré (décision PO §5.A-2 du
19/07/2026) — vérifiable par lecture : le service n'a **aucune** dépendance HTTP.

### 1.8 Repository (`Declaration.Infrastructure`) — écritures strictement bornées

`DeclarationDelaiPaiementRepository`, seul endroit contenant du SQL (ARCHITECTURE §5) :

| Opération | SQL | Contrainte respectée |
|---|---|---|
| Créer l'entête | `INSERT RT_DECLARATIONDELAISPAIEMENT` (16 colonnes, hors identity/rowversion) | transaction explicite |
| Muter l'état | `UPDATE` restreint aux **6 colonnes mutables du legacy** (`DDP_Statut`, `UT_IdModif`, `DDP_DateModif`, `DDP_IsDepose`, `DDP_Libelle`, `DDP_IsGeneretedFile`) | numéro/date/exercice/bornes/type/période **inatteignables par le contrat** |
| Supprimer l'entête | `DELETE` **après re-contrôle « 0 ligne » DANS la même transaction** | pas de suppression en cascade silencieuse |
| Intégrer des lignes | `INSERT RT_DECLARATIONDELAISPAIEMENTLG` (5 colonnes), **une seule transaction pour le lot**, batché par 500 | rollback explicite sur exception |
| Retirer une ligne | `DELETE … WHERE DDPL_Id = @DdplId AND DDP_Id = @DdpId` | **scopé** : impossible de toucher une autre déclaration |
| Lectures | entête, liste + compte de lignes, exercice, `MAX(DDP_Numero) LIKE`, config de numérotation (`P_SOCIETE`), lignes jointes, `F_COMPTET` | 100 % `SELECT` |

Toutes les requêtes sont **paramétrées** (Dapper). Les seules valeurs interpolées en C# sont (a) le
sentinel privé `'17530101'` (constante, même convention que `SelectionDelaiPaiementRepository`) et
(b) les noms de colonnes IF/ICE, **validés par whitelist** puis quotés par
`IdentiteFiscaleFournisseurConfig` (TASK-048). Le motif `LIKE` de numérotation reste un **paramètre**
Dapper, jamais interpolé.

---

## 2. Fichiers créés

- `Declaration.Core/DeclarationDelaiPaiementCycleDeVie.cs` — enums (`TypeDeclarationDelaiPaiement`,
  `TrimestreDelaiPaiement`, `StatutDeclarationDelaiPaiement`), `PeriodeDeclarationDelaiPaiement`,
  `EtatDeclarationDelaiPaiement`, `DeclarationDelaiPaiementExistanteResume`, calcul des bornes,
  unicité de période, les 9 gardes de transition.
- `Declaration.Core/ControleIdentiteFiscaleDelaiPaiement.cs` — contrôle IF/ICE PUR réutilisable +
  `MotifIdentiteFiscaleDelaiPaiement`, `IdentiteFiscaleFournisseurDeclare`,
  `FournisseurIdentiteFiscaleFautif`, `ResultatControleIdentiteFiscaleDelaiPaiement`.
- `Declaration.Core/NumerotationDeclarationDelaiPaiement.cs` — numérotation PURE +
  `ConfigurationNumerotationDelaiPaiement`.
- `Declaration.Application/Entities/DeclarationDelaiPaiementEntities.cs` — `DeclarationDelaiPaiement`,
  `DeclarationDelaiPaiementListItem`, `LigneAIntegrerDelaiPaiement`, `CleLigneDelaiPaiement`,
  `ResultatIntegrationLignesDelaiPaiement`, `LigneDeclarationDelaiPaiement`,
  `IdentiteFiscaleTiersErp`.
- `Declaration.Application/Interfaces/IDeclarationDelaiPaiementRepository.cs` — contrat de persistance,
  écritures autorisées énumérées explicitement.
- `Declaration.Application/Services/DeclarationDelaiPaiementService.cs` —
  `IDeclarationDelaiPaiementService` + impl + `CreerDeclarationDelaiPaiementRequest`.
- `Declaration.Infrastructure/Repositories/DeclarationDelaiPaiementRepository.cs`.
- `Declaration.Core.Tests/DeclarationDelaiPaiementCycleDeVieTests.cs` — **35** cas de test.
- `Declaration.Core.Tests/ControleIdentiteFiscaleDelaiPaiementTests.cs` — **29** cas de test.
- `Declaration.Core.Tests/NumerotationDeclarationDelaiPaiementTests.cs` — **17** cas de test.
- `Declaration.Orchestration.Tests/Task132CycleDeVieDeclarationDelaiPaiementTests.cs` — 12 tests
  (cycle de vie complet hors DB, repository en mémoire + faux `ISelectionDelaiPaiementService`).

## 3. Fichiers modifiés

- `Declaration.API/Program.cs` — **uniquement** 2 `AddScoped` TASK-132 + commentaire
  (`git diff --stat` : **8 insertions, 0 suppression**, aucun autre hunk).
- `TASKS/DDP-TASK-132-…md` → `IN_PROGRESS/DDP-TASK-132-…md` (déplacement demandé).

**Aucun autre fichier touché.** Pas de controller API, pas de front, pas de `DeclarationTVA.sql`, pas
de refactor, aucune anticipation de TASK-133/134.

### Cohabitation avec les sessions parallèles

Des modifications non committées d'**autres agents/sessions** sont présentes dans l'arbre :
`CHANGELOG.md`, `DONE.md`, `TODO.md`, `LANCEMENT_DEV.md`, `VERIFY/TASK-006|007|024_verify.md`,
`declaration-tva-web/.env.development`, `declaration-tva-web/vite.config.ts`,
`DOCS/PROMPT-WORKER-*.md`, 8 captures `VERIFY/task138-*|task139-*.png`, plus les renommages
TASK-186→190 **déjà indexés** par une session antérieure. **Aucun de ces fichiers n'a été modifié ni
stagé par cette session** : le commit a été fait avec des **pathspecs explicites** (jamais
`git add -A`, jamais `git commit -a`) et `git diff --cached` a été relu fichier par fichier avant
validation.

---

## 4. Couverture de tests (93 nouveaux, hors DB)

### 4.1 `Declaration.Core.Tests` — 81 nouveaux cas (196/196 verts au total)

> Les comptes ci-dessous sont des **cas de test exécutés** (les `[Theory]` sont donc comptées pour
> chacune de leurs `InlineData`) : 35 + 29 + 17 = **81**, chiffre vérifié par
> `dotnet test --filter FullyQualifiedName~<classe>`.

**Bornes de période et unicité — 14 cas**
- Annuelle = année civile complète ; les 4 trimestres avec leurs bornes calendaires exactes (Theory).
- Trimestrielle sans trimestre → « Trimestre invalide. » ; type inconnu → « Type déclaration invalide. ».
- Exercice hors bornes → message métier explicite (jamais une `ArgumentOutOfRangeException` brute).
- `DDP_Periode` : 0 pour une annuelle (**jamais** le trimestre résiduel trompeur du legacy), 1..4 sinon.
- Unicité : **mêmes bornes détectées malgré l'heure `23:59:59` stockée** (le contrôle legacy, interrogé
  à minuit sur une colonne stockée à 23:59:59, ne pouvait **jamais** matcher — cf. § Décisions n°2) ;
  autre exercice / autre trimestre → aucun conflit ; trimestre inclus dans une annuelle existante →
  bloqué ; **annuelle après un trimestre → NON bloquée** (asymétrie legacy assumée, cf. § Décisions n°3).

**Gardes de transition — 21 cas** — un test par garde et par transition, dont 3 vérifiant
explicitement l'**ordre d'évaluation legacy** (le dépôt est testé avant le statut, la génération avant
le dépôt), plus un test paramétrique refusant un état `null` sur **les 9 gardes**.

**Contrôle IF/ICE — 29 cas**
- Cas valide (2 fournisseurs conformes) ; aucune ligne → conforme sans message.
- IF absent / vide / blanc ; IF de 7 et de 9 caractères (longueur réelle citée) ; IF de 8 caractères
  **avec un espace** (les deux règles legacy sont bien séparées).
- ICE absent / longueur invalide / avec espace.
- **Cumul** : IF *et* ICE invalides ⇒ les 2 motifs listés, autant de libellés que de motifs.
- **Fournisseur introuvable dans le référentiel** ⇒ motif **unique** et explicite (on ne prétend pas
  savoir ce que contient une fiche qu'on n'a pas lue).
- **Multi-fournisseurs** : 4 fournisseurs dont 3 fautifs ⇒ les 3 cités, triés par code, le conforme
  **absent du message** ; même fournisseur sur plusieurs lignes ⇒ cité **une seule fois** avec le
  cumul de lignes ; accords singulier/pluriel ; intitulé absent ⇒ message resté lisible.
- **Invariant paramétré (11 cas)** : « aucun motif » ⇔ `EstIfValide && EstIceValide` — garantit
  qu'il n'existe **pas deux sources de vérité** entre les motifs explicatifs et
  `ValidationIdentiteFiscale`.
- Entrées `null` refusées.

**Numérotation — 17 cas** — racine préfixe/année/mois ; motif `LIKE` ; premier numéro `DDP26070001` ;
incréments (`0001→0002`, `0009→0010`, `0099→0100`, `9998→9999`) ; changement de mois ⇒ retour à 1 ;
**compteur saturé bloqué** ; suffixe non numérique bloqué ; dernier numéro trop court bloqué ;
`SO_DecDPNumCount` à 0/-1/10 bloqué ; dépassement des 30 caractères bloqué ; config `null` refusée.

### 4.2 `Declaration.Orchestration.Tests` — 12 nouveaux (217/217 verts au total)

Le test principal exerce le **cycle de vie complet hors base** en 9 étapes avec assertions sur ce qui
est **réellement écrit** : création (numéro, bornes `23:59:59`, `DDP_Periode`, statut) → clôture
refusée sans ligne → intégration (2 candidates écrites, la ligne « reprise manuelle requise » **jamais**
écrite, bornes passées à TASK-131 vérifiées, `AF_Id` nullable, `Depassement`, `EcheanceLegale`) →
rejeu sans doublon → clôture → **contrôle IF/ICE bloquant** (2 fournisseurs fautifs, flag
`DDP_IsGeneretedFile` refusé) → IF/ICE corrigés ⇒ génération autorisée → dépôt → **plus rien n'est
modifiable après dépôt** (4 assertions) → **les 6 colonnes immuables n'ont pas bougé de tout le cycle**.

Les 11 autres : doublon de période refusé avec citation de la déclaration existante ; trimestres
différents acceptés avec numéros incrémentés ; création sans utilisateur refusée (audit trail) ;
annuelle ⇒ `DDP_Periode` non applicable ; intégration sélective (candidate intégrée / reprise manuelle
**refusée** / clé introuvable **signalée**) ; **société non configurée ⇒ 0 ligne écrite mais information
restituée** ; suppression refusée avec lignes puis autorisée après retrait ; suppression d'une ligne
inexistante ⇒ erreur explicite ; fournisseur absent du référentiel ERP ⇒ bloqué ; génération sur une
déclaration EnCours ⇒ bloquée **avant toute lecture Sage** ; déclaration inexistante ⇒ « Impossible de
charger la déclaration. ».

---

## 5. Vérification base réelle (`GR_EMA_DISTRIBUTION`, `127.0.0.1`)

> Le nom d'hôte `DESKTOP-5BFKKEP` de `connections.json` **n'est pas résolvable** depuis cette session ;
> `127.0.0.1` pointe la même instance (`Iheb-PC\SQL2022`) — même constat que TASK-127/131.

Schéma des 2 tables **vérifié colonne par colonne** (`INFORMATION_SCHEMA.COLUMNS`) : 18 colonnes sur
`RT_DECLARATIONDELAISPAIEMENT` (dont `RowVersion timestamp`, `DDP_Libelle nvarchar(MAX)`,
`DDP_Numero nvarchar(30) NOT NULL`, `DDP_Id` identity) et 6 sur `RT_DECLARATIONDELAISPAIEMENTLG`
(`DDPL_Depassement decimal(24,6)`, `AF_Id int NULL`) — **conformes au mapping legacy**.
Index existants : `PK_dbo.RT_DECLARATIONDELAISPAIEMENT` (clustered) et `IX_SO_Id` (non unique).
**Aucun index unique sur le numéro ni sur les bornes de période** (cf. § Décisions n°1).

Harness temporaire (projet console dans le scratchpad, **hors dépôt, non committé**) instanciant la
chaîne RÉELLE `DeclarationDelaiPaiementRepository` + `SelectionDelaiPaiementRepository` →
`DelaiPaiementService` → `DelaiPaiementBootstrapService` → `SelectionDelaiPaiementService` →
`DeclarationDelaiPaiementService`.

**Ce qui a été RÉELLEMENT vérifié en base :**

| Point | Résultat réel |
|---|---|
| État initial | `RT_DECLARATIONDELAISPAIEMENT` **0**, `…LG` **0**, `DM_PARAM_DELAIPAIEMENT_SOCIETE` **0** |
| Société non configurée (garde-fou TASK-128 amont) | **0 candidate, 498 en reprise manuelle requise**, `DateMiseEnRoute = null` ⇒ **rien d'intégrable** (comportement amont confirmé, cf. § 7 n°1) |
| Après saisie d'une mise en route (`2023-07-01`, écriture **temporaire** sur la table `DM_*` GRF) | **498 candidates, 0 en reprise manuelle** |
| Création T1 2026 | `DDP_Numero = DDP26070001` (paramétrage réel `DDP`+aa+MM+4), `DDP_DateDebut = 2026-01-01 00:00:00`, `DDP_DateFin = 2026-03-31 23:59:59`, `DDP_Statut = 0`, `DDP_Type = 2`, `DDP_Periode = 1`, `DDP_IsDepose = 0`, `DDP_IsGeneretedFile = 0` |
| Unicité de période | 2ᵉ création T1 2026 **refusée** : « Déclaration existe déjà pour la même période (déclaration [DDP26070001], 01/01/2026 - 31/03/2026). » |
| Clôture sans ligne | **refusée** : « La déclaration ne contient aucune ligne. » |
| Intégration de 25 lignes réelles | 25 demandées / **25 intégrées** / 0 déjà intégrée / 0 refusée / 0 introuvable ; **25 lignes réellement présentes** en base |
| Persistance `AF_Id` nullable | **12 des 25 lignes** ont `AF_Id IS NULL` (buckets « part non affectée » TASK-131) — colonne nullable réellement exercée |
| Persistance `DDPL_Depassement` / `DDPL_EcheanceLegale` | ex. `EC_Id=19909, AF_Id=NULL, Depassement=349.000000, EcheanceLegale=2025-04-16` (cohérent avec le 349 recalculé à la main par TASK-131) ; **0 ligne avec `DDPL_Depassement <= 0`** |
| Garde anti-double-intégration | rejeu du même lot ⇒ **0 intégrée, 25 déjà intégrées**, toujours **25 lignes** en base |
| Suppression avec lignes | **refusée** : « La déclaration contient des lignes. » |
| Clôture / déclôture | `DDP_Statut` 0 → **1** → **0** → 1, effectif en base |
| Contrôle IF/ICE, cas conforme | 16 fournisseurs examinés, **0 fautif** sur `F_COMPTET` réel ⇒ génération autorisée |
| `MarquerFichierGenereAsync` | `DDP_IsGeneretedFile = 1` en base |
| `MarquerDeposeAsync` | `DDP_IsDepose = 1` en base ; déclôture ensuite **refusée** (« La déclaration est déposée. ») |
| Colonnes immuables après tout le cycle | `Numero`, `DateDebut`, `DateFin` (23:59:59 conservé), `Exercice`, `Type`, `Periode`, `UT_Id` **inchangés** |
| Incrément du compteur | 2ᵉ déclaration (T2 2026) ⇒ **`DDP26070002`** |
| **Contrôle IF/ICE, cas BLOQUANT sur données réelles** | 74 fournisseurs candidats ⇒ **72 conformes, 2 non conformes** (`4411INWI` et `FOUBREL`, intitulés « Tiers à créer », IF **et** ICE absents dans `F_COMPTET`) ; leurs 2 lignes intégrées puis clôturées ⇒ contrôle **NON conforme**, message listant les 2 fournisseurs et leurs motifs |
| **Aucune génération possible** | `MarquerFichierGenereAsync` lève, et `DDP_IsGeneretedFile` **reste 0 en base** ⇒ critère TASK-132 « aucune génération possible avec un IF/ICE absent ou mal formaté » **vérifié sur données réelles** |
| Dépôt sans fichier généré | **refusé** : « Le fichier du déclaration n'est pas généré. » |
| **Nettoyage** | `RT_DECLARATIONDELAISPAIEMENT` **0**, `…LG` **0**, `DM_PARAM_DELAIPAIEMENT_SOCIETE` **0**, `DM_REPRISE_DELAIPAIEMENT` **0** — vérifié par `sqlcmd` **indépendant** après le run |

Divergence de mapping tiers tranchée et mesurée : le legacy lisait `CT_PayeurNo/Code/Intitule` sur
`RT_ECHEANCE` pour la relecture des lignes ; on lit `CT_No/CT_Code/CT_Intitule` — **les mêmes colonnes
que la sélection TASK-131**, pour que le fournisseur contrôlé/exporté soit exactement celui qui a été
sélectionné. Contrôle réel : sur les **1 408** échéances achat de la base, `CT_Code = CT_PayeurCode`
sur **1 408/1 408** (0 divergence). Documenté en § Décisions n°4.

---

## 6. Preuve de non-modification de schéma

- `DeclarationTVA.sql` **non modifié** (absent de `git status`) → aucune table créée, aucune colonne
  ajoutée, aucun index/contrainte posé.
- Aucun `CREATE TABLE` / `ALTER TABLE` / `CREATE INDEX` / `DROP` / `TRUNCATE` / `MERGE` dans les
  8 fichiers livrés (`grep` : les seules occurrences de ces mots sont dans des **commentaires de
  documentation**).
- Écritures **exclusivement** sur `RT_DECLARATIONDELAISPAIEMENT` / `RT_DECLARATIONDELAISPAIEMENTLG`
  (tables EXISTANTES possédées GRF, réutilisées telles quelles, exactement comme `RT_CONVENTIONTIERS`
  pour TASK-129), et **jamais** en DDL.
- `P_SOCIETE` (numérotation + colonnes IF/ICE) et `F_COMPTET` (Sage) : **`SELECT` uniquement**.
- **Aucun besoin de colonne/contrainte supplémentaire n'a été rencontré** — mais l'absence d'index
  unique disponible est une **limite structurelle** documentée en § Décisions n°1, pas une omission.
- Écritures temporaires de test (`DM_PARAM_DELAIPAIEMENT_SOCIETE`, table **`DM_*` neuve possédée
  GRF**, TASK-128) : nettoyées, table revenue à 0 ligne (vérifié).

---

## 7. Checklist

- [x] Build back `dotnet build DeclarationTVA.slnx` — **0 erreur** (24 warnings, tous préexistants,
      **aucun** dans les fichiers TASK-132 ; vérifié en isolant les chemins des warnings).
- [x] `Declaration.Core.Tests` — **196/196 verts**, dont **81 nouveaux**.
- [x] `Declaration.Orchestration.Tests` — **217/217 verts**, dont **12 nouveaux**.
- [x] `Declaration.Export.Xml.Tests` 13/13, `Declaration.Export.Excel.Tests` 3/3 — verts, aucune régression.
- [x] Les 2 seuls tests rouges de la solution sont **préexistants et sans rapport** :
      `Declaration.Selection.Tests.IntegrationRegressionTests` (chaîne codée en dur
      `Server=.;Integrated Security=True`) et `Declaration.Controle.Tests.ComparateurTests`
      (`Server=.\sql2022`) — instances inexistantes sur ce poste, **aucun fichier de ces deux projets
      n'a été touché** (constat identique à VERIFY TASK-131).
- [x] **Critère TASK-132 « cycle de vie complet testé (création → intégration → clôture → contrôle
      IF/ICE → dépôt) »** — couvert (a) par un test d'orchestration en 9 étapes hors DB **et**
      (b) **rejoué de bout en bout contre la base réelle** (§ 5).
- [x] **Critère TASK-132 « aucune génération de fichier possible avec un IF/ICE fournisseur absent ou
      mal formaté ; message explicite listant les fournisseurs fautifs »** — couvert par 29 cas de test
      unitaires **et vérifié en base réelle** : `DDP_IsGeneretedFile` **reste 0** sur une déclaration
      portant 2 fournisseurs réellement fautifs, message citant `[4411INWI]` et `[FOUBREL]` avec leurs
      motifs.
- [x] Contrôle IF/ICE **RÉUTILISABLE par TASK-133** : calculateur PUR sans dépendance base + point
      d'entrée applicatif `VerifierGenerationFichierAutoriseeAsync` **sans effet de bord**, découplé de
      toute génération (qui n'existe pas dans ce périmètre).
- [x] Aucune table créée / aucune colonne ajoutée / aucun index ou contrainte posé / aucune table
      winform altérée (§ 6).
- [x] `UPDATE` restreint aux **6 colonnes mutables** du legacy — les autres sont inatteignables par le
      contrat de repository (vérifié aussi en base : colonnes immuables inchangées après tout le cycle).
- [x] Écritures multi-lignes en **transaction** (ARCHITECTURE §5) ; `DELETE` d'entête protégé par un
      re-contrôle « 0 ligne » **dans** la transaction ; `DELETE` de ligne **scopé** sur `DDP_Id`.
- [x] Pas de SQL inline hors couche repository ; requêtes **paramétrées** ; noms de colonnes IF/ICE
      validés par whitelist avant quotage (`IdentiteFiscaleFournisseurConfig`, TASK-048).
- [x] `F_COMPTET` lu **via la connexion Sage résolue par `SO_Id`** (TASK-118) + jointure applicative
      en mémoire — **jamais** de JOIN cross-base depuis la connexion GRF (leçon TASK-154).
- [x] Aucun secret codé en dur ; aucun bypass sécurité (aucun endpoint ajouté dans ce périmètre).
- [x] Aucune réutilisation de DLL/code WinForms — code neuf, seule la sémantique legacy est reproduite.
- [x] Aucune règle métier dupliquée : le verdict IF/ICE délègue à `ValidationIdentiteFiscale`
      (autorité unique), avec un **test d'invariant** garantissant l'absence de divergence.
- [x] `LignesRepriseManuelleRequise` **jamais intégrées** (message TASK-131 §10 respecté), mais
      **comptées et reportées** — aucune ligne écartée silencieusement.
- [x] Bornes passées à TASK-131 = **toujours** `DDP_DateDebut`/`DDP_DateFin` de la déclaration
      (assertion dédiée) — jamais une plage libre.
- [x] Audit trail : `UT_Id`/`UT_IdModif` **obligatoires et > 0** (jamais un `0` « utilisateur
      inconnu » écrit en base) ; `DDP_DateModif`/`UT_IdModif` mis à jour sur chaque transition.
- [x] Périmètre STRICT respecté : **aucune** génération XML/ZIP (TASK-133), **aucune** UI ni endpoint
      (TASK-134), **aucune** modification de l'algorithme de sélection (TASK-131), aucun refactor.
- [x] Diff `Program.cs` : **additif uniquement** (8 insertions, 0 suppression), vérifié via `git diff`.
- [x] Aucun fichier d'une autre session parallèle modifié ni stagé (`git diff --cached` relu).
- [x] Données de test en base réelle **intégralement nettoyées** (4 tables à 0 ligne, vérifié
      indépendamment).
- [ ] **Endpoints API / bout-en-bout applicatif** : **NON applicable / NON fait** — aucun controller
      n'est livré (hors périmètre, l'UI est TASK-134). La chaîne complète a été exercée par un harness
      console contre la base réelle, mais l'API n'a **pas** été démarrée. Les 2 services sont
      néanmoins **enregistrés en DI** pour que TASK-133/134 les consomment sans échec DI latent.

---

## 8. Décisions worker documentées (pas d'exigence PO explicite — à confirmer)

1. **Unicité du numéro et de la période : garantie APPLICATIVE uniquement, fenêtre de course
   résiduelle.** La table ne porte **aucun index unique** (vérifié : seuls
   `PK_dbo.RT_DECLARATIONDELAISPAIEMENT` et `IX_SO_Id` non unique) et la contrainte non négociable
   interdit d'en ajouter un (table possédée par `apbs-gr_winform`). Le legacy calculait le numéro à
   l'**ouverture du formulaire** et revérifiait l'unicité bien plus tard : deux utilisateurs
   simultanés obtenaient le même numéro. Ici numéro + contrôles + `INSERT` sont enchaînés côté serveur,
   ce qui **réduit** la fenêtre au temps d'un appel, sans l'**annuler**. **Signalé, pas contourné** :
   fermer complètement ce trou exigerait soit un index unique (interdit), soit une table `DM_*` de
   séquence (hors périmètre TASK-132, à arbitrer PO).
2. **`DDP_DateFin` stocké à `23:59:59`, comparaisons faites au JOUR.** Convention legacy reproduite
   (`dateFin.Date.AddDays(1).AddSeconds(-1)`, l.674) **pour l'interopérabilité** : une déclaration
   produite par l'ancien applicatif et une produite par GRF web doivent être indiscernables, et
   TASK-131 lit `MAX(DDP_DateFin)` sur les deux. Conséquence : le contrôle d'unicité par égalité exacte
   du legacy (l.655) était **structurellement mort** (il interrogeait à minuit une colonne stockée à
   23:59:59) — il est ici rendu réellement effectif en comparant au jour. Aucun impact sur TASK-131,
   qui applique `.Date` sur les bornes.
3. **Asymétrie du contrôle « période englobante » : reproduite, NON corrigée.** Créer une
   trimestrielle alors qu'une annuelle du même exercice existe est **bloqué** ; créer l'annuelle
   **après** un trimestre ne l'est **pas** (test explicite documentant le comportement). Le texte de
   TASK-132 tranche explicitement « pas de vrai risque de chevauchement à gérer ici » : je n'ai donc
   **pas** durci en contrôle bidirectionnel comme l'a fait TASK-129 pour les conventions, car cela
   interdirait aussi une coexistence annuelle + trimestrielle peut-être légitime. **Arbitrage PO
   demandé** (correction locale : une ligne dans `TrouverPeriodeEnConflit`).
4. **Tiers lu via `CT_No`/`CT_Code`/`CT_Intitule`, pas `CT_Payeur*`.** Le legacy relisait les lignes
   avec les colonnes `CT_Payeur*` de `RT_ECHEANCE` ; la sélection TASK-131, elle, utilise `CT_*`.
   Retenu : `CT_*`, pour que le fournisseur **contrôlé et exporté** soit exactement celui qui a été
   **sélectionné** (sinon un IF/ICE pourrait être validé sur un tiers différent de celui déclaré).
   Impact mesuré nul sur la base réelle (`CT_Code = CT_PayeurCode` sur 1 408/1 408 échéances achat),
   mais **à confirmer sur une base client utilisant réellement la notion de payeur** — voir § 9 n°3.
5. **`DDP_Periode = 0` pour une annuelle.** Le legacy y recopiait le trimestre résiduel du formulaire,
   valeur non significative jamais relue (le générateur écrit la période littérale `5` pour une
   annuelle). 0 = « non applicable » est explicite plutôt que trompeur. **TASK-133 doit émettre 5 pour
   l'annuelle sans lire `DDP_Periode`** (rappelé en § 10).
6. **Garde anti-double-intégration ajoutée** (le legacy n'en avait aucune) : une clé `(EC_Id, AF_Id)`
   déjà présente dans **cette** déclaration est ignorée et **reportée**. Ne remplace pas le mécanisme
   anti-double-**déclaration** de TASK-131 (calcul incrémental), qu'elle complète au niveau de la
   déclaration courante.
7. **`UT_Id` obligatoire et strictement positif.** Les colonnes `UT_Id`/`UT_IdModif` sont `NOT NULL` et
   ARCHITECTURE §5 exige un audit trail : plutôt que d'écrire un `0` « utilisateur inconnu » silencieux,
   le service **refuse** l'opération. TASK-134 devra donc propager le claim JWT `"UT_Id"` (déjà posé au
   login par `AuthController`, et déjà lu par `DelaiPaiementParametrageController`, TASK-128).
8. **Tampons de modification mis à jour à l'ajout/retrait de ligne** (le legacy ne le faisait pas) :
   `DDP_DateModif`/`UT_IdModif` seulement, aucune autre colonne. Ajout assumé pour la traçabilité.
9. **Divergence de règle IF/ICE avec le module TVA — À ARBITRER PO, pas tranchée par moi.**
   TASK-132 exige explicitement 8 caractères pour l'IF et 15 pour l'ICE (réactivation du bloc legacy,
   décision PO §5.A-5). Mais le **même dépôt** porte un commentaire contraire pour l'export TVA :
   `ValidationIdentiteFiscale.ValiderPourExport` **refuse délibérément de bloquer sur une longueur
   fixe** (« CDC §4.8/§5.2 : le fichier réellement accepté par le fisc contient des identifiants
   fournisseurs de **7 chiffres** », point ouvert §5.2 « format de longueur définitif à confirmer par le
   PO/fiscaliste »). J'ai appliqué **la consigne TASK-132 (8/15 strict)** car c'est la seule décision
   PO explicite de mon périmètre — **mais les deux modules ne peuvent pas avoir durablement raison en
   même temps.** Impact concret mesuré sur la base réelle : `168/357` fournisseurs ont un IF de
   longueur 8, `192/357` un ICE de longueur 15 ⇒ **une part importante du parc serait bloquée à
   l'export DDP alors qu'elle passe à l'export TVA.** **Point à trancher par le PO/fiscaliste avant
   mise en production**, cf. § 9 n°1.

---

## 9. Reste à valider (NON couvert par ce VERIFY)

1. **Longueur exacte exigée pour l'IF et l'ICE : décision PO/fiscaliste requise (le plus important).**
   Voir § 8 n°9. Tel que livré, la DDP bloque sur 8/15 strict (consigne TASK-132) alors que l'export
   TVA du même produit a délibérément renoncé à cette règle. Si le PO confirme que 8/15 est bien la
   règle DDP, il faut le **dire explicitement** ; s'il aligne la DDP sur la TVA, la correction est
   locale (`ControleIdentiteFiscaleDelaiPaiement.LongueurIdentifiantFiscal`/`LongueurIce`, ou passage
   à une règle « non vide et sans espace »). **Je n'ai pas tranché seul une règle fiscale.**
2. **Comportement AMONT hérité de TASK-131 §1.6 (signalé, hors de mon périmètre) :** tant qu'une
   société n'a pas sa **date de mise en route** configurée, `SelectionnerAsync` renvoie **0 ligne**
   intégrable — confirmé sur la base réelle (**0 candidate, 498 en reprise manuelle requise**). Une
   déclaration créée dans cet état ne pourra donc **jamais être clôturée** (garde « au moins une
   ligne »). **TASK-134 doit impérativement exposer la saisie de cette date** (endpoint déjà livré par
   TASK-128 : `PUT /api/delai-paiement/parametrage/{soId}`), sinon l'écran apparaîtra vide et bloqué au
   client. Ce n'est **pas** un défaut de TASK-132 : après saisie d'une date de mise en route, 498
   candidates apparaissent et l'intégration fonctionne (vérifié).
3. **`CT_Code` vs `CT_PayeurCode` : identiques sur la base de test, jamais vus divergents.**
   Décision n°4 non exerçable sur un parc où le payeur diffère du tiers facturé. À revoir avec le PO si
   un client utilise réellement la notion de payeur Sage (impact : quel IF/ICE est contrôlé/exporté).
4. **Aucun endpoint API livré** (périmètre : UI = TASK-134). Les 2 services sont enregistrés en DI et
   leur chaîne d'instanciation a été exercée par le harness contre la base réelle, mais **l'API n'a pas
   été démarrée** et aucun appel HTTP n'a été testé. Le contrôle d'autorisation par société
   (`EstSocieteAutorisee`, pattern `DeclarationsController`/`DelaiPaiementParametrageController`) reste
   donc **à implémenter par TASK-134** : le service ne vérifie **pas** que l'utilisateur a droit à la
   société — c'est le rôle du controller dans ce dépôt, mais **il ne faut pas l'oublier**.
5. **Fenêtre de course sur le numéro et sur la période : réduite, pas fermée** (§ 8 n°1). Aucun test de
   concurrence n'a été réalisé. À arbitrer PO si le client a plusieurs utilisateurs simultanés sur cet
   écran.
6. **Trace d'un usage antérieur de ces tables dans la base de test — à signaler au PO.** Les deux
   tables sont vides, mais les compteurs d'identité indiquent des lignes **historiques supprimées** :
   avant mon premier `INSERT`, `IDENT_CURRENT('RT_DECLARATIONDELAISPAIEMENT') ≈ 2` et
   `IDENT_CURRENT('RT_DECLARATIONDELAISPAIEMENTLG') = 1650` (mes 54 insertions expliquent exactement
   l'écart jusqu'à 1704). Autrement dit **≈2 déclarations et ≈1 650 lignes ont existé puis ont été
   supprimées**. Conséquence directe : l'historique anti-double-déclaration de TASK-131 (correction
   n°1, basée sur `RT_DECLARATIONDELAISPAIEMENTLG`) **n'a aucune trace de ces déclarations passées**.
   Cela **confirme concrètement** le risque déjà signalé en VERIFY TASK-131 §9 n°4 : sur un parc réel,
   le garde-fou « date de mise en route » (TASK-128) peut être la **seule** protection contre la
   double-déclaration. **À vérifier client par client avec le PO avant mise en route.**
7. **Performance non mesurée à l'échelle.** L'intégration a été exercée sur 25 lignes réelles (et le
   lot est batché par 500 en une transaction) ; aucun test sur plusieurs milliers de lignes. La lecture
   `F_COMPTET` est batchée par 500 codes tiers et n'a été exercée que sur 74 fournisseurs distincts.
8. **Génération du fichier elle-même : hors périmètre (TASK-133).** `MarquerFichierGenereAsync` ne pose
   qu'un flag ; aucun XML/ZIP n'est produit ni testé ici. L'anomalie legacy « fichier orphelin sur
   disque avec `DDP_IsGeneretedFile = 0` » (le générateur écrivait avant de poser le flag, et
   l'annulation de génération ne supprimait pas les fichiers, si bien que la régénération échouait sur
   `File.Exists`) **n'est PAS résolue ici** — elle relève entièrement de TASK-133, à qui elle est
   signalée en § 10.

---

## 10. Message explicite pour TASK-133 (consommateur direct du contrôle IF/ICE)

1. **Appeler `IDeclarationDelaiPaiementService.VerifierGenerationFichierAutoriseeAsync(ddpId)` AVANT
   d'écrire le premier octet.** Elle enchaîne la garde d'état (clôturée, non déposée, fichier pas déjà
   généré, au moins une ligne) **puis** le contrôle IF/ICE complet, et lève
   `InvalidOperationException` dont le `Message` est **déjà** le message utilisateur listant les
   fournisseurs fautifs. Aucun effet de bord, donc appelable autant de fois que voulu.
2. **Ne jamais réimplémenter le contrôle.** Le verdict structuré
   (`ResultatControleIdentiteFiscaleDelaiPaiement`) est retourné : `FournisseursFautifs` porte
   `TiersCode`, `TiersIntitule`, `IdentifiantFiscal`, `Ice`, `NombreLignes`, `Motifs` et
   `MotifsLibelles`. Pour un usage purement informatif (afficher sans bloquer), utiliser
   `ControlerIdentiteFiscaleAsync(ddpId)` qui ne lève pas.
3. **Après écriture réussie du fichier, appeler `MarquerFichierGenereAsync(ddpId, utilisateurId)`** —
   elle re-valide intégralement avant de poser `DDP_IsGeneretedFile` (le flag ne peut pas être posé sur
   une déclaration non conforme).
4. **Anomalie legacy à traiter dans TON périmètre, pas résolue ici** : le legacy écrivait XML+ZIP
   **puis** posait le flag, sans transaction ni compensation ⇒ un échec entre les deux laissait des
   **fichiers orphelins** sur disque avec `DDP_IsGeneretedFile = 0` ; et `AnnulerGenerationFichier`
   remettait le flag à 0 **sans supprimer les fichiers**, si bien que la régénération échouait ensuite
   sur `« Le fichier XML [x] existe déja. »`. Décide explicitement de la stratégie (écrire dans un
   temporaire puis renommer, ou supprimer les fichiers à l'annulation) et documente-la.
5. **`DDP_Periode` vaut 0 pour une annuelle** (décision n°5). N'émets **pas** `DDP_Periode` tel quel :
   le générateur legacy écrit la période littérale **`5`** pour une annuelle et `1..4` pour un
   trimestre. Utilise `DeclarationDelaiPaiement.Type` + `Trimestre` (propriété calculée, `null` pour
   une annuelle).
6. **`DDP_DateFin` est stocké à `23:59:59`** du dernier jour de période (décision n°2). Applique `.Date`
   si tu écris une date de fin dans le fichier.
7. **IF/ICE viennent de `F_COMPTET` (base SAGE), pas de GRF** (§ 1.3) : si tu as besoin d'autres champs
   tiers (numéro RC, adresse — le générateur legacy émet `<numRC>` et `<adresseSiegeSocial>`), résous la
   connexion Sage par `SO_Id` (`GetSageConnectionInfoAsync`, TASK-118) et joins **en mémoire** par
   `CT_Num` = `CT_Code`. **Jamais** de JOIN cross-base depuis la connexion GRF (leçon TASK-154). Les
   noms de colonnes configurables se lisent via `IdentiteFiscaleFournisseurConfig` (TASK-048).
   Attention : le legacy chargeait `TiersIce` mais ne l'écrivait **jamais** dans le XML DDP (seul
   `<identifiantFiscal>` y figure) — vérifie ce que le fisc attend réellement.
8. **La longueur exigée (8/15) est un point OUVERT** (§ 8 n°9 / § 9 n°1) : si le PO l'assouplit, tu n'as
   rien à changer — le contrôle reste au même endroit et tu continues d'appeler la même méthode.
9. `LigneDeclarationDelaiPaiement` (retourné par `GetLignesAsync`) porte déjà tout ce dont le
   générateur legacy avait besoin côté échéance/règlement (`DoNumero`, `DoDate`, `DoReference`,
   `EcheanceContractuelle`, `MontantEcheance`, `SoldeEcheance`, `DeviseId`, `MontantAffecte`,
   `ReglementNumero`, `ReglementPiece`, `ReglementType`, `ReglementDate`, `ReglementRapproche`,
   `ReglementDateRapprochement`, `ReglementModeId`) **sauf** `EC_Cours`/`MV_Cours`/`MV_Montant`/
   `MV_Solde`/`MV_Echeance`/`MV_Info1..4`/`EC_Info1..4` : ajoute-les à la projection du repository si
   ton XML les exige (colonnes existantes, simple `SELECT` additif).

---

## Verdict

Livré : cycle de vie complet de la déclaration Délai de Paiement (création avec bornes calculées +
numérotation serveur + unicité de période, intégration des lignes TASK-131 en transaction unique avec
garde anti-double-intégration, clôture/déclôture, génération, dépôt manuel, suppression) sur les tables
EXISTANTES sans **aucune** modification de schéma, **plus** le contrôle IF/ICE bloquant PUR et
réutilisable tel quel par TASK-133. Build 0 erreur, **93 tests neufs verts** (196/196 Core, 217/217
Orchestration), et **cycle complet rejoué contre la base réelle** avec nettoyage intégral vérifié — y
compris le cas bloquant sur 2 fournisseurs réellement non conformes, où `DDP_IsGeneretedFile` **reste
0**.

**Points ouverts nécessitant l'architecte/le PO** : (1) **la longueur exigée pour l'IF/ICE, en
contradiction assumée avec l'export TVA du même produit** (§ 8 n°9 / § 9 n°1) — le plus structurant ;
(2) l'asymétrie du contrôle de chevauchement annuelle/trimestrielle (§ 8 n°3) ; (3) la fenêtre de
course résiduelle sur le numéro, faute d'index unique autorisé (§ 8 n°1) ; (4) la trace de ~1 650
lignes de déclaration historiques supprimées, qui confirme le risque anti-double-déclaration signalé
par TASK-131 (§ 9 n°6). Aucun de ces points ne bloque la livraison du cycle de vie lui-même ; le n°1
doit être tranché **avant mise en production**.
