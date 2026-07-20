using Declaration.Application.Constants;

namespace Declaration.Setup.Models;

/// <summary>
/// Valeurs saisies/pré-remplies dans le formulaire de setup. Les champs secrets (mots de passe
/// SQL, JWT) sont *toujours* vides à l'extraction en mode mise à jour (TASK-115) : ce n'est qu'à
/// l'écriture (<see cref="Services.ConnectionsFileService.ApplyChanges"/>) qu'une valeur vide est
/// traitée comme "non modifié, conserver l'existant".
/// La connexion Sage n'est plus saisie ici (TASK-119) : elle est résolue dynamiquement par
/// <c>SO_Id</c> depuis <c>P_SOCIETE</c> à l'exécution (TASK-118), plus jamais une valeur globale
/// unique écrite dans <c>connections.json</c>.
/// Depuis TASK-122, <see cref="Grf"/> et <see cref="Persistence"/> sont saisies une seule fois côté
/// formulaire (une seule base physique sert les deux usages) puis dupliquées à la lecture — les
/// deux propriétés restent distinctes ici pour ne rien changer côté <c>connections.json</c>/
/// <see cref="Services.ConnectionsFileService"/> (toujours deux clés).
/// </summary>
public sealed class SetupData
{
    public string InstallFolder { get; set; } = "";

    public SqlConnectionParts Grf { get; set; } = new();
    public SqlConnectionParts Persistence { get; set; } = new();

    public string JwtSecretKey { get; set; } = "";

    public int Port { get; set; } = 5000;

    public SageVersion SageVersion { get; set; } = SageVersion.V10;

    /// <summary>
    /// Fixé (TASK-122) : constante produit pour Déclaratif Maroc, plus un champ de saisie
    /// utilisateur — jamais lu depuis un <c>connections.json</c> existant (cf.
    /// <see cref="Services.ConnectionsFileService.ExtractForPrefill"/>), toujours réécrit à
    /// l'identique lors d'une installation ou mise à jour. Source partagée avec Declaration.API
    /// (<see cref="LicenceConstants.ApLicenceSubject"/>) — TASK-115, complément du 19/07/2026.
    /// </summary>
    public string ApLicenceSubject { get; set; } = LicenceConstants.ApLicenceSubject;
    public string ApLicenceServerAddress { get; set; } = "127.0.0.1";
    public int ApLicenceServerPort { get; set; } = 8003;
}
