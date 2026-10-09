using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Declaration.Application.Entities;
using Declaration.Application.Services;
using Declaration.Core;
using Declaration.Core.Model;

namespace Declaration.Orchestration.Tests
{
    /// <summary>
    /// TASK-224 : Reproduction du bug de duplication des lignes pour les factures
    /// réglées en plusieurs fois (surdéclaration de 64 908,46 MAD sur TVA1-2026-03-T).
    /// </summary>
    public class Task224ReproductionDoublonsTests
    {
        [Fact]
        public void ConstructeurDeclaration_SurJeuSimulation_ProduitExactement15LignesEtTvaSaine()
        {
            // Arrange
            var factures = Task224DonneesSimulation.CreerFactures();
            var candidats = Task224DonneesSimulation.CreerCandidats();
            var affectations = candidats.Select(c => c.Affectation).ToList();

            // Act
            var modele = ConstructeurDeclaration.ConstruireDeclaration(
                affectations,
                aff => factures.TryGetValue(aff.NumeroFacture, out var doc) ? doc : null,
                2);

            // Assert : ConstructeurDeclaration est sain
            Assert.Equal(15, modele.Lignes.Count);
            var totalTva = Math.Round(modele.Lignes.Sum(l => l.Tva), 2);
            Assert.True(Math.Abs(totalTva - 68281.46m) <= 0.01m, $"Total TVA attendu ~68281.46 mais obtenu {totalTva}");

            // Vérification par facture scindée
            var lignesFF29 = modele.Lignes.Where(l => l.NumeroFacture == "FF260029").ToList();
            Assert.Equal(2, lignesFF29.Count);
            Assert.Equal(26747.80m, Math.Round(lignesFF29.Sum(l => l.Tva), 2));

            var lignesFF27 = modele.Lignes.Where(l => l.NumeroFacture == "FF260027").ToList();
            Assert.Equal(2, lignesFF27.Count);
            Assert.Equal(24758.20m, Math.Round(lignesFF27.Sum(l => l.Tva), 2));

            var lignesFF28 = modele.Lignes.Where(l => l.NumeroFacture == "FF260028").ToList();
            Assert.Equal(2, lignesFF28.Count);
            Assert.Equal(13402.46m, Math.Round(lignesFF28.Sum(l => l.Tva), 2));
        }

        [Fact]
        public void MapLignesCandidates_SurFacturesRegleesEnPlusieursFois_NeDoitPasDupliquerLesLignes()
        {
            // Arrange
            var declarationId = Guid.NewGuid();
            var factures = Task224DonneesSimulation.CreerFactures();
            var candidats = Task224DonneesSimulation.CreerCandidats();
            var affectations = candidats.Select(c => c.Affectation).ToList();

            var modele = ConstructeurDeclaration.ConstruireDeclaration(
                affectations,
                aff => factures.TryGetValue(aff.NumeroFacture, out var doc) ? doc : null,
                2);

            // Act
            var lignes = DeclarationWorkflowService.MapLignesCandidates(declarationId, "Decaissement", candidats, modele);

            // Assert attendu (15 lignes, 68 281,46 MAD, 0 doublon)
            // CE TEST EST ROUGE AVANT LE CORRECTIF (produit 21 lignes, total 133 189,91 MAD).
            Assert.Equal(15, lignes.Count);
            var totalTva = Math.Round(lignes.Sum(l => l.TVA), 2);
            Assert.Equal(68281.46m, totalTva);

            // Aucun groupe (facture, règlement, taux) présent plusieurs fois
            var doublons = lignes
                .GroupBy(l => new { l.NumeroFacture, l.NumeroRapprochement, l.Taux })
                .Where(g => g.Count() > 1)
                .ToList();
            Assert.Empty(doublons);

            // Chacune des 3 factures réglées en 2 fois doit avoir exactement 2 lignes (1 par règlement)
            var ff29 = lignes.Where(l => l.NumeroFacture == "FF260029").ToList();
            Assert.Equal(2, ff29.Count);
            Assert.Contains(ff29, l => l.NumeroRapprochement == "RF26100006");
            Assert.Contains(ff29, l => l.NumeroRapprochement == "RF26100007");

            var ff27 = lignes.Where(l => l.NumeroFacture == "FF260027").ToList();
            Assert.Equal(2, ff27.Count);

            var ff28 = lignes.Where(l => l.NumeroFacture == "FF260028").ToList();
            Assert.Equal(2, ff28.Count);
        }
    }
}
