# TASK-221 — Seuil de montant DDP étendu jusqu'au 31/12/2025 (tout montant à partir du 01/01/2026)

## ⛔ Statut : BLOQUÉE — arbitrage PO requis avant tout code (analyse du 06/10/2026)
Une analyse approfondie (flux « legacy », 1 sur 9 abouti : les 8 autres ont échoué sur la limite de
session, aucun constat n'a été contre-vérifié) a relevé 4 points à trancher :
1. **Base légale de 2025-12-31 introuvable.** La note circulaire DGI n°734 (§O-2, p.10, citée par
   l'agent après lecture du PDF ; recoupée par des sources secondaires : indicac.ma, Valoris, cielmaroc.ma)
   exempte les factures **émises avant le 01/01/2025** de montant ≤ 10 000 DH TTC ; **dès le 01/01/2025,
   toutes les factures sont concernées, sans seuil**. Legacy (build 12.3.2 du 26/04/2026) et GRF actuel
   s'arrêtent à 2024-12-31. La règle PO (seuil jusqu'au 31/12/2025) n'est appuyée par aucun texte trouvé ;
   risque : sous-déclaration, amende de 5 000 MAD par facture manquante (NC 734 p.7). **Le PO a-t-il une
   référence postérieure (loi de finances, décret, note DGI) ?** Sinon, conserver 2024-12-31 (TASK sans objet).
2. **Borne 10 000.** La NC 734 exclut « inférieur **ou égal** » à 10 000 ; le legacy et GRF déclarent
   10 000,00 exactement (`>=`, `SelectionDelaiPaiementRepository.cs:114`, `SelectionDelaiPaiementCalculator.cs:32`).
   Les sources secondaires divergent (« n'atteint pas 10 000 »). À arbitrer (passer à `>` ?).
3. **Seuil par échéance ou par facture.** Le legacy et GRF testent `EC_Montant` de l'échéance ; la NC 734
   raisonne par facture TTC. Une facture de 12 000 en 2 × 6 000 est exclue par le code. Non chiffré sur
   les données (mesures non réalisées).
4. **Intitulé à corriger** : la date 2025-12-31 n'existe pas dans le legacy, c'est un changement de règle,
   pas une conservation du legacy.
Points annexes (hors TASK, à arbitrer séparément) : date de début de loi dépendant du chiffre d'affaires de
la société (01/07/2023 > 50 M, 01/01/2024 10-50 M, 01/01/2025 2-10 M) alors que GRF applique 2023-07-01 à
toutes ; avoirs/montants négatifs (passent sans seuil après la date limite, 50 échéances négatives déjà
déclarées d'après `RECAP_SESSION_QA_TVA_DELAI.md:110`) ; hypothèse non vérifiée : comparaison SQL
`DO_Date > @DateLimiteMontant` sans `.Date` si `DO_Date` porte une heure.
Non analysés (flux perdus) : parité SQL/C#, tests impactés (statique et exécution), mesures sur données
réelles, cycle de vie des déclarations existantes, balayage des littéraux/aides UI.

## Contexte
Décision PO (06/10/2026) : la loi a commencé le **01/07/2023** (inchangé). Règle d'éligibilité à la déclaration DDP :

| Date de facture (`DoDate`) | Déclarée ? |
|---|---|
| < 01/07/2023 | non (hors loi) |
| 01/07/2023 → 31/12/2025 | **uniquement si montant ≥ 10 000** (devise société) |
| ≥ 01/01/2026 | oui, **tous montants** |

La règle est **déjà implémentée** dans `SeuilsLegauxDelaiPaiement`
(`Declaration.Core/SelectionDelaiPaiementCalculator.cs:14-33`, legacy `dateDebDecLoi`/`dateLimiteMontant`).
Seule la date de fin du seuil diffère : `DateLimiteSeuilMontant = 2024-12-31` doit devenir **2025-12-31**.
`DateDebutDeclarationLoi` (2023-07-01) et `SeuilMontant` (10 000, borne incluse) **ne changent pas**.

Cumul avec TASK-220 (inchangé) : le filtre `DoDate < DateMiseEnRouteSociete` s'applique en plus.

## Objectif
```
Entrée  : DateLimiteSeuilMontant = 2024-12-31.
Traitement : DateLimiteSeuilMontant -> 2025-12-31.
Sortie  : factures 01/01/2025-31/12/2025 de montant < 10 000 exclues du contrôle DDP ;
          factures >= 01/01/2026 toutes incluses ; le reste inchangé.
```

## Périmètre STRICT
- **Inclus** :
  1. `SelectionDelaiPaiementCalculator.cs` : constante `DateLimiteSeuilMontant` + commentaires XML (l.19-20, l.26-27).
  2. `SelectionDelaiPaiementCalculatorTests.cs` (l.533 et alentours) : adapter/ajouter les tests (scénarios ci-dessous).
  3. Grep des littéraux `2024-12-31`/`10000` dupliqués (backend, front, e2e) ; aucun littéral SQL à introduire :
     la requête reçoit déjà les constantes (`SelectionDelaiPaiementService.cs:102-108`).
- **Exclus** : `DateDebutDeclarationLoi`, `EXERCICE_MIN` front (inchangés) ; paramétrage par société
  (constantes statutaires) ; TASK-220 ; schéma SQL.

## Étapes
1. Grep exhaustif (cf. périmètre 3).
2. Modifier la constante + commentaires.
3. Adapter les tests unitaires + ajouter les scénarios.
4. Preuve sur données réelles (`SO_Id=1`, outil lecture seule type `scratch/Ddp220Proof`) : nombre
   d'échéances candidates avant/après, dont le nombre d'échéances 2025 < 10 000 qui sortent.
5. `dotnet build DeclarationTVA.slnx` ; tests Core/Orchestration.

## Scénarios de test (`EstEligibleSeuilLegal`)
| DoDate | Montant | Attendu |
|---|---|---|
| 2023-06-30 | 1 000 000 | non (avant loi) |
| 2023-07-01 | 9 999,99 | non |
| 2023-07-01 | 10 000,00 | oui (borne incluse) |
| 2025-01-01 | 9 999,99 | non (était oui avant la TASK) |
| 2025-12-31 | 9 999,99 | non |
| 2025-12-31 | 10 000,00 | oui |
| 2026-01-01 | 0,01 | oui (tous montants) |
| 2025-03-15 | 50 000 avec `DoDate < DateMiseEnRouteSociete` | non (TASK-220) |
+ calculateur de bout en bout : facture 2025 à 5 000 -> aucune ligne ; facture 2026 à 5 000 -> ligne.

## Livrables
Constante, tests, `VERIFY/TASK-221_verify.md` (preuve chiffrée avant/après, checklist avec preuve datée par critère).

## Critères de validation
- Les 8 scénarios passent.
- `dotnet build` 0 erreur, tests verts.

## Risques / dépendances
- **Impact métier** : les échéances de 2025 inférieures à 10 000 sortent du contrôle (elles y étaient incluses).
  Chiffrer avant bascule en production.
- **Montant comparé** = montant de l'échéance (`RT_ECHEANCE.Montant`, devise société), pas le total
  facture : une facture >= 10 000 scindée en échéances < 10 000 serait exclue. Comportement legacy
  conservé ; **à confirmer par le PO**.
- Anti-double-déclaration : lignes déjà déclarées non affectées.
