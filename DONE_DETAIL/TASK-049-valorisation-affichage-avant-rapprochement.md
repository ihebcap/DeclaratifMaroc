# TASK-049 — Valoriser pour affichage les factures affectées mais non encore rapprochées

> **Origine :** FC2600909 (`EC_Id=24062`) affichée « Non valorisé — OM non lue » alors qu'elle est
> réglée (441,20). Constat DB : `MV_Point=0` (décaissement **non rapproché**) → motif `NonRapproche`
> → exclue de la sélection → jamais lue OM → 0 ligne cache. **Décision PO :** afficher la valeur
> TVA **dès qu'il y a un règlement**, indépendamment de `MV_Point`. La lecture OM (taux/base) est
> la même que la facture soit rapprochée ou non → la valeur est exacte, pas inventée.

## Périmètre (élargissement d'AFFICHAGE uniquement)

Élargir **la valorisation `RafraichirValorisationAsync`** (bouton Rafraîchir de l'écran Factures)
de `EstEligible` à **`Motif ∈ {Eligible, NonRapproche}`** :
- ✅ inclus : affecté + comptabilisé + non annulé + non impayé + `DT_Id` null, mais **pas encore
  rapproché** (`MV_Point ≠ 1`) ;
- ❌ toujours exclus : `NonAffecte` (pas de règlement), `Annule`, `Impaye`, `NonComptabilise`,
  `DejaDeclare`, `HorsPeriode`.

Ces factures sont lues OM et écrites en cache **brut** (`Token_MV_Id NULL`, mécanique du correctif
cache récent) → l'écran Factures affiche leur HT/TVA au lieu de « OM non lue ».

## Garde-fous (non négociables)

1. **La déclaration reste stricte.** On ne touche **que** `RafraichirValorisationAsync`.
   `RecalculerLignesCandidatesAsync` (génération des lignes de déclaration) **garde `EstEligible`**
   → une facture non rapprochée n'entre **jamais** dans une déclaration.
2. **Cache non déclarable.** Une ligne à `Token_MV_Id NULL` n'est **jamais** servie comme
   ventilation déclarable (`TryServireDepuisCache` retourne null sans paiement courant) — déjà en
   place. Aucune fuite possible vers une déclaration.
3. **Affichage honnête.** L'écran montre la TVA (famille B) **et** le statut famille C
   `NonDeclarable` + `Reste à déclarer = montant plein` (dérivé `Déclaré=0`, existant) → l'utilisateur
   voit la valeur ET qu'elle n'est pas déclarée. Aucun libellé ne laisse croire au déclarable.

## Objectif
```
Entrée  : société + période (bornée, écran Factures)
Change  : RafraichirValorisationAsync sélectionne EstValorisable (Eligible OU NonRapproche)
          au lieu de EstEligible
Effet   : factures affectées non rapprochées → lues OM → cache brut (token NULL) → famille B affichée
Invariant: déclaration inchangée (EstEligible), aucune ligne token-NULL déclarée
```

## Livrables
- `AffectationCandidate.EstValorisable` = `Motif ∈ {Eligible, NonRapproche}`.
- `RafraichirValorisationAsync` : prédicat `EstValorisable` (Recalculer/déclaration inchangés).
- Test : `NonRapproche` → `EstValorisable` vrai ; `Annule`/`Impaye`/`NonAffecte`/`DejaDeclare` → faux.
- `VERIFY/TASK-049_verify.md` : preuve réelle — FC2600909 passe de 0 ligne cache à valorisée
  (token NULL), statut reste `NonDeclarable`, et **non-régression déclaration** (comptage lignes
  déclarées inchangé).

## Critères de validation
- Factures affectées non rapprochées : famille B valorisée (cache token NULL), statut `NonDeclarable`.
- Déclaration : aucune facture non rapprochée déclarée (EstEligible inchangé).
- Aucune ligne token-NULL servie comme déclarable.
- Lecture seule (Sage OM + cache persistance) ; aucun `DT_Id` écrit (TASK-028).
- Build + tests verts.

## Coût assumé
Plus de lectures OM (les non-rapprochées aussi) — une fois, puis en cache (session réutilisée
TASK-023). Acceptable, décision PO.

## Dépendances
- S'appuie sur le correctif cache token-NULL (déjà livré).
- Indépendant de TASK-045/046/047 (lecture OM) et TASK-048 (ICE/IF).
