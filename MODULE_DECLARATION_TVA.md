# Module Déclaration TVA — Décaissement Fournisseur (Maroc)

Analyse issue de la décompilation de `Tresorerie.UIDeclarationTva.dll` + schéma `TRESO_DISTRICAP` (serveur `.\sql2022`).

## 1. Objectif

GRFN (source inaccessible) calcule la déclaration TVA fournisseur en « boîte noire ».
On construit un **module séparé, lecture seule sur la base**, qui :
1. **Contrôle** : recalcule les lignes attendues et les compare à `RT_LigneDeclarationTva` (localise écarts + lignes manquantes).
2. **Recalcul + export** : régénère le relevé de déductions au format **Simpl-TVA DGI** avec un calcul corrigé.

On ne modifie pas GRFN.

## 1bis. Fonctionnement actuel de l'application

### Architecture (3 couches)
- **ERP Sage** = source de vérité des **factures fournisseurs, lignes de taxe, tiers/ICE, écritures comptables + rapprochement bancaire**. Accédé via le service `127.0.0.1:8003` (`IErpComptaService` / `IErpCommService`).
- **GRFN.exe** = gestion trésorerie + déclaration.
- **Base `TRESO_DISTRICAP`** (tables `RT_`) = référentiel trésorerie : règlements, échéances, affectations, et le **résultat** de la déclaration.

> GRF ne possède pas les factures : il gère la trésorerie et va chercher la TVA dans l'ERP au moment de la déclaration.

### Flux métier
1. **Facture** fournisseur → échéance (`RT_ECHEANCE`), depuis l'ERP ou saisie GR.
2. **Règlement** (chèque/traite/virement/espèce) → `RT_MOUVEMENT`.
3. **Rapprochement / affectation** → `RT_AFFECTATION` relie règlement ↔ échéance (montant affecté).
4. **Déclaration TVA** sur les règlements affectés de la période. Régime = TVA déductible **sur décaissement** (déductible au paiement, au prorata du montant payé).

### Cycle de vie d'une déclaration
1. `CreateDeclaration` (période, mensuel/trimestriel, exercice) → statut **EnCours**.
2. « Charger » → `GetDeclaration` = `GetDeclarationDecaissement` : calcule des lignes **candidates** (preview, **non enregistrées**).
3. « Intégrer » → `IntergerLigne` : écrit dans `RT_LigneDeclarationTva` et **marque l'affectation** (`AF.DT_Id`) → non reproposée ailleurs.
4. Ajustements : ajout manuel, import fichier, suppression, **report de ligne** (`IsReport=1`, exclu du fichier courant, reporté à la période suivante).
5. `ClotureDeclaration` → statut **Cloture** (verrouille ; annulation possible).
6. `GenererFichierDeclaration` → XML Simpl-TVA + zip (seulement si clôturée).
7. `DeposeDeclaration` → `IsDepose=1`.

### Deux origines de « données manquantes »
- **Technique** : l'algorithme saute la ligne en silence (chèque non rapproché, espèce hors période, `Solde==Montant`, facture GR non comptabilisée, taxe non « à taux »).
- **Humaine** : ligne proposée mais non intégrée par l'utilisateur, ou reportée.

### Détail : `IntergerLigne()` → `DeclarationTvaEncaissementLigneAjouter()` (Tresorerie.Core.dll)
`IntergerLigne` (contrôleur) ne fait que boucler les lignes sélectionnées et appeler, pour chacune, `SocieteManager.DeclarationTvaEncaissementLigneAjouter(..., integration=Integration)`. Ce dernier est la vraie porte d'entrée :

1. **Validations bloquantes** : `assiette != 0`, `0 ≤ taux ≤ 100`, `0 ≤ prorata ≤ 100`, champs obligatoires ; **Maroc** : identifiant fiscal = 8 car., ICE = 15 car., sans espaces ; déclaration EnCours et non déposée.
2. **Contrôles métier** (affectation fournisseur = domaine Achat) : chèque/traite/virement **non pointé** (`!IsPointe`) → exception ; règlement **non comptabilisé** (`IsComptabilise` ∉ {Comptabilise, TraiteFournisseurComptabilise}) → exception. (Dépense : comptabilisée + `WithTva`.)
3. **Transaction** : `INSERT RT_LigneDeclarationTva` + `AffectationSetDeclarationTva(affNo, declNo)` qui pose **`AF.DT_Id`** ⇒ l'affectation est consommée et ne sera plus jamais reproposée.

