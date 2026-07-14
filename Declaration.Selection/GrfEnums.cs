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

        // RT_ECHEANCE.EC_Type — origine de l'échéance (discriminant de valorisation).
        // LISTE BLANCHE : on ne valorise QUE ces trois types (vraies factures / solde).
        // Tous les autres (1 IMP, 3 RN, 90 G, 91 P, 100 GE, 101 PE, 102 VM, 103-108 remb.,
        // 109 CI, 110 II…) ne sont pas des factures à lire → exclus de la sélection.
        public const int EcType_FactureErp = 0;   // FC — Facture Erp (TVA via OM, worker Sage)
        public const int EcType_Solde = 4;         // S  — Solde
        public const int EcType_Fgr = 111;         // FGR — Facture GR (détail RT_HISTOCOMPTA, SQL)

        // Vrai si l'échéance est une facture/solde à valoriser (liste blanche 0/4/111).
        // Tout autre EC_Type (impayé, gain, remboursement, change…) n'est pas une facture à lire.
        public static bool EstEcTypeFacture(int ecType) =>
            ecType == EcType_FactureErp || ecType == EcType_Solde || ecType == EcType_Fgr;

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
