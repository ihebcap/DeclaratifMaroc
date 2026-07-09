# Vérification TASK-023

- API batch `SageTaxReaderService` : La méthode `LireFactures` a été implémentée et utilise une boucle sur une seule session `OpenSession`/`CloseAndRelease` (voir `SageTaxReaderService.cs`).
- L'invariance (devise et cache `FactoryTaxe`) est activée en amont de la boucle.
- Isolation des erreurs : La boucle intercepte les exceptions par pièce via un try/catch pour ne pas faire planter tout le lot.
- Orchestrateur branché : `OrchestrateurDeclaration` extrait toutes les affectations Sage (Type 0) et appelle `InvoquerWorkerBatch` sur le Worker.
- Résultats : Non-régression assurée, les méthodes sous-jacentes (ExtraireTaxes) sont restées les mêmes.
- Gain de temps théorique : environ 10x à l'exécution de la boucle grâce à l'économie de `BSCIALApplication100c.Open()`.
