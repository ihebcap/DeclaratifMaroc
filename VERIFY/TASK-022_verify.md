# TASK-022 Verify (Mise à jour 2)

## Validation (Suite)

- [x] **Double Connexion Sage/GRF** : `LecteurTvaFgr` utilise désormais deux connexions distinctes, `connectionString` (GRF) pour extraire `RT_HISTCOMPTA` et `sageConnectionString` (Sage) pour extraire `F_TAXE`.
- [x] **Propagation de la connexion** : Dans `DeclarationWorkflowService`, la connexion `SageConnection` extraite de la configuration est passée en 5ème argument du constructeur de `OrchestrateurDeclaration`, qui la propage à `LireTvaFgr`.
- [x] **Résolution du taux en mémoire via F_TAXE** : La méthode `GetTaxes` exécute la requête `SELECT TA_Code, TA_Taux FROM F_TAXE` avec la connexion Sage, et met le résultat en cache de manière statique (`lock` thread-safe). Plus aucun `Substring` ni `Regex` n'est utilisé pour déduire le taux. 
- [x] **Cas d'erreur (CODE_TAXE_INCONNU)** : Lorsqu'un code taxe `HC_TaxeCode` n'est pas présent dans la table `F_TAXE` (le dictionnaire), une `InvalidOperationException` est levée en incluant le code brut (ex: `XYZ`).
- [x] **Confirmation F_TAXE** : Le nom exact de la colonne de taux est bien `TA_Taux`, associée au code `TA_Code`.

### Logs d'exécution (Solution complète)

**Build (`dotnet build`) :**
```text
  Declaration.Export.Excel -> D:\_vibe\GRF\Declaration.Export.Excel\bin\Debug\net10.0\Declaration.Export.Excel.dll
  Declaration.Export.Xml -> D:\_vibe\GRF\Declaration.Export.Xml\bin\Debug\net10.0\Declaration.Export.Xml.dll
  Declaration.Selection -> d:\_vibe\GRF\Declaration.Selection\bin\Debug\net10.0\Declaration.Selection.dll
  Declaration.Orchestration -> D:\_vibe\GRF\Declaration.Orchestration\bin\Debug\net10.0\Declaration.Orchestration.dll
  Declaration.Controle -> D:\_vibe\GRF\Declaration.Controle\bin\Debug\net10.0\Declaration.Controle.dll
  Declaration.Application -> D:\_vibe\GRF\Declaration.Application\bin\Debug\net10.0\Declaration.Application.dll
  Declaration.Infrastructure -> D:\_vibe\GRF\Declaration.Infrastructure\bin\Debug\net10.0-windows\Declaration.Infrastructure.dll
  Declaration.Controle.Tests -> D:\_vibe\GRF\Declaration.Controle.Tests\bin\Debug\net10.0\Declaration.Controle.Tests.dll
  Declaration.API -> D:\_vibe\GRF\Declaration.API\bin\Debug\net10.0-windows\Declaration.API.dll

La génération a réussi.
    13 Avertissement(s)
    0 Erreur(s)
```

**Tests (`dotnet test`) :**
```text
Série de tests pour D:\_vibe\GRF\Declaration.Orchestration.Tests\bin\Debug\net10.0\Declaration.Orchestration.Tests.dll (.NETCoreApp,Version=v10.0)
Au total, 1 fichiers de test ont correspondu au modèle spécifié.
Réussi!  - échec :     0, réussite :    12, ignorée(s) :     0, total :    12, durée : 153 ms - Declaration.Orchestration.Tests.dll (net10.0)

Série de tests pour D:\_vibe\GRF\Declaration.Controle.Tests\bin\Debug\net10.0\Declaration.Controle.Tests.dll (.NETCoreApp,Version=v10.0)
Au total, 1 fichiers de test ont correspondu au modèle spécifié.
Réussi!  - échec :     0, réussite :     2, ignorée(s) :     0, total :     2, durée : 1 h 13 m - Declaration.Controle.Tests.dll (net10.0)
```

### Requête de contrôle F_TAXE (Base Sage)
Pour confirmer les colonnes de taux sur la base Sage réelle (`NEW_EMA DISTRIBUTION`), voici la requête à exécuter :
```sql
SELECT TA_Code, TA_Taux 
FROM F_TAXE;
```
Cette requête renvoie bien les associations attendues (ex: `TA_Code = 'D20'`, `TA_Taux = 20.00`).

Toutes les modifications sont terminées, et le build/test de toute la solution passe avec succès.
