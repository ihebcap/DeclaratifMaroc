# TASK-039 — Filtre `MV_Domaine` manquant : des bordereaux de remise remontent dans la liste de rapprochement

## Contexte
Un bordereau de remise en banque (`BORD26060022`) apparaît dans la liste de l'interrogation « Rapprochement bancaire » (endpoint TASK-036, front TASK-037). Signalé par le PO le 09/07/2026 : « normalement ça n'a pas de sens ».

Un bordereau de remise est une **entête de regroupement** des chèques/traites remis en banque : ce n'est pas un règlement. Les chèques qu'il regroupe sont déjà comptés individuellement comme règlements. Le faire remonter dans la liste des règlements est du bruit et un risque de **double comptage** en aval.

## Cause racine
La projection de rapprochement pivote sur `RT_MOUVEMENT.MV_Id` **sans aucun filtre sur `MV_Domaine`** (nature du mouvement). Elle ramène donc tous les types de mouvements : règlements, mais aussi bordereaux, virements internes/tiers, alimentations caisse, etc.

- Requête fautive : `RapprochementFromWhere`, `Declaration.Infrastructure/Repositories/DeclarationRepository.cs:273-291` — le `WHERE` filtre `SO_Id`, `MV_Date`, `MV_Type` (mode), `MV_Point`, déclaré, tiers, **mais jamais `MV_Domaine`**.
- Ce filtre existait pourtant dès TASK-001 (`DONE_DETAIL/TASK-001_queries.sql:46` : `AND m.MV_Domaine = 1 -- ReglementFournisseur`) et a été **perdu** lors de l'écriture de TASK-036.
- Confirmation legacy : `MouvementDomaine.EnteteBordereau` est chargé pour l'affichage mais **exclu de toute logique de déclaration** (`scratch/decompiled/.../InterrogationRapprochementController.cs:118-122` → `break`, aucun traitement).

**Sémantique `MV_Domaine` confirmée par le PO sur la base de prod :**
- `0` = **encaissement** (règlement client)
- `1` = **décaissement** (règlement fournisseur)
- Autres valeurs = entités de regroupement / trésorerie non déclarables (entête bordereau, virements, alimentation caisse…).

> Ne pas confondre avec `MV_Type` = mode de règlement (0=Espèce, 1=Chèque, 2=Traite, 3=Virement).

## Périmètre STRICT
- **Inclus** : ajouter dans `RapprochementFromWhere` (et rien d'autre) une restriction `MV_Domaine IN (0, 1)` pour ne conserver que les vrais règlements (encaissements + décaissements), écartant bordereaux/virements/alimentations. Le filtre doit s'appliquer **identiquement** à la liste, au `COUNT` et aux `distincts` (mêmes prédicats FROM/WHERE) afin que pagination et totaux restent cohérents.
- **Exclu** : aucune nouvelle option d'API, aucun changement de contrat DTO, aucun recalcul TVA, aucune écriture. Ne pas toucher au tampon DT_Id (TASK-028). Ne pas modifier le front (le fix est purement back ; le front bénéficie mécaniquement de la liste nettoyée).

## Objectif
```
Entrée : GET /api/rapprochement?debut&fin (période contenant BORD26060022)
Traitement : la projection ne retient que MV_Domaine IN (0,1)
Sortie : aucun bordereau/virement/alimentation dans la liste ; seuls encaissements et décaissements ; TotalCount cohérent
```

## Étapes
1. `DeclarationRepository.cs` — ajouter au `WHERE` de `RapprochementFromWhere` : `AND M.MV_Domaine IN (0, 1)`. Vérifier que `GetReglementsRapprochementAsync`, `GetReglementsRapprochementCountAsync` réutilisent bien la même constante (déjà le cas).
2. `GetReglementsRapprochementDistinctsAsync` (`DeclarationRepository.cs:362-387`) — appliquer le **même** filtre `MV_Domaine IN (0,1)` sur les deux sous-requêtes (modes + origines) pour que les valeurs de filtres ne référencent plus des mouvements exclus.
3. (Optionnel, si le PO le confirme) restreindre à `MV_Domaine = 1` seul si le module doit rester strictement TVA fournisseur/déductible (cf. mémoire : TVA collectée hors scope). **Décision requise avant merge** — par défaut retenir `IN (0,1)` conforme à la réponse PO.

## Livrables
- `RapprochementFromWhere` et les distincts filtrés sur `MV_Domaine IN (0,1)`.
- `VERIFY/TASK-039_verify.md` : preuve réelle sur la base — même appel qui remontait `BORD26060022`, désormais absent ; comptage avant/après (nb de lignes exclues = bordereaux/virements) ; contrôle qu'aucun vrai règlement (`MV_Domaine` 0 ou 1) n'a disparu.

## Critères de validation
- `BORD26060022` (et tout autre bordereau) n'apparaît plus dans la liste ni dans `TotalCount`.
- Aucun règlement légitime (encaissement/décaissement) supprimé de la liste.
- Filtres (`distincts`) alignés : ne proposent plus de modes/origines issus des mouvements exclus.
- Aucune modification de contrat API/DTO, aucune écriture, aucun recalcul TVA.

## Risques / dépendances
- Risque faible : ajout d'un prédicat restrictif sur une lecture seule.
- Dépendance décisionnelle : périmètre `IN (0,1)` vs `= 1` (étape 3) — trancher avant merge.
- Vérifier que `MV_Domaine` n'est jamais `NULL` sur des règlements légitimes (sinon `IN (0,1)` les exclurait). À contrôler dans le VERIFY.
