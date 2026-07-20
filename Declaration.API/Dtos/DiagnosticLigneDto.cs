using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Declaration.Application.Entities;

namespace Declaration.API.Dtos;

/// <summary>
/// TASK-144 — contrat JSON du diagnostic explicatif en ligne d'une ligne en anomalie.
/// Projection PURE lecture seule du <see cref="DiagnosticLigneResultat"/> (aucun recalcul).
/// Les trois blocs (identité échéance / résultat lecture OM / contrôle collision DO_Numero)
/// restent séparés : le front ne doit jamais présenter la collision comme LA cause de l'anomalie.
/// </summary>
public sealed record DiagnosticLigneDto
{
    public DiagnosticLigneDto(DiagnosticLigneResultat r)
    {
        EcId = r.EC_Id;
        EcNo = r.EC_No;
        DoNumero = r.DoNumero;
        TiersCode = r.TiersCode;
        TiersIntitule = r.TiersIntitule;
        MontantDevise = r.MontantDevise;
        Origine = r.Origine;
        MotifTechnique = r.MotifTechnique;
        MotifErreurCache = r.MotifErreurCache;
        ExplicationMetier = r.ExplicationMetier;
        ActionRecommandee = r.ActionRecommandee;
        CodeMotifReconnu = r.CodeMotifReconnu;
        CollisionDetectee = r.CollisionDetectee;
        CollisionCommentaire = r.CollisionCommentaire;
        Collisions = r.Collisions.Select(c => new DiagnosticCollisionDto(c)).ToList();
        CachePerime = r.CachePerime;
        CacheDateLecture = r.CacheDateLecture;
        CachePerimeCommentaire = r.CachePerimeCommentaire;
    }

    [JsonPropertyName("ecId")] public int EcId { get; }
    [JsonPropertyName("ecNo")] public int EcNo { get; }
    [JsonPropertyName("doNumero")] public string DoNumero { get; }
    [JsonPropertyName("tiersCode")] public string TiersCode { get; }
    [JsonPropertyName("tiersIntitule")] public string TiersIntitule { get; }
    [JsonPropertyName("montantDevise")] public decimal? MontantDevise { get; }
    [JsonPropertyName("origine")] public string Origine { get; }

    [JsonPropertyName("motifTechnique")] public string MotifTechnique { get; }
    [JsonPropertyName("motifErreurCache")] public string? MotifErreurCache { get; }
    [JsonPropertyName("explicationMetier")] public string ExplicationMetier { get; }
    [JsonPropertyName("actionRecommandee")] public string? ActionRecommandee { get; }
    [JsonPropertyName("codeMotifReconnu")] public string CodeMotifReconnu { get; }

    [JsonPropertyName("collisionDetectee")] public bool CollisionDetectee { get; }
    [JsonPropertyName("collisionCommentaire")] public string? CollisionCommentaire { get; }
    [JsonPropertyName("collisions")] public List<DiagnosticCollisionDto> Collisions { get; }

    // TASK-147 : cache PÉRIMÉ (relu avec succès après la création de la déclaration).
    [JsonPropertyName("cachePerime")] public bool CachePerime { get; }
    [JsonPropertyName("cacheDateLecture")] public DateTime? CacheDateLecture { get; }
    [JsonPropertyName("cachePerimeCommentaire")] public string? CachePerimeCommentaire { get; }
}

public sealed record DiagnosticCollisionDto
{
    public DiagnosticCollisionDto(DiagnosticCollisionEcheance c)
    {
        EcId = c.EC_Id;
        EcNo = c.EC_No;
        TiersCode = c.TiersCode;
        TiersIntitule = c.TiersIntitule;
        ADocumentSage = c.ADocumentSage;
        DoPieceSage = c.DoPieceSage;
        DateDocSage = c.DateDocSage;
        EstLigneConsultee = c.EstLigneConsultee;
    }

    [JsonPropertyName("ecId")] public int EcId { get; }
    [JsonPropertyName("ecNo")] public int EcNo { get; }
    [JsonPropertyName("tiersCode")] public string TiersCode { get; }
    [JsonPropertyName("tiersIntitule")] public string TiersIntitule { get; }
    [JsonPropertyName("aDocumentSage")] public bool ADocumentSage { get; }
    [JsonPropertyName("doPieceSage")] public string? DoPieceSage { get; }
    [JsonPropertyName("dateDocSage")] public DateTime? DateDocSage { get; }
    [JsonPropertyName("estLigneConsultee")] public bool EstLigneConsultee { get; }
}
