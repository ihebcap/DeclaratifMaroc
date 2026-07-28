using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Core;
using Declaration.Selection;
using Microsoft.Data.SqlClient;

namespace Declaration.Infrastructure.Repositories;

/// <summary>
/// TASK-132 (Délai de Paiement Maroc — cycle de vie de la déclaration) : repository Dapper sur les
/// tables EXISTANTES <c>RT_DECLARATIONDELAISPAIEMENT</c> / <c>RT_DECLARATIONDELAISPAIEMENTLG</c>
/// (propriété GRF — aucune migration, aucune modification de schéma, aucun index ajouté).
///
/// Mapping colonne ↔ propriété repris à l'identique du legacy
/// (<c>Tresorerie.Dapper/Repositories/DeclarationDelaisPaiementRepository.Script.cs:7-118</c> et
/// <c>LigneDeclarationDelaisPaiementRepository.Script.cs:7-81</c>), vérifié en base réelle
/// (<c>INFORMATION_SCHEMA.COLUMNS</c>, cf. VERIFY TASK-132).
///
/// Conventions respectées :
/// <list type="bullet">
/// <item><c>DDP_DateFin</c> stocké à <c>23:59:59</c> du dernier jour (legacy l.674) — interopérabilité
/// avec les déclarations produites par l'ancien applicatif ; toutes les comparaisons de bornes se font
/// donc au JOUR (<c>CAST(... AS date)</c>).</item>
/// <item>UPDATE restreint aux 6 colonnes mutables du legacy — aucune autre n'est atteignable ici.</item>
/// <item>Écritures multi-lignes en TRANSACTION explicite (ARCHITECTURE §5).</item>
/// <item><c>F_COMPTET</c> (IF/ICE fournisseur) lu via la connexion SAGE résolue par <c>SO_Id</c>
/// (TASK-118) et les noms de colonnes configurés par société (TASK-048), jointure APPLICATIVE en
/// mémoire — jamais de JOIN cross-base depuis la connexion GRF (leçon TASK-154).</item>
/// </list>
/// </summary>
public sealed class DeclarationDelaiPaiementRepository : IDeclarationDelaiPaiementRepository
{
    /// <summary>Limite de sécurité SQL Server (2 100 paramètres) — même garde que <c>SelectionDelaiPaiementRepository</c>.</summary>
    private const int TailleLot = 500;

    /// <summary>Sentinel SQL Server <c>datetime</c> minimum utilisé par le legacy pour « pas de date » (cf. <c>MV_PointDate</c>).</summary>
    private const string SentinelDateMin = "17530101";

    private readonly IDbConnectionFactory _connectionFactory;

    public DeclarationDelaiPaiementRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    private const string SelectEntete = @"
        DDP_Id              AS DdpId,
        DDP_Numero          AS Numero,
        SO_Id               AS SocieteId,
        DDP_Date            AS Date,
        DDP_Exercice        AS Exercice,
        DDP_Type            AS Type,
        DDP_DateDebut       AS DateDebut,
        DDP_DateFin         AS DateFin,
        DDP_Statut          AS Statut,
        DDP_DateCreation    AS DateCreation,
        UT_Id               AS CreateurId,
        DDP_DateModif       AS DateModification,
        UT_IdModif          AS ModificateurId,
        DDP_IsDepose        AS EstDeposee,
        DDP_Libelle         AS Libelle,
        DDP_IsGeneretedFile AS FichierGenere,
        DDP_Periode         AS Periode";

    // ─── Entête ────────────────────────────────────────────────────────────────────────────────────

