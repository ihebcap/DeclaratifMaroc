using System.Threading.Tasks;

namespace Declaration.Application.Interfaces;

/// <summary>
/// TASK-127 (socle Délai de Paiement Maroc) : lecture SEULE du paramétrage société relatif au délai
/// de paiement (<c>P_SOCIETE.SO_NbJoursDelaiPaiement</c>, filtré par <c>SO_Id</c>). Table déjà
/// existante, possédée par <c>apbs-gr_winform</c> — AUCUNE modification de schéma.
/// </summary>
public interface IDelaiPaiementParametrageRepository
{
    /// <summary>
    /// Retourne le délai de paiement par défaut (en jours) de la société <paramref name="societeId"/>
    /// (<c>SO_NbJoursDelaiPaiement</c>). Repli sur 60 (amorçage legacy) si la valeur est NULL.
    /// </summary>
    Task<int> GetNombreJoursDelaiDefautAsync(int societeId);
}
