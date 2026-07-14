using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Core.Model;

namespace Declaration.Selection
{
    public interface ISelectionExpliqueeService
    {
        Task<IEnumerable<AffectationCandidate>> SelectionnerExpliqueeAsync(
            int soId, 
            DateTime dateDebut, 
            DateTime dateFin, 
            string connectionString,
            Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>>? verifierFacture = null);

        /// <summary>
        /// TASK-050 — Valorisation facture-first : lit TOUTES les factures EC_Type=0 de la période
        /// depuis RT_ECHEANCE (axe date facture DO_Date), indépendamment du statut de règlement.
        /// Le statut de règlement (MV_Point) et de déclaration (DT_Id) est rattaché en 2ᵉ temps
        /// via LEFT JOIN RT_AFFECTATION / RT_MOUVEMENT.
        /// Retourne des AffectationCandidate avec :
        ///   - Motif = Eligible si rapproché non déclaré dans la période
        ///   - Motif = NonRapproche si affecté mais non rapproché (EstValorisable = true, non déclarable)
        ///   - Motif = HorsPeriode / DejaDeclare / etc. pour les cas exclus
        /// Garde-fou : aucune écriture — lecture seule stricte.
        /// </summary>
        Task<IEnumerable<AffectationCandidate>> LireFacturesDepuisPeriodeAsync(
            int soId,
            DateTime dateDebut,
            DateTime dateFin,
            string connectionString);
    }
}
