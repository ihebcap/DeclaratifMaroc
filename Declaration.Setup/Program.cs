namespace Declaration.Setup;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Qualification complète nécessaire : depuis la référence à Declaration.Application
        // (LicenceConstants), le nom "Application" non qualifié résout vers l'espace de noms
        // Declaration.Application (sibling de Declaration.Setup) plutôt que vers la classe
        // System.Windows.Forms.Application (using global implicite de UseWindowsForms).
        System.Windows.Forms.Application.Run(new SetupForm());
    }
}
