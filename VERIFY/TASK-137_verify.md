# VERIFY — TASK-137 — Mise en conformité de l'export XML "Relevé de déductions" avec le CDC DGI

## Fichiers modifiés (chemins exacts)
- `Declaration.Export.Xml/DeclarationXmlExporter.cs` (F1, F2, F3, F5, F6 + trim des IF/ICE passés à la validation)
- `Declaration.Core/ValidationIdentiteFiscale.cs` (F4 — assouplissement `ValiderPourExport` uniquement)
- `Declaration.Export.Xml.Tests/DeclarationXmlExporterTests.cs` (inversion assertions F2/F3, adaptation F1/F4, nouveaux cas)
- `Declaration.Core.Tests/ValidationIdentiteFiscaleTests.cs` (adaptation des tests `ValiderPourExport` au nouveau comportement)

Aucun autre fichier touché. `Declaration.Export.Excel`, `Ventilateur.cs`, `ConstructeurDeclaration.cs`, les DTO
et le front conservent `Taux` en pourcentage (affichage correct) — la conversion `/100` est **locale à l'export XML**.

---

## Constats F1–F6

### F1 — 🔴 BLOQUANT — `<tx>` en fraction décimale
- **Fait** : à l'écriture de `<tx>`, division locale `ligne.Taux / 100m` (aucune modification en amont).
- **Avant** : `<tx>{ligne.Taux.ToString("0.00", nfi)}</tx>` → `<tx>20.00</tx>`
- **Après** : `<tx>{FormatDecimal(ligne.Taux / 100m)}</tx>` → `<tx>0.2</tx>`
- **Test** : `GenererXml_Taux_ExporteEnFractionDecimale` (Theory) — `20→0.2`, `10→0.1`, `8→0.08`. ✅

### F2 — prolog XML + `xmlns:xsi`
- **Fait** : ajout du prolog `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` et de l'attribut
  `xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"` sur la racine. Encodage UTF-8 sans BOM conservé.
- **Avant** : `\r\n<DeclarationReleveDeduction>\r\n` (aucun prolog)
- **Après** :
  ```
  <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
  <DeclarationReleveDeduction xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  ```
