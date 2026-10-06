# TASK-221 — Seuils légaux DDP : début de la loi au 01/07/2024, seuil 10 000 jusqu'au 31/12/2025 (tout montant à partir du 01/01/2026)

## Contexte
Décision PO (06/10/2026) : la loi a commencé le **01/07/2024**. Règle d'éligibilité à la déclaration DDP :

| Date de facture (`DoDate`) | Déclarée ? |
|---|---|
| < 01/07/2024 | non (hors loi) |
| 01/07/2024 → 31/12/2025 | **uniquement si montant ≥ 10 000** (devise société) |
| ≥ 01/01/2026 | oui, **tous montants** |

Cette règle est **déjà implémentée** dans `SeuilsLegauxDelaiPaiement`
(`Declaration.Core/SelectionDelaiPaiementCalculator.cs:14-33`, reproduction du legacy
`dateDebDecLoi` / `dateLimiteMontant`), mais avec des **dates différentes** :
`DateDebutDeclarationLoi = 2023-07-01` et `DateLimiteSeuilMontant = 2024-12-31`.
La TASK consiste donc à **changer 2 constantes** et ce qui en dépend (tests, commentaires, UI).

Cumul avec TASK-220 (inchangé) : le filtre `DoDate < DateMiseEnRouteSociete` s'applique en plus.
Une facture est déclarable si elle passe les deux filtres.

## Objectif
```
Entrée  : constantes légales 2023-07-01 / 2024-12-31 / 10 000.
Traitement : DateDebutDeclarationLoi -> 2024-07-01 ; DateLimiteSeuilMontant -> 2025-12-31 ;
             SeuilMontant inchangé (10 000, borne incluse : montant >= 10 000 déclaré).
Sortie  : écran Contrôle DDP : factures < 01/07/2024 absentes ; factures 01/07/2024-31/12/2025
          présentes seulement si >= 10 000 ; factures >= 01/01/2026 toutes présentes.
```

## Périmètre STRICT
- **Inclus** :
  1. `SelectionDelaiPaiementCalculator.cs` : les 2 constantes + commentaires XML (l.17, l.20, l.26-27).
  2. `SelectionDelaiPaiementCalculatorTests.cs` (l.533 et alentours) : adapter/ajouter les tests (cf. scénarios).
  3. Front : `EXERCICE_MIN` dans `ControleLignesDelaiPaiementPanel.tsx:34` et
     `DeclarationsDelaiPaiementPanel.tsx:320` (2023 -> 2024) + commentaires. Vérifier qu'aucun autre
     littéral de date/seuil n'est dupliqué (grep `2023`, `10000`, `10_000`, `10 000`).
  4. Point UNIQUE de vérité conservé : la requête SQL (`GetEcheancesCandidatesAsync`) reçoit déjà les
     constantes en paramètres (`SelectionDelaiPaiementService.cs:102-108`) — ne pas introduire de littéral SQL.
- **Exclus** : paramétrage par société (constantes statutaires, cf. en-tête de la classe) ; TASK-220 ;
  calcul de l'échéance légale ; schéma SQL (aucune modification).

## Étapes
1. Grep exhaustif des littéraux `2023`/`2024-12-31`/`10000` (backend, front, tests, e2e Playwright).
2. Modifier les 2 constantes + commentaires.
3. Adapter les tests unitaires + ajouter les scénarios ci-dessous.
4. Mettre à jour `EXERCICE_MIN` côté front.
5. Preuve sur données réelles (`SO_Id=1`, outil lecture seule type `scratch/Ddp220Proof`) : nombre
   d'échéances candidates avant/après (ventilé : < 01/07/2024, fenêtre transitoire, >= 2026).
6. `dotnet build DeclarationTVA.slnx` ; tests Core/Orchestration ; `npm run lint` + `npm run build`.

## Scénarios de test (`EstEligibleSeuilLegal`)
| DoDate | Montant | Attendu |
|---|---|---|
| 2024-06-30 | 1 000 000 | non (avant loi) |
| 2024-07-01 | 9 999,99 | non |
| 2024-07-01 | 10 000,00 | oui (borne incluse) |
| 2025-12-31 | 9 999,99 | non |
| 2025-12-31 | 10 000,00 | oui |
| 2026-01-01 | 1,00 | oui (tous montants) |
| 2026-01-01 | 0,01 | oui |
| 2025-03-15 | 50 000 mais `DoDate < DateMiseEnRouteSociete` | non (TASK-220) |
+ test calculateur de bout en bout : facture 2025 à 5 000 -> aucune ligne ; facture 2026 à 5 000 -> ligne.

## Livrables
Constantes, tests, front, `VERIFY/TASK-221_verify.md` (preuve chiffrée avant/après, checklist avec preuve datée par critère).

## Critères de validation
- Les 8 scénarios ci-dessus passent.
- Aucun littéral `2023-07-01`/`2024-12-31` résiduel hors tests historiques justifiés.
- build .NET + lint/build front : 0 erreur.

## Risques / dépendances
- **Impact métier** : les factures 07/2023 -> 06/2024 sortent du contrôle ; les factures < 10 000 de 2025
  y **entrent pas** (déjà exclues) mais celles de 2025 (jusqu'au 31/12) restent filtrées alors qu'elles
  ne l'étaient plus depuis 01/01/2025 : **des lignes < 10 000 de 2025 disparaissent**. Chiffrer avant prod.
- **Montant comparé** = `RT_ECHEANCE.Montant` de l'échéance (devise société), pas le total facture :
  une facture >= 10 000 scindée en plusieurs échéances < 10 000 serait exclue. Comportement legacy
  conservé ; **à confirmer par le PO** (alternative : comparer le total facture).
- Anti-double-déclaration : des lignes déjà déclarées (historique `DernieresBornesDeclarees`) ne sont pas affectées.
