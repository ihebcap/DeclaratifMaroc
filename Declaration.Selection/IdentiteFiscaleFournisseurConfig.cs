using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Declaration.Selection
{
    /// <summary>
    /// Levée quand la configuration de l'identité fiscale fournisseur (noms de colonnes ICE/IF
    /// dans P_SOCIETE) est absente ou invalide. Blocage explicite au niveau société — jamais
    /// une erreur silencieuse répétée par facture. TASK-048.
    /// </summary>
    public sealed class ConfigurationIdentiteFiscaleException : ApplicationException
    {
        public ConfigurationIdentiteFiscaleException(string message) : base(message) { }
    }

    /// <summary>
    /// Source dynamique de l'ICE / IF fournisseur.
    ///
    /// L'ICE et l'IF du tiers vivent sur le MAÎTRE tiers ERP (F_COMPTET), pas sur le mouvement
    /// (MV_Ice / MV_Identifiant, snapshot souvent vide). Les NOMS des colonnes ERP portant ces
    /// données sont configurés par société dans P_SOCIETE (SO_DecTvaColNameIceFrs /
    /// SO_DecTvaColNameIdentifiantFrs) : ils diffèrent d'un client à l'autre et ne sont JAMAIS
    /// figés en dur (TASK-048, réplique de la mécanique legacy GetAllIceTiersToMaroc).
    ///
    /// Ces noms étant interpolés dans le SQL, ils sont validés par whitelist puis quotés entre
    /// crochets avant toute interpolation. Les valeurs (codes tiers) restent paramétrées.
    /// </summary>
    public sealed class IdentiteFiscaleFournisseurConfig
    {
        // Un identifiant SQL de colonne : lettres/chiffres/underscore uniquement. Exclut donc
        // ']', espaces et tout caractère permettant une évasion du quotage entre crochets.
        private static readonly Regex IdentifiantSqlValide =
            new Regex("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        /// <summary>Nom de colonne ERP portant l'ICE fournisseur (déjà validé).</summary>
        public string ColonneIce { get; }

        /// <summary>Nom de colonne ERP portant l'identifiant fiscal fournisseur (déjà validé).</summary>
        public string ColonneIdentifiant { get; }

        private IdentiteFiscaleFournisseurConfig(string colonneIce, string colonneIdentifiant)
        {
            ColonneIce = colonneIce;
            ColonneIdentifiant = colonneIdentifiant;
        }

        /// <summary>Expression SQL sûre pour l'ICE, quotée sur l'alias du maître tiers fourni.</summary>
        public string SelectIceExpression(string aliasMaitreTiers) => $"{aliasMaitreTiers}.[{ColonneIce}]";

        /// <summary>Expression SQL sûre pour l'IF, quotée sur l'alias du maître tiers fourni.</summary>
        public string SelectIdentifiantExpression(string aliasMaitreTiers) => $"{aliasMaitreTiers}.[{ColonneIdentifiant}]";

        /// <summary>
        /// Charge et valide la configuration pour une société (1 requête par société).
        /// Lève <see cref="ConfigurationIdentiteFiscaleException"/> si un nom de colonne est
        /// absent ou n'est pas un identifiant SQL valide.
        /// </summary>
        public static async Task<IdentiteFiscaleFournisseurConfig> ChargerAsync(SqlConnection connection, int soId)
        {
            const string sql = @"
                SELECT SO_DecTvaColNameIceFrs         AS ColonneIce,
                       SO_DecTvaColNameIdentifiantFrs AS ColonneIdentifiant
                FROM P_SOCIETE
                WHERE SO_Id = @so";

            var row = await connection.QuerySingleOrDefaultAsync<ConfigRow>(sql, new { so = soId });

            if (row == null)
                throw new ConfigurationIdentiteFiscaleException(
                    $"Société SO_Id={soId} introuvable dans P_SOCIETE : impossible de déterminer les colonnes ICE/IF du tiers.");

            return Creer(row.ColonneIce, row.ColonneIdentifiant, soId);
        }

        /// <summary>
        /// Construit la config à partir des noms de colonnes bruts (valide présence + whitelist).
        /// Séparé de <see cref="ChargerAsync"/> pour être testable sans base.
        /// </summary>
        public static IdentiteFiscaleFournisseurConfig Creer(string colonneIceBrute, string colonneIdentifiantBrute, int soId)
        {
            var colonneIce = ValiderIdentifiant(colonneIceBrute, "ICE", soId);
            var colonneIf = ValiderIdentifiant(colonneIdentifiantBrute, "identifiant fiscal (IF)", soId);

            return new IdentiteFiscaleFournisseurConfig(colonneIce, colonneIf);
        }

        private static string ValiderIdentifiant(string valeur, string libelle, int soId)
        {
            var nom = (valeur ?? "").Trim();

            // Tolérer un nom déjà quoté en config ([ICE]) : on retire les crochets englobants
            // avant validation, on les réappliquera de façon contrôlée.
            if (nom.Length >= 2 && nom[0] == '[' && nom[nom.Length - 1] == ']')
                nom = nom.Substring(1, nom.Length - 2).Trim();

            if (string.IsNullOrEmpty(nom))
                throw new ConfigurationIdentiteFiscaleException(
                    $"Colonne {libelle} du tiers non configurée dans P_SOCIETE (SO_Id={soId}). " +
                    "Renseignez la colonne ERP correspondante avant de déclarer.");

            if (!IdentifiantSqlValide.IsMatch(nom))
                throw new ConfigurationIdentiteFiscaleException(
                    $"Nom de colonne {libelle} invalide dans P_SOCIETE (SO_Id={soId}) : « {valeur} ». " +
                    "Seuls les caractères A-Z, a-z, 0-9 et « _ » sont autorisés.");

            return nom;
        }

        private sealed class ConfigRow
        {
            public string ColonneIce { get; set; }
            public string ColonneIdentifiant { get; set; }
        }
    }
}
