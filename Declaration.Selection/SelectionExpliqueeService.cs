using System;
using System.Collections.Generic;
using System.Linq;
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
            string sageConnectionString,
            Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>>? verifierFacture = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("La chaîne de connexion ne peut pas être vide.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(sageConnectionString))
                throw new ArgumentException("La chaîne de connexion Sage ne peut pas être vide.", nameof(sageConnectionString));

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
                var fournisseurs = (await connection.QueryAsync<AffectationCandidateRow>(GetSurensembleFournisseurSql(), param)).ToList();

                // 2. Dépenses
                var depenses = (await connection.QueryAsync<AffectationCandidateRow>(GetSurensembleDepenseSql(), param)).ToList();

                // 3. Encaissements Client
                var encaissements = (await connection.QueryAsync<AffectationCandidateRow>(GetSurensembleClientSql(), param)).ToList();

                // TASK-154 : F_COMPTET est une table Sage — plus jamais lue via la connexion GRF
                // (LEFT JOIN cross-base statique supprimé) ni via un synonyme. Jointure applicative
                // batchée (un seul SELECT ... WHERE CT_Num IN @codes) sur la connexion Sage déjà
                // résolue par SO_Id EN AMONT par l'appelant (ce service ne référence pas
                // Declaration.Infrastructure et ne résout jamais lui-même cette connexion), sur
                // l'union des tiers des 3 surensembles — jamais une connexion ouverte par tiers.
                await EnrichirTiersDepuisSageAsync(sageConnectionString, identiteConfig,
                    fournisseurs.Concat(depenses).Concat(encaissements));

                await MapAndEvaluate(result, fournisseurs, dateDebut, dateFin, SensAffectation.Achat, verifierFacture);
                await MapAndEvaluate(result, depenses, dateDebut, dateFin, SensAffectation.Achat, verifierFacture);
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

        private string GetSurensembleFournisseurSql() => $@"
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
                E.DO_Date AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                A.DT_Id,
                M.CT_Code AS TiersNumero,
                M.CT_Intitule AS TiersNom,
                M.MV_Numero AS NumeroRapprochement
            FROM RT_MOUVEMENT M
            LEFT JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            LEFT JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            WHERE M.SO_Id = @so
              AND M.MV_Domaine = @domaineFournisseur
              -- TASK-050 : MV_DECAISSE supprimé — cadrage direction par MV_Domaine=1 seul.
              -- MV_DECAISSE concerne uniquement les traites/effets (à revoir séparément).
              -- L'espèce (MV_Type=0) reste incluse via MV_Domaine sans filtre additionnel.
              -- TASK-099 : périmètre = date de COUPURE (rattrapage, plus de borne basse) + non
              -- encore déclaré (DT_Id IS NULL, rôle n°1 — borne aussi le volume sans borne basse).
              -- Rapproché → MV_PointDate ; espèce & non-rapproché → MV_Date (RegleDatePeriode,
              -- source unique). L'ensemble DÉCLARABLE reste gated par l'évaluateur (EstDeclarable).
              AND A.DT_Id IS NULL
              AND {RegleDatePeriode.DateReferenceSqlM} < @finExclude
        ";

        private string GetSurensembleDepenseSql() => $@"
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
                E.DO_Date AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                A.DT_Id,
                M.CT_Code AS TiersNumero,
                M.CT_Intitule AS TiersNom,
                M.MV_Numero AS NumeroRapprochement
            FROM RT_MOUVEMENT M
            LEFT JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            LEFT JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            WHERE M.SO_Id = @so
              AND M.MV_Domaine = @domaineDepense
              -- TASK-050 : MV_DECAISSE supprimé — cadrage direction par MV_Domaine=6 seul.
              -- MV_DECAISSE concerne uniquement les traites/effets (à revoir séparément).
              -- TASK-099 : périmètre = date de COUPURE (rattrapage, plus de borne basse) + non
              -- encore déclaré (DT_Id IS NULL). L'ensemble DÉCLARABLE reste gated par l'évaluateur.
              AND A.DT_Id IS NULL
              AND {RegleDatePeriode.DateReferenceSqlM} < @finExclude
        ";

        private string GetSurensembleClientSql() => $@"
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
                E.DO_Date AS DateFacture,
                E.EC_Type,
                E.EC_Id,
                A.DT_Id,
                M.CT_Code AS TiersNumero,
                M.CT_Intitule AS TiersNom,
                M.MV_Numero AS NumeroRapprochement
            FROM RT_MOUVEMENT M
            LEFT JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
            LEFT JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
            WHERE M.SO_Id = @so
              AND M.MV_Domaine = @domaineClient
              -- TASK-050 : MV_DECAISSE supprimé — cadrage direction par MV_Domaine=0 seul.
              -- MV_DECAISSE concerne uniquement les traites/effets (à revoir séparément).
              -- TASK-099 : périmètre = date de COUPURE (rattrapage, plus de borne basse) + non
              -- encore déclaré (DT_Id IS NULL). L'ensemble DÉCLARABLE reste gated par l'évaluateur.
              AND A.DT_Id IS NULL
              AND {RegleDatePeriode.DateReferenceSqlM} < @finExclude
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
            string connectionString,
            string sageConnectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("La chaîne de connexion ne peut pas être vide.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(sageConnectionString))
                throw new ArgumentException("La chaîne de connexion Sage ne peut pas être vide.", nameof(sageConnectionString));

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
                    modeEspece = GrfEnums.ModePaiement_Espece,
                    // TASK-145 : DO_Domaine (RT_ECHEANCE) — restreint cette lecture facture-first
                    // aux seuls documents d'ACHAT Sage (ErpDomaine.Achat=1). Sans ce filtre, une
                    // échéance EC_Type=0 de VENTE (DO_Domaine=0) était lue ici et évaluée avec
                    // Sens=Achat codé en dur, provoquant un appel Sage docFactoryAchat.ExistPiece
                    // sur une pièce de vente → "Facture d'achat introuvable" alors que la pièce
                    // existe côté vente. Les ventes restent gérées par SelectionnerExpliqueeAsync.
                    achatDomaine = GrfEnums.ErpDomaine_Achat
                };

                // Facture-first : pivot sur RT_ECHEANCE (toutes factures EC_Type=0 de la période).
                // Le règlement est rattaché en LEFT JOIN — si absent, MV_Id sera NULL, l'évaluateur
                // rend NonAffecte (cache non déclarable). Si présent mais non rapproché → NonRapproche
                // (EstValorisable=true, cache token NULL). Si rapproché dans la période → Eligible.
                // Note : on lit uniquement les factures fournisseur/dépense (domaines 1 et 6) car
                // EC_Type=0 est spécifique aux achats ERP. Les encaissements client restent gérés
                // par SelectionnerExpliqueeAsync (axe paiement, règle TVA-sur-encaissement).
                var sql = GetFactureFirstSql();
                var rows = (await connection.QueryAsync<AffectationCandidateRow>(sql, param)).ToList();

                // TASK-154 : F_COMPTET (Sage) rattaché en mémoire, jamais via la connexion GRF —
                // voir le commentaire équivalent dans SelectionnerExpliqueeAsync.
                await EnrichirTiersDepuisSageAsync(sageConnectionString, identiteConfig, rows);

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
        private string GetFactureFirstSql() => $@"
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
                M.MV_Numero                      AS NumeroRapprochement
                -- TASK-154 : TiersIF/TiersICE/TiersActivite (F_COMPTET, table Sage) ne sont plus
                -- lus ici — rattachés en mémoire après coup via EnrichirTiersDepuisSageAsync, par
                -- une jointure applicative sur TiersNumero (= COALESCE(M.CT_Code, E.CT_Code)
                -- ci-dessus, donc même clé qu'auraient utilisée les anciens LEFT JOIN T/T2).
            FROM RT_ECHEANCE E
            -- Rattachement du règlement (LEFT JOIN : facture sans règlement reste présente)
            LEFT JOIN RT_AFFECTATION A ON E.EC_Id = A.EC_Id
            LEFT JOIN RT_MOUVEMENT M   ON A.MV_Id = M.MV_Id
                                      AND M.SO_Id = @so
                                      AND M.MV_Domaine IN (@domaineFournisseur, @domaineDepense)
            WHERE E.SO_Id = @so
              -- TASK-052 (suite) : liste blanche complète (0=FC, 4=Solde, 111=FGR), pas seulement
              -- EC_Type_FactureErp — sinon les factures Solde/FGR ne sont jamais lues ici alors que
              -- l'évaluateur (EstEcTypeFacture) les considère dans son périmètre. Voir FF260076
              -- (EC_Type=111) jamais remontée avant ce correctif malgré une lecture facture-first.
              AND E.EC_Type IN (@ecTypeFacture, @ecTypeSolde, @ecTypeFgr)
              -- Axe date facture (TASK-050 : cohérent écran Factures, TASK-041)
              AND E.DO_Date >= @debut AND E.DO_Date < @finExclude
              -- TASK-145 : cette lecture est achat/dépense uniquement (voir commentaire l.253-255
              -- ci-dessus) -- sans ce filtre, une échéance de VENTE (DO_Domaine=0) était lue ici et
              -- évaluée avec Sens=Achat codé en dur (MapAndEvaluate), provoquant une fausse erreur
              -- Sage Facture d'achat introuvable sur une pièce de vente authentique.
              AND E.DO_Domaine = @achatDomaine
        ";

        /// <summary>
        /// TASK-154 : rattache les colonnes tiers (TiersIF/TiersICE/TiersActivite) portées par
        /// <c>F_COMPTET</c> — une table Sage, jamais présente dans la base GRF — via une jointure
        /// APPLICATIVE (dictionnaire en mémoire par <c>CT_Num</c>), après une lecture batchée
        /// unique sur la connexion Sage déjà résolue par l'appelant (<c>SO_Id</c> → base Sage,
        /// TASK-118). Jamais de JOIN SQL trois-parties, jamais de synonyme, jamais de connexion
        /// ouverte par tiers individuel — un seul <c>SELECT ... WHERE CT_Num IN @codes</c> pour
        /// tous les tiers distincts déjà lus côté GRF. Lecture seule stricte.
        /// </summary>
        private static async Task EnrichirTiersDepuisSageAsync(
            string sageConnectionString,
            IdentiteFiscaleFournisseurConfig identiteConfig,
            IEnumerable<AffectationCandidateRow> rows)
        {
            var codes = rows
                .Select(r => r.TiersNumero)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            if (codes.Count == 0) return;

            var sql = $@"
                SELECT
                    CT_Num,
                    {identiteConfig.SelectIdentifiantExpression("F_COMPTET")} AS TiersIF,
                    {identiteConfig.SelectIceExpression("F_COMPTET")} AS TiersICE,
                    CT_APE
                FROM F_COMPTET
                WHERE CT_Num IN @codes";

            using var sageConnection = new SqlConnection(sageConnectionString);
            await sageConnection.OpenAsync();
            var tiersRows = await sageConnection.QueryAsync<TiersFComptetRow>(sql, new { codes });

            var parCode = tiersRows
                .Where(t => !string.IsNullOrWhiteSpace(t.CT_Num))
                .GroupBy(t => t.CT_Num)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var r in rows)
            {
                if (r.TiersNumero != null && parCode.TryGetValue(r.TiersNumero, out var t))
                {
                    r.TiersIF = t.TiersIF;
                    r.TiersICE = t.TiersICE;
                    r.TiersActivite = t.CT_APE;
                }
            }
        }

        /// <summary>Ligne F_COMPTET (Sage) minimale pour la jointure applicative TASK-154.</summary>
        private sealed class TiersFComptetRow
        {
            public string CT_Num { get; set; } = "";
            public string? TiersIF { get; set; }
            public string? TiersICE { get; set; }
            public string? CT_APE { get; set; }
        }
    }
}
