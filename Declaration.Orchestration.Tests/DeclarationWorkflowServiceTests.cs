using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Declaration.Application.Services;
using Declaration.Application.Entities;
using Declaration.Selection;
using Declaration.Core.Model;

namespace Declaration.Orchestration.Tests
{
    public class DeclarationWorkflowServiceTests
    {
        [Fact]
        public void MapLignesCandidates_ConserveNumeroRapprochement_PourToutesLesBranches()
        {
            // Arrange
            var declarationId = Guid.NewGuid();
            var domaine = "Test";
            
            // Candidat 1 : Non éligible (Branche 1)
            var cand1 = new AffectationCandidate
            {
                Motif = MotifRejet.HorsPeriode, // MotifRejet.HorsPeriode => EstEligible = false
                Affectation = new AffectationADeclarer { NumeroFacture = "F1", NumeroRapprochement = "REG-001" }
            };

            // Candidat 2 : Éligible mais sans taxes (Branche 2)
            var cand2 = new AffectationCandidate
            {
                Motif = MotifRejet.Eligible, // EstEligible = true
                Affectation = new AffectationADeclarer { NumeroFacture = "F2", NumeroRapprochement = "REG-001" }
            };

            // Candidat 3 : Éligible avec taxes (Branche 3)
            var cand3 = new AffectationCandidate
            {
                Motif = MotifRejet.Eligible, // EstEligible = true
                Affectation = new AffectationADeclarer { NumeroFacture = "F3", NumeroRapprochement = "REG-001" }
            };

            var candidates = new List<AffectationCandidate> { cand1, cand2, cand3 };

            var modele = new DeclarationModele();
            // Lignes de taxes pour F3
            modele.Lignes.Add(new LigneDeclarationEnrichie { NumeroFacture = "F3" });
            modele.Lignes.Add(new LigneDeclarationEnrichie { NumeroFacture = "F3" }); // On simule 2 lignes de taxes pour la même facture

            // Act
            var lignes = DeclarationWorkflowService.MapLignesCandidates(declarationId, domaine, candidates, modele);

            // Assert
            // 1 pour F1 (Non éligible)
            // 1 pour F2 (Sans taxes)
            // 2 pour F3 (Taxes)
            Assert.Equal(4, lignes.Count);
            
            // Vérifier que chaque ligne possède bien le NumeroRapprochement "REG-001"
            foreach (var ligne in lignes)
            {
                Assert.Equal("REG-001", ligne.NumeroRapprochement);
            }
        }
    }
}
