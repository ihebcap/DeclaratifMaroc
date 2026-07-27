# TASK-180 — Export Excel : détail Collecté/Déductible, format date sans heure, date de rapprochement en colonne séparée

## Contexte
Retour PO (24/07/2026) sur l'export Excel de contrôle (`ExporterExcelControle`, feuille « Détail TVA »,
capture d'écran fournie) — 4 demandes. Une seule concerne une donnée déjà tracée (TASK-162, N° de
règlement), les 3 autres sont de nouveaux constats.

## Constat (lecture code, architecte)

### 1. « Totaux par taux » / « Totaux par code activité » sont regroupés, il faut le détail Déductible/Collecté
Capture PO : les deux tableaux de `CreerFeuilleDetailTva` (`Exporter.cs:263-321`, feuille « Détail TVA »
de l'export de contrôle TASK-160) et de `CreerFeuilleRecap` (`Exporter.cs:68-177`, feuille « Récap » de
l'export de dépôt TASK-010/155) additionnent **Décaissement (déductible) et Encaissement (collecté) dans
la même ligne** — un seul total par taux, un seul total par code activité, sans distinction du sens. Le
PO ne peut pas vérifier séparément sa TVA collectée et sa TVA déductible, alors que c'est la distinction
fiscale de base d'une déclaration de TVA.
- `RecapParTaux`/`RecapParActivite` (`Declaration.Core/Model.cs:76-90`) ne portent **aucun** champ de
  sens/domaine — uniquement `Taux`/`CodeActivite` + les 3 totaux.
- Calculés par `GroupBy(l => l.Taux)` / `GroupBy(l => l.CodeActivite)` seuls, à deux endroits distincts
  mais strictement symétriques : `ConstructeurDeclaration.cs:178-196` (export dépôt) et
  `DeclarationWorkflowService.cs:1390-1416` (export contrôle, `ConstruireModeleControleAsync`).
- **La donnée pour distinguer existe déjà sur chaque ligne** dans les deux chemins :
  `LigneDeclarationEnrichie.Source` (enum `SourceAffectation { Decaissement, Espece, Depense,
  Encaissement }`, `Model.cs:16/64`) — déjà utilisé pour `RecapParSource` (`GroupBy(l => l.Source)`,
  juste au-dessus dans les deux fichiers). `Source == Encaissement` ⇒ Collecté, toute autre valeur
  (Decaissement/Espece/Depense, cf. `[[grf-priorite-rapprochement-tva]]` en mémoire architecte : ces
  trois sources restent internes au domaine fournisseur) ⇒ Déductible.
- Précédent direct : TASK-174 (`DONE_DETAIL/TASK-174-...md`) a déjà fait exactement ce clivage
  Collecté/Déductible par code activité, mais **uniquement pour l'écran ③ front** (`GetCheckup`/
  `DeclarationFinalePanel.tsx`, regroupement par `Domaine` string côté DTO checkup) — jamais reporté
  sur l'export Excel, qui reste un chemin de calcul entièrement distinct (`ConstructeurDeclaration.cs`/
  `ConstruireModeleControleAsync`, pas `GetCheckupAsync`).

### 2. N° de règlement dans la liste des factures — déjà tracé, ne pas dupliquer
Déjà couvert intégralement par [TASK-162](TASK-162-export-excel-ajout-numero-reglement.md) (créée
23/07/2026, état **prêt**, non encore livrée) : colonne « N° Règlement » (`NumeroRapprochement` =
`RT_MOUVEMENT.MV_Numero`) dans `CreerFeuilleDetail` et `CreerFeuilleFacturesControle`. Aucune nouvelle
task ouverte ici — confirmer avec le PO que TASK-162 répond bien à ce point avant de la prioriser.

