# VERIFY — TASK-220 : exclusion des factures antérieures à la mise en route (écran Contrôle DDP)

## Contexte d'exécution

Implémentée par Claude en rôle **WORKER DE SECOURS** (dérogation « Claude ne code pas » déjà
validée par le PO, cf. `CLAUDE.md` racine §Séparation stricte implémentation / clôture). Ce fichier
est déposé dans `VERIFY/` pour review par un tiers (PO ou session ARCHITECT distincte) — **aucune
clôture n'a été effectuée** : pas de déplacement vers `DONE_DETAIL/`, pas de mise à jour de
`DONE.md`/`TODO.md`/`CHANGELOG.md`, pas de commit contenant « approuve ».

## Décision de conception (à valider en review)

Le périmètre laissait le choix au WORKER entre garder `DelaiPaiementBootstrapGuard` (réduit à une
exclusion booléenne) ou le supprimer entièrement. **Choix retenu : suppression complète** du
mécanisme de reprise manuelle et de tout le code qui n'a plus de raison d'être une fois le 3ᵉ état
(`AnterieureAvecRepriseSaisie`) supprimé — car le nouveau comportement PO est une exclusion pure
sans aucun état intermédiaire, contrairement à l'ancien garde-fou qui produisait 3 statuts.

## Fichiers modifiés

**Backend**
- `Declaration.Core/SelectionDelaiPaiementCalculator.cs` — critère remplacé par `DoDate <
  DateMiseEnRouteSociete` (exclusion avant construction de toute ligne, tous buckets confondus) ;
  `StatutLigneDelaiPaiement.RepriseManuelleRequise` et `OrigineBorneReference.{RepriseManuelle,
  Indeterminee}` supprimés (plus aucun code chemin ne peut les produire) ; `ReprisesManuelles`
  retiré de `ParametresSelectionDelaiPaiement` ; `AjouterLigne` simplifié (le statut est désormais
  toujours `Candidate`).
- `Declaration.Core/DelaiPaiementBootstrapGuard.cs` — **supprimé** (plus aucun appelant : la
  bascule à 3 états n'a plus de raison d'être).
- `Declaration.Core.Tests/DelaiPaiementBootstrapGuardTests.cs` — **supprimé** (classe testée
  supprimée). `SelectionDelaiPaiementCalculatorTests.cs` — région « GARDE-FOU » réécrite : 6 tests
  couvrant facture antérieure exclue, facture postérieure incluse, facture antérieure MAIS échéance
  légale postérieure exclue quand même (cas explicite du PO), historique + garde-fou, société non
  configurée (aucune exclusion), et le cas frontière `DoDate == DateMiseEnRouteSociete` (inclus, `>=`).
- `Declaration.Application/Interfaces/IRepriseDelaiPaiementRepository.cs` — **supprimé**.
- `Declaration.Application/Entities/DelaiPaiementBootstrapEntities.cs` — `RepriseDelaiPaiement`
  supprimé ; `ParametrageDelaiPaiementSociete` (date de mise en route) conservé inchangé.
- `Declaration.Infrastructure/Repositories/DelaiPaiementBootstrapRepository.cs` — n'implémente plus
  que `IParametrageDelaiPaiementSocieteRepository` ; les 3 méthodes contre `DM_REPRISE_DELAIPAIEMENT`
  supprimées.
- `Declaration.Application/Services/DelaiPaiementBootstrapService.cs` — `IDelaiPaiementBootstrapService`
  réduit à `GetDateMiseEnRouteAsync`/`SetDateMiseEnRouteAsync` ; `GetRepriseAsync`,
  `GetToutesReprisesAsync`, `SetRepriseAsync`, `ResoudreBasculeAsync` supprimés.
