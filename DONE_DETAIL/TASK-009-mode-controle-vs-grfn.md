# TASK-009 — Mode Contrôle : recalcul corrigé vs `RT_LigneDeclarationTva` (résultat GRFN)

## Contexte
**Objectif #1 d'origine du module** (`MODULE_DECLARATION_TVA.md` §1) : recalculer les lignes attendues et les **comparer au résultat stocké par l'ancien module** (`RT_LigneDeclarationTva`) pour localiser écarts et lignes manquantes. Désormais **faisable** : la base prod `GR_EMA_DISTRIBUTION` (client EMA) contient de vraies déclarations GRFN.

> ⚠️ **Repositionnement (PO, 08/07/2026)** : ce mode n'est **pas** un écran du workflow. C'est un **outil de diagnostic ponctuel et éphémère** — comprendre les erreurs des anciennes déclarations GRFN pour **ne pas les reproduire** dans le nouveau module. Il ne « vit » pas dans le cycle de vie de déclaration (création → checkup → génération). ⇒ **priorité basse**, rapport de diagnostic suffisant (pas besoin d'un écran riche intégré au front principal).

## Périmètre STRICT
- **Uniquement** : charger une déclaration GRFN existante, recalculer via le pipeline (TASK-007), **aligner et comparer** → rapport d'écarts.
- **Exclu** : génération du fichier de dépôt (XML #7), modification de quoi que ce soit dans GRFN/base (lecture seule).

## Positionnement / architecture
- Nouveau projet/adaptateur **`Declaration.Controle`** (`net10.0`) : lit `RT_DeclarationTva` / `RT_LigneDeclarationTva` (SQL lecture seule), appelle l'orchestrateur (TASK-007) pour recalculer la même période, aligne les deux ensembles.

## Objectif
```
Entrée : DT_Id (déclaration existante) OU (société + période)
Traitement : (a) charger lignes GRFN stockées ; (b) recalculer via pipeline corrigé ; (c) aligner par (facture / affectation / taux)
Sortie : RapportEcarts { lignes concordantes, écarts assiette/TVA (montant + cause), lignes présentes GRFN absentes du recalcul, et inversement }
```

### Causes d'écart à typer (issues de l'analyse GRFN §2)
- **Arrondi** : ancien `ToEven` + ratio arrondi à 6 déc. vs nouveau prorata direct + `AwayFromZero` (bugs 1/2/3).
- **Saut silencieux** ancien module : chèque non rapproché, espèce hors période, `Solde==Montant`, facture non comptabilisée, taxe non « à taux ».
- **Humain** : ligne non intégrée / reportée (`IsReport=1`).

## Contraintes techniques
- `net10.0`, SQL lecture seule GRF ; réutilise l'orchestrateur (TASK-007) pour le recalcul (pas de recalcul dupliqué).
- Alignement déterministe par clé (n° facture + taux + n° règlement/affectation).
- `decimal` pour toute comparaison de montants ; tolérance d'arrondi paramétrable pour classer « concordant » vs « écart ».

## Étapes
1. Charger `RT_DeclarationTva` + `RT_LigneDeclarationTva` (mapping colonnes §4 du module).
2. Reconstituer les `AffectationADeclarer` de la même période (via TASK-008) et recalculer (TASK-007).
3. **Aligner** GRFN ↔ recalcul par clé ; classer chaque paire : concordant / écart chiffré / manquant d'un côté.
4. **Typer la cause** de chaque écart (arrondi / saut silencieux / report / humain) autant que possible.
5. Produire le **rapport d'écarts** (structuré + agrégé : nb lignes, Σ écart TVA, top causes).

## Livrables
- `Declaration.Controle` : service `Comparer(DT_Id | période)` → `RapportEcarts`.
- `VERIFY/TASK-009_verify.md` : rapport réel sur une déclaration EMA (chiffres, exemples d'écarts avec cause, lignes manquantes), démonstration que le moteur corrigé explique/corrige les écarts GRFN.

## Critères de validation
- Comparaison réelle sur ≥ 1 déclaration existante d'EMA.
- Chaque écart est **chiffré et, si possible, causé**.
- Lignes manquantes (présentes d'un seul côté) listées avec hypothèse de cause.
- Lecture seule stricte.

## Risques / dépendances
- ⛔ **Bloqué** : base prod `GR_EMA_DISTRIBUTION` (serveur/credentials) + **TASK-007** (orchestration) + **TASK-008** (sélection, pour reconstituer la période).
- La cause d'un écart n'est pas toujours déterminable automatiquement → classer « indéterminé » plutôt que deviner.
- C'est la **démo à plus forte valeur** pour le client (montre concrètement les erreurs de l'ancien module) → à prioriser dès que la base est branchée.
