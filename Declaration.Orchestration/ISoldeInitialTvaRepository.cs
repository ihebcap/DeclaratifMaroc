using System.Collections.Generic;

namespace Declaration.Orchestration
{
    /// <summary>Saisie manuelle du comptable pour une ligne solde initial (EC_Type=4, cf. TASK-025).</summary>
    public class SaisieSoldeInitialTva
    {
        public decimal Taux { get; set; }
        public decimal MontantTva { get; set; }
    }

    /// <summary>
    /// TASK-025 (solde initial GRF, EC_Type=4) : persistance de la saisie manuelle taux + montant de
    /// TVA faite par le comptable. Le solde n'a aucun détail HT/TVA/taux côté Sage — le montant n'est
    /// connu qu'en TTC (RT_ECHEANCE.EC_Montant) — décision PO : intégrer ces lignes moyennant cette
    /// saisie plutôt que les éliminer silencieusement. Table DM_SOLDE_INITIAL_TVA, base de
    /// persistance (même base que DM_VENTILATION_SAGE_CACHE), jamais Sage.
    /// </summary>
    public interface ISoldeInitialTvaRepository
    {
        /// <summary>Une entrée par EC_Id ayant déjà une saisie ; absent = pas encore saisi.</summary>
        IReadOnlyDictionary<int, SaisieSoldeInitialTva> GetSaisiesBatch(int soId, IEnumerable<int> ecIds, string persistenceConnectionString);

        /// <summary>Upsert idempotent — la saisie peut être corrigée avant intégration définitive.</summary>
        void EnregistrerSaisie(int soId, int ecId, decimal taux, decimal montantTva, string saisiPar, string persistenceConnectionString);
    }
}
