using System;
using Xunit;
using Declaration.Application.Entities;

namespace Declaration.Orchestration.Tests
{
    public class Task195DeclarationTrimestrielleDatesTests
    {
        [Theory]
        [InlineData(2026, 1, 2026, 1, 1, 2026, 3, 31)] // T1: 01/01/2026 -> 31/03/2026
        [InlineData(2026, 2, 2026, 4, 1, 2026, 6, 30)] // T2: 01/04/2026 -> 30/06/2026
        [InlineData(2026, 3, 2026, 7, 1, 2026, 9, 30)] // T3: 01/07/2026 -> 30/09/2026
        [InlineData(2026, 4, 2026, 10, 1, 2026, 12, 31)] // T4: 01/10/2026 -> 31/12/2026
        public void CalculerIntervalleDates_Trimestrielle_RetourneBornesExactes(
            int exercice, int trimestre,
            int expStartYear, int expStartMonth, int expStartDay,
            int expEndYear, int expEndMonth, int expEndDay)
        {
            var (dateDebut, dateFin) = DeclarationEntete.CalculerIntervalleDates(exercice, trimestre, TypePeriode.Trimestrielle);

            Assert.Equal(new DateTime(expStartYear, expStartMonth, expStartDay), dateDebut);
            Assert.Equal(new DateTime(expEndYear, expEndMonth, expEndDay), dateFin);
        }

        [Theory]
        [InlineData(2026, 1, 2026, 1, 1, 2026, 1, 31)] // M1: 01/01/2026 -> 31/01/2026
        [InlineData(2026, 2, 2026, 2, 1, 2026, 2, 28)] // M2 (non bissextile): 01/02/2026 -> 28/02/2026
        [InlineData(2024, 2, 2024, 2, 1, 2024, 2, 29)] // M2 (bissextile 2024): 01/02/2024 -> 29/02/2024
        [InlineData(2026, 7, 2026, 7, 1, 2026, 7, 31)] // M7: 01/07/2026 -> 31/07/2026
        [InlineData(2026, 12, 2026, 12, 1, 2026, 12, 31)] // M12: 01/12/2026 -> 31/12/2026
        public void CalculerIntervalleDates_Mensuelle_RetourneBornesExactes(
            int exercice, int mois,
            int expStartYear, int expStartMonth, int expStartDay,
            int expEndYear, int expEndMonth, int expEndDay)
        {
            var (dateDebut, dateFin) = DeclarationEntete.CalculerIntervalleDates(exercice, mois, TypePeriode.Mensuelle);

            Assert.Equal(new DateTime(expStartYear, expStartMonth, expStartDay), dateDebut);
            Assert.Equal(new DateTime(expEndYear, expEndMonth, expEndDay), dateFin);
        }

        [Fact]
        public void ObtenirIntervalleDates_SurEnteteTrimestrielle_UtiliseProprietesEntete()
        {
            var entete = new DeclarationEntete
            {
                Exercice = 2026,
                Periode = 3, // T3
                Type = TypePeriode.Trimestrielle
            };

            var (dateDebut, dateFin) = entete.ObtenirIntervalleDates();

            Assert.Equal(new DateTime(2026, 7, 1), dateDebut);
            Assert.Equal(new DateTime(2026, 9, 30), dateFin);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(-1)]
        public void CalculerIntervalleDates_TrimestreInvalide_LeveArgumentOutOfRangeException(int trimestreInvalide)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DeclarationEntete.CalculerIntervalleDates(2026, trimestreInvalide, TypePeriode.Trimestrielle));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        [InlineData(-1)]
        public void CalculerIntervalleDates_MoisInvalide_LeveArgumentOutOfRangeException(int moisInvalide)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DeclarationEntete.CalculerIntervalleDates(2026, moisInvalide, TypePeriode.Mensuelle));
        }
    }
}
