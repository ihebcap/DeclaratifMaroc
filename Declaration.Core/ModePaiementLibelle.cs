namespace Declaration.Core
{
    /// <summary>
    /// TASK-163 : libellé métier du code Simpl-TVA de mode de paiement, pour affichage Excel
    /// uniquement — n'affecte jamais le code envoyé dans le XML de dépôt légal DGI
    /// (Declaration.Export.Xml.DeclarationXmlExporter, inchangé).
    /// Réplique volontairement les valeurs de
    /// <c>Declaration.Selection.GrfEnums.MapperModePaiementSimplTVA</c> (1=Espèce, 2=Chèque,
    /// 3=Virement, 4=Effet, 5=Compensation, 6=Autres), seul producteur du code interprété — à
    /// resynchroniser ici si cette table est corrigée après vérification MV_Type (cf. réserve
    /// TASK-163).
    /// </summary>
    public static class ModePaiementLibelle
    {
        public static string LibelleModePaiementSimplTVA(string? code)
        {
            return code switch
            {
                "1" => "Espèce",
                "2" => "Chèque",
                "3" => "Virement",
                "4" => "Effet",
                "5" => "Compensation",
                "6" => "Autres",
                _ => code ?? ""
            };
        }
    }
}
