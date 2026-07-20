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
        public async Task Evaluer_NonRapproche_EstValorisable_MaisPasEligible_Task049()
        {
            // TASK-049 : une facture affectée mais non rapprochée est VALORISABLE (affichage)
            // sans être ÉLIGIBLE (déclaration).
            var row = CreateValidRow();
            row.MV_Point = 0; // non rapproché

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);      // pas déclarable
            Assert.True(result.EstValorisable);    // mais affichable
        }

        [Fact]
        public async Task Evaluer_Eligible_EstValorisable_Task049()
        {
            var row = CreateValidRow();
            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.True(result.EstEligible);
            Assert.True(result.EstValorisable);
        }

        [Theory]
        [InlineData(MotifRejet.Annule)]
        [InlineData(MotifRejet.Impaye)]
        [InlineData(MotifRejet.NonComptabilise)]
        [InlineData(MotifRejet.DejaDeclare)]
        public void EstValorisable_ExclutLesMotifsSansReglementValide_Task049(MotifRejet motif)
        {
            // Seuls Eligible, NonRapproche, NonAffecte (TASK-052) et HorsPeriode (TASK-076) sont
            // valorisables : tout autre motif reste exclu de l'affichage (pas de lecture OM fiable
            // à proposer, ou règlement invalide/annulé/déjà déclaré).
            var candidate = new AffectationCandidate { Motif = motif };
            Assert.False(candidate.EstValorisable);
        }

        [Fact]
        public void EstValorisable_HorsPeriode_EstValorisableMaisPasEligible_Task076()
        {
            // TASK-076 : un règlement rapproché mais à une date bancaire (MV_PointDate) hors de la
            // période demandée reste VALORISABLE pour l'écran Factures (pivot facture, période =
            // date de facture) — seule la DÉCLARATION (EstEligible) reste gated sur la période exacte.
            var candidate = new AffectationCandidate { Motif = MotifRejet.HorsPeriode };
            Assert.True(candidate.EstValorisable);
            Assert.False(candidate.EstEligible);
        }

        [Fact]
        public void EstValorisable_NonAffecte_EstValorisableMaisPasEligible_Task052()
        {
            // TASK-052 : une facture sans aucun règlement affecté est VALORISABLE (affichage,
            // lecture OM en cache token NULL) sans être ÉLIGIBLE (déclaration).
            var candidate = new AffectationCandidate { Motif = MotifRejet.NonAffecte };

            Assert.False(candidate.EstEligible);
            Assert.True(candidate.EstValorisable);
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

        // ──────────────────────────────────────────────────────────────────────────────────
        // TASK-099 — Rattrapage : la période devient une simple date de COUPURE (fin de
        // période), plus de borne basse. Un règlement rapproché/payé largement AVANT le début
        // de la période demandée, jamais déclaré, doit rester éligible (cas RF26060125 : ne
        // plus se perdre définitivement entre deux mois).
        // ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Evaluer_RapprocheBienAvantDebut_JamaisDeclare_ResteEligible_Task099()
        {
            var row = CreateValidRow();
            row.MV_PointDate = _debut.AddMonths(-6); // rapproché 6 mois avant le début de la période demandée

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.True(result.EstEligible);
            Assert.Equal(MotifRejet.Eligible, result.Motif);
        }

        [Fact]
        public async Task Evaluer_EspecePayeeBienAvantDebut_JamaisDeclaree_ResteEligible_Task099()
        {
            var row = CreateValidRow(GrfEnums.Domaine_ReglementFournisseur, GrfEnums.ModePaiement_Espece);
            row.DatePaiement = _debut.AddYears(-1); // payée un an avant le début de la période demandée

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.True(result.EstEligible);
            Assert.Equal(MotifRejet.Eligible, result.Motif);
        }

        [Fact]
        public async Task Evaluer_HorsPeriodeApresLaFin_ResteRejeteMemeSansBorneBasse_Task099()
        {
            // La borne haute (coupure de période) reste, elle, strictement appliquée.
            var row = CreateValidRow();
            row.MV_PointDate = _finExclude; // après la fin de période

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.HorsPeriode, result.Motif);
        }

        // ──────────────────────────────────────────────────────────────────────────────────
        // TASK-062 — Date de référence de période (DateReference) unique
        // ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// TASK-062 : RF26060064-like — réglé en juin (MV_Date), rapproché en janvier (MV_PointDate).
        /// La période est située par MV_PointDate (DateReference) → éligible/déclarable en janvier,
        /// alors que sa date de règlement est hors période.
        /// </summary>
        [Fact]
        public async Task Evaluer_RapprocheEnPeriodeMaisRegleHorsPeriode_EligibleViaPointDate_Task062()
        {
            var row = CreateValidRow(GrfEnums.Domaine_ReglementFournisseur, modePaiement: 3); // virement
            row.MV_Point = GrfEnums.Point_Oui;
            row.MV_PointDate = _debut.AddDays(29); // 30/01/2026 — dans la période
            row.DatePaiement = new DateTime(2026, 6, 15); // MV_Date hors période

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.True(result.EstEligible, "Rapproché en janvier (MV_PointDate) doit être déclarable en janvier (TASK-062).");
            Assert.Equal(MotifRejet.Eligible, result.Motif);
        }

        /// <summary>
        /// TASK-062 : borne haute homogène (&lt; @finExclude). Une espèce datée du dernier jour de la
        /// période (31/01) reste dans la période via MV_Date (DateReference espèce).
        /// </summary>
        [Fact]
        public async Task Evaluer_EspeceAuDernierJour_ResteEligible_BorneHauteHomogene_Task062()
        {
            var row = CreateValidRow(GrfEnums.Domaine_ReglementFournisseur, GrfEnums.ModePaiement_Espece);
            row.DatePaiement = _fin; // 31/01/2026 — dernier jour inclus

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.True(result.EstEligible, "Une espèce au dernier jour de la période doit rester éligible (TASK-062).");
            Assert.Equal(MotifRejet.Eligible, result.Motif);
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

        [Theory]
        [InlineData(1)]   // IMP — Impayé
        [InlineData(90)]  // G  — Gain / écart d'ajustement
        [InlineData(104)] // RC — Remboursement client
        public async Task Evaluer_EcTypeHorsListeBlanche_ReturnsHorsPerimetre_NonValorisable(int ecType)
        {
            var row = CreateValidRow();
            row.EC_Type = ecType;

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible);
            Assert.False(result.EstValorisable); // pas de lecture worker → pas d'échec « facture introuvable »
            Assert.Equal(MotifRejet.EcTypeHorsPerimetre, result.Motif);
        }

        [Theory]
        [InlineData(0)]   // FC  — Facture Erp
        [InlineData(4)]   // S   — Solde
        [InlineData(111)] // FGR — Facture GR
        public async Task Evaluer_EcTypeListeBlanche_ResteEligible(int ecType)
        {
            var row = CreateValidRow();
            row.EC_Type = ecType;

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.True(result.EstEligible);
            Assert.Equal(MotifRejet.Eligible, result.Motif);
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

        // ──────────────────────────────────────────────────────────────────────────────────
        // TASK-050 — Tests de non-régression MV_DECAISSE et valorisation facture-first
        // ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// TASK-050 (a) : Un règlement MV_Type=1 (chèque) avec MV_DECAISSE=0 n'est plus écarté.
        /// Avant TASK-050, le filtre AND (MV_Type = @modeEspece OR MV_DECAISSE = @decaisseOui)
        /// excluait ces règlements (chèque + MV_DECAISSE=0 → ni espèce ni décaissé → écarté).
        /// L'évaluateur ne touche pas à MV_DECAISSE : la correction est dans la requête SQL.
        /// Ce test valide que l'évaluateur accepte bien une ligne avec MV_DECAISSE=0 et MV_Type=1.
        /// </summary>
        [Fact]
        public async Task Evaluer_ChequeAvecMvDecaisseZero_ResteEligible_Task050()
        {
            // MV_Type=1 (Chèque), MV_DECAISSE=0 — FC2600896-like
            var row = CreateValidRow(GrfEnums.Domaine_ReglementFournisseur, modePaiement: 1);
            row.MV_DECAISSE = 0; // MV_DECAISSE=0 ne doit plus exclure

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            // La correction SQL (suppression du filtre MV_DECAISSE) permet à cette ligne
            // d'atteindre l'évaluateur. L'évaluateur lui-même ignore MV_DECAISSE → Eligible.
            Assert.True(result.EstEligible, "Un chèque (MV_Type=1) avec MV_DECAISSE=0 doit être éligible (TASK-050).");
            Assert.Equal(MotifRejet.Eligible, result.Motif);
        }

        /// <summary>
        /// TASK-050 (b) : Une facture affectée mais non rapprochée est valorisée (cache token NULL)
        /// mais non déclarable. C'est le comportement facture-first : la facture est visible dans
        /// l'écran Factures avec HT/TVA réels, mais ne peut pas entrer dans une déclaration.
        /// </summary>
        [Fact]
        public async Task Evaluer_FactureSansRappro_EstValorisableMaisNonDeclarable_Task050()
        {
            var row = CreateValidRow();
            row.MV_Point = 0; // Non rapproché

            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Achat, null);

            Assert.False(result.EstEligible, "Une facture non rapprochée ne doit pas être déclarable (garde-fou TASK-050).");
            Assert.True(result.EstValorisable, "Une facture non rapprochée doit être valorisable pour affichage (TASK-050).");
            Assert.Equal(MotifRejet.NonRapproche, result.Motif);
        }

        /// <summary>
        /// TASK-050 (c) : Non-régression déclaration — aucune facture non rapprochée ne passe
        /// dans une déclaration. EstEligible est strictement false pour NonRapproche.
        /// Gate de déclaration invariant (TVA-sur-encaissement Maroc).
        /// </summary>
        [Theory]
        [InlineData(MotifRejet.NonRapproche)]
        [InlineData(MotifRejet.HorsPeriode)]
        [InlineData(MotifRejet.DejaDeclare)]
        [InlineData(MotifRejet.NonComptabilise)]
        [InlineData(MotifRejet.Annule)]
        [InlineData(MotifRejet.Impaye)]
        [InlineData(MotifRejet.NonAffecte)]
        public void NonRegression_AucuneFactureNonRapprocheeDeclarable_Task050(MotifRejet motif)
        {
            // Invariant : seul Eligible passe dans une déclaration.
            // Tous les autres motifs (dont NonRapproche) ont EstEligible=false.
            var candidate = new AffectationCandidate { Motif = motif };
            Assert.False(candidate.EstEligible,
                $"Motif={motif} ne doit jamais être déclarable (gate rapprochement TASK-050).");
        }

        [Fact]
        public async Task Evaluer_ClientEncaissementValid_ReturnsEligible_Task084()
        {
            var row = CreateValidRow(GrfEnums.Domaine_ReglementClient);
            row.MV_DECAISSE = GrfEnums.Decaisse_Non; // Encaissement
            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Vente, null);

            Assert.True(result.EstEligible);
            Assert.Equal(MotifRejet.Eligible, result.Motif);
            Assert.Equal(SensAffectation.Vente, result.Affectation.Sens);
            Assert.Equal(SourceAffectation.Encaissement, result.Affectation.Source);
        }

        [Fact]
        public async Task Evaluer_ClientEncaissementNonRapproche_ReturnsNonRapproche_Task084()
        {
            var row = CreateValidRow(GrfEnums.Domaine_ReglementClient);
            row.MV_DECAISSE = GrfEnums.Decaisse_Non;
            row.MV_Point = 0; // Non rapproché
            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Vente, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.NonRapproche, result.Motif);
        }

        [Fact]
        public async Task Evaluer_ClientEncaissementImpaye_ReturnsImpaye_Task084()
        {
            var row = CreateValidRow(GrfEnums.Domaine_ReglementClient);
            row.MV_DECAISSE = GrfEnums.Decaisse_Non;
            row.MV_Impaye = GrfEnums.Impaye_Impaye;
            var result = await SelectionExpliqueeEvaluator.EvaluerAsync(row, _debut, _fin, SensAffectation.Vente, null);

            Assert.False(result.EstEligible);
            Assert.Equal(MotifRejet.Impaye, result.Motif);
        }
    }
}
