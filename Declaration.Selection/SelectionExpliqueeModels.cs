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
        TaxeNonATaux,
        EcTypeHorsPerimetre,

        /// <summary>
        /// TASK-080 : le règlement/affectation est déjà Proposee/Integree dans une AUTRE déclaration
        /// EnCours ou Cloturee. Jamais posé par <see cref="SelectionExpliqueeEvaluator"/> (qui ignore
        /// toute notion de déclaration concurrente) — appliqué en aval par
        /// Declaration.Application.Services.DeclarationWorkflowService, seul endroit qui connaît la
        /// déclaration en cours de figeage et peut consulter DM_LGTVA des autres déclarations.
        /// </summary>
        DejaEnCoursAilleurs
    }

    public class AffectationCandidate
    {
        public AffectationADeclarer Affectation { get; set; } = new AffectationADeclarer();

        public MotifRejet Motif { get; set; }
        public bool EstEligible => Motif == MotifRejet.Eligible;

        /// <summary>
        /// TASK-080 : numéro de la déclaration concurrente qui porte déjà ce règlement (renseigné
        /// uniquement quand <see cref="Motif"/> = <see cref="MotifRejet.DejaEnCoursAilleurs"/>), pour
        /// un message d'exclusion honnête et précis côté front plutôt qu'un libellé générique.
        /// </summary>
        public string? ConflitDeclarationNumero { get; set; }

        /// <summary>
        /// Valorisable pour AFFICHAGE (lecture OM + cache brut), pas forcément déclarable :
        /// éligible, en attente de rapprochement (TASK-049), sans aucun règlement affecté
        /// (TASK-052), ou rapproché à une date bancaire hors de la période d'affichage demandée
        /// (TASK-076 : écran Factures pivot FACTURE — la période borne la lecture par DATE DE
        /// FACTURE, la valorisation ne doit pas dépendre en plus de la date de rapprochement).
        /// La lecture OM (taux/base) est identique quel que soit l'état du règlement ⇒ valeur
        /// exacte, jamais inventée.
        /// La DÉCLARATION reste strictement gated sur EstEligible ; une ligne cache à token NULL
        /// (facture non rapprochée ou non affectée) n'est jamais servie comme ventilation déclarable.
        /// </summary>
        public bool EstValorisable => Motif == MotifRejet.Eligible || Motif == MotifRejet.NonRapproche
            || Motif == MotifRejet.NonAffecte || Motif == MotifRejet.HorsPeriode;

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
            MotifRejet.EcTypeHorsPerimetre => "Échéance hors périmètre (ni facture ni solde)",
            // TASK-080 : libellé générique de repli — DeclarationWorkflowService construit un
            // message précis avec le numéro de la déclaration concurrente (ConflitDeclarationNumero).
            MotifRejet.DejaEnCoursAilleurs => "Déjà pris en compte dans une autre déclaration",
            _ => "Motif inconnu"
        };
    }
}
