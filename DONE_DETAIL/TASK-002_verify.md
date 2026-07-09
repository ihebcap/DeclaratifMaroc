# Vérification TASK-002 : Lecture facture et extraction taxes OM

## Résumé de l'exécution
L'application autonome `SageTaxReader.Console.exe` a été exécutée avec succès sur la base **`DISTRI_DEMO`**.
La connexion a pu être établie avec l'utilisateur `<Administrateur>` et le mot de passe `P@ssw0rd`.

Deux factures cibles complexes (multi-taux) ont été testées :
- **25FA01371** (Facture Vente)
- **G0110** (Facture Achat)

## Sortie console (Output)

```text
Vérification de la disponibilité de la base...
--------------------------------------------------
DO_Piece: 25FA01371 | Type: 6 | Sens: Vente
--------------------------------------------------
Taxe: FODEC (TaxeTypeTPHT) | Taux: 1% | BaseHT: 67,00 | MontantTVA: 0,67 | TTC: 67,67
Taxe: 3 (TaxeTypeTVAEncaiss) | Taux: 14% | BaseHT: 460,76 | MontantTVA: 64,51 | TTC: 525,27
Taxe: 1 (TaxeTypeTVAEncaiss) | Taux: 20% | BaseHT: 25217,59 | MontantTVA: 5043,52 | TTC: 30261,11
Taxe: TF (TaxeTypeTPTTC) | Taux: 0,25% | BaseHT: 168,05 | MontantTVA: 0,42 | TTC: 168,47
--------------------------------------------------
Totaux OM  -> Total HT document: 25947,05 (dont Frais: 10,00)
Escompte   -> 259,47
Total HT Net -> 25687,58
TVA Réelle -> 5108,03
Parafiscale-> 1,09
Arrondi    -> 0,00
Total TTC  -> 30796,70
[OK] L'écart est entièrement décomposé et justifié.
JSON Dump:
{
  "NumeroPiece": "25FA01371",
  "TypeDocument": 6,
  "Sens": "Vente",
  "TotalHT": 25947.049999999999,
  "TotalTva": 5108.0299999999997,
  "TotalParafiscale": 1.0900000000000001,
  "TotalTtc": 30796.700000000001,
  "Escompte": 259.47000000000003,
  "Frais": 10,
  "Acompte": 0,
  "TotalHTNet": 25687.580000000002,
  "EcartArrondi": 0,
  "LignesTaxe": [ ... ]
}

--------------------------------------------------
DO_Piece: G0110 | Type: 16 | Sens: Achat
--------------------------------------------------
Taxe: 2 (TaxeTypeTVAEncaiss) | Taux: 20% | BaseHT: 53,53 | MontantTVA: 10,71 | TTC: 64,24
Taxe: 02 (TaxeTypeTVAEncaiss) | Taux: 10% | BaseHT: 12,66 | MontantTVA: 1,27 | TTC: 13,93
Taxe: 4 (TaxeTypeTVADebit) | Taux: 7% | BaseHT: 5,85 | MontantTVA: 0,41 | TTC: 6,26
Taxe: TTN (TaxeTypeTPTTC) | Taux: 0% | BaseHT: 0,00 | MontantTVA: 1,00 | TTC: 1,00
--------------------------------------------------
Totaux OM  -> Total HT document: 609,07 (dont Frais: 555,00)
Escompte   -> 6,09
Total HT Net -> 602,98
TVA Réelle -> 12,39
Parafiscale-> 1,00
Arrondi    -> -0,37
Total TTC  -> 616,00
[ATTENTION] Écart d'arrondi détecté : -0,37
JSON Dump:
{
  "NumeroPiece": "G0110",
  "TypeDocument": 16,
  "Sens": "Achat",
  "TotalHT": 609.07000000000005,
  "TotalTva": 12.390000000000001,
  "TotalParafiscale": 1,
  "TotalTtc": 616,
  "Escompte": 6.0899999999999999,
  "Frais": 555,
  "Acompte": 0,
  "TotalHTNet": 602.98000000000002,
  "EcartArrondi": -0.37,
  "LignesTaxe": [ ... ]
}
```

## Analyse du contrôle de cohérence & Limitations levées

Les factures `25FA01371` et `G0110` fournies ont permis de **prouver** la robustesse de l'extraction sur des cas complexes (pièges) identifiés dans le cahier des charges :
1. **Cas Multi-taux prouvé** : La facture Vente extrait simultanément les taux 20%, 14%, 1% et 0,25%. La facture Achat extrait 20%, 10%, 7% et 0%.
2. **Taxe à montant 0 / Fixe prouvée** : La taxe `TTN` sur la facture `G0110` a bien un taux de 0% (TaxeTypeTPTTC) et une BaseHT de 0, avec pourtant un `MontantTVA` fixe de 1,00. L'objet métier a extrait cela parfaitement sans planter.
3. **Cas Para/TTC prouvé** : Les taxes `FODEC` (TaxeTypeTPHT) et `TF` (TaxeTypeTPTTC) démontrent la capacité à lire les taxes parafiscales correctement.
4. L'Écart entre `Σ(BaseHT) + Σ(TVA)` et `Total TTC`

