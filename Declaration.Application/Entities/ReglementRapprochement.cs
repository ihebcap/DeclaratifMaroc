using System;
using System.Collections.Generic;
using System.Linq;

namespace Declaration.Application.Entities;

/// <summary>
/// Ligne brute d'interrogation « Rapprochement bancaire » (TASK-036).
///
/// Pivot = le RÈGLEMENT (RT_MOUVEMENT.MV_Id). Une instance = un règlement, avec ses
/// affectations aux factures AGRÉGÉES (nombre + montant affecté) et l'écart explicite
/// « reste à affecter ». Projection issue d'un SELECT strictement en lecture seule ;
/// aucun effet de bord, aucun recalcul de TVA.
///
/// Les libellés métier (origine EC_Type, mode) et les indicateurs dérivés
/// (rapproché banque, déclaré, reste à affecter) sont calculés ici, source unique,
/// afin d'être testables hors de la couche API.
/// </summary>
public sealed class ReglementRapprochementRow
{
    // ─── Champs bruts SELECT (aliasés dans la requête) ─────────────────────────
    public string MvNumero { get; set; } = "";
    public int MvType { get; set; }        // RT_MOUVEMENT.MV_Type = mode de règlement
    public int MvDomaine { get; set; }     // RT_MOUVEMENT.MV_Domaine : 0=encaissement,1=décaissement,6=frais bancaire
    public int MvPoint { get; set; }       // RT_MOUVEMENT.MV_Point : 1 = rapproché banque
    public DateTime MvDate { get; set; }   // RT_MOUVEMENT.MV_Date
    public DateTime? MvPointDate { get; set; } // RT_MOUVEMENT.MV_PointDate = date de pointage sur extrait (nul si non rapproché)
    public string? MvExtraitNum { get; set; }  // RT_MOUVEMENT.MV_ExtraitNum = n° de l'extrait bancaire de pointage (nul/vide si non pointé)
    public DateTime? MvEcheance { get; set; }  // RT_MOUVEMENT.MV_Echeance = date d'échéance de la pièce
    public decimal MvMontant { get; set; } // RT_MOUVEMENT.MV_Montant = montant du règlement
    public string? Tiers { get; set; }     // RT_MOUVEMENT.CT_Intitule
    public string? TiersCode { get; set; } // RT_MOUVEMENT.CT_Code
    public string? BanqueCode { get; set; } // dbo.vBanque.BanqueCode (via BN_Id/SO_Id) — non porté par RT_MOUVEMENT

    public int NbAffectations { get; set; }        // COUNT(RT_AFFECTATION)
    public decimal? MontantAffecte { get; set; }   // SUM(RT_AFFECTATION.AF_Montant) — NULL si aucune affectation
    public int? EcTypeMin { get; set; }            // MIN(RT_ECHEANCE.EC_Type) des affectations
    public int? EcTypeMax { get; set; }            // MAX(RT_ECHEANCE.EC_Type) des affectations
    public int NbDeclare { get; set; }             // nb d'affectations avec DT_Id NOT NULL (TASK-028)

    // ─── Indicateurs dérivés (rendus VISIBLES, jamais absorbés) ────────────────

    /// <summary>Montant réellement affecté aux factures (0 si aucune affectation).</summary>
    public decimal MontantAffecteEffectif => MontantAffecte ?? 0m;

    /// <summary>
    /// Reste à affecter = montant du règlement − Σ affectations.
    /// Pilier confiance : l'écart n'est jamais masqué, il est rendu explicite.
    /// </summary>
    public decimal ResteAAffecter => MvMontant - MontantAffecteEffectif;

    /// <summary>
    /// Rapproché banque (source locale MV_Point, option B) OU règlement espèce (MV_Type = 0).
    /// L'espèce n'a par nature aucun extrait (MV_Point = 0, MV_PointDate nul) : l'encaissement/
    /// décaissement est soldé immédiatement, il est donc considéré auto-rapproché (TASK-042).
    /// SOURCE UNIQUE : le SQL du filtre @rapproche réplique EXACTEMENT cette expression.
    /// </summary>
    public bool EstRapprocheBanque => MvPoint == 1 || MvType == 0;

    /// <summary>
    /// Date de rapprochement affichée : pointage réel (MV_PointDate) pour tout mode bancaire ;
    /// pour l'espèce (MV_Type = 0), faute de MV_PointDate, on affiche MV_Date (date du soldé).
    /// Jamais de valeur fictive : nul pour un règlement bancaire non encore rapproché → « — ».
    /// </summary>
    public DateTime? DateRapprochement => MvType == 0 ? MvDate : MvPointDate;

