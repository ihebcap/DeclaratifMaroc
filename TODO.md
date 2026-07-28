# TODO — Module Déclaration TVA (GRF)

## 🐞 « Date Facture » erronée dans la Déclaration (grilles + export Excel) — signalée via un test comptable sur factures 2025 (PO 27/07/2026)
Signalement PO, déclenché par un retour comptable : « il manque les factures de 2025 » sur la
Déclaration. Diagnostic architecte (lecture code + vérification base réelle `GR_EMA_DISTRIBUTION`,
lecture seule) : **la sélection n'a aucun défaut** — la règle « date de rapprochement » (TASK-062,
§1quater/quinquies `MODULE_DECLARATION_TVA.md`) fonctionne correctement, confirmée sur données réelles
(352 factures fournisseur datées 2025, toutes rapprochées uniquement en 2026 — aucun rapprochement 2025
n'existe dans cette base —, non encore déclarées, donc légitimement candidates à une déclaration 2026).
Le vrai défaut est un **champ d'affichage faux** : la colonne « Date Facture » (grilles + export Excel,
feuille « Factures à déclarer ») montre en réalité `RT_AFFECTATION.AF_Date` (date d'enregistrement de
l'affectation) au lieu de `RT_ECHEANCE.DO_Date` (date réelle de la facture) — vérifié sur le cas concret
`FC2501193` fourni par le PO (`DO_Date` réel = 18/07/2025 ; valeur affichée 15/04/2026 = `AF_Date` exacte
de son affectation) et confirmé sur 4 autres échantillons. Cause : `SelectionExpliqueeService.cs`
(chemin règlement-first uniquement) aliase `A.AF_Date AS DateFacture` au lieu de `E.DO_Date AS
DateFacture`, alors que `RT_ECHEANCE E` est déjà jointe. Le chemin facture-first (TASK-050) est déjà
correct, non concerné. Conséquence : en cherchant les factures par année réelle, aucune ligne 2025
n'apparaît jamais dans cette colonne — d'où l'impression de factures manquantes, alors qu'elles sont
bien sélectionnées.

Demande PO complémentaire (même session de test) : ajouter la colonne `RT_ECHEANCE.DO_Reference` à
l'export, absente de bout en bout de la chaîne Déclaration (existe déjà côté écran Factures, TASK-041).
Puis une 3ᵉ demande (même session) : ajouter `RT_MOUVEMENT.MV_Piece`/`MV_Echeance` à la feuille
« Règlements sélectionnés » — `MV_Echeance` est déjà lu ailleurs dans le pipeline (juste à relier),
`MV_Piece` nécessite un `SELECT` neuf.

> ✅ **TASK-186 approuvée** (27/07/2026, revue architecte complète : diff `3fb550f` relu intégralement
> — 3 lignes changées, rien d'autre —, rejeu SQL indépendant confirmant `FC2501193` passe bien de
> `AF_Date`=15/04/2026 à `DO_Date`=18/07/2025, build individuel des 5 projets concernés + 4 suites de
> tests **rejouées indépendamment par l'architecte** — Core 89/89, Export.Excel 3/3, Selection 59/60
> (même échec préexistant non lié, auth SQL locale), Orchestration 189/189 — process `Declaration.API.exe`
> verrouillant le build solution confirmé être le même PID 33068 que documenté par le worker, donc
> blocage environnemental réel et non fabriqué). Voir `DONE_DETAIL/TASK-186-...md`.
>
> ✅ **TASK-188 approuvée** (27/07/2026, même revue) — colonnes « N° Pièce »/« Échéance » ajoutées à la
> feuille « Règlements sélectionnés », chemin réellement câblé par l'API (`ConstruireModeleControleAsync`
> → `GetReglementsRapprochementAsync`, aucun gap architecture). Diff `2211d03` relu intégralement,
> statistique `MV_Piece` vide (7,5 %, 110/1462) revérifiée indépendamment et confirmée exacte, échantillon
> `Reference`/`MvPiece` sur cas réels revérifié. Voir `DONE_DETAIL/TASK-188-...md`.
>
> ⚠️ **TASK-187 approuvée pour son périmètre écrit** (27/07/2026) — la propagation `Reference` (diff
> `1f55fcd`) est livrée et testée correctement de bout en bout jusqu'à
> `ConstructeurDeclaration`/`Exporter.cs`. **Trou fonctionnel comblé par TASK-189 ci-dessous** (le champ
> n'était pas persisté sur `DM_LGTVA`, donc jamais lu par les deux méthodes réellement câblées côté API).
>
> ✅ **TASK-189 approuvée** (27/07/2026, revue architecte complète : diff `20405ee` relu intégralement —
> migration additive `DM_LGTVA.Reference` (NVARCHAR(200) NULL) **rejouée une 3ᵉ fois indépendamment par
> l'architecte** sur `GR_EMA_DISTRIBUTION` → idempotente confirmée, type/nullabilité de la colonne
> revérifiés en base, 5148 lignes existantes confirmées lisibles avec `Reference = NULL`. Câblage des 5
> points du périmètre (`LigneCandidate`, `SaveLignesCandidatesAsync`, `MapLignesCandidates` 2 branches,
> `ConstruireModeleExportAsync`/`ConstruireModeleControleAsync`) relu et cohérent. Point additionnel
> légitime détecté par la recherche exhaustive demandée par la TASK et corrigé :
> `RecalculerLigneDepuisCacheAsync` (TASK-147) aurait silencieusement effacé une `Reference` déjà
> persistée lors d'un recalcul de ligne — vérifié conforme au même patron que `CodeActivite` déjà en
> place au même endroit. Tests **rejoués indépendamment par l'architecte** : Core 89/89, Selection 59/60
> (même échec préexistant non lié), Orchestration 196/196 (189 existants + 7 nouveaux). Voir
> `DONE_DETAIL/TASK-189-...md`.
> ⚠️ **Suivi opérationnel (27/07/2026, après-coup)** : le PO a testé en conditions réelles et confirmé
> le symptôme persistant — cause identifiée par l'architecte, **pas un défaut de code** :
> 1. Le service Windows `DeclaratifMaroc` installé (`C:\Program Files\APBS\Declaratif Maroc\`) était
>    resté sur un binaire du 24/07/2026 (antérieur aux 4 correctifs), **arrêté** au moment du contrôle.
>    Le PO a relancé `Deploy-All.ps1` (build frais confirmé : `deploy\Declaration.Selection.dll`
>    horodaté après le commit TASK-189) — **il reste à exécuter `installer\DeclaratifMaroc.exe` en
>    administrateur** pour que l'installation soit réellement mise à jour, puis démarrer le service.
> 2. **Même une fois l'API à jour, les lignes `DM_LGTVA` des 6 déclarations déjà existantes
>    (`TVA1-2026-01` à `06`, 5148 lignes, toutes `EnCours`) resteront fausses** : `DateFacture`/
>    `Reference` sont figées à l'écriture et jamais recalculées par le code existant (vérifié :
>    chargement, revalidation, resynchronisation ne les retouchent pas). **Décision PO (27/07/2026) :
>    backfill rétroactif demandé, limité aux déclarations `EnCours`** (jamais Clôturée/Déposée,
>    cohérent avec la doctrine déjà appliquée TASK-094) → **TASK-190 ouverte ci-dessous**.

> ✅ **TASK-190 approuvée** (27/07/2026, revue architecte complète : diff `4bda5b4` relu intégralement
> — écriture strictement limitée à `DateFacture`/`Reference`, endpoint gardé `UT_Admin=1`, garde-fou
> Clôturée/Déposée dans le code, pas seulement dans le rapport). **Exécution réelle vérifiée
> indépendamment sur `GR_EMA_DISTRIBUTION`** : requête de comparaison directe rejouée par
> l'architecte → 5148/5148 lignes `EnCours` avec `Reference` exacte (100 %), `DateFacture` exacte à la
> seconde sur 5106/5148 (99,2 %), les 42 restantes (0,8 %) avec un écart de **333 microsecondes**
> confirmé et compris (rounding client .NET DateTime ↔ `datetime`/`datetime2` SQL Server, sans impact
> — identiques à la seconde près, `DateFacture` n'étant jamais consommée à une précision supérieure).
> `FC2501193` revérifié directement : `DateFacture=2025-07-18`, `Reference=NULL` (attendu), HT/Taux/
> TVA/TTC/Etat confirmés inchangés. Les 6 déclarations reconfirmées `EnCours` (aucune touchée en
> dehors de ce statut). Build solution complète et 3 suites de tests **rejouées indépendamment** :
> Core 89/89, Selection 59/60 (échec préexistant non lié), Orchestration 205/205 — plus aucun verrou
> de process (confirmé). Voir `DONE_DETAIL/TASK-190-...md` et `DONE_DETAIL/TASK-190_verify.md`.
>
> ⚠️ **Précision PO (27/07/2026)** : l'exécution ci-dessus a eu lieu sur la base de référence de ce
> poste de dev (`GR_EMA_DISTRIBUTION`) — **le backfill doit être rejoué séparément sur le serveur du
> client réel** une fois l'endpoint livré là-bas (pipeline habituel `Deploy-All.ps1` →
> `installer\DeclaratifMaroc.exe` → installation). Refaire sur place les mêmes vérifications
> (déclarations bien `EnCours`, dry-run d'abord, rejeu à vide en 2ᵉ passage) — ne pas supposer que
> l'état constaté ici (6 déclarations, toutes `EnCours`) se reproduit chez le client.

## 🐞 Drill « Codes activité » (écran ③) : impossible de basculer Achats/Ventes sans sortir (signalement PO 24/07/2026, capture d'écran)
Signalement PO : dans le drill « Codes activité » ouvert depuis l'onglet Déductible (Achats), seule la
grille Achats est accessible — aucun moyen de voir/affecter les codes activité côté Encaissement
(Ventes) sans quitter le drill. Diagnostic architecte (code + base réelle `GR_EMA_DISTRIBUTION`) :
**pas un bug de mapping ni de sauvegarde** — référentiel, endpoint et persistance sont déjà symétriques
entre les deux domaines (57 codes Encaissement / 39 Decaissement en base, 3893 lignes Encaissement déjà
dans `DM_LGTVA`). La cause réelle est un pur trou d'ergonomie : le bandeau du drill
(`VerifierIntegrerPanel.tsx:636-666`) n'offre qu'un bouton « ← Retour au contrôle », les deux onglets
Achats/Ventes qui pilotent le domaine étant rendus uniquement dans la vue principale, masqués dès qu'un
drill est ouvert — obligeant à sortir puis rentrer par l'autre onglet pour changer de domaine.

> ✅ **TASK-183 approuvée** (24/07/2026, revue architecte complète : diff `0f9084f` relu intégralement
> — seul le bloc conditionnel `kind === 'codeActivite'` ajouté, aucun fichier `.cs` touché —, `tsc -b`
> et `vite build` rejoués indépendamment → 0 erreur). Voir `DONE.md`.
> ⚠️ **Point hors périmètre découvert pendant les tests, à tracer séparément** : les filtres
> texte/nombre (`factureNumero`, `tiers`, `montantHT`, `montantTTC`) envoyés par `DomainGrid.tsx` au
> `GET .../lignes` sont silencieusement ignorés côté back (`BuildLigneFilterWhere`) — seuls
> `numeroRapprochement`/`source`/`tauxTVA`/`origine` fonctionnent réellement (TASK-067B). Affecte les
> 4 kinds de drill + l'écran principal. Aucune TASK ouverte à ce stade, à arbitrer par le PO.

> ✅ **TASK-184 approuvée** (24/07/2026, revue architecte complète : diff intégral du commit
> `1b2b896` relu, build solution + 3 suites de tests rejouées indépendamment — 3/3, 52/52, 182/182 —,
> `.xlsx` de preuve réinspecté programmatiquement, bloc « Contrôle d'équilibre » absent confirmé,
> colonne « Domaine Activité » confirmée). Voir `DONE.md`.
> ✅ **TASK-185 approuvée** (24/07/2026, correction de TASK-184 : colonne « Domaine Activité »
> retirée — hors sujet —, lignes « Total Collecté »/« Total Deductible » ajoutées en pied de
> tableau. Revue architecte complète : diff intégral du commit `d288fce` relu, build + 3 suites de
> tests rejouées indépendamment — 3/3, 52/52, 182/182 —, `.xlsx` de preuve réinspecté
> programmatiquement, valeurs cohérentes avec TASK-184/TASK-182). Voir `DONE.md`.

## 🗺️ ROADMAP — V2 : source du détail TVA = grand livre comptable, indépendance vis-à-vis de Sage (décision PO 24/07/2026, pas une TASK prête)
Décision PO : dans une **deuxième version** du module, le détail TVA (assiette/taux/TTC par facture)
sera lu depuis un **grand livre comptable**, à la place des Objets Métier Sage (`SageTaxReader.Core`)
utilisés en V1. Objectif : ne plus dépendre de Sage.

État actuel (V1) : seule la **sélection/éligibilité** est déjà indépendante de Sage (rapprochement
local `RT_MOUVEMENT.MV_Point`, §1quinquies `MODULE_DECLARATION_TVA.md`) ; le détail TVA reste couplé
à Sage BO **par choix explicite du PO**, pour obtenir les montants exacts calculés par Sage sans
réintroduire d'arrondi. L'isolation existe déjà côté code (`SageTaxReader.Contracts` = DTO frontière,
worker out-of-process) — un nouvel adaptateur V2 pourrait s'y brancher sans retoucher le domaine.

**Point de vigilance avant de lancer la V2** : le grand livre comptable ciblé doit exposer la
ventilation TVA **par facture et par taux**, pas seulement des totaux de compte agrégés — sinon la
reconstruction rejoue le risque de dérive de centimes déjà identifié comme bug dans l'ancien GRFN
(§2 `MODULE_DECLARATION_TVA.md`).

Précision PO (24/07/2026) : le principe envisagé = **un compte général affecté à chaque taux de TVA**
(mapping compte↔taux, ex. sous-comptes 345x par taux) + un **contrôle de cohérence** (probablement
Σ TVA recalculée vs Σ mouvements du compte, sur le même principe que le contrôle d'équilibre déjà
existant en V1). Question ouverte non tranchée : l'**assiette HT** par taux vient-elle de la même
écriture que le compte de TVA, ou d'un compte contrepartie distinct à apparier (pièce/journal) ? Et
ce mapping compte↔taux est-il **configurable par société** (cohérent avec le principe multi-client
« tout configurable, rien en dur » du CDC) ou fixe ?

Pas de TASK ouverte : contexte insuffisant (schéma exact du grand livre cible, client concerné,
échéance, réponses aux deux questions ci-dessus). À transformer en TASK complète (tous champs
obligatoires) dès que ces éléments seront précisés par le PO.

**Extension confirmée au module Délai de Paiement (PO 28/07/2026)** : même principe d'indépendance
vis-à-vis de Sage à terme, appliqué cette fois au module DDP — GRF deviendra une source parmi d'autres,
le grand livre comptable s'ajoutant comme nouvelle source. Voir la réserve n°6 et
[TASK-191](TASKS/TASK-191-cablage-nature-date-livraison-marchandise-ddp.md) dans la section DDP
ci-dessous — même remarque de vigilance : pas d'abstraction multi-source anticipée tant que le schéma
cible n'est pas précisé.

## 🗺️ ROADMAP — écran ③ Vérifier & Intégrer (+ écran ② Affectations) : trop de boutons d'action distincts (ressenti PO 24/07/2026, à évaluer, pas une TASK prête)
Ressenti PO : l'écran est devenu compliqué à l'usage. Vérification architecte : **confirmé** — rien
que pour « resynchroniser une pièce Sage », il existe déjà 4 chemins distincts, livrés task par
task sans jamais être reconsolidés : (1) « Diagnostiquer » (écran ③, par ligne en anomalie) → ouvre
`DiagnosticModal` → bouton « Relire depuis Sage » ; (2) « Resynchroniser » direct par ligne (écran
③, `VerifierIntegrerPanel.tsx`/`DomainGrid.tsx`, TASK-170) ; (3) « Corriger / Resynchroniser »
(écran ② Affectations, `AffectationsDrill.tsx`) ; (4) bientôt un bulk (TASK-176). S'y ajoutent, à
côté : « Valider l'incohérence » (écran ②), 2 actions en masse existantes (état, code activité
TASK-173), « Recalculer depuis cache ». Chaque ajout est individuellement justifié par sa TASK
d'origine, mais l'accumulation rend l'écran dense — symptôme classique de fonctionnalités greffées
une par une sans repasse de consolidation.
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-178](TASKS/TASK-178-consolidation-boutons-action-ligne-verifier-integrer.md) | Consolider les 4 chemins vers « Resynchroniser » + rapprocher « Valider l'incohérence » de l'écran ③ — 3 arbitrages de conception documentés, à trancher par le PO avant tout code. | ⏸️ **en attente d'arbitrage PO** — ne pas coder avant, ne pas prioriser devant TASK-175/176/177. |

## 🔴 CRITIQUE — 500 au chargement des lignes en production, incohérence validée reste bloquante, resync ligne par ligne (signalement PO 24/07/2026, base client)
Trois signalements simultanés du PO sur la base de **production** (pas dev), écran ③ Vérifier &
Intégrer. Diagnostic architecte (lecture code) pour les trois :
1. **500 sur `GET {id}/lignes`** (Encaissement, puis après suppression+réintégration de la
   déclaration par le PO, Decaissement aussi) : le chargement en parallèle des deux domaines
   (`Promise.all` front) fait entrer en contention le verrou `soId` de TASK-156
   (`ExecuterAvecVerrouOMAsync`) — exception prévue pour devenir un 409 (commentaire de conception
   explicite), mais `DeclarationsController.GetLignes` est le seul des 4 appelants à ne PAS catcher
   `InvalidOperationException` → 500 générique au lieu de 409. C'est exactement la réserve non
   bloquante déjà tracée sous TASK-156 (« non vérifié empiriquement »), désormais confirmée en
   conditions réelles.
2. **Incohérence validée par le PO (TASK-078) reste signalée comme anomalie bloquante** : deux
   pièces citées (`FC2501717`/`RF26040040`, `FC2501667`/`RF26030075`, cas déjà connus TASK-082/
   audit TASK-143) validées via l'écran ② Affectations (`IncoherenceValidee=1` bien posé en base),
   mais `GetCheckupAsync` (contrôle « Absence d'anomalies bloquantes » de l'écran ③) ignore ce flag
   — seul `RevaliderLignesFigeesAsync` (écran ②) le respecte. La validation ne débloque donc que la
   moitié du parcours.
3. **Resynchronisation ligne par ligne** : le PO signale devoir resynchroniser des dizaines de
   lignes une par une (151 lignes sélectionnées dans son cas) — aucun endpoint bulk n'existe vers
   `ResynchroniserLigneAsync`, contrairement aux deux autres actions en masse déjà livrées
   (`:bulk` état, `code-activite:bulk` TASK-173).

TASK-175, TASK-176, TASK-177, TASK-179 : APPROUVÉES par l'architecte (24/07/2026) — voir
`DONE.md`/`CHANGELOG.md`. TASK-171 (arbitrage préalable à TASK-179) close par la même occasion — voir
`DONE_DETAIL/TASK-171-cascade-code-activite-mapping-tiers-jamais-fonctionnel.md`.

> ⚠️ **Suivi opérationnel restant (non-code)** : confirmer que le build déployé chez le client à
> l'origine du signalement 500 inclut bien les commits TASK-175/176/177/179 (24/07/2026, 11h50-12h42) —
> sans quoi le 409 explicite de TASK-175 n'est pas non plus actif chez ce client.

> ✅ **TASK-180 approuvée** (24/07/2026, revue architecte complète : diffs réels des 4 fichiers relus
> intégralement contre le VERIFY, build solution complète rejoué indépendamment — 0 erreur (verrou
> `Declaration.API.exe` confirmé de façon identique, contournement par sortie redirigée reproduit),
> `Declaration.Core.Tests`/`Declaration.Export.Excel.Tests`/`Declaration.Orchestration.Tests` rejoués
> indépendamment → 52/3/182 verts, `.xlsx` de preuve réel inspecté au format OOXML brut (pas seulement
> lu par confiance) : 8 entrées `RecapsParTaux` (4 Collecté/4 Déductible) sur `TVA1-2026-05`, somme
> Collecté+Déductible = 2 094 301,93 = Total Déclaré TTC confirmée par calcul indépendant, format
> `numFmtId=164 → "dd/mm/yyyy"` bien appliqué aux 2 colonnes date de la feuille « Règlements
> sélectionnés », valeur brute `DateFacture` confirmée porteuse d'une partie horaire non nulle
> (46197.583...) — la correction n'est pas un no-op. Voir `DONE.md`.
> ✅ **TASK-162 approuvée** (24/07/2026, même revue) — colonne « N° Règlement » confirmée en 2ᵉ
> position dans les deux feuilles concernées par lecture directe du `.xlsx` réel (`FF260057 |
> RF26060115`), diff conforme au VERIFY, aucune régression du format date TASK-180 après le décalage
> de colonnes. Voir `DONE.md`.

## 🐞 Export Excel « Détail TVA » : libellés trompeurs du bloc « Contrôle d'équilibre » (question PO 24/07/2026)
Le PO a demandé ce que signifiait le bloc « Contrôle d'équilibre » de la feuille « Détail TVA » (export
de contrôle). Diagnostic architecte : les valeurs sont justes, mais les libellés (hérités tels quels du
chemin de dépôt légal, où ils sont corrects) sont trompeurs dans ce chemin précis — `ConstruireModeleControleAsync`
calcule `TotalMontantAffecte` comme un simple **Total HT** (pas un vrai montant de règlement affecté) et
code `ResiduExplique` en dur à 0, ce qui fait que « Résidu Non TVA » n'est en réalité rien d'autre que
**−Total TVA**. Décision PO : ne pas recalculer, **juste corriger les libellés** dans ce chemin
(l'export de dépôt, dont les libellés sont déjà exacts, n'est pas concerné).

> ✅ **TASK-182 approuvée** (24/07/2026, revue architecte complète : diff `3fd4ab9` relu intégralement,
> `CreerFeuilleRecap` confirmée intacte, build solution complète + `Declaration.Export.Excel.Tests`/
> `Declaration.Orchestration.Tests` rejoués indépendamment — 3/3 et 182/182 —, `.xlsx` de preuve réel
> inspecté au format OOXML brut par l'architecte : « Total HT »/« Écart HT − TTC (= −Total TVA) »
> confirmés en place, valeurs inchangées = −ΣTVA sur `TVA1-2026-05`). Voir `DONE.md`.
> ⚠️ Réserve non bloquante : formulation exacte du 2ᵉ libellé retenue sans retour PO préalable (la
> TASK autorisait un équivalent) — à confirmer, ajustement resterait un simple changement de texte.

## 🔎 Échec d'écriture cache de ventilation observé en prod — `Nom d'objet 'DM_VENTILATION_SAGE_CACHE' non valide` (signalement PO 23/07/2026, log 22:57:16)
Log cité par le PO : `[VALO] EC_Id=24184 pièce FF260096 — échec écriture cache : Nom d'objet
'DM_VENTILATION_SAGE_CACHE' non valide.` Diagnostic architecte (lecture code, pas d'accès à
l'environnement source du log) :
- **Non bloquant** : ce message vient d'un `catch` explicite autour du seul `UpsertEntries`
  (`OrchestrateurDeclaration.cs:217-222`, pattern répété à 6 endroits pour isoler l'écriture cache du
  reste du traitement) — la valorisation Sage/OM de la pièce **a réussi** (sinon le message serait
  « échec écriture sentinelle d'erreur », pas « échec écriture cache »), seule l'écriture en cache a
  échoué. Effet de bord : cette pièce sera **relue intégralement via OM/Sage à chaque cycle** tant que
  le cache reste inutilisable (pas de perte de donnée déclarative, mais risque de contention/perf —
  même famille de risque que celui déjà documenté ci-dessous pour TASK-156).
- **Cause racine** — « Invalid object name » est l'erreur SQL Server levée quand la table n'existe pas
  (ou pas sous ce nom) dans la base ciblée par `PersistenceConnection`. Le code C#
  (`VentilationSageCacheRepository.cs`) référence `DM_VENTILATION_SAGE_CACHE` depuis TASK-118
  (renommage `GRC_VENTILATION_SAGE_CACHE` → `DM_VENTILATION_SAGE_CACHE`), mais ce renommage n'est
  appliqué que par une migration **idempotente mais manuelle** (`DeclarationTVA.sql`, section 1f,
  `sp_rename` gardé par `IF OBJECT_ID(...)`) — exécutée via `sqlcmd` par un opérateur, **pas encore
  automatique** (TASK-126, toujours à faire, dépend de TASK-115). Sur l'environnement qui a produit ce
  log, soit ce script n'a jamais été rejoué depuis le déploiement du code TASK-118 (table encore nommée
  `GRC_VENTILATION_SAGE_CACHE`, ou table de persistance jamais initialisée), soit la base pointée par
  `PersistenceConnection` de cet environnement n'est simplement pas celle sur laquelle le script a été
  exécuté. Impossible de trancher entre ces deux sans savoir de quel environnement provient ce log
  (dev `DESKTOP-5BFKKEP`, où TASK-118 a bien rejoué la migration avec succès, ou un autre poste/client).
- **Action immédiate recommandée (aucun code à changer)** : sur l'environnement d'où provient ce log,
  rejouer `DeclarationTVA.sql` (section 1f) via `sqlcmd` contre la base `PersistenceConnection` réelle
  de cet environnement — script idempotent, sans risque sur les données existantes (même principe que
  TASK-065/118, déjà vérifié en conditions réelles). **Ceci est exactement le scénario que TASK-126
  (exécution automatique du script par le setup) vise à éliminer** — cet incident réel en est une
  preuve concrète, à prendre en compte si le PO réévalue la priorité de TASK-126 (actuellement bloquée
  sur TASK-115).
- Pas de nouvelle task de correction de code ouverte ici : aucun défaut applicatif identifié, uniquement
  un script de migration non rejoué sur un environnement donné — action opérationnelle, pas
  développement. Si le PO confirme que l'environnement concerné a bien la migration TASK-118 à jour et
  que l'erreur persiste malgré tout, ce diagnostic serait à rouvrir (indiquerait alors une cause non
  identifiée ici).

## 🐞 Popup « Colonnes » ouvert hors écran (signalement PO 23/07/2026, capture écran ① Sélection)
Signalement PO : clic sur le bouton « Colonnes » (pied de grille, écran ① Sélection) → le popup de
sélection de colonnes s'ouvre vers le bas et se retrouve caché sous le bas de la fenêtre, inutilisable.

> ✅ **TASK-158 approuvée rétroactivement** (27/07/2026, revue architecte de recadrage `TODO.md` vs
> code source demandée par le PO) — le correctif (flip vertical dans `ColumnSelector.tsx`) s'est
> avéré **déjà livré en code** le 23/07/2026 (commit `cdb37e8a`), noyé sous le libellé `TASK-161`,
> jamais tracé via `IN_PROGRESS`/`VERIFY`. `npx tsc -b`/`npx vite build` rejoués indépendamment →
> 0 erreur, périmètre confirmé limité à `ColumnSelector.tsx`. Voir `DONE.md`.
> ⚠️ Réserve non bloquante : pas de reproduction visuelle réelle du cas PO dans cette revue.
> **Note d'hygiène process** : 2ᵉ cas de correctif livré hors cycle déclaré (après TASK-176/commit
> `99ef0fc`) — à surveiller si ça se reproduit une 3ᵉ fois.

## 🐞 Contention rafraîchissement valorisation OM (signalement client 23/07/2026, log serveur)
Signalement client : `[VALO] batch OM en exception, repli individuel : Timeout lors de l'exécution du worker
en mode batch.` à deux reprises. Diagnostic architecte sur le log complet : **aucune perte de données**
(repli individuel TASK-023/072 a fonctionné), mais deux causes racines confirmées en code — (A) aucun
verrou/anti-rebond sur `RafraichirValorisationAsync` (bouton « Rafraîchir » écran Factures) → 4 cycles
concurrents observés sur le même `soId`, compétition pour la même session OM/Sage jusqu'au timeout batch
(300 s, exact) ; (B) `TryServireDepuisCache` ne sert jamais le cache pour une facture sans paiement pointé
(`currentToken == null`) → relecture OM systématique du même jeu de factures non payées à chaque cycle,
contredisant le commentaire du code lui-même. Contournement immédiat validé avec le client : redémarrage
propre du service (WinSW), sans risque de perte (effet de bord cache uniquement, aucune écriture de
déclaration par ce chemin).

> ✅ **TASK-156 approuvée** (23/07/2026, revue architecte complète : VERIFY relu, diff des 6 fichiers
> vérifié indépendamment, build+tests rejoués) — verrou anti-chevauchement `soId` étendu en cours de
> route aux 4 appelants réels de l'orchestrateur OM (pas seulement le bouton Rafraîchir, cf.
> incident réel du 23/07 14:20 sur le chemin déclaration) + cache OM servi indépendamment du token de
> paiement (facture non payée stable = 1 seule lecture OM, plus de relecture systématique). Voir
> `DONE.md`.
> ⚠️ **Réserves non bloquantes à surveiller** (documentées dans le VERIFY, non résolues) : (1) le
> rejet 409 explicite côté front n'existe que sur le bouton Rafraîchir — les 3 autres chemins
> (chargement d'une déclaration, réintégration, resynchronisation) renverront une erreur générique en
> cas de rejet ; (2) **risque de contention nouvelle** au premier chargement d'une déclaration si le
> front appelle en parallèle « Decaissement » et « Encaissement » pour le même `soId` — l'un des deux
> onglets peut désormais échouer au chargement (409) là où il aurait auparavant réussi en silence
> (au prix, avant ce correctif, d'une double lecture OM). Non vérifié empiriquement (pas d'accès à
> l'écran réel en environnement de build) — à confirmer en conditions réelles ; si constaté, le
> contournement est un simple rechargement (le premier onglet aura fini).

## 🔧 WinSW en version pré-release : arrêt du service laisse des process orphelins (signalement client 23/07/2026)
Signalement client : arrêt du service `DeclaratifMaroc` en échec (`InvalidOperationException` dans
`WinSW.WrapperService.OnStop → StopTree → StopDescendants`), `Declaration.API.exe` (et ses workers OM
enfants) restant actifs en arrière-plan après la demande d'arrêt — intervention manuelle nécessaire.
Cause : bug interne à **WinSW** (tiers), version vendorisée en **pré-release**
(`Get-WinSW.ps1` : `v3.0.0-alpha.11`), dans sa logique de terminaison de l'arbre de process descendants.
Lié à TASK-156 (la présence d'un worker OM enfant vivant au moment de l'arrêt expose la fenêtre du bug)
sans en être une régression — défaut préexistant dans un composant tiers.

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-157](TASKS/TASK-157-winsw-version-stable-arret-service-processus-orphelins.md) | Remplacer la version pré-release de WinSW par une version stable (ou contourner sa gestion de l'arbre de descendants) — test réel obligatoire avec un worker OM vivant au moment de l'arrêt. | 🎯 **prêt** — non bloquant, contournement manuel disponible (tuer les process orphelins) en attendant. |

## 🔴 CRITIQUE — Aucun bouton d'export fonctionnel : endpoints génération/téléchargement en 501 (analyse architecte 23/07/2026)
Constat suite à TASK-137 (export XML corrigé et approuvé) : `Declaration.Export.Xml.DeclarationXmlExporter`
n'est appelé nulle part hors de ses propres tests. `Declaration.API/Controllers/DeclarationsController.cs`
expose `POST {id}/generation` et `GET {id}/fichiers/{type}` mais les deux renvoient un `501 Not
Implemented` littéral. Côté front, `GenerationPanel.tsx` (atteignable en production via
`DeclarationStepper.tsx`) a un bouton « Télécharger » qui ne fait qu'un `alert(...)`, aucun appel réseau
réel. **Un utilisateur ne peut aujourd'hui obtenir aucun fichier de dépôt (XML ni Excel) depuis
l'application.** Cartographie complète (sources de données manquantes, pattern de connexion à
réutiliser, contrainte JWT front) dans TASK-155.

**Point bloquant potentiel découvert pendant la cartographie** : aucune colonne d'identifiant fiscal de
la société elle-même n'a été repérée dans le code actuel lisant `P_SOCIETE` — le champ actuellement
utilisé ailleurs (`SocieteId`) est l'`SO_Id` interne GRF, pas l'IF réel attendu par le tag XML
`<identifiantFiscal>`. À vérifier en premier lieu sur le schéma réel de `P_SOCIETE` (étape 0 de
TASK-155) avant de pouvoir livrer un cycle complet.

> ✅ **TASK-155 approuvée** (23/07/2026) — les deux endpoints sont réellement câblés : IF société
> résolu et vérifié en base réelle (`P_SOCIETE.SO_Identifiant`, distincte de `SO_Id`), blocage
> explicite si absente/vide (jamais de placeholder, TASK-151) ; `ConstruireModeleExportAsync`
> reconstruit le `DeclarationModele` (lignes `Integree`) depuis une déclaration `Cloturée` ;
> `GenererFichiersExportAsync`/`ObtenirCheminsExportAsync` appellent les exporters XML/Excel
> existants (non modifiés) et relisent leurs chemins de façon déterministe. Écart de cartographie
> corrigé : le composant front réellement monté est `DeclarationFinalePanel.tsx` (déjà correct),
> pas `GenerationPanel.tsx` (composant mort, corrigé par hygiène). Voir `DONE.md`.
> ⚠️ Réserve non bloquante : cycle complet clôture→génération→téléchargement non rejoué sur une
> déclaration réelle (aucune `Cloturée` disponible en base de test au moment du VERIFY) — à
> confirmer par le PO sur un environnement avec une déclaration clôturée réelle.

> ✅ **TASK-154 approuvée** (23/07/2026) — `F_COMPTET` (table Sage) n'est plus jamais lu via la
> connexion GRF ni via synonyme cross-base : les 4 requêtes de `SelectionExpliqueeService.cs`
> résolvent désormais la connexion Sage dynamiquement par `SO_Id` et joignent `F_COMPTET` en
> mémoire (batch `WHERE CT_Num IN @codes`), jamais un JOIN SQL trois-parties. Voir `DONE.md`.
> ⚠️ Réserve non bloquante : `SelectionnerAffectationsService.cs` (service legacy, hors périmètre de
> TASK-154, non câblé dans `Declaration.API`) conserve encore 4 `LEFT JOIN F_COMPTET` identiques au
> défaut corrigé — inoffensif tant qu'il reste inatteignable depuis l'API, mais même cause racine
> si jamais réactivé/câblé. À traiter si le PO souhaite un jour réutiliser ce comparateur GRFN.

## 🌙 Lot session 20/07/2026 (worker nocturne) — revue architecte du 20/07/2026

> ✅ **TASK-145/146/148/149/150/151 approuvées** (20/07/2026, revue architecte complète : VERIFY
> lu, TASK d'origine relue, diff `git show` vérifié contre chaque VERIFY, purge/rapports vérifiés
> sur disque). Voir `DONE.md` pour le détail par task.

> ✅ **TASK-147 APPROUVÉE après correction** (20/07/2026) — rejet initial : le VERIFY n'avait bâti
> que `Declaration.API.csproj`, jamais `dotnet build DeclarationTVA.slnx` (solution complète), qui
> échouait avec **77 erreurs CS0535** — 7 méthodes ajoutées à `IDeclarationRepository` par TASK-144/
> TASK-147 jamais répercutées dans 11 `FakeDeclarationRepository` de
> `Declaration.Orchestration.Tests`. Correctif livré : les 7 méthodes ajoutées dans les 11 fakes
> (stub `NotImplementedException`/`Task.CompletedTask`, aucune n'étant exercée par les tests
> existants), aucun autre fichier touché. Vérifié indépendamment par l'architecte :
> `dotnet build DeclarationTVA.slnx` → 0 erreur, `dotnet test Declaration.Orchestration.Tests` →
> 137/137. Voir `DONE.md` et `VERIFY/TASK-147_verify.md` (section « CORRECTIF POST-REJET »).

> ✅ **TASK-152 CLÔTURÉE sans code** (décision PO, 20/07/2026, en réponse à l'arbitrage demandé par le
> worker) — option 1 retenue : rien de systématique, le diagnostic à la demande (TASK-144) suffit vu
> le taux de collision quasi nul (1 cas / 2 460 `EC_Id` vérifiés). Voir `DONE.md`.

> ⚠️ **Fait signalé par TASK-149, hors périmètre de correction, à investiguer par le PO** : la
> déclaration `TVA1-2026-01` a disparu de la base (`DM_ENTTVA`) entre l'audit TASK-143 et cette
> session, cause non élucidée (hors lecture seule). `TVA1-2026-02` a aussi été recréée/élargie
> (730→2005 lignes) la même nuit. Voir `DOCS/AUDIT-TASK-143/01-reaudit-post-145.md`.

> ⚠️ **TASK-144** reste **BLOQUÉE** (non close) : l'architecte a vérifié lui-même 2 des 3 points
> laissés ouverts par le VERIFY du 20/07/2026 (rejeu réel `FA2600106` conforme ; schéma `F_DOCREGL`
> confirmé, jointure réelle = `RT_ECHEANCE.EC_No = F_DOCREGL.DR_No`, pas `F_DOCREGL.EC_No` qui vaut
> toujours 0). **Le 3ᵉ point (validation PO des libellés métier de `DiagnosticMotifMetier.cs`) ne
> peut pas être auto-approuvé par un worker — il attend une lecture humaine du PO.** Voir
> `VERIFY/TASK-144_verify.md`.

## 🎯 Diagnostic en ligne (demande PO 20/07/2026 — objectif TASK-143 jugé non atteint)

| # | Task | Description | Statut |
|---|------|-------------|--------|
| 1 | [TASK-144](TASKS/TASK-144-diagnostic-en-ligne-factures-non-ventilees.md) | Le comptable doit comprendre **dans l'app**, sans revenir demander à l'assistant, pourquoi une ligne est en anomalie : diagnostic en ligne (EC_Id/numéro Sage/tiers + motif d'échec OM réel + contrôle collision `DO_Numero`↔`F_DOCREGL`). | 🎯 **prêt** — fondé sur recherche de code (motif brut non enrichi, aucun contrôle collision vivant, aucun écran de preuve dédié). |

> ✅ **TASK-143 approuvée** (20/07/2026) — audit exhaustif (lecture seule) des 6 déclarations
> `TVA1-2026-01` à `06` (5 182 lignes) contre les données réelles Sage. **Verdict : aucune des 6
> déclarations n'est signable en l'état.** 14 lignes `FACTURE_NON_VENTILEE` (01:2·02:9·03:1·04:2)
> expliquent à 100 % l'écart d'équilibre global (−100 576,90 MAD) ; `TVA1-2026-06` arithmétiquement
> équilibrée mais 3 lignes tiers ZF FOOD sans Identifiant Fiscal (export XML bloqué) ;
> `TVA1-2026-05` seule déclaration propre. Nouveau contrôle « numéro de pièce dupliqué entre tiers »
> exécuté sur les 2 460 `EC_Id` référencés : 1 seul cas (`FA2600106`, déjà diagnostiqué en session) —
> collision confirmée réelle mais **la déclaration référence la bonne échéance**, aucune confusion
> de tiers. Aucun tampon `DT_Id` orphelin, aucune double-déclaration de règlement. Rapport :
> `DOCS/AUDIT-TASK-143/00-SYNTHESE-GLOBALE.md` + fiches `01`…`06`. **3 anomalies confirmées →
> TASK de correction à ouvrir séparément** : (1) faire aboutir/exclure les 14 lignes non valorisées,
> (2) compléter l'IF fournisseur ZF FOOD, (3) désambiguïser l'affichage du tiers en cas de collision
> `DO_Numero` (préventif). Voir `DONE.md`.

> ✅ **TASK-142 approuvée** (20/07/2026) — drill « Lignes incohérentes (TTC ≠ HT+TVA) » (écran ②
> Vérifier & Intégrer) : colonne **Montant TVA** ajoutée (2ᵉ opérande de l'égalité que le drill
> isole, déjà exposée par l'API, simplement absente de l'affichage) + colonne **Écart
> (HT+TVA−TTC)** calculée en pur rendu front, ligne surlignée en rouge dès que l'écart dépasse
> 0,005 MAD. Prop `columns`/`colsStorageKey` dédiée à ce seul drill (`VerifierIntegrerPanel.tsx`) —
> aucun autre écran utilisant `DomainGrid` (① Sélection, Workstation, ProofModal,
> DeclarationFinalePanel) n'est affecté. Front-only, aucun endpoint modifié. Voir `DONE.md`.

> ✅ **TASK-106/TASK-108/TASK-107 livrées et approuvées** (17/07/2026) — les trois suivis ouverts
> pendant la revue de TASK-103 sont clos : TASK-106 (token espèce), TASK-108 (artefact d'ensembles
> du contrôle d'équilibre), TASK-107 (drill « Répartition par source » → factures/règlements,
> option 3 arbitrée PO). Voir `DONE.md`.

> ✅ **TASK-139 approuvée** (19/07/2026) — badge « Écart détecté » (étape ②) distingue désormais
> explicitement le cas où la ligne incohérente responsable est dans l'onglet **non affiché** :
> nouveau message « Écart expliqué : ... se trouvent dans l'onglet « X »» au lieu du trompeur
> « aucune ligne incohérente identifiée ». Option 1 (front seul, recommandation architecte)
> appliquée — aucun changement back, `recapIncoherence` déjà exhaustif. Preuve réelle (Playwright,
> vrai composant `VerifierIntegrerPanel`, cas exact PO `TVA1-2026-02`) : 4 scénarios couverts
> (renvoi vers l'autre onglet, bascule d'onglet, non-régression TASK-112 même onglet, non-régression
> écart réellement inexpliqué) — 4/4 rejoués par l'architecte. Build front 0 erreur (rejoué). Voir
> `DONE.md`.

> ✅ **TASK-141 approuvée** (19/07/2026) — badge « À contrôler » (étape ① Sélection) affiche
> désormais le motif réel : tooltip `Reste à affecter : X MAD` sur le badge + libellé inline dans la
> colonne « Affecté » (`partiel X % (reste X MAD)`), levant l'ambiguïté de l'arrondi entier à 100 %.
> Front seul, `ReglementsSelection.tsx`, donnée `resteAAffecter` déjà disponible. Rejet initial
> corrigé : premier passage ajoutait un « MAD » littéral en plus de celui déjà porté par
> `formatMoney()` (doublon « MAD MAD ») — corrigé et revérifié. Voir `DONE.md`.

> ✅ **TASK-140 approuvée** (19/07/2026) — règlements « Déjà déclaré » (`declare === true`)
> entièrement retirés de l'affichage de l'étape ① Sélection (front seul, `ReglementsSelection.tsx`) ;
> en contrepartie, la colonne « Déclaré » de l'écran Rapprochement global affiche désormais le
> **numéro** de la déclaration verrou (résolution applicative batchée `DT_Id → Numero` entre les deux
> connexions Dapper distinctes, jamais de JOIN SQL trois-parties). Endpoint partagé `GET
> /api/rapprochement` resté additif côté serveur, aucun filtrage retiré. Voir `DONE.md`.

> ✅ **TASK-137 approuvée** (23/07/2026, revue architecte complète : VERIFY relu, build+tests
> rejoués indépendamment — `Declaration.Export.Xml.Tests` 13/13, `Declaration.Core.Tests` 34/34,
> `Declaration.Orchestration.Tests` 137/137, `Declaration.Export.Excel.Tests` 1/1,
> `Declaration.Selection.Tests` 58/59 échec préexistant sans rapport) — export XML "Relevé de
> déductions" mis en conformité avec le CDC DGI externe : taux `tx` en fraction décimale (défaut
> bloquant propre au nouveau code, pas hérité de GRFN), prolog+`xmlns:xsi`, retrait `<prorata>`,
> assouplissement du blocage IF/ICE (CDC §4.8), formatage decimal sans arrondi forcé, `Trim()`.
> ⚠️ Réserves non bloquantes à trancher par le PO/fiscaliste avant tout dépôt réel : format exact
> final IF/ICE (CDC §5.2) et arrondi/précision définitifs des montants (CDC §5.1, `Ventilateur.cs`
> en amont non touché). Voir `DONE.md`.

> ✅ **TASK-181 approuvée** (24/07/2026, revue architecte complète : VERIFY relu, diff `8d364ca` relu
> intégralement, `Declaration.Export.Xml.Tests` rejoué indépendamment 13/13) — tag `<des>` de l'export
> XML porte désormais toujours le littéral fixe « Achat marchandise » (remplacement total, pas un
> fallback conditionnel), export Excel et `ConstruireModeleExportAsync` non touchés. Voir `DONE.md`.
> ⚠️ **Point ouvert non refermé** : la vraie source de désignation (`DM_LGTVA`/`LigneCandidate`,
> `Designation` toujours `""` en amont) reste à trancher — ce placeholder n'est qu'un pis-aller assumé
> par le PO en attendant cet arbitrage.

## 🔐 Simplification `DeclarationTVA.sql` + exécution automatique par le setup (PO 19/07/2026)
Décisions PO actées (session 19/07/2026) : (1) **retrait du login SQL dédié à moindre privilège**
(`decl_tva_app`, section 3 du script, garde-fou posé par TASK-114) — l'application utilisera le
compte SQL déjà provisionné par le client à l'installation. ⚠️ **Recul de sécurité assumé
explicitement par le PO** (contredit le critère de validation initial de TASK-114 : « jamais
admin/sa pour l'application ») — alternative à moindre risque proposée par l'architecte (générer/
écrire le mot de passe automatiquement sans toucher au cloisonnement) et écartée. (2) **Exécution
automatique du script par `Declaration.Setup`** (au lieu du `sqlcmd` manuel documenté dans
`LANCEMENT_DEV.md`) — le script est déjà idempotent (`IF NOT EXISTS` partout), le risque « tables
déjà existantes » est déjà couvert par construction. (3) **le script doit être livré tel quel dans
le dossier d'installation du client** (audit/rejeu manuel), **nettoyé de toute note interne de dev**
(références `TASK-XXX`, historique de bug, environnements de dev) — relecture manuelle, pas un
filtre automatique. (4) **`USE <base>;`** substitué dynamiquement par le nom de base saisi au
formulaire, entre crochets `[ ]` échappés (`]` doublé) pour supporter un caractère spécial —
recommandation architecte : l'exécution automatique elle-même se connecte directement à la base
cible (`Initial Catalog`) sans dépendre de ce texte, seule la copie livrée en clair l'utilise.

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-126](TASKS/TASK-126-simplification-script-sql-install-execution-auto-setup.md) | Retirer la section login/droits de `DeclarationTVA.sql`, le nettoyer de toute note interne de dev, rendre le `USE` dynamique (nom de base échappé), intégrer son exécution automatique + le livrer dans le dossier d'installation du client, via le wizard `Declaration.Setup` (TASK-115). | 🎯 **prêt** — TASK-115 est **clôturée depuis le 23/07/2026** (`DONE.md`), plus de dépendance bloquante (correction 27/07/2026 : `TODO.md` affirmait encore à tort `IN_PROGRESS`). |

## 🔴 CRITIQUE — API cassée sur tout déploiement : `Microsoft.Data.SqlClient` incompatible `net10.0` (signalement PO 19/07/2026)
Signalement PO : « Serveur injoignable » persistant en écran de connexion sur déploiement réel
(`DESKTOP-5BFKKEP:5500`). Diagnostic architecte par élimination (service/licence/SQL/réseau tous
vérifiés sains) puis **reproduction directe sur 3 environnements** (build déployé, install locale
PO `:5280`, déploiement distant `:5500`) : `GET /api/societes` → 500,
`System.PlatformNotSupportedException: Microsoft.Data.SqlClient is not supported on this
platform.` levée au constructeur `SqlConnection`, avant tout accès réseau. Cause : `Declaration.API`/
`Declaration.Infrastructure`/`Declaration.Application` ciblent `net10.0`, alors que
`Microsoft.Data.SqlClient` 7.0.2 (dernière version publiée, aucune mise à jour disponible) ne
fournit d'assets que jusqu'à `net9.0` (déjà hors support STS à ce jour) → **toute** route SQL est
cassée, sur tout poste. Décision : rétrogradation `net8.0` (LTS jusqu'à nov. 2026), pas `net9.0`
(STS déjà expiré) ; `Microsoft.Data.SqlClient` conservé (pas de retour vers `System.Data.SqlClient`,
déprécié).

> ✅ **TASK-123 approuvée** (19/07/2026) — resoumission après rejet initial (l'hypothèse du rejet, un défaut de repli de version des paquets `10.0.9`, était fausse : ils embarquent déjà un asset `lib/net8.0` dans le même `.nupkg`, confirmé indépendamment). Cause racine réelle = `deploy\` jamais nettoyé avant publish (fichier orphelin d'un ancien build net10) → corrigé dans `Deploy-All.ps1`. Confirmation réelle PO : mise à jour rejouée sans exception. Voir `DONE.md`.
>
> ✅ **TASK-124 approuvée** (19/07/2026) — `WinSwServiceManager.Stop()` attend désormais activement la fin réelle du process `Declaration.API.exe` (`WaitForApiProcessExit`, 45s, `TimeoutException` explicite) avant de rendre la main, plus seulement la fin du CLI WinSW éphémère. Confirmation réelle PO : mise à jour rejouée sans intervention manuelle. Voir `DONE.md`.

> ⚠️ Suivi distinct signalé (hors périmètre TASK-123, non tracé en task séparée à ce stade) :
> `Auth.tsx` (lignes 32/69) affiche « Serveur injoignable — vérifiez que l'API est démarrée » pour
> **toute** erreur capturée, y compris une 500 sans rapport avec un arrêt de l'API — ce message
> trompeur a retardé le diagnostic. À corriger si le PO le juge utile (nouvelle task front).

## 🐞 Grille de règlements vide après retour de « Passer au calcul » / « Détail des lignes » (signalement PO 19/07/2026)
Signalement PO (capture écran ① Sélection, `TVA1-2026-01`) : après avoir cliqué « Passer au
calcul » **ou** « Détail des lignes » puis être revenu à l'étape « 1. Sélection », la grille est
**entièrement vide** — « Règlements : 0 », « Sélectionnés : 0 », « Total sélectionné : 0,00 MAD »
— alors que des règlements étaient auparavant listés et sélectionnés. **Distinct de TASK-096**
(DONE) qui ne touchait que le total du pied de grille (`Sélectionnés : N` restant correct, lui) ;
ici les deux compteurs tombent à 0, pour deux déclencheurs. Analyse code (architecte) : le tunnel
est toujours `DeclarationStepper.tsx` (pas remplacé par un tunnel 8 écrans, cf. fusion TASK-090/091) ;
`selectedKeys.size` à 0 implique que `DeclarationStepper` **lui-même** a perdu son state (state
parent, non réinitialisé par un simple changement d'onglet) — signe d'un remontage complet du
composant, pas d'un recalcul local raté comme en TASK-096. 4 hypothèses documentées (H1 remontage
complet du stepper par navigation hors onglet ; H2 échec de re-fetch de `ReglementsSelection` seul ;
H3 jointure cross-catalogue `GrfConnection`/`PersistenceConnection` dans `/rapprochement` après
écriture par `SaveSelectionReglementsAsync` ; H4 `showToast` non stabilisé, facteur aggravant),
**non tranchées** faute de reproduction instrumentée (onglet réseau navigateur) — cf. TASK-125.

> ✅ **TASK-125 approuvée** (19/07/2026) — reproduction instrumentée réelle (API+DB réelles
> `GR_EMA_DISTRIBUTION`, build front de production, JWT réel, `TVA1-2026-01`) tranchant les 4
> hypothèses : H3 réfutée pour l'environnement réel du PO (mêmes catalogues) ; H1 (réouverture)
> confirmée nécessaire mais insuffisante seule ; H2 (absence de garde d'annulation) et H4
> (`showToast` non mémoïsé) confirmées — combinaison H1+H2+H4 = tempête auto-entretenue
> (1061-1078 requêtes `/rapprochement` mesurées pour une seule panne simulée), qui bloque
> durablement la grille/sélection à 0/0 même après retour à la normale du réseau. Correctif ciblé :
> `showToast` mémoïsé (`useCallback`, `App.tsx`) + garde `cancelled` sur `fetchAll`
> (`ReglementsSelection.tsx`, symétrique à l'effet `distincts` déjà présent). Preuve avant/après
> (1078→1 requêtes, récupération fiable après réparation) ; non-régression TASK-096/TASK-097
> vérifiée ; build front 0 erreur. **Limite assumée, non silencieuse** : une panne réseau isolée
> (hors tempête) laisse encore la grille à 0/0 jusqu'à une navigation manuelle (changement d'onglet
> ou réouverture) — pas de retry automatique, hors périmètre de cette task, amélioration UX
> distincte à cadrer si le PO le souhaite. Voir `DONE.md`.

## 🆕 Délai de Paiement Maroc — TASK-127 à 136 : LIVRÉES ET APPROUVÉES (revue architecte 28/07/2026)

Les 10 TASKs du périmètre ci-dessous (analyse PO 19/07/2026) ont été traitées de bout en bout par le
worker (`DOCS/PROMPT-WORKER-DDP-27-07-2026.md`) et **toutes approuvées** après revue architecte
indépendante (10 agents dédiés, diff réel relu intégralement + build/tests rejoués + vérifications DB
en lecture seule reproduites sur `GR_EMA_DISTRIBUTION`/`NEW_EMA DISTRIBUTION` pour chacune). Détail par
task dans `DONE.md` et `CHANGELOG.md`, fichiers TASK archivés dans `DONE_DETAIL/DDP-TASK-127...136-*.md`.

**Points ouverts issus de la revue, non bloquants pour cette livraison mais à trancher avant mise en
production** :
1. **Longueur IF(8)/ICE(15) stricte bloquante** (TASK-132/133/134) : sur les 357 fournisseurs réels
   vérifiés (`NEW_EMA DISTRIBUTION`), 175 IF vides et 160 ICE vides — le contrôle bloquant (conforme
   décision PO §5.A-5 du CDC) empêcherait la génération du fichier de dépôt pour plus de la moitié du
   parc tant que les fiches fournisseurs ne sont pas complétées. **Même divergence déjà connue côté
   module TVA voisin** (`ValidationIdentiteFiscale`) — à arbitrer PO/fiscaliste conjointement pour les
   deux modules plutôt que séparément.
2. **Interaction TASK-128 ↔ TASK-131 sur les échéances antérieures à la « date de mise en route »** :
   le garde-fou TASK-128 désactive le calcul automatique de retard **inconditionnellement** tant
   qu'aucune date de mise en route n'est configurée pour la société, même si l'échéance a un
   historique de déclaration legacy — interprétation documentée par le worker comme un choix, pas une
   exigence PO explicite (`DONE_DETAIL/DDP-TASK-128-parametre-date-mise-en-route-bootstrap.md`, section
   « Décisions worker documentées », point 2). Effet réel vérifié : **0 ligne chiffrée** dans l'écran de
   sélection tant qu'une société n'a pas saisi cette date (TASK-134 expose bien cette saisie). À
   confirmer par le PO avant le premier usage réel en déclaration.
3. **Couverture de test de la sélection incrémentale (TASK-131)** : le calcul anti-double-déclaration
   est testé de façon rigoureuse au niveau du calculateur pur (scénario T1→T2 explicite), mais la
   couche service/repository n'a pas de test automatisé dédié, et le mécanisme n'a **jamais été exercé
   avec un historique réellement écrit en base** (`RT_DECLARATIONDELAISPAIEMENTLG` vide à ce jour, le
   périmètre étant neuf) — ne pourra être vérifié en conditions réelles qu'après une première
   déclaration DDP effectivement intégrée en production/pré-production.
4. **Choix UI réversibles non tranchés PO** : sous-navigation en onglets internes vs sous-menu imbriqué
   à 2 niveaux (TASK-134/136) ; modale partagée vs écran dédié pour la saisie de mise en route
   (TASK-134).
5. **Asymétrie annuelle/trimestrielle du contrôle de chevauchement de déclarations** (TASK-132),
   reproduite du legacy, non corrigée — documentée, non bloquante.
6. **`natureMarchandise`/`dateLivraisonMarchandise` toujours au fallback** (CDC §5.A-4, dette déjà
   assumée par TASK-133) — requalifiée en tâche planifiée le 28/07/2026 :
   [TASK-191](TASKS/TASK-191-cablage-nature-date-livraison-marchandise-ddp.md). Motivation PO : le module
   sera indépendant à terme, GRF deviendra une source parmi d'autres, ajout prévu du **grand livre
   comptable** comme nouvelle source — cohérent avec la roadmap V2 déjà actée côté TVA (24/07/2026,
   ci-dessus). Non bloquant pour l'usage actuel.

### Analyse d'origine (PO 19/07/2026)
Cahier des charges source : `D:\_vibe\apbs-gr_winform\analayse\CDC-DELAI-PAIEMENT-MAROC.md` (analyse
lecture seule de l'ancien applicatif `apbs-gr_winform`, Tresorerie.*). Décisions actées en session
d'analyse architecte (19/07/2026), avec vérification directe du code legacy (au-delà du CDC fourni) :
- Développement **neuf** sur la plateforme GRF web (`Declaration.API`/React) — aucune réutilisation de
  DLL/code WinForms, même principe que le module TVA.
- Rapprochement bancaire (§3.3) réutilise le mécanisme déjà en place côté TVA (`RT_MOUVEMENT.MV_Point`/
  `MV_PointDate`, local GRF, TASK-036/037) — **pas** le mécanisme Sage de l'ancien module RAS suggéré par
  le CDC brut.
- **Attestation de régularité fiscale (§3.4) reportée** à une phase ultérieure — hors périmètre de cette
  vague (décision PO : ne concerne pas directement la DDP).
- **3 anomalies découvertes en lisant le code**, absentes du §4 du CDC, décision PO = corriger les 3 :
  chevauchement de conventions incomplet (`SocieteManager.Complement.cs:511`, `// TODO: verifier le
  chauvochement des date` l.531, jamais résolu par les développeurs d'origine) ; affectation partielle
  ignorée sur le bucket "échéance hors période non payée" (`// TODO: verifier les affectations`,
  `LigneControleDelaisPaiementController.cs:110`, jamais résolu) ; `Depassement` structurellement
  constant sur ce même bucket (l.127, `GetMaxFrom` renvoie toujours `dateDebut` vu le filtre l.83).
- **Absence totale de mécanisme anti-double-déclaration confirmée en code** (`DeclarationDelaisPaiementLigneAjouter`
  ne vérifie aucune unicité ; `INSERT` sans contrainte ; sélection sans exclusion des échéances déjà
  déclarées) → remplacé par un calcul **incrémental** (`Depassement` = écart depuis la **dernière borne
  déjà déclarée** pour cette échéance, pas depuis l'échéance légale — évite la double comptabilisation
  du retard entre déclarations successives) + garde-fou de bascule ("date de mise en route" par société,
  nouvelle table dédiée, aucune modification du schéma existant/`P_SOCIETE`).
- Mesure du délai fournisseur (§3.3, redéfinie PO §5.A-1) : **pas d'écran dédié** — extension de l'écran
  Factures existant (TASK-041, facture-pivot) plutôt que l'écran Rapprochement (règlement-pivot, qui ne
  montre pas les factures non payées — or une facture non payée en retard doit aussi être visible ici,
  comme elle l'est dans la DDP).
- Menu : nouvelle entrée **« Délai de paiement »** autonome (groupe DÉCLARATION), au même niveau que
  « Déclaration TVA », contenant Déclarations DDP + Sélection/Contrôle + Conventions. Rapprochement et
  Factures restent sous INTERROGATION, inchangés dans leur emplacement.

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-127](DONE_DETAIL/DDP-TASK-127-socle-resolution-delai-echeance-legale.md) | Socle : résolution du délai applicable + calcul de l'échéance légale (jour ouvré, `P_JOURSREPOS`) | ✅ **approuvée** (28/07/2026) |
| 2 | [TASK-128](DONE_DETAIL/DDP-TASK-128-parametre-date-mise-en-route-bootstrap.md) | Paramètre « date de mise en route » par société (nouvelle table) + garde-fou de bascule | ✅ **approuvée** (28/07/2026) — réserve n°2 ci-dessus |
| 3 | [TASK-129](DONE_DETAIL/DDP-TASK-129-convention-delai-paiement-tiers-back.md) | Convention délai de paiement par tiers (back) — corrige le chevauchement bidirectionnel | ✅ **approuvée** (28/07/2026) |
| 4 | [TASK-130](DONE_DETAIL/DDP-TASK-130-convention-delai-paiement-tiers-front.md) | Convention délai de paiement par tiers (front) | ✅ **approuvée** (28/07/2026) |
| 5 | [TASK-131](DONE_DETAIL/DDP-TASK-131-selection-lignes-hors-delai-calcul-incremental.md) | DDP : sélection des lignes hors délai + calcul incrémental anti-double-déclaration | ✅ **approuvée** (28/07/2026) — réserves n°2/3 ci-dessus |
| 6 | [TASK-132](DONE_DETAIL/DDP-TASK-132-cycle-de-vie-declaration-controle-if-ice.md) | DDP : cycle de vie déclaration + contrôle IF/ICE bloquant | ✅ **approuvée** (28/07/2026) — réserve n°1 ci-dessus |
| 7 | [TASK-133](DONE_DETAIL/DDP-TASK-133-generation-fichier-xml-zip.md) | DDP : génération fichier XML/ZIP (structure legacy reprise à l'identique) | ✅ **approuvée** (28/07/2026) |
| 8 | [TASK-134](DONE_DETAIL/DDP-TASK-134-front-liste-fiche-selection-controle.md) | DDP : front (liste/fiche/sélection/contrôle), filtre de période raisonné (jamais de plage libre) | ✅ **approuvée** (28/07/2026) — réserves n°1/4 ci-dessus |
| 9 | [TASK-135](DONE_DETAIL/DDP-TASK-135-mesure-delai-fournisseur-extension-ecran-factures.md) | Mesure du délai fournisseur (§3.3) : extension écran Factures (2 colonnes) | ✅ **approuvée** (28/07/2026) |
| 10 | [TASK-136](DONE_DETAIL/DDP-TASK-136-menu-entree-delai-de-paiement.md) | Menu : nouvelle entrée « Délai de paiement » (groupe DÉCLARATION) | ✅ **approuvée** (28/07/2026) — réserve n°4 ci-dessus |

> **Ordre d'exécution recommandé** : 127 → 128 → (129 → 130) ∥ (131 → 132 → 133 → 134) ∥ 135 → 136.
> Attestation de régularité fiscale (§3.4) explicitement hors périmètre de cette vague — à cadrer plus
> tard si le PO le demande, pas de task créée.

| # | Task | Objet | État |
|---|---|---|---|
| 11 | [TASK-191](TASKS/TASK-191-cablage-nature-date-livraison-marchandise-ddp.md) | Câblage réel `natureMarchandise`/`dateLivraisonMarchandise` (lève la dette CDC §5.A-4) — cf. réserve n°6 ci-dessus | 🎯 **prête** (créée 28/07/2026) |

## 🎨 Re-thème identité visuelle « noir + vert signature » (PO 18/07/2026)
Demande PO : faire évoluer la palette posée en TASK-083 (indigo) vers une teinte inspirée de
l'identité visuelle de la société cliente (référence : bandeau noir, accent vert, fond blanc).
Décisions PO actées (session 18/07/2026) : (1) référence = **inspiration à affiner**, pas une
charte pixel-perfect à reproduire ; (2) **structure du shell conservée** (sidebar gauche seule,
aucun bandeau horizontal ajouté — changement structurel explicitement écarté) ; (3) périmètre
**étendu aux badges de statut** codés en dur dans les composants métier (197 occurrences/18
fichiers, jamais traitées depuis TASK-083), pas seulement `index.css`/sidebar ; (4) **ajout icône
application « DM »** (18/07/2026, révise l'exclusion « pas de logo externe » de TASK-083) — utilisée
en sidebar (remplace le monogramme texte) **et** en favicon d'onglet navigateur. Fichiers déjà
générés par l'architecte (svg + png 32/192/512, palette alignée sur le tableau TASK-120) :
`TASKS/assets/task-120-icon-dm/` — intégrés au périmètre TASK-120, pas de nouvelle task.

> ✅ **TASK-121 approuvée** (19/07/2026) — variabilisation des badges de statut faite (variables
> `--status-*` dans `index.css`). Voir `DONE.md`. Démarrée sans attendre l'approbation de TASK-120
> (risque assumé, autorisation PO explicite) ; VERIFY re-soumis après rejet initial (2 codes hex
> sur les 8 prescrits par l'étape 2 avaient été omis du grep de contrôle — corrigé, cf. `DONE.md`).

> ✅ **TASK-120 clôturée** (19/07/2026, décision PO explicite) — substance jugée saine par
> l'architecte (grep palette, contraste WCAG, build, diff App.tsx/index.html/index.css), mais les
> captures (a)/(b)/(d) explicitement requises par la task n'ont jamais été produites (blocage
> structurel TASK-117 : pas de chemin vers `<Auth/>`/`<Dashboard/>` sans licence réelle dans cet
> environnement). Clôturée sans ces captures, sur confirmation PO que l'application tourne
> normalement en conditions réelles. Voir `DONE.md`.
>
> ⚠️ Réserve non levée : le vert signature (`--accent-primary`) et le vert de statut déjà existant
> (`--success`) peuvent créer une ambiguïté visuelle (élément actif vs statut "ok") — jamais
> formellement audité, à surveiller si signalé par un utilisateur réel.

## 🚨 Multi-bases Sage par `SO_Id` (signalement PO 17/07/2026)
Signalement PO : la base GRF (unique) doit pouvoir orchestrer des déclarations pour des sociétés
dont les données comptables vivent dans **plusieurs bases Sage distinctes**, choisies par
`SO_Id` — la chaîne de connexion GRF (`GrfConnection`/`PersistenceConnection`) reste **unique**,
seule `SageConnection` doit devenir dépendante du `SO_Id`. Analyse code (architecte) : aujourd'hui
`SageConnection` est globale et statique (`connections.json`), lue en dur par
`DeclarationWorkflowService.BuildOrchestrateur` (4 sites) sans jamais tenir compte du `SO_Id`
traité. **Risque majeur découvert en cours d'analyse** : le cache `GRC_VENTILATION_SAGE_CACHE`
(TASK-024) a pour clé `(EC_Id, Taux)` — `EC_Id` n'est unique qu'**au sein d'une seule base Sage** ;
sans correctif, deux bases Sage distinctes peuvent produire une collision silencieuse de
ventilation TVA entre sociétés différentes. **Précision PO (même session)** : les coordonnées
Sage par société existent déjà nativement dans `P_SOCIETE` (`SO_ErpDb`/`SO_ErpUserApp`/
`SO_ErpPasswdApp`, `SO_ErpServer` ignoré — même serveur que `GrfConnection`) ; `SO_Id` à ajouter au
cache **sans clé étrangère** (ne pas impacter l'EF de l'application principale propriétaire de
`P_SOCIETE`).

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-118](DONE_DETAIL/TASK-118-connexion-sage-dynamique-par-so-id.md) | **Résolution dynamique de la connexion Sage par `SO_Id`** (Server/User/Password de `GrfConnection` + `Database=P_SOCIETE.SO_ErpDb`, GRF reste unique) + correctif du cache de ventilation Sage (collision inter-bases, colonne `SO_Id` sans FK) + renommage `GRC_VENTILATION_SAGE_CACHE` → `DM_VENTILATION_SAGE_CACHE` (conformité `DM_*`, TASK-065). | ✅ **done** — 18/07/2026 (approuvée architecte, build+tests rejoués à l'identique du VERIFY, migration SQL rejouée en réel avec bug de batch T-SQL découvert et corrigé). Réserve non bloquante : scénario 2 bases Sage physiquement distinctes non prouvable (un seul `SO_Id` en dev) — collision structurellement rendue impossible par la contrainte PK, prouvé indépendamment. Voir DONE.md. |
| 2 | [TASK-119](DONE_DETAIL/TASK-119-correction-setup-formulaire-sage-so-id.md) | **Impact sur TASK-115** (setup GUI, `IN_PROGRESS`/VERIFY rejeté pour réserves indépendantes) : **suppression complète** (tranchée PO 18/07) des champs Sage/SageOM globaux du formulaire (`SetupData`/`SetupForm`/`ConnectionsFileService`), devenus obsolètes une fois la connexion Sage résolue par `SO_Id` (TASK-118). **+ icône** (monogramme « DM » sur indigo, cohérent TASK-083) **+ renommage de l'exe livré au client** (`Declaration.Setup.exe` → `DeclaratifMaroc.exe`, demande PO 18/07). | ✅ **done** — 18/07/2026 (approuvée architecte, retrait Sage/SageOM revérifié en source (`SetupData.cs`/`SetupForm.cs`/`ConnectionsFileService.cs`, seuls restent `SageVersion`/`CheckSageOm` hors périmètre), build Debug+Release rejoués à l'identique — 0 erreur/0 avertissement, `DeclaratifMaroc.exe`+icône confirmés sur disque, bug réel `DeploymentCopier` (auto-exclusion) corrigé au passage. Réserve non bloquante : pas de preuve visuelle GUI réelle dans cet environnement (même limite que TASK-115) — ⚠️ ne pas clore TASK-115 en prod avant ce correctif. Voir DONE.md. |

> ⚠️ **Suivi ouvert par l'audit TASK-118 (Étape 5), non corrigé, à trancher séparément si le PO le
> juge nécessaire** : 3 lectures brutes par `EC_Id`/`MV_Id` (sans filtre `SO_Id`) subsistent hors
> cache — `VentilationSageCacheRepository.GetCurrentPaiementToken`/`GetEcheanceMontantDevise`
> (`RT_AFFECTATION`/`RT_MOUVEMENT`/`RT_ECHEANCE`) et `DeclarationRepository.GetMvPointsActuelsAsync`
> (`RT_MOUVEMENT`). Ces tables sont la propriété de l'**application principale** (pas créées par
> GRF) — `RT_MOUVEMENT` porte déjà une colonne `SO_Id` ailleurs dans le code (`Declaration.Selection`,
> filtre `M.SO_Id = @so` systématique), ce qui suggère (non vérifié) que ce schéma partagé gère
> peut-être déjà nativement le multi-Sage. À déterminer avec le PO/l'architecte propriétaire de
> l'application principale avant tout correctif — hors périmètre strict de TASK-118.

## 🧪 Test du nouvel environnement client (`DESKTOP-5BFKKEP`, 16/07/2026)
PO : nouvel environnement client restauré sur `DESKTOP-5BFKKEP` (bases `GR_EMA_DISTRIBUTION` +
`NEW_EMA DISTRIBUTION`, mêmes noms), à tester intégralement avec les objets métiers réels. Build +
suite de tests rejoués OK (116+5+1 verts, 1 échec préexistant sans rapport, test à connexion codée
en dur vers l'ancien `.\sql2022`). Deux prérequis d'environnement manquants identifiés et corrigés
(actions DDL additives uniquement) : table `DM_SELECTION_REGLEMENT` (script `DeclarationTVA.sql`
non rejoué depuis TASK-097) et synonyme cross-base `F_COMPTET` (jamais documenté/scripté nulle
part — dette à combler, cf. `LANCEMENT_DEV.md`). Tunnel exercé via API réelle (login Admin,
déclaration `TVA1-2026-01`, 152 règlements réels) → a révélé l'anomalie ci-dessous.

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-100](DONE_DETAIL/TASK-100-reglement-ectype-inconnu-disparait-silencieusement.md) | **Règlement d'échéance `EC_Type` hors liste blanche (0/4/111) disparaît silencieusement du tunnel** après sélection+figeage (cas réel `RC26040045`, 13 053,66 MAD) — contredit le principe « aucune ligne silencieuse » posé par TASK-097. | ✅ **done** — 17/07/2026 (approuvée architecte ; ligne TODO restée obsolète, corrigée le 19/07/2026 en vérifiant le code : alertes `REGLEMENT_EXCLU` explicites + badges front, aucun rejet silencieux). Voir DONE.md. |
| 6 | [TASK-105](DONE_DETAIL/TASK-105-tracabilite-incoherence-validee-marqueur-filtre.md) | **Incohérence validée invisible après validation** : après « Valider l'incohérence », la facture sort de `facturesIncoherentes` (dérivé des alertes actives, `AffectationsDrill.tsx:431`) → bandeau/surlignage/boutons **disparaissent**, plus aucune trace. Le flag `incoherenceValidee` est pourtant déjà remonté du back (l.52/225) mais jamais affiché. Contredit « aucune ligne silencieuse ». Ajouter marqueur persistant + filtre (rendu pur). Front-only. | ✅ **done** — 17/07/2026 (approuvée architecte, colonne `Incoh.` + teinte ambre + filtre `Oui`/`Non`, preuve réelle `GR_EMA_DISTRIBUTION` avant/après/filtre, totaux inchangés). Voir DONE.md. |
| 8 | [TASK-113](DONE_DETAIL/TASK-113-domaingrid-lignes-absolute-colonnes-desalignees.md) | **`DomainGrid` : colonnes désalignées** (« traçage de grid » mal formé, signalement PO 17/07) — les `<tr>` virtualisés sont en `position:absolute` dans une `<table>`, désalignement des colonnes en-tête/lignes. Deux tentatives de patch in-place (`tableLayout:fixed`+`colgroup`, puis `width` explicite par cellule) **rejetées en test réel** malgré build+revue statique favorables à chaque fois (captures PO 19/07). | ❌ **clôturée sans correction** — paradigme abandonné, remplacé par TASK-138 (migration flexbox). Clôture confirmée par lecture du code (19/07/2026) : aucune trace résiduelle du paradigme `<table>`/`position:absolute`. |
| 9 | [TASK-138](DONE_DETAIL/TASK-138-domaingrid-migration-flexbox-remplace-table-virtualisee.md) | **`DomainGrid` : migration du rendu `<table>`+`<tr position:absolute>` vers le pattern flexbox de `AffectationsDrill.tsx`** (jamais affecté par le défaut TASK-113) — source unique de largeur de colonne partagée en-tête/lignes, virtualisation conservée via `<div>`. Preuve visuelle réelle **obligatoire** avant toute VERIFY (l'analyse statique seule a échoué deux fois sur ce composant). Impacte tous les usages de `DomainGrid` (① non-readonly + drills readonly). | ✅ **done** — 19/07/2026 (vérifié directement en code par l'architecte : `DomainGrid.tsx` 100 % `<div>`/rôles ARIA, largeur partagée via `colStyle()`, plus aucune `<table>`/`<tr>`). Voir DONE.md. |
| 2 | [TASK-101](DONE_DETAIL/TASK-101-support-multi-version-objets-metiers-sage.md) | **Support multi-version du DLL Objets Métiers Sage** (`Interop.Objets100cLib.dll`) : le worker `SageTaxReader.Console` était figé sur la v12 au build → 100 % d'échec de valorisation chez un client v10. | ✅ **done** — 17/07/2026 (implémentée en worker exceptionnel, cf. réserve `CLAUDE.md`). 4 variantes de build (`SageInteropVersion` MSBuild, v10 couvre aussi v11 — DLL confirmés bit-à-bit identiques) + script `publish-sagetaxreader-workers.ps1` + `LANCEMENT_DEV.md` à jour. Build 0 erreur ×4, non-régression solution principale (234/236, 2 échecs préexistants sans rapport), smoke test COM réel v10 sur `DESKTOP-5BFKKEP` (activation OK, échec résiduel = identifiants applicatifs Sage non fournis, sans rapport). **Réserve** : lecture réelle de facture (TVA non nulle) via ce nouveau mécanisme et variantes v7/v9 non rejouées en conditions réelles (pas d'identifiants/bases disponibles à ce poste) — cf. DONE_DETAIL. |

> Test étendu à Décaissement + Encaissement 06/2026 avec valorisation Sage réelle fonctionnelle
> (voir TASK-101 ✅ ci-dessus). Checkup de contrôle : écart d'équilibre observé (-381 592,21 MAD)
> **attendu et non bloquant** — le test ne couvre qu'une sélection partielle (règlements non
> déclarés uniquement, `declare=false`), pas l'univers complet du mois ; à ne pas confondre avec une
> anomalie de calcul. La déclaration `TVA1-2026-06` reste `EnCours`, **ne pas clôturer** (test, pas
> production).

## 🚨 CRITIQUE — Figeage/clôture non scopés à la sélection réelle de l'écran ① (PO 14/07/2026)
Signalement PO (« les cases sont décochées » au retour sur `TVA1-2026-06`) → analyse code
(architecte) a révélé un problème plus grave que la perte d'affichage : la sélection de
règlements faite à l'écran ① n'a **aucun effet côté back**. `ConstruireLignesFigeesAsync`
(`DeclarationWorkflowService.cs`) recalcule les taxes Sage/FGR pour **tous** les règlements
éligibles du mois, jamais filtré par la sélection ; `CloturerDeclarationAsync` pose le tampon
`DT_Id` sur **toutes** les lignes `Proposee`, donc tout le mois. La sélection front
(`selectedKeys`/`selectedRows`, `DeclarationStepper.tsx`) est un simple `useState` jamais transmis
ni persisté côté serveur — d'où aussi sa perte au retour (rien à restaurer). Confirmé par le PO :
le principe attendu est un recalcul et un flag `DT_Id` **scopés à la sélection**, pas au mois
entier. ⚠️ **Risque rétroactif** : les déclarations déjà `Cloturée` (`TVA1-2026-01`,
`TVA1-2026-06`...) ont vraisemblablement intégré tout le mois indépendamment de la sélection
affichée — impossible de reconstituer après coup ce qui avait été réellement coché (jamais
persisté). À trancher séparément avec le PO (audit manuel), hors périmètre du correctif code.

Signalement connexe : aucun mécanisme de réintégration manuelle d'une ligne `DM_LGTVA` figée
`Exclue` par un motif métier (hors le seul cas auto-géré `DejaEnCoursAilleurs`, TASK-080) — la
grille capable de le faire (`DomainGrid.tsx`, boutons Intégrer/Exclure/Reporter/Réinitialiser)
existe mais est montée en lecture seule partout où elle est atteignable dans le tunnel actuel.

Signalement complémentaire (PO 14/07/2026, cas réel `RF26060125`) : la fenêtre de période
actuelle (`RegleDatePeriode`) est une fenêtre mensuelle stricte — un règlement rapproché (ou payé
en espèce) un mois donné mais jamais déclaré ce mois-là devient **définitivement perdu** (la
fenêtre du mois suivant ne le recouvre pas). Règle corrigée demandée par le PO : période = simple
**date de coupure** (`DT_Id IS NULL` + `DateReference <= fin période`, sans borne basse) +
règlement non rapproché (hors espèce) **jamais affiché** dans l'écran ①.

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-099](DONE_DETAIL/TASK-099-redefinition-perimetre-declarable-rattrapage.md) | **Redéfinir le périmètre déclarable de l'écran ①** : suppression de la borne basse de période (rattrapage de l'arriéré non déclaré), masquage des règlements non rapprochés (hors espèce). | ✅ **done** — 14/07/2026 (approuvée architecte, preuve réelle `GR_EMA_DISTRIBUTION` sur les 4 scénarios VALIDATION dont non-régression TASK-037). Voir DONE.md. |
| 2 | [TASK-097](DONE_DETAIL/TASK-097-figeage-scope-selection-reglements.md) | **Scoper le figeage + la clôture à la sélection réelle de l'écran ①** (transmission + persistance de la sélection avant figeage, restauration au retour = lecture base, pas mémoire navigateur). | ✅ **done** — 14/07/2026 (approuvée architecte, réserve bypass backend corrigée + preuve réelle `GR_EMA_DISTRIBUTION` sur les 3 scénarios VALIDATION via l'API réelle). Voir DONE.md. |
| 3 | [TASK-098](DONE_DETAIL/TASK-098-reintegration-manuelle-lignes-exclues.md) | **Réintégration manuelle d'une ligne `Exclue`** par motif métier, avec revalorisation réelle (pas de flip d'état à montants nuls). | ❌ **clôturée — caduque** (décision PO 19/07/2026) : le principe métier confirmé est que l'exclusion d'une ligne **est** la non-déclaration de ce règlement pour la période, pas un blocage à réparer — TASK-097/099 recalculent la sélection à chaque déclaration et la période est une simple date de coupure (rattrapage de l'arriéré), donc un règlement exclu redevient naturellement sélectionnable à la déclaration suivante une fois sa cause corrigée. Aucun mécanisme de réintégration dédié requis. |

## 🐞 Bug signalé — « Total sélectionné » à 0 après retour de « Détail des lignes » (PO 14/07/2026)
Signalement PO sur `TVA1-2026-01` (148 règlements sélectionnés, capture écran ① Sélection) : le
pied de grille affiche « Total sélectionné : 0,00 MAD » après un aller-retour vers « Détail des
lignes ». Cause racine identifiée par analyse code (architecte) : `ReglementsSelection.tsx` calcule
ce total via un `useMemo` dont le tableau de dépendances (`[selectedKeys]`) ignore le rechargement
de `knownRowsRef` (un `useRef`, donc non suivi par React) survenant à chaque remontage du composant
(`DeclarationStepper.tsx` démonte/remonte `ReglementsSelection` via `showDrill`). Le bandeau bas du
tunnel (`DeclarationStepper.tsx:100`), lui, reste correct — la donnée existe, seul le calcul local
redondant est cassé.

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-096](DONE_DETAIL/TASK-096-total-selectionne-remis-a-zero-retour-drill.md) | **Fiabiliser le total sélectionné de l'écran ① Sélection** après un cycle démontage/remontage (« Détail des lignes » → retour) : Option B retenue — `selectedRows` passé en prop à `ReglementsSelection`, `selectedTotal` dérivé de ce prop. | ✅ **done** — 14/07/2026 (approuvée architecte). |