- `Declaration.Application/Services/SelectionDelaiPaiementService.cs` — ne lit plus les reprises
  (suppression de l'appel `GetToutesReprisesAsync` + construction du dictionnaire associé) ;
  `ResultatSelectionDelaiPaiement.LignesRepriseManuelleRequise` supprimé.
- `Declaration.Application/Services/DeclarationDelaiPaiementService.cs` — le refus d'intégration des
  lignes « reprise manuelle requise » (devenu impossible, la liste est toujours vide) supprimé.
- `Declaration.Application/Entities/DeclarationDelaiPaiementEntities.cs` —
  `ClesRefuseesRepriseManuelleRequise`/`NombreRepriseManuelleRequiseDisponibles` supprimés de
  `ResultatIntegrationLignesDelaiPaiement`.
- `Declaration.API/Dtos/DeclarationDelaiPaiementDto.cs` — champs DTO correspondants supprimés
  (`LigneSelectionDelaiPaiementDto.Statut` documenté comme toujours `"Candidate"`,
  `OrigineBorneReference` réduit à 2 valeurs).
- `Declaration.API/Controllers/DelaiPaiementParametrageController.cs` — endpoints `GET
  /delai-paiement/reprise/{soId}/{ecId}` et `POST /delai-paiement/reprise` supprimés (route de
  paramétrage de la date de mise en route conservée inchangée).
- `Declaration.API/Program.cs` — retrait de l'enregistrement DI
  `IRepriseDelaiPaiementRepository`.
- `Declaration.Orchestration.Tests/Task132CycleDeVieDeclarationDelaiPaiementTests.cs` et
  `Task133GenerationFichierDelaiPaiementTests.cs` — fixtures/tests adaptés (suppression du concept
  de ligne « reprise manuelle » dans les faux services de sélection).

**Frontend**
- `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx` — bouton « Reprise manuelle »
  (colonne Action) et badge Statut « Reprise manuelle requise » supprimés (statut toujours
  « Retard calculé ») ; bandeau d'alerte « société non configurée » supprimé (le message n'était
  plus vrai : absence de date = aucune exclusion, plus « tout bloqué ») ; `LIBELLES_ORIGINE_BORNE`/
  `COULEURS_ORIGINE_BORNE` réduits à 2 valeurs (`RepriseManuelle` retiré, cf. coordination TASK-219
  ci-dessous) ; `toutesLignes` ne concatène plus `lignesRepriseManuelleRequise` (champ supprimé).
- `declaration-tva-web/src/DeclarationsDelaiPaiementPanel.tsx` — bloc « Non intégrables (reprise
  manuelle requise) » de la popup de sélection supprimé ; badge « retard inconnu » de
  `LignesSelectionTable` supprimé (le dépassement est désormais toujours affiché, plus aucune ligne
  bloquée ne peut apparaître dans la sélection).
- `declaration-tva-web/src/MiseEnRouteDelaiPaiementModal.tsx` — `RepriseManuelleLigneModal` (modale
  + appel `setRepriseManuelleDdp`) supprimée ; texte d'aide de `MiseEnRouteDelaiPaiementModal` mis à
  jour pour refléter le nouveau comportement (exclusion, pas blocage).
- `declaration-tva-web/src/api.ts` — `setRepriseManuelleDdp`, les champs
  `lignesRepriseManuelleRequise`/`clesRefuseesRepriseManuelleRequise`/
  `nombreRepriseManuelleRequiseDisponibles` supprimés des DTO TypeScript ; `statut` réduit au type
  littéral `'Candidate'`.
