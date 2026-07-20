# TASK-097 — Figeage/clôture non scopés à la sélection réelle de l'écran ① (mois entier intégré)

Status: TODO
Priority: HIGH
Risk: CRITICAL
Module: Declaration.Application / Declaration.API / declaration-tva-web (tunnel ①②)

## OBJECTIF
Faire en sorte que la sélection de règlements faite par l'utilisateur à l'écran ① soit :
1. transmise au backend et persistée **avant** le figeage (au lieu de rester en `useState` React volatile) ;
2. le périmètre RÉEL utilisé par `ConstruireLignesFigeesAsync` (recalcul taxes Sage/FGR) et par
   `CloturerDeclarationAsync` (pose du tampon `DT_Id`) — au lieu du mois entier des règlements
   éligibles, comme c'est le cas aujourd'hui.

Principe métier attendu (confirmé PO 14/07/2026) : création déclaration → écran ① affiche les
règlements à intégrer (périmètre déjà filtré par TASK-099) → l'utilisateur sélectionne → clic
« Passer au calcul » déclenche le recalcul de taxe (Sage/FGR) **pour les factures affectées aux
règlements sélectionnés uniquement** → insertion dans `DM_LGTVA` + pose de `DT_Id` sur
`RT_AFFECTATION` pour flaguer **ces règlements-là**. Un retour sur la déclaration doit reproposer
la sélection telle que laissée. Si le client ne veut pas déclarer un règlement, il le décoche —
c'est la seule action requise (pas de statut « Reportee » dédié, cf. TASK-099).

### Simplification des états de figeage (confirmé PO 14/07/2026)
Plus de ligne `Exclue` produite par le figeage pour un problème détecté au niveau **facture**
pendant le recalcul (facture non ventilée/introuvable, tiers sans ICE/IF, etc.) : la ligne reste
`Proposee`/s'intègre normalement, avec une **alerte visible** à l'écran (②/checkup) signalant le
problème — jamais de mise de côté silencieuse ni de ligne à motifs qui bloque discrètement. C'est
cohérent avec TASK-099 qui élimine déjà en amont, au niveau SQL, les seuls motifs règlement
(`HorsPeriode`/`DejaDeclare`/`NonRapproche`) — il ne reste après TASK-099 que des problèmes de
qualité de donnée facture, qu'on choisit maintenant de ne plus « Exclure » mais de signaler.

**Recalcul et détection d'incohérence à préserver** (mécanisme déjà en place, TASK-072/076/077/078
— PO confirme qu'il faut le réutiliser, pas le reconstruire) : à chaque « Passer au calcul », même
si une facture existe déjà en cache de ventilation (TASK-024), le montant doit être recalculé/revalidé
— **attention particulière** si cette facture est **déjà déclarée** dans une autre déclaration
clôturée : en cas d'incohérence entre le montant qui serait recalculé maintenant et celui déjà
déclaré, une **alerte doit être visible à l'écran** (réutilise la sentinelle EC_Id en erreur +
l'alerte `LIGNE_FIGEE_A_REVERIFIER`). Le cache TASK-024 lui-même ne change pas de comportement (il
revalide déjà au token de paiement à chaque lecture) — ce recalcul ne doit pas le contourner.

## BUSINESS VALUE
Sans ce correctif, l'écran ① est décoratif : cocher/décocher des règlements n'a aucun effet sur ce
qui est réellement déclaré. La clôture intègre systématiquement tous les règlements éligibles du
mois, quelle que soit la sélection affichée à l'utilisateur — contradiction directe avec le
principe de transparence du projet (aucune ligne silencieuse) et avec le contrat fonctionnel de
TASK-054 (« ensemble de règlements sélectionnés transmis à l'étape ② »).

## CONTRAINTES
- Aucune régression sur le pipeline de figeage existant hors exclusivité (routing `EC_Type`
  TASK-022, cache Sage TASK-024, verrou `DT_Id` TASK-028/064) — ces mécanismes s'appliquent, ils
  doivent juste opérer sur un périmètre filtré par la sélection au lieu du mois entier. Le
  garde-fou d'exclusivité TASK-080 change en revanche de forme (cf. ci-dessous : gate de
  déclarabilité, plus de ligne `Exclue`).
