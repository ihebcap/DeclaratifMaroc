# TASK-084 — Ouverture du domaine « TVA Collectée » : règlements clients (MV_Domaine=0) dans le tunnel

> **Origine** : demande PO 14/07/2026 — « step1 gère les règlements fournisseurs, il faut aussi
> gérer les règlements clients (`MV_Domaine=0`) avec les mêmes règles ». Clarification PO :
> **« normalement c'est le même calcul, sauf que c'est de la TVA collectée »** → même moteur
> d'éligibilité, mais **distinction stricte** des deux domaines en aval (jamais mélangés dans un
> même total).
>
> ⚠️ **Reclassement de roadmap** : `TODO.md` §Roadmap listait jusqu'ici *« Hors roadmap confirmée
> (non mentionnés — restent à décider) : TVA Collectée (ventes/clients) »*. Cette task **ouvre**
> ce chantier — à confirmer par le PO avant lancement (cf. Risques).

## Contexte

Le tunnel règlement-first (écrans ①→⑧, TASK-053 à 059) traite aujourd'hui exclusivement la
**TVA déductible** : règlements **fournisseurs** (`MV_Domaine=1`, décaissement) + dépenses
(`MV_Domaine=6`). C'est un choix **délibéré**, pas un oubli :

- `ReglementsSelection.tsx:196-198` (écran ①) fige `domaine: ['Décaissement']` côté front, alors
  que l'endpoint `GET /api/rapprochement` (TASK-036/039) accepte déjà `MV_Domaine IN (0,1)`.
- Le commentaire en tête de fichier (`ReglementsSelection.tsx:10-20`) documente ce périmètre comme
  intentionnel (« périmètre décaissement uniquement pour l'écran ① »).

**Le socle d'éligibilité existe pourtant déjà en partie**, construit par TASK-015/021 pour la
« sélection expliquée » (transparence/motifs), pas pour la déclaration réelle :
- `Declaration.Selection/GrfEnums.cs:8-9` : `Domaine_ReglementClient = 0` / `Domaine_ReglementFournisseur = 1`.
- `SelectionExpliqueeService.GetSurensembleClientSql` (lignes 165-200) : miroir exact de la requête
  fournisseur (mêmes garde-fous `MV_Compta`/`MV_Annule`/`MV_Impaye`, même règle de date TASK-062),
  évalué avec `SensAffectation.Vente`.
- `SelectionnerAffectationsService.cs` (lignes 232-240) : branche `domaineClient` déjà présente
  dans le SQL de sélection réelle des affectations.
- `SelectionExpliqueeEvaluator.cs:46-58` : route déjà `MV_Domaine` vers
  `SourceAffectation.Encaissement` pour tout ce qui n'est ni Fournisseur ni Dépense.

**Ce qui manque** (vérifié par lecture de code, non implémenté) :
1. **Figeage réel** (`DeclarationWorkflowService`) : le chemin de production ne mobilise que
   `SensAffectation.Achat` (ex. `DeclarationWorkflowService.cs:439`, résynchronisation d'une pièce
   fournisseur). Aucune trace d'un chemin `Vente` câblé au workflow de déclaration/figeage.
2. **Modèle de déclaration** (`Declaration.Core.Model`, `LigneCandidate`) : pas de distinction
   connue TVA collectée / TVA déductible au niveau du total agrégé de la déclaration.
3. **Export XML DGI (Simpl-TVA)** : `TASK-014` (`DONE_DETAIL/TASK-014-...md:10-11,33,48`) a
   explicitement cadré l'encaissement client comme **schéma XML inconnu** (« relève très
   probablement d'un **autre formulaire DGI** — déclaration du CA / TVA collectée »). **Aucun
   schéma confirmé à ce jour.**
4. **Front écrans ②→⑧** : aucun ne distingue actuellement Achat/Vente dans l'affichage des totaux.

## Périmètre STRICT

- **Inclus** :
  1. Écran ① : lever la restriction `domaine: ['Décaissement']`, afficher aussi les règlements
     clients (`MV_Domaine=0`), avec un **marqueur visuel de domaine explicite** (colonne/badge
     Fournisseur vs Client) — jamais une liste mélangée sans distinction.
  2. Sélection/figeage réel : brancher `SensAffectation.Vente` dans le chemin de production
     (`DeclarationWorkflowService`/`OrchestrateurDeclaration`), en réutilisant le SQL miroir déjà
     écrit (`SelectionnerAffectationsService` branche `domaineClient`), **mêmes règles
     d'éligibilité** que les fournisseurs (motifs, verrou `DT_Id`, date de période TASK-062).
  3. Modèle de déclaration : les lignes issues de règlements clients portent un total
     **séparé** (TVA collectée) — jamais agrégé avec la TVA déductible dans un même chiffre.
  4. Écrans ②→⑥ (Affectations, Calcul TVA) : distinguer les deux domaines dans l'affichage
     (filtre ou section dédiée), en respectant les décisions déjà actées (TASK-069/070 allègement
     écran ③, pas de tableau détail redondant).
- **Exclu** (définitif, confirmé PO 14/07/2026) :
  - **Export XML DGI (Simpl-TVA)** : **hors périmètre de façon permanente**, pas seulement
    différé. Confirmation PO : le dépôt XML ne porte **que** la TVA déductible ; la TVA
    collectée n'a pas vocation à y figurer. Le schéma DGI inconnu (TASK-014) devient donc
    **non bloquant** — cette task ne le lève pas et n'a pas besoin de le lever.
  - Export Excel (TASK-010) : à étendre séparément une fois le modèle de données stabilisé.
  - Opérations bancaires (`MV_Domaine=6` côté sens Encaissement) : hors périmètre, cf. TASK-031.

## Objectif

```
Entrée  : société + période
Traitement : sélectionner les règlements CLIENTS (MV_Domaine=0) de la période, mêmes garde-fous
             qu'un règlement fournisseur (verrou DT_Id, MV_Compta/Annule/Impaye, date TASK-062),
             valoriser leur TVA (même moteur, sens Vente), agréger en total SÉPARÉ (collectée)
Sortie  : lignes de déclaration domaine=Client visibles dans le tunnel, total TVA collectée
          distinct du total TVA déductible ; export XML explicitement différé (motif visible)
```

## Étapes

1. Confirmer le périmètre avec le PO (cf. Risques) avant tout développement — reclassement de
   roadmap, pas un simple correctif.
2. `SelectionnerAffectationsService` / `SelectionExpliqueeService` : vérifier que la branche
   `domaineClient` déjà écrite produit un jeu de candidats correct sur données réelles
   (`GR_EMA_DISTRIBUTION`) — probablement jamais exécutée en conditions réelles à ce jour.
3. `DeclarationWorkflowService`/`OrchestrateurDeclaration` : brancher `SensAffectation.Vente` dans
   le chemin de figeage réel (actuellement `Achat` en dur sur au moins un point du code).
4. `Declaration.Core.Model` : ajouter la distinction Collectée/Déductible au niveau agrégat de la
   déclaration (nouveau champ ou séparation de `LigneCandidate` par sens).
5. Front écran ① : retirer la restriction `domaine: ['Décaissement']`, badge de domaine explicite,
   sélection actionnable identique aux fournisseurs.
6. Front écrans ②→⑥ : distinction visuelle des deux domaines (filtre par défaut ou sections).
7. Exclure explicitement la TVA collectée du générateur XML Simpl-TVA (confirmé définitif PO,
   pas un statut « à venir ») — s'assurer que le générateur existant ne filtre déjà que le
   déductible et documenter ce choix pour éviter qu'une évolution future ne l'y inclue par erreur.
8. Tests : miroir des tests existants Fournisseur pour Client (`SelectionExpliqueeEvaluatorTests`,
   `IntegrationRegressionTests`).

## Livrables

- Sélection + figeage réel des règlements clients, mêmes garde-fous que fournisseurs.
- Distinction stricte TVA collectée / TVA déductible dans le modèle et l'affichage (aucun total
  mélangé).
