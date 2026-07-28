# VERIFY — TASK-191

Date : 2026-07-28
Agent : Worker (session Développeur, prompt `DOCS/PROMPT-WORKER-TASK-191-28-07-2026.md`)

## Périmètre livré

Câblage réel des 2 champs XML `<natureMarchandise>`/`<dateLivraisonMarchandise>` du dépôt Délai de
Paiement, en répliquant à l'identique le pattern déjà en place pour
`SO_ColValueNumRegistreCommerceFournisseur` (`GetIdentitesFiscalesTiersAsync`,
`DeclarationDelaiPaiementRepository.cs`) :

1. **Lecture des 2 colonnes de config `P_SOCIETE`** (`SO_ColValueNatureMarchandise`/
   `SO_ColValueDateLivraisonMarchandise`), whitelist identique à `ValiderNomColonneOptionnelle`
   (même méthode réutilisée, pas dupliquée) — tolérance stricte à l'absence : si AUCUNE des deux
   colonnes n'est configurée pour la société, **aucun aller-retour Sage n'est fait** (dictionnaire
   vide retourné immédiatement), exactement le même principe de non-régression que le pattern NumRc.
2. **Clé de jointure vers `F_DOCENTETE` identifiée et vérifiée empiriquement AVANT tout code** (cf.
   § Clé de jointure ci-dessous) : `F_DOCENTETE.DO_Piece = RT_ECHEANCE.DO_Numero`, restreinte à
   `DO_Domaine = 1` (Achat) — lecture Sage strictement en lecture seule, par lot (même garde
   `TailleLot`/`Decouper` que `GetIdentitesFiscalesTiersAsync`).
3. **Câblage dans `DeclarationDelaiPaiementXmlModele`** : `FactureHorsDelaiXml.NatureMarchandise`
   (nouvelle propriété, absente jusqu'ici — le hardcode vide était en réalité dans l'EXPORTER, pas
   dans le modèle, cf. § Point additionnel découvert) et `DateLivraisonMarchandise` (propriété
   existante, fallback `dateEmission` conservé À L'IDENTIQUE quand la valeur réelle est absente).
4. **Tests unitaires — 3 cas minimum requis, livrés à 2 niveaux** (calcul pur + orchestration bout en
   bout XML) : société/document sans config (non-régression stricte), config présente mais valeur
   absente sur le document (même repli, jamais d'exception), config + valeur réelle présentes
   (câblage effectif, XML reflète la vraie valeur).

## Point additionnel découvert pendant l'implémentation (hors dette documentée, corrigé)

Le CDC/TASK-133 documentaient `<natureMarchandise>` comme « toujours vide » et l'imputaient à
`DeclarationDelaiPaiementLigneCalculator.Calculer` (commentaire `// dette assumée` sur
`DateLivraisonMarchandise` uniquement, l.210). En lisant le code réel :
`Declaration.Core/DeclarationDelaiPaiementXmlModele.cs` **n'avait même pas de propriété
`NatureMarchandise`** sur `FactureHorsDelaiXml` — le tag était **hardcodé directement dans
l'exporter** (`Declaration.Export.Xml/DeclarationDelaiPaiementXmlExporter.cs:114`,
`sb.Append("<natureMarchandise></natureMarchandise>\r\n");`, aucune lecture du modèle). Corrigé en
ajoutant la propriété au modèle ET en modifiant l'exporter pour la lire (`facture.NatureMarchandise`,
échappée XML comme les autres champs texte) — sans quoi le câblage côté repository/calculateur aurait
été un no-op silencieux, jamais visible dans le fichier produit. Documenté ici par transparence,
conforme à la règle « aucune dette technique silencieuse ».

## Clé de jointure retenue — vérifiée empiriquement AVANT tout code (§ contrainte explicite de la TASK)

**Retenue : `F_DOCENTETE.DO_Piece = RT_ECHEANCE.DO_Numero`, restreinte à `F_DOCENTETE.DO_Domaine = 1`
(Achat)** — déjà projetée par `GetLignesAsync` (`DeclarationDelaiPaiementRepository.cs`, colonne
`DoNumero`) et par `SelectionDelaiPaiementRepository` (`DoNumero`, l.96).