- Le figeage reste un événement UNIQUE et idempotent par (déclaration, domaine) — pas de
  re-figeage à chaque retour sur l'écran ①. La sélection doit donc être capturée et persistée
  **avant** le premier appel de figeage (`ChargerCandidatesSiNecessaireAsync`), pas après.
- La restauration de la sélection au retour sur la déclaration doit relire une source persistée
  (base), jamais reconstruire un état local par supposition.
- Pas de filtre `MV_DECAISSE=1` réintroduit (trou ~465 factures/mois, mémoire
  `grf-trou-selection-mv-decaisse`) — la restriction porte sur l'ensemble sélectionné de
  règlements, pas sur un filtre de mouvement.
- Aucune dette technique silencieuse : si un choix de conception limite la portée (ex. clé de
  sélection composite `numeroReglement+date+montant`, cf. `reglementKey` front), le documenter en
  NOTES du VERIFY.
- `MapLignesCandidates` ne doit plus produire `Etat=Exclue` **du tout**, pour aucun motif — confirmé
  PO (14/07/2026) : « on n'a pas ce fonctionnel pour le moment », le seul geste utilisateur est
  coché/décoché. `DM_LGTVA` ne contient donc plus, après cette task, que `Proposee`/`Integree` — tout
  ce qui n'est pas déclarable ne produit **aucune ligne du tout** (ni motif, ni état de rejet
  persisté), il est simplement absent du jeu de candidats ou non sélectionnable à l'écran ①.
- Ça inclut le garde-fou d'exclusivité inter-déclaration (TASK-080, ex-motif `DejaEnCoursAilleurs`) :
  un règlement déjà **sélectionné/coché** (persisté par cette même task) dans une **autre**
  déclaration `EnCours` de la même société doit être **non sélectionnable / non affiché comme
  déclarable** à l'écran ①, sans créer de ligne `Exclue` — simple gate de déclarabilité, au même
  titre que « déjà déclaré » (`DT_Id` posé) ou « non affecté » (`nbFacturesAffectees=0`, déjà
  correct aujourd'hui, confirmé PO — un règlement rapproché mais sans facture affectée reste
  `bloque`). Ce gate devient possible uniquement parce que cette task persiste la sélection
  côté serveur (avant, la sélection d'une autre déclaration `EnCours` n'était pas connue avant
  clôture) — c'est le remplacement direct de l'ancien mécanisme `ReintegrerReglementsLiberesAsync`/
  `AppliquerExclusiviteInterDeclarationAsync`, qui n'a plus lieu d'être sous cette forme.
- Le contrôle qualité-donnée (ICE/IF manquant, facture non ventilée) reste néanmoins **visible** :
  la ligne `Proposee` correspondante remonte toujours l'alerte existante (`TIERS_SANS_ICE`, etc.) au
  checkup — alerte bloquante pour la clôture, comportement à conserver tel quel.
- Le recalcul (Sage/FGR) doit s'exécuter à chaque « Passer au calcul » même si la facture est déjà
  en cache de ventilation (TASK-024) — ne jamais servir une valeur de cache sans revalidation. Si la
  facture est déjà déclarée ailleurs (`DT_Id` posé sur une autre déclaration `Cloturee`) et que le
  montant recalculé diverge de celui déjà déclaré, l'alerte `LIGNE_FIGEE_A_REVERIFIER` (ou équivalent)
  doit être visible — réutiliser le mécanisme TASK-072/076/077/078 tel quel, ne pas le dupliquer.

## FILES
- Declaration.Application/Services/DeclarationWorkflowService.cs (`ConstruireLignesFigeesAsync`,
  `ChargerCandidatesSiNecessaireAsync`, `CloturerDeclarationAsync`)
- Declaration.API/Controllers/DeclarationsController.cs (`GetLignes`, `Cloturer`, éventuel nouvel
  endpoint de persistance de sélection)
- Declaration.Application/Interfaces/IDeclarationRepository.cs
- Declaration.Infrastructure/Repositories/DeclarationRepository.cs
- declaration-tva-web/src/DeclarationStepper.tsx (`selectedKeys`/`selectedRows`, actuellement
  `useState` sans persistance ni restauration)
