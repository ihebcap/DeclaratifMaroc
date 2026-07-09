using System;
using Declaration.Core.Model;

namespace Declaration.Selection
{
    public enum MotifRejet
    {
        Eligible = 0,
        NonRapproche,
        HorsPeriode,
        DejaDeclare,
        NonComptabilise,
        Annule,
        Impaye,
        NonAffecte,
        FactureIntrouvable,
        TaxeNonATaux
    }

    public class AffectationCandidate
    {
        public AffectationADeclarer Affectation { get; set; } = new AffectationADeclarer();
        
        public MotifRejet Motif { get; set; }
        public bool EstEligible => Motif == MotifRejet.Eligible;

        public string MotifLibelle => Motif switch
        {
            MotifRejet.Eligible => "Éligible",
            MotifRejet.NonRapproche => "Règlement non rapproché",
            MotifRejet.HorsPeriode => "Rapproché hors de la période",
            MotifRejet.DejaDeclare => "Déjà déclaré (période précédente)",
            MotifRejet.NonComptabilise => "Règlement non comptabilisé",
            MotifRejet.Annule => "Règlement annulé",
            MotifRejet.Impaye => "Règlement impayé",
            MotifRejet.NonAffecte => "Rapproché mais non affecté à une facture",
            MotifRejet.FactureIntrouvable => "Facture introuvable dans Sage",
            MotifRejet.TaxeNonATaux => "Ligne de taxe non éligible (type ≠ taux)",
            _ => "Motif inconnu"
        };
    }
}
