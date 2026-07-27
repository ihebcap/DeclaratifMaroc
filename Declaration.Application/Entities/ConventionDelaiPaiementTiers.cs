using System;
using Declaration.Core;

namespace Declaration.Application.Entities;

/// <summary>
/// TASK-129 (Délai de Paiement Maroc — Convention par tiers) : entité de persistance, mapping 1:1 des
/// colonnes de la table existante <c>RT_CONVENTIONTIERS</c> (propriété GRF, réutilisée telle quelle —
/// aucune migration). Reproduit le modèle legacy <c>Tresorerie.Core.Models.ConventionDelaisPaiementTiers</c>
/// (colonnes identiques), sans réutilisation de code/DLL WinForms (développement neuf).
/// </summary>
public sealed class ConventionDelaiPaiementTiers
{
    /// <summary>CP_Id — clé primaire, IDENTITY.</summary>
    public int CpId { get; set; }

    /// <summary>SO_Id — société.</summary>
    public int SocieteId { get; set; }

    /// <summary>CT_No — tiers (fournisseur ou client selon <see cref="Domaine"/>).</summary>
    public int TiersNo { get; set; }

    /// <summary>CT_Code — code tiers (dénormalisé, comme le legacy).</summary>
    public string TiersCode { get; set; } = string.Empty;

    /// <summary>CP_Date — date de saisie de la convention.</summary>
    public DateTime Date { get; set; }

    /// <summary>CP_Numero — numéro de la convention (unique par tiers/domaine, contrôle legacy l.495-496).</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>CP_DateDebut — obligatoire pour le type Convention, non utilisé pour Facture.</summary>
    public DateTime? DateDebut { get; set; }

    /// <summary>CP_DateFin — obligatoire pour le type Convention, non utilisé pour Facture.</summary>
    public DateTime? DateFin { get; set; }

    /// <summary>CP_FileName — nom du fichier joint (PDF), optionnel (décision PO 19/07/2026, pas de durcissement).</summary>
    public string? FileName { get; set; }

    /// <summary>CP_File — contenu binaire du fichier joint (varbinary), stocké tel quel, aucune validation de format.</summary>
    public byte[]? File { get; set; }

    /// <summary>CP_DelaisPaiement — délai en jours (plafonné à 180, contrôle applicatif).</summary>
    public int NombreJoursDelaisPaiement { get; set; }

    /// <summary>CP_Domaine — Achat(0)/Vente(1), mapping direct avec le legacy Fournisseur(0)/Client(1).</summary>
    public DomaineDelaiPaiement Domaine { get; set; }

    /// <summary>CP_Type — Convention(0)/Facture(1).</summary>
    public TypeConventionDelaiPaiement Type { get; set; }

    /// <summary>
    /// CP_FactureNo — pour le type Facture UNIQUEMENT. ATTENTION (vérifié en code legacy,
    /// <c>ConventionDelaisPaiementTiersRepository.Script.cs:24</c> et
    /// <c>SocieteManager.Complement.cs:523</c>) : ce champ référence <c>RT_ECHEANCE.EC_Id</c> (clé
    /// technique interne de l'échéance), PAS <c>EC_No</c> (numéro ERP) ni le numéro de document
    /// (<c>DO_Numero</c>) — le numéro de document affiché/comparé par le calculateur TASK-127
    /// (<c>FactureNumero</c>) est résolu par une jointure <c>RT_ECHEANCE.EC_Id = CP_FactureNo</c>.
    /// </summary>
    public int? FactureNo { get; set; }
}
