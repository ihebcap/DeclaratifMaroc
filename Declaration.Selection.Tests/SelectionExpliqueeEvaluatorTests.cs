using System;
using System.Threading.Tasks;
using Xunit;
using Declaration.Core.Model;
using Declaration.Selection;

namespace Declaration.Selection.Tests
{
    public class SelectionExpliqueeEvaluatorTests
    {
        private readonly DateTime _debut = new DateTime(2026, 1, 1);
        private readonly DateTime _fin = new DateTime(2026, 1, 31);
        private readonly DateTime _finExclude = new DateTime(2026, 2, 1);

        private AffectationCandidateRow CreateValidRow(int domaine = GrfEnums.Domaine_ReglementFournisseur, int? modePaiement = null)
        {
            return new AffectationCandidateRow
            {
                MV_Id = 1,
                MV_Domaine = domaine,
                MV_Point = GrfEnums.Point_Oui,
                MV_PointDate = _debut.AddDays(15),
                DatePaiement = _debut.AddDays(10),
                MV_DECAISSE = GrfEnums.Decaisse_Oui,
                MV_Compta = GrfEnums.Compta_Comptabilise,
                MV_Annule = GrfEnums.Annule_Non,
                MV_Impaye = GrfEnums.Impaye_NonImpaye,
                ModePaiementId = modePaiement ?? 3, // Virement par ex
                AF_Id = 100,
                NumeroFacture = "FAC-123",
                MontantAffecte = 100m,
                DateFacture = _debut,
                DT_Id = null,
                TiersNumero = "T01",
                TiersNom = "Fournisseur A"
            };
        }

        [Fact]
        public async Task Evaluer_ValidRow_ReturnsEligible_RegressionTask008()
        {
            var row = CreateValidRow();
            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.True(result.EstEligible);
            Assert.Equal(MotifRejet.Eligible, result.Motif);
        }

        [Fact]
        public async Task Evaluer_NonRapproche_ReturnsMotif()
        {
            var row = CreateValidRow();
            row.MV_Point = 0; // Non pointé

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.NonRapproche, result.Motif);
            Assert.Equal("Règlement non rapproché", result.MotifLibelle);
        }

        [Fact]
        public async Task Evaluer_NonRapproche_Et_NonComptabilise_ReturnsNonComptabilise()
        {
            var row = CreateValidRow();
            row.MV_Point = 0; // Non pointé
            row.MV_Compta = GrfEnums.Compta_NonComptabilise; // Non comptabilisé (plus grave)

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            // Doit retourner NonComptabilise car les garde-fous universels priment sur le non-rapprochement
            Assert.Equal(MotifRejet.NonComptabilise, result.Motif);
        }

        [Fact]
        public async Task Evaluer_HorsPeriodePointDate_ReturnsMotif()
        {
            var row = CreateValidRow();
            row.MV_PointDate = _finExclude; // Hors période

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.HorsPeriode, result.Motif);
            Assert.Equal("Rapproché hors de la période", result.MotifLibelle);
        }

        [Fact]
        public async Task Evaluer_HorsPeriodeEspece_ReturnsMotif()
        {
            var row = CreateValidRow(GrfEnums.Domaine_ReglementFournisseur, GrfEnums.ModePaiement_Espece);
            row.DatePaiement = _finExclude; // Espèce se base sur DatePaiement

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.HorsPeriode, result.Motif);
        }

        [Fact]
        public async Task Evaluer_DejaDeclare_ReturnsMotif()
        {
            var row = CreateValidRow();
            row.DT_Id = 5; // Déjà déclaré

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.DejaDeclare, result.Motif);
            Assert.Equal("Déjà déclaré (période précédente)", result.MotifLibelle);
        }

        [Fact]
        public async Task Evaluer_NonComptabilise_ReturnsMotif()
        {
            var row = CreateValidRow();
            row.MV_Compta = GrfEnums.Compta_NonComptabilise;

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.NonComptabilise, result.Motif);
            Assert.Equal("Règlement non comptabilisé", result.MotifLibelle);
        }

        [Fact]
        public async Task Evaluer_Annule_ReturnsMotif()
        {
            var row = CreateValidRow();
            row.MV_Annule = 1; // Annulé

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.Annule, result.Motif);
            Assert.Equal("Règlement annulé", result.MotifLibelle);
        }

        [Fact]
        public async Task Evaluer_Impaye_ReturnsMotif()
        {
            var row = CreateValidRow();
            row.MV_Impaye = GrfEnums.Impaye_Impaye;

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.Impaye, result.Motif);
            Assert.Equal("Règlement impayé", result.MotifLibelle);
        }

        [Fact]
        public async Task Evaluer_NonAffecte_ReturnsMotif()
        {
            var row = CreateValidRow();
            row.AF_Id = null;

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.NonAffecte, result.Motif);
            Assert.Equal("Rapproché mais non affecté à une facture", result.MotifLibelle);
        }

        [Fact]
        public async Task Evaluer_FactureIntrouvable_ReturnsMotif()
        {
            var row = CreateValidRow();
            
            Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>> verifier = 
                (num, sens) => Task.FromResult((false, false));

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, verifier);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.FactureIntrouvable, result.Motif);
            Assert.Equal("Facture introuvable dans Sage", result.MotifLibelle);
        }

        [Fact]
        public async Task Evaluer_TaxeNonATaux_ReturnsMotif()
        {
            var row = CreateValidRow();
            
            Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>> verifier = 
                (num, sens) => Task.FromResult((true, false)); // Existe, mais taxe pas OK

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, verifier);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.TaxeNonATaux, result.Motif);
            Assert.Equal("Ligne de taxe non éligible (type ≠ taux)", result.MotifLibelle);
        }

        [Fact]
        public async Task Evaluer_VerifierOk_ReturnsEligible()
        {
            var row = CreateValidRow();
            
            Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>> verifier = 
                (num, sens) => Task.FromResult((true, true)); // Tout est bon

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, verifier);

            Assert.True(result.EstEligible);
            Assert.Equal(MotifRejet.Eligible, result.Motif);
        }
    }
}
