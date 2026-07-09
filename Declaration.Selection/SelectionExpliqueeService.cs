using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using Declaration.Core.Model;

namespace Declaration.Selection
{
    public class SelectionExpliqueeService : ISelectionExpliqueeService
    {
        public async Task<IEnumerable<AffectationCandidate>> SelectionnerExpliqueeAsync(
            int soId, 
            DateTime dateDebut, 
            DateTime dateFin, 
            string connectionString,
            Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>>? verifierFacture = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("La chaîne de connexion ne peut pas être vide.", nameof(connectionString));

            var result = new List<AffectationCandidate>();

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var param = new
                {
                    so = soId,
                    debut = dateDebut,
                    finExclude = dateFin.AddDays(1),
                    finInclude = dateFin,
                    domaineFournisseur = GrfEnums.Domaine_ReglementFournisseur,
                    domaineClient = GrfEnums.Domaine_ReglementClient,
                    domaineDepense = GrfEnums.Domaine_Depense,
                    decaisseOui = GrfEnums.Decaisse_Oui,
                    decaisseNon = GrfEnums.Decaisse_Non,
                    modeEspece = GrfEnums.ModePaiement_Espece,
                    pointOui = GrfEnums.Point_Oui
                };

                // 1. Décaissements Fournisseur + Espèces Fournisseur
                var fournisseurs = await connection.QueryAsync<AffectationCandidateRow>(GetSurensembleFournisseurSql(), param);
                await MapAndEvaluate(result, fournisseurs, dateDebut, dateFin, SensAffectation.Achat, verifierFacture);

                // 2. Dépenses
                var depenses = await connection.QueryAsync<AffectationCandidateRow>(GetSurensembleDepenseSql(), param);
                await MapAndEvaluate(result, depenses, dateDebut, dateFin, SensAffectation.Achat, verifierFacture);

                // 3. Encaissements Client
                var encaissements = await connection.QueryAsync<AffectationCandidateRow>(GetSurensembleClientSql(), param);
                await MapAndEvaluate(result, encaissements, dateDebut, dateFin, SensAffectation.Vente, verifierFacture);

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sélection expliquée (SO_Id={soId}) : {ex.Message}");
                throw;
            }
        }

        private async Task MapAndEvaluate(
            List<AffectationCandidate> list, 
            IEnumerable<AffectationCandidateRow> rows, 
            DateTime debut, 
            DateTime fin,
            SensAffectation sens,
            Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>>? verifierFacture)
        {
            foreach (var r in rows)
            {
                var candidate = await SelectionExpliqueeEvaluator.EvaluerAsync(r, debut, fin, sens, verifierFacture);
                list.Add(candidate);
            }
        }

        private string GetSurensembleFournisseurSql() => @"
            SELECT 
                M.MV_Id,
                M.MV_Domaine,
                M.MV_Point,
                M.MV_PointDate,
                M.MV_Date AS DatePaiement,
                M.MV_DECAISSE,
                M.MV_Compta,
                M.MV_Annule,
                M.MV_Impaye,
                M.MV_Type AS ModePaiementId,
                A.AF_Id,
                COALESCE(E.DO_Numero, CAST(A.AF_No AS VARCHAR(50))) AS NumeroFacture,
                A.AF_Montant AS MontantAffecte,
                A.AF_Date AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                A.DT_Id,
                M.CT_Code AS TiersNumero,
                M.CT_Intitule AS TiersNom,
                M.MV_Identifiant AS TiersIF,
                M.MV_Ice AS TiersICE,
                M.MV_Numero AS NumeroRapprochement,
                T.CT_APE AS TiersActivite
            FROM RT_MOUVEMENT M
            LEFT JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            LEFT JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code
            WHERE M.SO_Id = @so 
              AND M.MV_Domaine = @domaineFournisseur
              -- Cadrage direction: Pour l'espèce on ne filtre pas MV_DECAISSE comme dans TASK-008.
              AND (M.MV_Type = @modeEspece OR M.MV_DECAISSE = @decaisseOui)
              AND (
                  (M.MV_Date >= @debut AND M.MV_Date < @finExclude)
                  OR
                  (M.MV_PointDate >= @debut AND M.MV_PointDate < @finExclude)
                  OR
                  (ISNULL(M.MV_Point, 0) != @pointOui AND M.MV_Date < @finExclude)
              )
        ";

        private string GetSurensembleDepenseSql() => @"
            SELECT 
                M.MV_Id,
                M.MV_Domaine,
                M.MV_Point,
                M.MV_PointDate,
                M.MV_Date AS DatePaiement,
                M.MV_DECAISSE,
                M.MV_Compta,
                M.MV_Annule,
                M.MV_Impaye,
                M.MV_Type AS ModePaiementId,
                A.AF_Id,
                COALESCE(E.DO_Numero, CAST(A.AF_No AS VARCHAR(50))) AS NumeroFacture,
                A.AF_Montant AS MontantAffecte,
                A.AF_Date AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                A.DT_Id,
                M.CT_Code AS TiersNumero,
                M.CT_Intitule AS TiersNom,
                M.MV_Identifiant AS TiersIF,
                M.MV_Ice AS TiersICE,
                M.MV_Numero AS NumeroRapprochement,
                T.CT_APE AS TiersActivite
            FROM RT_MOUVEMENT M
            LEFT JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            LEFT JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code
            WHERE M.SO_Id = @so 
              AND M.MV_Domaine = @domaineDepense
              -- Cadrage direction: Uniquement les décaissements
              AND M.MV_DECAISSE = @decaisseOui
              AND (
                  (M.MV_Date >= @debut AND M.MV_Date < @finExclude)
                  OR
                  (M.MV_PointDate >= @debut AND M.MV_PointDate < @finExclude)
                  OR
                  (ISNULL(M.MV_Point, 0) != @pointOui AND M.MV_Date < @finExclude)
              )
        ";

        private string GetSurensembleClientSql() => @"
            SELECT 
                M.MV_Id,
                M.MV_Domaine,
                M.MV_Point,
                M.MV_PointDate,
                M.MV_Date AS DatePaiement,
                M.MV_DECAISSE,
                M.MV_Compta,
                M.MV_Annule,
                M.MV_Impaye,
                M.MV_Type AS ModePaiementId,
                A.AF_Id,
                COALESCE(E.DO_Numero, CAST(A.AF_No AS VARCHAR(50))) AS NumeroFacture,
                A.AF_Montant AS MontantAffecte,
                A.AF_Date AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                A.DT_Id,
                M.CT_Code AS TiersNumero,
                M.CT_Intitule AS TiersNom,
                M.MV_Identifiant AS TiersIF,
                M.MV_Ice AS TiersICE,
                M.MV_Numero AS NumeroRapprochement,
                T.CT_APE AS TiersActivite
            FROM RT_MOUVEMENT M
            LEFT JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            LEFT JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code
            WHERE M.SO_Id = @so 
              AND M.MV_Domaine = @domaineClient
              -- Cadrage direction: Uniquement les encaissements
              AND M.MV_DECAISSE = @decaisseNon
              AND (
                  (M.MV_Date >= @debut AND M.MV_Date < @finExclude)
                  OR
                  (M.MV_PointDate >= @debut AND M.MV_PointDate < @finExclude)
                  OR
                  (ISNULL(M.MV_Point, 0) != @pointOui AND M.MV_Date < @finExclude)
              )
        ";

    }
}