### 3. Date Facture (et Date Paiement) affichées avec une heure
Aucune des méthodes de `Exporter.cs` n'applique de `Style.NumberFormat` explicite aux cellules de type
date (`DatePaiement` colonne 13, `DateFacture` colonne 14 dans `CreerFeuilleDetail`/
`CreerFeuilleFacturesControle` ; `Date` colonne 2 dans `CreerFeuilleReglementsSelectionnes`) — la cellule
reçoit un `DateTime` .NET brut (`ws.Cell(...).Value = ligne.DateFacture.Value`) et ClosedXML lui applique
son format par défaut, qui expose la partie horaire dès que la valeur porte une heure non nulle. Les
colonnes sources (`AF_Date`/`DO_Date`, `SelectionExpliqueeService.cs`/`SelectionnerAffectationsService.cs`)
sont des `datetime` Sage — rien ne garantit qu'elles soient toujours à minuit en base. **Fix indépendant
de la cause exacte en base** : appliquer un format d'affichage explicite date-seule (`"dd/mm/yyyy"`) sur
toutes les cellules date de l'export, ce qui masque la partie horaire quelle que soit la valeur réelle
stockée, sans toucher à la donnée elle-même.

### 4. Feuille « Règlements sélectionnés » : la date de rapprochement est incluse dans le texte du statut
`CreerFeuilleReglementsSelectionnes` (`Exporter.cs:200-222`) affiche une seule colonne « État pointage »
contenant une chaîne composite : `"Rapproché (30/01/2026)"` ou `"Non rapproché"`
(`DeclarationWorkflowService.cs:1344-1346` :
`EtatPointage = r.EstRapprocheBanque ? $"Rapproché{(r.DateRapprochement.HasValue ? $" ({r.DateRapprochement.Value:dd/MM/yyyy})" : "")}" : "Non rapproché"`).
Le PO veut la date de rapprochement dans une colonne séparée, exploitable (tri/filtre Excel), pas
enfouie dans un texte. La donnée existe déjà (`ReglementRapprochement.DateRapprochement`, déjà lue à la
ligne 1345) — seul le modèle `ReglementSelectionneInfo` (`Model.cs:135-143`) et l'écriture Excel doivent
changer.

## Objectif
1. **Détail Collecté/Déductible** — sur les deux totaux « par taux » et « par code activité », dans les
   deux exports (dépôt `CreerFeuilleRecap` + contrôle `CreerFeuilleDetailTva`) :
   - Ajouter le clivage par sens (Collecté = `Source == Encaissement`, Déductible = sinon) au niveau du
     calcul (`ConstructeurDeclaration.cs:178-196` et `DeclarationWorkflowService.cs:1390-1416`) —
     `GroupBy(l => new { l.Taux, Collecte = l.Source == SourceAffectation.Encaissement })` (idem pour
     CodeActivite). Ajouter un champ (`bool Collecte` ou `string Sens`) à `RecapParTaux`/`RecapParActivite`.
   - À l'écriture Excel, soit deux blocs de tableau distincts (« Totaux par taux — Collecté » /
     « — Déductible »), soit une colonne « Sens » supplémentaire dans le même tableau — **arbitrage
     laissé au développeur, cohérence avec le rendu déjà validé par le PO pour TASK-174 côté front
     recommandée** (deux tableaux séparés, un par domaine).
2. **Format date sans heure** : appliquer `.Style.DateFormat.Format = "dd/mm/yyyy"` (ou équivalent
   `NumberFormat.Format`) sur toutes les cellules date écrites par `Exporter.cs` — `DatePaiement`,
   `DateFacture` (2 méthodes), `Date` (règlements sélectionnés).
3. **Date de rapprochement en colonne séparée** :
   - `ReglementSelectionneInfo` (`Model.cs:135-143`) : remplacer/compléter `EtatPointage` (garder un
     statut court, ex. `"Rapproché"`/`"Non rapproché"`, sans date concaténée) par un nouveau champ
     `DateTime? DateRapprochement`.
   - `DeclarationWorkflowService.cs:1344-1346` : ne plus construire la chaîne composite, mapper
     directement `EtatPointage = r.EstRapprocheBanque ? "Rapproché" : "Non rapproché"` et
     `DateRapprochement = r.DateRapprochement`.
   - `CreerFeuilleReglementsSelectionnes` (`Exporter.cs:200-222`) : ajouter une colonne « Date
     rapprochement » (format date-seule, point 2), cellule vide si non rapproché.

