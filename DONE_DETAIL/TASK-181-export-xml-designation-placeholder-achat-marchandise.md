# TASK-181 — Export XML « Relevé de déductions » : placeholder « Achat marchandise » pour `<des>`

## Contexte
Décision PO (24/07/2026) : le tag `<des>` (désignation de la pièce) de l'export XML CDC DGI est
aujourd'hui toujours vide (`Designation = ""` en dur, gap connu — TODO déjà posé à
`ConstructeurDeclaration.cs:149` et documenté au niveau de `ConstruireModeleExportAsync`,
`DeclarationWorkflowService.cs:1218` : source réelle `DM_LGTVA`/`LigneCandidate` jamais exposée,
hors périmètre TASK-155/TASK-011).

En attendant l'arbitrage de la vraie source, le PO acte un placeholder en dur : **"Achat
marchandise"**. Confirmé par le PO : cet export XML ne porte **que** la TVA déductible
(Décaissement) — `DeclarationXmlExporter.GenererXml` filtre déjà explicitement
`l.Source != SourceAffectation.Encaissement` (`DeclarationXmlExporter.cs:21-22`, confirmation PO du
14/07/2026, non remise en cause ici). Aucune conditionnalité par sens fiscal à prévoir : une seule
valeur, pour toutes les lignes exportées par ce fichier.

## Décision de périmètre
Modifier **uniquement** l'export XML (`DeclarationXmlExporter.cs`), pas la source partagée
`ConstruireModeleExportAsync` (`DeclarationWorkflowService.cs:1264`). Cette méthode alimente aussi
l'export Excel de dépôt (`Exporter.cs::CreerFeuilleDetail`, colonne « Désignation ») — le PO n'a
demandé le placeholder que pour « le fichier XML » ; toucher la source partagée changerait aussi le
rendu Excel sans que ce soit demandé. Le placeholder est donc posé au point d'écriture du tag `<des>`
lui-même, pas en amont.

## Périmètre STRICT
- **Inclus** :
  - `Declaration.Export.Xml/DeclarationXmlExporter.cs:65` : remplacer
    `EscapeXml(ligne.Designation?.Trim())` par le littéral fixe `"Achat marchandise"` (échappement
    XML conservé par cohérence, même si une chaîne fixe sans caractère spécial ne le requiert pas
    strictement).
  - Mettre à jour les tests existants qui assertent le contenu de `<des>`
    (`Declaration.Export.Xml.Tests/DeclarationXmlExporterTests.cs`, lignes ~51/128/144/251/338 —
    actuellement basés sur `ligne.Designation`, doivent désormais attendre
    `<des>Achat marchandise</des>` quelle que soit la valeur de `Designation` en entrée).
- **Exclu** :
  - `ConstruireModeleExportAsync`/`Designation = ""` (`DeclarationWorkflowService.cs:1264`) — reste
    inchangé, le gap DM_LGTVA/LigneCandidate reste ouvert et documenté tel quel.
  - `Exporter.cs` (export Excel, feuilles « Détail » et « Factures à déclarer ») — colonne
    « Désignation » continue d'afficher la valeur réelle du modèle (vide aujourd'hui), non concernée
    par ce placeholder XML.
  - Toute logique conditionnelle par sens fiscal (Décaissement/Encaissement) — le filtre existant
    exclut déjà l'Encaissement en amont ; ne pas ajouter de branche pour un cas qui ne peut pas se
    présenter dans ce fichier.

## Étapes
1. `DeclarationXmlExporter.cs:65` : remplacer la valeur écrite dans `<des>` par le littéral
   `"Achat marchandise"`.
2. Ajouter/adapter un commentaire bref au point de changement renvoyant à cette TASK et au point
   ouvert TODO.md (placeholder temporaire, vraie source à trancher plus tard).
3. Mettre à jour les tests `DeclarationXmlExporterTests.cs` impactés (assertions sur `<des>`).
4. Rejouer `dotnet test Declaration.Export.Xml.Tests` (et la suite complète pour non-régression).

## Livrables
- `Declaration.Export.Xml/DeclarationXmlExporter.cs` modifié (1 ligne + commentaire).
- Tests `DeclarationXmlExporterTests.cs` mis à jour.
- `VERIFY/TASK-181_verify.md` avec un exemple de fichier `.xml` généré montrant
  `<des>Achat marchandise</des>` sur toutes les lignes, + build/tests rejoués.

## Critères de validation
- Toutes les lignes du fichier XML généré portent `<des>Achat marchandise</des>`, indépendamment de
  la valeur de `LigneDeclarationEnrichie.Designation` en entrée (actuellement toujours vide, mais le
  placeholder doit s'appliquer même si une valeur non vide était un jour présente — c'est un
  remplacement total, pas un fallback conditionnel).
- Aucun changement sur l'export Excel (colonne « Désignation » des feuilles « Détail »/« Factures à
  déclarer » inchangée).
- Aucun changement sur le filtre Décaissement/Encaissement existant
  (`DeclarationXmlExporter.cs:21-22`).
- Build + `Declaration.Export.Xml.Tests` rejoués verts.

## Risques / dépendances
- **Risque quasi nul** : changement d'une seule valeur littérale, dans un seul fichier, sur un
  export déjà isolé (Décaissement uniquement, confirmé PO).
- **Dépendance/suivi** : ce placeholder reste un pis-aller assumé — la vraie source de désignation
  (`DM_LGTVA`/`LigneCandidate`) reste un point ouvert non tranché (cf. `TODO.md`, point ouvert
  24/07/2026). Ne pas fermer ce point ouvert en clôturant cette TASK ; seule la valeur littérale
  temporaire est livrée ici.
