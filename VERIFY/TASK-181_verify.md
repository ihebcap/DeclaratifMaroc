# TASK-181 Verify — Export XML : placeholder « Achat marchandise » pour `<des>`

## Périmètre livré

Le tag `<des>` de l'export XML « Relevé de déductions » (`DeclarationXmlExporter.GenererXml`) écrit
désormais toujours le littéral fixe `"Achat marchandise"`, quelle que soit la valeur de
`LigneDeclarationEnrichie.Designation` en entrée (vide aujourd'hui en pratique, mais remplacement
total assumé même si une valeur réelle était un jour présente — pas un fallback conditionnel).

Rien d'autre n'a changé :
- Le filtre Décaissement/Encaissement (`DeclarationXmlExporter.cs:21-22`) est intact.
- `ConstruireModeleExportAsync` (`DeclarationWorkflowService.cs`) et `Designation = ""` ne sont pas
  touchés — le gap `DM_LGTVA`/`LigneCandidate` reste ouvert tel quel (cf. `TODO.md`).
- `Declaration.Export.Excel/Exporter.cs` (colonne « Désignation » des feuilles Excel) n'est pas
  touché.

## Fichiers modifiés

- `Declaration.Export.Xml/DeclarationXmlExporter.cs` — ligne 65 (désormais 68) : remplacement de
  `EscapeXml(ligne.Designation?.Trim())` par `EscapeXml("Achat marchandise")`, avec un commentaire
  renvoyant à TASK-181 et au point ouvert `TODO.md`.
