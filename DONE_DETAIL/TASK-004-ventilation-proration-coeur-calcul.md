# TASK-004 — Ventilation / proratisation : cœur de calcul pur

## Contexte
Le worker OM (TASK-002) sait extraire, pour une facture, son **détail TVA exact** (`DocumentTaxesInfo` / `TaxeDetail` : base HT / taux / montant TVA / TTC par taux, + `TotalTtc`, escompte, frais). Il reste la brique qui **consomme** ce détail pour produire les **lignes de déclaration** : répartir le **montant payé (affecté)** d'un règlement sur les **taux de TVA de la facture**, au prorata, avec l'arrondi correct.

C'est la brique où vivent les bugs identifiés de l'ancien module GRFN (`MODULE_DECLARATION_TVA.md` §2) :
- **Bug 1** — prorata via `ratio = round(TTC/affecté, 6)` puis `assiette = base/ratio` → dérive de centimes sur paiement partiel.
- **Bug 2** — `Math.Round` en arrondi bancaire (ToEven) au lieu de `AwayFromZero` attendu par la DGI.
- **Bug 3** — assiette non arrondie alors que la TVA l'est → base/TVA/TTC incohérents.

Cette task les corrige **par construction**.

### Périmètre STRICT de cette task
- **Uniquement** : le **calcul pur** — entrée = `DocumentTaxesInfo` + montant affecté + `n` (décimales devise) ; sortie = lignes de déclaration ventilées par taux.
- **Exclu** : lecture Sage (fait par TASK-002), sélection SQL des règlements/affectations, cache, export Excel/XML, UI. Viennent après.

### Positionnement / architecture (à appliquer dès maintenant)
- Ce cœur est **pur** : aucune référence Sage/COM, aucun SQL, aucune I/O. Il ne connaît que des **DTO**. → **testable sans base vivante**, avec les 2 factures de TASK-002 en fixtures.
- **Prérequis d'archi** : le DTO frontière (`DocumentTaxesInfo` / `TaxeDetail`) vit aujourd'hui dans `SageTaxReader.Core`, cloué à `net48` + `x86` + `Interop.Objets100cLib`. **Extraire ces DTO dans un projet neutre** (`SageTaxReader.Contracts`, `netstandard2.0`, zéro dépendance), référencé à la fois par le worker (qui le remplit) et par ce cœur (qui le consomme). Sans quoi le cœur hérite du couplage Sage par la bande.
- Cœur **agnostique au sens** (achat/vente) et **au signe** : il calcule la valorisation par taux. Le signe (déductible fournisseur = négatif) et le domaine (décaissement / encaissement / dépense) sont appliqués par l'**orchestration** appelante, pas ici. (Cohérent avec le principe acté : le worker/cœur restitue, l'appli classe.)

## Règle de dépendance
- **Aucune** dépendance Sage/COM/SQL dans ce projet. Uniquement `SageTaxReader.Contracts` + BCL.
- **Ne rien réutiliser de GRFN** : la formule de §2 n'est qu'un **indice** de la règle métier ; on ré-implémente proprement (prorata direct, `AwayFromZero`).
- Règle de calcul de référence : `CAHIER_DES_CHARGES.md` §5 + `MODULE_DECLARATION_TVA.md` §2.

## Objectif
Une bibliothèque de calcul pure exposant, pour **une facture** (`DocumentTaxesInfo`) et **un montant affecté** (part payée du règlement sur cette facture), la **ventilation par taux de TVA** :

```
Entrée : DocumentTaxesInfo facture, decimal montantAffecte, int n
Sortie : liste de LigneDeclaration { taux, assiette, tva, ttc } + prorata + total   (montants en decimal)
```

### Règles de calcul (corrigées)
Pour **chaque ligne de taxe de type TVA** (`Type` commençant par `TaxeTypeTVA`) — les **parafiscales** (`TPHT` / `TPTTC`) sont **exclues** de la déclaration TVA déductible :

```
proration = montantAffecte / facture.TotalTtc         // pleine précision, AUCUN arrondi intermédiaire (dénominateur = DO_TotalTTC complet, REX TASK-002)
assiette  = Round( HT_i  × proration , n, AwayFromZero )   // HT_i de Sage prorata puis arrondi (corrige bug 1 + 3)
tva       = Round( TVA_i × proration , n, AwayFromZero )   // ⚠️ TVA_i de Sage prorata puis arrondi — CDC §5
ttc_ligne = assiette + tva
```

