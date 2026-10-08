# TASK-221 — Aligner la borne du seuil de montant DDP sur la note DGI 734 (« > 10 000 » au lieu de « >= 10 000 »)

RISK : MEDIUM (périmètre déclaratif légal, impact chiffré faible attendu).

## Contexte
Décision PO (07/10/2026) : **appliquer les règles de la DGI**. L'extension du seuil jusqu'au 31/12/2025, envisagée
avant lecture de la source, est **abandonnée** (aucun texte ne la soutient).

Règle de la note circulaire DGI n°734, §N et §O-2 (PDF `finances.gov.ma/Publication/dgi/2023/note-circulaire734.PDF`, p.9-10,
relu le 07/10/2026) :
- la loi 69-21 s'applique aux factures émises à compter du **01/07/2023** ;
- l'amende ne s'applique pas aux « factures émises avant le 1er janvier 2025 dont le montant est **inférieur ou égal**
  à dix mille (10.000) dirhams, toutes taxes comprises » ; ces factures « ne doivent pas être intégrées dans les
  déclarations des délais de paiement au titre des années 2023 et 2024 » ;
- aucune exemption après cette date : toutes les factures émises à partir du 01/01/2025 sont déclarées.
- Confirmé par l'annonce DGI du 21/03/2025 (finances.gov.ma, `detail-actualite.aspx?fiche=7218`) : « l'amende pécuniaire
  s'applique à toutes les factures émises à compter du 1er janvier 2025, y compris celles dont le montant est inférieur ou
  égal à 10 000 dirhams ». Le régime annuel (CA <= 50 M, transitoire 2024-2025) et le régime trimestriel (CA > 50 M) ne
  changent pas cette règle : l'exemption dépend de la **date d'émission de la facture**, pas du type de déclaration.

État du code : les **dates sont déjà conformes** (`DateDebutDeclarationLoi = 2023-07-01`, `DateLimiteSeuilMontant = 2024-12-31`,
`Declaration.Core/SelectionDelaiPaiementCalculator.cs:17,20`) : aucune ne change. **Écart unique** : la borne. Le code déclare
un montant égal à 10 000 (`montant >= SeuilMontant`, `SelectionDelaiPaiementCalculator.cs:32` ; SQL
`EC_Montant >= @SeuilMontant`, `SelectionDelaiPaiementRepository.cs:114`), alors que la note l'exclut (« inférieur ou égal »).
Le legacy avait le même `>=` (reproduit à l'identique par TASK-131).

## Objectif
```
Entrée  : facture émise entre le 01/07/2023 et le 31/12/2024 avec un montant exactement égal à 10 000,00.
Traitement : borne du seuil passée de >= à > (C# et SQL).
Sortie  : cette facture n'est plus sélectionnée ; 10 000,01 l'est ; rien ne change pour les dates ni après 2024.
```

## Périmètre STRICT
- **Inclus** :
  1. `SeuilsLegauxDelaiPaiement.EstEligibleSeuilLegal` : `montant > SeuilMontant` ; commentaires XML (l.19-29) mis à jour
     (référence NC DGI 734 §O-2, « exemption si montant <= 10 000 TTC »).
  2. `SelectionDelaiPaiementRepository.cs:114` : `EC_Montant > @SeuilMontant` (parité stricte SQL/C#, une seule règle).
  3. Tests (`SelectionDelaiPaiementCalculatorTests.cs:534,542`, `10_000m => true` devient `false`) + scénarios ci-dessous.
  4. Grep des textes d'aide/UI/docs mentionnant « >= 10 000 » / « 10 000 et plus » : mise à jour des textes vivants.
  5. Composante horaire : si `SELECT COUNT(*) FROM RT_ECHEANCE WHERE DO_Date <> CAST(DO_Date AS date)` est non nul, aligner
     `DO_Date > @DateLimiteMontant` (`SelectionDelaiPaiementRepository.cs:114`) sur la troncature `.Date` du C#
     (une facture du 31/12/2024 avec heure serait sinon traitée comme postérieure, donc sans seuil) ; sinon le noter dans le VERIFY.
- **Exclus** : toutes les dates ; le montant testé (échéance, non la facture TTC : point ouvert, cf. Risques) ; TASK-220 ;
  avoirs/montants négatifs ; schéma SQL ; affichage des colonnes de l'écran Contrôle.

## Étapes
1. Grep (périmètre 4) ; modifier C# + SQL + commentaires.
2. Adapter/ajouter les tests (scénarios ci-dessous).
3. Mesure sur données réelles (`SO_Id=1`, lecture seule) : nombre d'échéances d'achat avec `EC_Montant = 10000` exactement et
   `DO_Date` dans [2023-07-01 ; 2024-12-31], dont combien déjà déclarées (`RT_DECLARATIONDELAISPAIEMENTLG`) ; résultat de la
   requête de composante horaire.
4. `dotnet build DeclarationTVA.slnx` ; tests Core/Orchestration/Export.

## Scénarios de test (`EstEligibleSeuilLegal`, DateMiseEnRoute non configurée)
| DoDate | Montant | Attendu |
|---|---|---|
| 2023-06-30 | 1 000 000 | non (avant la loi) |
| 2023-07-01 | 9 999,99 | non |
| 2023-07-01 | 10 000,00 | non (borne exclue, « inférieur ou égal ») |
| 2023-07-01 | 10 000,01 | oui |
| 2024-12-31 | 10 000,00 | non |
| 2024-12-31 | 10 000,01 | oui |
| 2025-01-01 | 0,01 | oui (aucune exemption dès 2025) |
+ parité : la requête SQL et la méthode C# donnent le même résultat sur ces 7 cas.

## Livrables
Code C# + SQL, tests, `VERIFY/TASK-221_verify.md` : mesure de l'étape 3, checklist avec preuve datée par critère
(méthode + date).

## Critères de validation
- Les 7 scénarios passent ; aucun `>= SeuilMontant` résiduel (C# comme SQL).
- build .NET + tests : 0 erreur ; chiffres de l'étape 3 fournis au PO.

## Risques / dépendances
- Impact attendu faible (factures d'exactement 10 000,00 sur 07/2023-12/2024) : à chiffrer ; celles déjà déclarées ne seront
  plus alimentées.
- **Point ouvert, hors TASK** : la note raisonne par **facture TTC**, le code teste le montant de l'**échéance**
  (`EC_Montant`, comportement legacy). Une facture > 10 000 scindée en échéances <= 10 000 est exclue à tort. À arbitrer par le PO
  après chiffrage (somme des échéances par `DO_Numero`/tiers).
- **Point ouvert, hors TASK** : la colonne « Montant » de l'écran Contrôle est le montant de la ligne (part affectée ou solde),
  pas le montant de l'échéance, ce qui prête à confusion avec le seuil (constaté chez un client le 07/10/2026).
