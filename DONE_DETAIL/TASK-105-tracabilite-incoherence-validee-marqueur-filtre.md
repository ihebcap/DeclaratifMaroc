# TASK-105 — Traçabilité d'une incohérence validée : marqueur persistant + filtre (drill « Détail des affectations »)

> **Origine** : test PO 17/07/2026 sur `TVA1-2026-01`. Le PO a **validé une incohérence** (bouton
> « Valider l'incohérence », `AffectationsDrill.tsx`) puis n'a **plus aucun moyen de retrouver
> quelle facture** il a validée. Contredit le principe « aucune ligne silencieuse » : une validation
> d'incohérence est une **décision assumée** qui doit rester visible et auditable, pas disparaître.

## Contexte — cause identifiée

Le bandeau rouge, le surlignage et les boutons d'incohérence ne s'affichent que si la facture est
dans `facturesIncoherentes`, dérivé des **alertes actives** :

```ts
// AffectationsDrill.tsx:431 — alertes NON encore validées uniquement
const facturesIncoherentes = new Set(alertesIncoherence.map(a => a.refLigne));
```
```ts
// AffectationsDrill.tsx:581 — surlignage / bandeau conditionnés à cet ensemble
const estIncoherente = r.ecId > 0 && facturesIncoherentes.has(r.factureNumero);
```

Au clic sur **« Valider l'incohérence »** (`POST …/lignes/valider-incoherence`,
[l.386](../declaration-tva-web/src/AffectationsDrill.tsx#L386)), l'alerte est consommée côté back →
la facture **sort** de `facturesIncoherentes` → bandeau, surlignage et boutons **disparaissent
totalement**. Plus aucune trace à l'écran.

**Le paradoxe** : la donnée existe **déjà**. Chaque ligne porte `incoherenceValidee: boolean`
([l.52](../declaration-tva-web/src/AffectationsDrill.tsx#L52) /
[l.225](../declaration-tva-web/src/AffectationsDrill.tsx#L225)), remonté du back… mais ce flag n'est
**jamais affiché ni filtrable**. La décision est tracée en base (qui/quand — cf. le `title` du
bouton l.619), invisible dans l'UI.

## Périmètre STRICT

- **Inclus** :
  1. **Marqueur persistant** : sur une ligne/facture dont `incoherenceValidee === true`, afficher un
     repère durable (badge « Incohérence validée » ou état de conformité dédié), qui **reste** après
     validation — au lieu de la disparition actuelle. Réutiliser le flag déjà présent, aucune
     nouvelle source.
  2. **Filtre** permettant de **lister** les factures à incohérence validée (aligné sur le mécanisme
     de filtres existant de la grille, cf. `statutConformite` / les filtres de colonne).
  3. Si le back renvoie déjà **qui / quand** la validation a eu lieu, l'exposer au survol/dans le
     marqueur ; sinon, se limiter au marqueur + filtre (ne pas inventer la donnée).
- **Exclu** :
  - Toute modification du **calcul** (montants, TVA, écart d'équilibre) : le flag est déjà pris en
    compte back, la ligne et les totaux sont inchangés (« ligne et totaux inchangés », toast l.387).
  - La logique de **détection** d'incohérence (TASK-077/078) : inchangée. On rend seulement
    **visible** l'état déjà stocké.
  - L'annulation d'une validation (« dé-valider ») : hors périmètre — à traiter séparément si le PO
    le demande.

## Objectif

```
Entrée  : facture dont l'incohérence Sage a été validée (incoherenceValidee = true en base/DTO)
Rendu   : marqueur persistant « Incohérence validée » sur la ligne + filtre pour lister ces factures
Sortie  : le PO retrouve à tout moment quelles factures il a validées — décision assumée, auditable,
          conforme à « aucune ligne silencieuse »
```

## Livrables

- `AffectationsDrill.tsx` modifié : rendu du marqueur `incoherenceValidee` + option de filtre
  correspondante.
- Capture d'un cas réel : une facture après validation affichant son marqueur, et le filtre isolant
  les incohérences validées.
- `VERIFY/TASK-105_verify.md` : build front OK, capture avant/après validation montrant que la
  facture **reste identifiable** après le clic, confirmation que montants/total TVA sont inchangés.

## Critères de validation

- Après « Valider l'incohérence », la facture **reste visible/identifiable** via un marqueur (plus
  de disparition silencieuse).
- Un filtre permet de lister les factures à incohérence validée.
- Aucun changement de montants, de TVA déclarée, ni d'écart d'équilibre (rendu pur).
- Le workflow de validation existant (bouton, toast, appel API) est inchangé.

## Risques / dépendances

- Front-only sur données déjà exposées (`incoherenceValidee`). Vérifier que le flag est bien peuplé
  et rafraîchi après le `POST valider-incoherence` (rechargement du drill) — sinon, le marqueur
  n'apparaîtrait qu'au prochain chargement (à signaler, pas à corriger côté back sans demande).
- Vérifier si le back expose **qui/quand** : si non disponible dans le DTO actuel, se limiter au
  marqueur booléen (ne pas ouvrir un chantier back non demandé).
- Indépendante de TASK-104 (même écran, zones distinctes). Séquencer si livrées ensemble pour éviter
  un conflit d'édition sur `AffectationsDrill.tsx`.
