using Microsoft.Win32;

namespace Declaration.Setup.Services;

public enum PrerequisiteStatus
{
    Present,
    Absent,
    Unknown
}

/// <summary>
/// Vérification des prérequis runtime (TASK-115 §Périmètre point 6) — traitement différencié :
/// - .NET Framework 4.8 (worker) : vérifiable de façon fiable (registre officiel Microsoft).
/// - Sage OM (composants COM Sage 100) : *pas* installable automatiquement (logiciel tiers sous
///   licence) et la détection COM fiable dépend d'un ProgID/CLSID précis par version que le PO
///   n'a pas fourni à ce jour (cf. TASK-115 §Risques : "ne pas s'arrêter à présent/absent"). On
///   ne devine pas cette clé de registre (règle CLAUDE.md "ne jamais improviser un contexte
///   manquant") : le résultat reste <see cref="PrerequisiteStatus.Unknown"/> et le formulaire
///   demande une confirmation manuelle explicite avant de continuer, plutôt qu'un faux "présent".
/// </summary>
public static class PrerequisiteChecker
{
    private const int Net48ReleaseKeyMinimum = 528040;

    public static PrerequisiteStatus CheckDotNetFramework48()
    {
        try
        {
            using var ndpKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\");
            var release = ndpKey?.GetValue("Release");
            if (release is int releaseValue)
            {
                return releaseValue >= Net48ReleaseKeyMinimum ? PrerequisiteStatus.Present : PrerequisiteStatus.Absent;
            }

            return PrerequisiteStatus.Absent;
        }
        catch (Exception)
        {
            return PrerequisiteStatus.Unknown;
        }
    }

    /// <summary>
    /// Best-effort : pas de ProgID Sage OM connu/confirmé à ce jour → toujours Unknown.
    /// Conservé comme point d'extension explicite pour le jour où le PO fournit l'identifiant réel.
    /// </summary>
    public static PrerequisiteStatus CheckSageOm() => PrerequisiteStatus.Unknown;
}
