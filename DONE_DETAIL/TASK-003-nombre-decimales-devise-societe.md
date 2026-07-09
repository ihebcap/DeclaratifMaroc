# TASK-003 — Nombre de décimales d'arrondi (devise société) depuis Sage

## Contexte
Le calcul TVA arrondit les montants au **nombre de décimales de la devise société** (`n`, cf. `CAHIER_DES_CHARGES.md` §5, `MODULE_DECLARATION_TVA.md`). Indice trouvé dans l'ancien module (GRFN) :
```
TTC = Math.Round(valoTaxe.BaseCalcul + valoTaxe.Montant, 2)
```
⚠️ Le `2` est **codé en dur** → viole la règle multi-client (une société en devise à 3 décimales, ex. TND/KWD, serait fausse). On ne réutilise **rien** de GRFN : cette formule n'est qu'un **indice** sur la règle métier (TTC d'un taux = base + montant, arrondi à `n`). Le nombre `n` doit être **lu**, par société.

## Objectif
Déterminer, pour la société courante, le **nombre de décimales de sa devise** (`n`), à passer au calcul TVA à la place de toute constante. Fournir une petite fonction/lecteur réutilisable renvoyant `n` + la devise société.

## Règle de dépendance
- **Priorité 1 — Objets Métier Sage** (si la doc officielle l'expose) : garde le cœur du worker (TASK-002) dépendant **uniquement** de `Objets100cLib`, donc portable vers `.sage100-connector`.
- **Priorité 2 — base Sage en lecture directe** (repli) : lire la table des devises Sage. Documentation de structure disponible : `D:\_vibe\objetmetiers\V1210_Sage 100_Structure des fichiers.pdf` (+ `V11_...` / `V12_...`).
- **Source de vérité = doc officielle Sage** (OM `sage 100c objets métiers.pdf` + structure des fichiers). Aucune réutilisation de code/DLL GRFN/GOCOM.

## Étapes
1. **Identifier la devise société** : le paramètre société Sage porte la **devise de tenue de compte** (devise société). Trouver l'accès :
   - via OM : objet société / application (compta ou commerciale) exposant la devise société — **nom exact à confirmer** dans la doc OM ;
   - à défaut, via base Sage : paramètre société → code devise société.
2. **Lire le nombre de décimales de cette devise** :
   - via OM : propriété « nombre de décimales » de l'objet devise (**nom exact à confirmer** doc OM) ;
   - à défaut, via base Sage : table des devises, colonne « nombre de décimales » (**nom exact à confirmer** dans le PDF structure des fichiers v12.10).
3. **Exposer** `n` (int) + code/désignation devise via une fonction du cœur réutilisable (TASK-002). Aucune valeur en dur.
4. **Contrôle** : sur `DISTRI_DEMO`, afficher devise société + `n` et vérifier que l'arrondi `Math.Round(base + montant, n, AwayFromZero)` par taux redonne le TTC document (cohérence avec le contrôle de TASK-002).

## Livrables
- Fonction/lecteur `n = f(société)` intégré au cœur réutilisable de TASK-002 (OM si possible ; sinon lecteur SQL Sage isolé et documenté comme repli).
- `VERIFY/TASK-003_verify.md` : valeur `n` obtenue pour `DISTRI_DEMO`, la source retenue (OM ou table Sage) avec **objet/colonne exact + page doc officielle**, et la vérif de cohérence TTC.

## Critères de validation
- `n` **lu** dynamiquement (jamais une constante `2`).
- Source justifiée par la doc officielle Sage (OM ou structure des fichiers) — pas par GRFN.
- Cohérent avec la règle de calcul du CDC (§5 : `AwayFromZero`, `n` = décimales devise société).

## Risques / dépendances
- **Lié à TASK-002** : `n` alimente l'extraction/contrôle des taxes. À développer ensemble ou juste après.
- Distinguer **devise de tenue de compte** (société) d'une éventuelle **devise document** (facture en devise étrangère) : ici on veut la **devise société** pour la déclaration. Signaler si une facture est en devise ≠ société (impact à traiter plus tard).
- Si OM n'expose pas proprement le nombre de décimales → repli base Sage (ajoute une dépendance SQL au cœur, à cloisonner pour ne pas polluer la portabilité OM).
