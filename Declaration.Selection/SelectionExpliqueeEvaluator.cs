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

            // AF_Id == null (aucune affectation) testé AVANT MV_Compta/Annule/Impaye : sans
            // règlement affecté, il n'y a pas de RT_MOUVEMENT à évaluer (M est NULL, COALESCE
            // ramène MV_Compta à 0) — ces contrôles ne s'appliquent qu'aux factures affectées et
            // ne doivent jamais bloquer la lecture facture-first d'une facture NonAffecte
            // (TASK-052 : bug corrigé, NonAffecte était inatteignable car classé NonComptabilise).
            if (r.DT_Id != null) motif = MotifRejet.DejaDeclare;
            else if (r.AF_Id == null) motif = MotifRejet.NonAffecte;
            else if (r.MV_Compta != GrfEnums.Compta_Comptabilise) motif = MotifRejet.NonComptabilise;
            else if (r.MV_Annule != GrfEnums.Annule_Non) motif = MotifRejet.Annule;
            else if (source != SourceAffectation.Depense && r.MV_Impaye != GrfEnums.Impaye_NonImpaye) motif = MotifRejet.Impaye;
            // Liste blanche EC_Type : seules les vraies factures/solde (0/4/111) sont à lire.
            // Impayés, gains, remboursements, change… ne sont pas des factures ⇒ hors périmètre
            // (non valorisable), sans échec worker « facture introuvable ».
            else if (r.EC_Type != null && !GrfEnums.EstEcTypeFacture(r.EC_Type.Value)) motif = MotifRejet.EcTypeHorsPerimetre;

            if (motif == MotifRejet.Eligible)
            {
                // Période située par la DATE DE RÉFÉRENCE (TASK-062, source unique RegleDatePeriode).
                // TASK-099 : la période devient une simple date de COUPURE (fin de période) — plus de
                // borne basse (rattrapage de l'arriéré déclarable jamais déclaré, cf. RF26060125).
                // Gate EstDeclarable : espèce (auto-rapprochée) OU rapproché banque (MV_Point=1).
                // ⚠️ Keying espèce sur `source == Espece` (et NON MV_Type) : le comportement facture-first
                //    de l'espèce reste GELÉ tant que le PO n'a pas arbitré (dépense/client espèce inchangés).
                if (source == SourceAffectation.Espece)
                {
                    // Espèce fournisseur : DateReference = MV_Date (= DatePaiement).
                    if (r.DatePaiement >= finExclude)
                        motif = MotifRejet.HorsPeriode;
                }
                else if (r.MV_Point != GrfEnums.Point_Oui)
                {
                    // Non-espèce non rapproché : non déclarable (gate EstDeclarable).
                    motif = MotifRejet.NonRapproche;
                }
                else
                {
                    // Non-espèce rapproché : DateReference = MV_PointDate.
                    if (r.MV_PointDate == null || r.MV_PointDate >= finExclude)
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
                    EC_Id = r.EC_Id ?? 0,
                    // TASK-077 : MV_Id snapshoté pour la revalidation ciblée ultérieure du
                    // rapprochement (RT_MOUVEMENT.MV_Point). AffectationCandidateRow.MV_Id est
                    // déjà non-nullable (0 = aucun règlement rattaché, cf. requêtes SQL amont).
                    MV_Id = r.MV_Id
                }
            };
        }
    }
}
