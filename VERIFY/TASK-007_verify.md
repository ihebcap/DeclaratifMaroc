# TASK-007 Verify

## Validation
- [x] Worker invocable out-of-process (simulé via IWorkerInvoker ou par vrai process).
- [x] Cache prouvé : pour FAC001, invoqué 1 seule fois malgré 2 affectations (InvocationCount = 2 pour 3 affectations).
- [x] Erreur worker → alerte FACTURE_INTROUVABLE sans crash.
- [x] Aucune référence COM/Sage dans Orchestration (`net10.0` pur).

## Résultat JSON pour 3 affectations (2 factures)
```json
{
  "EnTete": {
    "IdentifiantSociete": "",
    "Exercice": 0,
    "Type": 0,
    "MoisPeriode": null,
    "TrimestrePeriode": null,
    "Numero": ""
  },
  "Lignes": [
    {
      "NumeroFacture": "FAC001",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "",
        "Nom": "",
        "IdentifiantFiscal": "12345678",
        "Ice": "123456789012345",
        "CodeActivite": ""
      },
      "CodeActivite": "(sans activit\u00E9)",
      "HT": 500.0,
      "Taux": 20,
      "Tva": 100.0,
      "Ttc": 600.0,
      "Prorata": 50.0,
      "ModePaiement": "",
      "DatePaiement": null,
      "DateFacture": null,
      "Source": 0,
      "IsReport": false
    },
    {
      "NumeroFacture": "FAC001",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "",
        "Nom": "",
        "IdentifiantFiscal": "12345678",
        "Ice": "123456789012345",
        "CodeActivite": ""
      },
      "CodeActivite": "(sans activit\u00E9)",
      "HT": 500.0,
      "Taux": 20,
      "Tva": 100.0,
      "Ttc": 600.0,
      "Prorata": 50.0,
      "ModePaiement": "",
      "DatePaiement": null,
      "DateFacture": null,
      "Source": 0,
      "IsReport": false
    },
    {
      "NumeroFacture": "FAC002",
      "NumeroRapprochement": "",
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
      "Source": 0,
      "IsReport": false
    }
  ],
  "RecapsParSource": [
    {
      "Source": 0,
      "TotalHT": 2000.0,
      "TotalTva": 400.0,
      "TotalTtc": 2400.0
    }
  ],
  "RecapsParTaux": [
    {
      "Taux": 20,
      "TotalHT": 2000.0,
      "TotalTva": 400.0,
      "TotalTtc": 2400.0
    }
  ],
  "RecapsParActivite": [
    {
      "CodeActivite": "(sans activit\u00E9)",
      "TotalHT": 2000.0,
      "TotalTva": 400.0,
      "TotalTtc": 2400.0
    }
  ],
  "ControleEquilibre": {
    "TotalMontantAffecte": 2400,
    "TotalDeclareTtc": 2400.0,
    "ResiduNonTva": 0.0,
    "ResiduExplique": 0.0,
    "ResiduInexplique": 0.0
  },
  "Alertes": [
    {
      "Niveau": 0,
      "Code": "SANS_ACTIVITE",
      "Message": "Code activit\u00E9 manquant.",
      "RefLigne": "Tiers: "
    }
  ]
}
```