- Tests couvrant le domaine Client (motifs, verrou, date de période) en miroir du Fournisseur.
- `VERIFY/TASK-084_verify.md` : preuve réelle sur `GR_EMA_DISTRIBUTION` — au moins un règlement
  client réel sélectionné/figé, total collectée ≠ total déductible, aucun mélange constaté.

## Critères de validation

- Un règlement client éligible apparaît dans le tunnel avec les mêmes garde-fous qu'un règlement
  fournisseur (mêmes motifs de rejet possibles : `DejaDeclare`, `NonAffecte`, `NonComptabilise`,
  `Annule`, etc. — réutilisation stricte de `SelectionExpliqueeEvaluator`).
- Total TVA collectée et total TVA déductible **jamais additionnés** dans un même chiffre affiché
  ou exporté.
- Export XML : le générateur Simpl-TVA continue de ne porter **que** la TVA déductible (confirmé
  PO) — vérifier par lecture de code qu'aucune ligne « Client » ne s'y infiltre après ouverture
  du domaine côté sélection/figeage.
- Aucune régression sur le flux fournisseur existant (non-régression du tunnel actuel).

## Risques / dépendances

- **🔴 Reclassement de roadmap** : cette task fait sortir « TVA Collectée » de la case « hors
  roadmap, non décidée » (`TODO.md` §Roadmap) sans que R1/R2/R3 n'aient été séquencés. À faire
  trancher explicitement par le PO (ordre vs RAS fournisseurs/Télédéclaration) avant lancement.
- ~~Export DGI bloquant~~ **Levé (confirmé PO 14/07/2026)** : le XML Simpl-TVA ne porte que la
  TVA déductible, par choix définitif — le schéma inconnu de TASK-014 n'a plus besoin d'être
  cadré pour cette task.
- **Chemin de figeage non prouvé en réel** : la branche `domaineClient` existe en SQL
  (TASK-015/021) mais n'a — à la connaissance de cette analyse — jamais été exercée sur le chemin
  de production réel (`DeclarationWorkflowService`). À traiter comme code neuf, pas comme un
  branchement trivial d'existant déjà validé.
- **Confusion utilisateur** : un tunnel qui affiche des règlements clients et fournisseurs sans
  distinction visuelle forte reproduirait l'anti-pattern déjà corrigé en TASK-039 (bordereau
  remontant sans filtre de nature) — le badge de domaine explicite (étape 5) est donc **non
  négociable**, pas une simple amélioration UX.
- **Verrou `DT_Id`** (TASK-028) : à vérifier que les triggers d'immuabilité globaux couvrent aussi
  bien les mouvements `MV_Domaine=0` que `=1` (portée annoncée « globale GRFN inclus » — à
  confirmer sans supposer).
