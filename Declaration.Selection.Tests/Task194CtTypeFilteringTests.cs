using Xunit;
using Declaration.Selection;

namespace Declaration.Selection.Tests
{
    public class Task194CtTypeFilteringTests
    {
        [Fact]
        public void GrfEnums_CtTypeConstants_AreCorrectlyDefined()
        {
            Assert.Equal(0, GrfEnums.CtType_Client);
            Assert.Equal(1, GrfEnums.CtType_Fournisseur);
        }
    }
}
