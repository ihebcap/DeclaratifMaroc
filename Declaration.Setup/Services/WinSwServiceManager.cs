using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Declaration.Setup.Services;

/// <summary>
/// Orchestration du service Windows via WinSW-x64 (TASK-115 : remplace sc.exe brut — apporte la
/// configuration déclarative du redémarrage sur crash et la redirection des logs).
///
/// Nommage produit (pas module, cf. TASK-115 §Risques) : le service installé par cette classe
/// s'appelle toujours "DeclaratifMaroc" pour une installation neuve, jamais "DeclarationTVA".
/// Le renommage d'une installation existante sous l'ancien nom (sc.exe "DeclarationTVA", TASK-116)
/// est hors périmètre — signalé, pas traité ici (question distincte à cadrer avec le PO).
/// </summary>
public sealed class WinSwServiceManager
{
    public const string ServiceId = "DeclaratifMaroc";

    // Nom d'affichage (console des services Windows + XML WinSW <name>) uniquement — l'identité
    // SCM du service est ServiceId ci-dessus, jamais ce champ : le renommer n'affecte pas la
    // détection d'une installation existante ni la mise à jour (demande PO du 19/07/2026).
    public const string ServiceName = "APBS Déclaratif Maroc";

    // Page de code OEM réelle du poste (ex. 850 en France), interrogée dynamiquement plutôt que
    // codée en dur : la même build doit pouvoir décoder correctement la sortie de WinSW quel que
    // soit le poste client (bug d'encodage constaté en essai réel le 19/07/2026, cf. RunWinSw).
    private static readonly Lazy<Encoding> OemEncoding = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(GetOEMCP());
    });

    [DllImport("kernel32.dll")]
    private static extern int GetOEMCP();

    private readonly string _installFolder;
    private string WinSwExePath => Path.Combine(_installFolder, $"{ServiceId}.exe");
    private string WinSwXmlPath => Path.Combine(_installFolder, $"{ServiceId}.xml");

    public WinSwServiceManager(string installFolder)
    {
        _installFolder = installFolder;
    }

    /// <summary>
    /// Recopie WinSW.exe (vendorisé à côté de DeclaratifMaroc.exe, cf. WinSW\Get-WinSW.ps1) et
    /// le gabarit XML dans le dossier cible, sous le nom du service, avec les placeholders substitués.
    /// </summary>
    public void PrepareServiceFiles(string setupSourceFolder)
    {
        var sourceWinSwExe = Path.Combine(setupSourceFolder, "WinSW", "WinSW.exe");
        if (!File.Exists(sourceWinSwExe))
        {
            throw new FileNotFoundException(
                "WinSW.exe absent — exécuter WinSW\\Get-WinSW.ps1 avant de packager DeclaratifMaroc.",
                sourceWinSwExe);
        }

        File.Copy(sourceWinSwExe, WinSwExePath, overwrite: true);

        var templatePath = Path.Combine(setupSourceFolder, "WinSW", "DeclaratifMaroc.winsw.xml.template");
        var xml = File.ReadAllText(templatePath)
            .Replace("{{SERVICE_ID}}", ServiceId)
            .Replace("{{SERVICE_NAME}}", ServiceName);
        File.WriteAllText(WinSwXmlPath, xml);
    }

    public bool IsServiceRegistered() => File.Exists(WinSwXmlPath) && RunWinSw("status") is { ExitCode: 0 };

    public void Install() => RunWinSwChecked("install");

    public void Start() => RunWinSwChecked("start");

    public void Stop()
    {
        // WinSW renvoie un code non-zéro si le service est déjà arrêté ou non enregistré :
        // pas bloquant pour une première installation (rien à arrêter).
        RunWinSw("stop");
        WaitForApiProcessExit(TimeSpan.FromSeconds(45));
    }

    /// <summary>
    /// "WinSW stop" ne fait que transmettre ControlService() au SCM et attendre la fin de CE process
    /// CLI éphémère (process.WaitForExit() dans RunWinSw) — il ne vérifie jamais que
    /// Declaration.API.exe (le process réel, piloté par l'autre instance de WinSW qui tourne en tant
    /// que service) a effectivement terminé et libéré ses handles de fichiers. Sans cette attente
    /// active, une copie lancée juste après peut échouer par verrou de fichier de façon non
    /// déterministe (variable selon charge disque/antivirus/durée de l'arrêt gracieux ASP.NET Core,
    /// bug constaté en essai réel). Le RetryOnFileLock de SetupForm (15s) reste un filet de sécurité,
    /// mais ne doit plus être le seul mécanisme d'attente.
    /// </summary>
    private void WaitForApiProcessExit(TimeSpan timeout)
    {
        var exePath = Path.Combine(_installFolder, "Declaration.API.exe");
        var deadline = DateTime.UtcNow + timeout;

        while (Process.GetProcessesByName("Declaration.API").Any(p => TryGetMainModulePath(p) == exePath))
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"Declaration.API.exe ({exePath}) n'a pas terminé dans le délai de {timeout.TotalSeconds:F0}s " +
                    "après l'arrêt du service DeclaratifMaroc — arrêt forcé requis avant de poursuivre la mise à jour.");
            }

            Thread.Sleep(500);
        }
    }

    private static string? TryGetMainModulePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    public void Uninstall() => RunWinSwChecked("uninstall");

    private void RunWinSwChecked(string command)
    {
        var result = RunWinSw(command);
        if (result is null || result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"WinSW {command} a échoué (code {result?.ExitCode.ToString() ?? "inconnu"}).\n{result?.Output}");
        }
    }

    private WinSwResult? RunWinSw(string command)
    {
        // WinSW écrit sa sortie console dans la page de code OEM du poste (850 en France), pas en
        // UTF-8/1252 : sans encodage explicite ici, .NET décode mal les caractères accentués des
        // messages d'erreur SCM remontés à l'utilisateur ("spécifié" -> "sp,cifi,", bug constaté en
        // essai réel le 19/07/2026).
        var oemEncoding = OemEncoding.Value;

        var psi = new ProcessStartInfo(WinSwExePath, command)
        {
            WorkingDirectory = _installFolder,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = oemEncoding,
            StandardErrorEncoding = oemEncoding,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new WinSwResult(process.ExitCode, output);
    }

    private sealed record WinSwResult(int ExitCode, string Output);
}
