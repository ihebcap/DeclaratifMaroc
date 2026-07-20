namespace Declaration.Setup.Services;

/// <summary>
/// Détection install vs mise à jour (TASK-115 §Risques : "point le plus sensible du projet").
///
/// Signal retenu et documenté avant codage : la présence de connections.json dans le dossier
/// cible. Justification — c'est déjà le seul fichier de configuration applicative du produit
/// (une installation existante en a nécessairement un, une installation neuve dans un dossier
/// vide n'en a pas) ; il est indépendant du nom du service Windows enregistré (qui va justement
/// changer, cf. §Risques renommage DeclarationTVA → DeclaratifMaroc — interroger le SCM par nom
/// donnerait un faux "install neuve" sur les postes déjà en prod sous l'ancien nom). Le dossier
/// cible est toujours saisi/confirmé explicitement par l'utilisateur dans le formulaire ; aucune
/// détection ne se fait à l'aveugle sur un chemin deviné.
/// </summary>
public static class InstallDetector
{
    public static bool IsExistingInstall(string targetFolder) =>
        !string.IsNullOrWhiteSpace(targetFolder) && ConnectionsFileService.Exists(targetFolder);
}
