using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IMouvementCaisseRepository
{
	IEnumerable<HistoriqueMouvementCaisse> GetAllMouvement(int[] caissesNo, int deviseNo);

	IEnumerable<SoldeCaisse> GetAllSolde(int caisseNo, int deviseNo);

	IEnumerable<SoldeCaisse> GetAllSoldeEspece(int caisseNo, int deviseNo);

	IEnumerable<LotsSoldeCaisse> GetLotsCaisse(int caisseNo, int modeNo, int deviseNo);

	bool IsCaisseVide(int caisseNo);
}
