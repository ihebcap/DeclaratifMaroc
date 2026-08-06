using System;

namespace Declaration.Application.Services;

/// <summary>
/// TASK-144 : traduction d'un motif technique d'anomalie en langage métier comptable + action
/// recommandée. Objectif PO : le comptable doit comprendre par lui-même, dans l'app, quoi faire —
/// pas seulement lire un code technique (reproche fait au rapport ponctuel TASK-143).
///
/// PURE, sans effet de bord, sans accès base. Le motif technique stocké sur la ligne
/// (<c>LigneCandidate.MotifRejet</c>) est un MESSAGE texte (posé par ConstructeurDeclaration /
/// MapLignesCandidates) ; on le reconnaît par motif de sous-chaîne plutôt que par un code figé
/// qui n'est pas persisté. Repli explicite (jamais muet) si le motif n'est pas reconnu.
///
/// ⚠ Les libellés métier ci-dessous DOIVENT être relus et validés par le PO avant clôture de
/// TASK-144 (critère de compréhensibilité, pas d'exhaustivité technique).
/// </summary>
public static class DiagnosticMotifMetier
{
    public sealed record Traduction(string Code, string Explication, string? Action);

    public static Traduction Traduire(string? motifTechnique, string? motifErreurCache)
    {
        var motif = (motifTechnique ?? "").Trim();
        var m = motif.ToLowerInvariant();
        var cache = (motifErreurCache ?? "").Trim();

        // SOLDE_INITIAL_SAISIE_REQUISE (TASK-025 — décision PO) : le solde initial (EC_Type=4) n'a
        // aucun détail HT/TVA/taux côté Sage (montant connu en TTC seul) — jamais un motif figé
        // « non géré » : le comptable peut le résoudre lui-même via une saisie manuelle, distincte
        // d'une relecture Sage (qui ne changerait rien ici, aucune donnée Sage à relire).
        if (m.StartsWith("solde initial"))
        {
            return new Traduction(
                "SOLDE_INITIAL_SAISIE_REQUISE",
                motif,
                "Saisissez le taux et le montant de TVA de ce solde initial ci-dessous pour l'intégrer à la déclaration "
                + "(le HT sera déduit automatiquement : TTC − TVA saisie).");
        }

        // FACTURE_ILLISIBLE_OM — la lecture Objets Métier Sage a échoué sur la pièce. Le motif
        // exact vient soit du message de la ligne, soit (plus fiable) du cache DM_VENTILATION_SAGE_CACHE.
        if (m.Contains("lecture om") || m.Contains("illisible") || m.Contains("valeur invalide")
            || !string.IsNullOrEmpty(cache))
        {
            var detail = !string.IsNullOrEmpty(cache) ? $" Message Sage : « {cache} »." : "";
            return new Traduction(
                "FACTURE_ILLISIBLE_OM",
                $"Sage n'a pas pu fournir le détail TVA de cette facture (échec de lecture Objets Métier).{detail} "
                + "La ligne ne peut donc pas être valorisée automatiquement.",
                "Vérifiez dans Sage que la facture est bien comptabilisée sous ce numéro de pièce et ce tiers, "
                + "que la ventilation de TVA y est renseignée, puis relancez la valorisation. "
                + "Si la facture est correcte côté Sage, contactez le support Sage avec le message d'erreur ci-dessus.");
        }

        // FACTURE_INTROUVABLE — aucun document (DTO) n'a été renvoyé pour l'échéance.
        if (m.Contains("introuvable") || m.Contains("dto non fourni") || m.Contains("non ventilée"))
        {
            return new Traduction(
                "FACTURE_INTROUVABLE",
                "La facture rattachée à ce règlement n'a pas été retrouvée côté Sage : aucun document n'a "
                + "été renvoyé pour cette échéance, la TVA ne peut donc pas être ventilée.",
                "Vérifiez dans Sage qu'une facture porte bien ce numéro de pièce pour ce tiers (attention aux "
                + "numéros de pièce identiques entre deux tiers — voir le contrôle « numéro de pièce » ci-dessous). "
                + "Si la facture existe, relancez la valorisation ; sinon, ce règlement ne doit pas être déclaré cette période.");
        }

        // Repli honnête : motif non catalogué — on n'invente pas d'explication, on affiche le brut.
        return new Traduction(
            "MOTIF_NON_CATALOGUE",
            string.IsNullOrEmpty(motif)
                ? "La ligne est en anomalie mais aucun motif technique n'a été enregistré."
                : $"Anomalie signalée : {motif}",
            "Motif non encore traduit en langage métier. Notez le message technique ci-dessus et signalez-le "
            + "à l'équipe GénéraFi pour enrichir le diagnostic.");
    }

    /// <summary>
    /// Commentaire métier du contrôle collision DO_Numero. INDÉPENDANT de l'échec de valorisation :
    /// ne jamais affirmer que la collision est la cause de l'anomalie (cf. cas réel FA2600106 où la
    /// déclaration pointait pourtant la bonne échéance).
    /// </summary>
    public static string CommentaireCollision(bool consulteeADocument, int nbAutresAvecDoc, int nbOrphelins)
    {
        if (consulteeADocument)
            return "L'échéance consultée pointe bien un document Sage réel — la collision de numéro de pièce "
                 + "avec un autre tiers n'est donc pas la cause de l'anomalie de valorisation (à traiter séparément).";

        if (nbAutresAvecDoc > 0)
            return "L'échéance consultée est ORPHELINE (aucun document Sage) alors qu'un autre tiers porte le même "
                 + "numéro de pièce AVEC un document réel : le règlement pourrait être rattaché à la mauvaise échéance. "
                 + "Vérifiez le tiers attendu dans Sage avant de déclarer.";

        return "Plusieurs tiers partagent ce numéro de pièce et aucun ne porte de document Sage clairement identifié : "
             + "vérifiez manuellement la comptabilisation côté Sage.";
    }
}
