# TASK-022 — Routing par `EC_Type` + lecteur TVA FGR en SQL (`RT_HISTOCOMPTA`)

## Contexte
Le retour du worker sur **TASK-009** a révélé un défaut structurel : l'orchestrateur (TASK-007) appelle
l'**objet métier Sage (OM)** pour **chaque** facture (~0,5-1 s/facture → ~35 min pour 2085), alors que les
factures **créées dans GRF (FGR, `EC_Type = 111`)** ont leur ventilation TVA **directement en SQL** dans
`RT_HISTOCOMPTA`. C'est à la fois :
- une **erreur de correction** : une FGR ne devrait **jamais** passer par l'OM (son détail est local) ;
- la **cause principale du goulot** perf (tout est envoyé à l'OM sans distinction).

Modèle confirmé sur données réelles (voir mémoire `grf-echeance-ectype-mapping`) :
`RT_ECHEANCE.EC_Type` discrimine l'origine → **0** = Sage (OM obligatoire), **111** = FGR (SQL
`RT_HISTOCOMPTA`), **4** = solde initial GRF (TVA non tranchée → TASK-025).

## Périmètre STRICT
- **Back uniquement.** Trois briques :
  1. **Exposer `EC_Type` (et `EC_Id`)** sur `AffectationADeclarer` — les requêtes de
     `SelectionnerAffectationsService` / `SelectionExpliqueeService` joignent **déjà** `RT_ECHEANCE (E)`,
     donc ajout de `E.EC_Type`, `E.EC_Id` au SELECT + au mapping.
  2. **Dispatcher par `EC_Type`** dans l'orchestrateur : `111` → lecteur FGR SQL ; `0` → OM (inchangé) ;
     `4` → **alerte explicite** « TVA solde initial non gérée (TASK-025) », **jamais** OM ni saut silencieux.
  3. **Lecteur FGR SQL** : reconstruire la ventilation TVA depuis `RT_HISTOCOMPTA`.
- **Exclu** : optimisation de la lecture Sage (session réutilisée → TASK-023), cache/matérialisation
  (TASK-024), traitement métier du solde initial (TASK-025), front. **Aucun changement du calcul** côté OM.

## Positionnement / architecture
- Nouveau lecteur `Declaration.Core` (ou adaptateur dédié) : `ILecteurTvaFgr` lisant `RT_HISTOCOMPTA` en
  SQL (Dapper), produisant la **même structure de sortie** que le lecteur OM (ventilation par taux :
  base HT, taux, TVA, TTC) pour être interchangeable dans l'orchestrateur.
- Le dispatcher vit dans l'orchestrateur (TASK-007) : il route **par affectation** selon `EC_Type`.

## Objectif
```
Entrée : AffectationADeclarer (désormais avec EC_Type + EC_Id)
Routing : EC_Type=111 → lecteur FGR SQL ; EC_Type=0 → OM ; EC_Type=4 → alerte (non géré)
Sortie  : ventilation TVA par taux, identique quel que soit le lecteur
```

### Règle de reconstruction FGR (`RT_HISTOCOMPTA`, jointure `MV_Id = EC_Id`)
`HC_Indice` = **un bucket par taux** :
- `indice 1` → **TTC** (ligne collectif fournisseur, compte 4411).
- `indice ≥ 2` avec **2 lignes** → un taux : **HT** = ligne portant `HC_TaxeCode` (ex. `D20` = 20 %),
  **TVA** = l'autre ligne (sans code). Taux déduit de `HC_TaxeCode`.
- `indice` avec **1 seule ligne sans code** → **exonéré** (HT, taux 0).
- **Contrôle transparence obligatoire** : `Σ(HT + TVA de tous les buckets) = TTC (indice 1)`. En cas
  d'écart → **alerte** (règle n°1 : aucune ligne silencieuse), on ne masque rien.
- Exemple réel `FF260070` : TTC 451.80 (indice 1) = HT 376.50 (indice 2, `D20`) + TVA 75.30.

## Contraintes techniques
- `net10.0`, SQL lecture seule, Dapper ; isolation `RT_*` (comme TASK-017).
- Mapping `HC_TaxeCode` → taux : réutiliser/étendre la table de correspondance existante (ne pas coder en
  dur si un mapping existe déjà).
- `decimal` pour tous les montants.
- **Aucune régression** : les Type 0 continuent de passer par l'OM à l'identique ; tests TASK-007/008/015/017
  restent verts.
- Sortie FGR **strictement interchangeable** avec la sortie OM (même contrat consommé par la ventilation).

## Étapes
1. Ajouter `EC_Type` + `EC_Id` au SELECT + mapping des requêtes de sélection ; enrichir `AffectationADeclarer`.
2. Implémenter `ILecteurTvaFgr` (lecture `RT_HISTOCOMPTA` + règle indice + contrôle Σ=TTC).
3. Ajouter le dispatcher `EC_Type` dans l'orchestrateur (111→FGR, 0→OM, 4→alerte).
4. Tests d'intégration sur `GR_EMA_DISTRIBUTION` : une FGR réelle (ex. `FF260070`) ventilée en SQL =
   ventilation attendue ; une facture Sage réelle reste routée OM à l'identique.
5. Mesurer l'impact : **nombre de factures désormais hors OM** (part des 111 sur une période réelle) et
   temps avant/après → à consigner dans le VERIFY.

## Livrables
- Sélection enrichie (`EC_Type`, `EC_Id`) + `AffectationADeclarer` étendu.
- `ILecteurTvaFgr` (lecteur SQL `RT_HISTOCOMPTA`) + dispatcher `EC_Type` dans l'orchestrateur.
- Tests d'intégration (FGR ventilée en SQL, Sage inchangée, contrôle Σ=TTC prouvé, cas exonéré + multi-taux).
- `VERIFY/TASK-022_verify.md` : preuve réelle (ventilation d'une FGR mono-taux + une multi-taux + une
  exonérée), non-régression Sage, part des factures sorties de l'OM, temps observé.

## Critères de validation
- Une FGR (`EC_Type=111`) est ventilée **sans appel OM**, résultat = attendu, contrôle Σ(HT+TVA)=TTC OK.
- Multi-taux et exonéré correctement reconstruits (buckets par `HC_Indice`).
- Une facture Sage (`EC_Type=0`) reste routée OM, montants inchangés (non-régression).
- Un `EC_Type=4` produit une **alerte explicite**, jamais un saut silencieux ni un mauvais calcul OM.
- Prouvé sur `GR_EMA_DISTRIBUTION` / `SO_Id=1`. Build + tests verts.

## Risques / dépendances
- **Prérequis** : sélection (TASK-008/015) et orchestrateur (TASK-007) — faits. Base prod dispo (TASK-001/017).
- **Mapping `HC_TaxeCode` → taux** : vérifier l'exhaustivité des codes rencontrés sur données réelles
  (D20, D14, D10, D7, exo…) ; un code inconnu → **alerte**, pas d'hypothèse silencieuse.
- **Débloque directement** la réduction du goulot : plus la part de FGR est grande, plus TASK-023 (Sage)
  a peu de volume résiduel à traiter.
- Découplé de TASK-009 (qui reste bloquée sur son VERIFY incomplet + le `JOIN RT_ECHEANCE`).