**Deux entonnoirs distincts** :
- *Chargement* (`GetDeclarationDecaissement`) = sauts **silencieux**.
- *Intégration* (`LigneAjouter`) = erreurs **bloquantes** (pointage, comptabilisation, ICE/IF).

⚠️ **À vérifier** : `DeleteDeclaration` libère-t-il `AF.DT_Id` ? Sinon une déclaration supprimée laisse les affectations « consommées » → factures définitivement absentes des futures déclarations.

## 1ter. Source du rapprochement bancaire (déterminant pour l'éligibilité)

Le rapprochement qui conditionne l'apparition d'une ligne (chèque/traite/virement) **vit dans SAGE**, pas dans GRF.

- Implémentation : `SageCompta.v12.Dapper.dll` (ERP=v12), Dapper/SQL direct sur la base Sage.
- Chaîne : `_erpComptaService.GetEcrituresRapproche` → `ErpCompta.GetEcrituresRapproche` → `IEcritureComptableRepository.GetAllRapproche`.
- Table : **`F_ECRITUREC`** (écritures comptables Sage).
- **Critère « rapprochée »** :
  ```sql
  WHERE ISNULL(EC_TresoPiece,'') <> ''            -- pièce de trésorerie renseignée
    AND JM_Date BETWEEN @Debut AND @Fin           -- exercice.Debut → déclaration.DateFin
  ```
- Colonnes clés (alias) : `cbMarq` → `No` (= pont vers GRF `EcritureComptable.ErpNo`), `EC_TresoPiece` → `PieceTresorerie`, `EC_DateRappro` → `DateRapprochement`, `EC_Piece` → `NumeroPiece`, `CG_Num` → `CompteGeneral`, `CT_Num` → `TiersNumero`, `TA_Code` → `TaxeCode`.
- **GRF écrit le rapprochement dans Sage** : `UPDATE F_ECRITUREC SET EC_TresoPiece=@x, EC_DateRappro=@d WHERE cbMarq=@No`. Sage est donc la source de vérité.

Trois notions distinctes dans `F_ECRITUREC` : **rapprochement** (`EC_TresoPiece`/`EC_DateRappro`), **pointage** (`EC_Point`/`EC_Pointage`), **lettrage** (`EC_Lettre`/`EC_Lettrage`).

> Conséquence module : pour savoir si un règlement est déclarable, le nouveau module doit lire la **base Sage** (`F_ECRITUREC.EC_TresoPiece <> ''`), en joignant sur `cbMarq = ErpNo` de l'écriture GRF. → besoin du nom de la base Sage + sa connexion.

## 1quater. Règles métier cibles (définies par le PO)

> Principe volontairement simple. Exemple : en 07/2026 on déclare le mois 06/2026.

