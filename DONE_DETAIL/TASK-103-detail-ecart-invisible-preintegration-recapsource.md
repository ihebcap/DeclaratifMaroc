# TASK-103 — Détail d'écart (« Répartition par source ») invisible en pré-intégration : `recapSource` limité aux lignes `Integree`

> **Origine** : test PO 17/07/2026 sur `TVA1-2026-01`, écran ② « Vérifier & Intégrer ». Le contrôle
> « Cohérence des totaux déclarés » affiche bien un écart (« Écart détecté : 341 155,01 MAD »), mais
> **aucun détail** n'apparaît en dessous — alors que TASK-087 devait afficher le tableau
> « Répartition par source » sous le badge en cas d'écart. Capture neuve, déclaration `EnCours`.

## Contexte — cause racine identifiée

Le tableau `RecapSourceTable` n'est monté que si `recapSource` (filtré par domaine) est non vide :

```tsx
// VerifierIntegrerPanel.tsx:1033
{c.id === 'equilibre' && c.status === 'error' && recapSource && recapSource.length > 0 && (
    <RecapSourceTable recapSource={recapSource} />
)}
```

Or **deux calculs du checkup utilisent des ensembles de lignes différents** :

| Donnée | Ensemble de lignes | Emplacement |
|---|---|---|
| **Écart** d'équilibre (badge ÉCART) | `Integree` **OU** `Proposee` | `DeclarationWorkflowService.cs:738` |
| **recapSource / recapTaux** (le détail) | `Integree` **seulement** | `DeclarationsController.cs:234-236` puis `:249` |

```csharp
// DeclarationWorkflowService.cs:738 — sert au calcul de l'écart
var integrees = toutes.Where(l => l.Etat == EtatLigne.Integree || l.Etat == EtatLigne.Proposee).ToList();
```
```csharp
// DeclarationsController.cs:234 — sert à recapSource / recapTaux
var integrees = lignes.Where(l => l.Etat == EtatLigne.Integree).ToList();
```

À l'étape ②, la déclaration est **`EnCours`** → les lignes valorisées éligibles sont en état
**`Proposee`** (elles ne passent `Integree` qu'à la clôture, `CloturerDeclarationAsync`).
Conséquence sur une déclaration non clôturée :

- l'**écart** inclut les `Proposee` → badge **ÉCART visible** ;
- **`recapSource` est vide** (aucune ligne `Integree` encore) → `displayRecapSource.length === 0`
  → **`RecapSourceTable` jamais rendu**.

Le détail promis par TASK-087 n'apparaît donc **que sur une déclaration déjà clôturée**, soit
précisément **pas** au moment ② où l'utilisateur doit comprendre l'écart **avant** d'intégrer. Le
VERIFY de TASK-087 a vraisemblablement été capturé sur un cas déjà intégré, masquant le défaut.

Le même écart d'ensemble affecte `displayTotalTVA` (`VerifierIntegrerPanel.tsx:340`), également
dérivé de `recapSource` : la TVA affichée à l'étape ② peut retomber sur le repli `localTotalTVA`
tant que `recapSource` est vide.

## Périmètre STRICT

- **Inclus** :
  1. Aligner la projection **`recapSource`** (`DeclarationsController.cs:236`) et **`recapTaux`**
     (`:249`) sur le **même ensemble que le contrôle d'équilibre** : lignes `Integree` **OU**
     `Proposee`. Un seul point de vérité pour « les lignes qui composent les totaux déclarés ».
  2. S'assurer que la valeur de `source` / `taux` / `domaine` reste correctement renseignée sur les
     lignes `Proposee` (elles sont valorisées par le même recalcul que les `Integree`) pour que le
     filtrage `sourceBelongsToDomain` côté front les rattache au bon onglet.
- **Exclu** :
  - Le **calcul de l'écart** lui-même (`DeclarationsController.cs:266-268`) : inchangé — déjà basé
    sur le bon ensemble.
  - Toute modification front (`RecapSourceTable`, condition d'affichage l.1033) : le composant est
    correct, il faut seulement l'alimenter.
  - L'écran ⑤ « Déclaration » (`DeclarationFinalePanel.tsx`) : post-clôture, les lignes y sont
    `Integree`, donc `Integree || Proposee` ne change rien à son rendu (aucune régression).

## Objectif

```
Entrée  : déclaration EnCours, lignes valorisées en état Proposee, écart d'équilibre ≠ 0
Traitement : recapSource / recapTaux agrègent le même ensemble (Integree || Proposee) que l'écart
Sortie  : à l'étape ②, dès qu'un écart est signalé, le tableau « Répartition par source » s'affiche
          sous le badge et permet de localiser d'où vient l'écart — avant intégration
```

## Livrables

- `DeclarationsController.cs` modifié : `recapSource` (l.236) et `recapTaux` (l.249) construits sur
  `Etat == Integree || Etat == Proposee` (aligné sur `l.234`/le contrôle d'équilibre).
- Test(s) dans `Declaration.Orchestration.Tests` (ou test d'API existant du checkup) : sur une
  déclaration aux lignes `Proposee` en écart, `recapSource` est **non vide** et sa somme
  HT/TVA/TTC correspond aux lignes `Proposee` valorisées.
- `VERIFY/TASK-103_verify.md` : capture de l'écran ② sur une déclaration **`EnCours`** en écart
  (cas réel `TVA1-2026-01`, 341 155,01 MAD) montrant le tableau « Répartition par source » affiché
  sous le badge ÉCART ; confirmation que ⑤ (déclaration clôturée) est inchangé.

## Critères de validation

- Sur une déclaration `EnCours` en écart, le tableau « Répartition par source » est **visible** à
  l'étape ② (défaut corrigé).
- La ventilation affichée couvre les lignes `Proposee` (les mêmes qui alimentent l'écart) — cohérence
  entre le montant de l'écart et le détail affiché.
- `displayTotalTVA` à l'étape ② reflète `recapSource` (plus de retombée systématique sur le repli).
- Écran ⑤ strictement inchangé (lignes `Integree` : ensemble identique avant/après).
- Le calcul du montant de l'écart est inchangé (non-régression sur le badge/valeur).

## Risques / dépendances

- Corrige un défaut d'alimentation de TASK-087 (déjà en DONE_DETAIL) — ne rouvre pas TASK-087, la
  complète côté back.
- Aucun risque de calcul : on élargit un ensemble d'**agrégation d'affichage** pour le faire
  coïncider avec l'ensemble déjà utilisé par le contrôle d'équilibre — pas de nouvelle règle métier.
- Vérifier qu'aucune autre consommation de `recapSource` / `recapTaux` (ex. `DeclarationFinalePanel`)
  ne dépende implicitement du fait qu'il ne contenait que des `Integree` — a priori non, ⑤ n'affiche
  que des déclarations clôturées.
