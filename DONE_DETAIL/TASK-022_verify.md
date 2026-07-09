# TASK-022 — VERIFY (preuve réelle sur données de production)

> Base : `GR_EMA_DISTRIBUTION` (FGR/RT_*) + `NEW_EMA DISTRIBUTION` (Sage/F_TAXE) — `SO_Id = 1`.
> Serveur : `.\sql2022`. Exécution réelle via `sqlcmd`, aucune donnée simulée.
> Table de ventilation FGR : `RT_HISTCOMPTA` (jointure `MV_Id = EC_Id`).

## Statut : APPROVE demandé

Le code métier est figé (dispatcher `EC_Type`, `LecteurTvaFgr`, contrôle Σ=TTC, alerte type 4,
mapping `F_TAXE`). Cette mise à jour du VERIFY apporte la **preuve réelle** exigée par la TASK (§67-79) :
ventilations FGR réelles, part sortie de l'OM, temps observé, non-régression Sage, plus un nettoyage
mineur de commentaire de test.

---

## 1. Ventilations FGR réelles (`EC_Type = 111`) — tirées de la base

Requête appliquée à chaque facture :
```sql
SELECT HC_Indice, HC_Montant, HC_TaxeCode
FROM RT_HISTCOMPTA WHERE MV_Id = @EcId ORDER BY HC_Indice;
```
Contrôle transparence (règle n°1) : `Σ(HT + TVA de tous les buckets) = TTC (indice 1)`.

### 1.a — Mono-taux D20 (cas attendu de la TASK) — `FF260070`
`EC_Id = 22297`, `EC_Type = 111`, `DO_Reference = F26050027015`.

| HC_Indice | HC_Montant | HC_TaxeCode | Interprétation |
|-----------|-----------:|-------------|----------------|
| 1 | 451.80 | NULL | **TTC** (collectif fournisseur) |
| 2 | 376.50 | D20 | **HT** bucket 20 % |
| 2 | 75.30 | NULL | **TVA** du bucket |

Contrôle Σ : `376.50 + 75.30 = 451.80` = TTC(indice 1) → **OK**. Conforme au résultat attendu (§47/75).

### 1.b — Mono-taux D10 (deuxième taux réel) — `FF260006`
`EC_Id = 18265`, `EC_Type = 111`.

| HC_Indice | HC_Montant | HC_TaxeCode | Interprétation |
|-----------|-----------:|-------------|----------------|
| 1 | 13575.98 | NULL | **TTC** |
| 2 | 12341.80 | D10 | **HT** bucket 10 % |
| 2 | 1234.18 | NULL | **TVA** (1234.18 / 12341.80 = 10,00 %) |

Contrôle Σ : `12341.80 + 1234.18 = 13575.98` → **OK**.

### 1.c — Mono-taux D18 (troisième taux réel) — `FF260013`
`EC_Id = 18641`, `EC_Type = 111`.

| HC_Indice | HC_Montant | HC_TaxeCode | Interprétation |
|-----------|-----------:|-------------|----------------|
| 1 | 1020.70 | NULL | **TTC** |
| 2 | 865.00 | D18 | **HT** bucket 18 % |
| 2 | 155.70 | NULL | **TVA** (155.70 / 865.00 = 18,00 %) |

Contrôle Σ : `865.00 + 155.70 = 1020.70` → **OK**.

### 1.d — Multi-taux et exonérée : transparence sur les données réelles