**Critère d'inclusion = ce qui n'a pas encore été déclaré, positionné par sa date propre — PAS par la date de facture** (la date facture n'est qu'un élément d'affichage).

### Décaissement (fournisseur)
- Règlements **rapprochés** dont la **date de rapprochement** tombe dans le mois déclaré (06/2026), **avec une affectation sur une facture**.
- Règlements **espèce** du mois déclaré (date de **règlement**), avec affectation sur facture.
- **Cas report/partiel** : un règlement rapproché en 05/2026 (ou avant) non encore déclaré — parce que non affecté à l'époque, ou déclaré **partiellement** — doit remonter en 06/2026 pour le **reste**. ⇒ le critère réel = *date de rapprochement ≤ fin de période* **ET** *affectation non encore (entièrement) déclarée*. Le "partiel" se gère à la granularité **affectation** (une facture peut avoir plusieurs affectations/paiements, chacune déclarée le mois de son rapprochement).
- On lit les **affectations** → n° facture, fournisseur, infos, et surtout **HT / taux TVA / TTC**. L'affectation est ventilée **en runtime** sur les **taux de TVA de la facture** (une facture peut avoir plusieurs taux).

### Encaissement (client)
- Exactement le même principe.

### Dépense
- Uniquement les dépenses **avec TVA**, selon la **date de la dépense**.

### Frais bancaire
- Uniquement les frais bancaires **avec TVA**, selon la **date du frais**.

### Décisions / points d'attention
- **TVA non stockée dans GRF** → recalcul depuis Sage à chaque fois = allers-retours. Le module doit récupérer le détail TVA facture une fois et le mettre en cache.
- **Gating rapprochement** : le PO envisage de **ne pas dépendre de l'appariement écriture-compta GRF↔Sage** (doute sur la synchro). Décision à confirmer, car l'ancien module gate sur `EC_TresoPiece<>''` ; assouplir change le périmètre déclaré. ⇒ tâche de vérification créée (synchro rapprochement) avant bascule.
- Concordance avec le code existant : le mécanisme "affectation non déclarée (`AF.DT_Id`) + rapprochée avant fin de période" reproduit déjà le cas report/partiel. La divergence porterait surtout sur **la fiabilité de la source du rapprochement**.

## 1quinquies. Source de rapprochement LOCALE (option B retenue)

Décision PO : ne pas dépendre de Sage pour l'éligibilité → utiliser le **rapprochement local GRF**.

GRF stocke le rapprochement localement :
- **`RT_MOUVEMENT.MV_Point`** (bit/int, = rapproché) et **`RT_MOUVEMENT.MV_PointDate`** (date de rapprochement).
- Posés lors du pointage du règlement sur un extrait bancaire (`RT_EXTRAITLIGNE.EXL_Rapproche` / `EXL_RapproDate`).
- Confirmé par la requête existante `ReglementFournisseurRepository.GetAllDecaisseByRapprochementACompta` (mapping DapperExtensions `MouvementMapping` : `IsPointer→MV_Point`, `DatePointage→MV_PointDate`, `IsDecaisse→MV_DECAISSE`, `IsComptabilise→MV_Compta`, `Domaine→MV_Domaine`, `CaisseNoIn→CA_IdIn`, `CaisseNo→CA_IdOut`).

### Requête cœur — décaissement fournisseur (local, sans Sage)
```sql
SELECT * FROM RT_MOUVEMENT
WHERE SO_Id       = @societeNo
  AND MV_Domaine  = <ReglementFournisseur>
  AND MV_PointDate >= @dateDebut           -- date de rapprochement locale
  AND MV_PointDate <  DATEADD(day,1,@dateFin)
  AND MV_Point    = 1                       -- rapproché
  AND MV_DECAISSE = 1
  AND MV_Compta   = @etatComptabilise
  AND MV_Annule   = 0
  AND MV_Impaye   = <NonImpaye>
  AND CA_IdOut IN (@caisses) AND <mode> IN (@modes);
```
- **Espèce** : remplacer le critère `MV_PointDate` par `MV_Date` dans le mois (pas de rapprochement pour l'espèce).
- Puis pour chaque règlement : lire ses **affectations non déclarées** (`RT_AFFECTATION` where `DT_Id IS NULL`) → n° facture, fournisseur, puis le détail **HT/taux/TTC par taux** de la facture, ventilation runtime par taux.

### Détail TVA facture = Objets Métier Sage (décision PO)
Les montants de TVA **ne sont pas stockés en base**. On les lit via les **Objets Métier Sage** (Gestion Commerciale) pour obtenir **exactement** les montants calculés par Sage et **ne réintroduire aucun arrondi**. C'est déjà ce que fait l'ancien module : `_erpCommService.GetDocument(...)` → `documentErp.Taxes` / `ErpLigneTaxe.GetBase(...)` (Objet Métier, pas SQL).

**Conséquences d'architecture :**
- Le module **n'est pas du SQL pur** → **composant .NET** référençant les Objets Métier Sage (wrapper existant `SageBO.Core` / `SageCommercial.*`, comme GRFN).
- **Éligibilité/sélection** = SQL local GRF (rapide). **Détail TVA** = BO Sage (exact).
- **Cache** : récupérer chaque facture **une seule fois** via BO et réutiliser son détail TVA pour toutes les affectations qui la pointent (évite les allers-retours).
- Base commerciale Sage cible : `DISTRI_DEMO`.

> ⚠️ À faire : mapper les valeurs int des enums (`MV_Domaine` ReglementFournisseur, `MV_Impaye` NonImpaye, `MV_Compta` EtatComptabilite) depuis `Tresorerie.Core.Enum`. Et la tâche `task_df358bf7` doit vérifier que `MV_Point`/`MV_PointDate` (local) est cohérent avec Sage `EC_TresoPiece`/`EC_DateRappro` avant bascule.

## 1sexies. Exigences client (retour terrain)

Source : retour client Adil Ahanguir (Consilium Pro), 07/07/2026, sur l'ancien module.
1. **Ajouter un moyen de contrôle dans l'interface de déclaration.**
2. **Extraction Excel** pour consulter l'état de la déclaration **avant dépôt**.

### Réponse module (les deux = une même brique)
Le « moyen de contrôle » se matérialise par un **état Excel** consultable tant que la déclaration est `EnCours` (avant clôture/dépôt). Il s'appuie sur l'identité de contrôle :
```
Total déclaration = Σ décaissement rapproché + Σ espèce + Σ dépense + Σ frais bancaire
```

**Contenu du contrôle :**
- Récap **par source** (décaissement/espèce/dépense/frais) + vérification `Σ sources = total`.
- Récap **par taux** et **par code activité**.
- **Alertes avant dépôt** : tiers sans ICE / identifiant ≠ 8 car. / ICE ≠ 15 car. / espaces ; facture introuvable ; règlement rapproché **non affecté** ; ligne à 0.

**Export Excel (2 feuilles) :**
- **Détail** : 1 ligne / affectation — n° facture, désignation, tiers, IF, ICE, HT, taux, TVA, TTC, mode, date paiement, date facture.
- **Récap** : totaux par source / taux / code activité + contrôle d'équilibre.

> GRFN dispose déjà de `ListDeclarationTvaController.GenerateCSVFile` (CSV brut). Le client demande mieux : Excel lisible + vue de contrôle avant dépôt → à traiter proprement dans le nouveau module.

## 1septies. Bases cibles & multi-client (base prod fournie)

Base **prod d'un client réel** (EMA Distribution) qui a **déjà utilisé l'ancien module** GRFN → contient de vraies déclarations dans `RT_LigneDeclarationTva`. C'est la base de référence de dev/validation ; l'appli sera ensuite **déployée chez d'autres clients** → **tout est configurable, rien en dur** (noms de base, serveur, credentials, `n` décimales — principe multi-client CDC).

| Rôle | Base | Contenu utile |
|---|---|---|
| **GRF (trésorerie)** | `GR_EMA_DISTRIBUTION` | `RT_MOUVEMENT`, `RT_AFFECTATION`, `RT_DeclarationTva`, **`RT_LigneDeclarationTva`** (résultat GRFN à contrôler) |
| **Sage (commerciale)** | `NEW_EMA DISTRIBUTION` *(⚠️ nom exact à confirmer — espace ou underscore ?)* | factures achat/vente + détail taxes (via worker OM) |

**Ce que ça débloque :**
- **TASK-001** (synchro rapprochement GRF↔Sage) : plus « base à fournir » → **exécutable**.
- **Sélection SQL (#5)** : requêtes `RT_MOUVEMENT`/`RT_AFFECTATION` désormais **testables sur données réelles**.
- **Mode Contrôle (objectif #1 du module, §1)** : la base contient les lignes GRFN → on peut **recalculer avec le moteur corrigé (TASK-004/005) et comparer à `RT_LigneDeclarationTva`** → rapport d'écarts. C'était la raison d'être initiale, désormais faisable.

**À confirmer avant de brancher la Sélection SQL / TASK-001 :**
1. **Serveur/instance SQL** des deux bases (prod client — probablement ≠ `.\sql2022` du dev).
2. **Orthographe exacte** de la base Sage (`NEW_EMA DISTRIBUTION` avec espace → connexion en `[NEW_EMA DISTRIBUTION]`, ou underscore).
3. **Credentials** : compte SQL/Sage lecture seule pour GRF **et** pour l'OM Sage.

## 2. Algorithme réel de GRFN (décaissement fournisseur)

Méthode `DeclarationTvaController.GetDeclarationDecaissement`.

**Source** : règlements fournisseurs (`ReglementFournisseurGetAllToDeclarationTvaEncaissement`, du début d'exercice → fin de période) + dépenses caisse.
Régime marocain = **TVA déductible sur décaissement** : on déclare quand on paie, **au prorata** du montant payé sur le total facture.

Pour chaque règlement → chaque **affectation** (facture) non encore déclarée (`AF.DT_Id IS NULL`) :

### Filtres (→ lignes ignorées silencieusement, cause « données manquantes »)
- Chèque/traite/virement : nécessite une **écriture bancaire rapprochée** en compta (`journal.Rapprochement == Tresorerie` + écriture présente dans les rapprochements de la période). Sinon `continue`.
- Espèce dont la date < début de période → ignoré.
- `reglement.Solde == reglement.Montant` (règlement non affecté) → exclu.
- Facture GR non comptabilisée → ignoré.
- Ligne de taxe dont `TypeTaux != Taux` → ignoré.

### Calcul (branche facture ERP)
```
ratio    = round(TotalTTC_facture / montant_affecté, 6)
assiette = base_taxe / ratio
tva      = round(assiette * taux/100, nbDecimalesSociété)   // MidpointRounding par défaut = ToEven
montant_déclaration = tva * -1
```
Branche FactureGR : idem avec `ratio = round(Σ échéances / montant_affecté, 6)`, `assiette = ligneTaxe.Montant / ratio`.
Branche dépense : `tva = MontantTva` (pas de prorata).

### Bugs / fragilités identifiés
1. **Prorata via ratio arrondi à 6 décimales puis division** → dérive de centimes sur paiements partiels. Correct : `assiette = base * montant_affecté / TotalTTC` (une seule opération, sans ratio intermédiaire arrondi).
2. **`Math.Round` sans `MidpointRounding.AwayFromZero`** → arrondi bancaire ; la DGI attend l'arrondi arithmétique. Écart possible d'1 centime.
3. **Assiette non arrondie** alors que la TVA l'est → base/TVA/ TTC incohérents dans la ligne.
4. **Sauts silencieux** (voir filtres) → factures absentes du relevé sans avertissement.

## 3. Format d'export cible (identifié dans `DeclarationTvaEncaissementFileGenerator`)

Relevé de déductions Simpl-TVA, XML zippé. Nom : `{Numero}-{Exercice}-{M|T}{période}.xml`.

```xml
<DeclarationReleveDeduction>
  <identifiantFiscal>{IF société}</identifiantFiscal>
  <annee>{exercice}</annee>
  <periode>{mois 1-12 | trimestre 1-4}</periode>
  <regime>{1=mensuel | 2=trimestriel}</regime>
  <releveDeductions>
    <rd>
      <ord>{ordre}</ord>
      <num>{n° facture}</num>
      <des>{désignation}</des>
      <mht>{assiette}</mht>
      <tva>{montant TVA}</tva>
      <ttc>{assiette+tva}</ttc>
      <refF><if>{identifiant 8c}</if><nom>{nom}</nom><ice>{ice 15c}</ice></refF>
      <tx>{taux}</tx>
      <prorata>{prorata}</prorata>
      <mp><id>{mode}</id></mp>   <!-- 1=espèce 2=chèque 3=op.bancaire 4=virement 5=traite 7=autre -->
      <dpai>{date paiement yyyy-MM-dd}</dpai>
      <dfac>{date facture yyyy-MM-dd}</dfac>
    </rd>
  </releveDeductions>
</DeclarationReleveDeduction>
```
Séparateur décimal = `.`. Validation Maroc : `identifiantFiscal` = 8 car., `ice` = 15 car., sans espaces. Lignes `IsReport=1` exclues.

**Détails exacts extraits (décompilation `DeclarationTvaEncaissementFileGenerator.Generate`, ilspy) — repris par TASK-011 :**
- **Pas de déclaration `<?xml?>`** : le contenu commence par `\r\n<DeclarationReleveDeduction>` ; écriture `File.WriteAllText` (UTF-8 sans BOM) + **`.zip`** du même nom contenant le xml (`{Numero}-{Exercice}-{M|T}{période}`).
- **Mapping `<mp><id>`** : Espèce=1, Chèque=2, Virement=4, Traite=5, Autre=7 ; **OperationBancaire=3** (écrase le type).
- **Champs `<rd>`** : `mht`=Assiette, `tva`=Montant, `ttc`=Assiette+Montant, `des`=DesignationDocument, `dpai`=DateMouvement, `dfac`=DateDocument, `ord` 1-based.
- **Validation bloquante** par ligne avec n° de ligne (IF=8, ICE=15, sans espaces).
- ⚠️ **Bug rejoué à l'export** : GRFN refait `Math.Round(x, nbDecimal)` en **`ToEven`** → TASK-011 ne re-arrondit pas (le modèle est déjà arrondi `AwayFromZero`).

## 4. Modèle de données (base `TRESO_DISTRICAP`)

| Table | Rôle | Colonnes clés |
|---|---|---|
| `RT_MOUVEMENT` | règlements/factures | `MV_Id, MV_Numero, MV_Date, MV_Montant, MV_Solde, MV_Domaine, MV_Type, MV_Tva, MV_TvaMontant, MV_TauxTva, MV_ErpTaxeNo, CT_Code, DT_Id` |
| `RT_AFFECTATION` | paiement ↔ échéance | `AF_Id, AF_Montant, MV_Id, EC_Id, DT_Id` (DT_Id = déclaration liée) |
| `RT_ECHEANCE` | échéances/factures | (à cartographier : type, document, domaine, comptabilisé) |
| `RT_ECHEANCETVA` | lignes de taxe échéance GR | (vide en dev) |
| `RT_DeclarationTva` | en-tête déclaration | `DT_Id, DT_Exercice, DT_Type, DT_MoisPeriode, DT_TrimestrePeriode, DT_DateDebut, DT_DateFin, DT_Statut` |
| `RT_LigneDeclarationTva` | lignes stockées (résultat GRFN) | `DTL_Assiette, DTL_Taux, DTL_Montant, DTL_TiersCode, DTL_TypePayement, DTL_MvNumero, DTL_DocNumero, DTL_MvDate, DTL_DocDate, DTL_EntityId, DTL_EntityType, DTL_Domaine, DTL_Prorata` |

Les **taxes des factures ERP** (branche principale) proviennent de l'**ERP Sage** via le service (port 8003), pas des tables `RT_`. → besoin du nom de la base Sage pour le recalcul complet.

## 5. Plan de réalisation (séquence de référence)

Ordre acté par le PO. Principe : **construire le fond d'abord, formater à la fin**. Les exports (Excel/XML) ne sont que des **rendus** de la déclaration finie → ils passent en dernier. Les briques pures (calcul, contrôle) se testent **sans base prod**, avec les 2 factures de TASK-002 en fixtures → elles avancent pendant que la Sélection SQL reste bloquée.

| Ordre | Brique | TASK | Statut | Bloqué par |
|---|---|---|---|---|
| 1 | Worker OM → détail TVA exact par facture | TASK-002 | ✅ fait | — |
| 2 | `n` décimales devise société (feed du calcul) | TASK-003 | ⬜ à faire | — |
| 3 | **Ventilation / proration** (calcul pur par taux) | TASK-004 | ⬜ en cours | — |
| 4 | **Modèle déclaration + contrôle** (enrichissement, agrégats par source/taux/activité, alertes ICE/IF, équilibre) | TASK-005 | ⬜ à faire | — (fixtures) |
| 5 | **Sélection SQL** (règlements/affectations locaux GRF → n° factures) | à créer | ⛔ bloqué | base prod (TASK-001) |
| 6 | **API TVA** (ASP.NET Core, Clean Arch alignée GRC_WEB, JWT, Windows Service) | TASK-012 | ⬜ à faire | TASK-007 (+008 pour réel) |
| 7 | **Front web + grilles de contrôle** (React/Vite/TS, inspiré gocom-web : `ExcelFilter` + design) — *c'est le moyen de contrôle avant dépôt (§1sexies)* | TASK-013 | ⬜ à faire | contrat TASK-012 (mockable) |
| 8 | **Export Excel** (artefact dépôt/archive) | TASK-010 | ⬜ **après front** | dépend #4 |
| 9 | **Export XML Simpl-TVA** + zip (fichier de dépôt DGI) | TASK-011 | ⬜ **après front** | dépend #4 |

> ⚠️ Le **contrôle** (totaux, alertes, équilibre) est **doublement** dispo : d'abord dans la brique **#4** (vérifiable en tests/JSON), puis restitué en **grilles interactives dans le front (#7)** — c'est *là* qu'est le « moyen de contrôle avant dépôt » demandé par le client, **pas** dans l'Excel. Les exports Excel/XML (#8/#9) ne sont que les **artefacts de dépôt/archive**, produits **après** que le front a permis de contrôler.

### Architecture cible (runtime) — Clean Architecture alignée `GRC_WEB`
- `SageTaxReader.Contracts` (`netstandard2.0`) = DTO frontière, référencé partout.
- `SageTaxReader.Core` (net48/net10-windows **x86** + COM Sage) = worker, remplit les DTO. Consommé **out-of-process** (exe JSON).
- **Domain/Application (net10.0 pur)** : `Declaration.Core` (ventilation #3 + modèle/contrôle #4) + orchestration (#7).
- **Infrastructure (net10.0-windows)** : sélection SQL (#5, Dapper), invocation worker, contrôle vs GRFN, exports (#8/#9).
- **API (net10.0-windows)** : `TASK-012` — controllers, JWT Bearer, OpenAPI, hébergé Windows Service, sert le front (`wwwroot`). Composition root câblant les couches.
- **Front (React/Vite/TS)** : `TASK-013` — SPA inspirée `gocom-web` (grilles `ExcelFilter`, design maison, axios JWT), consomme l'API.
- L'API/le cœur restent AnyCPU/x64 modernes ; seule la lecture Sage traverse le worker x86 out-of-process.

## 6. Bloquants — uniquement la Sélection SQL (#5)
Les briques #2/#3/#4 (calcul, contrôle) **ne sont pas bloquées** : fixtures = factures de TASK-002. Seule la **Sélection SQL (#5)** attend :
1. Base **réelle** GRF contenant les déclarations problématiques (le dev est vide).
2. Un **cas concret** : période + montant attendu vs obtenu (pour valider le bout-en-bout).
3. Mapping des enums `Tresorerie.Core.Enum` (`MV_Domaine`, `MV_Impaye`, `MV_Compta`) — cf. `MODULE_DECLARATION_TVA.md` §1quinquies.

> Le détail TVA vient du worker OM (base commerciale Sage `DISTRI_DEMO`, déjà accessible), pas d'une base ERP à localiser — ce bloquant historique est levé par TASK-002.

## Annexe A : Cadrage des domaines et schémas XML DGI (Simpl-TVA)

### 1. Table de mapping des domaines

Suite à l'analyse du code décompilé de `DeclarationTvaEncaissementFileGenerator` et de l'absence d'un générateur spécifique aux ventes dans GRFN, voici le mapping arrêté :

| Domaine | Formulaire DGI cible | Racine XML | Fichier(s) | Statut schéma |
|---|---|---|---|---|
| Décaissement fournisseur | Relevé de déductions | `DeclarationReleveDeduction` | 1 (regroupé) | ✅ documenté |
| Dépense (avec TVA) | Relevé de déductions | `DeclarationReleveDeduction` | Inclus dans le même fichier | ✅ documenté |
| Frais bancaire (avec TVA) | Relevé de déductions | `DeclarationReleveDeduction` | Inclus dans le même fichier | ✅ documenté |
| Encaissement client | Déclaration CA / TVA facturée | *Inconnu* | *Inconnu* | ⛔ introuvable (bloquant) |

**Décision de regroupement :**
Les décaissements, dépenses et frais bancaires sont **regroupés dans un seul et même fichier XML** de relevé de déductions. Dans le XML, la distinction se fait via la balise `<mp><id>` :
- Décaissement classique : `2` (Chèque), `4` (Virement), `5` (Traite)
- Dépense (caisse) : `1` (Espèce)
- Frais bancaire : `3` (Opération Bancaire - mappé via `EntityType == OperationBancaire`)

### 2. Schéma XML (Relevé de déductions)

**Domaines concernés :** Décaissement fournisseur, Dépense, Frais bancaire.

**En-tête et Racine :**
```xml
<DeclarationReleveDeduction>
  <identifiantFiscal>12345678</identifiantFiscal>
  <annee>2026</annee>
  <periode>6</periode> <!-- Mois (1-12) ou Trimestre (1-4) -->
  <regime>1</regime>   <!-- 1=Mensuel, 2=Trimestriel -->
  <releveDeductions>
    <!-- Lignes <rd> -->
  </releveDeductions>
</DeclarationReleveDeduction>
```

**Ligne détaillée (`<rd>`) :**
```xml
    <rd>
      <ord>1</ord>                  <!-- Numéro de ligne (1-based) -->
      <num>FAC-001</num>            <!-- Numéro de facture/document -->
      <des>Achat matériel</des>     <!-- Désignation -->
      <mht>1000.00</mht>            <!-- Assiette HT -->
      <tva>200.00</tva>             <!-- Montant TVA -->
      <ttc>1200.00</ttc>            <!-- TTC (Assiette + TVA) -->
      <refF>
        <if>11223344</if>           <!-- IF Tiers (8 caractères, sans espace) -->
        <nom>FOURNISSEUR SA</nom>   <!-- Nom Tiers -->
        <ice>123456789012345</ice>  <!-- ICE Tiers (15 caractères, sans espace) -->
      </refF>
      <tx>20</tx>                   <!-- Taux TVA (%) -->
      <prorata>100</prorata>        <!-- Prorata de déduction -->
      <mp><id>4</id></mp>           <!-- Mode de paiement (1=Espèce, 2=Chèque, 3=Op.Bancaire, 4=Virement, 5=Traite, 7=Autre) -->
      <dpai>2026-06-15</dpai>       <!-- Date de paiement (yyyy-MM-dd) -->
      <dfac>2026-06-01</dfac>       <!-- Date de facture (yyyy-MM-dd) -->
    </rd>
```

**Règles de validation (Maroc) :**
- Séparateur décimal : `.`
- IF : 8 caractères stricts, aucun espace.
- ICE : 15 caractères stricts, aucun espace.
- Exclusion : Les lignes marquées "À reporter" (`IsReport = true`) sont exclues de ce XML.

**Conditionnement et Nommage :**
- **Nom du fichier :** `{Numero_Declaration}-{Exercice}-{M|T}{periode}.xml` (ex: `142-2026-M6.xml`)
- **Encodage :** UTF-8 sans BOM (et sans déclaration `<?xml ... ?>`).
- **Archive :** Le fichier XML doit être zippé dans une archive du même nom (ex: `142-2026-M6.zip`).

### 3. Schéma XML (Encaissement Client / CA)
**Statut : Bloquant**
Le générateur est **introuvable** dans le code décompilé de GRFN (`Tresorerie.UIDeclarationTva.dll` et `Tresorerie.Core.dll` examinés).
**Action requise :** Escalade auprès du PO pour obtenir les spécifications techniques EDI "Simpl-TVA" de la DGI concernant la "Déclaration du Chiffre d'Affaires" ou un fichier XML d'exemple valide.

### 4. Note de répercussion sur les tâches en aval

- **TASK-011 (Export XML) :** Doit générer **un seul fichier XML (et son ZIP)** pour les trois domaines de déduction (décaissement, dépense, frais bancaire) en utilisant le schéma `DeclarationReleveDeduction`. La génération pour l'encaissement est suspendue en attente des spécifications.
- **TASK-012 (Endpoint API) :** L'API ne doit exposer pour l'instant qu'un seul téléchargement pour le relevé de déductions (zip unique combinant décaissement/dépense/frais).
- **TASK-013 (Front-web) :** L'interface ne doit proposer qu'un seul bouton "Télécharger le relevé de déductions (XML)" couvrant les 3 sources. La partie "Encaissement" reste en mode "Consultation/Contrôle" sur les grilles, sans bouton d'export XML pour l'instant.