## Périmètre STRICT
- **Inclus** : `Declaration.Core/Model.cs` (`RecapParTaux`/`RecapParActivite`/`ReglementSelectionneInfo`),
  `Declaration.Core/ConstructeurDeclaration.cs`, `Declaration.Application/Services/
  DeclarationWorkflowService.cs` (`ConstruireModeleControleAsync`), `Declaration.Export.Excel/Exporter.cs`
  (4 méthodes : `CreerFeuilleRecap`, `CreerFeuilleDetailTva`, `CreerFeuilleReglementsSelectionnes`, +
  format date dans `CreerFeuilleDetail`/`CreerFeuilleFacturesControle`).
- **Exclus** : TASK-162 (N° de règlement, déjà tracée séparément — ne pas fusionner les deux tasks pour
  garder un VERIFY lisible par sujet) ; toute modification du front (React) — demande strictement
  limitée à l'export Excel ; le calcul TVA lui-même (aucun recalcul, uniquement de la ré-agrégation et du
  formatage d'affichage).

## Livrables
- Fichiers listés ci-dessus modifiés.
- Tests mis à jour/ajoutés : `Declaration.Core.Tests/ConstructeurDeclarationTests.cs`,
  `Declaration.Orchestration.Tests/Task160ExportControleTests.cs` (dont les 2 tests existants qui
  vérifient `EtatPointage` par égalité de chaîne, `:170`/`:189`, à adapter au nouveau champ),
  `Declaration.Export.Excel.Tests/ExporterTests.cs`.
- `VERIFY/TASK-180_verify.md` avec un `.xlsx` d'exemple réel montrant : les 2 tableaux Collecté/Déductible
  peuplés sur un cas mixte, une date facture/paiement sans heure visible, la colonne « Date
  rapprochement » peuplée pour un règlement rapproché et vide pour un non rapproché.

## Critères de validation
- Somme (Collecté + Déductible) par taux = ancien total non scindé (aucune perte de montant) — même
  vérification pour le code activité.
- Aucune ligne sans `Source`/`CodeActivite` renseigné n'est masquée silencieusement (même règle que
  TASK-174 : bucket `""`/« — » visible, pas filtré).
- Date Facture/Date Paiement/Date rapprochement : plus aucune heure visible dans le fichier `.xlsx`
  généré, sur un cas réel où la donnée source porte une heure non nulle (à vérifier en base, cf. risque
  ci-dessous).
- Colonne « État pointage » : toujours `"Rapproché"` ou `"Non rapproché"` (jamais de date concaténée) ;
  nouvelle colonne « Date rapprochement » correcte et vide si non rapproché.
- Build + tests (`Declaration.Core.Tests`, `Declaration.Orchestration.Tests`,
  `Declaration.Export.Excel.Tests`) rejoués verts.

## Risques / dépendances
- **Non vérifié empiriquement** : que `AF_Date`/`DO_Date` portent réellement une heure non nulle en base
  pour au moins un cas — le fix (format explicite) résout l'affichage dans tous les cas, mais si aucune
  ligne réelle ne porte d'heure, le VERIFY devra le signaler explicitement (pas de preuve vide déguisée
  en preuve positive).
- Choix de rendu du point 1 (deux tableaux vs colonne « Sens ») à trancher par le développeur faute
  d'arbitrage PO explicite sur ce détail — signaler le choix fait dans le VERIFY, pas juste l'appliquer
  silencieusement.
- Aucune dépendance dure avec TASK-162 (fichier partagé `Exporter.cs`, mais sections de code disjointes
  — N° Règlement touche l'insertion de colonne dans `CreerFeuilleDetail`/`CreerFeuilleFacturesControle`,
  cette task touche `CreerFeuilleRecap`/`CreerFeuilleDetailTva`/`CreerFeuilleReglementsSelectionnes` +
  le format date des colonnes existantes). Si les deux tasks sont prises par le même worker, les livrer
  dans des commits séparés pour garder un VERIFY par sujet, comme demandé par les règles du projet.