- `Declaration.Export.Xml.Tests/DeclarationXmlExporterTests.cs` — 2 assertions mises à jour :
  - `GenererXml_Succes_CreeXmlEtZipAvecBonFormat` (ligne ~102) : attend désormais
    `<des>Achat marchandise</des>` (la ligne en entrée porte toujours `Designation = "Achat
    matériel"`, volontairement laissé tel quel pour prouver que la valeur d'entrée n'a plus
    d'influence sur la sortie).
  - `GenererXml_EspaceResiduel_EstTrimme` (ligne ~341) : attend désormais
    `<des>Achat marchandise</des>` (la ligne en entrée porte toujours `Designation = " Marchandises
    "`, même remarque — le test vérifie toujours le trim de `<num>` par ailleurs, seule
    l'assertion sur `<des>` a changé de sens).

Aucun autre fichier de production ni de test modifié dans ce commit.

## Diff résumé

```diff
-                sb.Append($"<des>{EscapeXml(ligne.Designation?.Trim())}</des>\r\n");
+                // Placeholder fixe assumé (PO 24/07/2026, TASK-181) : Designation reste vide en amont
+                // (DM_LGTVA/LigneCandidate non exposée, cf. TODO.md). Remplacement total, pas un
+                // fallback conditionnel — tant que ce point n'est pas explicitement révisé.
+                sb.Append($"<des>{EscapeXml("Achat marchandise")}</des>\r\n");
```

Tests : 2 assertions littérales changées (`Achat matériel` → `Achat marchandise`,
`Marchandises` → `Achat marchandise`), aucune restructuration de test.

## Exemple de fichier XML généré

Généré via un mini-programme jetable (hors dépôt, supprimé après vérification) appelant
`DeclarationXmlExporter.GenererXml` avec 2 lignes Décaissement : une avec `Designation = ""` (cas
réel actuel) et une avec `Designation = "Prestation de service réelle"` (cas hypothétique, pour
prouver le remplacement total). Extrait complet obtenu :

```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<DeclarationReleveDeduction xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
<identifiantFiscal>12345678</identifiantFiscal>
<annee>2026</annee>
<periode>6</periode>
<regime>1</regime>
<releveDeductions>
<rd>
<ord>1</ord>
<num>FA2600001</num>
<des>Achat marchandise</des>
<mht>1000</mht>
<tva>200</tva>
<ttc>1200</ttc>
<refF><if>12345678</if><nom>Fournisseur A</nom><ice>123456789012345</ice></refF>
<tx>0.2</tx>
<mp><id>4</id></mp>
<dpai>2026-06-15</dpai>
<dfac>2026-06-10</dfac>
</rd>
<rd>
<ord>2</ord>
<num>FA2600002</num>
<des>Achat marchandise</des>
<mht>500</mht>
<tva>100</tva>
<ttc>600</ttc>
<refF><if>87654321</if><nom>Fournisseur B</nom><ice>987654321098765</ice></refF>
<tx>0.2</tx>
<mp><id>2</id></mp>
<dpai>2026-06-16</dpai>
<dfac>2026-06-11</dfac>
</rd>
</releveDeductions>
</DeclarationReleveDeduction>
```

Les deux lignes portent `<des>Achat marchandise</des>` malgré des `Designation` d'entrée
différentes (vide / non vide) — confirme le remplacement total exigé par la TASK.

## Checklist

- [x] Build ciblé OK — `dotnet build Declaration.Export.Xml.Tests/Declaration.Export.Xml.Tests.csproj`
      → 0 erreur (warnings CS8604/CS8602 préexistants sur `EscapeXml`, non liés à ce changement,
      non introduits par lui).
- [x] `dotnet test Declaration.Export.Xml.Tests` → **13/13 réussis**.
- [x] Toutes les lignes du XML généré portent `<des>Achat marchandise</des>`, indépendamment de
      `Designation` en entrée (prouvé par le test `GenererXml_Succes_...` — ligne en entrée
      `Designation = "Achat matériel"` — et par l'exemple généré ci-dessus avec 2 valeurs
      différentes, dont une non vide).
- [x] Aucun changement sur l'export Excel — `Declaration.Export.Excel/Exporter.cs` non touché par
      ce commit (modifié par ailleurs dans l'arbre de travail par TASK-180, en cours en parallèle
      — non inclus ici).
- [x] Aucun changement sur le filtre Décaissement/Encaissement (`DeclarationXmlExporter.cs:21-22`)
      — vérifié par lecture, ligne intacte.
- [x] Aucun bypass sécurité — changement d'une valeur littérale d'affichage XML uniquement, aucune
      logique de sécurité/autorisation touchée.
- [x] Aucune dette technique silencieuse — le compromis (placeholder fixe, vraie source non
      tranchée) est déjà documenté dans la TASK et le `TODO.md` ; commentaire ajouté au point de
      code renvoyant explicitement à TASK-181 et au point ouvert.

## Reste à valider (honnête, non déclaré comme validé)

1. **Build complet `dotnet build DeclarationTVA.slnx` : non concluant dans cet environnement**,
   pour une raison **indépendante de ce changement** — deux process `Declaration.API.exe` sont
   actuellement en cours d'exécution sur ce poste (un service Windows, PID 65092 ; une instance
   console, PID 60376), verrouillant les DLL de sortie de `Declaration.API\bin\...`. Toute
   compilation qui touche `Declaration.API` (directement, ou via une référence de projet comme
   `Declaration.Orchestration.Tests` → `Declaration.API`) échoue avec `MSB3027`/`MSB3021`
   (copie de fichier verrouillé), **avant même la compilation du code**. Je n'ai pas arrêté ces
   process (risque de couper un environnement/test d'un autre worker en cours, TASK-180 étant
   signalé comme possiblement concurrent). **Ce blocage n'a aucun rapport avec le code modifié
   par TASK-181** : `Declaration.Export.Xml`/`Declaration.Export.Xml.Tests` ne référencent pas
   `Declaration.API` et compilent/testent sans erreur (voir ci-dessus, 13/13 verts).
2. **Suite complète rejouée projet par projet** (contournement du point 1, en excluant les seuls
   projets bloqués par le verrou ci-dessus) :
   - `Declaration.Core.Tests` → **52/52 réussis**.
   - `Declaration.Export.Xml.Tests` → **13/13 réussis** (cible de cette TASK).
   - `Declaration.Selection.Tests` → **58/59 réussis**, 1 échec préexistant sans rapport avec ce
     changement (déjà documenté ainsi dans `VERIFY/TASK-137_verify.md`, même chiffre 58/59).
   - `Declaration.Controle.Tests` → **1/2 réussi**, échec `Déclaration GRFN 66 introuvable` — test
     d'intégration dépendant de données réelles en base, aucun rapport avec l'export XML.
   - `Declaration.Export.Excel.Tests` → **1/3 réussis**, 2 échecs — **fichiers concernés
     (`Exporter.cs`, `ExporterTests.cs`) actuellement modifiés dans l'arbre de travail par
     TASK-180 (en cours en parallèle, non commité)** ; ces échecs appartiennent à ce chantier
     concurrent, pas à TASK-181 qui ne touche aucun des deux fichiers.
   - `Declaration.Orchestration.Tests` → build impossible, bloqué par le verrou du point 1
     (référence directe à `Declaration.API`) — non rejoué.
   Aucun de ces échecs/blocages ne peut être causé par TASK-181 : le diff de ce commit se limite
   strictement à `Declaration.Export.Xml/DeclarationXmlExporter.cs` et
   `Declaration.Export.Xml.Tests/DeclarationXmlExporterTests.cs` (vérifié via `git diff --stat`),
   fichiers qu'aucun des projets en échec ne référence.
3. Le fichier XML d'exemple ci-dessus a été généré via un mini-programme jetable (hors dépôt,
   supprimé après capture) plutôt que via un cycle complet clôture→génération réel (aucune
   déclaration `Cloturée` disponible en base de test au moment de ce VERIFY, même réserve que
   TASK-155). L'exemple prouve le comportement de `DeclarationXmlExporter` lui-même (seul
   composant modifié), pas le cycle bout-en-bout.

## Notes

- Le compromis assumé par cette TASK (placeholder fixe, vraie source de désignation non tranchée)
  reste documenté comme point ouvert dans `TODO.md` — ce VERIFY ne le referme pas.
- Recommandation à l'architecte : ne fermer le point 1 ci-dessus (build solution complet) que
  lorsque l'environnement sera libre de tout process `Declaration.API.exe` bloquant, ou en
  acceptant la vérification par sous-ensemble de projets ci-dessus comme suffisante pour ce
  changement à risque quasi nul et à portée strictement limitée à 2 fichiers.
