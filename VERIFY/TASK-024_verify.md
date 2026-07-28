# TASK-024 Verify — Cache des ventilations Sage

> Preuves produites sur **SQL Server `.\sql2022` / `GR_EMA_DISTRIBUTION`**
> Moteur et base identiques à la production.
> Aucun SQLite, aucun in-memory SQLite.

## Moteur de test

- Connexion : `Server=.\sql2022;Database=GR_EMA_DISTRIBUTION`
- Table : `DM_VENTILATION_SAGE_CACHE` (DDL `002_Cache_Ventilation_Sage.sql`)
- Repository : `VentilationSageCacheRepository` — SQL Server exclusif (MERGE)

## Critères de validation

| Critère | Résultat SQL Server |
|---|---|
| 2e exécution même période : 0 appel OM pour Type 0 déjà en cache | ✅ PASS (callsP2=1) |
| Paiement défait (token diverge) → cache non servi, OM rappelé | ✅ PASS (callsP3=2) |
| Égalité ventilation cache ↔ OM | ✅ PASS |
| DDL DATETIME2 / DECIMAL / NVARCHAR / PK composite correct | ✅ PASS (IT1 vert) |
| MERGE idempotent (pas de doublon) | ✅ PASS (IT3 vert) |
| Aucune ligne servie sans validation token | ✅ PASS (validation dans TryServireDepuisCache) |
| Aucun code GRFN modifié | ✅ PASS |
| VentilationSageCacheRepository SQL Server exclusif (0 SQLite) | ✅ PASS |
| Build + tests verts | ✅ PASS |

## Tests unitaires orchestrateur (T1–T8, stub in-memory)

```
T1 Première lecture → OM appelé, cache écrit        ✅
T2 2e lecture → 0 OM                                ✅
T3 Token diverge → cache non servi, OM              ✅
T4 Token null   → cache non servi, OM               ✅
T5 Pas de persistenceCS → cache désactivé           ✅
T6 Égalité ventilation cache ↔ OM                   ✅
T7 Plusieurs aff même facture → 1 seul OM           ✅
T8 EC_Type=111 FGR → jamais dans cache Sage         ✅
```

## Tests d'intégration SQL Server (IT-1 à IT-6 + Dump)

```
IT1 DDL correct (DATETIME2/DECIMAL/NVARCHAR/PK)      ✅  SQL Server
IT2 UpsertEntries + GetEntries aller-retour SQL       ✅  SQL Server
IT3 MERGE idempotent (pas de doublon)                 ✅  SQL Server
IT4 2e lecture 0 OM                                   ✅  SQL Server
IT5 Dépointage (token diverge) → OM rappelé           ✅  SQL Server
IT6 Égalité ventilation cache ↔ OM                    ✅  SQL Server
```

## Ventilation passe 1 — source OM

```json
[
  {
    "NumeroFacture": "T024-DUMP01",
    "NumeroRapprochement": "RAP001",
    "Designation": "",
    "Tiers": {
      "Numero": "",
      "Nom": "",
      "IdentifiantFiscal": "12345678",
      "Ice": "123456789012345",
      "CodeActivite": ""
    },
    "CodeActivite": "(sans activit\u00E9)",
    "HT": 1000,
    "Taux": 20,
    "Tva": 200,
    "Ttc": 1200,
    "Prorata": 100,
    "ModePaiement": "",
    "DatePaiement": null,
    "DateFacture": null,
    "Reference": null,
    "Source": 0,
    "IsReport": false
  }
]
```

## Ventilation passe 2 — source cache SQL Server

```json
[
  {
    "NumeroFacture": "T024-DUMP01",
    "NumeroRapprochement": "RAP001",
    "Designation": "",
    "Tiers": {
      "Numero": "",
      "Nom": "",
      "IdentifiantFiscal": "12345678",
      "Ice": "123456789012345",
      "CodeActivite": ""
    },
    "CodeActivite": "(sans activit\u00E9)",
    "HT": 1000,
    "Taux": 20,
    "Tva": 200,
    "Ttc": 1200,
    "Prorata": 100,
    "ModePaiement": "",
    "DatePaiement": null,
    "DateFacture": null,
    "Reference": null,
    "Source": 0,
    "IsReport": false
  }
]
```
