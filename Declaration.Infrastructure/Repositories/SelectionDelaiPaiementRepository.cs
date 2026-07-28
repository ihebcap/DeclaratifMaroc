using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Declaration.Application.Interfaces;
using Declaration.Core;

namespace Declaration.Infrastructure.Repositories;

/// <summary>
/// TASK-131 : requête de sélection des lignes hors délai de la Déclaration Délai de Paiement Maroc.
///
/// LECTURE SEULE STRICTE — uniquement des <c>SELECT</c> sur des tables EXISTANTES possédées GRF :
/// <c>RT_ECHEANCE</c>, <c>RT_AFFECTATION</c>, <c>RT_MOUVEMENT</c>,
/// <c>RT_DECLARATIONDELAISPAIEMENT</c>/<c>RT_DECLARATIONDELAISPAIEMENTLG</c>,
/// <c>P_SOCIETE</c>/<c>P_SOCIETEDEVISE</c>. AUCUN INSERT/UPDATE/DELETE, AUCUNE table créée, AUCUNE
/// modification de schéma (contrainte non négociable : ne jamais toucher au schéma d'une table
/// possédée par apbs-gr_winform).
///
/// Mappings vérifiés en code legacy (à ne pas confondre) :
/// <list type="bullet">
/// <item><c>RT_ECHEANCE.DO_Domaine</c> : enum <c>ErpDomaine</c> INVERSÉ — Vente=0, <b>Achat=1</b>
/// (même constat que <c>ConventionDelaiPaiementRepository.ToDoDomaineErp</c>, TASK-129). La DDP ne
/// porte que le domaine Achat (factures fournisseur, legacy <c>EcheanceFournisseurHelper</c>).</item>
/// <item><c>RT_ECHEANCE.EC_Type</c> : enum <c>EcheanceType</c> — Gain=90/Perte=91 EXCLUS, comme le
/// legacy <c>EcheanceRepository.GetAll</c> (l.215-216).</item>
/// <item><c>RT_ECHEANCE.DE_Id</c> = <c>P_DEVISE.DV_Id</c> ; devise société résolue
/// <c>P_SOCIETE.SO_DeviseErpNo</c> → <c>P_SOCIETEDEVISE.SD_No</c> → <c>DV_Id</c> (legacy
/// <c>Societe.GetDefaultDeviseSociete()</c> → <c>GetDeviseErp(DeviseErpNo)</c>).</item>
/// <item><c>RT_MOUVEMENT.MV_Type</c> = enum <c>ReglementType</c> (0 Espèce, 1 Chèque, 2 Traite,
/// 3 Virement, 4 Autre) ; <c>MV_Point</c>/<c>MV_PointDate</c> = rapprochement bancaire LOCAL GRF,
/// même mécanisme que le module TVA (décision PO 19/07/2026) ; <c>MV_Compta</c> = enum
/// <c>EtatComptabilite</c> (0 = non comptabilisé).</item>
/// </list>
/// </summary>
public sealed class SelectionDelaiPaiementRepository : ISelectionDelaiPaiementRepository
{
    /// <summary><c>RT_ECHEANCE.DO_Domaine</c> pour le domaine Achat (enum legacy <c>ErpDomaine</c> inversé).</summary>
    private const int DoDomaineAchat = 1;

    /// <summary><c>RT_ECHEANCE.EC_Type</c> = Gain (enum legacy <c>EcheanceType</c>), exclu de la sélection.</summary>
    private const int EcheanceTypeGain = 90;

    /// <summary><c>RT_ECHEANCE.EC_Type</c> = Perte, exclu de la sélection.</summary>
    private const int EcheanceTypePerte = 91;

    /// <summary>
    /// Sentinel SQL Server <c>datetime</c> minimum utilisé par le legacy pour « pas de date »
    /// (constaté sur <c>MV_PointDate</c>, cf. <c>DeclarationRepository</c> §TASK-135).
    /// </summary>
    private const string SentinelDateMin = "17530101";

    /// <summary>Limite de sécurité SQL Server (2 100 paramètres) — même garde que <c>DeclarationRepository.TamponnerAffectationsAsync</c>.</summary>
    private const int TailleLot = 1_000;

    private readonly IDbConnectionFactory _connectionFactory;

    public SelectionDelaiPaiementRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<int> GetDeviseSocieteIdAsync(int soId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var deviseId = await connection.QuerySingleOrDefaultAsync<int?>(@"
            SELECT SD.DV_Id
            FROM P_SOCIETE S
            JOIN P_SOCIETEDEVISE SD ON SD.SO_Id = S.SO_Id AND SD.SD_No = S.SO_DeviseErpNo
            WHERE S.SO_Id = @SoId",
            new { SoId = soId });

        // Échec EXPLICITE : sans devise société résolue, le filtre legacy « devise société uniquement »
        // ne peut pas être appliqué — mieux vaut bloquer que sélectionner des devises étrangères.
        if (deviseId == null)
            throw new InvalidOperationException(
                $"Devise société introuvable pour SO_Id={soId} (P_SOCIETE.SO_DeviseErpNo / P_SOCIETEDEVISE). " +
                "La sélection Délai de Paiement exige la devise société pour appliquer le seuil légal de montant.");

        return deviseId.Value;
    }

