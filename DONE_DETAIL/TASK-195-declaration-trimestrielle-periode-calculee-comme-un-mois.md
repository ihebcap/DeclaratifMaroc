# TASK-195 — Declaration trimestrielle calculée sur le mauvais intervalle de dates

## Problème
`DeclarationEntete.Periode` est un champ partagé (« Mois 1-12 ou Trimestre 1-4 »). Cependant, les 5 endroits de `DeclarationWorkflowService.cs` (lignes 265, 418, 543, 1127, 1341) traitaient systématiquement `Periode` comme un mois, même lorsque `Type == TypePeriode.Trimestrielle`. Par conséquent, une déclaration trimestrielle T3 (`Type=Trimestrielle`, `Periode=3`) scannait le mois de mars au lieu de l'intervalle réel juillet–septembre.

## Solution apportée
1. **Centralisation dans `DeclarationEntete`** :
   - Ajout d'une méthode statique `DeclarationEntete.CalculerIntervalleDates(int exercice, int periode, TypePeriode type)` et d'une méthode d'instance `declaration.ObtenirIntervalleDates()`.
   - Si `type == TypePeriode.Trimestrielle` : `periode` (1-4) détermine le mois de début `(periode - 1) * 3 + 1` et l'intervalle dure 3 mois (ex. T3 = 01/07 au 30/09).
   - Si `type == TypePeriode.Mensuelle` : comportement inchangé (mois 1-12, 01/M au dernier jour de M).
   - Validation stricte des bornes (`ArgumentOutOfRangeException` si mois < 1 ou > 12, ou trimestre < 1 ou > 4).

2. **Remplacement dans `DeclarationWorkflowService.cs`** :
   - Les 5 occurrences identifiées ont été remplacées par des appels centralisés `declaration.ObtenirIntervalleDates()`.

3. **Tests unitaires** :
   - Création de `Task195DeclarationTrimestrielleDatesTests.cs` dans `Declaration.Orchestration.Tests` couvrant les 4 trimestres (T1 à T4), les cas mensuels de contrôle (mois bissextile/non bissextile), la réutilisation des propriétés de l'entête et la levée d'exceptions sur paramètres invalides.

## Impact rétroactif PO
Les déclarations trimestrielles fermées ou créées en production avant ce correctif ont potentiellement figé des lignes sur le mauvais intervalle (un mois au lieu du trimestre). Il est recommandé au PO de supprimer et recréer les déclarations trimestrielles non clôturées pour recalculer les lignes sur l'intervalle exact.