- **⚠️ Prorater la `TVA_i` de Sage, NE PAS la recalculer** via `assiette × taux / 100`. Le CDC §5 (l.76-78) et le principe « montants = BO Sage, sans recalcul » (CDC §4/§9) imposent d'utiliser la `TVA_i` exacte lue par TASK-002, multipliée par la proration. Un recalcul par le taux jette la valeur Sage et peut diverger (et casse « zéro dérive » sur paiement total).
- **`denominateur = TotalTtc` complet** (escompte/frais/exonéré inclus), conformément au REX TASK-002 : c'est le TTC réellement payé qui sert de base au prorata. **Ne pas** re-soustraire l'escompte ici — les `base`/`TVA` des taxes OM sont déjà celles calculées par Sage (post-escompte).
- **Paiement total** (`montantAffecte = TotalTtc`, proration = 1) : `assiette = Round(HT_i, n)` / `tva = Round(TVA_i, n)` → redonne les valeurs Sage telles quelles (CDC §5 l.82) → zéro dérive.
- **`prorata` de la ligne** = `montantAffecte / TotalTtc` (× 100 pour le champ Simpl-TVA `<prorata>` si attendu en %). À exposer, ne pas coder en dur à 100.
- **Multi-taux** : une facture a N lignes TVA → N lignes de déclaration. Somme des assiettes ventilées ≤ HT payé, somme des TVA ≤ TVA facture.
- **Résidu d'arrondi — AUCUNE réconciliation** : les lignes par taux sont arrondies **indépendamment** (CDC §5), et la déclaration ne couvre que les **taux TVA** (parafiscales/frais/exonéré exclus). Donc `Σ(assiette+tva)` des lignes **n'égale pas** `montantAffecte`, et **c'est normal** : aucun ajustement/plug ne doit forcer l'égalité. Le contrôle d'équilibre du CDC §6 est au niveau **agrégé** (Σ sources), pas par affectation.
- `n` provient de **TASK-003** (décimales devise société) — **jamais** de constante `2`.

