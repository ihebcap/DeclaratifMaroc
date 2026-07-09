using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LigneDossierReglementComm
{
	public int No { get; set; }

	public int Ordre { get; set; }

	public int SocieteNo { get; set; }

	public int DossierNo { get; set; }

	public int EntityNo { get; set; }

	public TypeLigneDossier TypeLigneDossier { get; set; }

	public bool IsRetenue { get; set; }

	public decimal EntitySolde { get; set; }

	public decimal MontantAPaye { get; set; }

	public decimal MontantFacturationMois { get; set; }

	public DateTime DateCreation { get; set; }

	public int CreateurNo { get; set; }
}
