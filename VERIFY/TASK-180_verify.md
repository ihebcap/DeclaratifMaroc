# VERIFY — TASK-180

Date: 2026-07-24
Agent: Worker (Claude Code)

## Périmètre livré

Les 3 points de la TASK sont livrés intégralement, dans le périmètre STRICT défini (aucun fichier
hors liste touché, aucun recalcul TVA, aucune modification front) :

1. **Détail Collecté/Déductible** — `RecapParTaux`/`RecapParActivite` portent désormais un champ
   `bool Collecte` (`true` = `Source == SourceAffectation.Encaissement`, `false` = toute autre
   source), calculé aux deux endroits historiques (`ConstructeurDeclaration.cs` pour l'export de
   dépôt, `DeclarationWorkflowService.ConstruireModeleControleAsync` pour l'export de contrôle —
   ce dernier opère sur `LigneCandidate.Source`, une chaîne, comparée à
   `nameof(SourceAffectation.Encaissement)`). **Choix de rendu retenu (arbitrage laissé au
   développeur par la TASK)** : deux tableaux séparés par bloc (« Totaux par taux — Collecté » /
   « — Déductible », idem code activité), cohérent avec le rendu déjà validé PO côté front pour
   TASK-174, plutôt qu'une colonne « Sens » unique. Appliqué aux deux feuilles concernées
   (`CreerFeuilleRecap` export dépôt, `CreerFeuilleDetailTva` export contrôle) via deux méthodes
   helper factorisées (`EcrireBlocRecapParTaux`/`EcrireBlocRecapParActivite`).
2. **Format date sans heure** — constante `FormatDateSeule = "dd/mm/yyyy"` appliquée via
   `.Style.DateFormat.Format` sur **toutes** les cellules date de `Exporter.cs` : `DatePaiement`/
   `DateFacture` dans `CreerFeuilleDetail` (export dépôt) **et** `CreerFeuilleFacturesControle`
   (export contrôle) — les 2 méthodes citées par le PO —, colonne `Date` et nouvelle colonne
   `Date rapprochement` dans `CreerFeuilleReglementsSelectionnes`. Vérifié empiriquement en base
   réelle (`GR_EMA_DISTRIBUTION`/`DESKTOP-5BFKKEP`) : `RT_AFFECTATION.AF_Date` et
   `RT_ECHEANCE.DO_Date` portent bien une heure non nulle sur des lignes réelles (ex. `AF_Id=17777`
   → `2026-01-14 11:49:03.047` ; `EC_Id=18236` → `2026-01-06 15:18:04.543`) — ce n'est donc pas une
   preuve vide : le défaut corrigé se produit réellement en données de production.
