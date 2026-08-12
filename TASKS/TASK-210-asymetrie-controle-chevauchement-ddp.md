# TASK-210 — DDP : contrôle de chevauchement de période asymétrique (annuelle englobant des trimestrielles non détectée)

Status: 🆕 à faire
Priority: MEDIUM (permet de créer une déclaration DDP en doublon de couverture, reproduit du legacy)
Module: Declaration.Core

> **Origine :** point ouvert documenté dans TASK-132 (28/07/2026, `DONE_DETAIL/DDP-TASK-132-...md`) —
> « asymétrie annuelle/trimestrielle du contrôle de chevauchement, reproduite du legacy, non corrigée ».
> Jamais transformé en task séparée. Confirmé toujours présent le 09/08/2026 (lecture code).

## Cause identifiée (preuve de code)

[Declaration.Core/DeclarationDelaiPaiementCycleDeVie.cs:239-258](../Declaration.Core/DeclarationDelaiPaiementCycleDeVie.cs#L239-L258),
`TrouverPeriodeEnConflit` :

```csharp
// 1) Égalité exacte des bornes calculées (legacy l.655, rendue réellement effective).
var identique = duMemeExercice.FirstOrDefault(x => x.DateDebut.Date == debut && x.DateFin.Date == fin);
if (identique != null) return identique;

// 2) Période existante ENGLOBANTE (legacy l.659, reproduit tel quel, asymétrie incluse).
return duMemeExercice.FirstOrDefault(x => x.DateDebut.Date <= debut && x.DateFin.Date >= fin);
```

Le contrôle ne détecte que le cas où une déclaration **existante** englobe la **nouvelle** période
(ex. : annuelle déjà créée, tentative de créer un trimestre dedans → bloqué, correct). Il ne détecte
**pas** le cas inverse : créer une déclaration **annuelle** alors que des déclarations **trimestrielles**
existent déjà pour tout ou partie de cet exercice — la nouvelle période englobe des périodes existantes,
mais `TrouverPeriodeEnConflit` ne vérifie jamais si `debut <= x.DateDebut && fin >= x.DateFin` (le sens
inverse). Reproduit tel quel du legacy (commentaire explicite « asymétrie incluse » dans le code actuel),
jamais corrigé.

**Effet observable** : un utilisateur peut créer une déclaration DDP annuelle pour un exercice où
1 à 4 déclarations trimestrielles existent déjà — les échéances des trimestres déjà couverts risquent
d'être resélectionnées et déclarées une seconde fois dans l'annuelle (le calcul incrémental de
TASK-131 limite le risque de double-comptage de retard, mais ne bloque pas la création en double de la
déclaration elle-même, ce qui reste confus pour l'utilisateur et incohérent avec l'objectif d'unicité
de période).

## Périmètre STRICT

- **Inclus** :
  1. Ajouter dans `TrouverPeriodeEnConflit` le cas symétrique manquant : la nouvelle période englobe
     une ou plusieurs déclarations existantes du même exercice (`debut <= x.DateDebut.Date && fin >=
     x.DateFin.Date`).
  2. Message d'erreur explicite listant la ou les déclaration(s) existante(s) en conflit dans ce
     nouveau cas (cohérent avec le message déjà utilisé pour les cas 1/2 existants,
     `DeclarationDelaiPaiementService.cs:185-188`).
  3. Test unitaire dédié dans `DeclarationDelaiPaiementCycleDeVieTests.cs` couvrant explicitement ce
     scénario (annuelle après trimestrielles existantes) — absent aujourd'hui (seule l'asymétrie
     inverse, déjà couverte, l'est).
- **Exclu** :
  - Ne pas toucher aux deux cas déjà couverts (égalité exacte, existante englobante) — non-régression
    stricte.
  - Pas de vérification multi-exercice (le filtre `duMemeExercice` reste limité au même exercice,
    cohérent avec le comportement legacy existant sur les deux autres cas).

## Livrables

- `TrouverPeriodeEnConflit` détecte les 3 cas de conflit (égalité, existante englobante, nouvelle
  englobante).
- Nouveau test unitaire couvrant le cas symétrique ajouté.

## Critères de validation

- Créer 4 déclarations trimestrielles pour un exercice, puis tenter de créer l'annuelle du même
  exercice → rejet explicite citant la ou les déclaration(s) trimestrielle(s) en conflit.
- Les 2 cas de conflit déjà gérés avant cette TASK ne régressent pas (test existant à rejouer).
- Suite `Declaration.Core.Tests` complète toujours verte.

## Files

- [Declaration.Core/DeclarationDelaiPaiementCycleDeVie.cs](../Declaration.Core/DeclarationDelaiPaiementCycleDeVie.cs) (fonction `TrouverPeriodeEnConflit`, lignes 239-258).
- [Declaration.Core.Tests/DeclarationDelaiPaiementCycleDeVieTests.cs](../Declaration.Core.Tests/DeclarationDelaiPaiementCycleDeVieTests.cs).
