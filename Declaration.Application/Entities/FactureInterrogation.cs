using System.Collections.Generic;

namespace Declaration.Application.Entities;

/// <summary>
/// Statut de déclaration d'une facture — état CALCULÉ à 3 valeurs (TASK-041), dérivé des
/// affectations RT_AFFECTATION.DT_Id (jamais un booléen). La TVA fournisseur Maroc se déduit
/// au DÉCAISSEMENT : la base déclarable d'une facture = ce qui est réglé (affecté). Le tampon
/// DT_Id (TASK-028, lu jamais écrit) marque ce qui est déjà intégré dans une déclaration.
/// </summary>
public enum StatutDeclarationFacture
{
    /// <summary>Rien n'est encore déclaré (Σ affecté avec DT_Id non nul = 0). Inclut les factures non réglées (rien de décaissé ⇒ rien de déductible).</summary>
    NonDeclarable = 0,
    /// <summary>Une partie du réglé est déclarée (0 &lt; Déclaré &lt; Réglé).</summary>
    Partiel = 1,
    /// <summary>Tout le réglé est déclaré (Déclaré = Réglé, Réglé &gt; 0).</summary>
    Total = 2
}

/// <summary>
/// Ligne brute d'interrogation « Factures » (TASK-041). Pivot = la FACTURE (RT_ECHEANCE.EC_Id).
///
/// Trois familles de colonnes, sources et coûts distincts (cf. TASK-041) :
///   A — identité/facture : DO_Numero, DO_Date, fournisseur (CT_Code+CT_Intitule), DO_Reference,
///       TTC (EC_Montant), Solde — SQL local RT_ECHEANCE (gratuit).
///   C — statut déclaration : Réglé / Déclaré / Reste à déclarer / statut — agrégat RT_AFFECTATION
///       (DT_Id lu, jamais modifié). SQL local.
///   B — valorisation TVA : HT / TVA / Autre taxe / Écart / Escompte — servie depuis le cache de
///       ventilation (TASK-024) quand présent, sinon « non valorisé » + motif (jamais inventée).
///
/// Projection issue d'un SELECT strictement en lecture seule ; aucun recalcul, aucun appel Sage.
/// Les indicateurs dérivés sont calculés ici (source unique, testable hors API).
/// </summary>
public sealed class FactureInterrogationRow
{
    // ─── Famille A — identité facture (RT_ECHEANCE) ────────────────────────────
    public int EcId { get; set; }                 // RT_ECHEANCE.EC_Id — clé de jointure cache B
    public string DoNumero { get; set; } = "";    // DO_Numero — N° facture
    public System.DateTime DoDate { get; set; }   // DO_Date — date du document
    public string? CtCode { get; set; }           // CT_Code — code fournisseur
    public string? CtIntitule { get; set; }       // CT_Intitule — intitulé fournisseur
    public string? DoReference { get; set; }      // DO_Reference — référence pièce
    public decimal EcMontant { get; set; }        // EC_Montant — TTC (fait foi pour la facture)
    public int EcType { get; set; }               // EC_Type : 0=Sage, 111=FGR, 4=solde initial

    // ─── Famille C — affectations agrégées (RT_AFFECTATION) ────────────────────
    public int NbAffectations { get; set; }       // COUNT(RT_AFFECTATION)
    public decimal? Regle { get; set; }           // SUM(AF_Montant) — NULL si aucune affectation
    public decimal? Declare { get; set; }         // SUM(AF_Montant WHERE DT_Id NOT NULL) — TASK-028

    // ─── Famille B — valorisation TVA (cache TASK-024, renseignée post-lecture) ─
    // NULL ⇒ non valorisé : jamais un 0 silencieux. MotifNonValorise porte l'explication.
    public decimal? Ht { get; set; }
    public decimal? Tva { get; set; }
    public decimal? AutreTaxe { get; set; }
    public decimal? Ecart { get; set; }
    public decimal? Escompte { get; set; }
    public bool ValoriseeB { get; set; }
    public string MotifValorisation { get; set; } = "";

    // ─── TASK-076 — montants BRUTS Sage (donnée d'audit, jamais déclarable) ────
    // Renseignés UNIQUEMENT pour une facture exclue (incohérence HT+TVA(+Parafiscale)≠TTC,
    // TASK-072) : ce sont les montants tels que réellement lus chez Sage AU MOMENT DE LA
    // DÉTECTION, avant exclusion — jamais recalculés ni estimés côté back/front. Distincts
    // des colonnes valorisées (Ht/Tva ci-dessus, qui restent null pour ces factures) : ne
    // JAMAIS les confondre avec un montant déclarable. NULL par défaut = non capturé
    // (facture saine, ou capture indisponible pour ce chemin de détection précis).
    public decimal? HtBrut { get; set; }
    public decimal? TvaBrut { get; set; }
    public decimal? ParafiscaleBrut { get; set; }
    public decimal? TtcBrut { get; set; }

    // ─── Indicateurs dérivés famille C (rendus VISIBLES, jamais absorbés) ───────

    /// <summary>Montant réglé (affecté) effectif, 0 si aucune affectation.</summary>
    public decimal RegleEffectif => Regle ?? 0m;

    /// <summary>Déclaré = Σ affecté avec DT_Id non nul (0 si aucun).</summary>
    public decimal DeclareEffectif => Declare ?? 0m;

    /// <summary>Reste à déclarer = Réglé − Déclaré (part du décaissé pas encore intégrée).</summary>
    public decimal ResteADeclarer => RegleEffectif - DeclareEffectif;

