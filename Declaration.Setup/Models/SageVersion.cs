namespace Declaration.Setup.Models;

/// <summary>
/// Versions Sage 100 dont un worker net48 dédié existe dans deploy\workers\ (TASK-101).
/// V8 volontairement absente : DLL interop v8 non fournie par le PO à ce jour (TASK-115 §Risques).
/// </summary>
public enum SageVersion
{
    V7,
    V9,
    V10,
    V12
}

public static class SageVersionExtensions
{
    /// <summary>Chemin relatif (depuis le dossier d'installation) de l'exécutable worker correspondant.</summary>
    public static string WorkerRelativePath(this SageVersion version) => version switch
    {
        SageVersion.V7 => @"workers\v7\SageTaxReader.Console.v7.exe",
        SageVersion.V9 => @"workers\v9\SageTaxReader.Console.v9.exe",
        SageVersion.V10 => @"workers\v10\SageTaxReader.Console.v10.exe",
        SageVersion.V12 => @"workers\v12\SageTaxReader.Console.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    public static string DisplayLabel(this SageVersion version) => version switch
    {
        SageVersion.V7 => "Sage 100 v7",
        SageVersion.V9 => "Sage 100 v9",
        SageVersion.V10 => "Sage 100 v10 / v11",
        SageVersion.V12 => "Sage 100 v12",
        _ => version.ToString()
    };

    public static SageVersion? FromWorkerRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var version in Enum.GetValues<SageVersion>())
        {
            if (path.EndsWith(version.WorkerRelativePath(), StringComparison.OrdinalIgnoreCase))
            {
                return version;
            }
        }

        return null;
    }
}
