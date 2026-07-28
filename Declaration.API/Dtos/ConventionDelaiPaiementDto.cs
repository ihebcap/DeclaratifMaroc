using System;
using Declaration.Application.Entities;
using Declaration.Core;

namespace Declaration.API.Dtos;

/// <summary>
/// TASK-130 (Délai de Paiement Maroc — Convention par tiers, FRONT) : projection JSON de la grille
/// liste — une ligne par convention, colonnes conformes à l'Étape 1 de la TASK (tiers, type, dates ou
/// n° facture, délai en jours, statut de validité, pièce jointe).
/// </summary>
public sealed class ConventionDelaiPaiementDto
{
    public int CpId { get; init; }
    public int TiersNo { get; init; }
    public string TiersCode { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string Numero { get; init; } = string.Empty;
    public DateTime? DateDebut { get; init; }
    public DateTime? DateFin { get; init; }
    public int NombreJoursDelaisPaiement { get; init; }
    public string Domaine { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int? FactureNo { get; init; }
    public string? FactureNumero { get; init; }
    public bool HasFile { get; init; }

    /// <summary>
    /// Statut de validité (Étape 1 de la TASK) : Convention → <c>DateFin ≥ aujourd'hui</c> ;
    /// Facture → toujours valide dès lors qu'elle est rattachée à une échéance (FactureNo posé —
    /// toujours vrai en pratique, le service refuse la création sinon).
    /// </summary>
    public bool Valide { get; init; }

    public static ConventionDelaiPaiementDto From(ConventionDelaiPaiementListItem item)
    {
        var valide = item.Type == TypeConventionDelaiPaiement.Facture
            ? item.FactureNo.HasValue
            : item.DateFin.HasValue && item.DateFin.Value.Date >= DateTime.Today;

        return new ConventionDelaiPaiementDto
        {
            CpId = item.CpId,
            TiersNo = item.TiersNo,
            TiersCode = item.TiersCode,
            Date = item.Date,
            Numero = item.Numero,
            DateDebut = item.DateDebut,
            DateFin = item.DateFin,
            NombreJoursDelaisPaiement = item.NombreJoursDelaisPaiement,
            Domaine = item.Domaine.ToString(),
            Type = item.Type.ToString(),
            FactureNo = item.FactureNo,
            FactureNumero = item.FactureNumero,
            HasFile = item.HasFile,
            Valide = valide,
        };
    }

    /// <summary>
    /// Endpoint <c>GET {cpId}</c> (détail unitaire, fourni pour complétude CRUD — le front TASK-130
    /// consomme <see cref="From(ConventionDelaiPaiementListItem)"/> via <c>GetAll</c> pour sa grille).
    /// FactureNumero non résolu ici (pas de jointure sur ce chemin, décision documentée en VERIFY) :
    /// null pour le type Facture, sans impact sur l'écran liste qui ne passe pas par cette méthode.
    /// </summary>
    public static ConventionDelaiPaiementDto FromDetail(ConventionDelaiPaiementTiers entite)
    {
        var valide = entite.Type == TypeConventionDelaiPaiement.Facture
            ? entite.FactureNo.HasValue
            : entite.DateFin.HasValue && entite.DateFin.Value.Date >= DateTime.Today;

        return new ConventionDelaiPaiementDto
        {
            CpId = entite.CpId,
            TiersNo = entite.TiersNo,
            TiersCode = entite.TiersCode,
            Date = entite.Date,
            Numero = entite.Numero,
            DateDebut = entite.DateDebut,
            DateFin = entite.DateFin,
            NombreJoursDelaisPaiement = entite.NombreJoursDelaisPaiement,
            Domaine = entite.Domaine.ToString(),
            Type = entite.Type.ToString(),
            FactureNo = entite.FactureNo,
            FactureNumero = null,
            HasFile = entite.File != null && entite.File.Length > 0,
            Valide = valide,
        };
    }
}

/// <summary>Corps JSON de <c>POST /api/conventions-delai-paiement</c>.</summary>
public sealed class CreerConventionRequestBody
{
    public int SoId { get; set; }
    public int TiersNo { get; set; }
    public string TiersCode { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateTime? DateDebut { get; set; }
    public DateTime? DateFin { get; set; }
    public int NombreJoursDelaisPaiement { get; set; }
    /// <summary>"achat" | "vente" (insensible à la casse).</summary>
    public string Domaine { get; set; } = string.Empty;
    /// <summary>"convention" | "facture" (insensible à la casse).</summary>
    public string Type { get; set; } = string.Empty;
    public int? FactureNo { get; set; }
    public string? FileName { get; set; }
    /// <summary>
    /// Contenu du PDF encodé en base64 (pas de multipart/form-data — aucun autre endpoint de ce
    /// projet n'en utilise, décision documentée en VERIFY TASK-130 : reste cohérent avec le style
    /// JSON uniforme du reste de l'API).
    /// </summary>
    public string? FileBase64 { get; set; }
}

/// <summary>Corps JSON de <c>PUT /api/conventions-delai-paiement/{cpId}/terminer</c>.</summary>
public sealed class TerminerConventionRequestBody
{
    public DateTime NouvelleDateFin { get; set; }
}
