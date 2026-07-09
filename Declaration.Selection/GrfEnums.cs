namespace Declaration.Selection
{
    public static class GrfEnums
    {
        // Enums mappés depuis Tresorerie.Core.Enum (à vérifier sur GR_EMA_DISTRIBUTION)
        
        // MV_Domaine
        public const int Domaine_ReglementClient = 0;
        public const int Domaine_ReglementFournisseur = 1;
        public const int Domaine_Depense = 6;

        // MV_Impaye
        public const int Impaye_NonImpaye = 0;
        public const int Impaye_Impaye = 1;

        // MV_Compta
        public const int Compta_NonComptabilise = 0;
        public const int Compta_Comptabilise = 1;

        // Autres flags
        public const int Point_Oui = 1;
        public const int Annule_Non = 0;
        public const int Decaisse_Oui = 1;
        public const int Decaisse_Non = 0;
        public const int ModePaiement_Espece = 0;

        // Helper pour mapper un mode GRF vers le code Simpl-TVA.
        // Valeurs Simpl-TVA attendues: 1: Espèce, 2: Chèque, 3: Virement, 4: Effet, 5: Compensation, 6: Autres
        // Si la source est "Espece", ce sera toujours "1".
        public static string MapperModePaiementSimplTVA(int? modePaiementGrfId)
        {
            // TODO: Ajuster avec les vrais MP_Id de GRF.
            // Pour l'instant on utilise un fallback sur "3" (Virement) ou "6" (Autres).
            if (!modePaiementGrfId.HasValue)
                return "6"; // Autre par défaut

            return modePaiementGrfId.Value switch
            {
                0 => "1", // Espece (0) -> Simpl-TVA: 1
                1 => "2", // Cheque (1) -> Simpl-TVA: 2
                2 => "4", // Traite (2) -> Simpl-TVA: 4 (Effet)
                3 => "3", // Virement (3) -> Simpl-TVA: 3
                _ => "6"  // Autres (4) -> Simpl-TVA: 6
            };
        }
    }
}