**Observation :** 
Le contrôle d'équilibre `HT + TVA = TTC` n'est pas un invariant absolu. Les écarts relevés ne sont pas de simples "erreurs" ou des saisies "volontaires" arbitraires, mais proviennent d'éléments de facturation additionnels qui affectent le TTC sans toujours figurer dans le HT des lignes :
1. **Escompte** (ex: `25FA01371`) : Un escompte global vient réduire la base taxable et le TTC final.
2. **Frais de port / d'emballage** (ex: `G0110`) : Les frais (stockés dans `DO_ValFrais`) s'ajoutent au TTC mais ne sont pas dans le `DO_TotalHT` des lignes.
3. **Multiplicité des taxes sur une même base** : Dans `G0110`, la somme des bases de taxes (`72,04`) dépasse le HT des lignes hors frais (`54,07`) car certaines lignes subissent plusieurs taxes cumulées (ex: 20% + 10% sur la Ligne 1, 20% + 7% sur la Ligne 3). Le vrai `DO_TotalHT` du document (incluant les frais de port de 555,00) est bien de `609,07`.

**Décomposition chiffrée des écarts :**

L'écart n'a rien de "volontaire", c'est une combinaison mathématique parfaite de frais non listés dans le HT des articles, d'escompte, et d'arrondi ou taxes parafiscales.

*   **Facture `G0110` (Achat, Type = 16/Facture)** :
    *   **Total HT document** : 609,07 (inclut 555,00 de frais de port HT)
    *   **Escompte** : -6,09 (1% sur la base 609,07)
    *   **Total HT Net** : 602,98
    *   **TVA Réelle** : 12,39 (20% sur 53,53 + 10% sur 12,66 + 7% sur 5,85)
    *   **Parafiscale** : 1,00 (Timbre TTN)
    *   **Total Théorique (Valorisation)** : 616,37
    *   **Écart d'arrondi** : -0,37
    *   **Total TTC document** : 616,00
    L'outil extrait désormais ces totaux correctement (DO_TotalHT, DO_TotalTTC, escompte, frais, TVA séparée de Parafiscale) et montre la décomposition complète de l'écart.

*   **Facture `25FA01371` (Vente, Type = 6/Facture)** : 
    *   **Total HT document** : 25947,05 (inclut 10,00 de frais HT)
    *   **Escompte** : -259,47
    *   **Total HT Net** : 25687,58
    *   **TVA Réelle** : 5108,03
    *   **Parafiscale** : 1,09 (FODEC + TF)
    *   **Écart d'arrondi** : 0,00
    *   **Total TTC document** : 30796,70
    Ici l'écart est entièrement décomposé avec un arrondi nul.

**Acquis REX :**
L'équation `Σbase + Σtva = TTC` n'est pas un invariant. Le TTC se construit avec des éléments comme l'escompte, les frais non taxés, les arrondis et les taxes parafiscales. Le dénominateur de la proratisation (CDC §5) doit bien utiliser le `DO_TotalTTC` complet, mais l'extraction des taxes doit exposer le vrai HT document (`DO_TotalHT` + `DO_ValFrais`) ainsi que l'Escompte pour justifier la valorisation sans fausser les calculs avals.

**⚠️ Limitation déclarée (Sondage Comptabilisée)**
Le sondage de repli vers la "Facture Comptabilisée" (Type 7 pour Vente, Type 17 pour Achat) est implémenté dans le code (sondage `6 -> 7` / `16 -> 17`), mais **n'a pas été exercé** lors de ce test. Les deux factures `25FA01371` et `G0110` se sont avérées être des factures normales (Type 6 et 16). Aucune facture comptabilisée n'a été rencontrée/testée dans la base `DISTRI_DEMO`.
## Justification des APIs utilisées

Les objets et énumérations utilisés ont été sourcés et prouvés via la **documentation officielle** (cf. REX pour le détail des numéros de pages) :
1. **Lecture Vente** : `session.FactoryDocumentVente.ReadPiece(DocumentType.DocumentTypeVenteFacture, ...)`
2. **Lecture Achat** : `session.FactoryDocumentAchat` (Doc p.200) et enum `DocumentTypeAchatFacture` (Doc p.152).
3. **Extraction Taxes** : Interface `IBODocument3` et objet `IDocValorisation` (Doc p.351). Les taxes sont lues via `Valorisation.Taxes` `IDocValoTaxes` et objets `IDocValoTaxe` (Doc p.352). On lit ainsi `TaxeBase` et `TaxeMontant` sans aucune redéfinition.
