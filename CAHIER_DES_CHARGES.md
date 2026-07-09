# Cahier des charges — Module Déclaration TVA déductible (Maroc)

Version 3 — 08/07/2026 (**validé PO, toutes sections**). Analyse technique de référence : `MODULE_DECLARATION_TVA.md`.

**Module autonome** de bout en bout : sélection (SQL local GRF) → calcul (Objets Métier Sage) → contrôle + justificatif → export Excel → export XML de dépôt. Aucune dépendance à l'app existante ; multi-client (piloté par `P_SOCIETE`).

## 1. Objet
Nouveau module (séparé de GRFN, source inaccessible) produisant la **déclaration de TVA déductible** marocaine (relevé de déductions), avec un **écran de contrôle** et un **export Excel** consultables **avant dépôt**, puis un **export XML** de dépôt.

Objectif prioritaire : **fiabilité du calcul** (éliminer les erreurs et arrondis de l'ancien module) et **transparence** (justifier chaque montant).

## 2. Périmètre

### Inclus
- Déclaration de la **TVA déductible** (décaissement) sur 4 sources : règlements fournisseurs rapprochés, règlements espèce, dépenses, frais bancaires.
- Écran de contrôle + justificatif par ligne.
- Export **Excel** (état avant dépôt).
- Export **XML** de dépôt (relevé de déductions).

### Exclu / hors périmètre
- **Encaissement (TVA collectée)** : **affichage seul**, aucun export, aucun calcul de TVA à payer.
- Calcul de la **TVA à payer / crédit / report** (fait ailleurs ou manuellement).
- **RAS / retenue à la source** (module distinct).
- Modification de GRFN.

## 3. Règles métier (inclusion)

Exemple : en 07/2026 on déclare le mois 06/2026. **La date de facture ne sert jamais à décider l'inclusion (affichage seulement).**

| Source | Critère de date | Conditions |
|---|---|---|
| Décaissement fournisseur (chèque/traite/virement) | **date de rapprochement** dans le mois | rapproché + **affecté** à une facture, affectation non déclarée |
| Décaissement espèce | **date de règlement** dans le mois | affecté à une facture, affectation non déclarée |
| Dépense | **date de la dépense** | uniquement **avec TVA** |
| Frais bancaire | **date du frais** | uniquement **avec TVA** |

- **Report / partiel** : géré à la granularité **affectation**. Une facture réglée en plusieurs fois = plusieurs affectations, chacune déclarée le mois de son rapprochement. Le « reste à déclarer » = affectations non encore déclarées. Critère réel = *rapproché ≤ fin de période* **ET** *affectation non déclarée*.
- **Source du rapprochement = locale GRF fiable** : `RT_MOUVEMENT.MV_Point` / `MV_PointDate` (issue du pointage sur extraits `RT_EXTRAITLIGNE`). **Ne pas** utiliser `RT_HISTCOMPTA.HC_PieceTreso`/`HC_DateRappro` (image recopiée de Sage).

## 4. Sources de données

> **Principe de généricité (multi-client) — RÈGLE ABSOLUE.** Rien n'est codé en dur : ni les **noms de bases** (GRF / Sage), ni les **noms de colonnes Sage** (ICE, IF, nature, code activité, désignation), ni le prorata, ni les comptes/journaux. **Tout est lu depuis le paramétrage société** (`P_SOCIETE`) et la configuration de connexion, **par client**. Les valeurs ci-dessous observées sur la base de test ne sont que des exemples.

| Donnée | Origine | Accès |
|---|---|---|
| Sélection règlements / affectations / dates rappro | Base GRF **configurée** (`RT_MOUVEMENT`, `RT_AFFECTATION`, `RT_ECHEANCE`) | SQL local |
| **Détail TVA facture** (HT / taux / TVA / TTC par taux) | Base Sage **configurée** | **Objets Métier Sage** (exact, sans recalcul) |
| Tiers / ICE / IF / nature / code activité | Sage, **colonnes désignées par la config** | via service ERP |

### 4bis. Paramétrage société (`P_SOCIETE`) — le contrat de configuration
Le module lit ces champs et **s'adapte** à chaque client :

| Champ `P_SOCIETE` | Rôle | Exemple (base de test — NON constant) |
|---|---|---|
| `SO_Identifiant` | IF de la société (en-tête XML) | `123456` |
| `SO_DecTvaProrata` | Prorata d'assujettissement | `100.000000` |
| `SO_DecTvaColNameIceFrs` | **Nom** de la colonne Sage contenant l'ICE fournisseur | `CT_Siret` |
| `SO_DecTvaColNameIdentifiantFrs` | **Nom** de la colonne Sage contenant l'IF fournisseur | `CT_Identifiant` |
| `SO_ErpColNameNatureFournisseur` | Nom colonne Sage nature tiers | `NautreFournisseur` |
| `SO_ErpColNameCodeActiviteMarrocFournisseur` | Nom colonne Sage code activité | `ActiviteFournisseur` |
| `SO_DecTvaColNameDesignationDocument` | Nom colonne désignation facture | `DesignationDoc` |
| `SO_DecTvaJournal`, `SO_DecTvaCompteCrediteur`, `SO_DecTvaCompteDebiteur` | Journal / comptes déclaration | — |

⇒ La lecture de l'ICE/IF côté Sage se fait **sur la colonne nommée par la config**, jamais sur un nom en dur (cf. code existant `GetAllIceTiersToMaroc(colIce, colIdentifiant, colNature, colCodeActivite, …)`).

Règle : le détail TVA d'une facture est **lu une seule fois via BO** puis **mis en cache** pour toutes les affectations qui la pointent (évite les allers-retours).

## 5. Calcul (règle anti-erreur)

Pour chaque affectation (paiement partiel ou total d'une facture) :

```
proration_paiement = montant_affecté / TTC_facture        (pleine précision, AUCUN arrondi intermédiaire)

Pour chaque taux i de la facture (HT_i, TVA_i issus de Sage BO) :
    HT_déclaré_i  = ROUND(HT_i  × proration_paiement, n, AwayFromZero)
    TVA_déclaré_i = ROUND(TVA_i × proration_paiement, n, AwayFromZero)
    TTC_déclaré_i = HT_déclaré_i + TVA_déclaré_i
```
- `n` = nombre de décimales de la devise société.
- **Arrondi arithmétique** (`AwayFromZero`), pas l'arrondi bancaire de l'ancien module.
- **Paiement total** (`montant_affecté = TTC_facture`, proration = 1) : reprendre les valeurs Sage **telles quelles** (pas d'arrondi) → zéro dérive.
- **Interdit** : passer par un ratio arrondi puis diviser (bug de l'ancien module).
- **Prorata d'assujettissement** (`SO_DecTvaProrata`) : **porté comme champ déclaré** (`<prorata>`), **PAS multiplié dans le montant**. La TVA déclarée reste la TVA pleine ; l'application du prorata se fait en aval (administration). Conforme à l'ancien module.

## 6. Écran de contrôle (exigence client) + justificatif

Consultable tant que la déclaration est **EnCours** (avant clôture/dépôt).

**Contrôle d'équilibre :**
```
Total déclaration = Σ décaissement rapproché + Σ espèce + Σ dépense + Σ frais bancaire
```
Récapitulatifs : **par source**, **par taux**, **par code activité**.

**Justificatif par ligne** (aide à la personne qui déclare) : chaque montant déclaré est déplié pour montrer sa traçabilité :
`facture n° · HT facture · TTC facture · montant affecté · proration (= affecté/TTC) · taux · base déclarée · TVA déclarée · prorata assujettissement · date rapprochement / date règlement`
**+ (validé PO)** : `banque · journal · pièce trésorerie / n° extrait bancaire (rapprochement) · mode de règlement · n° règlement`.

**Alertes bloquantes/avertissements avant dépôt :**
- tiers sans **ICE** ou **identifiant fiscal** ; identifiant ≠ 8 caractères ; ICE ≠ 15 caractères ; espaces interdits ;
- facture introuvable côté Sage ;
- règlement rapproché **non affecté** (à traiter ou ignorer) ;
- ligne à assiette ou montant = 0.

## 7. Export Excel (avant dépôt)
- **Feuille Détail** : 1 ligne / affectation — n° facture, désignation, tiers, IF, ICE, HT, taux, TVA, TTC, mode de paiement, date paiement, date facture, source.
- **Feuille Récap** : totaux par source / par taux / par code activité + le contrôle d'équilibre.
- Généré sans clôturer la déclaration.

## 8. Export XML de dépôt (relevé de déductions)
- **Décision PO** : le **module est autonome** et **génère lui-même le XML** (aucune dépendance à l'app existante pour le dépôt). On **réutilise le format connu** de l'ancien module en le réimplémentant proprement.
- Format `DeclarationReleveDeduction` (identifié dans l'ancien module — à re-valider avec la DGI / Simpl-TVA).
- **ICE = 15 caractères STRICT (bloquant)** ; IF = 8 caractères ; sans espaces. Un tiers non conforme **bloque le dépôt** (données test à 14 = anomalie à corriger).
- Champs par ligne : `num, des, mht (HT), tva, ttc, refF(if/nom/ice), tx, prorata, mp (mode), dpai, dfac`.
- En-tête : `identifiantFiscal, annee, periode (mois 1-12 / trimestre 1-4), regime (1 mensuel / 2 trimestriel)`.
- Modes : 1=espèce, 2=chèque, 3=opération bancaire, 4=virement, 5=traite, 7=autre.
- Séparateur décimal `.` ; fichier zippé ; nom `{Numero}-{Exercice}-{M|T}{période}`.
- Validations Maroc identiques à l'écran de contrôle (IF 8 car., ICE 15 car., sans espaces).

## 9. Contraintes techniques
- Composant **.NET** référençant les Objets Métier Sage (réutilise `SageBO.Core` / `SageCommercial.*`).
- **Lecture seule** sur les bases (aucune écriture GRF/Sage).
- Sélection = SQL local (performant) ; montants = BO Sage (exact) ; cache facture.

## 10. Hypothèses / points à confirmer
- **Multi-client** : bases (GRF/Sage) et colonnes Sage (ICE/IF/nature/activité/désignation) **toujours** issues du paramétrage `P_SOCIETE` + config de connexion — jamais en dur. Les bases `TRESO_DISTRICAP`/`DISTRI_DEMO` et la colonne `CT_Siret` observées = **base de test uniquement**.
- Format XML Simpl-TVA à re-valider (DGI).
- Prorata d'assujettissement = `P_SOCIETE.SO_DecTvaProrata` (confirmé) ; règle si < 100 % à valider.
- `MV_Point`/`MV_PointDate` posé uniquement via extraits (pas de synchro Sage) — cf. TASK-001.
- Comportement multi-taux et arrondi à valider sur cas réels.
- **ICE = 15 caractères, contrôle STRICT bloquant** (validé PO). Données test à 14 = anomalie de données.
- **Dépôt XML** : tranché — **module autonome**, génère le XML lui-même (réimplémente le format connu). Pas de dépendance à l'app existante.

## 11. Livrables
1. Ce cahier des charges validé.
2. Module .NET (sélection + calcul + contrôle + exports Excel/XML).
3. Jeu de tests de non-régression (cas : paiement total, partiel, multi-taux, espèce, dépense, frais).