## 🔴 CRITIQUE — bugs bloquants découverts sur cas réel (PO 13/07/2026)
✅ **TASK-076, TASK-077, TASK-078, TASK-080, TASK-081 et TASK-082 livrées et approuvées**
(2026-07-13/14) — voir `DONE.md`. TASK-081 (bandeau absent au premier figeage) et TASK-082
(bandeau absent pour une ligne `Exclue` dès le figeage, cas réel `FC2501717`/`EC_Id=21473`)
découvertes et corrigées ensemble sur le même signalement PO (bandeau ② toujours silencieux
après le correctif 081 seul).

> ⚠️ TASK-072 (incohérence Σ(HT+TVA) vs rapproché, cas PO 68 règlements) et TASK-071 (deadlock d'intégration) ont été découverts ensemble sur cas réel et sont désormais **DONE** — cf. `DONE.md`. TASK-075 (relecture ②③ après intégration) également **DONE** (2026-07-13, implémentée en worker exceptionnel, **non vérifiée en environnement réel** — cf. réserve dans `DONE.md`). TASK-076/077/078 (suivis directs de TASK-072, workflow complet de détection/décision sur une incohérence Sage) également **DONE** (2026-07-13 — TASK-078 vérifiée par relecture de code exhaustive, **non confirmée visuellement en réel**, cf. réserve dans `DONE.md`, « on teste au fur et à mesure »). Ne pas confondre avec TASK-069/070 (mise en page ③, déjà traitées).

## Ordre d'exécution consolidé (re-séquencé 08/07/2026)

Vérifié dans le code : les Gap A/B/C décrits par les tasks sont **confirmés**. Ordre retenu :

**Vague 1 — correctness + perf (débloque le reste)**
1. **TASK-022** routing `EC_Type` — correction (FGR ne doit jamais passer par l'OM) + gros gain perf. Touche le SELECT sélection + dispatcher orchestrateur + lecteur FGR SQL. Accélère aussi le recalcul de TASK-009.
2. **TASK-023** session Sage réutilisée — ✅ **livré et approuvé** (2026-07-09) : 1 `Open()` → boucle `ReadPiece` → 1 `Close()`, timeout par pièce, isolation d'erreur (`EnErreur`/motif) ; ×10,6 mesuré sur base réelle. Voir DONE.md.

**Vague 2 — valeur front (Track A)**
3. **TASK-020 §1** n° règlement sur `LigneCandidate` — ✅ **livré** (Gap A comblé).
3bis. **TASK-027** (2ᵉ vague de TASK-020, §2) conformité IF/ICE calculée au DTO via extraction du validateur TASK-011 — ✅ **livré** (source unique `ValidationIdentiteFiscale`, `Conformite` exposé au DTO).
4. **TASK-021** reportées — ✅ **livré** (Gap B comblé, motif honnête, volume réel 304 mesuré).
5. **TASK-019** poste de travail 4 interrogations — ✅ **livré** (front API réelle ; bug TVA 0% = correctif back Gap A extrait → TASK-030).

**Vague 3 — décisions & finalisation**
6. ~~**TASK-025** cadrage `EC_Type=4`~~ — ✅ **livré et approuvé** (2026-07-09) : inventaire réel (34 lignes, 0 TVA déclarable), décision PO = maintenir l'alerte (statu quo). Voir DONE.md.
7. **TASK-009** mode contrôle — ✅ **livré et approuvé** (matrice d'écarts réelle sur EMA `DT_Id=66` ; type-4 non polluant sur ce jeu, 14 `ManquantRecalcul` tous causés au bug `DT_Id` partiel GRFN).
8. ~~**TASK-028** verrou d'intégration déclaration~~ — ✅ **livré et approuvé** (2026-07-09, re-soumis après rejet PO) : tampon `RT_AFFECTATION.DT_Id` + triggers immuabilité `RT_MOUVEMENT`/`RT_AFFECTATION` (portée globale GRFN inclus). Trou d'intégrité collision `dtId` fermé (détamponnage borné + `& int.MaxValue`). Voir DONE.md.
9. ~~**TASK-024** cache Sage~~ — ✅ **livré et approuvé** (2026-07-09) : cache SQL Server exclusif `GRC_VENTILATION_SAGE_CACHE`, validation du token de paiement local à la lecture, aucun code GRF touché. Voir DONE.md.

> ✅ **Collision couche sélection résolue** : TASK-009 a modifié `SelectionnerAffectationsService` (`JOIN RT_ECHEANCE` pour `DO_Numero`, filtre date conditionné à `dtId`) ; TASK-021 (`SelectionExpliqueeService`, fichier distinct) et TASK-022 touchaient le même périmètre SELECT/JOIN. Les trois sont désormais livrées et approuvées — edits réconciliés, build vert.
>
> **Chemin critique front** : 020 §1 ✅ + 027 ✅ → 021 → 019. **Chemin critique fiabilité/démo** : 022 (+023) → 025 → 009.

## Ordre d'exécution

### ✅ Fait
| Task | Objet | Note |
|---|---|---|
| [TASK-002](TASKS/TASK-002-worker-lecture-facture-taxes-om.md) | Worker OM : lecture facture + taxes | ✅ prouvé sur `DISTRI_DEMO` |
| [TASK-004](TASKS/TASK-004-ventilation-proration-coeur-calcul.md) | Ventilation / proration (calcul pur) | ✅ 9/9 tests — corrections via TASK-006 |
| [TASK-005](DONE_DETAIL/TASK-005-modele-declaration-controle.md) | Modèle déclaration + contrôle | ✅ VERIFY approuvé — contrôle réel (affecté vs ventilé) porté par TASK-006 |
| [TASK-006](DONE_DETAIL/TASK-006-corrections-controle-alertes.md) | Corrections revue (contrôle réel, alerte IF, prorata/désignation) | ✅ VERIFY approuvé — 9/9 tests |
| [TASK-001](DONE_DETAIL/TASK-001-verif-rapprochement.md) | Vérifier synchro rapprochement (source locale fiable ?) | ✅ prouvé sur `GR_EMA_DISTRIBUTION` — local suffisant (352/352, 1008/1008 hors espèces) |
| [TASK-007](DONE_DETAIL/TASK-007-orchestration-cache-pipeline.md) | Orchestration + cache (worker OM ↔ ventilation ↔ modèle) | ✅ VERIFY approuvé — 5/5 tests, cache prouvé, timeout worker robuste |
| [TASK-008](DONE_DETAIL/TASK-008-selection-sql-eligibilite-locale.md) | Sélection SQL : éligibilité locale → `AffectationADeclarer` | ✅ VERIFY approuvé |
| [TASK-015](DONE_DETAIL/TASK-015-selection-expliquee-eligibilite-motifs.md) | Sélection expliquée (surensemble + motifs) | ✅ VERIFY approuvé — test d'intégration formel vs TASK-008 |
| [TASK-013](DONE_DETAIL/TASK-013-front-web-declaration-tva.md) | Front web workflow par domaine (stepper, grilles serveur, checkup, génération) | ✅ VERIFY approuvé — build tsc+vite OK, e2e Playwright vert, statut `Écartée`+motif prouvé |
| [TASK-010](DONE_DETAIL/TASK-010-export-excel.md) | Export Excel (artefact dépôt/archive) — 2 feuilles Détail+Récap | ✅ VERIFY approuvé — `net10.0` pur ClosedXML, 1/1 test vert, .xlsx conforme |
| [TASK-011](DONE_DETAIL/TASK-011-export-xml-simpl-tva.md) | Export XML Simpl-TVA + zip (fichier de dépôt DGI) | ✅ VERIFY approuvé — `net10.0` pur, tests verts, validation IF/ICE et format conforme |
| [TASK-016](DONE_DETAIL/TASK-016-ajustements-ui-drilldown-bulk-filtre.md) | Ajustements UI : drill-down anomalie→grille filtrée + action de masse sur filtre | ✅ VERIFY approuvé — e2e Playwright vert, screenshots fournis |
| [TASK-014](DONE_DETAIL/TASK-014-mapping-domaines-schemas-xml-dgi.md) | Cadrage mapping domaines → formulaires/schémas XML DGI | ✅ VERIFY approuvé — décaissement regroupé Relevé de déductions, schéma CA escaladé PO |
| [TASK-012](DONE_DETAIL/TASK-012-api-tva-clean-architecture.md) | API TVA (ASP.NET Core, Clean Arch GRC_WEB, JWT) — workflow : persistance + endpoints paginés + état/ligne + checkup + clôture gardée | ✅ VERIFY approuvé — captures réelles vérifiées vs source ; transparence prouvée (fixture 2 éligibles + 3 écartées-avec-motif), clôture bloquante réelle, injection SQL corrigée, génération/contrôle délégués 501 → TASK-009/010/011 |
| [TASK-017](DONE_DETAIL/TASK-017-cablage-selection-prod-montants-reels.md) | Câblage sélection prod réelle + montants réels dans le figeage API (avenant TASK-012) | ✅ VERIFY approuvé — sélection réelle `GR_EMA_DISTRIBUTION` (écartées+motif), forfait 20 % retiré (montants worker OM, cas 10 % prouvé), comparaison source↔figé identique, checkup 0/clôture OK, isolation `RT_*` |
| [TASK-018](DONE_DETAIL/TASK-018-refonte-checkup-hub-tour-controle.md) | Refonte Checkup en hub « tour de contrôle » (remplace le tunnel) — front, mock-first | ✅ VERIFY approuvé sur mock — hub, ventilation 5 segments (écart 0), tuiles, clôture verrouillée, build/e2e OK. **⛔ Concept ABANDONNÉ (PO 08/07) → superseded par TASK-019** (héros d'agrégats jugé « n'importe quoi » pour un comptable) |
| [TASK-022](DONE_DETAIL/TASK-022-routing-ectype-lecteur-fgr-sql.md) | Routing `EC_Type` + lecteur FGR SQL (111→`RT_HISTCOMPTA`, 0→OM, 4→alerte) | ✅ VERIFY approuvé — taux via `F_TAXE`/Sage (2 connexions, sans parsing), Σ(HT+TVA)=TTC, build 0 erreur + Orchestration.Tests 28/28 (rejoués par l'architecte). **✅ Preuve réelle fournie (2026-07-09, re-jouée en direct par l'architecte sur `.\sql2022`) : `FF260070`/`EC_Id=22297` = 451.80=376.50(D20)+75.30 (spec §47), part hors OM 56/745=7,52 %, perf 56 FGR ≈0,28 s ; multi-taux/exo absents de la base (86 FGR mono-bucket) → tests unitaires. Dette Core.Tests → TASK-026 ✅** |
| [TASK-027](DONE_DETAIL/TASK-027-conformite-if-ice-dto-lignes.md) | Conformité IF/ICE calculée au DTO + extraction du validateur TASK-011 (source unique) | ✅ VERIFY approuvé — `ValidationIdentiteFiscale` (source unique), exporter rebranché sans régression, `Conformite` exposé au DTO ; build 0 erreur + Core.Tests 25/25 + Export.Xml.Tests 4/4 (rejoués par l'architecte). Réserves non bloquantes : warnings xUnit1012/CS8602, Core.Tests hors `.slnx` (TASK-026) |
| [TASK-021](DONE_DETAIL/TASK-021-selection-reportees-non-rapprochees.md) | Avenant back (Gap B) : sélection des reportées (non-rapprochées `MV_Point≠Oui`) marquées `Reportee` + motif | ✅ VERIFY approuvé — SQL miroir (3 domaines), garde-fous universels priment sur `NonRapproche` (motif honnête, règle n°1), mapping `Reportee`, volume réel 304/1309 réconcilié ; Selection.Tests 13/13 hors DB (rejoués par l'architecte). **⚠️ Réserves : test d'intégration + volume 304 sur base prod live non re-jouable côté architecte ; H1 non bornée / H2 sortie manuelle (assumés 1er tour)** |
| [TASK-019](DONE_DETAIL/TASK-019-poste-travail-declaration-4-interrogations.md) | Poste de travail 4 interrogations (Rapprochement · Affectation · Conformité IF/ICE · Factures 2 faces) — front, API réelle ; supersède TASK-018 | ✅ VERIFY approuvé — 5 captures e2e Playwright (dont actions de masse + clôture verrouillée grisée), réconciliation 5 segments `total = candidates` (TASK-013), 3 vues de preuves différenciées, build tsc+vite + oxlint OK. Bug **TVA 0%** diagnostiqué = correctif back Gap A (`Ventilateur.cs`/`LecteurTvaFgr.cs`) **extrait → TASK-030** (preuve données réelles `GR_EMA_DISTRIBUTION`). |
| [TASK-009](DONE_DETAIL/TASK-009-mode-controle-vs-grfn.md) | Mode Contrôle : recalcul corrigé vs `RT_LigneDeclarationTva` (écarts GRFN) — `Declaration.Controle` | ✅ VERIFY approuvé — comparaison réelle EMA `DT_Id=66` (06/2026) : 3 173 concordants (99,6 %), 14 `ManquantRecalcul` (bug `DT_Id` partiel GRFN, causés), 95 `ManquantGRFN` (sauts silencieux, Σ\|AF\|=475 811,82 MAD). Corrections `SelectionnerAffectationsService` (JOIN `RT_ECHEANCE`→`DO_Numero` + filtre date conditionné `dtId`) vérifiées en source ; repo `SELECT` seul (lecture seule stricte) ; build 0 erreur. Réconciliation montants §2/§3.2/§5 corrigée avant approbation. |
| [TASK-023](DONE_DETAIL/TASK-023-lecture-sage-session-reutilisee.md) | Session Sage réutilisée (`EC_Type=0`) : 1 `Open()` → boucle `ReadPiece` → 1 `Close()`, cache devise/codes taxe, **timeout par pièce**, isolation d'erreur | ✅ VERIFY approuvé — thread STA unique (aucun `Parallel.ForEach`), pièce KO → `EnErreur`+motif (alerte `FACTURE_ILLISIBLE_OM`, jamais matérialisée en cache), N traité = N demandé. Preuves réelles `DISTRI_DEMO` : **12/12 montants identiques** (unitaire vs batch), **30,04 s → 2,84 s (×10,6)**. Build 0 erreur (rejoué par l'architecte), 76 tests / 0 échec. Réserves non bloquantes : timeout pièce interrompt le reste du lot (transparent, jamais silencieux) ; perf sur base démo 12 pièces (extrapolation prod argumentée). |

### 🎯 Track A — front poste de travail
✅ **Terminé** — TASK-019 livrée et approuvée (voir table ✅ Fait). Correctif back associé Gap A TVA : **[TASK-030](DONE_DETAIL/TASK-030-correction-gap-a-tva.md)** — ✅ **livré et approuvé** (2026-07-09).

#### 🖥️ Refonte front rappro/TVA — retour PO 09/07/2026 (module déclaration seul, GRC = simple exemple de style)
Décisions PO actées : **menu groupé multi-entrées à valeur ajoutée** (voir 035) — INTERROGATION (Rapprochement bancaire 🟢 *global* + Factures ⚪) / DÉCLARATION (Déclaration TVA 🟢 + Relevé de déductions ⚪) / À VENIR grisé (RAS · Télédéclaration · Tableau de bord 🔒) ; on **montre** toute la portée, on ne **build** que le cœur. Pivot rapprochement = **par règlement** (TVA sur décaissement) ; traçabilité = **colonnes de preuve inline + preuve à la demande**, **pas de panneau latéral**. Priorité cœur = rapprochement + valorisation TVA (la génération est triviale). Décompilation GénéraFi (09/07/2026) confirme l'architecture (cf. mémoire `generafi-reference-produit-tva`).
> **Ordre d'exécution confirmé (PO)** : 034 → 035 → 036 → 037 → 038 (shell menu remonté en #2 pour rendre la valeur visible tôt).

| Ordre | Task | Nature | Objet | État |
|---|---|---|---|---|
| 2 | [TASK-035](DONE_DETAIL/TASK-035-navigation-deux-entrees-densite.md) | front | **Shell menu groupé** (INTERROGATION / DÉCLARATION / À VENIR) + densité « 0 espace perdu ». Entrées cœur actives (Rappro 🟢, Déclaration 🟢), autres ⚪ placeholder / 🔒 grisées. Câble *Déclaration* → flux existant, *Rapprochement bancaire* → écran TASK-037 (placeholder tant que 037 non livré). | ✅ **livré et approuvé** (2026-07-09) — front-only, build tsc+vite / oxlint 0 erreur, aucune dérive vers les écrans « à venir ». Voir DONE.md. |
| 3 | [TASK-036](DONE_DETAIL/TASK-036-endpoint-interrogation-rapprochement-global.md) | **back** | **Endpoint interrogation rapprochement GLOBAL** (règlement-pivot, lecture seule stricte) : `RT_MOUVEMENT`/`RT_AFFECTATION`/`MV_Point`/`EC_Type`/`DT_Id`, filtré/paginé, **reste à affecter visible**. DTO explicite | ✅ **livré et approuvé** (2026-07-09) — `GET /api/rapprochement` SELECT-only (aucun write/DLL, `DT_Id` lu jamais écrit), reste à affecter exposé, preuve réelle `GR_EMA_DISTRIBUTION`, build 0 erreur + 15/15 tests. **Débloque 037.** Voir DONE.md. |
| 4 | [TASK-037](DONE_DETAIL/TASK-037-ecran-rapprochement-bancaire-front.md) | front | **Écran Rapprochement bancaire** (grille règlement-pivot dense : montant/mode/date/rapproché banque/factures affectées/reste/origine/déclaré + drill preuve TVA) | ✅ **livré et approuvé** (2026-07-09) — grille dense pivot règlement, reste à affecter ≠ 0 rendu visible (orange+⚠), drill-down à la demande, filtres/tri/pagination, build tsc+vite / oxlint OK. **Décision PO : drill-down agrégé (nb factures + montant affecté + reste + origine OM/FGR) accepté** comme niveau d'interrogation ; ventilation facture-par-facture = poste Déclaration. Voir DONE.md. |
| 5 | [TASK-038](DONE_DETAIL/TASK-038-tracabilite-tva-inline-declaration.md) | front (+DTO) | **Traçabilité TVA inline** dans « Factures à déclarer » : colonnes origine (`EC_Type` Sage/FGR/Solde)/taux/motif visibles + `ProofModal` à la demande, sans panneau latéral | ✅ **livré et approuvé** (2026-07-09) — colonne `origine` (source unique `ReglementRapprochementRow.LibelleEcType` partagée avec l'écran Rapprochement), snapshot `EcType` entité→SQL→DTO→front, `ProofModal` inchangé, build back/front/lint + 43/43 tests. Voir DONE.md. |
| 6 | [TASK-041](DONE_DETAIL/TASK-041-ecran-factures-interrogation.md) | back+front | **Écran « Factures » (INTERROGATION, filtre date obligatoire)** : grille facture-pivot (N°/date/fournisseur/réf/HT/TVA/autre taxe/écart/escompte/TTC/solde + statut déclaration 3 valeurs Non déclarable·Partiel·Total). Familles A (`RT_ECHEANCE`, gratuit) + C (`RT_AFFECTATION`/`DT_Id`, statut calculé) réelles ; famille B (HT/TVA/…) **depuis cache TASK-024** ou « non valorisé »+motif. **Lecture seule stricte.** Option 2 (sélection/déclaration partielle actionnable) **différée**. | ✅ **livré et approuvé** (2026-07-09) — `GET /api/factures` SELECT-only (période 400, `DT_Id` lu jamais écrit), `FacturesFromWhere` partagé liste+COUNT+distincts, statut 3 valeurs répliqué à l'identique C#↔SQL, famille B au cache TASK-024 sinon « non valorisé »+motif. Preuve réelle `GR_EMA_DISTRIBUTION` (1408 factures, borne Solde=TTC−Réglé, cache vide→100 % non valorisé, logique Partiel/Total prouvée what-if). Voir DONE.md. |

> ⚠️ Aucune fusion avec GOCOM/`gocom-web` (modules distincts, cf. mémoire `grc-vs-declaration-modules-distincts`). GRC = simple référence de **style** (dense, 0 espace perdu).
> **Ordre PO** : 034 → 035 → 036 → 037 → 038. **Chemin critique cœur** : 034 → **036 → 037** (035 shell et 038 traçabilité peuvent avancer en parallèle après 034, mais 035 est priorisé en #2 pour la visibilité de la valeur).

#### 🧭 Flux Déclaration TVA règlement-first — tunnel 8 écrans (PO 11/07/2026, `reflexion dectva.md`)
Décisions PO actées : **règlement-first assumé** (point d'entrée = les règlements décaissés) ; densité **0 espace perdu** (cohérent 035/037/041) ; justificatif = **`ProofModal` à la demande**, pas de panneau latéral. Front-only + réutilisation massive de l'existant (back déjà livré : sélection/figeage TASK-012/017, verrou TASK-028, valorisation TASK-022/023/024, contrôle TASK-009, exports TASK-010/011, conformité TASK-027). Le tunnel remplace le stepper 2 étapes (`DeclarationStepper.tsx`).

| Ordre | Task | Nature | Objet | État |
|---|---|---|---|---|
| — | _(tunnel 053→059 livré)_ | — | — | ✅ |

> **Séquentiel strict** : 053 → 054 → 055 → 056 → 057 → 058 → 059 (chaque étape consomme la sortie de la précédente). 053 peut être livré seul (étapes en placeholder honnête).
> **Écran §1 « Gestion déclarations »** (liste + création + statut/historique) : **déjà couvert** par `DeclarationList.tsx` + `CreateDeclarationModal.tsx` (TASK-012/013) — enrichissement colonnes (Lignes/TVA/Actions) à traiter dans 053 si manquant, pas de task dédiée.
> **Écran §7 « Justificatif »** : **pas de task dédiée** — `ProofModal` existant (TASK-038) enrichi timeline dans 055 (décision PO : preuve à la demande, pas de panneau latéral).
> ⚠️ Garde-fou règlement-first : ne **jamais** filtrer `MV_DECAISSE=1` (trou ~465 factures/mois, mémoire `grf-trou-selection-mv-decaisse`) — utiliser `MV_Domaine IN (0,1)`.

#### 🗺️ Roadmap produit — APRÈS le cœur (décision PO 09/07/2026)
Jalons **planifiés pour plus tard** (à cadrer en TASKS le moment venu, **pas maintenant** — priorité = rapprochement + valorisation TVA). Repère : GénéraFi (`generafi-reference-produit-tva`), sans copier.

| Jalon | Domaine | Note de cadrage (à approfondir au lancement) |
|---|---|---|
| R1 | **RAS fournisseurs** (Retenue à la Source) | Nouveau domaine de calcul + sélection ; s'appuie sur le même socle règlement/affectation. |
| R2 | **Télé-Déclaration** (SIMPL) | Dépend d'une déclaration complète et figée (verrou `DT_Id` déjà en place) ; format/transport à spécifier. |
| R3 | **Tableau de bord / indicateurs** | TVA Collectée / Récupérable / **Due**, courbes (Solde TVA, RAS). Dépend de la disponibilité des données Récupérable + RAS (donc après R1). Peut être plus aéré que les écrans de travail. |

> **Hors roadmap confirmée** (non mentionnés — restent à décider) : intégration **Comptabilité**, Sauvegarde/Restauration, Assistance. **TVA Collectée (ventes/clients) reclassée 14/07/2026 → [TASK-084](DONE_DETAIL/TASK-084-ouverture-domaine-reglements-clients-tva-collectee.md) — ✅ livrée et approuvée 14/07/2026** (demande PO explicite ; domaine ouvert, distinction stricte, XML DGI hors périmètre permanent).
> ⚠️ Ne pas dériver : on **finit 034→038** avant d'ouvrir R1/R2/R3.

### ⚡ Track C — Perf & sources de TVA par `EC_Type` (retour worker TASK-009)
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-023](DONE_DETAIL/TASK-023-lecture-sage-session-reutilisee.md) | **Session Sage réutilisée** (Type 0) : 1 `Open()` → boucle `ReadPiece` → 1 `Close()` + cache devise/codes taxe ; **pas** de `Parallel.ForEach` | ✅ **livré et approuvé** (2026-07-09) — thread STA unique, **timeout par pièce**, pièce KO isolée (`EnErreur`+motif → alerte `FACTURE_ILLISIBLE_OM`) sans perte. Preuves réelles `DISTRI_DEMO` : 12/12 montants identiques, **30,04 s → 2,84 s (×10,6)**. Voir DONE.md. |
| 2 | [TASK-028](DONE_DETAIL/TASK-028-verrou-integration-declaration-dt-id.md) | **Verrou d'intégration déclaration** : tampon `RT_AFFECTATION.DT_Id` + triggers immuabilité `RT_MOUVEMENT`/`RT_AFFECTATION` (portée globale, GRFN inclus ; réouverture = `DT_Id→NULL`) | ✅ **livré et approuvé** (2026-07-09) — re-soumis après rejet PO (trou d'intégrité collision fermé). Voir DONE.md. |
| 3 | [TASK-024](DONE_DETAIL/TASK-024-cache-ventilations-sage-check-delta.md) | **Cache ventilations Sage (gel par le paiement, validé à la lecture)** : fraîcheur = facture clôturée Sage au règlement (pas la déclaration) ; matérialisée à la 1re lecture, relue 100 % SQL ; validation du token de paiement local avant de servir (aucun code GRF ni trigger). `cbModification` abandonné (PO) | ✅ **livré et approuvé** (2026-07-09) — cache **SQL Server exclusif** (0 SQLite), IT1-IT6 sur `GR_EMA_DISTRIBUTION`. |
| 4 | [TASK-025](DONE_DETAIL/TASK-025-cadrage-tva-solde-initial.md) | **Cadrage TVA solde initial** (`EC_Type=4`) : inventaire réel + décision PO | ✅ **livré et approuvé** (2026-07-09) — 34 lignes / 854 793 MAD, 0 TVA déclarable ; **décision PO = maintenir l'alerte** `SOLDE_INITIAL_NON_GERE` (statu quo, aucune implémentation). Voir DONE.md. |

### ⏸️ Backlog différé — domaines non gérés par le client (PO 09/07/2026)
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-031](TASKS/TASK-031-domaine-operation-bancaire-tva.md) | **Opération bancaire (frais bancaire) avec TVA** : domaine de déduction entièrement absent de notre code (legacy `GetDeclarationCommissionBancaire`, `<mp><id>=3`) → sélection + valorisation directe + intégration workflow/exports | ⏸️ **différé** — le client ne gère pas ce cas aujourd'hui ; hors chemin critique. |
| 2 | [TASK-032](TASKS/TASK-032-revue-depense-avec-tva.md) | **Revue dépense avec TVA** : (a) absence de filtre `WithTva` dans `GetDepenseSql`, (b) divergence de calcul (notre ventilation/prorata vs legacy direct `depense.MontantTva`) → vérifier + aligner sur base réelle | ⏸️ **différé** — à traiter avec TASK-031. |

> **Origine** : analyse d'écart ancien GRF ↔ notre code (09/07/2026). Le legacy a 3 sources de déduction (décaissement, dépense, **frais bancaire**) ; notre module en couvre 2 (frais bancaire absent) et traite la dépense différemment. Différé car non prioritaire pour le client.

### 🚧 Blocage valorisation famille B (Sage OM) — isolé par la traçabilité (10/07/2026)
| # | Task | Objet | État |
|---|---|---|---|
| 5 | [TASK-060](TASKS/TASK-060-remontee-claire-erreurs-valorisation.md) | **Remontée claire des erreurs de valorisation à l'utilisateur** : le rafraîchissement facture-first juin 2026 affiche `240 en erreur` en agrégat, sans qu'aucun écran ne détaille la répartition par code. **Preuve code** : le backend calcule et transmet déjà le détail complet (`RapportValorisation.Erreurs[]`, `Code`/`Message`/`RefLigne`, `FacturesController.cs:107-126`), mais le front (`FactureInterrogation.tsx:174-196`) ne fait qu'un `console.warn` + toast agrégé renvoyant vers « la console / les logs serveur ». Objectif : agrégation par code + affichage lisible (modale/panneau), distinguant qualité-de-données tiers (`TIERS_SANS_ICE`/`IF`) des anomalies applicatives (`ERREUR_FGR`, `FACTURE_ILLISIBLE_OM`...). | 🎯 **prêt** — demande PO 11/07 ; lecture seule, aucune logique de valorisation touchée. Dépend de TASK-050/052 (DONE). |
| 6 | [TASK-061](DONE_DETAIL/TASK-061-montants-float-vers-decimal.md) | **Montants persistés en FLOAT au lieu de DECIMAL** : `dbo.LigneCandidate` (`DeclarationTVA.sql:49-54,70,74`) déclare `HT`/`Taux`/`TVA`/`TTC`/`Prorata`/`MontantAffecte` en `FLOAT`, alors que le modèle C# est déjà en `decimal` (`WorkflowEntities.cs:56-67`). Désalignement de type sur des montants de TVA → arrondis binaires + conversion Dapper. Passer en `DECIMAL(18,6)` (CREATE + ALTER ADD + commentaire) **et** ajouter un bloc `ALTER COLUMN` idempotent pour migrer les bases déjà déployées. Aucune logique C# touchée. | ✅ **implémenté** (2026-07-13) — DECIMAL(18,6) appliqué, migration idempotente avec gestion dynamique des contraintes de défaut incluse. Voir DONE.md. |
| 7 | [TASK-064](DONE_DETAIL/TASK-064-trigger-immuabilite-totale-affectation-declaree.md) | **Immuabilité TOTALE d'une affectation déclarée (trigger)** : le verrou TASK-028 (`003_Verrou_DT_Id.sql:83-117`, bloc 1b) ne bloque l'UPDATE que des colonnes **financières** (`AF_Montant`/`MV_Id`/`EC_Id`/`AF_Date`) d'une affectation `DT_Id NOT NULL` → toute autre colonne reste modifiable. Généraliser le bloc 1b : **tout** UPDATE refusé si `DT_Id NOT NULL` avant ET après, **sauf** le dé-tamponnage pur `DT_Id : valeur → NULL` (réouverture, à préserver pour `ReouvriDeclarationAsync`). DELETE (1a) et trigger `RT_MOUVEMENT` (2) inchangés. Fichier unique. | ✅ **implémenté** (2026-07-13) — Trigger d'immuabilité totale implémenté avec exception exclusive de dé-tamponnage pur. Voir DONE.md. |
| 8 | [TASK-065](DONE_DETAIL/TASK-065-renommage-tables-conformes-dm-enttva-dm-lgtva.md) | **Renommer les tables en `DM_ENTTVA` / `DM_LGTVA` (nommage conforme)** : `DeclarationEntete`/`LigneCandidate` sont en PascalCase C# non conforme à la convention legacy (`PREFIXE_XX_`). Renommer **uniquement les tables SQL** (DDL `DeclarationTVA.sql` + index/FK + 22 littéraux `DeclarationRepository.cs`) via `sp_rename` idempotent ; **ne pas** toucher aux classes C#/DTO/TS (Dapper mappe par colonne). **Supprime aussi `001_Schema_TVA.sql`** (mort + incompatible `UNIQUEIDENTIFIER`). | ✅ **implémenté** (2026-07-13) — Tables, index et contraintes renommés via `sp_rename` idempotent, `001_Schema_TVA.sql` supprimé, repository mis à jour. |

> **Isolé grâce à la traçabilité** (mémoire `grf-valorisation-tracabilite-blocage-om`) : `logs/valorisation.log` + rapport HTTP `{ facturesTraitees, nbErreurs, erreurs[] }` + toast front. Les 30 `TIERS_SANS_ICE` + 30 `TIERS_SANS_IF` sont de la qualité de données, **pas** ce blocage.

### 🧹 Dette technique
✅ **Terminé** — TASK-026 livrée et approuvée (2026-07-09) : `Declaration.Core.Tests` compilable et vert (28/28, dont 3 tests chemin FGR), 6 projets de tests intégrés au `.slnx` (`Controle.Tests` filtrable sans DB). Build/test solution exhaustif rejoué par l'architecte (0 erreur, 75 réussites hors DB). Voir DONE.md.

| # | Task | Objet | État |
|---|---|---|---|
| 2 | [TASK-039](DONE_DETAIL/TASK-039-filtre-mv-domaine-rapprochement.md) | **Filtre `MV_Domaine` manquant sur l'endpoint rapprochement (TASK-036)** : un bordereau de remise `BORD26060022` remonte dans la liste des règlements. `RapprochementFromWhere` ne filtre pas la nature du mouvement → ajouter `MV_Domaine IN (0,1)` (0=encaissement, 1=décaissement), écarter bordereaux/virements/alimentations. Régression : filtre présent en TASK-001, perdu en TASK-036. | ✅ **livré et approuvé** (2026-07-09) — filtre `MV_Domaine IN (0,1)` sur `RapprochementFromWhere` (liste+COUNT partagés) + 2 sous-requêtes `distincts` ; preuve réelle `.\sql2022`/`GR_EMA_DISTRIBUTION` (BORD absent, 19 non-règlements exclus, 0 NULL, 694 règlements conservés), 0 erreur + 15/15 tests. Voir DONE.md. |
| 3 | [TASK-040](DONE_DETAIL/TASK-040-compteur-reglements-vs-filtres-grid.md) | **Compteur « Règlements : N » incohérent avec les filtres de la grille (rappro)** : `total` = `totalCount` serveur, alors que `numeroReglement`/`origine`/`domaine` sont filtrés **côté client sur la page seule** (`RapprochementInterrogation.tsx:146-157`) → compteur ≠ liste affichée. Fix : pousser ces 3 filtres **côté serveur** (WHERE partagé liste+count) pour rendre `totalCount` exact. | ✅ **livré et approuvé** (2026-07-09) — 3 filtres poussés dans `RapprochementFromWhere` (liste+COUNT), expressions `CASE` origine/domaine répliquant à l'identique `LibelleOrigine`/`LibelleDomaine`, filtrage client supprimé, multi-sélection `string[]` bout-en-bout. Build 0 erreur + 43/43 tests. Voir DONE.md. |
| 4 | [TASK-043](TASKS/TASK-043-tva-par-reglement-liste-rapprochement.md) | **Montant de TVA par règlement dans la liste de rapprochement (FGR + Sage)** : afficher, par règlement, la TVA de la facture/FGR. Enrichissement **de la page courante seule** (borné `size`), FGR via `LecteurTvaFgr`, Sage via cache TASK-024 + fallback session réutilisée TASK-023 ; prorata affectation partielle, somme multi-facture, état de valorisation explicite (jamais un `0` muet). Lecture seule, hors `COUNT`/tri. | ⏸️ **bloqué** — renommée TASK-041→**043** (collision avec l'écran Factures). back (projection + service + DTO endpoint TASK-036) + front. **Séquencer après TASK-039 et TASK-040** (mêmes projection/DTO ; 039 non livrée). Périmètre PO = FGR + Sage complet. |
| 5 | [TASK-062](TASKS/TASK-062-suppression-bouton-preuve-affectations.md) | **Supprimer le bouton « Preuve » de l'écran ② Affectations** : la modale `ProofModal` (3 onglets, `DomainGrid` filtré sur le n° rapprochement) est **redondante** avec la carte inline `FactureCard` — qui affiche déjà la preuve complète (payé÷TTC=%, TVA par taux, IF/ICE, conformité, origine) — et **non significative** (dump générique type 776 lignes `Encaissement/Proposée`, titre confondant règlement/rapprochement). Retirer bouton + câblage `ProofModal` dans `AffectationsDrill.tsx` **uniquement**. `DomainGrid.readonly` conservé (utilisé ailleurs). Front-only. | 🎯 **prêt** — décision PO 13/07 ; **recadré 13/07 (architecte)** : `ProofModal.tsx` **conservé** (partagé avec `WorkstationPanel.tsx` l.5/228 — sa suppression casserait le build) ; option « descendre aux lignes sources » écartée (tâche distincte si besoin). |
| 6 | [TASK-069](DONE_DETAIL/TASK-069-correctif-mise-en-page-ecran-calcul-tva.md) | **Correctif mise en page écran ③ Calcul TVA** : bandeau pied (total TVA à intégrer) rogné par la barre du stepper — conteneur `DeclarationStepper.tsx:155` (`flex:1`) sans `minHeight:0`, anti-pattern flexbox classique. Décision PO 13/07 : **option A** — bandeau interne du panneau conservé tel quel, correctif limité au layout. | ✅ **livré et approuvé** (2026-07-13) — `minHeight:0` ajouté au conteneur d'étape partagé, preuve visuelle PO confirmée (bandeau total visible, aucun recouvrement stepper). Voir DONE.md. **A révélé un 2e bug distinct** (bloc « Sous-totaux par taux » invisible, écrasé par le tableau factures) → extrait vers **TASK-070**. |
| 7 | [TASK-070](DONE_DETAIL/TASK-070-allegement-contenu-ecran-calcul-tva.md) | **Alléger l'écran ③ Calcul TVA** : retirer le tableau détail facture (déjà disponible en ② Affectations, colonne Taux filtrable + total TVA) ; ne garder que le bloc « Sous-totaux par taux TVA » + bandeau pied total. Corrige au passage le bug de visibilité du bloc sous-totaux (plus rien pour l'écraser) et la redondance signalée par le PO. Décision PO 13/07 : option retenue = alléger ③ (pas de fusion ②+③). | ✅ **livré et approuvé** (2026-07-13) — capture PO réelle (68 règlements/245 lignes), build 0 erreur. Voir DONE.md. |

### 🎨 Identité visuelle & branding produit (PO 13/07/2026)
Demande PO : la page d'authentification affiche aujourd'hui « Déclaration TVA » alors que la TVA n'est qu'un premier module d'une plateforme appelée à en accueillir d'autres (roadmap R1 RAS / R2 Télé-Déclaration / R3 Tableau de bord, cf. §Roadmap ci-dessous) — le nom de produit doit être distinct du nom de module. En parallèle, léger affinage du thème visuel (rester **simple**, mais choisir une teinte **élégante et propre au produit** plutôt que le bleu Material générique), sans toucher aux écrans denses existants. Direction retenue (clarification architecte 13/07) : **« Sobre + touche couleur signature »** (indigo profond + monogramme sidebar, pas de mode sombre).

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-083](DONE_DETAIL/TASK-083-theme-visuel-sobre-signature-declaratif-maroc.md) | **Thème visuel sobre + couleur signature** : nouvelle teinte d'accent (indigo profond, variables CSS `index.css`) + monogramme sidebar (`App.tsx`). Zéro écran de grille dense retouché. | ✅ **livré et approuvé** (2026-07-14) — 5 variables CSS + monogramme « DM », grep 0 résidu, build front 0 erreur. Voir DONE.md. |

> ✅ **TASK-093 (renommage titre auth « Déclaratif Maroc ») + TASK-083 (thème visuel) livrées et approuvées** (14/07/2026, front-only, cf. `DONE.md`). TASK-093 renumérotée depuis 082 (n° déjà pris par le bandeau incohérence).

### 🆕 Ouverture domaine TVA Collectée — règlements clients (PO 14/07/2026)
Demande PO : le step1 (écran ① Règlements) ne traite que les règlements **fournisseurs**
(`MV_Domaine=1`) ; il faut aussi gérer les règlements **clients** (`MV_Domaine=0`), avec les
mêmes règles d'éligibilité, en les **distinguant** (TVA collectée ≠ TVA déductible). Reclasse
« TVA Collectée » hors de la case roadmap « non mentionnés — restent à décider » (cf. §Roadmap
ci-dessous) — **à faire confirmer explicitement par le PO** avant lancement (arbitrage vs R1/R2/R3).

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-084](DONE_DETAIL/TASK-084-ouverture-domaine-reglements-clients-tva-collectee.md) | **Ouverture domaine règlements clients (`MV_Domaine=0`)** dans le tunnel, distinction stricte TVA collectée/déductible ; encaissement client exclu de l'export XML (hors périmètre permanent, confirmé PO). | ✅ **livré et approuvé** (2026-07-14) — preuve réelle `GR_EMA_DISTRIBUTION` (client `FA2600407` figé, totaux distincts, XML excluant la collectée), tests 56/56 + 5/5. Voir DONE.md. |

### 🔍 Écran ④ Intégration — récap vide, écart sans détail, avertissements tronqués (PO 14/07/2026)
Demande PO (capture écran ④ Intégration) : (1) le bloc « Récapitulatif de l'intégration » en haut
s'affiche vide ; (2) le contrôle « Équilibre comptable » affiche un écart global sans détail et
son libellé prête à confusion ; (3) les avertissements « Ligne exclue : ... » n'indiquent ni
référence ni montant. Analyse code (architecte) : dans les 3 cas, le backend expose déjà les
données utiles (`recapSource`/`recapTaux`/`refLigne`/`filtre` via `/checkup`), mais
`IntegrationPanel.tsx` ne les consomme pas (troncature d'affichage / état front non robuste), pas
un manque de données en base ou en API.

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-086](DONE_DETAIL/TASK-086-recap-integration-vide-fallback-api.md) | **Récap vide** : source des chiffres = réponse `/checkup` (déjà chargée) au lieu des props volatiles de l'étape ③, robuste au rechargement de page / ouverture directe sur ④. | 🔀 **absorbée par TASK-090** (la fusion ③+④ supprime le pipe d'état, cause racine ; critères repris tels quels). |
| 2 | [TASK-087](DONE_DETAIL/TASK-087-detail-ecart-equilibre-comptable.md) | **Détail de l'écart + renommage** du contrôle en « Cohérence des totaux déclarés » (libellé acté PO 14/07) : composant partagé `RecapSourceTable` monté sous le badge BLOQUANT. | ✅ **done** — 14/07/2026 (écrans « Vérifier & Intégrer » ④ + « Déclaration » ⑤). |
| 3 | [TASK-088](DONE_DETAIL/TASK-088-detail-avertissements-lignes-exclues.md) | **Détail des avertissements** : afficher `refLigne` (référence facture/pièce, déjà renvoyée par l'API) pour chaque « Ligne exclue », drill-down optionnel via le mécanisme TASK-016. | ✅ **done** — 14/07/2026 (approuvée architecte). |

> **Révision 14/07/2026 (PO « simplifier au maximum »)** : TASK-087 revue pour extraire un
> composant partagé (`RecapSourceTable`) réutilisé par ④ et ⑤ au lieu de dupliquer le rendu, un
> seul libellé retenu (pas d'option à trancher), `recapTaux` écarté du périmètre ④ (déjà visible
> sur ⑤). Audit confirmé : ③ (avant figeage, calcul front) et ⑤ (après figeage, calcul back) sont
> **volontairement** distincts (contrôle a posteriori) — pas de simplification à faire de ce
> côté-là ; ④ n'affichait aucun tableau par taux avant cette révision (juste un total repris de ③).
> Aucune dépendance bloquante entre les 4 tasks ; TASK-086/087/088 touchent `IntegrationPanel.tsx`
> (zones distinctes) — séquencer si livrées ensemble pour éviter les conflits d'édition.
>
> **Supersédé partiellement (PO 14/07/2026, décision tunnel 3 étapes)** : voir §Refonte tunnel
> ci-dessous — TASK-086 absorbée par TASK-090 ; TASK-087/088 s'exécutent après les fusions
> (ancrages retargetés). Le seul conflit d'édition réel identifié (086/087 sur `interface
> CheckupResult`) est dissous par l'absorption.

### 🧭 Refonte tunnel 6→3 étapes (PO 14/07/2026)
Décision PO actée (14/07/2026) : cible **3 étapes** — « ① Sélection · ② Vérifier & Intégrer ·
③ Déclaration » — retenue parmi 4 options après inventaire décisionnel des 6 écrans (architecte).
Constat : seules ① (choix des règlements) et ④ (acte d'engagement `POST /cloture`) portent une
décision ; ③ et ⑤ sont 100 % passifs (traversés d'un clic) ; ② n'a de valeur décisionnelle que
sur incohérence Sage (TASK-077/078, préservée en drill) ; ⑤ et ⑥ font le même `GET /checkup` ; le
bouton « Clôturer » de ⑥ est **mort par construction** (l'engagement unique a déjà lieu en ④,
garde back `EnCours` — vérifié `DeclarationWorkflowService.cs` l.799-826). La distinction
avant/après figeage (audit ③/⑤, cf. §écran ④ ci-dessus) est **conservée** : « Vérifier &
Intégrer » = avant figeage (agrégation front), « Déclaration » = après (checkup back).

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-090](DONE_DETAIL/TASK-090-fusion-calcul-integration-verifier-integrer.md) | **Fusion ③+④ → « Vérifier & Intégrer »** (sous-totaux par taux + check-list + engagement) — absorbe TASK-086, supprime le pipe d'état ③→④. | ✅ **done** — 14/07/2026. |
| 2 | [TASK-091](DONE_DETAIL/TASK-091-fusion-controle-synthese-declaration.md) | **Fusion ⑤+⑥ → « Déclaration »** (contrôle post-figeage + exports, un seul `/checkup`) + retrait du bouton « Clôturer » mort. | ✅ **done** — 14/07/2026. |
| 3 | [TASK-092](DONE_DETAIL/TASK-092-affectations-drill-a-la-demande-tunnel-3-etapes.md) | **② Affectations → drill à la demande** depuis ① + barre finale 3 étapes + redirection auto vers « Déclaration ». | ✅ **done** — 14/07/2026 (tunnel final 3 étapes atteint). |

> **Ordre d'exécution consolidé (PO + architecte 14/07/2026)** :
> ~~093~~ (✅ fait) → 083 → 085 → 089 → ~~090~~ (✅ fait) → ~~091~~ (✅ fait) → ~~092~~ (✅ fait — tunnel 3 étapes)
> → ~~087~~ (✅ fait) → ~~088~~ (✅ fait) → ~~084~~ (✅ fait — domaine TVA Collectée ouvert, reclassement roadmap acté PO).
> Les 3 fusions sont front-only (aucun endpoint modifié) ; e2e Playwright à adapter à chaque
> fusion (étapes supprimées de la barre).

### 🎛️ UX grilles — filtres « valeurs disponibles » & sélecteur de colonnes (PO 13/07/2026)
Demande PO (capture écran ② Affectations) : (1) chaque filtre de colonne doit proposer **la liste des valeurs réellement présentes** (type Excel : cases + recherche) sur **toutes** les listes ; (2) **sélecteur de colonnes** persistant (`localStorage`) sur toutes les listes. `ExcelFilter` gère déjà le mode `'list'` — le travail porte sur l'alimentation des options et un nouveau composant colonnes.

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-068](DONE_DETAIL/TASK-068-selecteur-colonnes-persistant-listes.md) | **Sélecteur de colonnes persistant (`localStorage`) sur toutes les listes** : hook `useColumnPrefs` + composant `ColumnSelector` réutilisables, câblés aux 6 grilles (clé de stockage par écran). Rend entête+corps sur `visibleColumns`, repli sûr si préférence absente. Front-only. | ✅ **livré et approuvé** (2026-07-13) — 6 grilles câblées, entête/corps cohérents (y compris virtualisé), repli sûr, build tsc+vite/oxlint 0 erreur, e2e Playwright vert. Réserve non bloquante : `DomainGrid` partage une clé unique entre ses 3 contextes d'usage. Voir DONE.md. |

> ⚠️ TASK-068 touche **les mêmes fichiers de grille** que TASK-067A/B (`ReglementsSelection`, `FactureInterrogation`, `RapprochementInterrogation`, `AffectationsDrill`, `DomainGrid`, `ControlGrid`), déjà stabilisés. Aucune dépendance bloquante restante.

### 🔐 Gouvernance & traçabilité
✅ **Terminé** — TASK-074 (authentification réelle GRF), TASK-073 (garde `UT_Admin=1` + audit
réouverture) et TASK-079 (suppression déclaration EnCours + écran liste enrichi) livrées et
approuvées (2026-07-13), dans cet ordre. Voir DONE.md. Périmètre D (test d'intégration automatisé)
de TASK-073 non traité — non bloquant, laissé à la discrétion du PO. **TASK-079 porte une réserve
non levée** (aucune preuve réelle en base rejouée dans cette session, cf. DONE.md) — à confirmer par
le PO avant usage réel en suppression. **Complément** : TASK-080 (exclusivité règlement entre
déclarations `EnCours` concurrentes) ✅ **livrée et approuvée** (2026-07-14, preuve réelle rejouée
contre `.\sql2022`/`GR_EMA_DISTRIBUTION`) — voir `DONE.md`.

> ⚠️ **Incident 14/07/2026** : garde-fou TASK-079 déclenché en réel sur `TVA1-2026-01` (610
> affectations tamponnées par des `DT_Id` orphelins, sans lien avec aucune déclaration existante) —
> corrigé en données (hors code, cf. `CHANGELOG.md`). Gap de conception révélé (aucune traçabilité
> persistée de `DT_Id` → déclaration) → **[TASK-094](DONE_DETAIL/TASK-094-tracabilite-dt-id-diagnostic-orphelins.md)
> — ✅ livrée (Option A + Option B)** (2026-07-14, implémentée en rôle worker exceptionnel, demande
> explicite PO) : diagnostic lecture seule `GET /api/diagnostic/dt-id` (`UT_Admin=1`) **et** colonne
> persistée `DM_ENTTVA.DT_Id` (posée à la clôture, effacée à la réouverture, migration `008_DM_ENTTVA_DT_Id.sql`
> appliquée réellement sur `GR_EMA_DISTRIBUTION`).
>
> ✅ **Point résolu (découvert en vérifiant TASK-094, refermé par la vérification de TASK-095)** :
> le diagnostic rejoué en réel avait montré que le recalcul manuel fait pendant l'incident avait
> probablement utilisé un runtime .NET différent de la production et avait donc qualifié à tort
> d'« orphelin » une des 3 valeurs `DT_Id` nullifiées (`830393939`, 10 lignes — appartenant en
> réalité à `TVA1-2026-06`, `Cloturee`, pas à une 3ᵉ déclaration disparue). Le cycle réouverture/
> reclôture réel exécuté pendant la vérification de TASK-095 a **naturellement re-posé** ce tampon
> (`CloturerDeclarationAsync` re-tamponne toujours toutes les affectations `DT_Id IS NULL` de la
> déclaration) : confirmé en base, `RT_AFFECTATION` porte de nouveau `830393939` (11 lignes) et
> `DM_ENTTVA.DT_Id` de `TVA1-2026-06` vaut désormais `830393939` — cohérents entre eux. Les 2 autres
> valeurs (`900191953`/56 lignes, `1538151731`/544 lignes) restent des orphelins **confirmés**, sans
> déclaration correspondante — 3ᵉ déclaration disparue toujours non identifiable, aucune action
> possible dessus. L'anomalie annexe notée au §5 de la task (« `TVA1-2026-06` ne porte nulle part
> son propre tampon réel ») était donc **fausse** — invalidée. Détail complet :
> `DONE_DETAIL/TASK-094-tracabilite-dt-id-diagnostic-orphelins.md` (section « Constat significatif »).

> 🆕 **[TASK-095](DONE_DETAIL/TASK-095-bouton-reouverture-declaration-cloturee.md)** — bouton
> « Réouvrir » en UI (Périmètre C de TASK-073) — ✅ **livrée et approuvée** (2026-07-14) : bouton
> visible `isAdmin && Cloturee` dans `DeclarationList.tsx` (symétrique à Supprimer), confirmation
> non édulcorée, restitution de traçabilité **C1** (toast utilisateur+horodatage, C2 différé sans
> demande PO explicite). Front-only, aucune ligne touchée dans `ReouvriDeclarationAsync`/
> `JournaliserAudit`/le trigger d'immuabilité. Preuve réelle rejouée (`.\sql2022`/
> `GR_EMA_DISTRIBUTION`) : réouverture déclenchée par le bouton, ligne `[REOUVERTURE]` dans
> `audit.log`, 403 backend confirmé pour un compte non-admin. **Réserve non bloquante** : la
> capture non-admin montre une liste vide plutôt que la même liste avec boutons masqués — preuve
> UI faible sur ce point précis, mais la frontière de sécurité réelle (403 backend) est prouvée.

### 📘 Documentation
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-029](TASKS/TASK-029-guide-fonctionnel-accessible-app.md) | **Guide fonctionnel accessible depuis l'app** : servir `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html` comme asset statique (`public/guide-fonctionnel-tva.html`) + entrée « Guide » dans la sidebar `App.tsx` (ouverture nouvel onglet) ; source unique = `DOCS/`, `public/` en miroir | 🎯 **prêt** — front-only, découplé des chemins critiques. Guide client **v2** livré (28/07/2026, PO : partie TVA jugée stable). Section « Code activité » ajoutée + note réouverture admin. **Correction factuelle** (PO : « Exclue ça n'existe pas maintenant ») confirmée par grep du code (`EtatLigne.Exclue/Reportee/Ecartee` plus jamais assignés depuis TASK-097/099, `WorkstationPanel.tsx` mort) — §6 Statuts réécrite (2 états : Proposée/Intégrée), §7 dates corrigée (plus de borne basse depuis TASK-099). |

### 📦 Déploiement
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-044](DONE_DETAIL/TASK-044-deploiement-mono-service-mono-dossier.md) | **Déploiement mono-service / mono-dossier** : **un seul** service Windows (l'API) qui appelle tout (worker OM inclus) et sert le front, dans **un seul** dossier deploy. | ⚠️ **clôturée par décision PO (23/07/2026), risque assumé** — code source vérifié conforme (`WorkerExePath`/logs relatifs, front servi, `Deploy-All.ps1` existe) mais **sans VERIFY** : `connections.json.exemple` et mise à jour de `DOCS/DEPLOIEMENT.md` non livrés, aucune preuve runtime (service installé/démarré, endpoints testés). Voir `DONE.md` et `DONE_DETAIL/TASK-044_verify.md`. |
| 2 | [TASK-116](DONE_DETAIL/TASK-116-fix-front-non-servi-racine-usedefaultfiles.md) | **Fix bloquant : front non servi sur `/` (404 en prod)** : `Program.cs:98` — `UseStaticFiles()` sans `UseDefaultFiles()` en amont, `GET /` renvoie 404 même `wwwroot` peuplé. Constaté en production 17/07/2026 pendant l'installation en cours. | ✅ **done** — 19/07/2026 (vérifié directement en code par l'architecte : `Program.cs:128-129` `UseDefaultFiles()`+`UseStaticFiles()` dans le bon ordre + `MapFallbackToFile`). Voir DONE.md. |
| 3 | [TASK-115](DONE_DETAIL/TASK-115-setup-gui-winsw-port-parametrable.md) | **Setup GUI (WinForms) + service via WinSW-x64 + port paramétrable** : un seul exe (install **ou** mise à jour détectée automatiquement) qui saisit connexions SQL/JWT/SageOM/port via formulaire, écrit `connections.json`, installe/met à jour le service Windows via WinSW-x64. Port retiré de tout fichier de config à éditer à la main. | ✅ **done** — 23/07/2026 (approuvée PO après test réel d'installation sur 2 environnements : réserve n°1 (3 scénarios GUI) levée par la preuve terrain ; réserve n°3 (Sage OM) et login avec mot de passe réel confirmés/acceptés par le PO à la clôture ; gouvernance — absence de revue indépendante sur les 12 compléments auto-évalués — couverte par cette revue architecte de clôture). Voir `DONE.md` et `DONE_DETAIL/TASK-115_verify.md`. |

> ✅ **TASK-114 livrée et approuvée** (17/07/2026, implémentée en worker exceptionnel — cf. réserve `CLAUDE.md`) : sécurisation JWT + droits SQL dédiés, prérequis de TASK-044 ci-dessus mais indépendante. Voir `DONE.md`.

> **Origine Track C** : le worker TASK-009 a mesuré ~0,5-1 s/facture (session COM Sage ouverte **par facture**) → ~35 min/2085. Cause réelle = pas de routing par origine (`EC_Type`) : tout partait à l'OM, même les FGR (détail dispo en SQL `RT_HISTOCOMPTA`). Modèle figé en mémoire (`grf-echeance-ectype-mapping`, `grf-tva-perf-lecture-sage`). **Découplé de TASK-009** (désormais livrée : le recalcul du contrôle vit dans `Declaration.Controle`).

### ✅ Track B — recalcul vs GRFN (base prod `GR_EMA_DISTRIBUTION`)
✅ **Terminé** — TASK-009 livrée et approuvée (voir table ✅ Fait). Comparaison réelle sur EMA `DT_Id=66`.

> **Débloqué** : accès base prod obtenu et validé via TASK-001 (`GR_EMA_DISTRIBUTION` mono-société `SO_Id=1` / Sage `[NEW_EMA DISTRIBUTION]`, nom à espace confirmé). Source de rapprochement retenue = **locale** `RT_MOUVEMENT.MV_Point`/`MV_PointDate` (option B).
> Track A et Track B sont **parallèles** : le pur avance pendant que la prod se débloque.
> **Contrôle = grilles interactives du front** (TASK-013), pas l'Excel. Les exports Excel/XML (010/011) sont des **artefacts de dépôt/archive** → repoussés **après le front**.

> Références : `CAHIER_DES_CHARGES.md` (contrat validé), `MODULE_DECLARATION_TVA.md` (analyse technique + plan §5).
> Doc OM = source unique : `D:\_vibe\objetmetiers\*.pdf`. Aucune réutilisation de code/DLL GRFN ou GOCOM.
