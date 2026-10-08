# TASK-221 — Seuil DDP : comparer le total TTC de la facture (et non l'échéance), borne `> 10 000` conforme à la note DGI 734, règle d'éligibilité unique en C#

RISK : HIGH (périmètre déclaratif légal ; des lignes entrent dans le contrôle ; le fichier XML de dépôt DGI change) — discipline de preuve par critère exigée dans le VERIFY.
Revue : 5 passes indépendantes le 08/10/2026 (sources, code, données, testabilité, risques) intégrées dans cette version.
Dépendance : **TASK-222 dépend de celle-ci** (même méthode `EstEligibleSeuilLegal`, même champ `MontantFactureTtc`). TASK-222 ne démarre qu'une fois celle-ci mergée.
Point d'arrêt : dépôt de `VERIFY/TASK-221_verify.md`. **Interdit** : déplacer vers `DONE_DETAIL/`, modifier `DONE.md`/`TODO.md`/`CHANGELOG.md`, commit contenant « approuve ». La clôture revient à une review par un agent qui n'a pas implémenté.

## Décisions PO préalables (à obtenir avant que le worker n'ait fini ; marquer la réponse dans le VERIFY)
- **A. XML de dépôt DGI** : `<montantFactureTtc>` portera le total TTC de la facture sur chaque ligne d'échéance déclarée (aujourd'hui le montant de l'échéance, `DeclarationDelaiPaiementGenerationService.cs:250`). Une facture en 3 échéances produit 3 lignes portant le même total. [ ] oui
- **B. Source du total** : somme des `EC_Montant` des échéances du document (choix par défaut, voir plus bas) ; STOP si la mesure 3a la contredit. [ ] oui
- **C. Signes mixtes** : le total est la somme nette (une échéance de 12 000 et une de -3 000 dans un même document donnent 9 000). [ ] oui
- **F. Déploiement** : TASK-221 et TASK-222 partent en une seule livraison, après visa écrit du PO sur la mesure 3b. [ ] oui
- **G. Brouillons de déclaration non déposés (statut EnCours)** : à régénérer après déploiement ? [ ] oui [ ] non

