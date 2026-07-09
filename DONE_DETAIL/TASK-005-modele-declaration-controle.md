# TASK-005 — Modèle déclaration + contrôle (agrégation, alertes, équilibre)

## Contexte
TASK-004 produit, pour **une affectation** (une part payée sur une facture), ses **lignes ventilées par taux** `{ taux, assiette, tva, ttc, prorata }` — mais **sans métadonnées** (tiers, IF/ICE, dates, source, mode de paiement) et **une affectation à la fois**.

Une **déclaration** est l'agrégat de **toutes** les affectations à déclarer sur une période : chaque ligne ventilée **enrichie** de ses métadonnées, plus les **totaux de contrôle** et les **alertes avant dépôt**. C'est la matière que le client (§1sexies) veut pouvoir **contrôler avant dépôt**, et que les exports Excel (#6) et XML (#7) ne feront que **restituer**.

Cette task construit ce **modèle de déclaration + le contrôle**, en pur, testable sans base.

### Périmètre STRICT de cette task
- **Uniquement** : le **modèle déclaration** (lignes enrichies), l'**agrégation** (par source / taux / code activité), le **contrôle d'équilibre**, et les **alertes**.
- **Exclu** : rendu Excel (#6), XML (#7), Sélection SQL (#5), lecture Sage (TASK-002), UI. La task ne **produit aucun fichier** — juste un modèle en mémoire + son état de contrôle (vérifiable par tests / dump JSON).

### Positionnement / architecture
- **Pur** : dans `Declaration.Core` (`net10.0`), aucune référence Sage/COM/SQL, aucune I/O. Réutilise `Ventiler(...)` de TASK-004 — **ne recalcule pas** la ventilation.
- **Contrat d'entrée = future frontière de la Sélection SQL (#5).** Le modèle prend en entrée une **liste d'affectations à déclarer** (métadonnées) + le `DocumentTaxesInfo` résolu de chaque facture. C'est exactement ce que la brique #5 remplira plus tard (elle lit GRF) et ce que le worker OM fournit (le DTO). Concevoir ce contrat proprement **ici** = le point d'accroche de #5.
- **Pas de réconciliation d'arrondi** (aligné TASK-004) : les lignes restent arrondies indépendamment ; le contrôle d'équilibre est au niveau **agrégé** (§6 CDC), pas par ligne.

## Règle de dépendance
- Aucune dépendance Sage/COM/SQL. Uniquement `SageTaxReader.Contracts` + `Declaration.Core` (ventilation TASK-004) + BCL.
- Arithmétique **`decimal`** (aligné TASK-004).
- Règles de contrôle et d'alerte de référence : `CAHIER_DES_CHARGES.md` + `MODULE_DECLARATION_TVA.md` §1sexies (contenu du contrôle) et §1quater (sources : décaissement / espèce / dépense / frais bancaire).

## Objectif
Un service pur qui, à partir d'une **liste d'affectations à déclarer** + le détail facture de chacune + `n`, produit un objet **`Declaration`** :

```
Entrée : IEnumerable<AffectationADeclarer>, résolution facture (DocumentTaxesInfo par n°), int n
Sortie : Declaration { Lignes enrichies, Récaps (source/taux/activité), ContrôleEquilibre, Alertes }
```

### Modèle d'entrée (= contrat de la Sélection SQL #5)
`AffectationADeclarer` (métadonnées connues côté GRF, hors détail TVA) :
```
NumeroFacture, Sens (Achat|Vente), Source (Decaissement|Espece|Depense|FraisBancaire),
MontantAffecte (decimal), DatePaiement, DateFacture, ModePaiement (code Simpl-TVA 1/2/3/4/5/7),
Tiers { Numero, Nom, IdentifiantFiscal, Ice, CodeActivite }
```
Le **détail TVA** (`DocumentTaxesInfo`) est fourni à part (worker OM) — le modèle ne lit pas Sage.

### Traitement
Pour chaque affectation :
1. Résoudre son `DocumentTaxesInfo` (fourni). Si absent → **alerte « facture introuvable »**, ligne non ventilée.
2. `Ventiler(facture, MontantAffecte, n)` (TASK-004) → lignes par taux TVA.
3. **Enrichir** chaque ligne ventilée avec les métadonnées de l'affectation → `LigneDeclarationEnrichie { NumeroFacture, Designation, Tiers{Numero,Nom,IF,Ice}, CodeActivite, HT(=assiette), Taux, Tva, Ttc, ModePaiement, DatePaiement, DateFacture, Source }`.

### Agrégation (récaps)
- **Par source** : Σ HT / Σ TVA / Σ TTC pour décaissement, espèce, dépense, frais bancaire.
- **Par taux** : Σ par taux de TVA.
- **Par code activité** : Σ par code activité.

### Contrôle d'équilibre (CDC §6)
```
Total déclaration  ==  Σ décaissement + Σ espèce + Σ dépense + Σ frais bancaire
```
Exposer le total, la somme par source, et l'**écart** (doit être nul). Niveau **agrégé** uniquement.

### Alertes avant dépôt (CDC §1sexies)
Une liste `Alerte { Niveau, Code, Message, RefLigne }` couvrant **au minimum** :
- Tiers **sans ICE**.
- Identifiant fiscal **≠ 8 caractères** ou contenant des espaces.
- ICE **≠ 15 caractères** ou contenant des espaces.
- **Facture introuvable** (DTO non fourni).
- Règlement **rapproché non affecté** (affectation attendue absente) — si l'info est portée par l'entrée.
- **Ligne à 0** (assiette ou TVA nulle).

## Contraintes techniques
- `net10.0`, pur, déterministe (aucune horloge/aléa) ; `decimal` de bout en bout.
- Le modèle est **sérialisable JSON** (dump = preuve de contrôle avant qu'un Excel n'existe).
- Aucune valeur métier en dur (codes source, longueurs IF/ICE, `n`) : constantes nommées / paramètres.

## Étapes
1. **Types du contrat d'entrée** : `AffectationADeclarer` + `TiersInfo` dans `Declaration.Core` (ou un sous-espace `Declaration.Core.Model`).
2. **Types de sortie** : `LigneDeclarationEnrichie`, `RecapParSource/Taux/Activite`, `ControleEquilibre`, `Alerte`, et l'agrégat `Declaration`.
3. **Service** `ConstruireDeclaration(IEnumerable<AffectationADeclarer>, Func<string,DocumentTaxesInfo?> resoudreFacture, int n)` → `Declaration` : boucle, ventile (TASK-004), enrichit, agrège, contrôle, lève les alertes.
4. **Tests unitaires** (sans Sage), fixtures = factures TASK-002 (`25FA01371`, `G0110`) + affectations fabriquées couvrant :
   - Plusieurs **sources** (décaissement + espèce + dépense) → vérifier `Σ sources = total`.
   - **Multi-taux** et **paiement partiel** (réutilise les cas TASK-004).
   - **Alertes déclenchées** : un tiers sans ICE, un IF de 7 car., un ICE de 14 car., une facture « introuvable », une ligne à 0.
   - Récaps par taux et par code activité corrects.

## Livrables
- `Declaration.Core` enrichi : modèle `Declaration` + service `ConstruireDeclaration(...)`, pur, `net10.0`.
- Projet de tests : fixtures + cas multi-sources / multi-taux / partiel / alertes ; tous verts sans base.
- `VERIFY/TASK-005_verify.md` : dump JSON d'une `Declaration` de démonstration (lignes + récaps + contrôle équilibre à écart nul + alertes listées), prouvant que le contrôle est **fonctionnel avant tout export**.

## Critères de validation
- Build `dotnet build` OK ; **aucune** référence Sage/COM/SQL dans `Declaration.Core`.
- Réutilise `Ventiler(...)` de TASK-004 (aucun recalcul de ventilation).
- `Σ(sources) == total` avec écart nul sur les fixtures ; récaps par taux / activité justes.
- Les 6 familles d'alertes se déclenchent sur les cas de test dédiés.
- `decimal` de bout en bout ; aucune constante métier en dur.
- Modèle sérialisable JSON (contrôle vérifiable sans Excel).

## Risques / dépendances
- **Prérequis TASK-004** (ventilation) et **TASK-003** (`n`). Peut démarrer dès que TASK-004 expose `Ventiler(...)`.
- **`CodeActivite`** : attribut tiers Sage — sa **source** (OM vs SQL) est un problème d'**adaptateur** (côté #5 / enrichissement), **pas** de ce modèle pur. Ici c'est un champ d'entrée ; si absent → récap « (sans activité) » + alerte informative, pas de blocage.
- Le contrat `AffectationADeclarer` doit rester **stable** : c'est la frontière que la Sélection SQL (#5) implémentera. Le figer maintenant évite un remaniement quand #5 arrivera.
- **Non bloqué** par la base prod : fixtures suffisent. La validation bout-en-bout (vraies affectations) attendra #5.
