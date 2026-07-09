# TASK-026 — Réparer `Declaration.Core.Tests` + intégrer les projets de tests au `.slnx`

## Contexte
La revue de **TASK-022** a révélé deux problèmes indépendants de son périmètre mais bloquants pour la fiabilité des VERIFY :

1. **`Declaration.Core.Tests` ne compile pas** (régression **pré-existante**, antérieure à TASK-022) :
   `ConstructeurDeclarationTests.cs` appelle `ConstruireDeclaration(affs, (id, sens) => …, n)` avec un
   lambda **2 arguments**, alors que la signature actuelle est
   `Func<AffectationADeclarer, DocumentTaxesInfo?>` (1 argument). Erreurs `CS1593` aux lignes 74, 109,
   133, 185. L'API a évolué (de `(numeroFacture, sens)` vers `AffectationADeclarer`, ~TASK-019) sans que
   ces tests soient mis à jour.
2. **Le `.slnx` exclut 5 des 6 projets de tests** : seul `Declaration.Controle.Tests` est référencé dans
   `DeclarationTVA.slnx`. Un `dotnet build`/`dotnet test` « de la solution » ne couvre donc qu'un projet de
   tests, ce qui a permis à un VERIFY (TASK-022) d'afficher « build/test solution vert » alors qu'un projet
   de tests était cassé.

## Impact
- `ConstructeurDeclaration` — cœur de la **transparence** (règle n°1) et modifié par TASK-022 (gestion
  `ECART_FGR` avec document partiel, `CODE_TAXE_INCONNU`, `SOLDE_INITIAL_NON_GERE`) — se retrouve **sans
  couverture de test compilable**.
- Les VERIFY ne peuvent pas prouver la non-régression du calcul tant que la commande « test solution »
  ignore la majorité des projets de tests.

## Périmètre STRICT
- **Tests + fichier solution uniquement.** Aucun changement de code de production.
- Ne pas modifier la signature de `ConstruireDeclaration` — c'est le **test** qui doit s'aligner sur le code.

## Étapes
1. Migrer `Declaration.Core.Tests/ConstructeurDeclarationTests.cs` : remplacer chaque lambda `(id, sens) => …`
   par un `Func<AffectationADeclarer, DocumentTaxesInfo?>` (résoudre par `affectation.NumeroFacture`), pour
   les 4 occurrences (l.74, 109, 133, 185) et toute autre du fichier.
2. Vérifier que la suite `Declaration.Core.Tests` compile et passe (`dotnet test Declaration.Core.Tests`).
3. Ajouter les 5 projets de tests manquants au `.slnx` :
   `Declaration.Core.Tests`, `Declaration.Orchestration.Tests`, `Declaration.Selection.Tests`,
   `Declaration.Export.Excel.Tests`, `Declaration.Export.Xml.Tests`.
4. (Recommandé) Ajouter **au moins un test** sur le chemin FGR de `ConstructeurDeclaration` :
   `FgrValidationException` → alerte `ECART_FGR` + document partiel ventilé ; `InvalidOperationException`
   « CODE_TAXE_INCONNU » → alerte `CODE_TAXE_INCONNU` + ligne non ventilée ; `EC_Type=4` →
   `SOLDE_INITIAL_NON_GERE`.
5. `dotnet build` + `dotnet test` **sur le `.slnx`** : prouver 0 erreur et tous les projets de tests exécutés.

## Contraintes techniques
- `net10.0`. Aucun accès base requis (tests en mémoire). Aucune modification `RT_*`.
- Ne pas casser `Declaration.Controle.Tests` (test d'intégration DB, long — peut rester marqué/filtré).

## Livrables
- `Declaration.Core.Tests` compilable et vert.
- `.slnx` incluant les 6 projets de tests.
- `VERIFY/TASK-026_verify.md` : log `dotnet build` + `dotnet test` **sur le `.slnx`** montrant tous les
  projets de tests exécutés (compte de tests par projet), 0 erreur.

## Critères de validation
- `dotnet test DeclarationTVA.slnx` exécute les 6 projets de tests (plus aucun projet de tests orphelin).
- `Declaration.Core.Tests` : 0 erreur de compilation, tous verts.
- Le chemin FGR de `ConstructeurDeclaration` est couvert par au moins un test (transparence prouvée).

## Risques / dépendances
- Découplé de tout autre travail (tests + fichier solution). Pré-requis implicite pour que les VERIFY
  futurs (TASK-023/009…) puissent affirmer « build/test solution » de façon exhaustive.