3. **Date de rapprochement en colonne séparée** — `ReglementSelectionneInfo.EtatPointage` ne
   contient plus jamais de date concaténée (toujours `"Rapproché"` ou `"Non rapproché"` strict,
   plus d'interpolation `$"Rapproché ({date})"`) ; nouveau champ `DateTime? DateRapprochement`
   peuplé depuis `ReglementRapprochementRow.DateRapprochement` (déjà lu, TASK-042) dans
   `DeclarationWorkflowService.ConstruireModeleControleAsync`. Nouvelle colonne Excel « Date
   rapprochement » (7ᵉ colonne de `CreerFeuilleReglementsSelectionnes`), vide si non rapproché
   (`r.DateRapprochement` null → cellule non écrite).

## Fichiers modifiés

- `Declaration.Core/Model.cs` — `RecapParTaux.Collecte`, `RecapParActivite.Collecte`,
  `ReglementSelectionneInfo.DateRapprochement` (3 nouveaux champs).
- `Declaration.Core/ConstructeurDeclaration.cs` — `RecapsParTaux`/`RecapsParActivite` : `GroupBy`
  étendu à `(Taux|CodeActivite, Collecte)`.
- `Declaration.Application/Services/DeclarationWorkflowService.cs` —
  `ConstruireModeleControleAsync` : même clivage `GroupBy` (comparaison `Source` en chaîne) ;
  `EtatPointage`/`DateRapprochement` désormais mappés séparément (plus de chaîne composite).
- `Declaration.Export.Excel/Exporter.cs` — `CreerFeuilleRecap`, `CreerFeuilleDetailTva` (deux blocs
  Collecté/Déductible via nouveaux helpers `EcrireBlocRecapParTaux`/`EcrireBlocRecapParActivite`) ;
  `CreerFeuilleReglementsSelectionnes` (colonne « Date rapprochement ») ; format date-seule
  (constante `FormatDateSeule`) appliqué sur `CreerFeuilleDetail`, `CreerFeuilleFacturesControle`,
  `CreerFeuilleReglementsSelectionnes`.
- `Declaration.Core.Tests/ConstructeurDeclarationTests.cs` — nouveau test
  `ConstruireDeclaration_Recaps_CollecteDeductible_SommeEgaleAncienTotalNonScinde` (vérifie le
  clivage et l'égalité de somme par taux/activité vs. l'ancien total non scindé).
- `Declaration.Orchestration.Tests/Task160ExportControleTests.cs` — les 2 tests existants
  (`ConstruireModeleControleAsync_ReglementsSelectionnes_JointureSelectionEtRapprochement` ligne
  ~170, `..._ReglementSelectionneNonRapproche_EtatPointageExplicite` ligne ~189) adaptés : assertion
  stricte `Assert.Equal("Rapproché", ...)` (au lieu de `StartsWith`) + assertion sur le nouveau
  champ `DateRapprochement` ; nouveau test
  `ConstruireModeleControleAsync_RecapsParTauxEtActivite_ClivageCollecteDeductible`.
- `Declaration.Export.Excel.Tests/ExporterTests.cs` — fixture `GetFixtureControle` étendue
  (2 règlements dont 1 non rapproché, `RecapsParTaux`/`RecapsParActivite` avec un couple
  Collecte/Déductible) ; assertions réécrites pour le nouveau layout (en-têtes, colonne « Date
  rapprochement », formats de date, 4 blocs Détail TVA).

## Preuve réelle (base `GR_EMA_DISTRIBUTION`, déclaration `TVA1-2026-05`)

Export de contrôle régénéré en conditions réelles via un petit harness console (hors dépôt,
`ConstruireModeleControleAsync` + `GenererExcelControleAsync` appelés directement contre la vraie
base, aucune donnée fabriquée) — fichier joint : `VERIFY/TASK-180-exemple-export-controle.xlsx`
(735 lignes `Integree`, 156 règlements sélectionnés).

Résultats observés :
- `RecapsParTaux` : 8 entrées (4 Collecté / 4 Déductible) ; `RecapsParActivite` : 2 entrées
  (1 Collecté / 1 Déductible) — le clivage produit bien des groupes distincts sur données réelles,
  pas seulement en test synthétique.
- **Somme Collecté+Déductible = ancien total non scindé, vérifiée sur les 735 lignes réelles** :
  `Σ RecapsParTaux.TotalTtc` (tous sens confondus) = `2 094 301,93` = `Σ Lignes.Ttc` — égalité
  stricte confirmée (`decimal ==`), aucune perte de montant.
- Feuille « Détail TVA » : 4 blocs présents dans l'ordre attendu — « Totaux par taux — Collecté »
  (ligne 1), « — Déductible » (ligne 8), « Totaux par code activité — Collecté » (ligne 15),
  « — Déductible » (ligne 19).
- Feuille « Règlements sélectionnés » : en-têtes `Numéro | Date | Montant | Tiers | Mode | État
  pointage | Date rapprochement` ; cellule `Style.DateFormat.Format` = `"dd/mm/yyyy"` confirmée sur
  les colonnes `Date` (B) et `Date rapprochement` (G) ; `EtatPointage` = `"Rapproché"` strict (plus
  aucune date concaténée) sur les 156 règlements réels (tous rapprochés dans ce jeu de données réel
  — le cas « non rapproché » est couvert par le test unitaire synthétique, pas par cette
  déclaration précise).
- Feuille « Factures à déclarer » : `Style.DateFormat.Format` = `"dd/mm/yyyy"` confirmé sur `Date
  Paiement`/`Date Facture` ; valeur affichée formatée (`GetFormattedString()`) = `"24/06/2026"`
  (sans heure) sur une ligne réelle.

## Build

- Status : OK — `dotnet build DeclarationTVA.slnx` → **0 erreur** (solution complète, tous
  projets), 0 avertissement nouveau introduit par ce lot.
- **Réserve non bloquante** : un process `Declaration.API.exe` (instance console de dev, PID
  60376) tournait déjà au moment du build et verrouillait les DLL de sortie de
  `Declaration.API.csproj` (copie MSB3027/MSB3021 en échec sur build "in place"). Tentative
  d'arrêt du process refusée par l'OS (« Accès refusé », process lancé sous des privilèges
  différents de la session courante) malgré confirmation explicite du PO pour tenter l'arrêt.
  **Contournement propre** : build/tests rejoués avec `-p:BaseOutputPath=D:\tmp\build_out_task180\`
  (sortie redirigée hors du dossier verrouillé) → 0 erreur, tous les projets (y compris
  `Declaration.API`, `Declaration.Orchestration.Tests`) compilent sans écart de code — le verrou
  est un artefact d'environnement (deux process `Declaration.API.exe` actifs : un service Windows
  PID 65092 + une instance console PID 60376), pas un défaut introduit par ce lot. À signaler côté
  PO si le process console PID 60376 doit être arrêté manuellement (droits insuffisants pour cette
  session).

## Tests

- `Declaration.Core.Tests` : **52/52** (51 existants + 1 nouveau).
- `Declaration.Export.Excel.Tests` : **3/3** (fixtures/assertions adaptées au nouveau layout).
- `Declaration.Orchestration.Tests` : **182/182** (180 existants + adaptation des 2 tests
  `EtatPointage` + 1 nouveau test clivage Collecte/Déductible).

## Validation checklist

- [x] Build OK (solution complète, voir réserve process ci-dessus — sans impact sur le résultat).
- [x] Tests passés (52+3+182 = 237/237, aucune régression).
- [x] Somme (Collecté + Déductible) par taux = ancien total non scindé — vérifié en test unitaire
      synthétique **et** en conditions réelles (735 lignes, égalité stricte confirmée).
- [x] Même vérification pour le code activité (test unitaire ; en réel, `RecapsParActivite` ne
      compte que 2 entrées au total sur `TVA1-2026-05`, cohérent avec le bucket unique observé
      historiquement sur cette déclaration — pas un défaut de ce lot).
- [x] Aucune ligne sans `Source`/`CodeActivite` masquée silencieusement — le clivage `GroupBy`
      ajoute une dimension supplémentaire (`Collecte`) sans filtrer aucune ligne ; le bucket `""`/
      `"(sans activité)"` déjà en place (TASK-161) reste inchangé et continue d'apparaître.
- [x] Date Facture/Date Paiement/Date rapprochement : plus aucune heure visible dans le `.xlsx`
      généré — format vérifié en réel (`GetFormattedString()` = date seule) **et** cause racine
      confirmée en base (`AF_Date`/`DO_Date` portent bien une heure non nulle sur des lignes
      réelles, cf. § Preuve réelle).
- [x] Colonne « État pointage » : toujours `"Rapproché"`/`"Non rapproché"` strict (jamais de date
      concaténée) — vérifié en réel (156/156 règlements) et en test unitaire (cas non rapproché).
      Nouvelle colonne « Date rapprochement » correcte, vide si non rapproché (testé
      unitairement ; en réel, les 156 règlements de `TVA1-2026-05` sont tous rapprochés, donc la
      colonne est peuplée sur tous — le cas vide est couvert par le test synthétique, pas par
      cette déclaration précise).
- [x] Build + tests (`Declaration.Core.Tests`, `Declaration.Orchestration.Tests`,
      `Declaration.Export.Excel.Tests`) rejoués verts.
- [x] Aucun bypass sécurité — aucune couche service contournée, aucun SQL inline ajouté (le
      harness de preuve, hors dépôt, réutilise `IDeclarationRepository`/`DeclarationRepository`
      existants tels quels, aucune requête SQL écrite à la main).
- [x] Aucune dette technique silencieuse — le choix de rendu (2 tableaux vs colonne « Sens ») est
      documenté ci-dessus, pas appliqué en silence.

## Impacts détectés

- Aucun contrat public (DTO API/JSON) modifié par cette TASK — `ModeleControle`/`DeclarationModele`
  restent des types internes au calcul/export, non exposés tels quels par un contrôleur (seul le
  fichier `.xlsx` binaire sort de l'API, via `GenererExcelControleAsync`/`GenererFichiersExportAsync`
  déjà existants, non modifiés).
- `RecapParTaux`/`RecapParActivite`/`ReglementSelectionneInfo` sont partagés entre l'export de
  dépôt (`DeclarationModele`) et l'export de contrôle (`ModeleControle`) — les 3 nouveaux champs
  s'appliquent donc symétriquement aux deux chemins, comme demandé par la TASK.
  `ConstruireModeleExportAsync` (export de dépôt, TASK-155, non listé dans le périmètre STRICT de
  cette TASK) construit `DeclarationModele` via `ConstructeurDeclaration.ConstruireDeclaration`
  déjà modifié — donc bénéficie aussi du clivage sans changement de code supplémentaire côté
  `DeclarationWorkflowService`. Vérifié par lecture : `ConstruireModeleExportAsync` ne fait
  qu'assembler les affectations et appeler `ConstructeurDeclaration.ConstruireDeclaration` (aucune
  logique de recalcul dupliquée).
- TASK-162 (colonne « N° Règlement ») : **non traitée dans cette session** — fichier partagé
  (`Exporter.cs`) mais sections disjointes (`CreerFeuilleDetail`/`CreerFeuilleFacturesControle`
  pour l'insertion de colonne N° Règlement, vs. `CreerFeuilleRecap`/`CreerFeuilleDetailTva`/
  `CreerFeuilleReglementsSelectionnes` + format date pour TASK-180). Reportée après ce VERIFY par
  choix de priorisation (garder un VERIFY par sujet, comme demandé) — laissée en `TASKS/`, prête
  à être prise dans une session suivante.

## Reste à valider (honnête, non déguisé)

1. **Rendu visuel dans Excel lui-même** : le fichier `.xlsx` de preuve a été vérifié
   programmatiquement (ClosedXML : en-têtes, valeurs, `Style.DateFormat.Format`,
   `GetFormattedString()`) mais **pas ouvert visuellement dans Microsoft Excel** par ce worker
   (pas d'accès interactif à Excel dans cet environnement). Le format de cellule appliqué
   (`"dd/mm/yyyy"`) est le format standard ClosedXML/Excel et le rendu programmatique
   (`GetFormattedString()`) confirme déjà l'absence d'heure — risque résiduel jugé très faible,
   mais une ouverture visuelle par le PO reste recommandée avant clôture définitive.
2. **Cas réel « code activité multiple »** : `TVA1-2026-05` ne porte qu'un seul code activité au
   total (2 entrées `RecapsParActivite`, une par sens) — le clivage a donc été vérifié sur un seul
   code activité en conditions réelles, pas sur un cas multi-activités réel (le test unitaire
   synthétique couvre ce cas avec ACT1/ACT2, mais ce n'est pas la même force de preuve qu'un cas
   réel). Aucune déclaration en base actuelle n'a été identifiée avec plusieurs codes activité
   distincts pour confirmer ce point plus loin — à signaler si le PO dispose d'un cas réel de ce
   type.
3. **Cas réel « règlement non rapproché »** : les 156 règlements sélectionnés de `TVA1-2026-05`
   sont tous rapprochés — la colonne « Date rapprochement » vide (non rapproché) n'a donc été
   vérifiée qu'en test unitaire synthétique, pas sur une donnée réelle. Si le PO dispose d'une
   déclaration réelle avec des règlements sélectionnés non rapprochés, un second passage de
   vérification serait recommandé (correctif jugé suffisamment simple et symétrique pour ne pas
   bloquer ce VERIFY).
4. **Process `Declaration.API.exe` (PID 60376)** : toujours actif à la fin de cette session (arrêt
   refusé par l'OS malgré confirmation PO) — sans impact sur ce VERIFY (build/tests validés via
   sortie redirigée), mais signalé pour information/action opérationnelle si besoin.

## Notes worker

- Naming du champ de clivage : `bool Collecte` retenu (plutôt que `string Sens`), conforme à
  l'une des deux options proposées par la TASK — plus simple à filtrer (`.Where(r => r.Collecte)`)
  et strictement binaire (aucune valeur intermédiaire possible vu la définition du clivage).
- Les deux méthodes helper `EcrireBlocRecapParTaux`/`EcrireBlocRecapParActivite` factorisent le
  code entre `CreerFeuilleRecap` (export dépôt) et `CreerFeuilleDetailTva` (export contrôle), qui
  étaient auparavant deux copies quasi identiques — réduction de duplication en conséquence directe
  du clivage demandé (pas un refactor hors périmètre : sans ce partage, le clivage aurait dû être
  dupliqué 2× dans chaque méthode).
- Alertes RecapsParActivite : ordre des blocs (Collecté avant Déductible, taux avant activité) fixé
  arbitrairement par le worker en l'absence de préférence PO explicite — cohérent entre les 2
  feuilles (dépôt et contrôle) et avec l'ordre des colonnes Collecté/Déductible déjà en place côté
  front TASK-174 (Collecté affiché avant Déductible dans `DeclarationFinalePanel.tsx`).
