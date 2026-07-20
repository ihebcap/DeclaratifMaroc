# TASK-104 — Payé / TTC / Prorata affichés une seule fois par facture (drill « Détail des affectations »)

> **Origine** : test PO 17/07/2026 sur `TVA1-2026-01`, écran « Détail des affectations » (drill
> règlement→factures, `AffectationsDrill.tsx`). La grille est **1 ligne par (facture × taux)**, mais
> les colonnes **Payé**, **TTC** et **Prorata** sont des valeurs **facture-level** répétées à
> l'identique sur chaque ligne de taux. Ex. `FA2502941` affiche `27 170,50 MAD` en Payé **et** en TTC
> sur ses 4 lignes de taux (10/0/9/20 %) → visuellement, on croit à 4 paiements de 27 170,50.

## Contexte — cause identifiée

`buildGridRows` ([AffectationsDrill.tsx:216-224](../declaration-tva-web/src/AffectationsDrill.tsx#L216))
projette une ligne par taux mais recopie les montants facture sur chacune :

```ts
const ttc = inverse(f.montantAffecte, f.prorata);   // facture-level, calculé 1×
f.lignesTaux.forEach(l => {
  rows.push({
    …,
    tauxTVA: l.tauxTVA, prorata: f.prorata, paye: f.montantAffecte, ttc,   // ← répétés
    baseTva: inverse(l.montantTVA, l.prorata), tva: l.montantTVA,          // ← propres au taux
  });
});
```

| Colonne | Granularité réelle | Rendu actuel |
|---|---|---|
| Payé (`f.montantAffecte`) | **facture** | répété sur chaque taux |
| TTC (`inverse(montantAffecte, prorata)`) | **facture** | répété sur chaque taux |
| Prorata (`f.prorata`) | **facture** | répété sur chaque taux |
| Base TVA / TVA déclarée | **taux** | propre à la ligne (sommable) |

**Ce qui n'est pas cassé** : le seul total de pied est **TVA déclarée** (`filteredTotalTva`,
[l.446](../declaration-tva-web/src/AffectationsDrill.tsx#L446)/[l.642](../declaration-tva-web/src/AffectationsDrill.tsx#L642)),
somme de valeurs **par taux** → correct, aucun quadruple comptage. Le calcul est sain ; le problème
est de **lisibilité** et de **risque d'usage** (un utilisateur qui trie/filtre/exporte la colonne
Payé ou TTC re-somme mécaniquement des montants répétés → faux total facture × nb de taux).

## Périmètre STRICT

- **Inclus** :
  1. N'afficher **Payé / TTC / Prorata qu'une seule fois par facture** — sur la **première ligne**
     du groupe (facture), cellules **vides** (ou visuellement rattachées) sur les lignes de taux
     suivantes du même groupe. Le repère de groupe existe déjà :
     `derniereLigneDuGroupe` / la clé `(numeroReglement, factureNumero)`
     ([l.582-583](../declaration-tva-web/src/AffectationsDrill.tsx#L582)) — réutiliser le même
     découpage pour détecter la **première** ligne du groupe.
  2. Conserver l'alignement, les filtres et le tri existants sans régression sur les colonnes
     par-taux (Taux / Base TVA / TVA déclarée inchangées).
- **Exclu** :
  - Tout changement de **calcul** (montants, prorata, inversion `inverse()`, total TVA) — pur
    rendu.
  - Le back / les DTO : aucune donnée nouvelle nécessaire (regroupement déjà présent côté front via
    `groupByFacture`).
  - Les lignes **non valorisées** (`nonValorise`) : déjà rendues en ligne unique par facture
    (pas de multi-taux) — comportement inchangé.

## Objectif

```
Entrée  : facture réglée, ventilée sur N taux → N lignes dans la grille
Rendu   : Payé / TTC / Prorata affichés sur la 1re ligne du groupe uniquement ; vides ensuite
Sortie  : lecture sans ambiguïté « 1 facture (payé/TTC/prorata) → sa ventilation par taux » ;
          plus aucun montant facture répété, donc plus de risque de re-somme erronée
```

## Livrables

- `AffectationsDrill.tsx` modifié : rendu conditionnel des cellules Payé/TTC/Prorata selon
  « première ligne du groupe facture » ; groupes visuellement lisibles.
- Capture avant/après du drill sur un cas multi-taux réel (`FA2502941`, 4 taux) montrant Payé/TTC
  affichés une seule fois.
- `VERIFY/TASK-104_verify.md` : build front OK (tsc+vite / lint), capture du cas réel, confirmation
  que le total TVA déclarée est inchangé (non-régression calcul).

## Critères de validation

- Payé / TTC / Prorata n'apparaissent **qu'une fois par facture** dans le drill.
- Base TVA / TVA déclarée restent affichées **par taux** (inchangé).
- Total « TVA déclarée » de pied inchangé (aucune régression de calcul).
- Filtres/tri de la grille toujours fonctionnels.

## Risques / dépendances

- Front-only, aucun risque de calcul. Vérifier le rendu quand un **filtre** ne laisse qu'une partie
  des lignes d'un groupe (la « première ligne du groupe » se recalcule sur `filteredRows`, pas sur
  l'ensemble brut) — repartir de la même logique que `derniereLigneDuGroupe`.
- Indépendante de TASK-105 (même écran, zones distinctes : colonnes de montants vs marqueur
  d'incohérence). Séquencer si livrées ensemble pour éviter un conflit d'édition sur
  `AffectationsDrill.tsx`.