## Contraintes techniques
- **Cibles runtime** : `Declaration.Core` en **`net10.0`** (LTS actuelle, nov. 2025) ; `SageTaxReader.Contracts` en **`netstandard2.0`** (pont universel : référençable depuis le worker Sage comme depuis net10). **Aucune** référence Sage/COM/SQL. Buildable par `dotnet build` (pas de COMReference). Prérequis machine : SDK .NET 10 installé.
- Fonction **déterministe et pure** : mêmes entrées → même sortie, aucun effet de bord, aucune horloge/aléa.
- Arrondi **explicite** `MidpointRounding.AwayFromZero` partout, précision `n` paramétrée.
- **Arithmétique en `decimal` — obligatoire, pas optionnel.** Le DTO `Contracts` reste en `double` (réalité COM, on n'y touche pas), mais `Declaration.Core` **convertit `double → decimal` à l'entrée** et fait **tout** le calcul (proration pleine précision + arrondi) en `decimal`. Raison : `Math.Round(double, n, AwayFromZero)` est **non fiable au point milieu** (une valeur `x,xx5` stockée `x,xx4999…` — cf. `609.07000000000005` observé en TASK-002 — arrondit du mauvais côté) ; `AwayFromZero` n'a de sens qu'avec un point milieu exact. Sur un montant déposé à la DGI, c'est une **erreur de correction**, pas un détail — et c'est le bug 2 qu'on prétend corriger. Ce n'est **pas** une refonte : conversion à la frontière, `decimal` en interne, sortie en `decimal`.

## Étapes
1. **Extraire les DTO** `DocumentTaxesInfo` / `TaxeDetail` de `SageTaxReader.Core` vers un nouveau projet **`SageTaxReader.Contracts`** (`netstandard2.0`, zéro dépendance). Faire référencer ce projet par `SageTaxReader.Core` (le worker continue de remplir les mêmes types). **Aucun changement de comportement** du worker.
2. **Nouveau projet `Declaration.Core`** (calcul pur, **`net10.0`**) référençant **uniquement** `SageTaxReader.Contracts`.
3. **Ventilation** : implémenter `Ventiler(DocumentTaxesInfo facture, decimal montantAffecte, int n)` → `IReadOnlyList<LigneDeclaration>` selon les règles ci-dessus (filtre TVA, proration de `HT_i` **et** `TVA_i` Sage, `AwayFromZero`, assiette **et** TVA arrondies ; conversion `double→decimal` des champs DTO à l'entrée).
4. **Type de sortie** `LigneDeclaration { decimal Taux, Assiette, Tva, Ttc, Prorata; string CodeTaxe }` + un récap `{ Σassiette, Σtva, Σttc }`.
5. **Tests unitaires** (projet de test, sans Sage) avec les **fixtures = sorties réelles de TASK-002** (`25FA01371`, `G0110`) sérialisées en `DocumentTaxesInfo` :
   - **Cas paiement total** (`montantAffecte = TotalTtc`) : la ventilation doit **redonner** les assiettes/TVA par taux de la facture (à l'arrondi `n` près) → prouve la neutralité du prorata à 100 %.
   - **Cas paiement partiel** (ex. 50 %, 33,33 %) : vérifier **absence de dérive** — `Σ tva ventilée` cohérente, et que la formule directe ne reproduit pas l'écart du ratio arrondi à 6 déc. (comparer au calcul GRFN pour **documenter** l'écart corrigé).
   - **Multi-taux** : les 4 taux de `25FA01371` (20/14/1/0,25) et de `G0110` (20/10/7/0) — vérifier que **seules les lignes TVA** produisent des lignes de déclaration (parafiscales `FODEC`/`TF`/`TTN` exclues).

## Livrables
- **`SageTaxReader.Contracts`** : DTO neutres (`DocumentTaxesInfo`, `TaxeDetail`), référencé par le worker.
- **`Declaration.Core`** : bibliothèque de ventilation pure (`Ventiler(...)` + `LigneDeclaration`), zéro dépendance Sage.
- **Projet de tests** : fixtures des 2 factures + cas total / partiel / multi-taux ; tous verts sans base.
- `VERIFY/TASK-004_verify.md` : résultats des tests (assiette/TVA par taux, cas total et partiel), **démonstration chiffrée** que le prorata direct + `AwayFromZero` corrige la dérive vs l'ancienne formule (bug 1/2/3), et la parafiscale bien exclue.

## Critères de validation
- Build `dotnet build` OK (`Declaration.Core` en net10.0, `Contracts` en netstandard2.0) ; **aucune** référence Sage/COM/SQL dans `Declaration.Core` ni `Contracts`.
- Worker TASK-002 **toujours vert** après extraction des DTO (aucune régression de comportement).
- Cas total : ventilation **redonne les valeurs Sage** (`HT_i`/`TVA_i`) telles quelles à l'arrondi `n` près, par taux.
- Cas partiel : `assiette = Round(HT_i × proration, n)` **et** `tva = Round(TVA_i × proration, n)` — proration en **une** opération (pas de ratio arrondi intermédiaire), `AwayFromZero`.
- **TVA proratée depuis la `TVA_i` Sage, jamais recalculée** via `assiette × taux` (CDC §5 / « sans recalcul »).
- **Calcul en `decimal`** (conversion `double→decimal` à l'entrée) ; aucun arrondi de la proration avant les arrondis finaux `n`.
- Parafiscales (`TPHT`/`TPTTC`) **exclues** des lignes de déclaration TVA.
- `n` injecté (jamais `2` en dur) ; `prorata` calculé (jamais 100 en dur).

## Risques / dépendances
- **Prérequis TASK-003** : `n` (décimales devise société) est un **input** du calcul. Tant que TASK-003 n'est pas tranchée, les tests peuvent figer `n=2` **dans la fixture de test uniquement** (jamais dans le code), à remplacer dès que TASK-003 livre.
- **Prérequis étape 1** (extraction DTO) : léger risque de régression du worker — couvert par « worker toujours vert » ci-dessus.
- Ne dépend **pas** de la base prod ni de la brique Sélection SQL (bloquées) → **peut avancer immédiatement**. C'est justement le point de départ choisi pour ne pas rester bloqué.
- Calcul en `decimal` **tranché** (obligatoire, cf. Contraintes techniques) : le `double` reste seulement sur le DTO `Contracts` (frontière COM), converti en `decimal` à l'entrée du cœur.
- Escompte/frais : ne PAS les re-traiter ici (déjà intégrés dans les bases OM / le `TotalTtc`) — sinon double comptage. Vérifié par le cas « paiement total ».