- `declaration-tva-web/tests/task134.spec.ts` — réécrit : fixtures sans ligne « reprise manuelle »
  (une facture antérieure à la mise en route n'apparaît plus du tout, comportement serveur réel),
  test B débarrassé du flux de saisie de reprise, test C réécrit pour vérifier qu'une société sans
  date configurée affiche ses lignes normalement (plus d'alerte de blocage).
- `declaration-tva-web/tests/task136.spec.ts` — fixture `lignesRepriseManuelleRequise: []` retirée.

**Non touché (conforme au périmètre)**
- `DM_REPRISE_DELAIPAIEMENT` (table SQL) : ni schéma ni données modifiés. Devient
  **fonctionnellement inutilisée** (plus aucun code applicatif ne la lit/l'écrit) — signalé ici,
  décision de rétention/archivage laissée au PO (hors périmètre technique, comme demandé).
- `DateDebutDeclarationLoi` / `EcheanceLegaleCalculator` : non modifiés.
- `SelectionDelaiPaiementRepository.GetEcheancesCandidatesAsync` : filtre SQL amont non ajouté (le
  filtre côté calculateur pur suffit ; optimisation explicitement facultative dans la TASK).

## Coordination TASK-219 (badge d'origine de la borne)

TASK-219 (déjà livrée, cf. `DONE_DETAIL/TASK-219-badge-origine-borne-controle-ddp.md`) avait ajouté
un badge `RepriseManuelle → "Reprise manuelle"` sur la colonne Origine. Cette TASK-220 retire cette
valeur de l'enum `OrigineBorneReference` (backend) et de `LIBELLES_ORIGINE_BORNE`/
`COULEURS_ORIGINE_BORNE` (front) : le badge ne peut donc plus jamais apparaître — pas un état mort
résiduel, il est **retiré du code**, conformément à l'impact déjà anticipé dans `TODO.md`.

## Cas `DateMiseEnRouteSociete == null` (risque signalé dans la TASK)

Décision retenue (position la plus sûre, cf. §Risques de la TASK) : **aucune exclusion** n'est
appliquée tant que la date n'est pas configurée — le calcul automatique reste actif pour toutes les
échéances de la société, à l'identique du comportement pour toute société où la mise en route est
déjà ancienne. C'est un changement de comportement PAR RAPPORT à l'ancien garde-fou (qui bloquait
alors TOUTES les échéances en « reprise manuelle requise », cf. ancien test
`Integration_SocieteNonConfiguree_...`), documenté et couvert par un test dédié
(`GardeFou_SocieteSansDateDeMiseEnRoute_AucuneExclusion`). Ce point mérite une confirmation PO
explicite s'il diverge de l'attente (pas tranché ailleurs dans la TASK de façon univoque).

## Checklist VALIDATION (preuve par critère, datée)

- [x] `dotnet build DeclarationTVA.slnx` → **0 erreur**. Vérifié le 2026-09-10, build complet
  rejoué après chaque étape de suppression (log de build inspecté directement, pas seulement le
  code de sortie).
- [x] Tests unitaires `Declaration.Core.Tests` → **209/209 verts**, dont les 6 nouveaux tests
  `GardeFou_*` (critère DoDate, cas frontière `>=`, échéance légale postérieure mais exclue quand
  même, société non configurée). Rejoué le 2026-09-10.
- [x] Tests `Declaration.Orchestration.Tests` → **266/266 verts** (fixtures adaptées, plus aucune
  référence aux symboles supprimés). Rejoué le 2026-09-10.
- [x] `npm run lint` (`declaration-tva-web/`) → **0 erreur** (warnings préexistants uniquement,
  aucun nouveau). Rejoué le 2026-09-10.
- [x] `npm run build` (`declaration-tva-web/`) → **0 erreur TypeScript**, build Vite réussi. Rejoué
  le 2026-09-10.
- [x] Grep exhaustif post-suppression sur `RepriseManuelle|ReprisesManuelles|Indeterminee|
  estRepriseManuelleRequise` sur toute la solution + le front : **aucune occurrence résiduelle**
  hors commentaires explicatifs et fichiers historiques (`CHANGELOG.md`, `DONE.md`,
  `DONE_DETAIL/TASK-219_verify.md`, `TASKS/TASK-220_*.md`) et le nom de table SQL
  `DM_REPRISE_DELAIPAIEMENT` lui-même (schéma, non touché par contrainte projet).
- [x] Tests e2e Playwright `task134.spec.ts` (réécrit) et `task136.spec.ts` (fixture corrigée)
  rejoués le 2026-09-10 : test B (période raisonnée, aucune intégration possible depuis l'écran de
  contrôle) et test C (société non configurée → aucune exclusion, lignes affichées normalement)
  **verts**. Test A (cycle complet création→dépôt) et le test de navigation `task136` échouent —
  **confirmé PRÉEXISTANT et sans rapport avec cette TASK** : rejoués à l'identique sur le code
  d'AVANT cette modification (`git stash` puis re-run), même échec strictement identique
  (`Aucune déclaration délai de paiement pour cette société.` introuvable / entrée de menu « Délai
  de paiement » introuvable), donc non imputable aux changements de cette TASK. Non corrigé ici
  (hors périmètre STRICT).
- [x] **Vérification sur données réelles (étape 5 de la TASK) — EFFECTUÉE le 2026-09-10**, suite au
  BLOQUÉ du 2026-09-10 signalant que l'échec initial (`sqlcmd` contre `DESKTOP-5BFKKEP`) était une
  erreur de nom d'hôte, pas une vraie absence d'accès réseau : l'instance réelle est
  `localhost\SQL2022` (confirmé via `Get-Service` — `MSSQL$SQL2022` running — puis connexion
  `sqlcmd` réussie). Méthode : petit programme console jetable
  (`scratch/Ddp220Proof/Program.cs`, non commité, gardé localement pour reproductibilité)
  réutilisant **le pipeline de production réel** (`ISelectionDelaiPaiementRepository.
  GetEcheancesCandidatesAsync` + `IDelaiPaiementService.ChargerContexteAsync().Resoudre`, mêmes
  repositories/services que `Declaration.API`, **aucune écriture** en base) contre
  `GR_EMA_DISTRIBUTION` réelle, société SO_Id=1 (seule société existante en base — vérifié par
  `SELECT SO_Id FROM P_SOCIETE`).

  **Résultat chiffré (SO_Id=1, DateMiseEnRouteSociete = 2026-06-01) :**
  | Mesure | Valeur |
  |---|---|
  | Échéances candidates (seuils légaux + devise société, toutes périodes) | 1408 |
  | Ancien critère (échéance légale, sans historique) → bloquées « reprise manuelle requise » | 872 |
  | **Nouveau critère (DoDate, TASK-220) → exclues** | **1290** |
  | Bascule vers EXCLUSION (étaient `CalculAutomatique` avant, disparaissent maintenant) | **418** |
  | Bascule vers INCLUSION (étaient bloquées avant, incluses maintenant) | 0 |

  **Lecture métier — IMPACT SIGNIFICATIF confirmé** : 418 échéances qui produisaient normalement un
  `Depassement` chiffré et intégrable (`CalculAutomatique`, jamais bloquées) **disparaissent
  purement et simplement** du contrôle DDP avec le nouveau critère, alors qu'aucune n'était
  auparavant en attente de reprise manuelle. Exemple représentatif (10 premières lignes) :
  `EC_Id=20809 DO_Numero=FC2600396 DoDate=2026-04-01 EcheanceLegale=2026-06-01` — facture d'avril,
  échéance légale calculée pile à la date de mise en route (délai société de ~60j), **exclue quand
  même** par le nouveau critère strict sur `DoDate` — c'est exactement le cas de figure décrit dans
  la TASK (§Nouveau comportement demandé, exemple 20/06/2024→18/09/2024). Confirme la lecture
  demandée par le PO (décision explicite, pas une omission) mais l'ampleur (418/1408 = ~30% des
  échéances candidates de cette société) valide la mise en garde de la TASK : **à vérifier
  explicitement par le PO avant bascule en production**, le dépôt T3 2026 verra mécaniquement
  disparaître ces lignes du contrôle si aucune action n'est prise en amont (aucune n'était visible
  comme « bloquée » avant, donc aucun signal actuel n'attire l'attention dessus).
- [x] Aucune modification de schéma ni de données sur `DM_REPRISE_DELAIPAIEMENT` (contrainte
  absolue du projet respectée — vérifié par grep sur `DeclarationTVA.sql`, fichier non modifié dans
  ce diff).

## Risques restants (à trancher par le reviewer)

1. **Impact chiffré confirmé significatif (418/1408 échéances, ~30%, cf. preuve ci-dessus)** :
   la TASK anticipait ce risque et demandait explicitement de le mesurer avant bascule ; c'est fait,
   mais le chiffre lui-même n'a pas été validé par le PO. Recommandation : ne pas déployer en
   production avant confirmation explicite du PO que cette ampleur est attendue/acceptée, faute de
   quoi le dépôt T3 2026 perdra silencieusement ~30% des échéances actuellement déclarables pour
   SO_Id=1.
2. **`DateMiseEnRouteSociete == null` → aucune exclusion** : changement de comportement côté société
   non configurée (avant : tout bloqué). À confirmer explicitement par le PO si ce n'était pas déjà
   son intention précise (la TASK ne tranche pas ce point de façon univoque, cf. §Risques de la
   TASK originale). Sans objet pour SO_Id=1 (date déjà configurée : 2026-06-01).
3. `DM_REPRISE_DELAIPAIEMENT` reste en base, vide de toute utilité applicative — décision de
   rétention/archivage à prendre séparément par le PO (hors périmètre technique).
