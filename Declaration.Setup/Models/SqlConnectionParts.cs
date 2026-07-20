namespace Declaration.Setup.Models;

/// <summary>
/// Décomposition d'une chaîne de connexion SQL Server au format utilisé par connections.json
/// ("Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;"), pour
/// permettre au formulaire de masquer uniquement le mot de passe (TASK-115 : jamais pré-rempli
/// en clair) sans perdre les autres champs.
/// </summary>
public sealed class SqlConnectionParts
{
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Password { get; set; } = "";
    public bool TrustServerCertificate { get; set; } = true;

    public string ToConnectionString() =>
        $"Server={Server};Database={Database};User Id={UserId};Password={Password};TrustServerCertificate={(TrustServerCertificate ? "True" : "False")};";

    public static SqlConnectionParts Parse(string? connectionString)
    {
        var parts = new SqlConnectionParts();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return parts;
        }

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = segment.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = segment[..idx].Trim();
            var value = segment[(idx + 1)..].Trim();

            switch (key.ToLowerInvariant())
            {
                case "server":
                case "data source":
                    parts.Server = value;
                    break;
                case "database":
                case "initial catalog":
                    parts.Database = value;
                    break;
                case "user id":
                case "uid":
                case "user":
                    parts.UserId = value;
                    break;
                case "password":
                case "pwd":
                    parts.Password = value;
                    break;
                case "trustservercertificate":
                    parts.TrustServerCertificate = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        return parts;
    }
}