Démarche de vérification (lecture seule stricte, `sqlcmd`, avant d'écrire une seule ligne de C#) :

1. **Schéma confirmé réel** (`INFORMATION_SCHEMA.COLUMNS`) :
   - `P_SOCIETE` (`GR_EMA_DISTRIBUTION`) : `SO_ColValueNatureMarchandise` et
     `SO_ColValueDateLivraisonMarchandise` existent bien, toutes deux `nvarchar(max)` — confirmées,
     pas supposées sur la seule foi du CDC.
   - `F_DOCENTETE` (`NEW_EMA DISTRIBUTION`, Sage) : 138 colonnes, dont `DO_Piece` (varchar(13)),
     `DO_Domaine` (smallint), `DO_Tiers` (varchar), `DO_DateLivr`/`DO_DateLivrRealisee` (datetime) —
     **aucune colonne `CT_Num`** (contrairement à `F_COMPTET`) ; le tiers du document est porté par
     `DO_Tiers`.

2. **Précédent déjà existant dans le code confirmant le principe de jointure** (pas une jointure
   inventée) : `SageTaxReader.Core/SageTaxReaderService.cs:233-234` joint déjà
   `F_DOCENTETE WHERE DO_Piece = @piece` pour lire un document par son numéro.

3. **Validation empirique sur un cas réel déjà connu** (`FC2501193`, EC_Id=21466, cas central de
   TASK-186/187/189/190) :

   ```
   RT_ECHEANCE (GR_EMA_DISTRIBUTION) : EC_Id=21466, DO_Domaine=1, DO_Numero=FC2501193,
                                        DO_Date=2025-07-18, CT_Code=F0039
   F_DOCENTETE (NEW_EMA DISTRIBUTION) : DO_Piece=FC2501193, DO_Domaine=1, DO_Date=2025-07-18,
                                         DO_Tiers=F0039, DO_DateLivr=1753-01-01 (sentinelle « vide »)
   ```
   `DO_Piece`, `DO_Domaine` ET `DO_Tiers`/`CT_Code` concordent exactement entre les deux bases pour le
   même document — confirme que `DO_Numero` (GRF) porte bien le numéro de pièce Sage.

4. **Risque de collision `DO_Numero` déjà documenté (audit TASK-143, `DOCS/AUDIT-TASK-143`)** :
   1 collision réelle connue sur 2 460 `EC_Id` (`FA2600106`, entre 2 tiers). Vérifié spécifiquement
   pour cette TASK : cette collision concerne des domaines DIFFÉRENTS (le document `F_DOCENTETE`
   trouvé pour `FA2600106` est `DO_Domaine=0`, Vente) — le filtre `DO_Domaine = 1` (Achat, seul
   domaine traité par la DDP) l'exclut naturellement.
   - Vérification directe de l'unicité `(DO_Piece, DO_Domaine)` sur `F_DOCENTETE` :
     `GROUP BY DO_Piece, DO_Domaine HAVING COUNT(*) > 1` → **0 ligne** en restreignant à
     `DO_Domaine = 1` (Achat). Un seul doublon existe tous domaines confondus (`DO_Piece='1'`,
     `DO_Domaine=2`, donnée de test/garbage non liée à Achat).
   - **Conclusion** : `(DO_Piece, DO_Domaine=1)` est une clé UNIQUE vérifiée sur les données réelles de
     ce périmètre — pas une hypothèse.

Aucune jointure approximative (ex. par date) n'a été nécessaire ni envisagée — la clé exacte existait
et a pu être confirmée avant tout code, conformément à la règle non négociable de la TASK.

## Fichiers modifiés

- `Declaration.Application/Entities/DeclarationDelaiPaiementEntities.cs` — nouvelle classe
  `ValeursMarchandiseErp` (`NatureMarchandise`/`DateLivraisonMarchandise`, tous deux nullables : `null`
  ⇔ config absente OU valeur absente sur le document, contrat unique tolérant aux deux cas).
- `Declaration.Application/Interfaces/IDeclarationDelaiPaiementRepository.cs` — nouvelle méthode
  `GetValeursMarchandiseAsync(int soId, IReadOnlyCollection<string> numerosFacture)`.
- `Declaration.Infrastructure/Repositories/DeclarationDelaiPaiementRepository.cs` — implémentation :
  lit les 2 colonnes de config, whitelist (`ValiderNomColonneOptionnelle`, réutilisée telle quelle),
  retourne un dictionnaire vide SANS appel Sage si aucune des deux colonnes n'est configurée, sinon
  lit `F_DOCENTETE` par lot (`DO_Domaine = 1 AND DO_Piece IN @Numeros`). Constante
  `DoDomaineAchat = 1` ajoutée (même valeur documentée que `SelectionDelaiPaiementRepository`).
- `Declaration.Core/DeclarationDelaiPaiementXmlModele.cs` — `FactureHorsDelaiXml.NatureMarchandise`
  (nouvelle propriété, défaut `string.Empty`) ; `DeclarationDelaiPaiementLigneCalculator.Calculer` :
  2 nouveaux paramètres optionnels `natureMarchandiseReelle`/`dateLivraisonMarchandiseReelle` (défaut
  `null` ⇒ comportement IDENTIQUE à avant pour tout appelant existant, aucune signature cassée).
- `Declaration.Export.Xml/DeclarationDelaiPaiementXmlExporter.cs` — `<natureMarchandise>` lit
  désormais `facture.NatureMarchandise` (échappée XML) au lieu du hardcode vide (cf. § Point
  additionnel ci-dessus).
- `Declaration.Application/Services/DeclarationDelaiPaiementGenerationService.cs` —
  `ConstruireModeleAsync` collecte les numéros de facture distincts et appelle
  `GetValeursMarchandiseAsync` (même pattern que `codesTiers`/`GetIdentitesFiscalesTiersAsync`) ;
  `ConstruireFactureXml` reçoit le dictionnaire, résout la ligne par `DoNumero` (absent du
  dictionnaire ⇒ `null`, même repli que valeur vide) et passe les 2 nouveaux paramètres au
  calculateur.
- `Declaration.Core.Tests/DeclarationDelaiPaiementLigneCalculatorTests.cs` — 3 nouveaux tests (calcul
  pur, cf. § Tests).
- `Declaration.Orchestration.Tests/Task133GenerationFichierDelaiPaiementTests.cs` — `FakeRepository`
  étendu (`ValeursMarchandise` + implémentation de `GetValeursMarchandiseAsync`) + 3 nouveaux tests
  bout en bout (repository fake → service → XML réellement écrit sur disque et relu).
- `Declaration.Orchestration.Tests/Task132CycleDeVieDeclarationDelaiPaiementTests.cs` — `FauxRepository`
  : stub `GetValeursMarchandiseAsync` (dictionnaire vide, jamais exercé par les scénarios de ce
  fichier — cycle de vie, pas génération XML), nécessaire pour continuer à satisfaire l'interface
  étendue.
- `IN_PROGRESS/TASK-191-cablage-nature-date-livraison-marchandise-ddp.md` — déplacé depuis `TASKS/`
  (voir note ci-dessous).

**Note sur le déplacement du fichier TASK** : le fichier `TASKS/TASK-191-....md` existait dans le
dépôt partagé (`D:\_vibe\GRF`) mais pas encore commité à cet endroit au moment où ce worktree isolé a
été synchronisé sur `main` (fast-forward `e4ffcf6` → `5baaa8d`) — la copie de travail de ce worktree ne
le voyait donc pas. Son contenu (lu directement dans le dépôt partagé, identique à celui cité dans le
prompt worker) a été reproduit tel quel dans `IN_PROGRESS/` de ce worktree, plutôt qu'un `git mv`
impossible sur un fichier absent de cette branche — même résultat final (le contenu de la TASK est
présent et déplacé hors de `TASKS/` pour la durée du traitement), décision documentée pour
transparence plutôt que silencieuse.

## Tests ajoutés

**Niveau calcul pur (`Declaration.Core.Tests/DeclarationDelaiPaiementLigneCalculatorTests.cs`, 3
tests)** :
1. `NatureEtDateLivraisonReellesAbsentes_RepliIdentiqueALaDetteAssumee_NonRegression` — aucun des 2
   paramètres fourni (équivalent société non configurée) → `NatureMarchandise = ""`,
   `DateLivraisonMarchandise = DateEmission` (repli strictement inchangé).
2. `NatureMarchandiseSeulePresente_DateLivraisonReplieSurDateEmission` — un seul des 2 champs
   résolu (config partielle / valeur partiellement absente sur le document) → chaque champ se replie
   INDÉPENDAMMENT, jamais d'exception croisée.
3. `NatureEtDateLivraisonReelles_PresentesEtNonNulles_ReflèteLesValeursReelles` — les 2 valeurs
   réelles fournies → le modèle porte la vraie valeur, `DateLivraisonMarchandise ≠ DateEmission`
   explicitement vérifié (pas un faux positif si le repli avait été appliqué par erreur).

**Niveau orchestration bout en bout, XML réellement écrit sur disque
(`Declaration.Orchestration.Tests/Task133GenerationFichierDelaiPaiementTests.cs`, 3 tests, chaîne
`FakeRepository` → `DeclarationDelaiPaiementService` → `DeclarationDelaiPaiementGenerationService` →
`DeclarationDelaiPaiementXmlExporter`, fichier XML relu après génération)** :
1. `GenererFichierAsync_SansConfigMarchandise_NatureVideEtDateLivraisonEgaleDateEmission_NonRegression`
   — `ValeursMarchandise` vide (aucune société configurée) → XML contient
   `<natureMarchandise></natureMarchandise>` et `<dateLivraisonMarchandise>2025-12-01</...>` (= DoDate
   de la ligne), identique au comportement TASK-133 d'origine.
2. `GenererFichierAsync_ConfigPresenteMaisValeurAbsenteSurDocument_MemeRepliQuAvant` — entrée
   dictionnaire présente pour `FAC001` mais avec les 2 champs `null` (config société existante, mais
   valeur introuvable/vide sur le document Sage) → même XML que le cas 1, aucune exception.
3. `GenererFichierAsync_ConfigEtValeurReellesPresentes_XmlRefleteLaValeurReelle` — entrée dictionnaire
   avec valeurs réelles (`"Materiel informatique"`, `2025-12-20`) → XML contient
   `<natureMarchandise>Materiel informatique</natureMarchandise>` et
   `<dateLivraisonMarchandise>2025-12-20</dateLivraisonMarchandise>`, et vérifie explicitement
   l'ABSENCE du fallback (`DoesNotContain` sur la date d'émission) — la valeur réelle est bien celle
   émise, pas un faux positif de coïncidence.

