using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using Declaration.Core.Model;

namespace Declaration.Selection
{
    public class SelectionnerAffectationsService : ISelectionnerAffectationsService
    {
        public async Task<IEnumerable<AffectationADeclarer>> SelectionnerAffectationsAsync(int soId, DateTime dateDebut, DateTime dateFin, string connectionString, int? dtIdToInclude = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("La chaîne de connexion ne peut pas être vide.", nameof(connectionString));

            var result = new List<AffectationADeclarer>();

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // ICE/IF fournisseur : source dynamique via noms de colonnes ERP configurés dans
                // P_SOCIETE (TASK-048). Blocage explicite si config absente/invalide.
                var identiteConfig = await IdentiteFiscaleFournisseurConfig.ChargerAsync(connection, soId);

                var param = new
                {
                    so = soId,
                    debut = dateDebut,
                    finExclude = dateFin.AddDays(1),
                    finInclude = dateFin,
                    domaineFournisseur = GrfEnums.Domaine_ReglementFournisseur,
                    domaineClient = GrfEnums.Domaine_ReglementClient,
                    domaineDepense = GrfEnums.Domaine_Depense,
                    comptabilise = GrfEnums.Compta_Comptabilise,
                    nonImpaye = GrfEnums.Impaye_NonImpaye,
                    pointOui = GrfEnums.Point_Oui,
                    annuleNon = GrfEnums.Annule_Non,
                    decaisseOui = GrfEnums.Decaisse_Oui,
                    decaisseNon = GrfEnums.Decaisse_Non,
                    modeEspece = GrfEnums.ModePaiement_Espece,
                    ecTypeFactureErp = GrfEnums.EcType_FactureErp,
                    ecTypeSolde = GrfEnums.EcType_Solde,
                    ecTypeFgr = GrfEnums.EcType_Fgr,
                    dtId = dtIdToInclude
                };

                // Les requêtes sont conçues pour cibler les mouvements pointés sur la période (ou datés sur la période pour les espèces),
                // décaissés/encaissés, comptabilisés, non annulés, non impayés.
                // On joint sur RT_AFFECTATION pour avoir le détail (la facture) et on filtre DT_Id IS NULL (non déclaré).

                // 1. Décaissements Fournisseur
                var decaissements = await connection.QueryAsync<AffectationRow>(
                    GetDecaissementFournisseurSql(identiteConfig), param);
                MapAndAdd(result, decaissements, SensAffectation.Achat, SourceAffectation.Decaissement);

                // 2. Espèces Fournisseur
                var especes = await connection.QueryAsync<AffectationRow>(
                    GetEspeceFournisseurSql(identiteConfig), param);
                MapAndAdd(result, especes, SensAffectation.Achat, SourceAffectation.Espece);

                // 3. Dépenses
                var depenses = await connection.QueryAsync<AffectationRow>(
                    GetDepenseSql(identiteConfig), param);
                MapAndAdd(result, depenses, SensAffectation.Achat, SourceAffectation.Depense);

                // 5. Encaissements Client (Ventes)
                var encaissements = await connection.QueryAsync<AffectationRow>(
                    GetEncaissementClientSql(identiteConfig), param);
                MapAndAdd(result, encaissements, SensAffectation.Vente, SourceAffectation.Encaissement);

                // Note: on pourrait aussi rajouter l'espèce Client.

                return result;
            }
            catch (ConfigurationIdentiteFiscaleException)
            {
                // Blocage explicite (config ICE/IF absente/invalide) : ne pas masquer en liste vide.
                throw;
            }
            catch (Exception ex)
            {
                // Robustesse demandée : on log (ici on pourrait injecter ILogger) et on retourne une liste vide
                // plutôt que de planter brutalement si la période ou société est introuvable.
                Console.WriteLine($"Erreur lors de la sélection des affectations (SO_Id={soId}) : {ex.Message}");
                return result;
            }
        }

