namespace Declaration.Application.Constants;

/// <summary>
/// TASK-117/TASK-122 : subject ApLicence figé en dur (jamais lu depuis connections.json --
/// anti-contournement, décision PO TASK-002/GRLicence du 17/07/2026). Source unique de vérité
/// partagée entre Declaration.API (LicenceMonitor) et Declaration.Setup (SetupData,
/// pré-remplissage/écriture de connections.json) — cf. TASK-115, complément du 19/07/2026
/// éliminant la duplication de cette valeur entre les deux projets.
/// </summary>
public static class LicenceConstants
{
    public const string ApLicenceSubject = "/LIC/TRESO_GRF_COM";
}
