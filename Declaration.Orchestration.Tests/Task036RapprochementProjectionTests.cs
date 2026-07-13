using System;
using Xunit;
using Declaration.Application.Entities;

namespace Declaration.Orchestration.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // TASK-036 — Tests de projection/mapping de l'interrogation « Rapprochement bancaire ».
    //
    // Prouvent la logique dérivée (source unique ReglementRapprochementRow) que l'API
    // projette ensuite en DTO :
    //   - reste à affecter = montant − Σ affectations, TOUJOURS visible (pilier confiance) ;
    //   - rapproché banque ← MV_Point ; déclaré ← DT_Id (NbDeclare) ; origine ← EC_Type.
    // 100 % in-process, aucune connexion réseau.
    // ═══════════════════════════════════════════════════════════════════════════════
    public class Task036RapprochementProjectionTests
    {
        [Fact]
        public void ResteAAffecter_EstLEcartMontantMoinsAffecte_EtVisible()
        {
            var row = new ReglementRapprochementRow { MvMontant = 3798.00m, MontantAffecte = 3797.42m, NbAffectations = 1 };

            Assert.Equal(3797.42m, row.MontantAffecteEffectif);
            Assert.Equal(0.58m, row.ResteAAffecter); // écart non nul rendu explicite
        }

        [Fact]
        public void SansAffectation_ResteAAffecter_EgaleTotalDuMontant()
        {
            var row = new ReglementRapprochementRow { MvMontant = 100840.21m, MontantAffecte = null, NbAffectations = 0 };

            Assert.Equal(0m, row.MontantAffecteEffectif);
            Assert.Equal(100840.21m, row.ResteAAffecter); // rien n'est masqué
            Assert.Equal("SansAffectation", row.Origine);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(0, false)]
        public void RapprocheBanque_RefleteMvPoint(int mvPoint, bool attendu)
        {
            // Mode non-espèce (chèque) : le rapproché suit strictement MV_Point.
            var row = new ReglementRapprochementRow { MvType = 1, MvPoint = mvPoint };
            Assert.Equal(attendu, row.EstRapprocheBanque);
        }

        // ─── TASK-042 : règle espèce (auto-rapprochée) + colonnes de preuve ─────────

        [Fact]
        public void Espece_EstToujoursRapprochee_MemeSansPointDate()
        {
            // Espèce (MV_Type=0) : par nature MV_Point=0 et MV_PointDate nul (aucun extrait).
            var d = new DateTime(2026, 3, 15);
            var row = new ReglementRapprochementRow { MvType = 0, MvPoint = 0, MvPointDate = null, MvDate = d };

            Assert.True(row.EstRapprocheBanque);            // auto-rapprochée
            Assert.Equal(d, row.DateRapprochement);         // faute de MV_PointDate → MV_Date
            Assert.Null(row.MvExtraitNum);                  // aucun n° d'extrait (jamais fictif)
        }

        [Fact]
        public void NonEspece_Rapproche_DateRapprochementEstMvPointDate()
        {
            var pointDate = new DateTime(2026, 4, 20);
            var row = new ReglementRapprochementRow
            {
                MvType = 1, MvPoint = 1, MvDate = new DateTime(2026, 4, 10),
                MvPointDate = pointDate, MvExtraitNum = "EXT-042"
            };

            Assert.True(row.EstRapprocheBanque);
            Assert.Equal(pointDate, row.DateRapprochement); // pointage réel, pas MV_Date
            Assert.Equal("EXT-042", row.MvExtraitNum);
        }

        [Fact]
        public void NonEspece_NonRapproche_DateRapprochementNulle()
        {
            var row = new ReglementRapprochementRow
            {
                MvType = 3, MvPoint = 0, MvPointDate = null, MvDate = new DateTime(2026, 5, 1)
            };

            Assert.False(row.EstRapprocheBanque);
            Assert.Null(row.DateRapprochement); // non rapproché bancaire → « — » au front
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(2, true)]
        public void Declare_RefleteDtId(int nbDeclare, bool attendu)
        {
            var row = new ReglementRapprochementRow { NbDeclare = nbDeclare };
            Assert.Equal(attendu, row.EstDeclare);
        }

        [Theory]
        [InlineData(0, "Sage")]
        [InlineData(111, "FGR")]
        [InlineData(4, "SoldeInitial")]
        public void Origine_RefleteEcType_QuandHomogene(int ecType, string attendu)
        {
            var row = new ReglementRapprochementRow { EcTypeMin = ecType, EcTypeMax = ecType, NbAffectations = 1 };
            Assert.Equal(attendu, row.Origine);
        }

        [Fact]
        public void Origine_EstMixte_QuandEcTypeHeterogene()
        {
            var row = new ReglementRapprochementRow { EcTypeMin = 0, EcTypeMax = 111, NbAffectations = 2 };
            Assert.Equal("Mixte", row.Origine);
        }

        [Theory]
        [InlineData(0, "Espèce")]
        [InlineData(1, "Chèque")]
        [InlineData(2, "Traite")]
        [InlineData(3, "Virement")]
        [InlineData(9, "Autres")]
        public void Mode_RefleteMvType(int mvType, string attendu)
        {
            var row = new ReglementRapprochementRow { MvType = mvType };
            Assert.Equal(attendu, row.Mode);
        }
    }
}
