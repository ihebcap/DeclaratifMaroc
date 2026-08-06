# TASK-197 — CRITIQUE : Dépense et Frais bancaire éliminés par le filtre de sélection (TASK-097), jamais déclarables

Status: 🆕 à faire — **BLOQUANT**
Priority: CRITICAL
Risk: LOW à corriger (exempter 2 sources d'un filtre existant), le risque est de mal cerner le
périmètre d'exemption
Module: Declaration.Application

> **Origine :** cas client réel (EMA, 05/08/2026) — après TASK-193 (frais bancaires rattachés au
> bon domaine) et TASK-196 (CT_Type sur l'écran Rapprochement), le PO signale que les frais
> bancaires n'apparaissent toujours pas dans l'onglet Sélection. Diagnostic : ce n'est pas un
> problème d'affichage, les frais bancaires (et les Dépenses) sont **structurellement impossibles**
> à faire apparaître dans une déclaration depuis TASK-097.

## Constat (preuve de code)

Deux faits combinés :

1. L'écran de sélection manuelle des règlements (`GET /rapprochement`, alimenté par
   `DeclarationRepository.GetReglementsRapprochementAsync`) exclut **volontairement**
   `MV_Domaine=6` (Dépense) et ne référence jamais `RT_PREVISIONNELLE` (Frais bancaire) —
   `DeclarationRepository.cs:671` : *« exclut bordereaux de remise, virements, alim. caisse ; les
   frais bancaires MV_Domaine=6 sont dans une autre table »*. Ces deux sources ne sont **jamais**
   listées comme sélectionnables.

2. `ConstruireLignesFigeesAsync` (`DeclarationWorkflowService.cs:288-294`, TASK-097) filtre
   **tous** les candidats, sans exception, sur l'appartenance à `selectionSet` (la sélection
   persistée par l'utilisateur) :
   ```csharp
   var selection = await _repository.GetSelectionReglementsAsync(declarationId);
   var selectionSet = new HashSet<string>(selection ?? Enumerable.Empty<string>());
   candidatesList = candidatesList.Where(c => selectionSet.Contains(c.Affectation.NumeroRapprochement)).ToList();
   ```

Combinaison : un candidat `Source=Depense` ou `Source=FraisBancaire` ne peut **jamais** être dans
`selectionSet` (impossible à sélectionner dans un écran qui ne le liste pas) → il est **toujours**
éliminé ici, quel que soit son statut d'éligibilité par ailleurs. TASK-193 (routage par domaine) ne
suffit donc pas : les frais bancaires passent le routage domaine puis meurent au filtre de
sélection, juste après.

Le commentaire `TASK-080` (`SelectionExpliqueeService.cs:152-155`) confirme l'intention de
conception opposée : *« RT_PREVISIONNELLE n'a pas de RT_AFFECTATION, donc le tampon DT_Id/TASK-028
ne s'applique jamais ici, exactement comme pour la Dépense »* — Dépense et FraisBancaire ont été
pensés comme des sources **auto-incluses**, sans geste de sélection manuelle. TASK-097 n'a
vraisemblablement jamais pris en compte ces deux sources lors de son implémentation (leur premier
usage réel remonte peut-être à après TASK-097, ou le cas n'a simplement jamais été testé bout en
bout jusqu'à ce diagnostic).

## Objectif

Exempter `Source == Depense` et `Source == FraisBancaire` du filtre `selectionSet` de TASK-097 —
ces deux sources restent gouvernées uniquement par leur propre garde-fou (`DT_Id IS NULL` /
exclusivité inter-déclaration TASK-080), pas par une sélection manuelle qui n'existe pas pour elles.

```csharp
candidatesList = candidatesList.Where(c =>
    c.Affectation.Source == SourceAffectation.Depense
    || c.Affectation.Source == SourceAffectation.FraisBancaire
    || selectionSet.Contains(c.Affectation.NumeroRapprochement)).ToList();
```

(ou équivalent — à valider par le développeur, l'idée est l'exemption, pas cette syntaxe exacte)

**Vérifier aussi** : le même motif `selectionSet.Contains(c.Affectation.NumeroRapprochement)`
apparaît une 2ᵉ fois ligne ~1135, et une variante `selectionSet.Contains(r.MvNumero)` ligne ~1343
(à vérifier si ce 3ᵉ cas est bien un chemin de construction de lignes déclarables, ou un chemin de
reporting/export non concerné par TASK-097 — ne pas appliquer l'exemption à l'aveugle sans
comprendre ce que fait précisément ce 3ᵉ bloc). Appliquer l'exemption partout où c'est pertinent
pour éviter une divergence entre 2-3 chemins qui devraient se comporter pareil.

## Garde-fous

- Ne pas supprimer le filtre TASK-097 pour les autres sources (Decaissement, Espece, Encaissement)
  — l'exemption est strictement limitée à Depense/FraisBancaire.
- Vérifier qu'aucun mécanisme de double-déclaration ne devient possible pour ces 2 sources en
  l'absence du filtre `selectionSet` — s'appuyer sur le garde-fou TASK-080 déjà en place (exclusivité
  inter-déclaration) plutôt que d'en inventer un nouveau.
- Test de régression obligatoire : un règlement Decaissement/Encaissement non sélectionné reste
  bien exclu (non-régression TASK-097) ; un frais bancaire/dépense éligible apparaît, lui, sans
  jamais avoir été « sélectionné ».

## Files

- [Declaration.Application/Services/DeclarationWorkflowService.cs:288-294](../Declaration.Application/Services/DeclarationWorkflowService.cs#L288-L294) (et occurrence(s) similaire(s) plus loin dans le fichier)
- [Declaration.Infrastructure/Repositories/DeclarationRepository.cs:671](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs#L671) (contexte — confirme l'exclusion volontaire de Dépense/écran Sélection, pas à modifier ici)
- [Declaration.Selection/SelectionExpliqueeService.cs:152-155](../Declaration.Selection/SelectionExpliqueeService.cs#L152-L155) (commentaire TASK-080, justification de l'intention de conception)

## Validation

- [ ] Un frais bancaire éligible (cas réel EMA, commissions bancaires TO_Id=1) apparaît dans les
      lignes figées d'une déclaration **sans avoir été sélectionné** dans l'onglet Sélection.
- [ ] Une Dépense éligible apparaît de la même façon (non-régression / confirmation que Dépense a le
      même besoin — vérifier si ce problème existait déjà avant TASK-193, ou si Dépense fonctionnait
      par un autre mécanisme non identifié ici).
- [ ] Non-régression stricte sur Decaissement/Encaissement (le filtre `selectionSet` reste actif
      pour ces sources).
- [ ] Test réel sur le cas EMA (déclaration mensuelle juillet) : les commissions bancaires
      apparaissent enfin dans la déclaration calculée.
- [ ] Build + tests unitaires OK.

## Dépendances / risques

- **Bloque** : TASK-193 est nécessaire mais pas suffisante sans ce fix — les deux doivent être
  livrées ensemble pour que les frais bancaires apparaissent réellement dans une déclaration.
- Risque principal : si Dépense fonctionnait par un mécanisme différent avant ce diagnostic (à
  vérifier — peut-être que Dépense n'a jamais été testée bout en bout non plus), corriger peut
  révéler d'autres écarts avec le comportement legacy GRFN sur ce domaine. À signaler au PO si
  observé pendant le développement.
