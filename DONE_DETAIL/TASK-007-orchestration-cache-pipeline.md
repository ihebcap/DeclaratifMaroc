# TASK-007 — Orchestration + cache (pipeline worker OM ↔ ventilation ↔ modèle)

## Contexte
Les briques pures existent (worker OM TASK-002 = détail TVA ; ventilation TASK-004 ; modèle+contrôle TASK-005). Il manque le **chef d'orchestre** qui les relie : à partir d'une liste d'`AffectationADeclarer` (fournie plus tard par la Sélection SQL #5 / TASK-008 ; **stubbée** ici), résoudre chaque facture via le **worker OM**, puis produire la `DeclarationModele`.

Point d'architecture acté (§5 du module) : le worker OM est **x86 + COM Sage**, consommé **out-of-process** (exe JSON) → l'orchestration reste `net10.0` moderne, non contaminée par x86.

## Périmètre STRICT
- **Uniquement** : invocation du worker out-of-process + **cache** des détails facture + assemblage via `ConstruireDeclaration`.
- **Exclu** : la vraie Sélection SQL (#5 / TASK-008 — ici on prend la liste d'affectations en entrée / stub), les exports (#6/#7), le Mode Contrôle (#9).

## Positionnement / architecture
- Nouveau projet **`Declaration.Orchestration`** (`net10.0`) référençant `Declaration.Core` + `SageTaxReader.Contracts`. **Aucune** référence COM directe.
- Le worker `SageTaxReader.Console` est adapté pour exposer un **mode « lecture facture → JSON `DocumentTaxesInfo` »** invocable en ligne de commande (args : n° pièce + sens ; sortie : JSON sur stdout). L'orchestrateur le lance en **process séparé** (x86) et **désérialise** le JSON en `DocumentTaxesInfo` (via `Contracts`).
- **Cache** : une facture (n° + sens) n'est lue **qu'une fois** ; réutilisée pour toutes les affectations qui la pointent (exigence anti-allers-retours du CDC).

## Règle de dépendance
- Communication worker = **contrat JSON** (`DocumentTaxesInfo` de `Contracts`), pas de référence in-process au worker.
- Paramètres worker (serveur, base Sage, credentials) = **configuration**, jamais en dur (multi-client).

## Objectif
Un service `OrchestrateurDeclaration` :
```
Entrée : IEnumerable<AffectationADeclarer>, config worker (serveur/base/credentials), int n
Traitement : pour chaque n° facture distinct → invoquer worker (cache) → DocumentTaxesInfo
Sortie : DeclarationModele (via ConstruireDeclaration)
```

## Contraintes techniques
- `net10.0` ; invocation process séparé (`System.Diagnostics.Process`) du worker x86 ; timeout + gestion d'erreur (worker plante / facture ouverte dans Sage → remonter en alerte, pas en crash).
- Cache clé = `(NumeroFacture, Sens)` ; thread-safe si appels concurrents (sinon séquentiel documenté).
- Sérialisation JSON = `System.Text.Json`, contrat partagé via `Contracts`.

## Étapes
1. **Mode JSON du worker** : ajouter à `SageTaxReader.Console` une commande qui prend (n° pièce, sens) et écrit le `DocumentTaxesInfo` en JSON sur stdout (réutilise `LireFactureVente`/`LireFactureAchat`). Lecture seule.
2. **Invocateur** : dans `Declaration.Orchestration`, lancer le worker en process séparé, capturer stdout, désérialiser en `DocumentTaxesInfo`. Gérer code retour ≠ 0 / timeout → `null` + raison.
3. **Cache** : mémoriser par `(facture, sens)`.
4. **Orchestrateur** : boucler les affectations, résoudre (cache), appeler `ConstruireDeclaration(affectations, resoudreFacture, n)`.
5. **Tests** : (a) avec worker **stub** (faux exe / faux résolveur JSON) → vérifier cache (1 seule invocation par facture) + assemblage correct ; (b) test d'intégration optionnel avec le vrai worker sur une base Sage accessible.

## Livrables
- `SageTaxReader.Console` : mode JSON documenté (args + format sortie).
- `Declaration.Orchestration` : `OrchestrateurDeclaration` + invocateur worker + cache.
- Tests (stub + intégration optionnelle).
- `VERIFY/TASK-007_verify.md` : trace d'un run (liste d'affectations stub → `DeclarationModele` JSON), preuve du cache (N affectations / 1 lecture par facture).

## Critères de validation
- Worker invocable out-of-process et renvoie un `DocumentTaxesInfo` désérialisable.
- Une facture référencée par plusieurs affectations n'est lue **qu'une fois** (cache prouvé).
- Erreur worker (timeout / introuvable) → alerte dans le modèle, pas de crash.
- `Declaration.Orchestration` sans référence COM/Sage directe (net10.0 pur).

## Risques / dépendances
- **Non bloqué** : testable avec stub (pas besoin base prod). Le test d'intégration réel a besoin d'une base Sage accessible.
- **Prérequis TASK-006** (modèle corrigé) et s'appuie sur TASK-004/005.
- Le contrat d'entrée `AffectationADeclarer` (TASK-005) doit rester stable — c'est ce que TASK-008 (Sélection SQL) remplira réellement.
- Sérialisation `double` du DTO : cohérente entre worker (net48) et orchestrateur (net10) car `Contracts` (netstandard2.0) est partagé.
