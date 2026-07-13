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
                    // TASK-050 : MV_DECAISSE ne concerne QUE les traites/effets → on ne filtre plus
                    // dessus. La direction du mouvement vient de MV_Domaine seul (0=encaissement,
                    // 1=décaissement fournisseur, 6=dépense). Usage exact de MV_DECAISSE pour les
                    // traites à revoir séparément (hors périmètre TASK-050).
                    modeEspece = GrfEnums.ModePaiement_Espece,
                    pointOui = GrfEnums.Point_Oui
                };

                // 1. Décaissements Fournisseur + Espèces Fournisseur
                var fournisseurs = await connection.QueryAsync<AffectationCandidateRow>(GetSurensembleFournisseurSql(identiteConfig), param);
                await MapAndEvaluate(result, fournisseurs, dateDebut, dateFin, SensAffectation.Achat, verifierFacture);

                // 2. Dépenses
                var depenses = await connection.QueryAsync<AffectationCandidateRow>(GetSurensembleDepenseSql(identiteConfig), param);
                await MapAndEvaluate(result, depenses, dateDebut, dateFin, SensAffectation.Achat, verifierFacture);

                // 3. Encaissements Client
                var encaissements = await connection.QueryAsync<AffectationCandidateRow>(GetSurensembleClientSql(identiteConfig), param);
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

        private string GetSurensembleFournisseurSql(IdentiteFiscaleFournisseurConfig id) => $@"
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
                {id.SelectIdentifiantExpression("T")} AS TiersIF,
                {id.SelectIceExpression("T")} AS TiersICE,
                M.MV_Numero AS NumeroRapprochement,
                T.CT_APE AS TiersActivite
            FROM RT_MOUVEMENT M
            LEFT JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            LEFT JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code
            WHERE M.SO_Id = @so
              AND M.MV_Domaine = @domaineFournisseur
              -- TASK-050 : MV_DECAISSE supprimé — cadrage direction par MV_Domaine=1 seul.
              -- MV_DECAISSE concerne uniquement les traites/effets (à revoir séparément).
              -- L'espèce (MV_Type=0) reste incluse via MV_Domaine sans filtre additionnel.
              -- Période située par la DATE DE RÉFÉRENCE (TASK-062, source unique RegleDatePeriode) :
              -- rapproché → MV_PointDate ; espèce & non-rapproché → MV_Date. Remplace l'ancien filet
              -- large 3 branches. L'ensemble DÉCLARABLE reste identique (seul l'évaluateur gate).
              AND {RegleDatePeriode.DateReferenceSqlM} >= @debut AND {RegleDatePeriode.DateReferenceSqlM} < @finExclude
        ";

        private string GetSurensembleDepenseSql(IdentiteFiscaleFournisseurConfig id) => $@"
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
                {id.SelectIdentifiantExpression("T")} AS TiersIF,
                {id.SelectIceExpression("T")} AS TiersICE,
                M.MV_Numero AS NumeroRapprochement,
                T.CT_APE AS TiersActivite
            FROM RT_MOUVEMENT M
            LEFT JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            LEFT JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code
            WHERE M.SO_Id = @so
              AND M.MV_Domaine = @domaineDepense
              -- TASK-050 : MV_DECAISSE supprimé — cadrage direction par MV_Domaine=6 seul.
              -- MV_DECAISSE concerne uniquement les traites/effets (à revoir séparément).
              -- Période située par la DATE DE RÉFÉRENCE (TASK-062, source unique RegleDatePeriode) :
              -- rapproché → MV_PointDate ; espèce & non-rapproché → MV_Date. Remplace l'ancien filet
              -- large 3 branches. L'ensemble DÉCLARABLE reste identique (seul l'évaluateur gate).
              AND {RegleDatePeriode.DateReferenceSqlM} >= @debut AND {RegleDatePeriode.DateReferenceSqlM} < @finExclude
        ";

        private string GetSurensembleClientSql(IdentiteFiscaleFournisseurConfig id) => $@"
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
                {id.SelectIdentifiantExpression("T")} AS TiersIF,
                {id.SelectIceExpression("T")} AS TiersICE,
                M.MV_Numero AS NumeroRapprochement,
                T.CT_APE AS TiersActivite
            FROM RT_MOUVEMENT M
            LEFT JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            LEFT JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            LEFT JOIN F_COMPTET T ON T.CT_Num = M.CT_Code
            WHERE M.SO_Id = @so
              AND M.MV_Domaine = @domaineClient
              -- TASK-050 : MV_DECAISSE supprimé — cadrage direction par MV_Domaine=0 seul.
              -- MV_DECAISSE concerne uniquement les traites/effets (à revoir séparément).
              -- Période située par la DATE DE RÉFÉRENCE (TASK-062, source unique RegleDatePeriode) :
              -- rapproché → MV_PointDate ; espèce & non-rapproché → MV_Date. Remplace l'ancien filet
              -- large 3 branches. L'ensemble DÉCLARABLE reste identique (seul l'évaluateur gate).
              AND {RegleDatePeriode.DateReferenceSqlM} >= @debut AND {RegleDatePeriode.DateReferenceSqlM} < @finExclude
        ";

        /// <summary>
        /// TASK-050 — Valorisation facture-first : lit TOUTES les factures EC_Type=0 de la période
        /// depuis RT_ECHEANCE (axe date facture DO_Date). Le statut de règlement (MV_Point,
        /// MV_Compta, MV_Annule, MV_Impaye) et de déclaration (DT_Id) est rattaché en LEFT JOIN.
        /// Une facture sans aucun règlement reçoit des valeurs de mouvement NULL → MV_Compta=0
        /// (NonComptabilise) → EstValorisable=false à ce stade, mais si elle a un MV_Id non NULL
        /// (règlement existant) alors l'évaluateur rend NonRapproche (EstValorisable=true).
        /// Lecture seule stricte — aucune écriture RT_*.
        /// </summary>
        public async Task<IEnumerable<AffectationCandidate>> LireFacturesDepuisPeriodeAsync(
            int soId,
            DateTime dateDebut,
            DateTime dateFin,
            string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("La chaîne de connexion ne peut pas être vide.", nameof(connectionString));

            var result = new List<AffectationCandidate>();

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var identiteConfig = await IdentiteFiscaleFournisseurConfig.ChargerAsync(connection, soId);

                var param = new
                {
                    so = soId,
                    debut = dateDebut,
                    finExclude = dateFin.AddDays(1),
                    finInclude = dateFin,
                    ecTypeFacture = GrfEnums.EcType_FactureErp,
                    ecTypeSolde = GrfEnums.EcType_Solde,
                    ecTypeFgr = GrfEnums.EcType_Fgr,
                    // TASK-050 : MV_DECAISSE supprimé — direction bornée par MV_Domaine seul.
                    domaineFournisseur = GrfEnums.Domaine_ReglementFournisseur,
                    domaineDepense = GrfEnums.Domaine_Depense,
                    pointOui = GrfEnums.Point_Oui,
                    modeEspece = GrfEnums.ModePaiement_Espece
                };

                // Facture-first : pivot sur RT_ECHEANCE (toutes factures EC_Type=0 de la période).
                // Le règlement est rattaché en LEFT JOIN — si absent, MV_Id sera NULL, l'évaluateur
                // rend NonAffecte (cache non déclarable). Si présent mais non rapproché → NonRapproche
                // (EstValorisable=true, cache token NULL). Si rapproché dans la période → Eligible.
                // Note : on lit uniquement les factures fournisseur/dépense (domaines 1 et 6) car
                // EC_Type=0 est spécifique aux achats ERP. Les encaissements client restent gérés
                // par SelectionnerExpliqueeAsync (axe paiement, règle TVA-sur-encaissement).
                var sql = GetFactureFirstSql(identiteConfig);
                var rows = await connection.QueryAsync<AffectationCandidateRow>(sql, param);

                // Évaluation : sens=Achat (EC_Type=0 = factures fournisseur/dépense).
                // La DÉCLARATION reste strictement gated sur EstEligible (rapproché, MV_Point=1,
                // DT_Id null). Une facture non rapprochée est valorisable (cache token NULL) mais
                // n'entre jamais dans une déclaration (garde-fou invariant TASK-050).
                await MapAndEvaluate(result, rows, dateDebut, dateFin, SensAffectation.Achat, null);

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la lecture facture-first (SO_Id={soId}) : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// SQL facture-first TASK-050 : pivot sur RT_ECHEANCE (EC_Type=@ecTypeFacture, DO_Date dans
        /// la période). LEFT JOIN RT_AFFECTATION puis RT_MOUVEMENT pour le statut règlement/déclaration.
        /// Une facture sans règlement reçoit MV_Id=NULL → AffectationCandidateRow avec MV_Compta=0,
        /// AF_Id=NULL, MV_Point=NULL → évaluateur rend NonAffecte (pas valorisable pour affichage,
        /// mais tracée pour transparence). Une facture avec règlement non rapproché → NonRapproche
        /// (EstValorisable=true). Lecture seule — aucune écriture RT_*.
        /// </summary>
        private string GetFactureFirstSql(IdentiteFiscaleFournisseurConfig id) => $@"
            SELECT
                COALESCE(M.MV_Id, 0)            AS MV_Id,
                COALESCE(M.MV_Domaine, @domaineFournisseur) AS MV_Domaine,
                M.MV_Point,
                M.MV_PointDate,
                M.MV_Date                        AS DatePaiement,
                COALESCE(M.MV_DECAISSE, 0)       AS MV_DECAISSE,
                COALESCE(M.MV_Compta, 0)         AS MV_Compta,
                COALESCE(M.MV_Annule, 0)         AS MV_Annule,
                COALESCE(M.MV_Impaye, 0)         AS MV_Impaye,
                M.MV_Type                        AS ModePaiementId,
                A.AF_Id,
                COALESCE(E.DO_Numero, CAST(E.EC_Id AS VARCHAR(50))) AS NumeroFacture,
                COALESCE(A.AF_Montant, E.EC_Montant) AS MontantAffecte,
                E.DO_Date                        AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                A.DT_Id,
                -- RT_ECHEANCE a CT_Code (pas CT_Num) ; fallback si MV absent
                COALESCE(M.CT_Code, E.CT_Code)   AS TiersNumero,
                -- RT_ECHEANCE a CT_Intitule directement ; fallback si MV absent
                COALESCE(M.CT_Intitule, E.CT_Intitule) AS TiersNom,
                {id.SelectIdentifiantExpression("T")} AS TiersIF,
                {id.SelectIceExpression("T")}     AS TiersICE,
                M.MV_Numero                      AS NumeroRapprochement,
                -- CT_APE depuis F_COMPTET (via mouvement si présent, sinon via échéance CT_Code)
                COALESCE(T.CT_APE, T2.CT_APE)    AS TiersActivite
            FROM RT_ECHEANCE E
            -- Rattachement du règlement (LEFT JOIN : facture sans règlement reste présente)
            LEFT JOIN RT_AFFECTATION A ON E.EC_Id = A.EC_Id
            LEFT JOIN RT_MOUVEMENT M   ON A.MV_Id = M.MV_Id
                                      AND M.SO_Id = @so
                                      AND M.MV_Domaine IN (@domaineFournisseur, @domaineDepense)
            -- Maître tiers via mouvement (règlement présent)
            LEFT JOIN F_COMPTET T      ON T.CT_Num = M.CT_Code
            -- Maître tiers via échéance si pas de mouvement (CT_APE uniquement)
            -- CORRECTION TASK-050 : E.CT_Code (pas CT_Num — colonne inexistante dans RT_ECHEANCE)
            LEFT JOIN F_COMPTET T2     ON T2.CT_Num = E.CT_Code
            WHERE E.SO_Id = @so
              -- TASK-052 (suite) : liste blanche complète (0=FC, 4=Solde, 111=FGR), pas seulement
              -- EC_Type_FactureErp — sinon les factures Solde/FGR ne sont jamais lues ici alors que
              -- l'évaluateur (EstEcTypeFacture) les considère dans son périmètre. Voir FF260076
              -- (EC_Type=111) jamais remontée avant ce correctif malgré une lecture facture-first.
              AND E.EC_Type IN (@ecTypeFacture, @ecTypeSolde, @ecTypeFgr)
              -- Axe date facture (TASK-050 : cohérent écran Factures, TASK-041)
              AND E.DO_Date >= @debut AND E.DO_Date < @finExclude
        ";

    }
}