    public async Task<IReadOnlyList<EcheanceDelaiPaiement>> GetEcheancesCandidatesAsync(
        int soId,
        int deviseSocieteId,
        DateTime dateDebutDeclarationLoi,
        DateTime dateLimiteSeuilMontant,
        decimal seuilMontant)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<EcheanceDelaiPaiement>(@"
            SELECT
                EC_Id        AS EcId,
                EC_No        AS EcNo,
                DO_Numero    AS DoNumero,
                DO_Date      AS DoDate,
                DO_Reference AS DoReference,
                EC_Etat      AS Etat,
                EC_Montant   AS Montant,
                EC_Solde     AS Solde,
                EC_MtDevise  AS MontantDevise,
                EC_SoldeDevise AS SoldeDevise,
                EC_Echeance  AS EcheanceContractuelle,
                CT_No        AS TiersNo,
                CT_Code      AS TiersCode,
                CT_Intitule  AS TiersIntitule
            FROM RT_ECHEANCE
            WHERE SO_Id = @SoId
              AND DO_Domaine = @DoDomaine
              AND EC_Type NOT IN (@TypeGain, @TypePerte)
              AND DE_Id = @DeviseSocieteId
              AND DO_Date >= @DateDebutLoi
              AND (DO_Date > @DateLimiteMontant OR EC_Montant >= @SeuilMontant)",
            new
            {
                SoId = soId,
                DoDomaine = DoDomaineAchat,
                TypeGain = EcheanceTypeGain,
                TypePerte = EcheanceTypePerte,
                DeviseSocieteId = deviseSocieteId,
                DateDebutLoi = dateDebutDeclarationLoi.Date,
                DateLimiteMontant = dateLimiteSeuilMontant.Date,
                SeuilMontant = seuilMontant
            });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<AffectationDelaiPaiement>> GetAffectationsAsync(int soId, IEnumerable<int> ecIds)
    {
        var ids = ecIds.Distinct().ToList();
        var resultat = new List<AffectationDelaiPaiement>();
        if (ids.Count == 0) return resultat;

        using var connection = _connectionFactory.CreateGrfConnection();

        for (int i = 0; i < ids.Count; i += TailleLot)
        {
            var lot = ids.Skip(i).Take(TailleLot).ToList();
            var rows = await connection.QueryAsync<AffectationDelaiPaiement>($@"
                SELECT
                    AF.AF_Id       AS AfId,
                    AF.EC_Id       AS EcId,
                    AF.AF_Date     AS AfDate,
                    AF.AF_Montant  AS Montant,
                    AF.AF_MtDevise AS MontantDevise,
                    M.MV_Id        AS MvId,
                    M.MV_Type      AS TypeReglement,
                    M.MV_Date      AS DateReglement,
                    CAST(CASE WHEN M.MV_Point <> 0 THEN 1 ELSE 0 END AS bit) AS EstRapproche,
                    CASE WHEN M.MV_Point <> 0 THEN NULLIF(M.MV_PointDate, '{SentinelDateMin}') END AS DateRapprochement,
                    CAST(CASE WHEN M.MV_Compta <> 0 THEN 1 ELSE 0 END AS bit) AS EstComptabilise,
                    M.MV_Numero    AS ReglementNumero,
                    M.MV_Piece     AS ReglementPiece
                FROM RT_AFFECTATION AF
                JOIN RT_MOUVEMENT M ON M.MV_Id = AF.MV_Id
                WHERE M.SO_Id = @SoId AND AF.EC_Id IN @EcIds",
                new { SoId = soId, EcIds = lot });

            resultat.AddRange(rows);
        }

        return resultat;
    }

    public async Task<IReadOnlyDictionary<int, DateTime>> GetDernieresBornesDeclareesAsync(int soId, IEnumerable<int> ecIds)
    {
        var ids = ecIds.Distinct().ToList();
        var resultat = new Dictionary<int, DateTime>();
        if (ids.Count == 0) return resultat;

        using var connection = _connectionFactory.CreateGrfConnection();

        for (int i = 0; i < ids.Count; i += TailleLot)
        {
            var lot = ids.Skip(i).Take(TailleLot).ToList();
            var rows = await connection.QueryAsync<BorneDeclareeRow>(@"
                SELECT LG.EC_Id AS EcId, MAX(D.DDP_DateFin) AS DerniereBorne
                FROM RT_DECLARATIONDELAISPAIEMENTLG LG
                JOIN RT_DECLARATIONDELAISPAIEMENT D ON D.DDP_Id = LG.DDP_Id
                WHERE D.SO_Id = @SoId AND LG.EC_Id IN @EcIds
                GROUP BY LG.EC_Id",
                new { SoId = soId, EcIds = lot });

            foreach (var row in rows) resultat[row.EcId] = row.DerniereBorne;
        }

        return resultat;
    }

    private sealed class BorneDeclareeRow
    {
        public int EcId { get; set; }
        public DateTime DerniereBorne { get; set; }
    }
}