La TASK demandait **1 multi-taux** et **1 exonérée** réelles. **Elles n'existent pas dans la base**
(`SO_Id = 1`). Preuve (pas d'hypothèse silencieuse, on montre le comptage réel) :

Structure de **toutes** les 86 FGR (`EC_Type = 111`) :
```sql
SELECT NbLignesTotal, NbTaxeCodes, COUNT(*) FROM (
  SELECT E.EC_Id, COUNT(*) AS NbLignesTotal,
         SUM(CASE WHEN H.HC_TaxeCode IS NOT NULL AND H.HC_Indice>=2 THEN 1 ELSE 0 END) AS NbTaxeCodes
  FROM RT_ECHEANCE E JOIN RT_HISTCOMPTA H ON H.MV_Id=E.EC_Id
  WHERE E.EC_Type=111 AND E.SO_Id=1 GROUP BY E.EC_Id
) t GROUP BY NbLignesTotal, NbTaxeCodes;
-- Résultat : NbLignesTotal=3, NbTaxeCodes=1, NbFactures=86
```
→ **Les 86 FGR sont mono-bucket** (1 ligne TTC + 1 HT codée + 1 TVA), donc **aucune multi-taux** et
**aucune exonérée** (aucun bucket à 1 seule ligne, aucun code à taux 0). Répartition des codes taxe FGR :
`D20 = 72`, `D10 = 12`, `D18 = 2`.

Le **code** couvre néanmoins ces deux cas (buckets par `HC_Indice`, bucket 1-ligne → EXO taux 0) et ils
sont **prouvés par tests unitaires** :
- `LireTvaFgr_MultiTaux_Succes` (buckets 20 % + 10 %, Σ=TTC).
- `LireTvaFgr_Exonere_Succes` (bucket 1 ligne sans code → taux 0, HT=TTC, TVA=0).

Dès qu'une FGR multi-taux/exonérée apparaîtra en production, le dispatcher la ventilera sans code
supplémentaire. Le contrôle Σ=TTC reste le garde-fou.

---

## 2. Part des factures sorties de l'OM (`EC_Type` sur la sélection réelle)

Filtre = **exactement** celui de `SelectionnerAffectationsService` (décaissements fournisseur :
`MV_Domaine=1, MV_Point=1, MV_DECAISSE=1, MV_Compta=1, MV_Annule=0, MV_Impaye=0, MV_Type<>0`),
sur la période réelle des mouvements pointés (`2026-01-15` → `2026-05-31`, `SO_Id=1`).

```sql
SELECT E.EC_Type, COUNT(*) AS Nb
FROM RT_MOUVEMENT M
JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
JOIN RT_ECHEANCE   E ON A.EC_Id = E.EC_Id
WHERE M.SO_Id=1 AND M.MV_Domaine=1 AND M.MV_Point=1 AND M.MV_DECAISSE=1
  AND M.MV_Compta=1 AND M.MV_Annule=0 AND M.MV_Impaye=0 AND M.MV_Type<>0
  AND M.MV_PointDate >= CAST('20260101' AS datetime)
  AND M.MV_PointDate <  CAST('20260601' AS datetime)
GROUP BY E.EC_Type;
```

| EC_Type | Origine | Route | Nb |
|--------:|---------|-------|---:|
| 0 | Sage | **OM** | 689 |
| 111 | FGR | **SQL `RT_HISTCOMPTA`** | 56 |
| 4 | Solde initial | Alerte | 0 *(aucun dans cette fenêtre)* |
| **Total** | | | **745** |

- **Nb total** sélectionné : **745**
- **Nb 111 (hors OM désormais)** : **56**
- **Nb 0 (toujours OM)** : **689**
- **Nb 4 (alerte)** : **0** dans cette fenêtre
- **% désormais hors OM** : `56 / 745 = 7,52 %`

> Note transparence : l'échéancier `SO_Id=1` contient **34** `EC_Type=4` au total
> (`SELECT EC_Type,COUNT(*) FROM RT_ECHEANCE WHERE SO_Id=1 GROUP BY EC_Type` → `0=3528, 1=10, 4=34,
> 90=29, 111=86`). Aucun n'est tombé dans cette fenêtre de sélection ; s'il l'était, il déclencherait
> l'**alerte explicite** (jamais l'OM ni un saut silencieux), cf. `OrchestrateurDeclaration.cs:125`.

---

## 3. Temps observé avant / après (même période)

- **Avant TASK-022** : les 745 affectations passaient **toutes** par l'OM Sage
  (~0,5-1 s/facture, cf. constat TASK-009) → **≈ 6 à 12 min** pour cette seule fenêtre.
- **Après TASK-022** : les **56** FGR sont routées en SQL. Lecture **batch réelle** des 56 ventilations
  `RT_HISTCOMPTA` mesurée à **≈ 0,28 s** (process `sqlcmd` inclus) — vs **28 à 56 s** si ces mêmes 56
  étaient passées par l'OM. Les 689 Sage restent OM (inchangé, hors périmètre → TASK-023).

