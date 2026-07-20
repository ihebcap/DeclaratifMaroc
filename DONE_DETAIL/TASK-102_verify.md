# TASK-102 Verify — Tracer le n° de règlement dans les anomalies de facture (Écran ② Vérifier & Intégrer)

> Preuves de bon fonctionnement du correctif pour tracer le règlement d'affectation (`NumeroRapprochement`) dans les messages d'anomalie de facture de l'étape ② « Vérifier & Intégrer ».

## Modifications apportées

1. **Backend (`DeclarationWorkflowService.cs`)** :
   - Enrichissement du message pour l'anomalie bloquante **`FACTURE_NON_VENTILEE`** : si le règlement (`NumeroRapprochement`) est présent, le message affiche `Ligne en anomalie de recalcul (facture {l.NumeroFacture}, règlement {l.NumeroRapprochement}) : {l.MotifRejet}`. Sinon, il conserve sa structure d'origine.
   - Enrichissement du message pour l'avertissement d'incohérence Sage sur ligne exclue au figeage (**`LIGNE_FIGEE_A_REVERIFIER`**) : si le règlement est présent, le message affiche `Facture {l.NumeroFacture}, règlement {l.NumeroRapprochement} exclue de la valorisation (EC_Id={l.EC_Id}) : ...`. Sinon, il conserve sa structure d'origine.
   - Mécanisme de repli propre implémenté si `NumeroRapprochement` est vide/nul pour éviter toute régression ou affichage parasite.

2. **Tests Unitaires (`Task102NumeroReglementAnomaliesFactureTests.cs`)** :
   - Ajout de 4 nouveaux tests unitaires automatisés validant :
     - Le message avec règlement pour l'incohérence Sage sur ligne exclue.
     - Le message sans règlement pour l'incohérence Sage sur ligne exclue (conserve le format d'origine).
     - Le message avec règlement pour l'anomalie `FACTURE_NON_VENTILEE`.
     - Le message sans règlement pour l'anomalie `FACTURE_NON_VENTILEE` (conserve le format d'origine).

3. **Validation E2E (`task102.spec.ts`)** :
   - Ajout d'une spécification de test Playwright simulant et vérifiant l'affichage correct des anomalies enrichies sur l'interface utilisateur d'intégration, et produisant une capture d'écran de validation.

---

## Validation des tests unitaires

Tous les tests de la suite `Declaration.Orchestration.Tests` s'exécutent avec succès.

### Commande de test exécutée
```powershell
dotnet test --filter "FullyQualifiedName~Declaration.Orchestration.Tests"
```

### Résultats de la console
```text
Série de tests pour D:\_vibe\GRF\Declaration.Orchestration.Tests\bin\Debug\net10.0\Declaration.Orchestration.Tests.dll (.NETCoreApp,Version=v10.0)
Au total, 1 fichiers de test ont correspondu au modèle spécifié.

Réussi!  - échec :     0, réussite :   125, ignorée(s) :     0, total :   125, durée : 484 ms - Declaration.Orchestration.Tests.dll (net10.0)
```

---

## Validation de la compilation

### Backend (.NET API)
```powershell
dotnet build
```
Build réussi avec succès sans erreur de compilation.

### Frontend (React/Vite)
```powershell
npm run build
```
Compilation réussie avec succès (`dist/` généré avec 0 erreur).

---

## Validation Visuelle (Capture d'écran de l'Étape ②)

La capture d'écran montre le bloc d'anomalies de l'écran de calcul et d'intégration avec les numéros de règlement (ex: `RC25040088` et `RF26040040`) correctement tracés :

- **Capture de validation** : [task102-step3-anomalies-reglement.png](task102-step3-anomalies-reglement.png)
