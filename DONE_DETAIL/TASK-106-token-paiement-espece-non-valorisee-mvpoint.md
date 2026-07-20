# TASK-106 — Règlement espèce « Éligible » mais jamais valorisé : token de paiement exige `MV_Point=1` sans exception espèce

> **Origine** : découverte en cours de vérification de TASK-103 (17/07/2026), en tentant de
> reconstituer un cas multi-sources mêlant `Espece` + `Decaissement` dans le même onglet Achats.
> 8 règlements espèce réels et éligibles à l'écran ① (`RF26030013`→`RF26030017`, `RF26010010`→
> `RF26010012`, affectation valide, `EC_Type=0`) ajoutés à une sélection : **0/8** ont produit une
> ligne valorisée. Bloque la démonstration de la ventilation intra-onglet de TASK-103, mais surtout
> **empêche toute déclaration d'une TVA sur règlement en espèces**.

## Contexte — cause racine identifiée

Deux couches appliquent une règle **divergente** sur l'exigence de rapprochement bancaire
(`MV_Point`) pour les règlements en espèces :

| Couche | Règle sur l'espèce | Emplacement |
|---|---|---|
| **Sélection / éligibilité** | espèce **exemptée** de `MV_Point` (déclarable via `DatePaiement`, jamais rapprochée en banque) | `SelectionExpliqueeEvaluator.cs:85-90` |
| **Valorisation / token de paiement** | `MV_Point = 1` **exigé pour tout `EC_Id`**, espèce comprise | `VentilationSageCacheRepository.cs:63` (`GetCurrentPaiementToken`) |

```csharp
// SelectionExpliqueeEvaluator.cs:85-90 — l'espèce est déclarable SANS MV_Point
if (source == SourceAffectation.Espece)
{
    if (r.DatePaiement >= finExclude) motif = MotifRejet.HorsPeriode;
}
else if (r.MV_Point != GrfEnums.Point_Oui) { motif = MotifRejet.NonRapproche; }
```
```csharp
// VentilationSageCacheRepository.cs:56-65 — le token exige MV_Point=1, sans exception
var sql = @"
    SELECT TOP 1 M.MV_Id, M.MV_Point
    FROM RT_AFFECTATION A
    JOIN RT_MOUVEMENT   M ON M.MV_Id = A.MV_Id
    WHERE A.EC_Id = @EcId
      AND M.MV_Point = 1";           // ⟵ exclut structurellement l'espèce (MV_Point ≠ 1)
return conn.QuerySingleOrDefault<PaiementToken>(sql, new { EcId = ecId });
```

Conséquence : un règlement espèce sélectionné franchit l'éligibilité mais, à la valorisation,
`GetCurrentPaiementToken` renvoie `null` → la facture est traitée comme « non/dépayée » →
sortie « aucun paiement pointé (token MV NULL, non déclarable) » (`valorisation.log`). Aucune
ligne n'est produite, **silencieusement** du point de vue du montant (la facture disparaît des
totaux).

Confirmé sur données réelles (base de dev `GR_EMA_DISTRIBUTION`) : **0/8** règlements espèce
valorisés.

## Enjeu — pas seulement TASK-103

Cette anomalie n'est pas cosmétique : elle **empêche la déclaration de toute TVA sur un règlement
en espèces**, alors que le périmètre déclarable espèce est explicitement supporté par la sélection
(TASK-099 : « Gate `EstDeclarable` : espèce (auto-rapprochée) OU rapproché banque »). Cohérent avec
le trou déjà mémorisé (`grf-trou-selection-mv-decaisse`) : le pivot facture-first espèce reste un
angle mort de valorisation.

## Point de conception à trancher (ne PAS coder avant arbitrage)

Le `PaiementToken` sert de **jeton de fraîcheur du cache** (TASK-024) : la ventilation Sage est
gelée au paiement puis revalidée à la lecture par l'état `MV_Point` (détection d'un dépointage
postérieur à la mise en cache). L'espèce **n'a pas d'état de rapprochement bancaire** — il faut
donc décider **quel jeton de fraîcheur** lui appliquer :

- **Option A** : pour l'espèce, jeton = présence de l'affectation + `MV_Date`/`MV_Id` (sans
  `MV_Point`), miroir exact de l'exemption de `SelectionExpliqueeEvaluator`. Simple, aligné sur la
  sélection.
