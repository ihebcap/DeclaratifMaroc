# TASK-008 — Sélection SQL : éligibilité locale (règlements/affectations → AffectationADeclarer)

## Contexte
Brique #5 du module. Elle produit, pour une **période** donnée, la **liste des affectations à déclarer** en lisant **en local GRF** (`GR_EMA_DISTRIBUTION`), sans dépendre du rapprochement comptable Sage (option B, cf. `MODULE_DECLARATION_TVA.md` §1quinquies). C'est l'adaptateur qui **remplit le contrat `AffectationADeclarer`** consommé par l'orchestrateur (TASK-007).

## Périmètre STRICT
- **Uniquement** : requêtes SQL lecture seule GRF → `IEnumerable<AffectationADeclarer>` pour une période/société.
- **Exclu** : lecture du détail TVA (worker OM, TASK-002/007), calcul (TASK-004/005), exports. La sélection ne fait **pas** de TVA — elle liste *quoi* déclarer.

## Positionnement / architecture
- Nouveau projet/adaptateur **`Declaration.Selection`** (`net10.0`) — SQL direct (Dapper ou ADO) sur GRF, renvoie des `AffectationADeclarer` (`Declaration.Core.Model`).
- **Lecture seule stricte** (aucun INSERT/UPDATE/DELETE). Connexion = **configuration** (multi-client).

## Règle de dépendance
- Source d'éligibilité = **décision de TASK-001** (local `MV_Point`/`MV_PointDate` fiable OUI/NON). **Ne pas coder la requête avant** d'avoir la reco de TASK-001.
- Enums (`MV_Domaine`=ReglementFournisseur, `MV_Impaye`=NonImpaye, `MV_Compta`=EtatComptabilite) à mapper depuis `Tresorerie.Core.Enum` (valeurs int réelles à confirmer sur la base).

## Objectif
```
Entrée : société (SO_Id), période (dateDebut, dateFin), config connexion GRF
Sortie : IEnumerable<AffectationADeclarer>  (n° facture, sens, source, montantAffecte, dates, tiers{IF,ICE,nom,activité}, mode paiement)
```

### Requête cœur (décaissement fournisseur, local — §1quinquies)
```sql
SELECT ... FROM RT_MOUVEMENT
WHERE SO_Id = @so AND MV_Domaine = <ReglementFournisseur>
  AND MV_Point = 1 AND MV_PointDate >= @debut AND MV_PointDate < DATEADD(day,1,@fin)
  AND MV_DECAISSE = 1 AND MV_Compta = @comptabilise AND MV_Annule = 0 AND MV_Impaye = <NonImpaye>
  AND CA_IdOut IN (@caisses) AND <mode> IN (@modes);
```
- **Espèce** : critère `MV_Date` dans le mois (pas de `MV_PointDate`).
- Pour chaque règlement → ses **affectations non encore déclarées** (`RT_AFFECTATION WHERE DT_Id IS NULL`) → n° facture + `AF_Montant` (= `MontantAffecte`, granularité affectation → gère report/partiel).
- **Encaissement (client)**, **Dépense** (avec TVA, date dépense), **Frais bancaire** (avec TVA, date frais) : mêmes principes, sources distinctes.
- Tiers : n°, nom, **IF / ICE / code activité** (depuis GRF ou à récupérer côté Sage/OM — à cartographier).

## Contraintes techniques
- `net10.0`, SQL lecture seule, paramétré (anti-injection), connexion en config.
- Mapping enums centralisé (constantes nommées), pas de magritte int en dur dispersé.
- Robustesse : société introuvable, période vide → résultat vide + log, pas d'exception opaque.

## Étapes
1. **Attendre la reco TASK-001** (local suffisant ?). Adapter le critère si TASK-001 impose un gating complémentaire.
2. Mapper les enums réels sur `GR_EMA_DISTRIBUTION`.
3. Requête **décaissement** + **espèce** (fournisseur) → affectations non déclarées.
4. Requêtes **encaissement / dépense / frais bancaire** (sources restantes).
5. Enrichissement **tiers** (IF/ICE/activité) — définir la source (GRF vs Sage).
6. Mapper le tout en `AffectationADeclarer` (dont `Source`, `ModePaiement` code Simpl-TVA).
7. **Tests** sur `GR_EMA_DISTRIBUTION` : compter les affectations sélectionnées sur une période, recouper avec un cas connu.

## Livrables
- `Declaration.Selection` : service `SelectionnerAffectations(so, debut, fin, config)`.
- Requêtes SQL documentées/réutilisables.
- `VERIFY/TASK-008_verify.md` : nombre d'affectations par source sur une période réelle, exemples, mapping enums retenu.

## Critères de validation
- Lecture seule confirmée (aucune écriture).
- Sélection cohérente avec la reco TASK-001 (source d'éligibilité).
- Report/partiel géré à la granularité **affectation** (`DT_Id IS NULL`, `AF_Montant`).
- Les 4 sources (décaissement/espèce/dépense/frais) produites.
- Sortie = `AffectationADeclarer` directement consommable par l'orchestrateur (TASK-007).

## Risques / dépendances
- ⛔ **Bloqué** : serveur/instance + credentials de `GR_EMA_DISTRIBUTION` (⚠️ à fournir) et **reco TASK-001**.
- Enums int à confirmer sur base réelle (risque de mauvais domaine/état).
- Source IF/ICE/code activité : peut nécessiter un aller côté Sage (tiers) → à cartographier ; ne pas bloquer la sélection principale pour autant.
- **Prérequis** TASK-001 (source fiable) + contrat `AffectationADeclarer` (TASK-005).