## Vérification base réelle (`GR_EMA_DISTRIBUTION`/`NEW_EMA DISTRIBUTION`, lecture seule stricte)

- Schéma `P_SOCIETE`/`F_DOCENTETE` confirmé (§ Clé de jointure ci-dessus).
- **Une seule société existe dans `GR_EMA_DISTRIBUTION`** : `SO_Id=1`. Ses 2 colonnes de config sont
  **NULL** :
  ```
  SO_Id | SO_ErpDb             | SO_ColValueNumRegistreCommerceFournisseur | SO_ColValueNatureMarchandise | SO_ColValueDateLivraisonMarchandise
  1     | NEW_EMA DISTRIBUTION | NULL                                      | NULL                         | NULL
  ```
  **Aucune société réelle n'a la configuration renseignée à ce jour** — cohérent avec le CDC (« mapping
  existe mais n'a jamais été branché ») et avec la dette documentée par TASK-133.
- Conséquence honnête : la preuve en base réelle porte sur la **non-régression** (avec la config NULL
  de la société réelle SO_Id=1, le code emprunte exactement la branche « dictionnaire vide, aucun
  appel Sage » — vérifiable en relisant le code, comportement identique à avant) et sur la **validité
  de la clé de jointure** (cas `FC2501193` ci-dessus, concordance `DO_Piece`/`DO_Domaine`/`DO_Tiers`
  entre les deux bases). Le **câblage effectif** (cas 3, config + valeur réelle) est prouvé par les
  tests automatisés (fakes reproduisant fidèlement le contrat du repository réel) plutôt que par une
  société réelle configurée, car aucune ne l'est dans cette base — reflet honnête, pas une lacune
  dissimulée.

