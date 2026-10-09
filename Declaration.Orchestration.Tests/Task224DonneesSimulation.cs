using System;
using System.Collections.Generic;
using Declaration.Application.Entities;
using Declaration.Core.Model;
using Declaration.Selection;
using SageTaxReader.Contracts;

namespace Declaration.Orchestration.Tests
{
    public static class Task224DonneesSimulation
    {
        public static Dictionary<string, DocumentTaxesInfo> CreerFactures()
        {
            var dict = new Dictionary<string, DocumentTaxesInfo>();

            void Ajouter(string numPiece, string tiers, double ht, double tva, double ttc)
            {
                dict[numPiece] = new DocumentTaxesInfo
                {
                    NumeroPiece = numPiece,
                    Designation = tiers,
                    TypeDocument = 6, // Achat
                    Sens = "Achat",
                    TotalHT = ht,
                    TotalHTNet = ht,
                    TotalTva = tva,
                    TotalTtc = ttc,
                    LignesTaxe = new List<TaxeDetail>
                    {
                        new TaxeDetail
                        {
                            Code = "T20",
                            Intitule = "TVA Récupérable 20%",
                            Type = "TaxeTypeTVADebit",
                            Taux = 20.0,
                            BaseHT = ht,
                            MontantTva = tva,
                            TTC = ttc
                        }
                    }
                };
            }

            Ajouter("FF260029", "LOGINFO INGENIERIE", 133739.00, 26747.80, 160486.80);
            Ajouter("FF260027", "UNION MAGHREB DES TE", 123791.00, 24758.20, 148549.20);
            Ajouter("FF260028", "SNSS TECHNOLOGIES", 67012.31, 13402.46, 80414.77);
            Ajouter("FF260030", "MAROC TELECOM", 1033.33, 206.67, 1240.00);
            Ajouter("FF260031", "MAROC TELECOM", 433.30, 86.66, 519.96);
            Ajouter("FF260026", "MAJAL INVEST", 4155.00, 831.00, 4986.00);
            Ajouter("FF260009", "MAROC TELECOM", 433.30, 86.66, 519.96);
            Ajouter("FF260008", "MAROC TELECOM", 1033.33, 206.67, 1240.00);
            Ajouter("FF260007", "MAJAL INVEST", 4155.00, 831.00, 4986.00);
            Ajouter("FF260005", "MAROC TELECOM", 1033.33, 206.67, 1240.00);
            Ajouter("FF260006", "MAROC TELECOM", 433.30, 86.66, 519.96);
            Ajouter("FF260001", "MAJAL INVEST", 4155.00, 831.00, 4986.00);

            return dict;
        }

        public static List<AffectationCandidate> CreerCandidats()
        {
            var factures = CreerFactures();
            var list = new List<AffectationCandidate>();
            int ecIdCounter = 1000;
            int mvIdCounter = 2000;

            void Ajouter(string numReg, string numFact, decimal montant, DateTime datePaiement, string tiersNom, string ice, string ifiscal)
            {
                ecIdCounter++;
                mvIdCounter++;
                var aff = new AffectationADeclarer
                {
                    NumeroFacture = numFact,
                    NumeroRapprochement = numReg,
                    Sens = SensAffectation.Achat,
                    Source = SourceAffectation.Decaissement,
                    MontantAffecte = montant,
                    DatePaiement = datePaiement,
                    DateFacture = datePaiement.AddDays(-15),
                    ModePaiement = "Virement",
                    EC_Type = 0,
                    EC_Id = ecIdCounter,
                    MV_Id = mvIdCounter,
                    Tiers = new TiersInfo
                    {
                        Nom = tiersNom,
                        Ice = ice,
                        IdentifiantFiscal = ifiscal,
                        CodeActivite = "001"
                    }
                };

                list.Add(new AffectationCandidate
                {
                    Affectation = aff,
                    Motif = MotifRejet.Eligible
                });
            }

            // B1 : 3 factures réglées en 2 fois (50 % + 50 %) — 6 règlements
            Ajouter("RF26100006", "FF260029", 80243.40m, new DateTime(2026, 10, 5), "LOGINFO INGENIERIE", "001500000000001", "10000001");
            Ajouter("RF26100007", "FF260029", 80243.40m, new DateTime(2026, 10, 15), "LOGINFO INGENIERIE", "001500000000001", "10000001");

            Ajouter("RF26070005", "FF260027", 74274.60m, new DateTime(2026, 7, 10), "UNION MAGHREB DES TE", "001500000000002", "10000002");
            Ajouter("RF26090005", "FF260027", 74274.60m, new DateTime(2026, 9, 10), "UNION MAGHREB DES TE", "001500000000002", "10000002");

            Ajouter("RF26090006", "FF260028", 40207.38m, new DateTime(2026, 9, 20), "SNSS TECHNOLOGIES", "001500000000003", "10000003");
            Ajouter("RF26090007", "FF260028", 40207.39m, new DateTime(2026, 9, 25), "SNSS TECHNOLOGIES", "001500000000003", "10000003");

            // B2 : 9 factures réglées en 1 fois (prorata 100 %) — 9 règlements
            Ajouter("RF26100004", "FF260030", 1240.00m, new DateTime(2026, 10, 2), "MAROC TELECOM", "001500000000004", "10000004");
            Ajouter("RF26100005", "FF260031", 519.96m, new DateTime(2026, 10, 3), "MAROC TELECOM", "001500000000004", "10000004");
            Ajouter("RF26090004", "FF260026", 4986.00m, new DateTime(2026, 9, 18), "MAJAL INVEST", "001500000000005", "10000005");
            Ajouter("RF26090002", "FF260009", 519.96m, new DateTime(2026, 9, 5), "MAROC TELECOM", "001500000000004", "10000004");
            Ajouter("RF26090003", "FF260008", 1240.00m, new DateTime(2026, 9, 6), "MAROC TELECOM", "001500000000004", "10000004");
            Ajouter("RF26080003", "FF260007", 4986.00m, new DateTime(2026, 8, 20), "MAJAL INVEST", "001500000000005", "10000005");
            Ajouter("RF26080001", "FF260005", 1240.00m, new DateTime(2026, 8, 10), "MAROC TELECOM", "001500000000004", "10000004");
            Ajouter("RF26080002", "FF260006", 519.96m, new DateTime(2026, 8, 12), "MAROC TELECOM", "001500000000004", "10000004");
            Ajouter("RF26070001", "FF260001", 4986.00m, new DateTime(2026, 7, 5), "MAJAL INVEST", "001500000000005", "10000005");

            return list;
        }
    }
}