    public async Task<int> CreerEnteteAsync(DeclarationDelaiPaiement entete)
    {
        if (entete == null) throw new ArgumentNullException(nameof(entete));

        using var connection = _connectionFactory.CreateGrfConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var ddpId = await connection.QuerySingleAsync<int>(@"
            INSERT INTO RT_DECLARATIONDELAISPAIEMENT
                (DDP_Numero, SO_Id, DDP_Date, DDP_Exercice, DDP_Type, DDP_DateDebut, DDP_DateFin,
                 DDP_Statut, DDP_DateCreation, UT_Id, DDP_DateModif, UT_IdModif, DDP_IsDepose,
                 DDP_Libelle, DDP_IsGeneretedFile, DDP_Periode)
            VALUES
                (@Numero, @SocieteId, @Date, @Exercice, @Type, @DateDebut, @DateFin,
                 @Statut, @DateCreation, @CreateurId, @DateModification, @ModificateurId, @EstDeposee,
                 @Libelle, @FichierGenere, @Periode);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            new
            {
                entete.Numero,
                entete.SocieteId,
                entete.Date,
                entete.Exercice,
                Type = (int)entete.Type,
                entete.DateDebut,
                entete.DateFin,
                Statut = (int)entete.Statut,
                entete.DateCreation,
                entete.CreateurId,
                entete.DateModification,
                entete.ModificateurId,
                entete.EstDeposee,
                entete.Libelle,
                entete.FichierGenere,
                entete.Periode
            },
            transaction);

        transaction.Commit();
        return ddpId;
    }

    public async Task<DeclarationDelaiPaiement?> GetEnteteAsync(int ddpId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.QuerySingleOrDefaultAsync<DeclarationDelaiPaiement>(
            $"SELECT {SelectEntete} FROM RT_DECLARATIONDELAISPAIEMENT WHERE DDP_Id = @DdpId",
            new { DdpId = ddpId });
    }

    public async Task<IReadOnlyList<DeclarationDelaiPaiementListItem>> GetAllAsync(int soId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<EnteteAvecCompteRow>(
            $@"SELECT {SelectEntete},
                      (SELECT COUNT(1) FROM RT_DECLARATIONDELAISPAIEMENTLG LG WHERE LG.DDP_Id = D.DDP_Id) AS NombreLignes
               FROM RT_DECLARATIONDELAISPAIEMENT D
               WHERE D.SO_Id = @SoId
               ORDER BY D.DDP_Exercice DESC, D.DDP_DateDebut DESC, D.DDP_Id DESC",
            new { SoId = soId });

        return rows.Select(r => new DeclarationDelaiPaiementListItem
        {
            Entete = new DeclarationDelaiPaiement
            {
                DdpId = r.DdpId,
                Numero = r.Numero,
                SocieteId = r.SocieteId,
                Date = r.Date,
                Exercice = r.Exercice,
                Type = (TypeDeclarationDelaiPaiement)r.Type,
                DateDebut = r.DateDebut,
                DateFin = r.DateFin,
                Statut = (StatutDeclarationDelaiPaiement)r.Statut,
                DateCreation = r.DateCreation,
                CreateurId = r.CreateurId,
                DateModification = r.DateModification,
                ModificateurId = r.ModificateurId,
                EstDeposee = r.EstDeposee,
                Libelle = r.Libelle,
                FichierGenere = r.FichierGenere,
                Periode = r.Periode
            },
            NombreLignes = r.NombreLignes
        }).ToList();
    }

