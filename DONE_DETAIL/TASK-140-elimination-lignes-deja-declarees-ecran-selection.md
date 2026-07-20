# TASK-140 — Éliminer les règlements « déjà déclarés » de l'écran ① Sélection (traçabilité déplacée vers l'écran Rapprochement)

Status: 🆕 à faire
Priority: MEDIUM
Risk: MEDIUM (touche l'endpoint partagé `GET /api/rapprochement` + nécessite un lien inter-bases)
Module: declaration-tva-web (ReglementsSelection.tsx, RapprochementInterrogation.tsx) / Declaration.API (RapprochementController) / Declaration.Infrastructure (DeclarationRepository)

## OBJECTIF

Dans l'étape ① Sélection d'une nouvelle déclaration (`ReglementsSelection.tsx`), les règlements déjà
verrouillés par une déclaration antérieure (`row.declare === true`, statut dérivé `bloque`, libellé
« Déjà déclaré (Bloqué) ») ne doivent **plus apparaître du tout** dans la grille — ils sont sans
valeur pour une nouvelle déclaration et encombrent la lecture (signalement PO, capture d'écran
`TVA1-2026-02`, sur ~15 lignes visibles plusieurs étaient déjà déclarées).

En contrepartie (arbitrage PO explicite, cf. NOTES), la traçabilité n'est pas perdue : l'écran
« Rapprochement » global (`RapprochementInterrogation.tsx`), lui, doit désormais afficher le
**numéro de la déclaration** (ex. `TVA1-2026-02`) au lieu du simple badge booléen Oui/Non actuel
sur la colonne « Déclaré ».

## BUSINESS VALUE

Écran de sélection plus lisible pour une opération qui se répète chaque mois (moins de lignes
mortes à scroller/filtrer). La confiance client n'est pas compromise : l'information « pourquoi ce
règlement n'est pas dans cette déclaration » reste accessible en un clic, ailleurs, avec plus de
détail qu'avant (le numéro exact de la déclaration verrou, pas juste un booléen) — cohérent avec le
principe directeur du module (mémoire `grf-objectif-confiance-transparence` : *rien ne disparaît en
silence*, ce n'est pas contredit tant que l'info reste retrouvable quelque part).

## PÉRIMÈTRE — ce qui change et ce qui ne change pas

- **Change** : `ReglementsSelection.tsx` (étape ① uniquement) — les règlements avec `declare === true`
  sont retirés du jeu de données affiché (front), pas seulement non sélectionnables.
- **Change** : `RapprochementInterrogation.tsx` (écran global, hors tunnel déclaration) — colonne
  « Déclaré » enrichie pour afficher le numéro de déclaration au lieu du booléen Oui/Non.
- **Ne change PAS** : les deux écrans consomment le **même endpoint** `GET /api/rapprochement`
  (cf. commentaire `ReglementsSelection.tsx:15-16`, « même endpoint … intouchable »). L'endpoint et
  le back ne doivent PAS filtrer les lignes déclarées côté serveur — sinon `RapprochementInterrogation.tsx`
  les perdrait aussi, ce qui n'est pas demandé. Le filtrage « déjà déclaré » est **strictement front,
  scopé à `ReglementsSelection.tsx`** ; seul le nouveau champ numéro de déclaration est ajouté côté
  back/DTO (additif, consommé par les deux écrans mais affiché seulement là où il y a lieu).
- **Ne change PAS** les autres statuts `bloque` (non affecté, « Autre (impayé) ») : uniquement le cas
  `row.declare === true` est retiré de l'affichage. Un règlement non affecté reste visible et bloqué
  (aucune raison métier de le masquer, ce n'est pas demandé par le PO et casserait la détection des
  règlements à traiter).

## CONTEXTE — lecture de code

- Statut dérivé actuel : `ReglementsSelection.tsx:65-70` (`statutDe`), commentaire ligne 58
  « JAMAIS masqué (mémoire grf-valorisation-tracabilite-blocage-om) » — cette task en fait une
  **exception documentée et voulue par le PO**, limitée au seul cas « déjà déclaré », à rappeler dans
  le commentaire mis à jour pour ne pas dérouter un futur lecteur du code.
- Sélection par défaut à l'ouverture (`ReglementsSelection.tsx:338-349`) coche déjà tout sauf
  `bloque` — une fois les lignes déclarées retirées du jeu de données en amont, ce bloc n'a plus
  besoin d'un cas particulier (elles ne sont simplement plus dans `allData`).
- Numéro de déclaration disponible : `Declaration.Application/Entities/WorkflowEntities.cs:19-44`
  (`DeclarationEntete.Numero` + `DeclarationEntete.DT_Id`, colonne additive posée par TASK-094
  précisément pour permettre un JOIN direct sans recalculer `DeriveDtId`).
- Actuellement l'agrégat ne remonte qu'un compteur, pas la valeur : `DeclarationRepository.cs`,
  sous-requête `RapprochementFromWhere` (~lignes 624-641, `SUM(CASE WHEN AF.DT_Id IS NOT NULL …) AS NbDeclare`)
  et projection `GetReglementsRapprochementAsync` (~lignes 810-860). Aucun JOIN vers `DM_ENTTVA`
  n'existe sur ce chemin (le seul JOIN `DM_ENTTVA` présent, ligne ~852, sert `NbSelectionAutre` via
  `DM_SELECTION_REGLEMENT`, non réutilisable tel quel).
- Ligne métier : `Declaration.Application/Entities/ReglementRapprochement.cs:39,80-81`
  (`NbDeclare`, `EstDeclare`) — ajouter un champ `NumeroDeclaration`/`DtId` à côté.
