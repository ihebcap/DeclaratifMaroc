using Xunit;
using Declaration.Selection;

namespace Declaration.Orchestration.Tests
{
    public class Task196RapprochementCtTypeFilteringTests
    {
        [Theory]
        [InlineData(0, 0, true)]  // Encaissement (0) + Tiers Client (0) => OK
        [InlineData(1, 1, true)]  // Décaissement (1) + Tiers Fournisseur (1) => OK
        [InlineData(0, 1, false)] // Encaissement (0) + Tiers Fournisseur (1) => Exclu
        [InlineData(1, 0, false)] // Décaissement (1) + Tiers Client (0) => Exclu
        [InlineData(0, 2, false)] // Encaissement (0) + Tiers Autre (2) => Exclu
        [InlineData(1, 2, false)] // Décaissement (1) + Tiers Autre (2) => Exclu
        [InlineData(6, 1, false)] // Dépense (6) => Exclu du périmètre encaissement/décaissement strict
        public void EvaluerEligibiliteCtTypeParDomaine_FiltreStrictementTypesAutres(int mvDomaine, int ctType, bool attenduEligible)
        {
            // Règle TASK-194 / TASK-196 :
            // (MV_Domaine = 0 AND CT_Type = 0) OR (MV_Domaine = 1 AND CT_Type = 1)
            bool estEligible = (mvDomaine == GrfEnums.Domaine_ReglementClient && ctType == GrfEnums.CtType_Client)
                            || (mvDomaine == GrfEnums.Domaine_ReglementFournisseur && ctType == GrfEnums.CtType_Fournisseur);

            Assert.Equal(attenduEligible, estEligible);
        }

        [Fact]
        public void GrfEnums_CtTypeConstants_MatchExpectedValues()
        {
            Assert.Equal(0, GrfEnums.CtType_Client);
            Assert.Equal(1, GrfEnums.CtType_Fournisseur);
        }
    }
}
