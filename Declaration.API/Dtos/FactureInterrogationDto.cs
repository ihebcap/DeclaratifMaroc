using System;
using System.Text.Json.Serialization;
using Declaration.Application.Entities;

namespace Declaration.API.Dtos;

/// <summary>
/// Contrat de réponse explicite d'une ligne « Factures » (TASK-041). Pivot = la FACTURE.
///
/// Une propriété par colonne (clés JSON figées via <see cref="JsonPropertyName"/>, vocabulaire
/// métier), y compris le motif de non-valorisation famille B — jamais un 0 silencieux.
///
/// Projection PURE en lecture seule : aucun effet de bord, aucun recalcul. Famille B servie
/// depuis le cache TASK-024 (HT/TVA) quand présent ; Autre taxe / Écart / Escompte non ventilés
/// en cache restent null (jamais dérivés d'un forfait). Statut = état à 3 valeurs dérivé du DT_Id.
/// </summary>
public sealed record FactureInterrogationDto
{
    public FactureInterrogationDto(FactureInterrogationRow r)
    {
        FactureNumero = r.DoNumero;
        Date = r.DoDate;
        FournisseurCode = r.CtCode ?? "";
        FournisseurIntitule = r.CtIntitule ?? "";
        Reference = r.DoReference ?? "";
        // Famille B (cache TASK-024) — null = non valorisé (motif fourni), jamais 0 silencieux.
        MontantHT = r.Ht;
        MontantTVA = r.Tva;
        AutreTaxe = r.AutreTaxe;
        Ecart = r.Ecart;
        Escompte = r.Escompte;
        Valorisee = r.ValoriseeB;
        MotifValorisation = r.MotifValorisation;
        // TASK-076 : montants bruts Sage (donnée d'audit, jamais déclarable) — renseignés
        // uniquement pour une facture exclue (incohérence HT/TVA/TTC, TASK-072). NULL sinon.
        HtBrut = r.HtBrut;
        TvaBrut = r.TvaBrut;
        ParafiscaleBrut = r.ParafiscaleBrut;
        TtcBrut = r.TtcBrut;
        // Famille A/C.
        MontantTTC = r.EcMontant;
        Regle = r.RegleEffectif;
        Declare = r.DeclareEffectif;
        ResteADeclarer = r.ResteADeclarer;
        SoldeFacture = r.SoldeFacture;
        NbAffectations = r.NbAffectations;
        Statut = FactureInterrogationRow.LibelleStatut(r.Statut);
        // Origine EC_Type : source UNIQUE partagée avec Rapprochement/Déclaration (TASK-036/038).
        Origine = r.Origine;
    }

    [JsonPropertyName("factureNumero")]
    public string FactureNumero { get; }

    [JsonPropertyName("date")]
    public DateTime Date { get; }

    [JsonPropertyName("fournisseurCode")]
    public string FournisseurCode { get; }

    [JsonPropertyName("fournisseurIntitule")]
    public string FournisseurIntitule { get; }

    [JsonPropertyName("reference")]
    public string Reference { get; }

    [JsonPropertyName("montantHT")]
    public decimal? MontantHT { get; }

    [JsonPropertyName("montantTVA")]
    public decimal? MontantTVA { get; }

    [JsonPropertyName("autreTaxe")]
    public decimal? AutreTaxe { get; }

    [JsonPropertyName("ecart")]
    public decimal? Ecart { get; }

    [JsonPropertyName("escompte")]
    public decimal? Escompte { get; }

    [JsonPropertyName("valorisee")]
    public bool Valorisee { get; }

    [JsonPropertyName("motifValorisation")]
    public string MotifValorisation { get; }

    // TASK-076 : montants bruts Sage tels que lus AVANT exclusion (donnée d'audit, NULL pour
    // une facture saine ou valorisée) — jamais un montant déclarable, à ne jamais confondre
    // avec montantHT/montantTVA ci-dessus.
    [JsonPropertyName("htBrut")]
    public decimal? HtBrut { get; }

    [JsonPropertyName("tvaBrut")]
    public decimal? TvaBrut { get; }

    [JsonPropertyName("parafiscaleBrut")]
    public decimal? ParafiscaleBrut { get; }

    [JsonPropertyName("ttcBrut")]
    public decimal? TtcBrut { get; }

    [JsonPropertyName("montantTTC")]
    public decimal MontantTTC { get; }

    [JsonPropertyName("regle")]
    public decimal Regle { get; }

    [JsonPropertyName("declare")]
    public decimal Declare { get; }

    [JsonPropertyName("resteADeclarer")]
    public decimal ResteADeclarer { get; }

    [JsonPropertyName("soldeFacture")]
    public decimal SoldeFacture { get; }

    [JsonPropertyName("nbAffectations")]
    public int NbAffectations { get; }

    [JsonPropertyName("statut")]
    public string Statut { get; }

    [JsonPropertyName("origine")]
    public string Origine { get; }
}