## Build

`dotnet build DeclarationTVA.slnx` (solution complète) → **OK, 0 erreur**. Un process
`Declaration.API.exe` (PID 9408) était actif au moment du build mais **n'a pas bloqué** cette fois
(aucun verrou de fichier rencontré) — build direct sans contournement nécessaire. Warnings identiques
et préexistants uniquement (aucun nouveau warning introduit par ce changement).

## Tests

- `Declaration.Core.Tests` → **218/218** verts (dont les 3 nouveaux, cf. § Tests).
- `Declaration.Export.Xml.Tests` → **26/26** verts (aucune régression sur la structure XML —
  `<natureMarchandise></natureMarchandise>` par défaut toujours présent dans les tests existants qui
  ne renseignent pas `NatureMarchandise`, car la propriété a une valeur par défaut `string.Empty`).
- `Declaration.Orchestration.Tests` → **228/228** verts (dont les 3 nouveaux, cf. § Tests).
- `Declaration.Export.Excel.Tests` → **3/3** verts (non touché par cette TASK, revérifié par
  précaution).
- `Declaration.Selection.Tests` → **59/60** — 1 échec **préexistant et non lié**
  (`IntegrationRegressionTests`, échec d'authentification SQL Windows locale
  `IHEB-PC\ihebc`, documenté depuis TASK-186 dans plusieurs VERIFY antérieurs, aucun rapport avec
  cette TASK).
