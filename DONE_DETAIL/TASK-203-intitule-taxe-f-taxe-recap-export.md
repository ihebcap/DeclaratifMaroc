# TASK-203 — Ajouter l'intitulé de taxe (`F_TAXE.TA_Intitule`) au récap et à l'export de contrôle (suite TASK-198)

Status: 🆕 à faire
Priority: MEDIUM
Module: Declaration.Orchestration / Declaration.Core / Declaration.Export.Excel / declaration-tva-web

> **Origine :** demande PO (06/08/2026) — dans l'écran ④ Confirmer (récap TVA, cf. TASK-202) et dans
> l'export Excel de l'état de contrôle, ajouter le **code taxe** et l'**intitulé taxe** issus de la
> table Sage `F_TAXE`. **La clé de regroupement reste inchangée : (taux de TVA, code taxe)** — ce
> n'est pas une nouvelle demande, TASK-198 (terminée le 05/08/2026) l'a déjà établie ; cette TASK
> ajoute uniquement l'**intitulé** (le libellé humain du code), absent jusqu'ici.

## Constat (preuve de code)

TASK-198 a déjà propagé `TA_Code` (le code) jusqu'au regroupement et à l'affichage, mais **jamais
`TA_Intitule`** (le libellé) :

- `Declaration.Orchestration/LecteurTvaFgr.cs:131` : `SELECT TA_Code, TA_Taux FROM F_TAXE` — ne lit
  pas l'intitulé.
- `Declaration.Core/Model.cs:98-109` (`RecapParTaux`) : porte `Taux` et `CodeTaxe`, aucun champ
  intitulé.
- `Declaration.Export.Excel/Exporter.cs:176-179` (`EcrireBlocRecapParTaux`, réutilisé par la feuille
  « Récap » de l'export dépôt **et** la feuille « Détail TVA » de l'export de contrôle) : le code taxe
  n'est même pas dans sa propre colonne — il est concaténé en texte dans la colonne « Taux » :
  `$"{recap.Taux:G} ({recap.CodeTaxe})"`.
