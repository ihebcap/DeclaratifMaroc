using System;
using System.Collections.Generic;
using Declaration.API.Dtos;
using Declaration.Application.Entities;
using Xunit;

namespace Declaration.Orchestration.Tests
{
    public class Task144DiagnosticEnLigneTests
    {
        [Fact]
        public void DiagnosticLigneDto_Constructeur_MappeChampsEtCollisionsCorrectement()
        {
            var source = new DiagnosticLigneResultat
            {
                EC_Id = 21849,
                DoNumero = "FA2600106",
                TiersCode = "F00123",
                TiersIntitule = "FOURNISSEUR EXEMPLE",
                MotifTechnique = "FACTURE_INTROUVABLE",
                ExplicationMetier = "Sage n'a pas pu fournir le détail TVA de cette facture.",
                ActionRecommandee = "Vérifiez la saisie sous Sage.",
                CachePerime = false,
                MotifErreurCache = "DTO non fourni",
                Collisions = new List<DiagnosticCollisionEcheance>
                {
                    new DiagnosticCollisionEcheance
                    {
                        EC_Id = 21850,
                        TiersCode = "F00999",
                        TiersIntitule = "AUTRE TIERS",
                        ADocumentSage = true,
                        DoPieceSage = "FA2600106"
                    }
                }
            };

            var dto = new DiagnosticLigneDto(source);

            Assert.Equal(21849, dto.EcId);
            Assert.Equal("FA2600106", dto.DoNumero);
            Assert.Equal("F00123", dto.TiersCode);
            Assert.Equal("FACTURE_INTROUVABLE", dto.MotifTechnique);
            Assert.Equal("Sage n'a pas pu fournir le détail TVA de cette facture.", dto.ExplicationMetier);
            Assert.Equal("DTO non fourni", dto.MotifErreurCache);
            Assert.Single(dto.Collisions);
            Assert.Equal("F00999", dto.Collisions[0].TiersCode);
            Assert.True(dto.Collisions[0].ADocumentSage);
        }
    }
}