    /// <summary>
    /// Date de RÉFÉRENCE DE PÉRIODE (TASK-062) : dans quelle période tombe ce règlement.
    /// SOURCE UNIQUE <see cref="Declaration.Selection.RegleDatePeriode.DateReferencePour"/>,
    /// répliquée à l'identique par le SQL des 3 chemins (interrogation, sélection, déclaration).
    ///   espèce &amp; non-rapproché → MV_Date ; non-espèce rapproché → MV_PointDate.
    /// À DISTINGUER de <see cref="DateRapprochement"/> (affichage : nul si non rapproché) :
    /// un non-espèce NON rapproché a bien une DateReference (MV_Date) pour rester visible dans
    /// l'interrogation, tout en n'étant pas déclarable (gate EstDeclarable).
    /// </summary>
    public DateTime? DateReference =>
        Declaration.Selection.RegleDatePeriode.DateReferencePour(MvType, MvPoint, MvPointDate, MvDate);

    /// <summary>Déclaré : au moins une affectation porte le tampon DT_Id (lecture seule).</summary>
    public bool EstDeclare => NbDeclare > 0;

    /// <summary>Libellé métier de l'origine (EC_Type des affectations).</summary>
    public string Origine => LibelleOrigine(EcTypeMin, EcTypeMax);

    /// <summary>Libellé métier du mode de règlement (MV_Type).</summary>
    public string Mode => LibelleMode(MvType);

    /// <summary>Libellé métier du domaine du mouvement (MV_Domaine).</summary>
    public string Domaine => LibelleDomaine(MvDomaine);

    // ─── Mappings métier (source unique, réutilisée par l'API et les distincts) ─

    /// <summary>
    /// EC_Type → origine (cf. mémoire grf-echeance-ectype-mapping) :
    /// 0 = Sage (TVA via OM), 111 = FGR, 4 = SoldeInitial. Set mixte → « Mixte ».
    /// Aucune affectation → « SansAffectation » (le reste à affecter vaut alors tout le montant).
    /// </summary>
    public static string LibelleOrigine(int? ecTypeMin, int? ecTypeMax)
    {
        if (ecTypeMin is null || ecTypeMax is null) return "SansAffectation";
        if (ecTypeMin != ecTypeMax) return "Mixte";
        return LibelleEcType(ecTypeMin.Value);
    }

    /// <summary>
    /// EC_Type → libellé d'origine de valorisation TVA (source unique, réutilisée par
    /// l'écran Rapprochement ET l'écran Déclaration — cf. TASK-038, pas de duplication).
    /// </summary>
    public static string LibelleEcType(int ecType) => ecType switch
    {
        0   => "Sage",
        111 => "FGR",
        4   => "SoldeInitial",
        _   => $"Autre ({ecType})"
    };

    /// <summary>MV_Type → mode de règlement (0=Espèce,1=Chèque,2=Traite,3=Virement, autre=Autres).</summary>
    public static string LibelleMode(int mvType) => mvType switch
    {
        0 => "Espèce",
        1 => "Chèque",
        2 => "Traite",
        3 => "Virement",
        _ => "Autres"
    };

    /// <summary>
    /// Reverse de <see cref="LibelleEcType"/> : libellé d'origine → EC_Type(s) correspondants
    /// (TASK-067B, filtre « origine » de <c>DomainGrid</c>). Source unique de la dérivation
    /// (aucune duplication de la règle métier côté SQL/Infrastructure) : un seul EC_Type par
    /// libellé connu, sauf « Autre (n) » qui encode directement le code dans le libellé.
    /// Libellé inconnu ⇒ liste vide (aucune contrainte ajoutée plutôt qu'une valeur inventée).
    /// </summary>
    public static IReadOnlyList<int> EcTypesDepuisLibelle(string? libelle)
    {
        if (string.IsNullOrWhiteSpace(libelle)) return Array.Empty<int>();
        switch (libelle)
        {
            case "Sage": return new[] { 0 };
            case "FGR": return new[] { 111 };
            case "SoldeInitial": return new[] { 4 };
            default:
                var m = System.Text.RegularExpressions.Regex.Match(libelle, @"^Autre \((-?\d+)\)$");
                return m.Success && int.TryParse(m.Groups[1].Value, out var code)
                    ? new[] { code }
                    : Array.Empty<int>();
        }
    }

    /// <summary>
    /// MV_Domaine → domaine du mouvement (0=Encaissement, 1=Décaissement, 6=Frais bancaire).
    /// Codes inconnus rendus explicites « Autre (n) » — jamais masqués (principe transparence).
    /// </summary>
    public static string LibelleDomaine(int mvDomaine) => mvDomaine switch
    {
        0 => "Encaissement",
        1 => "Décaissement",
        6 => "Frais bancaire",
        _ => $"Autre ({mvDomaine})"
    };
}