## Contexte
Décisions PO : 07/10/2026 « appliquer les règles de la DGI » (l'extension du seuil au 31/12/2025, envisagée avant lecture de la source, est abandonnée) ; 08/10/2026 « oui facture TTC » (le montant comparé est celui de la **facture TTC**).

Règle de la note circulaire DGI n°734 (PDF `finances.gov.ma/Publication/dgi/2023/note-circulaire734.PDF`, relue sur l'image le 08/10/2026) :
- §N p.9 : la loi 69-21 s'applique aux factures émises à compter du **01/07/2023** ; §O-1 p.9-10 échelonne l'amende, la déclaration et les sanctions selon le CA du dernier exercice clos (01/07/2023 si CA > 50 M ; 01/01/2024 si 10-50 M ; 01/01/2025 si 2-10 M) ;
- §O-2 p.10 : l'amende ne s'applique pas aux « factures émises avant le 1er janvier 2025 dont le montant est inférieur ou égal à dix mille (10.000) dirhams, toutes taxes comprises » ; ces factures « ne doivent pas être intégrées dans les déclarations des délais de paiement au titre des années 2023 et 2024 » ;
- aucune exemption après cette date. Annonce DGI du 21/03/2025 (finances.gov.ma, `detail-actualite.aspx?fiche=7218`) : « l'amende pécuniaire s'applique à toutes les factures émises à compter du 1er janvier 2025, y compris celles dont le montant est inférieur ou égal à 10 000 dirhams ».
- L'indépendance de l'exemption vis-à-vis du type de déclaration (annuelle transitoire 2024-2025 pour CA <= 50 M, trimestrielle sinon) est une **déduction** du §O-2 (qui lie l'exemption à la seule date d'émission), non énoncée en termes exprès par l'annonce.

État du code : les dates sont **conformes à la décision PO** (`DateDebutDeclarationLoi = 2023-07-01`, `DateLimiteSeuilMontant = 2024-12-31`, `Declaration.Core/SelectionDelaiPaiementCalculator.cs:17,20`) ; la date de début unique n'est pas modulée selon le CA de la société (hors périmètre ; TASK-220 l'atténue). Aucune date ne change. **Écarts à corriger** :
1. **Borne** : le code déclare un montant égal à 10 000 (`montant >= SeuilMontant`, `SelectionDelaiPaiementCalculator.cs:32` ; SQL `EC_Montant >= @SeuilMontant`, `SelectionDelaiPaiementRepository.cs:114`) ; la note l'exclut (« inférieur ou égal »).
2. **Montant testé** : `EC_Montant` de l'**échéance** (`SelectionDelaiPaiementRepository.cs:100,114` ; `SelectionDelaiPaiementCalculator.cs:351`), alors que la note vise la **facture TTC** : une facture de 12 000 TTC en 2 échéances de 6 000 est exclue à tort.
3. **Colonne « Montant » de l'écran Contrôle** : c'est le montant de la ligne (part affectée ou solde, `SelectionDelaiPaiementCalculator.cs:518`), donc invérifiable contre le seuil (confusion constatée chez un client, signalement PO du 07/10/2026). De plus cette colonne s'exporte **vide** : `grid/gridExport.ts` exporte `node.data[col.field]` et ignore le `valueGetter` (`ControleLignesDelaiPaiementPanel.tsx:226`, `DeclarationsDelaiPaiementPanel.tsx:629`).
4. **Fichier XML de dépôt** : `<montantFactureTtc>` reçoit `ligne.MontantEcheance` (`DeclarationDelaiPaiementGenerationService.cs:250`, relu en direct par `DeclarationDelaiPaiementRepository.GetLignesAsync:380`) : le fichier légal déclare le montant de l'échéance alors que l'élément s'appelle montant de la facture TTC.
5. **Heure dans la date** : SQL `DO_Date > @DateLimiteMontant` (paramètre à minuit) traite une facture du 31/12/2024 à 14:00 comme postérieure à la limite, alors que le C# (`.Date`) la traite comme du 31/12.
Les écarts 1 et 2 viennent du legacy (`LigneControleDelaisPaiementController.cs:76`), reproduit à l'identique par TASK-131.

### Définition de travail du « total TTC de la facture » (décision d'implémentation, à valider : décisions B et C)
`MontantFactureTtc` = somme, arrondie à 2 décimales (`Math.Round(x, 2, MidpointRounding.AwayFromZero)`), des `EC_Montant` de **toutes les échéances du même document** de la population de la sélection (même `SO_Id`, `DO_Domaine` achat, `DE_Id` = devise société, `EC_Type` hors gain/perte de change 90/91), **quel que soit `EC_Etat`/`EC_Solde`**. **Clé de document** : (`DO_Type`, `DO_Numero`) ; un `DO_Numero` NULL ou vide forme un document à lui seul (clé de repli = `EC_Id`) — les échéances non-Erp (Impayé, Solde, règlement négatif…) portent un `DO_Numero` qui n'est pas un numéro de facture (`EcheanceType` : Erp=0, Impaye=1, ReglementNegatif=3, Solde=4, Gain=90, Perte=91…).
Sources et limites : le legacy TVA utilise `documentErp.TotalTouteTaxe` (TTC Sage) pour les factures Erp (`DeclarationTvaController.cs:479,765`) et la somme des échéances du document pour les factures FGR (`:856`, sans filtre de type ni de devise). `EC_Montant` d'une échéance Erp est TTC (`DONE_DETAIL/TASK-052:56`). GRF retient la somme (base des échéances, sans dépendance à la base Sage, cohérent avec la direction PO d'indépendance vis-à-vis de Sage). **Alternative** : lire `F_DOCENTETE.DO_TotalTTC` par lots de `DO_Piece` (pattern de TASK-191, base Sage via `GetSageConnectionInfoAsync`) — à n'envisager qu'après STOP et arbitrage PO. La somme des échéances vaut le **net à payer** (`DO_NetAPayer`), qui diffère du TTC en cas d'acompte, d'escompte ou de retenue : c'est ce que mesure 3a.

