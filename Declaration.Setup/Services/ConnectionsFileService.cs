using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Declaration.Setup.Models;

namespace Declaration.Setup.Services;

/// <summary>
/// Lecture/écriture de connections.json — le seul fichier de configuration applicative
/// (TASK-115 §Critères de validation : le setup automatise son écriture, n'introduit pas de
/// second mécanisme de config parallèle). Merge par <see cref="JsonObject"/> pour préserver les
/// clés non gérées par le formulaire (ex. les "_comment_*") plutôt que de régénérer le fichier
/// à partir d'un DTO figé.
/// </summary>
public static class ConnectionsFileService
{
    private const string FileName = "connections.json";

    public static string GetPath(string installFolder) => Path.Combine(installFolder, FileName);

    public static bool Exists(string installFolder) => File.Exists(GetPath(installFolder));

    public static JsonObject LoadOrEmpty(string installFolder)
    {
        var path = GetPath(installFolder);
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        var text = File.ReadAllText(path);
        return (JsonNode.Parse(text) as JsonObject) ?? new JsonObject();
    }

    /// <summary>
    /// Extrait les valeurs à pré-remplir dans le formulaire. Les mots de passe (SQL/JWT) ne sont
    /// jamais renvoyés en clair — ils restent vides, avec un indicateur "inchangé" côté UI.
    /// </summary>
    public static SetupData ExtractForPrefill(JsonObject root, string installFolder)
    {
        var data = new SetupData { InstallFolder = installFolder };

        var cs = root["ConnectionStrings"] as JsonObject;
        data.Grf = SqlConnectionParts.Parse(GetString(cs, "GrfConnection"));
        data.Persistence = SqlConnectionParts.Parse(GetString(cs, "PersistenceConnection"));
        data.Grf.Password = "";
        data.Persistence.Password = "";

        data.JwtSecretKey = "";

        var server = root["ServerConfig"] as JsonObject;
        data.Port = GetInt(server, "Port") ?? 5000;

        var worker = root["WorkerConfig"] as JsonObject;
        data.SageVersion = SageVersionExtensions.FromWorkerRelativePath(GetString(worker, "WorkerExePath")) ?? SageVersion.V10;

        // ApLicenceSubject n'est plus lu ici (TASK-122) : constante produit fixée dans SetupData,
        // jamais reprise d'un connections.json existant (même si une valeur différente y figurait).
        var licence = root["ApLicence"] as JsonObject;
        data.ApLicenceServerAddress = GetString(licence, "ServerAddress") ?? "127.0.0.1";
        data.ApLicenceServerPort = GetInt(licence, "ServerPort") ?? 8003;

        return data;
    }

    /// <summary>
    /// Applique les valeurs du formulaire sur l'objet JSON. En mode mise à jour, un champ secret
    /// laissé vide conserve la valeur déjà présente dans le fichier (jamais écrasé par du vide).
    /// </summary>
    public static void ApplyChanges(JsonObject root, SetupData data, bool isUpdate)
    {
        var cs = GetOrCreateObject(root, "ConnectionStrings");
        ApplyConnection(cs, "GrfConnection", data.Grf, isUpdate);
        ApplyConnection(cs, "PersistenceConnection", data.Persistence, isUpdate);

        var jwt = GetOrCreateObject(root, "JwtSettings");
        if (!isUpdate || !string.IsNullOrEmpty(data.JwtSecretKey))
        {
            // Jamais demandé à l'utilisateur : générée automatiquement à l'installation si non
            // fournie (le formulaire n'expose plus ce champ), conservée telle quelle en mise à
            // jour tant qu'elle n'est pas explicitement changée (data.JwtSecretKey vide ci-dessus).
            jwt["SecretKey"] = !string.IsNullOrEmpty(data.JwtSecretKey)
                ? data.JwtSecretKey
                : GenerateSecretKey();
        }

        var server = GetOrCreateObject(root, "ServerConfig");
        server["Port"] = data.Port;

        var worker = GetOrCreateObject(root, "WorkerConfig");
        worker["WorkerExePath"] = data.SageVersion.WorkerRelativePath();

        var licence = GetOrCreateObject(root, "ApLicence");
        licence["Subject"] = data.ApLicenceSubject;
        licence["ServerAddress"] = data.ApLicenceServerAddress;
        licence["ServerPort"] = data.ApLicenceServerPort;
    }

    public static void Save(JsonObject root, string installFolder)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(GetPath(installFolder), root.ToJsonString(options));
    }

    private static void ApplyConnection(JsonObject cs, string key, SqlConnectionParts parts, bool isUpdate)
    {
        if (isUpdate && string.IsNullOrEmpty(parts.Password))
        {
            // Mot de passe non ressaisi par l'utilisateur : préserver la valeur existante.
            parts.Password = SqlConnectionParts.Parse(GetString(cs, key)).Password;
        }

        cs[key] = parts.ToConnectionString();
    }

    private static string GenerateSecretKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
    }

    private static JsonObject GetOrCreateObject(JsonObject root, string key)
    {
        if (root[key] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        root[key] = created;
        return created;
    }

    private static string? GetString(JsonObject? obj, string key) =>
        obj is not null && obj.TryGetPropertyValue(key, out var node) ? node?.GetValue<string>() : null;

    private static int? GetInt(JsonObject? obj, string key) =>
        obj is not null && obj.TryGetPropertyValue(key, out var node) && node is not null ? node.GetValue<int>() : null;
}
