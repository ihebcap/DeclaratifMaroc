# TASK-108 — Faux « écart détecté » à l'étape ② : le contrôle d'équilibre mélange deux ensembles de lignes

> **Origine** : micro-cadrage architecte (17/07/2026) demandé pour arbitrer TASK-107. En analysant
> ce que « localiser l'écart » signifie, découverte que **l'écart affiché à l'étape ② n'est pas un
> déséquilibre comptable** : c'est la TVA totale de la déclaration, mal étiquetée, produite par une
> asymétrie d'ensembles dans le calcul. Explique le « écart détecté » que le PO voyait
> systématiquement (`TVA1-2026-01` 341 155,01 ; `TVA1-2026-06` 1 538,27 ; `TVA1-2026-07` 1 480,50).

## Contexte — cause racine (lecture de code, corroborée par les chiffres réels)

Le contrôle d'équilibre veut vérifier que **la TVA explique l'écart entre HT et TTC**
(`TTC = HT + TVA`, cf. commentaire `DeclarationWorkflowService.cs:745` et `ResiduExplique`
`:754`). Mais ses trois termes ne sont **pas** agrégés sur le même ensemble de lignes :

| Terme | Ensemble de lignes | Emplacement |
|---|---|---|
| `TotalDeclareTtc` (Σ TTC) | `Integree` **OU** `Proposee` | `DeclarationWorkflowService.cs:742,748` |
| `TotalMontantAffecte` (Σ HT) | `Integree` **OU** `Proposee` | `DeclarationWorkflowService.cs:742,747` |
| `totalTva` (Σ TVA, terme soustrait) | `Integree` **SEUL** | `DeclarationsController.cs:234,271` |

```csharp
// DeclarationsController.cs:271-273
var totalTva = integrees.Sum(l => l.TVA);            // integrees = Integree SEUL (l.234)
var ecart = result.ControleEquilibre.TotalDeclareTtc // Σ TTC sur Integree||Proposee
            - (result.ControleEquilibre.TotalMontantAffecte + totalTva); // Σ HT (I||P) + Σ TVA (I)
```

Comme `TTC = HT + TVA` par ligne (`LigneCandidate` n'a pas de taxe parafiscale : `HT/Taux/TVA/TTC`
seulement), l'écart se simplifie :

```
écart = Σ_{Int∪Prop}(TTC) − Σ_{Int∪Prop}(HT) − Σ_{Int}(TVA)
      = Σ_{Int∪Prop}(TVA) − Σ_{Int}(TVA)
```

À l'étape ② la déclaration est **`EnCours`** → aucune ligne `Integree` (les lignes valorisées sont
`Proposee` jusqu'à la clôture) → le terme soustrait vaut **0** :

```
écart au ②  =  Σ_Proposee(TVA)  =  la TVA totale de la déclaration
```

**Corroboration empirique** (VERIFY TASK-103) : `TVA1-2026-06` écart 1 538,27 = TVA de la seule
source ; `TVA1-2026-07` écart 1 480,50 = Achats 1 243,33 + Ventes 237,17 = ΣTVA `Proposee`. Les
chiffres collent exactement à la dérivation.

**Corroboration inverse** : sur une déclaration **`Cloturee`**, toutes les lignes sont `Integree`
→ les ensembles coïncident → écart ≈ 0. C'est précisément ce que montrait le VERIFY de TASK-087 à
l'écran ⑤ (« badge Équilibre OK »). Même code, deux comportements selon l'état = signature du
décalage d'ensembles.

## Conséquences

1. Le contrôle « Cohérence des totaux déclarés » affiche **toujours** un « écart détecté » ≈ TVA
   totale sur **toute** déclaration `EnCours` non vide → faux positif permanent à l'étape ②.
2. Le tableau « Répartition par source » monté sous ce badge (TASK-087/103) ventile en réalité la
   **TVA totale** par source, pas un « écart » — les chiffres sont justes, seul le **libellé
   « écart »** au-dessus est trompeur au ②.
3. Rend TASK-107 (« localiser d'où vient l'écart ») **mal posée** : l'écart au ② n'a pas de pièce
   coupable, il *est* la TVA totale.

