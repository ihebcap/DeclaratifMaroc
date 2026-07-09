# Vérification TASK-003 : Nombre de décimales d'arrondi (devise société)

## 1. Valeur `n` obtenue pour `DISTRI_DEMO`
- **Devise société** : DIRHAMS
- **Nombre de décimales (`n`)** : 2

## 2. Source retenue
- **Méthode** : Objets Métier Sage (OM) - Priorité 1.
- **Raison** : La devise de tenue de compte est correctement exposée par l'OM et accessible sans nécessiter de connexion SQL.
- **Objet/Propriété exacts** :
  - `CptaApplication.FactoryDossier.List[1]` (accès au dossier de l'application comptable, cf. guide OM page 256).
  - Propriété `IBPDossier2.DeviseCompte` (cf. guide OM page 289).
  - Propriété `IBPDevise2.D_Format` (cf. guide OM page 287).
- **Fiabilisation (Parsing du format)** : Le parseur de la propriété `D_Format` (ex. `# ##0,00` ou `#,##0.000`) détecte dynamiquement le dernier séparateur de décimale (virgule ou point) en s'assurant qu'il ne s'agit pas d'un séparateur de milliers. Si la chaîne après le séparateur contient le caractère `#` (ex. `#,##0`), il s'agit d'un séparateur de milliers et non de décimales, et `n` vaut 0. Sinon, `n` est égal à la longueur de la partie fractionnaire (nombre de zéros).

## 3. Vérification de cohérence TTC
Les calculs d'arrondis ont été modifiés dans le cœur `SageTaxReaderService` pour utiliser `n` lu dynamiquement depuis l'OM et le mode `AwayFromZero` :
```csharp
Math.Round(valoTaxe.BaseCalcul + valoTaxe.Montant, decimales, MidpointRounding.AwayFromZero)
```
Lors de l'exécution sur la facture de vente `25FA01371` et d'achat `G0110`, les totaux restent parfaitement cohérents et identiques à l'arrondi qui était codé en dur, ce qui confirme que `n=2` est correctement appliqué pour la base `DISTRI_DEMO`.
- L'escompte, le HT, la TVA, le total TTC et les écarts d'arrondis sont identiques à ceux du TASK-002, prouvant que la refactorisation dynamique n'introduit aucune régression.