## Objectif
```
Entrée  : facture émise entre le 01/07/2023 et le 31/12/2024 ; seuil testé sur l'échéance, borne >=, SQL et C# qui dupliquent la règle.
Traitement : total TTC de la facture calculé en C# (somme des échéances du document), borne > 10 000, règle d'éligibilité unique
             (EstEligibleSeuilLegal) ; le SQL ne porte plus ni seuil ni date limite.
Sortie  : une facture n'est retenue que si son total TTC est > 10 000 (avant 2025) ; toutes ses échéances entrent ensemble ; dès 2025
          aucune exemption ; écran, export Excel et fichier XML de dépôt portent le total TTC de la facture.
```

## Choix d'architecture (ARCHITECT)
1. **Règle unique en C#.** `SelectionDelaiPaiementRepository.GetEcheancesCandidatesAsync` ne filtre plus par date limite ni par seuil : il renvoie les échéances de la population (`SO_Id`, `DO_Domaine` achat, `DE_Id` devise société, `EC_Type` hors 90/91), **sans filtre de date**, avec la colonne `DO_Type` en plus. Signature simplifiée (`soId`, `deviseSocieteId`) : retirer `dateDebutDeclarationLoi`, `dateLimiteSeuilMontant`, `seuilMontant` de `ISelectionDelaiPaiementRepository.cs:31-35` et de l'appelant (`SelectionDelaiPaiementService.cs:102-108`). Cela supprime la double implémentation SQL/C# et le piège de l'heure dans `DO_Date`.
2. **Calcul du total en C#, pur et testable** : nouvelle classe `MontantFactureTtcCalculator` (`Declaration.Core`) : regroupe par (`DoType`, `DoNumero` ; repli `EcId` si NULL/vide), somme `Montant`, arrondit, renseigne `MontantFactureTtc` sur chaque échéance du groupe. `EcheanceDelaiPaiement.MontantFactureTtc` doit être renseigné explicitement (propriété `required` ou équivalent) : jamais de valeur par défaut 0 qui exclurait silencieusement des échéances.
3. **Filtrage tôt** : le service applique `EstEligibleSeuilLegal` immédiatement après le calcul du total, **avant** la résolution des échéances légales, des affectations et des bornes déclarées, pour ne pas multiplier les requêtes en aval.
4. `EstEligibleSeuilLegal(DateTime dateDocument, decimal montantFactureTtc)` : `dateDocument.Date >= DateDebutDeclarationLoi && (dateDocument.Date > DateLimiteSeuilMontant || Math.Round(montantFactureTtc, 2, MidpointRounding.AwayFromZero) > SeuilMontant)`. La réapplication défensive du calculateur (`:351`) l'appelle avec `MontantFactureTtc`.
5. **Aucun index ni DDL sur `RT_ECHEANCE`** (table winform : aucun changement de schéma, même coordonné).