- DTO front : `Declaration.API/Dtos/ReglementRapprochementDto.cs` (~ligne 38, `Declare = r.EstDeclare`)
  — ajouter le champ JSON correspondant.
- Front colonne actuelle : `RapprochementInterrogation.tsx:53` (déclaration colonne), `:306`
  (rendu badge Oui/Non), `:506` (panneau détail ligne) — à faire évoluer aux 3 endroits.
- Type partagé `ReglementRow` (`ReglementsSelection.tsx:22-38`) : ajouter le même champ pour
  cohérence si le filtrage front en a besoin (pas strictement nécessaire pour filtrer, `declare`
  suffit, mais utile si l'écran ① veut un jour expliquer un règlement déclaré retrouvé via un lien).

## CONTRAINTE TECHNIQUE À TRANCHER AVANT IMPLÉMENTATION

`RT_AFFECTATION`/`RT_MOUVEMENT` vivent sur `GrfConnection` (base `GRFN_Dummy` en dev) et
`DM_ENTTVA` sur `PersistenceConnection` (base `DeclarationTVA` en dev) — connexions Dapper
**distinctes** (`appsettings.json`). Un `JOIN` SQL direct au sein d'une seule requête n'est donc pas
trivial (dépendrait d'un nom de base en dur, fragile selon l'environnement client). Deux options :

1. **Recommandé** : requête GRF inchangée dans sa forme (ajoute juste `MAX(AF.DT_Id) AS DtId` à la
   sous-requête agrégée) → au niveau applicatif (repository), un second appel batché sur
   `PersistenceConnection` (`SELECT DT_Id, Numero FROM DM_ENTTVA WHERE DT_Id IN @dtIds`) pour
   résoudre les `DtId` distincts de la page en numéros, puis fusion en mémoire avant retour du DTO.
2. JOIN SQL trois-parties (`[DeclarationTVA].dbo.DM_ENTTVA`) — à écarter sauf si le nom de base
   persistance est garanti stable en prod (à vérifier avec le PO/déploiement avant de choisir cette
   voie).

Cas limite à couvrir : un règlement dont les affectations porteraient (en théorie) des `DT_Id`
différents — ne devrait pas arriver en pratique (verrouillage atomique par déclaration, TASK-028),
mais si `MAX(AF.DT_Id)` diffère de `MIN(AF.DT_Id)` sur un règlement, ne pas masquer silencieusement :
remonter les deux ou signaler explicitement l'incohérence plutôt que d'en choisir un arbitrairement.

## FILES

- `declaration-tva-web/src/ReglementsSelection.tsx` (filtrage `allData`, ~ligne 340 zone de
  construction du jeu de données ; commentaire `statutDe` ligne 58 à mettre à jour)
- `declaration-tva-web/src/RapprochementInterrogation.tsx` (lignes 53, 306, 506)
- `Declaration.API/Dtos/ReglementRapprochementDto.cs`
- `Declaration.Application/Entities/ReglementRapprochement.cs` (lignes 39, 80-81)
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` (lignes ~624-641, ~810-860)

## VALIDATION

- [ ] Build OK, tests passés
- [ ] Étape ① Sélection : aucune ligne avec `declare === true` n'apparaît dans la grille, ni dans
      le compteur total, quel que soit le filtre appliqué (y compris filtre Statut).
- [ ] Étape ① Sélection : les autres statuts `bloque` (non affecté, origine « Autre (impayé) »)
      restent affichés comme avant (non-régression).
- [ ] Écran Rapprochement global : la colonne « Déclaré » affiche le numéro de la déclaration
      (ex. `TVA1-2026-02`) pour un règlement verrouillé, au lieu du badge Oui/Non ; reste « — »/Non
      pour un règlement non déclaré.
- [ ] Le filtre existant `declare` (Oui/Non) sur l'écran Rapprochement continue de fonctionner
      (filtre côté serveur inchangé, `RapprochementFilter.Declare`).
- [ ] Sur un jeu réel avec des règlements déjà déclarés dans plusieurs déclarations différentes,
      chaque règlement affiche le bon numéro (pas celui d'une autre déclaration).
- [ ] Cas règlement à `DT_Id` incohérent entre affectations (si trouvé en donnée réelle) : pas de
      valeur inventée, signalement explicite plutôt que masquage.

## ARCHITECTURE RULES APPLICABLES

- Principe « aucune ligne silencieuse » (mémoire `grf-objectif-confiance-transparence`,
  `grf-valorisation-tracabilite-blocage-om`) : le retrait de l'affichage en étape ① est une
  **exception ciblée et documentée**, compensée par un enrichissement de traçabilité ailleurs — pas
  une régression du principe.
- Endpoint `GET /api/rapprochement` réutilisé par les deux écrans (TASK-054) : rester additif côté
  contrat JSON, ne rien retirer/filtrer côté serveur.

## NOTES

**Origine** : signalement PO du 19/07/2026 sur une déclaration `TVA1-2026-02` (capture d'écran) —
plusieurs lignes « Déjà déclaré (Bloqué) » visibles dans l'étape ① Sélection, jugées sans valeur pour
une nouvelle déclaration.

**Arbitrage PO confirmé (clarification architecte avant rédaction de cette task)** : élimination
complète (et non un simple filtre/masquage optionnel) en étape ① Sélection, **à condition** que le
numéro de déclaration du règlement soit désormais visible dans l'écran des rapprochements — décision
prise en connaissance du principe de transparence existant (voir CONTEXTE ci-dessus), qui reste donc
respecté par déplacement de l'information plutôt que par suppression pure.
