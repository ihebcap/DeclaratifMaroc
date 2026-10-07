# TASK-221 — Seuil de montant DDP étendu jusqu'au 31/12/2025 (tous montants dès le 01/01/2026)

RISK : HIGH (périmètre déclaratif légal) — discipline de preuve par critère exigée dans le VERIFY.

## Contexte
Décision PO (07/10/2026, résumé final et explicite) :

| Date de facture (`DoDate` = `RT_ECHEANCE.DO_Date`) | Déclarée ? |
|---|---|
| < 01/07/2023 | non (hors loi, inchangé) |
| 01/07/2023 → 31/12/2025 | **uniquement si montant ≥ 10 000** (devise société, borne incluse) |
| ≥ 01/01/2026 | oui, **tous montants** |

Trace de la décision : le PO a donné successivement plusieurs dates de début de loi (01/07/2024, 01/07/2023,
01/07/2024, 01/01/2023) puis a tranché par un résumé : **début de loi = 01/07/2023**, c'est-à-dire la valeur déjà
codée. Seul ce résumé fait foi. Le seuil (>= 10 000) et les dates 31/12/2025 / 01/01/2026 n'ont jamais varié.

La règle existe déjà dans `SeuilsLegauxDelaiPaiement` (`Declaration.Core/SelectionDelaiPaiementCalculator.cs:14-33`,
legacy `dateDebDecLoi`/`dateLimiteMontant`). **Une seule constante change** :
`DateLimiteSeuilMontant = 2024-12-31` -> **2025-12-31**. `DateDebutDeclarationLoi = 2023-07-01` et
`SeuilMontant = 10 000` (borne `>=` incluse) restent inchangés.

Cumul avec TASK-220 (inchangé) : `DoDate < DateMiseEnRouteSociete` => exclue en plus.

### Réserve légale à connaître (analyse du 06/10/2026, 1 flux sur 9 abouti, non contre-vérifié)
La note circulaire DGI n°734 (§O-2) exempte les factures **émises avant le 01/01/2025** de montant ≤ 10 000 DH TTC ;
dès le 01/01/2025, toutes les factures sont concernées. Le legacy déployé (build 12.3.2) arrête son seuil au
2024-12-31. Aucune référence légale ne soutient 2025-12-31 dans ce que l'analyse a trouvé ; le PO a maintenu sa
règle sans citer de texte. Le début de loi dépend aussi du CA de la société dans la NC 734 (01/07/2023 > 50 M,
01/01/2024 10-50 M, 01/01/2025 2-10 M), hors périmètre ici. **À confirmer par le PO avant mise en production**
(amende de 5 000 MAD par facture manquante ou inexacte, NC 734 p.7). Non bloquant pour l'implémentation :
c'est une constante nommée, facile à corriger.

## Objectif
```
Entrée  : DateLimiteSeuilMontant = 2024-12-31.
Traitement : DateLimiteSeuilMontant -> 2025-12-31.
Sortie  : factures 01/01/2025-31/12/2025 de montant < 10 000 exclues du contrôle DDP ;
          factures >= 01/01/2026 toutes incluses ; le reste inchangé.
```

## Périmètre STRICT
- **Inclus** :
  1. `SelectionDelaiPaiementCalculator.cs` : constante `DateLimiteSeuilMontant` + commentaires XML (l.19-20, 25-29).
     Aligner le vocabulaire : le montant testé est celui de l'**échéance** (`EC_Montant`), pas de la facture.
  2. `SelectionDelaiPaiementCalculatorTests.cs` (l.533 et alentours) + autres tests dépendant de la constante
     (grep `SeuilsLegauxDelaiPaiement`, fixtures datées 2025) : adapter et ajouter les scénarios ci-dessous.
  3. Grep exhaustif des littéraux/textes dupliqués (`2024-12-31`, `10000`, aides TASK-217,
     `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html`) : mettre à jour les textes vivants, pas l'historique `DONE_DETAIL`.
  4. Composante horaire : si `SELECT COUNT(*) FROM RT_ECHEANCE WHERE DO_Date <> CAST(DO_Date AS date)` est non
     nul, aligner le SQL (`SelectionDelaiPaiementRepository.cs:113-114`, `DO_Date > @DateLimiteMontant`) sur la
     troncature `.Date` du C# ; sinon ne rien changer et le noter dans le VERIFY.
