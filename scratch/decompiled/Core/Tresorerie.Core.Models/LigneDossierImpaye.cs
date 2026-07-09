using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LigneDossierImpaye
{
	public int No { get; set; }

	public int DossierImpayeNo { get; set; }

	public int EntityNo { get; set; }

	public TypeLigneDossierImp Type { get; set; }

	public decimal MontantAImputer { get; set; }

	public short Ordre { get; set; }

	public int UtilisateurNo { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DateModification { get; set; }

	public int ModificateurNo { get; set; }
}
