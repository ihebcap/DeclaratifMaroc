# TASK-137 — Mise en conformité de l'export XML "Relevé de déductions" avec le CDC DGI

## Contexte
Analyse architecte (19/07/2026, sur demande PO) : comparaison du générateur XML existant
(`Declaration.Export.Xml/DeclarationXmlExporter.cs`, livré TASK-011) avec le cahier des charges
externe `D:\_vibe\apbs-gr_winform\analayse\CDC-EXPORT-XML-TVA-DEDUCTION.md`, lui-même issu de la
comparaison d'un fichier réellement **accepté** par le portail fiscal marocain avec l'ancien
générateur `apbs-gr_winform` (`DeclarationTvaEncaissementFileGenerator`).

**Origine de l'écart** : TASK-011 avait pour instruction explicite de **reproduire fidèlement**
l'algorithme de l'ancien générateur GRFN (décision PO de l'époque, avant l'existence de ce CDC).
Ce CDC, écrit après coup à partir d'un fichier réellement déposé et accepté, documente que
plusieurs comportements de l'ancien générateur sont en réalité des **anomalies non conformes**
au format attendu (§4 du CDC) — le nouveau générateur les a reproduites fidèlement, comme demandé
à l'époque, et hérite donc des mêmes non-conformités.

**Un défaut supplémentaire, propre au nouveau code (pas hérité de GRFN), a également été identifié
(§F1 ci-dessous) — le plus critique des sept.**

## Constats (par sévérité)

### F1 — 🔴 BLOQUANT : `<tx>` (taux de TVA) exporté en valeur pourcentage au lieu de fraction décimale
`DeclarationXmlExporter.cs:76` écrit `ligne.Taux.ToString("0.00", nfi)` directement. Or `Taux`
provient de `Ventilateur.cs:54` (`taxe.Taux`, valeur brute lue depuis `F_TAXE`/Sage, ex. `20` pour
un taux de 20 %) et n'est **jamais divisé par 100** dans tout le pipeline (confirmé aussi dans
`ConstructeurDeclaration.cs`, `Declaration.Export.Excel`, `LecteurTvaFgr.cs`). Le CDC §2.2/§2.4
exige `tx` en **fraction décimale** (`0.2` pour 20 %, `0.1` pour 10 %, `0.08` pour 8 %). Le fichier
actuel produirait `<tx>20.00</tx>` au lieu de `<tx>0.20</tx>` — un taux de TVA erroné d'un facteur
100 dans un fichier déposé auprès de l'administration fiscale. **Aucun test existant ne couvre ce
champ avec une valeur réaliste (0.2) — les fixtures de test utilisent `Taux = 20m`, ce qui a masqué
le problème.**

### F2 — 🔴 CRITIQUE (anomalie CDC §4.1 reproduite) : prolog XML et `xmlns:xsi` absents
`DeclarationXmlExporter.cs:41` écrit littéralement `"\r\n<DeclarationReleveDeduction>\r\n"` — ni
prolog `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>`, ni attribut
`xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"` sur la racine. Le test
`DeclarationXmlExporterTests.cs:82` **asserte explicitement leur absence comme comportement
correct** (`Assert.False(xmlContent.StartsWith("<?xml"))`). Le CDC (§2.1, §4.1) confirme, via le
fichier réellement accepté, que les deux sont **obligatoires**.

### F3 — 🔴 CRITIQUE (anomalie CDC §4.2 reproduite) : balise `<prorata>` en trop
`DeclarationXmlExporter.cs:77` insère `<prorata>` entre `<tx>` et `<mp>` — absente du format
accepté (CDC §2.2). Testée et assertée comme comportement attendu (test l.89).

### F4 — 🟠 MAJEUR (anomalie CDC §4.8/§5.2 reproduite) : blocage strict IF=8/ICE=15 caractères
`ValidationIdentiteFiscale.EstIfValide`/`EstIceValide` (`Declaration.Core`) exigent une longueur
**exactement** 8 (IF) / 15 (ICE), sinon `ValiderPourExport` lève une `ApplicationException`
bloquante. Le CDC signale que le fichier réellement accepté contient des identifiants fournisseurs
de **7 chiffres** (longueur variable) — un tel contrôle bloquerait la génération d'un export
pourtant valide pour certains tiers.

### F5 — 🟡 MOYEN (point ouvert CDC §5.1, non tranché) : arrondi systématique à 2 décimales
`mht`/`tva`/`ttc`/`tx`/`prorata` sont formatés en `"0.00"` (2 décimales fixes), en plus d'un premier
arrondi déjà appliqué en amont par `Ventilateur.cs` (`Math.Round(..., n, AwayFromZero)`). Le CDC
note que le fichier de référence contient des montants **non arrondis** à haute précision (jusqu'à
~15 chiffres significatifs, répartition proportionnelle) et que les entiers s'affichent **sans**
décimale (`1299`, pas `1299.00`). C'est un point ouvert explicite du CDC (§5.1) — **non tranché
avec le contrôle de gestion/fiscaliste**, à date ni pour l'ancien ni pour le nouveau générateur.

### F6 — 🟢 MINEUR : absence de `Trim()` sur les champs texte libres
`NumeroFacture`, `Designation`, `Tiers.IdentifiantFiscal`, `Tiers.Nom`, `Tiers.Ice` ne sont pas
nettoyés avant écriture. Le CDC §2.6/§4.7 recommande un `Trim()` explicite pour absorber d'éventuels
espaces/tabulations résiduels issus de la donnée source ERP.