- **Exclus** : `DateDebutDeclarationLoi`, `SeuilMontant`, `EXERCICE_MIN` front (inchangés) ; paramétrage par société ;
  date de début dépendant du CA ; avoirs/montants négatifs (cf. `RECAP_SESSION_QA_TVA_DELAI.md:110`) ; TASK-220 ;
  échéance légale ; schéma SQL.

## Étapes
1. Grep exhaustif (périmètre 3).
2. Modifier la constante et les commentaires.
3. Adapter/ajouter les tests (scénarios ci-dessous).
4. Mesures sur données réelles (`SO_Id=1`, lecture seule, outil type `scratch/Ddp220Proof`), avant/après :
   a. échéances candidates par tranche (< 07/2023 ; 07/2023-12/2024 ; 2025 en < / >= 10 000 ; >= 2026) ;
   b. échéances qui **sortent** (2025 < 10 000) : combien, dont combien restent après TASK-220, combien non
      soldées, combien ont déjà un historique dans `RT_DECLARATIONDELAISPAIEMENTLG` (plus aucun incrément) ;
   c. cas multi-échéances : documents dont le total est >= 10 000 mais dont au moins une échéance est < 10 000 ;
   d. lignes réellement produites pour la période du prochain dépôt, avant/après.
5. `dotnet build DeclarationTVA.slnx` ; tests Core/Orchestration/Export.

## Scénarios de test (`EstEligibleSeuilLegal`, DateMiseEnRoute non configurée sauf mention)
| DoDate | Montant | Attendu |
|---|---|---|
| 2023-06-30 | 1 000 000 | non (avant début de loi) |
| 2023-07-01 | 9 999,99 | non |
| 2023-07-01 | 10 000,00 | oui (borne incluse) |
| 2025-01-01 | 9 999,99 | non (était oui avant la TASK) |
| 2025-03-15 | 5 000 | non (était oui avant la TASK) |
| 2025-12-31 | 9 999,99 | non |
| 2025-12-31 | 10 000,00 | oui |
| 2026-01-01 | 0,01 | oui (tous montants) |
| 2024-09-01 | 50 000, `DateMiseEnRoute` = 2025-01-01 | non (TASK-220) |
| 2025-06-01 | -10 080 | non (négatif < 10 000 ; comportement existant) |
| 2026-02-01 | -10 080 | oui (tous montants ; comportement existant, hors périmètre) |
+ calculateur de bout en bout : facture 2025 à 5 000 -> aucune ligne ; facture 2026 à 5 000 -> une ligne.

## Livrables
Constante, tests, `VERIFY/TASK-221_verify.md` : mesures chiffrées (étape 4), checklist avec preuve datée par
critère (méthode + date), éventuelle correction SQL (périmètre 4) justifiée.

## Critères de validation
- Les scénarios ci-dessus passent ; aucun littéral `2024-12-31` résiduel hors historique.
- build .NET + tests : 0 erreur.
- Chiffres de l'étape 4 fournis au PO.

## Risques / dépendances
- **Impact métier** : les échéances 2025 < 10 000, incluses jusqu'ici, sortent du contrôle. Chiffrer avant la
  bascule (dépôt T3 2026). Aucune facture n'entre dans le périmètre.
- **Montant testé = échéance** (`RT_ECHEANCE.EC_Montant`, devise société), pas le total facture (comportement
  legacy). Une facture >= 10 000 scindée en échéances < 10 000 est exclue ; le PO parle de « montant facture » :
  le chiffrage 4c permet d'arbitrer (alternative : somme des échéances par `DO_Numero`/tiers).
- Réserve légale NC 734 ci-dessus (confirmation PO avant production).
- Anti-double-déclaration : lignes déjà déclarées non modifiées, mais plus alimentées si elles sortent du périmètre.
