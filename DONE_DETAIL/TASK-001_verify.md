# Rapport de Vérification : Rapprochement GRF ↔ Sage (TASK-001)

## 1. Objectif et Contexte
L'objectif de cette tâche est de vérifier si la source locale GRF (`RT_MOUVEMENT.MV_Point` et `MV_PointDate`) est suffisamment fiable et synchronisée avec l'ERP Sage (`F_ECRITUREC.EC_TresoPiece` et `EC_DateRappro`) pour servir de source unique de vérité dans le module de déclaration de TVA.

**Cadrage Société (SO_Id) :**
La base de trésorerie `GR_EMA_DISTRIBUTION` ne contient qu'une seule société (`SO_Id = 1`, Raison Sociale : `NEW_EMA DISTRIBUTION`). Cette société s'interface avec la base Sage du même nom `[NEW_EMA DISTRIBUTION]`. Bien que la base soit mono-société, le filtre `SO_Id = 1` a été maintenu dans les requêtes par rigueur.

**Mapping des constantes (Enums) :**
Pour valider le périmètre, nous avons extrait les valeurs des énumérations via réflexion sur l'assembly `Tresorerie.Core.dll` :
- `RT_MOUVEMENT.MV_Domaine = 1` correspond à `Tresorerie.Core.Enum.MouvementDomaine.ReglementFournisseur`. C'est le périmètre des décaissements.
- `RT_LigneDeclarationTva.DTL_Domaine = 2` correspond à `Tresorerie.Core.Enum.LigneDeclarationTvaEncaissementDomaine.Decaissement`.

## 2. Clé de Jointure et Taux de Correspondance
La jointure entre GRF et Sage s'effectue via la table image locale `RT_HISTCOMPTA` :
`RT_HISTCOMPTA.HC_No = [NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC.cbMarq`.

**Taux de correspondance :**
- **Total lignes `RT_HISTCOMPTA` :** 2 676
- **Lignes jointes avec succès à Sage :** 2 600 (**Taux : 97,16 %**)
- **Orphelins (76 lignes) :**
  - 45 lignes ont `HC_No = 0` (ce qui indique des écritures GRF non encore envoyées/comptabilisées dans Sage).
  - 31 lignes ont un `HC_No > 0` mais n'ont pas d'équivalent dans Sage (vraisemblablement supprimées dans l'ERP après coup).

Malgré ce petit pourcentage d'orphelins (qui n'apparaissent naturellement pas côté Sage), la jointure reste robuste pour les données consolidées.

## 3. Contrôle de Recopie Image (RT_HISTCOMPTA ↔ Sage)
L'ancien algorithme pouvait s'appuyer sur les colonnes "image" `HC_PieceTreso` et `HC_DateRappro` dans `RT_HISTCOMPTA` pour éviter de requêter Sage. Nous avons comparé ces colonnes locales avec la vraie table Sage `F_ECRITUREC`.
- **Résultat :** Nous avons identifié **1 écart** (`HC_No = 7443`), où la table image considère l'écriture rapprochée (`EXT 30/04/2026`) alors que Sage l'affiche non rapprochée (`1753-01-01`).
- **Conclusion :** La recopie image `RT_HISTCOMPTA` n'est pas fiable à 100 %.

## 4. Résultats de la Synchronisation (Source Locale vs Sage)
Nous avons comparé les statuts de rapprochement pour l'ensemble des **règlements fournisseurs avec affectation** (`MV_Domaine = 1`), en utilisant une logique dédupliquée (certains `MV_Id` générant plusieurs lignes comptables dont une seule est la banque).

- **Source locale :** `MV_Point = 1` (issue de l'extrait bancaire local).
- **Source Sage :** `EC_TresoPiece <> ''` (pointage côté Sage).

| Indicateur | Quantité (Règlements Uniques) |
|---|---|
| **Total Règlements Locaux Rapprochés** | 352 |
| **Total Règlements Sage Rapprochés** | 352 |
| **Présents en Local mais absents de Sage** | 0 |
| **Présents dans Sage mais absents du Local** | 0 |
| **Dates de rapprochement différentes** | 0 (Aucun écart de période) |

**Bilan de simulation :**
Il n'y a **aucune divergence (0%)**. Tout règlement fournisseur rapproché en local correspond parfaitement à une écriture de trésorerie rapprochée dans Sage, avec des dates rigoureusement identiques.

## 5. Réconciliation avec les Déclarations Existantes
Nous avons réconcilié l'historique de l'application (lignes sauvegardées dans `RT_LigneDeclarationTva`) avec les données sources locales de GRF (`RT_MOUVEMENT`) pour les Décaissements (`DTL_Domaine = 2`).

- **Total Lignes Déclarées (Décaissements) :** 1 068
- **Lignes correspondantes à un règlement local rapproché (`MV_Point = 1`) :** 1 008
- **Lignes correspondantes à un règlement non rapproché (`MV_Point = 0`) :** 60

**Justification des 60 écarts :**
Ces 60 lignes correspondent toutes exclusivement à des **Espèces** (`DTL_TypePayement = 0`). Les espèces ne faisant l'objet d'aucun rapprochement bancaire par nature, elles ont été correctement déclarées sans attendre un `MV_Point`. 
Ainsi, **100% des lignes non-espèces déclarées** dans l'application existante (soit 1008/1008) correspondent bien à la donnée locale `MV_Point = 1`.

## 6. Recommandations
- **Source locale (`MV_Point` / `MV_PointDate`) suffisante ? OUI.**
La synchronisation locale des règlements fournisseurs est parfaite (352/352, et 1008/1008 des déclarations historiques).
- **Action pour le nouveau module :** Conformément à l'option B du PO, le module cible doit être fondé exclusivement sur la requête locale `RT_MOUVEMENT` pour la sélection des décaissements. Cette décision élimine le besoin d'interroger la table `F_ECRITUREC` de Sage pour l'éligibilité, garantit l'intégrité de la sélection, et lève le risque lié aux 1 écarts image (`RT_HISTCOMPTA`).

## 7. Scripts SQL de contrôle
Toutes les requêtes SQL documentant ce rapport (y compris le détail du taux de correspondance, la jointure, le cadrage société, et la réconciliation existante) sont livrées et rejouables dans le fichier joint :
`D:\_vibe\GRF\VERIFY\TASK-001_queries.sql`
