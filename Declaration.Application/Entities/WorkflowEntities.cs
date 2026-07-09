using System;

namespace Declaration.Application.Entities;

public enum StatutDeclaration
{
    EnCours,
    Cloturee,
    Generee,
    Deposee
}

public enum TypePeriode
{
    Mensuelle,
    Trimestrielle
}

public class DeclarationEntete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Numero { get; set; } = "";
    public string SocieteId { get; set; } = "";
    public int Exercice { get; set; }
    public TypePeriode Type { get; set; }
    public int Periode { get; set; } // Mois (1-12) ou Trimestre (1-4)
    public StatutDeclaration Statut { get; set; } = StatutDeclaration.EnCours;
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public DateTime? DateCloture { get; set; }
}

public enum EtatLigne
{
    Proposee,
    Integree,
    Exclue,
    Reportee,
    Ecartee
}

public class LigneCandidate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeclarationId { get; set; }
    public EtatLigne Etat { get; set; } = EtatLigne.Proposee;
    public Declaration.Core.Model.EtatConformite Conformite => Declaration.Core.ValidationIdentiteFiscale.Evaluer(TiersIdentifiantFiscal, TiersICE);
    public string Domaine { get; set; } = ""; // ex: Decaissement, Encaissement
    public string MotifRejet { get; set; } = ""; // Pour les lignes exclues

    // Champs déclaratifs snapshotés
    public string NumeroFacture { get; set; } = "";
    public string NumeroRapprochement { get; set; } = "";
    public string TiersNom { get; set; } = "";
    public string TiersIdentifiantFiscal { get; set; } = "";
    public string TiersICE { get; set; } = "";
    public decimal HT { get; set; }
    public decimal Taux { get; set; }
    public decimal TVA { get; set; }
    public decimal TTC { get; set; }
    public string ModePaiement { get; set; } = "";
    public DateTime? DatePaiement { get; set; }
    public DateTime? DateFacture { get; set; }
    public string Source { get; set; } = "";
}