        private void MapAndAdd(List<AffectationADeclarer> list, IEnumerable<AffectationRow> rows, SensAffectation sens, SourceAffectation source)
        {
            foreach (var row in rows)
            {
                list.Add(new AffectationADeclarer
                {
                    NumeroFacture = row.NumeroFacture ?? "",
                    NumeroRapprochement = row.NumeroRapprochement ?? "",
                    Sens = sens,
                    Source = source,
                    MontantAffecte = row.MontantAffecte,
                    DatePaiement = row.DatePaiement,
                    DateFacture = row.DateFacture,
                    ModePaiement = source == SourceAffectation.Espece 
                        ? "1" // 1 = Espèce en Simpl-TVA
                        : GrfEnums.MapperModePaiementSimplTVA(row.ModePaiementId),
                    Tiers = new TiersInfo
                    {
                        Numero = row.TiersNumero ?? "",
                        Nom = row.TiersNom ?? "",
                        IdentifiantFiscal = row.TiersIF ?? "",
                        Ice = row.TiersICE ?? "",
                        CodeActivite = row.TiersActivite ?? ""
                    },
                    EC_Type = row.EC_Type ?? 0,
                    EC_Id = row.EC_Id ?? 0
                });
            }
        }

        private string GetDecaissementFournisseurSql(IdentiteFiscaleFournisseurConfig id) => $@"
            SELECT
                E.DO_Numero AS NumeroFacture,
                M.MV_Numero AS NumeroRapprochement,
                A.AF_Montant AS MontantAffecte,
                M.MV_Date AS DatePaiement,
                A.AF_Date AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                M.CT_Code AS TiersNumero,
                M.CT_Intitule AS TiersNom,
                {id.SelectIdentifiantExpression("T")} AS TiersIF,
                {id.SelectIceExpression("T")} AS TiersICE,
                T.CT_APE AS TiersActivite,
                M.MV_Type AS ModePaiementId
            FROM RT_MOUVEMENT M
            JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code
            WHERE M.SO_Id = @so 
              AND M.MV_Domaine = @domaineFournisseur
              AND M.MV_Point = @pointOui 
              AND M.MV_DECAISSE = @decaisseOui AND M.MV_Compta = @comptabilise 
              AND M.MV_Annule = @annuleNon AND M.MV_Impaye = @nonImpaye
              AND (
                  (A.DT_Id IS NULL AND {RegleDatePeriode.DateReferenceSqlM} < @finExclude) -- TASK-099 : coupure de période (rattrapage, plus de borne basse) via DateReference (TASK-062) ; rapproché → MV_PointDate
                  OR (@dtId IS NOT NULL AND A.DT_Id = @dtId)
              )
              AND M.MV_Type != @modeEspece -- Exclure l'espèce
              AND E.EC_Type IN (@ecTypeFactureErp, @ecTypeSolde, @ecTypeFgr) -- liste blanche : vraies factures + solde
        ";

        private string GetEspeceFournisseurSql(IdentiteFiscaleFournisseurConfig id) => $@"
            SELECT
                E.DO_Numero AS NumeroFacture,
                M.MV_Numero AS NumeroRapprochement,
                A.AF_Montant AS MontantAffecte,
                M.MV_Date AS DatePaiement,
                A.AF_Date AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                M.CT_Code AS TiersNumero,
                M.CT_Intitule AS TiersNom,
                {id.SelectIdentifiantExpression("T")} AS TiersIF,
                {id.SelectIceExpression("T")} AS TiersICE,
                T.CT_APE AS TiersActivite,
                M.MV_Type AS ModePaiementId
            FROM RT_MOUVEMENT M
            JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code
            WHERE M.SO_Id = @so 
              AND M.MV_Domaine = @domaineFournisseur
              AND M.MV_Compta = @comptabilise 
              AND M.MV_Annule = @annuleNon AND M.MV_Impaye = @nonImpaye
              AND (
                  (A.DT_Id IS NULL AND {RegleDatePeriode.DateReferenceSqlM} < @finExclude) -- TASK-099 : coupure de période (rattrapage, plus de borne basse) ; espèce → MV_Date via DateReference (TASK-062)
                  OR (@dtId IS NOT NULL AND A.DT_Id = @dtId)
              )
              AND M.MV_Type = @modeEspece -- Filtre espèce
              AND E.EC_Type IN (@ecTypeFactureErp, @ecTypeSolde, @ecTypeFgr) -- liste blanche : vraies factures + solde
        ";