- `declaration-tva-web/src/VerifierIntegrerPanel.tsx:920` : le tableau « Sous-totaux par taux TVA »
  affiche déjà `codeTaxe` dans sa propre colonne côté écran (contrairement à l'Excel) — mais pas
  d'intitulé.

Il existe déjà dans le projet un précédent direct pour lire un couple code/intitulé depuis une table
Sage-like : `DeclarationRepository.cs:1689` — `SELECT DTA_Code AS Code, DTA_Intitule AS Libelle, ...
FROM P_DECTVAACTIVITE` (table des codes activité). Même patron de nommage à reprendre pour `F_TAXE`.

> ⚠️ **À vérifier avant de coder** : le nom exact de la colonne d'intitulé dans `F_TAXE` côté Sage
> (`TA_Intitule` est l'hypothèse la plus probable par cohérence avec la convention Sage `XX_Code` /
> `XX_Intitule` déjà observée sur `P_DECTVAACTIVITE.DTA_Intitule`, mais **non confirmé par une lecture
> directe du schéma `F_TAXE`** dans cette session — à vérifier en base avant d'écrire la requête).

## Objectif

```
Entrée  : F_TAXE (TA_Code, TA_Taux, TA_Intitule) — lecture Sage, comme TA_Code/TA_Taux déjà lus.
Traitement : propager TA_Intitule le long de la MÊME chaîne que TA_Code (TASK-198) — aucun nouveau
             point de regroupement, la clé reste (Taux, CodeTaxe).
Sortie  : - Écran ④ Confirmer (récap) : tableau « Sous-totaux par taux TVA » gagne une colonne
            « Intitulé taxe », à côté de la colonne « Code taxe » déjà existante.
          - Export Excel (dépôt ET contrôle, même bloc partagé `EcrireBlocRecapParTaux`) : la colonne
            « Taux » actuelle qui concatène `Taux (CodeTaxe)` en texte est scindée en 3 colonnes
            distinctes : Taux, Ta_Code, Ta_Intitule.
```

## Périmètre STRICT

- **Inclus** :
  1. Recenser **tous** les points de résolution de `TA_Code` (même liste que TASK-198 :
     `LecteurTvaFgr.cs`, `SelectionExpliqueeService.cs` frais bancaire, chemin OM/Sage classique) et
     y ajouter la lecture de `TA_Intitule` en parallèle — même requête SQL, un champ de plus.
  2. Ajouter `Intitule` (ou `IntituleTaxe`) à `RecapParTaux` (`Declaration.Core/Model.cs`) et à tout
     DTO front intermédiaire qui porte déjà `CodeTaxe` (`LigneCandidateDto.cs`,
     `DeclarationsController.cs:479-483`, type TS `codeTaxe?: string` dans
     `VerifierIntegrerPanel.tsx`).
  3. Écran ④ Confirmer : tableau des sous-totaux par taux — ajouter une colonne « Intitulé taxe »
     après « Code taxe ».
  4. Export Excel — **scinder** la colonne actuelle `Taux (CodeTaxe)` (`Exporter.cs:176-179`) en 3
     colonnes distinctes : `Taux`, `Ta_Code`, `Ta_Intitule` (libellés de colonne à confirmer avec le
     PO — repris ici tels qu'il les a nommés). Ce changement s'applique aux **deux** feuilles qui
     réutilisent `EcrireBlocRecapParTaux` : « Récap » (export dépôt) et « Détail TVA » (export de
     contrôle) — pas seulement l'export de contrôle, puisque c'est le même bloc de code.
  5. **Clé de regroupement inchangée** : `(Taux, CodeTaxe)` — l'intitulé est un enrichissement
     d'affichage 1:1 avec le code, il n'entre jamais dans la clé de regroupement ni dans un `GroupBy`.
- **Exclu** :
  - Aucun changement à l'export XML de dépôt légal (hors périmètre, comme pour TASK-198).
  - Aucun recalcul de montant — uniquement un champ d'affichage supplémentaire.
  - Le tableau « Sous-totaux » n'est pas déplacé ni restructuré au-delà de l'ajout de colonne (le
    reste de sa mise en page, cf. TASK-202, est traité par cette autre TASK).

## Livrables

- `RecapParTaux` porte l'intitulé taxe, propagé depuis `F_TAXE.TA_Intitule` (nom de colonne SQL à
  confirmer en base avant de coder).
- Écran ④ : colonne « Intitulé taxe » visible dans le tableau des sous-totaux.
- Export Excel (dépôt + contrôle) : colonnes `Taux` / `Ta_Code` / `Ta_Intitule` distinctes, plus de
  concaténation texte.
- `VERIFY/TASK-203_verify.md` : preuve sur un cas réel avec deux codes taxe au même taux (même
  scénario que le VERIFY de TASK-198) — intitulé correct pour chacun, export Excel et écran cohérents
  entre eux, aucun changement de montant.

## Critères de validation

- L'intitulé taxe affiché correspond exactement à `F_TAXE.TA_Intitule` pour le `TA_Code` de la ligne.
- Deux codes taxe au même taux restent distincts avec leur propre intitulé, sur l'écran **et**
  dans l'export.
- Regroupement toujours strictement `(Taux, CodeTaxe)` — non-régression sur les totaux (TASK-198).
- Non-régression sur l'export XML de dépôt (non touché).

## Risques / dépendances

- **Dépend de TASK-198** (terminée) — réutilise sa chaîne de propagation, ne la refait pas.
- **Dépend potentiellement de TASK-202** pour l'emplacement exact du tableau des sous-totaux
  (écran ④ Confirmer) — si TASK-202 n'est pas encore développée au moment de traiter celle-ci,
  appliquer l'ajout de colonne sur l'écran récap actuel (« Vérifier & Intégrer »), à son emplacement
  présent (`VerifierIntegrerPanel.tsx:920`), et le développeur de TASK-202 reprendra la colonne lors
  de la scission d'écran.
- Risque de nom de colonne SQL incorrect (`TA_Intitule` est une hypothèse, pas une certitude) — à
  vérifier en base avant d'écrire la requête, même remarque que le garde-fou de TASK-198.

## Files

- [Declaration.Orchestration/LecteurTvaFgr.cs:123-144](../Declaration.Orchestration/LecteurTvaFgr.cs#L123-L144) (`GetTaxes`, requête `F_TAXE`).
- [Declaration.Selection/SelectionExpliqueeService.cs:130-147](../Declaration.Selection/SelectionExpliqueeService.cs#L130-L147) (résolution frais bancaire, même table).
- [Declaration.Core/Model.cs:98-109](../Declaration.Core/Model.cs#L98-L109) (`RecapParTaux`).
- [Declaration.Export.Excel/Exporter.cs:157-199](../Declaration.Export.Excel/Exporter.cs#L157-L199) (`EcrireBlocRecapParTaux`, colonnes à scinder).
- [Declaration.API/Dtos/LigneCandidateDto.cs](../Declaration.API/Dtos/LigneCandidateDto.cs) et [Declaration.API/Controllers/DeclarationsController.cs:479-483](../Declaration.API/Controllers/DeclarationsController.cs#L479-L483) (DTO/groupby côté API).
- [declaration-tva-web/src/VerifierIntegrerPanel.tsx:900-925](../declaration-tva-web/src/VerifierIntegrerPanel.tsx#L900-L925) (tableau sous-totaux, colonne à ajouter).
