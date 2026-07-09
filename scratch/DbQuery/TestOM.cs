using System;
using System.Runtime.InteropServices;
using Objets100cLib;

namespace SageTaxReader.ConsoleApp
{
    class Program2
    {
        [STAThread]
        static void Main()
        {
            var app = new BSCIALApplication100c();
            app.Name = "";
            app.CompanyServer = "IHEB-PC\\SQL2022";
            app.CompanyDatabaseName = "DISTRI_DEMO";
            app.Loggable.UserName = "<Administrateur>";
            app.Loggable.UserPwd = "bijou";
            app.Open();

            try {
                var factory = app.CptaApplication.FactoryDossier;
                Console.WriteLine($"FactoryDossier is a {factory.GetType()}");
                var list = factory.List;
                Console.WriteLine($"List count: {list.Count}");
                if (list.Count > 0) {
                    var dossier = (IBPDossier2)list[1];
                    Console.WriteLine($"Dossier: {dossier.D_RaisonSoc}");
                    var devise = dossier.DeviseCompte;
                    Console.WriteLine($"Devise: {devise.D_Intitule}, format: {devise.D_Format}");
                }
            } catch (Exception ex) {
                Console.WriteLine($"Err: {ex}");
            }
            
            app.Close();
            Marshal.ReleaseComObject(app);
        }
    }
}
