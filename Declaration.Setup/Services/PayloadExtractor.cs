using System.IO.Compression;
using System.Linq;

namespace Declaration.Setup.Services;

/// <summary>
/// Résout le dossier source des binaires à installer (API/front/workers/WinSW). En publication
/// "installeur unique" (payload.zip embarqué en ressource par Deploy-All.ps1), extrait ce zip
/// vers un dossier temporaire à la demande. En dev/debug (pas de zip embarqué), retombe sur le
/// dossier de l'exe (comportement historique inchangé — permet de continuer à tester le GUI
/// directement depuis bin\ sans repasser par tout le pipeline de déploiement).
/// </summary>
public static class PayloadExtractor
{
    private const string EmbeddedResourceName = "Declaration.Setup.payload.zip";

    private static bool HasEmbeddedPayload =>
        typeof(PayloadExtractor).Assembly.GetManifestResourceNames().Contains(EmbeddedResourceName);

    /// <summary>
    /// Retourne le dossier source à utiliser pour <see cref="DeploymentCopier"/> et
    /// <see cref="WinSwServiceManager.PrepareServiceFiles"/>. Si un payload est embarqué, extrait
    /// et retourne un dossier temporaire à nettoyer ensuite via <paramref name="cleanupPath"/> (out).
    /// </summary>
    public static string Resolve(out string? cleanupPath)
    {
        if (!HasEmbeddedPayload)
        {
            cleanupPath = null;
            return AppContext.BaseDirectory;
        }

        var assembly = typeof(PayloadExtractor).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException("Payload annoncé mais introuvable dans l'assembly.");

        var extractPath = Path.Combine(Path.GetTempPath(), "DeclaratifMaroc-Setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(extractPath);

        cleanupPath = extractPath;
        return extractPath;
    }
}
