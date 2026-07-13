# TASK-038 — Traçabilité TVA inline dans l'écran « Déclaration » (colonnes de preuve + preuve à la demande)

## Contexte
Objectif de fond du module = **regagner la confiance client par la transparence** (aucune ligne silencieuse ; cf. mémoire `grf-objectif-confiance-transparence`). Décision PO (09/07/2026) sur la traçabilité : **colonnes de preuve visibles en permanence** (scan au coup d'œil) **+ preuve détaillée à la demande** (`ProofModal` existant) — **sans panneau latéral** (mange de la largeur, contraire à « 0 espace perdu »).

Écran concerné : « Factures à déclarer » (`WorkstationPanel` → `DomainGrid`) côté entrée **Déclaration**.

## Périmètre STRICT
- **Inclus** : rendre visibles en colonnes les faits de valorisation TVA par ligne — **origine** (`EC_Type` : Sage/OM, FGR, Solde initial), **taux**, **motif d'écartement** — et conserver le détail OM/FGR au clic (`ProofModal`).
- **Exclu** : pas de panneau latéral permanent. Aucun changement du **calcul** TVA (TASK-030) ni des règles de sélection. Pas de refonte des actions de décision (Je déclare / Je ne déclare pas / Intégrer / Exclure / Reporter). Aucun couplage `gocom-web`.

## Cause / manque actuel
- La grille montre le statut et (après TASK-034) les montants/taux, mais **l'origine `EC_Type`** (Sage vs FGR vs solde) n'est **pas exposée** comme fait visible — or c'est LE discriminant de la manière dont la TVA a été lue (mémoire `grf-echeance-ectype-mapping`). Sans elle, le comptable ne peut pas juger la fiabilité d'une ligne d'un coup d'œil.

## Objectif
```
Entrée  : une déclaration valorisée
Sortie  : chaque ligne de « Factures à déclarer » montre en colonnes : origine (Sage/FGR/Solde),
          taux TVA, motif d'écartement — en plus des montants. Clic ligne → ProofModal (détail OM/FGR).
Effet   : transparence au coup d'œil ; aucune ligne dont l'origine/le motif reste caché.
```

## Étapes
1. **Exposer l'origine** au DTO de lignes : mapper `EC_Type` → libellé `origine` (Sage/OM | FGR | SoldeInitial). À **coordonner avec TASK-034** (même DTO) — idéalement livrer `origine` dans le même contrat.
2. `DomainGrid` : ajouter la colonne `origine` (filtre liste) et s'assurer que `taux` et `motif` sont visibles inline dans la vue « Factures ». Rester dense (largeurs maîtrisées, pas de scroll horizontal parasite hors grille).
3. Conserver le drill : clic ligne → `ProofModal` (détail ventilation OM/FGR) — **à la demande**, inchangé.
4. Pas de panneau latéral. Si la largeur sature, masquer les colonnes secondaires plutôt que d'introduire un panneau.

## Livrables
- Colonnes de preuve (origine/taux/motif) visibles dans « Factures à déclarer » + `ProofModal` conservé.
- `VERIFY/TASK-038_verify.md` : captures montrant l'origine `EC_Type` (Sage/FGR/Solde), le taux et le motif inline, et l'ouverture de la preuve détaillée au clic ; build `tsc + vite` + `oxlint` OK.

## Critères de validation
- Origine (`EC_Type`), taux et motif visibles en colonnes, sans clic.
- Preuve détaillée OM/FGR toujours accessible au clic (`ProofModal`).
- Aucun panneau latéral ; densité préservée ; pas de scroll horizontal hors grille.
- Aucun changement de calcul TVA / de sélection ; lecture seule sur ces champs.

## Risques / dépendances
- **Dépend de TASK-034** (contrat DTO) : `origine` doit être exposée proprement — livrer de préférence dans le **même** DTO pour éviter deux contrats.
- Indépendant de TASK-036/037 (autre écran) ; peut avancer en parallèle du rapprochement une fois 034 livré.
- Tenir la tension densité ↔ transparence : privilégier colonnes essentielles + preuve à la demande.
