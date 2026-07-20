using System;
using System.Collections.Generic;

namespace Declaration.Application.Entities;

// ─── Diagnostic explicatif en ligne d'une ligne en anomalie (TASK-144) ──────────────
//
// LECTURE SEULE STRICTE. Ces structures ne portent que des données déjà lues/persistées
// (RT_ECHEANCE, DM_VENTILATION_SAGE_CACHE, F_DOCREGL) — aucun recalcul de valorisation,
// aucune écriture. Elles alimentent le panneau « Diagnostiquer » (front) pour que le
// comptable comprenne, sans aide externe, pourquoi une ligne est en anomalie.

/// <summary>Ligne RT_ECHEANCE (base GRF) de l'échéance consultée : identité Sage exacte.</summary>
public sealed class EcheanceDiagnosticRow
{
    public int EC_Id { get; set; }
    public int EC_No { get; set; }
    public int EC_Type { get; set; }
    public string DO_Numero { get; set; } = "";
    public string CT_Code { get; set; } = "";
    public string CT_Intitule { get; set; } = "";
    public decimal? EC_MtDevise { get; set; }
}

/// <summary>
/// Autre échéance RT_ECHEANCE partageant le MÊME DO_Numero (collision potentielle entre tiers,
/// cf. mémoire grf-do-numero-collision-multi-tiers).
/// </summary>
public sealed class EcheanceCollisionRow
{
    public int EC_Id { get; set; }
    public int EC_No { get; set; }
    public string CT_Code { get; set; } = "";
    public string CT_Intitule { get; set; } = "";
}

/// <summary>
/// Document de règlement Sage (table F_DOCREGL, base Sage résolue par SO_Id) : présence d'un
/// document réel pour un EC_No donné (jointure EC_No = DR_No, cf. AUDIT-TASK-143).
/// </summary>
public sealed class DocumentReglementSageRow
{
    public int DR_No { get; set; }
    public string? DO_Piece { get; set; }
    public DateTime? DR_Date { get; set; }
}

/// <summary>
/// Une échéance de la collision DO_Numero, enrichie du verdict F_DOCREGL (document Sage réel
/// ou orpheline). Affichée comme information INDÉPENDANTE de l'échec de valorisation OM
/// (jamais présentée comme la cause automatique de l'anomalie — cf. cas réel FA2600106).
/// </summary>
public sealed class DiagnosticCollisionEcheance
{
    public int EC_Id { get; set; }
    public int EC_No { get; set; }
    public string TiersCode { get; set; } = "";
    public string TiersIntitule { get; set; } = "";
    public bool ADocumentSage { get; set; }
    public string? DoPieceSage { get; set; }
    public DateTime? DateDocSage { get; set; }
    /// <summary>Vrai pour l'échéance effectivement consultée (celle du diagnostic).</summary>
    public bool EstLigneConsultee { get; set; }
}

/// <summary>
/// TASK-147 : dernière lecture connue du cache de ventilation (DM_VENTILATION_SAGE_CACHE) pour un
/// EC_Id, quel que soit son résultat (succès ou sentinelle ERREUR).
/// </summary>
public sealed class CacheLectureRow
{
    public DateTime DateLecture { get; set; }
    public string? MotifErreur { get; set; }
    public decimal? TotalHT { get; set; }
    public decimal? TotalTva { get; set; }
    public decimal? TotalTtc { get; set; }
}

/// <summary>TASK-147 : un bucket de taux déjà lu avec succès dans le cache de ventilation.</summary>
public sealed class CacheBucketRow
{
    public decimal Taux { get; set; }
    public decimal HT { get; set; }
    public decimal Tva { get; set; }
    public decimal TTC { get; set; }
}

/// <summary>Résultat agrégé du diagnostic d'une ligne en anomalie (TASK-144).</summary>
public sealed class DiagnosticLigneResultat
{
    // Bloc 1 — identité de l'échéance Sage en cause
    public int EC_Id { get; set; }
    public int EC_No { get; set; }
    public string DoNumero { get; set; } = "";
    public string TiersCode { get; set; } = "";
    public string TiersIntitule { get; set; } = "";
    public decimal? MontantDevise { get; set; }
    public string Origine { get; set; } = "";

    // Bloc 2 — ce que la tentative de lecture/valorisation OM a réellement produit
    public string MotifTechnique { get; set; } = "";
    public string? MotifErreurCache { get; set; }

    // Traduction métier + action recommandée (bloc 2, langage comptable)
    public string ExplicationMetier { get; set; } = "";
    public string? ActionRecommandee { get; set; }
    public string CodeMotifReconnu { get; set; } = "";

    // Bloc 3 — contrôle collision DO_Numero (INDÉPENDANT du bloc 2)
    public bool CollisionDetectee { get; set; }
    public List<DiagnosticCollisionEcheance> Collisions { get; set; } = new();
    public string? CollisionCommentaire { get; set; }

    // Bloc 4 — TASK-147 : cache PÉRIMÉ (relu avec succès après la création de la déclaration).
    // INDÉPENDANT du bloc 2 (motif figé au moment de la création) — jamais fusionné avec lui.
    public bool CachePerime { get; set; }
    public DateTime? CacheDateLecture { get; set; }
    public string? CachePerimeCommentaire { get; set; }
}
