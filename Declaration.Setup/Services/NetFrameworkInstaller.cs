namespace Declaration.Setup.Services;

/// <summary>
/// .NET Framework 4.8 (requis par le worker net48 SageTaxReader.Console) : contrairement à Sage OM,
/// c'est un redistribuable Microsoft officiel installable silencieusement (TASK-115 §Périmètre
/// point 6). Nécessite les droits admin déjà requis pour l'installation du service, et peut exiger
/// un redémarrage Windows — l'appelant DOIT en informer explicitement l'utilisateur (pas une
/// installation "automatique et invisible", cf. task).
/// </summary>
public static class NetFrameworkInstaller
{
    // URL officielle et stable de l'installateur offline .NET Framework 4.8, hébergée sur le CDN
    // download.visualstudio.microsoft.com (contrairement aux liens download.microsoft.com/download/.../<GUID>
    // qui sont retirés/déplacés au fil du temps — cf. TASK-153, ancien lien constaté en 404).
    // Vérifiée manuellement le 2026-07-20 : HTTP 200, ~72 Mo, Content-Type application/octet-stream.
    private const string OfficialInstallerUrl =
        "https://download.visualstudio.microsoft.com/download/pr/7afca223-55d2-470a-8edc-6a1739ae3252/abd170b4b0ec15ad0222a809b761a036/ndp48-x86-x64-allos-enu.exe";

    public static async Task<bool> DownloadAndInstallSilentlyAsync(IProgress<string>? progress = null)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "NDP48-x86-x64-AllOS-ENU.exe");

        progress?.Report("Téléchargement du redistribuable .NET Framework 4.8 (Microsoft)...");
        using (var http = new HttpClient())
        using (var response = await http.GetAsync(OfficialInstallerUrl))
        {
            response.EnsureSuccessStatusCode();
            await using var fileStream = File.Create(tempFile);
            await response.Content.CopyToAsync(fileStream);
        }

        progress?.Report("Installation silencieuse en cours (peut prendre plusieurs minutes)...");
        var psi = new System.Diagnostics.ProcessStartInfo(tempFile, "/q /norestart")
        {
            UseShellExecute = true,
            Verb = "runas"
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync();
        // Codes 0 (succès) et 3010 (succès, redémarrage requis) sont tous deux des installations réussies.
        return process.ExitCode is 0 or 3010;
    }
}
