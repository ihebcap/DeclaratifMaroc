using System;
using System.Collections.Generic;

namespace Declaration.Core.Model
{
    public enum EtatConformite
    {
        Conforme,
        IfManquant,
        IceManquant,
        FormatInvalide
    }

    public enum SensAffectation { Achat, Vente }

    public enum SourceAffectation { Decaissement, Espece, Depense, Encaissement }

    public class TiersInfo
    {
        public string Numero { get; set; } = "";
        public string Nom { get; set; } = "";
        public string IdentifiantFiscal { get; set; } = "";
        public string Ice { get; set; } = "";
        public string CodeActivite { get; set; } = "";
    }

    public class AffectationADeclarer
    {
        public string NumeroFacture { get; set; } = "";
        public string NumeroRapprochement { get; set; } = "";
        public SensAffectation Sens { get; set; }
        public SourceAffectation Source { get; set; }
        public decimal MontantAffecte { get; set; }
        public DateTime? DatePaiement { get; set; }
        public DateTime? DateFacture { get; set; }
        public string ModePaiement { get; set; } = "";
        public TiersInfo Tiers { get; set; } = new TiersInfo();
        public bool EstRapprocheNonAffecte { get; set; } = false;
        public int EC_Type { get; set; }
        public int EC_Id { get; set; }

        // TASK-077 : identifiant du mouvement de règlement (RT_MOUVEMENT.MV_Id) à l'origine de
        // cette affectation — snapshoté au figeage pour permettre une revalidation ultérieure
        // ciblée (le règlement est-il toujours pointé ?), sans avoir à rejouer toute la
        // sélection. 0 si aucun règlement rattaché (cf. AffectationCandidateRow.MV_Id).
        public int MV_Id { get; set; }
    }

    public class LigneDeclarationEnrichie
    {
        public string NumeroFacture { get; set; } = "";
        public string NumeroRapprochement { get; set; } = "";
        public string Designation { get; set; } = "";
        public TiersInfo Tiers { get; set; } = new TiersInfo();
        public string CodeActivite { get; set; } = "";
        public decimal HT { get; set; }
        public decimal Taux { get; set; }
        public decimal Tva { get; set; }
        public decimal Ttc { get; set; }
        public decimal Prorata { get; set; }
        public string ModePaiement { get; set; } = "";
        public DateTime? DatePaiement { get; set; }
        public DateTime? DateFacture { get; set; }
        public SourceAffectation Source { get; set; }
        public bool IsReport { get; set; } = false;
    }

    public class RecapParSource
    {
        public SourceAffectation Source { get; set; }
        public decimal TotalHT { get; set; }
        public decimal TotalTva { get; set; }
        public decimal TotalTtc { get; set; }
    }

    public class RecapParTaux
    {
        public decimal Taux { get; set; }
        // TASK-180 : clivage fiscal Collecté (Source == Encaissement) / Déductible (autre source),
        // même critère que RecapParSource — permet de scinder l'affichage Excel sans recalcul TVA.
        public bool Collecte { get; set; }
        public decimal TotalHT { get; set; }
        public decimal TotalTva { get; set; }
        public decimal TotalTtc { get; set; }
    }

    public class RecapParActivite
    {
        public string CodeActivite { get; set; } = "";
        // TASK-180 : voir RecapParTaux.Collecte.
        public bool Collecte { get; set; }
        public decimal TotalHT { get; set; }
        public decimal TotalTva { get; set; }
        public decimal TotalTtc { get; set; }
    }

    public class ControleEquilibre
    {
        public decimal TotalMontantAffecte { get; set; }
        public decimal TotalDeclareTtc { get; set; }
        public decimal ResiduNonTva => TotalMontantAffecte - TotalDeclareTtc;
        public decimal ResiduExplique { get; set; }
        public decimal ResiduInexplique => ResiduNonTva - ResiduExplique;
    }

    public enum NiveauAlerte { Info, Warning, Error }

    public class Alerte
    {
        public NiveauAlerte Niveau { get; set; }
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
        public string RefLigne { get; set; } = "";
    }

    public enum TypePeriode { Mensuelle, Trimestrielle }

    public class EnTeteDeclaration
    {
        public string IdentifiantSociete { get; set; } = "";
        public int Exercice { get; set; }
        public TypePeriode Type { get; set; }
        public int? MoisPeriode { get; set; }
        public int? TrimestrePeriode { get; set; }
        public string Numero { get; set; } = "";
    }

    public class DeclarationModele
    {
        public EnTeteDeclaration EnTete { get; set; } = new EnTeteDeclaration();
        public List<LigneDeclarationEnrichie> Lignes { get; set; } = new();
        public List<RecapParSource> RecapsParSource { get; set; } = new();
        public List<RecapParTaux> RecapsParTaux { get; set; } = new();
        public List<RecapParActivite> RecapsParActivite { get; set; } = new();
        public ControleEquilibre ControleEquilibre { get; set; } = new();
        public List<Alerte> Alertes { get; set; } = new();
    }

    /// <summary>TASK-160 : ligne de la feuille "Règlements sélectionnés" de l'export de contrôle.</summary>
    public class ReglementSelectionneInfo
    {
        public string Numero { get; set; } = "";
        public DateTime? Date { get; set; }
        public decimal Montant { get; set; }
        public string Tiers { get; set; } = "";
        public string Mode { get; set; } = "";
        public string EtatPointage { get; set; } = "";
        // TASK-180 : date de rapprochement sortie du texte de EtatPointage vers un champ dédié
        // (colonne Excel séparée, exploitable), null si non rapproché.
        public DateTime? DateRapprochement { get; set; }
    }

    /// <summary>
    /// TASK-160 : modèle dédié à l'export de contrôle ad-hoc (réexécutable avant clôture),
    /// distinct de <see cref="DeclarationModele"/>/<c>ConstruireModeleExportAsync</c> qui n'accepte
    /// que les lignes Intégrée d'une déclaration Clôturée. Regroupe règlements sélectionnés + lignes
    /// Intégrée||Proposée + agrégats taux/activité + contrôle d'équilibre.
    /// </summary>
    public class ModeleControle
    {
        public EnTeteDeclaration EnTete { get; set; } = new EnTeteDeclaration();
        public List<ReglementSelectionneInfo> ReglementsSelectionnes { get; set; } = new();
        public List<LigneDeclarationEnrichie> Lignes { get; set; } = new();
        public List<RecapParTaux> RecapsParTaux { get; set; } = new();
        public List<RecapParActivite> RecapsParActivite { get; set; } = new();
        public ControleEquilibre ControleEquilibre { get; set; } = new();
    }
}
