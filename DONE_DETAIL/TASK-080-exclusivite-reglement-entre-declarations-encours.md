# TASK-080 — Exclusivité d'un règlement/affectation entre déclarations `EnCours` (garde-fou sélection)

## Origine
Remarque PO 13/07/2026, en réaction à TASK-079 : « un règlement qui a été affecté sur une
déclaration encore `EnCours` ne doit pas être intégré dans une autre déclaration ».

## Constat (preuve code)
1. La seule exclusion actuellement appliquée à la sélection est `DT_Id` (posé **uniquement à la
   clôture**, TASK-028) : `Declaration.Selection/SelectionExpliqueeEvaluator.cs:67`
   — `if (r.DT_Id != null) motif = MotifRejet.DejaDeclare;`.
2. Les requêtes de sélection (`SelectionExpliqueeService.cs`, 3 SQL surensemble + facture-first)
   ne consultent JAMAIS `DM_LGTVA`/`LigneCandidate` — elles ignorent totalement qu'un règlement est
   déjà pris en compte (`Etat = Proposee` ou `Integree`) par une **autre** déclaration `EnCours`.
3. `IDeclarationRepository.ExistsAsync` (`DeclarationRepository.cs:71-79`) n'empêche que la
   création d'une déclaration en doublon strict sur `(SocieteId, Exercice, Periode, Type)` — deux
   déclarations de `Type` différent (ou toute autre clé distincte) couvrant la même fenêtre de
   dates réelles peuvent coexister `EnCours` et sélectionner les **mêmes** règlements/affectations.
4. Conséquence concrète : rien n'empêche qu'un même règlement soit marqué `Integree` dans deux
   déclarations `EnCours` en parallèle. Le seul filet de sécurité est le trigger SQL
   `TR_RT_AFFECTATION_Immuabilite` (`003_Verrou_DT_Id.sql`), qui ne se déclenche **qu'à la clôture
   de la seconde déclaration** (tentative de re-tamponner un `DT_Id` déjà posé par la première) —
   une erreur 50028 tardive, en fin de workflow, potentiellement en cours de transaction partagée
   avec d'autres règlements sans problème → échec de clôture confus plutôt qu'un blocage clair et
   précoce à la sélection.
5. **Périmètre distinct de TASK-079** : TASK-079 gère la libération explicite (suppression d'une
   déclaration `EnCours`, admin-only, audité) — elle reste correcte : rien n'est verrouillé avant
   clôture, donc supprimer une déclaration `EnCours` libère bien ses lignes sans opération
   supplémentaire. TASK-080 couvre l'invariant inverse : **tant qu'une déclaration `EnCours` n'est
   pas supprimée (TASK-079) ni clôturée (donc soit encore vivante avec ses lignes), ses règlements
   ne doivent pas être sélectionnables ailleurs.**

## Objectif
Un règlement/affectation ne peut être `Proposee`/`Integree` que dans **au plus une** déclaration à
la fois, que celle-ci soit `EnCours` ou `Cloturee` — l'exclusion doit se produire **au moment de la
sélection** (motif explicite, pas un échec tardif à la clôture).

## Périmètre proposé
### A. Exclusion à la sélection (bloquant)
Ajouter, dans les 4 requêtes de sélection concernées (`GetSurensembleFournisseurSql`,
`GetSurensembleDepenseSql`, `GetSurensembleClientSql`, `GetFactureFirstSql` —
`Declaration.Selection/SelectionExpliqueeService.cs`), un `LEFT JOIN`/sous-requête vers `DM_LGTVA`
(base persistance, connexion distincte de `RT_*`/GRF — attention cross-connexion, voir Risques)
pour détecter qu'une ligne existe déjà avec `Etat IN (Proposee, Integree)` pour ce
`(NumeroFacture, NumeroRapprochement)` (ou clé équivalente déjà utilisée par TASK-077 pour
apparier, `EC_Id`/`MV_Id`) dans une déclaration **différente** de celle en cours de figeage.
Le motif de rejet doit être explicite et honnête (cohérent avec la doctrine « aucun rejet
silencieux » déjà en place pour `DejaDeclare`) : nouveau motif `MotifRejet.DejaEnCoursAilleurs`
(ou libellé équivalent), distinct de `DejaDeclare` (celui-ci reste réservé à `DT_Id` posé/clôture).

