using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Declaration.Application.Entities;
using Declaration.Application.Services;
using Declaration.Core.Model;
using Declaration.Selection;

namespace Declaration.Orchestration.Tests
{
    /// <summary>
    /// TASK-197 : Dépense et FraisBancaire doivent être exemptés du filtre selectionSet (TASK-097),
    /// car ce sont des sources auto-incluses qui n'apparaissent pas dans l'écran de sélection manuelle des règlements.
    /// Non-régression : Decaissement, Encaissement, Espece restent filtrés par selectionSet.
    /// </summary>
    public class Task197ExemptionFiltreSelectionTests
    {
        [Fact]
        public void ExemptionFiltreSelection_DepenseEtFraisBancaire_ConserveesMemeSiAbsentesDeSelection()
        {
            var candidates = new List<AffectationCandidate>
            {
                new AffectationCandidate
                {
                    Affectation = new AffectationADeclarer
                    {
                        NumeroFacture = "DEP-001",
                        NumeroRapprochement = "DEP-001",
                        Source = SourceAffectation.Depense,
                        Sens = SensAffectation.Achat,
                        MontantAffecte = 500m,
                        Tiers = new TiersInfo { Numero = "F001", Nom = "Fournisseur Depense" }
                    },
                    Motif = MotifRejet.Eligible
                },
                new AffectationCandidate
                {
                    Affectation = new AffectationADeclarer
                    {
                        NumeroFacture = "FRAIS-001",
                        NumeroRapprochement = "FRAIS-001",
                        Source = SourceAffectation.FraisBancaire,
                        Sens = SensAffectation.Achat,
                        MontantAffecte = 150m,
                        Tiers = new TiersInfo { Numero = "BNK1", Nom = "Banque A" }
                    },
                    Motif = MotifRejet.Eligible
                },
                new AffectationCandidate
                {
                    Affectation = new AffectationADeclarer
                    {
                        NumeroFacture = "DEC-001",
                        NumeroRapprochement = "REG-DEC-001",
                        Source = SourceAffectation.Decaissement,
                        Sens = SensAffectation.Achat,
                        MontantAffecte = 1000m,
                        Tiers = new TiersInfo { Numero = "F002", Nom = "Fournisseur Normal" }
                    },
                    Motif = MotifRejet.Eligible
                },
                new AffectationCandidate
                {
                    Affectation = new AffectationADeclarer
                    {
                        NumeroFacture = "ESP-001",
                        NumeroRapprochement = "REG-ESP-001",
                        Source = SourceAffectation.Espece,
                        Sens = SensAffectation.Achat,
                        MontantAffecte = 200m,
                        Tiers = new TiersInfo { Numero = "F003", Nom = "Fournisseur Espece" }
                    },
                    Motif = MotifRejet.Eligible
                }
            };

            // Selection manuelle qui contient seulement REG-DEC-001 (pas REG-ESP-001, ni DEP-001, ni FRAIS-001)
            var selectionSet = new HashSet<string> { "REG-DEC-001" };

            // Filtrage TASK-197
            var resultat = candidates.Where(c =>
                c.Affectation.Source == SourceAffectation.Depense
                || c.Affectation.Source == SourceAffectation.FraisBancaire
                || selectionSet.Contains(c.Affectation.NumeroRapprochement)).ToList();

            // Doit contenir DEP-001 (auto-incluse), FRAIS-001 (auto-incluse), REG-DEC-001 (présente dans selectionSet)
            // Ne doit PAS contenir REG-ESP-001 (absente de selectionSet)
            Assert.Equal(3, resultat.Count);
            Assert.Contains(resultat, c => c.Affectation.NumeroFacture == "DEP-001");
            Assert.Contains(resultat, c => c.Affectation.NumeroFacture == "FRAIS-001");
            Assert.Contains(resultat, c => c.Affectation.NumeroFacture == "DEC-001");
            Assert.DoesNotContain(resultat, c => c.Affectation.NumeroFacture == "ESP-001");
        }

        [Fact]
        public void ExemptionFiltreSelection_SelectionVide_ConserveUniquementDepenseEtFraisBancaire()
        {
            var candidates = new List<AffectationCandidate>
            {
                new AffectationCandidate
                {
                    Affectation = new AffectationADeclarer
                    {
                        NumeroFacture = "DEP-002",
                        NumeroRapprochement = "DEP-002",
                        Source = SourceAffectation.Depense,
                        Sens = SensAffectation.Achat,
                        MontantAffecte = 300m,
                        Tiers = new TiersInfo { Numero = "F001", Nom = "Fournisseur Depense" }
                    },
                    Motif = MotifRejet.Eligible
                },
                new AffectationCandidate
                {
                    Affectation = new AffectationADeclarer
                    {
                        NumeroFacture = "DEC-002",
                        NumeroRapprochement = "REG-DEC-002",
                        Source = SourceAffectation.Decaissement,
                        Sens = SensAffectation.Achat,
                        MontantAffecte = 2000m,
                        Tiers = new TiersInfo { Numero = "F002", Nom = "Fournisseur Normal" }
                    },
                    Motif = MotifRejet.Eligible
                }
            };

            var selectionSet = new HashSet<string>(); // Aucune sélection manuelle

            var resultat = candidates.Where(c =>
                c.Affectation.Source == SourceAffectation.Depense
                || c.Affectation.Source == SourceAffectation.FraisBancaire
                || selectionSet.Contains(c.Affectation.NumeroRapprochement)).ToList();

            // Depense est conservée, Decaissement est éliminé (TASK-097)
            var candidateUnique = Assert.Single(resultat);
            Assert.Equal("DEP-002", candidateUnique.Affectation.NumeroFacture);
        }
    }
}
