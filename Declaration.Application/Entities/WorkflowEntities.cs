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
    public int SocieteId { get; set; }
    public int Exercice { get; set; }
    public TypePeriode Type { get; set; }
    public int Periode { get; set; } // Mois (1-12) ou Trimestre (1-4)
    public StatutDeclaration Statut { get; set; } = StatutDeclaration.EnCours;
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public DateTime? DateCloture { get; set; }

    /// <summary>
    /// TASK-094 (Option B) — copie du tampon <c>DT_Id</c> posé sur <c>RT_AFFECTATION</c> à la
    /// clôture (même valeur, posée dans le même appel). Permet un `JOIN` direct sans recalculer
    /// <c>DeriveDtId</c> — élimine le risque de divergence entre runtimes .NET constaté sur
    /// l'incident du 14/07/2026 (cf. DONE_DETAIL/TASK-094). Colonne additive sur DM_ENTTVA,
    /// jamais rétroactive : NULL pour toute déclaration close avant cette migration.
    /// </summary>
    /// <remarks>
    /// Nommé avec underscore (et non <c>DtId</c>) pour matcher exactement la colonne SQL
    /// <c>DT_Id</c> — Dapper ne normalise pas les underscores par défaut (même convention que
    /// <see cref="LigneCandidate.EC_Id"/>/<see cref="LigneCandidate.MV_Id"/> ci-dessus).
    /// </remarks>
    public int? DT_Id { get; set; }
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

    // Opérandes de proratisation (TASK-055) — exposition pure des valeurs déjà calculées par
    // Declaration.Core.Ventilateur (LigneDeclarationEnrichie.Prorata) et de l'affectation source
    // (AffectationADeclarer.MontantAffecte). Aucun recalcul : uniquement pour rendre l'opération
    // payé÷TTC=% / TVA×%=déclarée traçable à l'écran. 0 pour les lignes non valorisées (Exclue/
    // Reportee) — jamais interprété comme un vrai prorata, cf. MotifRejet/Etat sur ces lignes.
    public decimal Prorata { get; set; }
    public decimal MontantAffecte { get; set; }

    public string ModePaiement { get; set; } = "";
    public DateTime? DatePaiement { get; set; }
    public DateTime? DateFacture { get; set; }
    public string Source { get; set; } = "";

    // Origine de valorisation TVA snapshotée (discriminant EC_Type de l'échéance) :
    // 0 = Sage/OM, 111 = FGR, 4 = Solde initial. Défaut 0 pour compat des lignes déjà figées.
    public int EcType { get; set; }

    // TASK-077 — clé de revalidation ciblée d'une ligne déjà figée : EC_Id (RT_ECHEANCE) et
    // MV_Id (RT_MOUVEMENT) snapshotés au moment du figeage. 0 = inconnu (lignes figées avant
    // ce correctif, ou lignes sans règlement/échéance rattachée) — jamais un vrai identifiant
    // Sage, qui est toujours positif. Sert exclusivement à ChargerCandidatesSiNecessaireAsync/
    // RevaliderLignesFigeesAsync pour re-vérifier, à chaque lecture, qu'une ligne figée reste
    // cohérente avec l'état courant du garde-fou TASK-072 (cache) et du rapprochement
    // (RT_MOUVEMENT.MV_Point) — jamais utilisée pour recalculer ni modifier la ligne elle-même.
    public int EC_Id { get; set; }
    public int MV_Id { get; set; }

    // TASK-078 — traçabilité de la décision PO sur une incohérence signalée (TASK-077) : une
    // fois validée, l'alerte LIGNE_FIGEE_A_REVERIFIER n'est plus remontée pour cette ligne
    // (mais la ligne/le total ne sont JAMAIS modifiés — seule la décision est tracée). Remise à
    // false automatiquement par une resynchronisation (les faits ont changé, l'ancienne
    // validation ne s'applique plus).
    public bool IncoherenceValidee { get; set; }
    public string? IncoherenceValideePar { get; set; }
    public DateTime? IncoherenceValideeLe { get; set; }
}

/// <summary>
/// TASK-094 — résultat du diagnostic d'un tampon <c>DT_Id</c> observé sur
/// <c>RT_AFFECTATION</c> : soit la déclaration dont le recalcul de <c>DeriveDtId</c>
/// correspond, soit <see cref="Orphelin"/> = true si aucune déclaration existante ne
/// matche (déclaration disparue, donnée de test, ou tout autre écart — cf. incident
/// 14/07/2026, `TVA1-2026-01`).
/// </summary>
public class DiagnosticDtIdResultat
{
    public int DtId { get; set; }
    public bool Orphelin { get; set; }
    public Guid? DeclarationId { get; set; }
    public string? DeclarationNumero { get; set; }
    public StatutDeclaration? DeclarationStatut { get; set; }
}