        private string GetDepenseSql(IdentiteFiscaleFournisseurConfig id) => $@"
            SELECT
                E.DO_Numero AS NumeroFacture,
                M.MV_Numero AS NumeroRapprochement,
                A.AF_Montant AS MontantAffecte,
                M.MV_Date AS DatePaiement,
                A.AF_Date AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                M.CT_Code AS TiersNumero,
                M.CT_Intitule AS TiersNom,
                {id.SelectIdentifiantExpression("T")} AS TiersIF,
                {id.SelectIceExpression("T")} AS TiersICE,
                T.CT_APE AS TiersActivite,
                M.MV_Type AS ModePaiementId
            FROM RT_MOUVEMENT M
            JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code
            WHERE M.SO_Id = @so 
              AND M.MV_Domaine = @domaineDepense
              AND M.MV_Point = @pointOui 
              AND M.MV_DECAISSE = @decaisseOui AND M.MV_Compta = @comptabilise 
              AND M.MV_Annule = @annuleNon
              AND (
                  (A.DT_Id IS NULL AND {RegleDatePeriode.DateReferenceSqlM} < @finExclude) -- TASK-099 : coupure de période (rattrapage, plus de borne basse) via DateReference (TASK-062) ; rapproché → MV_PointDate
                  OR (@dtId IS NOT NULL AND A.DT_Id = @dtId)
              )
              AND E.EC_Type IN (@ecTypeFactureErp, @ecTypeSolde, @ecTypeFgr) -- liste blanche : vraies factures + solde
        ";

        private string GetEncaissementClientSql(IdentiteFiscaleFournisseurConfig id) => $@"
            SELECT
                E.DO_Numero AS NumeroFacture,
                M.MV_Numero AS NumeroRapprochement,
                A.AF_Montant AS MontantAffecte,
                M.MV_Date AS DatePaiement,
                A.AF_Date AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                M.CT_Code AS TiersNumero,
                M.CT_Intitule AS TiersNom,
                {id.SelectIdentifiantExpression("T")} AS TiersIF,
                {id.SelectIceExpression("T")} AS TiersICE,
                T.CT_APE AS TiersActivite,
                M.MV_Type AS ModePaiementId
            FROM RT_MOUVEMENT M
            JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code
            WHERE M.SO_Id = @so 
              AND M.MV_Domaine = @domaineClient
              AND M.MV_Point = @pointOui 
              AND M.MV_DECAISSE = @decaisseNon -- Encaissement
              AND M.MV_Compta = @comptabilise 
              AND M.MV_Annule = @annuleNon AND M.MV_Impaye = @nonImpaye
              AND (
                  (A.DT_Id IS NULL AND {RegleDatePeriode.DateReferenceSqlM} < @finExclude) -- TASK-099 : coupure de période (rattrapage, plus de borne basse) via DateReference (TASK-062) ; rapproché → MV_PointDate
                  OR (@dtId IS NOT NULL AND A.DT_Id = @dtId)
              )
              AND E.EC_Type IN (@ecTypeFactureErp, @ecTypeSolde, @ecTypeFgr) -- liste blanche : vraies factures + solde
        ";

        private class AffectationRow
        {
            public string NumeroFacture { get; set; }
            public string NumeroRapprochement { get; set; }
            public decimal MontantAffecte { get; set; }
            public DateTime DatePaiement { get; set; }
            public DateTime? DateFacture { get; set; }
            public string TiersNumero { get; set; }
            public string TiersNom { get; set; }
            public string TiersIF { get; set; }
            public string TiersICE { get; set; }
            public string TiersActivite { get; set; }
            public int? ModePaiementId { get; set; }
            public int? EC_Type { get; set; }
            public int? EC_Id { get; set; }
        }
    }
}
