# TASK-221 — Seuil de montant DDP : comparer le total TTC de la facture (et non l'échéance) et aligner la borne sur la note DGI 734 (`> 10 000`)

RISK : HIGH (périmètre déclaratif légal ; des lignes peuvent entrer dans le contrôle) — discipline de preuve par critère exigée dans le VERIFY.

## Contexte
Décisions PO : 07/10/2026 « appliquer les règles de la DGI » (l'extension du seuil jusqu'au 31/12/2025, envisagée avant lecture de la
source, est abandonnée) ; 08/10/2026 « oui facture TTC » (le montant comparé au seuil est celui de la **facture TTC**).

Règle de la note circulaire DGI n°734, §N et §O-2 (PDF `finances.gov.ma/Publication/dgi/2023/note-circulaire734.PDF`, p.9-10, relu le
07/10/2026) :
- la loi 69-21 s'applique aux factures émises à compter du **01/07/2023** ;
- l'amende ne s'applique pas aux « factures émises avant le 1er janvier 2025 dont le montant est **inférieur ou égal** à dix mille
  (10.000) dirhams, **toutes taxes comprises** » ; ces factures « ne doivent pas être intégrées dans les déclarations des délais de
  paiement au titre des années 2023 et 2024 » ;
- aucune exemption après cette date : toutes les factures émises à partir du 01/01/2025 sont déclarées.
- Confirmé par l'annonce DGI du 21/03/2025 (finances.gov.ma, `detail-actualite.aspx?fiche=7218`) : « l'amende pécuniaire s'applique à
  toutes les factures émises à compter du 1er janvier 2025, y compris celles dont le montant est inférieur ou égal à 10 000 dirhams ».
  Le régime annuel (CA <= 50 M, transitoire 2024-2025) et le régime trimestriel (CA > 50 M) ne changent pas cette règle : l'exemption
  dépend de la **date d'émission de la facture**, pas du type de déclaration.

État du code : les **dates sont déjà conformes** (`DateDebutDeclarationLoi = 2023-07-01`, `DateLimiteSeuilMontant = 2024-12-31`,
`Declaration.Core/SelectionDelaiPaiementCalculator.cs:17,20`) : aucune ne change. **Deux écarts** :
1. **Borne** : le code déclare un montant égal à 10 000 (`montant >= SeuilMontant`, `SelectionDelaiPaiementCalculator.cs:32` ; SQL
   `EC_Montant >= @SeuilMontant`, `SelectionDelaiPaiementRepository.cs:114`), la note l'exclut (« inférieur ou égal »).
2. **Montant testé** : c'est `EC_Montant` de l'**échéance** (`SelectionDelaiPaiementRepository.cs:100,114` ;
   `SelectionDelaiPaiementCalculator.cs:351`, `echeance.Montant`), alors que la note vise le montant de la **facture TTC**. Une facture de
   12 000 TTC en 2 échéances de 6 000 est exclue à tort ; la colonne « Montant » de l'écran Contrôle (montant de la ligne : part affectée
   ou solde, `SelectionDelaiPaiementCalculator.cs:518`) ne permet pas de vérifier le seuil (confusion constatée chez un client le 07/10/2026).
Les deux écarts viennent du legacy (`LigneControleDelaisPaiementController.cs:76`), reproduit à l'identique par TASK-131.

