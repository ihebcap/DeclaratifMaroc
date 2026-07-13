# TASK-046 — Rollup HT document faux sur comptabilisées (`doc.DO_TotalHT` nul) → faux `EcartArrondi`

> **Défaut préexistant révélé par TASK-045.** Tant que les factures `EC_Type=0` échouaient
> toutes (« Valeur invalide ! »), ce bug était invisible. Depuis le correctif énum de 045, les
> pièces se lisent et le défaut apparaît sur ~12 % d'un échantillon (7/60).
> **La TVA par taux reste exacte** — c'est le total HT **document** qui est faux, donc l'écart
> d'arrondi affiché. Séquencement : après TASK-045.

## Contexte (faits mesurés)
`SageTaxReader.Core/SageTaxReaderService.cs`, méthode `ExtraireTaxes` :
```csharp
double htLignes = doc.DO_TotalHT;                 // ← source du HT document
...
TotalHT = Math.Round(htLignes + frais, ...),       // Total HT document
TotalHTNet = TotalHT - Escompte,
EcartArrondi = TotalTtc - (TotalHTNet + TotalTva + TotalParafiscale);
```
Sur certaines factures **comptabilisées**, l'en-tête Sage a `DO_TotalHT = 0` alors que le HT
réel est dans `DO_TotalHTNet` (et dans la somme des bases de taxe). Le rollup document devient
faux, ce qui gonfle `EcartArrondi`.

**Preuve `FC2501768` (NEW_EMA, achat cpta) :**
- DB `F_DOCENTETE` : `DO_TotalHT = 0`, `DO_TotalHTNet = 420`, `DO_TotalTTC = 504`.
- DB `F_DOCLIGNE` : HT 420 / TTC 504 / `D20` 20 %.
- OM `valo.Taxes` (correct) : `BaseHT = 420`, `MontantTva = 84`, code `D20`.
- Worker rend : `TotalHT = 0`, `TotalTva = 84` (juste), `TotalTtc = 504`, `EcartArrondi = 420` ❌.

Sur le lot de 60 pièces : 46 écarts nuls, quelques ±0,01–0,51 (arrondi normal), **7 écarts
énormes** (420 → 37035) tous du même mécanisme `DO_TotalHT` non peuplé.

## Référence produit (à respecter)
`VERIFY/REX_OM_TAXES.md` **interdit explicitement** l'usage de `doc.DO_TotalHT` pour tout
calcul lié à la TVA :
> « Les montants globaux HT (`doc.DO_TotalHT`) de l'en-tête ne doivent **jamais** être utilisés
> pour recalculer la TVA. La vraie base d'imposition (`TaxeBase`) est souvent différente. »

Le code actuel viole cette règle. La logique de ventilation TVA relève de **TASK-004**.

## Objectif
```
Entrée  : factures comptabilisées où DO_TotalHT=0 (HT réel en DO_TotalHTNet / bases de taxe)
Étape   : sourcer le HT document sans jamais dépendre de DO_TotalHT
Sortie  : TotalHT/TotalHTNet document exacts ; EcartArrondi ~0 ; TVA par taux inchangée (déjà juste)
```

## Pistes (à trancher, sans fabriquer de valeur)
1. **HT document = Σ des bases de taxe (`valoTaxe.BaseCalcul`) + frais non taxés**, cohérent
   avec le REX (`TaxeBase` fait foi). Attention : les frais de port HT peuvent être hors base
   taxable — les réintégrer explicitement (déjà lus via SQL dans `ExtraireTaxes`).
2. **Repli `DO_TotalHTNet`** quand `DO_TotalHT` est nul/incohérent — plus simple, mais valider
   qu'il intègre bien frais/escompte de la même façon que le calcul actuel.
3. Ne **pas** toucher au calcul d'escompte ni à l'extraction TVA (déjà exacts).

## Contrainte de non-régression
- Les pièces déjà à `EcartArrondi=0` (46/60) **doivent le rester** exactement.
- La TVA par taux (`LignesTaxe[].BaseHT`/`MontantTva`) **ne change pas** (elle vient de la
  valorisation OM, prouvée exacte en TASK-045).
- Cache TASK-024 : si la structure des buckets change, prévoir l'invalidation.

## Livrables
- Correctif `ExtraireTaxes` (source HT document), net48 pur, aucune valeur fabriquée.
- `VERIFY/TASK-046_verify.md` : avant/après sur les 7 pièces à écart (dont `FC2501768`),
  recoupé `DO_TotalHTNet` / `F_DOCLIGNE` ; re-run du lot 60 → écarts d'arrondi ramenés à ~0 ;
  preuve de non-régression sur les pièces déjà justes.

## Critères de validation
- `EcartArrondi` des 7 pièces ramené à l'arrondi normal (≤ quelques centimes), prouvé vs Sage.
- Aucune régression sur les pièces déjà exactes.
- TVA par taux strictement inchangée.
- `DO_TotalHT` n'est plus la source du HT document.
- Lecture seule stricte ; aucune écriture GRFN.

## Risques / dépendances
- **Dépend de TASK-045** (lecture OM débloquée) — sans elle, non testable.
- Touche la **valorisation cœur (TASK-004)** : revue attentive, périmètre limité au rollup HT.
- Impact possible sur le **cache de ventilation TASK-024** si les buckets HT sont recalculés.