- declaration-tva-web/src/ReglementsSelection.tsx (`onSelectionChange`, `reglementKey`)
- declaration-tva-web/src/VerifierIntegrerPanel.tsx (consommation actuelle de `selectedRows` en
  filtre purement d'affichage — à recâbler sur la source persistée)

## VALIDATION
- [ ] Build OK
- [ ] Tests passés
- [ ] Une déclaration créée, 3 règlements cochés sur 10 éligibles dans la période → seuls les 3
      règlements cochés apparaissent dans `DM_LGTVA` après « Passer au calcul » (preuve base réelle).
- [ ] Clôture de cette déclaration → `DT_Id` posé uniquement sur les affectations des 3 règlements
      sélectionnés (vérifié `RT_AFFECTATION`), pas sur les 7 autres.
- [ ] Fermeture de l'onglet / retour à la liste puis réouverture de la déclaration EnCours → les 3
      règlements réapparaissent cochés à l'identique (source = base, pas la mémoire du navigateur).
- [ ] Aucune régression sur les critères de validation existants de TASK-080/077/082 (garde-fous
      d'exclusivité et revalidation) rejouée sur le nouveau périmètre filtré.
- [ ] Une facture non ventilée (ou tiers sans ICE) parmi les règlements sélectionnés → la ligne
      reste `Proposee`, une alerte visible apparaît (écran ②/checkup), aucune ligne `Exclue` créée.
- [ ] Une facture déjà déclarée ailleurs, dont le montant recalculé diffère du montant déjà déclaré
      → alerte `LIGNE_FIGEE_A_REVERIFIER` (ou équivalent) visible, ligne/totaux inchangés (aucun
      recalcul automatique silencieux, cf. comportement TASK-077 existant).
- [ ] Une facture déjà en cache de ventilation (TASK-024) → revalidée au moment du calcul, jamais
      servie sans passer par la validation du token de paiement.
- [ ] Un règlement déjà coché dans une autre déclaration `EnCours` de la même société → non
      sélectionnable/non affiché comme déclarable dans la déclaration courante, sans ligne `Exclue`
      créée (preuve : 2 déclarations `EnCours` concurrentes, même société, même règlement).

## ARCHITECTURE RULES APPLICABLES
- Logique métier interdite dans la couche UI (la restriction de périmètre doit être appliquée
  côté back, pas seulement filtrée à l'affichage front comme c'est le cas aujourd'hui).
- Pas de duplication de logique existante — réutiliser `SelectionnerExpliqueeAsync` / l'orchestrateur,
  seulement les alimenter avec un sous-ensemble filtré au lieu du mois entier. Le principe de
  `AppliquerExclusiviteInterDeclarationAsync` (détecter un conflit inter-déclaration) est repris,
  mais sa sortie change : gate de déclarabilité (non sélectionnable), plus de motif/`Exclue` persisté.
- Audit trail : la sélection persistée doit être traçable (quel règlement, quand).

## NOTES
Découvert en répondant à une question PO sur `DM_LGTVA` (14/07/2026) — la sélection de l'écran ①
n'a aucun effet observable côté back (vérifié en lisant `ConstruireLignesFigeesAsync` et
`CloturerDeclarationAsync`, aucun des deux ne reçoit de paramètre de sélection).

**Risque rétroactif hors périmètre de cette task** : les déclarations déjà `Cloturée`
(`TVA1-2026-01`, `TVA1-2026-06`, ...) ont vraisemblablement intégré l'intégralité des règlements
éligibles du mois, indépendamment de ce qui avait été coché à l'écran — cette sélection n'ayant
jamais été persistée, impossible de reconstituer après coup ce que l'utilisateur croyait avoir
choisi. À signaler au PO pour décision (audit manuel si nécessaire) — ne pas tenter de « réparer »
rétroactivement dans le cadre de cette task.

Lié à TASK-098 (réintégration manuelle des lignes `Exclue`) — TASK-098 est désormais **en suspens**
après cette task : avec la simplification totale (plus aucune ligne `Exclue` produite), TASK-098 ne
garderait d'utilité que pour la dette historique (lignes `Exclue` déjà présentes en base sur des
déclarations figées avant ce correctif) — à réévaluer avec le PO une fois TASK-097 livrée.
