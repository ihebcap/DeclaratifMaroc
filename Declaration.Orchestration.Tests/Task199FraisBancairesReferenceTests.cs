using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Declaration.Core.Model;
using Declaration.Selection;

namespace Declaration.Orchestration.Tests
{
    /// <summary>
    /// TASK-199 : Frais bancaire : vérification que le mapping FraisBancaireRow -> AffectationADeclarer
    /// renseigne correctement la propriété Reference depuis MV_PieceBq (ou "" si NULL).
    /// </summary>
    public class Task199FraisBancairesReferenceTests
    {
        [Fact]
        public void MapFraisBancaireRows_AssigneReferenceDepuisMVPieceBq()
        {
            var rows = new List<SelectionExpliqueeService.FraisBancaireRow>
            {
                new SelectionExpliqueeService.FraisBancaireRow
                {
                    PT_Id = 101,
                    MV_Numero = "FRAIS-101",
                    MV_PieceBq = "BQ-PIECE-0099",
                    MV_Date = new DateTime(2026, 8, 1),
                    MV_Montant = 100m,
                    PT_MontantTva = 20m,
                    TO_ErpTaxeNo = 1,
                    TO_Sens = GrfEnums.SensPrevisionnelle_Decaissement,
                    TO_Intitule = "Frais tenues de compte",
                    BanqueCode = "BMCE"
                },
                new SelectionExpliqueeService.FraisBancaireRow
                {
                    PT_Id = 102,
                    MV_Numero = "FRAIS-102",
                    MV_PieceBq = null, // Cas NULL en base
                    MV_Date = new DateTime(2026, 8, 2),
                    MV_Montant = 50m,
                    PT_MontantTva = 10m,
                    TO_ErpTaxeNo = 1,
                    TO_Sens = GrfEnums.SensPrevisionnelle_Decaissement,
                    TO_Intitule = "Commissions",
                    BanqueCode = "BMCE"
                }
            };

            var tauxInfo = new Dictionary<int, (decimal Taux, string CodeTaxe)>
            {
                { 1, (20m, "T20") }
            };

            var affectations = SelectionExpliqueeService.MapFraisBancaireRows(rows, tauxInfo);

            Assert.Equal(2, affectations.Count);

            // Cas nominal : MV_PieceBq est non nul -> Reference prend la valeur MV_PieceBq
            var affectation1 = affectations.First(a => a.NumeroFacture == "FRAIS-101-101");
            Assert.Equal("BQ-PIECE-0099", affectation1.Reference);
            Assert.Equal(SourceAffectation.FraisBancaire, affectation1.Source);

            // Cas NULL : MV_PieceBq est null -> Reference reste une chaîne vide (""), jamais de valeur inventée ou de NullReferenceException
            var affectation2 = affectations.First(a => a.NumeroFacture == "FRAIS-102-102");
            Assert.Equal("", affectation2.Reference);
            Assert.Equal(SourceAffectation.FraisBancaire, affectation2.Source);
        }
    }
}