## Périmètre STRICT

- **Inclus** :
  1. Aligner le terme `totalTva` (`DeclarationsController.cs:271`) sur le **même ensemble** que
     `TotalDeclareTtc`/`TotalMontantAffecte` : `Integree || Proposee`. Un seul ensemble de vérité
     pour les trois termes du contrôle d'équilibre.
- **Exclu** :
  - `recapSource`/`recapTaux` : déjà sur `Integree||Proposee` depuis TASK-103 — ne pas retoucher.
  - Le calcul de `TotalDeclareTtc`/`TotalMontantAffecte` (`DeclarationWorkflowService`) : déjà sur
    le bon ensemble — inchangé.
  - Tout front (`RecapSourceTable`, `VerifierIntegrerPanel`) : le composant est correct ; une fois
    l'écart ~0, le badge d'erreur ne se montera plus de lui-même (condition `status==='error'`).
  - L'écran ⑤ / `Cloturee` : déjà équilibré (ensembles coïncidents) — aucune régression.

## Objectif

```
Entrée  : déclaration EnCours, lignes valorisées Proposee, TTC=HT+TVA par ligne
Traitement : les trois termes de l'écart agrègent le MÊME ensemble (Integree || Proposee)
Sortie  : à l'étape ②, écart ≈ 0 sur une déclaration équilibrée (plus de faux « écart détecté ») ;
          le contrôle ne se déclenche que sur un vrai résidu TTC ≠ HT+TVA (anomalie réelle)
```

## Livrables

- `DeclarationsController.cs` : `totalTva` calculé sur `lignesRecap` (`Integree || Proposee`, déjà
  défini l.240 pour TASK-103) au lieu de `integrees` (`Integree` seul).
- Test(s) dans `Declaration.Orchestration.Tests` : sur une déclaration `EnCours` équilibrée
  (lignes `Proposee`, `TTC=HT+TVA`), l'écart renvoyé par `/checkup` est ≈ 0 (`equilibre.isValid`
  vrai) ; non-régression : une déclaration `Cloturee` équilibrée reste à écart ≈ 0 ; une ligne
  réellement incohérente (`TTC ≠ HT+TVA`) produit toujours un écart non nul.
- `VERIFY/TASK-108_verify.md` : preuve sur données réelles — une déclaration `EnCours` réelle
  (ex. `TVA1-2026-06`/`-07`) passe de « écart détecté ≈ TVA totale » à écart ≈ 0 à l'étape ②, sans
  changer aucun montant déclaré ni le comportement de l'écran ⑤.

## Critères de validation

- Sur une déclaration `EnCours` équilibrée, l'étape ② n'affiche plus de faux « écart détecté ».
- L'écran ⑤ (`Cloturee`) reste inchangé (écart ≈ 0 comme avant).
- Un vrai résidu `TTC ≠ HT + TVA` (anomalie de valorisation) déclenche encore le contrôle.
- Aucun montant déclaré, aucune ligne, aucune sélection modifiés (correction d'affichage/contrôle).

## Risques / dépendances

- **Prérequis de TASK-107** : tant que l'écart au ② est un artefact, « localiser l'écart » n'a pas
  de sens. Après cette correction, seul un **vrai** déséquilibre subsiste — et il est, lui,
  décomposable par ligne (`TTC ≠ HT+TVA`). TASK-107 est **bloquée** jusqu'à celle-ci puis à
  réévaluer (probablement réduite à un drill vers les lignes réellement incohérentes).
- Faible risque : on rétrécit un ensemble d'agrégation d'affichage pour le faire coïncider avec
  celui déjà utilisé par les deux autres termes — aucune règle métier nouvelle, aucun recalcul TVA.
- À vérifier au passage (non bloquant) : qu'aucune incohérence Sage réelle (ligne `Exclue`, hors
  ensemble) censée remonter un écart ne soit masquée — elle est déjà signalée séparément par
  l'alerte `LIGNE_EXCLUE`/`LIGNE_FIGEE_A_REVERIFIER` (TASK-076/077/082), pas par l'écart d'équilibre.
```