- `Declaration.Controle.Tests` → 1/2 — 1 échec **préexistant et non lié** (`ComparateurTests`,
  dépendance à une déclaration GRFN spécifique en base, module `Declaration.Controle` non touché par
  cette TASK, vérifié par précaution uniquement).

Aucune suite `Declaration.Infrastructure.Tests` n'existe dans ce dépôt (le repository DDP, comme
`GetIdentitesFiscalesTiersAsync` avant lui, n'a pas de test automatisé direct au niveau Infrastructure
— seulement une vérification manuelle base réelle, documentée ci-dessus, et une couverture complète
via les fakes aux niveaux Core/Orchestration).

## Validation checklist

- [x] Build 0 erreur (solution complète).
- [x] Tests verts sur les 3 suites explicitement requises par le prompt (`Declaration.Core.Tests`,
      et les suites couvrant `Declaration.Export.Xml`/`Declaration.Infrastructure` — `Export.Xml.Tests`
      26/26 ; pas de suite `Infrastructure.Tests` dans ce dépôt, cf. ci-dessus) + `Orchestration.Tests`
      228/228 par cohérence (le câblage bout en bout y est exercé).
- [x] Clé de jointure identifiée et vérifiée empiriquement AVANT tout code (schéma réel + cas
      `FC2501193` + vérification d'unicité `(DO_Piece, DO_Domaine=1)`).
- [x] Aucune régression sur le comportement actuel quand la config est absente — vérifié à 2 niveaux
      (calcul pur ET bout en bout XML) + confirmé par la société réelle SO_Id=1 (config NULL,
      emprunte la même branche).
- [x] Quand la config est présente et la valeur existe, le XML généré reflète la valeur réelle (test
      dédié, avec vérification explicite de l'absence du fallback).
- [x] Aucune modification de schéma (uniquement des `SELECT`, aucune colonne/table créée).
- [x] Aucun bypass sécurité (lecture seule stricte sur Sage, whitelist de nom de colonne réutilisée
      à l'identique, aucun secret codé en dur).
- [x] Aucune dette technique silencieuse — le point additionnel (propriété manquante sur le modèle,
      hardcode dans l'exporter plutôt que documenté comme prévu) a été corrigé et documenté, pas
      contourné.

## Impacts détectés

- Aucun impact sur le contrôle bloquant IF/ICE (TASK-132) — ces 2 champs n'en font pas partie,
  confirmé par lecture du code (`ControleIdentiteFiscaleDelaiPaiement.cs` non touché, non référencé
  par ce changement).
- `Declaration.Export.Xml.DeclarationDelaiPaiementXmlExporter` : le changement de
  `<natureMarchandise></natureMarchandise>` (hardcode) vers une lecture du modèle est un changement de
  COMPORTEMENT OBSERVABLE potentiel si un appelant construisait déjà un `FactureHorsDelaiXml` avec
  l'intention (non exploitée jusqu'ici, car la propriété n'existait pas) de porter une valeur — vérifié
  qu'aucun appelant existant (hors les tests mis à jour) ne construit ce type ailleurs dans le code de
  production (`DeclarationDelaiPaiementGenerationService` est le seul point de construction réel).
- Signature de `DeclarationDelaiPaiementLigneCalculator.Calculer` étendue par 2 paramètres OPTIONNELS
  en fin de liste (valeur par défaut `null`) — aucun appelant existant (production ou tests) cassé,
  vérifié par la compilation complète et les 218 tests Core verts.

## Notes worker

- Le fichier `TASKS/TASK-191-....md` n'était pas encore commité dans le dépôt partagé au moment où ce
  worktree isolé a démarré ; le worktree a d'abord été mis à jour (`git merge --ff-only main`, avance
  rapide propre de `e4ffcf6` à `5baaa8d`) pour disposer de tout le code DDP (TASK-127 à 136) avant de
  commencer — sans cette étape, aucun des fichiers cités par la TASK n'aurait existé dans cette copie
  de travail. Le contenu de la TASK elle-même a été reproduit dans `IN_PROGRESS/` (cf. § Fichiers
  modifiés) faute de pouvoir la déplacer depuis un état qu'elle n'atteignait pas encore sur cette
  branche.
- 3 fichiers `VERIFY/TASK-006_verify.md`/`TASK-007_verify.md`/`TASK-024_verify.md` apparaissent modifiés
  dans `git status` de ce worktree mais **n'ont pas été touchés par ce travail** (contenu jamais lu ni
  édité pendant cette session) — laissés strictement en l'état, non inclus dans le commit de cette
  TASK, pas expliqués plus avant ici (hors périmètre).
- Décision de conception : les 2 champs de `ValeursMarchandiseErp` sont tous deux nullables et
  INDÉPENDANTS l'un de l'autre (une société peut configurer une seule des deux colonnes) — chaque champ
  se replie séparément dans `DeclarationDelaiPaiementLigneCalculator.Calculer`, jamais un repli
  « tout ou rien » qui aurait perdu une valeur partiellement disponible.

## Reste à valider (NON couvert par ce VERIFY)

1. **Aucune société réelle n'a les 2 colonnes de config renseignées à ce jour** (SO_Id=1, les deux
   NULL) — le câblage effectif (cas 3) est donc prouvé par les tests automatisés (fakes fidèles au
   contrat), PAS par une génération réelle contre une société Sage réellement configurée. Dès qu'une
   société cliente configurera ces colonnes, une vérification en conditions réelles (comme celles déjà
   faites pour NumRc/IF/ICE dans TASK-132/133) resterait à faire — non bloquant pour cette livraison
   (même situation que documentée dans TODO.md réserve n°1 pour IF/ICE, colonnes existantes mais peu
   renseignées en pratique).
2. Le comportement de Dapper quand la colonne configurée par une société n'est PAS d'un type
   directement convertible en `string`/`DateTime` (ex. une société configurerait par erreur une colonne
   numérique) n'a pas de test dédié — même risque déjà accepté pour le pattern NumRc répliqué à
   l'identique, pas une régression introduite par cette TASK.

## Verdict

Les 4 points du périmètre strict sont livrés et testés à 2 niveaux (6 tests dédiés : 3 calcul pur +
3 orchestration bout en bout XML sur disque), plus un point additionnel découvert et corrigé (propriété
manquante sur le modèle, hardcode exporteur). Clé de jointure `F_DOCENTETE.DO_Piece = RT_ECHEANCE.DO_Numero
AND DO_Domaine=1` identifiée AVANT tout code et vérifiée empiriquement sur la base réelle (schéma,
cas `FC2501193`, unicité `(DO_Piece, DO_Domaine=1)`) — aucun blocage rencontré, aucune jointure
approximative nécessaire. Build solution complète 0 erreur, tests verts sur toutes les suites
requises (Core 218/218, Export.Xml 26/26, Orchestration 228/228), 2 échecs préexistants non liés
revérifiés et confirmés sans rapport avec cette TASK (Selection 59/60, Controle 1/2). Aucune société
réelle n'ayant la configuration renseignée dans `GR_EMA_DISTRIBUTION`, la preuve en base réelle porte
sur la clé de jointure et la non-régression ; le câblage effectif (config + valeur réelle) est prouvé
par les tests automatisés, signalé honnêtement en § Reste à valider plutôt que présenté comme vérifié
en conditions réelles.