    /// <summary>Solde facture = TTC − Réglé (part de la facture non encore réglée).</summary>
    public decimal SoldeFacture => EcMontant - RegleEffectif;

    /// <summary>
    /// Statut de déclaration à 3 valeurs, dérivé des affectations DT_Id :
    ///   Déclaré = 0                → NonDeclarable ;
    ///   0 &lt; Déclaré &lt; Réglé  → Partiel ;
    ///   Déclaré = Réglé (&gt; 0)   → Total.
    /// </summary>
    public StatutDeclarationFacture Statut
    {
        get
        {
            if (DeclareEffectif <= 0m) return StatutDeclarationFacture.NonDeclarable;
            if (DeclareEffectif < RegleEffectif) return StatutDeclarationFacture.Partiel;
            return StatutDeclarationFacture.Total;
        }
    }

    /// <summary>Libellé métier de l'origine de valorisation TVA (source unique partagée, TASK-036/038).</summary>
    public string Origine => ReglementRapprochementRow.LibelleEcType(EcType);

    // ─── Mappings métier (source unique, réutilisée par l'API et les distincts) ─

    /// <summary>Libellé du statut à 3 valeurs (source unique partagée API/front/filtre).</summary>
    public static string LibelleStatut(StatutDeclarationFacture s) => s switch
    {
        StatutDeclarationFacture.Total => "Total",
        StatutDeclarationFacture.Partiel => "Partiel",
        _ => "NonDeclarable"
    };

    /// <summary>
    /// Renseigne la famille B depuis le cache de ventilation TASK-024 (totaux HT/TVA de la facture).
    /// Le cache ne stocke QUE HT et TVA (buckets TVA Sage) : Autre taxe / Écart / Escompte ne sont
    /// pas ventilés en cache et restent null (jamais dérivés d'un forfait — piège ancien GRF).
    /// Absence de cache ⇒ non valorisé + motif explicite (transparence : jamais masqué).
    /// </summary>
    public void AppliquerCacheB(decimal? totalHt, decimal? totalTva, string? motifErreurSpecifique = null,
        decimal? htBrut = null, decimal? tvaBrut = null, decimal? parafiscaleBrut = null, decimal? ttcBrut = null)
    {
        if (totalHt.HasValue || totalTva.HasValue)
        {
            Ht = totalHt;
            Tva = totalTva;
            ValoriseeB = true;
            MotifValorisation = "";
        }
        else
        {
            ValoriseeB = false;
            // TASK-072 : motif précis (incohérence Sage détectée et tracée en cache) prioritaire
            // sur le motif générique « OM non lue » — sinon les deux cas sont indistinguables.
            MotifValorisation = !string.IsNullOrWhiteSpace(motifErreurSpecifique)
                ? motifErreurSpecifique
                : MotifCacheAbsent(EcType);

            // TASK-076 : montants bruts Sage (donnée d'audit) — jamais mélangés avec Ht/Tva
            // ci-dessus (qui restent null ici, jamais réintroduits dans les totaux déclarables).
            HtBrut = htBrut;
            TvaBrut = tvaBrut;
            ParafiscaleBrut = parafiscaleBrut;
            TtcBrut = ttcBrut;
        }
    }

    /// <summary>
    /// Motif de non-valorisation famille B quand le cache TASK-024 ne couvre pas la facture.
    /// Rendu VISIBLE côté grille (aucune ligne silencieuse — règle n°1 du projet).
    /// </summary>
    public static string MotifCacheAbsent(int ecType) => ecType switch
    {
        4   => "Solde initial (EC_Type=4) : TVA non gérée",
        111 => "FGR : valorisation hors cache (lecture SQL RT_HISTOCOMPTA)",
        0   => "Non valorisé : facture absente du cache de ventilation (OM non lue)",
        _   => $"Non valorisé : origine EC_Type={ecType} hors périmètre TVA"
    };
}

/// <summary>
/// Totaux HT/TVA d'une facture lus dans le cache de ventilation Sage (TASK-024), agrégés par EC_Id.
/// </summary>
public sealed class VentilationCacheTotal
{
    public int EcId { get; set; }
    public decimal TotalHt { get; set; }
    public decimal TotalTva { get; set; }
}

/// <summary>
/// TASK-076 : ligne sentinelle d'erreur (CodeTaxe='ERREUR') du cache de ventilation TASK-024,
/// portant le motif ET les montants Sage bruts (tels que lus AVANT exclusion, TASK-072/076).
/// Lue par <c>DeclarationRepository.EnrichirFamilleBDepuisCacheAsync</c> pour l'écran Factures.
/// </summary>
public sealed class VentilationCacheErreur
{
    public int EcId { get; set; }
    public string MotifErreur { get; set; } = "";
    public decimal? BrutHT { get; set; }
    public decimal? BrutTva { get; set; }
    public decimal? BrutParafiscale { get; set; }
    public decimal? BrutTtc { get; set; }
}

/// <summary>
/// Valeurs distinctes présentes sur la période, pour alimenter les filtres de la liste factures.
/// Lecture seule (TASK-041).
/// </summary>
public sealed class FactureInterrogationDistincts
{
    public List<int> Origines { get; set; } = new();

    // ─── TASK-067B — colonnes identifiantes passées en filterType 'list' ───────────
    // Valeurs distinctes sur la MÊME période/WHERE que la liste principale (invariant
    // TASK-040), bornées (cf. DeclarationRepository.DistinctsTopBound). Fournisseur reste
    // en LIKE (texte libre, cardinalité non bornée — décision documentée en VERIFY).
    public List<string> Numeros { get; set; } = new();
    public List<string> References { get; set; } = new();
}
