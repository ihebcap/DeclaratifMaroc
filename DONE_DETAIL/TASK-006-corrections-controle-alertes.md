# TASK-006 — Corrections revue TASK-004/005 (contrôle, alerte IF, portage prorata/désignation)

## Contexte
La revue de TASK-004/005 (code livré, 9/9 tests verts) a relevé trois défauts à corriger **avant** de construire les briques aval :
1. **Contrôle d'équilibre tautologique** ([ConstructeurDeclaration.cs:131-138](../Declaration.Core/ConstructeurDeclaration.cs:131)) : `TotalSourcesTtc` est la somme des mêmes lignes que `TotalDeclarationTtc` regroupées par source → écart **toujours nul par construction**. Un contrôle qui ne peut pas échouer = fausse assurance avant dépôt (exactement ce que le client veut éviter, §1sexies).
2. **Identifiant fiscal manquant = silencieux** ([l.158](../Declaration.Core/ConstructeurDeclaration.cs:158)) : l'IF n'est validé que s'il est non vide → un tiers **sans IF** ne lève aucune alerte, alors que l'ICE manquant en lève une. Incohérent ; IF obligatoire au Maroc.
3. **`Prorata` et `Designation` perdus à l'enrichissement** : `LigneDeclarationEnrichie` ne porte pas le `Prorata` (calculé par le Ventilateur puis jeté) ni de désignation. Or l'export XML Simpl-TVA (#7) exige `<prorata>` et `<des>` par ligne.

## Périmètre STRICT
- **Uniquement** ces trois corrections dans `Declaration.Core` + mise à jour des tests.
- **Exclu** : toute nouvelle fonctionnalité, refonte, ou brique aval.

## Objectif
1. **Redéfinir le contrôle d'équilibre en contrôle réel** (décision PO : décomposition du résidu). `ControleEquilibre` expose :
   - `TotalMontantAffecte` = Σ des `MontantAffecte` des affectations retenues,
   - `TotalDeclareTtc` = Σ des `(assiette + tva)` déclarés,
   - `ResiduNonTva` = `TotalMontantAffecte − TotalDeclareTtc` (part payée hors TVA déductible : parafiscal, exonéré, effet escompte),
   - `ResiduExplique` (si décomposable via le DTO : Σ parafiscal + escompte des factures concernées) et `ResiduInexplique = ResiduNonTva − ResiduExplique`.
   - **Alerte** `EQUILIBRE_RESIDU_INEXPLIQUE` si `|ResiduInexplique| > tolérance` (tolérance = quelques centimes × nb lignes).
   > Le contrôle **d'équilibre définitif** (vs résultat attendu) reste le **Mode Contrôle vs GRFN** (TASK-009) ; ici on supprime le faux zéro et on expose des chiffres honnêtes.
2. **Alerte IF manquant** : ajouter `TIERS_SANS_IF` (Error) quand `IdentifiantFiscal` est vide, symétrique de `TIERS_SANS_ICE`.
3. **Porter `Prorata` sur `LigneDeclarationEnrichie`** (depuis la ligne ventilée) et **ajouter un champ `Designation`** (alimenté vide pour l'instant, avec TODO explicite : source = worker OM, à exposer plus tard — cf. TASK-002/DTO).

## Contraintes techniques
- `net10.0`, pur, `decimal` de bout en bout (aligné TASK-004/005).
- Corrections **chirurgicales** : ne pas remanier la signature publique au-delà du nécessaire (ajout de champs OK, pas de renommage cosmétique).
- Espaces : remplacer `Contains(" ")` par un test couvrant **tout blanc** (`Any(char.IsWhiteSpace)`) pour IF et ICE.

## Étapes
1. Redéfinir `ControleEquilibre` (modèle + calcul dans `ConstruireDeclaration`) selon l'objectif 1 + alerte résidu inexpliqué.
2. Ajouter `TIERS_SANS_IF` dans `ValiderAffectation` ; corriger le test des espaces (IF + ICE).
3. Ajouter `Prorata` + `Designation` à `LigneDeclarationEnrichie` et les propager depuis la ventilation.
4. **Tests** : remplacer l'assertion « écart toujours 0 » par des cas où le **résidu est non nul et attendu** (facture avec parafiscal/escompte) et un cas de **résidu inexpliqué → alerte**. Ajouter un cas **tiers sans IF → `TIERS_SANS_IF`**. Vérifier `Prorata` présent sur les lignes enrichies.

## Livrables
- `Declaration.Core` corrigé (contrôle réel, alerte IF, prorata/désignation portés).
- Tests mis à jour, tous verts.
- `VERIFY/TASK-006_verify.md` : avant/après du contrôle (chiffres montrant qu'un résidu non nul est bien exposé, plus de faux zéro), et les nouvelles alertes déclenchées.

## Critères de validation
- Le contrôle d'équilibre peut **produire un écart/résidu non nul** sur une facture à parafiscal/escompte (plus de tautologie).
- `TIERS_SANS_IF` se déclenche sur un tiers sans identifiant fiscal.
- `Prorata` présent sur chaque `LigneDeclarationEnrichie` ; champ `Designation` existant (même si vide).
- Tests verts ; aucune régression sur la ventilation (TASK-004).

## Risques / dépendances
- **Non bloqué** (pur, fixtures). À faire **en premier** : les briques aval (Excel #6, XML #7, Mode Contrôle #9) s'appuient sur ce modèle corrigé.
- La décomposition `ResiduExplique` dépend des champs déjà présents dans le DTO (`TotalParafiscale`, `Escompte`) → disponible sans toucher au worker.
