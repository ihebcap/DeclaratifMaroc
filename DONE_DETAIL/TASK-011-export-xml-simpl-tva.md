# TASK-011 — Export XML Simpl-TVA (relevé de déductions) + zip

## Contexte
Format de dépôt DGI = relevé de déductions **XML zippé**. L'algorithme **exact** de l'ancien module a été **extrait par décompilation** (ilspy) de `GRFWinform/Tresorerie.UIDeclarationTva.dll` → classe **`DeclarationTvaEncaissementFileGenerator.Generate(...)`**. **Décision PO : on refait la même chose** (le format est une **spec externe DGI**, pas de la logique métier → aucune violation de la règle anti-GRFN ; on reproduit un format, on ne réutilise ni DLL ni code). On corrige seulement les 2 défauts identifiés.

> Décompilation de référence : `scratchpad/UIDeclarationTva/Tresorerie.UIDeclarationTva.decompiled.cs` (classe l.14453). Ne pas la copier — la **réimplémenter** proprement à partir du `DeclarationModele`.

## Périmètre STRICT
- **Uniquement** : sérialiser une `DeclarationModele` (TASK-005/006) en XML Simpl-TVA **identique au format GRFN** + zip, avec validations Maroc.
- **Exclu** : calcul, lecture Sage/GRF, dépôt effectif, Excel (#6). Rendu pur, **après le front**.

## Positionnement / architecture
- Adaptateur **`Declaration.Export.Xml`** (`net10.0`) consommant `Declaration.Core`. Aucune dépendance Sage/SQL. Consomme le **`DeclarationModele`** (pas la table `RT_LigneDeclarationTva` — c'est notre pipeline qui produit les lignes).

## Algorithme GRFN à reproduire (extrait exact)
**Filtre & garde** : lignes `!IsReport` seulement ; refuser si aucune ligne / déclaration non clôturée / fichier déjà généré.

**En-tête** (⚠️ **aucune déclaration `<?xml?>`**, texte commence par `\r\n`) :
```
<DeclarationReleveDeduction>
<identifiantFiscal>{société.Identifiant}</identifiantFiscal>
<annee>{Exercice}</annee>
<periode>{Mensuelle ? MoisPeriode : TrimestrePeriode}</periode>
<regime>{Mensuelle ? 1 : 2}</regime>
<releveDeductions>
```
**Par ligne** `<rd>` (ordre des champs à respecter) :
```
<ord>   = compteur 1-based
<num>   = DocumentNumero
<des>   = DesignationDocument
<mht>   = Assiette      (séparateur ".")
<tva>   = Montant       (".")
<ttc>   = Assiette+Montant (".")
<refF><if>{TiersIdentifiant}</if><nom>{TiersIntitule}</nom><ice>{TiersIce}</ice></refF>
<tx>    = Taux
<prorata> = Prorata
<mp><id>{mode}</id></mp>
<dpai>  = DateMouvement  ("yyyy-MM-dd")
<dfac>  = DateDocument   ("yyyy-MM-dd")
```
**Mapping mode `<id>`** : Espèce=1, Chèque=2, Virement=4, Traite=5, Autre=7 ; **OperationBancaire=3** (écrase le type de règlement).

**Validation Maroc (bloquante, par ligne, avec n° de ligne)** : `TiersIdentifiant.Length == 8`, `TiersIce.Length == 15`, aucun **espace** dans l'un ou l'autre → sinon `ApplicationException`.

**Pied** : `</releveDeductions></DeclarationReleveDeduction>`.
**Fichier** : `{Numero}-{Exercice}-{M|T}{période}.xml` (UTF-8 sans BOM via `File.WriteAllText`) **+** un `.zip` du même nom contenant le xml ; refuser si l'un existe déjà.

## Corrections vs GRFN (les 2 seules divergences volontaires)
1. **Arrondi** : GRFN refait `Math.Round(x, nbDecimal)` en **`ToEven`** à l'export (bug 2 rejoué). Nos lignes du `DeclarationModele` sont **déjà arrondies** (`AwayFromZero`, `n`) par la ventilation. → **Ne PAS re-arrondir** ici (formatage seul avec `n` décimales) ; si un formatage à `n` est nécessaire, utiliser `AwayFromZero`. Jamais `ToEven`.
2. **Désignation** : `<des>` = `DesignationDocument`. Le DTO worker ne l'expose pas encore (champ `Designation` vide, cf. TASK-006). → à alimenter (source : worker OM) ; sinon `<des>` vide + alerte, ne pas bloquer.

## Contraintes techniques
- `net10.0`, pur ; `.` décimal via `NumberFormatInfo`/`InvariantCulture` ; dates `yyyy-MM-dd` ; **UTF-8 sans BOM** ; **pas de `<?xml?>`** (conforme GRFN — à confirmer si un jour un XML DGI réel montre le contraire).
- Lignes `IsReport=1` exclues (ajouter le champ `IsReport` au modèle si le report doit être géré ici — sinon documenter hors périmètre).
- Validation IF/ICE **bloquante** avant écriture (aligné alertes TASK-005/006).

## Étapes
1. Ajouter au modèle ce qui manque au regard de l'algo : `Designation` (déjà via TASK-006), en-tête déclaration (`Identifiant société`, `Exercice`, `Type`, `MoisPeriode`/`TrimestrePeriode`, `Numero`), et `IsReport` si géré.
2. Réimplémenter `Generate` : en-tête → boucle `<rd>` (ordre + mapping mode exact) → pied ; formatage `.`/dates/n sans re-arrondi ToEven.
3. Validation Maroc bloquante par ligne (message avec n° de ligne, comme GRFN).
4. Écriture `.xml` + `.zip` au nom normalisé ; refus si existe.
5. **Tests** : depuis une `DeclarationModele` fixture → comparer la sortie au format attendu (structure, ordre des champs, séparateurs, mapping mode) ; cas de rejet IF/ICE ; présence du zip.
6. **Validation finale (quand dispo)** : diff contre un **XML Simpl-TVA réellement déposé** (EMA a utilisé l'ancien module → récupérer un fichier réel) pour confirmer l'absence de `<?xml?>`, l'encodage et les détails de format.

## Livrables
- `Declaration.Export.Xml` : `GenererXml(DeclarationModele, enTete, cheminSortie)` + zip.
- Tests (fixtures + rejet validation).
- `VERIFY/TASK-011_verify.md` : XML d'exemple, nom de fichier, résultat validations, et diff vs XML réel si fourni.

## Critères de validation
- Format **identique** à `DeclarationTvaEncaissementFileGenerator` (structure, ordre des champs, mapping mode, nom fichier, zip) — hors les 2 corrections.
- `.` décimal ; dates `yyyy-MM-dd` ; UTF-8 sans BOM ; pas de `<?xml?>`.
- IF 8 / ICE 15 / sans espaces vérifiés → génération refusée sinon (avec n° de ligne).
- Lignes `IsReport` exclues.
- **Aucun re-arrondi ToEven** ; aucune dépendance Sage/SQL.

## Risques / dépendances
- **Non bloqué** (fixtures) mais **après le front** (contrôle = grilles TASK-013 ; l'XML est le fichier de dépôt).
- **Prérequis TASK-006** (prorata/désignation) + TASK-005 (modèle) + en-tête déclaration à modéliser.
- **Désignation** non exposée par le worker → à sourcer (sinon `<des>` vide documenté).
- **`<?xml?>` / encodage** : GRFN n'en met pas ; confirmer sur un XML DGI réel avant un dépôt en production (étape 6).
