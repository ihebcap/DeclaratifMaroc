# TASK-006 Verify

## Validation Checklist
- [x] Le contrôle d'équilibre produit un écart/résidu non nul sur une facture à parafiscal/escompte.
- [x] TIERS_SANS_IF se déclenche sur un tiers sans identifiant fiscal.
- [x] Prorata présent sur chaque LigneDeclarationEnrichie ; champ Designation existant.
- [x] Tests verts ; aucune régression sur la ventilation.

## Dump JSON (montrant le résidu non nul et les alertes)
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
      "NumeroFacture": "25FA01371",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F001",
        "Nom": "Fournisseur 1",
        "IdentifiantFiscal": "12345678",
        "Ice": "123456789012345",
        "CodeActivite": "ACT1"
      },
      "CodeActivite": "ACT1",
      "HT": 224.42,
      "Taux": 14,
      "Tva": 31.42,
      "Ttc": 255.84,
      "Prorata": 48.706517256719064055564394890,
      "ModePaiement": "2",
      "DatePaiement": "2026-07-01T00:00:00",
      "DateFacture": "2026-06-15T00:00:00",
      "Source": 0,
      "IsReport": false
    },
    {
      "NumeroFacture": "25FA01371",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F001",
        "Nom": "Fournisseur 1",
        "IdentifiantFiscal": "12345678",
        "Ice": "123456789012345",
        "CodeActivite": "ACT1"
      },
      "CodeActivite": "ACT1",
      "HT": 12282.61,
      "Taux": 20,
      "Tva": 2456.52,
      "Ttc": 14739.13,
      "Prorata": 48.706517256719064055564394890,
      "ModePaiement": "2",
      "DatePaiement": "2026-07-01T00:00:00",
      "DateFacture": "2026-06-15T00:00:00",
      "Source": 0,
      "IsReport": false
    },
    {
      "NumeroFacture": "25FA01371",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F001",
        "Nom": "Fournisseur 1",
        "IdentifiantFiscal": "12345678",
        "Ice": "123456789012345",
        "CodeActivite": "ACT1"
      },
      "CodeActivite": "ACT1",
      "HT": 0,
      "Taux": 20,
      "Tva": 0,
      "Ttc": 0,
      "Prorata": 48.706517256719064055564394890,
      "ModePaiement": "2",
      "DatePaiement": "2026-07-01T00:00:00",
      "DateFacture": "2026-06-15T00:00:00",
      "Source": 0,
      "IsReport": false
    },
    {
      "NumeroFacture": "G0110",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F002",
        "Nom": "Fournisseur 2",
        "IdentifiantFiscal": "",
        "Ice": "",
        "CodeActivite": ""
      },
      "CodeActivite": "(sans activit\u00E9)",
      "HT": 26.77,
      "Taux": 20,
      "Tva": 5.36,
      "Ttc": 32.13,
      "Prorata": 50.0,
      "ModePaiement": "1",
      "DatePaiement": "2026-07-02T00:00:00",
      "DateFacture": "2026-06-16T00:00:00",
      "Source": 1,
      "IsReport": false
    },
    {
      "NumeroFacture": "G0110",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F002",
        "Nom": "Fournisseur 2",
        "IdentifiantFiscal": "",
        "Ice": "",
        "CodeActivite": ""
      },
      "CodeActivite": "(sans activit\u00E9)",
      "HT": 6.33,
      "Taux": 10,
      "Tva": 0.64,
      "Ttc": 6.97,
      "Prorata": 50.0,
      "ModePaiement": "1",
      "DatePaiement": "2026-07-02T00:00:00",
      "DateFacture": "2026-06-16T00:00:00",
      "Source": 1,
      "IsReport": false
    },
    {
      "NumeroFacture": "25FA01371",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F003",
        "Nom": "Fournisseur 3",
        "IdentifiantFiscal": "1234567",
        "Ice": "123456789012345",
        "CodeActivite": ""
      },
      "CodeActivite": "(sans activit\u00E9)",
      "HT": 1.50,
      "Taux": 14,
      "Tva": 0.21,
      "Ttc": 1.71,
      "Prorata": 0.324710115044793760370429300,
      "ModePaiement": "",
      "DatePaiement": null,
      "DateFacture": null,
      "Source": 0,
      "IsReport": false
    },
    {
      "NumeroFacture": "25FA01371",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F003",
        "Nom": "Fournisseur 3",
        "IdentifiantFiscal": "1234567",
        "Ice": "123456789012345",
        "CodeActivite": ""
      },
      "CodeActivite": "(sans activit\u00E9)",
      "HT": 81.88,
      "Taux": 20,
      "Tva": 16.38,
      "Ttc": 98.26,
      "Prorata": 0.324710115044793760370429300,
      "ModePaiement": "",
      "DatePaiement": null,
      "DateFacture": null,
      "Source": 0,
      "IsReport": false
    },
    {
      "NumeroFacture": "25FA01371",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F003",
        "Nom": "Fournisseur 3",
        "IdentifiantFiscal": "1234567",
        "Ice": "123456789012345",
        "CodeActivite": ""
      },
      "CodeActivite": "(sans activit\u00E9)",
      "HT": 0,
      "Taux": 20,
      "Tva": 0,
      "Ttc": 0,
      "Prorata": 0.324710115044793760370429300,
      "ModePaiement": "",
      "DatePaiement": null,
      "DateFacture": null,
      "Source": 0,
      "IsReport": false
    },
    {
      "NumeroFacture": "25FA01371",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F004",
        "Nom": "Fournisseur 4",
        "IdentifiantFiscal": "12345678",
        "Ice": "12345678901234",
        "CodeActivite": ""
      },
      "CodeActivite": "(sans activit\u00E9)",
      "HT": 1.50,
      "Taux": 14,
      "Tva": 0.21,
      "Ttc": 1.71,
      "Prorata": 0.324710115044793760370429300,
      "ModePaiement": "",
      "DatePaiement": null,
      "DateFacture": null,
      "Source": 0,
      "IsReport": false
    },
    {
      "NumeroFacture": "25FA01371",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F004",
        "Nom": "Fournisseur 4",
        "IdentifiantFiscal": "12345678",
        "Ice": "12345678901234",
        "CodeActivite": ""
      },
      "CodeActivite": "(sans activit\u00E9)",
      "HT": 81.88,
      "Taux": 20,
      "Tva": 16.38,
      "Ttc": 98.26,
      "Prorata": 0.324710115044793760370429300,
      "ModePaiement": "",
      "DatePaiement": null,
      "DateFacture": null,
      "Source": 0,
      "IsReport": false
    },
    {
      "NumeroFacture": "25FA01371",
      "NumeroRapprochement": "",
      "Designation": "",
      "Tiers": {
        "Numero": "F004",
        "Nom": "Fournisseur 4",
        "IdentifiantFiscal": "12345678",
        "Ice": "12345678901234",
        "CodeActivite": ""
      },
      "CodeActivite": "(sans activit\u00E9)",
      "HT": 0,
      "Taux": 20,
      "Tva": 0,
      "Ttc": 0,
      "Prorata": 0.324710115044793760370429300,
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
      "TotalHT": 12673.79,
      "TotalTva": 2521.12,
      "TotalTtc": 15194.91
    },
    {
      "Source": 1,
      "TotalHT": 33.10,
      "TotalTva": 6.00,
      "TotalTtc": 39.10
    }
  ],
  "RecapsParTaux": [
    {
      "Taux": 14,
      "Collecte": false,
      "TotalHT": 227.42,
      "TotalTva": 31.84,
      "TotalTtc": 259.26
    },
    {
      "Taux": 20,
      "Collecte": false,
      "TotalHT": 12473.14,
      "TotalTva": 2494.64,
      "TotalTtc": 14967.78
    },
    {
      "Taux": 10,
      "Collecte": false,
      "TotalHT": 6.33,
      "TotalTva": 0.64,
      "TotalTtc": 6.97
    }
  ],
  "RecapsParActivite": [
    {
      "CodeActivite": "ACT1",
      "Collecte": false,
      "TotalHT": 12507.03,
      "TotalTva": 2487.94,
      "TotalTtc": 14994.97
    },
    {
      "CodeActivite": "(sans activit\u00E9)",
      "Collecte": false,
      "TotalHT": 199.86,
      "TotalTva": 39.18,
      "TotalTtc": 239.04
    }
  ],
  "ControleEquilibre": {
    "TotalMontantAffecte": 15508,
    "TotalDeclareTtc": 15234.01,
    "ResiduNonTva": 273.99,
    "ResiduExplique": 132.14683071562862254722096849,
    "ResiduInexplique": 141.84316928437137745277903151
  },
  "Alertes": [
    {
      "Niveau": 1,
      "Code": "LIGNE_A_ZERO",
      "Message": "Ligne \u00E0 0 (assiette nulle).",
      "RefLigne": "Facture: 25FA01371, Taxe: ZERO"
    },
    {
      "Niveau": 2,
      "Code": "TIERS_SANS_ICE",
      "Message": "Tiers sans ICE.",
      "RefLigne": "Facture: G0110"
    },
    {
      "Niveau": 2,
      "Code": "TIERS_SANS_IF",
      "Message": "Tiers sans identifiant fiscal.",
      "RefLigne": "Facture: G0110"
    },
    {
      "Niveau": 0,
      "Code": "SANS_ACTIVITE",
      "Message": "Code activit\u00E9 manquant.",
      "RefLigne": "Tiers: F002"
    },
    {
      "Niveau": 2,
      "Code": "IF_INVALIDE",
      "Message": "Identifiant fiscal \u2260 8 caract\u00E8res ou contenant des espaces.",
      "RefLigne": "Facture: 25FA01371"
    },
    {
      "Niveau": 0,
      "Code": "SANS_ACTIVITE",
      "Message": "Code activit\u00E9 manquant.",
      "RefLigne": "Tiers: F003"
    },
    {
      "Niveau": 1,
      "Code": "LIGNE_A_ZERO",
      "Message": "Ligne \u00E0 0 (assiette nulle).",
      "RefLigne": "Facture: 25FA01371, Taxe: ZERO"
    },
    {
      "Niveau": 2,
      "Code": "ICE_INVALIDE",
      "Message": "ICE \u2260 15 caract\u00E8res ou contenant des espaces.",
      "RefLigne": "Facture: 25FA01371"
    },
    {
      "Niveau": 0,
      "Code": "SANS_ACTIVITE",
      "Message": "Code activit\u00E9 manquant.",
      "RefLigne": "Tiers: F004"
    },
    {
      "Niveau": 1,
      "Code": "LIGNE_A_ZERO",
      "Message": "Ligne \u00E0 0 (assiette nulle).",
      "RefLigne": "Facture: 25FA01371, Taxe: ZERO"
    },
    {
      "Niveau": 2,
      "Code": "FACTURE_INTROUVABLE",
      "Message": "Facture introuvable (DTO non fourni).",
      "RefLigne": "Facture: INCONNU"
    },
    {
      "Niveau": 2,
      "Code": "REGLEMENT_NON_AFFECTE",
      "Message": "R\u00E8glement rapproch\u00E9 non affect\u00E9 (affectation attendue absente).",
      "RefLigne": "Facture/R\u00E8glement: REG_1"
    },
    {
      "Niveau": 1,
      "Code": "EQUILIBRE_RESIDU_INEXPLIQUE",
      "Message": "R\u00E9sidu inexpliqu\u00E9 de 141,84 (tol\u00E9rance: 0,55).",
      "RefLigne": "Global"
    }
  ]
}
```
