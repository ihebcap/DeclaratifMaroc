# TASK-047 — Sourcer le TTC document depuis la valorisation OM (et non l'en-tête SQL)

> **Origine :** découverte connexe de TASK-046 (§6 du VERIFY). Décision PO : le TTC document
> doit venir de la **valorisation OM** (source qui fait foi, comme la TVA), pas de l'en-tête
> `DO_TotalTTC`. Séquencement : après TASK-046.

## Contexte (faits mesurés)
`SageTaxReader.Core/SageTaxReaderService.cs`, méthode `ExtraireTaxes` :
```csharp
TotalTtc = doc.DO_TotalTTC,   // ← en-tête SQL
```
L'en-tête `DO_TotalTTC` est la source du TTC document. Or :
- L'OM (`valo.TotalTTC`) **recoupe l'en-tête** sur les pièces saines et **conserve le signe**
  (avoirs négatifs — `AV2500043 → -1920`, pas de normalisation positive comme pour le HT).
- Sur une pièce à en-tête TTC cassé/nul, l'OM recalcule la valeur depuis les lignes.
- Cohérence de principe : la TVA par taux vient déjà de l'OM ; le TTC doit venir de la même
  source qui fait foi.

**Distinction importante (anti-fabrication) :** l'écart `header − Σ lignes` observé sur
certaines pièces (ex. `FC2501166` : header/`DO_NetAPayer`/`DO_MontantRegle` = 4522, Σ lignes =
4527,74, **sans escompte ni frais**) est un **écart réel** de la pièce Sage, pas un champ cassé.
`OM.TotalTTC` = 4522 = en-tête ⇒ le passage à l'OM **ne masque pas** cet écart (`EcartArrondi`
reste −5,74, visible). On ne substitue **pas** Σ lignes (qui fabriquerait un TTC ≠ facturé/réglé).

## Objectif
```
Entrée  : toutes pièces (achat/vente comptabilisées, avoirs)
Étape   : TotalTtc = valo.TotalTTC (OM) au lieu de doc.DO_TotalTTC
Sortie  : TTC = OM, signe conservé (avoirs négatifs), écarts réels toujours visibles,
          non-régression sur les pièces saines (OM == en-tête)
```

## Contrainte de non-régression
- Pièces saines (`OM.TotalTTC == DO_TotalTTC`) : `TotalTtc` et `EcartArrondi` inchangés.
- Avoirs : TTC négatif conservé (OM ne normalise pas le TTC en positif).
- Aucune valeur fabriquée : on ne remplace jamais par Σ lignes ; l'écart réel reste tracé.

## Livrables
- Correctif `ExtraireTaxes` (`TotalTtc = Math.Round(valo.TotalTTC, decimales, …)`).
- `VERIFY/TASK-047_verify.md` : preuve OM==en-tête sur pièces saines, avoir négatif, écart réel
  préservé, non-régression FC2600001-004.

## Critères de validation
- `TotalTtc` sourcé de `valo.TotalTTC`, plus de `doc.DO_TotalTTC`.
- Avoirs : TTC négatif.
- Non-régression sur pièces saines.
- Lecture seule stricte ; aucune écriture GRFN.

## Point ouvert (hors périmètre worker — à vérifier après test PO)
Signe des **avoirs/retours** dans le **front** : les sources facture (`EC_Montant`, cache
`GRC_VENTILATION_SAGE_CACHE`) sont déjà signées négatives. Le seul montant non signé est
`RT_MOUVEMENT.MV_Montant` (toujours positif) sur l'écran **Rapprochement/règlements**. À
confirmer par le PO lors du test avant tout changement d'affichage.