/// <summary>
/// Valeurs distinctes présentes sur la période, pour alimenter les filtres de la liste
/// (TASK-036, aligné front TASK-037). Lecture seule.
/// </summary>
public sealed class ReglementRapprochementDistincts
{
    public List<int> Modes { get; set; } = new();
    public List<int> Origines { get; set; } = new();

    // ─── TASK-067B — colonnes à forte cardinalité passées en filterType 'list' ──────
    // Valeurs distinctes réellement présentes sur la MÊME période/WHERE que la liste
    // principale (invariant TASK-040), bornées (cf. DeclarationRepository.DistinctsTopBound)
    // pour ne jamais renvoyer un volume disproportionné. Contrat { colonne: [valeurs] }.
    public List<string> NumerosReglement { get; set; } = new();
    public List<string> NumerosExtrait { get; set; } = new();
    public List<string> Banques { get; set; } = new();
}

/// <summary>
/// Filtres optionnels de l'interrogation « Rapprochement bancaire » (TASK-063).
///
/// Regroupe TOUS les filtres par colonne pour éviter une signature de méthode à
/// ~25 paramètres positionnels (source d'erreurs). Chaque filtre est appliqué CÔTÉ
/// SERVEUR, à l'identique dans la liste ET le COUNT (invariant TASK-040 : TotalCount,
/// pagination et grille coïncident sous n'importe quelle combinaison).
///
/// Conventions de type (parité de filtre par type de donnée) :
///   - énumérations (Modes) et booléens multi (RapprocheBanque, Declare, Point) → listes :
///     liste vide = pas de contrainte ; multi-sélection RÉELLE (plus de valeur ignorée) ;
///   - montants / compteurs → plages Min/Max NULL-safe (borne vide = pas de contrainte) ;
///   - dates → plages Min/Max NULL-safe (bornes incluses côté service) ;
///   - textes → LIKE (chaîne vide/nulle = pas de contrainte).
///
/// LECTURE SEULE : aucun de ces filtres n'écrit en base (DT_Id intouché — TASK-028).
/// </summary>
public sealed class RapprochementFilter
{
    // Énumération + booléens en multi-sélection réelle (int : MV_Type ; bool → 0/1 côté SQL).
    public IReadOnlyList<int>? Modes { get; init; }
    public IReadOnlyList<bool>? RapprocheBanque { get; init; }
    public IReadOnlyList<bool>? Declare { get; init; }
    public IReadOnlyList<bool>? Point { get; init; }

    // Multi-sélection libellés (comparés à la MÊME expression de dérivation que la projection).
    public IReadOnlyList<string>? Origines { get; init; }
    public IReadOnlyList<string>? Domaines { get; init; }

    // Texte libre à cardinalité non bornée (nom de tiers) → reste en LIKE (TASK-067B : décision
    // documentée en VERIFY, pas de repli LIKE générique, seulement pour ce champ précis).
    public string? Tiers { get; init; }

    // TASK-067B — colonnes identifiantes (n° pièce, n° extrait, code banque) passées en
    // MULTI-SÉLECTION RÉELLE (liste de valeurs distinctes, cf. GetReglementsRapprochementDistinctsAsync),
    // à la place de l'ancien LIKE scalaire. Liste vide/nulle ⇒ pas de contrainte.
    public IReadOnlyList<string>? Numeros { get; init; }
    public IReadOnlyList<string>? NumerosExtrait { get; init; }
    public IReadOnlyList<string>? Banques { get; init; }

    // Plages montants / compteur (comparées à l'expression de projection, pas à une colonne absente).
    public decimal? MontantMin { get; init; }
    public decimal? MontantMax { get; init; }
    public decimal? ResteMin { get; init; }
    public decimal? ResteMax { get; init; }
    public int? NbFacturesMin { get; init; }
    public int? NbFacturesMax { get; init; }

    // Plages dates (rapprochement, échéance).
    public DateTime? DateRappMin { get; init; }
    public DateTime? DateRappMax { get; init; }
    public DateTime? EcheanceMin { get; init; }
    public DateTime? EcheanceMax { get; init; }

    /// <summary>
    /// Normalise un filtre booléen multi-sélection en drapeaux SQL 0/1 distincts (source unique,
    /// TASK-063). Fin du « filtre menteur » : cocher Oui ET Non ⇒ { 1, 0 } ⇒ SQL IN (0,1) ⇒ tout
    /// (union réelle, plus de 1re valeur silencieusement retenue). Liste vide/nulle ⇒ pas de
    /// contrainte (le SQL court-circuite via le garde @hasX = 0).
    /// </summary>
    public static IReadOnlyList<int> ToFlags(IReadOnlyList<bool>? values)
        => (values ?? Array.Empty<bool>()).Select(b => b ? 1 : 0).Distinct().ToList();
}
