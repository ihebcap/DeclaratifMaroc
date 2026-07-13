using System;
using Xunit;
using Declaration.Application.Entities;
using Declaration.Selection;

namespace Declaration.Orchestration.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // TASK-062 — Source unique de la date de période (DateReference).
    //
    // Prouve que la règle « dans quelle période tombe un règlement » est UNIQUE et
    // répliquée à l'identique SQL ⇄ C# (comme EstRapprocheBanque) :
    //   • espèce (MV_Type=0)               → MV_Date
    //   • non-espèce rapproché (MV_Point=1) → MV_PointDate
    //   • non-espèce non rapproché          → MV_Date
    // + le gate EstDeclarable (espèce OU rapproché).
    // 100 % in-process, aucune connexion réseau.
    // ═══════════════════════════════════════════════════════════════════════════════
    public class Task062DateReferenceTests
    {
        // ─── DateReference : les 3 familles ────────────────────────────────────────

        [Fact]
        public void Espece_DateReference_EstMvDate()
        {
            var d = new DateTime(2026, 1, 20);
            // Espèce : MV_Type=0, jamais rapprochée (MV_Point=0, MV_PointDate nul).
            Assert.Equal(d, RegleDatePeriode.DateReferencePour(mvType: 0, mvPoint: 0, mvPointDate: null, mvDate: d));
        }

        [Fact]
        public void NonEspece_Rapproche_DateReference_EstMvPointDate()
        {
            var mvDate = new DateTime(2026, 6, 15);
            var pointDate = new DateTime(2026, 1, 30);
            // RF26060064-like : réglé en juin, rapproché en janvier → période = janvier (MV_PointDate).
            Assert.Equal(pointDate,
                RegleDatePeriode.DateReferencePour(mvType: 3, mvPoint: 1, mvPointDate: pointDate, mvDate: mvDate));
        }

        [Fact]
        public void NonEspece_NonRapproche_DateReference_EstMvDate()
        {
            var mvDate = new DateTime(2026, 5, 1);
            // Non rapproché (MV_Point=0) : reste situé par MV_Date (visible dans l'interrogation).
            Assert.Equal(mvDate,
                RegleDatePeriode.DateReferencePour(mvType: 3, mvPoint: 0, mvPointDate: null, mvDate: mvDate));
        }

        [Fact]
        public void NonEspece_Rapproche_SansPointDate_DateReference_EstNull()
        {
            var mvDate = new DateTime(2026, 5, 1);
            // Donnée incohérente (MV_Point=1 sans MV_PointDate) : reproduit le CASE SQL → NULL.
            // Signalé au VERIFY ; pas de COALESCE implicite (fidélité SQL ⇄ C#).
            Assert.Null(RegleDatePeriode.DateReferencePour(mvType: 1, mvPoint: 1, mvPointDate: null, mvDate: mvDate));
        }

        // ─── EstDeclarable : gate déclaration ──────────────────────────────────────

        [Theory]
        [InlineData(0, 0, true)]  // espèce (auto-rapprochée) → déclarable
        [InlineData(3, 1, true)]  // non-espèce rapproché → déclarable
        [InlineData(3, 0, false)] // non-espèce non rapproché → non déclarable
        [InlineData(1, 0, false)] // chèque non rapproché → non déclarable
        public void EstDeclarable_RefleteRegle(int mvType, int mvPoint, bool attendu)
        {
            Assert.Equal(attendu, RegleDatePeriode.EstDeclarable(mvType, mvPoint));
        }

        // ─── Cohérence domaine : ReglementRapprochementRow.DateReference ────────────

        [Fact]
        public void Row_DateReference_DelegueALaSourceUnique_RF26060064()
        {
            // RF26060064 : MV_Type=3 (virement), MV_Point=1, MV_PointDate=30/01, MV_Date=15/06.
            var row = new ReglementRapprochementRow
            {
                MvType = 3, MvPoint = 1,
                MvPointDate = new DateTime(2026, 1, 30),
                MvDate = new DateTime(2026, 6, 15)
            };

            Assert.Equal(new DateTime(2026, 1, 30), row.DateReference); // janvier, pas juin
            Assert.Equal(
                RegleDatePeriode.DateReferencePour(row.MvType, row.MvPoint, row.MvPointDate, row.MvDate),
                row.DateReference);
        }

        // ─── Anti-divergence SQL ⇄ C# ──────────────────────────────────────────────

        [Fact]
        public void SqlFragment_EstLExpressionCanonique_GardeContreEditionUnilaterale()
        {
            // Le fragment SQL DOIT rester l'expression canonique documentée. Toute modification
            // impose de mettre à jour DateReferencePour (et ce test) en miroir.
            Assert.Equal(
                "(CASE WHEN M.MV_Type <> 0 AND M.MV_Point = 1 THEN M.MV_PointDate ELSE M.MV_Date END)",
                RegleDatePeriode.DateReferenceSqlM);
            Assert.Equal("(M.MV_Type = 0 OR M.MV_Point = 1)", RegleDatePeriode.EstDeclarableSqlM);
        }

        [Theory]
        [InlineData(0, 0, true)]   // espèce
        [InlineData(3, 1, false)]  // non-espèce rapproché
        [InlineData(3, 0, false)]  // non-espèce non rapproché
        [InlineData(1, 1, true)]   // chèque rapproché
        public void SqlCase_EtDerivationCSharp_RendentLaMemeDateReference(int mvType, int mvPoint, bool pointDateNull)
        {
            var mvDate = new DateTime(2026, 6, 15);
            DateTime? pointDate = pointDateNull ? (DateTime?)null : new DateTime(2026, 1, 30);

            // Émulation stricte du CASE SQL (RegleDatePeriode.DateReferenceSqlM).
            DateTime? sql = (mvType != 0 && mvPoint == 1) ? pointDate : mvDate;
            DateTime? csharp = RegleDatePeriode.DateReferencePour(mvType, mvPoint, pointDate, mvDate);

            Assert.Equal(sql, csharp);
        }
    }
}
