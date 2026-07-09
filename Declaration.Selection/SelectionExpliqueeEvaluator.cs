using System;
using System.Threading.Tasks;
using Declaration.Core.Model;

namespace Declaration.Selection
{
    public class AffectationCandidateRow
    {
        public int MV_Id { get; set; }
        public int MV_Domaine { get; set; }
        public int? MV_Point { get; set; }
        public DateTime? MV_PointDate { get; set; }
        public DateTime DatePaiement { get; set; }
        public int MV_DECAISSE { get; set; }
        public int MV_Compta { get; set; }
        public int MV_Annule { get; set; }
        public int MV_Impaye { get; set; }
        public int? ModePaiementId { get; set; }
        public int? AF_Id { get; set; }
        public string? NumeroFacture { get; set; }
        public decimal? MontantAffecte { get; set; }
        public DateTime? DateFacture { get; set; }
        public int? DT_Id { get; set; }
        public int? EC_Type { get; set; }
        public int? EC_Id { get; set; }
        public string? TiersNumero { get; set; }
        public string? TiersNom { get; set; }
        public string? TiersIF { get; set; }
        public string? TiersICE { get; set; }
        public string? NumeroRapprochement { get; set; }
        public string? TiersActivite { get; set; }
    }

    public static class SelectionExpliqueeEvaluator
    {
        public static async Task<AffectationCandidate> EvaluerAsync(
            AffectationCandidateRow r, 
            DateTime debut, 
            DateTime fin,
            SensAffectation sens,
            Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>>? verifierFacture = null)
        {
            DateTime finExclude = fin.AddDays(1);
            SourceAffectation source;

            if (r.MV_Domaine == GrfEnums.Domaine_ReglementFournisseur)
            {
                source = r.ModePaiementId == GrfEnums.ModePaiement_Espece 
                    ? SourceAffectation.Espece : SourceAffectation.Decaissement;
            }
            else if (r.MV_Domaine == GrfEnums.Domaine_Depense)
            {
                source = SourceAffectation.Depense;
            }
            else
            {
                source = SourceAffectation.Encaissement;
            }

            MotifRejet motif = MotifRejet.Eligible;

            if (r.DT_Id != null) motif = MotifRejet.DejaDeclare;
            else if (r.MV_Compta != GrfEnums.Compta_Comptabilise) motif = MotifRejet.NonComptabilise;
            else if (r.MV_Annule != GrfEnums.Annule_Non) motif = MotifRejet.Annule;
            else if (source != SourceAffectation.Depense && r.MV_Impaye != GrfEnums.Impaye_NonImpaye) motif = MotifRejet.Impaye;
            else if (r.AF_Id == null) motif = MotifRejet.NonAffecte;

            if (motif == MotifRejet.Eligible)
            {
                if (source == SourceAffectation.Espece)
                {
                    if (r.DatePaiement < debut || r.DatePaiement > fin)
                        motif = MotifRejet.HorsPeriode;
                }
                else
                {
                    if (r.MV_Point != GrfEnums.Point_Oui)
                        motif = MotifRejet.NonRapproche;
                    else if (r.MV_PointDate == null || r.MV_PointDate < debut || r.MV_PointDate >= finExclude)
                        motif = MotifRejet.HorsPeriode;
                }
            }

            if (motif == MotifRejet.Eligible && verifierFacture != null && r.AF_Id != null)
            {
                var validation = await verifierFacture(r.NumeroFacture ?? "", sens);
                if (!validation.Existe) motif = MotifRejet.FactureIntrouvable;
                else if (!validation.TaxeOk) motif = MotifRejet.TaxeNonATaux;
            }

            return new AffectationCandidate
            {
                Motif = motif,
                Affectation = new AffectationADeclarer
                {
                    NumeroFacture = r.NumeroFacture ?? "",
                    NumeroRapprochement = r.NumeroRapprochement ?? "",
                    Sens = sens,
                    Source = source,
                    MontantAffecte = r.MontantAffecte ?? 0m,
                    DatePaiement = r.DatePaiement,
                    DateFacture = r.DateFacture,
                    ModePaiement = source == SourceAffectation.Espece 
                        ? "1" 
                        : GrfEnums.MapperModePaiementSimplTVA(r.ModePaiementId),
                    Tiers = new TiersInfo
                    {
                        Numero = r.TiersNumero ?? "",
                        Nom = r.TiersNom ?? "",
                        IdentifiantFiscal = r.TiersIF ?? "",
                        Ice = r.TiersICE ?? "",
                        CodeActivite = r.TiersActivite ?? ""
                    },
                    EC_Type = r.EC_Type ?? 0,
                    EC_Id = r.EC_Id ?? 0
                }
            };
        }
    }
}
