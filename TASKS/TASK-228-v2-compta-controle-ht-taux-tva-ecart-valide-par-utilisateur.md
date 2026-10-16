# TASK-228 — V2 compta : contrôle HT × taux = TVA, écart affiché, validation par l'utilisateur

Status: 🆕 à faire
Priority: HIGH
Risk: HIGH
Module: Declaration.Compta.Core + Declaration.Compta.Persistence + Declaration.API + declaration-tva-web
Dépend de : TASK-225, TASK-226 ; **front = TASK-231 + TASK-232** (charte xGR et XGrid, arbitrage PO 16/10/2026)

> **Origine :** PO 16/10/2026 : « on s'assure que HT × taux = TVA ; on affiche l'écart et l'utilisateur valide, c'est sa responsabilité ».

## Décisions de conception

1. **Contrôle par facture et par taux** : `TVA_attendue = round(HT_taux × taux / 100, n décimales société, AwayFromZero)` ; `écart = TVA_constatée − TVA_attendue`. `n` = décimales devise (même source que V1, TASK-003).
2. **Aucune tolérance** : tout écart ≠ 0 est affiché, jamais masqué (PO). Il n'y a pas de seuil auto-validant.
3. Écart non validé = **bloquant la clôture** de la déclaration (cohérent avec le garde-fou « anomalies bloquantes » V1).
4. **Validation** = action explicite de l'utilisateur, persistée dans `EcartValide` (EF) : facture (`cbMarq` ligne tiers), taux, HT/TVA constatée/TVA attendue **figés**, utilisateur, date, commentaire facultatif.
5. **Invalidation automatique** : si les montants Sage changent après validation (HT/TVA différents des montants figés), la validation devient caduque et l'écart redevient bloquant (visible, traçable).
6. Le montant déclaré reste la **TVA constatée en compta** ; la validation n'altère aucun montant.
7. Réutiliser le patron UX « Valider l'incohérence » (TASK-078) ; ne pas réinventer. Validation en masse autorisée, avec confirmation et liste des factures concernées.
8. S'applique aux deux sens (déductible et collectée).

## Périmètre STRICT

- **Inclus** : calcul pur du contrôle, entité + repository `EcartValide`, endpoints (liste des écarts d'une période, valider un/plusieurs, annuler une validation), écran grille (**XGrid**, TASK-232 ; `ApbsGrid` interdit) avec colonnes HT, taux, TVA constatée, TVA attendue, écart, statut, bouton valider.
- **Exclu** : correction du montant (hors périmètre ; V1 TASK-206 non repris ici), comptes de taxe sans tiers (TASK-229).

## Livrables

- Contrôle + persistance + API + écran + tests.

## Critères de validation

- Pièce D20 290,80 / 58,16 → aucun écart ; pièce construite avec TVA 58,00 → écart −0,16 affiché, bloquant.
- Valider l'écart le débloque ; modifier le montant dans la fixture Sage le rebloque (invalidation prouvée).
- Validation conservée après redémarrage de l'API et après relance du calcul (idempotence).
- Un utilisateur non habilité ne peut pas valider (même garde que `UT_Admin`/droits déjà en place — à lire avant code).
- Build + lint + build front 0 erreur ; **checklist UI de `DOCS/UI_STANDARDS.md` cochée** ; grille via XGrid, KpiCard/tags de la charte xGR ; preuve par critère datée.

## Files

- `Declaration.Compta.Core/ControleCoherenceTva.cs`, `Declaration.Compta.Persistence/`, `Declaration.API/Controllers/`, `declaration-tva-web/src/` (lire `DOCS/UI_STANDARDS.md`).
- Patron : TASK-078 (`IncoherenceValidee`), `DeclarationWorkflowService.cs`.
