namespace Declaration.Setup.Services;

/// <summary>
/// Copie le contenu du dossier de déploiement (celui où réside DeclaratifMaroc.exe, packagé
/// aux côtés de l'API/worker/front — TASK-115 §Périmètre point 4) vers le dossier cible.
///
/// Exclusions volontaires :
/// - connections.json : jamais copié tel quel, toujours géré par <see cref="ConnectionsFileService"/>
///   (préservation explicite en mode mise à jour, sauf champs modifiés).
/// - DeclaratifMaroc.* (ex-Declaration.Setup.*, TASK-119) : l'installateur ne s'installe pas
///   lui-même dans le dossier cible.
/// - WinSW\ (dossier source de vendoring) : le service WinSW final est généré directement dans
///   le dossier cible par <see cref="WinSwServiceManager.PrepareServiceFiles"/>, pas copié tel quel.
///
/// Copie non atomique corrigée (TASK-115, complément du 19/07/2026) : un échec en cours de copie
/// (verrou, disque plein, coupure...) laissait auparavant le dossier cible dans un état mixte
/// ancien/nouveau incohérent (bug constaté en essai réel : apphost .NET 10 mêlé à des DLL runtime
/// .NET 8 non remplacées). La copie se fait maintenant intégralement vers un dossier de staging
/// temporaire (<see cref="StagingDirName"/>, à l'intérieur du dossier cible) ; ce n'est qu'une fois
/// TOUS les fichiers copiés avec succès que le contenu du staging est déplacé (File.Move, pas une
/// nouvelle copie) vers le dossier cible. En cas d'échec pendant le staging, le dossier cible n'a
/// reçu aucune modification de ses fichiers applicatifs — seul le sous-dossier de staging (nettoyé
/// dans tous les cas, y compris en échec) a été écrit.
/// </summary>
public static class DeploymentCopier
{
    private static readonly string[] ExcludedTopLevelEntries = { "WinSW", "connections.json" };

    private const string StagingDirName = "_update_staging";

    public static void CopyBinaries(string sourceFolder, string targetFolder)
    {
        Directory.CreateDirectory(targetFolder);

        var stagingFolder = Path.Combine(targetFolder, StagingDirName);
        if (Directory.Exists(stagingFolder))
        {
            Directory.Delete(stagingFolder, recursive: true);
        }

        Directory.CreateDirectory(stagingFolder);

        try
        {
            CopyToStaging(sourceFolder, stagingFolder);
            PromoteDirectory(stagingFolder, targetFolder);
        }
        finally
        {
            if (Directory.Exists(stagingFolder))
            {
                Directory.Delete(stagingFolder, recursive: true);
            }
        }

        Directory.CreateDirectory(Path.Combine(targetFolder, "logs"));
    }

    private static void CopyToStaging(string sourceFolder, string stagingFolder)
    {
        foreach (var sourceFile in Directory.EnumerateFiles(sourceFolder))
        {
            var fileName = Path.GetFileName(sourceFile);
            if (ExcludedTopLevelEntries.Contains(fileName, StringComparer.OrdinalIgnoreCase) ||
                fileName.StartsWith("DeclaratifMaroc", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(sourceFile, Path.Combine(stagingFolder, fileName), overwrite: true);
        }

        foreach (var sourceDir in Directory.EnumerateDirectories(sourceFolder))
        {
            var dirName = Path.GetFileName(sourceDir);
            if (ExcludedTopLevelEntries.Contains(dirName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyDirectoryRecursive(sourceDir, Path.Combine(stagingFolder, dirName));
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            CopyDirectoryRecursive(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }

    /// <summary>
    /// Bascule le contenu déjà entièrement copié (staging) vers la cible réelle par déplacement
    /// (rename), pas par une nouvelle copie octet à octet — quasi-instantané sur un même volume,
    /// donc une fenêtre d'incohérence minimale par rapport à une copie complète. Fusionne avec
    /// l'existant plutôt que de vider <paramref name="targetDir"/> au préalable : la cible contient
    /// aussi des fichiers hors périmètre de cette copie (connections.json, DeclaratifMaroc.*,
    /// dossier logs\, fichiers WinSW générés) qui doivent survivre intacts.
    /// </summary>
    private static void PromoteDirectory(string stagingDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var stagedFile in Directory.EnumerateFiles(stagingDir))
        {
            var destFile = Path.Combine(targetDir, Path.GetFileName(stagedFile));
            if (File.Exists(destFile))
            {
                File.Delete(destFile);
            }

            File.Move(stagedFile, destFile);
        }

        foreach (var stagedDir in Directory.EnumerateDirectories(stagingDir))
        {
            PromoteDirectory(stagedDir, Path.Combine(targetDir, Path.GetFileName(stagedDir)));
        }
    }
}