    public async Task<IReadOnlyList<DeclarationDelaiPaiement>> GetAllParExerciceAsync(int soId, int exercice)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<DeclarationDelaiPaiement>(
            $@"SELECT {SelectEntete} FROM RT_DECLARATIONDELAISPAIEMENT
               WHERE SO_Id = @SoId AND DDP_Exercice = @Exercice",
            new { SoId = soId, Exercice = exercice });
        return rows.ToList();
    }

    public async Task<bool> ExisteNumeroAsync(int soId, string numero)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var count = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(1) FROM RT_DECLARATIONDELAISPAIEMENT WHERE SO_Id = @SoId AND DDP_Numero = @Numero",
            new { SoId = soId, Numero = numero });
        return count > 0;
    }

    /// <summary>
    /// Le motif de numérotation est un <c>LIKE</c> construit à partir de <c>P_SOCIETE</c> (jamais
    /// d'une saisie utilisateur) et reste passé en PARAMÈTRE Dapper — aucune interpolation SQL.
    /// </summary>
    public async Task<string?> GetDernierNumeroAsync(int soId, string patternLike)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.QuerySingleOrDefaultAsync<string?>(
            @"SELECT MAX(DDP_Numero) FROM RT_DECLARATIONDELAISPAIEMENT
              WHERE SO_Id = @SoId AND DDP_Numero LIKE @Pattern",
            new { SoId = soId, Pattern = patternLike });
    }

    public async Task<ConfigurationNumerotationDelaiPaiement> GetConfigurationNumerotationAsync(int soId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ConfigurationNumerotationRow>(
            @"SELECT SO_DecDPPrefix   AS Prefixe,
                     SO_DecDPNumAnnee AS InclureAnnee,
                     SO_DecDPNumMois  AS InclureMois,
                     SO_DecDPNumCount AS NombreChiffres
              FROM P_SOCIETE WHERE SO_Id = @SoId",
            new { SoId = soId });

        // Échec EXPLICITE, jamais un paramétrage de repli qui produirait des numéros arbitraires.
        if (row == null)
            throw new InvalidOperationException(
                $"Société SO_Id={soId} introuvable dans P_SOCIETE : impossible de résoudre la numérotation " +
                "des déclarations Délai de Paiement.");

        return new ConfigurationNumerotationDelaiPaiement
        {
            Prefixe = row.Prefixe,
            InclureAnnee = row.InclureAnnee,
            InclureMois = row.InclureMois,
            NombreChiffres = row.NombreChiffres
        };
    }

    public async Task MettreAJourEtatAsync(
        int ddpId,
        StatutDeclarationDelaiPaiement statut,
        bool estDeposee,
        bool fichierGenere,
        string? libelle,
        int modificateurId,
        DateTime dateModification)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        // Exactement les 6 colonnes de l'UPDATE legacy (Script.cs:95-104) : le numéro, la date,
        // l'exercice, les bornes de période, le type et la période restent IMMUABLES.
        var lignes = await connection.ExecuteAsync(@"
            UPDATE RT_DECLARATIONDELAISPAIEMENT
               SET DDP_Statut          = @Statut,
                   UT_IdModif          = @ModificateurId,
                   DDP_DateModif       = @DateModification,
                   DDP_IsDepose        = @EstDeposee,
                   DDP_Libelle         = @Libelle,
                   DDP_IsGeneretedFile = @FichierGenere
             WHERE DDP_Id = @DdpId",
            new
            {
                DdpId = ddpId,
                Statut = (int)statut,
                ModificateurId = modificateurId,
                DateModification = dateModification,
                EstDeposee = estDeposee,
                Libelle = libelle,
                FichierGenere = fichierGenere
            },
            transaction);

        if (lignes != 1)
        {
            transaction.Rollback();
            throw new InvalidOperationException(
                $"Mise à jour de la déclaration DDP_Id={ddpId} impossible : {lignes} ligne(s) affectée(s) au lieu de 1.");
        }

        transaction.Commit();
    }

    public async Task SupprimerEnteteAsync(int ddpId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        // Garde de dernier recours EN BASE, dans la même transaction que le DELETE : refuse la
        // suppression si une ligne a été intégrée entretemps (course entre deux utilisateurs). Le
        // contrôle métier a déjà eu lieu côté service — ceci n'est PAS un doublon décoratif, c'est la
        // seule protection possible sans contrainte de schéma (interdite sur une table winform).
        var nombreLignes = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(1) FROM RT_DECLARATIONDELAISPAIEMENTLG WHERE DDP_Id = @DdpId",
            new { DdpId = ddpId }, transaction);

        if (nombreLignes > 0)
        {
            transaction.Rollback();
            throw new InvalidOperationException("La déclaration contient des lignes.");
        }

        await connection.ExecuteAsync(
            "DELETE FROM RT_DECLARATIONDELAISPAIEMENT WHERE DDP_Id = @DdpId",
            new { DdpId = ddpId }, transaction);

        transaction.Commit();
    }

    // ─── Lignes ────────────────────────────────────────────────────────────────────────────────────

    public async Task<int> CompterLignesAsync(int ddpId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.QuerySingleAsync<int>(
            "SELECT COUNT(1) FROM RT_DECLARATIONDELAISPAIEMENTLG WHERE DDP_Id = @DdpId",
            new { DdpId = ddpId });
    }

    public async Task<IReadOnlyList<CleLigneDelaiPaiement>> GetClesLignesAsync(int ddpId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<CleRow>(
            "SELECT EC_Id AS EcId, AF_Id AS AfId FROM RT_DECLARATIONDELAISPAIEMENTLG WHERE DDP_Id = @DdpId",
            new { DdpId = ddpId });

        return rows.Select(r => new CleLigneDelaiPaiement(r.EcId, r.AfId)).ToList();
    }

    public async Task<int> AjouterLignesAsync(int ddpId, IReadOnlyCollection<LigneAIntegrerDelaiPaiement> lignes)
    {
        if (lignes == null) throw new ArgumentNullException(nameof(lignes));
        if (lignes.Count == 0) return 0;

        using var connection = _connectionFactory.CreateGrfConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var total = 0;
        try
        {
            // Exactement les 5 colonnes écrites par le legacy. Une SEULE transaction pour tout le lot :
            // soit l'intégration passe entièrement, soit rien n'est écrit (le legacy pouvait laisser une
            // intégration partielle, IntergerLigne bouclant sans transaction englobante).
            foreach (var lot in Decouper(lignes, TailleLot))
            {
                total += await connection.ExecuteAsync(@"
                    INSERT INTO RT_DECLARATIONDELAISPAIEMENTLG
                        (DDP_Id, EC_Id, AF_Id, DDPL_Depassement, DDPL_EcheanceLegale)
                    VALUES (@DdpId, @EcId, @AfId, @Depassement, @EcheanceLegale)",
                    lot.Select(l => new
                    {
                        DdpId = ddpId,
                        l.EcId,
                        l.AfId,
                        Depassement = (decimal)l.Depassement,
                        EcheanceLegale = l.EcheanceLegale.Date
                    }).ToList(),
                    transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return total;
    }

    /// <summary>
    /// Jointures identiques au legacy (<c>LigneDeclarationDelaisPaiementRepository.Script.cs:7-48</c>)
    /// SAUF le tiers : le legacy lisait <c>CT_Payeur*</c>, on lit <c>CT_No</c>/<c>CT_Code</c>/
    /// <c>CT_Intitule</c> — les MÊMES colonnes que la sélection TASK-131, pour que le fournisseur
    /// contrôlé/exporté soit exactement celui qui a été sélectionné (divergence documentée en VERIFY ;
    /// identiques sur 1 408/1 408 échéances achat de la base réelle).
    /// </summary>
    public async Task<IReadOnlyList<LigneDeclarationDelaiPaiement>> GetLignesAsync(int ddpId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<LigneDeclarationDelaiPaiement>($@"
            SELECT
                LG.DDPL_Id             AS DdplId,
                LG.DDP_Id              AS DdpId,
                LG.EC_Id               AS EcId,
                LG.AF_Id               AS AfId,
                LG.DDPL_Depassement    AS Depassement,
                LG.DDPL_EcheanceLegale AS EcheanceLegale,
                E.CT_No                AS TiersNo,
                E.CT_Code              AS TiersCode,
                E.CT_Intitule          AS TiersIntitule,
                E.DO_Numero            AS DoNumero,
                E.DO_Date              AS DoDate,
                E.DO_Reference         AS DoReference,
                E.EC_Echeance          AS EcheanceContractuelle,
                E.EC_Montant           AS MontantEcheance,
                E.EC_Solde             AS SoldeEcheance,
                E.DE_Id                AS DeviseId,
                AF.AF_Montant          AS MontantAffecte,
                M.MV_Id                AS MvId,
                M.MV_Numero            AS ReglementNumero,
                M.MV_Piece             AS ReglementPiece,
                M.MV_Type              AS ReglementType,
                M.MV_Date              AS ReglementDate,
                CASE WHEN M.MV_Id IS NULL THEN NULL
                     ELSE CAST(CASE WHEN M.MV_Point <> 0 THEN 1 ELSE 0 END AS bit) END AS ReglementRapproche,
                CASE WHEN M.MV_Point <> 0 THEN NULLIF(M.MV_PointDate, '{SentinelDateMin}') END AS ReglementDateRapprochement,
                M.MR_Id                AS ReglementModeId
            FROM RT_DECLARATIONDELAISPAIEMENTLG LG
            INNER JOIN RT_ECHEANCE E ON E.EC_Id = LG.EC_Id
            LEFT JOIN RT_AFFECTATION AF ON AF.AF_Id = LG.AF_Id
            LEFT JOIN RT_MOUVEMENT M ON M.MV_Id = AF.MV_Id
            WHERE LG.DDP_Id = @DdpId
            ORDER BY E.CT_Code, E.DO_Date, LG.DDPL_Id",
            new { DdpId = ddpId });

        return rows.ToList();
    }

    public async Task<int> SupprimerLigneAsync(int ddpId, int ddplId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        // Scopé sur DDP_Id : impossible de supprimer par erreur une ligne d'une AUTRE déclaration.
        var lignes = await connection.ExecuteAsync(
            "DELETE FROM RT_DECLARATIONDELAISPAIEMENTLG WHERE DDPL_Id = @DdplId AND DDP_Id = @DdpId",
            new { DdplId = ddplId, DdpId = ddpId }, transaction);

        transaction.Commit();
        return lignes;
    }

    // ─── Référentiel tiers ERP (LECTURE SEULE, base Sage) ─────────────────────────────────────────

    /// <summary>
    /// TASK-154 : <c>F_COMPTET</c> est une table SAGE — jamais lue via la connexion GRF. La connexion
    /// est résolue par <c>SO_Id</c> (TASK-118) et les noms de colonnes IF/ICE proviennent de
    /// <c>P_SOCIETE</c> via <see cref="IdentiteFiscaleFournisseurConfig"/> (TASK-048), qui les valide
    /// par whitelist avant tout quotage — les codes tiers, eux, restent paramétrés.
    ///
    /// TASK-133 (SELECT additif, aucune duplication de logique — cf. VERIFY TASK-132 §10.9) : projette
    /// en plus <c>NumRc</c> (colonne configurable par société, <c>SO_ColValueNumRegistreCommerceFournisseur</c>,
    /// vide/non-configurée toléré — legacy émet une chaîne vide dans ce cas) et <c>Adresse</c>
    /// (<c>CT_Adresse</c>, colonne FIXE, jamais configurable). Les deux servent UNIQUEMENT au fichier
    /// XML de dépôt (<c>&lt;numRC&gt;</c>/<c>&lt;adresseSiegeSocial&gt;</c>) — n'entrent PAS dans le
    /// contrôle bloquant IF/ICE de TASK-132.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IdentiteFiscaleTiersErp>> GetIdentitesFiscalesTiersAsync(
        int soId, IReadOnlyCollection<string> tiersCodes)
    {
        var resultat = new Dictionary<string, IdentiteFiscaleTiersErp>(StringComparer.OrdinalIgnoreCase);

        var codes = (tiersCodes ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (codes.Count == 0) return resultat;

        var grfCs = _connectionFactory.GetGrfConnectionString();
        IdentiteFiscaleFournisseurConfig identiteConfig;
        string? colonneNumRc;
        using (var grfConnection = new SqlConnection(grfCs))
        {
            await grfConnection.OpenAsync();
            identiteConfig = await IdentiteFiscaleFournisseurConfig.ChargerAsync(grfConnection, soId);
            colonneNumRc = await grfConnection.QuerySingleOrDefaultAsync<string?>(
                "SELECT SO_ColValueNumRegistreCommerceFournisseur FROM P_SOCIETE WHERE SO_Id = @SoId",
                new { SoId = soId });
        }

        // Toléré vide/absent (pas de RC configuré pour cette société) : legacy émet '' dans ce cas,
        // jamais une erreur (le n° RC n'est pas soumis au contrôle bloquant IF/ICE).
        var expressionNumRc = ValiderNomColonneOptionnelle(colonneNumRc) is string nomValide
            ? $"F_COMPTET.[{nomValide}]"
            : "''";

        var sageInfo = await _connectionFactory.GetSageConnectionInfoAsync(soId);

        var sql = $@"
            SELECT CT_Num AS TiersCode,
                   {identiteConfig.SelectIdentifiantExpression("F_COMPTET")} AS IdentifiantFiscal,
                   {identiteConfig.SelectIceExpression("F_COMPTET")}         AS Ice,
                   {expressionNumRc}                                        AS NumRc,
                   CT_Adresse                                               AS Adresse
            FROM F_COMPTET
            WHERE CT_Num IN @Codes";

        using var sageConnection = new SqlConnection(sageInfo.ConnectionString);
        await sageConnection.OpenAsync();

        foreach (var lot in Decouper(codes, TailleLot))
        {
            var rows = await sageConnection.QueryAsync<IdentiteFiscaleTiersErp>(sql, new { Codes = lot });
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.TiersCode)) continue;
                resultat[row.TiersCode.Trim()] = row;
            }
        }

        return resultat;
    }

    /// <summary>
    /// Valide un nom de colonne SQL OPTIONNEL (whitelist <c>^[A-Za-z0-9_]+$</c>, tolère les crochets
    /// englobants) — même principe que <see cref="IdentiteFiscaleFournisseurConfig"/> (TASK-048), mais
    /// dupliqué localement à dessein : ce champ (n° RC) est TOLÉRANT à l'absence (retourne <c>null</c>),
    /// contrairement à l'IF/l'ICE qui sont OBLIGATOIRES et lèvent — les deux contrats sont donc
    /// délibérément distincts, pas une simple omission de réutilisation.
    /// </summary>
    private static string? ValiderNomColonneOptionnelle(string? valeurBrute)
    {
        var nom = (valeurBrute ?? string.Empty).Trim();
        if (nom.Length >= 2 && nom[0] == '[' && nom[^1] == ']')
            nom = nom.Substring(1, nom.Length - 2).Trim();

        if (nom.Length == 0) return null;

        return System.Text.RegularExpressions.Regex.IsMatch(nom, "^[A-Za-z0-9_]+$")
            ? nom
            : throw new InvalidOperationException(
                $"Nom de colonne n° registre de commerce invalide dans P_SOCIETE : « {valeurBrute} ». " +
                "Seuls les caractères A-Z, a-z, 0-9 et « _ » sont autorisés.");
    }

    /// <summary>
    /// TASK-191 : <c>RT_ECHEANCE.DO_Domaine</c> pour le domaine Achat (même enum legacy <c>ErpDomaine</c>
    /// inversé que <c>SelectionDelaiPaiementRepository.DoDomaineAchat</c> — la DDP ne porte que des
    /// factures fournisseur). Sert ici à restreindre la jointure <c>F_DOCENTETE</c> au même domaine,
    /// condition nécessaire à l'unicité de <c>DO_Piece</c> (vérifiée en base réelle
    /// <c>NEW_EMA DISTRIBUTION</c>, cf. VERIFY TASK-191 : 0 doublon <c>(DO_Piece, DO_Domaine)</c> sur le
    /// périmètre Achat).
    /// </summary>
    private const int DoDomaineAchat = 1;

    /// <summary>
    /// TASK-191 : lecture des valeurs réelles « nature marchandise »/« date livraison marchandise »
    /// (<c>F_DOCENTETE</c>, Sage, lecture seule), pilotée par les 2 colonnes configurables par société
    /// (<c>P_SOCIETE.SO_ColValueNatureMarchandise</c>/<c>SO_ColValueDateLivraisonMarchandise</c>), même
    /// contrat de tolérance que <see cref="GetIdentitesFiscalesTiersAsync"/> (whitelist
    /// <see cref="ValiderNomColonneOptionnelle"/>, jamais d'erreur liée à l'absence de configuration).
    ///
    /// Clé de jointure retenue (cf. VERIFY TASK-191) : <c>F_DOCENTETE.DO_Piece = RT_ECHEANCE.DO_Numero</c>
    /// (déjà projeté par <c>GetLignesAsync</c>/<c>SelectionDelaiPaiementRepository</c>, colonne
    /// <c>DoNumero</c>) restreinte à <c>DO_Domaine = @DoDomaineAchat</c> — même pattern que
    /// <c>SageTaxReaderService.ExtraireTaxes</c> (<c>WHERE DO_Piece = @piece</c>), vérifiée empiriquement
    /// unique sur ce périmètre (0 doublon <c>(DO_Piece, DO_Domaine=1)</c> en base réelle).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ValeursMarchandiseErp>> GetValeursMarchandiseAsync(
        int soId, IReadOnlyCollection<string> numerosFacture)
    {
        var resultat = new Dictionary<string, ValeursMarchandiseErp>(StringComparer.OrdinalIgnoreCase);

        var numeros = (numerosFacture ?? Array.Empty<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (numeros.Count == 0) return resultat;

        string? colonneNatureBrute;
        string? colonneDateLivraisonBrute;
        using (var grfConnection = new SqlConnection(_connectionFactory.GetGrfConnectionString()))
        {
            await grfConnection.OpenAsync();
            var row = await grfConnection.QuerySingleOrDefaultAsync<ConfigurationMarchandiseRow>(
                @"SELECT SO_ColValueNatureMarchandise         AS ColonneNature,
                         SO_ColValueDateLivraisonMarchandise  AS ColonneDateLivraison
                  FROM P_SOCIETE WHERE SO_Id = @SoId",
                new { SoId = soId });
            colonneNatureBrute = row?.ColonneNature;
            colonneDateLivraisonBrute = row?.ColonneDateLivraison;
        }

        var colonneNature = ValiderNomColonneOptionnelle(colonneNatureBrute);
        var colonneDateLivraison = ValiderNomColonneOptionnelle(colonneDateLivraisonBrute);

        // Toléré : AUCUNE des deux colonnes configurée pour cette société ⇒ comportement actuel
        // inchangé, sans même interroger Sage (non-régression stricte, TASK-191).
        if (colonneNature == null && colonneDateLivraison == null) return resultat;

        var expressionNature = colonneNature != null ? $"F_DOCENTETE.[{colonneNature}]" : "NULL";
        var expressionDateLivraison = colonneDateLivraison != null ? $"F_DOCENTETE.[{colonneDateLivraison}]" : "NULL";

        var sageInfo = await _connectionFactory.GetSageConnectionInfoAsync(soId);

        var sql = $@"
            SELECT DO_Piece                     AS NumeroFacture,
                   {expressionNature}            AS NatureMarchandise,
                   {expressionDateLivraison}     AS DateLivraisonMarchandise
            FROM F_DOCENTETE
            WHERE DO_Domaine = @DoDomaineAchat AND DO_Piece IN @Numeros";

        using var sageConnection = new SqlConnection(sageInfo.ConnectionString);
        await sageConnection.OpenAsync();

        foreach (var lot in Decouper(numeros, TailleLot))
        {
            var rows = await sageConnection.QueryAsync<ValeursMarchandiseRow>(
                sql, new { DoDomaineAchat, Numeros = lot });
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.NumeroFacture)) continue;
                resultat[row.NumeroFacture.Trim()] = new ValeursMarchandiseErp
                {
                    NatureMarchandise = row.NatureMarchandise,
                    DateLivraisonMarchandise = row.DateLivraisonMarchandise
                };
            }
        }

        return resultat;
    }

    // ─── Société (LECTURE SEULE, base GRF) — TASK-133 ─────────────────────────────────────────────

    /// <summary>
    /// TASK-133 : champs société de l'en-tête XML — LECTURE SEULE sur <c>P_SOCIETE</c> (base GRF).
    /// Échec explicite si la société est introuvable, jamais un en-tête incomplet silencieux.
    /// </summary>
    public async Task<SocieteDelaiPaiementInfo> GetSocieteInfoAsync(int soId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SocieteInfoRow>(
            // SO_TypeDecDP ajouté par TASK-134 (SELECT additif, colonne EXISTANTE de P_SOCIETE) :
            // type de déclaration DDP par défaut de la société, CDC §7.1.
            @"SELECT SO_Identifiant     AS IdentifiantFiscal,
                     SO_ActiviteMarroc  AS ActiviteMarrocCode,
                     SO_DateJugement    AS DateJugement,
                     SO_ChiffreAffaire  AS ChiffreAffaire,
                     SO_TypeDecDP       AS TypeDeclarationParDefautCode
              FROM P_SOCIETE WHERE SO_Id = @SoId",
            new { SoId = soId });

        if (row == null)
            throw new InvalidOperationException(
                $"Société SO_Id={soId} introuvable dans P_SOCIETE : impossible de construire l'en-tête " +
                "du fichier de dépôt Délai de Paiement.");

        return new SocieteDelaiPaiementInfo
        {
            IdentifiantFiscal = row.IdentifiantFiscal,
            ActiviteMarrocCode = row.ActiviteMarrocCode,
            DateJugement = row.DateJugement,
            ChiffreAffaire = row.ChiffreAffaire,
            TypeDeclarationParDefautCode = row.TypeDeclarationParDefautCode
        };
    }

    // ─── Référentiel mode de règlement (LECTURE SEULE, base GRF) — TASK-133 ───────────────────────

    /// <summary>
    /// TASK-133 : <c>P_MODEREGLEMENT.MR_TypeNo</c> (table EXISTANTE, propriété GRF, réutilisée telle
    /// quelle) indexé par <c>MR_Id</c>. Un <c>MR_Id</c> absent du dictionnaire retourné = mode
    /// introuvable (legacy : <c>modeLigne == null</c>, jamais bloquant).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, Declaration.Core.Model.TypeModeReglementDelaiPaiement>> GetTypesModeReglementAsync(
        IReadOnlyCollection<int> modeIds)
    {
        var resultat = new Dictionary<int, Declaration.Core.Model.TypeModeReglementDelaiPaiement>();

        var ids = (modeIds ?? Array.Empty<int>()).Distinct().ToList();
        if (ids.Count == 0) return resultat;

        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<ModeReglementRow>(
            "SELECT MR_Id AS MrId, MR_TypeNo AS TypeNo FROM P_MODEREGLEMENT WHERE MR_Id IN @Ids",
            new { Ids = ids });

        foreach (var row in rows)
            resultat[row.MrId] = (Declaration.Core.Model.TypeModeReglementDelaiPaiement)row.TypeNo;

        return resultat;
    }

    // ─── Utilitaires ──────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<List<T>> Decouper<T>(IEnumerable<T> source, int taille)
    {
        var lot = new List<T>(taille);
        foreach (var element in source)
        {
            lot.Add(element);
            if (lot.Count < taille) continue;
            yield return lot;
            lot = new List<T>(taille);
        }
        if (lot.Count > 0) yield return lot;
    }

    private sealed class ConfigurationNumerotationRow
    {
        public string? Prefixe { get; set; }
        public bool InclureAnnee { get; set; }
        public bool InclureMois { get; set; }
        public int NombreChiffres { get; set; }
    }

    private sealed class CleRow
    {
        public int EcId { get; set; }
        public int? AfId { get; set; }
    }

    /// <summary>Projection plate entête + compte de lignes (évite un multi-mapping Dapper sur un type valeur).</summary>
    private sealed class EnteteAvecCompteRow
    {
        public int DdpId { get; set; }
        public string Numero { get; set; } = string.Empty;
        public int SocieteId { get; set; }
        public DateTime Date { get; set; }
        public int Exercice { get; set; }
        public int Type { get; set; }
        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }
        public int Statut { get; set; }
        public DateTime DateCreation { get; set; }
        public int CreateurId { get; set; }
        public DateTime DateModification { get; set; }
        public int ModificateurId { get; set; }
        public bool EstDeposee { get; set; }
        public string? Libelle { get; set; }
        public bool FichierGenere { get; set; }
        public int Periode { get; set; }
        public int NombreLignes { get; set; }
    }

    /// <summary>Projection brute P_SOCIETE — TASK-133.</summary>
    private sealed class SocieteInfoRow
    {
        public string? IdentifiantFiscal { get; set; }
        public int ActiviteMarrocCode { get; set; }
        public DateTime? DateJugement { get; set; }
        public decimal ChiffreAffaire { get; set; }

        /// <summary>TASK-134 : <c>SO_TypeDecDP</c> (1 = Annuelle, 2 = Trimestrielle).</summary>
        public int TypeDeclarationParDefautCode { get; set; }
    }

    /// <summary>Projection brute P_MODEREGLEMENT — TASK-133.</summary>
    private sealed class ModeReglementRow
    {
        public int MrId { get; set; }
        public int TypeNo { get; set; }
    }

    /// <summary>Projection brute des 2 colonnes de config P_SOCIETE — TASK-191.</summary>
    private sealed class ConfigurationMarchandiseRow
    {
        public string? ColonneNature { get; set; }
        public string? ColonneDateLivraison { get; set; }
    }

    /// <summary>Projection brute F_DOCENTETE (nature/date livraison marchandise) — TASK-191.</summary>
    private sealed class ValeursMarchandiseRow
    {
        public string? NumeroFacture { get; set; }
        public string? NatureMarchandise { get; set; }
        public DateTime? DateLivraisonMarchandise { get; set; }
    }
}