- **Test** : assertions `Assert.StartsWith("<?xml ...")` + `Assert.Contains(... xmlns:xsi ...)`
  (l'ancienne `Assert.False(StartsWith("<?xml"))` a été inversée). ✅

### F3 — retrait de `<prorata>`
- **Fait** : ligne `<prorata>...</prorata>` supprimée de la sérialisation (entre `<tx>` et `<mp>`).
  Le champ `Prorata` reste dans le modèle (non utilisé à l'export XML).
- **Avant** : `<tx>...</tx>` puis `<prorata>100.00</prorata>` puis `<mp>...`
- **Après** : `<tx>0.2</tx>` directement suivi de `<mp>...`
- **Test** : `Assert.DoesNotContain("<prorata>", xmlContent)` (l'ancienne assertion de présence a été inversée). ✅

### F4 — assouplissement du blocage IF=8 / ICE=15 (application directe CDC §4.8)
- **Fait** : `ValiderPourExport` ne bloque plus sur une longueur exacte. Blocage conservé uniquement sur
  identifiant/ICE **vide** (après trim) ou contenant un **espace/tabulation résiduel**. Les identifiants sont
  `Trim()`és avant appel depuis l'exporter.
- **Avant** : `if (!EstIfValide(...)) throw ...` (exige exactement 8 / 15 caractères).
- **Après** : blocage sur `IsNullOrWhiteSpace(...) || ContientEspace(...)` uniquement.
- **Note importante** : ce changement est l'**application directe de la recommandation du CDC §4.8**
  (« Ne pas bloquer sur une longueur fixe sans confirmation du format exact attendu par le fisc »), pas un
  arbitrage technique du worker. `EstIfValide`/`EstIceValide`/`Evaluer` (indicateurs de conformité affichés
  côté front) restent **inchangés** (toujours basés sur 8/15) — seul le blocage bloquant à l'export a été assoupli.
- **Test** : `GenererXml_IfSeptChiffresEtIceNonStandard_NeLancePas` (exporter) et
  `ValiderPourExport_IfSeptChiffresEtIceNonStandard_NeLancePas` (core) → IF `1084334` (7 chiffres) accepté.
  Tests de blocage conservés sur vide/espace. ✅

### F5 — arrondi : plus de format `"0.00"` forcé à l'export
- **Fait** : nouveau `FormatDecimal(decimal) => value.ToString("0.##########", CultureInfo.InvariantCulture)`
  appliqué à `mht`/`tva`/`ttc`/`tx`. La valeur decimal est écrite telle qu'elle arrive (déjà arrondie en amont
  par `Ventilateur`, **non modifié**), point décimal `.` en culture invariante, **sans padding de zéros**.
- **Avant** : `1000.50` / `<ttc>1299.00</ttc>`
- **Après** : `1000.5` / `<ttc>1299</ttc>` (entier sans décimale)
- **Test** : `GenererXml_MontantEntier_SansDecimale` (`1299` et non `1299.00`) + assertions `1082.5`, `216.5`. ✅

### F6 — `Trim()` sur champs texte
- **Fait** : `.Trim()` appliqué sur `NumeroFacture`, `Designation`, `Tiers.IdentifiantFiscal`, `Tiers.Nom`,
  `Tiers.Ice` avant écriture (dans l'exporter, pas dans le modèle).
- **Avant** : `<num>  FACZ001 </num>`
- **Après** : `<num>FACZ001</num>`
- **Test** : `GenererXml_EspaceResiduel_EstTrimme`. ✅

### Cas complémentaires ajoutés
- **Avoir (montants négatifs)** : `GenererXml_Avoir_MontantsNegatifsPropagesSansTransformation` →
  `<mht>-15840</mht>`, `<tva>-3168</tva>`, `<ttc>-19008</ttc>`, `<tx>0.2</tx>` (taux positif). ✅
- **Raison sociale avec `&`** : `GenererXml_RaisonSocialeAvecEsperluette_EchappeeCorrectement` →
  `<nom>A &amp; B SARL</nom>` + validation `XDocument.Parse` (XML bien formé). ✅

---

## Extrait du XML généré (échantillon réel, 2 lignes)
```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<DeclarationReleveDeduction xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
<identifiantFiscal>25018370</identifiantFiscal>
<annee>2025</annee>
<periode>3</periode>
<regime>1</regime>
<releveDeductions>
<rd>
<ord>1</ord>
<num>FACZ00048600</num>            <!-- trimé (source "  FACZ00048600 ") -->
<des>IMMOBILISATION</des>
<mht>1082.5</mht>
<tva>216.5</tva>
<ttc>1299</ttc>                    <!-- entier sans .00 -->
<refF><if>1084334</if><nom>KITEA &amp; CO</nom><ice>001544256000053</ice></refF>  <!-- IF 7 chiffres OK, & échappé -->
<tx>0.2</tx>                       <!-- fraction décimale -->
<mp><id>1</id></mp>               <!-- pas de <prorata> -->
<dpai>2025-03-09</dpai>
<dfac>2025-03-09</dfac>
</rd>
<rd>
<ord>2</ord>
<num>AV2404269</num>
<des>Marchandises</des>
<mht>-15840</mht>                  <!-- avoir: signe négatif propagé tel quel -->
<tva>-3168</tva>
<ttc>-19008</ttc>
<refF><if>12345678</if><nom>ACME</nom><ice>123456789012345</ice></refF>
<tx>0.2</tx>
<mp><id>2</id></mp>
<dpai>2025-03-10</dpai>
<dfac>2025-03-10</dfac>
</rd>
</releveDeductions>
</DeclarationReleveDeduction>
```
Vérifié à l'œil : prolog ✓ · xmlns:xsi ✓ · absence de `<prorata>` ✓ · `tx` en fraction décimale ✓ ·
absence de `.00` sur un entier (`1299`) ✓ · `&` échappé ✓ · Trim ✓ · signes négatifs de l'avoir intacts ✓.

---

## Build & tests

### Build (solution complète)
`dotnet build DeclarationTVA.slnx` → **0 erreur** (25 avertissements préexistants, non introduits par la task).

### Tests
| Projet | Résultat |
|---|---|
| `Declaration.Export.Xml.Tests` | ✅ 13/13 réussis, 0 échec |
| `Declaration.Core.Tests` | ✅ 34/34 réussis, 0 échec |
| `Declaration.Selection.Tests` | ✅ 59/59 |
| `Declaration.Orchestration.Tests` | ✅ 137/137 |
| `Declaration.Export.Excel.Tests` | ✅ 1/1 |
| `Declaration.Controle.Tests` | ⚠️ 1 échec **préexistant, hors périmètre** |

**Précision sur `Declaration.Controle.Tests`** : le test `ComparateurTests.GenererRapportVerification`
échoue avec `System.Exception : Déclaration GRFN 66 introuvable`. C'est un **test d'intégration base de
données** (ouvre une `SqlConnection` réelle et interroge `DT_Id 66`, cf. `ComparateurTests.cs:28,45`).
Il n'a **aucun lien** avec les fichiers modifiés (export XML / validation) — échec environnemental
(pas de base disponible), **non introduit par cette task**.

---

## ⚠️ Points restant à faire confirmer par le PO / fiscaliste avant tout dépôt réel en production

1. **Format exact final attendu pour IF / ICE (CDC §5.2)** — le blocage sur longueur fixe (8/15) a été
   **assoupli par défaut** ici, en application directe de la recommandation CDC §4.8 (le fichier réellement
   accepté contient des IF à 7 chiffres). Ce n'est **pas figé** : la longueur exacte attendue (fixe ou
   variable), l'éventuel padding de zéros à gauche, et la tolérance sur l'ICE doivent être **confirmés par
   le fiscaliste** avant clôture définitive. Si un format de longueur strict s'avère finalement requis, le
   contrôle devra être re-durci.

2. **Arrondi / précision définitifs des montants (CDC §5.1)** — le formatage `"0.00"` forcé à l'écriture a
   été **retiré** (défaut recommandé par le CDC : ne pas re-arrondir à l'export, préserver la précision).
   **Cependant `Ventilateur.cs` (amont) n'a pas été touché** : il applique déjà un `Math.Round(..., n,
   AwayFromZero)` lors de la répartition proportionnelle. La question plus large — *`Ventilateur` doit-il
   cesser d'arrondir en amont pour préserver la pleine précision (~15 chiffres) constatée dans le fichier
   de référence DGI ?* — reste un **point ouvert distinct, hors périmètre de cette task**, à trancher
   séparément par le PO/contrôle de gestion.

---

## Note
Conformément au rôle : aucun écriture dans `TODO.md`, aucun déplacement de fichier TASK — cela relève de
l'architecte à la revue.
