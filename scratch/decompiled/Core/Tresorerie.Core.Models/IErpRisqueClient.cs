namespace Tresorerie.Core.Models;

public interface IErpRisqueClient
{
	int ClientNo { get; }

	decimal Ecart { get; }

	bool IsBloque { get; }

	decimal MontantAssurance { get; }

	decimal PlafondAutorisee { get; }

	decimal TotalBonCommandes { get; }

	decimal TotalBonLivraisons { get; }

	decimal TotalFactures { get; }

	decimal TotalSoldeComptable { get; }
}