### B. Cohérence avec le figeage existant (bloquant)
`ChargerCandidatesSiNecessaireAsync` (`DeclarationWorkflowService.cs:117`) et
`RevaliderLignesFigeesAsync` (TASK-077) ne doivent pas se déclencher en boucle sur ce nouveau motif
— une ligne exclue pour `DejaEnCoursAilleurs` doit rester `Exclue` de façon stable tant que l'autre
déclaration existe, et redevenir sélectionnable dès que celle-ci est supprimée (TASK-079) ou que sa
ligne change d'état (`Reportee`/`Ecartee`/`Exclue`) — pas seulement à la clôture.

### C. Affichage front (non bloquant)
Si le motif `DejaEnCoursAilleurs` apparaît dans les lignes écartées d'un domaine, il doit être
visible avec un message clair (ex. « déjà pris en compte dans la déclaration TVA{numéro} »), en
cohérence avec la doctrine existante des motifs de rejet honnêtes.

## Garde-fous
1. Ne pas toucher au verrou `DT_Id`/triggers SQL (`003_Verrou_DT_Id.sql`) — cette task ajoute une
   garde **en amont** (sélection), le verrou existant reste le filet de sécurité final.
2. La requête d'exclusion contre `DM_LGTVA` doit être bornée (`DeclarationId != @declarationIdEnCours`
   et `Etat IN (Proposee, Integree)`) — ne jamais exclure une ligne de la déclaration courante
   elle-même (idempotence du figeage déjà en place).
3. Ne pas dupliquer la règle de détection : réutiliser la même clé d'appariement que TASK-077
   (`NumeroFacture`/`NumeroRapprochement`, ou `EC_Id`/`MV_Id` si déjà backfillés) plutôt qu'en
   inventer une nouvelle.

## Risques / points à trancher en conception
- **Connexions séparées** : les requêtes de sélection s'exécutent sur `GrfConnectionString`
  (`RT_*`, base GRF/Sage), tandis que `DM_LGTVA` vit sur `PersistenceConnection` (cf.
  `DeclarationRepository.CreatePersistenceConnection`). Un `JOIN` SQL direct est impossible entre
  les deux bases si elles sont physiquement distinctes — à vérifier (TASK-024 a déjà traité un cas
  similaire pour le cache Sage). Si bases distinctes : charger l'ensemble des clés déjà
  `Proposee`/`Integree` ailleurs via une requête `PersistenceConnection` séparée, puis exclure côté
  C# (comme le fait déjà `GetEcIdsEnErreurAsync`/`GetMvPointsActuelsAsync` pour TASK-077) plutôt
  qu'un JOIN SQL cross-base.
- **Volume** : si beaucoup de déclarations `EnCours` coexistent, borner la requête d'exclusion à la
  même société (`SocieteId`) au minimum.
- **Rétro-compatibilité TASK-077** : si des lignes déjà figées avant ce correctif se retrouvent
  dans cette situation de double-affectation, prévoir leur détection au même titre que les autres
  revalidations légères (`RevaliderLignesFigeesAsync`), pas seulement au figeage initial.

## Livrables de preuve (VERIFY)
1. Preuve réelle : créer deux déclarations `EnCours` couvrant délibérément le même règlement (ex.
   deux `Type` différents sur la même période, ou tout autre chevauchement réel constaté en base) →
   le second figeage exclut la ligne avec motif `DejaEnCoursAilleurs`, visible côté front.
2. Preuve réelle : suppression de la première déclaration (TASK-079) → le règlement redevient
   sélectionnable dans la seconde après resynchronisation/revalidation.
3. Aucune régression sur le motif `DejaDeclare` existant (clôture/`DT_Id`).
4. Build + tests `Declaration.Selection.Tests` (dont `SelectionExpliqueeEvaluatorTests.cs`,
   `IntegrationRegressionTests.cs`) verts.

## Dépendances
- **Précède/complète** TASK-079 (suppression déclaration `EnCours`) : TASK-079 reste valide sans
  cette task (la libération fonctionne déjà techniquement), mais sans TASK-080 le système autorise
  une fenêtre de double-affectation entre la création de deux déclarations concurrentes — à livrer
  dans le même mouvement pour fermer le trou de bout en bout.
- **S'appuie sur** la doctrine de motifs honnêtes (TASK-015/021) et l'appariement de clés TASK-077.
- **Aucune dépendance** sur TASK-028/064 (verrou) — reste le filet de sécurité final, inchangé.