## Périmètre STRICT
- **Inclus** :
  1. Repository + interface (point 1 ci-dessus) ; `scratch/Ddp220Proof/Program.cs:54` à adapter (hors solution, ne casse pas le build).
  2. Core : `MontantFactureTtcCalculator`, `EcheanceDelaiPaiement` (+ `DoType`, `MontantFactureTtc`), `LigneSelectionDelaiPaiement` (+ `MontantFactureTtc`, renseigné dans `AjouterLigne` ~l.524), `SeuilsLegauxDelaiPaiement.EstEligibleSeuilLegal` (borne `>`, arrondi), commentaires XML alignés (note DGI 734 §O-2, « total TTC de la facture » ; titres de tests « reproduits à l'identique du legacy » à corriger : la borne diverge volontairement).
  3. Service : calcul du total + filtrage tôt (point 3).
  4. **XML de dépôt** (décision A) : `LigneDeclarationDelaiPaiement` (`DeclarationDelaiPaiementEntities.cs:187`) reçoit `MontantFactureTtc` ; `DeclarationDelaiPaiementRepository.GetLignesAsync` (l.380) le calcule avec la **même définition et le même `MontantFactureTtcCalculator`** ; `DeclarationDelaiPaiementGenerationService.cs:250` passe `ligne.MontantFactureTtc` à `montantFactureTtc`. `montantNonEncorePaye` et `montantPayeHorsDelai` restent au niveau échéance.
  5. API/DTO : `LigneSelectionDelaiPaiementDto` (l.184 et `From` l.215) et `LigneDeclarationDelaiPaiementDto` (l.282 et `From` l.305) exposent `montantFactureTtc`.
  6. Front : `api.ts` (types, l.313/318/356) ; `ControleLignesDelaiPaiementPanel.tsx` : nouvelle colonne « Montant facture TTC » (`field: 'montantFactureTtc'`, propriété réelle donc exportable), colonne existante renommée « Montant de la ligne » avec `field: 'montantLigne'` (sans `valueGetter`, ce qui corrige l'export vide), `headerTooltip` pour chacune (« Part affectée ou solde de l'échéance » / « Somme des échéances de la facture ; comparée au seuil de 10 000 MAD pour les factures émises avant 2025 »), `storageKey` sur la grille (écart existant : `grf.<ecran>.<grille>`, `DOCS/UI_STANDARDS.md`) ; même correction `field` dans `DeclarationsDelaiPaiementPanel.tsx:629` (+ l.688 et suivantes si un total est affiché, l.1174). Conformité `DOCS/UI_STANDARDS.md` (lire intégralement et appliquer : `ApbsGrid`, format fr-FR 2 décimales aligné à droite).
  7. Tests : voir « Plan de tests ». 8. Textes d'aide/UI/docs : grep « >= 10 000 », « 10 000 et plus », « montant de l'échéance » ; `ISelectionDelaiPaiementRepository.cs:27-29` ; mise à jour des textes vivants, pas de `DONE_DETAIL`.
- **Exclus** : toutes les dates ; modulation de la date de début selon le CA ; TASK-220 ; devise étrangère (déjà exclue par `DE_Id`) ; avoirs (comportement existant : un total négatif est <= seuil) ; lecture de la base Sage hors mesures ; index/DDL sur `RT_ECHEANCE` ; réglage par société et bandeau (TASK-222) ; en-tête d'export Excel (TASK-222).

## Étapes (ordre exécutable)
0. **Précondition** : accès en lecture à la base GRF (et Sage pour 3a) depuis la machine du worker (le serveur de `connections.json` était injoignable depuis le poste de l'architecte le 08/10/2026). Sans accès : STOP, jamais d'estimation.
1. Lire `DOCS/UI_STANDARDS.md`, le repository de sélection, le calculateur, les grilles ; `git log -5` sur les fichiers touchés et rebase sur `main`.
2. **Mesures AVANT** (lecture seule, `SO_Id=1`, puis les autres sociétés présentes), consignées avec date et base ciblée, scripts joints au VERIFY (annexe ci-dessous) :
   - 3a. Écart |Σ échéances − `F_DOCENTETE.DO_TotalTTC`| > 0,01 (jointure Sage `DO_Piece = DO_Numero`, `DO_Domaine = 1`, types de pièces facture d'achat à identifier — 16/17 d'après la relecture, à confirmer), et idem avec `DO_NetAPayer` : nombre de documents comparés, d'écarts, ventilation par cause (retenue, escompte, acompte, autre `EC_Type`, devise étrangère, échéance manquante), 10 exemples. **STOP et question au PO** si au moins un écart change le verdict d'éligibilité d'un document (franchissement de 10 000) ou si les écarts touchent plus de 1 % des documents. Base Sage inaccessible : STOP (pas d'estimation).
   - 3a-bis. Clé de document : documents avec plusieurs `CT_No`, plusieurs `EC_Type`, plusieurs `DO_Date` (jour), plusieurs `DE_Id` ; nombre de `DO_Numero` NULL/vides ; `DO_Date` avec heure non nulle. Résultat non nul : le signaler ; ajuster la clé si les mesures le justifient.
   - 3b. Échéances qui **entrent** (documents de total > 10 000 avec au moins une échéance <= 10 000, DoDate 01/07/2023-31/12/2024) : nombre, montants, retard cumulé de leur première déclaration (borne = échéance légale), part restant après TASK-220, part non soldée, part déjà présente dans `RT_DECLARATIONDELAISPAIEMENTLG`.
   - 3c. Échéances/documents qui **sortent** (total exactement 10 000,00) ; documents de total négatif ; documents de signes mixtes.
   - 3d. Lignes réellement produites pour la période du prochain dépôt, avant/après.
   - 3e. Types de colonnes (`EC_Montant`, `DO_Date`, `DO_Numero`, `DO_Type`), nombre de lignes renvoyées par la requête de candidats avant/après (STOP si > 100 000 : arbitrage PO), temps d'exécution, index existants (`sys.indexes`).
3. Implémenter (points « Inclus »), tests.
4. **Mesures APRÈS** (mêmes requêtes).
5. `dotnet build DeclarationTVA.slnx` ; `dotnet test` (Core, Orchestration, Export.Excel, Export.Xml) ; `npm run lint` + `npm run build` (dans `declaration-tva-web/`) ; `npx playwright test` (`task134`, `task136` et tout test qui mocke les lignes DDP : grep `montantLigne`) ; checklist de revue UI de `DOCS/UI_STANDARDS.md` cochée **par critère avec méthode et date**.
6. Déposer `VERIFY/TASK-221_verify.md` et **s'arrêter**.

## Plan de tests
- **Core** (`Declaration.Core.Tests/SelectionDelaiPaiementCalculatorTests.cs`) : helper `Echeance(...)` : paramètre `decimal? montantFactureTtc = null`, `MontantFactureTtc = montantFactureTtc ?? montant` (l.31-52) ; l.534 et l.542 : `10_000m` passe de `True` à `False`, ajouter `10_000,01m` → `True` ; nouveaux tests : `SeuilsLegaux_Borne10000_NonEligible`, `SeuilsLegaux_10000_01_Eligible`, `SeuilsLegaux_Arrondi_10000_004_NonEligible`, `…_10000_005_Eligible`, `…_MontantZero_NonEligible`, `…_Negatif_NonEligible`, `…_2025_01_01_800_Eligible`, `…_Date2024_12_31_14h_TraiteCommeLimite`, `Calculateur_UtiliseMontantFactureTtc_PasMontantEcheance` (échéance 6 000, total 12 000 → ligne), `Calculateur_ExcluSiTotalSous10000` (échéance 20 000, total 9 000 → vide), `Calculateur_MiseEnRoute_PrioriteSurSeuil`, `MontantFactureTtcCalculator_*` (groupes par clé, NULL/vide = document propre, plusieurs tiers/types non fusionnés, signes mixtes, arrondi, échéance payée comptée, échéance sans total signalée).
- **Orchestration** : adapter les fakes de `Task132CycleDeVieDeclarationDelaiPaiementTests.cs` (~l.350) et `Task133GenerationFichierDelaiPaiementTests.cs` (~l.293, 488) ; ajouter un test Task133 : `<montantFactureTtc>` = total du document sur chaque ligne d'échéance ; test du service : filtrage tôt.
- **Playwright** : `task134.spec.ts` (fixtures l.64-76, 189 : ajouter `montantFactureTtc`) et `task136.spec.ts` ; nouveau `task221.spec.ts` : colonnes « Montant facture TTC » et « Montant de la ligne » présentes, valeurs exportées non vides.
- Export Excel : la colonne exportée vaut la propriété réelle (plus de colonne vide).

## Scénarios de test (`EstEligibleSeuilLegal` + `MontantFactureTtcCalculator`, DateMiseEnRoute non configurée sauf mention)
| # | Document (échéances `EC_Montant`) | DoDate | Attendu pour chaque échéance |
|---|---|---|---|
| 1 | 1 × 10 000,00 | 2024-06-01 | non (borne exclue) |
| 2 | 1 × 10 000,01 | 2024-06-01 | oui |
| 3 | 6 000 + 6 000 (total 12 000) | 2024-06-01 | oui, les deux (était non) |
| 4 | 5 000 + 5 000 (total 10 000,00) | 2024-06-01 | non |
| 5 | 4 500 + 4 500 (total 9 000) | 2024-06-01 | non |
| 6 | 3 échéances (total 12 000), dont une payée | 2024-06-01 | oui, les trois |
| 7 | 1 × 12 000 payée en partie (solde 3 000) | 2024-06-01 | oui (total, pas solde) |
| 8 | 1 × 1 000 000 | 2023-06-30 | non (avant la loi) |
| 9 | 1 × 800 | 2025-01-01 | oui |
| 10 | 400 + 400 | 2025-04-23 | oui |
| 11 | 12 000 + une échéance gain/perte (EC_Type 90/91) de 5 000 | 2024-06-01 | oui pour l'échéance de 12 000 ; le gain/perte est hors population et hors total |
| 12 | 1 × -12 000 (avoir) | 2024-06-01 | non |
| 13 | 1 × 9 000 | 2024-12-31 à 14:00 | non (heure ignorée, traité comme le jour limite) |
| 14 | 1 × 50 000, `DateMiseEnRoute` = 2025-01-01 | 2024-09-01 | éligible au seuil puis exclue par le calculateur (TASK-220) : aucune ligne |
| 15 | 1 × 0,00 | 2024-06-01 | non |
| 16 | 1 × 0,00 | 2025-06-01 | oui (aucune exemption dès 2025) |
| 17 | 1 × 10 000,004 | 2024-06-01 | non (arrondi 10 000,00) |
| 18 | 1 × 10 000,005 | 2024-06-01 | oui (arrondi 10 000,01) |
| 19 | 12 000 + (-3 000), même document (total net 9 000) | 2024-06-01 | non (décision C) |
| 20 | 8 000 + 8 000 dont `DO_Date` différentes d'un jour | 2024-06-01 / 06-02 | même verdict pour les deux (oui) |
| 21 | 6 000 + 6 000, même `DO_Numero`, `DO_Type` ou `CT_No` différents | 2024-06-01 | documents distincts, jamais fusionnés : non / non |
| 22 | 1 × 12 000, `DO_Numero` vide, + 1 × 5 000, `DO_Numero` vide | 2024-06-01 | deux documents propres (clé de repli) : oui / non |
| 23 | 1 × 10 000,01 | 2023-07-01 | oui (borne de date incluse) |
| 24 | 1 × 10 000,01 | 2024-12-31 00:00 | oui |
| 25 | 1 × 10 000,00 | 2025-01-01 | oui (plus d'exemption) |
Propriétés : deux échéances d'un même document reçoivent toujours le même verdict ; la colonne « Montant facture TTC » de l'écran et de l'export égale la somme des échéances ; `<montantFactureTtc>` du XML égale le total du document.

## Annexe : requêtes de mesure (lecture seule ; `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED`)
```sql
-- 3e types
SELECT COLUMN_NAME, DATA_TYPE, NUMERIC_PRECISION, NUMERIC_SCALE FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_NAME='RT_ECHEANCE' AND COLUMN_NAME IN ('EC_Montant','DO_Date','DO_Numero','DO_Type');
-- 3a-bis clé de document
SELECT DO_Type, DO_Numero, COUNT(DISTINCT CT_No) nbTiers, COUNT(DISTINCT EC_Type) nbTypes,
       COUNT(DISTINCT CAST(DO_Date AS date)) nbDates, COUNT(DISTINCT DE_Id) nbDevises
  FROM RT_ECHEANCE WHERE SO_Id=@So AND DO_Domaine=1 GROUP BY DO_Type, DO_Numero
HAVING COUNT(DISTINCT CT_No)>1 OR COUNT(DISTINCT EC_Type)>1 OR COUNT(DISTINCT CAST(DO_Date AS date))>1 OR COUNT(DISTINCT DE_Id)>1;
SELECT COUNT(*) FROM RT_ECHEANCE WHERE SO_Id=@So AND DO_Domaine=1 AND (DO_Numero IS NULL OR DO_Numero='');
SELECT COUNT(*) FROM RT_ECHEANCE WHERE DO_Date <> CAST(DO_Date AS date);
-- 3a/3b/3c totaux par document (population de la sélection)
;WITH P AS (SELECT * FROM RT_ECHEANCE WHERE SO_Id=@So AND DO_Domaine=1 AND EC_Type NOT IN (90,91)
            AND DE_Id=(SELECT SD.DV_Id FROM P_SOCIETE S JOIN P_SOCIETEDEVISE SD ON SD.SO_Id=S.SO_Id AND SD.SD_No=S.SO_DeviseErpNo WHERE S.SO_Id=@So)),
D AS (SELECT DO_Type, DO_Numero, ROUND(SUM(EC_Montant),2) Tot, COUNT(*) NbEc, MIN(DO_Date) MinDate, MIN(EC_Montant) MinEc FROM P GROUP BY DO_Type, DO_Numero)
SELECT * FROM D;  -- 3a : comparer Tot à F_DOCENTETE.DO_TotalTTC / DO_NetAPayer (base Sage) ; 3b : Tot > 10000 AND NbEc > 1 AND MinEc <= 10000 ; 3c : Tot = 10000 ou Tot < 0
```
Le calcul des échéances légales et des bornes (3b : retard cumulé, 3d) passe par le pipeline réel en lecture seule (`scratch/Ddp220Proof/Program.cs` comme modèle).

## Livrables
Code C# + SQL + front + tests ; `VERIFY/TASK-221_verify.md` : mesures avant/après (tableau, scripts exécutés, date, base ciblée, type SQL de `EC_Montant`), réponses du PO aux décisions A, B, C, F, G, checklist avec preuve datée par critère (méthode + date), captures de la grille et de l'export (jeu de données précisé), preuve que le XML porte le total.

## Critères de validation
- Les 25 scénarios passent ; `grep -rn ">= SeuilMontant" --include=*.cs` et `grep -rn "EC_Montant >=" --include=*.cs` → 0 résultat ; aucun test d'éligibilité sur `Montant` seul.
- Mesure 3a fournie, sans écart changeant un verdict (sinon STOP et arbitrage PO) ; mesures 3a-bis et 3e fournies.
- `dotnet build` + `dotnet test` + lint/build front + Playwright : 0 erreur ; checklist UI cochée par critère avec méthode et date.
- Export Excel : les colonnes « Montant facture TTC » et « Montant de la ligne » sont non vides ; `<montantFactureTtc>` du XML = total du document.

## Risques / dépendances
- **Des lignes entrent** : les échéances de factures > 10 000 TTC scindées en échéances <= 10 000 n'étaient jamais sélectionnées ; elles le seront, avec une première déclaration à cumul de retard potentiellement important (borne = échéance légale). Condition de livraison : la mesure 3b est transmise au PO, déploiement en production après son visa écrit (décision F). Les déclarations déjà **déposées** ne sont pas modifiées (à prouver par un test : un changement de règle ne réécrit aucune déclaration déposée) ; aucune régénération automatique ; brouillons non déposés : décision G.
- **Fichier légal** : le XML de dépôt change de contenu (`<montantFactureTtc>`) : décision A.
- **Définition du total TTC** : somme des échéances = net à payer ; peut différer du TTC Sage (acomptes, escomptes, retenues). Cas à surveiller : signes mixtes, document multi-devises (total tronqué sans signal), `DO_Numero` partagé entre types de pièces. Vérifié par 3a et 3a-bis.
- **Volume/performance** : la requête renvoie désormais toutes les échéances de la population sans filtre de date (mesure 3e).
- **Seuls les totaux exactement égaux à 10 000,00** sortent (avant 2025) ; avoirs : comportement existant (alerte `RECAP_SESSION_QA_TVA_DELAI.md:110`) inchangé.
- Anti-double-déclaration : lignes déjà déclarées non modifiées ; les échéances qui entrent sont traitées comme « première déclaration ».
- Conflits potentiels de merge : `ControleLignesDelaiPaiementPanel.tsx` (TASK-201, TASK-178 ouvertes), `SelectionDelaiPaiementCalculator.cs` (TASK-210) ; `IN_PROGRESS/` était vide le 08/10/2026 : revérifier au démarrage.