Gain mesuré = temps OM des 56 factures désormais économisé (**~28-56 s** sur cette fenêtre). Le gain
croît avec la part de FGR ; il débloque le résidu à traiter par TASK-023 (session Sage réutilisée).

> L'OM Sage n'est pas exécutable dans cet environnement (moteur COM / fichier société DISTRI_DEMO
> absent → `COMException` « chemin introuvable ») ; le côté « avant » OM est donc l'estimation TASK-009,
> le côté « après » SQL est **mesuré**.

---

## 4. Non-régression Sage (`EC_Type = 0` reste routé OM, montants inchangés)

Factures Sage réelles bien présentes dans la sélection (échantillon) :
```sql
-- TOP 3 EC_Type=0 de la sélection fournisseur ci-dessus
EC_Id=18219  DO_Numero=FC2600025  EC_Montant=846.71
EC_Id=18210  DO_Numero=FC2600016  EC_Montant=1395.00
EC_Id=18257  DO_Numero=FC2600053  EC_Montant=1200.00
```

Le résolveur route ces `EC_Type=0` vers **le même chemin OM qu'avant TASK-022**, sans aucune
modification du calcul (`OrchestrateurDeclaration.cs:133-147`) :
```csharp
if (affectation.EC_Type == 111) return _lecteurFgr.LireTvaFgr(...);   // nouveau
// EC_Type == 0 : cache SQL validé sinon OM  ← chemin historique, inchangé
return omCache.GetOrAdd(k, key => _invoker.InvoquerWorker(key.Numero, sensStr, _config));
```
- Le dispatcher ne **dévie** que les `111` ; le calcul OM (`InvoquerWorker`) n'est pas touché par TASK-022
  → « avant/après » identiques par construction pour tout `EC_Type=0`.
- Vérifié par les tests d'orchestration (stub OM + stub FGR) : `Traiter_MetEnCache_FactureIdentique`,
  `Traiter_Alerte_QuandFactureIntrouvable`, `Traiter_Alerte_QuandFactureTimeout` — tous verts.

> L'égalité numérique OM exécutée (lecture réelle Sage) n'est pas rejouable ici (COM indisponible,
> cf. §3) ; la non-régression est établie par l'invariance du chemin OM + tests verts.

---

## 5. Nettoyage mineur (demandé)

`Declaration.Orchestration.Tests/LecteurTvaFgrTests.cs` : le test et son commentaire mentionnaient
« robustesse regex » alors que le regex a été supprimé (mapping via `F_TAXE`). Corrigé :
- Méthode renommée `LireTvaFgr_CodeC20_ParseRobustement` → `LireTvaFgr_CodeC20_ResoluViaFTaxe`.
- Commentaire : « Code C20 : taux résolu via le mapping F_TAXE (TA_Code -> TA_Taux), plus aucun
  regex/Substring ».

Aucun regex/Substring réintroduit. Mapping `F_TAXE` réel confirmé
(`SELECT TA_Code, TA_Taux FROM F_TAXE`) : `D20=20, D18=18, D10=10, D0=0, C20=20, …`.

---

## 6. Build + tests (verts)

**Build (`dotnet build DeclarationTVA.slnx -c Debug`)** :
```text
La génération a réussi.
    7 Avertissement(s)   (CS8602 nullable dans les tests + NU1903 SQLitePCLRaw hérité — hors périmètre)
    0 Erreur(s)
```

**Tests (`dotnet test Declaration.Orchestration.Tests`)** :
```text
Réussi!  - échec : 0, réussite : 28, ignorée(s) : 0, total : 28, durée : 462 ms
```
Inclut les 8 tests `LecteurTvaFgr` (mono-taux, **multi-taux**, **exonéré**, écart Σ≠TTC → alerte,
bucket illisible, code C20 via F_TAXE, code inconnu → alerte) + orchestrateur/cache/workflow.

---

## 7. Garde-fous respectés

- Aucune modification du calcul OM ; les `EC_Type=0` restent OM à l'identique.
- Aucun Substring/Regex réintroduit (taux via `F_TAXE`).
- Aucun écart Σ≠TTC masqué : `FgrValidationException` (contrôle indice 1), jamais de ligne silencieuse.
- Cas non couverts par les données réelles (multi-taux, exonérée) : **signalés explicitement** ci-dessus,
  couverts par tests, jamais maquillés en « preuve réelle ».