### F7 — Point de vigilance (pas une anomalie confirmée) : signe des avoirs non testé
Contrairement à l'anomalie GRFN §4.5 du CDC (inversion `* -1` inconditionnelle par sens
achat/vente), **aucun** `* -1` inconditionnel n'a été trouvé dans le pipeline actuel
(`SelectionnerAffectationsService`, `DeclarationWorkflowService`, `Ventilateur`) — bon point. En
revanche, le modèle (`Declaration.Core/Model.cs`) ne porte aucune notion explicite de "type de
document facture vs avoir" : le signe de `mht`/`tva`/`ttc` dépend entièrement de ce que retourne la
donnée source (Sage/OM), jamais vérifié explicitement, et **aucun cas d'avoir n'existe dans les
fixtures de test actuelles** (`DeclarationXmlExporterTests.cs`, `VentilateurTests.cs`). Risque
résiduel non confirmé — à couvrir par un test dédié + une preuve sur cas réel avant mise en
production, pas un défaut de code identifié à ce stade.

## Périmètre STRICT
- **Inclus** : correction F1 à F4 (bloquantes) ; F5 tranché en collaboration avec le PO/fiscaliste
  (garder la pleine précision decimal, formatage sans arrondi supplémentaire à l'écriture — cf.
  recommandation CDC §5.1 — sauf si le PO confirme qu'un arrondi 2 décimales est en réalité correct) ;
  F6 (Trim) ; F7 traité comme ajout de couverture de test (cas avoir synthétique), pas comme
  correctif de code sauf si le test révèle un défaut réel.
- **Exclu** : toute autre partie du pipeline de calcul/ventilation hors du strict nécessaire à ces
  corrections ; le format Excel (`Declaration.Export.Excel`, hors périmètre CDC).

## Étapes
1. **F1 (priorité absolue)** : diviser `Taux` par 100 à l'écriture de `<tx>` (ou, si jugé plus sûr,
   au point de lecture Sage — à trancher par l'architecte selon l'impact sur `Declaration.Export.Excel`
   et `Declaration.Controle`, qui utilisent aussi `Taux` en valeur pourcentage pour l'affichage — **ne
   pas casser leur format d'affichage**, la conversion doit être locale à l'export XML DGI).
2. Ajouter le prolog XML + l'attribut `xmlns:xsi` sur la racine (F2).
3. Retirer la balise `<prorata>` de la sérialisation (F3).
4. Retirer/assouplir le contrôle bloquant de longueur fixe IF=8/ICE=15 — remplacer par une
   validation non bloquante (ou un contrôle de format moins strict) en attendant confirmation
   définitive du format exact attendu par le fisc (CDC §5.2) ; **signaler** au PO que ce point reste
   ouvert côté CDC et nécessite son arbitrage avant clôture définitive de cette task.
5. Trancher F5 avec le PO (arrondi vs précision pleine) puis ajuster le formatage en conséquence ;
   afficher les entiers sans décimale si aucun arrondi n'est appliqué.
6. Ajouter `Trim()` sur les champs texte libres avant écriture (F6).
7. Mettre à jour tous les tests existants qui assertent les anciens comportements F2/F3/F4 (ils
   deviennent des régressions volontaires assumées, pas des bugs) + ajouter des cas de test pour
   F1 (taux réaliste 0.2/0.1/0.08), un cas avoir (F7, montants négatifs), un cas raison sociale avec
   `&` (échappement, recommandé CDC §6).
8. Si possible, obtenir/produire un export réel et le comparer champ par champ au fichier de
   référence du CDC (`analayse\exemple\TVA TABLEAU DE DEDUCTION MOIS 3-25.xml`, hors dépôt réel —
   test manuel de non-régression, hors périmètre automatisé).

## Livrables
- `DeclarationXmlExporter.cs` corrigé (F1-F4, F6).
- Décision documentée du PO sur F5 (arrondi) et sur le degré de blocage acceptable pour F4 (IF/ICE).
- Tests mis à jour/ajoutés couvrant F1, F2, F3, F4, F6, F7 (cas avoir), et le cas `&` du CDC §6.
- `VERIFY/TASK-137_verify.md`.

## Critères de validation
- `<tx>` exporté en fraction décimale (0.2 pour 20 %), vérifié par test explicite.
- Prolog XML + `xmlns:xsi` présents en sortie.
- Aucune balise `<prorata>` dans la sortie.
- Contrôle IF/ICE non bloquant sur un identifiant à 7 chiffres (ou blocage confirmé explicitement
  par le PO comme comportement voulu, documenté comme tel).
- Décision PO tracée pour F5 (arrondi), formatage aligné en conséquence.
- `Trim()` appliqué et vérifié par test sur au moins un champ avec espace résiduel.
- Cas avoir (montants négatifs) et cas `&` couverts par un test.
- Build 0 erreur, suite de tests `Declaration.Export.Xml.Tests`/`Declaration.Core.Tests` verte.

## Risques / dépendances
- **F1 est un risque de conformité fiscale réel** si un export a déjà été déposé en production
  avec ce défaut — à vérifier avec le PO si un dépôt réel a déjà eu lieu via ce nouveau générateur
  (à distinguer du dépôt historique via `apbs-gr_winform`).
- F4 et F5 restent des **points ouverts du CDC lui-même** (§5.1, §5.2) — non tranchables par le seul
  architecte, nécessitent une confirmation du fiscaliste/contrôle de gestion du client avant clôture
  définitive. Le worker ne doit pas trancher seul ; documenter la décision prise.
- Aucune dépendance technique bloquante avec d'autres tasks en cours.
