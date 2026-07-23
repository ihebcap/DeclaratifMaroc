using System;
using System.Text.Json.Serialization;
using Declaration.Application.Entities;

namespace Declaration.API.Dtos;

/// <summary>
/// Contrat de réponse explicite des lignes candidates (TASK-034).
///
/// Les clés JSON sont figées via <see cref="JsonPropertyName"/> et alignées sur le
/// vocabulaire déjà consommé par le front (DomainGrid / WorkstationPanel), afin que
/// le contrat ne dépende plus du camelCase accidentel des membres C#
/// (ex. « HT » → « hT », « TVA » → « tVA »).
///
/// Projection PURE en lecture seule : aucun effet de bord, aucun recalcul de TVA
/// (la valorisation reste celle produite par le back — cf. TASK-030).
/// </summary>
public sealed record LigneCandidateDto
{
    public LigneCandidateDto(LigneCandidate l)
    {
        Id = l.Id;
        FactureNumero = l.NumeroFacture;
        NumeroRapprochement = l.NumeroRapprochement;
        Tiers = l.TiersNom;
        TiersIdentifiantFiscal = l.TiersIdentifiantFiscal;
        TiersICE = l.TiersICE;
        MontantHT = l.HT;
        TauxTVA = l.Taux;
        MontantTVA = l.TVA;
        MontantTTC = l.TTC;
        Prorata = l.Prorata;
        MontantAffecte = l.MontantAffecte;
        Source = l.Source;
        StatutLigne = (int)l.Etat; // int laissé brut : renderCell mappe l'index vers le libellé
        Motif = l.MotifRejet;
        StatutConformite = l.Conformite.ToString(); // libellé lisible ("Conforme", "IfManquant", …)
        // Libellé d'origine EC_Type : source UNIQUE partagée avec l'écran Rapprochement
        // (ReglementRapprochementRow.LibelleEcType, TASK-036) — pas de mapping dupliqué (TASK-038).
        Origine = ReglementRapprochementRow.LibelleEcType(l.EcType);
        // TASK-078 : clé pour cibler valider-incohérence/resynchroniser + état de traçabilité,
        // exposés en lecture seule (aucune valeur ni total recalculé).
        EcId = l.EC_Id;
        IncoherenceValidee = l.IncoherenceValidee;
        // TASK-161 : code activité résolu en cascade, exposé en lecture + son état de surcharge
        // manuelle (même pattern qu'IncoherenceValidee ci-dessus). "" défensif si la colonne SQL
        // est NULL (ligne figée avant la migration 010).
        CodeActivite = l.CodeActivite ?? "";
        CodeActiviteModifieManuellement = l.CodeActiviteModifieManuellement;
    }

    [JsonPropertyName("id")]
    public Guid Id { get; }

    [JsonPropertyName("factureNumero")]
    public string FactureNumero { get; }

    [JsonPropertyName("numeroRapprochement")]
    public string NumeroRapprochement { get; }

    [JsonPropertyName("tiers")]
    public string Tiers { get; }

    [JsonPropertyName("tiersIdentifiantFiscal")]
    public string TiersIdentifiantFiscal { get; }

    [JsonPropertyName("tiersICE")]
    public string TiersICE { get; }

    [JsonPropertyName("montantHT")]
    public decimal MontantHT { get; }

    [JsonPropertyName("tauxTVA")]
    public decimal TauxTVA { get; }

    [JsonPropertyName("montantTVA")]
    public decimal MontantTVA { get; }

    [JsonPropertyName("montantTTC")]
    public decimal MontantTTC { get; }

    // TASK-055 — opérandes de traçabilité du prorata (payé÷TTC=%, TVA×%=déclarée) : exposition
    // pure de valeurs déjà produites par Declaration.Core, pas de nouveau calcul.
    [JsonPropertyName("prorata")]
    public decimal Prorata { get; }

    [JsonPropertyName("montantAffecte")]
    public decimal MontantAffecte { get; }

    [JsonPropertyName("source")]
    public string Source { get; }

    [JsonPropertyName("statutLigne")]
    public int StatutLigne { get; }

    [JsonPropertyName("motif")]
    public string Motif { get; }

    [JsonPropertyName("statutConformite")]
    public string StatutConformite { get; }

    [JsonPropertyName("origine")]
    public string Origine { get; }

    [JsonPropertyName("ecId")]
    public int EcId { get; }

    [JsonPropertyName("incoherenceValidee")]
    public bool IncoherenceValidee { get; }

    [JsonPropertyName("codeActivite")]
    public string CodeActivite { get; }

    [JsonPropertyName("codeActiviteModifieManuellement")]
    public bool CodeActiviteModifieManuellement { get; }
}