- **Option B** : jeton dédié espèce basé sur un autre invariant (à identifier) si le simple
  `MV_Id` ne garantit pas la stabilité de la ventilation.

**Contrainte impérative** : la levée de l'exigence `MV_Point` doit être **strictement bornée à la
source `Espece`** (clé `source == SourceAffectation.Espece`, comme la sélection), **jamais**
généralisée aux non-espèces — sinon on déclarerait des factures non rapprochées (régression
fiscale directe de TASK-099). Le comportement `Depense`/client espèce reste **gelé** tant que le
PO ne l'a pas arbitré (cf. commentaire `SelectionExpliqueeEvaluator.cs:83-84`).

## Périmètre STRICT

- **Inclus** :
  1. Aligner `GetCurrentPaiementToken` (et tout autre point de la lecture/validation du cache
     imposant `MV_Point=1`) sur l'exemption espèce déjà posée par `SelectionExpliqueeEvaluator`,
     bornée à la source `Espece`.
  2. Garantir qu'un règlement espèce éligible produit une (ou des) ligne(s) valorisée(s) réelle(s)
     (montants Sage/FGR selon `EC_Type`), traçées comme les autres.
  3. Si l'espèce ne peut pas être valorisée pour une **autre** raison réelle, le motif doit rester
     **explicite** (jamais un `0` muet ni une disparition silencieuse — règle n°1).
- **Exclu** :
  - Toute modification de la **sélection/éligibilité** (`SelectionExpliqueeEvaluator`) : elle est
    déjà correcte, c'est la valorisation qui diverge.
  - L'espèce **client** et l'espèce **dépense** : gelées, hors périmètre (arbitrage PO séparé).
  - Toute levée de `MV_Point` pour une source **non-espèce**.

## Objectif

```
Entrée  : règlement espèce fournisseur éligible (EC_Type=0/111, affectation valide, MV_Point≠1)
Traitement : la lecture/validation du cache reconnaît l'espèce (exemption MV_Point bornée à Espece)
Sortie  : ligne(s) TVA valorisée(s) réelle(s) produites et déclarables — plus de « token MV NULL »
          sur un règlement espèce éligible ; aucune facture qui disparaît silencieusement
```

## Livrables

- `VentilationSageCacheRepository.cs` (et éventuels appelants de la validation de token) modifié :
  exemption `MV_Point` bornée à `Espece`, conforme à l'option de conception retenue.
- Test(s) dans `Declaration.Orchestration.Tests` : un règlement espèce éligible (`MV_Point≠1`)
  produit un token non nul et une ligne valorisée ; non-régression stricte sur un non-espèce non
  rapproché (toujours rejeté / non valorisé).
- `VERIFY/TASK-106_verify.md` : preuve **sur données réelles** — les règlements espèce cités
  (`RF26030013`→`RF26030017`, `RF26010010`→`RF26010012`) passent de **0/8** valorisés à N/8, avec
  montants réels ; log `valorisation.log` ne montrant plus « token MV NULL » pour ces espèces ;
  confirmation qu'aucun non-espèce non rapproché n'est valorisé par effet de bord.

## Critères de validation

- Un règlement espèce fournisseur éligible produit une ligne valorisée réelle (plus de token nul).
- La levée d'exigence `MV_Point` est **bornée à `Espece`** (vérifié : un non-espèce non rapproché
  reste non valorisé — non-régression TASK-099).
- Aucune valorisation espèce échouée n'est silencieuse : motif explicite si échec réel.
- Le mécanisme de fraîcheur du cache (TASK-024) reste cohérent pour l'espèce (pas de service d'une
  ventilation périmée).

## Risques / dépendances

- **Risque fiscal** : élargir l'exemption `MV_Point` au-delà de l'espèce déclarerait des factures
  non rapprochées → régression directe de TASK-099. Bornage strict obligatoire.
- Dépend du modèle de cache TASK-024 (jeton de fraîcheur) : le point de conception ci-dessus doit
  être arbitré avant implémentation.
- Débloque la démonstration intra-onglet laissée en réserve dans TASK-103 (multi-sources
  `Espece`+`Decaissement` dans le même onglet Achats).
- Lié à la mémoire `grf-trou-selection-mv-decaisse` (angle mort de valorisation de l'espèce).
