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
    /// TASK-193 : FraisBancaire (SourceAffectation.FraisBancaire) rattaché au domaine selon son Sens
    /// (SensAffectation.Achat -> Decaissement, SensAffectation.Vente -> Encaissement).
    /// </summary>
    public class Task193FraisBancairesDomaineTests
    {
        [Fact]
        public void FraisBancaire_SensAchat_RattacheAuDomaineDecaissement()
        {
            var candidats = new List<Declaration.Selection.AffectationCandidate>
            {
                new Declaration.Selection.AffectationCandidate
                {
                    Affectation = new AffectationADeclarer
                    {
                        NumeroFacture = "FRAIS-001-1",
                        NumeroRapprochement = "FRAIS-001-1",
                        Sens = SensAffectation.Achat,
                        Source = SourceAffectation.FraisBancaire,
                        MontantAffecte = 120m,
                        Tiers = new TiersInfo { Numero = "BNK1", Nom = "Banque A" }
                    },
                    Motif = MotifRejet.Eligible
                },
                new Declaration.Selection.AffectationCandidate
                {
                    Affectation = new AffectationADeclarer
                    {
                        NumeroFacture = "FRAIS-002-2",
                        NumeroRapprochement = "FRAIS-002-2",
                        Sens = SensAffectation.Vente,
                        Source = SourceAffectation.FraisBancaire,
                        MontantAffecte = 240m,
                        Tiers = new TiersInfo { Numero = "BNK2", Nom = "Banque B" }
                    },
                    Motif = MotifRejet.Eligible
                }
            };

            var modele = new DeclarationModele();
            modele.Lignes.Add(new LigneDeclarationEnrichie
            {
                NumeroFacture = "FRAIS-001-1",
                HT = 100m,
                Taux = 20m,
                Tva = 20m,
                Ttc = 120m
            });
            modele.Lignes.Add(new LigneDeclarationEnrichie
            {
                NumeroFacture = "FRAIS-002-2",
                HT = 200m,
                Taux = 20m,
                Tva = 40m,
                Ttc = 240m
            });

            // 1. Filtrage domaine "Decaissement"
            var candidatsDecaissement = candidats.Where(c => c.Affectation.Source == SourceAffectation.Decaissement
                                                          || c.Affectation.Source == SourceAffectation.Espece
                                                          || c.Affectation.Source == SourceAffectation.Depense
                                                          || (c.Affectation.Source == SourceAffectation.FraisBancaire && c.Affectation.Sens == SensAffectation.Achat));

            var lignesDecaissement = DeclarationWorkflowService.MapLignesCandidates(
                Guid.NewGuid(), "Decaissement", candidatsDecaissement, modele);

            // Doit contenir uniquement FRAIS-001-1 (Sens Achat)
            var ligneAchat = Assert.Single(lignesDecaissement);
            Assert.Equal("FRAIS-001-1", ligneAchat.NumeroFacture);
            Assert.Equal("Decaissement", ligneAchat.Domaine);
            Assert.Equal("FraisBancaire", ligneAchat.Source);

            // 2. Filtrage domaine "Encaissement"
            var candidatsEncaissement = candidats.Where(c => c.Affectation.Source == SourceAffectation.Encaissement
                                                          || (c.Affectation.Source == SourceAffectation.FraisBancaire && c.Affectation.Sens == SensAffectation.Vente));

            var lignesEncaissement = DeclarationWorkflowService.MapLignesCandidates(
                Guid.NewGuid(), "Encaissement", candidatsEncaissement, modele);

            // Doit contenir uniquement FRAIS-002-2 (Sens Vente)
            var ligneVente = Assert.Single(lignesEncaissement);
            Assert.Equal("FRAIS-002-2", ligneVente.NumeroFacture);
            Assert.Equal("Encaissement", ligneVente.Domaine);
            Assert.Equal("FraisBancaire", ligneVente.Source);
        }
    }
}
