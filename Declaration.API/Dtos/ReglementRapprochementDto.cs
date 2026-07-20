using System;
using System.Text.Json.Serialization;
using Declaration.Application.Entities;

namespace Declaration.API.Dtos;

/// <summary>
/// Contrat de réponse explicite d'une ligne « Rapprochement bancaire » (TASK-036).
///
/// Pivot = le RÈGLEMENT. Clés JSON figées via <see cref="JsonPropertyName"/> en vocabulaire
/// métier (même discipline que TASK-034), alignées pour le front TASK-037.
///
/// Projection PURE en lecture seule : aucun effet de bord, aucun recalcul. Les indicateurs
/// dérivés (reste à affecter, rapproché banque, déclaré, origine, mode) proviennent d'une
/// source unique côté domaine (<see cref="ReglementRapprochementRow"/>).
/// </summary>
public sealed record ReglementRapprochementDto
{
    public ReglementRapprochementDto(ReglementRapprochementRow r)
    {
        NumeroReglement = r.MvNumero;
        Date = r.MvDate;
        Mode = r.Mode;
        Domaine = r.Domaine;
        Tiers = r.Tiers ?? "";
        TiersCode = r.TiersCode ?? "";
        Montant = r.MvMontant;
        RapprocheBanque = r.EstRapprocheBanque;
        Point = r.MvPoint == 1;                     // indicateur brut du pointage bancaire (distinct de l'espèce auto)
        DateRapprochement = r.DateRapprochement;    // pointage réel, ou MV_Date pour l'espèce
        NumeroExtrait = r.MvExtraitNum ?? "";       // vide si non pointé (jamais de n° fictif — TASK-042)
        Echeance = r.MvEcheance;
        BanqueCode = r.BanqueCode ?? "";
        NbFacturesAffectees = r.NbAffectations;
        MontantAffecte = r.MontantAffecteEffectif;
        ResteAAffecter = r.ResteAAffecter;
        Origine = r.Origine;
        Declare = r.EstDeclare;
        NumeroDeclaration = r.NumeroDeclaration;   // TASK-140 : numéro de la déclaration verrou (null si non résolu — jamais inventé)
    }

    [JsonPropertyName("numeroReglement")]
    public string NumeroReglement { get; }

    [JsonPropertyName("date")]
    public DateTime Date { get; }

    [JsonPropertyName("mode")]
    public string Mode { get; }

    [JsonPropertyName("domaine")]
    public string Domaine { get; }

    [JsonPropertyName("tiers")]
    public string Tiers { get; }

    [JsonPropertyName("tiersCode")]
    public string TiersCode { get; }

    [JsonPropertyName("montant")]
    public decimal Montant { get; }

    [JsonPropertyName("rapprocheBanque")]
    public bool RapprocheBanque { get; }

    [JsonPropertyName("point")]
    public bool Point { get; }

    [JsonPropertyName("dateRapprochement")]
    public DateTime? DateRapprochement { get; }

    [JsonPropertyName("numeroExtrait")]
    public string NumeroExtrait { get; }

    [JsonPropertyName("echeance")]
    public DateTime? Echeance { get; }

    [JsonPropertyName("banqueCode")]
    public string BanqueCode { get; }

    [JsonPropertyName("nbFacturesAffectees")]
    public int NbFacturesAffectees { get; }

    [JsonPropertyName("montantAffecte")]
    public decimal MontantAffecte { get; }

    [JsonPropertyName("resteAAffecter")]
    public decimal ResteAAffecter { get; }

    [JsonPropertyName("origine")]
    public string Origine { get; }

    [JsonPropertyName("declare")]
    public bool Declare { get; }

    // TASK-140 : numéro de la déclaration qui verrouille ce règlement (null si non déclaré/non résolu).
    // Additif (contrat étendu, rien retiré) : affiché par l'écran Rapprochement global à la place du
    // simple booléen ; l'écran ① Sélection, lui, retire ces lignes côté front.
    [JsonPropertyName("numeroDeclaration")]
    public string? NumeroDeclaration { get; }
}