### Définition retenue du « total TTC de la facture »
`MontantFactureTtc` = **somme des `EC_Montant` de toutes les échéances du même document** : même `SO_Id`, même `DO_Domaine` (achat) et
même `DO_Numero`, avec les mêmes filtres que la sélection (`EC_Type` hors gain/perte de change, `DE_Id` = devise société), **quel que soit
`EC_Etat`** (échéances payées incluses : le total ne dépend pas de l'avancement des règlements). Raisons : c'est la définition que le legacy
utilise déjà côté TVA (`DeclarationTvaController.cs:856`, `DocumentMontantDeviseSociete = echeancesCollection.Sum(x => x.Montant)`) ;
elle reste dans la base des échéances (les factures Sage sont dans une autre base, `F_DOCENTETE` via `GetSageConnectionInfoAsync`), en
cohérence avec la direction PO d'indépendance vis-à-vis de Sage. **Alternative écartée par défaut** : lire `F_DOCENTETE.DO_TotalTTC` par lots de
`DO_Piece` (pattern de TASK-191) ; à n'envisager que si la mesure de l'étape 3a montre que la somme des échéances diffère du total TTC Sage
(dans ce cas : **STOP et signaler au PO**, ne pas improviser).

## Objectif
```
Entrée  : facture émise entre le 01/07/2023 et le 31/12/2024 (exemption <= 10 000 TTC) ; seuil testé aujourd'hui sur l'échéance, borne >=.
Traitement : MontantFactureTtc (somme des échéances du document) comparé au seuil avec borne > ; dates inchangées.
Sortie  : une facture n'est retenue que si son total TTC est > 10 000 (avant 2025) ; toutes ses échéances entrent alors ensemble ;
          dès 2025 aucune exemption ; l'écran et l'export montrent le total TTC de la facture.
```

## Périmètre STRICT
- **Inclus** :
  1. SQL `SelectionDelaiPaiementRepository.GetEcheancesCandidatesAsync` : calculer `MontantFactureTtc` par somme fenêtrée sur la
     population filtrée (SO_Id, DO_Domaine achat, EC_Type hors gain/perte, DE_Id devise société), **avant** d'appliquer le seuil, puis
     `(DO_Date > @DateLimiteMontant OR MontantFactureTtc > @SeuilMontant)` ; renvoyer la colonne. Vérifier que `DO_Date` est identique pour
     toutes les échéances d'un même document (sinon la fenêtre doit porter sur le document entier avant le filtre de date).
  2. `EcheanceDelaiPaiement` (+ `MontantFactureTtc`), `SeuilsLegauxDelaiPaiement.EstEligibleSeuilLegal(dateDocument, montantFactureTtc)`
     avec `montant > SeuilMontant`, réapplication défensive du calculateur (`SelectionDelaiPaiementCalculator.cs:351`) sur
     `MontantFactureTtc` ; commentaires XML alignés (note DGI 734 §O-2, « total TTC de la facture »).
  3. Propagation jusqu'à l'écran : `LigneSelectionDelaiPaiement`/DTO exposent `MontantFactureTtc` ; colonne « Montant facture TTC » ajoutée
     à la grille de l'écran Contrôle (`ControleLignesDelaiPaiementPanel.tsx`, via `ApbsGrid`) et à l'export Excel ; la colonne « Montant »
     existante est renommée « Montant de la ligne » (aucune ambiguïté). Conformité `DOCS/UI_STANDARDS.md` (lire et appliquer).
  4. Tests (`SelectionDelaiPaiementCalculatorTests.cs:534,542` et autres dépendances de la signature) + scénarios ci-dessous.
  5. Grep des textes d'aide/UI/docs mentionnant « >= 10 000 », « 10 000 et plus » ou « montant de l'échéance » : mise à jour des textes vivants.
  6. Composante horaire : si `SELECT COUNT(*) FROM RT_ECHEANCE WHERE DO_Date <> CAST(DO_Date AS date)` est non nul, aligner
     `DO_Date > @DateLimiteMontant` sur la troncature `.Date` du C#; sinon le noter dans le VERIFY.
- **Exclus** : toutes les dates ; TASK-220 ; devise étrangère (déjà exclue par `DE_Id`) ; avoirs/montants négatifs (comportement existant :
  un total négatif est <= seuil) ; lecture de la base Sage (sauf décision PO après STOP) ; schéma SQL ; réglage par société (TASK-222).

## Étapes
1. Lire `DOCS/UI_STANDARDS.md`, le repository de sélection, le calculateur, la grille de l'écran Contrôle.
2. Implémenter SQL + entité + règle (C#) ; tests.
3. Mesures sur données réelles (`SO_Id=1`, lecture seule), avant/après :
   a. **Contrôle de définition** : pour les documents d'achat (devise société), comparer `SUM(EC_Montant)` du document à
      `F_DOCENTETE.DO_TotalTTC` (base Sage, jointure `DO_Piece = DO_Numero`, `DO_Domaine = 1`) : nombre et causes des écarts. Écart non
      négligeable ⇒ STOP, signaler au PO.
   b. Impact multi-échéances : documents dont le total est > 10 000 mais dont au moins une échéance est <= 10 000 (échéances qui **entrent**),
      par tranche de dates ; montants, dépassement cumulé que produirait leur première déclaration (borne = échéance légale), nombre qui
      restent après TASK-220.
   c. Impact de la borne : échéances/documents dont le total vaut exactement 10 000 (qui sortent).
   d. Lignes réellement produites pour la période du prochain dépôt, avant/après.
   e. Résultat de la requête de composante horaire ; temps d'exécution de la requête de candidats avant/après.
4. `dotnet build DeclarationTVA.slnx` ; tests Core/Orchestration/Export ; `npm run lint` + `npm run build` ; checklist UI de `DOCS/UI_STANDARDS.md`.

## Scénarios de test (`EstEligibleSeuilLegal` + parité SQL/C#, DateMiseEnRoute non configurée sauf mention)
| # | Document (échéances `EC_Montant`) | DoDate | Attendu pour chaque échéance |
|---|---|---|---|
| 1 | 1 × 10 000,00 | 2024-06-01 | non (borne exclue) |
| 2 | 1 × 10 000,01 | 2024-06-01 | oui |
| 3 | 6 000 + 6 000 (total 12 000) | 2024-06-01 | oui, les deux (était non) |
| 4 | 5 000 + 5 000 (total 10 000,00) | 2024-06-01 | non |
| 5 | 4 500 + 4 500 (total 9 000) | 2024-06-01 | non |
| 6 | 3 échéances (total 12 000), dont une payée | 2024-06-01 | oui (le total inclut l'échéance payée) |
| 7 | 1 × 12 000 payée en partie (solde 3 000) | 2024-06-01 | oui (le seuil porte sur le total, pas le solde) |
| 8 | 1 × 1 000 000 | 2023-06-30 | non (avant la loi) |
| 9 | 1 × 800 | 2025-01-01 | oui (aucune exemption dès 2025) |
| 10 | 400 + 400 | 2025-04-23 | oui |
| 11 | 12 000 TTC + une échéance gain/perte de change (exclue du total) | 2024-06-01 | oui ; le total ignore l'échéance gain/perte |
| 12 | 1 × -12 000 (avoir) | 2024-06-01 | non (total négatif <= seuil ; comportement existant) |
| 13 | 1 × 9 000 | 2024-12-31 à 14:00 | non (jamais traité comme postérieur à la limite) |
| 14 | 1 × 50 000, `DateMiseEnRoute` = 2025-01-01 | 2024-09-01 | non (TASK-220) |
+ propriétés : SQL et C# donnent le même résultat sur ces 14 cas ; la colonne « Montant facture TTC » de l'écran/export égale la somme des
échéances ; deux échéances d'un même document reçoivent toujours le même verdict d'éligibilité.

## Livrables
Code C# + SQL + front + tests, `VERIFY/TASK-221_verify.md` : mesures de l'étape 3, checklist avec preuve datée par critère (méthode + date),
captures de la grille et de l'export avec la colonne « Montant facture TTC ».

## Critères de validation
- Les 14 scénarios passent ; aucun `>= SeuilMontant` ni test sur `EC_Montant` seul résiduel (C# comme SQL).
- Mesure 3a fournie et sans écart bloquant (sinon STOP).
- build .NET + tests + lint/build front : 0 erreur ; checklist UI cochée ; chiffres de l'étape 3 fournis au PO.

## Risques / dépendances
- **Des lignes entrent** : les échéances de factures > 10 000 TTC scindées en échéances <= 10 000 n'étaient jamais sélectionnées ; elles le
  seront, avec une première déclaration à cumul de retard potentiellement important (borne = échéance légale) : à chiffrer (étape 3b) avant la
  bascule (dépôt T3 2026). Seules les factures dont le total vaut exactement 10 000 (avant 2025) sortent.
- **Définition du total TTC** : somme des échéances, supposée égale au total TTC Sage ; vérifiée à l'étape 3a (STOP si écart). Cas à surveiller :
  retenues de garantie, escomptes, acomptes, échéances d'un autre type que gain/perte, `DO_Numero` non unique entre types de pièces.
- **Performance** : somme fenêtrée sur l'ensemble des échéances d'achat de la société ; à mesurer (étape 3e).
- Anti-double-déclaration : lignes déjà déclarées non modifiées ; les échéances qui entrent sont traitées comme « première déclaration ».
- TASK-222 (réglage par société) dépend de cette TASK : même méthode `EstEligibleSeuilLegal`, même champ `MontantFactureTtc`.
